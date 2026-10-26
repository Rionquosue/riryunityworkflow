#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS with the VRChat Avatars SDK 3.7.0 or newer.
using UnityEditor;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Menu entries for the VRChat tools. They live in this assembly, which only
    /// compiles with the VRChat SDK; every other entry is in Editor/UI/RiryMenus.cs,
    /// where the editing tips also apply.
    /// </summary>
    internal static class RiryVRChatMenus
    {
        private const string ConstraintTrain = "Tools/Rirytools/Constraint Train";
        // "GameObject/" entries with a priority below 50 also appear in the Hierarchy's right-click menu.
        private const string ConstraintTrainHierarchy = "GameObject/Rirytools/Constraint Train...";

        [MenuItem(ConstraintTrain, false, 2030)]
        private static void ConstraintTrainItem() { ConstraintTrainWindow.Open(); }

        // No MenuCommand parameter, so a multi-selection opens the window once.
        [MenuItem(ConstraintTrainHierarchy, false, 20)]
        private static void ConstraintTrainHierarchyItem() { ConstraintTrainWindow.OpenFromHierarchy(); }

        // Scene and Prefab Mode objects only, not prefab assets.
        [MenuItem(ConstraintTrainHierarchy, true)]
        private static bool ConstraintTrainHierarchyValid()
        {
            return Selection.activeTransform != null && !EditorUtility.IsPersistent(Selection.activeTransform);
        }
    }
}

// ============================================================================
// GLOSSARY: VRChat menu entries
//   Tools/Rirytools/Constraint Train (2030)         -> ConstraintTrainWindow.Open (Editor/VRChat/ConstraintTrainWindow.UI.cs)
//   GameObject/Rirytools/Constraint Train... (20)   -> ConstraintTrainWindow.OpenFromHierarchy: the active
//                                                      object becomes the chain start, a selected descendant the end.
// ============================================================================
#endif
