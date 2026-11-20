using System;
using System.Collections.Generic;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Works out the typed values of a legacy avatar's face tracking parameters (VRCFaceTracking's FT/v2 family) from
    /// tracking keys, with the formulas of a parameter set from the catalog. It binds only parameters in the avatar's
    /// Expression Parameters, the ones VRChat's OSC input can reach, by their exact names: a name matches an entry when it
    /// is the entry's name or ends with "/" + that name. Template outputs (smoothing, proxies, binary decoders) are never
    /// bound. The same bindings check values that arrive already mapped against the hardware limits (ValidatePassthrough).
    /// </summary>
    internal sealed class FTEmulatorLegacyMapper
    {
        internal enum BindingKind { Float, Bool, BinaryBit, BinaryNegative, Status, Custom }

        /// <summary>The binary Bools of one parameter: name + 1, 2, 4, ... and name + Negative.</summary>
        internal sealed class BinaryGroup
        {
            internal int Bits;
            internal bool HasNegative;
        }

        internal sealed class Binding
        {
            /// <summary>The avatar's exact parameter name.</summary>
            internal string Parameter;
            internal FTEmulatorValueType Type;
            internal BindingKind Kind;
            /// <summary>The profile entry it follows, or "Custom".</summary>
            internal string Source;
            internal FTEmulatorExpression Value;
            internal float Min;
            internal float Max = 1f;
            internal float BoolBelow;
            internal int Shift;
            internal BinaryGroup Group;
            internal bool EyeRegion;
            internal int[] Dependencies = new int[0];
        }

        private const string NegativeSuffix = "Negative";

        private readonly Dictionary<string, Binding> byName = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private readonly FTEmulatorAvatarProfile set;

        internal readonly List<Binding> Bindings = new List<Binding>();
        /// <summary>Parameters that were not bound, each with the reason.</summary>
        internal readonly List<KeyValuePair<string, string>> Unmapped = new List<KeyValuePair<string, string>>();

        /// <param name="set">The parameter set (Kind "parameters"), or null for custom mappings only.</param>
        /// <param name="parameters">The avatar's Expression Parameters.</param>
        /// <param name="custom">The user's own mappings: an exact parameter name and a formula of tracking keys.</param>
        /// <param name="taken">Says a parameter has another owner (a native forwarder) and must not be bound.</param>
        internal FTEmulatorLegacyMapper(FTEmulatorCatalog catalog, FTEmulatorAvatarProfile set, IEnumerable<FTEmulatorAvatarParameter> parameters,
            IEnumerable<KeyValuePair<string, string>> custom, Func<string, bool> taken)
        {
            this.set = set;
            var entries = new Dictionary<string, FTEmulatorProfileParameter>(StringComparer.Ordinal);
            if (set != null)
            {
                foreach (FTEmulatorProfileParameter entry in set.Entries) entries[entry.Name] = entry;
            }
            var customFormulas = new Dictionary<string, FTEmulatorExpression>(StringComparer.Ordinal);
            if (custom != null)
            {
                foreach (KeyValuePair<string, string> pair in custom)
                {
                    FTEmulatorExpression expression = FTEmulatorExpression.Compile(pair.Value, catalog.Key, out string error);
                    if (expression != null) customFormulas[pair.Key] = expression;
                    else Unmapped.Add(new KeyValuePair<string, string>(pair.Key, "Custom mapping: " + error));
                }
            }
            var groups = new Dictionary<string, BinaryGroup>(StringComparer.Ordinal);
            foreach (FTEmulatorAvatarParameter parameter in parameters)
            {
                if (string.IsNullOrEmpty(parameter.Name) || byName.ContainsKey(parameter.Name)) continue;
                if (taken != null && taken(parameter.Name)) continue;
                Binding binding = null;
                if (customFormulas.TryGetValue(parameter.Name, out FTEmulatorExpression formula))
                {
                    binding = new Binding { Kind = BindingKind.Custom, Source = "Custom", Value = formula, Min = -1f, Max = 1f, Dependencies = formula.Dependencies };
                }
                else if (set != null && !Excluded(parameter.Name))
                {
                    binding = Bind(parameter, entries, groups);
                }
                if (binding == null) continue;
                binding.Parameter = parameter.Name;
                binding.Type = parameter.Type;
                byName[parameter.Name] = binding;
                Bindings.Add(binding);
            }
        }

        private bool Excluded(string name)
        {
            foreach (string prefix in set.ExcludePrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // The entry a name follows: the name itself, or what comes after one of its slashes.
        private static FTEmulatorProfileParameter Match(string name, Dictionary<string, FTEmulatorProfileParameter> entries)
        {
            if (entries.TryGetValue(name, out FTEmulatorProfileParameter entry)) return entry;
            for (int slash = name.IndexOf('/'); slash >= 0; slash = name.IndexOf('/', slash + 1))
            {
                if (entries.TryGetValue(name.Substring(slash + 1), out entry)) return entry;
            }
            return null;
        }

        private Binding Bind(FTEmulatorAvatarParameter parameter, Dictionary<string, FTEmulatorProfileParameter> entries, Dictionary<string, BinaryGroup> groups)
        {
            string name = parameter.Name;
            FTEmulatorProfileParameter entry = Match(name, entries);
            if (entry != null)
            {
                if (parameter.Type == FTEmulatorValueType.Int)
                {
                    Unmapped.Add(new KeyValuePair<string, string>(name, "Int parameter: " + entry.Name + " is a Float or a Bool"));
                    return null;
                }
                return new Binding
                {
                    Kind = parameter.Type == FTEmulatorValueType.Bool ? BindingKind.Bool : BindingKind.Float, Source = entry.Name, Value = entry.Value,
                    Min = entry.Min, Max = entry.Max, BoolBelow = entry.BoolBelow, Dependencies = entry.Value.Dependencies
                };
            }
            if (parameter.Type == FTEmulatorValueType.Bool)
            {
                // Binary: the entry's name followed by a power of two, or by Negative.
                int digits = name.Length;
                while (digits > 0 && char.IsDigit(name[digits - 1])) digits--;
                if (digits < name.Length && name.Length - digits <= 4 && int.TryParse(name.Substring(digits), out int step) && step > 0 && (step & (step - 1)) == 0)
                {
                    string stem = name.Substring(0, digits);
                    entry = Match(stem, entries);
                    if (entry != null)
                    {
                        BinaryGroup group = Group(groups, stem);
                        group.Bits++;
                        int shift = 0;
                        while (1 << shift < step) shift++;
                        return new Binding { Kind = BindingKind.BinaryBit, Source = entry.Name, Value = entry.Value, Shift = shift, Group = group, Dependencies = entry.Value.Dependencies };
                    }
                }
                if (name.EndsWith(NegativeSuffix, StringComparison.Ordinal))
                {
                    string stem = name.Substring(0, name.Length - NegativeSuffix.Length);
                    entry = Match(stem, entries);
                    if (entry != null)
                    {
                        BinaryGroup group = Group(groups, stem);
                        group.HasNegative = true;
                        return new Binding { Kind = BindingKind.BinaryNegative, Source = entry.Name, Value = entry.Value, Group = group, Dependencies = entry.Value.Dependencies };
                    }
                }
                foreach (KeyValuePair<string, string> status in set.Status)
                {
                    if (name == status.Key || name.EndsWith("/" + status.Key, StringComparison.Ordinal))
                        return new Binding { Kind = BindingKind.Status, Source = status.Key, EyeRegion = status.Value == FTEmulatorCatalog.EyeRegion };
                }
            }
            return null;
        }

        private static BinaryGroup Group(Dictionary<string, BinaryGroup> groups, string stem)
        {
            if (!groups.TryGetValue(stem, out BinaryGroup group)) groups[stem] = group = new BinaryGroup();
            return group;
        }

        internal Binding Find(string parameter)
        {
            return parameter != null && byName.TryGetValue(parameter, out Binding binding) ? binding : null;
        }

        /// <summary>The tracking keys a bound parameter's value reads (empty when it is not bound).</summary>
        internal int[] GetDependencies(string parameter)
        {
            Binding binding = Find(parameter);
            return binding != null ? binding.Dependencies : new int[0];
        }

        /// <summary>The value binding gives for these tracking key values, in its parameter's type (Bool as 0 or 1).</summary>
        internal static float Evaluate(Binding binding, float[] values, bool eyeAvailable, bool faceAvailable)
        {
            if (binding.Kind == BindingKind.Status) return (binding.EyeRegion ? eyeAvailable : faceAvailable) ? 1f : 0f;
            float value = binding.Value.Evaluate(values);
            switch (binding.Kind)
            {
                case BindingKind.Bool: return value < binding.BoolBelow ? 1f : 0f;
                case BindingKind.BinaryNegative: return value < 0f ? 1f : 0f;
                case BindingKind.BinaryBit:
                {
                    // Without a Negative parameter the negative half is cut, as VRCFaceTracking does.
                    if (value < 0f && !binding.Group.HasNegative) return 0f;
                    float size = Math.Abs(value);
                    if (size > 0.99999f) return 1f;
                    int steps = (int)(size * (1 << binding.Group.Bits));
                    return (steps >> binding.Shift & 1) == 1 ? 1f : 0f;
                }
                case BindingKind.Custom:
                    if (binding.Type == FTEmulatorValueType.Bool) return value >= 0.5f ? 1f : 0f;
                    if (binding.Type == FTEmulatorValueType.Int) return (float)Math.Round(value);
                    return Math.Min(Math.Max(value, binding.Min), binding.Max);
                default: return Math.Min(Math.Max(value, binding.Min), binding.Max);
            }
        }

        /// <summary>
        /// Every bound parameter's value, added to output. A parameter is left out (FTEmulator does not write it) while a
        /// key it reads is not driven. Status parameters are always given. Returns how many were left out.
        /// </summary>
        internal int Map(float[] values, bool[] driven, bool eyeAvailable, bool faceAvailable, List<FTEmulatorParameterValue> output)
        {
            int skipped = 0;
            foreach (Binding binding in Bindings)
            {
                bool ready = true;
                foreach (int key in binding.Dependencies)
                {
                    if (!driven[key]) { ready = false; break; }
                }
                if (!ready) { skipped++; continue; }
                output.Add(new FTEmulatorParameterValue(binding.Parameter, binding.Type, Evaluate(binding, values, eyeAvailable, faceAvailable)));
            }
            return skipped;
        }

        /// <summary>
        /// Whether a value that arrived already mapped may be applied under the hardware limits. Null when it may, else
        /// the reason. A combined value cannot be split back into the channels it was made from, so one that mixes allowed
        /// and blocked channels is refused, and so is a parameter the profile does not describe.
        /// </summary>
        internal string ValidatePassthrough(string parameter, bool[] allowed, FTEmulatorHardwareProfile hardware, string[] keys)
        {
            Binding binding = Find(parameter);
            if (binding != null && binding.Kind == BindingKind.Status) return "Set by FTEmulator: a status parameter is not taken from the input";
            if (hardware.Unrestricted) return null;
            if (binding == null)
                return "Strict: not described by the avatar's profile, so it cannot be checked against " + hardware.Name;
            int blocked = 0;
            string example = null;
            foreach (int key in binding.Dependencies)
            {
                if (allowed[key]) continue;
                blocked++;
                example = example ?? keys[key];
            }
            if (blocked == 0) return null;
            if (blocked == binding.Dependencies.Length) return "Blocked: " + hardware.Name + " has no " + example + " channel";
            return "Strict: mixes channels " + hardware.Name + " has with ones it lacks (" + example + "); the combined value cannot be split";
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator legacy mapper
// ============================================================================
//
// FTEmulatorLegacyMapper (this file)
//   Constructor              Binds the avatar's Expression Parameters to a parameter set's entries and to custom mappings.
//                            A parameter another owner has (taken) and names under the set's ExcludePrefixes are left alone.
//   Bindings / Find(name)    The bound parameters. Binding: Parameter (exact name), Type, Kind, Source (the entry's name or
//                            "Custom"), Value (the formula), Min, Max, BoolBelow, Shift and Group (binary), EyeRegion
//                            (status), Dependencies (tracking keys read).
//   BindingKind              Float; Bool (true while the value is below BoolBelow); BinaryBit (name + 1, 2, 4, ...);
//                            BinaryNegative (name + Negative); Status (true while that region is tracked); Custom.
//   Unmapped                 Parameters that could not be bound, with the reason (an Int parameter, a custom formula error).
//   Evaluate(binding, values, eye, face)  One binding's value. Binary: the value's size times 2^bits, one bit per Bool;
//                            above 0.99999 every bit is set; a negative value sets Negative, or gives 0 without one.
//   Map(values, driven, eye, face, output)  All values; a parameter is left out while a key it reads is not driven.
//   GetDependencies(name)    The tracking keys a parameter reads.
//   ValidatePassthrough(name, allowed, hardware, keys)  Null, or why an already mapped value is refused.
