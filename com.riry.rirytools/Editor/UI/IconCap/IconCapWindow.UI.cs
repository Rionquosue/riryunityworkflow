using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// What IconCap draws. State, previews and actions are in IconCapWindow.cs.
    /// </summary>
    internal sealed partial class IconCapWindow
    {
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
                    RiryToolGUI.ExitAfterModal();
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
        }

        private void DrawObjects()
        {
            foldObjects = EditorGUILayout.Foldout(foldObjects, "Objects", true);
            if (!foldObjects) return;
            EditorGUI.indentLevel++;
            onlySelected = RiryToolGUI.Set(this, SettingsUndo, onlySelected, EditorGUILayout.ToggleLeft("Capture only selected objects", onlySelected));
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
                isolated = RiryToolGUI.Set(this, SettingsUndo, isolated, (GameObject)EditorGUILayout.ObjectField(new GUIContent("Isolated item", "Only this item and its children are captured."),
                    isolated, typeof(GameObject), true));
                using (new EditorGUI.DisabledScope(isolated == null))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(50f))) isolated = RiryToolGUI.Set(this, SettingsUndo, isolated, (GameObject)null);
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
            DrawCaptureOptions();
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

        // The capture options, drawn in Camera.
        private void DrawCaptureOptions()
        {
            IconCapOptions options = settings.Options;
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

        private void DrawJobs()
        {
            foldJobs = EditorGUILayout.Foldout(foldJobs, "Jobs", true);
            if (!foldJobs) return;
            if (jobFolders == null) jobFolders = IconCapFiles.JobFolders(IconCapFiles.FullPath(exportFolder));
            EditorGUI.indentLevel++;
            DrawCurrentJob();
            // Saved jobs: blank when there are none.
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                BlockTitle("Saved jobs:");
                EditorGUI.indentLevel++;
                DrawSavedJobs();
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        private static void BlockTitle(string title)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                GUILayout.Label(title, EditorStyles.boldLabel);
            }
        }

        // Jobs > Current job: this session's captures (click: Restore camera and Reselect objects), Save job on the title row,
        // then the Job popup and Add to job when the folder has jobs.
        private void DrawCurrentJob()
        {
            string folder = IconCapFiles.FullPath(exportFolder);
            IconCapJobFile jobs = Jobs(folder);
            bool any = SavedHere(folder).Count > 0;
            var tip = any ? "" : "Save icons into the export folder first.";
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    GUILayout.Label("Current job:", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(session.Count + (session.Count == 1 ? " icon" : " icons"), EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(!any))
                    {
                        if (GUILayout.Button(new GUIContent("Save job", tip), GUILayout.Width(80f))) EditorApplication.delayCall += () => SaveJob(folder);
                    }
                }
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
                if (jobs.Jobs.Count > 0)
                {
                    // The Popup's label is already indented by indentLevel, so no extra space before it.
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var names = new string[jobs.Jobs.Count];
                        for (int i = 0; i < names.Length; i++) names[i] = jobs.Jobs[i].Name;
                        jobChoice = Mathf.Clamp(jobChoice, 0, names.Length - 1);
                        jobChoice = EditorGUILayout.Popup("Job", jobChoice, names);
                        if (EditorGUIUtility.currentViewWidth < JobRow)
                        {
                            // Narrow: the button on its own line.
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
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSavedJobs()
        {
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
        }
    }
}

// ============================================================================
// GLOSSARY: IconCap window, drawing
// IconCapWindow is one partial class: IconCapWindow.cs (state, previews, actions) and IconCapWindow.UI.cs (drawing). The menu entry, Tools > RiryTools > IconCap, is in Editor/UI/RiryToolsMenuBar.cs; the Main Menu's Tools tab opens it in its own window.
// ============================================================================
//
// IconCapWindow (this part): OnGUI / DrawWindow and one Draw method per block
//   Top                 Preset (None, then Documents/RiryTools/iconcap presets), Save a preset, Size (16 to 4096), Capture.
//   Preview             The live capture (Watch renders it while the window is shown, at most every PreviewInterval,
//                       up to PreviewMaxPixels); Through generator adds an Icon Generator's stack. Effect stack beside
//                       it when the window is at least WideWidth wide, else below.
//   Export              Folder (Browse), Save as PNG (a save panel in the folder), Save again (overwrites the last
//                       file saved).
//   Objects             Capture only selected objects with its object slots (SnapToSelection: the selected item while
//                       at most one slot is filled); Isolated item and Clear.
//   Camera              Field of view (Scene View, Custom, 35mm, 50mm, 85mm), Far clip (0.01 to 1,000,000), Camera roll, Reset,
//                       then the capture options (DrawCaptureOptions): Alpha cutout, Lighting (Scene, HDRI, Flat), HDRI
//                       cubemap, Exclude (six switches).
//   Background          Mode (Transparent, Opaque, Transparent with background), Skybox (Opaque), Colour.
//   Restore             Last capture, Last save: Restore camera, Reselect objects.
//   Icon Generator      Asset, New, Regenerate, Large previews, sources beside their results, Prefix, Suffix,
//                       Template, Generator effect stack.
//   Jobs                Two boxed blocks. Current job (DrawCurrentJob): this session's captures (click: Restore camera and
//                       Reselect objects), Export, Remove (not once exported); Save job on the title row (the captures
//                       saved in the folder); Job popup and Add to job when the folder has jobs. Saved jobs (DrawSavedJobs,
//                       blank when there are none): every job in Assets and in the export folder: name, folder, icon
//                       count, Run, and its icons.
//   Narrow rows         Below PrefixSuffixRow (460), JobRow (440) and RestoreRow (420) window width, Prefix/Suffix,
//                       the Job row's Add to job and Restore's buttons go on a second line. A job's foldout is as wide as
//                       its name; when the row passes the window width less RowMargin (40), its folder, count and Run
//                       go on a second line.
