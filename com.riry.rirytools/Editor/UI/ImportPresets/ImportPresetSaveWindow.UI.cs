using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>What the Save Import Preset window draws.</summary>
    internal sealed partial class ImportPresetSaveWindow
    {
        internal static void Open(AssetImporter importer)
        {
            if (!ImportPresetLibrary.CanSave(importer)) return;
            var window = CreateInstance<ImportPresetSaveWindow>();
            window.assetPath = importer.assetPath;
            window.presetName = DefaultPresetName(importer);
            window.titleContent = new GUIContent("Save Import Preset");
            window.minSize = new Vector2(420, 230);
            window.ShowUtility();
        }

        /// <summary>Main Menu tab: shows an asset's importer settings to save, as Open does.</summary>
        internal void UseImporter(AssetImporter importer)
        {
            if (!ImportPresetLibrary.CanSave(importer)) return;
            assetPath = importer.assetPath;
            presetName = DefaultPresetName(importer);
            message = null;
        }

        /// <summary>The name a new preset gets: the importer type without "Importer", and the file extension ("Model fbx").</summary>
        private static string DefaultPresetName(AssetImporter importer)
        {
            string type = importer.GetType().Name;
            if (type.EndsWith("Importer", StringComparison.Ordinal)) type = type.Substring(0, type.Length - 8);
            string extension = ImportPresetLibrary.NormalizeExtension(Path.GetExtension(importer.assetPath));
            return extension.Length > 0 ? type + " " + extension : type;
        }

        internal string AssetPath
        {
            get { return assetPath; }
        }

        private void OnGUI()
        {
            AssetImporter importer = string.IsNullOrEmpty(assetPath) ? null : AssetImporter.GetAtPath(assetPath);
            if (!ImportPresetLibrary.CanSave(importer))
            {
                EditorGUILayout.HelpBox("The selected asset is gone or has no importer settings to save.",
                    MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Asset", assetPath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Importer", importer.GetType().Name);

            var options = new List<string> { "New configuration" };
            options.AddRange(configurations);
            choice = Mathf.Clamp(choice, 0, options.Count - 1);
            choice = EditorGUILayout.Popup("Configuration", choice, options.ToArray());
            if (choice == 0) newConfiguration = EditorGUILayout.TextField("New name", newConfiguration);
            presetName = EditorGUILayout.TextField("Preset name", presetName);

            string configuration = choice == 0 ? newConfiguration : configurations[choice - 1];
            string error = ImportPresetLibrary.ValidateName(configuration) ?? ImportPresetLibrary.ValidateName(presetName);
            // A name problem shows only after Save was clicked with it.
            if (error == null) saveTried = false;
            if (error != null && saveTried) EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Library")) ImportPresetActions.OpenLibrary();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Save", GUILayout.Width(80)))
                {
                    if (error != null) saveTried = true;
                    else
                    {
                        Save(importer, configuration);
                        RiryToolGUI.ExitAfterModal();
                    }
                }
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Save Import Preset window, drawing
// Fields and Save are in ImportPresetSaveWindow.cs.
// ============================================================================
//
// ImportPresetSaveWindow (this part): saves an asset's import settings to the library
//   Open(importer)        The popup, on that asset (DefaultPresetName fills the name).
//   UseImporter(importer) / AssetPath  For the Main Menu tab, which follows the selection; clears the last message.
//   DefaultPresetName(importer)  "<importer type without Importer> <extension>", e.g. "Model fbx".
//   OnGUI()               Configuration popup (or a new one), preset name, Save.
