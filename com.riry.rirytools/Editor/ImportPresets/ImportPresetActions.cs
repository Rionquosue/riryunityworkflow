using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// What the import preset menu entries do. The entries themselves are in
    /// Editor/UI/RiryMenus.cs and the windows in Editor/UI/ImportPresetWindows.cs.
    /// </summary>
    internal static class ImportPresetActions
    {
        internal static void ChooseFolderPreset()
        {
            ImportPresetFolderWindow.Open(SelectedFolders());
        }

        internal static bool ValidateChooseFolderPreset()
        {
            return SelectedFolders().Count > 0;
        }

        internal static void SaveToLibrary()
        {
            ImportPresetSaveWindow.Open(SelectedImporter());
        }

        internal static bool ValidateSaveToLibrary()
        {
            return ImportPresetLibrary.CanSave(SelectedImporter());
        }

        internal static void OpenLibrary()
        {
            EditorUtility.RevealInFinder(ImportPresetLibrary.EnsureRoot());
        }

        /// <summary>
        /// Selected folders, the folder of each selected file, or, after a click
        /// on empty space in the Project window, the folder being shown.
        /// </summary>
        internal static List<string> SelectedFolders()
        {
            var folders = new List<string>();
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string folder = AssetDatabase.IsValidFolder(path) ? path : ImportPresetMarker.ParentFolder(path);
                if (folder != null && IsWritableFolder(folder) && !folders.Contains(folder)) folders.Add(folder);
            }
            if (folders.Count == 0)
            {
                string active = ActiveFolder();
                if (active != null && IsWritableFolder(active)) folders.Add(active);
            }
            return folders;
        }

        private static string ActiveFolder()
        {
            // Unity has no public API for the folder shown in the Project window.
            MethodInfo method = typeof(ProjectWindowUtil).GetMethod("GetActiveFolderPath",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null) return null;
            try
            {
                string path = method.Invoke(null, null) as string;
                return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) ? path : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static bool IsWritableFolder(string folder)
        {
            if (folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal)) return true;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(folder);
            return package != null && (package.source == UnityEditor.PackageManager.PackageSource.Embedded ||
                package.source == UnityEditor.PackageManager.PackageSource.Local);
        }

        private static AssetImporter SelectedImporter()
        {
            if (Selection.assetGUIDs.Length != 1) return null;
            string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return null;
            return AssetImporter.GetAtPath(path);
        }
    }

}
