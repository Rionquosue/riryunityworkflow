using System.Collections.Generic;
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
            if (blocked) EditorGUILayout.HelpBox("Unavailable in Play Mode or while compiling.", MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(blocked))
            {
                var realBones = new List<Transform>();
                DrawRealChain(realBones);
                EditorGUILayout.Space();
                DrawTargets(realBones);
                EditorGUILayout.Space();
                DrawTiming();
                EditorGUILayout.Space();
                DrawOutput();
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
                // The chain ends spread out again after the train end; the train end stays.
                chainEnds = ConstraintTrainPlanner.DefaultChainEnds(trainEnd, chains);
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
            }
            for (int c = 0; c < chainCount - 1; c++)
            {
                chainEnds[c] = EditorGUILayout.Slider(
                    new GUIContent("Chain " + (c + 2) + " end", "Value where every bone reaches physics chain " + (c + 2) + "."),
                    chainEnds[c], 0.01f, 1f);
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
//   trainEnd        [S] float          Value where every bone reaches physics chain 1 (the train ends).
//   chainEnds       [S] float[]        Value where every bone reaches physics chain c+2 (Chain 2 end, ...).
//   stepCurve       [S] AnimationCurve Shape of each move from one target to the next.
//   blendCurve      [S] AnimationCurve Influence of the next chain across a blend.
//   clip            [S] AnimationClip  Output clip; empty saves a new one.
//   status              string         Message shown at the bottom of the window.
//   scroll              Vector2        Scroll position of the window.
//   UndoName            const string   Undo group name for every change.
//
// MENU ENTRIES (Editor/VRChat/RiryVRChatMenus.cs)
//   Tools/RiryTools/Constraint Train          -> Open()
//   GameObject/RiryTools/Constraint Train  -> OpenFromHierarchy() (Hierarchy right-click)
//
// UI METHODS (ConstraintTrainWindow.UI.cs)
//   Open()                         Opens the window (Tools menu).
//   OpenFromHierarchy()            Opens it with the Hierarchy selection (calls UseSelection).
//   OnGUI()                        Draws the sections in order; blocks them in Play Mode.
//   DrawRealChain(realBones)       Start, end and avatar root fields; fills realBones.
//   DrawTargets(realBones)         Chain count, endpoints, Rest/Physics rows, Generate buttons.
//   DrawChainRow(...)              One target row: object field plus Generate button.
//   DrawTiming()                   Train end, step curve, blend curve, Chain 2 end ... Chain N end.
//   DrawOutput()                   Clip field, Set up constraints, Build animation.
//   DrawPrefabSave()               Save as Prefab Overrides / Save to <name>_ROOT (prefab instances only).
//
// ACTIONS AND HELPERS (ConstraintTrainWindow.cs)
//   UseSelection(active, selected) Sets realStart (and realEnd from a selected descendant).
//   OnEnable()                     Window size and field repair.
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
//   Plan                           Real bones, Rest and physics chains, settings and root.
//
// OTHER FILES
//   ConstraintTrainChains (Editor/ConstraintTrain/ConstraintTrainChains.cs)
//     RestPrefix, ChainPrefix(c), EndpointSuffix, TryCollectChain, TryFollowChain,
//     FindExisting, AnimationPathError, FindSharedTarget, FindAvatarRoot, CreateChain.
//   ConstraintTrainPlanner (Editor/ConstraintTrain/ConstraintTrainPlanner.cs)
//     MaxChains, Settings, DefaultTrainEnd, DefaultChainEnds, Validate, SourceCount, ChainSource,
//     EvaluateWeights, BreakTimes, Shape, DescribeSource, BuildCurves, SourceWeightProperty.
//   ConstraintTrainPrefabs (Editor/ConstraintTrain/ConstraintTrainPrefabs.cs)
//     Changes, Collect, IsGeneratedChain, ApplyToSource, RootPrefabPath, SaveAsRootVariant.
