using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riry.RiryTools.Editor
{
    /// <summary>Save Mockup for a mockup without a name: asks for one.</summary>
    internal sealed class MenuMockupNameWindow : EditorWindow
    {
        private MenuMockupEditWindow owner;
        private string mockupName = "";
        private string error;

        internal static void Open(MenuMockupEditWindow owner)
        {
            var window = CreateInstance<MenuMockupNameWindow>();
            window.owner = owner;
            window.titleContent = new GUIContent("Save Mockup");
            Rect over = owner.position;
            // Tall enough for the name problem's HelpBox above the buttons, with a little to spare.
            window.position = new Rect(over.center.x - 150f, over.center.y - 62f, 300f, 124f);
            window.minSize = window.maxSize = new Vector2(300f, 124f);
            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            mockupName = EditorGUILayout.TextField("Name", mockupName);
            // A name problem shows only after Save was clicked with it.
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f))) Close();
                if (GUILayout.Button("Save", GUILayout.Width(80f))) { Save(); RiryToolGUI.ExitAfterModal(); }
            }
        }

        private void Save()
        {
            string chosen = mockupName.Trim();
            error = ImportPresetLibrary.ValidateName(chosen);
            if (error != null || owner == null) return;
            string path = Path.Combine(MenuMockup.Folder, chosen + "." + MenuMockup.FileExtension);
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Save Mockup", chosen + "." + MenuMockup.FileExtension +
                    " exists. Replace it?", "Replace", "Cancel"))
                return;
            owner.Save(path);
            Close();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup Save Mockup window
// Menu Mockup (a Debug tool): the model and file format are in Editor/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockupNameWindow ("Save Mockup"): asks for the name of a new mockup; a name problem shows after Save.
