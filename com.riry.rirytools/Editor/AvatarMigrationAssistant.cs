using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Avatar Migration Assistant: copies components and settings from an old avatar
    /// (source) to a new one (target), and retargets the new one's animations; the source
    /// and the original files are never changed. This is the main window; its parts are in
    /// Editor/AvatarMigrationAssistant/, named by what they do. Copy: the object map
    /// (MatchObjects.cs), Manual matching when anything is unmatched or on every run with
    /// Manual matching on (ManualMatching.cs), the up-front check (UpFrontCheck.cs), the
    /// run (CopyRun.cs and its parts) and its report (RunReport.cs). Retarget animations:
    /// RetargetAnimations.cs, VrcFuryPrefabs.cs and MigratedFolder.cs. Without the VRChat
    /// SDK it still runs with the VRChat parts left out; opening it then shows the VRC SDK
    /// notice (SdkNotice.cs). Opened from Tools > RiryTools > Avatar Migration Assistant
    /// and the Main Menu's tab.
    /// </summary>
    internal sealed class AvatarMigrationAssistant : EditorWindow
    {
        internal const string Title = "Avatar Migration Assistant";
        // The VRChat Avatars SDK is in the project when this type is.
        private const string SdkProbeType = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
        private static readonly string[] ListNames = { "Common", "All" };
        private static readonly string[] AddReplace = { "Add", "Replace" };

        private static bool? sdkPresent;

        [SerializeField] private GameObject source;
        [SerializeField] private GameObject target;
        [SerializeField] private string lastRun = "";
        [SerializeField] private List<GameObject> ignore = new List<GameObject>();
        // Feature 9: the target's child prefabs included in Retarget animations.
        [SerializeField] private List<GameObject> includedPrefabs = new List<GameObject>();
        // The map of the last run, used again by Retarget animations for the same avatars.
        [SerializeField] private AvatarMigrationMap lastMap;
        [SerializeField] private bool foldMatching = true;
        [SerializeField] private bool foldCopy = true;
        [SerializeField] private bool foldIgnore;
        [SerializeField] private bool foldAnimations;
        [SerializeField] private List<string> openTypes = new List<string>();
        [SerializeField] private Vector2 scroll;

        [NonSerialized] private AvatarMigrationSettings settings;
        [NonSerialized] private List<AvatarExtraTypes.Category> extraCategories;
        [NonSerialized] private List<Transform> targetPrefabs;

        internal static void Open()
        {
            AvatarMigrationAssistant window = GetWindow<AvatarMigrationAssistant>(Title);
            window.minSize = new Vector2(420f, 360f);
            window.Show();
            EditorApplication.delayCall += AvatarMigrationSdkNotice.ShowIfNeeded;
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
                sdkPresent = AvatarCopyTypes.Find(SdkProbeType) != null;
                return sdkPresent.Value;
            }
        }

        private void OnHierarchyChange()
        {
            targetPrefabs = null;
            Repaint();
        }

        private void OnFocus()
        {
            extraCategories = null;
            targetPrefabs = null;
        }

        // ---- Drawing ----------------------------------------------------------------

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GameObject before = target;
            source = SceneObject("Source (old avatar)", source);
            target = SceneObject("Target (new avatar)", target);
            if (target != before) targetPrefabs = null;

            EditorGUI.BeginChangeCheck();
            DrawMatching();
            DrawCopy();
            DrawIgnore();
            DrawAnimations();
            if (EditorGUI.EndChangeCheck()) Settings.Save();
            EditorGUILayout.EndScrollView();

            string problem = Problem();
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Info);
            using (new EditorGUI.DisabledScope(problem != null))
            {
                if (GUILayout.Button("Copy", GUILayout.Height(28f))) EditorApplication.delayCall += Run;
            }
            if (lastRun.Length > 0) EditorGUILayout.HelpBox(lastRun, MessageType.None);
        }

        private void DrawMatching()
        {
            foldMatching = EditorGUILayout.Foldout(foldMatching, "Matching", true);
            if (!foldMatching) return;
            EditorGUI.indentLevel++;
            Settings.StructureTolerance = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Structure tolerance",
                "How far apart, in avatar-root space, an object and a candidate under the matched parent may be to match by structure."),
                Settings.StructureTolerance));
            Settings.ManualMatching = EditorGUILayout.Toggle(new GUIContent("Manual matching",
                "Open Manual matching on every run, listing every object. Off: it opens only when something is unmatched."),
                Settings.ManualMatching);
            EditorGUI.indentLevel--;
        }

        private void DrawCopy()
        {
            foldCopy = EditorGUILayout.Foldout(foldCopy, "Copy", true);
            if (!foldCopy) return;
            EditorGUI.indentLevel++;
            Settings.ShowAll = GUILayout.Toolbar(Settings.ShowAll ? 1 : 0, ListNames) == 1;
            foreach (AvatarCopyType type in AvatarCopyTypes.All)
            {
                if (type.Common || Settings.ShowAll) DrawType(type);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawType(AvatarCopyType type)
        {
            AvatarTypeSettings typeSettings = Settings.Type(type);
            bool sdkMissing = type.NeedsSdk && !SdkPresent;
            bool available = type.Available && !sdkMissing;
            string label = type.Label + (sdkMissing ? " (VRC SDK not detected)" : !type.Available ? " (not in project)" : "");
            bool open = openTypes.Contains(type.Id);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!available))
                {
                    typeSettings.Enabled = EditorGUILayout.ToggleLeft(label, typeSettings.Enabled);
                }
                if (available && typeSettings.Enabled)
                {
                    bool now = GUILayout.Toggle(open, open ? "Options ▲" : "Options ▼", EditorStyles.miniButton, GUILayout.Width(80f));
                    if (now != open)
                    {
                        if (now) openTypes.Add(type.Id);
                        else openTypes.Remove(type.Id);
                        open = now;
                    }
                }
            }
            if (!available || !typeSettings.Enabled || !open) return;
            EditorGUI.indentLevel++;
            bool creates = type.Id != AvatarCopyTypes.SkinnedMeshRenderers && type.Id != AvatarCopyTypes.AvatarDescriptor;
            bool replaces = creates && type.Id != AvatarCopyTypes.Transforms;
            if (creates)
                typeSettings.CreateMissing = EditorGUILayout.Toggle(new GUIContent("Create missing objects",
                    "Make target objects for source objects that are still unmatched, under the nearest matched parent."), typeSettings.CreateMissing);
            if (replaces)
                typeSettings.Replace = EditorGUILayout.Popup(new GUIContent("Existing on target",
                    "Add: alongside the target's own, skipping any that is identical. Replace: remove the target's first."),
                    typeSettings.Replace ? 1 : 0, ToContents(AddReplace)) == 1;
            switch (type.Id)
            {
                case AvatarCopyTypes.Transforms:
                    Settings.TransformPosition = EditorGUILayout.Toggle("Position", Settings.TransformPosition);
                    Settings.TransformRotation = EditorGUILayout.Toggle("Rotation", Settings.TransformRotation);
                    Settings.TransformScale = EditorGUILayout.Toggle("Scale", Settings.TransformScale);
                    Settings.TransformTagLayer = EditorGUILayout.Toggle("Tag and layer", Settings.TransformTagLayer);
                    Settings.TransformActive = EditorGUILayout.Toggle("Active state", Settings.TransformActive);
                    break;
                case AvatarCopyTypes.SkinnedMeshRenderers:
                    Settings.SkinnedSettings = EditorGUILayout.Toggle("Settings", Settings.SkinnedSettings);
                    Settings.SkinnedBlendshapes = EditorGUILayout.Toggle(new GUIContent("Blendshapes", "Values matched by name."), Settings.SkinnedBlendshapes);
                    Settings.SkinnedMaterials = EditorGUILayout.Toggle("Materials", Settings.SkinnedMaterials);
                    Settings.SkinnedBounds = EditorGUILayout.Toggle("Bounds", Settings.SkinnedBounds);
                    break;
                case AvatarCopyTypes.Animators:
                    Settings.RootAnimator = EditorGUILayout.Toggle(new GUIContent("Root animator", "Also the Animator on the avatar root."), Settings.RootAnimator);
                    break;
                case AvatarCopyTypes.AvatarDescriptor:
                    Settings.DescriptorViewpoint = EditorGUILayout.Toggle("Viewpoint", Settings.DescriptorViewpoint);
                    Settings.DescriptorLipSync = EditorGUILayout.Toggle("Lip sync and visemes", Settings.DescriptorLipSync);
                    Settings.DescriptorEyeLook = EditorGUILayout.Toggle("Eye look", Settings.DescriptorEyeLook);
                    Settings.DescriptorPlayableLayers = EditorGUILayout.Toggle("Playable layers", Settings.DescriptorPlayableLayers);
                    Settings.DescriptorExpressions = EditorGUILayout.Toggle("Expressions", Settings.DescriptorExpressions);
                    Settings.DescriptorColliders = EditorGUILayout.Toggle("Colliders", Settings.DescriptorColliders);
                    Settings.DescriptorBlueprintId = EditorGUILayout.ToggleLeft("Copy original blueprint ID?", Settings.DescriptorBlueprintId);
                    Settings.DescriptorAvatarScale = EditorGUILayout.Toggle(new GUIContent("Avatar scale",
                        "Copy the root scale and move the viewpoint to match."), Settings.DescriptorAvatarScale);
                    break;
                case AvatarCopyTypes.ExtraTypes:
                    DrawExtraTypes();
                    break;
            }
            EditorGUI.indentLevel--;
        }

        private void DrawExtraTypes()
        {
            if (extraCategories == null) extraCategories = AvatarExtraTypes.Categories();
            EditorGUILayout.LabelField("Lists: " + AvatarExtraTypes.Folder, EditorStyles.wordWrappedMiniLabel);
            foreach (AvatarExtraTypes.Category category in extraCategories)
            {
                EditorGUILayout.LabelField(category.Name, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (string entry in category.Entries)
                {
                    bool found = AvatarExtraTypes.Resolve(entry).Count > 0;
                    bool enabled = Settings.ExtraTypesEnabled.Contains(entry);
                    using (new EditorGUI.DisabledScope(!found))
                    {
                        bool now = EditorGUILayout.ToggleLeft(entry + (found ? "" : " (not in project)"), enabled);
                        if (now == enabled) continue;
                        if (now) Settings.ExtraTypesEnabled.Add(entry);
                        else Settings.ExtraTypesEnabled.Remove(entry);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawIgnore()
        {
            foldIgnore = EditorGUILayout.Foldout(foldIgnore, "Ignore list", true);
            if (!foldIgnore) return;
            EditorGUI.indentLevel++;
            for (int i = 0; i <= ignore.Count; i++)
            {
                GameObject current = i < ignore.Count ? ignore[i] : null;
                var chosen = SceneObject(i == 0 ? "Ignore" : " ", current);
                if (chosen == current) continue;
                if (chosen != null && (chosen == source || chosen == target))
                {
                    ShowNotification(new GUIContent("The avatar root can't be ignored."));
                    continue;
                }
                if (i == ignore.Count)
                {
                    if (chosen != null) ignore.Add(chosen);
                }
                else ignore[i] = chosen;
                AvatarIgnoreList.DropEmpty(ignore);
                break;
            }
            Settings.IgnoreChildren = EditorGUILayout.Toggle(new GUIContent("Include children", "Everything below an ignored object is ignored too."),
                Settings.IgnoreChildren);
            EditorGUI.indentLevel--;
        }

        private void DrawAnimations()
        {
            foldAnimations = EditorGUILayout.Foldout(foldAnimations, "Animations", true);
            if (!foldAnimations) return;
            EditorGUI.indentLevel++;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Migrated folder", "Every file the tool creates goes here, at its original path."),
                    new GUIContent(Settings.MigratedFolder.Length > 0 ? Settings.MigratedFolder : "(asked for when needed)"));
                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    string chosen = AvatarMigratedFolder.Choose(Settings.MigratedFolder);
                    if (chosen != null)
                    {
                        Settings.MigratedFolder = chosen;
                        GUI.changed = true;
                    }
                }
            }
            if (target != null)
            {
                if (targetPrefabs == null) targetPrefabs = new AvatarPrefabScope(target.transform).OutermostChildPrefabs();
                if (targetPrefabs.Count > 0) EditorGUILayout.LabelField("Child prefabs on the target", EditorStyles.boldLabel);
                foreach (Transform prefab in targetPrefabs)
                {
                    bool included = includedPrefabs.Contains(prefab.gameObject);
                    bool now = EditorGUILayout.ToggleLeft(new GUIContent("Include child prefab: " + prefab.name,
                        AnimationUtility.CalculateTransformPath(prefab, target.transform)), included);
                    if (now == included) continue;
                    if (now) includedPrefabs.Add(prefab.gameObject);
                    else includedPrefabs.Remove(prefab.gameObject);
                }
            }
            using (new EditorGUI.DisabledScope(Problem() != null))
            {
                if (GUILayout.Button(new GUIContent("Retarget animations", "Duplicate the target's controllers and clips into the migrated folder " +
                        "with their paths rewritten for the target, and point the target at the duplicates.")))
                    EditorApplication.delayCall += Retarget;
            }
            EditorGUI.indentLevel--;
        }

        private static GUIContent[] ToContents(string[] names)
        {
            var contents = new GUIContent[names.Length];
            for (int i = 0; i < names.Length; i++) contents[i] = new GUIContent(names[i]);
            return contents;
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

        // ---- Running ----------------------------------------------------------------

        // Features 1 and 2: the map, confirmed in Manual matching when needed; null when cancelled.
        private AvatarMigrationMap BuildMap()
        {
            AvatarMigrationMap map = AvatarMigrationMap.Build(source.transform, target.transform, Settings.StructureTolerance);
            if (Settings.ManualMatching || map.Unmatched().Count > 0)
            {
                Dictionary<string, Transform> unused;
                if (!AvatarMigrationMatchWindow.Review(map, Settings.ManualMatching, null, out unused)) return null;
            }
            return map;
        }

        private void Run()
        {
            if (Problem() != null) return;
            AvatarMigrationMap map = BuildMap();
            if (map == null)
            {
                Finish("Cancelled. Nothing was changed.");
                return;
            }
            AvatarIgnoreList.DropEmpty(ignore);
            HashSet<Transform> ignored = AvatarIgnoreList.Resolve(map, ignore, Settings.IgnoreChildren);
            AvatarUpFrontAnswers answers;
            if (!AvatarUpFrontCheck.Run(map, Settings, ignored, out answers))
            {
                Finish("Aborted. Nothing was changed.");
                return;
            }
            AvatarRunReport report = AvatarCopyRun.Execute(map, Settings, answers, ignored);
            lastMap = map;
            Finish(report.Summary);
            AvatarRunReportWindow.Open(report);
        }

        // Feature 8: needs the map, so features 1 and 2 run first when there is none for these avatars.
        private void Retarget()
        {
            if (Problem() != null) return;
            AvatarMigrationMap map = lastMap != null && lastMap.SourceRoot == source.transform && lastMap.TargetRoot == target.transform
                ? lastMap
                : BuildMap();
            if (map == null)
            {
                Finish("Cancelled. Nothing was changed.");
                return;
            }
            if (!AvatarMigratedFolder.Ensure(Settings))
            {
                Finish("Retarget animations needs a folder for the migrated files. Nothing was changed.");
                return;
            }
            var included = new HashSet<Transform>();
            foreach (GameObject prefab in includedPrefabs)
            {
                if (prefab != null && prefab.transform.IsChildOf(target.transform)) included.Add(prefab.transform);
            }
            var report = new AvatarRunReport();
            if (!AvatarAnimationRetarget.Run(map, Settings, included, report))
            {
                Finish("Cancelled. Nothing was changed.");
                return;
            }
            lastMap = map;
            Finish("Animations retargeted: " + report.FilesCreated.Count + " file" + (report.FilesCreated.Count == 1 ? "" : "s") + " created.");
            AvatarRunReportWindow.Open(report);
        }

        private void Finish(string result)
        {
            lastRun = result;
            Repaint();
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant window
// The menu entry, Tools > RiryTools > Avatar Migration Assistant (menu key N), is in RiryMenus.cs;
// the Main Menu's Avatar Migration Assistant tab hosts this window. Its parts are in Editor/AvatarMigrationAssistant/:
//   MatchObjects.cs (1, the object map), ManualMatching.cs (2), CopyTypes.cs (3), Settings.cs (EditorPrefs),
//   UpFrontCheck.cs (everything up front), CopyRun.cs (the run, 4 and 15), TypeRules.cs, CopyPrefabs.cs (11),
//   UpgradeConstraints.cs, RepointReferences.cs (5 and 12), CorrectRotation.cs (7), CorrectScale.cs (6), IgnoreList.cs (13),
//   ExtraTypes.cs (14), RunReport.cs, RetargetAnimations.cs (8), VrcFuryPrefabs.cs (9), MigratedFolder.cs (10),
//   SdkNotice.cs (the VRC SDK not detected popup).
// ============================================================================
//
// AvatarMigrationAssistant (this file)
//   SdkPresent               The VRChat SDK, found by type name. Without it, Open shows AvatarMigrationSdkNotice and the
//                            VRChat types are shown as not available; the rest still runs.
//   Source / Target          Scene avatars (old and new); copying an avatar onto itself, or onto one inside it, is blocked.
//   Matching                 Structure tolerance, Manual matching.
//   Copy                     Common / All list; per type: Enabled, Options (Create missing objects, Existing on target: Add
//                            or Replace, and the sub-options of Transforms, SkinnedMeshRenderers, Animators, the Avatar
//                            Descriptor and Extra types).
//   Ignore list              Objects on either avatar (not the roots), Include children.
//   Animations               Migrated folder (Browse), Include child prefab per target child prefab, Retarget animations.
//   Copy / Run()             Map (BuildMap: features 1 and 2), ignore list, up-front check, the run, the report popup.
//   Retarget()               The last run's map for the same avatars, else BuildMap; the migrated folder; AvatarAnimationRetarget.
//   [SerializeField]         source, target, lastRun, ignore, includedPrefabs, lastMap, the fold states, openTypes, scroll.
