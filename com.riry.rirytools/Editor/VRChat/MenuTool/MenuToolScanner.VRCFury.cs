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
    /// <summary>Menu Tool scan: VRCFury features that add menu items (Toggle, Puppet, Full Controller), read by type name.</summary>
    internal static partial class MenuToolScanner
    {
        // ---- VRCFury -------------------------------------------------------------

        private static void AddVrcfury(Context context, Component[] components)
        {
            if (!context.Scan.VrcfuryInstalled) return;
            foreach (Component component in components)
            {
                if (VrcfurySpsTypes.Contains(component.GetType().FullName))
                    context.Scan.Note("VRCFury SPS on " + component.gameObject.name + " adds an SPS menu; it is not shown.");
                if (!Is(component, VrcfuryType)) continue;
                var serialized = new SerializedObject(component);
                foreach (SerializedProperty feature in Features(serialized)) AddVrcfuryFeature(context, component, feature);
            }
        }

        /// <summary>A VRCFury component's features: its content, and the older config.features list.</summary>
        internal static IEnumerable<SerializedProperty> Features(SerializedObject serialized)
        {
            SerializedProperty content = serialized.FindProperty("content");
            if (content != null && !string.IsNullOrEmpty(content.managedReferenceFullTypename)) yield return content.Copy();
            SerializedProperty list = serialized.FindProperty("config.features");
            if (list == null || !list.isArray) yield break;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.managedReferenceFullTypename)) yield return element.Copy();
            }
        }

        /// <summary>The feature's class name ("Toggle") from "assembly VF.Model.Feature.Toggle".</summary>
        internal static string FeatureName(SerializedProperty feature)
        {
            string full = feature.managedReferenceFullTypename ?? "";
            int dot = full.LastIndexOf('.');
            int space = full.LastIndexOf(' ');
            return full.Substring(Math.Max(dot, space) + 1);
        }

        private static void AddVrcfuryFeature(Context context, Component component, SerializedProperty feature)
        {
            string block = BlockOf(component, context.Root);
            string name = FeatureName(feature);
            switch (name)
            {
                case "Toggle":
                    Block(context.Scan, block);
                    AddVrcfuryToggle(context, component, feature, block);
                    break;
                case "Puppet":
                    Block(context.Scan, block);
                    AddVrcfuryPuppet(context, component, feature, block);
                    break;
                case "FullController":
                    Block(context.Scan, block);
                    AddVrcfuryFullController(context, component, feature, block);
                    break;
                case "MoveMenuItem":
                    context.VrcfRules.Add(new MenuToolVrcfuryMenuRule { Kind = name, Component = component, Path = Str(feature, "fromPath"), To = Str(feature, "toPath") });
                    break;
                case "ReorderMenuItem":
                {
                    SerializedProperty position = feature.FindPropertyRelative("position");
                    context.VrcfRules.Add(new MenuToolVrcfuryMenuRule { Kind = name, Component = component, Path = Str(feature, "path"), Position = position != null ? position.intValue : 0 });
                    break;
                }
                case "SetIcon":
                    context.VrcfRules.Add(new MenuToolVrcfuryMenuRule { Kind = name, Component = component, Path = Str(feature, "path"), Icon = Ref(feature, "icon") as Texture2D });
                    break;
                default:
                    if (VrcfuryUnmodelled.Contains(name))
                        context.Scan.Note("VRCFury " + name + " on " + component.gameObject.name + " may add menu items; they are not shown.");
                    break;
            }
        }

        private static void AddVrcfuryToggle(Context context, Component component, SerializedProperty feature, string block)
        {
            string path = Str(feature, "name");
            if (string.IsNullOrEmpty(path))
            {
                context.Scan.Note("VRCFury Toggle on " + component.gameObject.name + " has no menu path.");
                return;
            }
            bool slider = Bool(feature, "slider");
            MenuToolEntry parent = ParentOf(context, component, block, path, out string leaf);
            // VRCFury's parameter: the global one, else a unique one named after the whole menu path. A VRChat parameter
            // used as the global one is not synced.
            bool global = Bool(feature, "useGlobalParam") && !string.IsNullOrWhiteSpace(Str(feature, "globalParam"));
            string parameter = global ? Str(feature, "globalParam") : Unique(path);
            bool synced = !(Bool(feature, "useGlobalParam") && VrcfuryGlobals.Contains(Str(feature, "globalParam")));
            var entry = new MenuToolEntry
            {
                Name = leaf,
                Type = slider ? ControlType.RadialPuppet : Bool(feature, "holdButton") ? ControlType.Button : ControlType.Toggle,
                Icon = Bool(feature, "enableIcon") ? Ref(feature, "icon") as Texture2D : null,
                Origin = MenuToolOrigin.VRCFury,
                Source = component,
                Block = block,
                VrcfComponent = component,
                VrcfPath = path,
                Estimated = !global
            };
            if (slider) entry.SubParameters = new[] { parameter };
            else entry.Parameter = parameter;
            parent.Add(entry);
            float defaultValue = slider ? Float(feature, "defaultSliderValue") : Bool(feature, "defaultOn") ? 1f : 0f;
            if (synced) Declare(context.Scan, parameter, slider ? ValueType.Float : ValueType.Bool, true, Bool(feature, "saved"), defaultValue, block, component, !global);
        }

        // VRCFury's unique parameter name (UniqueParamService): "VF##_" and the name without an earlier "VF##_"; the number
        // depends on the build, so it is shown as VF_.
        private static string Unique(string name)
        {
            return "VF_" + Regex.Replace(name ?? "", @"^VF\d+_", "");
        }

        private static void AddVrcfuryPuppet(Context context, Component component, SerializedProperty feature, string block)
        {
            string path = Str(feature, "name");
            if (string.IsNullOrEmpty(path)) return;
            // As VRCFury's Puppet: an axis is a parameter only when a stop uses it; a slider is a radial on X, made only then.
            bool usesX = false, usesY = false;
            SerializedProperty stops = feature.FindPropertyRelative("stops");
            for (int i = 0; stops != null && i < stops.arraySize; i++)
            {
                SerializedProperty stop = stops.GetArrayElementAtIndex(i);
                usesX |= Float(stop, "x") != 0f;
                usesY |= Float(stop, "y") != 0f;
            }
            string x = Unique(path + "_x"), y = Unique(path + "_y");
            bool slider = Bool(feature, "slider");
            if (!slider || usesX)
            {
                MenuToolEntry parent = ParentOf(context, component, block, path, out string leaf);
                parent.Add(new MenuToolEntry
                {
                    Name = leaf,
                    Type = slider ? ControlType.RadialPuppet : ControlType.TwoAxisPuppet,
                    Icon = Bool(feature, "enableIcon") ? Ref(feature, "icon") as Texture2D : null,
                    SubParameters = slider ? new[] { x } : new[] { usesX ? x : "", usesY ? y : "" },
                    Origin = MenuToolOrigin.VRCFury,
                    Source = component,
                    Block = block,
                    VrcfComponent = component,
                    VrcfPath = path,
                    Estimated = true
                });
            }
            bool saved = Bool(feature, "saved");
            if (usesX) Declare(context.Scan, x, ValueType.Float, true, saved, Float(feature, "defaultX"), block, component, true);
            if (usesY) Declare(context.Scan, y, ValueType.Float, true, saved, Float(feature, "defaultY"), block, component, true);
        }

        private static void AddVrcfuryFullController(Context context, Component component, SerializedProperty feature, string block)
        {
            Func<string, string> rename = FullControllerRename(feature, out Func<string, bool> renamedUniquely);
            bool ignoreSaved = Bool(feature, "ignoreSaved");
            foreach (VRCExpressionParameters parameters in Refs<VRCExpressionParameters>(feature.FindPropertyRelative("prms"), "parameters"))
            {
                foreach (VRCExpressionParameters.Parameter parameter in parameters.parameters ?? new VRCExpressionParameters.Parameter[0])
                {
                    if (parameter == null || string.IsNullOrEmpty(parameter.name)) continue;
                    Declare(context.Scan, rename(parameter.name), parameter.valueType, parameter.networkSynced, parameter.saved && !ignoreSaved, parameter.defaultValue,
                        block, component, renamedUniquely(parameter.name), WidthOf(parameter), FlippedOf(parameter));
                }
            }
            SerializedProperty menus = feature.FindPropertyRelative("menus");
            if (menus != null)
            {
                for (int i = 0; i < menus.arraySize; i++)
                {
                    SerializedProperty element = menus.GetArrayElementAtIndex(i);
                    var menu = Ref(element, "menu") as VRCExpressionsMenu;
                    if (menu == null)
                    {
                        context.Scan.Note("VRCFury Full Controller on " + component.gameObject.name + " has an empty menu slot.");
                        continue;
                    }
                    List<string> prefix = MenuToolVrcfuryMenu.Split(Str(element, "prefix"));
                    MenuToolEntry parent = MenuToolVrcfuryMenu.Folder(context.Scan.Root, prefix, true, Maker(component, block));
                    AddMenuAsset(context, parent, menu, MenuToolOrigin.VRCFury, component, block, entry =>
                    {
                        entry.VrcfComponent = component;
                        entry.Estimated = entry.Parameters().Any(renamedUniquely);
                        entry.Parameter = rename(entry.Parameter);
                        entry.SubParameters = entry.SubParameters.Select(rename).ToArray();
                    }, new HashSet<VRCExpressionsMenu>());
                }
            }
            SerializedProperty controllers = feature.FindPropertyRelative("controllers");
            if (controllers == null) return;
            for (int i = 0; i < controllers.arraySize; i++)
            {
                var controller = Ref(controllers.GetArrayElementAtIndex(i), "controller") as AnimatorController;
                if (controller != null) context.Scan.Controllers.Add(Usage("VRCFury: " + controller.name, controller, component, rename));
            }
        }

        // VRCFury's Full Controller parameter rule (RewriteParamName): VRChat's own parameters keep their names; with
        // "all non-synced are global", so does any name its parameter files do not declare; with GoGo Loco (its files
        // declare Go/Locomotion and it has a Base controller) every name gets "Go/" in front unless it starts with it;
        // otherwise a name the Global Parameters list matches keeps its name ("Name", "Prefix*", "!" in front to exclude,
        // the last match winning until an exclusion), and the rest get a unique VF##_ name. uniquely says which do.
        private static Func<string, string> FullControllerRename(SerializedProperty feature, out Func<string, bool> uniquely)
        {
            var globals = new List<string>();
            SerializedProperty globalList = feature.FindPropertyRelative("globalParams");
            for (int i = 0; globalList != null && i < globalList.arraySize; i++) globals.Add(globalList.GetArrayElementAtIndex(i).stringValue ?? "");
            bool nonSyncedGlobal = Bool(feature, "allNonsyncedAreGlobal");
            var declared = new HashSet<string>();
            foreach (VRCExpressionParameters parameters in Refs<VRCExpressionParameters>(feature.FindPropertyRelative("prms"), "parameters"))
            {
                foreach (VRCExpressionParameters.Parameter parameter in parameters.parameters ?? new VRCExpressionParameters.Parameter[0])
                {
                    if (parameter != null && parameter.name != null) declared.Add(parameter.name);
                }
            }
            bool hasBase = false;
            SerializedProperty controllers = feature.FindPropertyRelative("controllers");
            for (int i = 0; controllers != null && i < controllers.arraySize; i++)
            {
                SerializedProperty type = controllers.GetArrayElementAtIndex(i).FindPropertyRelative("type");
                if (type != null && type.intValue == (int)VRCAvatarDescriptor.AnimLayerType.Base) hasBase = true;
            }
            bool gogo = hasBase && declared.Contains("Go/Locomotion");
            Func<string, bool> keeps = name =>
            {
                if (string.IsNullOrWhiteSpace(name) || VrcfuryGlobals.Contains(name)) return true;
                if (nonSyncedGlobal && !declared.Contains(name)) return true;
                if (gogo) return true;
                bool global = false;
                foreach (string entry in globals)
                {
                    string pattern = entry;
                    bool negative = pattern.StartsWith("!", StringComparison.Ordinal);
                    if (negative) pattern = pattern.Substring(1);
                    bool wildcard = pattern.EndsWith("*", StringComparison.Ordinal);
                    if (wildcard) pattern = pattern.Substring(0, pattern.Length - 1);
                    if (name == pattern || (wildcard && name.StartsWith(pattern, StringComparison.Ordinal)))
                    {
                        if (negative)
                        {
                            global = false;
                            break;
                        }
                        global = true;
                    }
                }
                return global;
            };
            uniquely = name => !keeps(name);
            return name =>
            {
                if (string.IsNullOrWhiteSpace(name) || VrcfuryGlobals.Contains(name)) return name;
                if (nonSyncedGlobal && !declared.Contains(name)) return name;
                if (gogo) return name.StartsWith("Go/", StringComparison.Ordinal) ? name : "Go/" + name;
                return keeps(name) ? name : Unique(name);
            };
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool scanner: VRCFury
// MenuToolScanner is one partial class: MenuToolScanner.cs (the scan, references, parameters and controllers) and MenuToolScanner.Native.cs, .ModularAvatar.cs and .VRCFury.cs. It builds a MenuToolScan (MenuToolModel.cs) and is read-only.
// ============================================================================
//
// MenuToolScanner (this part)
//   VRCFury                  By type name: "VF.Model.VRCFury" content / config.features (Features, FeatureName). Toggle
//                            (name path, slider, holdButton, globalParam (not synced when a VRChat parameter), saved,
//                            defaultOn, icon; parameter VF_<whole path>), Puppet (x/y floats only for axes a stop uses; slider
//                            = radial on X), FullController (menus with prefix, prms, controllers; FullControllerRename),
//                            MoveMenuItem, ReorderMenuItem and SetIcon as VrcfuryMenuRules (MenuToolVrcfuryMenu.Apply).
//                            Unique names (Unique) are shown as VF_<name>, Estimated. SPS and other unmodelled features are noted.
//   VrcfuryGlobals           Names VRCFury never renames: BuiltIn plus UserEyeTracking, UserFaceTracking (declared in MenuToolScanner.cs).
//   FullControllerRename     VRCFury's RewriteParamName: globals, all-non-synced-are-global (undeclared names), GoGo Loco
//                            ("Go/" prefix), Global Parameters list ("*" prefix, "!" exclusion), else VF_<name>.
