using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
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
        // The Constraint Tools window, which draws its own Train, Bezier and Insertable prop sub-tabs.
        internal static Func<EditorWindow> CreateConstraintTool;
        internal static Func<EditorWindow> CreateMenuTool;
        internal static Func<EditorWindow> CreateFindReplace;
        // The FTEmulator window, hosted in the FTEmulator tab.
        internal static Func<EditorWindow> CreateFTEmulator;

        private static readonly string[] Tabs = { "General Tools", "Menu Tools", "Animator Tools", "Debug Tools", "FTEmulator" };
        // The General Tools tab's sub-tabs: General (the buttons and toggles below), then a sub-tab per tool.
        private static readonly string[] ToolsTabs = { "General", "GUID Tools", "Blendshape Sync", "Import Presets", "Constraint Tools", "AutoSync" };
        // Tools that need more room than a tab: the General Tools tab's General sub-tab has a button for each, which opens it in its own window.
        private static readonly string[] PopupTools = { "Avatar Migration Assistant", "IconCap" };
        private static readonly string[] AnimatorTabs = { "Find and Replace", "Clean Animation Keys", "Controller Cleaner", "Extract Animations" };
        private static readonly string[] ImportTabs =
        {
            "Set Folder Import Presets", "Save Import Preset", "Open Import Preset Library"
        };

        /// <summary>A tool window drawn in a tab; never shown on its own.</summary>
        [Serializable]
        private sealed class Hosted
        {
            public string Key;
            public EditorWindow Window;
        }

        // The tab strip: flat cells of equal width with no gaps, the active one coloured by its level and bold, a
        // hover tint, wrapping onto more rows when the window is narrow (like YGDR Animator Editor's).
        // Internal so other tab strips (Menu Mockup's Tabs block) look the same.
        internal const float TabMinWidth = 110f;
        // Tab levels: MainTabs (the Main Menu's tabs), SubTabs (a tab's sub-tabs), ToolTabs (the tools inside a sub-tab,
        // such as Constraint Tools' Train). Each level is shorter and its active tab a different colour, so it is clear
        // which strip belongs under which.
        internal const int MainTabs = 0;
        internal const int SubTabs = 1;
        internal const int ToolTabs = 2;
        // Minimum heights per level; a strip whose names need two lines grows to fit.
        internal static readonly float[] TabMinHeights = { 48f, 36f, 24f };
        // Active tab per level: light blue at the brightest value, a darker blue, then seafoam green a little darker
        // still. The same in the light and dark skins; their text is dark (ActiveTabText) to read on them.
        internal static readonly Color[] ActiveTabs =
        {
            new Color32(150, 210, 255, 255), new Color32(95, 160, 225, 255), new Color32(80, 205, 165, 255)
        };
        internal static readonly Color ActiveTabText = new Color32(20, 20, 20, 255);
        internal static readonly Color DarkTab = new Color32(56, 56, 56, 255);
        internal static readonly Color LightTab = new Color32(200, 200, 200, 255);
        internal static readonly Color TabSeparator = new Color(0f, 0f, 0f, 0.5f);
        // Outline around every tab, in whole pixels so it draws crisply.
        internal const float TabBorder = 2f;
        // Space between a tab strip and the sub-tab strip under it.
        internal const float SubTabGap = 2f;
        // Space above the main tabs, so their top outline isn't hidden under the window's own tab.
        internal const float TopMargin = 2f;
        internal const float HoverTint = 0.1f;
        private static GUIStyle tabLabel;
        private static GUIStyle activeTabLabel;

        [SerializeField] private int tab;
        [SerializeField] private int importTab;
        [SerializeField] private int animatorTab;
        [SerializeField] private int toolsTab;
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
            // Stop all tool scripts: no tool is drawn, only the notice and the toggle that turns it off.
            if (StopAllToolScripts.Active)
            {
                RiryToolGUI.Stopped();
                DrawStopToggle();
                return;
            }
            int chosen = TabStrip(this, tab, Tabs);
            if (chosen != tab) tabScroll = Vector2.zero;
            tab = chosen;
            // Tabs with sub-tabs put them right under the main tabs, SubTabGap apart; the others leave 4 px above their tool.
            EditorGUILayout.Space(Current == "General Tools" || Current == "Animator Tools" ? SubTabGap : 4f);
            drawn = null;
            // Menu Tools scrolls its own parts: its radial preview is placed over the window and cannot follow a scroll view.
            if (Current == "Menu Tools")
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

        // What is drawn below the tabs: the General Tools tab's current sub-tab, or the current tab.
        private string Section
        {
            get { return Current == "General Tools" ? ToolsTabs[Mathf.Clamp(toolsTab, 0, ToolsTabs.Length - 1)] : Current; }
        }

        private void DrawTab()
        {
            if (Current == "General Tools")
            {
                toolsTab = TabStrip(this, toolsTab, ToolsTabs, GUI.skin.verticalScrollbar.fixedWidth + 4f, SubTabs);
                // Import Presets and Constraint Tools draw their own sub-tabs right under these.
                if (Section != "Import Presets" && Section != "Constraint Tools") EditorGUILayout.Space(4f);
            }
            switch (Section)
            {
                case "AutoSync":
                    bool sync = EditorGUILayout.ToggleLeft("Sync", AutoSync.Enabled);
                    if (sync != AutoSync.Enabled) { AutoSyncGitActions.ToggleSync(); RiryToolGUI.ExitAfterModal(); }
                    if (!GitState.GitInstalled) EditorGUILayout.HelpBox(new GitNotInstalledException(null).Message, MessageType.Info);
                    else Draw(Active());
                    break;
                case "Animator Tools":
                    animatorTab = TabStrip(this, animatorTab, AnimatorTabs, GUI.skin.verticalScrollbar.fixedWidth + 4f, SubTabs);
                    EditorGUILayout.Space(4f);
                    // Find and Replace needs the VRChat SDK; Controller Cleaner does not.
                    if (animatorTab == 0 && CreateFindReplace == null)
                    {
                        EditorGUILayout.HelpBox(new VrcSdkNotFoundException("Find and Replace").Message, MessageType.Info);
                        break;
                    }
                    Draw(Active());
                    break;
                case "Blendshape Sync":
                    // The running jobs for review; a job is added or edited in its popup.
                    BlendshapeSyncJobList.Draw();
                    break;
                case "Constraint Tools":
                    // The Constraint Tools window draws its own sub-tabs (Editor/VRChat/UI/ConstraintTools/ConstraintToolWindow.cs).
                    if (CreateConstraintTool == null)
                    {
                        EditorGUILayout.HelpBox(new VrcSdkNotFoundException("Constraint Tools").Message, MessageType.Info);
                        break;
                    }
                    Draw(Active());
                    break;
                case "Menu Tools":
                    if (CreateMenuTool == null)
                    {
                        EditorGUILayout.HelpBox(new VrcSdkNotFoundException("Menu Tools").Message, MessageType.Info);
                        break;
                    }
                    Draw(Active());
                    break;
                case "Import Presets":
                    EditorGUILayout.Space(SubTabGap);
                    importTab = TabStrip(this, importTab, ImportTabs, GUI.skin.verticalScrollbar.fixedWidth + 4f, ToolTabs);
                    EditorGUILayout.Space(4f);
                    if (importTab == 2)
                    {
                        if (GUILayout.Button("Open Import Preset Library", GUILayout.Height(28f))) ImportPresetActions.OpenLibrary();
                    }
                    else Draw(Active());
                    break;
                case "FTEmulator":
                    // FTEmulator needs the VRChat SDK and its own package.
                    if (CreateFTEmulator == null) EditorGUILayout.HelpBox(RiryToolsMenuBar.FTEmulatorUnavailable(), MessageType.Info);
                    else Draw(Active());
                    break;
                case "General":
                    // General. Each opens in its own window, not in this one, which may be docked small.
                    foreach (string tool in PopupTools)
                    {
                        if (!GUILayout.Button(tool, GUILayout.Height(28f))) continue;
                        if (tool == "IconCap") EditorApplication.delayCall += IconCapWindow.OpenPopup;
                        else EditorApplication.delayCall += AvatarMigratorWindow.OpenPopup;
                    }
                    if (GUILayout.Button(MaterialCleaner.Title, GUILayout.Height(28f))) EditorApplication.delayCall += MaterialCleanerWindow.Open;
                    EditorGUILayout.Space(4f);
                    // Set only when clicked: the setter logs, so assigning it every draw spammed the Console.
                    bool stop = EditorGUILayout.ToggleLeft(new GUIContent("Stop Auto Game",
                        "Keep focus off the Game view when Play Mode starts."), StopAutoGame.Enabled);
                    if (stop != StopAutoGame.Enabled) StopAutoGame.Enabled = stop;
                    bool guard = EditorGUILayout.ToggleLeft(new GUIContent("Script Load Guard",
                        "Block saving a scene or prefab that loaded with far fewer objects than its file holds."), ScriptLoadGuard.Enabled);
                    if (guard != ScriptLoadGuard.Enabled) ScriptLoadGuard.Enabled = guard;
                    break;
                case "Debug Tools":
                    // Reloads scripts on the next frame (Unity 2022.3 has no button for it).
                    if (GUILayout.Button(RefreshScripts.DisplayName, GUILayout.Height(28f))) RefreshScripts.Run();
                    if (GUILayout.Button("Find Null references", GUILayout.Height(28f))) EditorApplication.delayCall += FindNullReferencesWindow.Run;
                    if (GUILayout.Button("Menu Mockup", GUILayout.Height(28f))) EditorApplication.delayCall += MenuMockup.Open;
                    EditorGUILayout.Space(4f);
                    DrawStopToggle();
                    break;
                default:
                    Draw(Active());
                    break;
            }
        }

        // Set only when clicked: turning it on or off reloads scripts.
        private static void DrawStopToggle()
        {
            bool stop = EditorGUILayout.ToggleLeft(new GUIContent(StopAllToolScripts.DisplayName,
                "Emergency stop: every RiryTools script stops running and its menus are greyed out until this is turned off."), StopAllToolScripts.Active);
            if (stop != StopAllToolScripts.Active) StopAllToolScripts.Set(stop);
        }

        // The hosted window for the current tab, made on first use; null for tabs without one.
        private EditorWindow Active()
        {
            switch (Section)
            {
                case "AutoSync": return Host("Configuration", () => CreateInstance<AutoSyncGitConfigureWindow>(), window => ((AutoSyncGitConfigureWindow)window).Reload());
                case "Animator Tools":
                    if (animatorTab == 1) return Host("Clean Animation Keys", () => CreateInstance<CleanAnimationKeysWindow>(), null);
                    if (animatorTab == 2) return Host(ControllerCleanerWindow.Title, () => CreateInstance<ControllerCleanerWindow>(), null);
                    if (animatorTab == 3)
                        return Host("Extract Animations", () => CreateInstance<ImportPresetExtractAnimationWindow>(),
                            window => ((ImportPresetExtractAnimationWindow)window).ShowSelection());
                    return CreateFindReplace == null ? null : Host("Find and Replace", CreateFindReplace, null);
                case "Constraint Tools": return CreateConstraintTool == null ? null : Host("Constraint Tools", CreateConstraintTool, null);
                case "GUID Tools": return Host("GUID Tools", () => CreateInstance<GuidToolsWindow>(), null);
                case "Menu Tools": return CreateMenuTool == null ? null : Host("Menu Tools", CreateMenuTool, null);
                case "FTEmulator": return CreateFTEmulator == null ? null : Host("FTEmulator", CreateFTEmulator, null);
                case "Import Presets":
                    switch (importTab)
                    {
                        case 0:
                            return Host("Set Folder Import Presets", () => CreateInstance<ImportPresetFolderWindow>(),
                                window => ((ImportPresetFolderWindow)window).ShowActiveFolder());
                        case 1:
                            return Host("Save Import Preset", () => CreateInstance<ImportPresetSaveWindow>(),
                                window => ((ImportPresetSaveWindow)window).UseImporter(ImportPresetActions.SelectedImporter()));
                    }
                    return null;
                default: return null;
            }
        }

        /// <summary>
        /// A row of tabs, wrapping onto more rows when they don't fit at TabMinWidth. Every tab in
        /// a row is the same width, and as tall as its name needs (at least TabMinHeights[level]).
        /// Returns the tab chosen. owner is the window it is drawn in (its width, and repainted on hover).
        /// Also used by Constraint Tools' window.
        /// </summary>
        internal static int TabStrip(EditorWindow owner, int selected, string[] names, float inset = 0f, int level = MainTabs)
        {
            if (tabLabel == null)
            {
                tabLabel = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 12, padding = new RectOffset(4, 4, 3, 3)
                };
                activeTabLabel = new GUIStyle(tabLabel) { fontStyle = FontStyle.Bold };
                activeTabLabel.normal.textColor = ActiveTabText;
            }
            // inset: room kept free on the right (the tab scroll view's scrollbar, for the sub-tabs).
            float available = owner.position.width - 8f - inset;
            // The main tabs always stay on one row across the top; the other levels wrap.
            int perRow = level == MainTabs ? names.Length : Mathf.Clamp(Mathf.FloorToInt(available / TabMinWidth), 1, names.Length);
            int rows = (names.Length + perRow - 1) / perRow;
            float width = available / perRow;
            // Measured at the narrowest a cell gets (TabMinWidth), not the current width: measured at the current
            // width, the strip shrank to one line in the band of widths where every name happened to fit, then
            // jumped back when the cells narrowed again. Now it only changes when the number of rows does.
            float measure = Mathf.Min(width, TabMinWidth);
            level = Mathf.Clamp(level, MainTabs, ToolTabs);
            float height = TabMinHeights[level];
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
                    Color color = active ? ActiveTabs[level] : (dark ? DarkTab : LightTab);
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
            if (StopAllToolScripts.Active) return;
            EditorWindow window = ActiveIfMade();
            if (window != null) Call(window, "OnInspectorUpdate");
            Repaint();
        }

        private void OnSelectionChange()
        {
            if (StopAllToolScripts.Active) return;
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
            if (StopAllToolScripts.Active) return;
            EditorWindow window = ActiveIfMade();
            if (window != null) Call(window, "OnProjectChange");
        }

        private void OnHierarchyChange()
        {
            if (StopAllToolScripts.Active) return;
            EditorWindow window = ActiveIfMade();
            if (window != null) Call(window, "OnHierarchyChange");
        }

        // The current tab's hosted window if it was made, without making it.
        private EditorWindow ActiveIfMade()
        {
            string name = Section;
            string key = name == "AutoSync" ? "Configuration"
                : name == "Import Presets" ? (importTab < 2 ? ImportTabs[importTab] : null)
                : name == "Animator Tools" ? AnimatorTabs[Mathf.Clamp(animatorTab, 0, AnimatorTabs.Length - 1)]
                : name == "General" || name == "Blendshape Sync" || name == "Debug Tools" ? null : name;
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
//   Tabs                     Tools, Menu Tools, Animator Tools, Debug Tools, FTEmulator (not Menu Mockup or Documentation).
//                            Current is the selected tab's name; Section is the General Tools tab's sub-tab or the current tab;
//                            tabs are handled by name.
//   Menu Tools tab            CreateMenuTool (set by Editor/VRChat/UI/RiryToolsVRChatMenus.cs; null without the VRChat SDK) makes the
//                            hosted MenuToolWindow (MenuToolWindow.CreateHosted). It shows the Parameter budget, drawn
//                            outside the tab scroll view (the budget scrolls itself); its Menu manager tab opens the Menu
//                            Tool window.
//   IRiryHostedTool          A hosted tool told the window it is drawn in (Surface), set on every Draw, and NotDrawn() in a GUI
//                            pass that drew another tab (drawn: the one drawn), so Menu Tools' radial leaves at once.
//   General Tools tab / ToolsTabs Sub-tabs General, GUID Tools, Blendshape Sync, Import Presets, Constraint Tools, AutoSync
//                            (toolsTab, [SerializeField]).
//   General / PopupTools     A button per tool that needs more room (Avatar Migration Assistant, IconCap); each opens
//                            the tool in its own floating window (OpenPopup), not inside this one; then Material Cleaner, Find
//                            Null references, Refresh Scripts (RefreshScripts.Run), the Stop Auto Game toggle
//                            (StopAutoGame.Enabled, set only when clicked) and the Script Load Guard toggle
//                            (ScriptLoadGuard.Enabled, the same way).
//   FTEmulator tab           CreateFTEmulator (set by Editor/VRChat/UI/RiryToolsVRChatMenus.cs; null without the VRChat SDK,
//                            and the sub-tab says so) makes the hosted FTEmulatorWindow (FTEmulatorWindow.CreateHosted,
//                            com.riry.ftemulator/Editor/UI/FTEmulator/FTEmulatorWindow.cs), hosted under the key "FTEmulator".
//   ImportTabs               Import Presets' sub-tabs: Set Folder Import Presets, Save Import Preset,
//                            Extract Animation, Open Import Preset Library (a button).
//   Constraint Tools tab      CreateConstraintTool (set by Editor/VRChat/UI/RiryToolsVRChatMenus.cs; null without the VRChat SDK,
//                            and the tab says so) makes the hosted ConstraintToolWindow, which draws its own Train, Bezier and
//                            Insertable prop sub-tabs (Editor/VRChat/UI/ConstraintTools/ConstraintToolWindow.cs).
//   Hosted / hosted          Each tab's tool window, made on first use and never shown; drawn by calling its OnGUI.
//   Active() / Host(...)     The current tab's tool, made and set up like its Open would.
//   Call(window, method)     Calls the tool's own OnGUI, OnInspectorUpdate, OnSelectionChange, OnProjectChange or
//                            OnHierarchyChange, so it behaves as when shown.
//   OnGUI()                  The tabs, the current tool in a scroll view (tabScroll, [SerializeField], reset on a tab
//                            change), and the Documentation link at the bottom right.
//   Tab constants            TabMinWidth, MainTabs / SubTabs / ToolTabs (levels), TabMinHeights, ActiveTabs, ActiveTabText,
//                            DarkTab, LightTab, TabSeparator,
//                            TabBorder, HoverTint: internal, so Menu Mockup's Tabs block draws the same strip.
//   TabStrip(owner, selected, names, inset)  Static: the tool tabs, Constraint Tools', Import Presets', Animator Tools' and
//                            General Tools' sub-tabs, and FTEmulator Debug's tabs: flat cells of equal width (at least TabMinWidth,
//                            110), wrapping onto more rows, each as tall as its longest name needs at TabMinWidth (at least
//                            TabMinHeights[level]: 48, 36 or 24), so the height doesn't change with the window's width; the active tab in ActiveTabs[level] with dark bold text, a hover tint
//                            (HoverTint), like YGDR Animator Editor's tab strip. Every tab has a TabBorder (2 px) outline
//                            in TabSeparator, on edges rounded to whole pixels. The rows come from the window width less
//                            inset; the cells then fill the rect the layout gives, so the margins are equal and the sub-tabs
//                            line up with the main tabs.
//   TopMargin / SubTabGap    2 px above the main tabs (and Constraint Tools window's); 2 px between a tab strip and the sub-tab strip under it
//                            (Constraint Tools, Import Presets).
//   AutoSync tab             Sync toggle (AutoSyncGitActions.ToggleSync) above Configuration.
//   Blendshape Sync tab      BlendshapeSyncJobList (Editor/UI/BlendshapeSync/BlendshapeSyncJobList.cs): the running jobs, and Add Job.
