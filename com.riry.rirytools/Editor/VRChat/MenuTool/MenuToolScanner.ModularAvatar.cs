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
    /// <summary>Menu Tool scan: Modular Avatar menu installers, items and parameters, read by type name.</summary>
    internal static partial class MenuToolScanner
    {
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
                MenuToolEntry parent = context.Scan.Root;
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

        private static void AddInstallerContent(Context context, MenuToolEntry parent, Component installer, HashSet<GameObject> expanding)
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
                else AddMenuAsset(context, parent, menu, MenuToolOrigin.ModularAvatar, installer, block, entry => entry.MaInstaller = installer,
                    new HashSet<VRCExpressionsMenu>());
            }
        }

        private static void AddMaChildren(Context context, MenuToolEntry parent, GameObject source, Component installer, HashSet<GameObject> expanding)
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

        private static void AddMaItem(Context context, MenuToolEntry parent, Component item, Component installer, HashSet<GameObject> expanding)
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
            MenuToolEntry entry = FromControl(control, string.IsNullOrEmpty(label) ? item.gameObject.name : label);
            entry.Origin = MenuToolOrigin.ModularAvatar;
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
                AddMenuAsset(context, entry, control.subMenu, MenuToolOrigin.ModularAvatar, item, block, sub => sub.MaInstaller = installer,
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
    }
}

// ============================================================================
// GLOSSARY: Menu Tool scanner: Modular Avatar
// MenuToolScanner is one partial class: MenuToolScanner.cs (the scan, references, parameters and controllers) and MenuToolScanner.Native.cs, .ModularAvatar.cs and .VRCFury.cs. It builds a MenuToolScan (MenuToolModel.cs) and is read-only.
// ============================================================================
//
// MenuToolScanner (this part)
//   Modular Avatar           By type name (Ma*Type): Menu Installer (installTargetMenu, menuToAppend, or the Menu Item / Menu
//                            Group on it), Menu Item (Control, label, MenuSource Children / asset, menuSource_otherObjectChildren,
//                            isSynced/isSaved/isDefault), Menu Group (targetObject), Install Target (installer), Parameters
//                            (nameOrPrefix, remapTo, syncType, localOnly, saved, defaultValue, internalParameter), Merge Animator.
//                            Remap applies MA Parameters renames from the object up. Undeclared item parameters are created as
//                            MA would (AddMaAutoParameters).
