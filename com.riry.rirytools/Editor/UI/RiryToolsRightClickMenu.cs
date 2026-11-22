using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Every right-click entry of this package: the Project window (Assets/...) and the
    /// Hierarchy (GameObject/...). Menu bar entries are in RiryToolsMenuBar.cs.
    /// Laid out like the Main Menu's tabs (General Tools, Menu Tools, Animator Tools), with the Hierarchy-only tools
    /// (Attach To, Invert Global Position, Find scripts that reference this) last. Constraint Tools', Menu Tools' and
    /// Find and Replace's entries are in
    /// Editor/VRChat/UI/RiryToolsVRChatMenus.cs, because they only compile with the VRChat SDK.
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
    internal static class RiryToolsRightClickMenu
    {
        // ---- Project window right-click -------------------------------------
        private const string GeneralToolsAssets = "Assets/RiryTools/General Tools/";
        private const string ImportPreset = GeneralToolsAssets + "Import Presets/Set Folder Import Presets";
        private const string SaveImportPreset = GeneralToolsAssets + "Import Presets/Save Import Preset";
        private const string MaterialCleanerAssets = GeneralToolsAssets + "PopUp Tools/Material Cleaner";
        private const string PrefabCleanerAssets = GeneralToolsAssets + "PopUp Tools/" + GuidToolUnusedOverrides.Title;
        private const string GuidRegeneratorAssets = GeneralToolsAssets + "PopUp Tools/" + GuidToolTargetsWindow.RegeneratorTitle;
        private const string GuidRepairAssets = GeneralToolsAssets + "PopUp Tools/" + GuidToolTargetsWindow.RepairTitle;
        private const string CleanAnimationKeysAssets = "Assets/RiryTools/Animator Tools/Clean Animation Keys";
        private const string ExtractAnimations = "Assets/RiryTools/Animator Tools/Extract Animations";

        // ---- Hierarchy right-click -------------------------------------------
        private const string GeneralToolsHierarchy = "GameObject/RiryTools/General Tools/";
        private const string BlendshapeSyncHierarchy = GeneralToolsHierarchy + "Blendshape Sync";
        private const string AvatarMigratorHierarchy = GeneralToolsHierarchy + "PopUp Tools/Avatar Migration Assistant";
        private const string ControllerCleanerHierarchy = "GameObject/RiryTools/Animator Tools/Controller Cleaner";
        private const string AttachToHierarchy = "GameObject/RiryTools/Attach To";
        private const string InvertGlobalPositionHierarchy = "GameObject/RiryTools/" + InvertGlobalPosition.UndoName;
        private const string FindReferencingScriptsHierarchy = "GameObject/RiryTools/" + FindReferencingScripts.Title;

        // ---- VRChat tools (also used by RiryToolsVRChatMenus.cs) ------------
        internal const string ConstraintTrainHierarchy = GeneralToolsHierarchy + "Constraint Tools/Train";
        internal const string ConstraintBezierHierarchy = GeneralToolsHierarchy + "Constraint Tools/Bezier";
        internal const string InsertablePropHierarchy = GeneralToolsHierarchy + "Constraint Tools/Insertable prop";
        internal const string DualAxisHierarchy = GeneralToolsHierarchy + "Constraint Tools/Dual axis detector";
        internal const string ThreeAxisHierarchy = GeneralToolsHierarchy + "Constraint Tools/Three axis detector";
        internal const string MenuToolHierarchy = "GameObject/RiryTools/Menu Tools";
        internal const string FindReplaceHierarchy = "GameObject/RiryTools/" + RiryToolsMenuBar.FindReplaceLabel;
        internal const string FindReplaceAssets = "Assets/RiryTools/" + RiryToolsMenuBar.FindReplaceLabel;

        // =====================================================================
        // Project window right-click
        // =====================================================================

        [MenuItem(ImportPreset, false, 2000)]
        private static void ImportPresetItem() { ImportPresetActions.QuickFolderPreset(); }

        [MenuItem(ImportPreset, true)]
        private static bool ImportPresetValid() { return !StopAllToolScripts.Active && (ImportPresetActions.ValidateChooseFolderPreset()); }

        [MenuItem(SaveImportPreset, false, 2001)]
        private static void SaveImportPresetItem() { ImportPresetActions.SaveToLibrary(); }

        [MenuItem(SaveImportPreset, true)]
        private static bool SaveImportPresetValid() { return !StopAllToolScripts.Active && (ImportPresetActions.ValidateSaveToLibrary()); }

        [MenuItem(MaterialCleanerAssets, false, 2002)]
        private static void MaterialCleanerAssetsItem() { MaterialCleanerWindow.Open(Selection.activeObject as UnityEngine.Material); }

        [MenuItem(MaterialCleanerAssets, true)]
        private static bool MaterialCleanerAssetsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(PrefabCleanerAssets, false, 2003)]
        private static void PrefabCleanerAssetsItem()
        {
            GuidToolBrokenReferenceWindow.ScrubUnusedOverrides(GuidToolReferenceRepair.SelectedFiles());
        }

        [MenuItem(PrefabCleanerAssets, true)]
        private static bool PrefabCleanerAssetsValid() { return !StopAllToolScripts.Active && (GuidToolRegenerate.HasValidSelection()); }

        [MenuItem(GuidRegeneratorAssets, false, 2004)]
        private static void GuidRegeneratorItem() { GuidToolRegenerate.RegenerateSelectedGuids(); }

        [MenuItem(GuidRegeneratorAssets, true)]
        private static bool GuidRegeneratorValid() { return !StopAllToolScripts.Active && (GuidToolRegenerate.HasValidSelection()); }

        [MenuItem(GuidRepairAssets, false, 2005)]
        private static void GuidRepairItem() { GuidToolReferenceRepair.RepairSelected(); }

        [MenuItem(GuidRepairAssets, true)]
        private static bool GuidRepairValid() { return !StopAllToolScripts.Active && (GuidToolRegenerate.HasValidSelection()); }

        // Animator Tools/Find and Replace is 2010 (RiryToolsVRChatMenus.cs, or below without the VRChat SDK).

        [MenuItem(CleanAnimationKeysAssets, false, 2011)]
        private static void CleanAnimationKeysAssetsItem()
        {
            CleanAnimationKeysWindow.Open(Selection.GetFiltered<UnityEngine.AnimationClip>(SelectionMode.Assets));
        }

        [MenuItem(CleanAnimationKeysAssets, true)]
        private static bool CleanAnimationKeysAssetsValid() { return !StopAllToolScripts.Active && (Selection.GetFiltered<UnityEngine.AnimationClip>(SelectionMode.Assets).Length > 0); }

        [MenuItem(ExtractAnimations, false, 2012)]
        private static void ExtractAnimationsItem() { ImportPresetExtractAnimationWindow.Open(); }

        [MenuItem(ExtractAnimations, true)]
        private static bool ExtractAnimationsValid()
        {
            if (StopAllToolScripts.Active) return false;
            // Only a model with embedded animations.
            string model = ImportPresetAnimations.SelectedModel();
            return model != null && ImportPresetAnimations.HasClips(model);
        }

        // =====================================================================
        // Hierarchy right-click (also in the GameObject menu; priorities below 50)
        // =====================================================================

        [MenuItem(BlendshapeSyncHierarchy, false, 10)]
        private static void BlendshapeSyncHierarchyItem() { BlendshapeSyncJobs.Add(SelectedRenderer()); }

        [MenuItem(BlendshapeSyncHierarchy, true)]
        private static bool BlendshapeSyncHierarchyValid() { return !StopAllToolScripts.Active && (SelectedRenderer() != null); }

        // Constraint Tools' tabs are 11-15 (RiryToolsVRChatMenus.cs, or below without the VRChat SDK).

        [MenuItem(AvatarMigratorHierarchy, false, 16)]
        private static void AvatarMigratorHierarchyItem() { AvatarMigratorWindow.OpenPopup(Selection.activeGameObject); }

        [MenuItem(AvatarMigratorHierarchy, true)]
        private static bool AvatarMigratorHierarchyValid() { return !StopAllToolScripts.Active && (AttachTo.Selected().Count > 0); }

        // Menu Tools is 17 and Animator Tools/Find and Replace 18 (RiryToolsVRChatMenus.cs, or below without the VRChat SDK).

        [MenuItem(ControllerCleanerHierarchy, false, 19)]
        private static void ControllerCleanerHierarchyItem() { ControllerCleanerWindow.Open(ControllerCleanerWindow.ControllerOf(Selection.activeGameObject)); }

        [MenuItem(ControllerCleanerHierarchy, true)]
        private static bool ControllerCleanerHierarchyValid()
        {
            return !StopAllToolScripts.Active && AttachTo.Selected().Count > 0 && ControllerCleanerWindow.ControllerOf(Selection.activeGameObject) != null;
        }

        // The Hierarchy-only tools, after a separator.

        [MenuItem(AttachToHierarchy, false, 30)]
        private static void AttachToItem() { AttachToWindow.Open(); }

        [MenuItem(AttachToHierarchy, true)]
        private static bool AttachToValid() { return !StopAllToolScripts.Active && (AttachTo.Selected().Count > 0); }

        [MenuItem(InvertGlobalPositionHierarchy, false, 31)]
        private static void InvertGlobalPositionItem() { InvertGlobalPositionPrompts.Run(); }

        [MenuItem(InvertGlobalPositionHierarchy, true)]
        private static bool InvertGlobalPositionValid() { return !StopAllToolScripts.Active && (AttachTo.Selected().Count > 0); }

        [MenuItem(FindReferencingScriptsHierarchy, false, 32)]
        private static void FindReferencingScriptsItem() { FindReferencingScriptsWindow.Open(); }

        [MenuItem(FindReferencingScriptsHierarchy, true)]
        private static bool FindReferencingScriptsValid() { return !StopAllToolScripts.Active && (AttachTo.Selected().Count > 0); }

        // The Skinned Mesh Renderer on the active scene object, or null.
        private static UnityEngine.SkinnedMeshRenderer SelectedRenderer()
        {
            UnityEngine.GameObject selected = Selection.activeGameObject;
            return selected != null && !EditorUtility.IsPersistent(selected) ? selected.GetComponent<UnityEngine.SkinnedMeshRenderer>() : null;
        }

#if !RIRYTOOLS_VRC_CONSTRAINTS
        // =====================================================================
        // VRChat tools without the VRChat SDK
        // With the SDK, these paths belong to RiryToolsVRChatMenus.cs instead. Keep
        // the priorities of both files in step.
        // =====================================================================

        [MenuItem(ConstraintTrainHierarchy, false, 11)]
        private static void ConstraintTrainHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }

        [MenuItem(ConstraintBezierHierarchy, false, 12)]
        private static void ConstraintBezierHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }

        [MenuItem(InsertablePropHierarchy, false, 13)]
        private static void InsertablePropHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }

        [MenuItem(DualAxisHierarchy, false, 14)]
        private static void DualAxisHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }

        [MenuItem(ThreeAxisHierarchy, false, 15)]
        private static void ThreeAxisHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }

        [MenuItem(MenuToolHierarchy, false, 17)]
        private static void MenuToolHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Menu Tools"); }

        [MenuItem(FindReplaceHierarchy, false, 18)]
        private static void FindReplaceHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Find and Replace"); }

        [MenuItem(FindReplaceAssets, false, 2010)]
        private static void FindReplaceAssetsItem() { RiryToolsMenuBar.VrcSdkMissing("Find and Replace"); }
#endif
    }
}

// ============================================================================
// GLOSSARY: right-click entries
// Path (priority)                                   -> what it calls (file). Laid out like the Main Menu's tabs.
// ============================================================================
//
// PROJECT WINDOW RIGHT-CLICK (Assets/RiryTools/...)
//   General Tools/Import Presets/Set Folder Import Presets (2000) -> ImportPresetActions.QuickFolderPreset; enabled for a folder
//   General Tools/Import Presets/Save Import Preset (2001) -> ImportPresetActions.SaveToLibrary; enabled for an asset with an importer
//   General Tools/PopUp Tools/Material Cleaner (2002) -> MaterialCleanerWindow.Open with the selected material
//   General Tools/PopUp Tools/Prefab Cleaner (2003) -> GuidToolBrokenReferenceWindow.ScrubUnusedOverrides on the selected files (asks first)
//   General Tools/PopUp Tools/GUID Regenerator (2004) -> GuidToolRegenerate.RegenerateSelectedGuids on the selection
//   General Tools/PopUp Tools/GUID Repair (2005) -> GuidToolReferenceRepair.RepairSelected -> GuidToolBrokenReferenceWindow
//   Animator Tools/Find and Replace (2010)     -> RiryToolsVRChatMenus.cs, for an animator controller
//   Animator Tools/Clean Animation Keys (2011) -> CleanAnimationKeysWindow.Open with the selected clips
//   Animator Tools/Extract Animations (2012)   -> ImportPresetExtractAnimationWindow.Open; only for a model with embedded animations
//
// HIERARCHY RIGHT-CLICK (GameObject/RiryTools/..., also in the GameObject menu)
//   General Tools/Blendshape Sync (10)          -> BlendshapeSyncJobs.Add with the selected Skinned Mesh Renderer as the master
//   General Tools/Constraint Tools/Train (11), Bezier (12), Insertable prop (13), Dual axis detector (14), Three axis detector (15)
//                                                -> RiryToolsVRChatMenus.cs: that tab, filled from the selection
//   General Tools/PopUp Tools/Avatar Migration Assistant (16) -> AvatarMigratorWindow.OpenPopup with the selection's avatar as Source
//   Menu Tools (17)                             -> RiryToolsVRChatMenus.cs: MenuToolWindow.OpenFromHierarchy, the selection's avatar as target
//   Animator Tools/Find and Replace (18)        -> RiryToolsVRChatMenus.cs, with the selected scene object
//   Animator Tools/Controller Cleaner (19)      -> ControllerCleanerWindow.Open with the controller of the Animator on the selection
//                                                or its nearest parent with one (ControllerCleanerWindow.ControllerOf)
//   Attach To (30)                              -> AttachToWindow.Open (Editor/UI/Utilities/AttachToWindow.cs)
//   Invert Global Position (31)                 -> InvertGlobalPositionPrompts.Run (Editor/UI/Utilities/InvertGlobalPositionPrompts.cs)
//   Find scripts that reference this (32)       -> FindReferencingScriptsWindow.Open (Editor/UI/Utilities/FindReferencingScriptsWindow.cs)
//   Without the VRChat SDK, the VRChat entries show the "VRCSDK not found" popup (this file).
//   SelectedRenderer      The Skinned Mesh Renderer on the active scene object.
// ============================================================================
