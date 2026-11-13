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
    internal struct MenuToolBudgetCount
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

        internal static MenuToolBudgetCount Of(IEnumerable<MenuToolParamEntry> parameters)
        {
            var count = new MenuToolBudgetCount();
            foreach (MenuToolParamEntry parameter in parameters.Where(p => p.Declared))
            {
                count.Total++;
                if (parameter.Synced) count.Synced++;
                else count.Local++;
                count.Bits += parameter.Bits;
            }
            return count;
        }

        internal static MenuToolBudgetCount Of(VRCExpressionParameters.Parameter[] parameters)
        {
            var count = new MenuToolBudgetCount();
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
            internal MenuToolBudgetCount Off;
            internal MenuToolBudgetCount On;
            internal bool Exact;
            // Why the exact comparison could not run, or "" when it did (or was not asked for).
            internal string Unavailable = "";
            // What VRCFury would say about the compression (it cannot fit the avatar, or it is set to Fail), or "".
            internal string Note = "";
            internal readonly List<string> Compressed = new List<string>();
        }

        /// <summary>The estimate from a scan: VRCFury's compressor solver on its parameters and assembled menu.</summary>
        internal static Result Estimate(MenuToolScan scan)
        {
            var result = new Result { Off = MenuToolBudgetCount.Of(scan.Params.Values) };
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
                result.Off = MenuToolBudgetCount.Of(off);
                result.On = MenuToolBudgetCount.Of(on);
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
        internal static int Bits(MenuToolScan scan, MenuToolEntry entry)
        {
            return ParametersUnder(entry).Select(scan.Param).Where(p => p != null && p.Declared).Sum(p => p.Bits);
        }

        /// <summary>Synced Floats under a menu entry (each once), for its shading.</summary>
        internal static int SyncedFloats(MenuToolScan scan, MenuToolEntry entry)
        {
            return ParametersUnder(entry).Select(scan.Param).Count(p => p != null && p.Declared && p.Synced && p.Type == ValueType.Float);
        }

        internal static HashSet<string> ParametersUnder(MenuToolEntry entry)
        {
            var names = new HashSet<string>(entry.Parameters());
            foreach (MenuToolEntry below in entry.Descendants()) names.UnionWith(below.Parameters());
            return names;
        }

        /// <summary>Synced parameters no menu control uses: the "Animator only" block.</summary>
        internal static List<MenuToolParamEntry> AnimatorOnly(MenuToolScan scan)
        {
            return scan.Params.Values.Where(p => p.Declared && p.Synced && p.UsedBy.Count == 0).OrderBy(p => p.Name).ToList();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool budget
// VRCFury's compressor solver is in MenuToolCompressor.cs.
// ============================================================================
//
// MenuToolBudgetCount                Total custom parameters, Synced, Local, synced Bits; MaxBits 256 and MaxParameters 8192
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
