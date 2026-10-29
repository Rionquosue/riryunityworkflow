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
    internal static class RiryVRChatMenus
    {
        [MenuItem(RiryMenus.ConstraintTrain, false, 2030)]
        private static void ConstraintTrainItem() { ConstraintTrainWindow.Open(); }

        // No MenuCommand parameter, so a multi-selection opens the window once.
        // "GameObject/" entries with a priority below 50 also appear in the Hierarchy's right-click menu.
        [MenuItem(RiryMenus.ConstraintTrainHierarchy, false, 20)]
        private static void ConstraintTrainHierarchyItem() { ConstraintTrainWindow.OpenFromHierarchy(); }

        // Scene and Prefab Mode objects only, not prefab assets.
        [MenuItem(RiryMenus.ConstraintTrainHierarchy, true)]
        private static bool ConstraintTrainHierarchyValid()
        {
            return Selection.activeTransform != null && !EditorUtility.IsPersistent(Selection.activeTransform);
        }
    }
}

// ============================================================================
// GLOSSARY: VRChat menu entries
//   Tools/RiryTools/Constraint Train (2030)         -> ConstraintTrainWindow.Open (Editor/VRChat/ConstraintTrainWindow.UI.cs)
//   GameObject/RiryTools/Constraint Train... (20)   -> ConstraintTrainWindow.OpenFromHierarchy: the active
//                                                      object becomes the chain start, a selected descendant the end.
// ============================================================================
