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
    /// files. State and actions; the drawing is in ImportPresetFolderWindow.UI.cs.
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
            betterFbx = rules.BetterFbx.Contains(ImportPresetBetterFBX.SourceExtension);
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
            bool? fbx = hasBlend ? betterFbx : (bool?)null;
            if (!ImportPresetFolderRules.ConfirmApply(ImportPresetFolderRules.WouldChange(folder, chosen, fbx), folder)) return;
            int processed = 0;
            if (!ImportPresetFolderRules.Apply(folder, chosen, fbx, ref processed))
            {
                message = "ImportPreset.md in " + folder + " was edited by hand, so it was left unchanged.";
                return;
            }
            message = "Saved " + folder + ". Applied to " + processed + " file(s).";
            Scan();
            Repaint();
        }
    }
}

// ============================================================================
// GLOSSARY: Set Folder Import Presets window, fields and actions
// Drawing is in ImportPresetFolderWindow.UI.cs; the rules are in Editor/ImportPresets/ImportPresetFolderRules.cs.
// ============================================================================
//
// ImportPresetFolderWindow (this part)
//   folder                [S] string        The folder shown; first the Project window's (ImportPresetActions.ActiveFolder).
//   search                [S] string        Folder field text; suggestions show while it is not the folder, at the top
//                                           of the scroll view (so they never push Apply and Close out of view).
//   picks                 [S] List<Pick>    The chosen preset (or none) per file type.
//   betterFbx             [S] bool          Export .blend with Better FBX.
//   types                     List<FileType>  File types directly in the folder, each with the presets that fit it.
//   hasBlend / handEdited     bool          The folder holds .blend files / its ImportPreset.md was edited by hand.
//   allFolders                List<string>  Every folder in Assets, for suggestions.
//   message                   string        Result of Apply.
//   OnEnable() / OnProjectChange()  Find the file types again (after a domain reload or an import); the picks stay.
//   LoadConfigurations(out error)  Library configurations, or why there are none (Save Import Preset).
//   Load(folder)          Shows a folder with the picks from its ImportPreset.md.
//   Scan()                Finds the file types, .blend files and a hand-edited marker.
//   Suggestions(typed)    Up to 8 folders, ranked by FuzzyMatch.Score on name or path.
//   Apply()               Asks first when files would be reimported (WouldChange, ConfirmApply: Undo can't revert
//                         it), then ImportPresetFolderRules.Apply for the folder.
