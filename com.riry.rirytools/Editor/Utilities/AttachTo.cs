using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Where Attach To found an avatar's bones.</summary>
    internal enum AttachToSource
    {
        None,
        // The Animator's humanoid bones.
        Humanoid,
        // The bones named in the Avatar's human description, when it is not humanoid.
        AvatarBoneMap,
        // Transform names, when there is no Avatar or it names no bones.
        NameMatching
    }

    /// <summary>
    /// Attach To: snaps the selected objects to the global position of one of an avatar's
    /// bones (rotation and scale are left alone), then asks "Move in hierarchy?"; Yes
    /// reparents them to the bone, keeping their global position, rotation and scale.
    /// A prefab is only unpacked after the Unpack prefabs? warning.
    /// </summary>
    internal static class AttachTo
    {
        internal const string UndoName = "Attach To";
        // The bones listed before any are added by hand.
        internal static readonly HumanBodyBones[] CommonBones =
        {
            HumanBodyBones.Head,
            HumanBodyBones.Chest,
            HumanBodyBones.Hips,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg
        };
        // Bones added with Add bone, by HumanBodyBones name, comma separated.
        internal const string AddedBonesKey = "RiryTools.AttachTo.AddedBones";
        // The most names a dialog lists before "and n more".
        private const int DialogNames = 10;

        /// <summary>The common bones, then the ones added with Add bone.</summary>
        internal static List<HumanBodyBones> ListedBones()
        {
            var bones = new List<HumanBodyBones>(CommonBones);
            foreach (string name in EditorPrefs.GetString(AddedBonesKey, "").Split(','))
            {
                HumanBodyBones bone;
                if (System.Enum.TryParse(name, out bone) && bone != HumanBodyBones.LastBone && !bones.Contains(bone)) bones.Add(bone);
            }
            return bones;
        }

        internal static void AddBone(HumanBodyBones bone)
        {
            List<HumanBodyBones> added = ListedBones().Skip(CommonBones.Length).ToList();
            if (CommonBones.Contains(bone) || added.Contains(bone)) return;
            added.Add(bone);
            EditorPrefs.SetString(AddedBonesKey, string.Join(",", added.Select(each => each.ToString())));
        }

        internal static void RemoveBone(HumanBodyBones bone)
        {
            List<HumanBodyBones> added = ListedBones().Skip(CommonBones.Length).Where(each => each != bone).ToList();
            EditorPrefs.SetString(AddedBonesKey, string.Join(",", added.Select(each => each.ToString())));
        }

        internal static bool IsAdded(HumanBodyBones bone)
        {
            return !CommonBones.Contains(bone) && ListedBones().Contains(bone);
        }

        internal static string Label(HumanBodyBones bone)
        {
            return ObjectNames.NicifyVariableName(bone.ToString());
        }

        /// <summary>The selected scene objects, top level only.</summary>
        internal static List<Transform> Selected()
        {
            return Selection.GetTransforms(SelectionMode.TopLevel).Where(each => each != null && !EditorUtility.IsPersistent(each)).ToList();
        }

        /// <summary>
        /// Snaps the objects to the bone's global position, asks Move in hierarchy?, and on Yes
        /// reparents them to the bone. One Undo step. An object that is the bone or one of its
        /// parents is left out.
        /// </summary>
        internal static void Attach(IList<Transform> objects, Transform bone, string boneLabel)
        {
            List<Transform> targets = objects.Where(each => each != null && !bone.IsChildOf(each)).ToList();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(UndoName, "An object can't be attached to itself or to a bone below it.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName + " " + boneLabel);

            foreach (Transform target in targets)
            {
                Undo.RecordObject(target, UndoName);
                target.position = bone.position;
            }

            bool move = EditorUtility.DisplayDialog("Move in hierarchy?",
                "Move " + Names(targets.Select(each => each.name)) + " under " + bone.name + "?\n\n" +
                "Their global position, rotation and scale stay the same; their local values change.",
                "Yes", "No");
            if (move)
            {
                List<Transform> locked = targets.Where(NeedsUnpack).ToList();
                if (locked.Count > 0)
                {
                    bool unpack = EditorUtility.DisplayDialog("Unpack prefabs?",
                        "Moving these in the hierarchy unpacks the prefab they are part of:\n\n" +
                        Names(locked.Select(each => each.name + " (in " + PrefabUtility.GetOutermostPrefabInstanceRoot(each.gameObject).name + ")")) +
                        "\n\nSkip these leaves them snapped where they are.",
                        "Unpack", "Skip these");
                    if (unpack)
                    {
                        foreach (Transform target in locked) Unpack(target);
                    }
                    else
                    {
                        targets.RemoveAll(locked.Contains);
                    }
                }
                foreach (Transform target in targets)
                {
                    // Undo.SetTransformParent keeps the global position, rotation and scale.
                    if (target.parent == bone) continue;
                    if (!NeedsUnpack(target)) Undo.SetTransformParent(target, bone, UndoName);
                    else
                        RiryLog.Warn(RiryLog.AttachTo, target.name + " was snapped to " + bone.name + " but not moved under it: its prefab " +
                            "instance could not be unpacked (for example a variant's base in Prefab Mode).", target);
                }
            }
            Undo.CollapseUndoOperations(group);
            RiryLog.Info(RiryLog.AttachTo, "Snapped " + Names(targets.Select(each => each.name)).Replace("\n", ", ") + " to " + bone.name +
                (move ? " and moved them under it." : "."), bone);
        }

        /// <summary>True when the object is inside a prefab instance and is not its outermost root, so it can't be reparented.</summary>
        private static bool NeedsUnpack(Transform target)
        {
            GameObject gameObject = target.gameObject;
            return PrefabUtility.IsPartOfPrefabInstance(gameObject) && PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject) != gameObject;
        }

        /// <summary>Unpacks the outermost prefab instance around the object, one level at a time, until it can be reparented.</summary>
        private static void Unpack(Transform target)
        {
            GameObject last = null;
            while (target != null && NeedsUnpack(target))
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(target.gameObject);
                // A root that did not unpack (one Unity can't unpack, such as a variant's base in Prefab Mode).
                if (root == null || root == last) return;
                last = root;
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            }
        }

        private static string Names(IEnumerable<string> names)
        {
            List<string> all = names.ToList();
            string shown = string.Join("\n", all.Take(DialogNames));
            return all.Count > DialogNames ? shown + "\nand " + (all.Count - DialogNames) + " more" : shown;
        }
    }
}

// ============================================================================
// GLOSSARY: Attach To
// Attach To (a Utilities tool): the popup is Editor/UI/Utilities/AttachToWindow.cs; the Hierarchy entry is in Editor/UI/RiryToolsRightClickMenu.cs.
// ============================================================================
//
// AttachToSource            Where the bones came from: Humanoid, AvatarBoneMap (the Avatar's human description), NameMatching.
// AttachTo                  The action.
//   UndoName                "Attach To": the Undo group, named "Attach To <bone>".
//   CommonBones             Head, Chest, Hips, both hands, both feet, both upper legs: listed before any added bone.
//   AddedBonesKey           EditorPrefs "RiryTools.AttachTo.AddedBones": bones added with Add bone, comma separated.
//   ListedBones / AddBone   The common bones then the added ones / remembers one more.
//   RemoveBone / IsAdded    Forgets an added bone / true for a bone added with Add bone (never a common one).
//   Label                   A bone's display name ("Left Upper Leg").
//   Selected                The selected scene objects, top level only.
//   Attach                  Snaps to the bone's global position, asks Move in hierarchy?; Yes reparents (keeping global
//                           position, rotation and scale) after the Unpack prefabs? warning for objects inside a prefab
//                           instance (Unpack or Skip these). Skips an object that is the bone or one of its parents.
//   NeedsUnpack / Unpack    Inside a prefab instance and not its outermost root / unpacks outermost roots until it is not.
//   DialogNames             10: names a dialog lists before "and n more".
