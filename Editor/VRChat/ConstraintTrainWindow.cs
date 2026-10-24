#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS with the VRChat Avatars SDK 3.7.0 or newer.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Sets up VRC Parent Constraints on a real bone chain and builds a Motion
    /// Time clip that moves the chain onto physics chains as a staggered train.
    /// </summary>
    internal sealed class ConstraintTrainWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Rirytools/Constraint Train";
        private const string UndoName = "Constraint Train";

        private sealed class Plan
        {
            public readonly List<Transform> Real = new List<Transform>();
            public readonly List<Transform> Rest = new List<Transform>();
            public readonly List<List<Transform>> Chains = new List<List<Transform>>();
            public ConstraintTrainPlanner.Settings Settings;
            public Transform Root;
        }

        [SerializeField] private Transform avatarRoot;
        [SerializeField] private Transform realStart;
        [SerializeField] private Transform realEnd;
        [SerializeField] private int chainCount = 2;
        [SerializeField] private Transform restStart;
        [SerializeField] private Transform[] chainStarts = new Transform[ConstraintTrainPlanner.MaxChains];
        [SerializeField] private bool addEndpoints = true;
        [SerializeField] private float trainEnd = 0.5f;
        [SerializeField] private AnimationCurve stepCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private AnimationCurve blendCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private float blendMidpoint = 0.75f;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private float previewValue;

        [NonSerialized] private bool previewing;
        [NonSerialized] private AnimationClip previewClip;
        [NonSerialized] private string status;
        private Vector2 scroll;

        [MenuItem(MenuPath, false, 2030)]
        private static void Open()
        {
            GetWindow<ConstraintTrainWindow>("Constraint Train");
        }

        private void OnEnable()
        {
            minSize = new Vector2(420, 480);
            if (chainStarts == null || chainStarts.Length != ConstraintTrainPlanner.MaxChains)
            {
                var resized = new Transform[ConstraintTrainPlanner.MaxChains];
                if (chainStarts != null)
                    Array.Copy(chainStarts, resized, Mathf.Min(chainStarts.Length, resized.Length));
                chainStarts = resized;
            }
            if (stepCurve == null) stepCurve = AnimationCurve.Linear(0, 0, 1, 1);
            if (blendCurve == null) blendCurve = AnimationCurve.Linear(0, 0, 1, 1);
        }

        private void OnDisable()
        {
            StopPreview();
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

        private ConstraintTrainPlanner.Settings CurrentSettings(int boneCount)
        {
            return new ConstraintTrainPlanner.Settings
            {
                BoneCount = boneCount,
                ChainCount = chainCount,
                TrainEnd = trainEnd,
                StepCurve = stepCurve,
                BlendCurve = blendCurve
            };
        }

        private static string GenerationError(List<Transform> realBones)
        {
            Transform start = realBones[0];
            if (EditorUtility.IsPersistent(start) || !start.gameObject.scene.IsValid())
                return "Use real bones from a scene or Prefab Mode, not a prefab asset.";
            if (start.parent == null)
                return "The real chain start needs a parent to hold the generated chains.";
            return null;
        }

        private Transform Generate(List<Transform> realBones, string prefix, bool endpoint)
        {
            Transform existing = ConstraintTrainChains.FindExisting(realBones, prefix);
            if (existing != null)
            {
                status = "Using the existing chain " + existing.name + ".";
                return existing;
            }
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            Transform created = ConstraintTrainChains.CreateChain(realBones, prefix, endpoint, UndoName);
            status = "Created " + created.name + " and its children.";
            return created;
        }

        private void GenerateMissing(List<Transform> realBones)
        {
            if (restStart == null)
                restStart = Generate(realBones, ConstraintTrainChains.RestPrefix, false);
            for (int c = 0; c < chainCount; c++)
            {
                if (chainStarts[c] == null)
                    chainStarts[c] = Generate(realBones, ConstraintTrainChains.ChainPrefix(c), addEndpoints);
            }
            status = "All chains are assigned.";
        }

        private bool TryGather(out Plan plan, out string error)
        {
            plan = new Plan();
            if (!ConstraintTrainChains.TryCollectChain(realStart, realEnd, plan.Real, out error)) return false;
            error = GenerationError(plan.Real);
            if (error != null) return false;
            if (avatarRoot == null)
            {
                error = "Assign the avatar root.";
                return false;
            }
            foreach (Transform bone in plan.Real)
            {
                if (bone == avatarRoot || !bone.IsChildOf(avatarRoot))
                {
                    error = "Every real bone must be below the avatar root.";
                    return false;
                }
                error = ConstraintTrainChains.AnimationPathError(bone, avatarRoot);
                if (error != null) return false;
            }

            plan.Root = avatarRoot;
            plan.Settings = CurrentSettings(plan.Real.Count);
            error = ConstraintTrainPlanner.Validate(plan.Settings);
            if (error != null) return false;

            if (!TryFollowTargets("Rest", restStart, plan, plan.Rest, out error)) return false;
            for (int c = 0; c < chainCount; c++)
            {
                var chain = new List<Transform>();
                if (!TryFollowTargets("Physics chain " + (c + 1), chainStarts[c], plan, chain, out error))
                    return false;
                plan.Chains.Add(chain);
            }

            var all = new List<List<Transform>>(plan.Chains) { plan.Rest };
            Transform shared = ConstraintTrainChains.FindSharedTarget(all);
            if (shared != null)
            {
                error = "'" + shared.name + "' is used by more than one target chain. Each chain needs its own bones.";
                return false;
            }
            return true;
        }

        private static bool TryFollowTargets(string label, Transform start, Plan plan, List<Transform> chain,
            out string error)
        {
            if (!ConstraintTrainChains.TryFollowChain(start, plan.Real, chain, out error))
            {
                error = label + ": " + error;
                return false;
            }
            foreach (Transform target in chain)
            {
                // A target inside the constrained chain would follow its own constraint.
                if (target.IsChildOf(plan.Real[0]))
                {
                    error = label + ": targets must not be inside the real bone chain.";
                    return false;
                }
                // VRChat removes constraint sources outside the avatar on upload.
                if (!target.IsChildOf(plan.Root))
                {
                    error = label + ": targets must be inside the avatar root.";
                    return false;
                }
            }
            return true;
        }

        private static Transform Target(Plan plan, int bone, int source)
        {
            int chain;
            int chainBone;
            ConstraintTrainPlanner.DescribeSource(bone, source, out chain, out chainBone);
            return chain < 0 ? plan.Rest[chainBone] : plan.Chains[chain][chainBone];
        }

        private void SetUpConstraints()
        {
            StopPreview();
            Plan plan;
            string error;
            if (!TryGather(out plan, out error))
            {
                status = error;
                return;
            }

            // Sources are animated by index, so a constraint that already has
            // sources is replaced instead of extended.
            int replace = 0;
            var others = new List<string>();
            foreach (Transform bone in plan.Real)
            {
                if (bone.GetComponents<VRCParentConstraint>().Length > 1)
                {
                    status = bone.name + " has more than one VRC Parent Constraint. Keep one, then set up again.";
                    return;
                }
                // Other constraints on the same bone fight over its pose.
                if (bone.GetComponents<VRCConstraintBase>().Length > bone.GetComponents<VRCParentConstraint>().Length ||
                    bone.GetComponent<UnityEngine.Animations.IConstraint>() != null)
                    others.Add(bone.name);
                VRCParentConstraint existing = bone.GetComponent<VRCParentConstraint>();
                if (existing == null || existing.Sources.Count == 0) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(existing) &&
                    !PrefabUtility.IsAddedComponentOverride(existing))
                {
                    status = bone.name + " has a VRC Parent Constraint from its prefab. Remove its sources " +
                        "or the component manually, then set up again.";
                    return;
                }
                replace++;
            }
            if (replace > 0 && !EditorUtility.DisplayDialog("Constraint Train",
                    "Replace the VRC Parent Constraint on " + replace + " bone(s)? Their current sources " +
                    "are discarded.", "Replace", "Cancel"))
                return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            for (int bone = 0; bone < plan.Real.Count; bone++)
            {
                GameObject owner = plan.Real[bone].gameObject;
                VRCParentConstraint constraint = owner.GetComponent<VRCParentConstraint>();
                if (constraint != null && constraint.Sources.Count > 0)
                {
                    Undo.DestroyObjectImmediate(constraint);
                    constraint = null;
                }
                if (constraint == null) constraint = Undo.AddComponent<VRCParentConstraint>(owner);
                else Undo.RecordObject(constraint, UndoName);

                var weights = new float[ConstraintTrainPlanner.SourceCount(bone, plan.Settings.ChainCount)];
                ConstraintTrainPlanner.EvaluateWeights(plan.Settings, bone, 0f, weights);
                // The source list is a struct: edit a copy and assign it back.
                var sources = constraint.Sources;
                for (int s = 0; s < weights.Length; s++)
                {
                    sources.Add(new VRCConstraintSource
                    {
                        SourceTransform = Target(plan, bone, s),
                        Weight = weights[s],
                        ParentPositionOffset = Vector3.zero,
                        ParentRotationOffset = Vector3.zero
                    });
                }
                constraint.Sources = sources;
                constraint.GlobalWeight = 1f;
                constraint.SolveInLocalSpace = false;
                constraint.Locked = true;
                constraint.IsActive = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(constraint);
                EditorUtility.SetDirty(constraint);
            }
            Undo.CollapseUndoOperations(group);
            status = "Set up VRC Parent Constraints on " + plan.Real.Count + " bones.";
            if (others.Count > 0)
                status += " Other constraints on " + string.Join(", ", others) +
                    " also move these bones and may fight the train.";
        }

        private void BuildAnimation()
        {
            StopPreview();
            Plan plan;
            string error;
            if (!TryGather(out plan, out error))
            {
                status = error;
                return;
            }

            if (clip == null)
            {
                string fileName = plan.Real[0].name + "_ConstraintTrain";
                foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(invalid, '_');
                string path = EditorUtility.SaveFilePanelInProject("Save Constraint Train Clip",
                    fileName, "anim", "Choose where to save the clip.");
                if (string.IsNullOrEmpty(path)) return;
                var created = new AnimationClip { frameRate = 60 };
                WriteCurves(created, plan);
                AssetDatabase.CreateAsset(created, path);
                clip = created;
            }
            else
            {
                if (!AssetDatabase.IsNativeAsset(clip) || (clip.hideFlags & HideFlags.NotEditable) != 0 ||
                    IsInImmutablePackage(clip) || !AssetDatabase.IsOpenForEdit(clip))
                {
                    status = "The assigned clip is imported or read-only. Clear the field to save a new clip.";
                    return;
                }
                if (!EditorUtility.DisplayDialog("Constraint Train",
                        "Replace every curve in '" + clip.name + "'?", "Replace", "Cancel"))
                    return;
                Undo.RegisterCompleteObjectUndo(clip, UndoName);
                clip.ClearCurves();
                WriteCurves(clip, plan);
            }
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            status = "Built '" + clip.name + "'. Drive it with Motion Time.";
        }

        private static bool IsInImmutablePackage(UnityEngine.Object asset)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(AssetDatabase.GetAssetPath(asset));
            return package != null && package.source != UnityEditor.PackageManager.PackageSource.Embedded &&
                package.source != UnityEditor.PackageManager.PackageSource.Local;
        }

        private void WriteCurves(AnimationClip target, Plan plan)
        {
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            for (int bone = 0; bone < plan.Real.Count; bone++)
            {
                string path = AnimationUtility.CalculateTransformPath(plan.Real[bone], avatarRoot);
                AnimationCurve[] boneCurves = ConstraintTrainPlanner.BuildCurves(plan.Settings, bone);
                for (int s = 0; s < boneCurves.Length; s++)
                {
                    bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(VRCParentConstraint),
                        ConstraintTrainPlanner.SourceWeightProperty(s)));
                    curves.Add(boneCurves[s]);
                }
            }
            AnimationUtility.SetEditorCurves(target, bindings.ToArray(), curves.ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(target);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(target, settings);
        }

        private void StartPreview()
        {
            Plan plan;
            string error;
            if (!TryGather(out plan, out error))
            {
                status = error;
                return;
            }
            if (AnimationMode.InAnimationMode())
            {
                status = "Another tool is using Animation Mode. Stop its preview first.";
                return;
            }
            previewClip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
            WriteCurves(previewClip, plan);
            AnimationMode.StartAnimationMode();
            previewing = true;
            SamplePreview();
        }

        private void RebuildPreview()
        {
            Plan plan;
            string error;
            if (!TryGather(out plan, out error)) return;
            previewClip.ClearCurves();
            WriteCurves(previewClip, plan);
            SamplePreview();
        }

        private void SamplePreview()
        {
            if (!AnimationMode.InAnimationMode() || avatarRoot == null || previewClip == null)
            {
                StopPreview();
                return;
            }
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(avatarRoot.gameObject, previewClip, previewValue);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        private void StopPreview()
        {
            if (previewing && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            previewing = false;
            if (previewClip != null) DestroyImmediate(previewClip);
            previewClip = null;
        }
    }
}
#endif
