#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Every menu entry of this package in one place: the Tools dropdown, the
    /// Project window right-click menu (Assets/...), and the GIT menu. Constraint
    /// Train's entries (Tools and Hierarchy right-click) are in
    /// Editor/VRChat/RiryVRChatMenus.cs, because they only compile with the VRChat SDK.
    /// Without the SDK, the same entries at the bottom of this file show a
    /// "VRCSDK not found" popup instead.
    ///
    /// Editing tips:
    /// - Change a path to move or rename an entry; "/" makes a submenu.
    /// - The number is the priority: lower is higher up. A gap of 11 or more
    ///   between neighbours draws a separator.
    /// - "Assets/..." entries also appear in the Project window's right-click menu.
    ///   "GameObject/..." entries with a priority below 50 also appear in the
    ///   Hierarchy's right-click menu.
    /// - A method marked with "true" is the validator: it greys the entry out when
    ///   it returns false, and can set a check mark.
    /// </summary>
    internal static class RiryMenus
    {
        // ---- Tools dropdown -------------------------------------------------
        private const string CleanAnimationKeys = "Tools/Rirytools/Clean Animation Keys";
        private const string GuidTools = "Tools/Rirytools/GUID Tools";
        private const string BlendshapeSync = "Tools/Rirytools/Blendshape Sync";
        private const string DeleteGuidCache = "Tools/Rirytools/Delete GUID Reference Cache";
        private const string OpenPresetLibrary = "Tools/Rirytools/Open Import Preset Library";
        private const string KeepTabsInFront = "Tools/Rirytools/Play Mode/Keep Tabs in Front";
        private const string CloseGameView = "Tools/Rirytools/Play Mode/Close Game View Opened by Play Mode";

        // ---- Project window right-click -------------------------------------
        private const string RegenerateGuid = "Assets/Rirytools/Regenerate GUID";
        private const string ImportPreset = "Assets/Rirytools/Import Preset...";
        private const string SaveImportPreset = "Assets/Rirytools/Save Import Settings to Preset Library...";
        private const string ExtractAnimations = "Assets/Rirytools/Extract Animations (Generic)";
        private const string RepairGuidReferences = "Assets/Rirytools/Repair Missing GUID References";
        private const string RemoveBlankShapekeys = "Assets/Tools/Remove blank shapekeys";

        // ---- GIT menu -------------------------------------------------------
        // U+2215 (division slash) keeps "Scene/Prefab" on one entry instead of a submenu.
        private const string AutoSync = "GIT/AutoSync Scene∕Prefab";
        private const string SyncNow = "GIT/Sync Now";
        private const string CheckBrokenReferences = "GIT/Check for Broken References...";
        private const string ConfigureSync = "GIT/Configure Sync...";
        private const string AutoSyncSettings = "GIT/AutoSync Settings...";
        private const string GitHelp = "GIT/Help";

        // =====================================================================
        // Tools dropdown
        // =====================================================================

        [MenuItem(CleanAnimationKeys, false, 2000)]
        private static void CleanAnimationKeysItem() { RemoveBlankShapeKeys.RunFromMainMenu(); }

        [MenuItem(CleanAnimationKeys, true)]
        private static bool CleanAnimationKeysValid() { return RemoveBlankShapeKeys.ValidateMainMenuCommand(); }

        [MenuItem(GuidTools, false, 2010)]
        private static void GuidToolsItem() { GuidToolsWindow.OpenWindow(); }

        [MenuItem(BlendshapeSync, false, 2020)]
        private static void BlendshapeSyncItem() { BlendShapeSyncWindow.Open(); }

        [MenuItem(DeleteGuidCache, false, 2020)]
        private static void DeleteGuidCacheItem() { ClearRegenerateGuidCache.DeleteCacheWithConfirmation(); }

        // Constraint Train is 2030 (RiryVRChatMenus.cs).

        [MenuItem(OpenPresetLibrary, false, 2040)]
        private static void OpenPresetLibraryItem() { ImportPresetMenu.OpenLibrary(); }

        [MenuItem(KeepTabsInFront, false, 2060)]
        private static void KeepTabsInFrontItem() { PlayModeWindowKeeper.KeepTabs = !PlayModeWindowKeeper.KeepTabs; }

        [MenuItem(KeepTabsInFront, true)]
        private static bool KeepTabsInFrontValid()
        {
            Menu.SetChecked(KeepTabsInFront, PlayModeWindowKeeper.KeepTabs);
            return true;
        }

        [MenuItem(CloseGameView, false, 2061)]
        private static void CloseGameViewItem() { PlayModeWindowKeeper.CloseGameView = !PlayModeWindowKeeper.CloseGameView; }

        [MenuItem(CloseGameView, true)]
        private static bool CloseGameViewValid()
        {
            Menu.SetChecked(CloseGameView, PlayModeWindowKeeper.CloseGameView);
            return true;
        }

        // =====================================================================
        // Project window right-click
        // =====================================================================

        [MenuItem(RegenerateGuid, false, 2000)]
        private static void RegenerateGuidItem() { RegenerateAssetGuid.RegenerateSelectedGuids(); }

        [MenuItem(RegenerateGuid, true)]
        private static bool RegenerateGuidValid() { return RegenerateAssetGuid.HasValidSelection(); }

        [MenuItem(ImportPreset, false, 2001)]
        private static void ImportPresetItem() { ImportPresetMenu.ChooseFolderPreset(); }

        [MenuItem(ImportPreset, true)]
        private static bool ImportPresetValid() { return ImportPresetMenu.ValidateChooseFolderPreset(); }

        [MenuItem(SaveImportPreset, false, 2002)]
        private static void SaveImportPresetItem() { ImportPresetMenu.SaveToLibrary(); }

        [MenuItem(SaveImportPreset, true)]
        private static bool SaveImportPresetValid() { return ImportPresetMenu.ValidateSaveToLibrary(); }

        [MenuItem(ExtractAnimations, false, 2003)]
        private static void ExtractAnimationsItem() { ImportPresetAnimations.ExtractSelected(); }

        [MenuItem(ExtractAnimations, true)]
        private static bool ExtractAnimationsValid() { return ImportPresetAnimations.ValidateExtractSelected(); }

        [MenuItem(RepairGuidReferences, false, 2004)]
        private static void RepairGuidReferencesItem() { GuidReferenceRepair.RepairSelected(); }

        [MenuItem(RepairGuidReferences, true)]
        private static bool RepairGuidReferencesValid() { return RegenerateAssetGuid.HasValidSelection(); }

        [MenuItem(RemoveBlankShapekeys, false, 2100)]
        private static void RemoveBlankShapekeysItem() { RemoveBlankShapeKeys.RunFromProjectWindow(); }

        [MenuItem(RemoveBlankShapekeys, true)]
        private static bool RemoveBlankShapekeysValid() { return RemoveBlankShapeKeys.ValidateProjectWindowCommand(); }

        // =====================================================================
        // GIT menu
        // =====================================================================

        [MenuItem(AutoSync, false, 1)]
        private static void AutoSyncItem() { GitActions.ToggleAutoSync(); }

        [MenuItem(AutoSync, true)]
        private static bool AutoSyncValid()
        {
            Menu.SetChecked(AutoSync, GitAutosave.Enabled);
            return true;
        }

        [MenuItem(SyncNow, false, 2)]
        private static void SyncNowItem() { GitActions.SyncNow(); }

        [MenuItem(CheckBrokenReferences, false, 20)]
        private static void CheckBrokenReferencesItem() { BrokenReferenceWindow.OpenForOpenScenes("Checked from the GIT menu."); }

        [MenuItem(ConfigureSync, false, 40)]
        private static void ConfigureSyncItem() { GitActions.ConfigureSync(); }

        // Offered while there is no git, repository or remote.
        [MenuItem(ConfigureSync, true)]
        private static bool ConfigureSyncValid() { return GitActions.ConfigureSyncAvailable(); }

        [MenuItem(AutoSyncSettings, false, 41)]
        private static void AutoSyncSettingsItem() { GitAutosaveWindow.Open(); }

        [MenuItem(GitHelp, false, 100)]
        private static void GitHelpItem() { GitHelpWindow.Open(); }

#if !RIRY_VRC_CONSTRAINTS
        // =====================================================================
        // VRChat tools without the VRChat SDK
        // With the SDK, these paths belong to RiryVRChatMenus.cs instead. Keep
        // the paths and priorities of both files in step.
        // =====================================================================

        private const string ConstraintTrain = "Tools/Rirytools/Constraint Train";
        private const string ConstraintTrainHierarchy = "GameObject/Rirytools/Constraint Train...";

        [MenuItem(ConstraintTrain, false, 2030)]
        private static void ConstraintTrainItem() { VrcSdkMissing("Constraint Train"); }

        [MenuItem(ConstraintTrainHierarchy, false, 20)]
        private static void ConstraintTrainHierarchyItem() { VrcSdkMissing("Constraint Train"); }

        /// <summary>Throws VrcSdkNotFoundException and shows it as the "VRCSDK not found" popup.</summary>
        private static void VrcSdkMissing(string tool)
        {
            try
            {
                throw new VrcSdkNotFoundException(tool);
            }
            catch (VrcSdkNotFoundException exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorUtility.DisplayDialog("VRCSDK not found", exception.Message, "OK");
            }
        }
#endif
    }
}

// ============================================================================
// GLOSSARY: menu entries
// Path (priority)                                   -> what it calls (file)
// ============================================================================
//
// TOOLS DROPDOWN
//   Tools/Rirytools/Clean Animation Keys (2000)     -> RemoveBlankShapeKeys.RunFromMainMenu (Editor/RemoveBlankShapeKeys.cs)
//   Tools/Rirytools/GUID Tools (2010)               -> GuidToolsWindow.OpenWindow (Editor/UI/GuidToolsWindow.cs)
//   Tools/Rirytools/Blendshape Sync (2020)          -> BlendShapeSyncWindow.Open (Editor/UI/BlendShapeSyncWindow.UI.cs)
//   Tools/Rirytools/Delete GUID Reference Cache (2020) -> ClearRegenerateGuidCache.DeleteCacheWithConfirmation (Editor/ClearRegenerateGuidCache.cs)
//   Tools/Rirytools/Constraint Train (2030)         -> ConstraintTrainWindow.Open (Editor/VRChat/RiryVRChatMenus.cs);
//                                                      without the VRChat SDK -> "VRCSDK not found" popup (this file)
//   Tools/Rirytools/Open Import Preset Library (2040) -> ImportPresetMenu.OpenLibrary (Editor/ImportPresets/ImportPresetMenu.cs)
//   Tools/Rirytools/Play Mode/Keep Tabs in Front (2060)  [check mark] -> PlayModeWindowKeeper.KeepTabs (Editor/PlayModeWindowKeeper.cs)
//   Tools/Rirytools/Play Mode/Close Game View Opened by Play Mode (2061) [check mark] -> PlayModeWindowKeeper.CloseGameView
//
// PROJECT WINDOW RIGHT-CLICK
//   Assets/Rirytools/Regenerate GUID (2000)         -> RegenerateAssetGuid.RegenerateSelectedGuids (Editor/RegenerateAssetGuid.cs)
//   Assets/Rirytools/Import Preset... (2001)        -> ImportPresetMenu.ChooseFolderPreset -> ImportPresetFolderWindow (Editor/UI/ImportPresetWindows.cs)
//   Assets/Rirytools/Save Import Settings to Preset Library... (2002) -> ImportPresetMenu.SaveToLibrary -> ImportPresetSaveWindow
//   Assets/Rirytools/Extract Animations (Generic) (2003) -> ImportPresetAnimations.ExtractSelected (Editor/ImportPresets/ImportPresetAnimations.cs)
//   Assets/Rirytools/Repair Missing GUID References (2004) -> GuidReferenceRepair.RepairSelected -> BrokenReferenceWindow (Editor/UI/BrokenReferenceWindow.UI.cs)
//   Assets/Tools/Remove blank shapekeys (2100)      -> RemoveBlankShapeKeys.RunFromProjectWindow
//
// HIERARCHY RIGHT-CLICK
//   GameObject/Rirytools/Constraint Train... (20)   -> ConstraintTrainWindow.OpenFromHierarchy (Editor/VRChat/RiryVRChatMenus.cs);
//                                                      without the VRChat SDK -> "VRCSDK not found" popup (this file)
//
// GIT MENU (main menu bar)
//   GIT/AutoSync Scene∕Prefab (1)  [check mark]     -> GitActions.ToggleAutoSync / GitAutosave.Enabled (Editor/GitAutosave/GitActions.cs)
//   GIT/Sync Now (2)                                -> GitActions.SyncNow
//   GIT/Check for Broken References... (20)         -> BrokenReferenceWindow.OpenForOpenScenes (Editor/GuidRepair/BrokenReferenceWindow.cs)
//   GIT/Configure Sync... (40)  [greyed out with a repository and remote] -> GitActions.ConfigureSync -> GitConfigureWindow (Editor/UI/GitWindows.cs)
//   GIT/AutoSync Settings... (41)                   -> GitAutosaveWindow.Open (Editor/UI/GitWindows.cs)
//   GIT/Help (100)                                  -> GitHelpWindow.Open (Editor/UI/GitWindows.cs)
//
// OTHER UI OUTSIDE MENUS
//   "Clean keys" button in the Animation window     -> Editor/UI/CleanAnimationKeysUI.cs
//   Animation cleanup result window                 -> Editor/UI/CleanAnimationKeysUI.cs
//   SCRIPT CAUSING FAILED LOAD window               -> Editor/UI/ScriptLoadFailureWindow.cs
//   Constraint Train window                         -> Editor/VRChat/ConstraintTrainWindow.UI.cs
//   Confirmation dialogs (DisplayDialog) stay beside the action they confirm.
#endif
