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
        // The Main Menu's Constraint Tool tab hosts the Constraint Tool window (with its Train, Bezier and Insertable prop
        // sub-tabs), its Menu Tool tab the Menu Tool's Parameter budget (its Menu manager opens the Menu Tool window), and
        // its Tools tab's FTEmulator sub-tab the FTEmulator window.
        static RiryToolsVRChatMenus()
        {
            RiryToolsMainMenu.CreateMenuTool = MenuToolWindow.CreateHosted;
            RiryToolsMainMenu.CreateFindReplace = BehaviourFindReplaceWindow.CreateHosted;
            RiryToolsMainMenu.CreateConstraintTool = ConstraintToolWindow.CreateHosted;
            RiryToolsMainMenu.CreateFTEmulator = FTEmulatorWindow.CreateHosted;
        }

        [MenuItem(RiryToolsMenuBar.ConstraintTool, false, 2030)]
        private static void ConstraintToolItem() { ConstraintToolWindow.OpenFromTools(); }



        [MenuItem(RiryToolsMenuBar.ConstraintTool, true)]


        private static bool ConstraintToolValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.MenuTool, false, 2066)]
        private static void MenuToolItem() { MenuToolWindow.Open(); }


        [MenuItem(RiryToolsMenuBar.MenuTool, true)]

        private static bool MenuToolValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.FindReplaceTools, false, 1995)]
        private static void FindReplaceToolsItem() { BehaviourFindReplaceWindow.OpenPopup(null); }


        [MenuItem(RiryToolsMenuBar.FindReplaceTools, true)]

        private static bool FindReplaceToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsMenuBar.FTEmulatorTools, false, 2067)]
        private static void FTEmulatorToolsItem() { FTEmulatorWindow.Open(); }


        [MenuItem(RiryToolsMenuBar.FTEmulatorTools, true)]

        private static bool FTEmulatorToolsValid() { return !StopAllToolScripts.Active; }

        [MenuItem(RiryToolsRightClickMenu.FindReplaceHierarchy, false, 22)]
        private static void FindReplaceHierarchyItem() { BehaviourFindReplaceWindow.OpenPopup(Selection.activeGameObject); }

        [MenuItem(RiryToolsRightClickMenu.FindReplaceHierarchy, true)]
        private static bool FindReplaceHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        [MenuItem(RiryToolsRightClickMenu.FindReplaceAssets, false, 2006)]
        private static void FindReplaceAssetsItem() { BehaviourFindReplaceWindow.OpenPopup(Selection.activeObject); }

        [MenuItem(RiryToolsRightClickMenu.FindReplaceAssets, true)]
        private static bool FindReplaceAssetsValid() { return !StopAllToolScripts.Active && (Selection.activeObject is UnityEditor.Animations.AnimatorController); }

        // No MenuCommand parameter, so a multi-selection opens the window once.
        // "GameObject/" entries with a priority below 50 also appear in the Hierarchy's right-click menu.
        [MenuItem(RiryToolsRightClickMenu.ConstraintTrainHierarchy, false, 20)]
        private static void ConstraintTrainHierarchyItem() { ConstraintToolWindow.OpenTrainFromHierarchy(); }

        [MenuItem(RiryToolsRightClickMenu.ConstraintTrainHierarchy, true)]
        private static bool ConstraintTrainHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        [MenuItem(RiryToolsRightClickMenu.ConstraintBezierHierarchy, false, 21)]
        private static void ConstraintBezierHierarchyItem() { ConstraintToolWindow.OpenBezierFromHierarchy(); }

        [MenuItem(RiryToolsRightClickMenu.ConstraintBezierHierarchy, true)]
        private static bool ConstraintBezierHierarchyValid() { return !StopAllToolScripts.Active && (SceneObjectSelected()); }

        // Scene and Prefab Mode objects only, not prefab assets.
        private static bool SceneObjectSelected()
        {
            return Selection.activeTransform != null && !EditorUtility.IsPersistent(Selection.activeTransform);
        }
    }
}

// ============================================================================
// GLOSSARY: VRChat menu entries
//   Tools/RiryTools/Constraint Tool (2030)       -> ConstraintToolWindow.OpenFromTools (Editor/VRChat/UI/ConstraintTools/ConstraintToolWindow.cs):
//                                                      the Constraint Tool window on the tab used last
//   Tools/RiryTools/Menu Tool (2066), menu key E -> MenuToolWindow.Open (Editor/VRChat/UI/MenuTool/MenuToolWindow.cs): its own
//                                                      window; the Main Menu's Menu Tool tab hosts its Parameter budget
//                                                      (CreateMenuTool = MenuToolWindow.CreateHosted); its Menu manager opens
//                                                      this window
//   Tools/RiryTools/Animator Tools/Find and Replace (1995) -> BehaviourFindReplaceWindow.OpenPopup (Editor/VRChat/UI/AnimatorTools/
//                                                      BehaviourFindReplaceWindow.cs); also in the Hierarchy (22) and Project (2006)
//                                                      right-click menus, for a scene object and an animator controller
//   Tools/RiryTools/FTEmulator (2067)            -> FTEmulatorWindow.Open (Editor/VRChat/UI/FTEmulator/FTEmulatorWindow.cs): its own
//                                                      window; the Main Menu's Tools tab hosts it in its FTEmulator sub-tab
//                                                      (CreateFTEmulator = FTEmulatorWindow.CreateHosted). No right-click entry.
//   GameObject/RiryTools/Constraint Tool/Train (20)  -> ConstraintToolWindow.OpenTrainFromHierarchy: the Train tab; the active
//                                                      object becomes the chain start, a selected descendant the end.
//   GameObject/RiryTools/Constraint Tool/Bezier (21) -> ConstraintToolWindow.OpenBezierFromHierarchy: the Bezier tab, the same way.
//   SceneObjectSelected      Validator of the Hierarchy entries: a scene or Prefab Mode object is active.
// ============================================================================
