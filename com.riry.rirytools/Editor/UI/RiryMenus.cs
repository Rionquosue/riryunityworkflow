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
        // ---- Tools dropdown: one entry or submenu per tool ------------------
        private const string SyncTools = "Tools/RiryTools/AutoSync/Sync";
        private const string ConfigurationTools = "Tools/RiryTools/AutoSync/Configuration";
        private const string BlendshapeSync = "Tools/RiryTools/Blendshape Sync";
        private const string CleanAnimationKeysMenu = "Tools/RiryTools/Clean Animation Keys";
        private const string GuidTools = "Tools/RiryTools/GUID Tools";
        private const string ImportPresetTools = "Tools/RiryTools/Import Presets/Import Preset";
        private const string SaveImportPresetTools = "Tools/RiryTools/Import Presets/Save Import Settings to Preset Library";
        private const string ExtractAnimationsTools = "Tools/RiryTools/Import Presets/Extract Animations (Generic)";
        private const string OpenPresetLibrary = "Tools/RiryTools/Import Presets/Open Import Preset Library";
        private const string MenuMockupMenu = "Tools/RiryTools/Menu Mockup";
        private const string StopAutoGameMenu = "Tools/RiryTools/Stop Auto Game";
        private const string Documentation = "Tools/RiryTools/Documentation";

        // Where Documentation leads. Change it here when the wiki is up.
        internal const string DocumentationLink = "https://github.com/Rionquosue/riryunityworkflow";

        // ---- Project window right-click -------------------------------------
        private const string RegenerateGuidAssets = "Assets/RiryTools/Regenerate GUID";
        private const string ImportPreset = "Assets/RiryTools/Import Preset";
        private const string SaveImportPreset = "Assets/RiryTools/Save Import Settings to Preset Library";
        private const string ExtractAnimations = "Assets/RiryTools/Extract Animations (Generic)";
        private const string RepairGuidReferences = "Assets/RiryTools/Repair Missing GUID References";
        private const string CleanAnimationKeysAssets = "Assets/RiryTools/Clean Animation Keys";

        // ---- Git menu -------------------------------------------------------
        private const string Sync = "Git/Sync";
        private const string Configuration = "Git/Configuration";

        // ---- VRChat tools (also used by RiryVRChatMenus.cs) -----------------
        // "&T" makes T its menu key (Clean Animation Keys has C). The space before it is
        // U+00A0, because Unity reads "<space>&..." at the end of a path as an Alt shortcut.
        internal const string ConstraintTrain = "Tools/RiryTools/Constraint\u00A0&Train";
        internal const string ConstraintTrainHierarchy = "GameObject/RiryTools/Constraint Train";

        // =====================================================================
        // Tools dropdown
        // =====================================================================

        [MenuItem(SyncTools, false, 2000)]
        private static void SyncToolsItem() { GitActions.ToggleSync(); }

        [MenuItem(SyncTools, true)]
        private static bool SyncToolsValid()
        {
            Menu.SetChecked(SyncTools, AutoSync.Enabled);
            return true;
        }

        [MenuItem(ConfigurationTools, false, 2001)]
        private static void ConfigurationToolsItem() { GitActions.OpenConfiguration(); }

        [MenuItem(BlendshapeSync, false, 2010)]
        private static void BlendshapeSyncItem() { BlendshapeSyncWindow.Open(); }

        [MenuItem(CleanAnimationKeysMenu, false, 2020)]
        private static void CleanAnimationKeysItem() { CleanAnimationKeysWindow.Open(new UnityEngine.AnimationClip[0]); }

        // Constraint Train is 2030 (RiryVRChatMenus.cs).

        [MenuItem(GuidTools, false, 2040)]
        private static void GuidToolsItem() { GuidToolsWindow.Open(); }

        [MenuItem(ImportPresetTools, false, 2050)]
        private static void ImportPresetToolsItem() { ImportPresetActions.ChooseFolderPreset(); }

        [MenuItem(ImportPresetTools, true)]
        private static bool ImportPresetToolsValid() { return ImportPresetActions.ValidateChooseFolderPreset(); }

        [MenuItem(SaveImportPresetTools, false, 2051)]
        private static void SaveImportPresetToolsItem() { ImportPresetActions.SaveToLibrary(); }

        [MenuItem(SaveImportPresetTools, true)]
        private static bool SaveImportPresetToolsValid() { return ImportPresetActions.ValidateSaveToLibrary(); }

        [MenuItem(ExtractAnimationsTools, false, 2052)]
        private static void ExtractAnimationsToolsItem() { ImportPresetAnimations.ExtractSelected(); }

        [MenuItem(ExtractAnimationsTools, true)]
        private static bool ExtractAnimationsToolsValid() { return ImportPresetAnimations.ValidateExtractSelected(); }

        [MenuItem(OpenPresetLibrary, false, 2053)]
        private static void OpenPresetLibraryItem() { ImportPresetActions.OpenLibrary(); }

        [MenuItem(StopAutoGameMenu, false, 2060)]
        private static void StopAutoGameItem() { StopAutoGame.Enabled = !StopAutoGame.Enabled; }

        [MenuItem(StopAutoGameMenu, true)]
        private static bool StopAutoGameValid()
        {
            Menu.SetChecked(StopAutoGameMenu, StopAutoGame.Enabled);
            return true;
        }

        [MenuItem(MenuMockupMenu, false, 2065)]
        private static void MenuMockupItem() { MenuMockup.Open(); }

        [MenuItem(Documentation, false, 2070)]
        private static void DocumentationItem() { UnityEngine.Application.OpenURL(DocumentationLink); }

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
        private static void CleanAnimationKeysAssetsItem()
        {
            CleanAnimationKeysWindow.Open(Selection.GetFiltered<UnityEngine.AnimationClip>(SelectionMode.Assets));
        }

        [MenuItem(CleanAnimationKeysAssets, true)]
        private static bool CleanAnimationKeysAssetsValid() { return Selection.GetFiltered<UnityEngine.AnimationClip>(SelectionMode.Assets).Length > 0; }

        // =====================================================================
        // Git menu
        // =====================================================================

        [MenuItem(Sync, false, 1)]
        private static void SyncItem() { GitActions.ToggleSync(); }

        [MenuItem(Sync, true)]
        private static bool SyncValid()
        {
            Menu.SetChecked(Sync, AutoSync.Enabled);
            return true;
        }

        [MenuItem(Configuration, false, 2)]
        private static void ConfigurationItem() { GitActions.OpenConfiguration(); }

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
// TOOLS DROPDOWN (one entry or submenu per tool; a first letter selects its entry)
//   Tools/RiryTools/AutoSync/Sync (2000)  [check mark] -> GitActions.ToggleSync / AutoSync.Enabled (Editor/AutoSync/GitActions.cs)
//   Tools/RiryTools/AutoSync/Configuration (2001) -> GitActions.OpenConfiguration -> GitConfigureWindow (Editor/UI/GitWindows.cs)
//   Tools/RiryTools/Blendshape Sync (2010)          -> BlendshapeSyncWindow.Open (Editor/UI/BlendshapeSyncWindow.UI.cs)
//   Tools/RiryTools/Clean Animation Keys (2020)     -> CleanAnimationKeysWindow.Open (Editor/UI/CleanAnimationKeysWindow.cs), empty
//   Tools/RiryTools/Constraint Train (2030), menu key T -> ConstraintTrainWindow.Open (Editor/VRChat/ConstraintTrainWindow.UI.cs, entry in
//                                                      Editor/VRChat/RiryVRChatMenus.cs); without the VRChat SDK -> "VRCSDK not found" popup (this file)
//   Tools/RiryTools/GUID Tools (2040)               -> GuidToolsWindow.Open (Editor/UI/GuidToolsWindow.cs)
//   Tools/RiryTools/Import Presets/Import Preset (2050) -> ImportPresetActions.ChooseFolderPreset (Editor/ImportPresets/ImportPresetActions.cs)
//   Tools/RiryTools/Import Presets/Save Import Settings to Preset Library (2051) -> ImportPresetActions.SaveToLibrary
//   Tools/RiryTools/Import Presets/Extract Animations (Generic) (2052) -> ImportPresetAnimations.ExtractSelected
//   Tools/RiryTools/Import Presets/Open Import Preset Library (2053) -> ImportPresetActions.OpenLibrary
//   Tools/RiryTools/Stop Auto Game (2060)  [check mark] -> StopAutoGame.Enabled (Editor/StopAutoGame.cs)
//   Tools/RiryTools/Menu Mockup (2065)              -> MenuMockup.Open (Editor/MenuMockup.cs), a placeholder that does nothing yet
//   Tools/RiryTools/Documentation (2070)            -> opens DocumentationLink (this file) in the browser
//
// PROJECT WINDOW RIGHT-CLICK
//   Assets/RiryTools/Regenerate GUID (2000)         -> RegenerateGuid.RegenerateSelectedGuids (Editor/GuidTools/RegenerateGuid.cs)
//   Assets/RiryTools/Import Preset (2001)        -> ImportPresetActions.ChooseFolderPreset -> ImportPresetFolderWindow (Editor/UI/ImportPresetWindows.cs)
//   Assets/RiryTools/Save Import Settings to Preset Library (2002) -> ImportPresetActions.SaveToLibrary -> ImportPresetSaveWindow
//   Assets/RiryTools/Extract Animations (Generic) (2003) -> ImportPresetAnimations.ExtractSelected (Editor/ImportPresets/ImportPresetAnimations.cs)
//   Assets/RiryTools/Repair Missing GUID References (2004) -> GuidReferenceRepair.RepairSelected -> BrokenReferenceWindow (Editor/UI/BrokenReferenceWindow.UI.cs)
//   Assets/RiryTools/Clean Animation Keys (2005)    -> CleanAnimationKeysWindow.Open with the selected AnimationClips
//
// HIERARCHY RIGHT-CLICK
//   GameObject/RiryTools/Constraint Train (20)   -> ConstraintTrainWindow.OpenFromHierarchy (entry in Editor/VRChat/RiryVRChatMenus.cs);
//                                                      without the VRChat SDK -> "VRCSDK not found" popup (this file)
//
// GIT MENU (main menu bar)
//   Git/Sync (1)  [check mark]                      -> GitActions.ToggleSync / AutoSync.Enabled (Editor/AutoSync/GitActions.cs)
//   Git/Configuration (2)                        -> GitActions.OpenConfiguration -> GitConfigureWindow (Editor/UI/GitWindows.cs)
//
// OTHER UI OUTSIDE MENUS
//   "Clean Keys" button in the Animation window    -> CleanAnimationKeysWindow.Open with that window's clip (Editor/UI/CleanAnimationKeysWindow.cs)
//   SCRIPT CAUSING FAILED LOAD window               -> Editor/UI/ScriptLoadFailureWindow.cs
//   Constraint Train window                         -> Editor/VRChat/ConstraintTrainWindow.UI.cs
//   Confirmation dialogs (DisplayDialog) stay beside the action they confirm.
