#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.IO;
using UnityEditor;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Applies a folder's import preset configuration to files imported for
    /// the first time. Reimports never change settings, so manual changes stay.
    /// </summary>
    internal sealed class ImportPresetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            // Only a first import (no .meta settings yet) is configured. Files
            // brought in with their .meta, such as from a .unitypackage, keep theirs.
            if (!assetImporter.importSettingsMissing) return;
            string path = assetPath;
            if (string.IsNullOrEmpty(path) || Directory.Exists(path)) return;
            if (string.Equals(Path.GetFileName(path), ImportPresetMarker.FileName, StringComparison.OrdinalIgnoreCase))
                return;

            string folder = ImportPresetMarker.FindMarkerFolder(path, File.Exists);
            if (folder == null) return;
            string configuration = ImportPresetMarker.ReadConfiguration(folder);
            if (configuration == null) return;
            string applied;
            ImportPresetLibrary.TryApply(assetImporter, configuration, out applied);
        }
    }
}
#endif
