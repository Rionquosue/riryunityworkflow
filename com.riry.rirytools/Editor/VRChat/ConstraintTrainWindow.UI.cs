#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS with the VRChat Avatars SDK 3.7.0 or newer.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Everything the Constraint Train window draws. Its menu entries are in
    /// RiryVRChatMenus.cs. The state and the actions behind the buttons are in
    /// ConstraintTrainWindow.cs; both files are one class, so methods and fields
    /// can be moved between them freely. A glossary is at the bottom.
    /// </summary>
    internal sealed partial class ConstraintTrainWindow
    {
        internal static void Open()
        {
            GetWindow<ConstraintTrainWindow>("Constraint Train");
        }

        internal static void OpenFromHierarchy()
        {
            ConstraintTrainWindow window = GetWindow<ConstraintTrainWindow>("Constraint Train");
            window.UseSelection(Selection.activeTransform, Selection.transforms);
        }

        private void OnGUI()
        {
            bool blocked = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
            if (blocked)
            {
                StopPreview();
                EditorGUILayout.HelpBox("Unavailable in Play Mode or while compiling.", MessageType.Info);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(blocked))
            {
                EditorGUI.BeginChangeCheck();
                var realBones = new List<Transform>();
                DrawRealChain(realBones);
                EditorGUILayout.Space();
                DrawTargets(realBones);
                EditorGUILayout.Space();
                DrawTiming();
                bool changed = EditorGUI.EndChangeCheck();

                EditorGUILayout.Space();
                DrawOutput();
                EditorGUILayout.Space();
                DrawPreview(changed);
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        private void DrawRealChain(List<Transform> realBones)
        {
            EditorGUILayout.LabelField("Real bone chain", EditorStyles.boldLabel);
            realStart = (Transform)EditorGUILayout.ObjectField("Start (RB1)", realStart, typeof(Transform), true);
            realEnd = (Transform)EditorGUILayout.ObjectField("End", realEnd, typeof(Transform), true);
            if (avatarRoot == null && realStart != null)
                avatarRoot = ConstraintTrainChains.FindAvatarRoot(realStart);
            avatarRoot = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Avatar root", "Animation paths are relative to this Animator. " +
                    "Defaults to the outermost Animator above the chain."),
                avatarRoot, typeof(Transform), true);

            string error;
            if (realStart == null || realEnd == null) return;
            if (!ConstraintTrainChains.TryCollectChain(realStart, realEnd, realBones, out error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }
            var names = new StringBuilder();
            for (int i = 0; i < realBones.Count; i++)
            {
                if (i > 0) names.Append(" > ");
                names.Append(realBones[i].name);
            }
            EditorGUILayout.LabelField(realBones.Count + " bones: " + names, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawTargets(List<Transform> realBones)
        {
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
            int chains = EditorGUILayout.IntSlider("Physics chains", chainCount, 1, ConstraintTrainPlanner.MaxChains);
            if (chains != chainCount)
            {
                // Follow the default split while the user has not chosen another value.
                if (Mathf.Approximately(trainEnd, ConstraintTrainPlanner.DefaultTrainEnd(chainCount)))
                    trainEnd = ConstraintTrainPlanner.DefaultTrainEnd(chains);
                chainCount = chains;
            }
            addEndpoints = EditorGUILayout.Toggle(
                new GUIContent("Add endpoints", "Generated physics chains get an extra end bone that " +
                    "repeats the offset between the last two real bones."), addEndpoints);

            bool canGenerate = realBones.Count > 0 && GenerationError(realBones) == null;
            restStart = DrawChainRow("Rest", restStart, realBones, ConstraintTrainChains.RestPrefix, false,
                canGenerate);
            for (int c = 0; c < chainCount; c++)
            {
                chainStarts[c] = DrawChainRow("Physics chain " + (c + 1), chainStarts[c], realBones,
                    ConstraintTrainChains.ChainPrefix(c), addEndpoints, canGenerate);
            }
            using (new EditorGUI.DisabledScope(!canGenerate))
            {
                if (GUILayout.Button("Generate all missing chains")) GenerateMissing(realBones);
            }
            if (realBones.Count > 0 && !canGenerate)
                EditorGUILayout.HelpBox(GenerationError(realBones), MessageType.Warning);
        }

        private Transform DrawChainRow(string label, Transform start, List<Transform> realBones,
            string prefix, bool endpoint, bool canGenerate)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                start = (Transform)EditorGUILayout.ObjectField(label, start, typeof(Transform), true);
                using (new EditorGUI.DisabledScope(!canGenerate || start != null))
                {
                    if (GUILayout.Button(new GUIContent("Generate", "Create " + prefix + "* empties."),
                        GUILayout.Width(75)))
                        start = Generate(realBones, prefix, endpoint);
                }
            }
            return start;
        }

        private void DrawTiming()
        {
            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                trainEnd = EditorGUILayout.Slider(
                    new GUIContent("Train end", "Value where every bone reaches its physics chain 1 bone."),
                    trainEnd, 0.01f, 1f);
                if (GUILayout.Button("Default", GUILayout.Width(65)))
                    trainEnd = ConstraintTrainPlanner.DefaultTrainEnd(chainCount);
            }
            var unit = new Rect(0, 0, 1, 1);
            stepCurve = EditorGUILayout.CurveField(
                new GUIContent("Train step curve", "Shape of each move from one target to the next."),
                stepCurve, Color.cyan, unit);
            using (new EditorGUI.DisabledScope(chainCount < 2))
            {
                blendCurve = EditorGUILayout.CurveField(
                    new GUIContent("Chain blend curve",
                        "Influence of the next physics chain across each blend. Rescaled to run 0 to 1."),
                    blendCurve, Color.green, unit);
                using (new EditorGUILayout.HorizontalScope())
                {
                    blendMidpoint = EditorGUILayout.Slider(
                        new GUIContent("Next chain at halfway",
                            "Builds a power curve with this influence halfway through a blend."),
                        blendMidpoint, 0.05f, 0.95f);
                    if (GUILayout.Button("Apply", GUILayout.Width(50)))
                        blendCurve = ConstraintTrainPlanner.PowerCurve(
                            ConstraintTrainPlanner.ExponentForMidpoint(blendMidpoint));
                    if (GUILayout.Button("Linear", GUILayout.Width(55)))
                        blendCurve = AnimationCurve.Linear(0, 0, 1, 1);
                }
            }
            string settingsError = ConstraintTrainPlanner.Validate(CurrentSettings(1));
            if (settingsError != null) EditorGUILayout.HelpBox(settingsError, MessageType.Warning);
        }

        // Shown only for a prefab instance in a scene; in Prefab Mode the chains save with the prefab.
        private void DrawPrefabSave()
        {
            if (realStart == null || EditorUtility.IsPersistent(realStart) || !PrefabUtility.IsPartOfPrefabInstance(realStart) ||
                PrefabStageUtility.GetPrefabStage(realStart.gameObject) != null)
                return;
            // The outermost prefab receives the changes, even when the bones come from a model nested in it.
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(realStart);
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (!PrefabUtility.IsPartOfImmutablePrefab(root))
            {
                EditorGUILayout.HelpBox("These bones are in an instance of " + source + ". Generated chains and constraints " +
                    "are overrides in this scene until saved to the prefab.", MessageType.Info);
                if (GUILayout.Button("Save as Prefab Overrides")) SaveToPrefab();
                return;
            }
            EditorGUILayout.HelpBox(source + " is a model or read-only prefab and cannot hold overrides. Saving creates " +
                ConstraintTrainPrefabs.RootPrefabPath(source) + " as a variant of it with these changes, and connects " +
                "this object to it.", MessageType.Info);
            if (GUILayout.Button("Save to " + Path.GetFileName(ConstraintTrainPrefabs.RootPrefabPath(source)))) SaveToPrefab();
        }

        private void DrawOutput()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            clip = (AnimationClip)EditorGUILayout.ObjectField(
                new GUIContent("Clip", "Leave empty to save a new clip. An assigned clip is replaced."),
                clip, typeof(AnimationClip), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set up constraints")) SetUpConstraints();
                if (GUILayout.Button("Build animation")) BuildAnimation();
            }
            DrawPrefabSave();
            EditorGUILayout.LabelField(
                "Use the clip in a state with Motion Time driven by your float parameter.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawPreview(bool settingsChanged)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            float value = EditorGUILayout.Slider("Value", previewValue, 0f, 1f);
            bool valueChanged = !Mathf.Approximately(value, previewValue);
            previewValue = value;

            bool wantPreview = EditorGUILayout.Toggle(
                new GUIContent("Preview in Scene", "Samples the generated weights with Animation Mode. " +
                    "Set up constraints first."), previewing);
            if (wantPreview && !previewing) StartPreview();
            else if (!wantPreview && previewing) StopPreview();
            else if (previewing && settingsChanged) RebuildPreview();
            else if (previewing && valueChanged) SamplePreview();

            Plan plan;
            string error;
            if (!TryGather(out plan, out error))
            {
                EditorGUILayout.HelpBox(error, MessageType.None);
                return;
            }
            EditorGUILayout.LabelField(Readout(plan, previewValue), EditorStyles.wordWrappedMiniLabel);
        }

        private string Readout(Plan plan, float value)
        {
            var text = new StringBuilder();
            for (int bone = 0; bone < plan.Real.Count; bone++)
            {
                var weights = new float[ConstraintTrainPlanner.SourceCount(bone, plan.Settings.ChainCount)];
                ConstraintTrainPlanner.EvaluateWeights(plan.Settings, bone, value, weights);
                text.Append(plan.Real[bone].name).Append(": ");
                bool first = true;
                for (int s = 0; s < weights.Length; s++)
                {
                    if (weights[s] < 0.0005f) continue;
                    if (!first) text.Append(", ");
                    first = false;
                    text.Append(Target(plan, bone, s).name).Append(' ')
                        .Append((weights[s] * 100f).ToString("0.#", CultureInfo.InvariantCulture)).Append('%');
                }
                text.Append('\n');
            }
            return text.ToString().TrimEnd();
        }
    }
}

// ============================================================================
// GLOSSARY
// Names used by the window, and where each lives. Both window files are one
// partial class, so every field and method below is reachable from either.
// ============================================================================
//
// FIELDS (ConstraintTrainWindow.cs; [S] = serialized, survives script reloads)
//   avatarRoot      [S] Transform      Animator root; clip paths are relative to it.
//                                      Filled with the outermost Animator above realStart.
//   realStart       [S] Transform      First real bone, RB1 (closest to the root).
//   realEnd         [S] Transform      Last real bone of the chain.
//   chainCount      [S] int            Physics chains used, 1..ConstraintTrainPlanner.MaxChains.
//   restStart       [S] Transform      First bone of the Rest_ chain (source 0, "REST").
//   chainStarts     [S] Transform[]    First bone of each physics chain (Phys1_..Phys5_).
//   addEndpoints    [S] bool           Generated physics chains get an extra _End bone.
//   trainEnd        [S] float          Value where every bone reaches physics chain 1.
//   stepCurve       [S] AnimationCurve Shape of each move from one target to the next.
//   blendCurve      [S] AnimationCurve Influence of the next chain across a blend.
//   blendMidpoint   [S] float          Influence halfway through a blend for the power-curve preset.
//   clip            [S] AnimationClip  Output clip; empty saves a new one.
//   previewValue    [S] float          Motion Time value sampled by the preview.
//   previewing          bool           Animation Mode preview is running.
//   previewClip         AnimationClip  Temporary clip sampled by the preview.
//   status              string         Message shown at the bottom of the window.
//   scroll              Vector2        Scroll position of the window.
//   UndoName            const string   Undo group name for every change.
//
// MENU ENTRIES (Editor/VRChat/RiryVRChatMenus.cs)
//   Tools/Rirytools/Constraint Train          -> Open()
//   GameObject/Rirytools/Constraint Train...  -> OpenFromHierarchy() (Hierarchy right-click)
//
// UI METHODS (ConstraintTrainWindow.UI.cs)
//   Open()                         Opens the window (Tools menu).
//   OpenFromHierarchy()            Opens it with the Hierarchy selection (calls UseSelection).
//   OnGUI()                        Draws the sections in order; blocks them in Play Mode.
//   DrawRealChain(realBones)       Start, end and avatar root fields; fills realBones.
//   DrawTargets(realBones)         Chain count, endpoints, Rest/Physics rows, Generate buttons.
//   DrawChainRow(...)              One target row: object field plus Generate button.
//   DrawTiming()                   Train end, step curve, blend curve and presets.
//   DrawOutput()                   Clip field, Set up constraints, Build animation.
//   DrawPrefabSave()               Save as Prefab Overrides / Save to <name>_ROOT (prefab instances only).
//   DrawPreview(settingsChanged)   Preview slider and toggle, and the weight readout.
//   Readout(plan, value)           Text listing each bone's source weights at a value.
//
// ACTIONS AND HELPERS (ConstraintTrainWindow.cs)
//   UseSelection(active, selected) Sets realStart (and realEnd from a selected descendant).
//   OnEnable() / OnDisable()       Window size and field repair / stops the preview.
//   SaveToPrefab()                 Applies overrides to the prefab, or saves a _ROOT variant.
//   CurrentSettings(boneCount)     Planner settings from the timing fields.
//   GenerationError(realBones)     Why chains cannot be generated, or null.
//   Generate(realBones, prefix, endpoint)  Creates (or reuses) one chain; returns its start.
//   GenerateMissing(realBones)     Generates every empty Rest/Physics slot.
//   TryGather(out plan, out error) Validates everything and builds a Plan.
//   TryFollowTargets(...)          Follows one target chain alongside the real bones.
//   Target(plan, bone, source)     The Transform behind a constraint source index.
//   SetUpConstraints()             Adds or replaces the VRC Parent Constraints.
//   BuildAnimation()               Writes the Motion Time clip.
//   IsInImmutablePackage(asset)    True for assets in read-only packages.
//   WriteCurves(clip, plan)        Writes every source-weight curve into a clip.
//   StartPreview() / RebuildPreview() / SamplePreview() / StopPreview()  Animation Mode preview.
//   Plan                           Real bones, Rest and physics chains, settings and root.
//
// OTHER FILES
//   ConstraintTrainChains (Editor/ConstraintTrain/ConstraintTrainChains.cs)
//     RestPrefix, ChainPrefix(c), EndpointSuffix, TryCollectChain, TryFollowChain,
//     FindExisting, AnimationPathError, FindSharedTarget, FindAvatarRoot, CreateChain.
//   ConstraintTrainPlanner (Editor/ConstraintTrain/ConstraintTrainPlanner.cs)
//     MaxChains, Settings, DefaultTrainEnd, Validate, SourceCount, ChainSource, EvaluateWeights,
//     BreakTimes, Shape, DescribeSource, BuildCurves, SourceWeightProperty, PowerCurve,
//     ExponentForMidpoint.
//   ConstraintTrainPrefabs (Editor/ConstraintTrain/ConstraintTrainPrefabs.cs)
//     Changes, Collect, IsGeneratedChain, ApplyToSource, RootPrefabPath, SaveAsRootVariant.
#endif
