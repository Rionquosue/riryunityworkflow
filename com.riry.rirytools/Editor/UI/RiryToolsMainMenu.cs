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
    /// Opened from Tools > RiryTools > Main Menu (Editor/UI/RiryMenus.cs).
    /// </summary>
    internal sealed class RiryToolsMainMenu : EditorWindow
    {
        // Set by Editor/VRChat/RiryVRChatMenus.cs when the VRChat SDK is in the project.
        internal static Func<EditorWindow> CreateConstraintTrain;
        internal static Func<EditorWindow> CreateConstraintBezier;
        internal static Func<EditorWindow> CreateConstraintInsertable;

        private static readonly string[] Tabs =
        {
            "AutoSync", "Blendshape Sync", "Clean Animation Keys", "Constraint Tool", "GUID Tools", "Import Presets", "Stop Auto Game", "Tools"
        };
        // Tools that need more room than a tab: the Tools tab has a button for each, which opens it in its own window.
        private static readonly string[] PopupTools = { "Avatar Migration Assistant", "IconCap" };
        // Constraint Tool's sub-tabs, as in its own window (Editor/VRChat/ConstraintToolWindow.cs).
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
        private const float TabMinWidth = 110f;
        private const float TabMinHeight = 24f;
        private static readonly Color DarkTab = new Color32(56, 56, 56, 255);
        private static readonly Color DarkActiveTab = new Color32(90, 90, 90, 255);
        private static readonly Color LightTab = new Color32(200, 200, 200, 255);
        private static readonly Color LightActiveTab = new Color32(160, 160, 160, 255);
        private static readonly Color TabSeparator = new Color(0f, 0f, 0f, 0.35f);
        private const float HoverTint = 0.1f;
        private static GUIStyle tabLabel;
        private static GUIStyle activeTabLabel;

        [SerializeField] private int tab;
        [SerializeField] private int constraintTab;
        [SerializeField] private int importTab;
        // The current tab's scroll position; back to the top when the tab changes.
        [SerializeField] private Vector2 tabScroll;
        [SerializeField] private List<Hosted> hosted = new List<Hosted>();

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
            int chosen = TabStrip(this, tab, Tabs);
            if (chosen != tab) tabScroll = Vector2.zero;
            tab = chosen;
            EditorGUILayout.Space(4f);
            // A scroll view, so a tool taller or wider than the window stays usable.
            using (var scroll = new EditorGUILayout.ScrollViewScope(tabScroll, GUILayout.ExpandHeight(true)))
            {
                tabScroll = scroll.scrollPosition;
                DrawTab();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (EditorGUILayout.LinkButton("Documentation")) Application.OpenURL(RiryMenus.DocumentationLink);
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
                    if (sync != AutoSync.Enabled) { GitActions.ToggleSync(); RiryGui.ExitAfterModal(); }
                    if (!GitState.GitInstalled) EditorGUILayout.HelpBox(new GitNotInstalledException(null).Message, MessageType.Info);
                    else Draw(Active());
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
                    constraintTab = TabStrip(this, constraintTab, ConstraintTabs, GUI.skin.verticalScrollbar.fixedWidth + 4f);
                    EditorGUILayout.Space(4f);
                    Draw(Active());
                    break;
                case "Import Presets":
                    importTab = TabStrip(this, importTab, ImportTabs, GUI.skin.verticalScrollbar.fixedWidth + 4f);
                    EditorGUILayout.Space(4f);
                    if (importTab == 3)
                    {
                        if (GUILayout.Button("Open Import Preset Library", GUILayout.Height(28f))) ImportPresetActions.OpenLibrary();
                    }
                    else Draw(Active());
                    break;
                case "Stop Auto Game":
                    // Set only when clicked: the setter logs, so assigning it every draw spammed the Console.
                    bool stop = EditorGUILayout.ToggleLeft(new GUIContent("Stop Auto Game",
                        "Keep focus off the Game view when Play Mode starts."), StopAutoGame.Enabled);
                    if (stop != StopAutoGame.Enabled) StopAutoGame.Enabled = stop;
                    break;
                case "Tools":
                    // Each opens in its own window, not in this one, which may be docked small.
                    foreach (string tool in PopupTools)
                    {
                        if (!GUILayout.Button(tool, GUILayout.Height(28f))) continue;
                        if (tool == "IconCap") EditorApplication.delayCall += IconCapWindow.OpenPopup;
                        else EditorApplication.delayCall += AvatarMigrationAssistant.OpenPopup;
                    }
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
                case "AutoSync": return Host("Configuration", () => CreateInstance<GitConfigureWindow>(), window => ((GitConfigureWindow)window).Reload());
                case "Clean Animation Keys": return Host("Clean Animation Keys", () => CreateInstance<CleanAnimationKeysWindow>(), null);
                case "Constraint Tool":
                    if (CreateConstraintTrain == null) return null;
                    if (constraintTab == 2) return Host("Insertable prop", CreateConstraintInsertable, null);
                    return constraintTab == 1 ? Host("Bezier", CreateConstraintBezier, null) : Host("Train", CreateConstraintTrain, null);
                case "GUID Tools": return Host("GUID Tools", () => CreateInstance<GuidToolsWindow>(), null);
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
                            return Host("Extract Animation", () => CreateInstance<ExtractAnimationWindow>(),
                                window => ((ExtractAnimationWindow)window).ShowSelection());
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
            float height = TabMinHeight;
            foreach (string name in names) height = Mathf.Max(height, activeTabLabel.CalcHeight(new GUIContent(name), width));
            Rect area = GUILayoutUtility.GetRect(available, rows * height, GUILayout.ExpandWidth(true));
            bool dark = EditorGUIUtility.isProSkin;
            Event current = Event.current;
            for (int i = 0; i < names.Length; i++)
            {
                var cell = new Rect(area.x + (i % perRow) * width, area.y + (i / perRow) * height, width, height);
                bool active = i == selected;
                if (current.type == EventType.Repaint)
                {
                    Color color = active ? (dark ? DarkActiveTab : LightActiveTab) : (dark ? DarkTab : LightTab);
                    if (!active && cell.Contains(current.mousePosition))
                        color = dark ? color + new Color(HoverTint, HoverTint, HoverTint, 0f) : color - new Color(HoverTint / 2f, HoverTint / 2f, HoverTint / 2f, 0f);
                    EditorGUI.DrawRect(cell, color);
                    EditorGUI.DrawRect(new Rect(cell.xMax - 1f, cell.y, 1f, cell.height), TabSeparator);
                    EditorGUI.DrawRect(new Rect(cell.x, cell.yMax - 1f, cell.width, 1f), TabSeparator);
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

        private static void Draw(EditorWindow window)
        {
            if (window != null) Call(window, "OnGUI");
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
                : name == "Blendshape Sync" || name == "Stop Auto Game" || name == "Tools" ? null : name;
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
// The menu entry, Tools > RiryTools > Main Menu, is in RiryMenus.cs.
// ============================================================================
//
// RiryToolsMainMenu (this file): the dockable "RiryTools" window
//   Tabs                     AutoSync, Blendshape Sync, Clean Animation Keys, Constraint Tool, GUID Tools, Import Presets,
//                            Stop Auto Game, Tools (not Menu Mockup or Documentation). Current is the selected tab's
//                            name; tabs are handled by name.
//   Tools tab / PopupTools   A button per tool that needs more room (Avatar Migration Assistant, IconCap); each opens
//                            the tool in its own floating window (OpenPopup), not inside this one.
//   ImportTabs               Import Presets' sub-tabs: Set Folder Import Presets, Save Import Preset,
//                            Extract Animation, Open Import Preset Library (a button).
//   ConstraintTabs           Constraint Tool's sub-tabs (constraintTab, [SerializeField]): Train, Bezier and Insertable prop,
//                            hosted under those names, like its own window (Editor/VRChat/ConstraintToolWindow.cs).
//   CreateConstraintTrain / CreateConstraintBezier / CreateConstraintInsertable  Set by Editor/VRChat/RiryVRChatMenus.cs; null without the VRChat SDK
//                            (the tab says so).
//   Hosted / hosted          Each tab's tool window, made on first use and never shown; drawn by calling its OnGUI.
//   Active() / Host(...)     The current tab's tool, made and set up like its Open would.
//   Call(window, method)     Calls the tool's own OnGUI, OnInspectorUpdate, OnSelectionChange, OnProjectChange or
//                            OnHierarchyChange, so it behaves as when shown.
//   OnGUI()                  The tabs, the current tool in a scroll view (tabScroll, [SerializeField], reset on a tab
//                            change), and the Documentation link at the bottom right.
//   TabStrip(owner, selected, names, inset)  Static: the tool tabs, Constraint Tool's and Import Presets' sub-tabs: flat cells of equal width (at least TabMinWidth,
//                            110), wrapping onto more rows, each as tall as its wrapped name (at least TabMinHeight,
//                            24); the active tab lighter (DarkActiveTab / LightActiveTab) and bold, a hover tint
//                            (HoverTint), like YGDR Animator Editor's tab strip.
//   AutoSync tab             Sync toggle (GitActions.ToggleSync) above Configuration; Stop Auto Game tab: its toggle.
//   Blendshape Sync tab      BlendshapeSyncJobList (Editor/UI/BlendshapeSyncJobWindow.cs): the running jobs, and Add Job.
