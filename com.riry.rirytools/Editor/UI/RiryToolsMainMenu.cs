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

        private static readonly string[] Tabs =
        {
            "AutoSync", "Blendshape Sync", "Clean Animation Keys", "Constraint Train", "GUID Tools", "IconCap", "Import Presets", "Stop Auto Game"
        };
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

        // About three times a standard tab row; Import Presets' sub-tabs about twice.
        private static readonly float TabHeight = 3f * (EditorGUIUtility.singleLineHeight + 2f);
        private static readonly float ImportTabHeight = 2f * (EditorGUIUtility.singleLineHeight + 2f);
        private GUIStyle tabStyle;

        [SerializeField] private int tab;
        [SerializeField] private int importTab;
        [SerializeField] private List<Hosted> hosted = new List<Hosted>();

        internal static void Open()
        {
            RiryToolsMainMenu window = GetWindow<RiryToolsMainMenu>("RiryTools");
            window.minSize = new Vector2(480f, 380f);
            window.Show();
        }

        private void OnGUI()
        {
            // The tool tabs are three lines tall, Import Presets' sub-tabs two, and their names wrap
            // when the window is narrow.
            // A name the skin has no "left"/"mid"/"right" styles for, so Toolbar draws every tab
            // with this style instead of the skin's own, which do not wrap.
            if (tabStyle == null)
                tabStyle = new GUIStyle(GUI.skin.button) { name = "RiryToolsTab", wordWrap = true, fixedHeight = 0f };
            tab = GUILayout.Toolbar(tab, Tabs, tabStyle, GUILayout.Height(TabHeight));
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                DrawTab();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (EditorGUILayout.LinkButton("Documentation")) Application.OpenURL(RiryMenus.DocumentationLink);
            }
        }

        private void DrawTab()
        {
            switch (tab)
            {
                case 0:
                    bool sync = EditorGUILayout.ToggleLeft("Sync", AutoSync.Enabled);
                    if (sync != AutoSync.Enabled) GitActions.ToggleSync();
                    if (!GitState.GitInstalled) EditorGUILayout.HelpBox(new GitNotInstalledException(null).Message, MessageType.Info);
                    else Draw(Active());
                    break;
                case 1:
                    // The running jobs for review; a job is added or edited in its popup.
                    BlendshapeSyncJobList.Draw();
                    break;
                case 3:
                    if (CreateConstraintTrain == null)
                        EditorGUILayout.HelpBox(new VrcSdkNotFoundException("Constraint Train").Message, MessageType.Info);
                    else Draw(Active());
                    break;
                case 6:
                    importTab = GUILayout.Toolbar(importTab, ImportTabs, tabStyle, GUILayout.Height(ImportTabHeight));
                    EditorGUILayout.Space(4f);
                    if (importTab == 3)
                    {
                        if (GUILayout.Button("Open Import Preset Library", GUILayout.Height(28f))) ImportPresetActions.OpenLibrary();
                    }
                    else Draw(Active());
                    break;
                case 7:
                    StopAutoGame.Enabled = EditorGUILayout.ToggleLeft(new GUIContent("Stop Auto Game",
                        "Keep focus off the Game view when Play Mode starts."), StopAutoGame.Enabled);
                    break;
                default:
                    Draw(Active());
                    break;
            }
        }

        // The hosted window for the current tab, made on first use; null for tabs without one.
        private EditorWindow Active()
        {
            switch (tab)
            {
                case 0: return Host("Configuration", () => CreateInstance<GitConfigureWindow>(), window => ((GitConfigureWindow)window).Reload());
                case 2: return Host("Clean Animation Keys", () => CreateInstance<CleanAnimationKeysWindow>(), null);
                case 3: return CreateConstraintTrain == null ? null : Host("Constraint Train", CreateConstraintTrain, null);
                case 4: return Host("GUID Tools", () => CreateInstance<GuidToolsWindow>(), null);
                case 5: return Host("IconCap", () => CreateInstance<IconCapWindow>(), null);
                case 6:
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
            string key = tab == 0 ? "Configuration" : tab == 6 ? (importTab < 3 ? ImportTabs[importTab] : null) : tab == 1 || tab == 7 ? null : Tabs[tab];
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
//   Tabs                     AutoSync, Blendshape Sync, Clean Animation Keys, Constraint Train, GUID Tools, IconCap,
//                            Import Presets, Stop Auto Game (menu order; not Menu Mockup or Documentation).
//   ImportTabs               Import Presets' sub-tabs: Set Folder Import Presets, Save Import Preset,
//                            Extract Animation, Open Import Preset Library (a button).
//   CreateConstraintTrain    Set by Editor/VRChat/RiryVRChatMenus.cs; null without the VRChat SDK (the tab says so).
//   Hosted / hosted          Each tab's tool window, made on first use and never shown; drawn by calling its OnGUI.
//   Active() / Host(...)     The current tab's tool, made and set up like its Open would.
//   Call(window, method)     Calls the tool's own OnGUI, OnInspectorUpdate, OnSelectionChange, OnProjectChange or
//                            OnHierarchyChange, so it behaves as when shown.
//   OnGUI()                  The tabs, the current tool, and the Documentation link at the bottom right.
//   TabHeight / ImportTabHeight / tabStyle
//                            Tool tabs about three rows tall, Import Presets' sub-tabs two, names wrapping;
//                            tabStyle is named RiryToolsTab so Toolbar does not swap in the skin's non-wrapping styles.
//   AutoSync tab             Sync toggle (GitActions.ToggleSync) above Configuration; Stop Auto Game tab: its toggle.
//   Blendshape Sync tab      BlendshapeSyncJobList (Editor/UI/BlendshapeSyncJobWindow.cs): the running jobs, and Add Job.
