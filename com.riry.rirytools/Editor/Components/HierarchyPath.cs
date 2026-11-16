using System.Collections.Generic;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>A transform's place in the hierarchy, from its root down: "Avatar/Body".</summary>
    internal static class HierarchyPath
    {
        internal static string Of(Transform transform, string separator = "/")
        {
            var names = new List<string>();
            for (Transform t = transform; t != null; t = t.parent) names.Insert(0, t.name);
            return string.Join(separator, names);
        }
    }
}
