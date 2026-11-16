using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Constraint Tool: one window with a tab per subtool, Train (ConstraintTrainWindow),
    /// Bezier (ConstraintBezierWindow) and Insertable prop (InsertablePropWindow). Each subtool is its own window, hosted unseen and drawn
    /// by calling its OnGUI, as the Main Menu does. Opened from Tools > RiryTools > Constraint
    /// Tool and the Hierarchy's RiryTools > Constraint Tool > Train / Bezier (RiryToolsVRChatMenus.cs).
    /// </summary>
    internal sealed class ConstraintToolWindow : EditorWindow, IRiryHostedTool
    {
        internal const string Title = "Constraint Tool";
        internal static readonly string[] Tabs = { "Train", "Bezier", "Insertable prop" };
        internal const int TrainTab = 0;
        internal const int BezierTab = 1;
        internal const int InsertableTab = 2;

        [SerializeField] private int tab;
        [SerializeField] private ConstraintTrainWindow train;
        [SerializeField] private ConstraintBezierWindow bezier;
        [SerializeField] private InsertablePropWindow insertable;
        // The Main Menu's copy (CreateHosted): never shown on its own.
        [SerializeField] private bool hosted;
        // The Main Menu while this window is hosted in its Constraint Tool tab (IRiryHostedTool); null on its own.
        [System.NonSerialized] private EditorWindow surface;

        public EditorWindow Surface
        {
            set { surface = value; }
        }

        // Nothing is drawn outside IMGUI.
        public void NotDrawn()
        {
        }

        /// <summary>The Main Menu's copy (RiryToolsVRChatMenus sets RiryToolsMainMenu.CreateConstraintTool to this): never shown on its own.</summary>
        internal static EditorWindow CreateHosted()
        {
            var window = CreateInstance<ConstraintToolWindow>();
            window.hosted = true;
            return window;
        }

        /// <summary>Opens the window on that tab; a tab below 0 keeps the one it had.</summary>
        internal static ConstraintToolWindow Open(int tab)
        {
            // Never the Main Menu's copy: GetWindow could find that one and show it as a second window drawing the same tool.
            ConstraintToolWindow window = null;
            foreach (ConstraintToolWindow found in Resources.FindObjectsOfTypeAll<ConstraintToolWindow>())
            {
                if (found.hosted) continue;
                window = found;
                break;
            }
            if (window == null)
            {
                window = CreateInstance<ConstraintToolWindow>();
                window.titleContent = new GUIContent(Title);
            }
            window.minSize = new Vector2(420f, 500f);
            window.wantsMouseMove = true;
            if (tab >= 0) window.tab = tab;
            window.Show();
            window.Focus();
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

        private InsertablePropWindow Insertable
        {
            get
            {
                if (insertable == null) insertable = Hidden<InsertablePropWindow>();
                return insertable;
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
            get { return tab == InsertableTab ? Insertable : tab == BezierTab ? (EditorWindow)Bezier : Train; }
        }

        private void OnGUI()
        {
            if (surface != null)
            {
                // Hosted: sub-tabs under the Main Menu's tabs, sized to the Main Menu and clear of its tab scroll view's scrollbar.
                EditorGUILayout.Space(RiryToolsMainMenu.SubTabGap);
                tab = RiryToolsMainMenu.TabStrip(surface, tab, Tabs, GUI.skin.verticalScrollbar.fixedWidth + 4f);
            }
            else
            {
                wantsMouseMove = true;
                // As in the Main Menu, so the strip's top outline is not hidden under the window's own tab.
                GUILayout.Space(RiryToolsMainMenu.TopMargin);
                tab = RiryToolsMainMenu.TabStrip(this, tab, Tabs);
            }
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
            if (insertable != null) DestroyImmediate(insertable);
        }
    }
}

// ============================================================================
// GLOSSARY: Constraint Tool window
// ============================================================================
//
// ConstraintToolWindow        The "Constraint Tool" window (Title); minSize 420 x 500. Also hosted in the Main Menu's
//                             Constraint Tool tab (CreateHosted; Surface is then the Main Menu, and the sub-tabs are drawn
//                             under its tabs, SubTabGap below them). Tabs is the one list of the sub-tabs.
//   hosted                    [SerializeField] The Main Menu's copy; Open never picks it (GetWindow could show it as a
//                             second window drawing the same tool).
//   Tabs                      Train (TrainTab 0, ConstraintTrainWindow), Bezier (BezierTab 1, ConstraintBezierWindow) and
//                             Insertable prop (InsertableTab 2, InsertablePropWindow),
//                             drawn with RiryToolsMainMenu.TabStrip below RiryToolsMainMenu.TopMargin.
//   tab                       [SerializeField] The open tab.
//   train / bezier / insertable  [SerializeField] The subtool windows, made on first use (Hidden) and never shown;
//                             drawn by calling their OnGUI (RiryToolsMainMenu.Call). Destroyed with this window.
//   Open(tab)                 Opens the window on that tab (below 0 keeps the last one). OpenFromTools: Tools > RiryTools >
//                             Constraint Tool, on the tab used last. OpenTrainFromHierarchy / OpenBezierFromHierarchy:
//                             the Hierarchy entries, which open that tab and pass the selection to its UseSelection.
//   OnInspectorUpdate / OnSelectionChange / OnHierarchyChange  Passed on to the open subtool, then repaint.
