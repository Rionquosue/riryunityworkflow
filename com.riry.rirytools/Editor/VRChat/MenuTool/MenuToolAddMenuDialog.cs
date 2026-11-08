using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Menu Tool's Add menu dialog: the new submenu's name and the submenu it goes in (Root and every submenu of the
    /// planned layout). Add hands both to the window, which plans the new menu; nothing changes until Apply.
    /// </summary>
    internal sealed class MenuToolAddMenuDialog : EditorWindow
    {
        private const string NameControl = "RiryTools.MenuTool.AddMenuName";
        private static readonly Vector2 Size = new Vector2(380f, 112f);

        [NonSerialized] private List<KeyValuePair<string, string>> parents;
        [NonSerialized] private Action<string, string> add;
        [NonSerialized] private string[] labels;
        [NonSerialized] private int parent;
        [NonSerialized] private string menuName = "";
        [NonSerialized] private bool focused;

        /// <summary>parents: each submenu's path and label, the root first; shown: the path chosen at first; add(name, parent path).</summary>
        internal static void Open(List<KeyValuePair<string, string>> parents, string shown, Action<string, string> add)
        {
            var dialog = CreateInstance<MenuToolAddMenuDialog>();
            dialog.titleContent = new GUIContent("Add menu");
            dialog.parents = parents;
            dialog.add = add;
            // A "/" in a popup label would open a submenu of the popup.
            dialog.labels = parents.Select(p => p.Value.Replace("/", "∕")).ToArray();
            dialog.parent = Mathf.Max(0, parents.FindIndex(p => p.Key == shown));
            dialog.minSize = dialog.maxSize = Size;
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            dialog.position = new Rect(main.center.x - Size.x / 2f, main.center.y - Size.y / 2f, Size.x, Size.y);
            dialog.ShowModalUtility();
        }

        private void OnGUI()
        {
            // After a script reload the window it reports to is gone.
            if (add == null || parents == null || parents.Count == 0)
            {
                Close();
                return;
            }
            string trimmed = menuName.Trim();
            bool valid = trimmed.Length > 0 && !trimmed.Contains("/");
            Event current = Event.current;
            if (current.type == EventType.KeyDown)
            {
                if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && valid)
                {
                    Add(trimmed);
                    return;
                }
                if (current.keyCode == KeyCode.Escape)
                {
                    Close();
                    return;
                }
            }
            EditorGUILayout.Space(6f);
            GUI.SetNextControlName(NameControl);
            menuName = EditorGUILayout.TextField("Name", menuName);
            if (!focused)
            {
                EditorGUI.FocusTextInControl(NameControl);
                focused = true;
            }
            parent = EditorGUILayout.Popup("Parent", parent, labels);
            if (trimmed.Contains("/")) EditorGUILayout.HelpBox("A menu name cannot contain /.", MessageType.Warning);
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f))) Close();
                using (new EditorGUI.DisabledScope(!valid))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(80f))) Add(trimmed);
                }
            }
            EditorGUILayout.Space(4f);
        }

        private void Add(string name)
        {
            Action<string, string> report = add;
            string into = parents[Mathf.Clamp(parent, 0, parents.Count - 1)].Key;
            add = null;
            Close();
            report(name, into);
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool Add menu dialog
// ============================================================================
//
// MenuToolAddMenuDialog (this file)
//   Open(parents, shown, add)  A modal utility window (Size 380 x 112, centred on the main window) titled Add menu: Name (focused,
//                            NameControl) and Parent (Popup of every submenu, Root first, shown chosen at first). Add (or Enter)
//                            is greyed while the trimmed name is empty or holds "/"; it closes the dialog and calls add(name,
//                            parent path). Cancel or Escape closes it. After a script reload it closes itself.
