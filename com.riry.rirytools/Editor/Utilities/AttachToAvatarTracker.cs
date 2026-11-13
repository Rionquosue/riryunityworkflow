using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Remembers the last avatar selected in the Hierarchy: the nearest object above (or at) the
    /// selection with a VRCAvatarDescriptor, else the nearest with a humanoid Animator.
    /// </summary>
    [InitializeOnLoad]
    internal static class AttachToAvatarTracker
    {
        internal const string LastAvatarKey = "RiryTools.AttachTo.LastAvatar";
        // Found by name, so the VRChat SDK is not needed.
        private const string DescriptorTypeName = "VRCAvatarDescriptor";

        static AttachToAvatarTracker()
        {
            Selection.selectionChanged += SelectionChanged;
        }

        internal static GameObject LastAvatar
        {
            get
            {
                int id = SessionState.GetInt(LastAvatarKey, 0);
                return id == 0 ? null : EditorUtility.InstanceIDToObject(id) as GameObject;
            }
        }

        private static void SelectionChanged()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null || EditorUtility.IsPersistent(selected)) return;
            GameObject avatar = AvatarOf(selected.transform);
            if (avatar != null) SessionState.SetInt(LastAvatarKey, avatar.GetInstanceID());
        }

        internal static GameObject AvatarOf(Transform selected)
        {
            for (Transform each = selected; each != null; each = each.parent)
            {
                if (each.GetComponents<Component>().Any(component => component != null && component.GetType().Name == DescriptorTypeName)) return each.gameObject;
            }
            for (Transform each = selected; each != null; each = each.parent)
            {
                Animator animator = each.GetComponent<Animator>();
                if (animator != null && animator.avatar != null && animator.avatar.isHuman) return each.gameObject;
            }
            return null;
        }
    }
}

// ============================================================================
// GLOSSARY: Attach To last avatar
// Attach To (a Utilities tool): the popup is Editor/UI/Utilities/AttachToWindow.cs; the Hierarchy entry is in Editor/UI/RiryToolsRightClickMenu.cs.
// ============================================================================
//
// AttachToAvatarTracker     Keeps the last selected avatar (VRCAvatarDescriptor, else humanoid Animator) in SessionState
//                           "RiryTools.AttachTo.LastAvatar" as an instance ID; LastAvatar reads it.
