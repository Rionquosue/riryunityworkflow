using System;
using System.Collections.Generic;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// An avatar descriptor's native face tracking settings, read into plain data (FTEmulatorNativeDescriptor reads
    /// them), so the evaluator needs no SDK type.
    /// </summary>
    internal sealed class FTEmulatorNativeConfig
    {
        // VRCAvatarDescriptor.ParameterBehaviorTargetMode, by value.
        internal const int ForwardToAnimator = 0, ApplyBlendSetWeight = 1, ApplyToBlendshape = 2, LimitBlendshapeMax = 3, LimitBlendshapeMin = 4;
        // VRCAvatarDescriptor.ParameterBehaviorCombineMode, by value.
        internal const int CombineMax = 0, CombineMin = 1, CombineAdd = 2, CombineMultiply = 3, CombineCustom = 4;
        /// <summary>The input after the tracking keys: the owner's voice level.</summary>
        internal const string VoiceInput = "Voice";
        /// <summary>The viseme inputs, in VRC_AvatarDescriptor.Viseme order; they follow Voice.</summary>
        internal static readonly string[] VisemeInputs =
        {
            "Viseme_sil", "Viseme_pp", "Viseme_ff", "Viseme_th", "Viseme_dd", "Viseme_kk", "Viseme_ch", "Viseme_ss", "Viseme_nn", "Viseme_rr",
            "Viseme_aa", "Viseme_e", "Viseme_ih", "Viseme_oh", "Viseme_ou"
        };
        // The client's limits, as the SDK states them.
        internal const int MaxMeshes = 16, MaxSets = 128, MaxBehaviors = 128, MaxInputsPerBehavior = 32, MaxTotalInstructions = 2048;

        internal sealed class Set
        {
            internal string Name;
            internal float DefaultWeight;
            /// <summary>Per tracking key: the blendshape it drives (empty: none) and its multiplier.</summary>
            internal string[] KeyBlendshapes = new string[0];
            internal float[] KeyMultipliers = new float[0];
            /// <summary>Per viseme: the same.</summary>
            internal string[] VisemeBlendshapes = new string[0];
            internal float[] VisemeMultipliers = new float[0];
        }

        internal sealed class Behavior
        {
            internal int TargetMode;
            internal string OutputParameter;
            internal string TargetSet;
            internal string TargetBlendshape;
            internal string WeightSet;
            /// <summary>Input indices: a tracking key, then Voice, then the visemes.</summary>
            internal int[] Inputs = new int[0];
            internal bool[] Inverted = new bool[0];
            internal int CombineMode;
            internal string Expression;
        }

        internal bool Enabled;
        internal bool UseAllTrackingKeys;
        /// <summary>LipSync mode is Viseme BlendShape Face Tracking: visemes go through face tracking.</summary>
        internal bool VisemesRouted;
        /// <summary>Eye Gaze Mode is Bone Rotation: the eye look keys drive no blendshape.</summary>
        internal bool GazeDisabled;
        /// <summary>Eyelids are bones or their own blendshapes: the eye closed keys drive no blendshape.</summary>
        internal bool BlinkDisabled;
        /// <summary>Viseme Dampen is on (it needs viseme input, which FTEmulator does not have).</summary>
        internal bool VisemeDampen;
        internal int MeshCount;
        internal readonly List<Set> Sets = new List<Set>();
        internal readonly List<Behavior> Behaviors = new List<Behavior>();

        /// <summary>Face tracking is on and has something to drive.</summary>
        internal bool Configured
        {
            get { return Enabled && (Sets.Count > 0 || Behaviors.Count > 0); }
        }
    }

    /// <summary>
    /// Works out what VRChat's native face tracking would do with a frame of tracking keys, from the descriptor's
    /// settings: Blendshapes Sets (the highest value wins where sets share a blendshape), set weights, and Parameter
    /// Behaviors (Forward to Animator, Apply Blend Set Weight, Apply to Blendshape, Limit Blendshape Min and Max; Max,
    /// Min, Add, Multiply and Custom combine modes). VRChat's client is not called and its code is not available: what
    /// the documentation leaves open is decided here and listed in Assumptions, and what is not emulated is listed in
    /// Unsupported, which makes the preview Partial.
    /// </summary>
    internal sealed class FTEmulatorNativeEvaluator
    {
        private sealed class Mapping
        {
            internal int Input;
            internal int Target;
            internal float Multiplier;
        }

        private sealed class CompiledSet
        {
            internal string Name;
            internal float Weight;
            internal readonly List<Mapping> Mappings = new List<Mapping>();
        }

        private sealed class CompiledBehavior
        {
            internal FTEmulatorNativeConfig.Behavior Source;
            internal FTEmulatorExpression Expression;
            internal int[] Inputs;
            internal int Target = -1;      // blendshape, set or forwarded index
            internal int WeightSet = -1;
        }

        private readonly int keyCount;
        private readonly bool[] keyUsable;
        private readonly List<CompiledSet> sets = new List<CompiledSet>();
        private readonly List<CompiledBehavior> behaviors = new List<CompiledBehavior>();
        private readonly float[] inputs;
        private readonly Dictionary<string, int> setIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The blendshapes face tracking drives, by name; Evaluate's weights are in this order.</summary>
        internal readonly string[] Blendshapes;
        /// <summary>The animator parameters face tracking forwards to; Evaluate's forwarded values are in this order.</summary>
        internal readonly string[] Forwarded;
        /// <summary>Settings the avatar uses that are not emulated. Not empty means a Partial preview.</summary>
        internal readonly List<string> Unsupported = new List<string>();
        /// <summary>Settings that were left out the way the client leaves them out (past a limit, a formula that does not compile).</summary>
        internal readonly List<string> Ignored = new List<string>();
        /// <summary>What VRChat's documentation leaves open, and how it is decided here.</summary>
        internal static readonly string[] Assumptions =
        {
            "Apply Blend Set Weight behaviors run before the sets, the other behaviors after them, each in list order.",
            "Apply to Blendshape joins the sets' values by highest value; Limit Min and Max are applied after that.",
            "A blendshape weight is held to 0..100.",
            "A set weight changed by a state behavior moves at Lerp Speed per second; 0 changes it at once.",
            "Division by zero in a Custom expression gives 0."
        };

        internal bool Partial
        {
            get { return Unsupported.Count > 0; }
        }

        internal int SetCount
        {
            get { return sets.Count; }
        }

        internal FTEmulatorNativeEvaluator(FTEmulatorNativeConfig config, FTEmulatorCatalog catalog)
        {
            keyCount = catalog.Keys.Length;
            int inputCount = keyCount + 1 + FTEmulatorNativeConfig.VisemeInputs.Length;
            inputs = new float[inputCount];
            // Without Use All Tracking Keys only eye look and blink reach the avatar.
            keyUsable = new bool[keyCount];
            for (int key = 0; key < keyCount; key++)
                keyUsable[key] = config.UseAllTrackingKeys || catalog.Keys[key].StartsWith("EyeLook", StringComparison.Ordinal) || catalog.Keys[key].StartsWith("EyeClosed", StringComparison.Ordinal);

            var blendshapes = new List<string>();
            var blendshapeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            int Target(string name)
            {
                if (!blendshapeIndex.TryGetValue(name, out int index))
                {
                    blendshapeIndex[name] = index = blendshapes.Count;
                    blendshapes.Add(name);
                }
                return index;
            }

            bool voiceUsed = false, visemesUsed = false;
            if (config.Sets.Count > FTEmulatorNativeConfig.MaxSets) Ignored.Add("Blendshapes Sets after the first " + FTEmulatorNativeConfig.MaxSets);
            for (int s = 0; s < Math.Min(config.Sets.Count, FTEmulatorNativeConfig.MaxSets); s++)
            {
                FTEmulatorNativeConfig.Set source = config.Sets[s];
                var set = new CompiledSet { Name = source.Name ?? "", Weight = Clamp01(source.DefaultWeight) };
                for (int key = 0; key < Math.Min(keyCount, source.KeyBlendshapes.Length); key++)
                {
                    string name = source.KeyBlendshapes[key];
                    if (string.IsNullOrEmpty(name) || !keyUsable[key]) continue;
                    if (config.GazeDisabled && catalog.Keys[key].StartsWith("EyeLook", StringComparison.Ordinal)) continue;
                    if (config.BlinkDisabled && catalog.Keys[key].StartsWith("EyeClosed", StringComparison.Ordinal)) continue;
                    set.Mappings.Add(new Mapping { Input = key, Target = Target(name), Multiplier = key < source.KeyMultipliers.Length ? source.KeyMultipliers[key] : 1f });
                }
                for (int viseme = 0; viseme < Math.Min(FTEmulatorNativeConfig.VisemeInputs.Length, source.VisemeBlendshapes.Length); viseme++)
                {
                    string name = source.VisemeBlendshapes[viseme];
                    if (string.IsNullOrEmpty(name) || !config.VisemesRouted) continue;
                    visemesUsed = true;
                    set.Mappings.Add(new Mapping
                    {
                        Input = keyCount + 1 + viseme, Target = Target(name),
                        Multiplier = viseme < source.VisemeMultipliers.Length ? source.VisemeMultipliers[viseme] : 1f
                    });
                }
                if (!setIndex.ContainsKey(set.Name)) setIndex[set.Name] = sets.Count;
                sets.Add(set);
            }

            var forwarded = new List<string>();
            var forwardedIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            int instructions = 0;
            if (config.Behaviors.Count > FTEmulatorNativeConfig.MaxBehaviors) Ignored.Add("Parameter Behaviors after the first " + FTEmulatorNativeConfig.MaxBehaviors);
            for (int b = 0; b < Math.Min(config.Behaviors.Count, FTEmulatorNativeConfig.MaxBehaviors); b++)
            {
                FTEmulatorNativeConfig.Behavior source = config.Behaviors[b];
                string label = "Parameter Behavior #" + (b + 1);
                var behavior = new CompiledBehavior { Source = source };
                if (source.CombineMode == FTEmulatorNativeConfig.CombineCustom)
                {
                    behavior.Expression = FTEmulatorExpression.Compile(source.Expression, name => Input(catalog, name), out string error);
                    if (behavior.Expression == null) { Ignored.Add(label + ": " + error); continue; }
                    if (instructions + behavior.Expression.InstructionCount > FTEmulatorNativeConfig.MaxTotalInstructions)
                    {
                        Ignored.Add(label + ": past the avatar's budget of " + FTEmulatorNativeConfig.MaxTotalInstructions + " expression instructions");
                        continue;
                    }
                    instructions += behavior.Expression.InstructionCount;
                    behavior.Inputs = behavior.Expression.Dependencies;
                }
                else
                {
                    if (source.Inputs.Length == 0) { Ignored.Add(label + ": no inputs"); continue; }
                    if (source.Inputs.Length > FTEmulatorNativeConfig.MaxInputsPerBehavior) Ignored.Add(label + ": inputs after the first " + FTEmulatorNativeConfig.MaxInputsPerBehavior);
                    behavior.Inputs = new int[Math.Min(source.Inputs.Length, FTEmulatorNativeConfig.MaxInputsPerBehavior)];
                    Array.Copy(source.Inputs, behavior.Inputs, behavior.Inputs.Length);
                }
                foreach (int input in behavior.Inputs)
                {
                    if (input == keyCount) voiceUsed = true;
                    else if (input > keyCount) visemesUsed = true;
                }
                switch (source.TargetMode)
                {
                    case FTEmulatorNativeConfig.ForwardToAnimator:
                        if (string.IsNullOrEmpty(source.OutputParameter)) { Ignored.Add(label + ": no animator parameter"); continue; }
                        if (!forwardedIndex.TryGetValue(source.OutputParameter, out behavior.Target))
                        {
                            forwardedIndex[source.OutputParameter] = behavior.Target = forwarded.Count;
                            forwarded.Add(source.OutputParameter);
                        }
                        break;
                    case FTEmulatorNativeConfig.ApplyBlendSetWeight:
                        if (source.TargetSet == null || !setIndex.TryGetValue(source.TargetSet, out behavior.Target))
                        {
                            Ignored.Add(label + ": the Blendshapes Set '" + source.TargetSet + "' does not exist");
                            continue;
                        }
                        break;
                    case FTEmulatorNativeConfig.ApplyToBlendshape:
                    case FTEmulatorNativeConfig.LimitBlendshapeMax:
                    case FTEmulatorNativeConfig.LimitBlendshapeMin:
                        if (string.IsNullOrEmpty(source.TargetBlendshape)) { Ignored.Add(label + ": no blendshape"); continue; }
                        behavior.Target = Target(source.TargetBlendshape);
                        if (!string.IsNullOrEmpty(source.WeightSet) && !setIndex.TryGetValue(source.WeightSet, out behavior.WeightSet))
                        {
                            behavior.WeightSet = -1;
                            Ignored.Add(label + ": the weight set '" + source.WeightSet + "' does not exist (not scaled)");
                        }
                        break;
                    default:
                        Unsupported.Add(label + ": unknown target mode " + source.TargetMode);
                        continue;
                }
                behaviors.Add(behavior);
            }
            Blendshapes = blendshapes.ToArray();
            Forwarded = forwarded.ToArray();
            if (voiceUsed) Unsupported.Add("Voice input: there is no microphone input, it reads 0");
            if (visemesUsed) Unsupported.Add("Viseme inputs: visemes are not simulated, they read 0");
            if (config.VisemeDampen) Unsupported.Add("Viseme Dampen: needs viseme input");
            if (config.MeshCount > FTEmulatorNativeConfig.MaxMeshes) Ignored.Add("Face Tracking Meshes after the first " + FTEmulatorNativeConfig.MaxMeshes);
        }

        // A name in a Custom expression: a tracking key (when usable), Voice, or a viseme.
        private int Input(FTEmulatorCatalog catalog, string name)
        {
            int key = catalog.Key(name);
            if (key >= 0) return keyUsable[key] ? key : -1;
            if (name == FTEmulatorNativeConfig.VoiceInput) return keyCount;
            int viseme = Array.IndexOf(FTEmulatorNativeConfig.VisemeInputs, name);
            return viseme >= 0 ? keyCount + 1 + viseme : -1;
        }

        private static float Clamp01(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Min(Math.Max(value, 0f), 1f);
        }

        internal bool HasSet(string name)
        {
            return name != null && setIndex.ContainsKey(name);
        }

        /// <summary>A set's weight now (0 for an unknown set).</summary>
        internal float GetWeight(string set)
        {
            return set != null && setIndex.TryGetValue(set, out int index) ? sets[index].Weight : 0f;
        }

        /// <summary>Sets a set's weight (0..1), as the Animator Face Tracking Blendshapes Set state behavior does.</summary>
        internal void SetWeight(string set, float weight)
        {
            if (set != null && setIndex.TryGetValue(set, out int index)) sets[index].Weight = Clamp01(weight);
        }

        /// <summary>
        /// One frame. keys are the tracking key strengths (a key that is not driven is 0). weights receives the blendshape
        /// weights in Blendshapes order (0..100), forwarded the animator values in Forwarded order.
        /// </summary>
        internal void Evaluate(float[] keys, float[] weights, float[] forwarded)
        {
            Array.Clear(inputs, 0, inputs.Length);
            for (int key = 0; key < keyCount; key++) inputs[key] = keyUsable[key] ? keys[key] : 0f;

            foreach (CompiledBehavior behavior in behaviors)
            {
                if (behavior.Source.TargetMode == FTEmulatorNativeConfig.ApplyBlendSetWeight) sets[behavior.Target].Weight = Combine(behavior, 0f, 1f);
            }
            Array.Clear(weights, 0, weights.Length);
            foreach (CompiledSet set in sets)
            {
                if (set.Weight <= 0f) continue;
                foreach (Mapping mapping in set.Mappings)
                {
                    float value = inputs[mapping.Input] * mapping.Multiplier * set.Weight;
                    if (value > weights[mapping.Target]) weights[mapping.Target] = value;
                }
            }
            foreach (CompiledBehavior behavior in behaviors)
            {
                int mode = behavior.Source.TargetMode;
                if (mode == FTEmulatorNativeConfig.ApplyBlendSetWeight) continue;
                if (mode == FTEmulatorNativeConfig.ForwardToAnimator)
                {
                    // Custom may be signed; the other combine modes stay 0..1.
                    forwarded[behavior.Target] = behavior.Expression != null ? Combine(behavior, -1f, 1f) : Combine(behavior, 0f, 1f);
                    continue;
                }
                float value = Combine(behavior, 0f, 1f);
                if (behavior.WeightSet >= 0) value *= sets[behavior.WeightSet].Weight;
                if (mode == FTEmulatorNativeConfig.ApplyToBlendshape) weights[behavior.Target] = Math.Max(weights[behavior.Target], value);
                else if (mode == FTEmulatorNativeConfig.LimitBlendshapeMax) weights[behavior.Target] = Math.Min(weights[behavior.Target], value);
                else weights[behavior.Target] = Math.Max(weights[behavior.Target], value);
            }
            for (int i = 0; i < weights.Length; i++) weights[i] = Clamp01(weights[i]) * 100f;
        }

        private float Combine(CompiledBehavior behavior, float low, float high)
        {
            float result;
            if (behavior.Expression != null) result = behavior.Expression.Evaluate(inputs);
            else
            {
                int mode = behavior.Source.CombineMode;
                result = mode == FTEmulatorNativeConfig.CombineMin ? float.MaxValue : mode == FTEmulatorNativeConfig.CombineMultiply ? 1f : 0f;
                for (int i = 0; i < behavior.Inputs.Length; i++)
                {
                    int input = behavior.Inputs[i];
                    float value = input >= 0 && input < inputs.Length ? inputs[input] : 0f;
                    if (i < behavior.Source.Inverted.Length && behavior.Source.Inverted[i]) value = 1f - value;
                    switch (mode)
                    {
                        case FTEmulatorNativeConfig.CombineMin: result = Math.Min(result, value); break;
                        case FTEmulatorNativeConfig.CombineAdd: result += value; break;
                        case FTEmulatorNativeConfig.CombineMultiply: result *= value; break;
                        default: result = Math.Max(result, value); break;
                    }
                }
            }
            if (float.IsNaN(result) || float.IsInfinity(result)) result = 0f;
            return Math.Min(Math.Max(result, low), high);
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator native evaluator
// ============================================================================
//
// FTEmulatorNativeConfig     The descriptor's face tracking settings as plain data: Enabled, UseAllTrackingKeys, VisemesRouted,
//                            GazeDisabled, BlinkDisabled, VisemeDampen, MeshCount, Sets (Name, DefaultWeight, a blendshape and
//                            multiplier per tracking key and per viseme) and Behaviors (TargetMode, OutputParameter, TargetSet,
//                            TargetBlendshape, WeightSet, Inputs, Inverted, CombineMode, Expression). Configured: enabled with a
//                            set or a behavior. Inputs are indexed: the tracking keys, then Voice, then VisemeInputs.
//                            Limits: MaxMeshes 16, MaxSets 128, MaxBehaviors 128, MaxInputsPerBehavior 32,
//                            MaxTotalInstructions 2048.
// FTEmulatorNativeEvaluator (this file)
//   Blendshapes / Forwarded  The blendshape names and animator parameter names it drives, in output order.
//   Evaluate(keys, weights, forwarded)  One frame: Apply Blend Set Weight behaviors, then the sets (highest value wins),
//                            then the blendshape behaviors and the forwards. Weights are 0..100.
//   GetWeight / SetWeight / HasSet  A Blendshapes Set's weight (set by FTEmulatorNativeSetWeights).
//   Unsupported / Partial    What the avatar uses and is not emulated (Voice, visemes, Viseme Dampen).
//   Ignored                  What was left out as the client leaves it out (past a limit, a broken expression).
//   Assumptions              What the documentation leaves open and how it is decided here.
