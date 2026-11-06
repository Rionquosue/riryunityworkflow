using System;
using System.Collections.Generic;
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
                    string source = SourceFolder();
                    if (source == null)
                    {
                        Once("RiryTools.UnpackNoPackage", () => RiryLog.Warn(RiryLog.ProjectFolder, "The RiryTools package could not be " +
                            "located, so its project files were not copied into " + AssetPath + "."));
                        return;
                    }
                    var copied = new List<string>();
                    if (Unpack(source, Path.Combine(RiryToolsPaths.ProjectRoot, AssetPath), copied) > 0)
                    {
                        // Files the package ships come back when missing (delete one to get the package's copy); said each time.
                        RiryLog.Info(RiryLog.ProjectFolder, "Copied " + copied.Count + " missing file(s) from the package into " + AssetPath +
                            ":\n" + string.Join("\n", copied));
                        AssetDatabase.Refresh();
                    }
                }
                catch (Exception exception)
                {
                    // This runs after every script reload, so a lasting problem is reported once per editor session.
                    Once("RiryTools.UnpackProblem." + exception.Message, () =>
                        RiryLog.Exception(RiryLog.ProjectFolder, "Could not copy its files into " + AssetPath, exception));
                }
            };
        }

        private static void Once(string key, Action report)
        {
            if (SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, true);
            report();
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
        /// included, so shipped assets keep their GUIDs). Returns the number copied;
        /// <paramref name="copiedFiles"/>, when given, receives each copied file's path.
        /// </summary>
        internal static int Unpack(string source, string destination, List<string> copiedFiles = null)
        {
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return 0;
            int copied = 0;
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, file.Substring(source.Length).TrimStart('/', '\\'));
                if (File.Exists(target)) continue;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target);
                }
                catch (Exception exception)
                {
                    throw new IOException("Could not copy " + target + ": " + exception.Message, exception);
                }
                copied++;
                if (copiedFiles != null) copiedFiles.Add(target);
            }
            return copied;
        }
    }
}

// ============================================================================
// GLOSSARY: RiryTools project folder
// ============================================================================
//
// RiryToolsProjectFolder (this file)
//   AssetPath            "Assets/RiryTools": files the tools make or the package ships for the project.
//   static constructor   After each script reload, copies the package's ProjectFolder~ files that are missing from
//                        Assets/RiryTools (Unpack), logs each file copied, then refreshes. A failure, or a package that
//                        can't be located, is logged once per editor session (Once, SessionState), naming the file.
//   Ensure()             Creates Assets/RiryTools when needed; returns its path.
//   Unpack(source, destination, copiedFiles)  Copies missing files, .meta included; returns how many and lists them.
