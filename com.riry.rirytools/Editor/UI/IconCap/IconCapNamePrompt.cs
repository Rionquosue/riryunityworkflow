using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>A small modal prompt for a name (Save job, Save a preset); null when cancelled.</summary>
    internal sealed class IconCapNamePrompt : EditorWindow
    {
        private string label = "";
        private string value = "";
        private bool accepted;

        internal static string Ask(string title, string label, string value)
        {
            var prompt = CreateInstance<IconCapNamePrompt>();
            prompt.titleContent = new GUIContent(title);
            prompt.label = label;
            prompt.value = value ?? "";
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            prompt.position = new Rect(main.center.x - 180f, main.center.y - 40f, 360f, 70f);
            prompt.minSize = prompt.maxSize = new Vector2(360f, 70f);
            prompt.ShowModalUtility();
            return prompt.accepted ? prompt.value.Trim() : null;
        }

        private void OnGUI()
        {
            bool enter = Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName("Name");
            value = EditorGUILayout.TextField(label, value);
            EditorGUI.FocusTextInControl("Name");
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f))) Close();
                using (new EditorGUI.DisabledScope(value.Trim().Length == 0))
                {
                    if ((GUILayout.Button("Save", GUILayout.Width(80f)) || enter) && value.Trim().Length > 0)
                    {
                        accepted = true;
                        Close();
                    }
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: IconCap name prompt
// ============================================================================
//
// IconCapNamePrompt (this file): the Save job and Save a preset name prompt.
//   Ask(title, label, value)   Shows the prompt modally; returns the name, or null when cancelled.
