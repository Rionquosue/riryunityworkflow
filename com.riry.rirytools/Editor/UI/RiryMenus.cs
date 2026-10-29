using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Every menu entry of this package in one place: the Tools dropdown, the
    /// Project window right-click menu (Assets/...), and the Git menu. Constraint
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
        private const string CleanAnimationKeysMenu = "Tools/RiryTools/Clean Animation Keys";
        private const string GuidTools = "Tools/RiryTools/GUID Tools";
        private const string BlendshapeSync = "Tools/RiryTools/Blendshape Sync";
        private const string DeleteGuidCache = "Tools/RiryTools/Delete GUID Reference Cache";
        private const string OpenPresetLibrary = "Tools/RiryTools/Open Import Preset Library";
        private const string KeepTabsInFront = "Tools/RiryTools/Play Mode/Keep Tabs in Front";
        private const string CloseGameView = "Tools/RiryTools/Play Mode/Close Game View Opened by Play Mode";

        // ---- Project window right-click -------------------------------------
        private const string RegenerateGuidAssets = "Assets/RiryTools/Regenerate GUID";
        private const string ImportPreset = "Assets/RiryTools/Import Preset...";
        private const string SaveImportPreset = "Assets/RiryTools/Save Import Settings to Preset Library...";
        private const string ExtractAnimations = "Assets/RiryTools/Extract Animations (Generic)";
        private const string RepairGuidReferences = "Assets/RiryTools/Repair Missing GUID References";
        private const string CleanAnimationKeysAssets = "Assets/RiryTools/Clean Animation Keys";

        // ---- Git menu -------------------------------------------------------
        // U+2215 (division slash) keeps "Scene/Prefab" on one entry instead of a submenu.
        private const string AutoSyncToggle = "Git/AutoSync Scene∕Prefab";
        private const string SyncNow = "Git/Sync Now";
        private const string CheckBrokenReferences = "Git/Check for Broken References...";
        private const string ConfigureSync = "Git/Configure Sync...";
        private const string AutoSyncSettings = "Git/AutoSync Settings...";
        private const string GitHelp = "Git/Help";

        // ---- VRChat tools (also used by RiryVRChatMenus.cs) -----------------
        internal const string ConstraintTrain = "Tools/RiryTools/Constraint Train";
        internal const string ConstraintTrainHierarchy = "GameObject/RiryTools/Constraint Train...";

        // =====================================================================
        // Tools dropdown
        // =====================================================================

        [MenuItem(CleanAnimationKeysMenu, false, 2000)]
        private static void CleanAnimationKeysItem() { CleanAnimationKeys.RunFromMenu(); }

        [MenuItem(CleanAnimationKeysMenu, true)]
        private static bool CleanAnimationKeysValid() { return CleanAnimationKeys.ValidateMenu(); }

        [MenuItem(GuidTools, false, 2010)]
        private static void GuidToolsItem() { GuidToolsWindow.Open(); }

        [MenuItem(BlendshapeSync, false, 2020)]
        private static void BlendshapeSyncItem() { BlendshapeSyncWindow.Open(); }

        [MenuItem(DeleteGuidCache, false, 2011)]
        private static void DeleteGuidCacheItem() { GuidReferenceCache.DeleteWithConfirmation(); }

        // Constraint Train is 2030 (RiryVRChatMenus.cs).

        [MenuItem(OpenPresetLibrary, false, 2040)]
        private static void OpenPresetLibraryItem() { ImportPresetActions.OpenLibrary(); }

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

        [MenuItem(RegenerateGuidAssets, false, 2000)]
        private static void RegenerateGuidItem() { RegenerateGuid.RegenerateSelectedGuids(); }

        [MenuItem(RegenerateGuidAssets, true)]
        private static bool RegenerateGuidValid() { return RegenerateGuid.HasValidSelection(); }

        [MenuItem(ImportPreset, false, 2001)]
        private static void ImportPresetItem() { ImportPresetActions.ChooseFolderPreset(); }

        [MenuItem(ImportPreset, true)]
        private static bool ImportPresetValid() { return ImportPresetActions.ValidateChooseFolderPreset(); }

        [MenuItem(SaveImportPreset, false, 2002)]
        private static void SaveImportPresetItem() { ImportPresetActions.SaveToLibrary(); }

        [MenuItem(SaveImportPreset, true)]
        private static bool SaveImportPresetValid() { return ImportPresetActions.ValidateSaveToLibrary(); }

        [MenuItem(ExtractAnimations, false, 2003)]
        private static void ExtractAnimationsItem() { ImportPresetAnimations.ExtractSelected(); }

        [MenuItem(ExtractAnimations, true)]
        private static bool ExtractAnimationsValid() { return ImportPresetAnimations.ValidateExtractSelected(); }

        [MenuItem(RepairGuidReferences, false, 2004)]
        private static void RepairGuidReferencesItem() { GuidReferenceRepair.RepairSelected(); }

        [MenuItem(RepairGuidReferences, true)]
        private static bool RepairGuidReferencesValid() { return RegenerateGuid.HasValidSelection(); }

        [MenuItem(CleanAnimationKeysAssets, false, 2005)]
        private static void CleanAnimationKeysAssetsItem() { CleanAnimationKeys.RunFromMenu(); }

        [MenuItem(CleanAnimationKeysAssets, true)]
        private static bool CleanAnimationKeysAssetsValid() { return CleanAnimationKeys.ValidateMenu(); }

        // =====================================================================
        // Git menu
        // =====================================================================

        [MenuItem(AutoSyncToggle, false, 1)]
        private static void AutoSyncToggleItem() { GitActions.ToggleAutoSync(); }

        [MenuItem(AutoSyncToggle, true)]
        private static bool AutoSyncToggleValid()
        {
            Menu.SetChecked(AutoSyncToggle, AutoSync.Enabled);
            return true;
        }

        [MenuItem(SyncNow, false, 2)]
        private static void SyncNowItem() { GitActions.SyncNow(); }

        [MenuItem(CheckBrokenReferences, false, 20)]
        private static void CheckBrokenReferencesItem() { BrokenReferenceWindow.OpenForOpenScenes("Checked from the Git menu."); }

        [MenuItem(ConfigureSync, false, 40)]
        private static void ConfigureSyncItem() { GitActions.ConfigureSync(); }

        // Offered while there is no git, repository or remote.
        [MenuItem(ConfigureSync, true)]
        private static bool ConfigureSyncValid() { return GitActions.ConfigureSyncAvailable(); }

        [MenuItem(AutoSyncSettings, false, 41)]
        private static void AutoSyncSettingsItem() { AutoSyncWindow.Open(); }

        [MenuItem(GitHelp, false, 100)]
        private static void GitHelpItem() { GitHelpWindow.Open(); }

#if !RIRYTOOLS_VRC_CONSTRAINTS
        // =====================================================================
        // VRChat tools without the VRChat SDK
        // With the SDK, these paths belong to RiryVRChatMenus.cs instead. Keep
        // the priorities of both files in step.
        // =====================================================================

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
//   Tools/RiryTools/Clean Animation Keys (2000)     -> CleanAnimationKeys.RunFromMenu (Editor/CleanAnimationKeys.cs)
//   Tools/RiryTools/GUID Tools (2010)               -> GuidToolsWindow.Open (Editor/UI/GuidToolsWindow.cs)
//   Tools/RiryTools/Delete GUID Reference Cache (2011) -> GuidReferenceCache.DeleteWithConfirmation (Editor/GuidTools/GuidReferenceCache.cs)
//   Tools/RiryTools/Blendshape Sync (2020)          -> BlendshapeSyncWindow.Open (Editor/UI/BlendshapeSyncWindow.UI.cs)
//   Tools/RiryTools/Constraint Train (2030)         -> ConstraintTrainWindow.Open (Editor/VRChat/ConstraintTrainWindow.UI.cs, entry in
//                                                      Editor/VRChat/RiryVRChatMenus.cs); without the VRChat SDK -> "VRCSDK not found" popup (this file)
//   Tools/RiryTools/Open Import Preset Library (2040) -> ImportPresetActions.OpenLibrary (Editor/ImportPresets/ImportPresetActions.cs)
//   Tools/RiryTools/Play Mode/Keep Tabs in Front (2060)  [check mark] -> PlayModeWindowKeeper.KeepTabs (Editor/PlayModeWindowKeeper.cs)
//   Tools/RiryTools/Play Mode/Close Game View Opened by Play Mode (2061) [check mark] -> PlayModeWindowKeeper.CloseGameView
//
// PROJECT WINDOW RIGHT-CLICK
//   Assets/RiryTools/Regenerate GUID (2000)         -> RegenerateGuid.RegenerateSelectedGuids (Editor/GuidTools/RegenerateGuid.cs)
//   Assets/RiryTools/Import Preset... (2001)        -> ImportPresetActions.ChooseFolderPreset -> ImportPresetFolderWindow (Editor/UI/ImportPresetWindows.cs)
//   Assets/RiryTools/Save Import Settings to Preset Library... (2002) -> ImportPresetActions.SaveToLibrary -> ImportPresetSaveWindow
//   Assets/RiryTools/Extract Animations (Generic) (2003) -> ImportPresetAnimations.ExtractSelected (Editor/ImportPresets/ImportPresetAnimations.cs)
//   Assets/RiryTools/Repair Missing GUID References (2004) -> GuidReferenceRepair.RepairSelected -> BrokenReferenceWindow (Editor/UI/BrokenReferenceWindow.UI.cs)
//   Assets/RiryTools/Clean Animation Keys (2005)    -> CleanAnimationKeys.RunFromMenu (the selected clip)
//
// HIERARCHY RIGHT-CLICK
//   GameObject/RiryTools/Constraint Train... (20)   -> ConstraintTrainWindow.OpenFromHierarchy (entry in Editor/VRChat/RiryVRChatMenus.cs);
//                                                      without the VRChat SDK -> "VRCSDK not found" popup (this file)
//
// GIT MENU (main menu bar)
//   Git/AutoSync Scene∕Prefab (1)  [check mark]     -> GitActions.ToggleAutoSync / AutoSync.Enabled (Editor/AutoSync/GitActions.cs)
//   Git/Sync Now (2)                                -> GitActions.SyncNow
//   Git/Check for Broken References... (20)         -> BrokenReferenceWindow.OpenForOpenScenes (Editor/GuidTools/BrokenReferenceWindow.cs)
//   Git/Configure Sync... (40)  [greyed out with a repository and remote] -> GitActions.ConfigureSync -> GitConfigureWindow (Editor/UI/GitWindows.cs)
//   Git/AutoSync Settings... (41)                   -> AutoSyncWindow.Open (Editor/UI/GitWindows.cs)
//   Git/Help (100)                                  -> GitHelpWindow.Open (Editor/UI/GitWindows.cs)
//
// OTHER UI OUTSIDE MENUS
//   "Clean Keys" button in the Animation window    -> CleanAnimationKeysButton (Editor/UI/CleanAnimationKeysWindow.cs)
//   Clean Animation Keys result window              -> CleanAnimationKeysWindow (Editor/UI/CleanAnimationKeysWindow.cs)
//   SCRIPT CAUSING FAILED LOAD window               -> Editor/UI/ScriptLoadFailureWindow.cs
//   Constraint Train window                         -> Editor/VRChat/ConstraintTrainWindow.UI.cs
//   Confirmation dialogs (DisplayDialog) stay beside the action they confirm.
