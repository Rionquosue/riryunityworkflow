using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Clean Animation Keys window: the animations to clean, the whitelist,
    /// the keep rules and Clean. Logic is in Editor/CleanAnimationKeys.cs.
    /// </summary>
    internal sealed class CleanAnimationKeysWindow : EditorWindow
    {
        private const int MaxAnimations = 64;
        private const float AnimationListHeight = 150.0f;
        private const int MaxSuggestions = 8;

        [SerializeField] private List<AnimationClip> animations = new List<AnimationClip>();
        [SerializeField] private List<string> whitelist = new List<string>();
        [SerializeField] private CleanAnimationKeys.Options options = new CleanAnimationKeys.Options();
        [SerializeField] private string search = "";
        [SerializeField] private string summary;
        private Vector2 animationScroll;
        private Vector2 whitelistScroll;
        // Every shapekey and bone name keyed in the listed animations, for suggestions.
        private List<string> keyedNames;

        /// <summary>Opens the window and adds the clips that are not listed yet.</summary>
        internal static void Open(IEnumerable<AnimationClip> clips)
        {
            CleanAnimationKeysWindow window = GetWindow<CleanAnimationKeysWindow>(true, CleanAnimationKeys.DisplayName);
            window.minSize = new Vector2(480.0f, 520.0f);
            foreach (AnimationClip clip in clips) window.Add(clip);
            window.Show();
        }

        private void Add(AnimationClip clip)
        {
            if (clip == null || animations.Contains(clip) || animations.Count >= MaxAnimations) return;
            animations.Add(clip);
            keyedNames = null;
        }

        internal void AddToWhitelist(IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                if (!whitelist.Contains(name)) whitelist.Add(name);
            }
            summary = null;
            Repaint();
        }

        internal CleanAnimationKeys.Options Options
        {
            get { return options; }
        }

        internal List<string> Whitelist
        {
            get { return whitelist; }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6.0f);
            EditorGUILayout.LabelField("Utility for removing blank animation shapekeys from imported animations that " +
                "automatically key every bone and shapekey.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(6.0f);
            EditorGUILayout.LabelField("Animations", EditorStyles.boldLabel);
            DrawAnimations();

            EditorGUILayout.Space(6.0f);
            EditorGUILayout.LabelField("Whitelist", EditorStyles.boldLabel);
            DrawWhitelist();

            EditorGUILayout.Space(6.0f);
            EditorGUI.BeginChangeCheck();
            options.KeepNonZeroShapekeys = EditorGUILayout.ToggleLeft(new GUIContent("Keep non-zero shapekeys",
                "Keep a shapekey curve that has any non-zero key."), options.KeepNonZeroShapekeys);
            options.KeepShapekeyTangentMotion = EditorGUILayout.ToggleLeft(new GUIContent("Keep shapekey tangent motion",
                "Keep a shapekey whose keys are 0 but whose tangents move the value between keys."), options.KeepShapekeyTangentMotion);
            options.KeepMovingBones = EditorGUILayout.ToggleLeft(new GUIContent("Keep moving bones",
                "Keep a bone's translation, rotation or scale when its start and finish differ (3 decimal places)."), options.KeepMovingBones);
            options.KeepBoneMidKeys = EditorGUILayout.ToggleLeft(new GUIContent("Keep bone mid-keys/tangents",
                "Keep a bone's translation, rotation or scale with more than a start and finish key, or whose tangents " +
                "move between matching endpoints."), options.KeepBoneMidKeys);
            if (EditorGUI.EndChangeCheck()) summary = null;

            GUILayout.FlexibleSpace();
            if (summary != null) EditorGUILayout.HelpBox(summary, MessageType.Info);
            using (new EditorGUI.DisabledScope(animations.Count == 0))
            {
                if (GUILayout.Button("Clean", GUILayout.Height(28.0f))) Clean();
            }
            EditorGUILayout.Space(6.0f);
        }

        // One row per animation plus one empty row to add the next, up to 64; the list scrolls.
        private void DrawAnimations()
        {
            animationScroll = EditorGUILayout.BeginScrollView(animationScroll, EditorStyles.helpBox,
                GUILayout.Height(AnimationListHeight));
            int rows = Math.Min(animations.Count + 1, MaxAnimations);
            for (int i = 0; i < rows; i++)
            {
                AnimationClip current = i < animations.Count ? animations[i] : null;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var chosen = (AnimationClip)EditorGUILayout.ObjectField(current, typeof(AnimationClip), false);
                    if (chosen != current)
                    {
                        if (chosen == null) animations.RemoveAt(i);
                        else if (!animations.Contains(chosen))
                        {
                            if (current == null) animations.Add(chosen);
                            else animations[i] = chosen;
                        }
                        keyedNames = null;
                        summary = null;
                        GUIUtility.ExitGUI();
                    }
                    using (new EditorGUI.DisabledScope(current == null))
                    {
                        if (GUILayout.Button("Whitelist", GUILayout.Width(80.0f)) && current != null)
                            CleanAnimationKeysWhitelistWindow.Open(this, current);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // A search field with fuzzy suggestions from the listed animations, then the entries.
        private void DrawWhitelist()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField(search);
                using (new EditorGUI.DisabledScope(search.Trim().Length == 0))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(80.0f)))
                    {
                        AddToWhitelist(new[] { search.Trim() });
                        search = "";
                        GUI.FocusControl(null);
                    }
                }
            }

            if (search.Trim().Length > 0)
            {
                if (keyedNames == null) keyedNames = KeyedNames();
                foreach (string suggestion in Suggestions(search.Trim()))
                {
                    if (GUILayout.Button(suggestion, EditorStyles.miniButton))
                    {
                        AddToWhitelist(new[] { suggestion });
                        search = "";
                        GUI.FocusControl(null);
                    }
                }
            }

            whitelistScroll = EditorGUILayout.BeginScrollView(whitelistScroll, GUILayout.MaxHeight(110.0f));
            for (int i = 0; i < whitelist.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(whitelist[i]);
                    if (GUILayout.Button("x", GUILayout.Width(22.0f)))
                    {
                        whitelist.RemoveAt(i);
                        summary = null;
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private List<string> KeyedNames()
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AnimationClip clip in animations)
            {
                if (clip == null) continue;
                foreach (CleanAnimationKeys.Group group in CleanAnimationKeys.Groups(clip)) names.Add(group.Name);
            }
            return new List<string>(names);
        }

        // Names containing the typed letters in order, best first: whole-word start, then substring, then scattered.
        private IEnumerable<string> Suggestions(string typed)
        {
            var scored = new List<KeyValuePair<int, string>>();
            foreach (string name in keyedNames)
            {
                if (whitelist.Contains(name)) continue;
                int score = FuzzyScore(name, typed);
                if (score > 0) scored.Add(new KeyValuePair<int, string>(score, name));
            }
            scored.Sort((left, right) => left.Key != right.Key
                ? right.Key.CompareTo(left.Key)
                : string.Compare(left.Value, right.Value, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < scored.Count && i < MaxSuggestions; i++) yield return scored[i].Value;
        }

        internal static int FuzzyScore(string name, string typed)
        {
            int index = name.IndexOf(typed, StringComparison.OrdinalIgnoreCase);
            if (index == 0) return 3;
            if (index > 0) return 2;
            int position = 0;
            foreach (char letter in typed)
            {
                position = name.IndexOf(letter.ToString(), position, StringComparison.OrdinalIgnoreCase);
                if (position < 0) return 0;
                position++;
            }
            return 1;
        }

        private void Clean()
        {
            try
            {
                summary = CleanAnimationKeys.Clean(animations, options, whitelist);
            }
            catch (InvalidOperationException exception)
            {
                summary = null;
                EditorUtility.DisplayDialog(CleanAnimationKeys.DisplayName, exception.Message, "OK");
            }
        }
    }

    /// <summary>
    /// One animation's keys that Clean would remove, each with a checkbox; APPLY
    /// adds the checked names to the whitelist.
    /// </summary>
    internal sealed class CleanAnimationKeysWhitelistWindow : EditorWindow
    {
        private CleanAnimationKeysWindow owner;
        private readonly List<CleanAnimationKeys.Group> groups = new List<CleanAnimationKeys.Group>();
        private readonly HashSet<string> chosen = new HashSet<string>();
        private Vector2 scroll;

        internal static void Open(CleanAnimationKeysWindow owner, AnimationClip clip)
        {
            CleanAnimationKeysWhitelistWindow window = GetWindow<CleanAnimationKeysWhitelistWindow>(true, "Whitelist: " + clip.name);
            window.minSize = new Vector2(360.0f, 360.0f);
            window.owner = owner;
            window.groups.Clear();
            window.chosen.Clear();
            var seen = new HashSet<string>();
            foreach (CleanAnimationKeys.Group group in CleanAnimationKeys.Removable(clip, owner.Options, owner.Whitelist))
            {
                if (seen.Add(group.Label)) window.groups.Add(group);
            }
            window.groups.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
            window.Show();
        }

        private void OnGUI()
        {
            if (owner == null)
            {
                Close();
                return;
            }
            EditorGUILayout.HelpBox("Only keys that will be removed shown", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (CleanAnimationKeys.Group group in groups)
            {
                bool on = chosen.Contains(group.Name);
                if (EditorGUILayout.ToggleLeft(group.Label, on) != on)
                {
                    if (on) chosen.Remove(group.Name);
                    else chosen.Add(group.Name);
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("APPLY", GUILayout.Height(28.0f)))
            {
                owner.AddToWhitelist(chosen);
                Close();
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
    internal static class CleanAnimationKeysButton
    {
        private const string ButtonElementName =
            "rirytools-clean-animation-keys";
        private const double ScanIntervalSeconds = 0.5;

        private static readonly object ButtonOwner = new object();

        private static double nextScanTime;

        static CleanAnimationKeysButton()
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
                CleanAnimationKeysWindow.Open(new[] { animationWindow.animationClip });
            });

            button.name = ButtonElementName;
            button.userData = ButtonOwner;
            button.text = "Clean Keys";
            button.tooltip =
                "Open Clean Animation Keys with this Animation window's active AnimationClip.";

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
// CleanAnimationKeysWindow (this file)
//   animations               List<AnimationClip>  Animations to clean (up to MaxAnimations, 64).
//   whitelist                List<string>    Shapekey and bone names never removed; kept only while the window is open.
//   options                  CleanAnimationKeys.Options  The four keep-rule checkboxes.
//   search                   string          Whitelist search text.
//   summary                  string          Result of the last Clean.
//   keyedNames               List<string>    Names keyed in the listed animations, for suggestions.
//   AnimationListHeight      const float     Height of the scrolling Animations list (150).
//   MaxSuggestions           const int       Suggestions shown (8).
//   Open(clips)              Opens the window and adds clips (Project selection or Animation window clip).
//   DrawAnimations()         One row per animation with Whitelist, plus an empty row while under 64.
//   DrawWhitelist()          Search field with Add, fuzzy suggestions, entries with x.
//   Suggestions(typed) / FuzzyScore(name, typed)  Ranks names: starts with, contains, then letters in order.
//   AddToWhitelist(names)    Adds names (from Add, a suggestion, or APPLY).
//   Clean()                  CleanAnimationKeys.Clean, then shows the summary.
//
// CleanAnimationKeysWhitelistWindow (this file): the Whitelist popup
//   Open(owner, clip)        Lists the groups Clean would remove from the clip.
//   OnGUI()                  "Only keys that will be removed shown", a checkbox per key, APPLY.
//
// CleanAnimationKeysButton (this file): the "Clean Keys" button
//   ButtonElementName        const string    UI Toolkit name, used to find an installed button.
//   ScanIntervalSeconds      const double    How often the focused window is checked (0.5 s).
//   WatchForAnimationWindows()  Installs the button on a focused Animation window.
//   InstallButton(window)    Adds the button, which opens the window with that window's clip.
//
// CleanAnimationKeys (Editor/CleanAnimationKeys.cs): the logic
//   DisplayName              "Clean Animation Keys": window title and Undo name.
//   Options                  The keep rules; curves that cannot be read or hold NaN/Infinity are always kept.
//   Group                    One shapekey, or one bone's translation, rotation or scale; Name is what the whitelist matches.
//   Groups(clip) / Removable(clip, options, whitelist)  Every group; the ones Clean would remove.
//   Clean(clips, options, whitelist)  Removes them from every editable clip as one Undo step; returns the summary.
//   TryRemoveBindings(...)   Removes curves, rolling back on failure.
//   RepaintAnimationWindows()  Refreshes open Animation windows after a change.
