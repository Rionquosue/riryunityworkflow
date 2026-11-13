using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Saves the selected asset's import settings into the shared library.</summary>
    internal sealed partial class ImportPresetSaveWindow : EditorWindow
    {
        [SerializeField] private string assetPath;
        [SerializeField] private int choice;
        [SerializeField] private string newConfiguration = "";
        [SerializeField] private string presetName = "";
        [NonSerialized] private bool saveTried;
        [NonSerialized] private List<string> configurations = new List<string>();
        [NonSerialized] private string message;

        private void OnEnable()
        {
            configurations = ImportPresetFolderWindow.LoadConfigurations(out message);
        }

        private void Save(AssetImporter importer, string configuration)
        {
            string target = Path.Combine(ImportPresetLibrary.ConfigurationPath(configuration),
                presetName + ImportPresetLibrary.PresetExtension);
            if (File.Exists(target) && !EditorUtility.DisplayDialog("Save Import Preset",
                    "Replace " + presetName + ImportPresetLibrary.PresetExtension + " in '" + configuration + "'?",
                    "Replace", "Cancel"))
                return;
            try
            {
                string saved = ImportPresetLibrary.SavePreset(importer, configuration, presetName);
                configurations = ImportPresetFolderWindow.LoadConfigurations(out message);
                choice = configurations.FindIndex(name =>
                    string.Equals(name, configuration, StringComparison.OrdinalIgnoreCase)) + 1;
                message = "Saved " + saved;
            }
            catch (Exception exception)
            {
                message = "Could not save: " + exception.Message;
                RiryLog.Exception(RiryLog.ImportPresets, "Save Import Preset could not save " + presetName, exception);
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Save Import Preset window, fields and saving
// Drawing is in ImportPresetSaveWindow.UI.cs.
// ============================================================================
//
// ImportPresetSaveWindow (this part)
//   FIELDS: assetPath, choice (configuration index), newConfiguration, presetName,
//           saveTried (shows a name problem after Save), configurations, message.
//   OnEnable()            Loads the configurations (ImportPresetFolderWindow.LoadConfigurations).
//   Save(importer, configuration)  Writes the preset into the configuration, asking before replacing one.
