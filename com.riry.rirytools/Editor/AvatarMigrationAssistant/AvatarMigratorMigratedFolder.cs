using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 10: every file the tool creates goes into one root folder inside Assets,
    /// picked by the user and saved with the settings, at the original path with the leading
    /// "Assets/" removed (files from Packages/ go to root/Packages/...). Missing folders are
    /// created, and an existing file is never overwritten.
    /// </summary>
    internal static class AvatarMigratorMigratedFolder
    {
        /// <summary>A valid root folder, asking for one when none is set; false when the user cancels.</summary>
        internal static bool Ensure(AvatarMigratorSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.MigratedFolder) && AssetDatabase.IsValidFolder(settings.MigratedFolder)) return true;
            string chosen = Choose(settings.MigratedFolder);
            if (chosen == null) return false;
            settings.MigratedFolder = chosen;
            settings.Save();
            return true;
        }

        /// <summary>A folder inside Assets picked with the folder panel, as "Assets/...", or null.</summary>
        internal static string Choose(string current)
        {
            while (true)
            {
                string start = string.IsNullOrEmpty(current) ? Application.dataPath : Path.Combine(RiryToolsPaths.ProjectRoot, current);
                string picked = EditorUtility.OpenFolderPanel("Folder for migrated files", start, "");
                if (string.IsNullOrEmpty(picked)) return null;
                string asset = IconCapFiles.AssetPath(picked);
                if (asset != null) return asset;
                if (!EditorUtility.DisplayDialog(AvatarMigrationAssistant.Title, "Pick a folder inside Assets.", "Pick again", "Cancel")) return null;
            }
        }

        /// <summary>A new, unique asset path for a copy of the asset at assetPath, with its folders made.</summary>
        internal static string Destination(string root, string assetPath, string extension = null)
        {
            string relative = assetPath.StartsWith("Assets/", StringComparison.Ordinal) ? assetPath.Substring("Assets/".Length) : assetPath;
            if (extension != null) relative = Path.ChangeExtension(relative, extension);
            string destination = (root.TrimEnd('/') + "/" + relative).Replace('\\', '/');
            MakeFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
            return AssetDatabase.GenerateUniqueAssetPath(destination);
        }

        private static void MakeFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            MakeFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant migrated folder (AvatarMigratorMigratedFolder.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarMigratorMigratedFolder (this file)
//   Ensure(settings)          settings.MigratedFolder, asked for (Choose) when it is not a folder; saved with the settings.
//   Choose(current)           OpenFolderPanel; only a folder inside Assets is taken.
//   Destination(root, path, extension)  root + the path without "Assets/" (Packages/... kept), folders made with
//                             AssetDatabase.CreateFolder, AssetDatabase.GenerateUniqueAssetPath so nothing is overwritten.
