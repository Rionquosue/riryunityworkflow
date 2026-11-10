using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Every menu entry of this package in one place: the Tools dropdown, the
    /// Project window right-click menu (Assets/...), and the Git menu. Constraint
    /// Tool's entries (Tools and Hierarchy right-click) are in
    /// Editor/UI/VRChat/RiryVRChatMenus.cs, because they only compile with the VRChat SDK.
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
        private const string MainMenu = "Tools/RiryTools/Main Menu";
        private const string SyncTools = "Tools/RiryTools/AutoSync/Sync";
        private const string ConfigurationTools = "Tools/RiryTools/AutoSync/Configuration";
        // "&n" makes N its menu key (A is AutoSync's, M Main Menu's and Menu Mockup's).
        private const string AvatarMigrationAssistantMenu = "Tools/RiryTools/Avatar Migratio&n Assistant";
        private const string BlendshapeSync = "Tools/RiryTools/Blendshape Sync";
        private const string CleanAnimationKeysMenu = "Tools/RiryTools/Clean Animation Keys";
        private const string GuidTools = "Tools/RiryTools/GUID Tools";
        // "&o" makes O its menu key (Import Presets has I).
        private const string IconCapMenu = "Tools/RiryTools/Ic&onCap";
        private const string ImportPresetTools = "Tools/RiryTools/Import Presets/Set Folder Import Presets";
        private const string SaveImportPresetTools = "Tools/RiryTools/Import Presets/Save Import Preset";
        private const string ExtractAnimationsTools = "Tools/RiryTools/Import Presets/Extract Animation";
        private const string OpenPresetLibrary = "Tools/RiryTools/Import Presets/Open Import Preset Library";
        private const string MenuMockupMenu = "Tools/RiryTools/Menu Mockup";
        private const string FindNullReferencesMenu = "Tools/RiryTools/Find Null references";
        private const string ReloadDomainMenu = "Tools/RiryTools/Reload domain";
        private const string StopAutoGameMenu = "Tools/RiryTools/Stop Auto Game";
        private const string Documentation = "Tools/RiryTools/Documentation";

        // Where Documentation leads. Change it here when the wiki is up.
        internal const string DocumentationLink = "https://github.com/Rionquosue/riryunityworkflow";

        // ---- Project window right-click -------------------------------------
        private const string RegenerateGuidAssets = "Assets/RiryTools/Regenerate GUID";
        private const string ImportPreset = "Assets/RiryTools/Set Folder Import Presets";
        private const string SaveImportPreset = "Assets/RiryTools/Save Import Preset";
        private const string ExtractAnimations = "Assets/RiryTools/Extract Animation";
        private const string RepairGuidReferences = "Assets/RiryTools/Repair Missing GUID References";
        private const string CleanAnimationKeysAssets = "Assets/RiryTools/Clean Animation Keys";

        // ---- Hierarchy right-click -------------------------------------------
        private const string AttachToHierarchy = "GameObject/RiryTools/Attach To";

        // ---- Git menu -------------------------------------------------------
        private const string Sync = "Git/Sync";
        private const string Configuration = "Git/Configuration";

        // ---- VRChat tools (also used by RiryVRChatMenus.cs) -----------------
        // "&T" makes T its menu key (Clean Animation Keys has C). The space before it is
        // U+00A0, because Unity reads "<space>&..." at the end of a path as an Alt shortcut.
        internal const string ConstraintTool = "Tools/RiryTools/Constraint\u00A0&Tool";
        internal const string ConstraintTrainHierarchy = "GameObject/RiryTools/Constraint Tool/Train";
        internal const string ConstraintBezierHierarchy = "GameObject/RiryTools/Constraint Tool/Bezier";
        // "&e" makes E its menu key (M is Main Menu's and Menu Mockup's).
        internal const string MenuTool = "Tools/RiryTools/M&enu Tool";
        internal const string FindReplaceTools = "Tools/RiryTools/Animator Tools/Find and Replace";
        internal const string FindReplaceHierarchy = "GameObject/RiryTools/Animator tools/Find And Replace";
        internal const string FindReplaceAssets = "Assets/RiryTools/Animator Tools/Find And Replace";

        // =====================================================================
        // Tools dropdown
        // =====================================================================

        [MenuItem(MainMenu, false, 1990)]
        private static void MainMenuItem() { RiryToolsMainMenu.Open(); }

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

        [MenuItem(AvatarMigrationAssistantMenu, false, 2005)]
        private static void AvatarMigrationAssistantItem() { AvatarMigrationAssistant.Open(); }

        [MenuItem(BlendshapeSync, false, 2010)]
        private static void BlendshapeSyncItem() { BlendshapeSyncJobs.Add(); }

        [MenuItem(CleanAnimationKeysMenu, false, 2020)]
        private static void CleanAnimationKeysItem() { CleanAnimationKeysWindow.Open(new UnityEngine.AnimationClip[0]); }

        // Constraint Tool is 2030 (RiryVRChatMenus.cs).

        [MenuItem(GuidTools, false, 2040)]
        private static void GuidToolsItem() { GuidToolsWindow.Open(); }

        [MenuItem(IconCapMenu, false, 2045)]
        private static void IconCapItem() { IconCapWindow.Open(); }

        [MenuItem(ImportPresetTools, false, 2050)]
        private static void ImportPresetToolsItem() { ImportPresetActions.ChooseFolderPreset(); }

        [MenuItem(SaveImportPresetTools, false, 2051)]
        private static void SaveImportPresetToolsItem() { ImportPresetActions.SaveToLibrary(); }

        [MenuItem(SaveImportPresetTools, true)]
        private static bool SaveImportPresetToolsValid() { return ImportPresetActions.ValidateSaveToLibrary(); }

        [MenuItem(ExtractAnimationsTools, false, 2052)]
        private static void ExtractAnimationsToolsItem() { ExtractAnimationWindow.Open(); }

        [MenuItem(OpenPresetLibrary, false, 2053)]
        private static void OpenPresetLibraryItem() { ImportPresetActions.OpenLibrary(); }

        [MenuItem(FindNullReferencesMenu, false, 2056)]
        private static void FindNullReferencesItem() { FindNullReferencesWindow.Run(); }

        // Unity 2022.3 has no menu item for this, only the Play Mode setting in Project Settings > Editor.
        [MenuItem(ReloadDomainMenu, false, 2058)]
        internal static void ReloadDomain() { EditorUtility.RequestScriptReload(); }

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
        private static void ImportPresetItem() { ImportPresetActions.QuickFolderPreset(); }

        [MenuItem(ImportPreset, true)]
        private static bool ImportPresetValid() { return ImportPresetActions.ValidateChooseFolderPreset(); }

        [MenuItem(SaveImportPreset, false, 2002)]
        private static void SaveImportPresetItem() { ImportPresetActions.SaveToLibrary(); }

        [MenuItem(SaveImportPreset, true)]
        private static bool SaveImportPresetValid() { return ImportPresetActions.ValidateSaveToLibrary(); }

        [MenuItem(ExtractAnimations, false, 2003)]
        private static void ExtractAnimationsItem() { ExtractAnimationWindow.Open(); }

        [MenuItem(ExtractAnimations, true)]
        private static bool ExtractAnimationsValid()
        {
            // Only a model with embedded animations.
            string model = ImportPresetAnimations.SelectedModel();
            return model != null && ImportPresetAnimations.HasClips(model);
        }

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
        // Hierarchy right-click (also in the GameObject menu)
        // =====================================================================

        [MenuItem(AttachToHierarchy, false, 19)]
        private static void AttachToItem() { AttachToWindow.Open(); }

        [MenuItem(AttachToHierarchy, true)]
        private static bool AttachToValid() { return AttachTo.Selected().Count > 0; }

        // Constraint Tool/Train is 20 and Constraint Tool/Bezier 21 (RiryVRChatMenus.cs, or below without the VRChat SDK).

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

        [MenuItem(ConstraintTool, false, 2030)]
        private static void ConstraintToolItem() { VrcSdkMissing("Constraint Tool"); }

        [MenuItem(ConstraintTrainHierarchy, false, 20)]
        private static void ConstraintTrainHierarchyItem() { VrcSdkMissing("Constraint Tool"); }

        [MenuItem(ConstraintBezierHierarchy, false, 21)]
        private static void ConstraintBezierHierarchyItem() { VrcSdkMissing("Constraint Tool"); }

        [MenuItem(MenuTool, false, 2066)]
        private static void MenuToolItem() { VrcSdkMissing("Menu Tool"); }

        [MenuItem(FindReplaceTools, false, 1995)]
        private static void FindReplaceToolsItem() { VrcSdkMissing("Find and Replace"); }

        [MenuItem(FindReplaceHierarchy, false, 22)]
        private static void FindReplaceHierarchyItem() { VrcSdkMissing("Find and Replace"); }

        [MenuItem(FindReplaceAssets, false, 2006)]
        private static void FindReplaceAssetsItem() { VrcSdkMissing("Find and Replace"); }

        /// <summary>
        /// Shows VrcSdkNotFoundException's message as the "VRCSDK not found" popup. A missing SDK is a setup state, not a
        /// failure, so nothing is logged: the popup says it all.
        /// </summary>
        private static void VrcSdkMissing(string tool)
        {
            EditorUtility.DisplayDialog("VRCSDK not found", new VrcSdkNotFoundException(tool).Message, "OK");
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
//   Tools/RiryTools/Main Menu (1990)               -> RiryToolsMainMenu.Open (Editor/UI/RiryToolsMainMenu.cs): every tool as a tab
//   Tools/RiryTools/AutoSync/Sync (2000)  [check mark] -> GitActions.ToggleSync / AutoSync.Enabled (Editor/AutoSync/GitActions.cs)
//   Tools/RiryTools/AutoSync/Configuration (2001) -> GitActions.OpenConfiguration -> GitConfigureWindow (Editor/UI/GitWindows.cs)
//   Tools/RiryTools/Avatar Migration Assistant (2005), menu key N -> AvatarMigrationAssistant.Open
//                                                      (Editor/AvatarMigrationAssistant.cs); without the VRChat SDK it still
//                                                      opens, after the VRC SDK not detected popup
//   Tools/RiryTools/Blendshape Sync (2010)          -> BlendshapeSyncJobs.Add: a new job in its popup (Editor/UI/BlendshapeSyncJobWindow.cs)
//   Tools/RiryTools/Clean Animation Keys (2020)     -> CleanAnimationKeysWindow.Open (Editor/UI/CleanAnimationKeysWindow.cs), empty
//   Tools/RiryTools/Constraint Tool (2030), menu key T -> ConstraintToolWindow.OpenFromTools (Editor/VRChat/ConstraintToolWindow.cs, entry in
//                                                      Editor/VRChat/RiryVRChatMenus.cs): Train and Bezier tabs, on the one used last;
//                                                      without the VRChat SDK -> "VRCSDK not found" popup (this file)
//   Tools/RiryTools/GUID Tools (2040)               -> GuidToolsWindow.Open (Editor/UI/GuidToolsWindow.cs)
//   Tools/RiryTools/IconCap (2045), menu key O     -> IconCapWindow.Open (Editor/UI/IconCapWindow.cs)
//   Tools/RiryTools/Import Presets/Set Folder Import Presets (2050) -> ImportPresetActions.ChooseFolderPreset -> ImportPresetFolderWindow (Editor/UI/ImportPresetWindows.cs)
//   Tools/RiryTools/Import Presets/Save Import Preset (2051) -> ImportPresetActions.SaveToLibrary
//   Tools/RiryTools/Import Presets/Extract Animation (2052) -> ExtractAnimationWindow.Open (Editor/UI/ExtractAnimationWindow.cs)
//   Tools/RiryTools/Import Presets/Open Import Preset Library (2053) -> ImportPresetActions.OpenLibrary
//   Tools/RiryTools/Reload domain (2058)            -> ReloadDomain: EditorUtility.RequestScriptReload (also the Main Menu's Tools tab)
//   Tools/RiryTools/Stop Auto Game (2060)  [check mark] -> StopAutoGame.Enabled (Editor/StopAutoGame.cs)
//   Tools/RiryTools/Menu Mockup (2065)              -> MenuMockup.Open (Editor/MenuMockup.cs) -> MenuMockupEditWindow and MenuMockupComponentsWindow (Editor/UI/MenuMockupWindows.cs)
//   Tools/RiryTools/Menu Tool (2066), menu key E   -> MenuToolWindow.Open (Editor/VRChat/MenuTool/MenuToolWindow.cs, entry in
//                                                      Editor/VRChat/RiryVRChatMenus.cs); without the VRChat SDK -> "VRCSDK not
//                                                      found" popup (this file)
//   Tools/RiryTools/Documentation (2070)            -> opens DocumentationLink (this file) in the browser
//
// PROJECT WINDOW RIGHT-CLICK
//   Assets/RiryTools/Regenerate GUID (2000)         -> RegenerateGuid.RegenerateSelectedGuids (Editor/GuidTools/RegenerateGuid.cs)
//   Assets/RiryTools/Set Folder Import Presets (2001) -> ImportPresetActions.QuickFolderPreset -> ImportPresetQuickWindow (Editor/UI/ImportPresetQuickWindow.cs)
//   Assets/RiryTools/Save Import Preset (2002) -> ImportPresetActions.SaveToLibrary -> ImportPresetSaveWindow
//   Assets/RiryTools/Extract Animation (2003) -> ExtractAnimationWindow.Open with the selected model (Editor/UI/ExtractAnimationWindow.cs);
//                                                      enabled only for a model with embedded animations
//   Assets/RiryTools/Repair Missing GUID References (2004) -> GuidReferenceRepair.RepairSelected -> BrokenReferenceWindow (Editor/UI/BrokenReferenceWindow.UI.cs)
//   Assets/RiryTools/Clean Animation Keys (2005)    -> CleanAnimationKeysWindow.Open with the selected AnimationClips
//
// HIERARCHY RIGHT-CLICK (also in the GameObject menu)
//   GameObject/RiryTools/Attach To (19)         -> AttachToWindow.Open (Editor/UI/AttachToWindow.cs): the Attach To popup;
//                                                      enabled when scene objects are selected (AttachTo.Selected, Editor/AttachTo.cs)
//   GameObject/RiryTools/Constraint Tool/Train (20)  -> ConstraintToolWindow.OpenTrainFromHierarchy (entry in Editor/VRChat/RiryVRChatMenus.cs)
//   GameObject/RiryTools/Constraint Tool/Bezier (21) -> ConstraintToolWindow.OpenBezierFromHierarchy (same file); both use the selection
//                                                      as the chain; without the VRChat SDK -> "VRCSDK not found" popup (this file)
//
// GIT MENU (main menu bar)
//   Git/Sync (1)  [check mark]                      -> GitActions.ToggleSync / AutoSync.Enabled (Editor/AutoSync/GitActions.cs)
//   Git/Configuration (2)                        -> GitActions.OpenConfiguration -> GitConfigureWindow (Editor/UI/GitWindows.cs)
//
// OTHER UI OUTSIDE MENUS
//   "Clean Keys" button in the Animation window    -> CleanAnimationKeysWindow.Open with that window's clip (Editor/UI/CleanAnimationKeysWindow.cs)
//   SCRIPT CAUSING FAILED LOAD window               -> Editor/UI/ScriptLoadFailureWindow.cs
//   Constraint Tool window                          -> Editor/VRChat/ConstraintToolWindow.cs; its Train tab Editor/VRChat/ConstraintTrainWindow.UI.cs,
//                                                      its Bezier tab Editor/VRChat/ConstraintBezierWindow.UI.cs
//   Confirmation dialogs (DisplayDialog) stay beside the action they confirm.
