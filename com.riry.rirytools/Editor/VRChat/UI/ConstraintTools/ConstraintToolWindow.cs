using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Constraint Tool: one window with a tab per subtool, Train (ConstraintTrainWindow),
    /// Bezier (ConstraintBezierWindow), Insertable prop (InsertablePropWindow), and Dual axis detector and Three axis detector
    /// (TiltDetectorWindow). Each subtool is its own window, hosted unseen and drawn
    /// by calling its OnGUI, as the Main Menu does. Opened from Tools > RiryTools > Constraint
    /// Tool and the Hierarchy's RiryTools > Constraint Tool > Train / Bezier (RiryToolsVRChatMenus.cs).
    /// </summary>
    internal sealed class ConstraintToolWindow : EditorWindow, IRiryHostedTool
    {
        internal const string Title = "Constraint Tool";
        internal static readonly string[] Tabs = { "Train", "Bezier", "Insertable prop", "Dual axis detector", "Three axis detector" };
        internal const int TrainTab = 0;
        internal const int BezierTab = 1;
        internal const int InsertableTab = 2;
        internal const int DualAxisTab = 3;
        internal const int ThreeAxisTab = 4;

        [SerializeField] private int tab;
        [SerializeField] private ConstraintTrainWindow train;
        [SerializeField] private ConstraintBezierWindow bezier;
        [SerializeField] private InsertablePropWindow insertable;
        [SerializeField] private TiltDetectorWindow dualAxis;
        [SerializeField] private TiltDetectorWindow threeAxis;
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

        // The active object becomes the Socket, the avatar it is in the Avatar root.
        internal static void OpenInsertableFromHierarchy()
        {
            Open(InsertableTab).Insertable.UseSelection(Selection.activeTransform);
        }

        // The active object becomes Source, another selected object Relative to.
        internal static void OpenDualAxisFromHierarchy()
        {
            Open(DualAxisTab).DualAxis.UseSelection(Selection.activeTransform, Selection.transforms);
        }

        internal static void OpenThreeAxisFromHierarchy()
        {
            Open(ThreeAxisTab).ThreeAxis.UseSelection(Selection.activeTransform, Selection.transforms);
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

        private TiltDetectorWindow DualAxis
        {
            get
            {
                if (dualAxis == null) dualAxis = Hidden<TiltDetectorWindow>();
                return dualAxis;
            }
        }

        private TiltDetectorWindow ThreeAxis
        {
            get
            {
                if (threeAxis == null)
                {
                    threeAxis = Hidden<TiltDetectorWindow>();
                    threeAxis.threeAxis = true;
                }
                return threeAxis;
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
            get
            {
                switch (tab)
                {
                    case BezierTab: return Bezier;
                    case InsertableTab: return Insertable;
                    case DualAxisTab: return DualAxis;
                    case ThreeAxisTab: return ThreeAxis;
                    default: return Train;
                }
            }
        }

        private void OnGUI()
        {
            if (RiryToolGUI.Stopped()) return;
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
            if (StopAllToolScripts.Active) return;
            RiryToolsMainMenu.Call(Current, "OnInspectorUpdate");
            Repaint();
        }

        private void OnSelectionChange()
        {
            if (StopAllToolScripts.Active) return;
            RiryToolsMainMenu.Call(Current, "OnSelectionChange");
            Repaint();
        }

        private void OnHierarchyChange()
        {
            if (StopAllToolScripts.Active) return;
            RiryToolsMainMenu.Call(Current, "OnHierarchyChange");
            Repaint();
        }

        private void OnDestroy()
        {
            if (train != null) DestroyImmediate(train);
            if (bezier != null) DestroyImmediate(bezier);
            if (insertable != null) DestroyImmediate(insertable);
            if (dualAxis != null) DestroyImmediate(dualAxis);
            if (threeAxis != null) DestroyImmediate(threeAxis);
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
//                             OpenInsertableFromHierarchy / OpenDualAxisFromHierarchy / OpenThreeAxisFromHierarchy: the same,
//                             for the Insertable prop, Dual axis detector and Three axis detector tabs.
//   OnInspectorUpdate / OnSelectionChange / OnHierarchyChange  Passed on to the open subtool, then repaint.
