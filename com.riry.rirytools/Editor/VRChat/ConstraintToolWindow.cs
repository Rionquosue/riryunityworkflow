using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Constraint Tool: one window with a tab per subtool, Train (ConstraintTrainWindow) and
    /// Bezier (ConstraintBezierWindow). Each subtool is its own window, hosted unseen and drawn
    /// by calling its OnGUI, as the Main Menu does. Opened from Tools > RiryTools > Constraint
    /// Tool and the Hierarchy's RiryTools > Constraint Tool > Train / Bezier (RiryVRChatMenus.cs).
    /// </summary>
    internal sealed class ConstraintToolWindow : EditorWindow
    {
        internal const string Title = "Constraint Tool";
        internal static readonly string[] Tabs = { "Train", "Bezier" };
        internal const int TrainTab = 0;
        internal const int BezierTab = 1;

        [SerializeField] private int tab;
        [SerializeField] private ConstraintTrainWindow train;
        [SerializeField] private ConstraintBezierWindow bezier;

        /// <summary>Opens the window on that tab; a tab below 0 keeps the one it had.</summary>
        internal static ConstraintToolWindow Open(int tab)
        {
            ConstraintToolWindow window = GetWindow<ConstraintToolWindow>(Title);
            window.minSize = new Vector2(420f, 500f);
            window.wantsMouseMove = true;
            if (tab >= 0) window.tab = tab;
            window.Show();
            return window;
        }

        // Tools > RiryTools > Constraint Tool: the tab used last.
        internal static void OpenFromTools() { Open(-1); }

        // The active object becomes the chain start, a selected descendant the end.
        internal static void OpenTrainFromHierarchy()
        {
            Open(TrainTab).Train.UseSelection(Selection.activeTransform, Selection.transforms);
        }

        internal static void OpenBezierFromHierarchy()
        {
            Open(BezierTab).Bezier.UseSelection(Selection.activeTransform, Selection.transforms);
        }

        private ConstraintTrainWindow Train
        {
            get
            {
                if (train == null) train = Hidden<ConstraintTrainWindow>();
                return train;
            }
        }

        private ConstraintBezierWindow Bezier
        {
            get
            {
                if (bezier == null) bezier = Hidden<ConstraintBezierWindow>();
                return bezier;
            }
        }

        private static T Hidden<T>() where T : EditorWindow
        {
            var window = CreateInstance<T>();
            window.hideFlags = HideFlags.HideAndDontSave;
            return window;
        }

        private EditorWindow Current
        {
            get { return tab == BezierTab ? (EditorWindow)Bezier : Train; }
        }

        private void OnGUI()
        {
            wantsMouseMove = true;
            tab = RiryToolsMainMenu.TabStrip(this, tab, Tabs);
            EditorGUILayout.Space(4f);
            RiryToolsMainMenu.Call(Current, "OnGUI");
        }

        // The messages Unity sends a shown window are passed on to the subtool.
        private void OnInspectorUpdate()
        {
            RiryToolsMainMenu.Call(Current, "OnInspectorUpdate");
            Repaint();
        }

        private void OnSelectionChange()
        {
            RiryToolsMainMenu.Call(Current, "OnSelectionChange");
            Repaint();
        }

        private void OnHierarchyChange()
        {
            RiryToolsMainMenu.Call(Current, "OnHierarchyChange");
            Repaint();
        }

        private void OnDestroy()
        {
            if (train != null) DestroyImmediate(train);
            if (bezier != null) DestroyImmediate(bezier);
        }
    }
}

// ============================================================================
// GLOSSARY: Constraint Tool window
// ============================================================================
//
// ConstraintToolWindow        The "Constraint Tool" window (Title); minSize 420 x 500.
//   Tabs                      Train (TrainTab 0, ConstraintTrainWindow) and Bezier (BezierTab 1, ConstraintBezierWindow),
//                             drawn with RiryToolsMainMenu.TabStrip.
//   tab                       [SerializeField] The open tab.
//   train / bezier            [SerializeField] The subtool windows, made on first use (Hidden) and never shown;
//                             drawn by calling their OnGUI (RiryToolsMainMenu.Call). Destroyed with this window.
//   Open(tab)                 Opens the window on that tab (below 0 keeps the last one). OpenFromTools: Tools > RiryTools >
//                             Constraint Tool, on the tab used last. OpenTrainFromHierarchy / OpenBezierFromHierarchy:
//                             the Hierarchy entries, which open that tab and pass the selection to its UseSelection.
//   OnInspectorUpdate / OnSelectionChange / OnHierarchyChange  Passed on to the open subtool, then repaint.
