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
    /// <summary>Counts of one parameter set, against VRChat's limits.</summary>
    internal struct BudgetCount
    {
        internal const int MaxBits = 256;
        internal const int MaxParameters = 8192;

        internal int Total;
        internal int Synced;
        internal int Local;
        internal int Bits;

        internal int BitsLeft
        {
            get { return MaxBits - Bits; }
        }

        internal int ParametersLeft
        {
            get { return MaxParameters - Total; }
        }

        internal static BudgetCount Of(IEnumerable<ParamEntry> parameters)
        {
            var count = new BudgetCount();
            foreach (ParamEntry parameter in parameters.Where(p => p.Declared))
            {
                count.Total++;
                if (parameter.Synced) count.Synced++;
                else count.Local++;
                count.Bits += parameter.Bits;
            }
            return count;
        }

        internal static BudgetCount Of(VRCExpressionParameters.Parameter[] parameters)
        {
            var count = new BudgetCount();
            foreach (VRCExpressionParameters.Parameter parameter in parameters)
            {
                if (parameter == null || string.IsNullOrEmpty(parameter.name) || MenuToolScanner.BuiltIn.Contains(parameter.name)) continue;
                count.Total++;
                if (parameter.networkSynced) count.Synced++;
                else count.Local++;
                if (parameter.networkSynced) count.Bits += MenuToolScanner.WidthOf(parameter);
            }
            return count;
        }
    }

    /// <summary>
    /// Parameter budget with VRCFury's Parameter Compressor off and on. VRCFury compresses automatically, only when the
    /// avatar is over the limit, under its Compress / Ask / Fail setting; the estimate runs its solver (MenuToolCompressor)
    /// on the scan. The exact comparison builds two isolated copies of the avatar with only that setting changed, through
    /// the VRChat SDK's own build callbacks (as VRCFury's Build Test Copy does), and compares the parameters each produced.
    /// The copies are deleted afterwards and the setting restored; the avatar itself is never changed.
    /// </summary>
    internal static class MenuToolBudget
    {
        private const string CallbacksType = "VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks";
        private const string VrcfuryType = "VF.Model.VRCFury";

        internal sealed class Result
        {
            internal BudgetCount Off;
            internal BudgetCount On;
            internal bool Exact;
            // Why the exact comparison could not run, or "" when it did (or was not asked for).
            internal string Unavailable = "";
            // What VRCFury would say about the compression (it cannot fit the avatar, or it is set to Fail), or "".
            internal string Note = "";
            internal readonly List<string> Compressed = new List<string>();
        }

        /// <summary>The estimate from a scan: VRCFury's compressor solver on its parameters and assembled menu.</summary>
        internal static Result Estimate(MenuScan scan)
        {
            var result = new Result { Off = BudgetCount.Of(scan.Params.Values) };
            result.On = result.Off;
            int max = MenuToolCompressor.MaxCost();
            if (result.Off.Bits <= max) return result;
            MenuToolCompressor.Decision decision = MenuToolCompressor.Solve(scan, result.Off.Bits, out int bestCost);
            if (MenuToolCompressor.Setting() == MenuToolCompressor.Fail)
                result.Note = "VRCFury's Parameter Compressor is set to Fail (Tools > VRCFury), so an upload over " + max + " bits fails instead of compressing.";
            if (decision == null)
            {
                // VRCFury compresses nothing then, and the upload fails.
                result.Note = (result.Note.Length > 0 ? result.Note + " " : "") + "VRCFury cannot fit this avatar: its most compression reaches " +
                    bestCost + " of " + max + " bits, so it compresses nothing and the upload fails.";
                return result;
            }
            int added = decision.IndexBits + decision.NumberSlots + decision.BoolSlots;
            result.On.Bits = decision.FinalCost(result.Off.Bits);
            result.On.Synced = result.Off.Synced - decision.Compress.Count + added;
            // With more than one batch, every compressed parameter also gets a local latch copy.
            result.On.Total = result.Off.Total + added + (decision.BatchCount > 1 ? decision.Compress.Count : 0);
            result.On.Local = result.On.Total - result.On.Synced;
            result.Compressed.AddRange(decision.Compress.Select(p => p.Name));
            return result;
        }

        /// <summary>
        /// Builds the avatar twice in isolation, with VRCFury's compressor setting at Fail (it compresses nothing) and at
        /// Compress, and compares the parameters. The setting is restored afterwards. Needs VRCFury and an avatar in an
        /// open scene (a prefab asset is placed in the scene for it).
        /// </summary>
        internal static Result Exact(VRCAvatarDescriptor avatar)
        {
            var result = new Result();
            Type vrcfury = RiryToolsTypes.Named(VrcfuryType);
            Type callbacks = RiryToolsTypes.Named(CallbacksType);
            MethodInfo preprocess = callbacks != null ? callbacks.GetMethod("OnPreprocessAvatar", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null) : null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) result.Unavailable = "Not available in Play Mode.";
            else if (avatar == null) result.Unavailable = "No avatar descriptor.";
            else if (vrcfury == null) result.Unavailable = "VRCFury is not installed.";
            else if (preprocess == null) result.Unavailable = "The VRChat SDK's build callbacks were not found.";
            if (result.Unavailable.Length > 0) return result;

            GameObject placed = null;
            GameObject source = avatar.gameObject;
            Scene active = SceneManager.GetActiveScene();
            Scene temporary = default;
            try
            {
                // The copies are built in a temporary scene, closed unsaved afterwards, so no open scene is marked changed.
                // Unity makes no additive untitled scene while another untitled one is open: then the active scene is used.
                bool untitledOpen = Enumerable.Range(0, SceneManager.sceneCount).Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path));
                if (!untitledOpen)
                {
                    temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(temporary);
                }
                if (EditorUtility.IsPersistent(source))
                {
                    // A prefab asset: build from a temporary instance.
                    placed = (GameObject)PrefabUtility.InstantiatePrefab(source, SceneManager.GetActiveScene());
                    source = placed;
                }
                EditorUtility.DisplayProgressBar("Menu Tool", "Building " + avatar.name + " without the Parameter Compressor", 0.2f);
                VRCExpressionParameters.Parameter[] off = Build(source, preprocess, false, out string offError);
                EditorUtility.DisplayProgressBar("Menu Tool", "Building " + avatar.name + " with the Parameter Compressor", 0.6f);
                VRCExpressionParameters.Parameter[] on = Build(source, preprocess, true, out string onError);
                if (off == null || on == null)
                {
                    result.Unavailable = offError ?? onError ?? "The build failed.";
                    return result;
                }
                result.Exact = true;
                result.Off = BudgetCount.Of(off);
                result.On = BudgetCount.Of(on);
                var syncedOn = new HashSet<string>(on.Where(p => p != null && p.networkSynced).Select(p => p.name));
                result.Compressed.AddRange(off.Where(p => p != null && p.networkSynced && !syncedOn.Contains(p.name)).Select(p => p.name));
                RiryLog.Info(RiryLog.MenuTool, "Compression compared on two test builds of " + avatar.name + ": " + result.Off.Bits + " synced bits off, " +
                    result.On.Bits + " on (" + result.Compressed.Count + " parameters compressed).", avatar);
                return result;
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Building the compression test copies of " + avatar.name, exception, avatar);
                result.Unavailable = "The build failed: " + exception.Message;
                return result;
            }
            finally
            {
                if (placed != null)
                {
                    ForgetUndo(placed);
                    Object.DestroyImmediate(placed);
                }
                if (temporary.IsValid())
                {
                    if (active.IsValid()) SceneManager.SetActiveScene(active);
                    EditorSceneManager.CloseScene(temporary, true);
                }
                EditorUtility.ClearProgressBar();
            }
        }

        // Build tools may record undo steps on a copy; they would point at deleted objects, so they are cleared first.
        private static void ForgetUndo(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component != null) Undo.ClearUndo(component);
                }
                Undo.ClearUndo(transform.gameObject);
            }
        }

        // One isolated copy: cloned, VRCFury's compressor setting set (Compress or Fail), built by the SDK's callbacks, its
        // parameters read, then deleted and the setting restored.
        private static VRCExpressionParameters.Parameter[] Build(GameObject source, MethodInfo preprocess, bool withCompressor, out string error)
        {
            error = null;
            bool hadSetting = EditorPrefs.HasKey(MenuToolCompressor.SettingKey);
            int setting = EditorPrefs.GetInt(MenuToolCompressor.SettingKey, MenuToolCompressor.Compress);
            GameObject clone = Object.Instantiate(source);
            try
            {
                clone.name = source.name + (withCompressor ? " (RiryTools compression on)" : " (RiryTools compression off)");
                clone.SetActive(true);
                EditorPrefs.SetInt(MenuToolCompressor.SettingKey, withCompressor ? MenuToolCompressor.Compress : MenuToolCompressor.Fail);
                bool built = (bool)preprocess.Invoke(null, new object[] { clone });
                var descriptor = clone.GetComponent<VRCAvatarDescriptor>();
                if (!built || descriptor == null || descriptor.expressionParameters == null)
                {
                    error = "The VRChat SDK's build callbacks stopped the build (see the Console).";
                    return null;
                }
                // Copies (widths kept), as the built asset goes with the clone.
                return descriptor.expressionParameters.parameters.Where(p => p != null).Select(p => (VRCExpressionParameters.Parameter)JsonUtility.FromJson(
                    JsonUtility.ToJson(p), typeof(VRCExpressionParameters.Parameter))).ToArray();
            }
            finally
            {
                if (hadSetting) EditorPrefs.SetInt(MenuToolCompressor.SettingKey, setting);
                else EditorPrefs.DeleteKey(MenuToolCompressor.SettingKey);
                if (clone != null)
                {
                    ForgetUndo(clone);
                    Object.DestroyImmediate(clone);
                }
            }
        }

        /// <summary>The synced bits under a menu entry: each parameter once.</summary>
        internal static int Bits(MenuScan scan, MenuEntry entry)
        {
            return ParametersUnder(entry).Select(scan.Param).Where(p => p != null && p.Declared).Sum(p => p.Bits);
        }

        /// <summary>Synced Floats under a menu entry (each once), for its shading.</summary>
        internal static int SyncedFloats(MenuScan scan, MenuEntry entry)
        {
            return ParametersUnder(entry).Select(scan.Param).Count(p => p != null && p.Declared && p.Synced && p.Type == ValueType.Float);
        }

        internal static HashSet<string> ParametersUnder(MenuEntry entry)
        {
            var names = new HashSet<string>(entry.Parameters());
            foreach (MenuEntry below in entry.Descendants()) names.UnionWith(below.Parameters());
            return names;
        }

        /// <summary>Synced parameters no menu control uses: the "Animator only" block.</summary>
        internal static List<ParamEntry> AnimatorOnly(MenuScan scan)
        {
            return scan.Params.Values.Where(p => p.Declared && p.Synced && p.UsedBy.Count == 0).OrderBy(p => p.Name).ToList();
        }
    }

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
            internal readonly List<ParamEntry> Compress = new List<ParamEntry>();
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
        internal static Decision Solve(MenuScan scan, int original, out int bestCost)
        {
            int max = MaxCost();
            var addDriven = new HashSet<string>(scan.Controllers.SelectMany(controller => controller.AddDriven));
            Dictionary<string, ControlType> menuTypes = MenuTypes(scan.Root);
            List<ParamEntry> parameters = scan.Params.Values.Where(p => p.Declared).ToList();
            bestCost = original;
            Decision best = null;
            bool bestFits = false;
            float bestTime = 0f;
            foreach (ControlType[] allowed in Attempts)
            {
                var decision = new Decision();
                foreach (ParamEntry parameter in parameters)
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
        private static Dictionary<string, ControlType> MenuTypes(MenuEntry root)
        {
            var types = new Dictionary<string, ControlType>();
            Action<string, ControlType> add = (name, type) =>
            {
                if (string.IsNullOrEmpty(name)) return;
                if (types.TryGetValue(name, out ControlType old) && Array.IndexOf(Priority, type) < Array.IndexOf(Priority, old)) return;
                types[name] = type;
            };
            foreach (MenuEntry control in root.Descendants())
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

        private static string Sub(MenuEntry control, int index)
        {
            return index < control.SubParameters.Length ? control.SubParameters[index] : null;
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool budget
// ============================================================================
//
// BudgetCount                Total custom parameters, Synced, Local, synced Bits; MaxBits 256 and MaxParameters 8192
//                            (VRChat's limits); BitsLeft / ParametersLeft. Of(scan params) or Of(built parameters).
// MenuToolBudget (this file)
//   Result                   Off, On, Exact, Unavailable, Note (VRCFury cannot fit the avatar, or is set to Fail), Compressed.
//   Estimate(scan)           Off: the scan's counts. On: nothing changes at or under MenuToolCompressor.MaxCost; over it,
//                            VRCFury's decision: bits FinalCost, synced less the compressed plus index bits and slots, total
//                            plus index bits, slots and (more than one batch) a local latch per compressed parameter.
//   Exact(avatar)            Two isolated copies (Build): VRCFury's compressor setting (MenuToolCompressor.SettingKey) at Fail,
//                            then Compress, restored after each, built by VRCBuildPipelineCallbacks.OnPreprocessAvatar (by
//                            reflection), parameters
//                            read, copies deleted (their undo records cleared first, ForgetUndo), all in a temporary additive
//                            scene closed unsaved (the active scene when an untitled scene is open). Not in Play Mode.
//                            Compressed: synced without the compressor, not synced with it.
//                            Unavailable says why it could not run. A prefab asset is placed in the scene for it, then removed.
//   Bits / SyncedFloats      Per menu entry (its subtree), each parameter once.
// MenuToolCompressor         VRCFury's Parameter Compressor solver: SettingKey "com.vrcfury.parameterCompressor" (Compress 0,
//                            Ask 1, Fail 2; Setting()), MaxCost (SDK MAX_PARAMETER_COST, 256 if broken, less 19 with
//                            DexProtect), Attempts (control type sets, in VRCFury's order), Priority (a parameter used by several
//                            types counts as the last), MenuTypes (GetParamsUsedInMenu), Solve (eligible: synced, standard
//                            encoding, used by an allowed type, not Add-driven unless four-axis puppets are allowed; first set
//                            that fits, a later one only if it halves the sync time; null when none fits). Decision: Compress,
//                            NumberSlots, BoolSlots, BatchCount, IndexBits, FinalCost (original + index bits + 8 per number slot
//                            + 1 per bool slot - compressed bits), Optimize (widens slots up to MaxCost, keeping batches level).
//   AnimatorOnly             Synced parameters no menu control uses.
