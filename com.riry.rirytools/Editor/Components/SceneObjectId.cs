using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// A scene object kept as a string (its GlobalObjectId), so it can be saved in a file and found again; used by
    /// Blendshape Sync's saved jobs and IconCap's captures.
    /// </summary>
    internal static class SceneObjectId
    {
        /// <summary>The object's id, or "" for none.</summary>
        internal static string Of(Object target)
        {
            return target == null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
        }

        /// <summary>The object with this id as a T, or null when it is not loaded, is gone or is not a T.</summary>
        internal static T Find<T>(string id) where T : Object
        {
            GlobalObjectId parsed;
            if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out parsed)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as T;
        }
    }
}

// ============================================================================
// GLOSSARY: scene object ids
// ============================================================================
//
// SceneObjectId (this file)
//   Of(target)               The object's GlobalObjectId as a string; "" for none.
//   Find<T>(id)              The object with that id, or null.
