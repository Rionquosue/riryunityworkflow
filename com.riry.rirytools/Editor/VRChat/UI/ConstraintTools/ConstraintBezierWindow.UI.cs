using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Everything the Bezier tab draws. The state and the actions behind the buttons are in
    /// ConstraintBezierWindow.cs; both files are one class. Drawn inside ConstraintToolWindow
    /// and the Main Menu's Constraint Tool tab, never on its own. A glossary is at the bottom.
    /// </summary>
    internal sealed partial class ConstraintBezierWindow
    {
        private static readonly GUIContent[] ModeNames = { new GUIContent("Stretch"), new GUIContent("Keep length") };
        private static readonly GUIContent[] TargetNames = { new GUIContent("Deform bones"), new GUIContent("Mechanism chain") };
        private static readonly GUIContent[] SpacingNames = { new GUIContent("Existing"), new GUIContent("Even") };

        private void OnGUI()
        {
            bool blocked = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
            if (blocked) EditorGUILayout.HelpBox("Unavailable in Play Mode or while compiling.", MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(blocked))
            {
                var bones = new List<Transform>();
                DrawChain(bones);
                EditorGUILayout.Space();
                DrawOptions();
                EditorGUILayout.Space();
                DrawRig(bones);
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        private void DrawChain(List<Transform> bones)
        {
            EditorGUILayout.LabelField("Bone chain", EditorStyles.boldLabel);
            chainStart = RiryToolGUI.Set(this, SettingsUndo, chainStart, (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Start", "First bone of the chain: the curve starts here (Anchor Start)."), chainStart, typeof(Transform), true));
            chainEnd = RiryToolGUI.Set(this, SettingsUndo, chainEnd, (Transform)EditorGUILayout.ObjectField(
                new GUIContent("End", "Last bone of the chain: the curve ends here (Anchor End)."), chainEnd, typeof(Transform), true));
            if (chainStart == null || chainEnd == null) return;
            string error;
            if (!ConstraintToolChains.TryCollectChain(chainStart, chainEnd, bones, out error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }
            var names = new StringBuilder();
            for (int i = 0; i < bones.Count; i++)
            {
                if (i > 0) names.Append(" > ");
                names.Append(bones[i].name);
            }
            EditorGUILayout.LabelField(bones.Count + " bones: " + names, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            handlesFollowAnchors = RiryToolGUI.Set(this, SettingsUndo, handlesFollowAnchors, EditorGUILayout.Toggle(
                new GUIContent("Handles follow anchors", "On: each handle is a child of its anchor, so moving or rotating " +
                    "an anchor carries its handle. Off: the handles are separate objects."), handlesFollowAnchors));
            mode = (Mode)RiryToolGUI.Set(this, SettingsUndo, (int)mode, EditorGUILayout.Popup(
                new GUIContent("Mode", "Stretch: every bone is held on the curve, and stretches when the curve is longer. " +
                    "Keep length: only the first bone is held on the curve; the rest aim along it and keep their lengths."),
                (int)mode, ModeNames));
            target = (Target)RiryToolGUI.Set(this, SettingsUndo, (int)target, EditorGUILayout.Popup(
                new GUIContent("Constrain", "Deform bones: the curve's constraints go on the bones. Mechanism chain: they go on a " +
                    MechanismPrefix + " copy of the chain, and each bone follows its copy by a VRC Parent Constraint."),
                (int)target, TargetNames));
            spacing = (Spacing)RiryToolGUI.Set(this, SettingsUndo, (int)spacing, EditorGUILayout.Popup(
                new GUIContent("Spacing", "Where each bone sits along the curve, from the distances between the bones. " +
                    "Existing: at its own distance along the chain. Even: at equal distances."),
                (int)spacing, SpacingNames));
            twist = RiryToolGUI.Set(this, SettingsUndo, twist, EditorGUILayout.Toggle(
                new GUIContent("Twist from anchors", "Rotating an anchor twists the chain, blended from one anchor to the other. " +
                    "Off: the bones take their twist from the rig."), twist));
        }

        private void DrawRig(List<Transform> bones)
        {
            EditorGUILayout.LabelField("Rig", EditorStyles.boldLabel);
            if (bones.Count == 0) return;
            string error = ChainError(bones);
            if (error != null)
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }
            bool rigged = RigRoots(bones[0]).Count > 0;
            if (rigged)
            {
                // Constraint report of the rig in the scene.
                EditorGUILayout.LabelField("This rig: " + Report(bones) + ".", EditorStyles.wordWrappedLabel);
            }
            else
            {
                DrawRestFit(bones);
                EditorGUILayout.LabelField(new GUIContent("Generate adds " + PlannedConstraints(bones.Count) + " constraints.",
                    "Depth is reported once the rig is made: the longest run of constraints that wait on each other."),
                    EditorStyles.wordWrappedLabel);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(rigged))
                {
                    if (GUILayout.Button("Generate rig")) { Act(() => Generate(bones)); RiryToolGUI.ExitAfterModal(); }
                }
                using (new EditorGUI.DisabledScope(!rigged))
                {
                    if (GUILayout.Button("Remove rig")) { Act(() => RemoveRig(bones)); RiryToolGUI.ExitAfterModal(); }
                }
            }
        }

        // Rest fit error: how far the bones are from the curve fitted to them.
        private void DrawRestFit(List<Transform> bones)
        {
            ConstraintBezierCurve.Fit fit = ConstraintBezierCurve.FitTo(RestPositions(bones), spacing == Spacing.Even);
            int worst = 0;
            float total = 0f;
            for (int i = 0; i < fit.Error.Length; i++)
            {
                total += fit.Error[i];
                if (fit.Error[i] > fit.Error[worst]) worst = i;
            }
            string distances = "largest " + fit.Error[worst].ToString("0.####") + " m (" + bones[worst].name + "), average " +
                (total / fit.Error.Length).ToString("0.####") + " m";
            EditorGUILayout.LabelField(mode == Mode.Stretch
                    ? "Rest fit: bones move onto the curve by " + distances + "."
                    : "Rest fit: no bone moves at rest; the curve passes the bones at " + distances + ".",
                EditorStyles.wordWrappedLabel);
        }
    }
}

// ============================================================================
// GLOSSARY: Constraint Tool's Bezier tab
// Both window files are one partial class. The curve math is in
// Editor/VRChat/ConstraintTools/ConstraintBezier/ConstraintBezierCurve.cs.
// ============================================================================
//
// FIELDS (ConstraintBezierWindow.cs; [S] = serialized)
//   chainStart / chainEnd  [S] Transform  First and last bone of the chain (TryCollectChain).
//   handlesFollowAnchors   [S] bool       Handle Start/End are children of Anchor Start/End (on) or of the rig (off).
//   mode                   [S] Mode       Stretch (every node on its sample) or KeepLength (only the first).
//   target                 [S] Target     DeformBones (constraints on the bones) or MechanismChain (on a Bezier_ copy;
//                                         each bone gets a VRC Parent Constraint to its copy).
//   spacing                [S] Spacing    Existing (each node at its distance along the chain) or Even.
//   twist                  [S] bool       Twist from anchors: per-node Twist helpers are the aims' world up.
//   status                     string     Message at the bottom, also logged by Act(action) after Generate rig and
//                                         Remove rig.  scroll: Vector2, the tab's scroll position.
//   UndoName "Bezier rig"; SettingsUndo "Bezier settings" (RiryToolGUI.Set on every field).
//   RigPrefix "BezierRig_", MechanismPrefix "Bezier_"; MinAimDistance 1e-5: shorter cannot be aimed along.
//
// THE RIG (Generate): BezierRig_<start>, a sibling of the chain start at its position
//   Anchor Start (P0), Anchor End (P3)    At the first and last bone; the rig's rotation.
//   Handle Start (P1), Handle End (P2)    Fitted by least squares; under their anchors or the rig.
//   Samples/Sample NN                     VRC Position Constraint: P0..P3 at ConstraintBezierCurve.Weights(t).
//   Twist/Twist NN                        VRC Rotation Constraint: Anchor Start (1 - t), Anchor End (t). Twist on only.
//   Nodes (bones or Bezier_ copies)       VRC Position Constraint to their sample (Stretch: all; Keep length: the first),
//                                         VRC Aim Constraint at the next sample (the last at Handle End, or the sample
//                                         before when Handle End is on it), up from its Twist helper or the rig.
//                                         Aim and up axes are the node's rest axes, so the rest rotation is unchanged.
//
// METHODS
//   UseSelection(active, selected)  Hierarchy entry: start = active, end = deepest selected descendant.
//   ChainError(bones)               Why the chain cannot be rigged, or null.
//   FindRig / FindMechanism         BezierRig_/Bezier_ + start name among the start's siblings.
//   PlannedConstraints(nodes)       Count Generate adds for the current options.
//   Generate(bones)                 Builds the rig as one Undo step; refuses when a rig or Bezier_ chain exists.
//   Finish(constraint, sources, weights)  Sources with zero offsets; weight 1, world space, Locked, active.
//   AddAim(node, aimAt, direction, upFrom)  The node's aim, exact at rest.
//   RigRoots / InRig / IsRigConstraint  The rig's objects, and a bone constraint whose every source is inside them.
//   RigConstraints(bones) / Report(bones)  Constraint report: count and Depth of the rig in the scene.
//   Depth(...)                      Longest run of constraints waiting on each other (sources, world up, parents).
//   RemoveRig(bones)                Removes BezierRig_, Bezier_ and the bones' rig constraints as one Undo step;
//                                   refuses objects saved in the prefab itself; asks first, listing what goes.
//   OnGUI / DrawChain / DrawOptions / DrawRig / DrawRestFit (this file)  Bone chain, Options, Rig (rest fit or
//                                   report, Generate rig, Remove rig), status.
