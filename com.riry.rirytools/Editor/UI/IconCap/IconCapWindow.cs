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
    internal sealed partial class IconCapWindow : EditorWindow
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
        [SerializeField] private bool foldObjects = true;
        [SerializeField] private bool foldCamera;
        [SerializeField] private bool foldBackground;
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
            public bool LargePreviews, FoldEffects, FoldObjects, FoldCamera, FoldBackground, FoldRestore,
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
                Generator = generator, LargePreviews = largePreviews, FoldEffects = foldEffects,
                FoldObjects = foldObjects, FoldCamera = foldCamera, FoldBackground = foldBackground,
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
                foldObjects = kept.FoldObjects;
                foldCamera = kept.FoldCamera;
                foldBackground = kept.FoldBackground;
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
}

// ============================================================================
// GLOSSARY: IconCap window, state and actions
// IconCapWindow is one partial class: IconCapWindow.cs (state, previews, actions) and IconCapWindow.UI.cs (drawing). The menu entry, Tools > RiryTools > IconCap, is in Editor/UI/RiryToolsMenuBar.cs; the Main Menu's Tools tab opens it in its own window.
// ============================================================================
//
// IconCapWindow (this part)
//   Open() / OpenPopup() The Tools menu entry (dockable) / the Main Menu's Tools tab (a floating window).
//   SettingsUndo        "IconCap settings": OnGUI compares the settings' JSON before and after each draw (DrawWindow);
//                       a change is put back, recorded and set again, so Ctrl+Z undoes Size, a loaded preset, the
//                       effect stack, camera, background and options. Capture only selected, the object slots and
//                       Isolated item record the same way (RiryToolGUI.Set). Repaints on Undo/Redo.
//   Kept / Keep() / OnUndoRedo()  Everything else (session, latest, last capture and save, export folder, generators,
//                       job, preset name, folds, scroll), kept after each draw and action and put back after any
//                       Undo/Redo, so Ctrl+Z of a setting never reverts captures or exports.
//   Logging             Run and Regenerate problems go to the Console as well as the dialog; a preview error is logged
//                       once per message (loggedPreviewProblems), with its stack.
//   [SerializeField]    settings, onlySelected, slots, isolated, exportFolder, session, latest, lastSavedPath,
//                       lastCapture, lastSave, throughGenerator, generator, largePreviews, jobChoice, presetName,
//                       the fold states, scroll.
//   Watch() / RenderPreview(side) / ReleasePreviews()  The live preview.
//   Capture / SaveAsPng / SaveAgain / Export / RemoveAt  Captures and saves (IconCapRenderer, IconCapFiles).
//   Jobs / SavedHere / AddToJob / SaveJob / Run          Jobs in a folder's .iconcap.json (IconCapFiles).
//   SavePreset / ApplyPreset / NewGenerator / Regenerate / RestoreCamera / Reselect  The remaining buttons.
