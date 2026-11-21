using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// GameObject/RiryTools/Invert Global Position: mirrors the selected objects to the other side of their avatar
    /// (InvertGlobalPosition), then asks each time whether to move them under the mirrored bone, unpacking prefabs only
    /// after the same warning as Attach To. One undo step for all of it.
    /// </summary>
    internal static class InvertGlobalPositionPrompts
    {
        internal static void Run()
        {
            List<Transform> selected = AttachTo.Selected();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(InvertGlobalPosition.UndoName);
            List<Transform> moved = InvertGlobalPosition.Mirror(selected);
            if (moved.Count == 0)
            {
                EditorUtility.DisplayDialog(InvertGlobalPosition.UndoName,
                    "Select objects inside an avatar (under a VRC Avatar Descriptor or an Animator), not the avatar root itself.", "OK");
                return;
            }
            // Objects under a left or right bone whose mirrored bone is not their parent already.
            var reparent = new List<KeyValuePair<Transform, Transform>>();
            foreach (Transform target in moved)
            {
                Transform bone = InvertGlobalPosition.MirroredBone(target);
                if (bone != null && target.parent != bone && !bone.IsChildOf(target)) reparent.Add(new KeyValuePair<Transform, Transform>(target, bone));
            }
            if (reparent.Count > 0 && EditorUtility.DisplayDialog("Move in hierarchy?",
                    "Move these under the bone on the other side?\n\n" + string.Join("\n", reparent.Select(p => p.Key.name + " → " + p.Value.name)) +
                    "\n\nTheir global position, rotation and scale stay the same; their local values change.", "Yes", "No"))
            {
                List<Transform> locked = reparent.Select(p => p.Key).Where(AttachTo.NeedsUnpack).ToList();
                if (locked.Count > 0)
                {
                    bool unpack = EditorUtility.DisplayDialog("Unpack prefabs?",
                        "Moving these in the hierarchy unpacks the prefab they are part of:\n\n" +
                        string.Join("\n", locked.Select(t => t.name + " (in " + PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject).name + ")")) +
                        "\n\nSkip these leaves them mirrored where they are.", "Unpack", "Skip these");
                    if (unpack) foreach (Transform target in locked) AttachTo.Unpack(target);
                    else reparent.RemoveAll(p => locked.Contains(p.Key));
                }
                foreach (KeyValuePair<Transform, Transform> pair in reparent)
                {
                    if (!InvertGlobalPosition.MoveUnder(pair.Key, pair.Value))
                        RiryLog.Warn(RiryLog.InvertGlobalPosition, pair.Key.name + " was mirrored but not moved under " + pair.Value.name +
                            ": its prefab instance could not be unpacked.", pair.Key);
                }
            }
            Undo.CollapseUndoOperations(group);
            RiryLog.Info(RiryLog.InvertGlobalPosition, "Mirrored " + string.Join(", ", moved.Select(t => t.name)) + " to the other side of the avatar.");
        }
    }
}

// ============================================================================
// GLOSSARY: InvertGlobalPositionPrompts
//   Run()                    The Hierarchy entry: InvertGlobalPosition.Mirror on the top-level selection (AttachTo.Selected),
//                            then, for objects under a left or right humanoid bone, "Move in hierarchy?" (Yes / No), the
//                            "Unpack prefabs?" warning for objects inside a prefab instance (Unpack / Skip these), and
//                            InvertGlobalPosition.MoveUnder. One undo step. Nothing to mirror: a dialog says what to select.
// ============================================================================
