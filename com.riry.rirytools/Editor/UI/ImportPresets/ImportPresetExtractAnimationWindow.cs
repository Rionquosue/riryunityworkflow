using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Extract Animation window: Target, Destination, Automatically clean keys
    /// (with Configure and Save animation extract settings), Automatically extract
    /// animations, and Extract. Every change is saved to the target's settings file
    /// (Editor/ImportPresets/ImportPresetAnimationExtraction.cs); extraction is in
    /// Editor/ImportPresets/ImportPresetAnimations.cs.
    /// </summary>
    internal sealed class ImportPresetExtractAnimationWindow : EditorWindow
    {
        [SerializeField] private GameObject target;
        [SerializeField] private ImportPresetAnimationExtraction settings = new ImportPresetAnimationExtraction();
        // The Configure window: Clean Animation Keys working on these settings.
        [SerializeField] private CleanAnimationKeysWindow configure;

        internal static void Open()
        {
            ImportPresetExtractAnimationWindow window = GetWindow<ImportPresetExtractAnimationWindow>(true, "Extract Animation");
            window.minSize = new Vector2(420.0f, 190.0f);
            window.UseSelection();
            window.Show();
        }

        internal ImportPresetAnimationExtraction Settings
        {
            get { return settings; }
        }

        private string TargetPath
        {
            get { return target != null ? AssetDatabase.GetAssetPath(target) : null; }
        }

        /// <summary>Main Menu tab: takes the selected model, as Open does.</summary>
        internal void ShowSelection()
        {
            UseSelection();
        }

        private void OnSelectionChange()
        {
            if (UseSelection()) Repaint();
        }

        // A model selected in the Project window becomes the target.
        private bool UseSelection()
        {
            string path = ImportPresetAnimations.SelectedModel();
            if (path == null || path == TargetPath) return false;
            SetTarget(AssetDatabase.LoadMainAssetAtPath(path) as GameObject);
            return true;
        }

        private const string SettingsUndo = "Extract Animation settings";
        // The target as last chosen: picking a model is not an Undo step, so Undo never switches it back (OnUndoRedo).
        [NonSerialized] private GameObject keptTarget;

        private void OnEnable()
        {
            keptTarget = target;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        // A setting put back by Undo or Redo is saved to the settings file, as the edit was. A step recorded for an earlier
        // target leaves the current target and its file as they are.
        private void OnUndoRedo()
        {
            if (target != keptTarget) SetTarget(keptTarget);
            else SettingsChanged();
            Repaint();
        }

        private void SetTarget(GameObject model)
        {
            target = model;
            keptTarget = model;
            string path = TargetPath;
            settings = (path != null ? ImportPresetAnimationExtraction.Load(path) : null) ?? new ImportPresetAnimationExtraction();
            if (path != null && settings.Destination.Length == 0)
                settings.Destination = ImportPresetAnimations.AnimationsFolder(path);
            if (configure != null) configure.Close();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6.0f);
            var chosen = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Target",
                "The FBX or other model file to extract the clips from. Filled from the Project window selection."),
                target, typeof(GameObject), false);
            if (chosen != target)
            {
                if (chosen == null || AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(chosen)) is ModelImporter)
                    SetTarget(chosen);
                GUIUtility.ExitGUI();
            }

            string path = TargetPath;
            using (new EditorGUI.DisabledScope(path == null))
            {
                EditorGUI.BeginChangeCheck();
                settings.Destination = RiryToolGUI.Set(this, SettingsUndo, settings.Destination, EditorGUILayout.DelayedTextField(new GUIContent("Destination",
                    "Folder the clips are written to. Default: Animations in the target's folder."), settings.Destination).Trim());
                if (path != null && settings.Destination.Length == 0)
                    settings.Destination = ImportPresetAnimations.AnimationsFolder(path);
                using (new EditorGUILayout.HorizontalScope())
                {
                    settings.AutoClean = RiryToolGUI.Set(this, SettingsUndo, settings.AutoClean, EditorGUILayout.ToggleLeft(new GUIContent("Automatically clean keys",
                        "Clean the extracted clips with Clean Animation Keys. Configure sets what is kept."), settings.AutoClean));
                    if (settings.AutoClean && GUILayout.Button("Configure", GUILayout.Width(90.0f))) OpenConfigure(path);
                }
                if (settings.AutoClean)
                {
                    settings.SaveCleanSettings = RiryToolGUI.Set(this, SettingsUndo, settings.SaveCleanSettings, EditorGUILayout.ToggleLeft(
                        new GUIContent("Save animation extract settings",
                        "Keep the cleaning settings in the target's settings file, so automatic extraction cleans too."),
                        settings.SaveCleanSettings));
                }
                settings.AutoExtract = RiryToolGUI.Set(this, SettingsUndo, settings.AutoExtract, EditorGUILayout.ToggleLeft(
                    new GUIContent("Automatically extract animations",
                    "Always extract the Generic clips before the Humanoid or other import preset set for the folder is applied."),
                    settings.AutoExtract));
                if (EditorGUI.EndChangeCheck()) SettingsChanged();

                string problem = path != null ? ImportPresetAnimations.DestinationProblem(settings.Destination) : null;
                if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Error);

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(problem != null))
                {
                    if (GUILayout.Button("Extract", GUILayout.Height(28.0f))) { Extract(path); RiryToolGUI.ExitAfterModal(); }
                }
                EditorGUILayout.Space(6.0f);
            }
        }

        // The Configure window lists the clips already extracted to Destination.
        private void OpenConfigure(string path)
        {
            if (configure == null) configure = CleanAnimationKeysWindow.OpenFor(this, Path.GetFileName(path));
            configure.Add(ImportPresetAnimations.ExtractedClips(path, ImportPresetAnimations.Destination(path, settings)));
            configure.Focus();
        }

        /// <summary>Saves the settings file (or removes it when there is nothing to keep).</summary>
        internal void SettingsChanged()
        {
            string path = TargetPath;
            if (path == null) return;
            try
            {
                ImportPresetAnimationExtraction.Save(path, settings);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.ImportPresets, "Extract Animation could not save " + ImportPresetAnimationExtraction.PathFor(path), exception);
                EditorUtility.DisplayDialog("Extract Animation", "Could not save " + ImportPresetAnimationExtraction.PathFor(path) +
                    ":\n" + exception.Message, "OK");
                RiryToolGUI.ExitAfterModal();
            }
        }

        private void Extract(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            ImportPresetAnimations.Extract(importer, settings);
            if (configure != null)
            {
                configure.Add(ImportPresetAnimations.ExtractedClips(path, ImportPresetAnimations.Destination(path, settings)));
                configure.Repaint();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Extract Animation window
// Menu entries (Tools > RiryTools > Import Presets and Project right-click) are in RiryToolsMenuBar.cs and RiryToolsRightClickMenu.cs.
// ============================================================================
//
// ImportPresetExtractAnimationWindow (this file)
//   target                   GameObject  The model (FBX or other model file); filled from the Project window selection.
//   settings                 ImportPresetAnimationExtraction  Destination, the three toggles, the cleaning options and whitelist.
//   configure                CleanAnimationKeysWindow  The Configure window, sharing settings.CleanOptions and settings.Whitelist.
//   Open()                   Opens the window with the selected model.
//   ShowSelection()          The same for the Main Menu tab.
//   UseSelection() / SetTarget(model)  Changes the target and loads its settings file, or the defaults.
//   OnGUI()                  Target; Destination; Automatically clean keys with Configure; Save animation extract
//                            settings (only while cleaning); Automatically extract animations; Extract.
//   OpenConfigure(path)      Opens Clean Animation Keys for these settings, listing the clips already in Destination.
//   SettingsChanged()        ImportPresetAnimationExtraction.Save: writes or removes the settings file.
//   SettingsUndo             "Extract Animation settings": Destination and the three toggles are recorded (RiryToolGUI.Set), like
//                            the Configure window's options on this window; OnUndoRedo saves the file again. keptTarget: the
//                            model as last chosen; a step recorded for an earlier target does not switch it back.
//   Extract(path)            ImportPresetAnimations.Extract with these settings.
//
// ImportPresetAnimationExtraction (Editor/ImportPresets/ImportPresetAnimationExtraction.cs)
//   ".<model>_ani-extract.json~" beside the model: no .meta (Unity skips "." and "~" names), hidden on Windows.
//   Worth                    AutoExtract, or AutoClean with SaveCleanSettings: only then the file exists.
//   Load(model) / Save(model, settings)  Reads; writes or deletes, then git add (and queued for Sync when it is on).
//
// ImportPresetOwnedFiles (Editor/ImportPresets/ImportPresetOwnedFiles.cs)
//   Library/RiryTools/ImportPresets/owned.txt: "path<TAB>length<TAB>ticks[<TAB>model GUID]" per file this tool wrote.
//   IsUnchanged(path)        Still as written: replaced automatically. Record(path, owner) stores a clip's model GUID.
//   OwnedByOther(path, owner)  A clip extracted from another model (still in the project): never replaced, even when the
//                            Extract dialog's Replace is chosen; reported with that model (OwnerPath) as kept.
