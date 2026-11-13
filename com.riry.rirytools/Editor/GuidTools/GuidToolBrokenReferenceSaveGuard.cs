using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Interrupts saving a scene, or the prefab open in Prefab Mode, that holds broken
    /// asset references in memory (references whose asset GUID no longer exists): the
    /// save goes ahead only after CONTINUE ANYWAY is clicked three times
    /// (GuidToolBrokenSaveWindow). Missing scripts are not counted. Only the open scene or prefab
    /// being saved is checked, and nothing is checked while a scene is loading
    /// (GuidToolBrokenReferenceMonitor.Loading).
    /// </summary>
    internal sealed class GuidToolBrokenReferenceSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (GuidToolBrokenReferenceMonitor.Loading) return paths;
            var allowed = new List<string>();
            foreach (string path in paths)
            {
                List<BrokenReference> broken = FindBroken(path);
                if (broken.Count > 0 && !GuidToolBrokenSaveWindow.Confirm(path, Describe(broken)))
                {
                    RiryLog.Warn(RiryLog.GuidTools, GuidToolReferenceRepair.Title + ": saving " + path + " was aborted; it has " + broken.Count +
                        " broken asset reference(s). Repair them with Tools > RiryTools > GUID Tools.", AssetDatabase.LoadMainAssetAtPath(path));
                    continue;
                }
                allowed.Add(path);
            }
            return allowed.ToArray();
        }

        /// <summary>One broken reference: where it is used, and the missing asset's GUID (null when Unity does not know it).</summary>
        internal sealed class BrokenReference
        {
            internal string Where;
            internal string Guid;
        }

        /// <summary>Broken asset references in the loaded scene or Prefab Mode prefab at the path; none when it is not open.</summary>
        internal static List<BrokenReference> FindBroken(string path)
        {
            var roots = new List<GameObject>();
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) roots.AddRange(scene.GetRootGameObjects());
            }
            else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == path) roots.Add(stage.prefabContentsRoot);
            }
            var broken = new List<BrokenReference>();
            bool? fileNamesMissing = null;
            foreach (GameObject root in roots)
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    string objectPath = HierarchyPath.Of(transform);
                    if (PrefabUtility.IsPrefabAssetMissing(transform.gameObject))
                        broken.Add(new BrokenReference { Where = objectPath + " > Prefab", Guid = MissingPrefabGuid(transform.gameObject) });
                    foreach (Component component in transform.GetComponents<Component>())
                    {
                        // A missing script shows as a null component; scripts are not in scope yet.
                        if (component == null) continue;
                        SerializedProperty property = new SerializedObject(component).GetIterator();
                        while (property.Next(true))
                        {
                            // A reference to an asset that is gone keeps its instance ID but has no object.
                            if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script" ||
                                property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0)
                                continue;
                            string guid;
                            if (!PointsAtMissingAsset(property.objectReferenceInstanceIDValue, path, ref fileNamesMissing, out guid)) continue;
                            broken.Add(new BrokenReference
                            {
                                Where = objectPath + " > " + ObjectNames.NicifyVariableName(component.GetType().Name) + " > " +
                                    FieldName(property.propertyPath),
                                Guid = guid
                            });
                        }
                    }
                }
            }
            return broken;
        }

        /// <summary>
        /// Whether an empty reference is a broken GUID: its asset GUID no longer exists. An
        /// empty reference whose GUID still exists, such as one left over from a scene opened
        /// earlier, is not broken. One with no GUID to look up counts only when the file on
        /// disk itself names a missing asset.
        /// </summary>
        private static bool PointsAtMissingAsset(int instanceId, string path, ref bool? fileNamesMissing, out string guid)
        {
            long localId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instanceId, out guid, out localId) && !string.IsNullOrEmpty(guid))
                return GuidToolReferenceRepair.IsMissing(guid);
            guid = null;
            if (fileNamesMissing == null) fileNamesMissing = FileNamesMissingAsset(path);
            return fileNamesMissing.Value;
        }

        // The prefab asset a prefab instance came from, read from its instance's source reference.
        private static string MissingPrefabGuid(GameObject instance)
        {
            Object handle = PrefabUtility.GetPrefabInstanceHandle(instance);
            if (handle == null) return null;
            SerializedProperty source = new SerializedObject(handle).FindProperty("m_SourcePrefab");
            string guid;
            long localId;
            return source != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source.objectReferenceInstanceIDValue, out guid, out localId) &&
                !string.IsNullOrEmpty(guid) ? guid : null;
        }

        // "m_Materials.Array.data[0]" as "Materials[0]".
        private static string FieldName(string propertyPath)
        {
            var parts = new List<string>();
            foreach (string part in propertyPath.Replace(".Array.data[", "[").Split('.')) parts.Add(ObjectNames.NicifyVariableName(part));
            return string.Join(".", parts);
        }

        /// <summary>
        /// "Skin.mat in Avatar/Body > Mesh Renderer > Materials[0] (GUID:...)" for each reference: the
        /// missing asset's last known name from the GUID remap ledger or Git history, or "Unknown asset".
        /// </summary>
        private static List<string> Describe(List<BrokenReference> broken)
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BrokenReference reference in broken)
            {
                if (reference.Guid != null) guids.Add(reference.Guid);
            }
            Dictionary<string, string> names = GuidToolReferenceRepair.LastKnownNames(guids);
            var lines = new List<string>();
            foreach (BrokenReference reference in broken)
            {
                string name;
                if (reference.Guid == null || !names.TryGetValue(reference.Guid, out name)) name = "Unknown asset";
                lines.Add(name + " in " + reference.Where + " (GUID:" + (reference.Guid ?? "unknown") + ")");
            }
            return lines;
        }

        private static bool FileNamesMissingAsset(string path)
        {
            string fullPath = System.IO.Path.Combine(RiryToolsPaths.ProjectRoot, path);
            if (!System.IO.File.Exists(fullPath) || !GuidToolRegenerate.LooksLikeText(fullPath, new byte[4096])) return false;
            foreach (KeyValuePair<long, string> reference in GuidToolReferenceRepair.FindGuidReferences(System.IO.File.ReadAllBytes(fullPath)))
            {
                if (GuidToolReferenceRepair.IsMissing(reference.Value)) return true;
            }
            return false;
        }
    }
}

// ============================================================================
// GLOSSARY: Broken reference save guard
// ============================================================================
//
// GuidToolBrokenReferenceSaveGuard / GuidToolBrokenSaveWindow (Editor/GuidTools/GuidToolBrokenReferenceSaveGuard.cs, Editor/UI/GuidTools/GuidToolBrokenSaveWindow.cs): interrupt saving a scene or
//   Prefab Mode prefab with broken asset references (FindBroken: empty references whose GUID is missing),
//   listing each as "<last known name> in <object > component > field> (GUID:...)";
//   CONTINUE ANYWAY needs three clicks ([2], [1]). Skipped while GuidToolBrokenReferenceMonitor.Loading.
