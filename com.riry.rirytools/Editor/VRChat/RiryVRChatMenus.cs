using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Menu entries for the VRChat tools. They live in this assembly, which only
    /// compiles with the VRChat SDK; every other entry is in Editor/UI/RiryMenus.cs,
    /// where the editing tips also apply and the paths are defined. Without the SDK,
    /// RiryMenus.cs has the same paths showing a "VRCSDK not found" popup, so keep
    /// the priorities of both files in step.
    /// </summary>
    [InitializeOnLoad]
    internal static class RiryVRChatMenus
    {
        // The Main Menu's Constraint Tool tab hosts these windows (its Train, Bezier and Insertable prop sub-tabs), and
        // its Menu Tool tab the Menu Tool's Parameter budget (its Menu manager opens the Menu Tool window).
        static RiryVRChatMenus()
        {
            RiryToolsMainMenu.CreateMenuTool = MenuToolWindow.CreateHosted;
            RiryToolsMainMenu.CreateConstraintTrain = () => UnityEngine.ScriptableObject.CreateInstance<ConstraintTrainWindow>();
            RiryToolsMainMenu.CreateConstraintBezier = () => UnityEngine.ScriptableObject.CreateInstance<ConstraintBezierWindow>();
            RiryToolsMainMenu.CreateConstraintInsertable = () => UnityEngine.ScriptableObject.CreateInstance<InsertablePropWindow>();
        }

        [MenuItem(RiryMenus.ConstraintTool, false, 2030)]
        private static void ConstraintToolItem() { ConstraintToolWindow.OpenFromTools(); }

        [MenuItem(RiryMenus.MenuTool, false, 2066)]
        private static void MenuToolItem() { MenuToolWindow.Open(); }

        // No MenuCommand parameter, so a multi-selection opens the window once.
        // "GameObject/" entries with a priority below 50 also appear in the Hierarchy's right-click menu.
        [MenuItem(RiryMenus.ConstraintTrainHierarchy, false, 20)]
        private static void ConstraintTrainHierarchyItem() { ConstraintToolWindow.OpenTrainFromHierarchy(); }

        [MenuItem(RiryMenus.ConstraintTrainHierarchy, true)]
        private static bool ConstraintTrainHierarchyValid() { return SceneObjectSelected(); }

        [MenuItem(RiryMenus.ConstraintBezierHierarchy, false, 21)]
        private static void ConstraintBezierHierarchyItem() { ConstraintToolWindow.OpenBezierFromHierarchy(); }

        [MenuItem(RiryMenus.ConstraintBezierHierarchy, true)]
        private static bool ConstraintBezierHierarchyValid() { return SceneObjectSelected(); }

        // Scene and Prefab Mode objects only, not prefab assets.
        private static bool SceneObjectSelected()
        {
            return Selection.activeTransform != null && !EditorUtility.IsPersistent(Selection.activeTransform);
        }
    }
}

// ============================================================================
// GLOSSARY: VRChat menu entries
//   Tools/RiryTools/Constraint Tool (2030)       -> ConstraintToolWindow.OpenFromTools (Editor/VRChat/ConstraintToolWindow.cs):
//                                                      the Constraint Tool window on the tab used last
//   Tools/RiryTools/Menu Tool (2066), menu key E -> MenuToolWindow.Open (Editor/VRChat/MenuTool/MenuToolWindow.cs): its own
//                                                      window; the Main Menu's Menu Tool tab hosts its Parameter budget
//                                                      (CreateMenuTool = MenuToolWindow.CreateHosted); its Menu manager opens
//                                                      this window
//   GameObject/RiryTools/Constraint Tool/Train (20)  -> ConstraintToolWindow.OpenTrainFromHierarchy: the Train tab; the active
//                                                      object becomes the chain start, a selected descendant the end.
//   GameObject/RiryTools/Constraint Tool/Bezier (21) -> ConstraintToolWindow.OpenBezierFromHierarchy: the Bezier tab, the same way.
//   SceneObjectSelected      Validator of both Hierarchy entries: a scene or Prefab Mode object is active.
// ============================================================================
