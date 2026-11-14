using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Builds the assembled expression menu of an avatar, or of a standalone prefab, without changing anything: the
    /// descriptor's menu and parameters, Modular Avatar menu installers, items, groups and parameters, and VRCFury Toggle,
    /// Puppet and Full Controller features, then VRCFury's Move Menu Item, Reorder Menu Item and Set Icon rules as the
    /// build would apply them, with VRCFury's menu path rules (MenuToolVrcfuryMenu). Modular Avatar and VRCFury are read by
    /// type name, so neither is needed to compile. VRCFury renames most parameters at build ("VF##_name"); those are shown
    /// as VF_name and marked Estimated.
    /// </summary>
    internal static partial class MenuToolScanner
    {
        internal const int MaxControls = 8;
        internal const string BaseBlock = "Base avatar";
        private const string VrcfuryType = "VF.Model.VRCFury";
#if RIRYTOOLS_MENUTOOL_MA
        private const string MaInstallerType = "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";
        private const string MaItemType = "nadena.dev.modular_avatar.core.ModularAvatarMenuItem";
        private const string MaGroupType = "nadena.dev.modular_avatar.core.ModularAvatarMenuGroup";
        private const string MaInstallTargetType = "nadena.dev.modular_avatar.core.ModularAvatarMenuInstallTarget";
        private const string MaParametersType = "nadena.dev.modular_avatar.core.ModularAvatarParameters";
        private const string MaMergeAnimatorType = "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator";
#endif
        // VRCFury components that add an SPS menu this scan does not model.
        // VRCFury features that add menu items this scan does not model.
        private static readonly string[] VrcfuryUnmodelled = { "TPSIntegration", "TPSIntegration2", "OGBIntegration", "OGBIntegration2", "ZawooIntegration", "Modes" };

        // VRChat's own parameters: not custom expression parameters, never counted.
        internal static readonly HashSet<string> BuiltIn = new HashSet<string>
        {
            "IsLocal", "PreviewMode", "Viseme", "Voice", "GestureLeft", "GestureRight", "GestureLeftWeight", "GestureRightWeight",
            "AngularY", "VelocityX", "VelocityY", "VelocityZ", "VelocityMagnitude", "Upright", "Grounded", "Seated", "AFK",
            "TrackingType", "VRMode", "MuteSelf", "InStation", "Earmuffs", "IsOnFriendsList", "AvatarVersion", "IsAnimatorEnabled",
            "ScaleModified", "ScaleFactor", "ScaleFactorInverse", "EyeHeightAsMeters", "EyeHeightAsPercent"
        };

        // Parameters VRCFury never renames (FullControllerBuilder.VRChatGlobalParams).
        private static readonly HashSet<string> VrcfuryGlobals = new HashSet<string>(BuiltIn) { "UserEyeTracking", "UserFaceTracking" };

        private sealed class Context
        {
            internal MenuToolScan Scan;
            internal GameObject Root;
#if RIRYTOOLS_MENUTOOL_MA
            internal readonly HashSet<Component> InstalledByTarget = new HashSet<Component>();
            internal readonly List<(string Name, Component Item, bool Synced, bool Saved, bool IsDefault, MenuToolEntry Entry)> MaAuto =
                new List<(string, Component, bool, bool, bool, MenuToolEntry)>();
#endif
            internal readonly List<MenuToolVrcfuryMenuRule> VrcfRules = new List<MenuToolVrcfuryMenuRule>();
            // VRCFury SPS: its options feature (the first), and the sockets and plugs, in hierarchy order.
            internal SerializedProperty SpsOptions;
            internal Component SpsOptionsComponent;
            internal readonly List<Component> Sockets = new List<Component>();
            internal readonly List<Component> Plugs = new List<Component>();
        }

        /// <summary>Scans the avatar the target belongs to, or the target alone when no avatar descriptor is above it.</summary>
        internal static MenuToolScan Scan(GameObject target)
        {
            var scan = new MenuToolScan { Target = target };
            if (target == null) return scan;
            scan.Avatar = DescriptorAbove(target.transform);
            scan.Standalone = scan.Avatar == null;
            scan.VrcfuryInstalled = RiryToolsTypes.Named(VrcfuryType) != null;
#if RIRYTOOLS_MENUTOOL_MA
            scan.ModularAvatarInstalled = RiryToolsTypes.Named(MaInstallerType) != null;
#endif
            var context = new Context { Scan = scan, Root = scan.Avatar != null ? scan.Avatar.gameObject : target };
            if (scan.Standalone) scan.Note("No avatar descriptor above " + target.name + ": its own contributions are shown; where they land and what they cost depend on the avatar it is added to.");

            AddNative(context);
            Component[] components = AllComponents(context.Root);
#if RIRYTOOLS_MENUTOOL_MA
            AddModularAvatar(context, components);
#endif
            AddVrcfury(context, components);
            MenuToolVrcfuryMenu.Apply(scan, context.VrcfRules, rule => Maker(rule.Component, BlockOf(rule.Component, context.Root)));
            NoteReferences(context, components);
            CollectParameterUse(context);
            AddControllers(context, components);
            CheckCapacity(scan, scan.Root);
            return scan;
        }

        internal static VRCAvatarDescriptor DescriptorAbove(Transform transform)
        {
            for (Transform at = transform; at != null; at = at.parent)
            {
                var descriptor = at.GetComponent<VRCAvatarDescriptor>();
                if (descriptor != null) return descriptor;
            }
            return null;
        }

        // Every component under the root, inactive ones included, without missing scripts or EditorOnly objects (which
        // VRChat strips from the build).
        private static Component[] AllComponents(GameObject root)
        {
            return root.GetComponentsInChildren<Component>(true).Where(component => component != null && !EditorOnly(component.transform, root.transform)).ToArray();
        }

        private static bool EditorOnly(Transform transform, Transform root)
        {
            for (Transform at = transform; at != null; at = at.parent)
            {
                if (at.CompareTag("EditorOnly")) return true;
                if (at == root) break;
            }
            return false;
        }

        private static bool Is(Component component, string fullName)
        {
            return component != null && component.GetType().FullName == fullName;
        }

        /// <summary>The content block of a component: the installed prefab it is part of, or its own object's name.</summary>
        internal static string BlockOf(Component component, GameObject root)
        {
            GameObject prefab = PrefabUtility.GetOutermostPrefabInstanceRoot(component.gameObject);
            // An avatar that is itself a prefab instance: the nearest prefab inside it is the installed one.
            if (prefab == root) prefab = NearestPrefabBelow(component.transform, root.transform) ?? null;
            return prefab != null && prefab != root ? "Prefab: " + prefab.name : "Feature: " + component.gameObject.name;
        }

        private static GameObject NearestPrefabBelow(Transform transform, Transform root)
        {
            GameObject found = null;
            for (Transform at = transform; at != null && at != root; at = at.parent)
            {
                if (PrefabUtility.IsAnyPrefabInstanceRoot(at.gameObject)) found = at.gameObject;
            }
            return found;
        }

        private static void Block(MenuToolScan scan, string block)
        {
            if (!scan.Blocks.Contains(block)) scan.Blocks.Add(block);
        }

        internal static bool IsUnder(MenuToolEntry entry, MenuToolEntry ancestor)
        {
            for (MenuToolEntry at = entry.Parent; at != null; at = at.Parent)
            {
                if (at == ancestor) return true;
            }
            return false;
        }

        // ---- References and parameters ------------------------------------------

        // Other scripts referencing menu or parameter assets: references, not confirmed contributions.
        private static void NoteReferences(Context context, Component[] components)
        {
            foreach (Component component in components)
            {
                if (!(component is MonoBehaviour)) continue;
                string type = component.GetType().FullName ?? "";
                if (type.StartsWith("VRC.", StringComparison.Ordinal) || type.StartsWith("VF.", StringComparison.Ordinal) ||
                    type.StartsWith("nadena.dev.", StringComparison.Ordinal)) continue;
                SerializedProperty property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object value = property.objectReferenceValue;
                    if (value is VRCExpressionsMenu || value is VRCExpressionParameters)
                        context.Scan.Note(component.GetType().Name + " on " + component.gameObject.name + " references " + value.name +
                            " (a reference, not a confirmed contribution: unsupported generator).");
                }
            }
        }

        private static void Declare(MenuToolScan scan, string name, ValueType type, bool synced, bool saved, float defaultValue, string block,
            UnityEngine.Object source, bool estimated, int width = 8, bool flipped = false)
        {
            if (BuiltIn.Contains(name)) return;
            MenuToolParamEntry entry = scan.Param(name);
            if (entry == null)
            {
                entry = new MenuToolParamEntry { Name = name, Type = type, Synced = synced, Saved = saved, Default = defaultValue, Estimated = estimated, Width = width, Flipped = flipped };
                scan.Params[name] = entry;
            }
            else if (!entry.Declared)
            {
                entry.Type = type;
                entry.Synced = synced;
                entry.Width = width;
                entry.Flipped = flipped;
            }
            else
            {
                // Declared again: synced if any declaration is (VRChat keeps one entry; the builders merge them).
                entry.Synced |= synced;
            }
            entry.Declared = true;
            entry.Estimated &= estimated;
            entry.DeclaredTypes.Add(type);
            if (!entry.Blocks.Contains(block)) entry.Blocks.Add(block);
            if (source != null && !entry.Sources.Contains(source)) entry.Sources.Add(source);
        }

        // An Int's or Float's synced width as the VRChat SDK counts it (2 to 8 bits; SDK 3.10.6 and later), else 8.
        internal static int WidthOf(VRCExpressionParameters.Parameter parameter)
        {
#if RIRYTOOLS_VRC_BIT_WIDTH
            return parameter.valueType == ValueType.Bool ? 1 : parameter.GetBitWidthByType();
#else
            return 8;
#endif
        }

        internal static bool FlippedOf(VRCExpressionParameters.Parameter parameter)
        {
#if RIRYTOOLS_VRC_BIT_WIDTH
            return parameter.valueType != ValueType.Bool && parameter.flippedRepresentation;
#else
            return false;
#endif
        }

        private static void CollectParameterUse(Context context)
        {
            foreach (MenuToolEntry entry in context.Scan.Root.Descendants())
            {
                foreach (string name in entry.Parameters())
                {
                    if (BuiltIn.Contains(name)) continue;
                    MenuToolParamEntry parameter = context.Scan.Param(name);
                    if (parameter == null)
                    {
                        // Used by a menu but not declared: it works locally only, and costs nothing.
                        parameter = new MenuToolParamEntry { Name = name, Type = entry.Type == ControlType.RadialPuppet || entry.SubParameters.Length > 0
                            ? ValueType.Float : ValueType.Int, Synced = false, Estimated = entry.Estimated };
                        context.Scan.Params[name] = parameter;
                        context.Scan.Note("The menu item " + entry.Path + " uses " + name + ", which is not an expression parameter (it only works locally).");
                    }
                    if (!parameter.UsedBy.Contains(entry)) parameter.UsedBy.Add(entry);
                    if (!parameter.Blocks.Contains(entry.Block)) parameter.Blocks.Add(entry.Block);
                }
            }
        }

        // ---- Animator controllers -------------------------------------------------

        private static void AddControllers(Context context, Component[] components)
        {
            VRCAvatarDescriptor avatar = context.Scan.Avatar;
            if (avatar != null)
            {
                foreach (VRCAvatarDescriptor.CustomAnimLayer layer in avatar.baseAnimationLayers.Concat(avatar.specialAnimationLayers ?? new VRCAvatarDescriptor.CustomAnimLayer[0]))
                {
                    var controller = layer.animatorController as AnimatorController;
                    if (layer.isDefault || controller == null) continue;
                    context.Scan.Controllers.Add(Usage("Avatar " + layer.type + " (" + controller.name + ")", controller, avatar, null));
                }
            }
#if RIRYTOOLS_MENUTOOL_MA
            foreach (Component merge in components.Where(c => Is(c, MaMergeAnimatorType)))
            {
                var controller = Field(merge, "animator") as AnimatorController;
                if (controller != null) context.Scan.Controllers.Add(Usage("MA Merge Animator: " + controller.name + " (" + merge.gameObject.name + ")", controller, merge, null));
            }
#endif
        }

        private static MenuToolControllerUsage Usage(string name, AnimatorController controller, UnityEngine.Object source, Func<string, string> rename)
        {
            var usage = new MenuToolControllerUsage { Name = name, Controller = controller, Source = source };
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                var names = new HashSet<string>();
                var added = new HashSet<string>();
                if (layer.stateMachine != null) Collect(layer.stateMachine, names, added, new HashSet<AnimatorStateMachine>());
                var entry = new MenuToolLayerUsage { Name = layer.name };
                entry.Parameters.AddRange(names.Select(n => rename != null ? rename(n) : n).Where(n => !BuiltIn.Contains(n)).OrderBy(n => n));
                usage.Layers.Add(entry);
                usage.AddDriven.UnionWith(added.Select(n => rename != null ? rename(n) : n));
            }
            return usage;
        }

        // added: the parameters Parameter Drivers change with Add.
        private static void Collect(AnimatorStateMachine machine, HashSet<string> names, HashSet<string> added, HashSet<AnimatorStateMachine> seen)
        {
            if (machine == null || !seen.Add(machine)) return;
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions) Conditions(transition, names);
            foreach (AnimatorTransition transition in machine.entryTransitions) Conditions(transition, names);
            Behaviours(machine.behaviours, names, added);
            foreach (ChildAnimatorState child in machine.states)
            {
                AnimatorState state = child.state;
                if (state == null) continue;
                foreach (AnimatorStateTransition transition in state.transitions) Conditions(transition, names);
                if (state.speedParameterActive) names.Add(state.speedParameter);
                if (state.timeParameterActive) names.Add(state.timeParameter);
                if (state.mirrorParameterActive) names.Add(state.mirrorParameter);
                if (state.cycleOffsetParameterActive) names.Add(state.cycleOffsetParameter);
                Motion(state.motion, names, new HashSet<BlendTree>());
                Behaviours(state.behaviours, names, added);
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                foreach (AnimatorTransition transition in machine.GetStateMachineTransitions(child.stateMachine)) Conditions(transition, names);
                Collect(child.stateMachine, names, added, seen);
            }
        }

        private static void Conditions(AnimatorTransitionBase transition, HashSet<string> names)
        {
            if (transition == null) return;
            foreach (AnimatorCondition condition in transition.conditions) names.Add(condition.parameter);
        }

        private static void Motion(UnityEngine.Motion motion, HashSet<string> names, HashSet<BlendTree> seen)
        {
            var tree = motion as BlendTree;
            if (tree == null || !seen.Add(tree)) return;
            if (tree.blendType == BlendTreeType.Direct)
            {
                foreach (ChildMotion child in tree.children) names.Add(child.directBlendParameter);
            }
            else
            {
                names.Add(tree.blendParameter);
                if (tree.blendType != BlendTreeType.Simple1D) names.Add(tree.blendParameterY);
            }
            foreach (ChildMotion child in tree.children) Motion(child.motion, names, seen);
        }

        private static void Behaviours(StateMachineBehaviour[] behaviours, HashSet<string> names, HashSet<string> added)
        {
            foreach (StateMachineBehaviour behaviour in behaviours)
            {
                var driver = behaviour as VRC_AvatarParameterDriver;
                if (driver == null) continue;
                foreach (VRC_AvatarParameterDriver.Parameter parameter in driver.parameters)
                {
                    if (!string.IsNullOrEmpty(parameter.name) && parameter.type == VRC_AvatarParameterDriver.ChangeType.Add) added.Add(parameter.name);
                    if (!string.IsNullOrEmpty(parameter.name)) names.Add(parameter.name);
                    if (!string.IsNullOrEmpty(parameter.source)) names.Add(parameter.source);
                }
            }
        }

        private static void CheckCapacity(MenuToolScan scan, MenuToolEntry menu)
        {
            if (menu.Children.Count > MaxControls)
            {
                string where = menu.Parent == null ? "The root menu" : menu.Path;
                scan.Note(scan.VrcfuryInstalled
                    ? where + " has " + menu.Children.Count + " controls; VRCFury pages the rest into a Next submenu."
                    : where + " has " + menu.Children.Count + " controls; VRChat shows " + MaxControls + " at most.");
            }
            foreach (MenuToolEntry child in menu.Children.Where(c => c.IsSubMenu)) CheckCapacity(scan, child);
        }

        // ---- Reflection and serialized helpers -------------------------------------

        internal static object Field(object target, string name)
        {
            return target == null ? null : FieldOf(target, name);
        }

        private static object FieldOf(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field.GetValue(target);
            }
            return null;
        }

        private static bool BoolOf(object target, string name)
        {
            return FieldOf(target, name) is bool value && value;
        }

        private static bool Bool(Component component, string name, bool fallback)
        {
            return FieldOf(component, name) is bool value ? value : fallback;
        }

        private static string Str(SerializedProperty parent, string name)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            return property != null && property.propertyType == SerializedPropertyType.String ? property.stringValue : "";
        }

        private static bool Bool(SerializedProperty parent, string name)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
        }

        private static float Float(SerializedProperty parent, string name)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            return property != null && property.propertyType == SerializedPropertyType.Float ? property.floatValue : 0f;
        }

        // A VRCFury GuidWrapper field (icon, menu, parameters, controller): its object reference.
        private static UnityEngine.Object Ref(SerializedProperty parent, string name)
        {
            SerializedProperty property = parent.FindPropertyRelative(name + ".objRef");
            return property != null ? property.objectReferenceValue : null;
        }

        private static IEnumerable<T> Refs<T>(SerializedProperty list, string field) where T : UnityEngine.Object
        {
            if (list == null || !list.isArray) yield break;
            for (int i = 0; i < list.arraySize; i++)
            {
                var value = Ref(list.GetArrayElementAtIndex(i), field) as T;
                if (value != null) yield return value;
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool scanner
// MenuToolScanner is one partial class: MenuToolScanner.cs (the scan, references, parameters and controllers) and MenuToolScanner.Native.cs, .ModularAvatar.cs and .VRCFury.cs. It builds a MenuToolScan (MenuToolModel.cs) and is read-only.
// ============================================================================
//
// MenuToolScanner (this part)
//   Scan(target)             The avatar above the target (DescriptorAbove), or the target alone (Standalone). Native menu and
//                            parameters, then Modular Avatar, then VRCFury, then VRCFury's rules in order, references,
//                            parameter use, animator controllers, capacity notes.
//   WidthOf / FlippedOf      A parameter's synced width and flipped flag (SDK 3.10.6: GetBitWidthByType, flippedRepresentation;
//                            RIRYTOOLS_VRC_BIT_WIDTH), else 8 / false.
//   NoteReferences           Other scripts' references to menu or parameter assets: noted as references, not contributions.
//   Declare                  Adds or merges a MenuToolParamEntry (built-in VRChat parameters skipped).
//   CollectParameterUse      UsedBy per parameter; undeclared ones noted (local only).
//   AddControllers / Usage / Collect  Descriptor layers and MA Merge Animators: per layer, the parameters conditions, blend
//                            trees, state parameters and VRC Avatar Parameter Drivers use; AddDriven, those a driver Adds to.
//   CheckCapacity            More than MaxControls (8) controls: VRCFury pages them, otherwise VRChat shows 8.
//   IsUnder(entry, ancestor) An entry is inside that submenu (also used by MenuToolPlan).
