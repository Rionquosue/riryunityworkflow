using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Shared IMGUI helpers.</summary>
    internal static class RiryToolGUI
    {
        /// <summary>
        /// Call after an action that can open a dialog or file panel from a window's OnGUI. A modal
        /// window repaints other windows while it is open, which leaves this window's layout groups
        /// out of step; this ends the current draw (GUIUtility.ExitGUI) so the window starts a
        /// fresh one, instead of logging layout errors and drawing that frame wrongly. Does nothing
        /// outside OnGUI, so it is safe on paths also reached from menus or delayed calls.
        /// Never call it inside a try that catches Exception: ExitGUI works by throwing.
        /// </summary>
        internal static void ExitAfterModal()
        {
            // Event.current is only set while a GUI event is being handled (GUIUtility.guiDepth is internal).
            if (Event.current != null) GUIUtility.ExitGUI();
        }

        /// <summary>
        /// For a window setting drawn as field = Set(this, name, field, EditorGUILayout.X(..., field)):
        /// when the control changed the value, records the owner for Undo first (while it still holds
        /// the old value), then returns the new value to assign. Unchanged, it returns the current value.
        /// </summary>
        internal static T Set<T>(Object owner, string undoName, T current, T edited)
        {
            if (EqualityComparer<T>.Default.Equals(current, edited)) return current;
            if (owner != null) Undo.RecordObject(owner, undoName);
            return edited;
        }
    }
}

// ============================================================================
// GLOSSARY: shared IMGUI helpers
// ============================================================================
//
// RiryToolGUI.Set(owner, name, current, edited)  A window setting's new value, after recording the owner for Undo when it
//                           changed, so Ctrl+Z puts the setting back.
// RiryToolGUI.ExitAfterModal()  After a dialog or file panel opened from OnGUI: GUIUtility.ExitGUI when inside OnGUI
//                           (Event.current is set), so the draw restarts cleanly; nothing elsewhere.
