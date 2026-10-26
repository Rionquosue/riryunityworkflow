#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riry.UnityWorkflow.Editor
{
    internal sealed class RemoveBlankShapeKeysResultWindow : EditorWindow
    {
        private AnimationClip clip;
        private string sourceLabel;
        private List<RemoveBlankShapeKeys.WhitelistItem> whitelist;
        private Vector2 scrollPosition;
        private bool removeKeysAnyways;
        private bool overrideRemovalCompleted;
        private int floatCurveCount;
        private int floatKeyCount;
        private int objectCurveCount;
        private int objectKeyCount;
        private int animationEventCount;
        private int otherFloatCurveCount;
        private int removedGroupCount;
        private int removedBindingCount;

        internal static void ShowResult(
            AnimationClip clip,
            string sourceLabel,
            RemoveBlankShapeKeys.AnalysisResult result,
            int removedBindingCount)
        {
            RemoveBlankShapeKeysResultWindow window =
                ScriptableObject.CreateInstance<RemoveBlankShapeKeysResultWindow>();

            window.clip = clip;
            window.sourceLabel = sourceLabel;
            window.whitelist =
                new List<RemoveBlankShapeKeys.WhitelistItem>(result.Whitelist);
            window.floatCurveCount = result.FloatCurveCount;
            window.floatKeyCount = result.FloatKeyCount;
            window.objectCurveCount = result.ObjectCurveCount;
            window.objectKeyCount = result.ObjectKeyCount;
            window.animationEventCount = result.AnimationEventCount;
            window.otherFloatCurveCount = result.OtherFloatCurveCount;
            window.removedGroupCount = result.RemovedGroupCount;
            window.removedBindingCount = removedBindingCount;

            window.titleContent = new GUIContent("Animation cleanup");
            window.minSize = new Vector2(560.0f, 320.0f);
            window.maxSize = new Vector2(760.0f, 680.0f);

            float windowHeight = Mathf.Clamp(
                330.0f + window.whitelist.Count * 24.0f,
                360.0f,
                620.0f);
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();

            window.position = new Rect(
                mainWindow.center.x - 320.0f,
                mainWindow.center.y - windowHeight * 0.5f,
                640.0f,
                windowHeight);

            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            if (whitelist == null)
            {
                Close();
                return;
            }

            EditorGUILayout.Space(8.0f);
            EditorGUILayout.LabelField(
                RemoveBlankShapeKeys.DisplayName,
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Clip",
                clip != null ? clip.name : "<missing>");
            EditorGUILayout.LabelField("Source", sourceLabel ?? "Unknown");

            EditorGUILayout.Space(4.0f);
            EditorGUILayout.HelpBox(BuildSummary(), MessageType.Info);

            EditorGUILayout.Space(4.0f);
            EditorGUILayout.LabelField(
                "Whitelist (preserved)",
                EditorStyles.boldLabel);

            if (whitelist.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No active blendshape or bone transform groups were " +
                    "whitelisted.",
                    MessageType.None);
            }
            else
            {
                scrollPosition = EditorGUILayout.BeginScrollView(
                    scrollPosition,
                    GUILayout.MinHeight(120.0f));

                for (int i = 0; i < whitelist.Count; i++)
                {
                    RemoveBlankShapeKeys.WhitelistItem item = whitelist[i];

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(item.Label, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        item.Reason,
                        EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4.0f);
                removeKeysAnyways = EditorGUILayout.ToggleLeft(
                    "Remove keys anyways",
                    removeKeysAnyways);

                if (removeKeysAnyways)
                {
                    EditorGUILayout.HelpBox(
                        "This removes every curve group shown in the whitelist. " +
                        "The operation remains available through Unity Undo.",
                        MessageType.Warning);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(
                       whitelist.Count == 0 || !removeKeysAnyways))
            {
                if (GUILayout.Button("Remove whitelisted keys", GUILayout.Height(26.0f)))
                {
                    RemoveWhitelistedKeys();
                }
            }

            if (GUILayout.Button("Close", GUILayout.Width(100.0f), GUILayout.Height(26.0f)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8.0f);
        }

        private string BuildSummary()
        {
            string summary =
                "Parsed " + floatCurveCount + " float curves (" +
                floatKeyCount + " keys), " + objectCurveCount +
                " object-reference curves (" + objectKeyCount +
                " keys), and " + animationEventCount + " animation events.\n" +
                "Removed " + removedGroupCount + " inactive groups (" +
                removedBindingCount + " curves). Preserved " +
                whitelist.Count + " whitelisted groups.";

            if (otherFloatCurveCount > 0)
            {
                summary += " " + otherFloatCurveCount +
                    " other float curves were parsed and left untouched.";
            }

            if (overrideRemovalCompleted)
            {
                summary += " Whitelisted curves were also removed.";
            }

            return summary;
        }

        private void RemoveWhitelistedKeys()
        {
            if (clip == null || whitelist == null || whitelist.Count == 0)
            {
                return;
            }

            List<EditorCurveBinding> bindings =
                new List<EditorCurveBinding>();

            for (int itemIndex = 0;
                 itemIndex < whitelist.Count;
                 itemIndex++)
            {
                EditorCurveBinding[] itemBindings =
                    whitelist[itemIndex].Bindings;

                for (int bindingIndex = 0;
                     bindingIndex < itemBindings.Length;
                     bindingIndex++)
                {
                    bindings.Add(itemBindings[bindingIndex]);
                }
            }

            string error;
            if (!RemoveBlankShapeKeys.TryRemoveBindings(
                    clip,
                    bindings,
                    "Remove Whitelisted Animation Keys",
                    out error))
            {
                EditorUtility.DisplayDialog(
                    RemoveBlankShapeKeys.DisplayName,
                    error,
                    "OK");
                return;
            }

            removedBindingCount += bindings.Count;
            removedGroupCount += whitelist.Count;
            whitelist.Clear();
            bindings.Clear();
            removeKeysAnyways = false;
            overrideRemovalCompleted = true;

            RemoveBlankShapeKeys.RepaintAnimationWindows();
            EditorApplication.RepaintProjectWindow();
            Repaint();
        }

        private void OnDisable()
        {
            clip = null;
            sourceLabel = null;

            if (whitelist != null)
            {
                whitelist.Clear();
                whitelist = null;
            }
        }
    }

    /// <summary>
    /// Unity 2022.3's AnimationWindow does not opt in to the public Overlay API.
    /// Add one lightweight UI Toolkit button over its existing IMGUI toolbar.
    /// Only the focused editor window is inspected, twice per second, so this
    /// also supports Animation windows opened after the script is compiled.
    /// </summary>
    [InitializeOnLoad]
    internal static class RemoveBlankShapeKeysAnimationWindowButton
    {
        private const string ButtonElementName =
            "animation-cleanup-tools-remove-blank-shapekeys";
        private const double ScanIntervalSeconds = 0.5;

        private static readonly object ButtonOwner = new object();

        private static double nextScanTime;

        static RemoveBlankShapeKeysAnimationWindowButton()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            EditorApplication.update += WatchForAnimationWindows;
        }

        private static void WatchForAnimationWindows()
        {
            if (EditorApplication.timeSinceStartup < nextScanTime)
            {
                return;
            }

            nextScanTime = EditorApplication.timeSinceStartup +
                           ScanIntervalSeconds;

            AnimationWindow animationWindow =
                EditorWindow.focusedWindow as AnimationWindow;

            if (animationWindow != null)
            {
                InstallButton(animationWindow);
            }
        }

        private static void InstallButton(AnimationWindow animationWindow)
        {
            if (animationWindow == null ||
                animationWindow.rootVisualElement == null)
            {
                return;
            }

            VisualElement root = animationWindow.rootVisualElement;
            Button existing = root.Q<Button>(ButtonElementName);

            if (existing != null)
            {
                if (object.ReferenceEquals(existing.userData, ButtonOwner))
                {
                    return;
                }

                existing.RemoveFromHierarchy();
            }

            Button button = new Button(delegate
            {
                RemoveBlankShapeKeys.RunFromAnimationWindow(animationWindow);
            });

            button.name = ButtonElementName;
            button.userData = ButtonOwner;
            button.text = "Clean keys";
            button.tooltip =
                "Remove inactive blendshape and static bone transform curves " +
                "from this Animation window's active AnimationClip.";

            // The AnimationWindow toolbar is IMGUI in Unity 2022.3, so the
            // public rootVisualElement is the accessible attachment point.
            // Absolute positioning keeps this button out of the window layout.
            button.style.position = Position.Absolute;
            button.style.top = 2.0f;
            button.style.right = 6.0f;
            button.style.width = 78.0f;
            button.style.height = 20.0f;

            root.Add(button);
        }

        private static void BeforeAssemblyReload()
        {
            EditorApplication.update -= WatchForAnimationWindows;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        }
    }
}

// ============================================================================
// GLOSSARY: Clean Animation Keys UI
// Menu entries (Tools dropdown and Project right-click) are in RiryMenus.cs.
// ============================================================================
//
// RemoveBlankShapeKeysResultWindow (this file): the modal result/review window
//   clip                     AnimationClip   The clip that was cleaned.
//   sourceLabel              string          Where the clip came from (Animation window or Project selection).
//   whitelist                List<WhitelistItem>  Groups kept for review (label, reason, bindings).
//   removeKeysAnyways        bool            "Remove keys anyways" toggle for the kept groups.
//   overrideRemovalCompleted bool            The kept groups were removed after confirmation.
//   floatCurveCount / floatKeyCount / objectCurveCount / objectKeyCount /
//   animationEventCount / otherFloatCurveCount / removedGroupCount /
//   removedBindingCount      int             Numbers shown in the summary.
//   scrollPosition           Vector2         Scroll position of the review list.
//   ShowResult(clip, sourceLabel, result, removedBindingCount)  Opens the window.
//   OnGUI()                  Summary, review list and buttons.
//
// RemoveBlankShapeKeysAnimationWindowButton (this file): the "Clean keys" button
//   ButtonElementName        const string    UI Toolkit name, used to find an installed button.
//   ScanIntervalSeconds      const double    How often the focused window is checked (0.5 s).
//   WatchForAnimationWindows()  Installs the button on a focused Animation window.
//   InstallButton(window)    Adds the button to the window's rootVisualElement (top right).
//
// RemoveBlankShapeKeys (Editor/RemoveBlankShapeKeys.cs): the logic
//   DisplayName              "Clean Animation Keys", the dialog title.
//   RunFromMainMenu() / ValidateMainMenuCommand()        Tools menu: Animation window clip or selection.
//   RunFromProjectWindow() / ValidateProjectWindowCommand()  Project right-click: selected clip.
//   RunFromAnimationWindow(window)  The "Clean keys" button.
//   Run(clip, sourceLabel)   Analyses, removes, then shows the result window.
//   Analyze(clip)            AnalysisResult: removable bindings and the whitelist.
//   TryRemoveBindings(...)   Removes the kept groups when "Remove keys anyways" is confirmed.
//   RepaintAnimationWindows()  Refreshes open Animation windows after a change.
//   WhitelistItem / AnalysisResult  Results passed to the window.
#endif
