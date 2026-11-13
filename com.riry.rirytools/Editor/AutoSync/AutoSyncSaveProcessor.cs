using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    internal sealed class AutoSyncSaveProcessor : AssetModificationProcessor
    {
        // Saves made through Unity's save pipeline, in addition to the scene and Prefab Mode events.
        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths) AutoSync.Saved(path);
            return paths;
        }
    }
}
