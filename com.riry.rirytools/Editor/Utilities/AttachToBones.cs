using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
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
}

// ============================================================================
// GLOSSARY: Attach To bone lookup
// Attach To (a Utilities tool): the popup is Editor/UI/Utilities/AttachToWindow.cs; the Hierarchy entry is in Editor/UI/RiryToolsRightClickMenu.cs.
// ============================================================================
//
// AttachToBones.Find        Humanoid bones; else the Avatar's bone map, by name; else name matching, breadth first.
//                           The objects being attached are excluded.
//   MatchName               Name matching: drops IgnoredParts and digits, reads the side (L, R, Left, Right), then
//                           looks the rest up in CentreNames or SidedNames (RightOf for the right side).
