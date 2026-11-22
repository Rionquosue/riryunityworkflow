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
        // ---- Tools dropdown: laid out like the Main Menu's tabs ---------------
        // Main Menu, General Tools (Blendshape Sync, Import Presets, Constraint Tools, AutoSync, PopUp Tools), Menu Tools,
        // Animator Tools, Debug Tools, FTEmulator, then Documentation.
        private const string MainMenu = "Tools/RiryTools/Main Menu";
        internal const string GeneralTools = "Tools/RiryTools/General Tools/";
        private const string BlendshapeSync = GeneralTools + "Blendshape Sync";
        private const string ImportPresetTools = GeneralTools + "Import Presets/Set Folder Import Presets";
        private const string SaveImportPresetTools = GeneralTools + "Import Presets/Save Import Preset";
        private const string OpenPresetLibrary = GeneralTools + "Import Presets/Open Import Preset Library";
        private const string SyncTools = GeneralTools + "AutoSync/Sync";
        private const string ConfigurationTools = GeneralTools + "AutoSync/Configuration";
        private const string PopUpTools = GeneralTools + "PopUp Tools/";
        // "&n" makes N its menu key, "&o" O.
        private const string AvatarMigratorMenu = PopUpTools + "Avatar Migratio&n Assistant";
        private const string IconCapMenu = PopUpTools + "Ic&onCap";
        private const string MaterialCleanerMenu = PopUpTools + "Material Cleaner";
        private const string PrefabCleanerMenu = PopUpTools + GuidToolUnusedOverrides.Title;
        private const string GuidRegeneratorMenu = PopUpTools + GuidToolTargetsWindow.RegeneratorTitle;
        private const string GuidRepairMenu = PopUpTools + GuidToolTargetsWindow.RepairTitle;
        private const string GuidCheckerMenu = PopUpTools + "GUID Broken Reference Checker";
        private const string CleanAnimationKeysMenu = "Tools/RiryTools/Animator Tools/Clean Animation Keys";
        private const string ControllerCleanerMenu = "Tools/RiryTools/Animator Tools/Controller Cleaner";
        private const string ExtractAnimationsTools = "Tools/RiryTools/Animator Tools/Extract Animations";
        private const string RefreshScriptsMenu = "Tools/RiryTools/Debug Tools/" + RefreshScripts.DisplayName;
        private const string FindNullReferencesMenu = "Tools/RiryTools/Debug Tools/Find Null references";
        private const string MenuMockupMenu = "Tools/RiryTools/Debug Tools/Menu Mockup";
        private const string DeleteGuidCacheMenu = "Tools/RiryTools/Debug Tools/Delete GUID Reference Cache";
        private const string StopAutoGameMenu = "Tools/RiryTools/Debug Tools/Stop Auto Game";
        private const string ScriptLoadGuardMenu = "Tools/RiryTools/Debug Tools/Script Load Guard";
        private const string StopAllToolScriptsMenu = "Tools/RiryTools/Debug Tools/" + StopAllToolScripts.DisplayName;
        private const string Documentation = "Tools/RiryTools/Documentation";

        // Where Documentation leads. Change it here when the wiki is up.
        internal const string DocumentationLink = "https://github.com/Rionquosue/riryunityworkflow";

        // ---- Git menu -------------------------------------------------------
        private const string Sync = "Git/Sync";
        private const string Configuration = "Git/Configuration";

        // ---- VRChat tools (also used by RiryToolsVRChatMenus.cs) ------------
        // "&T" makes T its menu key. The space before it is U+00A0, because Unity reads "<space>&..." at the end of a path
        // segment as an Alt shortcut. One entry per Constraint Tools tab, in the tabs' order.
        internal const string ConstraintTool = GeneralTools + "Constraint\u00A0&Tools/";
        internal const string ConstraintTrainTools = ConstraintTool + "Train";
        internal const string ConstraintBezierTools = ConstraintTool + "Bezier";
        internal const string InsertablePropTools = ConstraintTool + "Insertable prop";
        internal const string DualAxisTools = ConstraintTool + "Dual axis detector";
        internal const string ThreeAxisTools = ConstraintTool + "Three axis detector";
        // "&e" makes E its menu key (M is Main Menu's).
        internal const string MenuTool = "Tools/RiryTools/M&enu Tools";
        // Find and Replace's place under RiryTools, the same in the Tools menu and the Project and Hierarchy right-click menus.
        internal const string FindReplaceLabel = "Animator Tools/Find and Replace";
        internal const string FindReplaceTools = "Tools/RiryTools/" + FindReplaceLabel;
        // FTEmulator is in the Tools menu and the Main Menu's FTEmulator tab only: it has no right-click entry.
        internal const string FTEmulatorTools = "Tools/RiryTools/FTEmulator";

        // Priorities, in the Main Menu's order. Constraint Tools' tabs are 2020-2024, Menu Tools 2060, Find and Replace 2070
        // (RiryToolsVRChatMenus.cs, or below without the VRChat SDK) and FTEmulator 2090 (com.riry.ftemulator's FTEmulatorMenus.cs,
        // or below without it).

        // =====================================================================
        // Tools dropdown
        // =====================================================================

        [MenuItem(MainMenu, false, 1990)]
        private static void MainMenuItem() { RiryToolsMainMenu.Open(); }

        // ---- General Tools --------------------------------------------------

        [MenuItem(BlendshapeSync, false, 2000)]
        private static void BlendshapeSyncItem() { BlendshapeSyncJobs.Add(); }

        [MenuItem(BlendshapeSync, true)]
        private static bool BlendshapeSyncValid() { return !StopAllToolScripts.Active; }

        [MenuItem(ImportPresetTools, false, 2010)]
        private static void ImportPresetToolsItem() { ImportPresetActions.ChooseFolderPreset(); }

        [MenuItem(ImportPresetTools, true)]
        private static bool ImportPresetToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(SaveImportPresetTools, false, 2011)]
        private static void SaveImportPresetToolsItem() { ImportPresetActions.SaveToLibrary(); }

        [MenuItem(SaveImportPresetTools, true)]
        private static bool SaveImportPresetToolsValid() { return !StopAllToolScripts.Active && (ImportPresetActions.ValidateSaveToLibrary()); }

        [MenuItem(OpenPresetLibrary, false, 2012)]
        private static void OpenPresetLibraryItem() { ImportPresetActions.OpenLibrary(); }

        [MenuItem(OpenPresetLibrary, true)]
        private static bool OpenPresetLibraryValid() { return !StopAllToolScripts.Active; }

        [MenuItem(SyncTools, false, 2030)]
        private static void SyncToolsItem() { AutoSyncGitActions.ToggleSync(); }

        [MenuItem(SyncTools, true)]
        private static bool SyncToolsValid()
        {
            if (StopAllToolScripts.Active) return false;
            Menu.SetChecked(SyncTools, AutoSync.Enabled);
            return true;
        }

        [MenuItem(ConfigurationTools, false, 2031)]
        private static void ConfigurationToolsItem() { AutoSyncGitActions.OpenConfiguration(); }

        [MenuItem(ConfigurationTools, true)]
        private static bool ConfigurationToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(AvatarMigratorMenu, false, 2040)]
        private static void AvatarMigratorItem() { AvatarMigratorWindow.Open(); }

        [MenuItem(AvatarMigratorMenu, true)]
        private static bool AvatarMigratorValid() { return !StopAllToolScripts.Active; }

        [MenuItem(IconCapMenu, false, 2041)]
        private static void IconCapItem() { IconCapWindow.Open(); }

        [MenuItem(IconCapMenu, true)]
        private static bool IconCapValid() { return !StopAllToolScripts.Active; }

        [MenuItem(MaterialCleanerMenu, false, 2042)]
        private static void MaterialCleanerItem() { MaterialCleanerWindow.Open(); }

        [MenuItem(MaterialCleanerMenu, true)]
        private static bool MaterialCleanerValid() { return !StopAllToolScripts.Active; }

        [MenuItem(PrefabCleanerMenu, false, 2043)]
        private static void PrefabCleanerItem() { GuidToolPrefabCleanerWindow.Open(); }

        [MenuItem(PrefabCleanerMenu, true)]
        private static bool PrefabCleanerValid() { return !StopAllToolScripts.Active; }

        [MenuItem(GuidRegeneratorMenu, false, 2044)]
        private static void GuidRegeneratorItem() { GuidToolTargetsWindow.OpenRegenerator(); }

        [MenuItem(GuidRegeneratorMenu, true)]
        private static bool GuidRegeneratorValid() { return !StopAllToolScripts.Active; }

        [MenuItem(GuidRepairMenu, false, 2045)]
        private static void GuidRepairItem() { GuidToolTargetsWindow.OpenRepair(); }

        [MenuItem(GuidRepairMenu, true)]
        private static bool GuidRepairValid() { return !StopAllToolScripts.Active; }

        [MenuItem(GuidCheckerMenu, false, 2046)]
        private static void GuidCheckerItem() { GuidToolBrokenReferenceWindow.OpenForOpenScenes("Checked from the Tools menu."); }

        [MenuItem(GuidCheckerMenu, true)]
        private static bool GuidCheckerValid() { return !StopAllToolScripts.Active; }

        // ---- Animator Tools (Find and Replace, 2070, is in RiryToolsVRChatMenus.cs) -----

        [MenuItem(CleanAnimationKeysMenu, false, 2071)]
        private static void CleanAnimationKeysItem() { CleanAnimationKeysWindow.Open(new UnityEngine.AnimationClip[0]); }

        [MenuItem(CleanAnimationKeysMenu, true)]
        private static bool CleanAnimationKeysValid() { return !StopAllToolScripts.Active; }

        [MenuItem(ControllerCleanerMenu, false, 2072)]
        private static void ControllerCleanerItem() { ControllerCleanerWindow.Open(); }

        [MenuItem(ControllerCleanerMenu, true)]
        private static bool ControllerCleanerValid() { return !StopAllToolScripts.Active; }

        [MenuItem(ExtractAnimationsTools, false, 2073)]
        private static void ExtractAnimationsToolsItem() { ImportPresetExtractAnimationWindow.Open(); }

        [MenuItem(ExtractAnimationsTools, true)]
        private static bool ExtractAnimationsToolsValid() { return !StopAllToolScripts.Active; }

        // ---- Debug Tools ----------------------------------------------------

        [MenuItem(RefreshScriptsMenu, false, 2080)]
        internal static void RefreshScriptsItem() { RefreshScripts.Run(); }

        [MenuItem(RefreshScriptsMenu, true)]
        private static bool RefreshScriptsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(FindNullReferencesMenu, false, 2081)]
        private static void FindNullReferencesItem() { FindNullReferencesWindow.Run(); }

        [MenuItem(FindNullReferencesMenu, true)]
        private static bool FindNullReferencesValid() { return !StopAllToolScripts.Active; }

        [MenuItem(MenuMockupMenu, false, 2082)]
        private static void MenuMockupItem() { MenuMockup.Open(); }

        [MenuItem(MenuMockupMenu, true)]
        private static bool MenuMockupValid() { return !StopAllToolScripts.Active; }

        [MenuItem(DeleteGuidCacheMenu, false, 2083)]
        private static void DeleteGuidCacheItem() { GuidToolClearReferenceCache.Run(); }

        [MenuItem(DeleteGuidCacheMenu, true)]
        private static bool DeleteGuidCacheValid() { return !StopAllToolScripts.Active; }

        [MenuItem(StopAutoGameMenu, false, 2084)]
        private static void StopAutoGameItem() { StopAutoGame.Enabled = !StopAutoGame.Enabled; }

        [MenuItem(StopAutoGameMenu, true)]
        private static bool StopAutoGameValid()
        {
            if (StopAllToolScripts.Active) return false;
            Menu.SetChecked(StopAutoGameMenu, StopAutoGame.Enabled);
            return true;
        }

        [MenuItem(ScriptLoadGuardMenu, false, 2085)]
        private static void ScriptLoadGuardItem() { ScriptLoadGuard.Enabled = !ScriptLoadGuard.Enabled; }

        [MenuItem(ScriptLoadGuardMenu, true)]
        private static bool ScriptLoadGuardValid()
        {
            if (StopAllToolScripts.Active) return false;
            Menu.SetChecked(ScriptLoadGuardMenu, ScriptLoadGuard.Enabled);
            return true;
        }

        // Works while stopped: it is how the stop is turned off.
        [MenuItem(StopAllToolScriptsMenu, false, 2086)]
        private static void StopAllToolScriptsItem() { StopAllToolScripts.Set(!StopAllToolScripts.Active); }

        [MenuItem(StopAllToolScriptsMenu, true)]
        private static bool StopAllToolScriptsValid()
        {
            Menu.SetChecked(StopAllToolScriptsMenu, StopAllToolScripts.Active);
            return true;
        }

        [MenuItem(Documentation, false, 2100)]
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

        [MenuItem(ConstraintTrainTools, false, 2020)]
        private static void ConstraintTrainToolsItem() { VrcSdkMissing("Constraint Tools"); }

        [MenuItem(ConstraintBezierTools, false, 2021)]
        private static void ConstraintBezierToolsItem() { VrcSdkMissing("Constraint Tools"); }

        [MenuItem(InsertablePropTools, false, 2022)]
        private static void InsertablePropToolsItem() { VrcSdkMissing("Constraint Tools"); }

        [MenuItem(DualAxisTools, false, 2023)]
        private static void DualAxisToolsItem() { VrcSdkMissing("Constraint Tools"); }

        [MenuItem(ThreeAxisTools, false, 2024)]
        private static void ThreeAxisToolsItem() { VrcSdkMissing("Constraint Tools"); }

        [MenuItem(MenuTool, false, 2060)]
        private static void MenuToolItem() { VrcSdkMissing("Menu Tools"); }

        [MenuItem(MenuTool, true)]
        private static bool MenuToolValid() { return !StopAllToolScripts.Active; }

        [MenuItem(FindReplaceTools, false, 2070)]
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
        [MenuItem(FTEmulatorTools, false, 2090)]
        private static void FTEmulatorToolsItem() { EditorUtility.DisplayDialog("FTEmulator not available", FTEmulatorUnavailable(), "OK"); }

        [MenuItem(FTEmulatorTools, true)]
        private static bool FTEmulatorToolsValid() { return !StopAllToolScripts.Active; }
#endif
    }
}

// ============================================================================
// GLOSSARY: menu bar entries
// Path (priority)                                   -> what it calls (file)
// Right-click entries are in RiryToolsRightClickMenu.cs. The Tools dropdown follows the Main Menu's tabs.
// ============================================================================
//
// TOOLS DROPDOWN (Tools/RiryTools/...)
//   Main Menu (1990)                               -> RiryToolsMainMenu.Open (Editor/UI/RiryToolsMainMenu.cs)
//   General Tools/Blendshape Sync (2000)           -> BlendshapeSyncJobs.Add: a new job in its popup
//   General Tools/Import Presets/Set Folder Import Presets (2010), Save Import Preset (2011), Open Import Preset Library (2012)
//                                                   -> ImportPresetActions.ChooseFolderPreset / SaveToLibrary / OpenLibrary
//   General Tools/Constraint Tools/Train (2020), Bezier (2021), Insertable prop (2022), Dual axis detector (2023), Three axis
//                                                   detector (2024), menu key T -> ConstraintToolWindow.Open on that tab (entries in
//                                                   Editor/VRChat/UI/RiryToolsVRChatMenus.cs); without the VRChat SDK -> "VRCSDK not found" popup
//   General Tools/AutoSync/Sync (2030) [check mark], Configuration (2031) -> AutoSyncGitActions.ToggleSync / OpenConfiguration
//   General Tools/PopUp Tools/Avatar Migration Assistant (2040, menu key N), IconCap (2041, menu key O), Material Cleaner (2042),
//                                                   Prefab Cleaner (2043), GUID Regenerator (2044), GUID Repair (2045), GUID Broken
//                                                   Reference Checker (2046) -> AvatarMigratorWindow.Open, IconCapWindow.Open,
//                                                   MaterialCleanerWindow.Open, GuidToolPrefabCleanerWindow.Open, GuidToolTargetsWindow.
//                                                   OpenRegenerator / OpenRepair, GuidToolBrokenReferenceWindow.OpenForOpenScenes
//   Menu Tools (2060), menu key E                  -> MenuToolWindow.Open (entry in RiryToolsVRChatMenus.cs)
//   Animator Tools/Find and Replace (2070, RiryToolsVRChatMenus.cs), Clean Animation Keys (2071), Controller Cleaner (2072),
//                                                   Extract Animations (2073)
//   Debug Tools/Refresh Scripts (2080), Find Null references (2081), Menu Mockup (2082), Delete GUID Reference Cache (2083),
//                                                   Stop Auto Game (2084) [check mark], Script Load Guard (2085) [check mark], Stop all
//                                                   tool scripts (2086) [check mark, works while stopped]
//   FTEmulator (2090)                              -> in com.riry.ftemulator (Editor/UI/FTEmulatorMenus.cs); without it or the VRChat SDK,
//                                                   a popup saying why (this file). No right-click entry.
//   Documentation (2100)                           -> opens DocumentationLink in the browser
//
// GIT MENU (main menu bar)
//   Git/Sync (1) [check mark], Git/Configuration (2) -> AutoSyncGitActions.ToggleSync / OpenConfiguration
//
// VrcSdkMissing(tool)   The "VRCSDK not found" popup, shared with RiryToolsRightClickMenu.cs.
// ============================================================================
