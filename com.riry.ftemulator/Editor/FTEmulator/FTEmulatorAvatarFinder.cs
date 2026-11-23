using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Finds the avatar the user chose. The object itself is used while it exists; once it is gone (VRCFury's build
    /// on entering Play Mode replaces it, and with it its GUID and instance) the scene's avatars are searched by the
    /// saved hierarchy path, then by a fuzzy name: case, "(Clone)", bracketed suffixes, trailing numbers and separators
    /// are ignored. Active avatars win over inactive ones.
    /// </summary>
    internal static class FTEmulatorAvatarFinder
    {
        /// <summary>The object picked in the window this Editor session; may be destroyed.</summary>
        internal static GameObject Chosen;

        internal static void Choose(GameObject avatar)
        {
            Chosen = avatar;
            FTEmulatorSettings.AvatarName = avatar != null ? avatar.name : "";
            FTEmulatorSettings.AvatarPath = avatar != null ? PathOf(avatar.transform) : "";
        }

        internal static bool HasChoice
        {
            get { return Chosen != null || FTEmulatorSettings.AvatarName.Length > 0; }
        }

        /// <summary>The chosen avatar as it exists now, or null.</summary>
        internal static GameObject Find()
        {
            if (Chosen != null && Chosen.activeInHierarchy) return Chosen;
            string name = Chosen != null ? Chosen.name : FTEmulatorSettings.AvatarName;
            string path = Chosen != null ? PathOf(Chosen.transform) : FTEmulatorSettings.AvatarPath;
            if (string.IsNullOrEmpty(name)) return null;
            string wanted = Normalize(name);
            GameObject best = null;
            int bestScore = int.MaxValue;
            foreach (VRCAvatarDescriptor descriptor in Object.FindObjectsOfType<VRCAvatarDescriptor>(true))
            {
                GameObject candidate = descriptor.gameObject;
                int score;
                if (PathOf(candidate.transform) == path) score = 0;
                else if (candidate.name == name) score = 2;
                else
                {
                    string normalized = Normalize(candidate.name);
                    if (normalized == wanted) score = 4;
                    else if (normalized.Length > 0 && wanted.Length > 0 && (normalized.Contains(wanted) || wanted.Contains(normalized))) score = 6;
                    else continue;
                }
                if (!candidate.activeInHierarchy) score++;
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            if (best != null && best.activeInHierarchy) Chosen = best;
            return best != null ? best : Chosen;
        }

        /// <summary>Whether two avatars are the same one by the rules above (the same object, path or fuzzy name).</summary>
        internal static bool Same(GameObject a, GameObject b)
        {
            if (a == null || b == null) return false;
            if (a == b || PathOf(a.transform) == PathOf(b.transform)) return true;
            string x = Normalize(a.name), y = Normalize(b.name);
            return x.Length > 0 && (x == y || x.Contains(y) || y.Contains(x));
        }

        internal static string Normalize(string name)
        {
            var result = new StringBuilder(name.Length);
            int depth = 0;
            foreach (char c in name.ToLowerInvariant())
            {
                if (c == '(' || c == '[') depth++;
                else if ((c == ')' || c == ']') && depth > 0) depth--;
                else if (depth == 0 && char.IsLetterOrDigit(c)) result.Append(c);
            }
            string text = result.ToString();
            if (text.EndsWith("clone")) text = text.Substring(0, text.Length - 5);
            return text.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (Transform at = transform; at != null; at = at.parent) parts.Insert(0, at.name);
            return string.Join("/", parts);
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator avatar finder
// ============================================================================
//
// FTEmulatorAvatarFinder (this file)
//   Chosen / Choose(avatar)  The window's pick; its name and hierarchy path go to FTEmulatorSettings.
//   Find()                   The chosen object while active; else the scene's VRCAvatarDescriptors scored: same path 0,
//                            same name 2, same Normalize()d name 4, one Normalize()d name inside the other 6; +1 inactive.
//   Same(a, b)               The same avatar by object, path or fuzzy name (matching a Gesture Manager's avatar).
//   Normalize(name)          Lowercase letters and digits only, bracketed parts, "clone" and trailing digits removed.
