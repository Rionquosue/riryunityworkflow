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
                    if (target.parent != bone && !NeedsUnpack(target)) Undo.SetTransformParent(target, bone, UndoName);
                }
            }
            Undo.CollapseUndoOperations(group);
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

    /// <summary>Finds an avatar's bones: humanoid, then the Avatar's bone map, then name matching.</summary>
    internal static class AttachToBones
    {
        // Name matching: name parts dropped before matching (rig prefixes such as VRoid's J_Bip_C_).
        private static readonly HashSet<string> IgnoredParts = new HashSet<string> { "j", "bip", "c", "def", "mixamorig", "bone" };
        // Name matching: a bone's name, without its side, to the bone. Sided ones are the left bone.
        private static readonly Dictionary<string, HumanBodyBones> CentreNames = new Dictionary<string, HumanBodyBones>
        {
            { "hips", HumanBodyBones.Hips }, { "hip", HumanBodyBones.Hips }, { "pelvis", HumanBodyBones.Hips },
            { "spine", HumanBodyBones.Spine },
            { "chest", HumanBodyBones.Chest },
            { "upperchest", HumanBodyBones.UpperChest },
            { "neck", HumanBodyBones.Neck },
            { "head", HumanBodyBones.Head },
            { "jaw", HumanBodyBones.Jaw }
        };
        private static readonly Dictionary<string, HumanBodyBones> SidedNames = new Dictionary<string, HumanBodyBones>
        {
            { "shoulder", HumanBodyBones.LeftShoulder }, { "clavicle", HumanBodyBones.LeftShoulder },
            { "upperarm", HumanBodyBones.LeftUpperArm }, { "arm", HumanBodyBones.LeftUpperArm },
            { "lowerarm", HumanBodyBones.LeftLowerArm }, { "forearm", HumanBodyBones.LeftLowerArm }, { "elbow", HumanBodyBones.LeftLowerArm },
            { "hand", HumanBodyBones.LeftHand }, { "wrist", HumanBodyBones.LeftHand },
            { "upperleg", HumanBodyBones.LeftUpperLeg }, { "thigh", HumanBodyBones.LeftUpperLeg }, { "upleg", HumanBodyBones.LeftUpperLeg },
            { "lowerleg", HumanBodyBones.LeftLowerLeg }, { "leg", HumanBodyBones.LeftLowerLeg }, { "knee", HumanBodyBones.LeftLowerLeg },
            { "calf", HumanBodyBones.LeftLowerLeg }, { "shin", HumanBodyBones.LeftLowerLeg },
            { "foot", HumanBodyBones.LeftFoot }, { "ankle", HumanBodyBones.LeftFoot },
            { "toe", HumanBodyBones.LeftToes }, { "toes", HumanBodyBones.LeftToes }, { "toebase", HumanBodyBones.LeftToes },
            { "eye", HumanBodyBones.LeftEye }
        };
        // The left bone to the right one.
        private static readonly Dictionary<HumanBodyBones, HumanBodyBones> RightOf = new Dictionary<HumanBodyBones, HumanBodyBones>
        {
            { HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder },
            { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm },
            { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm },
            { HumanBodyBones.LeftHand, HumanBodyBones.RightHand },
            { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg },
            { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg },
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot },
            { HumanBodyBones.LeftToes, HumanBodyBones.RightToes },
            { HumanBodyBones.LeftEye, HumanBodyBones.RightEye }
        };
        // Splits a name into words: "LeftUpperArm", "Hand_L", "J_Bip_L_UpperArm", "upperarm.R".
        private static readonly Regex Parts = new Regex("[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+");

        /// <summary>
        /// The avatar's bones. Objects under any of the excluded transforms are never taken
        /// as bones (the objects being attached).
        /// </summary>
        internal static Dictionary<HumanBodyBones, Transform> Find(GameObject avatar, ICollection<Transform> excluded, out AttachToSource source)
        {
            var bones = new Dictionary<HumanBodyBones, Transform>();
            source = AttachToSource.None;
            if (avatar == null) return bones;
            List<Transform> all = BreadthFirst(avatar.transform, excluded);

            Animator animator = avatar.GetComponent<Animator>();
            if (animator == null || animator.avatar == null)
            {
                animator = all.Select(each => each.GetComponent<Animator>()).FirstOrDefault(each => each != null && each.avatar != null);
            }

            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                for (var bone = (HumanBodyBones)0; bone < HumanBodyBones.LastBone; bone++)
                {
                    Transform found = animator.GetBoneTransform(bone);
                    if (found != null && all.Contains(found)) bones[bone] = found;
                }
                if (bones.Count > 0)
                {
                    source = AttachToSource.Humanoid;
                    return bones;
                }
            }

            if (animator != null && animator.avatar != null)
            {
                var byName = new Dictionary<string, HumanBodyBones>();
                for (var bone = (HumanBodyBones)0; bone < HumanBodyBones.LastBone; bone++) byName[bone.ToString().ToLowerInvariant()] = bone;
                foreach (HumanBone mapped in animator.avatar.humanDescription.human)
                {
                    HumanBodyBones bone;
                    if (mapped.humanName == null || !byName.TryGetValue(mapped.humanName.Replace(" ", "").ToLowerInvariant(), out bone)) continue;
                    Transform found = all.FirstOrDefault(each => each.name == mapped.boneName);
                    if (found != null && !bones.ContainsKey(bone)) bones[bone] = found;
                }
                if (bones.Count > 0)
                {
                    source = AttachToSource.AvatarBoneMap;
                    return bones;
                }
            }

            // Breadth first, so the bone nearest the avatar root wins.
            foreach (Transform each in all)
            {
                HumanBodyBones bone;
                if (each != avatar.transform && MatchName(each.name, out bone) && !bones.ContainsKey(bone)) bones[bone] = each;
            }
            if (bones.Count > 0) source = AttachToSource.NameMatching;
            return bones;
        }

        /// <summary>The bone a common limb name stands for, such as "Chest", "LeftHand", "Hand_L" or "J_Bip_L_UpperArm".</summary>
        internal static bool MatchName(string name, out HumanBodyBones bone)
        {
            bone = HumanBodyBones.LastBone;
            // "mixamorig:LeftHand": only what follows the last colon.
            int colon = name.LastIndexOf(':');
            if (colon >= 0) name = name.Substring(colon + 1);

            int side = 0; // 1 left, 2 right
            var words = new List<string>();
            foreach (Match part in Parts.Matches(name))
            {
                string word = part.Value.ToLowerInvariant();
                if (char.IsDigit(word[0]) || IgnoredParts.Contains(word)) continue;
                if (word == "l" || word == "left") { side = 1; continue; }
                if (word == "r" || word == "right") { side = 2; continue; }
                // "lefthand", written all in lower case.
                if (words.Count == 0 && word.Length > 4 && word.StartsWith("left")) { side = 1; word = word.Substring(4); }
                else if (words.Count == 0 && word.Length > 5 && word.StartsWith("right")) { side = 2; word = word.Substring(5); }
                words.Add(word);
            }
            string joined = string.Concat(words);
            if (joined.Length == 0) return false;

            if (side == 0) return CentreNames.TryGetValue(joined, out bone);
            HumanBodyBones left;
            if (!SidedNames.TryGetValue(joined, out left)) return false;
            bone = side == 1 ? left : RightOf[left];
            return true;
        }

        private static List<Transform> BreadthFirst(Transform root, ICollection<Transform> excluded)
        {
            var all = new List<Transform>();
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Transform each = queue.Dequeue();
                if (excluded != null && excluded.Contains(each)) continue;
                all.Add(each);
                foreach (Transform child in each) queue.Enqueue(child);
            }
            return all;
        }
    }

    /// <summary>
    /// Remembers the last avatar selected in the Hierarchy: the nearest object above (or at) the
    /// selection with a VRCAvatarDescriptor, else the nearest with a humanoid Animator.
    /// </summary>
    [InitializeOnLoad]
    internal static class AttachToAvatarTracker
    {
        internal const string LastAvatarKey = "RiryTools.AttachTo.LastAvatar";
        // Found by name, so the VRChat SDK is not needed.
        private const string DescriptorTypeName = "VRCAvatarDescriptor";

        static AttachToAvatarTracker()
        {
            Selection.selectionChanged += SelectionChanged;
        }

        internal static GameObject LastAvatar
        {
            get
            {
                int id = SessionState.GetInt(LastAvatarKey, 0);
                return id == 0 ? null : EditorUtility.InstanceIDToObject(id) as GameObject;
            }
        }

        private static void SelectionChanged()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null || EditorUtility.IsPersistent(selected)) return;
            GameObject avatar = AvatarOf(selected.transform);
            if (avatar != null) SessionState.SetInt(LastAvatarKey, avatar.GetInstanceID());
        }

        internal static GameObject AvatarOf(Transform selected)
        {
            for (Transform each = selected; each != null; each = each.parent)
            {
                if (each.GetComponents<Component>().Any(component => component != null && component.GetType().Name == DescriptorTypeName)) return each.gameObject;
            }
            for (Transform each = selected; each != null; each = each.parent)
            {
                Animator animator = each.GetComponent<Animator>();
                if (animator != null && animator.avatar != null && animator.avatar.isHuman) return each.gameObject;
            }
            return null;
        }
    }
}

// ============================================================================
// GLOSSARY: Attach To
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
// AttachToBones.Find        Humanoid bones; else the Avatar's bone map, by name; else name matching, breadth first.
//                           The objects being attached are excluded.
//   MatchName               Name matching: drops IgnoredParts and digits, reads the side (L, R, Left, Right), then
//                           looks the rest up in CentreNames or SidedNames (RightOf for the right side).
// AttachToAvatarTracker     Keeps the last selected avatar (VRCAvatarDescriptor, else humanoid Animator) in SessionState
//                           "RiryTools.AttachTo.LastAvatar" as an instance ID; LastAvatar reads it.
