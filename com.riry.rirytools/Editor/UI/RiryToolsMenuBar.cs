using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Every menu bar entry of this package: the Tools dropdown and the Git menu.
    /// Right-click entries (Project window and Hierarchy) are in RiryToolsRightClickMenu.cs.
    /// Constraint Tools', Menu Tools', Find and Replace's and FTEmulator's entries are in
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
        private const string ControllerCleanerMenu = "Tools/RiryTools/Animator Tools/Controller Cleaner";
        private const string MaterialCleanerMenu = "Tools/RiryTools/Utilities/Material Cleaner";
        private const string GuidTools = "Tools/RiryTools/GUID Tools";
        // "&o" makes O its menu key (Import Presets has I).
        private const string IconCapMenu = "Tools/RiryTools/Ic&onCap";
        private const string ImportPresetTools = "Tools/RiryTools/Import Presets/Set Folder Import Presets";
        private const string SaveImportPresetTools = "Tools/RiryTools/Import Presets/Save Import Preset";
        private const string ExtractAnimationsTools = "Tools/RiryTools/Animator Tools/Extract Animations";
        private const string OpenPresetLibrary = "Tools/RiryTools/Import Presets/Open Import Preset Library";
        private const string MenuMockupMenu = "Tools/RiryTools/Debug Tools/Menu Mockup";
        private const string FindNullReferencesMenu = "Tools/RiryTools/Find Null references";
        private const string RefreshScriptsMenu = "Tools/RiryTools/Debug Tools/" + RefreshScripts.DisplayName;
        private const string StopAllToolScriptsMenu = "Tools/RiryTools/Debug Tools/" + StopAllToolScripts.DisplayName;
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
        internal const string ConstraintTool = "Tools/RiryTools/Constraint\u00A0&Tools";
        // "&e" makes E its menu key (M is Main Menu's and Menu Mockup's).
        internal const string MenuTool = "Tools/RiryTools/M&enu Tools";
        // Find and Replace's place under RiryTools, the same in the Tools menu and the Project and Hierarchy right-click menus.
        internal const string FindReplaceLabel = "Animator Tools/Find and Replace";
        internal const string FindReplaceTools = "Tools/RiryTools/" + FindReplaceLabel;
        // FTEmulator is in the Tools menu and the Main Menu's FTEmulator tab only: it has no right-click entry.
        internal const string FTEmulatorTools = "Tools/RiryTools/FTEmulator";

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
            if (StopAllToolScripts.Active) return false;
            Menu.SetChecked(SyncTools, AutoSync.Enabled);
            return true;
        }

        [MenuItem(ConfigurationTools, false, 2001)]
        private static void ConfigurationToolsItem() { AutoSyncGitActions.OpenConfiguration(); }



        [MenuItem(ConfigurationTools, true)]


        private static bool ConfigurationToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(AvatarMigratorMenu, false, 2005)]
        private static void AvatarMigratorItem() { AvatarMigratorWindow.Open(); }


        [MenuItem(AvatarMigratorMenu, true)]

        private static bool AvatarMigratorValid() { return !StopAllToolScripts.Active; }

        [MenuItem(BlendshapeSync, false, 2010)]
        private static void BlendshapeSyncItem() { BlendshapeSyncJobs.Add(); }


        [MenuItem(BlendshapeSync, true)]

        private static bool BlendshapeSyncValid() { return !StopAllToolScripts.Active; }

        // In the Animator Tools submenu, before Find and Replace (1995).
        [MenuItem(CleanAnimationKeysMenu, false, 1994)]
        private static void CleanAnimationKeysItem() { CleanAnimationKeysWindow.Open(new UnityEngine.AnimationClip[0]); }


        [MenuItem(CleanAnimationKeysMenu, true)]

        private static bool CleanAnimationKeysValid() { return !StopAllToolScripts.Active; }

        // After Find and Replace (1995).
        [MenuItem(ControllerCleanerMenu, false, 1996)]
        private static void ControllerCleanerItem() { ControllerCleanerWindow.Open(); }


        [MenuItem(ControllerCleanerMenu, true)]

        private static bool ControllerCleanerValid() { return !StopAllToolScripts.Active; }

        // Constraint Tools is 2030 (RiryToolsVRChatMenus.cs).

        [MenuItem(MaterialCleanerMenu, false, 2042)]
        private static void MaterialCleanerItem() { MaterialCleanerWindow.Open(); }

        [MenuItem(MaterialCleanerMenu, true)]
        private static bool MaterialCleanerValid() { return !StopAllToolScripts.Active; }

        [MenuItem(GuidTools, false, 2040)]
        private static void GuidToolsItem() { GuidToolsWindow.Open(); }



        [MenuItem(GuidTools, true)]


        private static bool GuidToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(IconCapMenu, false, 2045)]
        private static void IconCapItem() { IconCapWindow.Open(); }


        [MenuItem(IconCapMenu, true)]

        private static bool IconCapValid() { return !StopAllToolScripts.Active; }

        [MenuItem(ImportPresetTools, false, 2050)]
        private static void ImportPresetToolsItem() { ImportPresetActions.ChooseFolderPreset(); }


        [MenuItem(ImportPresetTools, true)]

        private static bool ImportPresetToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(SaveImportPresetTools, false, 2051)]
        private static void SaveImportPresetToolsItem() { ImportPresetActions.SaveToLibrary(); }

        [MenuItem(SaveImportPresetTools, true)]
        private static bool SaveImportPresetToolsValid() { return !StopAllToolScripts.Active && (ImportPresetActions.ValidateSaveToLibrary()); }

        // In the Animator Tools submenu, after Controller Cleaner (1996).
        [MenuItem(ExtractAnimationsTools, false, 1997)]
        private static void ExtractAnimationsToolsItem() { ImportPresetExtractAnimationWindow.Open(); }


        [MenuItem(ExtractAnimationsTools, true)]

        private static bool ExtractAnimationsToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(OpenPresetLibrary, false, 2053)]
        private static void OpenPresetLibraryItem() { ImportPresetActions.OpenLibrary(); }


        [MenuItem(OpenPresetLibrary, true)]

        private static bool OpenPresetLibraryValid() { return !StopAllToolScripts.Active; }

        [MenuItem(FindNullReferencesMenu, false, 2056)]
        private static void FindNullReferencesItem() { FindNullReferencesWindow.Run(); }


        [MenuItem(FindNullReferencesMenu, true)]

        private static bool FindNullReferencesValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RefreshScriptsMenu, false, 2058)]
        internal static void RefreshScriptsItem() { RefreshScripts.Run(); }


        [MenuItem(RefreshScriptsMenu, true)]

        private static bool RefreshScriptsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(StopAutoGameMenu, false, 2060)]
        private static void StopAutoGameItem() { StopAutoGame.Enabled = !StopAutoGame.Enabled; }

        [MenuItem(StopAutoGameMenu, true)]
        private static bool StopAutoGameValid()
        {
            if (StopAllToolScripts.Active) return false;
            Menu.SetChecked(StopAutoGameMenu, StopAutoGame.Enabled);
            return true;
        }

        [MenuItem(ScriptLoadGuardMenu, false, 2061)]
        private static void ScriptLoadGuardItem() { ScriptLoadGuard.Enabled = !ScriptLoadGuard.Enabled; }

        [MenuItem(ScriptLoadGuardMenu, true)]
        private static bool ScriptLoadGuardValid()
        {
            if (StopAllToolScripts.Active) return false;
            Menu.SetChecked(ScriptLoadGuardMenu, ScriptLoadGuard.Enabled);
            return true;
        }

        [MenuItem(MenuMockupMenu, false, 2065)]
        private static void MenuMockupItem() { MenuMockup.Open(); }



        [MenuItem(MenuMockupMenu, true)]


        private static bool MenuMockupValid() { return !StopAllToolScripts.Active; }

        // Works while stopped: it is how the stop is turned off.
        [MenuItem(StopAllToolScriptsMenu, false, 2068)]
        private static void StopAllToolScriptsItem() { StopAllToolScripts.Set(!StopAllToolScripts.Active); }

        [MenuItem(StopAllToolScriptsMenu, true)]
        private static bool StopAllToolScriptsValid()
        {
            Menu.SetChecked(StopAllToolScriptsMenu, StopAllToolScripts.Active);
            return true;
        }

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
            if (StopAllToolScripts.Active) return false;
            Menu.SetChecked(Sync, AutoSync.Enabled);
            return true;
        }

        [MenuItem(Configuration, false, 2)]
        private static void ConfigurationItem() { AutoSyncGitActions.OpenConfiguration(); }



        [MenuItem(Configuration, true)]


        private static bool ConfigurationValid() { return !StopAllToolScripts.Active; }

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
        private static void ConstraintToolItem() { VrcSdkMissing("Constraint Tools"); }



        [MenuItem(ConstraintTool, true)]


        private static bool ConstraintToolValid() { return !StopAllToolScripts.Active; }

        [MenuItem(MenuTool, false, 2066)]
        private static void MenuToolItem() { VrcSdkMissing("Menu Tools"); }


        [MenuItem(MenuTool, true)]

        private static bool MenuToolValid() { return !StopAllToolScripts.Active; }

        [MenuItem(FindReplaceTools, false, 1995)]
        private static void FindReplaceToolsItem() { VrcSdkMissing("Find and Replace"); }


        [MenuItem(FindReplaceTools, true)]

        private static bool FindReplaceToolsValid() { return !StopAllToolScripts.Active; }

#endif

        /// <summary>Why FTEmulator cannot open: the VRChat SDK is missing, or its package (com.riry.ftemulator) is.</summary>
        internal static string FTEmulatorUnavailable()
        {
#if RIRYTOOLS_VRC_CONSTRAINTS
            return "FTEmulator is its own package, com.riry.ftemulator, which is not in this project. Add it from the RiryTools listing in the VRChat Creator Companion.";
#else
            return new VrcSdkNotFoundException("FTEmulator").Message;
#endif
        }

#if !(RIRYTOOLS_VRC_CONSTRAINTS && RIRYTOOLS_FTEMULATOR)
        // Without the VRChat SDK or the FTEmulator package. With both, the path belongs to com.riry.ftemulator's
        // FTEmulatorMenus.cs instead; keep the priority in step.
        [MenuItem(FTEmulatorTools, false, 2067)]
        private static void FTEmulatorToolsItem() { EditorUtility.DisplayDialog("FTEmulator not available", FTEmulatorUnavailable(), "OK"); }


        [MenuItem(FTEmulatorTools, true)]

        private static bool FTEmulatorToolsValid() { return !StopAllToolScripts.Active; }
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
//   Tools/RiryTools/Constraint Tools (2030), menu key T -> ConstraintToolWindow.OpenFromTools (Editor/VRChat/UI/ConstraintTools/ConstraintToolWindow.cs, entry in
//                                                      Editor/VRChat/UI/RiryToolsVRChatMenus.cs): Train and Bezier tabs, on the one used last;
//                                                      without the VRChat SDK -> "VRCSDK not found" popup (this file)
//   Tools/RiryTools/GUID Tools (2040)               -> GuidToolsWindow.Open (Editor/UI/GuidTools/GuidToolsWindow.cs)
//   Tools/RiryTools/IconCap (2045), menu key O     -> IconCapWindow.Open (Editor/UI/IconCap/IconCapWindow.cs)
//   Tools/RiryTools/Import Presets/Set Folder Import Presets (2050) -> ImportPresetActions.ChooseFolderPreset -> ImportPresetFolderWindow (Editor/UI/ImportPresets/)
//   Tools/RiryTools/Import Presets/Save Import Preset (2051) -> ImportPresetActions.SaveToLibrary
//   Tools/RiryTools/Animator Tools/Extract Animations (1997) -> ImportPresetExtractAnimationWindow.Open (Editor/UI/ImportPresets/ImportPresetExtractAnimationWindow.cs)
//   Tools/RiryTools/Import Presets/Open Import Preset Library (2053) -> ImportPresetActions.OpenLibrary
//   Tools/RiryTools/Utilities/Material Cleaner (2042) -> MaterialCleanerWindow.Open
//   Tools/RiryTools/Find Null references (2056)     -> FindNullReferencesWindow.Run (Editor/UI/Debug/FindNullReferencesWindow.cs)
//   Tools/RiryTools/Refresh Scripts (2058)          -> RefreshScripts.Run: refresh, reload, reopen RiryTools windows (also the Main Menu's Debug Tools tab)
//   Tools/RiryTools/Stop Auto Game (2060)  [check mark] -> StopAutoGame.Enabled (Editor/Utilities/StopAutoGame.cs)
//   Tools/RiryTools/Script Load Guard (2061)  [check mark] -> ScriptLoadGuard.Enabled (Editor/Debug/ScriptLoadGuard.cs)
//   Tools/RiryTools/Menu Mockup (2065)              -> MenuMockup.Open (Editor/Debug/MenuMockup.cs) -> MenuMockupEditWindow and MenuMockupComponentsWindow (Editor/UI/Debug/)
//   Tools/RiryTools/Menu Tools (2066), menu key E   -> MenuToolWindow.Open (Editor/VRChat/UI/MenuTool/MenuToolWindow.cs, entry in
//                                                      Editor/VRChat/UI/RiryToolsVRChatMenus.cs); without the VRChat SDK -> "VRCSDK not
//                                                      found" popup (this file)
//   Tools/RiryTools/FTEmulator (2067)               -> FTEmulatorWindow.Open (com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs, entry in
//                                                      Editor/VRChat/UI/RiryToolsVRChatMenus.cs); without the VRChat SDK -> "VRCSDK not
//                                                      found" popup (this file). No right-click entry.
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
//   SCRIPT CAUSING FAILED LOAD window               -> Editor/UI/Debug/ScriptLoadGuardFailureWindow.cs
//   Constraint Tools window                          -> Editor/VRChat/UI/ConstraintTools/ConstraintToolWindow.cs; its Train tab Editor/VRChat/UI/ConstraintTools/ConstraintTrainWindow.UI.cs,
//                                                      its Bezier tab Editor/VRChat/UI/ConstraintTools/ConstraintBezierWindow.UI.cs
//   FTEmulator Debug window                         -> com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorDebugWindow.cs, opened from the FTEmulator window
//   Confirmation dialogs (DisplayDialog) stay beside the action they confirm.
