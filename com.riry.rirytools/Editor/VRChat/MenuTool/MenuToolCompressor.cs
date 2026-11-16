using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// VRCFury's Parameter Compressor solver (ParameterCompressorSolverService and OptimizationDecision), run on a scan.
    /// Over the limit, it tries sets of menu control types in turn (radial puppets; toggles; both; two- and four-axis
    /// puppets; and wider mixes), each compressing the synced, standard-width parameters only those controls use (a
    /// parameter used by several control types counts as the last of radial, toggle, two-axis, four-axis, button, submenu;
    /// parameters a Parameter Driver adds to only with four-axis puppets). Compressed parameters are sent in batches through
    /// number (8-bit) and bool slots, plus index bits, the slots widened until the bits are full. It keeps the first set
    /// that fits, and a later one only if it at least halves the sync time.
    /// </summary>
    internal static class MenuToolCompressor
    {
        // VRCFury's editor setting (Tools > VRCFury): 0 Compress, 1 Ask, 2 Fail.
        internal const string SettingKey = "com.vrcfury.parameterCompressor";
        internal const int Compress = 0;
        internal const int Fail = 2;
        private const float BatchTime = 0.1f;

        // Highest priority last: a parameter used by several control types counts as the last of them.
        private static readonly ControlType[] Priority =
        {
            ControlType.RadialPuppet, ControlType.Toggle, ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet, ControlType.Button, ControlType.SubMenu
        };

        private static readonly ControlType[][] Attempts =
        {
            new[] { ControlType.RadialPuppet },
            new[] { ControlType.Toggle },
            new[] { ControlType.RadialPuppet, ControlType.Toggle },
            new[] { ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet },
            new[] { ControlType.RadialPuppet, ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet },
            new[] { ControlType.Toggle, ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet },
            new[] { ControlType.RadialPuppet, ControlType.Toggle, ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet }
        };

        /// <summary>What to compress and how many slots carry it.</summary>
        internal sealed class Decision
        {
            internal readonly List<MenuToolParamEntry> Compress = new List<MenuToolParamEntry>();
            internal int NumberSlots;
            internal int BoolSlots;

            private int Numbers
            {
                get { return Compress.Count(p => p.Type != ValueType.Bool); }
            }

            private int Bools
            {
                get { return Compress.Count(p => p.Type == ValueType.Bool); }
            }

            internal int BatchCount
            {
                get { return Batches(NumberSlots, BoolSlots); }
            }

            internal int IndexBits
            {
                get { return IndexBitsFor(BatchCount); }
            }

            private int Batches(int numberSlots, int boolSlots)
            {
                return Math.Max(Chunks(Numbers, numberSlots), Chunks(Bools, boolSlots));
            }

            private static int Chunks(int count, int size)
            {
                return count == 0 || size <= 0 ? 0 : (count + size - 1) / size;
            }

            private static int IndexBitsFor(int batches)
            {
                int bits = 1;
                while ((1 << bits) < batches + 1) bits++;
                return bits;
            }

            private int Cost(int original, int numberSlots, int boolSlots)
            {
                return original + IndexBitsFor(Batches(numberSlots, boolSlots)) + numberSlots * 8 + boolSlots - Compress.Sum(p => p.Cost);
            }

            internal int FinalCost(int original)
            {
                return Cost(original, NumberSlots, BoolSlots);
            }

            // Widens the slots until the bits are full, keeping number and bool batches level.
            internal void Optimize(int original, int max)
            {
                int bools = Bools, numbers = Numbers;
                BoolSlots = bools > 0 ? 1 : 0;
                NumberSlots = numbers > 0 ? 1 : 0;
                while (true)
                {
                    if (NumberSlots < numbers && Cost(original, NumberSlots + 1, BoolSlots) <= max &&
                        (bools == 0 || (float)NumberSlots / numbers < (float)BoolSlots / bools)) NumberSlots++;
                    else if (BoolSlots < bools && Cost(original, NumberSlots, BoolSlots + 1) <= max) BoolSlots++;
                    else break;
                }
            }
        }

        /// <summary>VRCFury's limit: the SDK's maximum cost (256), less 19 bits when DexProtect is installed.</summary>
        internal static int MaxCost()
        {
            int max = VRCExpressionParameters.MAX_PARAMETER_COST;
            // Some modified VRChat SDKs have a broken value.
            if (max > 9999) max = 256;
            if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "DexProtectEditor")) max -= 19;
            return max;
        }

        /// <summary>VRCFury's compressor setting (Compress unless set).</summary>
        internal static int Setting()
        {
            return EditorPrefs.GetInt(SettingKey, Compress);
        }

        /// <summary>
        /// The decision VRCFury would make for a scan over the limit, or null when even its most compression does not fit
        /// (bestCost: the fewest bits it reaches).
        /// </summary>
        internal static Decision Solve(MenuToolScan scan, int original, out int bestCost)
        {
            int max = MaxCost();
            var addDriven = new HashSet<string>(scan.Controllers.SelectMany(controller => controller.AddDriven));
            Dictionary<string, ControlType> menuTypes = MenuTypes(scan.Root);
            List<MenuToolParamEntry> parameters = scan.Params.Values.Where(p => p.Declared).ToList();
            bestCost = original;
            Decision best = null;
            bool bestFits = false;
            float bestTime = 0f;
            foreach (ControlType[] allowed in Attempts)
            {
                var decision = new Decision();
                foreach (MenuToolParamEntry parameter in parameters)
                {
                    if (!parameter.Synced || !parameter.StandardEncoding) continue;
                    if (!menuTypes.TryGetValue(parameter.Name, out ControlType type) || !allowed.Contains(type)) continue;
                    if (addDriven.Contains(parameter.Name) && !allowed.Contains(ControlType.FourAxisPuppet)) continue;
                    decision.Compress.Add(parameter);
                }
                decision.Optimize(original, max);
                int cost = decision.FinalCost(original);
                float time = decision.BatchCount * BatchTime;
                if (bestFits)
                {
                    // A more aggressive set only if it at least halves the sync time, and still fits.
                    if (time > bestTime / 2f || cost > max) continue;
                }
                else if (cost >= bestCost) continue;
                bestCost = cost;
                best = decision;
                bestFits = cost <= max;
                bestTime = time;
                if (bestFits && time <= 1f) break;
            }
            return bestFits ? best : null;
        }

        // Each parameter of the assembled menu and the control type it counts as (GetParamsUsedInMenu): a puppet's own
        // parameter counts as a submenu, its axes as the puppet.
        private static Dictionary<string, ControlType> MenuTypes(MenuToolEntry root)
        {
            var types = new Dictionary<string, ControlType>();
            Action<string, ControlType> add = (name, type) =>
            {
                if (string.IsNullOrEmpty(name)) return;
                if (types.TryGetValue(name, out ControlType old) && Array.IndexOf(Priority, type) < Array.IndexOf(Priority, old)) return;
                types[name] = type;
            };
            foreach (MenuToolEntry control in root.Descendants())
            {
                switch (control.Type)
                {
                    case ControlType.RadialPuppet:
                        add(control.Parameter, ControlType.SubMenu);
                        add(Sub(control, 0), control.Type);
                        break;
                    case ControlType.Button:
                    case ControlType.Toggle:
                        add(control.Parameter, control.Type);
                        break;
                    case ControlType.FourAxisPuppet:
                        add(control.Parameter, ControlType.SubMenu);
                        for (int i = 0; i < 4; i++) add(Sub(control, i), control.Type);
                        break;
                    case ControlType.TwoAxisPuppet:
                        add(control.Parameter, ControlType.SubMenu);
                        for (int i = 0; i < 2; i++) add(Sub(control, i), control.Type);
                        break;
                    case ControlType.SubMenu:
                        add(control.Parameter, ControlType.SubMenu);
                        break;
                }
            }
            return types;
        }

        private static string Sub(MenuToolEntry control, int index)
        {
            return index < control.SubParameters.Length ? control.SubParameters[index] : null;
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool compressor
// ============================================================================
//
// MenuToolCompressor (this file)        VRCFury's Parameter Compressor solver: SettingKey "com.vrcfury.parameterCompressor" (Compress 0,
//                            Ask 1, Fail 2; Setting()), MaxCost (SDK MAX_PARAMETER_COST, 256 if broken, less 19 with
//                            DexProtect), Attempts (control type sets, in VRCFury's order), Priority (a parameter used by several
//                            types counts as the last), MenuTypes (GetParamsUsedInMenu), Solve (eligible: synced, standard
//                            encoding, used by an allowed type, not Add-driven unless four-axis puppets are allowed; first set
//                            that fits, a later one only if it halves the sync time; null when none fits). Decision: Compress,
//                            NumberSlots, BoolSlots, BatchCount, IndexBits, FinalCost (original + index bits + 8 per number slot
//                            + 1 per bool slot - compressed bits), Optimize (widens slots up to MaxCost, keeping batches level).
//   AnimatorOnly             Synced parameters no menu control uses.
