using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Every right-click entry of this package: the Project window (Assets/...) and the
    /// Hierarchy (GameObject/...). Menu bar entries are in RiryToolsMenuBar.cs.
    /// Constraint Tools' and Find and Replace's entries are in
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
        private const string RegenerateGuidAssets = "Assets/RiryTools/Regenerate GUID";
        private const string ImportPreset = "Assets/RiryTools/Set Folder Import Presets";
        private const string SaveImportPreset = "Assets/RiryTools/Save Import Preset";
        private const string ExtractAnimations = "Assets/RiryTools/Animator Tools/Extract Animations";
        private const string RepairGuidReferences = "Assets/RiryTools/Repair Missing GUID References";
        private const string CleanAnimationKeysAssets = "Assets/RiryTools/Animator Tools/Clean Animation Keys";
        private const string MaterialCleanerAssets = "Assets/RiryTools/Material Cleaner";
        private const string PrefabCleanerAssets = "Assets/RiryTools/" + GuidToolUnusedOverrides.Title;

        // ---- Hierarchy right-click -------------------------------------------
        private const string AttachToHierarchy = "GameObject/RiryTools/Attach To";
        private const string InvertGlobalPositionHierarchy = "GameObject/RiryTools/" + InvertGlobalPosition.UndoName;

        // ---- VRChat tools (also used by RiryToolsVRChatMenus.cs) ------------
        internal const string ConstraintTrainHierarchy = "GameObject/RiryTools/Constraint Tools/Train";
        internal const string ConstraintBezierHierarchy = "GameObject/RiryTools/Constraint Tools/Bezier";
        internal const string InsertablePropHierarchy = "GameObject/RiryTools/Constraint Tools/Insertable prop";
        internal const string DualAxisHierarchy = "GameObject/RiryTools/Constraint Tools/Dual axis detector";
        internal const string ThreeAxisHierarchy = "GameObject/RiryTools/Constraint Tools/Three axis detector";
        internal const string FindReplaceHierarchy = "GameObject/RiryTools/" + RiryToolsMenuBar.FindReplaceLabel;
        internal const string FindReplaceAssets = "Assets/RiryTools/" + RiryToolsMenuBar.FindReplaceLabel;

        // =====================================================================
        // Project window right-click
        // =====================================================================

        [MenuItem(RegenerateGuidAssets, false, 2000)]
        private static void RegenerateGuidItem() { GuidToolRegenerate.RegenerateSelectedGuids(); }

        [MenuItem(RegenerateGuidAssets, true)]
        private static bool RegenerateGuidValid() { return !StopAllToolScripts.Active && (GuidToolRegenerate.HasValidSelection()); }

        [MenuItem(ImportPreset, false, 2001)]
        private static void ImportPresetItem() { ImportPresetActions.QuickFolderPreset(); }

        [MenuItem(ImportPreset, true)]
        private static bool ImportPresetValid() { return !StopAllToolScripts.Active && (ImportPresetActions.ValidateChooseFolderPreset()); }

        [MenuItem(SaveImportPreset, false, 2002)]
        private static void SaveImportPresetItem() { ImportPresetActions.SaveToLibrary(); }

        [MenuItem(SaveImportPreset, true)]
        private static bool SaveImportPresetValid() { return !StopAllToolScripts.Active && (ImportPresetActions.ValidateSaveToLibrary()); }

        [MenuItem(ExtractAnimations, false, 2003)]
        private static void ExtractAnimationsItem() { ImportPresetExtractAnimationWindow.Open(); }

        [MenuItem(ExtractAnimations, true)]
        private static bool ExtractAnimationsValid()
        {
            if (StopAllToolScripts.Active) return false;
            // Only a model with embedded animations.
            string model = ImportPresetAnimations.SelectedModel();
            return model != null && ImportPresetAnimations.HasClips(model);
        }

        [MenuItem(RepairGuidReferences, false, 2004)]
        private static void RepairGuidReferencesItem() { GuidToolReferenceRepair.RepairSelected(); }

        [MenuItem(RepairGuidReferences, true)]
        private static bool RepairGuidReferencesValid() { return !StopAllToolScripts.Active && (GuidToolRegenerate.HasValidSelection()); }

        [MenuItem(CleanAnimationKeysAssets, false, 2005)]
        private static void CleanAnimationKeysAssetsItem()
        {
            CleanAnimationKeysWindow.Open(Selection.GetFiltered<UnityEngine.AnimationClip>(SelectionMode.Assets));
        }

        [MenuItem(CleanAnimationKeysAssets, true)]
        private static bool CleanAnimationKeysAssetsValid() { return !StopAllToolScripts.Active && (Selection.GetFiltered<UnityEngine.AnimationClip>(SelectionMode.Assets).Length > 0); }

        // Animator Tools/Find and Replace is 2006 (RiryToolsVRChatMenus.cs, or below without the VRChat SDK).

        [MenuItem(MaterialCleanerAssets, false, 2007)]
        private static void MaterialCleanerAssetsItem() { MaterialCleanerWindow.Open(Selection.activeObject as UnityEngine.Material); }

        [MenuItem(MaterialCleanerAssets, true)]
        private static bool MaterialCleanerAssetsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(PrefabCleanerAssets, false, 2008)]
        private static void PrefabCleanerAssetsItem()
        {
            GuidToolBrokenReferenceWindow.ScrubUnusedOverrides(GuidToolReferenceRepair.SelectedFiles());
        }

        [MenuItem(PrefabCleanerAssets, true)]
        private static bool PrefabCleanerAssetsValid() { return !StopAllToolScripts.Active && (GuidToolRegenerate.HasValidSelection()); }

        // =====================================================================
        // Hierarchy right-click (also in the GameObject menu)
        // =====================================================================

        [MenuItem(AttachToHierarchy, false, 19)]
        private static void AttachToItem() { AttachToWindow.Open(); }

        [MenuItem(AttachToHierarchy, true)]
        private static bool AttachToValid() { return !StopAllToolScripts.Active && (AttachTo.Selected().Count > 0); }

        [MenuItem(InvertGlobalPositionHierarchy, false, 26)]
        private static void InvertGlobalPositionItem() { InvertGlobalPositionPrompts.Run(); }

        [MenuItem(InvertGlobalPositionHierarchy, true)]
        private static bool InvertGlobalPositionValid() { return !StopAllToolScripts.Active && (AttachTo.Selected().Count > 0); }

        // Constraint Tools/Train is 20, Constraint Tools/Bezier 21, Animator Tools/Find and Replace 22, Constraint Tools/Insertable prop 23,
        // Constraint Tools/Dual axis detector 24 and Constraint Tools/Three axis detector 25
        // (RiryToolsVRChatMenus.cs, or below without the VRChat SDK).

#if !RIRYTOOLS_VRC_CONSTRAINTS
        // =====================================================================
        // VRChat tools without the VRChat SDK
        // With the SDK, these paths belong to RiryToolsVRChatMenus.cs instead. Keep
        // the priorities of both files in step.
        // =====================================================================

        [MenuItem(ConstraintTrainHierarchy, false, 20)]
        private static void ConstraintTrainHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }



        [MenuItem(ConstraintTrainHierarchy, true)]


        private static bool ConstraintTrainHierarchyValid() { return !StopAllToolScripts.Active; }

        [MenuItem(ConstraintBezierHierarchy, false, 21)]
        private static void ConstraintBezierHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }


        [MenuItem(ConstraintBezierHierarchy, true)]

        private static bool ConstraintBezierHierarchyValid() { return !StopAllToolScripts.Active; }

        [MenuItem(InsertablePropHierarchy, false, 23)]
        private static void InsertablePropHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }

        [MenuItem(InsertablePropHierarchy, true)]
        private static bool InsertablePropHierarchyValid() { return !StopAllToolScripts.Active; }

        [MenuItem(DualAxisHierarchy, false, 24)]
        private static void DualAxisHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }

        [MenuItem(DualAxisHierarchy, true)]
        private static bool DualAxisHierarchyValid() { return !StopAllToolScripts.Active; }

        [MenuItem(ThreeAxisHierarchy, false, 25)]
        private static void ThreeAxisHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tools"); }

        [MenuItem(ThreeAxisHierarchy, true)]
        private static bool ThreeAxisHierarchyValid() { return !StopAllToolScripts.Active; }

        [MenuItem(FindReplaceHierarchy, false, 22)]
        private static void FindReplaceHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Find and Replace"); }


        [MenuItem(FindReplaceHierarchy, true)]

        private static bool FindReplaceHierarchyValid() { return !StopAllToolScripts.Active; }

        [MenuItem(FindReplaceAssets, false, 2006)]
        private static void FindReplaceAssetsItem() { RiryToolsMenuBar.VrcSdkMissing("Find and Replace"); }


        [MenuItem(FindReplaceAssets, true)]

        private static bool FindReplaceAssetsValid() { return !StopAllToolScripts.Active; }
#endif
    }
}

// ============================================================================
// GLOSSARY: right-click menu entries
// Path (priority)                                   -> what it calls (file)
// Menu bar entries are in RiryToolsMenuBar.cs.
// ============================================================================
//
// PROJECT WINDOW RIGHT-CLICK
//   Assets/RiryTools/Regenerate GUID (2000)         -> GuidToolRegenerate.RegenerateSelectedGuids (Editor/GuidTools/GuidToolRegenerate.cs)
//   Assets/RiryTools/Set Folder Import Presets (2001) -> ImportPresetActions.QuickFolderPreset -> ImportPresetQuickWindow (Editor/UI/ImportPresets/ImportPresetQuickWindow.cs)
//   Assets/RiryTools/Save Import Preset (2002) -> ImportPresetActions.SaveToLibrary -> ImportPresetSaveWindow
//   Assets/RiryTools/Animator Tools/Extract Animations (2003) -> ImportPresetExtractAnimationWindow.Open with the selected model (Editor/UI/ImportPresets/ImportPresetExtractAnimationWindow.cs);
//                                                      enabled only for a model with embedded animations
//   Assets/RiryTools/Repair Missing GUID References (2004) -> GuidToolReferenceRepair.RepairSelected -> GuidToolBrokenReferenceWindow (Editor/UI/GuidTools/GuidToolBrokenReferenceWindow.UI.cs)
//   Assets/RiryTools/Animator Tools/Clean Animation Keys (2005) -> CleanAnimationKeysWindow.Open with the selected AnimationClips
//   Assets/RiryTools/Animator Tools/Find and Replace (2006) -> entry in Editor/VRChat/UI/RiryToolsVRChatMenus.cs; without the
//                                                      VRChat SDK -> "VRCSDK not found" popup (this file)
//   Assets/RiryTools/Material Cleaner (2007)    -> MaterialCleanerWindow.Open with the selected material in its Material field
//   Assets/RiryTools/Prefab Cleaner (2008)      -> GuidToolBrokenReferenceWindow.ScrubUnusedOverrides on the selected files
//                                                      and folders' contents (GuidToolReferenceRepair.SelectedFiles); asks first
//
// HIERARCHY RIGHT-CLICK (also in the GameObject menu)
//   GameObject/RiryTools/Attach To (19)         -> AttachToWindow.Open (Editor/UI/Utilities/AttachToWindow.cs): the Attach To popup;
//                                                      enabled when scene objects are selected (AttachTo.Selected, Editor/Utilities/AttachTo.cs)
//   GameObject/RiryTools/Invert Global Position (26) -> InvertGlobalPositionPrompts.Run (Editor/UI/Utilities/InvertGlobalPositionPrompts.cs):
//                                                      mirrors the selection to the avatar's other side, then asks about the mirrored bone
//   GameObject/RiryTools/Constraint Tools/Train (20)  -> ConstraintToolWindow.OpenTrainFromHierarchy (entry in Editor/VRChat/UI/RiryToolsVRChatMenus.cs)
//   GameObject/RiryTools/Constraint Tools/Bezier (21) -> ConstraintToolWindow.OpenBezierFromHierarchy (same file); both use the selection
//                                                      as the chain; without the VRChat SDK -> "VRCSDK not found" popup (this file)
//   GameObject/RiryTools/Animator Tools/Find and Replace (22) -> entry in Editor/VRChat/UI/RiryToolsVRChatMenus.cs; without the
//                                                      VRChat SDK -> "VRCSDK not found" popup (this file)
//   GameObject/RiryTools/Constraint Tools/Insertable prop (23), Dual axis detector (24), Three axis detector (25)
//                                                   -> ConstraintToolWindow.OpenInsertableFromHierarchy / OpenDualAxisFromHierarchy /
//                                                      OpenThreeAxisFromHierarchy (entries in Editor/VRChat/UI/RiryToolsVRChatMenus.cs);
//                                                      without the VRChat SDK -> "VRCSDK not found" popup (this file)
