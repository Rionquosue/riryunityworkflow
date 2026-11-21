using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Menu entries for the VRChat tools. They live in this assembly, which only
    /// compiles with the VRChat SDK; every other entry is in Editor/UI/RiryToolsMenuBar.cs and RiryToolsRightClickMenu.cs,
    /// where the editing tips also apply and the paths are defined. Without the SDK,
    /// RiryToolsMenuBar.cs and RiryToolsRightClickMenu.cs have the same paths showing a "VRCSDK not found" popup, so keep
    /// the priorities of both files in step.
    /// </summary>
    [InitializeOnLoad]
    internal static class RiryToolsVRChatMenus
    {
        // The Main Menu's General Tools > Constraint Tools sub-tab hosts the Constraint Tools window (with its Train, Bezier and Insertable prop
        // sub-tabs), and its Menu Tools tab the Menu Tools' Parameter budget (its Menu manager opens the Menu Tools window).
        // FTEmulator's entries are in its own package (com.riry.ftemulator, Editor/UI/FTEmulatorMenus.cs).
        static RiryToolsVRChatMenus()
        {
            RiryToolsMainMenu.CreateMenuTool = MenuToolWindow.CreateHosted;
            RiryToolsMainMenu.CreateFindReplace = BehaviourFindReplaceWindow.CreateHosted;
            RiryToolsMainMenu.CreateConstraintTool = ConstraintToolWindow.CreateHosted;
        }

        [MenuItem(RiryToolsMenuBar.ConstraintTrainTools, false, 2020)]
        private static void ConstraintTrainToolsItem() { ConstraintToolWindow.Open(ConstraintToolWindow.TrainTab); }

        [MenuItem(RiryToolsMenuBar.ConstraintTrainTools, true)]
        private static bool ConstraintTrainToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.ConstraintBezierTools, false, 2021)]
        private static void ConstraintBezierToolsItem() { ConstraintToolWindow.Open(ConstraintToolWindow.BezierTab); }

        [MenuItem(RiryToolsMenuBar.ConstraintBezierTools, true)]
        private static bool ConstraintBezierToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.InsertablePropTools, false, 2022)]
        private static void InsertablePropToolsItem() { ConstraintToolWindow.Open(ConstraintToolWindow.InsertableTab); }

        [MenuItem(RiryToolsMenuBar.InsertablePropTools, true)]
        private static bool InsertablePropToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.DualAxisTools, false, 2023)]
        private static void DualAxisToolsItem() { ConstraintToolWindow.Open(ConstraintToolWindow.DualAxisTab); }

        [MenuItem(RiryToolsMenuBar.DualAxisTools, true)]
        private static bool DualAxisToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.ThreeAxisTools, false, 2024)]
        private static void ThreeAxisToolsItem() { ConstraintToolWindow.Open(ConstraintToolWindow.ThreeAxisTab); }

        [MenuItem(RiryToolsMenuBar.ThreeAxisTools, true)]
        private static bool ThreeAxisToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.MenuTool, false, 2060)]
        private static void MenuToolItem() { MenuToolWindow.Open(); }

        [MenuItem(RiryToolsMenuBar.MenuTool, true)]
        private static bool MenuToolValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.FindReplaceTools, false, 2070)]
        private static void FindReplaceToolsItem() { BehaviourFindReplaceWindow.OpenPopup(null); }

        [MenuItem(RiryToolsMenuBar.FindReplaceTools, true)]
        private static bool FindReplaceToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsRightClickMenu.FindReplaceAssets, false, 2010)]
        private static void FindReplaceAssetsItem() { BehaviourFindReplaceWindow.OpenPopup(Selection.activeObject); }

        [MenuItem(RiryToolsRightClickMenu.FindReplaceAssets, true)]
        private static bool FindReplaceAssetsValid() { return !StopAllToolScripts.Active && (Selection.activeObject is UnityEditor.Animations.AnimatorController); }

        // No MenuCommand parameter, so a multi-selection opens the window once.
        // "GameObject/" entries with a priority below 50 also appear in the Hierarchy's right-click menu.

        [MenuItem(RiryToolsRightClickMenu.ConstraintTrainHierarchy, false, 11)]
        private static void ConstraintTrainHierarchyItem() { ConstraintToolWindow.OpenTrainFromHierarchy(); }

        [MenuItem(RiryToolsRightClickMenu.ConstraintTrainHierarchy, true)]
        private static bool ConstraintTrainHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        [MenuItem(RiryToolsRightClickMenu.ConstraintBezierHierarchy, false, 12)]
        private static void ConstraintBezierHierarchyItem() { ConstraintToolWindow.OpenBezierFromHierarchy(); }

        [MenuItem(RiryToolsRightClickMenu.ConstraintBezierHierarchy, true)]
        private static bool ConstraintBezierHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        [MenuItem(RiryToolsRightClickMenu.InsertablePropHierarchy, false, 13)]
        private static void InsertablePropHierarchyItem() { ConstraintToolWindow.OpenInsertableFromHierarchy(); }

        [MenuItem(RiryToolsRightClickMenu.InsertablePropHierarchy, true)]
        private static bool InsertablePropHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        [MenuItem(RiryToolsRightClickMenu.DualAxisHierarchy, false, 14)]
        private static void DualAxisHierarchyItem() { ConstraintToolWindow.OpenDualAxisFromHierarchy(); }

        [MenuItem(RiryToolsRightClickMenu.DualAxisHierarchy, true)]
        private static bool DualAxisHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        [MenuItem(RiryToolsRightClickMenu.ThreeAxisHierarchy, false, 15)]
        private static void ThreeAxisHierarchyItem() { ConstraintToolWindow.OpenThreeAxisFromHierarchy(); }

        [MenuItem(RiryToolsRightClickMenu.ThreeAxisHierarchy, true)]
        private static bool ThreeAxisHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        [MenuItem(RiryToolsRightClickMenu.MenuToolHierarchy, false, 17)]
        private static void MenuToolHierarchyItem() { MenuToolWindow.OpenFromHierarchy(Selection.activeGameObject); }

        [MenuItem(RiryToolsRightClickMenu.MenuToolHierarchy, true)]
        private static bool MenuToolHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        [MenuItem(RiryToolsRightClickMenu.FindReplaceHierarchy, false, 18)]
        private static void FindReplaceHierarchyItem() { BehaviourFindReplaceWindow.OpenPopup(Selection.activeGameObject); }

        [MenuItem(RiryToolsRightClickMenu.FindReplaceHierarchy, true)]
        private static bool FindReplaceHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        // Scene and Prefab Mode objects only, not prefab assets.
        private static bool SceneObjectSelected()
        {
            return Selection.activeTransform != null && !EditorUtility.IsPersistent(Selection.activeTransform);
        }
    }
}

// ============================================================================
// GLOSSARY: VRChat menu entries (paths in RiryToolsMenuBar.cs and RiryToolsRightClickMenu.cs)
//   Tools/RiryTools/General Tools/Constraint Tools/Train (2020), Bezier (2021), Insertable prop (2022), Dual axis detector (2023),
//                                                   Three axis detector (2024) -> ConstraintToolWindow.Open on that tab
//   Tools/RiryTools/Menu Tools (2060), menu key E -> MenuToolWindow.Open; the Main Menu's Menu Tools tab hosts its Parameter budget
//   Tools/RiryTools/Animator Tools/Find and Replace (2070) -> BehaviourFindReplaceWindow.OpenPopup
//   Assets/RiryTools/Animator Tools/Find and Replace (2010) -> the popup with the selected animator controller
//   GameObject/RiryTools/General Tools/Constraint Tools/Train (11), Bezier (12), Insertable prop (13), Dual axis detector (14),
//                                                   Three axis detector (15) -> ConstraintToolWindow.Open*FromHierarchy: that tab,
//                                                   filled from the selection
//   GameObject/RiryTools/Menu Tools (17)          -> MenuToolWindow.OpenFromHierarchy: the selection's avatar as the target
//   GameObject/RiryTools/Animator Tools/Find and Replace (18) -> the popup with the selected scene object
//   Tools/RiryTools/FTEmulator (2090)             -> in com.riry.ftemulator (Editor/UI/FTEmulatorMenus.cs), not here.
//   SceneObjectSelected      Validator of the Hierarchy entries: a scene or Prefab Mode object is active.
// ============================================================================
