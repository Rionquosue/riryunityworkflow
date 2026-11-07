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
                if (parameter.networkSynced) count.Bits += parameter.valueType == ValueType.Bool ? 1 : 8;
            }
            return count;
        }
    }

    /// <summary>
    /// Parameter budget with VRCFury's Parameter Compressor off and on. The estimate follows the compressor's rules: synced
    /// Ints and Floats a menu controls share one 16-bit transport (synced Bools too with Include Bools; two- and four-axis
    /// puppet parameters only with Include Puppets). The exact comparison builds two isolated copies of the avatar with only
    /// the compressor changed, through the VRChat SDK's own build callbacks (as VRCFury's Build Test Copy does), and compares
    /// the parameters each produced. The copies are deleted afterwards; the avatar itself is never changed.
    /// </summary>
    internal static class MenuToolBudget
    {
        internal const int TransportBits = 16;
        private const string CallbacksType = "VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks";
        private const string VrcfuryType = "VF.Model.VRCFury";
        private const string CompressorType = "VF.Model.Feature.UnlimitedParameters";

        internal sealed class Result
        {
            internal BudgetCount Off;
            internal BudgetCount On;
            internal bool Exact;
            // Why the exact comparison could not run, or "" when it did (or was not asked for).
            internal string Unavailable = "";
            internal readonly List<string> Compressed = new List<string>();
        }

        /// <summary>The estimate from a scan.</summary>
        internal static Result Estimate(MenuScan scan, bool includeBools, bool includePuppets)
        {
            var result = new Result { Off = BudgetCount.Of(scan.Params.Values) };
            List<ParamEntry> eligible = scan.Params.Values.Where(p => Eligible(p, includeBools, includePuppets)).ToList();
            result.On = result.Off;
            if (eligible.Count > 0)
            {
                result.On.Bits = result.Off.Bits - eligible.Sum(p => p.Bits) + TransportBits;
                result.Compressed.AddRange(eligible.Select(p => p.Name));
            }
            return result;
        }

        // Synced, controlled by the menu, Int or Float (Bool only with includeBools), not a two- or four-axis puppet's
        // parameter unless includePuppets.
        internal static bool Eligible(ParamEntry parameter, bool includeBools, bool includePuppets)
        {
            if (!parameter.Declared || !parameter.Synced || parameter.UsedBy.Count == 0) return false;
            if (parameter.Type == ValueType.Bool && !includeBools) return false;
            bool puppet = parameter.UsedBy.Any(entry => entry.Type == ControlType.TwoAxisPuppet || entry.Type == ControlType.FourAxisPuppet);
            return !puppet || includePuppets;
        }

        /// <summary>
        /// Builds the avatar twice in isolation, with the Parameter Compressor removed and with it added, and compares
        /// the parameters. Needs VRCFury and an avatar in an open scene (a prefab asset is placed in the scene for it).
        /// </summary>
        internal static Result Exact(VRCAvatarDescriptor avatar, bool includeBools, bool includePuppets)
        {
            var result = new Result();
            Type vrcfury = MenuToolScanner.TypeNamed(VrcfuryType);
            Type compressor = MenuToolScanner.TypeNamed(CompressorType);
            Type callbacks = MenuToolScanner.TypeNamed(CallbacksType);
            MethodInfo preprocess = callbacks != null ? callbacks.GetMethod("OnPreprocessAvatar", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null) : null;
            if (EditorApplication.isPlayingOrWillChangePlaymode) result.Unavailable = "Not available in Play Mode.";
            else if (avatar == null) result.Unavailable = "No avatar descriptor.";
            else if (vrcfury == null || compressor == null) result.Unavailable = "VRCFury is not installed.";
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
                VRCExpressionParameters.Parameter[] off = Build(source, preprocess, vrcfury, compressor, false, includeBools, includePuppets, out string offError);
                EditorUtility.DisplayProgressBar("Menu Tool", "Building " + avatar.name + " with the Parameter Compressor", 0.6f);
                VRCExpressionParameters.Parameter[] on = Build(source, preprocess, vrcfury, compressor, true, includeBools, includePuppets, out string onError);
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

        // One isolated copy: cloned, the compressor set, built by the SDK's callbacks, its parameters read, then deleted.
        private static VRCExpressionParameters.Parameter[] Build(GameObject source, MethodInfo preprocess, Type vrcfury, Type compressor, bool withCompressor,
            bool includeBools, bool includePuppets, out string error)
        {
            error = null;
            GameObject clone = Object.Instantiate(source);
            try
            {
                clone.name = source.name + (withCompressor ? " (RiryTools compression on)" : " (RiryTools compression off)");
                clone.SetActive(true);
                foreach (Component component in clone.GetComponentsInChildren(vrcfury, true))
                {
                    var serialized = new SerializedObject(component);
                    if (MenuToolScanner.Features(serialized).Any(feature => MenuToolScanner.FeatureName(feature) == "UnlimitedParameters"))
                        Object.DestroyImmediate(component);
                }
                if (withCompressor)
                {
                    Component added = clone.AddComponent(vrcfury);
                    var serialized = new SerializedObject(added);
                    SerializedProperty content = serialized.FindProperty("content");
                    content.managedReferenceValue = Activator.CreateInstance(compressor, true);
                    SerializedProperty bools = content.FindPropertyRelative("includeBools");
                    if (bools != null) bools.boolValue = includeBools;
                    SerializedProperty puppets = content.FindPropertyRelative("includePuppets");
                    if (puppets != null) puppets.boolValue = includePuppets;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                bool built = (bool)preprocess.Invoke(null, new object[] { clone });
                var descriptor = clone.GetComponent<VRCAvatarDescriptor>();
                if (!built || descriptor == null || descriptor.expressionParameters == null)
                {
                    error = "The VRChat SDK's build callbacks stopped the build (see the Console).";
                    return null;
                }
                return descriptor.expressionParameters.parameters.Where(p => p != null).Select(p => new VRCExpressionParameters.Parameter
                {
                    name = p.name, valueType = p.valueType, networkSynced = p.networkSynced, saved = p.saved, defaultValue = p.defaultValue
                }).ToArray();
            }
            finally
            {
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
}

// ============================================================================
// GLOSSARY: Menu Tool budget
// ============================================================================
//
// BudgetCount                Total custom parameters, Synced, Local, synced Bits; MaxBits 256 and MaxParameters 8192
//                            (VRChat's limits); BitsLeft / ParametersLeft. Of(scan params) or Of(built parameters).
// MenuToolBudget (this file)
//   Estimate(scan, bools, puppets)  Compressor on: eligible parameters' bits replaced by one TransportBits (16) transport.
//   Eligible                 Declared, synced, used by a menu control; Bool only with Include Bools; two/four-axis puppet
//                            parameters only with Include Puppets.
//   Exact(avatar, ...)       Two isolated copies (Build): compressor removed / added (VRCFury UnlimitedParameters set through
//                            SerializedObject), built by VRCBuildPipelineCallbacks.OnPreprocessAvatar (by reflection), parameters
//                            read, copies deleted (their undo records cleared first, ForgetUndo), all in a temporary additive
//                            scene closed unsaved (the active scene when an untitled scene is open). Not in Play Mode.
//                            Compressed: synced without the compressor, not synced with it.
//                            Unavailable says why it could not run. A prefab asset is placed in the scene for it, then removed.
//   Bits / SyncedFloats      Per menu entry (its subtree), each parameter once.
//   AnimatorOnly             Synced parameters no menu control uses.
