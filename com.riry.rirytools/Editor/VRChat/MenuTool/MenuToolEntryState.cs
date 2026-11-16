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

        // The avatar root's GlobalObjectId (its saved scene's GUID and its file ID), so a renamed or moved avatar is still found.
        public string Avatar = "";
        // A VRCFury avatar is replaced in Play Mode, so it is found by its hierarchy path ("Root/Child") instead.
        public bool Vrcfury;
        public string Path = "";
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

        /// <summary>The avatar's GlobalObjectId, or null for an object in a scene that has not been saved (it has no lasting id).</summary>
        internal static string IdOf(GameObject avatar)
        {
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(avatar);
            return id.identifierType == 2 && !id.assetGUID.Empty() ? id.ToString() : null;
        }

        internal static void Clear()
        {
            SessionState.EraseString(Key);
        }
    }
}
