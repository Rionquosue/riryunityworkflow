using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Empty objects the Constraint Tool's Train, Bezier and Insertable prop tabs generate.</summary>
    internal static class ConstraintToolObjects
    {
        /// <summary>
        /// A new empty on that layer, recorded for Undo under <paramref name="undoName"/>, as a child of
        /// <paramref name="parent"/> with an identity local pose.
        /// </summary>
        internal static Transform CreateEmpty(string name, Transform parent, int layer, string undoName)
        {
            var created = new GameObject(name);
            created.layer = layer;
            Undo.RegisterCreatedObjectUndo(created, undoName);
            created.transform.SetParent(parent, false);
            return created.transform;
        }
    }
}

// ============================================================================
// GLOSSARY: Constraint Tool objects (shared by Train, Bezier and Insertable prop)
// ============================================================================
//
// ConstraintToolObjects (this file)
//   CreateEmpty(name, parent, layer, undoName)  A new empty child, layer set, recorded for Undo.
