using System;
using System.Collections.Generic;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Holds input to what the selected hardware profile can track. Input is first reduced to the device's own controls
    /// (Project), manual and held values are applied to those controls by the parameter store, and the controls are then
    /// expanded back to tracking keys (Expand), so keys that share one control always move together. Validate runs last,
    /// after every override, and clears any key the device does not drive.
    /// </summary>
    internal sealed class FTEmulatorHardwareFilter
    {
        internal readonly FTEmulatorHardwareProfile Profile;
        internal readonly FTEmulatorBehavior Behavior;
        /// <summary>Per tracking key: the device drives it under this behavior.</summary>
        internal readonly bool[] Allowed;
        /// <summary>Per tracking key: the output that drives it, or null.</summary>
        private readonly FTEmulatorHardwareOutput[] outputs;
        private readonly string[] keys;
        private readonly bool[] eyeKey;

        internal FTEmulatorHardwareFilter(FTEmulatorCatalog catalog, FTEmulatorHardwareProfile profile, FTEmulatorBehavior behavior)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Behavior = behavior;
            keys = catalog.Keys;
            eyeKey = catalog.EyeKey;
            Allowed = new bool[keys.Length];
            outputs = new FTEmulatorHardwareOutput[keys.Length];
            foreach (FTEmulatorHardwareOutput output in profile.Outputs)
            {
                if (output.Derived && behavior != FTEmulatorBehavior.ModuleBehavior) continue;
                outputs[output.Key] = output;
                Allowed[output.Key] = true;
            }
        }

        /// <summary>
        /// Reduces a frame to the profile's controls. available[i] is false where the control cannot be worked out: it has
        /// no rule, or the source did not observe a key the rule reads.
        /// </summary>
        internal void Project(FTEmulatorExpressionFrame frame, float[] controls, bool[] available)
        {
            for (int i = 0; i < Profile.Controls.Length; i++)
            {
                FTEmulatorHardwareControl control = Profile.Controls[i];
                available[i] = false;
                if (control.From == null) continue;
                bool observed = true;
                foreach (int key in control.From.Dependencies)
                {
                    if (key >= frame.Valid.Length || !frame.Valid[key]) { observed = false; break; }
                }
                if (!observed) continue;
                controls[i] = Math.Min(Math.Max(control.From.Evaluate(frame.Values), control.Min), control.Max);
                available[i] = true;
            }
        }

        /// <summary>
        /// Expands controls to tracking keys. A key is driven when every control its output reads is driven; a key that is
        /// not driven is left at 0 and FTEmulator does not write the outputs that need it.
        /// </summary>
        internal void Expand(float[] controls, bool[] driven, float[] values, bool[] valueDriven)
        {
            for (int key = 0; key < keys.Length; key++)
            {
                values[key] = 0f;
                valueDriven[key] = false;
                FTEmulatorHardwareOutput output = outputs[key];
                if (output == null) continue;
                bool ready = true;
                foreach (int control in output.Value.Dependencies)
                {
                    if (!driven[control]) { ready = false; break; }
                }
                if (!ready) continue;
                values[key] = Math.Min(Math.Max(output.Value.Evaluate(controls), 0f), 1f);
                valueDriven[key] = true;
            }
        }

        /// <summary>
        /// The last check, after every override: a key the device does not drive is cleared whatever put a value there.
        /// Such a key then reads as a known zero when the device tracks that region (a headset without a tongue sensor
        /// reports no tongue, so a combined parameter still gets its value), and as not driven when the device has no
        /// tracking for the region at all. Returns how many values were cleared.
        /// </summary>
        internal int Validate(float[] values, bool[] valueDriven)
        {
            int cleared = 0;
            for (int key = 0; key < keys.Length; key++)
            {
                if (Allowed[key]) continue;
                if (values[key] != 0f) cleared++;
                values[key] = 0f;
                valueDriven[key] = eyeKey[key] ? Profile.EyeTracking : Profile.FaceTracking;
            }
            return cleared;
        }

        /// <summary>The controls a key's value comes from (empty for a key that is not allowed).</summary>
        internal int[] ControlsOf(int key)
        {
            return outputs[key] != null ? outputs[key].Value.Dependencies : new int[0];
        }

        /// <summary>The keys a control drives under this behavior.</summary>
        internal List<int> KeysOf(int control)
        {
            var result = new List<int>();
            for (int key = 0; key < keys.Length; key++)
            {
                if (outputs[key] != null && Array.IndexOf(outputs[key].Value.Dependencies, control) >= 0) result.Add(key);
            }
            return result;
        }

        internal bool IsDerived(int key)
        {
            return outputs[key] != null && outputs[key].Derived;
        }

        internal string NoteOf(int key)
        {
            return outputs[key] != null ? outputs[key].Note : "";
        }

        /// <summary>Why a key is not allowed, for the debug window; null when it is.</summary>
        internal string BlockedReason(int key)
        {
            if (Allowed[key]) return null;
            foreach (FTEmulatorHardwareOutput output in Profile.Outputs)
            {
                if (output.Key == key && output.Derived) return "Blocked: " + Profile.Name + " only derives it (Documented module behavior)";
            }
            return "Blocked: " + Profile.Name + " has no " + keys[key] + " channel";
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator hardware filter
// ============================================================================
//
// FTEmulatorHardwareFilter (this file)
//   Profile / Behavior       The hardware profile and Strict or ModuleBehavior (Derived outputs count only with the latter).
//   Allowed                  Per tracking key: the device drives it.
//   Project(frame, controls, available)  Input reduced to the device's controls, each held to its range; not available
//                            where the control has no rule or the source did not observe a key the rule reads.
//   Expand(controls, driven, values, valueDriven)  Controls back to tracking keys (0..1); a key is driven only when every
//                            control it reads is.
//   Validate(values, valueDriven)  Clears every key that is not Allowed; the count of values it cleared. A cleared key
//                            counts as driven (a known zero) when the profile tracks its region, else as not driven.
//   ControlsOf(key) / KeysOf(control)  Which controls a key reads, and which keys a control drives (the linked rows).
//   IsDerived(key) / NoteOf(key) / BlockedReason(key)  For the debug window.
