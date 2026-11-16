using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace Riry.RiryTools.Editor
{
    /// <summary>Icons for menu rows: the source system's component icon, and a type icon for each control.</summary>
    internal static class MenuToolIcons
    {
        private static readonly Dictionary<string, Texture> ByType = new Dictionary<string, Texture>();

        /// <summary>The icon of the component that contributes the entry, as the Inspector shows it (VRCFury and MA only).</summary>
        internal static Texture Origin(MenuToolEntry entry)
        {
            if (entry.Origin == MenuToolOrigin.Native) return null;
#if RIRYTOOLS_MENUTOOL_MA
            Component component = entry.Origin == MenuToolOrigin.VRCFury ? entry.VrcfComponent ?? entry.Source as Component : entry.MaItem ?? entry.MaInstaller ?? entry.Source as Component;
#else
            Component component = entry.Origin == MenuToolOrigin.VRCFury ? entry.VrcfComponent ?? entry.Source as Component : entry.Source as Component;
#endif
            if (component == null) return null;
            string key = component.GetType().FullName;
            Texture icon;
            if (!ByType.TryGetValue(key, out icon))
            {
                // The installed package's own component icon; nothing is copied from it.
                icon = AssetPreview.GetMiniTypeThumbnail(component.GetType()) ?? EditorGUIUtility.ObjectContent(component, component.GetType()).image;
                ByType[key] = icon;
            }
            return icon;
        }

        internal static string TypeName(ControlType type)
        {
            switch (type)
            {
                case ControlType.SubMenu: return "Submenu";
                case ControlType.Button: return "Button";
                case ControlType.Toggle: return "Toggle";
                case ControlType.RadialPuppet: return "Radial puppet";
                case ControlType.TwoAxisPuppet: return "Two-axis puppet";
                case ControlType.FourAxisPuppet: return "Four-axis puppet";
                default: return type.ToString();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool icons
// ============================================================================
//
// MenuToolIcons (this file)             Origin(entry): the contributing VRCFury or Modular Avatar component's own icon (the installed
//                            package's, looked up per type); TypeName(control type).
