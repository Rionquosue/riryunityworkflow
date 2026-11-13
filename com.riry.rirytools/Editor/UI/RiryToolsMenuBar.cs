using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Every menu bar entry of this package: the Tools dropdown and the Git menu.
    /// Right-click entries (Project window and Hierarchy) are in RiryToolsRightClickMenu.cs.
    /// Constraint Tool's, Menu Tool's and Find and Replace's entries are in
    /// Editor/VRChat/UI/RiryToolsVRChatMenus.cs, because they only compile with the VRChat SDK.
    /// Without the SDK, the same entries at the bottom of this file show a
    /// "VRCSDK not found" popup instead.
    ///
    /// Editing tips:
    /// - Change a path to move or rename an entry; "/" makes a submenu.
    /// - The number is the priority: lower is higher up. A gap of 11 or more
    ///   between neighbours draws a separator.
    /// - A method marked with "true" is the validator: it greys the entry out when
    ///   it returns false, and can set a check mark.
    /// </summary>
    internal static class RiryToolsMenuBar
    {
        // ---- Tools dropdown: one entry or submenu per tool ------------------
        private const string MainMenu = "Tools/RiryTools/Main Menu";
        private const string SyncTools = "Tools/RiryTools/AutoSync/Sync";
        private const string ConfigurationTools = "Tools/RiryTools/AutoSync/Configuration";
        // "&n" makes N its menu key (A is AutoSync's, M Main Menu's and Menu Mockup's).
        private const string AvatarMigratorMenu = "Tools/RiryTools/Avatar Migratio&n Assistant";
        private const string BlendshapeSync = "Tools/RiryTools/Blendshape Sync";
        private const string CleanAnimationKeysMenu = "Tools/RiryTools/Animator Tools/Clean Animation Keys";
        private const string GuidTools = "Tools/RiryTools/GUID Tools";
        // "&o" makes O its menu key (Import Presets has I).
        private const string IconCapMenu = "Tools/RiryTools/Ic&onCap";
        private const string ImportPresetTools = "Tools/RiryTools/Import Presets/Set Folder Import Presets";
        private const string SaveImportPresetTools = "Tools/RiryTools/Import Presets/Save Import Preset";
        private const string ExtractAnimationsTools = "Tools/RiryTools/Import Presets/Extract Animation";
        private const string OpenPresetLibrary = "Tools/RiryTools/Import Presets/Open Import Preset Library";
        private const string MenuMockupMenu = "Tools/RiryTools/Menu Mockup";
        private const string FindNullReferencesMenu = "Tools/RiryTools/Find Null references";
        private const string RefreshScriptsMenu = "Tools/RiryTools/Refresh Scripts";
        private const string StopAutoGameMenu = "Tools/RiryTools/Stop Auto Game";
        private const string ScriptLoadGuardMenu = "Tools/RiryTools/Script Load Guard";
        private const string Documentation = "Tools/RiryTools/Documentation";

        // Where Documentation leads. Change it here when the wiki is up.
        internal const string DocumentationLink = "https://github.com/Rionquosue/riryunityworkflow";

        // ---- Git menu -------------------------------------------------------
        private const string Sync = "Git/Sync";
        private const string Configuration = "Git/Configuration";

        // ---- VRChat tools (also used by RiryToolsVRChatMenus.cs) ------------
        // "&T" makes T its menu key. The space before it is
        // U+00A0, because Unity reads "<space>&..." at the end of a path as an Alt shortcut.
        internal const string ConstraintTool = "Tools/RiryTools/Constraint\u00A0&Tool";
        // "&e" makes E its menu key (M is Main Menu's and Menu Mockup's).
        internal const string MenuTool = "Tools/RiryTools/M&enu Tool";
        internal const string FindReplaceTools = "Tools/RiryTools/Animator Tools/Find and Replace";

        // =====================================================================
        // Tools dropdown
        // =====================================================================

        [MenuItem(MainMenu, false, 1990)]
        private static void MainMenuItem() { RiryToolsMainMenu.Open(); }

        [MenuItem(SyncTools, false, 2000)]
        private static void SyncToolsItem() { AutoSyncGitActions.ToggleSync(); }

        [MenuItem(SyncTools, true)]
        private static bool SyncToolsValid()
        {
            Menu.SetChecked(SyncTools, AutoSync.Enabled);
            return true;
        }

        [MenuItem(ConfigurationTools, false, 2001)]
        private static void ConfigurationToolsItem() { AutoSyncGitActions.OpenConfiguration(); }

        [MenuItem(AvatarMigratorMenu, false, 2005)]
        private static void AvatarMigratorItem() { AvatarMigratorWindow.Open(); }

        [MenuItem(BlendshapeSync, false, 2010)]
        private static void BlendshapeSyncItem() { BlendshapeSyncJobs.Add(); }

        // In the Animator Tools submenu, before Find and Replace (1995).
        [MenuItem(CleanAnimationKeysMenu, false, 1994)]
        private static void CleanAnimationKeysItem() { CleanAnimationKeysWindow.Open(new UnityEngine.AnimationClip[0]); }

        // Constraint Tool is 2030 (RiryToolsVRChatMenus.cs).

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

        [MenuItem(RefreshScriptsMenu, false, 2058)]
        internal static void RefreshScriptsItem() { RefreshScripts.Run(); }

        [MenuItem(StopAutoGameMenu, false, 2060)]
        private static void StopAutoGameItem() { StopAutoGame.Enabled = !StopAutoGame.Enabled; }

        [MenuItem(StopAutoGameMenu, true)]
        private static bool StopAutoGameValid()
        {
            Menu.SetChecked(StopAutoGameMenu, StopAutoGame.Enabled);
            return true;
        }

        [MenuItem(ScriptLoadGuardMenu, false, 2061)]
        private static void ScriptLoadGuardItem() { ScriptLoadGuard.Enabled = !ScriptLoadGuard.Enabled; }

        [MenuItem(ScriptLoadGuardMenu, true)]
        private static bool ScriptLoadGuardValid()
        {
            Menu.SetChecked(ScriptLoadGuardMenu, ScriptLoadGuard.Enabled);
            return true;
        }

        [MenuItem(MenuMockupMenu, false, 2065)]
        private static void MenuMockupItem() { MenuMockup.Open(); }

        [MenuItem(Documentation, false, 2070)]
        private static void DocumentationItem() { UnityEngine.Application.OpenURL(DocumentationLink); }

        // =====================================================================
        // Git menu
        // =====================================================================

        [MenuItem(Sync, false, 1)]
        private static void SyncItem() { AutoSyncGitActions.ToggleSync(); }

        [MenuItem(Sync, true)]
        private static bool SyncValid()
        {
            Menu.SetChecked(Sync, AutoSync.Enabled);
            return true;
        }

        [MenuItem(Configuration, false, 2)]
        private static void ConfigurationItem() { AutoSyncGitActions.OpenConfiguration(); }

        /// <summary>
        /// Shows VrcSdkNotFoundException's message as the "VRCSDK not found" popup. A missing SDK is a setup state, not a
        /// failure, so nothing is logged: the popup says it all. Also used by RiryToolsRightClickMenu.cs.
        /// </summary>
        internal static void VrcSdkMissing(string tool)
        {
            EditorUtility.DisplayDialog("VRCSDK not found", new VrcSdkNotFoundException(tool).Message, "OK");
        }

#if !RIRYTOOLS_VRC_CONSTRAINTS
        // =====================================================================
        // VRChat tools without the VRChat SDK
        // With the SDK, these paths belong to RiryToolsVRChatMenus.cs instead. Keep
        // the priorities of both files in step.
        // =====================================================================

        [MenuItem(ConstraintTool, false, 2030)]
        private static void ConstraintToolItem() { VrcSdkMissing("Constraint Tool"); }

        [MenuItem(MenuTool, false, 2066)]
        private static void MenuToolItem() { VrcSdkMissing("Menu Tool"); }

        [MenuItem(FindReplaceTools, false, 1995)]
        private static void FindReplaceToolsItem() { VrcSdkMissing("Find and Replace"); }
#endif
    }
}

// ============================================================================
// GLOSSARY: menu bar entries
// Path (priority)                                   -> what it calls (file)
// Right-click entries are in RiryToolsRightClickMenu.cs.
// ============================================================================
//
// TOOLS DROPDOWN (one entry or submenu per tool; a first letter selects its entry)
//   Tools/RiryTools/Main Menu (1990)               -> RiryToolsMainMenu.Open (Editor/UI/RiryToolsMainMenu.cs): every tool as a tab
//   Tools/RiryTools/Animator Tools/Clean Animation Keys (1994) -> CleanAnimationKeysWindow.Open (Editor/UI/AnimatorTools/CleanAnimationKeysWindow.cs), empty
//   Tools/RiryTools/Animator Tools/Find and Replace (1995) -> entry in Editor/VRChat/UI/RiryToolsVRChatMenus.cs; without the
//                                                      VRChat SDK -> "VRCSDK not found" popup (this file)
//   Tools/RiryTools/AutoSync/Sync (2000)  [check mark] -> AutoSyncGitActions.ToggleSync / AutoSync.Enabled (Editor/AutoSync/AutoSyncGitActions.cs)
//   Tools/RiryTools/AutoSync/Configuration (2001) -> AutoSyncGitActions.OpenConfiguration -> AutoSyncGitConfigureWindow (Editor/UI/AutoSync/)
//   Tools/RiryTools/Avatar Migration Assistant (2005), menu key N -> AvatarMigratorWindow.Open
//                                                      (Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs); without the VRChat SDK it still
//                                                      opens, after the VRC SDK not detected popup
//   Tools/RiryTools/Blendshape Sync (2010)          -> BlendshapeSyncJobs.Add: a new job in its popup (Editor/UI/BlendshapeSync/BlendshapeSyncJobWindow.cs)
//   Tools/RiryTools/Constraint Tool (2030), menu key T -> ConstraintToolWindow.OpenFromTools (Editor/VRChat/UI/ConstraintToolWindow.cs, entry in
//                                                      Editor/VRChat/UI/RiryToolsVRChatMenus.cs): Train and Bezier tabs, on the one used last;
//                                                      without the VRChat SDK -> "VRCSDK not found" popup (this file)
//   Tools/RiryTools/GUID Tools (2040)               -> GuidToolsWindow.Open (Editor/UI/GuidTools/GuidToolsWindow.cs)
//   Tools/RiryTools/IconCap (2045), menu key O     -> IconCapWindow.Open (Editor/UI/IconCapWindow.cs)
//   Tools/RiryTools/Import Presets/Set Folder Import Presets (2050) -> ImportPresetActions.ChooseFolderPreset -> ImportPresetFolderWindow (Editor/UI/ImportPresetWindows.cs)
//   Tools/RiryTools/Import Presets/Save Import Preset (2051) -> ImportPresetActions.SaveToLibrary
//   Tools/RiryTools/Import Presets/Extract Animation (2052) -> ExtractAnimationWindow.Open (Editor/UI/ExtractAnimationWindow.cs)
//   Tools/RiryTools/Import Presets/Open Import Preset Library (2053) -> ImportPresetActions.OpenLibrary
//   Tools/RiryTools/Find Null references (2056)     -> FindNullReferencesWindow.Run (Editor/UI/FindNullReferencesWindow.cs)
//   Tools/RiryTools/Refresh Scripts (2058)          -> RefreshScripts.Run: refresh, reload, reopen RiryTools windows (also the Main Menu's Tools tab)
//   Tools/RiryTools/Stop Auto Game (2060)  [check mark] -> StopAutoGame.Enabled (Editor/StopAutoGame.cs)
//   Tools/RiryTools/Script Load Guard (2061)  [check mark] -> ScriptLoadGuard.Enabled (Editor/Debug/ScriptLoadGuard.cs)
//   Tools/RiryTools/Menu Mockup (2065)              -> MenuMockup.Open (Editor/MenuMockup.cs) -> MenuMockupEditWindow and MenuMockupComponentsWindow (Editor/UI/MenuMockupWindows.cs)
//   Tools/RiryTools/Menu Tool (2066), menu key E   -> MenuToolWindow.Open (Editor/VRChat/UI/MenuToolWindow.cs, entry in
//                                                      Editor/VRChat/UI/RiryToolsVRChatMenus.cs); without the VRChat SDK -> "VRCSDK not
//                                                      found" popup (this file)
//   Tools/RiryTools/Documentation (2070)            -> opens DocumentationLink (this file) in the browser
//
// GIT MENU (main menu bar)
//   Git/Sync (1)  [check mark]                      -> AutoSyncGitActions.ToggleSync / AutoSync.Enabled (Editor/AutoSync/AutoSyncGitActions.cs)
//   Git/Configuration (2)                        -> AutoSyncGitActions.OpenConfiguration -> AutoSyncGitConfigureWindow (Editor/UI/AutoSync/)
//
// VrcSdkMissing(tool)   The "VRCSDK not found" popup, shared with RiryToolsRightClickMenu.cs.
//
// OTHER UI OUTSIDE MENUS
//   "Clean Keys" button in the Animation window    -> CleanAnimationKeysWindow.Open with that window's clip (Editor/UI/AnimatorTools/CleanAnimationKeysButton.cs)
//   SCRIPT CAUSING FAILED LOAD window               -> Editor/UI/Components/ScriptLoadFailureWindow.cs
//   Constraint Tool window                          -> Editor/VRChat/UI/ConstraintToolWindow.cs; its Train tab Editor/VRChat/UI/ConstraintTrainWindow.UI.cs,
//                                                      its Bezier tab Editor/VRChat/UI/ConstraintBezierWindow.UI.cs
//   Confirmation dialogs (DisplayDialog) stay beside the action they confirm.
