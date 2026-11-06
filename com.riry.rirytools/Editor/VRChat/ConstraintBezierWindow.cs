using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Constraint Tool's Bezier tab: rigs a bone chain to one cubic Bezier curve with VRC
    /// constraints. Generate fits the curve to the chain at rest and creates, as a sibling
    /// of the chain start, BezierRig_&lt;start&gt;: Anchor Start (P0) and Anchor End (P3),
    /// Handle Start (P1) and Handle End (P2), one Sample per node held on the curve by a
    /// VRC Position Constraint (sources P0..P3 at the Bernstein weights of its t), and with
    /// Twist on, one Twist helper per node blending the anchors' rotations. Each node aims
    /// at the next sample (the last at Handle End), taking its up from its Twist helper or
    /// the rig; Stretch also holds every node on its sample, Keep length only the first.
    /// The nodes are the chain's bones, or with Mechanism chain a Bezier_ copy of them that
    /// the bones follow by VRC Parent Constraints. Aim and up axes are taken from the rest
    /// pose, so no rotation changes at rest. This file holds the state and the actions; the
    /// drawing is in ConstraintBezierWindow.UI.cs. Shown in a tab of ConstraintToolWindow.
    /// </summary>
    internal sealed partial class ConstraintBezierWindow : EditorWindow
    {
        internal enum Mode { Stretch, KeepLength }
        internal enum Target { DeformBones, MechanismChain }
        internal enum Spacing { Existing, Even }

        private const string UndoName = "Bezier rig";
        private const string SettingsUndo = "Bezier settings";
        internal const string RigPrefix = "BezierRig_";
        internal const string MechanismPrefix = "Bezier_";
        // Shorter than this, a direction cannot be aimed along.
        private const float MinAimDistance = 1e-5f;

        [SerializeField] private Transform chainStart;
        [SerializeField] private Transform chainEnd;
        [SerializeField] private bool handlesFollowAnchors = true;
        [SerializeField] private Mode mode = Mode.Stretch;
        [SerializeField] private Target target = Target.DeformBones;
        [SerializeField] private Spacing spacing = Spacing.Existing;
        [SerializeField] private bool twist = true;

        [NonSerialized] private string status;
        // Runs an action button and puts the status it sets in the Console too: the window's status line is gone once it
        // closes, so this traces what the tool did. (A status set again, even to the same text, is a new string.)
        private void Act(Action action)
        {
            string before = status;
            action();
            if (!string.IsNullOrEmpty(status) && !ReferenceEquals(status, before))
                RiryLog.Info(RiryLog.ConstraintTool, "Bezier: " + status);
        }
        private Vector2 scroll;

        /// <summary>The active object becomes the chain start; the deepest selected object below it the end.</summary>
        internal void UseSelection(Transform active, Transform[] selected)
        {
            Transform end = null;
            foreach (Transform candidate in selected)
            {
                if (candidate != active && candidate.IsChildOf(active) && (end == null || candidate.IsChildOf(end)))
                    end = candidate;
            }
            Undo.RecordObject(this, SettingsUndo);
            if (active != chainStart) chainEnd = null;
            chainStart = active;
            if (end != null) chainEnd = end;
            status = null;
            Repaint();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        /// <summary>Why the chain cannot be rigged, or null.</summary>
        private static string ChainError(List<Transform> bones)
        {
            if (bones.Count < 2) return "The chain needs at least two bones.";
            Transform start = bones[0];
            if (EditorUtility.IsPersistent(start) || !start.gameObject.scene.IsValid())
                return "Use bones from a scene or Prefab Mode, not a prefab asset.";
            if (start.parent == null) return "The chain start needs a parent to hold the rig.";
            float length = 0f;
            for (int i = 1; i < bones.Count; i++) length += Vector3.Distance(bones[i - 1].position, bones[i].position);
            if (length < MinAimDistance) return "The chain has no length.";
            return null;
        }

        private static List<Vector3> RestPositions(List<Transform> bones)
        {
            var positions = new List<Vector3>(bones.Count);
            foreach (Transform bone in bones) positions.Add(bone.position);
            return positions;
        }

        /// <summary>An exact child-name lookup ('/' in a name is not a path here).</summary>
        private static Transform FindSibling(Transform start, string name)
        {
            Transform parent = start.parent;
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            }
            return null;
        }

        private static Transform FindRig(Transform start)
        {
            return start != null ? FindSibling(start, RigPrefix + start.name) : null;
        }

        private static Transform FindMechanism(Transform start)
        {
            return start != null ? FindSibling(start, MechanismPrefix + start.name) : null;
        }

        /// <summary>Constraints Generate adds for these options, by kind.</summary>
        private int PlannedConstraints(int nodes)
        {
            int count = nodes;                                    // samples
            if (twist) count += nodes;                            // twist helpers
            count += mode == Mode.Stretch ? nodes : 1;            // node positions
            count += nodes;                                       // node aims
            if (target == Target.MechanismChain) count += nodes;  // bones follow the mechanism
            return count;
        }

        private void Generate(List<Transform> bones)
        {
            string error = ChainError(bones);
            if (error == null && FindRig(bones[0]) != null) error = "This chain already has " + RigPrefix + bones[0].name + ". Remove the rig first.";
            if (error == null && target == Target.MechanismChain && FindMechanism(bones[0]) != null)
                error = MechanismPrefix + bones[0].name + " already exists. Remove the rig first.";
            if (error != null)
            {
                status = error;
                return;
            }

            ConstraintBezierCurve.Fit fit = ConstraintBezierCurve.FitTo(RestPositions(bones), spacing == Spacing.Even);
            int count = bones.Count;
            var others = new List<string>();
            foreach (Transform bone in bones)
            {
                if (bone.GetComponent<VRCConstraintBase>() != null || bone.GetComponent<UnityEngine.Animations.IConstraint>() != null)
                    others.Add(bone.name);
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                Transform start = bones[0];
                int layer = start.gameObject.layer;
                Transform rig = CreateEmpty(RigPrefix + start.name, start.parent, layer);
                rig.localPosition = start.localPosition;

                Transform anchorStart = CreateEmpty("Anchor Start", rig, layer);
                anchorStart.position = fit.Controls[0];
                Transform anchorEnd = CreateEmpty("Anchor End", rig, layer);
                anchorEnd.position = fit.Controls[3];
                Transform handleStart = CreateEmpty("Handle Start", handlesFollowAnchors ? anchorStart : rig, layer);
                handleStart.position = fit.Controls[1];
                Transform handleEnd = CreateEmpty("Handle End", handlesFollowAnchors ? anchorEnd : rig, layer);
                handleEnd.position = fit.Controls[2];
                Transform[] controls = { anchorStart, handleStart, handleEnd, anchorEnd };

                // Samples: each held on the curve at its node's t.
                Transform samplesFolder = CreateEmpty("Samples", rig, layer);
                var samples = new Transform[count];
                var weights = new float[4];
                for (int i = 0; i < count; i++)
                {
                    samples[i] = CreateEmpty("Sample " + (i + 1).ToString("00"), samplesFolder, layer);
                    samples[i].position = fit.Points[i];
                    ConstraintBezierCurve.Weights(fit.T[i], weights);
                    var position = Undo.AddComponent<VRCPositionConstraint>(samples[i].gameObject);
                    Finish(position, controls, weights);
                }

                // Twist helpers: the anchors' rotations blended at t; at rest both match the rig.
                Transform[] upFrom = new Transform[count];
                if (twist)
                {
                    Transform twistFolder = CreateEmpty("Twist", rig, layer);
                    for (int i = 0; i < count; i++)
                    {
                        Transform helper = CreateEmpty("Twist " + (i + 1).ToString("00"), twistFolder, layer);
                        helper.position = fit.Points[i];
                        var rotation = Undo.AddComponent<VRCRotationConstraint>(helper.gameObject);
                        Finish(rotation, new[] { anchorStart, anchorEnd }, new[] { 1f - fit.T[i], fit.T[i] });
                        upFrom[i] = helper;
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++) upFrom[i] = rig;
                }

                // The nodes: the bones, or a mechanism chain the bones then follow.
                List<Transform> nodes = bones;
                if (target == Target.MechanismChain)
                {
                    Transform first = ConstraintTrainChains.CreateChain(bones, MechanismPrefix, false, UndoName);
                    nodes = new List<Transform> { first };
                    while (nodes.Count < count) nodes.Add(nodes[nodes.Count - 1].GetChild(0));
                    for (int i = 0; i < count; i++)
                    {
                        var parent = Undo.AddComponent<VRCParentConstraint>(bones[i].gameObject);
                        Finish(parent, new[] { nodes[i] }, new[] { 1f });
                    }
                }

                int skipped = 0;
                for (int i = 0; i < count; i++)
                {
                    Transform node = nodes[i];
                    // Where the node is at rest once rigged: on its sample with Stretch, where it is now otherwise.
                    Vector3 restPosition = mode == Mode.Stretch ? fit.Points[i] : node.position;
                    if (mode == Mode.Stretch || i == 0)
                    {
                        var position = Undo.AddComponent<VRCPositionConstraint>(node.gameObject);
                        Finish(position, new[] { samples[i] }, new[] { 1f });
                    }
                    // Each node aims at the next sample; the last one at Handle End, which gives the curve's end direction.
                    Transform aimAt = i < count - 1 ? samples[i + 1] : handleEnd;
                    Vector3 aimPoint = i < count - 1 ? fit.Points[i + 1] : fit.Controls[2];
                    if (i == count - 1 && Vector3.Distance(aimPoint, restPosition) < MinAimDistance)
                    {
                        aimAt = samples[count - 2];
                        aimPoint = fit.Points[count - 2];
                    }
                    Vector3 direction = aimPoint - restPosition;
                    if (direction.magnitude < MinAimDistance)
                    {
                        skipped++;
                        continue;
                    }
                    AddAim(node, aimAt, direction.normalized, upFrom[i]);
                }

                status = "Rigged " + count + " bones to " + rig.name + ": " + Report(bones) + ".";
                if (skipped > 0) status += " " + skipped + " node(s) had no direction to aim along and were left unaimed.";
                if (others.Count > 0)
                    status += " Other constraints on " + string.Join(", ", others) + " also move these bones and may fight the rig.";
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }

        private static Transform CreateEmpty(string name, Transform parent, int layer)
        {
            var created = new GameObject(name);
            created.layer = layer;
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        /// <summary>Sources at these weights with no offsets, then the settings every rig constraint shares.</summary>
        private static void Finish(VRCConstraintBase constraint, Transform[] sources, float[] weights)
        {
            // The source list is a struct: edit a copy and assign it back.
            var list = constraint.Sources;
            for (int s = 0; s < sources.Length; s++)
            {
                list.Add(new VRCConstraintSource
                {
                    SourceTransform = sources[s],
                    Weight = weights[s],
                    ParentPositionOffset = Vector3.zero,
                    ParentRotationOffset = Vector3.zero
                });
            }
            constraint.Sources = list;
            constraint.GlobalWeight = 1f;
            constraint.SolveInLocalSpace = false;
            constraint.FreezeToWorld = false;
            constraint.Locked = true;
            constraint.IsActive = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(constraint);
            EditorUtility.SetDirty(constraint);
        }

        /// <summary>
        /// Aims the node along direction (world, at rest). The aim and up axes are the node's own
        /// rest axes, and the world up is its rest up in upFrom's space, so at rest the aim
        /// gives back the node's current rotation exactly, with no offsets.
        /// </summary>
        private static void AddAim(Transform node, Transform aimAt, Vector3 direction, Transform upFrom)
        {
            Quaternion rest = node.rotation;
            // The node's own axis furthest from the aim, made square to it.
            Vector3 up = Vector3.zero;
            float best = float.MaxValue;
            foreach (Vector3 axis in new[] { rest * Vector3.right, rest * Vector3.up, rest * Vector3.forward })
            {
                float along = Mathf.Abs(Vector3.Dot(axis, direction));
                if (along >= best) continue;
                best = along;
                up = axis;
            }
            up = Vector3.ProjectOnPlane(up, direction).normalized;
            // upFrom is the rig or a Twist helper; both have the rig's rotation at rest.
            Quaternion upRest = upFrom.rotation;

            var aim = Undo.AddComponent<VRCAimConstraint>(node.gameObject);
            aim.AimAxis = Quaternion.Inverse(rest) * direction;
            aim.UpAxis = Quaternion.Inverse(rest) * up;
            aim.WorldUp = VRCConstraintBase.WorldUpType.ObjectRotationUp;
            aim.WorldUpTransform = upFrom;
            aim.WorldUpVector = Quaternion.Inverse(upRest) * up;
            aim.AffectsRotationX = true;
            aim.AffectsRotationY = true;
            aim.AffectsRotationZ = true;
            aim.RotationAtRest = Vector3.zero;
            aim.RotationOffset = Vector3.zero;
            Finish(aim, new[] { aimAt }, new[] { 1f });
        }

        /// <summary>The rig's objects: BezierRig_ and Bezier_ of this chain, those that exist.</summary>
        private static List<Transform> RigRoots(Transform start)
        {
            var roots = new List<Transform>();
            Transform rig = FindRig(start);
            Transform mechanism = FindMechanism(start);
            if (rig != null) roots.Add(rig);
            if (mechanism != null) roots.Add(mechanism);
            return roots;
        }

        private static bool InRig(Transform transform, List<Transform> roots)
        {
            if (transform == null) return false;
            foreach (Transform root in roots)
            {
                if (transform.IsChildOf(root)) return true;
            }
            return false;
        }

        /// <summary>
        /// A constraint on a chain bone that Generate added: every source, and its world up object,
        /// is inside the rig. Constraints of the user's own are never matched.
        /// </summary>
        private static bool IsRigConstraint(VRCConstraintBase constraint, List<Transform> roots)
        {
            var sources = constraint.Sources;
            if (sources.Count == 0) return false;
            for (int s = 0; s < sources.Count; s++)
            {
                if (!InRig(sources[s].SourceTransform, roots)) return false;
            }
            var aim = constraint as VRCAimConstraint;
            return aim == null || aim.WorldUpTransform == null || InRig(aim.WorldUpTransform, roots);
        }

        /// <summary>Every constraint of this chain's rig: inside BezierRig_ and Bezier_, and Generate's on the bones.</summary>
        private static List<VRCConstraintBase> RigConstraints(List<Transform> bones)
        {
            var found = new List<VRCConstraintBase>();
            List<Transform> roots = RigRoots(bones[0]);
            if (roots.Count == 0) return found;
            foreach (Transform root in roots) found.AddRange(root.GetComponentsInChildren<VRCConstraintBase>(true));
            foreach (Transform bone in bones)
            {
                foreach (VRCConstraintBase constraint in bone.GetComponents<VRCConstraintBase>())
                {
                    if (IsRigConstraint(constraint, roots)) found.Add(constraint);
                }
            }
            return found;
        }

        /// <summary>"N constraints, depth D" for the chain's rig as it is in the scene.</summary>
        private static string Report(List<Transform> bones)
        {
            List<VRCConstraintBase> constraints = RigConstraints(bones);
            var depths = new Dictionary<VRCConstraintBase, int>();
            int deepest = 0;
            foreach (VRCConstraintBase constraint in constraints) deepest = Mathf.Max(deepest, Depth(constraint, depths, 0));
            return constraints.Count + " constraints, depth " + deepest;
        }

        /// <summary>
        /// The longest run of constraints that wait on each other, ending at this one. A constraint waits
        /// on every constraint that moves one of its sources, its world up object or its own object's
        /// parents, or any of their parents.
        /// </summary>
        private static int Depth(VRCConstraintBase constraint, Dictionary<VRCConstraintBase, int> depths, int guard)
        {
            int known;
            if (depths.TryGetValue(constraint, out known)) return known;
            // A loop or a very deep rig stops counting here instead of overflowing.
            if (guard > 256) return 1;
            depths[constraint] = 1;
            var inputs = new List<Transform>();
            var sources = constraint.Sources;
            for (int s = 0; s < sources.Count; s++) inputs.Add(sources[s].SourceTransform);
            var aim = constraint as VRCAimConstraint;
            if (aim != null) inputs.Add(aim.WorldUpTransform);
            Transform own = constraint.TargetTransform != null ? constraint.TargetTransform : constraint.transform;
            inputs.Add(own.parent);
            int depth = 1;
            foreach (Transform input in inputs)
            {
                for (Transform current = input; current != null; current = current.parent)
                {
                    foreach (VRCConstraintBase other in current.GetComponents<VRCConstraintBase>())
                    {
                        if (other == constraint) continue;
                        depth = Mathf.Max(depth, Depth(other, depths, guard + 1) + 1);
                    }
                }
            }
            depths[constraint] = depth;
            return depth;
        }

        /// <summary>Removes BezierRig_, Bezier_ and the constraints Generate added to the bones, as one Undo step.</summary>
        private void RemoveRig(List<Transform> bones)
        {
            List<Transform> roots = RigRoots(bones[0]);
            if (roots.Count == 0)
            {
                status = "This chain has no Bezier rig.";
                return;
            }
            // Objects saved in the prefab itself cannot be removed from an instance.
            var locked = new List<string>();
            foreach (Transform root in roots)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root) && !PrefabUtility.IsAddedGameObjectOverride(root.gameObject))
                    locked.Add(root.name);
            }
            var onBones = new List<VRCConstraintBase>();
            foreach (Transform bone in bones)
            {
                foreach (VRCConstraintBase constraint in bone.GetComponents<VRCConstraintBase>())
                {
                    if (!IsRigConstraint(constraint, roots)) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(constraint) && !PrefabUtility.IsAddedComponentOverride(constraint))
                        locked.Add(bone.name + "'s " + constraint.GetType().Name);
                    else onBones.Add(constraint);
                }
            }
            if (locked.Count > 0)
            {
                // A count and the first few names, so a long chain cannot grow the status box over the tab.
                const int Shown = 5;
                status = locked.Count + " part(s) of the rig are saved in the prefab (" +
                    string.Join(", ", locked.GetRange(0, Math.Min(Shown, locked.Count))) +
                    (locked.Count > Shown ? " and " + (locked.Count - Shown) + " more" : "") + "). Remove the rig in Prefab Mode.";
                return;
            }

            // The rig is found by name, so the user sees what will go before anything is deleted.
            var names = new List<string>();
            foreach (Transform root in roots) names.Add(root.name + " (with " + (root.GetComponentsInChildren<Transform>(true).Length - 1) + " child object(s))");
            if (!EditorUtility.DisplayDialog("Remove Bezier rig", "Remove these objects beside " + bones[0].name + ":\n" +
                    string.Join("\n", names) + "\n\nand " + onBones.Count + " constraint(s) on the bones whose sources are all inside them?" +
                    "\n\nThey are found by name. Ctrl+Z brings everything back.", "Remove", "Cancel"))
            {
                status = "Remove cancelled; nothing was changed.";
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove " + UndoName);
            try
            {
                foreach (VRCConstraintBase constraint in onBones) Undo.DestroyObjectImmediate(constraint);
                foreach (Transform root in roots) Undo.DestroyObjectImmediate(root.gameObject);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
            status = "Removed the Bezier rig: " + roots.Count + " object(s) and " + onBones.Count + " constraint(s) on the bones.";
        }
    }
}

// ============================================================================
// GLOSSARY: Bezier tab state and actions
// The full glossary of both window files (fields, the rig it builds, methods) is at the bottom of
// ConstraintBezierWindow.UI.cs.
// ============================================================================
//
//   Mode / Target / Spacing   Stretch or KeepLength; DeformBones or MechanismChain; Existing or Even.
//   Generate(bones)           Fits the curve (ConstraintBezierCurve.FitTo) and builds BezierRig_<start>, as one Undo step.
//   RemoveRig(bones)          After a confirmation listing them (they are found by name), removes it, Bezier_<start> and
//                             the bones' rig constraints, as one Undo step.
//   Report(bones) / Depth     Constraint report: count and depth of the rig in the scene.
