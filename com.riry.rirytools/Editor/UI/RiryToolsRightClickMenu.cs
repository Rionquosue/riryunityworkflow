using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Every right-click entry of this package: the Project window (Assets/...) and the
    /// Hierarchy (GameObject/...). Menu bar entries are in RiryToolsMenuBar.cs.
    /// Constraint Tool's and Find and Replace's entries are in
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
        private const string ExtractAnimations = "Assets/RiryTools/Extract Animation";
        private const string RepairGuidReferences = "Assets/RiryTools/Repair Missing GUID References";
        private const string CleanAnimationKeysAssets = "Assets/RiryTools/Clean Animation Keys";

        // ---- Hierarchy right-click -------------------------------------------
        private const string AttachToHierarchy = "GameObject/RiryTools/Attach To";

        // ---- VRChat tools (also used by RiryToolsVRChatMenus.cs) ------------
        internal const string ConstraintTrainHierarchy = "GameObject/RiryTools/Constraint Tool/Train";
        internal const string ConstraintBezierHierarchy = "GameObject/RiryTools/Constraint Tool/Bezier";
        internal const string FindReplaceHierarchy = "GameObject/RiryTools/Animator tools/Find And Replace";
        internal const string FindReplaceAssets = "Assets/RiryTools/Animator Tools/Find And Replace";

        // =====================================================================
        // Project window right-click
        // =====================================================================

        [MenuItem(RegenerateGuidAssets, false, 2000)]
        private static void RegenerateGuidItem() { GuidToolRegenerate.RegenerateSelectedGuids(); }

        [MenuItem(RegenerateGuidAssets, true)]
        private static bool RegenerateGuidValid() { return GuidToolRegenerate.HasValidSelection(); }

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
        private static void RepairGuidReferencesItem() { GuidToolReferenceRepair.RepairSelected(); }

        [MenuItem(RepairGuidReferences, true)]
        private static bool RepairGuidReferencesValid() { return GuidToolRegenerate.HasValidSelection(); }

        [MenuItem(CleanAnimationKeysAssets, false, 2005)]
        private static void CleanAnimationKeysAssetsItem()
        {
            CleanAnimationKeysWindow.Open(Selection.GetFiltered<UnityEngine.AnimationClip>(SelectionMode.Assets));
        }

        [MenuItem(CleanAnimationKeysAssets, true)]
        private static bool CleanAnimationKeysAssetsValid() { return Selection.GetFiltered<UnityEngine.AnimationClip>(SelectionMode.Assets).Length > 0; }

        // Animator Tools/Find And Replace is 2006 (RiryToolsVRChatMenus.cs, or below without the VRChat SDK).

        // =====================================================================
        // Hierarchy right-click (also in the GameObject menu)
        // =====================================================================

        [MenuItem(AttachToHierarchy, false, 19)]
        private static void AttachToItem() { AvatarAttachWindow.Open(); }

        [MenuItem(AttachToHierarchy, true)]
        private static bool AttachToValid() { return AttachTo.Selected().Count > 0; }

        // Constraint Tool/Train is 20, Constraint Tool/Bezier 21 and Animator tools/Find And Replace 22
        // (RiryToolsVRChatMenus.cs, or below without the VRChat SDK).

#if !RIRYTOOLS_VRC_CONSTRAINTS
        // =====================================================================
        // VRChat tools without the VRChat SDK
        // With the SDK, these paths belong to RiryToolsVRChatMenus.cs instead. Keep
        // the priorities of both files in step.
        // =====================================================================

        [MenuItem(ConstraintTrainHierarchy, false, 20)]
        private static void ConstraintTrainHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tool"); }

        [MenuItem(ConstraintBezierHierarchy, false, 21)]
        private static void ConstraintBezierHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Constraint Tool"); }

        [MenuItem(FindReplaceHierarchy, false, 22)]
        private static void FindReplaceHierarchyItem() { RiryToolsMenuBar.VrcSdkMissing("Find and Replace"); }

        [MenuItem(FindReplaceAssets, false, 2006)]
        private static void FindReplaceAssetsItem() { RiryToolsMenuBar.VrcSdkMissing("Find and Replace"); }
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
//   Assets/RiryTools/Set Folder Import Presets (2001) -> ImportPresetActions.QuickFolderPreset -> ImportPresetQuickWindow (Editor/UI/ImportPresetQuickWindow.cs)
//   Assets/RiryTools/Save Import Preset (2002) -> ImportPresetActions.SaveToLibrary -> ImportPresetSaveWindow
//   Assets/RiryTools/Extract Animation (2003) -> ExtractAnimationWindow.Open with the selected model (Editor/UI/ExtractAnimationWindow.cs);
//                                                      enabled only for a model with embedded animations
//   Assets/RiryTools/Repair Missing GUID References (2004) -> GuidToolReferenceRepair.RepairSelected -> BrokenReferenceWindow (Editor/UI/BrokenReferenceWindow.UI.cs)
//   Assets/RiryTools/Clean Animation Keys (2005)    -> CleanAnimationKeysWindow.Open with the selected AnimationClips
//   Assets/RiryTools/Animator Tools/Find And Replace (2006) -> entry in Editor/VRChat/UI/RiryToolsVRChatMenus.cs; without the
//                                                      VRChat SDK -> "VRCSDK not found" popup (this file)
//
// HIERARCHY RIGHT-CLICK (also in the GameObject menu)
//   GameObject/RiryTools/Attach To (19)         -> AvatarAttachWindow.Open (Editor/UI/AvatarAttachWindow.cs): the Attach To popup;
//                                                      enabled when scene objects are selected (AttachTo.Selected, Editor/AttachTo.cs)
//   GameObject/RiryTools/Constraint Tool/Train (20)  -> ConstraintToolWindow.OpenTrainFromHierarchy (entry in Editor/VRChat/UI/RiryToolsVRChatMenus.cs)
//   GameObject/RiryTools/Constraint Tool/Bezier (21) -> ConstraintToolWindow.OpenBezierFromHierarchy (same file); both use the selection
//                                                      as the chain; without the VRChat SDK -> "VRCSDK not found" popup (this file)
//   GameObject/RiryTools/Animator tools/Find And Replace (22) -> entry in Editor/VRChat/UI/RiryToolsVRChatMenus.cs; without the
//                                                      VRChat SDK -> "VRCSDK not found" popup (this file)
