using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Clean Animation Keys window: the animations to clean, the whitelist,
    /// the keep rules and Clean. Logic is in Editor/AnimatorTools/CleanAnimationKeys.cs.
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
        // Set when opened by Configure in Extract Animation: the options and whitelist are that window's settings.
        [SerializeField] private ImportPresetExtractAnimationWindow extract;
        private Vector2 animationScroll;
        private const string SettingsUndo = "Clean Animation Keys settings";
        // What the options and whitelist belong to, for Undo: Extract Animation's settings in Configure, else this window.
        private UnityEngine.Object SettingsOwner
        {
            get { return extract != null ? (UnityEngine.Object)extract : this; }
        }
        // Scrolls everything above Clean.
        private Vector2 outerScroll;
        private Vector2 whitelistScroll;
        // Every shapekey and bone name keyed in the listed animations, for suggestions.
        private List<string> keyedNames;

        /// <summary>Opens the window and adds the clips that are not listed yet.</summary>
        internal static void Open(IEnumerable<AnimationClip> clips)
        {
            // A Configure window (Extract Animation) is never reused for this.
            CleanAnimationKeysWindow window = null;
            foreach (CleanAnimationKeysWindow open in Resources.FindObjectsOfTypeAll<CleanAnimationKeysWindow>())
            {
                if (open.extract == null) window = open;
            }
            if (window == null)
            {
                window = CreateInstance<CleanAnimationKeysWindow>();
                window.titleContent = new GUIContent(CleanAnimationKeys.DisplayName);
                window.minSize = new Vector2(480.0f, 520.0f);
            }
            foreach (AnimationClip clip in clips) window.Add(clip);
            window.ShowUtility();
            window.Focus();
        }

        /// <summary>
        /// Opens a separate window for Extract Animation's Configure. Its options and
        /// whitelist are the Extract Animation settings, saved there on every change.
        /// </summary>
        internal static CleanAnimationKeysWindow OpenFor(ImportPresetExtractAnimationWindow owner, string targetName)
        {
            var window = CreateInstance<CleanAnimationKeysWindow>();
            window.titleContent = new GUIContent(CleanAnimationKeys.DisplayName + ": " + targetName);
            window.minSize = new Vector2(480.0f, 520.0f);
            window.extract = owner;
            window.ShowUtility();
            return window;
        }

        internal void Add(IEnumerable<AnimationClip> clips)
        {
            foreach (AnimationClip clip in clips) Add(clip);
        }

        private void Add(AnimationClip clip)
        {
            if (clip == null || animations.Contains(clip) || animations.Count >= MaxAnimations) return;
            animations.Add(clip);
            keyedNames = null;
        }

        internal void AddToWhitelist(IEnumerable<string> names)
        {
            Undo.RecordObject(SettingsOwner, SettingsUndo);
            foreach (string name in names)
            {
                if (!whitelist.Contains(name)) whitelist.Add(name);
            }
            Changed();
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

        // The whitelist or an option changed.
        private void Changed()
        {
            summary = null;
            if (extract != null) extract.SettingsChanged();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        // A list, whitelist or option put back by Undo or Redo: refresh, and in Configure save the settings again.
        private void OnUndoRedo()
        {
            keyedNames = null;
            summary = null;
            if (extract != null) extract.SettingsChanged();
            Repaint();
        }

        private void OnGUI()
        {
            if (extract != null)
            {
                options = extract.Settings.CleanOptions;
                whitelist = extract.Settings.Whitelist;
            }
            // Everything above Clean scrolls, so suggestions or a short window never push Clean out of view.
            outerScroll = EditorGUILayout.BeginScrollView(outerScroll);
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
            options.KeepNonZeroShapekeys = RiryToolGUI.Set(SettingsOwner, SettingsUndo, options.KeepNonZeroShapekeys, EditorGUILayout.ToggleLeft(new GUIContent("Keep non-zero shapekeys",
                "Keep a shapekey curve that has any non-zero key."), options.KeepNonZeroShapekeys));
            options.KeepShapekeyTangentMotion = RiryToolGUI.Set(SettingsOwner, SettingsUndo, options.KeepShapekeyTangentMotion, EditorGUILayout.ToggleLeft(new GUIContent("Keep shapekey tangent motion",
                "Keep a shapekey whose keys are 0 but whose tangents move the value between keys."), options.KeepShapekeyTangentMotion));
            options.KeepMovingBones = RiryToolGUI.Set(SettingsOwner, SettingsUndo, options.KeepMovingBones, EditorGUILayout.ToggleLeft(new GUIContent("Keep moving bones",
                "Keep a bone's translation, rotation or scale when its start and finish differ (3 decimal places)."), options.KeepMovingBones));
            options.KeepBoneMidKeys = RiryToolGUI.Set(SettingsOwner, SettingsUndo, options.KeepBoneMidKeys, EditorGUILayout.ToggleLeft(new GUIContent("Keep bone mid-keys/tangents",
                "Keep a bone's translation, rotation or scale with more than a start and finish key, or whose tangents " +
                "move between matching endpoints."), options.KeepBoneMidKeys));
            if (EditorGUI.EndChangeCheck()) Changed();
            EditorGUILayout.EndScrollView();

            if (summary != null) EditorGUILayout.HelpBox(summary, MessageType.Info);
            using (new EditorGUI.DisabledScope(animations.Count == 0))
            {
                if (GUILayout.Button("Clean", GUILayout.Height(28.0f))) { Clean(); RiryToolGUI.ExitAfterModal(); }
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
                        Undo.RecordObject(this, SettingsUndo);
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
                        Undo.RecordObject(SettingsOwner, SettingsUndo);
                        whitelist.RemoveAt(i);
                        Changed();
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
                int score = FuzzyMatch.Score(name, typed);
                if (score > 0) scored.Add(new KeyValuePair<int, string>(score, name));
            }
            scored.Sort((left, right) => left.Key != right.Key
                ? right.Key.CompareTo(left.Key)
                : string.Compare(left.Value, right.Value, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < scored.Count && i < MaxSuggestions; i++) yield return scored[i].Value;
        }

        private void Clean()
        {
            int curves = 0, clips = 0;
            foreach (AnimationClip clip in animations)
            {
                if (clip == null) continue;
                int count = 0;
                foreach (CleanAnimationKeys.Group group in CleanAnimationKeys.Removable(clip, options, whitelist)) count += group.Bindings.Count;
                curves += count;
                if (count > 0) clips++;
            }
            // The clips are edited in place, and other controllers may share them.
            if (!EditorUtility.DisplayDialog(CleanAnimationKeys.DisplayName, "Remove " + curves + " curve(s) from " + clips +
                    " animation(s)?\n\nThe animations are changed in place, so every controller that uses them changes too. " +
                    "Ctrl+Z undoes it.", "Clean", "Cancel"))
                return;
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
}

// ============================================================================
// GLOSSARY: Clean Animation Keys window
// Menu entries (Tools dropdown and Project right-click) are in Editor/UI/RiryToolsMenuBar.cs and RiryToolsRightClickMenu.cs; the logic is in Editor/AnimatorTools/CleanAnimationKeys.cs.
// ============================================================================
//
// CleanAnimationKeysWindow (this file)
//   animations               List<AnimationClip>  Animations to clean (up to MaxAnimations, 64).
//   whitelist                List<string>    Shapekey and bone names never removed; kept only while the window is open,
//                                            except in Configure, where it is saved with the Extract Animation settings.
//   options                  CleanAnimationKeys.Options  The four keep-rule checkboxes.
//   search                   string          Whitelist search text.
//   summary                  string          Result of the last Clean.
//   keyedNames               List<string>    Names keyed in the listed animations, for suggestions.
//   AnimationListHeight      const float     Height of the scrolling Animations list (150).
//   MaxSuggestions           const int       Suggestions shown (8).
//   extract                  ImportPresetExtractAnimationWindow  Set for Configure: options and whitelist are its settings.
//   Open(clips)              Opens the window and adds clips (Project selection or Animation window clip).
//   OpenFor(owner, name)     Opens a separate window for Extract Animation's Configure.
//   Changed()                Clears the summary; in Configure, saves the Extract Animation settings.
//   DrawAnimations()         One row per animation with Whitelist, plus an empty row while under 64.
//   DrawWhitelist()          Search field with Add, fuzzy suggestions, entries with x.
//   Suggestions(typed)       Ranks names with FuzzyMatch.Score (Editor/Components/FuzzyMatch.cs): starts with, contains, letters in order.
//   AddToWhitelist(names)    Adds names (from Add, a suggestion, or APPLY).
//   Clean()                  Asks first (curve and animation counts; clips are edited in place, shared ones included),
//                            then CleanAnimationKeys.Clean, then shows the summary.
//   outerScroll              Everything above the summary and Clean scrolls, so Clean always stays in view.
//   SettingsUndo / SettingsOwner  "Clean Animation Keys settings": Undo of the animation list (this window) and the
//                            whitelist and options (Extract Animation in Configure, else this window); OnUndoRedo
//                            refreshes and, in Configure, saves the settings.
//
