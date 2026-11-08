using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Menu Tool's Add menu dialog: the new submenu's name and the submenu it goes in (Root and every submenu of the
    /// planned layout). Add hands both to the window, which plans the new menu; nothing changes until Apply. Opened with
    /// OpenControl it is the Add New Control dialog: also the control's type, parameter, value and icon.
    /// </summary>
    internal sealed class MenuToolAddMenuDialog : EditorWindow
    {
        private const string NameControl = "RiryTools.MenuTool.AddMenuName";
        private static readonly Vector2 Size = new Vector2(380f, 112f);
        private static readonly Vector2 ControlSize = new Vector2(380f, 200f);
        private static readonly ControlType[] Types = { ControlType.Toggle, ControlType.Button, ControlType.RadialPuppet, ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet };
        private static readonly string[] TypeNames = { "Toggle", "Button", "Radial Puppet", "Two Axis Puppet", "Four Axis Puppet" };

        /// <summary>What Add New Control plans: name, parent path, type, parameter, value, icon.</summary>
        internal sealed class NewControl
        {
            internal string Name;
            internal string Parent;
            internal ControlType Type;
            internal string Parameter;
            internal float Value;
            internal Texture2D Icon;
        }

        [NonSerialized] private List<KeyValuePair<string, string>> parents;
        [NonSerialized] private Action<string, string> add;
        [NonSerialized] private string[] labels;
        [NonSerialized] private int parent;
        [NonSerialized] private string menuName = "";
        [NonSerialized] private bool focused;
        [NonSerialized] private Action<NewControl> addControl;
        [NonSerialized] private string[] parameters;
        [NonSerialized] private int type;
        [NonSerialized] private string parameter = "";
        [NonSerialized] private float value = 1f;
        [NonSerialized] private Texture2D icon;

        /// <summary>Add New Control: as Open, plus the control's fields; parameters are the avatar's names to pick from.</summary>
        internal static void OpenControl(List<KeyValuePair<string, string>> parents, string shown, string[] parameters, Action<NewControl> add)
        {
            Show(parents, shown, "Add New Control", ControlSize, dialog =>
            {
                dialog.addControl = add;
                dialog.parameters = parameters;
            });
        }

        /// <summary>parents: each submenu's path and label, the root first; shown: the path chosen at first; add(name, parent path).</summary>
        internal static void Open(List<KeyValuePair<string, string>> parents, string shown, Action<string, string> add)
        {
            Show(parents, shown, "Add menu", Size, dialog => dialog.add = add);
        }

        private static void Show(List<KeyValuePair<string, string>> parents, string shown, string title, Vector2 size, Action<MenuToolAddMenuDialog> set)
        {
            var dialog = CreateInstance<MenuToolAddMenuDialog>();
            dialog.titleContent = new GUIContent(title);
            dialog.parents = parents;
            set(dialog);
            // A "/" in a popup label would open a submenu of the popup.
            dialog.labels = parents.Select(p => p.Value.Replace("/", "∕")).ToArray();
            dialog.parent = Mathf.Max(0, parents.FindIndex(p => p.Key == shown));
            dialog.minSize = dialog.maxSize = size;
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            dialog.position = new Rect(main.center.x - size.x / 2f, main.center.y - size.y / 2f, size.x, size.y);
            dialog.ShowModalUtility();
        }

        private void OnGUI()
        {
            // After a script reload the window it reports to is gone.
            if (add == null && addControl == null || parents == null || parents.Count == 0)
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
            if (addControl != null)
            {
                type = EditorGUILayout.Popup("Type", type, TypeNames);
                using (new EditorGUILayout.HorizontalScope())
                {
                    parameter = EditorGUILayout.TextField(Types[type] == ControlType.Toggle || Types[type] == ControlType.Button ? "Parameter" : "First axis parameter", parameter);
                    // Pick one of the avatar's parameters, or keep the typed name.
                    if (GUILayout.Button("▾", EditorStyles.miniButton, GUILayout.Width(22f)) && parameters != null)
                    {
                        var menu = new GenericMenu();
                        foreach (string name in parameters) menu.AddItem(new GUIContent(name.Replace("/", "∕")), name == parameter, () => parameter = name);
                        if (parameters.Length == 0) menu.AddDisabledItem(new GUIContent("No parameters"));
                        menu.ShowAsContext();
                    }
                }
                if (Types[type] == ControlType.Toggle || Types[type] == ControlType.Button) value = EditorGUILayout.FloatField("Value", value);
                icon = (Texture2D)EditorGUILayout.ObjectField("Icon", icon, typeof(Texture2D), false, GUILayout.Height(18f));
            }
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
            Action<NewControl> reportControl = addControl;
            string into = parents[Mathf.Clamp(parent, 0, parents.Count - 1)].Key;
            add = null;
            addControl = null;
            Close();
            if (reportControl != null) reportControl(new NewControl { Name = name, Parent = into, Type = Types[type], Parameter = parameter.Trim(), Value = value, Icon = icon });
            else report(name, into);
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
//   OpenControl(...)         Add New Control (ControlSize 380 x 200): also Type (Types / TypeNames: Toggle, Button, Radial, Two
//                            Axis, Four Axis Puppet), Parameter (typed, or picked from the avatar's with ▾; the first axis for a
//                            puppet), Value (Toggle and Button) and Icon; Add reports a NewControl.
