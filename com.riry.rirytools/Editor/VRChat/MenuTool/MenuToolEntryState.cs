using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Menu manager's entry state: the parameter writes made in the radial preview, in order, and the submenu it showed,
    /// for one avatar. Kept in SessionState, so it lasts until Unity quits or Reset entry state. The Gesture Manager bridge
    /// replays it on that avatar once Gesture Manager has set it up in Play Mode.
    /// </summary>
    [Serializable]
    internal sealed class MenuToolEntryState
    {
        internal const string Key = "RiryTools.MenuTool.EntryState";

        [Serializable]
        internal struct Write
        {
            public string Name;
            public float Value;
        }

        // The avatar root's scene name and hierarchy path ("Root/Child").
        public string Scene = "";
        public string Avatar = "";
        // The submenu shown, as a path of control names in the assembled menu ("" is the root).
        public string Menu = "";
        public List<Write> Writes = new List<Write>();

        internal static MenuToolEntryState Load()
        {
            string json = SessionState.GetString(Key, "");
            if (json.Length == 0) return null;
            try { return JsonUtility.FromJson<MenuToolEntryState>(json); }
            catch (ArgumentException) { return null; }
        }

        internal static bool Saved
        {
            get { return SessionState.GetString(Key, "").Length > 0; }
        }

        internal void Save()
        {
            SessionState.SetString(Key, JsonUtility.ToJson(this));
        }

        internal static void Clear()
        {
            SessionState.EraseString(Key);
        }

        internal static string PathOf(Transform transform)
        {
            var names = new List<string>();
            for (Transform t = transform; t != null; t = t.parent) names.Insert(0, t.name);
            return string.Join("/", names);
        }
    }
}
