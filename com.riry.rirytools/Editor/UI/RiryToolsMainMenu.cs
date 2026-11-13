using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// A tool window that draws parts of itself outside IMGUI (Menu Tool's radial preview): hosted in a tab, it is told
    /// the window it is actually drawn in.
    /// </summary>
    internal interface IRiryHostedTool
    {
        EditorWindow Surface { set; }

        /// <summary>Another tab is shown: take anything drawn outside IMGUI off the window.</summary>
        void NotDrawn();
    }

    /// <summary>
    /// Main Menu: one dockable RiryTools window with a tab per tool, in menu order.
    /// Each tab draws the tool's own window, hosted unseen, so the tools work there as
    /// they do on their own; their own menu entries are unchanged. Documentation is a
    /// link at the bottom right. Menu Mockup stays in the Tools menu only.
    /// Opened from Tools > RiryTools > Main Menu (Editor/UI/RiryToolsMenuBar.cs).
    /// </summary>
    internal sealed class RiryToolsMainMenu : EditorWindow
    {
        // Set by Editor/VRChat/UI/RiryToolsVRChatMenus.cs when the VRChat SDK is in the project.
        internal static Func<EditorWindow> CreateConstraintTrain;
        internal static Func<EditorWindow> CreateConstraintBezier;
        internal static Func<EditorWindow> CreateConstraintInsertable;
        internal static Func<EditorWindow> CreateMenuTool;
        internal static Func<EditorWindow> CreateFindReplace;

        private static readonly string[] Tabs =
        {
            "AutoSync", "Blendshape Sync", "Clean Animation Keys", "Constraint Tool", "GUID Tools", "Import Presets", "Menu Tool", "Tools", "Animator Tools" 
        };
        // Tools that need more room than a tab: the Tools tab has a button for each, which opens it in its own window.
        private static readonly string[] PopupTools = { "Avatar Migration Assistant", "IconCap" };
        // Constraint Tool's sub-tabs, as in its own window (Editor/VRChat/UI/ConstraintToolWindow.cs).
        private static readonly string[] AnimatorTabs = { "Find and Replace" };
        private static readonly string[] ConstraintTabs = { "Train", "Bezier", "Insertable prop" };
        private static readonly string[] ImportTabs =
        {
            "Set Folder Import Presets", "Save Import Preset", "Extract Animation", "Open Import Preset Library"
        };

        /// <summary>A tool window drawn in a tab; never shown on its own.</summary>
        [Serializable]
        private sealed class Hosted
        {
            public string Key;
            public EditorWindow Window;
        }

        // The tab strip: flat cells of equal width with no gaps, the active one lighter and bold, a
        // hover tint, wrapping onto more rows when the window is narrow (like YGDR Animator Editor's).
        // Internal so other tab strips (Menu Mockup's Tabs block) look the same.
        internal const float TabMinWidth = 110f;
        internal const float TabMinHeight = 24f;
        internal static readonly Color DarkTab = new Color32(56, 56, 56, 255);
        internal static readonly Color DarkActiveTab = new Color32(90, 90, 90, 255);
        internal static readonly Color LightTab = new Color32(200, 200, 200, 255);
        internal static readonly Color LightActiveTab = new Color32(160, 160, 160, 255);
        internal static readonly Color TabSeparator = new Color(0f, 0f, 0f, 0.5f);
        // Outline around every tab, in whole pixels so it draws crisply.
        internal const float TabBorder = 2f;
        // Space above the sub-tab strips (Constraint Tool, Import Presets), under the main tabs.
        internal const float SubTabGap = 8f;
        // Space above the main tabs, so their top outline isn't hidden under the window's own tab.
        internal const float TopMargin = 2f;
        internal const float HoverTint = 0.1f;
        private static GUIStyle tabLabel;
        private static GUIStyle activeTabLabel;

        [SerializeField] private int tab;
        [SerializeField] private int constraintTab;
        [SerializeField] private int importTab;
        // The current tab's scroll position; back to the top when the tab changes.
        [SerializeField] private Vector2 tabScroll;
        [SerializeField] private List<Hosted> hosted = new List<Hosted>();
        // The hosted tool drawn in this GUI pass, so the others hear they were not (IRiryHostedTool.NotDrawn).
        [NonSerialized] private EditorWindow drawn;

        internal static void Open()
        {
            RiryToolsMainMenu window = GetWindow<RiryToolsMainMenu>("RiryTools");
            window.minSize = new Vector2(480f, 380f);
            window.wantsMouseMove = true;
            window.Show();
        }

        private void OnGUI()
        {
            wantsMouseMove = true;
            GUILayout.Space(TopMargin);
            int chosen = TabStrip(this, tab, Tabs);
            if (chosen != tab) tabScroll = Vector2.zero;
            tab = chosen;
            EditorGUILayout.Space(4f);
            drawn = null;
            // Menu Tool scrolls its own parts: its radial preview is placed over the window and cannot follow a scroll view.
            if (Current == "Menu Tool")
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true))) DrawTab();
            }
            else
            {
                // A scroll view, so a tool taller or wider than the window stays usable.
                using (var scroll = new EditorGUILayout.ScrollViewScope(tabScroll, GUILayout.ExpandHeight(true)))
                {
                    tabScroll = scroll.scrollPosition;
                    DrawTab();
                }
            }
            foreach (Hosted entry in hosted)
            {
                if (entry.Window != null && entry.Window != drawn && entry.Window is IRiryHostedTool tool) tool.NotDrawn();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (EditorGUILayout.LinkButton("Documentation")) Application.OpenURL(RiryToolsMenuBar.DocumentationLink);
            }
        }

        // The current tab's name; the tabs are told apart by name, not by number.
        private string Current
        {
            get { return Tabs[Mathf.Clamp(tab, 0, Tabs.Length - 1)]; }
        }

        private void DrawTab()
        {
            switch (Current)
            {
                case "AutoSync":
                    bool sync = EditorGUILayout.ToggleLeft("Sync", AutoSync.Enabled);
                    if (sync != AutoSync.Enabled) { AutoSyncGitActions.ToggleSync(); RiryToolGUI.ExitAfterModal(); }
                    if (!GitState.GitInstalled) EditorGUILayout.HelpBox(new GitNotInstalledException(null).Message, MessageType.Info);
                    else Draw(Active());
                    break;
                case "Animator Tools":
                    if (CreateFindReplace == null)
                    {
                        EditorGUILayout.HelpBox(new VrcSdkNotFoundException("Find and Replace").Message, MessageType.Info);
                        break;
                    }
                    EditorGUILayout.Space(SubTabGap);
                    TabStrip(this, 0, AnimatorTabs, GUI.skin.verticalScrollbar.fixedWidth + 4f);
                    EditorGUILayout.Space(4f);
                    Draw(Active());
                    break;
                case "Blendshape Sync":
                    // The running jobs for review; a job is added or edited in its popup.
                    BlendshapeSyncJobList.Draw();
                    break;
                case "Constraint Tool":
                    if (CreateConstraintTrain == null)
                    {
                        EditorGUILayout.HelpBox(new VrcSdkNotFoundException("Constraint Tool").Message, MessageType.Info);
                        break;
                    }
                    EditorGUILayout.Space(SubTabGap);
                    constraintTab = TabStrip(this, constraintTab, ConstraintTabs, GUI.skin.verticalScrollbar.fixedWidth + 4f);
                    EditorGUILayout.Space(4f);
                    Draw(Active());
                    break;
                case "Menu Tool":
                    if (CreateMenuTool == null)
                    {
                        EditorGUILayout.HelpBox(new VrcSdkNotFoundException("Menu Tool").Message, MessageType.Info);
                        break;
                    }
                    Draw(Active());
                    break;
                case "Import Presets":
                    EditorGUILayout.Space(SubTabGap);
                    importTab = TabStrip(this, importTab, ImportTabs, GUI.skin.verticalScrollbar.fixedWidth + 4f);
                    EditorGUILayout.Space(4f);
                    if (importTab == 3)
                    {
                        if (GUILayout.Button("Open Import Preset Library", GUILayout.Height(28f))) ImportPresetActions.OpenLibrary();
                    }
                    else Draw(Active());
                    break;
                case "Tools":
                    // Each opens in its own window, not in this one, which may be docked small.
                    foreach (string tool in PopupTools)
                    {
                        if (!GUILayout.Button(tool, GUILayout.Height(28f))) continue;
                        if (tool == "IconCap") EditorApplication.delayCall += IconCapWindow.OpenPopup;
                        else EditorApplication.delayCall += AvatarMigratorWindow.OpenPopup;
                    }
                    if (GUILayout.Button("Find Null references", GUILayout.Height(28f))) EditorApplication.delayCall += FindNullReferencesWindow.Run;
                    // Reloads scripts on the next frame (Unity 2022.3 has no button for it).
                    if (GUILayout.Button(RefreshScripts.DisplayName, GUILayout.Height(28f))) RefreshScripts.Run();
                    EditorGUILayout.Space(4f);
                    // Set only when clicked: the setter logs, so assigning it every draw spammed the Console.
                    bool stop = EditorGUILayout.ToggleLeft(new GUIContent("Stop Auto Game",
                        "Keep focus off the Game view when Play Mode starts."), StopAutoGame.Enabled);
                    if (stop != StopAutoGame.Enabled) StopAutoGame.Enabled = stop;
                    bool guard = EditorGUILayout.ToggleLeft(new GUIContent("Script Load Guard",
                        "Block saving a scene or prefab that loaded with far fewer objects than its file holds."), ScriptLoadGuard.Enabled);
                    if (guard != ScriptLoadGuard.Enabled) ScriptLoadGuard.Enabled = guard;
                    break;
                default:
                    Draw(Active());
                    break;
            }
        }

        // The hosted window for the current tab, made on first use; null for tabs without one.
        private EditorWindow Active()
        {
            switch (Current)
            {
                case "AutoSync": return Host("Configuration", () => CreateInstance<AutoSyncGitConfigureWindow>(), window => ((AutoSyncGitConfigureWindow)window).Reload());
                case "Animator Tools": return CreateFindReplace == null ? null : Host("Find and Replace", CreateFindReplace, null);
                case "Clean Animation Keys": return Host("Clean Animation Keys", () => CreateInstance<CleanAnimationKeysWindow>(), null);
                case "Constraint Tool":
                    if (CreateConstraintTrain == null) return null;
                    if (constraintTab == 2) return Host("Insertable prop", CreateConstraintInsertable, null);
                    return constraintTab == 1 ? Host("Bezier", CreateConstraintBezier, null) : Host("Train", CreateConstraintTrain, null);
                case "GUID Tools": return Host("GUID Tools", () => CreateInstance<GuidToolsWindow>(), null);
                case "Menu Tool": return CreateMenuTool == null ? null : Host("Menu Tool", CreateMenuTool, null);
                case "Import Presets":
                    switch (importTab)
                    {
                        case 0:
                            return Host("Set Folder Import Presets", () => CreateInstance<ImportPresetFolderWindow>(),
                                window => ((ImportPresetFolderWindow)window).ShowActiveFolder());
                        case 1:
                            return Host("Save Import Preset", () => CreateInstance<ImportPresetSaveWindow>(),
                                window => ((ImportPresetSaveWindow)window).UseImporter(ImportPresetActions.SelectedImporter()));
                        case 2:
                            return Host("Extract Animation", () => CreateInstance<ImportPresetExtractAnimationWindow>(),
                                window => ((ImportPresetExtractAnimationWindow)window).ShowSelection());
                    }
                    return null;
                default: return null;
            }
        }

        /// <summary>
        /// A row of tabs, wrapping onto more rows when they don't fit at TabMinWidth. Every tab in
        /// a row is the same width, and as tall as its name needs (at least TabMinHeight).
        /// Returns the tab chosen. owner is the window it is drawn in (its width, and repainted on hover).
        /// Also used by Constraint Tool's window.
        /// </summary>
        internal static int TabStrip(EditorWindow owner, int selected, string[] names, float inset = 0f)
        {
            if (tabLabel == null)
            {
                tabLabel = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 12, padding = new RectOffset(4, 4, 3, 3)
                };
                activeTabLabel = new GUIStyle(tabLabel) { fontStyle = FontStyle.Bold };
            }
            // inset: room kept free on the right (the tab scroll view's scrollbar, for the sub-tabs).
            float available = owner.position.width - 8f - inset;
            int perRow = Mathf.Clamp(Mathf.FloorToInt(available / TabMinWidth), 1, names.Length);
            int rows = (names.Length + perRow - 1) / perRow;
            float width = available / perRow;
            // Measured at the narrowest a cell gets (TabMinWidth), not the current width: measured at the current
            // width, the strip shrank to one line in the band of widths where every name happened to fit, then
            // jumped back when the cells narrowed again. Now it only changes when the number of rows does.
            float measure = Mathf.Min(width, TabMinWidth);
            float height = TabMinHeight;
            foreach (string name in names) height = Mathf.Max(height, activeTabLabel.CalcHeight(new GUIContent(name), measure));
            Rect area = GUILayoutUtility.GetRect(available, rows * height, GUILayout.ExpandWidth(true));
            bool dark = EditorGUIUtility.isProSkin;
            Event current = Event.current;
            // The rows and height are worked out from the window (the same in every event); the cells then fill the rect
            // the layout gave, so the strip has equal margins and the sub-tabs line up with the main tabs. In Layout the
            // rect is a placeholder, and nothing is drawn.
            if (current.type != EventType.Layout) width = area.width / perRow;
            for (int i = 0; i < names.Length; i++)
            {
                // Edges rounded to whole pixels: at fractional positions the outlines drew faintly or not at all.
                int column = i % perRow;
                int row = i / perRow;
                float left = Mathf.Round(area.x + column * width);
                float top = Mathf.Round(area.y + row * height);
                var cell = new Rect(left, top, Mathf.Round(area.x + (column + 1) * width) - left, Mathf.Round(area.y + (row + 1) * height) - top);
                bool active = i == selected;
                if (current.type == EventType.Repaint)
                {
                    Color color = active ? (dark ? DarkActiveTab : LightActiveTab) : (dark ? DarkTab : LightTab);
                    if (!active && cell.Contains(current.mousePosition))
                        color = dark ? color + new Color(HoverTint, HoverTint, HoverTint, 0f) : color - new Color(HoverTint / 2f, HoverTint / 2f, HoverTint / 2f, 0f);
                    EditorGUI.DrawRect(cell, color);
                    // An outline TabBorder thick on every side: each tab draws its right and bottom edges, and the
                    // first column and row their left and top, so shared edges aren't doubled.
                    EditorGUI.DrawRect(new Rect(cell.xMax - TabBorder, cell.y, TabBorder, cell.height), TabSeparator);
                    EditorGUI.DrawRect(new Rect(cell.x, cell.yMax - TabBorder, cell.width, TabBorder), TabSeparator);
                    if (column == 0) EditorGUI.DrawRect(new Rect(cell.x, cell.y, TabBorder, cell.height), TabSeparator);
                    if (row == 0) EditorGUI.DrawRect(new Rect(cell.x, cell.y, cell.width, TabBorder), TabSeparator);
                    (active ? activeTabLabel : tabLabel).Draw(cell, new GUIContent(names[i]), false, false, false, false);
                }
                else if (current.type == EventType.MouseDown && current.button == 0 && cell.Contains(current.mousePosition))
                {
                    selected = i;
                    GUI.changed = true;
                    current.Use();
                }
                else if (current.type == EventType.MouseMove && cell.Contains(current.mousePosition)) owner.Repaint();
            }
            return selected;
        }

        private EditorWindow Host(string key, Func<EditorWindow> create, Action<EditorWindow> initialize)
        {
            Hosted entry = hosted.Find(item => item.Key == key);
            // A tool that closed itself (its Close button) starts again fresh.
            if (entry != null && entry.Window != null) return entry.Window;
            if (entry == null) hosted.Add(entry = new Hosted { Key = key });
            entry.Window = create();
            entry.Window.hideFlags = HideFlags.HideAndDontSave;
            if (initialize != null) initialize(entry.Window);
            return entry.Window;
        }

        private void Draw(EditorWindow window)
        {
            if (window == null) return;
            // Set every draw: hosted windows outlive a script reload, this reference does not.
            if (window is IRiryHostedTool tool) tool.Surface = this;
            drawn = window;
            Call(window, "OnGUI");
        }

        // Calls one of the hosted window's own message methods, when it has it.
        internal static void Call(EditorWindow window, string method)
        {
            MethodInfo info = window.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (info == null) return;
            // A delegate, not Invoke, so GUIUtility.ExitGUI and errors pass through unwrapped.
            ((Action)Delegate.CreateDelegate(typeof(Action), window, info))();
        }

        // The messages Unity sends a shown window are passed on to the tab's tool.
        private void OnInspectorUpdate()
        {
            EditorWindow window = ActiveIfMade();
            if (window != null) Call(window, "OnInspectorUpdate");
            Repaint();
        }

        private void OnSelectionChange()
        {
            EditorWindow window = ActiveIfMade();
            if (window == null) return;
            Call(window, "OnSelectionChange");
            // Save Import Preset follows the selected asset while it is a tab.
            var save = window as ImportPresetSaveWindow;
            AssetImporter importer = ImportPresetActions.SelectedImporter();
            if (save != null && ImportPresetLibrary.CanSave(importer) && importer.assetPath != save.AssetPath) save.UseImporter(importer);
            Repaint();
        }

        private void OnProjectChange()
        {
            EditorWindow window = ActiveIfMade();
            if (window != null) Call(window, "OnProjectChange");
        }

        private void OnHierarchyChange()
        {
            EditorWindow window = ActiveIfMade();
            if (window != null) Call(window, "OnHierarchyChange");
        }

        // The current tab's hosted window if it was made, without making it.
        private EditorWindow ActiveIfMade()
        {
            string name = Current;
            string key = name == "AutoSync" ? "Configuration"
                : name == "Import Presets" ? (importTab < 3 ? ImportTabs[importTab] : null)
                : name == "Constraint Tool" ? ConstraintTabs[Mathf.Clamp(constraintTab, 0, ConstraintTabs.Length - 1)]
                : name == "Blendshape Sync" || name == "Tools" ? null : name;
            Hosted entry = key == null ? null : hosted.Find(item => item.Key == key);
            return entry != null ? entry.Window : null;
        }

        private void OnDestroy()
        {
            foreach (Hosted entry in hosted)
            {
                if (entry.Window != null) DestroyImmediate(entry.Window);
            }
            hosted.Clear();
        }
    }
}

// ============================================================================
// GLOSSARY: Main Menu
// The menu entry, Tools > RiryTools > Main Menu, is in RiryToolsMenuBar.cs.
// ============================================================================
//
// RiryToolsMainMenu (this file): the dockable "RiryTools" window
//   Tabs                     AutoSync, Blendshape Sync, Clean Animation Keys, Constraint Tool, GUID Tools, Import Presets,
//                            Menu Tool, Tools (not Menu Mockup or Documentation). Current is the selected tab's
//                            name; tabs are handled by name.
//   Menu Tool tab            CreateMenuTool (set by Editor/VRChat/RiryToolsVRChatMenus.cs; null without the VRChat SDK) makes the
//                            hosted MenuToolWindow (MenuToolWindow.CreateHosted). It shows the Parameter budget, drawn
//                            outside the tab scroll view (the budget scrolls itself); its Menu manager tab opens the Menu
//                            Tool window.
//   IRiryHostedTool          A hosted tool told the window it is drawn in (Surface), set on every Draw, and NotDrawn() in a GUI
//                            pass that drew another tab (drawn: the one drawn), so Menu Tool's radial leaves at once.
//   Tools tab / PopupTools   A button per tool that needs more room (Avatar Migration Assistant, IconCap); each opens
//                            the tool in its own floating window (OpenPopup), not inside this one; then Refresh Scripts
//                            (RefreshScripts.Run), the Stop Auto Game toggle (StopAutoGame.Enabled, set only when clicked) and
//                            the Script Load Guard toggle (ScriptLoadGuard.Enabled, the same way).
//   ImportTabs               Import Presets' sub-tabs: Set Folder Import Presets, Save Import Preset,
//                            Extract Animation, Open Import Preset Library (a button).
//   ConstraintTabs           Constraint Tool's sub-tabs (constraintTab, [SerializeField]): Train, Bezier and Insertable prop,
//                            hosted under those names, like its own window (Editor/VRChat/ConstraintToolWindow.cs).
//   CreateConstraintTrain / CreateConstraintBezier / CreateConstraintInsertable  Set by Editor/VRChat/RiryToolsVRChatMenus.cs; null without the VRChat SDK
//                            (the tab says so).
//   Hosted / hosted          Each tab's tool window, made on first use and never shown; drawn by calling its OnGUI.
//   Active() / Host(...)     The current tab's tool, made and set up like its Open would.
//   Call(window, method)     Calls the tool's own OnGUI, OnInspectorUpdate, OnSelectionChange, OnProjectChange or
//                            OnHierarchyChange, so it behaves as when shown.
//   OnGUI()                  The tabs, the current tool in a scroll view (tabScroll, [SerializeField], reset on a tab
//                            change), and the Documentation link at the bottom right.
//   Tab constants            TabMinWidth, TabMinHeight, DarkTab, DarkActiveTab, LightTab, LightActiveTab, TabSeparator,
//                            TabBorder, HoverTint: internal, so Menu Mockup's Tabs block draws the same strip.
//   TabStrip(owner, selected, names, inset)  Static: the tool tabs, Constraint Tool's and Import Presets' sub-tabs: flat cells of equal width (at least TabMinWidth,
//                            110), wrapping onto more rows, each as tall as its longest name needs at TabMinWidth (at least
//                            TabMinHeight, 24), so the height doesn't change with the window's width; the active tab lighter (DarkActiveTab / LightActiveTab) and bold, a hover tint
//                            (HoverTint), like YGDR Animator Editor's tab strip. Every tab has a TabBorder (2 px) outline
//                            in TabSeparator, on edges rounded to whole pixels. The rows come from the window width less
//                            inset; the cells then fill the rect the layout gives, so the margins are equal and the sub-tabs
//                            line up with the main tabs.
//   TopMargin / SubTabGap    2 px above the main tabs (and Constraint Tool window's); 8 px between them and a tab's sub-tabs
//                            (Constraint Tool, Import Presets).
//   AutoSync tab             Sync toggle (AutoSyncGitActions.ToggleSync) above Configuration.
//   Blendshape Sync tab      BlendshapeSyncJobList (Editor/UI/BlendshapeSync/BlendshapeSyncJobList.cs): the running jobs, and Add Job.
