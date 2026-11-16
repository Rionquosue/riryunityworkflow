using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
    /// <summary>Blocks saving a scene or prefab whose load failed, so the file on disk stays intact.</summary>
    internal sealed class ScriptLoadGuardSaveBlocker : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            var allowed = new List<string>();
            foreach (string path in paths)
            {
                if (!ScriptLoadGuard.IsGuarded(path))
                {
                    allowed.Add(path);
                    continue;
                }
                RiryLog.Error(RiryLog.ScriptLoadGuard, "SCRIPT CAUSING FAILED LOAD: saving " + path +
                    " was blocked because it did not load correctly; the file on disk was kept.", AssetDatabase.LoadMainAssetAtPath(path));
                EditorApplication.delayCall += ScriptLoadGuardFailureWindow.Open;
            }
            return allowed.ToArray();
        }
    }
}

// ============================================================================
// GLOSSARY: Script Load Guard save blocker
// ============================================================================
//
// ScriptLoadGuardSaveBlocker (this file): removes blocked files from every save.
