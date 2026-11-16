using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Riry.RiryTools.Editor
{
    /// <summary>What a session runs with: the user's choices, copied when it starts and changed through the session's commands.</summary>
    internal sealed class FTEmulatorSessionOptions
    {
        internal FTEmulatorInput Input = FTEmulatorInput.VrcftOsc;
        internal string HardwareId = FTEmulatorCatalog.UnrestrictedId;
        internal FTEmulatorBehavior Behavior = FTEmulatorBehavior.Strict;
        /// <summary>FTEmulatorTemplateResolver.Auto, NativeChoice, or an avatar profile's id.</summary>
        internal string Backend = FTEmulatorTemplateResolver.Auto;
        internal int Port = 9000;
        internal bool Discovery = true;
        /// <summary>With the Manual input: write the typed values to the avatar. Off, they are only shown.</summary>
        internal bool ManualOutput = true;
        /// <summary>The user's own mappings: an exact parameter name and a formula of tracking keys.</summary>
        internal Dictionary<string, string> CustomMappings = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>One line of the debug window's tables.</summary>
    internal sealed class FTEmulatorDebugRow
    {
        /// <summary>The store key commands are sent with; null for a row that cannot be changed.</summary>
        internal string Key;
        internal string Name;
        internal string Group = "";
        internal string Type = "Float";
        internal bool HasLive;
        internal float Live;
        internal bool HasEffective;
        internal float Effective;
        internal string Owner = "None";
        internal string Hardware = "";
        internal string Source = "";
        internal string Note = "";
        internal FTEmulatorRowMode Mode;
        internal bool Mapped;
        internal bool Blocked;
        internal float Min;
        internal float Max = 1f;
        /// <summary>The hardware control this row follows, for the link to it; null when it has none or several.</summary>
        internal string LinkedControl;
    }

    /// <summary>What the windows show: a copy taken on the main thread, never the session's own state.</summary>
    internal sealed class FTEmulatorSessionSnapshot
    {
        internal FTEmulatorState State;
        internal string StateText = "";
        internal string Avatar = "";
        internal FTEmulatorResolution Resolution;
        internal FTEmulatorHardwareProfile Hardware;
        internal FTEmulatorBehavior Behavior;
        internal FTEmulatorInput Input;
        internal bool Partial;
        internal readonly List<string> PartialReasons = new List<string>();
        internal readonly List<FTEmulatorDebugRow> Controls = new List<FTEmulatorDebugRow>();
        internal readonly List<FTEmulatorDebugRow> Expressions = new List<FTEmulatorDebugRow>();
        internal readonly List<FTEmulatorDebugRow> Parameters = new List<FTEmulatorDebugRow>();
        internal int Applied, Blocked, Unmapped, Held;
        internal bool EyeTracking, FaceTracking;
        // The OSC panel.
        internal string SourceProblem;
        internal int Port;
        internal long Received, Rejected;
        internal int PacketsPerSecond;
        internal double SampleAge = -1;
        internal string LastPayload;
        internal string Sender;
        internal string ServiceName;
        internal bool Discoverable;
        internal int AdvertisedParameters;
        internal readonly List<string> Peers = new List<string>();
    }

    /// <summary>
    /// One run of FTEmulator on one avatar, from Start to Stop: it takes the newest input, holds it to the hardware
    /// profile, applies the debug window's overrides, works out the avatar's values (native or legacy) and writes them.
    /// It is plain C#: the avatar is reached only through the output, the mesh output and the scan it is given, so it
    /// runs the same with Gesture Manager in Play Mode and with stand-ins in a test. Everything here runs on the main
    /// thread; a source's own thread only fills the source's buffer.
    /// Order per frame (Tick, before animation): read input, controls from input, overrides, expand to tracking keys,
    /// final hardware check, native evaluation or legacy mapping, parameter writes. LateTick (after animation) writes the
    /// native blendshape weights.
    /// </summary>
    internal sealed class FTEmulatorSession
    {
        internal const string UserFaceTracking = "UserFaceTracking";
        internal const string UserEyeTracking = "UserEyeTracking";
        // Every this many frames, a few written parameters are read back to see whether the avatar took them.
        private const int VerifyEvery = 30;
        private const float VerifyTolerance = 0.02f;
        private static int generations;

        private readonly FTEmulatorCatalog catalog;
        private readonly IFTEmulatorOutput output;
        private readonly Func<FTEmulatorAvatarSnapshot> scan;
        private readonly Func<FTEmulatorAvatarSnapshot, IFTEmulatorMeshOutput> createMeshOutput;
        private readonly Func<FTEmulatorSessionOptions, FTEmulatorDiagnostics, IFTEmulatorSource> createSource;
        private readonly Func<bool> playing;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private readonly FTEmulatorParameterStore store = new FTEmulatorParameterStore();
        private readonly FTEmulatorSessionContext context;
        // Parameters FTEmulator drives now, with the value each had before: put back when it stops driving them.
        private readonly Dictionary<string, float> owned = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> written = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<FTEmulatorParameterValue> writes = new List<FTEmulatorParameterValue>();
        private readonly HashSet<string> writtenNow = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> notOnAvatar = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> notTaken = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> release = new List<string>();
        private readonly List<FTEmulatorParameterValue> passthrough = new List<FTEmulatorParameterValue>();
        private readonly Dictionary<string, KeyValuePair<float, float>> setTargets = new Dictionary<string, KeyValuePair<float, float>>(StringComparer.Ordinal);

        private FTEmulatorAvatarSnapshot avatar;
        private FTEmulatorResolution resolution;
        private FTEmulatorHardwareProfile hardware;
        private FTEmulatorHardwareFilter filter;
        private FTEmulatorLegacyMapper mapper;
        private FTEmulatorLegacyMapper passthroughMapper;
        private FTEmulatorNativeEvaluator evaluator;
        private IFTEmulatorMeshOutput meshOutput;
        private IFTEmulatorSource source;
        private FTEmulatorOscQuery query;
        private string[] controlKeys = new string[0];
        private float[] controlValues = new float[0];
        private bool[] controlAvailable = new bool[0];
        private float[] controlEffective = new float[0];
        private bool[] controlDriven = new bool[0];
        private float[] values = new float[0];
        private bool[] valueDriven = new bool[0];
        private float[] lastInput = new float[0];
        private bool[] lastInputValid = new bool[0];
        private float[] weights = new float[0];
        private bool[] weightOwned = new bool[0];
        private float[] forwarded = new float[0];
        private List<string> missingBlendshapes = new List<string>();
        private bool started, stopped, paused, listening, meshActive;
        private bool eyeAvailable, faceAvailable;
        private double lastTick = -1;
        private int frame;
        private int applied, blockedValues;
        private string sourceProblem;
        private string stateProblem;

        internal readonly FTEmulatorSessionOptions Options;
        internal readonly FTEmulatorDiagnostics Diagnostics;
        internal readonly int Generation;
        /// <summary>Console lines: the start, the stop summary, and each problem once. Set by FTEmulator; null in tests.</summary>
        internal Action<string> Info;

        internal FTEmulatorSession(FTEmulatorCatalog catalog, FTEmulatorSessionOptions options, IFTEmulatorOutput output, Func<FTEmulatorAvatarSnapshot> scan,
            Func<FTEmulatorAvatarSnapshot, IFTEmulatorMeshOutput> createMeshOutput,
            Func<FTEmulatorSessionOptions, FTEmulatorDiagnostics, IFTEmulatorSource> createSource, Func<bool> playing, FTEmulatorDiagnostics diagnostics)
        {
            this.catalog = catalog;
            Options = options;
            this.output = output;
            this.scan = scan;
            this.createMeshOutput = createMeshOutput;
            this.createSource = createSource;
            this.playing = playing;
            Diagnostics = diagnostics ?? new FTEmulatorDiagnostics();
            Generation = Interlocked.Increment(ref generations);
            context = new FTEmulatorSessionContext(Generation, () => !stopped && this.playing(), cancel.Token);
        }

        internal bool Running
        {
            get { return started && !stopped; }
        }

        internal FTEmulatorState State
        {
            get
            {
                if (!Running) return FTEmulatorState.Stopped;
                if (paused) return FTEmulatorState.Paused;
                if (store.Frozen) return FTEmulatorState.Frozen;
                if (Options.Input == FTEmulatorInput.VrcftOsc && !listening) return FTEmulatorState.ListeningOff;
                return FTEmulatorState.Live;
            }
        }

        internal bool Listening
        {
            get { return listening; }
        }

        internal bool Frozen
        {
            get { return store.Frozen; }
        }

        internal FTEmulatorAvatarSnapshot Avatar
        {
            get { return avatar; }
        }

        internal FTEmulatorResolution Resolution
        {
            get { return resolution; }
        }

        internal FTEmulatorNativeEvaluator Evaluator
        {
            get { return evaluator; }
        }

        internal FTEmulatorLegacyMapper Mapper
        {
            get { return mapper; }
        }

        /// <summary>Starts the session. False, with the reason, outside Play Mode, without profile data or without a bound avatar.</summary>
        internal bool Start(out string problem)
        {
            problem = null;
            if (started) return !stopped;
            if (!playing()) problem = "Enter Play Mode to start tracking.";
            else if (!catalog.Usable) problem = "FTEmulator's profile data could not be read: " + (catalog.Problems.Count > 0 ? catalog.Problems[0] : "no profiles");
            else if (output == null || !output.Valid) problem = "Select a Gesture Manager avatar.";
            if (problem != null) return false;
            started = true;
            Rebuild(true);
            listening = Options.Input == FTEmulatorInput.VrcftOsc;
            if (listening) StartSource();
            Info?.Invoke("Started on '" + avatar.Name + "': backend " + resolution.Summary + ", hardware " + hardware.Label + ", input " +
                (Options.Input == FTEmulatorInput.VrcftOsc ? "VRCFT OSC on port " + Options.Port : "Manual") + ".");
            return true;
        }

        // Scans (when asked), resolves the backend and builds the filter, the store's rows and the output side.
        private void Rebuild(bool rescan)
        {
            if (rescan || avatar == null) avatar = scan() ?? new FTEmulatorAvatarSnapshot();
            resolution = FTEmulatorTemplateResolver.Resolve(avatar, catalog, Options.Backend);
            hardware = catalog.GetHardware(Options.HardwareId) ?? catalog.GetHardware(FTEmulatorCatalog.UnrestrictedId) ?? catalog.Hardware[0];
            filter = new FTEmulatorHardwareFilter(catalog, hardware, Options.Behavior);
            int keys = catalog.Keys.Length;
            values = new float[keys];
            valueDriven = new bool[keys];
            if (lastInput.Length != keys)
            {
                lastInput = new float[keys];
                lastInputValid = new bool[keys];
            }
            int controls = hardware.Controls.Length;
            controlKeys = new string[controls];
            controlValues = new float[controls];
            controlAvailable = new bool[controls];
            controlEffective = new float[controls];
            controlDriven = new bool[controls];
            // Rows of another device go, with any value typed or held on them; parameter rows are checked again below.
            store.Retain(key => key.StartsWith(FTEmulatorParameterStore.ParameterPrefix, StringComparison.Ordinal) ||
                hardware.ControlIndex.ContainsKey(key.Substring(FTEmulatorParameterStore.ControlPrefix.Length)));
            for (int i = 0; i < controls; i++)
            {
                FTEmulatorHardwareControl control = hardware.Controls[i];
                controlKeys[i] = FTEmulatorParameterStore.ControlPrefix + control.Name;
                store.Ensure(controlKeys[i], FTEmulatorValueType.Float, control.Min, control.Max, control.Neutral);
            }
            store.ClearLive();
            if (source != null) source.Clear();

            // The output side.
            ReleaseMesh();
            evaluator = null;
            forwarded = new float[0];
            weights = new float[0];
            weightOwned = new bool[0];
            missingBlendshapes = new List<string>();
            setTargets.Clear();
            notOnAvatar.Clear();
            notTaken.Clear();
            FTEmulatorAvatarProfile set = resolution.ParameterSet;
            if (resolution.Backend == FTEmulatorAvatarProfile.Native)
            {
                evaluator = new FTEmulatorNativeEvaluator(avatar.Native, catalog);
                weights = new float[evaluator.Blendshapes.Length];
                weightOwned = new bool[evaluator.Blendshapes.Length];
                forwarded = new float[evaluator.Forwarded.Length];
                meshOutput = createMeshOutput != null ? createMeshOutput(avatar) : null;
                if (meshOutput != null) missingBlendshapes = meshOutput.Bind(evaluator.Blendshapes);
                foreach (string name in missingBlendshapes) Diagnostics.Record(FTEmulatorDiagnostics.MappingFailure, "blendshape '" + name + "' is on no Face Tracking Mesh");
                foreach (string feature in evaluator.Unsupported) Diagnostics.Record(FTEmulatorDiagnostics.NativeUnsupported, feature);
                var forwardedNames = new HashSet<string>(evaluator.Forwarded, StringComparer.Ordinal);
                // A parameter the native forwarder drives has that one owner: nothing legacy binds it.
                mapper = new FTEmulatorLegacyMapper(catalog, null, avatar.ExpressionParameters, Options.CustomMappings, forwardedNames.Contains);
                passthroughMapper = set != null ? new FTEmulatorLegacyMapper(catalog, set, avatar.ExpressionParameters, null, forwardedNames.Contains) : null;
            }
            else
            {
                // Legacy drives from its parameter set; an unknown avatar is driven by custom mappings only.
                mapper = new FTEmulatorLegacyMapper(catalog, resolution.Backend == FTEmulatorAvatarProfile.Legacy ? set : null, avatar.ExpressionParameters,
                    Options.CustomMappings, null);
                passthroughMapper = resolution.Backend == FTEmulatorAvatarProfile.Legacy ? mapper : null;
            }
            foreach (KeyValuePair<string, string> pair in mapper.Unmapped) Diagnostics.Record(FTEmulatorDiagnostics.MappingFailure, pair.Key + ": " + pair.Value);
            foreach (string name in avatar.TypeConflicts) Diagnostics.Record(FTEmulatorDiagnostics.TypeConflict, name);
            foreach (FTEmulatorParameterStore.Entry entry in store.Entries)
            {
                if (!entry.Key.StartsWith(FTEmulatorParameterStore.ParameterPrefix, StringComparison.Ordinal)) continue;
                store.SetBlocked(entry.Key, PassthroughProblem(entry.Key.Substring(FTEmulatorParameterStore.ParameterPrefix.Length)));
            }
            if (query != null && query.Running) query.ReplaceAvatar(Advert());
            Diagnostics.Set("Avatar", avatar.Name + " (" + avatar.Id + ")");
            Diagnostics.Set("Backend", resolution.Summary);
            Diagnostics.Set("Hardware", hardware.Id + " " + hardware.Version + ", " + Options.Behavior);
        }

        private FTEmulatorAvatarAdvert Advert()
        {
            // Native support is only claimed when the native backend is the one running.
            return new FTEmulatorAvatarAdvert(avatar.Id, resolution.Backend == FTEmulatorAvatarProfile.Native, avatar.ExpressionParameters, Generation);
        }

        private void StartSource()
        {
            sourceProblem = null;
            try
            {
                if (source == null) source = createSource(Options, Diagnostics);
                if (source == null) return;
                source.Start(context);
                var receiver = source as FTEmulatorOscReceiver;
                if (receiver != null && Options.Discovery)
                {
                    query = new FTEmulatorOscQuery(Diagnostics);
                    query.Start(receiver.Port, Advert(), context);
                }
            }
            catch (Exception exception)
            {
                // A taken port, or a service that could not start: the session goes on, with the manual controls.
                // Whatever went wrong, nothing half-open is left behind.
                if (!(exception is FTEmulatorPortInUseException)) Diagnostics.Record(FTEmulatorDiagnostics.ReceiverFault, exception.GetType().Name + ": " + exception.Message);
                sourceProblem = exception.Message;
                StopSource();
                listening = false;
                store.SetSourceStopped(true, FTEmulatorClock.Now);
            }
        }

        private void StopSource()
        {
            if (query != null) query.Stop();
            query = null;
            if (source != null) source.Stop();
        }

        /// <summary>
        /// Active listening. Off: the receiver is closed, the service withdrawn and the last values kept. On: input runs
        /// again from the next new sample; nothing that arrived in between is used.
        /// </summary>
        internal void SetListening(bool on)
        {
            if (!Running || Options.Input != FTEmulatorInput.VrcftOsc || on == listening) return;
            double now = FTEmulatorClock.Now;
            if (on)
            {
                listening = true;
                StartSource();
                if (listening) store.SetSourceStopped(false, now);
            }
            else
            {
                StopSource();
                listening = false;
                store.SetSourceStopped(true, now);
            }
        }

        /// <summary>Freeze output: the outputs keep their values while input goes on being shown. Off: resumes from a new sample.</summary>
        internal void SetFrozen(bool frozen)
        {
            if (!Running) return;
            store.SetFrozen(frozen, FTEmulatorClock.Now);
            if (!frozen && source != null) source.Clear();
        }

        /// <summary>Unity's pause. Paused: nothing is applied and what arrives is dropped. Resumed: starts from a new sample.</summary>
        internal void SetPaused(bool pause)
        {
            if (paused == pause) return;
            paused = pause;
            if (source != null) source.Clear();
            store.ClearLive();
            lastTick = -1;
        }

        internal void SetInput(FTEmulatorInput input)
        {
            if (Options.Input == input) return;
            Options.Input = input;
            if (!Running) return;
            double now = FTEmulatorClock.Now;
            StopSource();
            store.ClearLive();
            if (input == FTEmulatorInput.VrcftOsc)
            {
                listening = true;
                StartSource();
                if (listening) store.SetSourceStopped(false, now);
            }
            else
            {
                listening = false;
                sourceProblem = null;
                store.SetSourceStopped(false, now);
            }
        }

        /// <summary>
        /// Another device. Values typed or held for the old device's controls are dropped, and what FTEmulator was
        /// driving is released before the next frame is taken.
        /// </summary>
        internal void SetHardware(string id, FTEmulatorBehavior behavior)
        {
            if (Options.HardwareId == id && Options.Behavior == behavior) return;
            Options.HardwareId = id;
            Options.Behavior = behavior;
            if (!Running) return;
            ReleaseParameters();
            Rebuild(false);
        }

        internal void SetBackend(string choice)
        {
            if (Options.Backend == choice) return;
            Options.Backend = choice;
            if (!Running) return;
            ReleaseParameters();
            Rebuild(false);
        }

        /// <summary>The port and discovery choice, applied by closing and opening the receiver.</summary>
        internal void RestartConnection(int port, bool discovery)
        {
            Options.Port = port;
            Options.Discovery = discovery;
            if (!Running || Options.Input != FTEmulatorInput.VrcftOsc) return;
            StopSource();
            if (source != null) source.Dispose();
            source = null;
            listening = true;
            StartSource();
            if (listening) store.SetSourceStopped(false, FTEmulatorClock.Now);
        }

        internal void SetCustomMappings(Dictionary<string, string> mappings)
        {
            Options.CustomMappings = mappings ?? new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Running) return;
            ReleaseParameters();
            Rebuild(false);
        }

        /// <summary>Reads the avatar again (after its parameters, descriptor or build result changed).</summary>
        internal void Rescan()
        {
            if (!Running) return;
            ReleaseParameters();
            Rebuild(true);
        }

        internal void ResetOverrides()
        {
            store.ResetOverrides();
        }

        internal bool SetRowMode(string key, FTEmulatorRowMode mode)
        {
            return store.SetMode(key, mode, FTEmulatorClock.Now);
        }

        internal bool SetRowManual(string key, float value)
        {
            return store.SetManual(key, value);
        }

        /// <summary>A Blendshapes Set's new weight from a state behavior: reached at speed per second (0: at once).</summary>
        internal void RequestSetWeight(string set, float weight, float speed)
        {
            if (evaluator == null || !evaluator.HasSet(set)) return;
            if (speed <= 0f)
            {
                setTargets.Remove(set);
                evaluator.SetWeight(set, weight);
            }
            else setTargets[set] = new KeyValuePair<float, float>(Math.Min(Math.Max(weight, 0f), 1f), speed);
        }

        /// <summary>One frame, before animation. now is FTEmulatorClock time.</summary>
        internal void Tick(double now)
        {
            if (!Running) return;
            if (!playing())
            {
                Stop("Play Mode ended");
                return;
            }
            if (paused) return;
            if (!output.Valid)
            {
                // The avatar or its graph was replaced: what was bound is gone.
                stateProblem = "Gesture Manager has no active avatar. Select one and Rescan.";
                owned.Clear();
                written.Clear();
                return;
            }
            stateProblem = null;
            float delta = lastTick < 0 ? 0f : (float)Math.Min(Math.Max(now - lastTick, 0.0), 0.1);
            lastTick = now;
            frame++;

            // 1. The newest input only.
            if (source != null && source.Running && source.TryRead(out FTEmulatorExpressionFrame expressions, out FTEmulatorParameterFrame parameters))
            {
                if (expressions != null)
                {
                    if (expressions.Generation != Generation || expressions.Values.Length != values.Length)
                        Diagnostics.Record(FTEmulatorDiagnostics.StaleGeneration, "expression frame of session " + expressions.Generation);
                    else
                    {
                        Array.Copy(expressions.Values, lastInput, lastInput.Length);
                        Array.Copy(expressions.Valid, lastInputValid, lastInputValid.Length);
                        filter.Project(expressions, controlValues, controlAvailable);
                        for (int i = 0; i < controlKeys.Length; i++)
                        {
                            if (controlAvailable[i]) store.SetLive(controlKeys[i], controlValues[i], now);
                        }
                    }
                }
                if (parameters != null)
                {
                    if (parameters.Generation != Generation) Diagnostics.Record(FTEmulatorDiagnostics.StaleGeneration, "parameter frame of session " + parameters.Generation);
                    else
                    {
                        foreach (FTEmulatorParameterValue value in parameters.Values) TakeParameter(value, now);
                    }
                }
            }

            // 2. Overrides act on the device's controls.
            bool anyControl = false;
            for (int i = 0; i < controlKeys.Length; i++)
            {
                controlDriven[i] = store.TryEffective(controlKeys[i], now, out controlEffective[i]);
                anyControl |= controlDriven[i];
            }

            // 3. Controls to tracking keys, then the final check: nothing the device lacks gets through.
            filter.Expand(controlEffective, controlDriven, values, valueDriven);
            blockedValues = filter.Validate(values, valueDriven);
            eyeAvailable = false;
            faceAvailable = false;
            for (int key = 0; key < values.Length; key++)
            {
                if (!filter.Allowed[key] || !valueDriven[key]) continue;
                if (catalog.EyeKey[key]) eyeAvailable |= hardware.EyeTracking;
                else faceAvailable |= hardware.FaceTracking;
            }

            // Values that arrive already mapped count as tracking of the region their channels belong to, so the avatar's
            // tracking-active parameters follow them too.
            passthrough.Clear();
            foreach (FTEmulatorParameterStore.Entry entry in store.Entries)
            {
                if (!entry.Key.StartsWith(FTEmulatorParameterStore.ParameterPrefix, StringComparison.Ordinal)) continue;
                if (!store.TryEffective(entry.Key, now, out float value)) continue;
                string name = entry.Key.Substring(FTEmulatorParameterStore.ParameterPrefix.Length);
                passthrough.Add(new FTEmulatorParameterValue(name, entry.Type, value));
                if (passthroughMapper == null) continue;
                foreach (int key in passthroughMapper.GetDependencies(name))
                {
                    if (!filter.Allowed[key]) continue;
                    if (catalog.EyeKey[key]) eyeAvailable |= hardware.EyeTracking;
                    else faceAvailable |= hardware.FaceTracking;
                }
            }

            // 4. The avatar's values.
            writes.Clear();
            bool drive = Options.Input != FTEmulatorInput.Manual || Options.ManualOutput;
            if (drive)
            {
                if (evaluator != null)
                {
                    MoveSetWeights(delta);
                    meshActive = anyControl;
                    if (anyControl)
                    {
                        evaluator.Evaluate(values, weights, forwarded);
                        for (int i = 0; i < weightOwned.Length; i++) weightOwned[i] = true;
                        for (int i = 0; i < forwarded.Length; i++) writes.Add(new FTEmulatorParameterValue(evaluator.Forwarded[i], FTEmulatorValueType.Float, forwarded[i]));
                    }
                    writes.Add(new FTEmulatorParameterValue(UserFaceTracking, FTEmulatorValueType.Bool, faceAvailable ? 1f : 0f));
                    writes.Add(new FTEmulatorParameterValue(UserEyeTracking, FTEmulatorValueType.Bool, eyeAvailable ? 1f : 0f));
                }
                mapper.Map(values, valueDriven, eyeAvailable, faceAvailable, writes);
                writes.AddRange(passthrough);
            }
            else meshActive = false;
            if (!meshActive) ReleaseMeshWeights();

            // 5. Parameter writes, each parameter once: the first owner in the order above keeps it.
            applied = 0;
            writtenNow.Clear();
            foreach (FTEmulatorParameterValue value in writes)
            {
                if (!writtenNow.Add(value.Name)) continue;
                if (avatar.TypeConflicts.Contains(value.Name)) continue;
                if (!owned.ContainsKey(value.Name))
                {
                    if (!output.TryRead(value.Name, out float before))
                    {
                        // The status parameters are optional: an avatar that does not read them has none.
                        if (value.Name != UserFaceTracking && value.Name != UserEyeTracking && notOnAvatar.Add(value.Name))
                            Diagnostics.Record(FTEmulatorDiagnostics.MappingFailure, "'" + value.Name + "' is not a parameter of the running avatar");
                        continue;
                    }
                    owned[value.Name] = before;
                }
                if (output.Write(value.Name, value.Value))
                {
                    written[value.Name] = value.Value;
                    applied++;
                }
            }
            // What is no longer driven goes back to the value it had.
            release.Clear();
            foreach (KeyValuePair<string, float> pair in owned)
            {
                if (!writtenNow.Contains(pair.Key)) release.Add(pair.Key);
            }
            foreach (string name in release)
            {
                output.Write(name, owned[name]);
                owned.Remove(name);
                written.Remove(name);
            }

            // 6. Did the avatar take what was written?
            if (frame % VerifyEvery == 0) Verify();
            Diagnostics.Add("Frames");
        }

        // A value that arrived already mapped. It is checked against the hardware limits before it becomes a row's value.
        private void TakeParameter(FTEmulatorParameterValue value, double now)
        {
            string key = FTEmulatorParameterStore.ParameterPrefix + value.Name;
            bool known = store.Find(key) != null;
            // These values are sent when they change, not every frame: the last one stays.
            store.Ensure(key, value.Type, -10000f, 10000f, 0f, false);
            string problem = PassthroughProblem(value.Name);
            if (problem != null && (!known || store.Find(key).Blocked == null)) Diagnostics.Record(FTEmulatorDiagnostics.ProfileBlock, value.Name + ": " + problem);
            store.SetBlocked(key, problem);
            if (problem == null) store.SetLive(key, value.Value, now);
        }

        private string PassthroughProblem(string name)
        {
            if (name == UserFaceTracking || name == UserEyeTracking) return "Set by FTEmulator: a status parameter is not taken from the input";
            if (evaluator != null && Array.IndexOf(evaluator.Forwarded, name) >= 0) return "Driven by the native forwarder";
            if (avatar.TypeConflicts.Contains(name)) return "Its controllers disagree on its type";
            if (passthroughMapper != null) return passthroughMapper.ValidatePassthrough(name, filter.Allowed, hardware, catalog.Keys);
            return hardware.Unrestricted ? null : "Strict: the avatar has no known profile, so it cannot be checked against " + hardware.Name;
        }

        private void MoveSetWeights(float delta)
        {
            if (setTargets.Count == 0) return;
            release.Clear();
            foreach (KeyValuePair<string, KeyValuePair<float, float>> pair in setTargets)
            {
                float current = evaluator.GetWeight(pair.Key), target = pair.Value.Key, step = pair.Value.Value * delta;
                float next = Math.Abs(target - current) <= step ? target : current + Math.Sign(target - current) * step;
                evaluator.SetWeight(pair.Key, next);
                if (next == target) release.Add(pair.Key);
            }
            foreach (string set in release) setTargets.Remove(set);
        }

        // A write the avatar did not take: a parameter an animation curve controls, or one something else sets each frame.
        private void Verify()
        {
            int checkedCount = 0;
            foreach (KeyValuePair<string, float> pair in written)
            {
                if (checkedCount++ >= 64) break;
                if (notTaken.Contains(pair.Key) || !output.TryRead(pair.Key, out float actual)) continue;
                if (Math.Abs(actual - pair.Value) <= VerifyTolerance) continue;
                notTaken.Add(pair.Key);
                Diagnostics.Record(FTEmulatorDiagnostics.OwnershipConflict, "'" + pair.Key + "' reads " + actual.ToString("0.###", CultureInfo.InvariantCulture) +
                    " after " + pair.Value.ToString("0.###", CultureInfo.InvariantCulture) + " was written: an animation curve or another tool controls it");
            }
        }

        /// <summary>After animation: the native blendshape weights worked out in Tick.</summary>
        internal void LateTick()
        {
            if (!Running || paused || !meshActive || meshOutput == null || !output.Valid) return;
            meshOutput.Apply(weights, weightOwned);
        }

        private void ReleaseMeshWeights()
        {
            if (meshOutput == null) return;
            bool any = false;
            for (int i = 0; i < weightOwned.Length; i++)
            {
                any |= weightOwned[i];
                weightOwned[i] = false;
            }
            if (any) meshOutput.Restore();
        }

        private void ReleaseMesh()
        {
            if (meshOutput != null) meshOutput.Restore();
            meshOutput = null;
            meshActive = false;
        }

        // Every parameter FTEmulator drives goes back to the value it had before.
        private void ReleaseParameters()
        {
            if (output != null && output.Valid)
            {
                foreach (KeyValuePair<string, float> pair in owned) output.Write(pair.Key, pair.Value);
            }
            owned.Clear();
            written.Clear();
        }

        /// <summary>
        /// Ends the session: closes the source and the service, puts back the parameters and blendshape weights it was
        /// driving, and releases the output. Safe to call again.
        /// </summary>
        internal void Stop(string reason)
        {
            if (!started || stopped) return;
            stopped = true;
            cancel.Cancel();
            StopSource();
            if (source != null) source.Dispose();
            source = null;
            ReleaseParameters();
            ReleaseMesh();
            if (output != null) output.Release();
            Info?.Invoke("Stopped (" + reason + "): " + Diagnostics.Counter("Frames") + " frame(s) applied, " + Diagnostics.Count(FTEmulatorDiagnostics.PacketRejected) +
                " packet(s) rejected, " + Diagnostics.Count(FTEmulatorDiagnostics.ProfileBlock) + " value(s) blocked by the hardware profile.");
        }

        /// <summary>A copy of the session's state for the windows.</summary>
        internal FTEmulatorSessionSnapshot Snapshot()
        {
            double now = FTEmulatorClock.Now;
            var snapshot = new FTEmulatorSessionSnapshot
            {
                State = State, Avatar = avatar != null ? avatar.Name : "", Resolution = resolution, Hardware = hardware, Behavior = Options.Behavior,
                Input = Options.Input, EyeTracking = eyeAvailable, FaceTracking = faceAvailable, SourceProblem = sourceProblem, Applied = applied,
            };
            snapshot.StateText = stateProblem ?? StateText(snapshot.State);
            if (!started || hardware == null) return snapshot;

            if (evaluator != null)
            {
                snapshot.PartialReasons.AddRange(evaluator.Unsupported);
                foreach (string name in missingBlendshapes) snapshot.PartialReasons.Add("Blendshape '" + name + "' is on no Face Tracking Mesh");
            }
            snapshot.PartialReasons.AddRange(resolution.Missing);
            snapshot.Partial = snapshot.PartialReasons.Count > 0;

            var receiver = source as FTEmulatorOscReceiver;
            if (receiver != null)
            {
                snapshot.Port = receiver.Port;
                snapshot.Received = receiver.Received;
                snapshot.Rejected = receiver.Rejected;
                snapshot.PacketsPerSecond = receiver.PacketsPerSecond;
                snapshot.SampleAge = receiver.LastTime < 0 ? -1 : now - receiver.LastTime;
                snapshot.LastPayload = receiver.LastKind;
                snapshot.Sender = receiver.Sender;
            }
            if (query != null && query.Running)
            {
                snapshot.ServiceName = query.ServiceName;
                snapshot.Discoverable = query.Discoverable;
                snapshot.Peers.AddRange(query.Peers);
                snapshot.AdvertisedParameters = query.Avatar != null ? query.Avatar.ParameterCount : 0;
            }

            // Hardware controls: the rows overrides act on.
            for (int i = 0; i < hardware.Controls.Length; i++)
            {
                FTEmulatorHardwareControl control = hardware.Controls[i];
                FTEmulatorParameterStore.Entry entry = store.Find(controlKeys[i]);
                List<int> keys = filter.KeysOf(i);
                var names = new List<string>();
                foreach (int key in keys) names.Add(catalog.Keys[key]);
                var row = new FTEmulatorDebugRow
                {
                    Key = controlKeys[i], Name = control.Name, Group = control.Group, HasLive = entry.HasLive, Live = entry.Live,
                    HasEffective = controlDriven[i], Effective = controlEffective[i], Owner = FTEmulatorParameterStore.Owner(entry, store.Frozen, store.SourceStopped),
                    Hardware = "Allowed", Mode = entry.Mode, Min = control.Min, Max = control.Max, Mapped = keys.Count > 0,
                    Source = control.From == null ? "Manual only" : entry.HasLive ? "Observed" : "Unavailable",
                    Note = keys.Count == 0 ? "Drives no tracking key under " + Options.Behavior : "Drives " + string.Join(", ", names.ToArray()),
                };
                if (row.Owner == "Held" || row.Owner == "Manual") snapshot.Held++;
                snapshot.Controls.Add(row);
            }

            // Tracking keys: what the controls expand to. Read-only; a key follows its control.
            for (int key = 0; key < catalog.Keys.Length; key++)
            {
                int[] from = filter.ControlsOf(key);
                bool allowed = filter.Allowed[key];
                var row = new FTEmulatorDebugRow
                {
                    Name = catalog.Keys[key], Group = catalog.EyeKey[key] ? "Eye" : "Face", HasLive = lastInputValid[key], Live = lastInput[key],
                    HasEffective = allowed && valueDriven[key], Effective = values[key], Blocked = !allowed, Mapped = allowed,
                    Hardware = allowed ? filter.IsDerived(key) ? "Derived" : "Allowed" : filter.BlockedReason(key),
                    Source = lastInputValid[key] ? "Observed" : "Unavailable", Note = allowed ? filter.NoteOf(key) : "",
                };
                if (allowed && from.Length == 1)
                {
                    row.LinkedControl = hardware.Controls[from[0]].Name;
                    FTEmulatorParameterStore.Entry entry = store.Find(controlKeys[from[0]]);
                    // A control that drives several keys, or a key that is not the control itself, is a link, not an owner.
                    row.Owner = filter.KeysOf(from[0]).Count > 1 || hardware.Controls[from[0]].Name != catalog.Keys[key]
                        ? "Linked" : FTEmulatorParameterStore.Owner(entry, store.Frozen, store.SourceStopped);
                }
                else if (allowed) row.Owner = "Linked";
                if (!allowed) snapshot.Blocked++;
                snapshot.Expressions.Add(row);
            }

            // Avatar outputs: native forwards and blendshapes, mapped legacy parameters, and values that arrive already mapped.
            if (evaluator != null)
            {
                for (int i = 0; i < evaluator.Forwarded.Length; i++)
                    snapshot.Parameters.Add(OutputRow(evaluator.Forwarded[i], "Float", "Native", "Forward to Animator", meshActive, forwarded[i]));
                snapshot.Parameters.Add(OutputRow(UserFaceTracking, "Bool", "Session", "Status", true, faceAvailable ? 1f : 0f));
                snapshot.Parameters.Add(OutputRow(UserEyeTracking, "Bool", "Session", "Status", true, eyeAvailable ? 1f : 0f));
                for (int i = 0; i < evaluator.Blendshapes.Length; i++)
                {
                    FTEmulatorDebugRow row = OutputRow(evaluator.Blendshapes[i], "Blendshape", "Native", "Face Tracking Mesh", meshActive, weights[i]);
                    row.Max = 100f;
                    if (missingBlendshapes.Contains(evaluator.Blendshapes[i]))
                    {
                        row.Mapped = false;
                        row.Owner = "None";
                        row.Note = "On no Face Tracking Mesh";
                        snapshot.Unmapped++;
                    }
                    snapshot.Parameters.Add(row);
                }
            }
            foreach (FTEmulatorLegacyMapper.Binding binding in mapper.Bindings)
            {
                bool has = written.TryGetValue(binding.Parameter, out float value);
                FTEmulatorDebugRow row = OutputRow(binding.Parameter, binding.Type.ToString(), binding.Kind == FTEmulatorLegacyMapper.BindingKind.Status ? "Session" : "Mapped",
                    binding.Kind == FTEmulatorLegacyMapper.BindingKind.Status ? "Status" : binding.Source + " (" + binding.Kind + ")", has, value);
                row.Min = binding.Min;
                row.Max = binding.Max;
                int blocked = 0;
                foreach (int key in binding.Dependencies)
                {
                    if (!filter.Allowed[key]) blocked++;
                }
                row.Hardware = blocked == 0 ? "Allowed" : blocked == binding.Dependencies.Length ? "Blocked: " + hardware.Name + " has none of its channels" : "Partly blocked: the missing channels read 0";
                row.Blocked = blocked > 0 && blocked == binding.Dependencies.Length;
                row.Note = binding.Value != null ? binding.Value.Text : "";
                if (binding.Dependencies.Length == 1)
                {
                    int[] from = filter.ControlsOf(binding.Dependencies[0]);
                    if (from.Length == 1) row.LinkedControl = hardware.Controls[from[0]].Name;
                }
                if (notOnAvatar.Contains(binding.Parameter)) { row.Mapped = false; row.Note = "Not a parameter of the running avatar"; snapshot.Unmapped++; }
                else if (notTaken.Contains(binding.Parameter)) row.Note = "Not taken: an animation curve or another tool controls it";
                snapshot.Parameters.Add(row);
            }
            foreach (FTEmulatorParameterStore.Entry entry in store.Entries)
            {
                if (!entry.Key.StartsWith(FTEmulatorParameterStore.ParameterPrefix, StringComparison.Ordinal)) continue;
                bool has = store.TryEffective(entry.Key, now, out float value);
                var row = new FTEmulatorDebugRow
                {
                    Key = entry.Key, Name = entry.Key.Substring(FTEmulatorParameterStore.ParameterPrefix.Length), Type = entry.Type.ToString(),
                    HasLive = entry.HasLive, Live = entry.Live, HasEffective = has, Effective = value, Mode = entry.Mode,
                    Owner = FTEmulatorParameterStore.Owner(entry, store.Frozen, store.SourceStopped), Hardware = entry.Blocked ?? "Allowed", Blocked = entry.Blocked != null,
                    Source = "VRCFT (already mapped)", Mapped = entry.Blocked == null, Min = -1f, Max = 1f,
                };
                if (row.Blocked) snapshot.Blocked++;
                if (row.Owner == "Held" || row.Owner == "Manual") snapshot.Held++;
                snapshot.Parameters.Add(row);
            }
            foreach (KeyValuePair<string, string> pair in mapper.Unmapped)
            {
                snapshot.Parameters.Add(new FTEmulatorDebugRow { Name = pair.Key, Type = "", Owner = "None", Hardware = "", Source = "Unmapped", Note = pair.Value });
                snapshot.Unmapped++;
            }
            return snapshot;
        }

        private static FTEmulatorDebugRow OutputRow(string name, string type, string owner, string source, bool has, float value)
        {
            return new FTEmulatorDebugRow { Name = name, Type = type, Owner = has ? owner : "None", Hardware = "Allowed", Source = source, HasEffective = has, Effective = value, Mapped = true };
        }

        private string StateText(FTEmulatorState state)
        {
            switch (state)
            {
                case FTEmulatorState.Live: return Options.Input == FTEmulatorInput.Manual ? "Manual values" + (Options.ManualOutput ? "" : " (not written)") : "Live";
                case FTEmulatorState.ListeningOff: return sourceProblem != null ? "Input not started · values held" : "Input stopped · values held";
                case FTEmulatorState.Frozen: return "Output held · input still monitored";
                case FTEmulatorState.Paused: return "Paused";
                default: return "Stopped";
            }
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator session
// ============================================================================
//
// FTEmulatorSessionOptions   Input, HardwareId, Behavior, Backend, Port, Discovery, ManualOutput, CustomMappings.
// FTEmulatorDebugRow         Key (null: read-only), Name, Group, Type, Live, Effective, Owner, Hardware, Source, Note, Mode,
//                            Mapped, Blocked, Min, Max, LinkedControl.
// FTEmulatorSessionSnapshot  State, StateText, Avatar, Resolution, Hardware, Behavior, Input, Partial and PartialReasons, the
//                            Controls, Expressions and Parameters rows, Applied / Blocked / Unmapped / Held counts,
//                            EyeTracking / FaceTracking, and the OSC panel's readings.
// FTEmulatorSession (this file)
//   Start(out problem)       Refused outside Play Mode, without usable profile data or without a bound output. Scans,
//                            resolves, builds, and starts the source for the VRCFT OSC input.
//   Tick(now)                One frame before animation: newest input (other generations are dropped as StaleGeneration),
//                            controls, overrides, Expand, Validate, native Evaluate or legacy Map, parameter writes. A
//                            parameter is captured before its first write and put back when it stops being driven.
//                            Every VerifyEvery (30) frames up to 64 written values are read back; one that differs by more
//                            than VerifyTolerance (0.02) is recorded as OwnershipConflict.
//   LateTick()               After animation: the native blendshape weights.
//   SetListening / SetFrozen / SetPaused / SetInput / SetHardware / SetBackend / RestartConnection / SetCustomMappings /
//   Rescan / ResetOverrides / SetRowMode / SetRowManual  The windows' commands.
//   RequestSetWeight(set, weight, speed)  A Blendshapes Set's weight from a state behavior, moved there over time.
//   Stop(reason)             Closes the source and service, puts back parameters and blendshape weights, releases the output.
//   Snapshot()               The windows' copy of the state.
//   State / Running / Listening / Frozen / Avatar / Resolution / Evaluator / Mapper / Generation / Diagnostics / Info
//   Status parameters        UserFaceTracking and UserEyeTracking (native) and the parameter set's status parameters
//                            (legacy) are the session's own: true while that region is tracked, by controls or by values
//                            that arrive already mapped. They are never taken from the input.
