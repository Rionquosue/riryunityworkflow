using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    /// and the Main Menu's Tools tab (in its own window).
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
        [NonSerialized] private List<Transform> targetPrefabs;

        internal static void Open()
        {
            AvatarMigrationAssistant window = GetWindow<AvatarMigrationAssistant>(Title);
            window.minSize = new Vector2(420f, 360f);
            window.Show();
            EditorApplication.delayCall += AvatarMigrationSdkNotice.ShowIfNeeded;
        }

        /// <summary>The Main Menu's Tools tab: the tool in its own floating window.</summary>
        internal static void OpenPopup()
        {
            AvatarMigrationAssistant window = GetWindow<AvatarMigrationAssistant>(true, Title);
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
            targetPrefabs = null;
        }

        // ---- Drawing ----------------------------------------------------------------

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= DropLastMap;
            Undo.undoRedoPerformed += DropLastMap;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= DropLastMap;
        }

        // After Undo or Redo the last run's map can point at objects the run made, so Retarget builds a new one.
        private void DropLastMap()
        {
            lastMap = null;
        }

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
            EditorGUILayout.LabelField(new GUIContent("Types: " + AvatarCopyTypes.FilePath, "Edit this file to change the list; it is read again when it changes."),
                EditorStyles.wordWrappedMiniLabel);
            // Indented like the controls around it (GUILayout.Toolbar ignores the indent level).
            Rect toolbar = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            Settings.ShowAll = GUI.Toolbar(toolbar, Settings.ShowAll ? 1 : 0, ListNames) == 1;
            // Grouped by category (the file's kinds of type); each category folds.
            var categories = new List<string>();
            foreach (AvatarCopyType type in AvatarCopyTypes.All)
            {
                if ((type.Common || Settings.ShowAll) && !categories.Contains(type.Category)) categories.Add(type.Category);
            }
            foreach (string category in categories)
            {
                string key = "category:" + category;
                bool open = EditorGUILayout.Foldout(openTypes.Contains(key), category, true);
                if (open != openTypes.Contains(key))
                {
                    if (open) openTypes.Add(key);
                    else openTypes.Remove(key);
                }
                if (!open) continue;
                EditorGUI.indentLevel++;
                foreach (AvatarCopyType type in AvatarCopyTypes.All)
                {
                    if (type.Category == category && (type.Common || Settings.ShowAll)) DrawType(type);
                }
                EditorGUI.indentLevel--;
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
            bool creates = type.Rule != AvatarCopyTypes.SkinnedMeshRenderersRule && type.Rule != AvatarCopyTypes.DescriptorRule;
            bool replaces = creates && type.Rule != AvatarCopyTypes.TransformsRule;
            if (creates)
                typeSettings.CreateMissing = EditorGUILayout.Toggle(new GUIContent("Create missing objects",
                    "Make target objects for source objects that are still unmatched, under the nearest matched parent."), typeSettings.CreateMissing);
            if (replaces)
                typeSettings.Replace = EditorGUILayout.Popup(new GUIContent("Existing on target",
                    "Add: alongside the target's own, skipping any that is identical. Replace: remove the target's first, on the objects " +
                    "the source copies into (not on ignored objects, objects only the target has, or child prefabs)."),
                    typeSettings.Replace ? 1 : 0, ToContents(AddReplace)) == 1;
            switch (type.Rule)
            {
                case AvatarCopyTypes.TransformsRule:
                    Settings.TransformPosition = EditorGUILayout.Toggle("Position", Settings.TransformPosition);
                    Settings.TransformRotation = EditorGUILayout.Toggle("Rotation", Settings.TransformRotation);
                    Settings.TransformScale = EditorGUILayout.Toggle("Scale", Settings.TransformScale);
                    Settings.TransformTagLayer = EditorGUILayout.Toggle("Tag and layer", Settings.TransformTagLayer);
                    Settings.TransformActive = EditorGUILayout.Toggle("Active state", Settings.TransformActive);
                    break;
                case AvatarCopyTypes.SkinnedMeshRenderersRule:
                    Settings.SkinnedSettings = EditorGUILayout.Toggle("Settings", Settings.SkinnedSettings);
                    Settings.SkinnedBlendshapes = EditorGUILayout.Toggle(new GUIContent("Blendshapes", "Values matched by name."), Settings.SkinnedBlendshapes);
                    Settings.SkinnedMaterials = EditorGUILayout.Toggle("Materials", Settings.SkinnedMaterials);
                    Settings.SkinnedBounds = EditorGUILayout.Toggle("Bounds", Settings.SkinnedBounds);
                    break;
                case AvatarCopyTypes.AnimatorsRule:
                    Settings.RootAnimator = EditorGUILayout.Toggle(new GUIContent("Root animator", "Also the Animator on the avatar root."), Settings.RootAnimator);
                    break;
                case AvatarCopyTypes.DescriptorRule:
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
            }
            EditorGUI.indentLevel--;
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
                    new GUIContent(Settings.MigratedFolder.Length > 0 ? Settings.MigratedFolder : "(asked for when needed)", Settings.MigratedFolder));
                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    string chosen = AvatarMigratedFolder.Choose(Settings.MigratedFolder);
                    if (chosen != null)
                    {
                        Settings.MigratedFolder = chosen;
                        GUI.changed = true;
                    }
                    RiryGui.ExitAfterModal();
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

    /// <summary>
    /// One entry of the type list: one checkbox in the Copy list (feature 3). Its types are
    /// found by full name across the loaded assemblies (Namespace.* takes every component in
    /// a namespace), so VRChat, DynamicBone, FinalIK and other optional types are simply
    /// left out when they are not in the project.
    /// </summary>
    internal sealed class AvatarCopyType
    {
        internal string Id = "";
        internal string Label = "";
        internal string Category = "";
        internal bool Common;
        internal bool DefaultEnabled;
        internal bool NeedsSdk;
        // A built-in rule (AvatarCopyTypes rule names), or "".
        internal string Rule = "";
        internal readonly List<string> TypeNames = new List<string>();

        private Type[] resolved;

        /// <summary>The types found in this project; empty when none is.</summary>
        internal Type[] Types
        {
            get
            {
                if (resolved != null) return resolved;
                var types = new List<Type>();
                foreach (string name in TypeNames)
                {
                    foreach (Type type in AvatarCopyTypes.Resolve(name))
                    {
                        if (!types.Contains(type)) types.Add(type);
                    }
                }
                resolved = types.ToArray();
                return resolved;
            }
        }

        internal bool Available
        {
            get { return Types.Length > 0 || Rule == AvatarCopyTypes.PrefabsRule; }
        }
    }

    /// <summary>
    /// The type list, read from Documents/RiryTools/migrator/types.yml. That file is a copy of
    /// the package's template (Editor/AvatarMigrationAssistant/types.yml, every type from
    /// Pumkin's Avatar Tools sorted by kind), made when it is missing; users edit the copy.
    /// Entries with a built-in place run in the order of feature 3 (RunOrder), the rest after
    /// them in file order.
    /// </summary>
    internal static class AvatarCopyTypes
    {
        internal const string FolderName = "migrator";
        internal const string FileName = "types.yml";
        // The GUID of the template (types.yml.meta).
        private const string TemplateGuid = "0756bf5a933b437db4405f7a01a55498";

        // Built-in rules.
        internal const string PrefabsRule = "prefabs";
        internal const string TransformsRule = "transforms";
        internal const string MeshRenderersRule = "meshrenderers";
        internal const string SkinnedMeshRenderersRule = "skinnedmeshrenderers";
        internal const string AnimatorsRule = "animators";
        internal const string CamerasRule = "cameras";
        internal const string DescriptorRule = "descriptor";
        internal const string UnityConstraintRule = "unityconstraint";

        // Type names the run handles by name.
        internal const string PhysBone = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";
        internal const string PhysBoneCollider = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider";
        internal const string ContactSender = "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender";
        internal const string ContactReceiver = "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver";
        internal const string DescriptorType = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
        internal const string PipelineManager = "VRC.Core.PipelineManager";
        internal const string VrcConstraintPrefix = "VRC.SDK3.Dynamics.Constraint.Components.VRC";

        /// <summary>Feature 3's run order, by entry id; other entries run after these, in file order.</summary>
        private static readonly string[] RunOrder =
        {
            "prefab", "particlesystem", "collider", "rigidbody", "trailrenderer", "light", "animator", "audiosource",
            "contactreceiver", "contactsender", "physbonecollider", "physbone", "vrcstation", "vrcheadchop",
            "dynamicbonecollider", "dynamicbone", "aimconstraint", "lookatconstraint", "parentconstraint", "positionconstraint",
            "rotationconstraint", "scaleconstraint", "vrcaimconstraint", "vrclookatconstraint", "vrcparentconstraint",
            "vrcpositionconstraint", "vrcrotationconstraint", "vrcscaleconstraint", "characterjoint", "configurablejoint",
            "fixedjoint", "hingejoint", "springjoint", "transform", "meshrenderer", "skinnedmeshrenderer", "camera", "finalik",
            "vrcavatardescriptor"
        };

        private static readonly Dictionary<string, Type> Cache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static List<AvatarCopyType> all;
        private static DateTime loadedStamp;
        // The last read problem: All runs on every draw, so one problem is logged once, not every repaint.
        private static string lastProblem;

        internal static string FilePath
        {
            get { return RiryToolsPaths.Documents(Path.Combine(FolderName, FileName)); }
        }

        /// <summary>Every entry, in run order; read again when the file changes.</summary>
        internal static List<AvatarCopyType> All
        {
            get
            {
                try
                {
                    CopyTemplateIfMissing();
                    DateTime stamp = File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : DateTime.MinValue;
                    if (all != null && stamp == loadedStamp) return all;
                    loadedStamp = stamp;
                    all = Read(FilePath);
                    lastProblem = null;
                }
                catch (Exception exception)
                {
                    if (exception.Message != lastProblem)
                        Debug.LogWarning(AvatarMigrationAssistant.Title + ": could not read " + FilePath + ": " + exception.Message);
                    lastProblem = exception.Message;
                    if (all == null) all = new List<AvatarCopyType>();
                }
                return all;
            }
        }

        /// <summary>The first entry with a built-in rule, or null.</summary>
        internal static AvatarCopyType WithRule(string rule)
        {
            return All.Find(type => type.Rule == rule);
        }

        private static void CopyTemplateIfMissing()
        {
            if (File.Exists(FilePath)) return;
            string template = AssetDatabase.GUIDToAssetPath(TemplateGuid);
            if (string.IsNullOrEmpty(template)) throw new FileNotFoundException("the package's types.yml template is missing.");
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.Copy(Path.GetFullPath(template), FilePath);
        }

        // ---- Reading the file -------------------------------------------------------

        private static List<AvatarCopyType> Read(string path)
        {
            var entries = new List<AvatarCopyType>();
            var root = YamlLite.Parse(File.ReadAllLines(path)) as List<KeyValuePair<string, object>>;
            var categories = root != null ? YamlLite.Get(root, "categories") as List<KeyValuePair<string, object>> : null;
            if (categories == null) return entries;
            foreach (KeyValuePair<string, object> category in categories)
            {
                var items = category.Value as List<object>;
                if (items == null) continue;
                foreach (object item in items)
                {
                    var map = item as List<KeyValuePair<string, object>>;
                    if (map == null) continue;
                    var entry = new AvatarCopyType
                    {
                        Category = category.Key,
                        Id = YamlLite.Text(map, "id"),
                        Label = YamlLite.Text(map, "label"),
                        Common = YamlLite.Flag(map, "common"),
                        DefaultEnabled = YamlLite.Flag(map, "enabled"),
                        NeedsSdk = YamlLite.Flag(map, "sdk"),
                        Rule = YamlLite.Text(map, "rule")
                    };
                    var names = YamlLite.Get(map, "types") as List<object>;
                    if (names != null)
                    {
                        foreach (object name in names)
                        {
                            if (name is string && ((string)name).Length > 0) entry.TypeNames.Add((string)name);
                        }
                    }
                    if (entry.Id.Length == 0) entry.Id = entry.Label;
                    if (entry.Label.Length == 0) entry.Label = entry.Id;
                    if (entry.Id.Length > 0 && !entries.Exists(other => other.Id == entry.Id)) entries.Add(entry);
                }
            }
            // Built-in places first, in feature 3's order; the rest keep their file order.
            var ordered = new List<AvatarCopyType>();
            foreach (string id in RunOrder)
            {
                AvatarCopyType found = entries.Find(entry => entry.Id == id);
                if (found != null) ordered.Add(found);
            }
            foreach (AvatarCopyType entry in entries)
            {
                if (!ordered.Contains(entry)) ordered.Add(entry);
            }
            return ordered;
        }

        // ---- Finding types ----------------------------------------------------------

        /// <summary>The component types a name stands for: one by full name, or every one in a namespace for Namespace.*.</summary>
        internal static List<Type> Resolve(string name)
        {
            var types = new List<Type>();
            if (name.EndsWith(".*", StringComparison.Ordinal))
            {
                string space = name.Substring(0, name.Length - 2);
                foreach (Type type in AllTypes())
                {
                    if (type.Namespace == space && !type.IsAbstract && typeof(Component).IsAssignableFrom(type)) types.Add(type);
                }
                return types;
            }
            Type found = Find(name);
            if (found != null && typeof(Component).IsAssignableFrom(found)) types.Add(found);
            return types;
        }

        /// <summary>A type by full name in any loaded assembly, or null.</summary>
        internal static Type Find(string fullName)
        {
            Type found;
            if (Cache.TryGetValue(fullName, out found)) return found;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    found = assembly.GetType(fullName, false);
                }
                catch (Exception)
                {
                    found = null;
                }
                if (found != null) break;
            }
            Cache[fullName] = found;
            return found;
        }

        /// <summary>A type by its simple name in any loaded assembly, or null (for SDK editor classes).</summary>
        internal static Type FindByName(string name)
        {
            Type found;
            if (Cache.TryGetValue("?" + name, out found)) return found;
            foreach (Type type in AllTypes())
            {
                if (type.Name != name) continue;
                found = type;
                break;
            }
            Cache["?" + name] = found;
            return found;
        }

        private static IEnumerable<Type> AllTypes()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }
                foreach (Type type in types)
                {
                    if (type != null) yield return type;
                }
            }
        }
    }

    /// <summary>
    /// The small part of YAML the type list uses: maps ("key: value", "key:" with an indented
    /// block), lists ("- value", "- key: value" starting a map) and # comments. Maps keep
    /// their order as lists of key/value pairs.
    /// </summary>
    internal static class YamlLite
    {
        private struct Line
        {
            internal int Indent;
            internal string Text;
        }

        internal static object Parse(string[] raw)
        {
            var lines = new List<Line>();
            foreach (string text in raw)
            {
                string line = StripComment(text).TrimEnd();
                if (line.Trim().Length == 0) continue;
                lines.Add(new Line { Indent = line.Length - line.TrimStart().Length, Text = line.Trim() });
            }
            int index = 0;
            return lines.Count == 0 ? null : Block(lines, ref index, lines[0].Indent);
        }

        private static string StripComment(string line)
        {
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"' || line[i] == '\'') quoted = !quoted;
                if (line[i] == '#' && !quoted && (i == 0 || char.IsWhiteSpace(line[i - 1]))) return line.Substring(0, i);
            }
            return line;
        }

        private static bool IsList(Line line)
        {
            return line.Text == "-" || line.Text.StartsWith("- ", StringComparison.Ordinal);
        }

        private static bool IsKey(string text)
        {
            int colon = text.IndexOf(':');
            return colon > 0 && (colon == text.Length - 1 || text[colon + 1] == ' ');
        }

        private static object Block(List<Line> lines, ref int index, int indent)
        {
            if (IsList(lines[index]))
            {
                var list = new List<object>();
                while (index < lines.Count && lines[index].Indent == indent && IsList(lines[index]))
                {
                    string rest = lines[index].Text.Substring(1).Trim();
                    if (rest.Length == 0)
                    {
                        index++;
                        list.Add(index < lines.Count && lines[index].Indent > indent ? Block(lines, ref index, lines[index].Indent) : null);
                    }
                    else if (IsKey(rest))
                    {
                        // "- key: value" starts a map two columns in.
                        lines[index] = new Line { Indent = indent + 2, Text = rest };
                        list.Add(Block(lines, ref index, indent + 2));
                    }
                    else
                    {
                        list.Add(Unquote(rest));
                        index++;
                    }
                }
                return list;
            }
            var map = new List<KeyValuePair<string, object>>();
            while (index < lines.Count && lines[index].Indent == indent && !IsList(lines[index]))
            {
                string text = lines[index].Text;
                int colon = text.IndexOf(':');
                if (colon <= 0)
                {
                    index++;
                    continue;
                }
                string key = Unquote(text.Substring(0, colon).Trim());
                string value = text.Substring(colon + 1).Trim();
                index++;
                object child = null;
                if (value.Length > 0) child = Unquote(value);
                else if (index < lines.Count && (lines[index].Indent > indent || (lines[index].Indent == indent && IsList(lines[index]))))
                    child = Block(lines, ref index, lines[index].Indent);
                map.Add(new KeyValuePair<string, object>(key, child));
            }
            return map;
        }

        private static string Unquote(string text)
        {
            if (text.Length >= 2 && ((text[0] == '"' && text[text.Length - 1] == '"') || (text[0] == '\'' && text[text.Length - 1] == '\'')))
                return text.Substring(1, text.Length - 2);
            return text;
        }

        internal static object Get(List<KeyValuePair<string, object>> map, string key)
        {
            foreach (KeyValuePair<string, object> pair in map)
            {
                if (pair.Key == key) return pair.Value;
            }
            return null;
        }

        internal static string Text(List<KeyValuePair<string, object>> map, string key)
        {
            return Get(map, key) as string ?? "";
        }

        internal static bool Flag(List<KeyValuePair<string, object>> map, string key)
        {
            return string.Equals(Text(map, key), "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant window
// The menu entry, Tools > RiryTools > Avatar Migration Assistant (menu key N), is in RiryMenus.cs;
// the Main Menu's Tools tab opens it in its own window (OpenPopup). Its parts are in Editor/AvatarMigrationAssistant/:
//   MatchObjects.cs (1, the object map), ManualMatching.cs (2), types.yml (3 and 14, the type list, read in this file),
//   Settings.cs (EditorPrefs),
//   UpFrontCheck.cs (everything up front), CopyRun.cs (the run, 4 and 15), TypeRules.cs, CopyPrefabs.cs (11),
//   UpgradeConstraints.cs, RepointReferences.cs (5 and 12), CorrectRotation.cs (7), CorrectScale.cs (6), IgnoreList.cs (13),
//   RunReport.cs, RetargetAnimations.cs (8), VrcFuryPrefabs.cs (9), MigratedFolder.cs (10),
//   SdkNotice.cs (the VRC SDK not detected popup).
// ============================================================================
//
// AvatarMigrationAssistant (this file)
//   SdkPresent               The VRChat SDK, found by type name. Without it, Open shows AvatarMigrationSdkNotice and the
//                            VRChat types are shown as not available; the rest still runs.
//   Source / Target          Scene avatars (old and new); copying an avatar onto itself, or onto one inside it, is blocked.
//   Matching                 Structure tolerance, Manual matching.
//   Copy                     The type list's file; Common / All; the entries grouped by category (each folds); per entry:
//                            Enabled, Options (Create missing objects, Existing on target: Add or Replace, and the
//                            sub-options of Transforms, SkinnedMeshRenderers, Animators and the Avatar Descriptor).
//   Ignore list              Objects on either avatar (not the roots), Include children.
//   Animations               Migrated folder (Browse), Include child prefab per target child prefab, Retarget animations.
//   Copy / Run()             Map (BuildMap: features 1 and 2), ignore list, up-front check, the run, the report popup.
//   Retarget()               The last run's map for the same avatars, else BuildMap; the migrated folder; AvatarAnimationRetarget.
//   Open() / OpenPopup()     The Tools menu entry (dockable) / the Main Menu's Tools tab (a floating window); both show the
//                            VRC SDK notice when the SDK is missing.
//   [SerializeField]         source, target, lastRun, ignore, includedPrefabs, lastMap, the fold states, openTypes, scroll.
//   DropLastMap()            Undo.undoRedoPerformed: forgets lastMap, so Retarget rebuilds the map after Undo or Redo.
//
// ---- Type list (this file) ----
// AvatarMigrationAssistant/types.yml  The template: every type from Pumkin's Avatar Tools, sorted by kind into categories;
//                       each entry has id, label, common, enabled, sdk, rule and types.
// Documents/RiryTools/migrator/types.yml  The copy the tool reads (made from the template when missing); users edit it.
// AvatarCopyType        One entry: Id, Label, Category, Common, DefaultEnabled, NeedsSdk, Rule, TypeNames; Types (found in
//                       this project, Namespace.* expanded), Available.
// AvatarCopyTypes
//   All / WithRule(rule)  The entries in run order (RunOrder: feature 3's order, then the rest in file order), read again
//                       when the file changes; the first entry with a built-in rule.
//   Rules               prefabs, transforms, meshrenderers, skinnedmeshrenderers, animators, cameras, descriptor,
//                       unityconstraint: the entries the run treats specially.
//   Resolve / Find / FindByName  Types by name across the loaded assemblies (cached).
// YamlLite              The YAML subset the list uses: maps, lists, "- key: value" maps, # comments.
