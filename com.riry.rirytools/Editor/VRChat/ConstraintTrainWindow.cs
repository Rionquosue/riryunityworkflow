using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Sets up VRC Parent Constraints on a real bone chain and builds a Motion
    /// Time clip that moves the chain onto physics chains as a staggered train.
    /// This file holds the state and the actions; everything drawn in the window,
    /// and the Open methods, are in ConstraintTrainWindow.UI.cs. The menu entries
    /// are in RiryVRChatMenus.cs.
    /// </summary>
    internal sealed partial class ConstraintTrainWindow : EditorWindow
    {
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
        [SerializeField] private float[] chainEnds = ConstraintTrainPlanner.DefaultChainEnds(0.5f, 2);
        [SerializeField] private AnimationClip clip;

        [NonSerialized] private string status;
        private Vector2 scroll;

        /// <summary>
        /// The active object becomes the chain start; another selected object below
        /// it becomes the end (the deepest one when several are).
        /// </summary>
        private void UseSelection(Transform active, Transform[] selected)
        {
            Transform end = null;
            foreach (Transform candidate in selected)
            {
                if (candidate != active && candidate.IsChildOf(active) && (end == null || candidate.IsChildOf(end)))
                    end = candidate;
            }
            if (active != realStart)
            {
                // Chains and the avatar root found for the previous start do not apply to a new one.
                avatarRoot = null;
                restStart = null;
                chainStarts = new Transform[ConstraintTrainPlanner.MaxChains];
                realEnd = null;
            }
            realStart = active;
            if (end != null) realEnd = end;
            Repaint();
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
            if (chainEnds == null || chainEnds.Length != ConstraintTrainPlanner.MaxChains - 1)
                chainEnds = ConstraintTrainPlanner.DefaultChainEnds(trainEnd, chainCount);
        }

        private void SaveToPrefab()
        {
            ConstraintTrainPrefabs.Changes changes = ConstraintTrainPrefabs.Collect(realStart, component => component is VRCParentConstraint);
            if (changes == null) return;
            try
            {
                if (!changes.Immutable)
                {
                    if (changes.Count == 0)
                    {
                        status = "There are no generated chains or constraint changes to save.";
                        return;
                    }
                    if (!EditorUtility.DisplayDialog("Constraint Train", "Apply " + changes.AddedChains.Count + " chain(s), " +
                            changes.AddedConstraints.Count + " new constraint(s) and " + changes.ChangedConstraints.Count +
                            " changed constraint(s) to " + changes.AssetPath + "?\n\nEvery instance of that prefab gets them.",
                            "Apply", "Cancel"))
                        return;
                    ConstraintTrainPrefabs.ApplyToSource(changes, UndoName);
                    status = "Saved the changes to " + changes.AssetPath + ".";
                    return;
                }

                string path = ConstraintTrainPrefabs.RootPrefabPath(changes.AssetPath);
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    int choice = EditorUtility.DisplayDialogComplex("Constraint Train", path + " already exists.",
                        "Replace It", "Cancel", "Save a New Copy");
                    if (choice == 1) return;
                    if (choice == 2) path = AssetDatabase.GenerateUniqueAssetPath(path);
                }
                ConstraintTrainPrefabs.SaveAsRootVariant(changes, path);
                status = "Saved to " + path + " and connected " + changes.InstanceRoot.name + " to it. Save the scene to keep the link.";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                status = "Saving to the prefab failed: " + exception.Message;
            }
        }

        private ConstraintTrainPlanner.Settings CurrentSettings(int boneCount)
        {
            return new ConstraintTrainPlanner.Settings
            {
                BoneCount = boneCount,
                ChainCount = chainCount,
                TrainEnd = trainEnd,
                ChainEnds = chainEnds,
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
                    fileName, "anim", "Choose where to save the clip.", RiryToolsProjectFolder.Ensure());
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
    }
}
