using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// References a file holds that nothing reads, which the broken-reference scan does not list: a material's texture slots
    /// its current shader does not have (left from an earlier shader), and in a prefab or an open scene, any reference outside
    /// the objects that actually hold a missing reference when loaded (stale override values, a script's leftover serialized
    /// data). A scene that is not open, and a scene's settings documents (render, lightmap, navmesh), are always listed.
    /// One instance per scanned file; the file is only loaded when asked about.
    /// </summary>
    internal sealed class GuidToolInactiveReferences
    {
        private static readonly Regex TexEnvName = new Regex(@"^\s*-\s+(\S+):\s*$");

        private readonly string path;
        private readonly byte[] bytes;
        private Shader shader;
        private bool loaded;
        // Prefab: the file IDs of the objects holding a missing reference, and whether one of them has no YAML document of
        // its own in the file (an object of a nested prefab instance, whose overrides live in a PrefabInstance document).
        private HashSet<long> live;
        private bool liveNested;
        private HashSet<long> documents;
        // Scene: the file IDs of its own GameObjects and components, and the PrefabInstance documents whose objects hold a
        // missing reference.
        private HashSet<long> sceneObjects;
        private HashSet<long> livePrefabInstances;

        internal GuidToolInactiveReferences(string path, byte[] bytes)
        {
            this.path = path;
            this.bytes = bytes;
        }

        /// <summary>Whether the reference at this offset (its "guid: " value) is read by nothing.</summary>
        internal bool Inactive(int offset)
        {
            if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) return UnusedTexture(offset);
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                LoadScene();
                if (live == null || !Document(offset, out int sceneClass, out long sceneDocument)) return false;
                if (sceneClass == 1001) return !livePrefabInstances.Contains(sceneDocument);
                return sceneObjects.Contains(sceneDocument) && !live.Contains(sceneDocument);
            }
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return false;
            LoadPrefab();
            if (live == null || !Document(offset, out int classId, out long document)) return false;
            // 1001: PrefabInstance, whose overrides land on the nested instance's objects.
            return classId == 1001 ? !liveNested : !live.Contains(document);
        }

        // The class and file ID of the YAML document ("--- !u!<class> &<id>") the offset is in.
        private bool Document(int offset, out int classId, out long id)
        {
            classId = 0;
            id = 0;
            for (int i = Math.Min(offset, bytes.Length - 1); i >= 0; i--)
            {
                if (bytes[i] != (byte)'-' || (i > 0 && bytes[i - 1] != (byte)'\n') || i + 3 > bytes.Length || bytes[i + 1] != (byte)'-' || bytes[i + 2] != (byte)'-') continue;
                int end = i;
                while (end < bytes.Length && bytes[end] != (byte)'\n') end++;
                Match match = Regex.Match(Encoding.ASCII.GetString(bytes, i, end - i), @"^--- !u!(\d+) &(-?\d+)");
                return match.Success && int.TryParse(match.Groups[1].Value, out classId) && long.TryParse(match.Groups[2].Value, out id);
            }
            return false;
        }

        // A texture slot (m_SavedProperties.m_TexEnvs) the material's shader does not have. A missing shader keeps everything.
        private bool UnusedTexture(int offset)
        {
            if (!loaded)
            {
                loaded = true;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                shader = material != null ? material.shader : null;
                if (shader != null && shader.name == "Hidden/InternalErrorShader") shader = null;
            }
            if (shader == null) return false;
            string name = null;
            int end = offset;
            while (end > 0)
            {
                int start = end;
                while (start > 0 && bytes[start - 1] != (byte)'\n') start--;
                string line = Encoding.ASCII.GetString(bytes, start, Math.Max(0, end - start)).TrimEnd('\r');
                string trimmed = line.Trim();
                if (name == null)
                {
                    Match match = TexEnvName.Match(line);
                    if (match.Success) name = match.Groups[1].Value;
                }
                else if (trimmed.EndsWith(":", StringComparison.Ordinal) && !trimmed.StartsWith("-", StringComparison.Ordinal))
                    return trimmed == "m_TexEnvs:" && shader.FindPropertyIndex(name) < 0;
                if (trimmed.StartsWith("---", StringComparison.Ordinal)) return false;
                end = start - 1;
            }
            return false;
        }

        // Loads the prefab once and notes which of its objects hold a missing reference (a reference Unity can't load).
        private void LoadPrefab()
        {
            if (loaded) return;
            loaded = true;
            UnityEngine.Object[] objects;
            try { objects = AssetDatabase.LoadAllAssetsAtPath(path); }
            catch (Exception) { return; }
            if (objects == null || objects.Length == 0) return;
            documents = Documents();
            live = new HashSet<long>();
            foreach (UnityEngine.Object item in objects)
            {
                if (item == null || !HoldsMissing(item)) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string guid, out long id)) continue;
                live.Add(id);
                if (!documents.Contains(id)) liveNested = true;
            }
        }

        // The open scene's GameObjects and components: each of its own (no prefab instance) by file ID, and each of a prefab
        // instance by its PrefabInstance document (GlobalObjectId.targetPrefabId), noted when it holds a missing reference.
        private void LoadScene()
        {
            if (loaded) return;
            loaded = true;
            Scene scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded) return;
            var objects = new List<UnityEngine.Object>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    objects.Add(transform.gameObject);
                    foreach (Component component in transform.GetComponents<Component>())
                    {
                        if (component != null) objects.Add(component);
                    }
                }
            }
            var ids = new GlobalObjectId[objects.Count];
            GlobalObjectId.GetGlobalObjectIdsSlow(objects.ToArray(), ids);
            live = new HashSet<long>();
            sceneObjects = new HashSet<long>();
            livePrefabInstances = new HashSet<long>();
            for (int i = 0; i < objects.Count; i++)
            {
                bool missing = HoldsMissing(objects[i]);
                if (ids[i].targetPrefabId != 0)
                {
                    if (missing) livePrefabInstances.Add((long)ids[i].targetPrefabId);
                    continue;
                }
                long id = (long)ids[i].targetObjectId;
                sceneObjects.Add(id);
                if (missing) live.Add(id);
            }
        }

        private static bool HoldsMissing(UnityEngine.Object item)
        {
            using (var serialized = new SerializedObject(item))
            {
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null &&
                        property.objectReferenceInstanceIDValue != 0)
                        return true;
                }
            }
            return false;
        }

        // The file IDs of the file's own YAML documents ("--- !u!<class> &<id>").
        private HashSet<long> Documents()
        {
            var ids = new HashSet<long>();
            foreach (Match match in Regex.Matches(Encoding.ASCII.GetString(bytes), @"^--- !u!\d+ &(-?\d+)", RegexOptions.Multiline))
            {
                if (long.TryParse(match.Groups[1].Value, out long id)) ids.Add(id);
            }
            return ids;
        }
    }
}

// ============================================================================
// GLOSSARY: GUID Tools inactive references
// ============================================================================
//
// GuidToolInactiveReferences (this file)
//   Inactive(offset)       A material texture slot its shader lacks (UnusedTexture), or in a
//                            prefab a reference in a document whose object holds no missing reference when loaded; a PrefabInstance
//                            document's references count only while an object of a nested instance holds one (LoadPrefab). In an
//                            open scene (LoadScene) the same by GlobalObjectId: its own objects by file ID, a PrefabInstance
//                            document's references while one of that instance's objects holds one; settings documents always count.
