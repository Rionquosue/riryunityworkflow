using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// build would apply them. Modular Avatar and VRCFury are read by type name, so neither is needed to compile.
    /// VRCFury renames most parameters at build ("VF##_name"); those are shown as VF_name and marked Estimated.
    /// </summary>
    internal static class MenuToolScanner
    {
        internal const int MaxControls = 8;
        internal const string BaseBlock = "Base avatar";
        private const string VrcfuryType = "VF.Model.VRCFury";
        private const string MaInstallerType = "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";
        private const string MaItemType = "nadena.dev.modular_avatar.core.ModularAvatarMenuItem";
        private const string MaGroupType = "nadena.dev.modular_avatar.core.ModularAvatarMenuGroup";
        private const string MaInstallTargetType = "nadena.dev.modular_avatar.core.ModularAvatarMenuInstallTarget";
        private const string MaParametersType = "nadena.dev.modular_avatar.core.ModularAvatarParameters";
        private const string MaMergeAnimatorType = "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator";
        // VRCFury components that add an SPS menu this scan does not model.
        private static readonly string[] VrcfurySpsTypes = { "VF.Component.VRCFuryHapticSocket", "VF.Component.VRCFuryHapticPlug" };
        // VRCFury features that add menu items this scan does not model.
        private static readonly string[] VrcfuryUnmodelled = { "SpsOptions", "TPSIntegration", "TPSIntegration2", "OGBIntegration", "OGBIntegration2", "ZawooIntegration", "SecurityLock", "Modes" };

        // VRChat's own parameters: not custom expression parameters, never counted.
        internal static readonly HashSet<string> BuiltIn = new HashSet<string>
        {
            "IsLocal", "PreviewMode", "Viseme", "Voice", "GestureLeft", "GestureRight", "GestureLeftWeight", "GestureRightWeight",
            "AngularY", "VelocityX", "VelocityY", "VelocityZ", "VelocityMagnitude", "Upright", "Grounded", "Seated", "AFK",
            "TrackingType", "VRMode", "MuteSelf", "InStation", "Earmuffs", "IsOnFriendsList", "AvatarVersion", "IsAnimatorEnabled",
            "ScaleModified", "ScaleFactor", "ScaleFactorInverse", "EyeHeightAsMeters", "EyeHeightAsPercent"
        };

        private sealed class Context
        {
            internal MenuScan Scan;
            internal GameObject Root;
            internal readonly HashSet<Component> InstalledByTarget = new HashSet<Component>();
            internal readonly List<(string Name, Component Item, bool Synced, bool Saved, bool IsDefault, MenuEntry Entry)> MaAuto =
                new List<(string, Component, bool, bool, bool, MenuEntry)>();
            internal readonly List<Action> VrcfRules = new List<Action>();
        }

        /// <summary>Scans the avatar the target belongs to, or the target alone when no avatar descriptor is above it.</summary>
        internal static MenuScan Scan(GameObject target)
        {
            var scan = new MenuScan { Target = target };
            if (target == null) return scan;
            scan.Avatar = DescriptorAbove(target.transform);
            scan.Standalone = scan.Avatar == null;
            scan.VrcfuryInstalled = TypeNamed(VrcfuryType) != null;
            scan.ModularAvatarInstalled = TypeNamed(MaInstallerType) != null;
            var context = new Context { Scan = scan, Root = scan.Avatar != null ? scan.Avatar.gameObject : target };
            if (scan.Standalone) scan.Note("No avatar descriptor above " + target.name + ": its own contributions are shown; where they land and what they cost depend on the avatar it is added to.");

            AddNative(context);
            Component[] components = AllComponents(context.Root);
            AddModularAvatar(context, components);
            AddVrcfury(context, components);
            foreach (Action rule in context.VrcfRules) rule();
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

        internal static Type TypeNamed(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
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

        private static void Block(MenuScan scan, string block)
        {
            if (!scan.Blocks.Contains(block)) scan.Blocks.Add(block);
        }

        // ---- Native --------------------------------------------------------------

        private static void AddNative(Context context)
        {
            VRCAvatarDescriptor avatar = context.Scan.Avatar;
            if (avatar == null) return;
            Block(context.Scan, BaseBlock);
            if (!avatar.customExpressions)
            {
                context.Scan.Note(avatar.name + " uses the default expressions (Customize Expressions is off): no menu or parameters of its own.");
                return;
            }
            if (avatar.expressionsMenu == null) context.Scan.Note(avatar.name + " has no Expressions Menu set.");
            else
            {
                context.Scan.Root.SubMenuAsset = avatar.expressionsMenu;
                context.Scan.Root.Source = avatar.expressionsMenu;
                AddMenuAsset(context, context.Scan.Root, avatar.expressionsMenu, MenuOrigin.Native, avatar.expressionsMenu, BaseBlock, null,
                    new HashSet<VRCExpressionsMenu>());
            }
            if (avatar.expressionParameters == null) context.Scan.Note(avatar.name + " has no Expression Parameters set.");
            else
            {
                foreach (VRCExpressionParameters.Parameter parameter in avatar.expressionParameters.parameters ?? new VRCExpressionParameters.Parameter[0])
                {
                    if (parameter == null || string.IsNullOrEmpty(parameter.name)) continue;
                    Declare(context.Scan, parameter.name, parameter.valueType, parameter.networkSynced, parameter.saved, parameter.defaultValue,
                        BaseBlock, avatar.expressionParameters, false);
                }
            }
        }

        /// <summary>
        /// Adds the controls of a menu asset (and its submenus) under parent. edit adjusts each new entry (ownership,
        /// parameter renames); a menu already being expanded is a cycle and is not expanded again.
        /// </summary>
        private static void AddMenuAsset(Context context, MenuEntry parent, VRCExpressionsMenu menu, MenuOrigin origin, UnityEngine.Object source,
            string block, Action<MenuEntry> edit, HashSet<VRCExpressionsMenu> expanding)
        {
            if (menu == null) return;
            if (!expanding.Add(menu))
            {
                context.Scan.Note("The menu " + menu.name + " contains itself through a submenu; it is shown once.");
                return;
            }
            List<VRCExpressionsMenu.Control> controls = menu.controls ?? new List<VRCExpressionsMenu.Control>();
            for (int i = 0; i < controls.Count; i++)
            {
                VRCExpressionsMenu.Control control = controls[i];
                if (control == null) continue;
                MenuEntry entry = FromControl(control, control.name);
                entry.Origin = origin;
                entry.Source = source;
                entry.Block = block;
                entry.OwnerMenu = menu;
                entry.OwnerIndex = i;
                if (edit != null) edit(entry);
                MenuEntry placed = MergeInto(parent, entry, origin == MenuOrigin.VRCFury);
                if (control.type != ControlType.SubMenu) continue;
                if (placed == entry) entry.SubMenuAsset = control.subMenu;
                if (control.subMenu == null) context.Scan.Note("The submenu " + placed.Path + " in " + menu.name + " has no menu asset.");
                else AddMenuAsset(context, placed, control.subMenu, origin, source, block, edit, expanding);
            }
            expanding.Remove(menu);
        }

        internal static MenuEntry FromControl(VRCExpressionsMenu.Control control, string name)
        {
            return new MenuEntry
            {
                Name = name ?? "",
                Type = control.type,
                Icon = control.icon,
                Parameter = control.parameter != null ? control.parameter.name ?? "" : "",
                Value = control.value,
                SubParameters = control.subParameters != null ? control.subParameters.Select(p => p != null ? p.name ?? "" : "").ToArray() : new string[0],
                Labels = control.labels != null ? control.labels.ToArray() : new VRCExpressionsMenu.Control.Label[0]
            };
        }

        // VRCFury merges a submenu into an existing one of the same name (returned: its controls go there); everything
        // else is added as is.
        private static MenuEntry MergeInto(MenuEntry parent, MenuEntry entry, bool mergeSubMenus)
        {
            if (mergeSubMenus && entry.IsSubMenu)
            {
                MenuEntry existing = parent.Children.FirstOrDefault(child => child.IsSubMenu && child.Name == entry.Name);
                if (existing != null) return existing;
            }
            parent.Add(entry);
            return entry;
        }

        /// <summary>The submenu at a path under root, made (as VRCFury makes it) where missing.</summary>
        internal static MenuEntry SubMenuAt(MenuEntry root, string path, MenuOrigin origin, UnityEngine.Object source, string block)
        {
            MenuEntry at = root;
            if (string.IsNullOrEmpty(path)) return at;
            foreach (string part in path.Split('/'))
            {
                if (part.Length == 0) continue;
                MenuEntry next = at.Children.FirstOrDefault(child => child.IsSubMenu && child.Name == part);
                if (next == null)
                {
                    next = new MenuEntry { Name = part, Type = ControlType.SubMenu, Origin = origin, Source = source, Block = block };
                    at.Add(next);
                }
                at = next;
            }
            return at;
        }

        // ---- Modular Avatar ------------------------------------------------------

        private static void AddModularAvatar(Context context, Component[] components)
        {
            if (!context.Scan.ModularAvatarInstalled) return;
            // Installers placed by an Install Target are installed there, not at their own target menu.
            foreach (Component target in components.Where(c => Is(c, MaInstallTargetType)))
            {
                var installer = Field(target, "installer") as Component;
                if (installer != null) context.InstalledByTarget.Add(installer);
            }
            foreach (Component installer in components.Where(c => Is(c, MaInstallerType)))
            {
                if (context.InstalledByTarget.Contains(installer)) continue;
                if (installer is Behaviour behaviour && !behaviour.enabled) continue;
                var targetMenu = Field(installer, "installTargetMenu") as VRCExpressionsMenu;
                MenuEntry parent = context.Scan.Root;
                if (targetMenu != null && context.Scan.Avatar != null && targetMenu != context.Scan.Avatar.expressionsMenu)
                {
                    parent = context.Scan.Root.Descendants().FirstOrDefault(entry => entry.IsSubMenu && entry.SubMenuAsset == targetMenu);
                    if (parent == null)
                    {
                        context.Scan.Note("MA Menu Installer on " + installer.gameObject.name + " installs into " + targetMenu.name +
                            ", which is not in the avatar's menu; shown at the root.");
                        parent = context.Scan.Root;
                    }
                }
                AddInstallerContent(context, parent, installer, new HashSet<GameObject>());
            }
            foreach (Component parameters in components.Where(c => Is(c, MaParametersType))) AddMaParameters(context, parameters);
            AddMaAutoParameters(context);
        }

        private static void AddInstallerContent(Context context, MenuEntry parent, Component installer, HashSet<GameObject> expanding)
        {
            GameObject owner = installer.gameObject;
            string block = BlockOf(installer, context.Root);
            Block(context.Scan, block);
            Component item = owner.GetComponents<Component>().FirstOrDefault(c => Is(c, MaItemType));
            Component group = owner.GetComponents<Component>().FirstOrDefault(c => Is(c, MaGroupType));
            if (item != null) AddMaItem(context, parent, item, installer, expanding);
            else if (group != null) AddMaChildren(context, parent, (Field(group, "targetObject") as GameObject) ?? owner, installer, expanding);
            else
            {
                var menu = Field(installer, "menuToAppend") as VRCExpressionsMenu;
                if (menu == null) context.Scan.Note("MA Menu Installer on " + owner.name + " has nothing to install.");
                else AddMenuAsset(context, parent, menu, MenuOrigin.ModularAvatar, installer, block, entry => entry.MaInstaller = installer,
                    new HashSet<VRCExpressionsMenu>());
            }
        }

        private static void AddMaChildren(Context context, MenuEntry parent, GameObject source, Component installer, HashSet<GameObject> expanding)
        {
            if (source == null || !expanding.Add(source)) return;
            foreach (Transform child in source.transform)
            {
                if (EditorOnly(child, context.Root.transform)) continue;
                Component[] components = child.GetComponents<Component>();
                Component item = components.FirstOrDefault(c => Is(c, MaItemType));
                Component group = components.FirstOrDefault(c => Is(c, MaGroupType));
                Component installTarget = components.FirstOrDefault(c => Is(c, MaInstallTargetType));
                if (item != null) AddMaItem(context, parent, item, installer, expanding);
                else if (group != null) AddMaChildren(context, parent, (Field(group, "targetObject") as GameObject) ?? child.gameObject, installer, expanding);
                else if (installTarget != null && Field(installTarget, "installer") is Component placed) AddInstallerContent(context, parent, placed, expanding);
            }
            expanding.Remove(source);
        }

        private static void AddMaItem(Context context, MenuEntry parent, Component item, Component installer, HashSet<GameObject> expanding)
        {
            var control = Field(item, "Control") as VRCExpressionsMenu.Control;
            if (control == null)
            {
                context.Scan.Note("MA Menu Item on " + item.gameObject.name + " could not be read (Modular Avatar may have changed).");
                return;
            }
            string label = Field(item, "label") as string;
            string block = BlockOf(item, context.Root);
            Block(context.Scan, block);
            MenuEntry entry = FromControl(control, string.IsNullOrEmpty(label) ? item.gameObject.name : label);
            entry.Origin = MenuOrigin.ModularAvatar;
            entry.Source = item;
            entry.Block = block;
            entry.MaItem = item;
            entry.MaInstaller = installer;
            entry.Parameter = Remap(context, item.gameObject, entry.Parameter);
            entry.SubParameters = entry.SubParameters.Select(name => Remap(context, item.gameObject, name)).ToArray();
            parent.Add(entry);

            bool writes = entry.Type == ControlType.Toggle || entry.Type == ControlType.Button || entry.Type == ControlType.RadialPuppet;
            if (writes && string.IsNullOrEmpty(entry.Parameter) && entry.Type != ControlType.RadialPuppet)
            {
                // Modular Avatar gives a toggle or button without a parameter a generated one.
                entry.Parameter = "(auto) " + entry.Name;
                entry.Estimated = true;
            }
            if (!string.IsNullOrEmpty(entry.Parameter))
            {
                context.MaAuto.Add((entry.Parameter, item, Bool(item, "isSynced", true), Bool(item, "isSaved", true), Bool(item, "isDefault", false), entry));
            }

            if (entry.Type != ControlType.SubMenu) return;
            object source = Field(item, "MenuSource");
            bool children = source != null && Convert.ToInt32(source) == 1;
            if (children)
            {
                GameObject from = (Field(item, "menuSource_otherObjectChildren") as GameObject) ?? item.gameObject;
                entry.MaChildrenSource = from;
                AddMaChildren(context, entry, from, installer, expanding);
            }
            else if (control.subMenu != null)
            {
                entry.SubMenuAsset = control.subMenu;
                AddMenuAsset(context, entry, control.subMenu, MenuOrigin.ModularAvatar, item, block, sub => sub.MaInstaller = installer,
                    new HashSet<VRCExpressionsMenu>());
            }
        }

        private static void AddMaParameters(Context context, Component component)
        {
            var list = Field(component, "parameters") as IList;
            if (list == null) return;
            string block = BlockOf(component, context.Root);
            Block(context.Scan, block);
            foreach (object config in list)
            {
                string name = FieldOf(config, "nameOrPrefix") as string;
                if (string.IsNullOrEmpty(name)) continue;
                if (BoolOf(config, "isPrefix"))
                {
                    context.Scan.Note("MA Parameters on " + component.gameObject.name + " has the prefix " + name + "; parameters by prefix are not listed.");
                    continue;
                }
                string remapTo = FieldOf(config, "remapTo") as string;
                string final = !string.IsNullOrEmpty(remapTo) ? remapTo : name;
                Transform above = component.transform.parent;
                if (above != null) final = Remap(context, above.gameObject, final);
                int syncType = Convert.ToInt32(FieldOf(config, "syncType") ?? 0);
                // NotSynced: an animator parameter only, not an expression parameter.
                if (syncType == 0) continue;
                bool internalParameter = BoolOf(config, "internalParameter");
                ValueType type = syncType == 1 ? ValueType.Int : syncType == 2 ? ValueType.Float : ValueType.Bool;
                float defaultValue = Convert.ToSingle(FieldOf(config, "defaultValue") ?? 0f);
                Declare(context.Scan, internalParameter ? final + " (internal)" : final, type, !BoolOf(config, "localOnly"), BoolOf(config, "saved"),
                    defaultValue, block, component, internalParameter);
            }
        }

        // Menu items whose parameter nothing declares: Modular Avatar creates it (Float for a radial puppet, Bool when every
        // item writes 0 or 1, otherwise Int).
        private static void AddMaAutoParameters(Context context)
        {
            foreach (var group in context.MaAuto.GroupBy(auto => auto.Name))
            {
                if (context.Scan.Param(group.Key) != null && context.Scan.Param(group.Key).Declared) continue;
                ValueType type = group.Any(auto => auto.Entry.Type == ControlType.RadialPuppet) ? ValueType.Float
                    : group.All(auto => auto.Entry.Value == 0f || auto.Entry.Value == 1f) ? ValueType.Bool : ValueType.Int;
                var first = group.First();
                var withDefault = group.LastOrDefault(auto => auto.IsDefault);
                float defaultValue = withDefault.Item != null ? withDefault.Entry.Value : 0f;
                Declare(context.Scan, group.Key, type, first.Synced, first.Saved, defaultValue, BlockOf(first.Item, context.Root), first.Item, true);
            }
        }

        // A parameter name after Modular Avatar's remapping by MA Parameters on the object and its ancestors, innermost first.
        private static string Remap(Context context, GameObject from, string name)
        {
            if (string.IsNullOrEmpty(name) || !context.Scan.ModularAvatarInstalled) return name;
            for (Transform at = from.transform; at != null; at = at.parent)
            {
                foreach (Component parameters in at.GetComponents<Component>().Where(c => Is(c, MaParametersType)))
                {
                    var list = Field(parameters, "parameters") as IList;
                    if (list == null) continue;
                    foreach (object config in list)
                    {
                        if (BoolOf(config, "isPrefix") || FieldOf(config, "nameOrPrefix") as string != name) continue;
                        string remapTo = FieldOf(config, "remapTo") as string;
                        if (!string.IsNullOrEmpty(remapTo)) name = remapTo;
                        break;
                    }
                }
                if (at == context.Root.transform) break;
            }
            return name;
        }

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
                {
                    string from = Str(feature, "fromPath"), to = Str(feature, "toPath");
                    context.VrcfRules.Add(() => ApplyMove(context.Scan, component, from, to));
                    break;
                }
                case "ReorderMenuItem":
                {
                    string path = Str(feature, "path");
                    SerializedProperty position = feature.FindPropertyRelative("position");
                    int index = position != null ? position.intValue : 0;
                    context.VrcfRules.Add(() => ApplyReorder(context.Scan, component, path, index));
                    break;
                }
                case "SetIcon":
                {
                    string path = Str(feature, "path");
                    var icon = Ref(feature, "icon") as Texture2D;
                    context.VrcfRules.Add(() =>
                    {
                        MenuEntry entry = context.Scan.Root.Find(path);
                        if (entry != null) entry.Icon = icon;
                        else context.Scan.Note("VRCFury Set Icon on " + component.gameObject.name + ": " + path + " is not in the menu.");
                    });
                    break;
                }
                case "UnlimitedParameters":
                    context.Scan.Compressor = true;
                    context.Scan.CompressorBools |= Bool(feature, "includeBools");
                    context.Scan.CompressorPuppets |= Bool(feature, "includePuppets");
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
            SplitPath(path, out string parentPath, out string leaf);
            MenuEntry parent = SubMenuAt(context.Scan.Root, parentPath, MenuOrigin.VRCFury, component, block);
            bool global = Bool(feature, "useGlobalParam") && !string.IsNullOrEmpty(Str(feature, "globalParam"));
            string parameter = global ? Str(feature, "globalParam") : "VF_" + leaf;
            var entry = new MenuEntry
            {
                Name = leaf,
                Type = slider ? ControlType.RadialPuppet : Bool(feature, "holdButton") ? ControlType.Button : ControlType.Toggle,
                Icon = Bool(feature, "enableIcon") ? Ref(feature, "icon") as Texture2D : null,
                Origin = MenuOrigin.VRCFury,
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
            Declare(context.Scan, parameter, slider ? ValueType.Float : ValueType.Bool, true, Bool(feature, "saved"), defaultValue, block, component, !global);
        }

        private static void AddVrcfuryPuppet(Context context, Component component, SerializedProperty feature, string block)
        {
            string path = Str(feature, "name");
            if (string.IsNullOrEmpty(path)) return;
            SplitPath(path, out string parentPath, out string leaf);
            MenuEntry parent = SubMenuAt(context.Scan.Root, parentPath, MenuOrigin.VRCFury, component, block);
            string x = "VF_" + leaf + "_x", y = "VF_" + leaf + "_y";
            parent.Add(new MenuEntry
            {
                Name = leaf,
                Type = ControlType.TwoAxisPuppet,
                Icon = Bool(feature, "enableIcon") ? Ref(feature, "icon") as Texture2D : null,
                SubParameters = new[] { x, y },
                Origin = MenuOrigin.VRCFury,
                Source = component,
                Block = block,
                VrcfComponent = component,
                VrcfPath = path,
                Estimated = true
            });
            bool saved = Bool(feature, "saved");
            Declare(context.Scan, x, ValueType.Float, true, saved, Float(feature, "defaultX"), block, component, true);
            Declare(context.Scan, y, ValueType.Float, true, saved, Float(feature, "defaultY"), block, component, true);
        }

        private static void AddVrcfuryFullController(Context context, Component component, SerializedProperty feature, string block)
        {
            // Its parameters keep their names when global (listed, "*" for all, or every unsynced one), else VRCFury renames them.
            var globals = new HashSet<string>();
            SerializedProperty globalList = feature.FindPropertyRelative("globalParams");
            if (globalList != null)
            {
                for (int i = 0; i < globalList.arraySize; i++) globals.Add(globalList.GetArrayElementAtIndex(i).stringValue);
            }
            bool allGlobal = globals.Contains("*");
            bool nonSyncedGlobal = Bool(feature, "allNonsyncedAreGlobal");
            bool ignoreSaved = Bool(feature, "ignoreSaved");
            var renames = new Dictionary<string, string>();
            foreach (VRCExpressionParameters parameters in Refs<VRCExpressionParameters>(feature.FindPropertyRelative("prms"), "parameters"))
            {
                foreach (VRCExpressionParameters.Parameter parameter in parameters.parameters ?? new VRCExpressionParameters.Parameter[0])
                {
                    if (parameter == null || string.IsNullOrEmpty(parameter.name)) continue;
                    bool global = allGlobal || globals.Contains(parameter.name) || (nonSyncedGlobal && !parameter.networkSynced);
                    string name = global ? parameter.name : "VF_" + parameter.name;
                    renames[parameter.name] = name;
                    Declare(context.Scan, name, parameter.valueType, parameter.networkSynced, parameter.saved && !ignoreSaved, parameter.defaultValue,
                        block, component, !global);
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
                    string prefix = Str(element, "prefix");
                    MenuEntry parent = SubMenuAt(context.Scan.Root, prefix, MenuOrigin.VRCFury, component, block);
                    AddMenuAsset(context, parent, menu, MenuOrigin.VRCFury, component, block, entry =>
                    {
                        entry.VrcfComponent = component;
                        entry.Estimated = true;
                        entry.Parameter = Rename(renames, entry.Parameter);
                        entry.SubParameters = entry.SubParameters.Select(name => Rename(renames, name)).ToArray();
                    }, new HashSet<VRCExpressionsMenu>());
                }
            }
            SerializedProperty controllers = feature.FindPropertyRelative("controllers");
            if (controllers == null) return;
            for (int i = 0; i < controllers.arraySize; i++)
            {
                var controller = Ref(controllers.GetArrayElementAtIndex(i), "controller") as AnimatorController;
                if (controller != null) context.Scan.Controllers.Add(Usage("VRCFury: " + controller.name, controller, component, renames));
            }
        }

        private static string Rename(Dictionary<string, string> renames, string name)
        {
            string renamed;
            return name != null && renames.TryGetValue(name, out renamed) ? renamed : name;
        }

        private static void ApplyMove(MenuScan scan, Component component, string from, string to)
        {
            MenuEntry entry = scan.Root.Find(from);
            if (entry == null || entry == scan.Root || string.IsNullOrEmpty(to))
            {
                scan.Note("VRCFury Move Menu Item on " + component.gameObject.name + ": " + from + " is not in the menu.");
                return;
            }
            SplitPath(to, out string parentPath, out string leaf);
            GameObject root = scan.Avatar != null ? scan.Avatar.gameObject : scan.Target;
            MenuEntry parent = SubMenuAt(scan.Root, parentPath, MenuOrigin.VRCFury, component, BlockOf(component, root));
            if (parent == entry || IsUnder(parent, entry))
            {
                scan.Note("VRCFury Move Menu Item on " + component.gameObject.name + " moves " + from + " into itself; skipped.");
                return;
            }
            MenuEntry existing = parent.Children.FirstOrDefault(child => child.IsSubMenu && child.Name == leaf);
            if (entry.IsSubMenu && existing != null && existing != entry)
            {
                foreach (MenuEntry child in entry.Children.ToList()) existing.Add(child);
                entry.Parent.Children.Remove(entry);
                return;
            }
            entry.Name = leaf;
            parent.Add(entry);
        }

        private static void ApplyReorder(MenuScan scan, Component component, string path, int position)
        {
            MenuEntry entry = scan.Root.Find(path);
            if (entry == null || entry.Parent == null)
            {
                scan.Note("VRCFury Reorder Menu Item on " + component.gameObject.name + ": " + path + " is not in the menu.");
                return;
            }
            MenuEntry parent = entry.Parent;
            parent.Children.Remove(entry);
            parent.Children.Insert(Mathf.Clamp(position, 0, parent.Children.Count), entry);
        }

        internal static bool IsUnder(MenuEntry entry, MenuEntry ancestor)
        {
            for (MenuEntry at = entry.Parent; at != null; at = at.Parent)
            {
                if (at == ancestor) return true;
            }
            return false;
        }

        internal static void SplitPath(string path, out string parent, out string leaf)
        {
            path = (path ?? "").Trim('/');
            int slash = path.LastIndexOf('/');
            parent = slash < 0 ? "" : path.Substring(0, slash);
            leaf = slash < 0 ? path : path.Substring(slash + 1);
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

        private static void Declare(MenuScan scan, string name, ValueType type, bool synced, bool saved, float defaultValue, string block,
            UnityEngine.Object source, bool estimated)
        {
            if (BuiltIn.Contains(name)) return;
            ParamEntry entry = scan.Param(name);
            if (entry == null)
            {
                entry = new ParamEntry { Name = name, Type = type, Synced = synced, Saved = saved, Default = defaultValue, Estimated = estimated };
                scan.Params[name] = entry;
            }
            else if (!entry.Declared)
            {
                entry.Type = type;
                entry.Synced = synced;
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

        private static void CollectParameterUse(Context context)
        {
            foreach (MenuEntry entry in context.Scan.Root.Descendants())
            {
                foreach (string name in entry.Parameters())
                {
                    if (BuiltIn.Contains(name)) continue;
                    ParamEntry parameter = context.Scan.Param(name);
                    if (parameter == null)
                    {
                        // Used by a menu but not declared: it works locally only, and costs nothing.
                        parameter = new ParamEntry { Name = name, Type = entry.Type == ControlType.RadialPuppet || entry.SubParameters.Length > 0
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
            foreach (Component merge in components.Where(c => Is(c, MaMergeAnimatorType)))
            {
                var controller = Field(merge, "animator") as AnimatorController;
                if (controller != null) context.Scan.Controllers.Add(Usage("MA Merge Animator: " + controller.name + " (" + merge.gameObject.name + ")", controller, merge, null));
            }
        }

        private static ControllerUsage Usage(string name, AnimatorController controller, UnityEngine.Object source, Dictionary<string, string> renames)
        {
            var usage = new ControllerUsage { Name = name, Controller = controller, Source = source };
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                var names = new HashSet<string>();
                if (layer.stateMachine != null) Collect(layer.stateMachine, names, new HashSet<AnimatorStateMachine>());
                var entry = new LayerUsage { Name = layer.name };
                entry.Parameters.AddRange(names.Select(n => renames != null ? Rename(renames, n) : n).Where(n => !BuiltIn.Contains(n)).OrderBy(n => n));
                usage.Layers.Add(entry);
            }
            return usage;
        }

        private static void Collect(AnimatorStateMachine machine, HashSet<string> names, HashSet<AnimatorStateMachine> seen)
        {
            if (machine == null || !seen.Add(machine)) return;
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions) Conditions(transition, names);
            foreach (AnimatorTransition transition in machine.entryTransitions) Conditions(transition, names);
            Behaviours(machine.behaviours, names);
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
                Behaviours(state.behaviours, names);
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                foreach (AnimatorTransition transition in machine.GetStateMachineTransitions(child.stateMachine)) Conditions(transition, names);
                Collect(child.stateMachine, names, seen);
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

        private static void Behaviours(StateMachineBehaviour[] behaviours, HashSet<string> names)
        {
            foreach (StateMachineBehaviour behaviour in behaviours)
            {
                var driver = behaviour as VRC_AvatarParameterDriver;
                if (driver == null) continue;
                foreach (VRC_AvatarParameterDriver.Parameter parameter in driver.parameters)
                {
                    if (!string.IsNullOrEmpty(parameter.name)) names.Add(parameter.name);
                    if (!string.IsNullOrEmpty(parameter.source)) names.Add(parameter.source);
                }
            }
        }

        private static void CheckCapacity(MenuScan scan, MenuEntry menu)
        {
            if (menu.Children.Count > MaxControls)
            {
                string where = menu.Parent == null ? "The root menu" : menu.Path;
                scan.Note(scan.VrcfuryInstalled
                    ? where + " has " + menu.Children.Count + " controls; VRCFury pages the rest into a Next submenu."
                    : where + " has " + menu.Children.Count + " controls; VRChat shows " + MaxControls + " at most.");
            }
            foreach (MenuEntry child in menu.Children.Where(c => c.IsSubMenu)) CheckCapacity(scan, child);
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
// ============================================================================
//
// MenuToolScanner (this file): read-only; builds a MenuScan (MenuToolModel.cs)
//   Scan(target)             The avatar above the target (DescriptorAbove), or the target alone (Standalone). Native menu and
//                            parameters, then Modular Avatar, then VRCFury, then VRCFury's rules in order, references,
//                            parameter use, animator controllers, capacity notes.
//   AddMenuAsset             A menu asset's controls (and submenus), owned (OwnerMenu/OwnerIndex) by that asset; cycles noted.
//   MergeInto                VRCFury merges a submenu into one of the same name; others are appended.
//   SubMenuAt                The submenu at a path, made where missing (VRCFury's behaviour).
//   Modular Avatar           By type name (Ma*Type): Menu Installer (installTargetMenu, menuToAppend, or the Menu Item / Menu
//                            Group on it), Menu Item (Control, label, MenuSource Children / asset, menuSource_otherObjectChildren,
//                            isSynced/isSaved/isDefault), Menu Group (targetObject), Install Target (installer), Parameters
//                            (nameOrPrefix, remapTo, syncType, localOnly, saved, defaultValue, internalParameter), Merge Animator.
//                            Remap applies MA Parameters renames from the object up. Undeclared item parameters are created as
//                            MA would (AddMaAutoParameters).
//   VRCFury                  By type name: "VF.Model.VRCFury" content / config.features (Features, FeatureName). Toggle
//                            (name path, slider, holdButton, globalParam, saved, defaultOn, icon), Puppet (x/y floats),
//                            FullController (menus with prefix, prms, globalParams, allNonsyncedAreGlobal, controllers),
//                            MoveMenuItem (ApplyMove), ReorderMenuItem (ApplyReorder), SetIcon, UnlimitedParameters (Compressor).
//                            Renamed parameters are shown as VF_<name>, Estimated. SPS and other unmodelled features are noted.
//   NoteReferences           Other scripts' references to menu or parameter assets: noted as references, not contributions.
//   Declare                  Adds or merges a ParamEntry (built-in VRChat parameters skipped).
//   CollectParameterUse      UsedBy per parameter; undeclared ones noted (local only).
//   AddControllers / Usage / Collect  Descriptor layers and MA Merge Animators: per layer, the parameters conditions, blend
//                            trees, state parameters and VRC Avatar Parameter Drivers use.
//   CheckCapacity            More than MaxControls (8) controls: VRCFury pages them, otherwise VRChat shows 8.
