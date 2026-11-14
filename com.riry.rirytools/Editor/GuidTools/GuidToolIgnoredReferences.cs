using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Broken references the user chose to Ignore, per project (UserSettings): a missing GUID, or a missing object
    /// ("path (file ID n)"). Ignored ones are listed apart in Broken References, are left out of the save check and its
    /// warning, and do not prompt when a scene or prefab opens.
    /// </summary>
    internal static class GuidToolIgnoredReferences
    {
        private const string Key = "RiryTools.GuidTools.Ignored";

        internal static HashSet<string> All()
        {
            string stored = EditorUserSettings.GetConfigValue(Key) ?? "";
            return new HashSet<string>(stored.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        }

        internal static bool Contains(string reference)
        {
            return !string.IsNullOrEmpty(reference) && All().Contains(reference);
        }

        internal static void Add(string reference)
        {
            HashSet<string> all = All();
            if (all.Add(reference)) Save(all);
        }

        internal static void Remove(string reference)
        {
            HashSet<string> all = All();
            if (all.Remove(reference)) Save(all);
        }

        private static void Save(HashSet<string> all)
        {
            EditorUserSettings.SetConfigValue(Key, string.Join("\n", all.OrderBy(r => r, StringComparer.Ordinal)));
        }
    }
}
