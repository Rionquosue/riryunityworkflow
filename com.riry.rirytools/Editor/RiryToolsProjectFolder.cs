#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The project's Assets/RiryTools folder: files the package ships in its
    /// ProjectFolder~ folder are copied there (an installed package is
    /// read-only), and tools save generated assets there when they have no better
    /// place. A file that already exists is never overwritten, so edits survive
    /// package updates; delete a file to get the package's copy back.
    /// </summary>
    [InitializeOnLoad]
    internal static class RiryToolsProjectFolder
    {
        internal const string AssetPath = "Assets/RiryTools";
        private const string SourceFolderName = "ProjectFolder~";

        static RiryToolsProjectFolder()
        {
            // After the first import, so the AssetDatabase can take the new files.
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (Unpack(SourceFolder(), Path.Combine(RegenerateAssetGuid.ProjectRootPath, AssetPath)) > 0)
                        AssetDatabase.Refresh();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("RiryTools could not unpack its files into " + AssetPath + ":\n" + exception.Message);
                }
            };
        }

        /// <summary>The package's ProjectFolder~, or null when the package cannot be located.</summary>
        private static string SourceFolder()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RiryToolsProjectFolder).Assembly);
            return package == null ? null : Path.Combine(package.resolvedPath, SourceFolderName);
        }

        /// <summary>Creates Assets/RiryTools when needed and returns its asset path.</summary>
        internal static string Ensure()
        {
            if (!AssetDatabase.IsValidFolder(AssetPath)) AssetDatabase.CreateFolder("Assets", "RiryTools");
            return AssetPath;
        }

        /// <summary>
        /// Copies every file under <paramref name="source"/> that is missing from
        /// <paramref name="destination"/>, keeping the folder layout (.meta files
        /// included, so shipped assets keep their GUIDs). Returns the number copied.
        /// </summary>
        internal static int Unpack(string source, string destination)
        {
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return 0;
            int copied = 0;
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, file.Substring(source.Length).TrimStart('/', '\\'));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target);
                copied++;
            }
            return copied;
        }
    }
}
#endif
