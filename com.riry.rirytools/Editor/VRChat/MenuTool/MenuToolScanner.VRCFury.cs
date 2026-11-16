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
                string type = component.GetType().FullName;
                if (type == VrcfurySocketType) context.Sockets.Add(component);
                else if (type == VrcfuryPlugType) context.Plugs.Add(component);
                if (!Is(component, VrcfuryType)) continue;
                var serialized = new SerializedObject(component);
                foreach (SerializedProperty feature in Features(serialized)) AddVrcfuryFeature(context, component, feature);
            }
            // VRCFury adds the SPS menu after the features (BakeHapticSocketsService, BakeHapticPlugsService).
            AddVrcfurySps(context);
        }

        // ---- VRCFury SPS ----------------------------------------------------------

        private const string VrcfurySocketType = "VF.Component.VRCFuryHapticSocket";
        private const string VrcfuryPlugType = "VF.Component.VRCFuryHapticPlug";

        // What VRCFury takes out of an object's name for a socket's menu name (HapticUtils.NormalizeName), in its order.
        private static readonly string[] SpsNameNoise =
        {
            "ezdps_([a-z][a-z]?_?)?", "dps", "sps", "gameobject", "object", "armature", "tps", "haptic", "socket", "plug", "ring",
            "hole", "mesh", @"\(\d+\)", "orifice", "oriface", "penetrator", "penetrater"
        };

        /// <summary>
        /// The SPS menu VRCFury builds: SPS Options' menu path ("SPS" when empty) with a toggle per socket that has Add Menu
        /// Item, and its options (Auto Mode, Stealth Mode, Legacy Compatibility, and the plugs' Disable Depth Pass, Disable
        /// Realtime Shadows, DPS Tip Light), in an Options submenu when there are socket toggles.
        /// </summary>
        private static void AddVrcfurySps(Context context)
        {
            if (context.Sockets.Count == 0 && context.Plugs.Count == 0) return;
            Component owner = context.SpsOptionsComponent ?? (context.Sockets.Count > 0 ? context.Sockets[0] : context.Plugs[0]);
            string block = BlockOf(owner, context.Root);
            Block(context.Scan, block);
            string menuPath = context.SpsOptions != null ? Str(context.SpsOptions, "menuPath") : "";
            if (string.IsNullOrWhiteSpace(menuPath)) menuPath = "SPS";
            bool saved = context.SpsOptions != null && Bool(context.SpsOptions, "saveSockets");
            var sockets = context.Sockets.Select(s => new SerializedObject(s)).ToList();
            List<SerializedObject> listed = sockets.Where(s => SoBool(s, "addMenuItem")).ToList();
            string optionsPath = listed.Count > 0 ? menuPath + "/<b>Options" : menuPath;

            if (listed.Any(s => SoBool(s, "enableAuto")))
                AddSpsToggle(context, owner, block, optionsPath + "/<b>Auto Mode<\\/b>\n<size=20>Activates hole nearest to a VRCFury plug", "autoMode", false, saved, false);
            if (listed.Count > 0)
                AddSpsToggle(context, owner, block, optionsPath + "/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", "stealth", true, saved, false);
            if (listed.Any(s => SoBool(s, "useLights")))
                AddSpsToggle(context, owner, block, optionsPath + "/<b>Legacy Compatibility<\\/b>\n<size=20>DPS \\/ TPS \\/ SPS1\nOne socket at a time", "legacy", true, saved,
                    context.SpsOptions == null || Bool(context.SpsOptions, "legacyModeEnabledOnAvatarLoad"));

            var usedNames = new HashSet<string>();
            var usedOsc = new HashSet<string>();
            for (int i = 0; i < sockets.Count; i++)
            {
                string preferred = sockets[i].FindProperty("name")?.stringValue;
                if (string.IsNullOrWhiteSpace(preferred)) preferred = SpsFallbackName(context.Sockets[i].transform);
                string menuName = MakeUnique(usedNames, preferred);
                string osc = sockets[i].FindProperty("oscId")?.stringValue;
                string oscId = MakeUnique(usedOsc, string.IsNullOrWhiteSpace(osc) ? menuName : osc);
                if (!SoBool(sockets[i], "addMenuItem")) continue;
                MenuToolEntry entry = AddSpsToggle(context, context.Sockets[i], BlockOf(context.Sockets[i], context.Root), menuPath + "/" + menuName, oscId, true, saved, false);
                entry.Icon = sockets[i].FindProperty("menuIcon.objRef")?.objectReferenceValue as Texture2D;
            }

            var plugs = context.Plugs.Select(p => new SerializedObject(p)).ToList();
            if (plugs.Any(p => SoBool(p, "enableSps")))
            {
                AddSpsToggle(context, owner, block, optionsPath + "/<b>Disable Depth Pass<\\/b>\n<size=20>(May fix 'ghost' plugs in some worlds)", "disableDepth", true, true, false);
                AddSpsToggle(context, owner, block, optionsPath + "/<b>Disable Realtime Shadows<\\/b>\n<size=20>(May fix 'ghost' plugs in some worlds)", "disableRealtimeShadows", true, true, false);
            }
            if (plugs.Any(p => SoBool(p, "addDpsTipLight")))
                AddSpsToggle(context, owner, block, optionsPath + "/<b>DPS Tip Light<\\/b>\n<size=20>Allows plugs to trigger old DPS animations", "tipLight", true, false, false);

            if (context.SpsOptions != null)
            {
                MenuToolEntry folder = MenuToolVrcfuryMenu.Folder(context.Scan.Root, MenuToolVrcfuryMenu.Split(menuPath), false, null);
                var icon = Ref(context.SpsOptions, "menuIcon") as Texture2D;
                if (folder != null && icon != null) folder.Icon = icon;
            }
        }

        // One SPS toggle at a VRCFury menu path, with VRCFury's unique parameter (estimated) declared.
        private static MenuToolEntry AddSpsToggle(Context context, Component component, string block, string path, string parameterName, bool synced, bool saved, bool on)
        {
            MenuToolEntry parent = ParentOf(context, component, block, path, out string leaf);
            string parameter = Unique(parameterName);
            var entry = new MenuToolEntry
            {
                Name = leaf, Type = ControlType.Toggle, Parameter = parameter, Origin = MenuToolOrigin.VRCFury, Source = component, Block = block,
                VrcfComponent = component, VrcfPath = path, Estimated = true
            };
            parent.Add(entry);
            if (synced) Declare(context.Scan, parameter, ValueType.Bool, true, saved, on ? 1f : 0f, block, component, true);
            return entry;
        }

        // The first name up the hierarchy that is not empty once VRCFury's noise words are taken out (GetFallbackId).
        private static string SpsFallbackName(Transform transform)
        {
            for (Transform t = transform; t != null; t = t.parent)
            {
                string name = t.name;
                foreach (string noise in SpsNameNoise) name = Regex.Replace(name, noise, "", RegexOptions.IgnoreCase);
                name = name.Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            return "Unknown";
        }

        // VRCFury's MakeUnique: the name, else name_2, name_3, ...
        private static string MakeUnique(HashSet<string> used, string name)
        {
            string unique = name;
            for (int i = 2; used.Contains(unique); i++) unique = name + "_" + i;
            used.Add(unique);
            return unique;
        }

        private static bool SoBool(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
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
                case "SpsOptions":
                    if (context.SpsOptions == null)
                    {
                        context.SpsOptions = feature.Copy();
                        context.SpsOptionsComponent = component;
                    }
                    break;
                case "SecurityLock":
                    Block(context.Scan, block);
                    AddVrcfurySecurityLock(context, component, block);
                    break;
                case "OverrideMenuSettings":
                {
                    string next = Str(feature, "nextText");
                    if (!string.IsNullOrEmpty(next)) context.Scan.NextText = next;
                    var nextIcon = Ref(feature, "nextIcon") as Texture2D;
                    if (nextIcon != null) context.Scan.NextIcon = nextIcon;
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

        // Security Lock: a Security submenu with buttons 1 to 7 and Unlocked (8), all on VRCFury's unique SecurityInput Int (not synced).
        private static void AddVrcfurySecurityLock(Context context, Component component, string block)
        {
            string parameter = Unique("SecurityInput");
            for (int i = 1; i <= 8; i++)
            {
                MenuToolEntry parent = ParentOf(context, component, block, "Security/" + (i == 8 ? "Unlocked" : i.ToString()), out string leaf);
                parent.Add(new MenuToolEntry
                {
                    Name = leaf, Type = ControlType.Button, Parameter = parameter, Value = i, Origin = MenuToolOrigin.VRCFury, Source = component,
                    Block = block, VrcfComponent = component, VrcfPath = "Security/" + leaf, Estimated = true
                });
            }
            Declare(context.Scan, Unique("SecurityLockSync"), ValueType.Bool, true, false, 0f, block, component, true);
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
