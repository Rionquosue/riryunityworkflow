using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// IconCap: square PNG icons captured from the Scene View, run through an effect
    /// stack and saved to an export folder; captures can be saved as jobs and remade
    /// with Run. Opened from Tools > RiryTools > IconCap and the Main Menu's Tools tab (in its own window).
    /// The live preview renders only while the window is shown: it is requested when the
    /// window draws and rendered on the next editor update (Watch).
    /// </summary>
    internal sealed class IconCapWindow : EditorWindow
    {
        private const float WideWidth = 640f;
        // Narrower windows than these put a row's parts on two lines (each is the row's width plus a buffer).
        private const float PrefixSuffixRow = 460f;
        private const float JobRow = 440f;
        private const float RestoreRow = 420f;
        // A row's fixed space besides its contents: the scroll bar and the window margins.
        private const float RowMargin = 40f;
        private const float PreviewMaxSide = 512f;
        private const int PreviewMaxPixels = 1024;
        private const double PreviewInterval = 0.1;
        private const float SmallThumbnail = 64f;
        private const float LargeThumbnail = 128f;
        private static readonly string[] FovNames = { "Scene View", "Custom", "35mm", "50mm", "85mm" };
        private static readonly string[] BackgroundNames = { "Transparent", "Opaque", "Transparent with background" };
        private static readonly GUIContent[] LightingNames = { new GUIContent("Scene"), new GUIContent("HDRI"), new GUIContent("Flat (unlit)") };

        [SerializeField] private IconCapSettings settings = new IconCapSettings();
        [SerializeField] private bool onlySelected;
        [SerializeField] private List<SkinnedMeshRenderer> slots = new List<SkinnedMeshRenderer>();
        [SerializeField] private GameObject isolated;
        [SerializeField] private string exportFolder = "Assets";
        [SerializeField] private List<IconCapEntry> session = new List<IconCapEntry>();
        [SerializeField] private int latest = -1;
        [SerializeField] private string lastSavedPath = "";
        [SerializeField] private IconCapEntry lastCapture;
        [SerializeField] private IconCapEntry lastSave;
        [SerializeField] private IconGenerator throughGenerator;
        [SerializeField] private IconGenerator generator;
        [SerializeField] private bool largePreviews;
        [SerializeField] private int jobChoice;
        [SerializeField] private string presetName = "";
        [SerializeField] private bool foldEffects = true;
        [SerializeField] private bool foldSession = true;
        [SerializeField] private bool foldObjects = true;
        [SerializeField] private bool foldCamera;
        [SerializeField] private bool foldBackground;
        [SerializeField] private bool foldOptions = true;
        [SerializeField] private bool foldRestore;
        [SerializeField] private bool foldGenerator;
        [SerializeField] private bool foldGeneratorStack;
        [SerializeField] private bool foldJobs;
        [SerializeField] private Vector2 scroll;

        [NonSerialized] private IconCapEffectStackGUI captureStack;
        [NonSerialized] private IconCapEffectStackGUI generatorStack;
        [NonSerialized] private RenderTexture preview;
        [NonSerialized] private string previewProblem;
        [NonSerialized] private readonly HashSet<string> loggedPreviewProblems = new HashSet<string>();
        [NonSerialized] private int previewRequest;
        [NonSerialized] private double previewTime;
        [NonSerialized] private Vector3 viewPosition;
        [NonSerialized] private Quaternion viewRotation;
        [NonSerialized] private readonly List<RenderTexture> generatorPreviews = new List<RenderTexture>();
        [NonSerialized] private string generatorPreviewKey;
        [NonSerialized] private int generatorRequest;
        [NonSerialized] private List<string> presetNames;
        [NonSerialized] private List<string> jobFolders;
        [NonSerialized] private readonly Dictionary<string, IconCapJobFile> jobFiles = new Dictionary<string, IconCapJobFile>();
        [NonSerialized] private readonly HashSet<string> openJobs = new HashSet<string>();

        internal static void Open()
        {
            IconCapWindow window = GetWindow<IconCapWindow>("IconCap");
            window.minSize = new Vector2(360f, 420f);
            window.Show();
        }

        /// <summary>The Main Menu's Tools tab: IconCap in its own floating window.</summary>
        internal static void OpenPopup()
        {
            IconCapWindow window = GetWindow<IconCapWindow>(true, "IconCap");
            window.minSize = new Vector2(360f, 420f);
            window.Show();
        }

        private const string SettingsUndo = "IconCap settings";

        /// <summary>
        /// The window's state that is not a capture setting. Undo records the whole window, so after any Undo or Redo
        /// these are put back as they were (OnUndoRedo): Ctrl+Z of a setting never brings back old captures, exports,
        /// the export folder, the generator or the other fields.
        /// </summary>
        private sealed class Kept
        {
            public List<IconCapEntry> Session;
            public int Latest, JobChoice;
            public string LastSavedPath, ExportFolder, PresetName;
            public IconCapEntry LastCapture, LastSave;
            public IconGenerator ThroughGenerator, Generator;
            public bool LargePreviews, FoldEffects, FoldSession, FoldObjects, FoldCamera, FoldBackground, FoldOptions, FoldRestore,
                FoldGenerator, FoldGeneratorStack, FoldJobs;
            public Vector2 Scroll;
        }

        [NonSerialized] private Kept kept;

        // Called after every draw and every action that changes that state.
        private void Keep()
        {
            kept = new Kept
            {
                Session = session, Latest = latest, JobChoice = jobChoice, LastSavedPath = lastSavedPath, ExportFolder = exportFolder,
                PresetName = presetName, LastCapture = lastCapture, LastSave = lastSave, ThroughGenerator = throughGenerator,
                Generator = generator, LargePreviews = largePreviews, FoldEffects = foldEffects, FoldSession = foldSession,
                FoldObjects = foldObjects, FoldCamera = foldCamera, FoldBackground = foldBackground, FoldOptions = foldOptions,
                FoldRestore = foldRestore, FoldGenerator = foldGenerator, FoldGeneratorStack = foldGeneratorStack, FoldJobs = foldJobs,
                Scroll = scroll
            };
        }

        private void OnUndoRedo()
        {
            if (kept != null)
            {
                session = kept.Session;
                latest = kept.Latest;
                jobChoice = kept.JobChoice;
                lastSavedPath = kept.LastSavedPath;
                exportFolder = kept.ExportFolder;
                presetName = kept.PresetName;
                lastCapture = kept.LastCapture;
                lastSave = kept.LastSave;
                throughGenerator = kept.ThroughGenerator;
                generator = kept.Generator;
                largePreviews = kept.LargePreviews;
                foldEffects = kept.FoldEffects;
                foldSession = kept.FoldSession;
                foldObjects = kept.FoldObjects;
                foldCamera = kept.FoldCamera;
                foldBackground = kept.FoldBackground;
                foldOptions = kept.FoldOptions;
                foldRestore = kept.FoldRestore;
                foldGenerator = kept.FoldGenerator;
                foldGeneratorStack = kept.FoldGeneratorStack;
                foldJobs = kept.FoldJobs;
                scroll = kept.Scroll;
            }
            Repaint();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            Keep();
            EditorApplication.update += Watch;
            if (slots.TrueForAll(slot => slot == null)) SnapToSelection();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= Watch;
            ReleasePreviews();
        }

        private void OnSelectionChange()
        {
            SnapToSelection();
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnFocus()
        {
            ForgetFiles();
        }

        private void OnProjectChange()
        {
            ForgetFiles();
        }

        private void ForgetFiles()
        {
            presetNames = null;
            jobFolders = null;
            jobFiles.Clear();
        }

        // ---- Live preview -------------------------------------------------------

        // Renders what the last drawing asked for, at most every PreviewInterval, and
        // repaints when the Scene View camera moves. Nothing is asked for while hidden.
        private void Watch()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && view.camera != null &&
                (view.camera.transform.position != viewPosition || view.camera.transform.rotation != viewRotation))
            {
                viewPosition = view.camera.transform.position;
                viewRotation = view.camera.transform.rotation;
                Repaint();
            }
            if (previewRequest > 0 && EditorApplication.timeSinceStartup - previewTime >= PreviewInterval)
            {
                int side = previewRequest;
                previewRequest = 0;
                previewTime = EditorApplication.timeSinceStartup;
                RenderPreview(side);
                Repaint();
            }
            if (generatorRequest > 0)
            {
                int side = generatorRequest;
                generatorRequest = 0;
                RenderGeneratorPreviews(side);
                Repaint();
            }
        }

        private void RenderPreview(int side)
        {
            string problem;
            Camera view = ViewCamera(out problem);
            List<Renderer> targets = problem == null ? CurrentTargets(out problem) : null;
            previewProblem = problem;
            if (targets == null) return;
            IconCapSettings snapshot = Snapshot(view);
            int size = Mathf.Clamp(Mathf.Min(side, snapshot.Size), 16, PreviewMaxPixels);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture rendered = IconCapRenderer.Render(snapshot, targets, size, session.Count);
                if (throughGenerator != null)
                {
                    RenderTexture through = IconCapEffects.Apply(rendered, throughGenerator.Stack, session.Count, size / (float)snapshot.Size);
                    RenderTexture.ReleaseTemporary(rendered);
                    rendered = through;
                }
                if (preview == null || preview.width != size)
                {
                    if (preview != null) DestroyImmediate(preview);
                    preview = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                Graphics.Blit(rendered, preview);
                RenderTexture.ReleaseTemporary(rendered);
            }
            catch (Exception exception)
            {
                // The preview redraws constantly: each distinct problem is logged once, with its stack.
                if (exception.Message != previewProblem && loggedPreviewProblems.Add(exception.Message))
                    RiryLog.Exception(RiryLog.IconCap, "The preview could not be rendered", exception);
                previewProblem = exception.Message;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private void ReleasePreviews()
        {
            if (preview != null) DestroyImmediate(preview);
            preview = null;
            foreach (RenderTexture texture in generatorPreviews)
            {
                if (texture != null) DestroyImmediate(texture);
            }
            generatorPreviews.Clear();
            generatorPreviewKey = null;
        }

        // ---- Drawing --------------------------------------------------------------

        private void OnGUI()
        {
            if (captureStack == null) captureStack = new IconCapEffectStackGUI();
            if (generatorStack == null) generatorStack = new IconCapEffectStackGUI();
            // Every change to the capture settings (Size, preset, effect stack, camera, background, options) in this
            // draw is one Undo step: the settings are put back as they were, recorded, then set again.
            string settingsBefore = JsonUtility.ToJson(settings);
            try
            {
                DrawWindow();
            }
            finally
            {
                string settingsAfter = JsonUtility.ToJson(settings);
                if (settingsAfter != settingsBefore)
                {
                    JsonUtility.FromJsonOverwrite(settingsBefore, settings);
                    Undo.RecordObject(this, SettingsUndo);
                    JsonUtility.FromJsonOverwrite(settingsAfter, settings);
                }
                Keep();
            }
        }

        private void DrawWindow()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawTop();
            float width = EditorGUIUtility.currentViewWidth;
            if (width >= WideWidth)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(width * 0.5f)))
                    {
                        DrawPreview(width * 0.5f - 16f);
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawCaptureStack();
                    }
                }
            }
            else
            {
                DrawPreview(width - 24f);
                DrawCaptureStack();
            }
            DrawExport();
            DrawObjects();
            DrawCamera();
            DrawBackground();
            DrawOptions();
            DrawRestore();
            DrawGenerator();
            DrawJobs();
            EditorGUILayout.EndScrollView();
        }

        private void DrawTop()
        {
            if (presetNames == null) presetNames = IconCapFiles.PresetNames();
            using (new EditorGUILayout.HorizontalScope())
            {
                var options = new List<string> { "None" };
                options.AddRange(presetNames);
                int current = presetNames.IndexOf(presetName) + 1;
                int picked = EditorGUILayout.Popup("Preset", current, options.ToArray());
                if (picked != current)
                {
                    presetName = picked == 0 ? "" : presetNames[picked - 1];
                    if (picked > 0) ApplyPreset(IconCapFiles.LoadPreset(presetName));
                }
                if (GUILayout.Button("Save a preset", GUILayout.Width(100f))) EditorApplication.delayCall += SavePreset;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                settings.Size = EditorGUILayout.IntSlider("Size", settings.Size, 16, IconCapRenderer.MaxSize);
                if (GUILayout.Button("Capture", GUILayout.Width(80f))) EditorApplication.delayCall += Capture;
            }
        }

        private void DrawPreview(float width)
        {
            float side = Mathf.Max(64f, Mathf.Min(width, PreviewMaxSide));
            Rect rect = GUILayoutUtility.GetRect(side, side, GUILayout.Width(side), GUILayout.Height(side));
            if (Event.current.type == EventType.Repaint)
            {
                previewRequest = Mathf.RoundToInt(side * EditorGUIUtility.pixelsPerPoint);
                if (preview != null) EditorGUI.DrawTextureTransparent(rect, preview, ScaleMode.ScaleToFit);
                else EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.2f));
            }
            if (previewProblem != null) EditorGUILayout.HelpBox(previewProblem, MessageType.Info);
            throughGenerator = (IconGenerator)EditorGUILayout.ObjectField(new GUIContent("Through generator",
                "Shows the capture with this Icon Generator's effect stack applied too."), throughGenerator, typeof(IconGenerator), false);
        }

        private void DrawCaptureStack()
        {
            foldEffects = EditorGUILayout.Foldout(foldEffects, "Effect stack", true);
            if (foldEffects) captureStack.Draw(settings.Effects, null);
        }

        private void DrawExport()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                exportFolder = EditorGUILayout.TextField("Export", exportFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    string chosen = EditorUtility.OpenFolderPanel("Export folder", IconCapFiles.FullPath(exportFolder), "");
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        exportFolder = IconCapFiles.DisplayFolder(chosen);
                        GUI.FocusControl(null);
                    }
                    RiryGui.ExitAfterModal();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save as PNG")) EditorApplication.delayCall += SaveAsPng;
                using (new EditorGUI.DisabledScope(lastSavedPath.Length == 0 || Latest() == null))
                {
                    if (GUILayout.Button(new GUIContent("Save again", "Overwrites " + lastSavedPath + " with the latest capture.")))
                        EditorApplication.delayCall += SaveAgain;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                foldSession = EditorGUILayout.Foldout(foldSession, "This session", true);
                GUILayout.FlexibleSpace();
                GUILayout.Label(session.Count + (session.Count == 1 ? " icon" : " icons"), EditorStyles.miniLabel);
            }
            if (!foldSession) return;
            EditorGUI.indentLevel++;
            for (int i = 0; i < session.Count; i++)
            {
                IconCapEntry entry = session[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    // MinWidth 0: a long name is cut off (full in the tooltip) instead of pushing Export and Remove out of view.
                    string rowName = entry.FileName + (entry.Exported ? "" : " (not exported)");
                    if (GUILayout.Button(new GUIContent(rowName,
                            rowName + "\nMoves the Scene View camera back to this capture and reselects its objects."), EditorStyles.label,
                            GUILayout.MinWidth(0f)))
                    {
                        RestoreCamera(entry);
                        Reselect(entry);
                    }
                    GUILayout.FlexibleSpace();
                    int index = i;
                    if (GUILayout.Button(new GUIContent("Export", "Saves this image to the export folder."), EditorStyles.miniButton, GUILayout.Width(55f)))
                        EditorApplication.delayCall += () => Export(index);
                    using (new EditorGUI.DisabledScope(entry.Exported))
                    {
                        if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60f)))
                        {
                            RemoveAt(index);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
            DrawJobButtons();
            EditorGUI.indentLevel--;
        }

        private void DrawJobButtons()
        {
            string folder = IconCapFiles.FullPath(exportFolder);
            IconCapJobFile jobs = Jobs(folder);
            bool any = SavedHere(folder).Count > 0;
            var tip = any ? "" : "Save icons into the export folder first.";
            // The Popup's label is already indented by indentLevel, so no extra space before it.
            using (new EditorGUILayout.HorizontalScope())
            {
                if (jobs.Jobs.Count > 0)
                {
                    var names = new string[jobs.Jobs.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = jobs.Jobs[i].Name;
                    jobChoice = Mathf.Clamp(jobChoice, 0, names.Length - 1);
                    jobChoice = EditorGUILayout.Popup("Job", jobChoice, names);
                    if (EditorGUIUtility.currentViewWidth < JobRow)
                    {
                        // Narrow: the buttons on their own line.
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                    }
                    using (new EditorGUI.DisabledScope(!any))
                    {
                        if (GUILayout.Button(new GUIContent("Add to job", tip), GUILayout.Width(80f)))
                            AddToJob(folder, jobs, jobs.Jobs[jobChoice]);
                    }
                }
                else GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!any))
                {
                    if (GUILayout.Button(new GUIContent("Save job", tip), GUILayout.Width(80f))) EditorApplication.delayCall += () => SaveJob(folder);
                }
            }
        }

        private void DrawObjects()
        {
            foldObjects = EditorGUILayout.Foldout(foldObjects, "Objects", true);
            if (!foldObjects) return;
            EditorGUI.indentLevel++;
            onlySelected = RiryGui.Set(this, SettingsUndo, onlySelected, EditorGUILayout.ToggleLeft("Capture only selected objects", onlySelected));
            if (onlySelected)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i <= slots.Count; i++)
                {
                    SkinnedMeshRenderer current = i < slots.Count ? slots[i] : null;
                    var chosen = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(i == 0 ? "Objects" : " ", current, typeof(SkinnedMeshRenderer), true);
                    if (chosen == current) continue;
                    Undo.RecordObject(this, SettingsUndo);
                    if (i == slots.Count)
                    {
                        if (chosen != null) slots.Add(chosen);
                    }
                    else if (chosen == null) slots.RemoveAt(i);
                    else slots[i] = chosen;
                    break;
                }
                EditorGUI.indentLevel--;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                isolated = RiryGui.Set(this, SettingsUndo, isolated, (GameObject)EditorGUILayout.ObjectField(new GUIContent("Isolated item", "Only this item and its children are captured."),
                    isolated, typeof(GameObject), true));
                using (new EditorGUI.DisabledScope(isolated == null))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(50f))) isolated = RiryGui.Set(this, SettingsUndo, isolated, (GameObject)null);
                }
            }
            if (isolated != null && EditorUtility.IsPersistent(isolated)) isolated = null;
            EditorGUI.indentLevel--;
        }

        private void DrawCamera()
        {
            foldCamera = EditorGUILayout.Foldout(foldCamera, "Camera", true);
            if (!foldCamera) return;
            IconCapCamera camera = settings.Camera;
            EditorGUI.indentLevel++;
            camera.FovMode = (IconCapFov)EditorGUILayout.Popup("Field of view", (int)camera.FovMode, FovNames);
            if (camera.FovMode == IconCapFov.Custom) camera.Fov = EditorGUILayout.Slider(" ", camera.Fov, 1f, 179f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Far clip", "From 0.01 to 1,000,000."));
                float exponent = GUILayout.HorizontalSlider(Mathf.Log10(Mathf.Clamp(camera.FarClip, 0.01f, 1000000f)), -2f, 6f);
                if (!Mathf.Approximately(exponent, Mathf.Log10(Mathf.Clamp(camera.FarClip, 0.01f, 1000000f))))
                    camera.FarClip = (float)Math.Round(Mathf.Pow(10f, exponent), 3);
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                camera.FarClip = Mathf.Clamp(EditorGUILayout.FloatField(camera.FarClip, GUILayout.Width(80f)), 0.01f, 1000000f);
                EditorGUI.indentLevel = indent;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                camera.Roll = EditorGUILayout.Slider("Camera roll", camera.Roll, -180f, 180f);
                if (GUILayout.Button("Reset", GUILayout.Width(50f))) camera.Roll = 0f;
            }
            EditorGUI.indentLevel--;
        }

        private void DrawBackground()
        {
            foldBackground = EditorGUILayout.Foldout(foldBackground, "Background", true);
            if (!foldBackground) return;
            IconCapBackgroundSettings background = settings.Background;
            EditorGUI.indentLevel++;
            background.Mode = (IconCapBackground)EditorGUILayout.Popup("Mode", (int)background.Mode, BackgroundNames);
            if (background.Mode == IconCapBackground.Opaque)
                background.Skybox = EditorGUILayout.Toggle(new GUIContent("Skybox", "The skybox instead of a colour."), background.Skybox);
            if (background.Mode != IconCapBackground.Transparent && !(background.Mode == IconCapBackground.Opaque && background.Skybox))
                background.Colour = EditorGUILayout.ColorField(new GUIContent("Colour"), background.Colour, true, false, false);
            EditorGUI.indentLevel--;
        }

        private void DrawOptions()
        {
            foldOptions = EditorGUILayout.Foldout(foldOptions, "Capture options", true);
            if (!foldOptions) return;
            IconCapOptions options = settings.Options;
            EditorGUI.indentLevel++;
            options.AlphaCutout = EditorGUILayout.Toggle(new GUIContent("Alpha cutout",
                "Materials with alpha clipping are cut by their own cutoff in the alpha. Materials without it are solid."), options.AlphaCutout);
            options.Lighting = (IconCapLighting)EditorGUILayout.Popup(new GUIContent("Lighting",
                "Scene: no lighting override. HDRI: lit by an HDR environment. Flat: base colour, unlit."), (int)options.Lighting, LightingNames);
            if (options.Lighting == IconCapLighting.Hdri)
            {
                var cube = (Cubemap)EditorGUILayout.ObjectField(new GUIContent("HDRI", "An HDR image imported with Texture Shape: Cube."),
                    IconCapAssets.Load<Cubemap>(options.Hdri), typeof(Cubemap), false);
                options.Hdri = IconCapAssets.Guid(cube);
            }
            using (new EditorGUI.DisabledScope(options.Lighting == IconCapLighting.Flat))
            {
                EditorGUILayout.LabelField("Exclude");
                EditorGUI.indentLevel++;
                options.ExcludeGlobal = EditorGUILayout.ToggleLeft("Global lights (directional)", options.ExcludeGlobal);
                options.ExcludeScene = EditorGUILayout.ToggleLeft("Scene lights (point and spot)", options.ExcludeScene);
                options.ExcludeWorld = EditorGUILayout.ToggleLeft("World lights (ambient and environment)", options.ExcludeWorld);
                options.ExcludeReflections = EditorGUILayout.ToggleLeft("Reflections", options.ExcludeReflections);
                options.ExcludeBaked = EditorGUILayout.ToggleLeft("Baked lighting", options.ExcludeBaked);
                options.ExcludeShadows = EditorGUILayout.ToggleLeft("Shadows", options.ExcludeShadows);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        private void DrawRestore()
        {
            foldRestore = EditorGUILayout.Foldout(foldRestore, "Restore", true);
            if (!foldRestore) return;
            EditorGUI.indentLevel++;
            DrawRestoreRow("Last capture", lastCapture);
            DrawRestoreRow("Last save", lastSave);
            EditorGUI.indentLevel--;
        }

        private void DrawRestoreRow(string label, IconCapEntry entry)
        {
            bool has = entry != null && entry.FileName.Length > 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));
                if (EditorGUIUtility.currentViewWidth < RestoreRow)
                {
                    // Narrow: the buttons on their own line.
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                }
                using (new EditorGUI.DisabledScope(!has))
                {
                    if (GUILayout.Button("Restore camera")) RestoreCamera(entry);
                    if (GUILayout.Button("Reselect objects")) Reselect(entry);
                }
            }
        }

        private void DrawGenerator()
        {
            foldGenerator = EditorGUILayout.Foldout(foldGenerator, "Icon Generator", true);
            if (!foldGenerator) return;
            EditorGUI.indentLevel++;
            using (new EditorGUILayout.HorizontalScope())
            {
                generator = (IconGenerator)EditorGUILayout.ObjectField("Asset", generator, typeof(IconGenerator), false);
                if (GUILayout.Button(new GUIContent("New", "Creates an Icon Generator asset."), GUILayout.Width(45f)))
                    EditorApplication.delayCall += NewGenerator;
                using (new EditorGUI.DisabledScope(generator == null))
                {
                    if (GUILayout.Button(new GUIContent("Regenerate", "Remakes every output beside the asset. Never replaces a file it did not make. " +
                        "Undo can't revert the PNG files."), GUILayout.Width(85f)))
                        EditorApplication.delayCall += Regenerate;
                }
            }
            if (generator == null)
            {
                EditorGUI.indentLevel--;
                return;
            }

            Undo.RecordObject(generator, "Edit Icon Generator");
            EditorGUI.BeginChangeCheck();
            largePreviews = EditorGUILayout.Toggle("Large previews", largePreviews);
            DrawSources();
            if (EditorGUIUtility.currentViewWidth >= PrefixSuffixRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    generator.Prefix = EditorGUILayout.TextField("Prefix", generator.Prefix);
                    generator.Suffix = EditorGUILayout.TextField("Suffix", generator.Suffix);
                }
            }
            else
            {
                generator.Prefix = EditorGUILayout.TextField("Prefix", generator.Prefix);
                generator.Suffix = EditorGUILayout.TextField("Suffix", generator.Suffix);
            }
            var template = (IconGenerator)EditorGUILayout.ObjectField(new GUIContent("Template",
                "Uses this generator's effect stack instead of its own."), generator.Template, typeof(IconGenerator), false);
            generator.Template = template == generator ? null : template;
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(generator);

            foldGeneratorStack = EditorGUILayout.Foldout(foldGeneratorStack, "Generator effect stack", true);
            if (foldGeneratorStack)
            {
                if (generator.Template != null)
                    EditorGUILayout.HelpBox("This generator uses " + generator.Template.name + "'s effect stack.", MessageType.Info);
                else generatorStack.Draw(generator.Effects, generator);
            }
            EditorGUI.indentLevel--;
        }

        // Each source next to its result, as many to a row as fit; the empty field adds a source.
        private void DrawSources()
        {
            float thumbnail = largePreviews ? LargeThumbnail : SmallThumbnail;
            // Source field, result and the 12 px gap, plus the fields' margins and a buffer.
            float cell = thumbnail * 2f + 12f + 12f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 40f) / cell));
            List<IconGenerator.Source> sources = generator.Sources;
            sources.RemoveAll(source => source == null || source.Texture == null);
            string key = GeneratorKey(thumbnail);
            if (Event.current.type == EventType.Repaint && key != generatorPreviewKey)
                generatorRequest = Mathf.RoundToInt(thumbnail * EditorGUIUtility.pixelsPerPoint);

            for (int start = 0; start <= sources.Count; start += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    for (int i = start; i < Mathf.Min(start + perRow, sources.Count + 1); i++)
                    {
                        if (i == sources.Count)
                        {
                            var added = (Texture2D)EditorGUILayout.ObjectField(GUIContent.none, null, typeof(Texture2D), false,
                                GUILayout.Width(thumbnail), GUILayout.Height(thumbnail));
                            if (added != null) sources.Add(new IconGenerator.Source { Texture = added });
                            GUILayout.Label("+ Add", EditorStyles.miniLabel);
                            break;
                        }
                        var changed = (Texture2D)EditorGUILayout.ObjectField(GUIContent.none, sources[i].Texture, typeof(Texture2D), false,
                            GUILayout.Width(thumbnail), GUILayout.Height(thumbnail));
                        if (changed != sources[i].Texture) sources[i].Texture = changed;
                        Rect result = GUILayoutUtility.GetRect(thumbnail, thumbnail, GUILayout.Width(thumbnail), GUILayout.Height(thumbnail));
                        if (Event.current.type == EventType.Repaint)
                        {
                            RenderTexture texture = i < generatorPreviews.Count ? generatorPreviews[i] : null;
                            if (texture != null) EditorGUI.DrawTextureTransparent(result, texture, ScaleMode.ScaleToFit);
                            else EditorGUI.DrawRect(result, new Color(0f, 0f, 0f, 0.2f));
                        }
                        GUILayout.Space(12f);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private string GeneratorKey(float thumbnail)
        {
            string key = thumbnail + "|" + EditorJsonUtility.ToJson(generator);
            var seen = new HashSet<IconGenerator>();
            for (IconGenerator template = generator.Template; template != null && seen.Add(template); template = template.Template)
                key += "|" + EditorJsonUtility.ToJson(template);
            return key;
        }

        private void RenderGeneratorPreviews(int size)
        {
            if (generator == null) return;
            foreach (RenderTexture texture in generatorPreviews)
            {
                if (texture != null) DestroyImmediate(texture);
            }
            generatorPreviews.Clear();
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < generator.Sources.Count; i++)
                {
                    Texture2D source = generator.Sources[i] != null ? generator.Sources[i].Texture : null;
                    if (source == null)
                    {
                        generatorPreviews.Add(null);
                        continue;
                    }
                    RenderTexture made = generator.Generate(source, i, size);
                    var kept = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    Graphics.Blit(made, kept);
                    RenderTexture.ReleaseTemporary(made);
                    generatorPreviews.Add(kept);
                }
            }
            finally
            {
                RenderTexture.active = previous;
            }
            generatorPreviewKey = GeneratorKey(largePreviews ? LargeThumbnail : SmallThumbnail);
        }

        private void DrawJobs()
        {
            foldJobs = EditorGUILayout.Foldout(foldJobs, "Jobs", true);
            if (!foldJobs) return;
            if (jobFolders == null) jobFolders = IconCapFiles.JobFolders(IconCapFiles.FullPath(exportFolder));
            EditorGUI.indentLevel++;
            if (jobFolders.Count == 0) EditorGUILayout.LabelField("No jobs yet.", EditorStyles.miniLabel);
            foreach (string folder in jobFolders)
            {
                IconCapJobFile file = Jobs(folder);
                foreach (IconCapJob job in file.Jobs)
                {
                    string key = folder + "|" + job.Name;
                    string folderName = IconCapFiles.DisplayFolder(folder);
                    string countText = job.Entries.Count + (job.Entries.Count == 1 ? " icon" : " icons");
                    // The foldout as wide as its name, so the name never runs under the folder label.
                    float nameWidth = EditorGUI.indentLevel * 15f + 16f + EditorStyles.foldout.CalcSize(new GUIContent(job.Name)).x + 4f;
                    float rowWidth = nameWidth + EditorStyles.miniLabel.CalcSize(new GUIContent(folderName)).x +
                        EditorStyles.miniLabel.CalcSize(new GUIContent(countText)).x + 50f + 24f;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect nameRect = GUILayoutUtility.GetRect(nameWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(nameWidth));
                        bool open = EditorGUI.Foldout(nameRect, openJobs.Contains(key), job.Name, true);
                        if (open) openJobs.Add(key);
                        else openJobs.Remove(key);
                        if (rowWidth > EditorGUIUtility.currentViewWidth - RowMargin)
                        {
                            // Too narrow: the folder, count and Run on the next line.
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space((EditorGUI.indentLevel + 1) * 15f);
                        }
                        GUILayout.Label(new GUIContent(folderName, folderName), EditorStyles.miniLabel, GUILayout.MinWidth(0f));
                        GUILayout.Label(countText, EditorStyles.miniLabel);
                        string runFolder = folder;
                        IconCapJob runJob = job;
                        if (GUILayout.Button(new GUIContent("Run", "Remakes every icon; the files replaced are moved into a new OLD_<n> folder."),
                                GUILayout.Width(50f)))
                            EditorApplication.delayCall += () => Run(runFolder, runJob);
                    }
                    if (!openJobs.Contains(key)) continue;
                    EditorGUI.indentLevel++;
                    foreach (IconCapEntry entry in job.Entries)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(new GUIContent(entry.FileName, entry.FileName), GUILayout.MinWidth(0f));
                            if (GUILayout.Button("Restore camera", GUILayout.Width(110f))) RestoreCamera(entry);
                            if (GUILayout.Button("Reselect objects", GUILayout.Width(115f))) Reselect(entry);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
        }

        // ---- Capturing and saving -------------------------------------------------

        private static Camera ViewCamera(out string problem)
        {
            SceneView view = SceneView.lastActiveSceneView;
            problem = view == null || view.camera == null ? "Open a Scene View to capture from." : null;
            return problem == null ? view.camera : null;
        }

        private List<Renderer> CurrentTargets(out string problem)
        {
            var selected = new List<Renderer>();
            foreach (SkinnedMeshRenderer slot in slots)
            {
                if (slot != null) selected.Add(slot);
            }
            return IconCapRenderer.Targets(onlySelected, selected, isolated, out problem);
        }

        private IconCapObjects CurrentObjects()
        {
            var objects = new IconCapObjects { OnlySelected = onlySelected, Isolated = IconCapAssets.SceneId(isolated) };
            foreach (SkinnedMeshRenderer slot in slots)
            {
                if (slot != null) objects.Renderers.Add(IconCapAssets.SceneId(slot));
            }
            return objects;
        }

        private IconCapSettings Snapshot(Camera view)
        {
            IconCapSettings snapshot = IconCapAssets.Copy(settings);
            snapshot.Camera.TakeView(view);
            return snapshot;
        }

        // The selected object when at most one slot is filled; with more, the slots stay.
        private void SnapToSelection()
        {
            int filled = 0;
            foreach (SkinnedMeshRenderer slot in slots)
            {
                if (slot != null) filled++;
            }
            if (filled > 1) return;
            GameObject active = Selection.activeGameObject;
            if (active == null || EditorUtility.IsPersistent(active)) return;
            SkinnedMeshRenderer renderer = active.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null) renderer = active.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null) return;
            slots.RemoveAll(slot => slot == null);
            if (slots.Count == 0) slots.Add(renderer);
            else slots[0] = renderer;
        }

        private string NewName()
        {
            string name = isolated != null ? isolated.name : null;
            if (name == null)
            {
                SkinnedMeshRenderer first = slots.Find(slot => slot != null);
                name = onlySelected && first != null ? first.name : "Icon";
            }
            string file = IconCapFiles.PngName(name);
            for (int number = 2; session.Exists(entry => string.Equals(entry.FileName, file, StringComparison.OrdinalIgnoreCase)); number++)
                file = IconCapFiles.PngName(name + " " + number);
            return file;
        }

        private IconCapEntry Latest()
        {
            return latest >= 0 && latest < session.Count ? session[latest] : null;
        }

        private void Capture()
        {
            string problem;
            Camera view = ViewCamera(out problem);
            List<Renderer> targets = problem == null ? CurrentTargets(out problem) : null;
            if (targets == null)
            {
                EditorUtility.DisplayDialog("IconCap", problem, "OK");
                return;
            }
            var entry = new IconCapEntry { FileName = NewName(), Settings = Snapshot(view), Objects = CurrentObjects() };
            RenderTexture image = IconCapRenderer.Render(entry.Settings, targets, entry.Settings.Size, session.Count);
            entry.Image = IconCapEffects.Read(image);
            RenderTexture.ReleaseTemporary(image);
            session.Add(entry);
            latest = session.Count - 1;
            lastCapture = IconCapAssets.Copy(entry);
            Keep();
            Repaint();
        }

        // The entry's image; made again from its settings when this session no longer has it.
        private Texture2D ImageOf(IconCapEntry entry, int index)
        {
            if (entry.Image != null) return entry.Image;
            string problem;
            List<Renderer> targets = IconCapRenderer.Targets(entry.Objects, out problem);
            if (targets == null)
            {
                EditorUtility.DisplayDialog("IconCap", entry.FileName + ": " + problem, "OK");
                return null;
            }
            RenderTexture image = IconCapRenderer.Render(entry.Settings, targets, entry.Settings.Size, index);
            entry.Image = IconCapEffects.Read(image);
            RenderTexture.ReleaseTemporary(image);
            return entry.Image;
        }

        private void SaveAsPng()
        {
            if (Latest() == null) Capture();
            IconCapEntry entry = Latest();
            if (entry == null) return;
            string folder = IconCapFiles.FullPath(exportFolder);
            Directory.CreateDirectory(folder);
            string path = EditorUtility.SaveFilePanel("Save as PNG", folder, entry.FileName, "png");
            if (string.IsNullOrEmpty(path)) return;
            Save(entry, latest, Path.GetFullPath(path));
        }

        private void SaveAgain()
        {
            IconCapEntry entry = Latest();
            if (entry != null && lastSavedPath.Length > 0) Save(entry, latest, lastSavedPath);
        }

        private void Export(int index)
        {
            if (index < 0 || index >= session.Count) return;
            IconCapEntry entry = session[index];
            string path = Path.Combine(IconCapFiles.FullPath(exportFolder), entry.FileName);
            if (File.Exists(path) && !SamePath(entry.SavedPath, path) &&
                !EditorUtility.DisplayDialog("IconCap", path + " exists. Replace it?", "Replace", "Cancel"))
                return;
            Save(entry, index, path);
        }

        // Writes the entry's image to path; any other entry saved there is no longer exported.
        private void Save(IconCapEntry entry, int index, string path)
        {
            Texture2D image = ImageOf(entry, index);
            if (image == null) return;
            IconCapFiles.WritePng(image, path);
            foreach (IconCapEntry other in session)
            {
                if (other != entry && SamePath(other.SavedPath, path)) other.SavedPath = "";
            }
            entry.SavedPath = path;
            entry.FileName = Path.GetFileName(path);
            lastSavedPath = path;
            lastSave = IconCapAssets.Copy(entry);
            jobFiles.Clear();
            Keep();
            Repaint();
        }

        private void RemoveAt(int index)
        {
            if (index < 0 || index >= session.Count || session[index].Exported) return;
            if (session[index].Image != null) DestroyImmediate(session[index].Image);
            session.RemoveAt(index);
            if (latest == index) latest = session.Count - 1;
            else if (latest > index) latest--;
            Keep();
        }

        private static bool SamePath(string left, string right)
        {
            return !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) &&
                string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        // ---- Jobs and presets -----------------------------------------------------

        private IconCapJobFile Jobs(string folder)
        {
            IconCapJobFile file;
            if (!jobFiles.TryGetValue(folder, out file))
            {
                file = IconCapFiles.ReadJobs(folder);
                jobFiles[folder] = file;
            }
            return file;
        }

        // This session's captures saved in the folder, as job entries.
        private List<IconCapEntry> SavedHere(string folder)
        {
            var saved = new List<IconCapEntry>();
            foreach (IconCapEntry entry in session)
            {
                if (!entry.Exported || !SamePath(Path.GetDirectoryName(entry.SavedPath), folder)) continue;
                IconCapEntry copy = IconCapAssets.Copy(entry);
                copy.SavedPath = "";
                saved.Add(copy);
            }
            return saved;
        }

        private void AddToJob(string folder, IconCapJobFile file, IconCapJob job)
        {
            foreach (IconCapEntry entry in SavedHere(folder))
            {
                job.Entries.RemoveAll(other => string.Equals(other.FileName, entry.FileName, StringComparison.OrdinalIgnoreCase));
                job.Entries.Add(entry);
            }
            IconCapFiles.WriteJobs(folder, file);
            jobFolders = null;
        }

        private void SaveJob(string folder)
        {
            string name = IconCapNamePrompt.Ask("Save job", "Job name", "");
            if (name == null) return;
            IconCapJobFile file = IconCapFiles.ReadJobs(folder);
            if (file.Jobs.Exists(job => string.Equals(job.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                EditorUtility.DisplayDialog("IconCap", "This folder already has a job named " + name + ".", "OK");
                return;
            }
            file.Jobs.Add(new IconCapJob { Name = name, Entries = SavedHere(folder) });
            IconCapFiles.WriteJobs(folder, file);
            jobChoice = file.Jobs.Count - 1;
            Keep();
            ForgetFiles();
            Repaint();
        }

        private void Run(string folder, IconCapJob job)
        {
            // Replaced images move to OLD_<n>, but the new ones take over their GUIDs, so whatever used them shows the new icon.
            List<string> replaced = IconCapFiles.ExistingIcons(folder, job);
            if (replaced.Count > 0 && !EditorUtility.DisplayDialog("IconCap", "Run replaces " + replaced.Count + " existing image(s):\n" +
                    string.Join("\n", replaced.GetRange(0, Math.Min(10, replaced.Count))) +
                    (replaced.Count > 10 ? "\n... and " + (replaced.Count - 10) + " more" : "") +
                    "\n\nThe old images move into a new " + IconCapFiles.OldFolderPrefix + "<n> folder. The new icons keep the old " +
                    "files' GUIDs, so materials and other assets that used those images show the new icons.", "Run", "Cancel"))
                return;
            string problems = IconCapFiles.Run(folder, job);
            if (problems.Length > 0)
            {
                RiryLog.Warn(RiryLog.IconCap, "Job " + job.Name + " in " + folder + ": some icons were not remade:\n" + problems);
                EditorUtility.DisplayDialog("IconCap", "Some icons were not remade:\n\n" + problems, "OK");
            }
            ForgetFiles();
            Repaint();
        }

        private void SavePreset()
        {
            string name = IconCapNamePrompt.Ask("Save a preset", "Preset name", presetName);
            if (name == null) return;
            string existing = IconCapFiles.ExistingPreset(name);
            if (existing != null && !EditorUtility.DisplayDialog("IconCap", "The preset " + existing + " already exists" +
                    (existing != name.Trim() ? " (\"" + name + "\" saves to the same file)" : "") + ". Replace it?", "Replace", "Cancel"))
                return;
            IconCapFiles.SavePreset(name, IconCapAssets.Copy(settings));
            presetName = name;
            presetNames = null;
            Keep();
            Repaint();
        }

        // A preset's settings; the view stays where it is.
        private void ApplyPreset(IconCapSettings preset)
        {
            if (preset == null) return;
            IconCapCamera camera = settings.Camera;
            camera.FovMode = preset.Camera.FovMode;
            camera.Fov = preset.Camera.Fov;
            camera.FarClip = preset.Camera.FarClip;
            camera.Roll = preset.Camera.Roll;
            settings.Size = preset.Size;
            settings.Background = preset.Background ?? new IconCapBackgroundSettings();
            settings.Options = preset.Options ?? new IconCapOptions();
            settings.Effects = preset.Effects ?? new List<IconCapEffect>();
            GUI.FocusControl(null);
        }

        // ---- Icon Generator -------------------------------------------------------

        private void NewGenerator()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Icon Generator", "Icon Generator", "asset",
                "Outputs are saved beside the Icon Generator.");
            if (string.IsNullOrEmpty(path)) return;
            var created = CreateInstance<IconGenerator>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            generator = created;
            Keep();
            Repaint();
        }

        private void Regenerate()
        {
            if (generator == null) return;
            string problems = generator.Regenerate();
            if (problems.Length > 0)
            {
                RiryLog.Warn(RiryLog.IconCap, "Icon Generator " + generator.name + ":\n" + problems, generator);
                EditorUtility.DisplayDialog("Icon Generator", problems, "OK");
            }
            else RiryLog.Info(RiryLog.IconCap, "Icon Generator " + generator.name + " remade its outputs.", generator);
        }

        // ---- Restore --------------------------------------------------------------

        /// <summary>Moves the Scene View camera to where the entry was captured from.</summary>
        private static void RestoreCamera(IconCapEntry entry)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || entry == null) return;
            IconCapCamera camera = entry.Settings.Camera;
            view.orthographic = camera.Orthographic;
            Vector3 forward = camera.Rotation * Vector3.forward;
            if (camera.Orthographic)
            {
                view.LookAtDirect(camera.Position + forward * camera.OrthographicSize, camera.Rotation, camera.OrthographicSize);
            }
            else
            {
                float size = Mathf.Max(0.01f, view.size);
                float distance = size / Mathf.Sin(view.camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                view.LookAtDirect(camera.Position + forward * distance, camera.Rotation, size);
            }
            view.Repaint();
        }

        /// <summary>Selects the entry's isolated item, or its captured objects.</summary>
        private static void Reselect(IconCapEntry entry)
        {
            if (entry == null) return;
            var selection = new List<UnityEngine.Object>();
            var isolatedItem = IconCapAssets.Find<GameObject>(entry.Objects.Isolated);
            if (isolatedItem != null) selection.Add(isolatedItem);
            else
            {
                foreach (string id in entry.Objects.Renderers)
                {
                    var renderer = IconCapAssets.Find<Renderer>(id);
                    if (renderer != null) selection.Add(renderer.gameObject);
                }
            }
            if (selection.Count > 0) Selection.objects = selection.ToArray();
        }
    }

    /// <summary>A small modal prompt for a name (Save job, Save a preset); null when cancelled.</summary>
    internal sealed class IconCapNamePrompt : EditorWindow
    {
        private string label = "";
        private string value = "";
        private bool accepted;

        internal static string Ask(string title, string label, string value)
        {
            var prompt = CreateInstance<IconCapNamePrompt>();
            prompt.titleContent = new GUIContent(title);
            prompt.label = label;
            prompt.value = value ?? "";
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            prompt.position = new Rect(main.center.x - 180f, main.center.y - 40f, 360f, 70f);
            prompt.minSize = prompt.maxSize = new Vector2(360f, 70f);
            prompt.ShowModalUtility();
            return prompt.accepted ? prompt.value.Trim() : null;
        }

        private void OnGUI()
        {
            bool enter = Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName("Name");
            value = EditorGUILayout.TextField(label, value);
            EditorGUI.FocusTextInControl("Name");
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f))) Close();
                using (new EditorGUI.DisabledScope(value.Trim().Length == 0))
                {
                    if ((GUILayout.Button("Save", GUILayout.Width(80f)) || enter) && value.Trim().Length > 0)
                    {
                        accepted = true;
                        Close();
                    }
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: IconCap window
// The menu entry, Tools > RiryTools > IconCap, is in RiryMenus.cs; the Main Menu's Tools tab opens it in its own window.
// ============================================================================
//
// IconCapWindow (this file)
//   Top                 Preset (None, then Documents/RiryTools/iconcap presets), Save a preset, Size (16 to 4096), Capture.
//   Preview             The live capture (Watch renders it while the window is shown, at most every PreviewInterval,
//                       up to PreviewMaxPixels); Through generator adds an Icon Generator's stack. Effect stack beside
//                       it when the window is at least WideWidth wide, else below.
//   Export              Folder (Browse), Save as PNG (a save panel in the folder), Save again (overwrites the last
//                       file saved). This session: each capture (click: Restore camera and Reselect objects), Export,
//                       Remove (not once exported); Job, Add to job, Save job (the captures saved in the folder).
//   Objects             Capture only selected objects with its object slots (SnapToSelection: the selected item while
//                       at most one slot is filled); Isolated item and Clear.
//   Camera              Field of view (Scene View, Custom, 35mm, 50mm, 85mm), Far clip (0.01 to 1,000,000), Camera roll, Reset.
//   Background          Mode (Transparent, Opaque, Transparent with background), Skybox (Opaque), Colour.
//   Capture options     Alpha cutout, Lighting (Scene, HDRI, Flat), HDRI cubemap, Exclude (six switches).
//   Restore             Last capture, Last save: Restore camera, Reselect objects.
//   Icon Generator      Asset, New, Regenerate, Large previews, sources beside their results, Prefix, Suffix,
//                       Template, Generator effect stack.
//   Jobs                Every job in Assets and in the export folder: name, folder, icon count, Run, and its icons.
//   Narrow rows         Below PrefixSuffixRow (460), JobRow (440) and RestoreRow (420) window width, Prefix/Suffix,
//                       the Job row's buttons and Restore's buttons go on a second line. A job's foldout is as wide as
//                       its name; when the row passes the window width less RowMargin (40), its folder, count and Run
//                       go on a second line.
//   Open() / OpenPopup() The Tools menu entry (dockable) / the Main Menu's Tools tab (a floating window).
//   SettingsUndo        "IconCap settings": OnGUI compares the settings' JSON before and after each draw (DrawWindow);
//                       a change is put back, recorded and set again, so Ctrl+Z undoes Size, a loaded preset, the
//                       effect stack, camera, background and options. Capture only selected, the object slots and
//                       Isolated item record the same way (RiryGui.Set). Repaints on Undo/Redo.
//   Kept / Keep() / OnUndoRedo()  Everything else (session, latest, last capture and save, export folder, generators,
//                       job, preset name, folds, scroll), kept after each draw and action and put back after any
//                       Undo/Redo, so Ctrl+Z of a setting never reverts captures or exports.
//   Logging             Run and Regenerate problems go to the Console as well as the dialog; a preview error is logged
//                       once per message (loggedPreviewProblems), with its stack.
//   [SerializeField]    settings, onlySelected, slots, isolated, exportFolder, session, latest, lastSavedPath,
//                       lastCapture, lastSave, throughGenerator, generator, largePreviews, jobChoice, presetName,
//                       the fold states, scroll.
// IconCapNamePrompt (this file): the Save job and Save a preset name prompt.
