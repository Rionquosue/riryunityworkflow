using System.Collections.Generic;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 13: objects left out of copying. Entries can be picked on either avatar; one on
    /// the target is mapped back to its source object through the map. With Include children
    /// on, everything below an entry is left out too. The avatar root can't be ignored, and
    /// empty entries are dropped.
    /// </summary>
    internal static class AvatarIgnoreList
    {
        /// <summary>Drops empty entries from the list, in place.</summary>
        internal static void DropEmpty(List<GameObject> entries)
        {
            entries.RemoveAll(entry => entry == null);
        }

        /// <summary>The source objects to leave out.</summary>
        internal static HashSet<Transform> Resolve(AvatarMigrationMap map, List<GameObject> entries, bool includeChildren)
        {
            var ignored = new HashSet<Transform>();
            foreach (GameObject entry in entries)
            {
                if (entry == null) continue;
                Transform source = entry.transform;
                if (source != map.SourceRoot && !source.IsChildOf(map.SourceRoot)) source = map.SourceOf(source);
                if (source == null || source == map.SourceRoot) continue;
                if (!includeChildren)
                {
                    ignored.Add(source);
                    continue;
                }
                foreach (Transform below in source.GetComponentsInChildren<Transform>(true)) ignored.Add(below);
            }
            return ignored;
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant ignore list (IgnoreList.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarIgnoreList (this file)
//   DropEmpty(entries)                        Removes empty entries.
//   Resolve(map, entries, includeChildren)    Source objects to leave out; target entries mapped back (map.SourceOf);
//                                             never the avatar root; with includeChildren, everything below too.
