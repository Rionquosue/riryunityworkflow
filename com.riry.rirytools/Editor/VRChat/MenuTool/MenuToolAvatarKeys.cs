using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// A GUID stamped on each avatar the Menu Tool plans for, kept in UserSettings (never on the avatar): avatars.json maps
    /// each avatar's GlobalObjectId to its key. An avatar in a scene not saved yet has no lasting id, so its key is kept
    /// for the session by instance id and moved into avatars.json when its scene is saved.
    /// </summary>
    [InitializeOnLoad]
    internal static class MenuToolAvatarKeys
    {
        private const string UnsavedKey = "RiryTools.MenuTool.UnsavedAvatars";

        [Serializable]
        private sealed class Entry
        {
            public string Object = "";
            public string Key = "";
        }

        [Serializable]
        private sealed class Registry
        {
            public List<Entry> Avatars = new List<Entry>();
        }

        static MenuToolAvatarKeys()
        {
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private static string RegistryPath
        {
            get { return Path.Combine(MenuToolPlan.Folder, "avatars.json"); }
        }

        internal static string KeyFor(GameObject avatar)
        {
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(avatar);
            if (Lasting(id))
            {
                Registry registry = Read();
                string text = id.ToString();
                Entry entry = registry.Avatars.FirstOrDefault(e => e.Object == text);
                if (entry == null)
                {
                    entry = new Entry { Object = text, Key = NewKey() };
                    registry.Avatars.Add(entry);
                    Write(registry);
                }
                return entry.Key;
            }
            Dictionary<int, string> unsaved = Unsaved();
            string key;
            if (!unsaved.TryGetValue(avatar.GetInstanceID(), out key))
            {
                key = NewKey();
                unsaved[avatar.GetInstanceID()] = key;
                WriteUnsaved(unsaved);
            }
            return key;
        }

        // An object in a saved scene or a prefab asset has a GlobalObjectId that lasts; one in an unsaved scene has none.
        private static bool Lasting(GlobalObjectId id)
        {
            return id.identifierType != 0 && !id.assetGUID.Empty();
        }

        private static string NewKey()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            Dictionary<int, string> unsaved = Unsaved();
            if (unsaved.Count == 0) return;
            Registry registry = null;
            foreach (KeyValuePair<int, string> pair in unsaved.ToList())
            {
                var avatar = EditorUtility.InstanceIDToObject(pair.Key) as GameObject;
                if (avatar == null)
                {
                    unsaved.Remove(pair.Key);
                    continue;
                }
                if (avatar.scene != scene) continue;
                GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(avatar);
                if (!Lasting(id)) continue;
                if (registry == null) registry = Read();
                registry.Avatars.RemoveAll(e => e.Object == id.ToString());
                registry.Avatars.Add(new Entry { Object = id.ToString(), Key = pair.Value });
                unsaved.Remove(pair.Key);
            }
            if (registry != null) Write(registry);
            WriteUnsaved(unsaved);
        }

        private static Registry Read()
        {
            var registry = new Registry();
            try
            {
                if (File.Exists(RegistryPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(RegistryPath), registry);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Reading " + RegistryPath, exception);
            }
            return registry;
        }

        private static void Write(Registry registry)
        {
            try
            {
                Directory.CreateDirectory(MenuToolPlan.Folder);
                File.WriteAllText(RegistryPath, JsonUtility.ToJson(registry, true));
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Saving " + RegistryPath, exception);
            }
        }

        // "instance id=key" pairs, one per line.
        private static Dictionary<int, string> Unsaved()
        {
            var map = new Dictionary<int, string>();
            foreach (string line in SessionState.GetString(UnsavedKey, "").Split('\n'))
            {
                int equals = line.IndexOf('=');
                int id;
                if (equals > 0 && int.TryParse(line.Substring(0, equals), out id)) map[id] = line.Substring(equals + 1);
            }
            return map;
        }

        private static void WriteUnsaved(Dictionary<int, string> map)
        {
            SessionState.SetString(UnsavedKey, string.Join("\n", map.Select(pair => pair.Key + "=" + pair.Value)));
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool avatar keys
// ============================================================================
//
// MenuToolAvatarKeys         A GUID per avatar, in UserSettings/RiryTools/MenuTool/avatars.json (GlobalObjectId -> key); an avatar
//                            in an unsaved scene keeps its key for the session (SessionState RiryTools.MenuTool.UnsavedAvatars,
//                            by instance id) until its scene is saved (sceneSaved), then it joins avatars.json.
