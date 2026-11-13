using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>What the Set Folder Import Presets window draws. State and actions are in ImportPresetFolderWindow.cs.</summary>
    internal sealed partial class ImportPresetFolderWindow
    {
        /// <summary>Opens the window on the folder shown in the Project window.</summary>
        internal static void Open()
        {
            ImportPresetFolderWindow window = GetWindow<ImportPresetFolderWindow>(true, "Set Folder Import Presets");
            window.minSize = new Vector2(420, 240);
            string active = ImportPresetActions.ActiveFolder();
            window.Load(Usable(active) ? active : "Assets");
            window.search = window.folder;
            window.Show();
        }

        /// <summary>Main Menu tab: shows the folder in the Project window, as Open does.</summary>
        internal void ShowActiveFolder()
        {
            string active = ImportPresetActions.ActiveFolder();
            Load(Usable(active) ? active : "Assets");
            search = folder;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            string typed = EditorGUILayout.TextField(new GUIContent("Folder", "Type part of a folder's name or path."), search);
            if (typed != search)
            {
                search = typed;
                string path = typed.Trim().TrimEnd('/');
                if (path != folder && Usable(path)) Load(path);
            }
            // The suggestions scroll with the rest, so they never push the buttons below out of view.
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (search.Trim().Length > 0 && search.Trim().TrimEnd('/') != folder)
            {
                foreach (string suggestion in Suggestions(search.Trim()))
                {
                    if (!GUILayout.Button(new GUIContent(suggestion, suggestion), EditorStyles.miniButton, GUILayout.MinWidth(0f))) continue;
                    Load(suggestion);
                    search = suggestion;
                    GUI.FocusControl(null);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space(6);
            if (!Usable(folder))
            {
                EditorGUILayout.HelpBox("Choose a folder in Assets.", MessageType.Info);
            }
            else
            {
                if (handEdited)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox("ImportPreset.md in this folder was edited by hand, so this tool will not change it.",
                            MessageType.Warning);
                        if (GUILayout.Button("Open", GUILayout.Width(50)))
                            AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(ImportPresetMarker.PathIn(folder)));
                    }
                }
                if (types.Count == 0)
                    EditorGUILayout.HelpBox("No files directly in this folder take import presets.", MessageType.Info);
                foreach (ImportPresetFolderRules.FileType type in types)
                {
                    ImportPresetFolderRules.Pick pick = PickFor(type.Label);
                    int index;
                    string[] names = ImportPresetFolderRules.Entries(type, pick, out index);
                    int chosen = EditorGUILayout.Popup(type.Label, index, names);
                    if (chosen != index) SetPick(ImportPresetFolderRules.Chosen(type, pick, chosen));
                }
                if (hasBlend)
                {
                    betterFbx = EditorGUILayout.ToggleLeft(new GUIContent("Export .blend with Better FBX",
                        "Writes <name>.fbx beside each .blend; the FBX then gets the Model files preset."), betterFbx);
                    if (betterFbx) DrawBlenderSettings();
                }
            }
            DrawTags();
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!Usable(folder) || handEdited ||
                    (hasBlend && betterFbx && ImportPresetBetterFBX.OptionsError(ImportPresetBetterFBX.Options) != null)))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(80))) { Apply(); RiryToolGUI.ExitAfterModal(); }
                }
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            }
            EditorGUILayout.Space(4);
        }

        // The tags in preferences.yml: each one is a file type of its own for files whose name contains it.
        private void DrawTags()
        {
            EditorGUILayout.Space(6);
            tagsExpanded = EditorGUILayout.Foldout(tagsExpanded, new GUIContent("Tags",
                "A file whose name contains a tag (any case) is listed under that tag's file type, such as normal files. " +
                "Saved for every project in " + ImportPresetPreferences.FilePath + "."), true);
            if (!tagsExpanded) return;
            using (new EditorGUI.IndentLevelScope())
            {
                List<string> tags = ImportPresetPreferences.Tags();
                foreach (string tag in tags)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(tag);
                        if (!GUILayout.Button("x", GUILayout.Width(22))) continue;
                        tags.Remove(tag);
                        SaveTags(tags);
                        GUIUtility.ExitGUI();
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    newTag = EditorGUILayout.TextField(newTag);
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        string tag = newTag.Trim().ToLowerInvariant();
                        tagProblem = ImportPresetPreferences.ValidateTag(tag);
                        if (tagProblem == null)
                        {
                            if (!tags.Contains(tag)) tags.Add(tag);
                            SaveTags(tags);
                            newTag = "";
                            GUI.FocusControl(null);
                        }
                    }
                }
                // A tag problem shows only after Add was clicked with it.
                if (tagProblem != null) EditorGUILayout.HelpBox(tagProblem, MessageType.Warning);
            }
        }

        private void SaveTags(List<string> tags)
        {
            try
            {
                ImportPresetPreferences.SaveTags(tags);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.ImportPresets, "Could not save " + ImportPresetPreferences.FilePath, exception);
                message = "Could not save " + ImportPresetPreferences.FilePath + ": " + exception.Message;
            }
            Scan();
        }

        private static void DrawBlenderSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Better FBX (saved for this computer)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                ImportPresetBetterFBX.BlenderPath = EditorGUILayout.TextField("Blender", ImportPresetBetterFBX.BlenderPath);
                if (GUILayout.Button("Browse", GUILayout.Width(65)))
                {
                    string chosen = EditorUtility.OpenFilePanel("Blender executable", "", "");
                    if (!string.IsNullOrEmpty(chosen)) ImportPresetBetterFBX.BlenderPath = chosen;
                    RiryToolGUI.ExitAfterModal();
                }
            }
            string resolved = ImportPresetBetterFBX.ResolveBlender();
            EditorGUILayout.LabelField(resolved != null ? "Using: " + resolved : "Blender not found.",
                EditorStyles.wordWrappedMiniLabel);
            ImportPresetBetterFBX.Options = EditorGUILayout.TextField(
                new GUIContent("Exporter options", "Extra bpy.ops.better_export.fbx arguments, for example " +
                    "my_fbx_unit='m', use_triangulate=True. Every action is exported unless you set my_animation_type."), ImportPresetBetterFBX.Options);
            string error = ImportPresetBetterFBX.OptionsError(ImportPresetBetterFBX.Options);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
    }
}

// ============================================================================
// GLOSSARY: Set Folder Import Presets window, drawing
// Menu entries (Set Folder Import Presets, Save Import Preset, Extract Animation and Open Import Preset Library, under Tools > RiryTools > Import Presets and the Project right-click menu) are in Editor/UI/RiryToolsMenuBar.cs and RiryToolsRightClickMenu.cs. The right-click Set Folder Import Presets popup is ImportPresetQuickWindow.cs. Fields and actions are in ImportPresetFolderWindow.cs.
// ============================================================================
//
// ImportPresetFolderWindow (this part): edits one folder's ImportPreset.md
//   Open()                Opens the window on the Project window's folder.
//   ShowActiveFolder()    The same for the Main Menu tab.
//   OnGUI()               Folder with suggestions; one popup per file type; Better FBX (only with .blend files); Tags; Apply; Close.
//   DrawTags() / SaveTags(tags)  The Tags foldout: preferences.yml tags with x, and Add (a problem shows after Add).
//   DrawBlenderSettings() Blender path and Better FBX options.
