using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Set Folder Import Presets: one folder, chosen with a fuzzy search field, and a
    /// preset for each file type directly in it, plus Better FBX when it holds .blend
    /// files. State and actions; the drawing is in Editor/UI/ImportPresetWindows.cs.
    /// Rules are written by ImportPresetFolderRules.
    /// </summary>
    internal sealed partial class ImportPresetFolderWindow : EditorWindow
    {
        private const int MaxSuggestions = 8;

        [SerializeField] private string folder = "";
        [SerializeField] private string search = "";
        [SerializeField] private List<ImportPresetFolderRules.Pick> picks = new List<ImportPresetFolderRules.Pick>();
        [SerializeField] private bool betterFbx;
        [SerializeField] private bool tagsExpanded;
        [SerializeField] private string newTag = "";
        [NonSerialized] private string tagProblem;
        [NonSerialized] private List<ImportPresetFolderRules.FileType> types = new List<ImportPresetFolderRules.FileType>();
        [NonSerialized] private bool hasBlend;
        [NonSerialized] private bool handEdited;
        [NonSerialized] private List<string> allFolders;
        [NonSerialized] private string message;
        private Vector2 scroll;

        // After a domain reload (such as after an import) the file types are found again; the choices stay.
        private void OnEnable()
        {
            if (picks == null) picks = new List<ImportPresetFolderRules.Pick>();
            Scan();
        }

        private void OnProjectChange()
        {
            allFolders = null;
            Scan();
            Repaint();
        }

        /// <summary>Lists configurations, or reports why the library is unavailable.</summary>
        internal static List<string> LoadConfigurations(out string error)
        {
            error = null;
            try
            {
                ImportPresetLibrary.EnsureRoot();
                return ImportPresetLibrary.ConfigurationNames();
            }
            catch (Exception exception)
            {
                error = "The preset library is unavailable (" + ImportPresetLibrary.RootPath + "): " +
                    exception.Message;
                return new List<string>();
            }
        }

        private static bool Usable(string path)
        {
            return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) && ImportPresetActions.IsWritableFolder(path);
        }

        /// <summary>Shows a folder with the choices in its ImportPreset.md.</summary>
        private void Load(string path)
        {
            folder = path;
            message = null;
            ImportPresetMarker.Rules rules = ImportPresetFolderRules.Read(path);
            picks.Clear();
            foreach (ImportPresetMarker.TypeRule rule in rules.TypeRules)
                picks.Add(new ImportPresetFolderRules.Pick { Label = rule.Label, Configuration = rule.Configuration, Preset = rule.Preset });
            betterFbx = rules.BetterFbx.Contains(BetterFbxExporter.SourceExtension);
            Scan();
        }

        // Finds the file types in the folder and the library presets for each.
        private void Scan()
        {
            types = Usable(folder) ? ImportPresetFolderRules.Types(new[] { folder }) : new List<ImportPresetFolderRules.FileType>();
            hasBlend = Usable(folder) && ImportPresetFolderRules.HasBlend(folder);
            handEdited = Usable(folder) && ImportPresetFolderRules.HandEdited(folder);
        }

        private ImportPresetFolderRules.Pick PickFor(string label)
        {
            ImportPresetFolderRules.Pick pick = picks.Find(item => string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase));
            if (pick != null) return pick;
            pick = new ImportPresetFolderRules.Pick { Label = label };
            picks.Add(pick);
            return pick;
        }

        private void SetPick(ImportPresetFolderRules.Pick pick)
        {
            picks.RemoveAll(item => string.Equals(item.Label, pick.Label, StringComparison.OrdinalIgnoreCase));
            picks.Add(pick);
        }

        // Folders whose name or path matches the typed text, best first (FuzzyMatch.Score).
        private List<string> Suggestions(string typed)
        {
            if (allFolders == null)
            {
                allFolders = new List<string>();
                AddFolders("Assets");
            }
            var scored = new List<KeyValuePair<int, string>>();
            foreach (string path in allFolders)
            {
                int score = Math.Max(FuzzyMatch.Score(Path.GetFileName(path), typed), FuzzyMatch.Score(path, typed));
                if (score > 0) scored.Add(new KeyValuePair<int, string>(score, path));
            }
            scored.Sort((left, right) => left.Key != right.Key
                ? right.Key.CompareTo(left.Key)
                : string.Compare(left.Value, right.Value, StringComparison.OrdinalIgnoreCase));
            var result = new List<string>();
            for (int i = 0; i < scored.Count && i < MaxSuggestions; i++) result.Add(scored[i].Value);
            return result;
        }

        private void AddFolders(string parent)
        {
            allFolders.Add(parent);
            foreach (string child in AssetDatabase.GetSubFolders(parent)) AddFolders(child);
        }

        private void Apply()
        {
            var chosen = new List<ImportPresetFolderRules.Pick>();
            foreach (ImportPresetFolderRules.FileType type in types) chosen.Add(PickFor(type.Label));
            int processed = 0;
            if (!ImportPresetFolderRules.Apply(folder, chosen, hasBlend ? betterFbx : (bool?)null, ref processed))
            {
                message = "ImportPreset.md in " + folder + " was edited by hand, so it was left unchanged.";
                return;
            }
            message = "Saved " + folder + ". Applied to " + processed + " file(s).";
            Scan();
            Repaint();
        }
    }

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
                Debug.LogException(exception);
            }
        }
    }
}
