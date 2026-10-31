using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The small Set Folder Import Presets popup from the Project right-click menu:
    /// one configuration for all selected folders (Apply to all), or one per folder.
    /// Each dropdown has a File types foldout with a preset for each file type in its
    /// folders, starting from the configuration's. Apply writes each folder's
    /// ImportPreset.md and applies the changed presets (ImportPresetFolderRules.Apply).
    /// The full window, from the Tools menu, is ImportPresetFolderWindow.
    /// </summary>
    internal sealed class ImportPresetQuickWindow : EditorWindow
    {
        /// <summary>One dropdown: its folders, configuration (null for None), foldout and per-type picks.</summary>
        [Serializable]
        private sealed class Group
        {
            public List<string> Folders = new List<string>();
            public string Configuration;
            public bool Expanded;
            public List<ImportPresetFolderRules.Pick> Picks = new List<ImportPresetFolderRules.Pick>();
        }

        [SerializeField] private List<string> folders = new List<string>();
        [SerializeField] private bool applyToAll = true;
        [SerializeField] private Group all;
        [SerializeField] private List<Group> each = new List<Group>();
        [NonSerialized] private List<string> configurations = new List<string>();
        [NonSerialized] private string[] configurationNames = new string[0];
        [NonSerialized] private List<ImportPresetFolderRules.FileType> allTypes = new List<ImportPresetFolderRules.FileType>();
        [NonSerialized] private List<List<ImportPresetFolderRules.FileType>> eachTypes = new List<List<ImportPresetFolderRules.FileType>>();
        private Vector2 scroll;

        internal static void Open(List<string> folders)
        {
            var window = CreateInstance<ImportPresetQuickWindow>();
            window.folders = folders;
            window.titleContent = new GUIContent("Set Folder Import Presets");
            window.minSize = new Vector2(380.0f, 140.0f);
            window.all = NewGroup(folders);
            window.each.Clear();
            foreach (string folder in folders) window.each.Add(NewGroup(new List<string> { folder }));
            window.Scan();
            window.ShowUtility();
        }

        // After a domain reload (such as after an import) the file types are found again; the choices stay.
        private void OnEnable()
        {
            if (all != null) Scan();
        }

        private void OnProjectChange()
        {
            Scan();
            Repaint();
        }

        // Starts from the folders' ImportPreset.md: their presets, and the first one's configuration.
        private static Group NewGroup(List<string> folders)
        {
            var group = new Group { Folders = new List<string>(folders) };
            foreach (string folder in folders)
            {
                foreach (ImportPresetMarker.TypeRule rule in ImportPresetFolderRules.Read(folder).TypeRules)
                {
                    if (group.Picks.Exists(pick => string.Equals(pick.Label, rule.Label, StringComparison.OrdinalIgnoreCase))) continue;
                    group.Picks.Add(new ImportPresetFolderRules.Pick { Label = rule.Label, Configuration = rule.Configuration, Preset = rule.Preset });
                    if (group.Configuration == null) group.Configuration = rule.Configuration;
                }
            }
            return group;
        }

        private void Scan()
        {
            string error;
            configurations = ImportPresetFolderWindow.LoadConfigurations(out error);
            var names = new List<string> { ImportPresetFolderRules.NoneChoice };
            names.AddRange(configurations);
            configurationNames = names.ToArray();
            allTypes = ImportPresetFolderRules.Types(all.Folders);
            eachTypes.Clear();
            foreach (Group group in each) eachTypes.Add(ImportPresetFolderRules.Types(group.Folders));
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4.0f);
            applyToAll = EditorGUILayout.ToggleLeft("Apply to all", applyToAll);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (applyToAll)
            {
                DrawGroup(all, folders.Count == 1 ? folders[0] : folders.Count + " folders", allTypes);
            }
            else
            {
                for (int i = 0; i < each.Count && i < eachTypes.Count; i++) DrawGroup(each[i], each[i].Folders[0], eachTypes[i]);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Abort", GUILayout.Width(80.0f))) Close();
                using (new EditorGUI.DisabledScope(folders.Count == 0))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(80.0f))) Apply();
                }
            }
            EditorGUILayout.Space(4.0f);
        }

        // The configuration dropdown, then the File types foldout with one dropdown per type.
        private void DrawGroup(Group group, string title, List<ImportPresetFolderRules.FileType> types)
        {
            int index = group.Configuration == null ? 0 : Math.Max(0,
                configurations.FindIndex(name => string.Equals(name, group.Configuration, StringComparison.OrdinalIgnoreCase)) + 1);
            int chosen = EditorGUILayout.Popup(title, index, configurationNames);
            if (chosen != index)
            {
                group.Configuration = chosen == 0 ? null : configurations[chosen - 1];
                group.Picks.Clear();
                foreach (ImportPresetFolderRules.FileType type in types)
                    group.Picks.Add(ImportPresetFolderRules.FirstIn(type, group.Configuration));
            }
            group.Expanded = EditorGUILayout.Foldout(group.Expanded, "File types", true);
            if (!group.Expanded) return;
            using (new EditorGUI.IndentLevelScope())
            {
                if (types.Count == 0) EditorGUILayout.LabelField("No files take import presets.", EditorStyles.miniLabel);
                foreach (ImportPresetFolderRules.FileType type in types)
                {
                    ImportPresetFolderRules.Pick pick = PickFor(group, type.Label);
                    int current;
                    string[] names = ImportPresetFolderRules.Entries(type, pick, out current);
                    int picked = EditorGUILayout.Popup(type.Label, current, names);
                    if (picked == current) continue;
                    group.Picks.RemoveAll(item => string.Equals(item.Label, type.Label, StringComparison.OrdinalIgnoreCase));
                    group.Picks.Add(ImportPresetFolderRules.Chosen(type, pick, picked));
                }
            }
        }

        // A type with no pick gets none, so it is left unchanged.
        private static ImportPresetFolderRules.Pick PickFor(Group group, string label)
        {
            return group.Picks.Find(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase)) ??
                new ImportPresetFolderRules.Pick { Label = label };
        }

        /// <summary>Writes each folder's presets for the file types in it, then applies the changed ones.</summary>
        private void Apply()
        {
            int processed = 0;
            var skipped = new List<string>();
            for (int i = 0; i < folders.Count; i++)
            {
                Group group = applyToAll ? all : each[i];
                var picks = new List<ImportPresetFolderRules.Pick>();
                foreach (string label in ImportPresetFolderRules.InFolder(folders[i]).Keys) picks.Add(PickFor(group, label));
                if (!ImportPresetFolderRules.Apply(folders[i], picks, null, ref processed)) skipped.Add(folders[i]);
            }
            Debug.Log("Import Presets: set " + (folders.Count - skipped.Count) + " folder(s); applied to " + processed + " file(s).");
            if (skipped.Count > 0)
                EditorUtility.DisplayDialog("Set Folder Import Presets", "Left hand-edited ImportPreset.md unchanged in:\n" +
                    string.Join("\n", skipped), "OK");
            Close();
        }
    }
}

// ============================================================================
// GLOSSARY: Set Folder Import Presets popup (Project right-click)
// The menu entry is in RiryMenus.cs; the full window (Tools menu) is ImportPresetFolderWindow.
// ============================================================================
//
// ImportPresetQuickWindow (this file)
//   folders                  List<string>  Selected folders (ImportPresetActions.SelectedFolders).
//   applyToAll               bool          Apply to all: one dropdown (all) for every folder; off, one per folder (each).
//   Group                    Folders, Configuration (null for None), Expanded (File types foldout), Picks per file type.
//   allTypes / eachTypes     The file types in each group's folders, with the presets that fit each.
//   Open(folders)            Opens the popup, starting from the folders' ImportPreset.md.
//   OnEnable() / OnProjectChange()  Find the file types again (after a domain reload or an import); the choices stay.
//   DrawGroup(group, title, types)  Configuration dropdown (sets every type to its preset), then the File types foldout.
//   Apply()                  ImportPresetFolderRules.Apply for each folder with the picks for the types in it.
