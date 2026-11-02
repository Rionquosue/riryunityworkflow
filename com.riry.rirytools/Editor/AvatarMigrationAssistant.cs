using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Avatar Migration Assistant: copies components and settings from an old avatar
    /// (source) to a new one (target); the source is never changed. This is the main
    /// window; its parts are in Editor/AvatarMigrationAssistant/, named by what they do.
    /// A run first builds the object map (MatchObjects.cs) and opens Manual matching
    /// (ManualMatching.cs) when anything is unmatched, or on every run with Manual
    /// matching on. Without the VRChat SDK it still runs; the VRChat parts are left out
    /// and an error says so. Opened from Tools > RiryTools > Avatar Migration Assistant
    /// and the Main Menu's tab.
    /// </summary>
    internal sealed class AvatarMigrationAssistant : EditorWindow
    {
        internal const string Title = "Avatar Migration Assistant";
        // The VRChat Avatars SDK is in the project when this type is.
        private const string SdkProbeType = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
        internal const string SdkMissingMessage = "VRChat SDK not found. PhysBones, PhysBone colliders, contacts, VRC constraints, " +
            "VRCStation, VRCHeadChop, the Avatar Descriptor and the VRC constraint upgrade will not be available. Everything else can still be copied.";

        private static bool? sdkPresent;

        [SerializeField] private GameObject source;
        [SerializeField] private GameObject target;
        [SerializeField] private string lastRun = "";

        [System.NonSerialized] private AvatarMigrationSettings settings;

        internal static void Open()
        {
            AvatarMigrationAssistant window = GetWindow<AvatarMigrationAssistant>(Title);
            window.minSize = new Vector2(380f, 220f);
            window.Show();
        }

        private AvatarMigrationSettings Settings
        {
            get { return settings ?? (settings = AvatarMigrationSettings.Load()); }
        }

        /// <summary>Whether the VRChat Avatars SDK is in the project, found by type name in the loaded assemblies.</summary>
        internal static bool SdkPresent
        {
            get
            {
                if (sdkPresent.HasValue) return sdkPresent.Value;
                sdkPresent = false;
                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetType(SdkProbeType, false) == null) continue;
                    sdkPresent = true;
                    break;
                }
                return sdkPresent.Value;
            }
        }

        private void OnGUI()
        {
            if (!SdkPresent) EditorGUILayout.HelpBox(SdkMissingMessage, MessageType.Error);
            source = SceneObject("Source (old avatar)", source);
            target = SceneObject("Target (new avatar)", target);

            EditorGUI.BeginChangeCheck();
            Settings.StructureTolerance = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Structure tolerance",
                "How far apart, in avatar-root space, an object and a candidate under the matched parent may be to match by structure."),
                Settings.StructureTolerance));
            Settings.ManualMatching = EditorGUILayout.Toggle(new GUIContent("Manual matching",
                "Open Manual matching on every run, listing every object. Off: it opens only when something is unmatched."),
                Settings.ManualMatching);
            if (EditorGUI.EndChangeCheck()) Settings.Save();

            string problem = Problem();
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Info);
            using (new EditorGUI.DisabledScope(problem != null))
            {
                if (GUILayout.Button("Copy", GUILayout.Height(28f))) EditorApplication.delayCall += Run;
            }
            if (lastRun.Length > 0) EditorGUILayout.HelpBox(lastRun, MessageType.None);
        }

        private static GameObject SceneObject(string label, GameObject current)
        {
            var chosen = (GameObject)EditorGUILayout.ObjectField(label, current, typeof(GameObject), true);
            return chosen != null && EditorUtility.IsPersistent(chosen) ? current : chosen;
        }

        // Why a run cannot start, or null. Copying an avatar onto itself is blocked.
        private string Problem()
        {
            if (source == null || target == null) return "Set the source and the target avatar.";
            if (source == target || target.transform.IsChildOf(source.transform) || source.transform.IsChildOf(target.transform))
                return "The source and target must be two separate avatars.";
            return null;
        }

        private void Run()
        {
            if (Problem() != null) return;
            if (!SdkPresent) Debug.LogError(Title + ": " + SdkMissingMessage);
            AvatarMigrationMap map = AvatarMigrationMap.Build(source.transform, target.transform, Settings.StructureTolerance);
            List<AvatarMatch> unmatched = map.Unmatched();
            if (Settings.ManualMatching || unmatched.Count > 0)
            {
                Dictionary<string, Transform> animationTargets;
                if (!AvatarMigrationMatchWindow.Review(map, Settings.ManualMatching, null, out animationTargets))
                {
                    lastRun = "Cancelled. Nothing was changed.";
                    Repaint();
                    return;
                }
            }
            lastRun = Summary(map);
            Debug.Log(Title + ": " + lastRun);
            Repaint();
        }

        private static string Summary(AvatarMigrationMap map)
        {
            var counts = new Dictionary<AvatarMatchMethod, int>();
            foreach (AvatarMatch match in map.Matches)
            {
                int count;
                counts.TryGetValue(match.Method, out count);
                counts[match.Method] = count + 1;
            }
            int Get(AvatarMatchMethod method)
            {
                int count;
                return counts.TryGetValue(method, out count) ? count : 0;
            }
            return "Map confirmed: " + (map.Matches.Count - Get(AvatarMatchMethod.None)) + " of " + map.Matches.Count + " objects matched (" +
                Get(AvatarMatchMethod.NamePath) + " by name path, " + Get(AvatarMatchMethod.Humanoid) + " humanoid, " +
                Get(AvatarMatchMethod.Structure) + " by structure, " + Get(AvatarMatchMethod.Manual) + " manual), " +
                Get(AvatarMatchMethod.None) + " unmatched. Copying components is the next stage and is not built yet; nothing was changed.";
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant window
// The menu entry, Tools > RiryTools > Avatar Migration Assistant (menu key N), is in RiryMenus.cs;
// the Main Menu's Avatar Migration Assistant tab hosts this window. Its parts are in Editor/AvatarMigrationAssistant/:
//   MatchObjects.cs (feature 1, the object map), ManualMatching.cs (feature 2), Settings.cs (EditorPrefs).
// ============================================================================
//
// AvatarMigrationAssistant (this file)
//   SdkPresent / SdkMissingMessage  The VRChat SDK, found by type name; without it an error is shown and logged and
//                            the VRChat parts are left out, the rest still runs.
//   Source / Target          Scene avatars (old and new); copying an avatar onto itself, or onto one inside it, is blocked.
//   Structure tolerance / Manual matching  AvatarMigrationSettings (EditorPrefs, saved on change).
//   Copy / Run()             Builds the map (feature 1), opens Manual matching when something is unmatched or Manual
//                            matching is on (feature 2); Cancel stops with nothing changed. Copying comes in stage 2.
//   lastRun                  The last run's result, shown below Copy and logged.
//   [SerializeField]         source, target, lastRun.
