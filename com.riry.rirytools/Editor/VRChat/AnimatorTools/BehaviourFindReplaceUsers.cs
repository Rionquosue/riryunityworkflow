using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Find and Replace's shared-controller check: every scene, prefab and asset in Assets whose file names the
    /// controller's GUID, closed scenes included. Reads text-serialized files (Unity's default); binary ones are not seen.
    /// </summary>
    internal static class BehaviourFindReplaceUsers
    {
        private static readonly HashSet<string> Searched = new HashSet<string> { ".prefab", ".unity", ".asset", ".controller", ".overridecontroller" };

        /// <summary>The asset paths that reference the controller, the controller itself left out.</summary>
        internal static List<string> Of(AnimatorController controller)
        {
            var users = new List<string>();
            string path = AssetDatabase.GetAssetPath(controller);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return users;
            string needle = "guid: " + guid;
            string assets = Application.dataPath;
            foreach (string file in Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories))
            {
                if (!Searched.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
                string asset = "Assets" + file.Substring(assets.Length).Replace('\\', '/');
                if (asset == path) continue;
                try
                {
                    if (File.ReadAllText(file).Contains(needle)) users.Add(asset);
                }
                catch (IOException exception)
                {
                    RiryLog.Warn(RiryLog.FindReplace, "Could not read " + asset + " while looking for users of " + path + ": " + exception.Message);
                }
            }
            return users;
        }
    }
}
