using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Where tracking values come from.</summary>
    internal enum FTEmulatorInput
    {
        /// <summary>VRCFaceTracking sends OSC to FTEmulator's receiver.</summary>
        VrcftOsc,
        /// <summary>No source: only the values typed in the debug window.</summary>
        Manual
    }

    /// <summary>The FTEmulator window's mode tab. Playback and OSC use the VRCFT OSC input; Manual the Manual input.</summary>
    internal enum FTEmulatorMode
    {
        /// <summary>A companion recording played through the OSC receiver.</summary>
        Playback,
        /// <summary>VRCFaceTracking sends live OSC to the receiver.</summary>
        Osc,
        /// <summary>Only the values typed in the debug window.</summary>
        Manual
    }

    /// <summary>How a hardware profile's channels reach Unified Expressions.</summary>
    internal enum FTEmulatorBehavior
    {
        /// <summary>Only what the sensors measure.</summary>
        Strict,
        /// <summary>Also the couplings the device's VRCFT module adds (marked Derived).</summary>
        ModuleBehavior
    }

    internal enum FTEmulatorValueType { Float, Int, Bool }

    /// <summary>Who decides a row's value.</summary>
    internal enum FTEmulatorRowMode
    {
        /// <summary>The source's latest value.</summary>
        Live,
        /// <summary>The value it had when it was held.</summary>
        Held,
        /// <summary>The value typed by the user.</summary>
        Manual,
        /// <summary>Not driven: FTEmulator leaves the outputs that need it alone.</summary>
        Disabled
    }

    internal enum FTEmulatorState
    {
        /// <summary>No session.</summary>
        Stopped,
        /// <summary>Input is applied.</summary>
        Live,
        /// <summary>Active listening is off: the source is closed and the last values are held.</summary>
        ListeningOff,
        /// <summary>Freeze output: the source runs, the outputs are held.</summary>
        Frozen,
        /// <summary>Unity is paused: nothing is applied and arriving input is dropped.</summary>
        Paused
    }

    /// <summary>One typed avatar parameter value, with its exact, case-sensitive name.</summary>
    internal readonly struct FTEmulatorParameterValue
    {
        internal readonly string Name;
        internal readonly FTEmulatorValueType Type;
        /// <summary>Float as it is, Int as a whole number, Bool as 0 or 1.</summary>
        internal readonly float Value;

        internal FTEmulatorParameterValue(string name, FTEmulatorValueType type, float value)
        {
            Name = name;
            Type = type;
            Value = value;
        }
    }

    /// <summary>A parameter an avatar has: its exact name, type and whether it is synced.</summary>
    internal sealed class FTEmulatorAvatarParameter
    {
        internal string Name;
        internal FTEmulatorValueType Type;
        internal bool Synced;
    }

    /// <summary>
    /// Unified Expression strengths from a source, in the catalog's key order. Not changed after it is made.
    /// Valid says which keys the source observed: a key that is not valid is unknown, not zero.
    /// </summary>
    internal sealed class FTEmulatorExpressionFrame
    {
        internal readonly float[] Values;
        internal readonly bool[] Valid;
        internal readonly long Sequence;
        internal readonly int Generation;
        internal readonly double Time;

        private FTEmulatorExpressionFrame(float[] values, bool[] valid, long sequence, int generation, double time)
        {
            Values = values;
            Valid = valid;
            Sequence = sequence;
            Generation = generation;
            Time = time;
        }

        /// <summary>Null when a value is NaN or infinite, or the two arrays differ in length. The arrays are copied.</summary>
        internal static FTEmulatorExpressionFrame Create(float[] values, bool[] valid, long sequence, int generation, double time)
        {
            if (values == null || valid == null || values.Length != valid.Length) return null;
            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) return null;
            }
            return new FTEmulatorExpressionFrame((float[])values.Clone(), (bool[])valid.Clone(), sequence, generation, time);
        }
    }

    /// <summary>Avatar parameter values that arrived already mapped (VRCFT's /avatar/parameters/... messages), newest per name.</summary>
    internal sealed class FTEmulatorParameterFrame
    {
        internal readonly IReadOnlyList<FTEmulatorParameterValue> Values;
        internal readonly long Sequence;
        internal readonly int Generation;
        internal readonly double Time;

        internal FTEmulatorParameterFrame(IReadOnlyList<FTEmulatorParameterValue> values, long sequence, int generation, double time)
        {
            Values = values;
            Sequence = sequence;
            Generation = generation;
            Time = time;
        }
    }

    /// <summary>What a source is started with: the session it belongs to and whether that session may still run.</summary>
    internal sealed class FTEmulatorSessionContext
    {
        /// <summary>Counts sessions. A frame from another generation is dropped.</summary>
        internal readonly int Generation;
        /// <summary>True while Unity is in Play Mode and this generation is the current one.</summary>
        internal readonly Func<bool> Active;
        internal readonly CancellationToken Cancel;

        internal FTEmulatorSessionContext(int generation, Func<bool> active, CancellationToken cancel)
        {
            Generation = generation;
            Active = active;
            Cancel = cancel;
        }
    }

    /// <summary>A source of tracking input. Start refuses when the session is not active (outside Play Mode).</summary>
    internal interface IFTEmulatorSource : IDisposable
    {
        string Name { get; }
        bool Running { get; }
        void Start(FTEmulatorSessionContext context);
        /// <summary>The newest input since the last read; either may be null. False when there is nothing new.</summary>
        bool TryRead(out FTEmulatorExpressionFrame expressions, out FTEmulatorParameterFrame parameters);
        /// <summary>Drops what was received and not read.</summary>
        void Clear();
        void Stop();
    }

    /// <summary>Where avatar parameter values are written: Gesture Manager's active module.</summary>
    internal interface IFTEmulatorOutput
    {
        string Name { get; }
        /// <summary>Binds to the manager object. False, with the reason, when it has no active avatar.</summary>
        bool Bind(UnityEngine.Object manager, out string problem);
        /// <summary>False once the bound module or avatar was replaced: bind again.</summary>
        bool Valid { get; }
        GameObject Avatar { get; }
        /// <summary>The parameters the output can write, as it has them now.</summary>
        IReadOnlyList<FTEmulatorAvatarParameter> Parameters { get; }
        /// <summary>False when the output has no such parameter.</summary>
        bool Write(string name, float value);
        bool TryRead(string name, out float value);
        void Release();
    }

    /// <summary>Where native face tracking's blendshape weights are written, after animation.</summary>
    internal interface IFTEmulatorMeshOutput
    {
        /// <summary>Finds the blendshapes by name on the avatar's face tracking meshes. Returns the names found on none.</summary>
        List<string> Bind(string[] blendshapes);
        /// <summary>Writes the owned weights (0..100).</summary>
        void Apply(float[] weights, bool[] owned);
        /// <summary>Puts back the weights captured at Bind, except where something else changed them since.</summary>
        void Restore();
    }

    /// <summary>One clock for sources and the session: seconds since the first use, not affected by Unity's time scale or pause.</summary>
    internal static class FTEmulatorClock
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        internal static double Now
        {
            get { return Watch.Elapsed.TotalSeconds; }
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator shared types
// ============================================================================
//
// FTEmulatorInput              VrcftOsc (VRCFaceTracking over OSC) or Manual (debug window values only).
// FTEmulatorBehavior           Strict (sensor channels only) or ModuleBehavior (also the module's Derived couplings).
// FTEmulatorValueType          Float, Int, Bool: an avatar parameter's type, kept as it arrived.
// FTEmulatorRowMode            Live, Held, Manual, Disabled: who decides a debug row's value.
// FTEmulatorState              Stopped, Live, ListeningOff, Frozen, Paused.
// FTEmulatorParameterValue     Name (exact), Type, Value.
// FTEmulatorAvatarParameter    Name, Type, Synced: a parameter the avatar has.
// FTEmulatorExpressionFrame    Values and Valid per tracking key, Sequence, Generation, Time. Create(...) copies the arrays and
//                              gives null for NaN, infinity or arrays of different lengths.
// FTEmulatorParameterFrame     Already mapped parameter values, newest per name, with Sequence, Generation, Time.
// FTEmulatorSessionContext     Generation, Active (Play Mode and still the current session), Cancel.
// IFTEmulatorSource            Start(context), TryRead(out expressions, out parameters), Clear(), Stop(), Dispose().
// IFTEmulatorOutput            Bind(manager), Valid, Avatar, Parameters, Write(name, value), TryRead(name), Release().
//                              Implemented by FTEmulatorGestureManager (com.riry.ftemulator/Editor/FTEmulator/GestureManager/).
// IFTEmulatorMeshOutput        Bind(blendshapes), Apply(weights, owned), Restore(). Implemented by FTEmulatorBlendshapeWriter.
// FTEmulatorClock.Now          Seconds on a Stopwatch shared by sources and the session.
