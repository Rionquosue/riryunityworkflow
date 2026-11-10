using System;
using System.Collections.Generic;
using System.Reflection;

namespace Riry.RiryTools.Editor
{
    /// <summary>Finds a type by full name in any loaded assembly, for optional packages read by reflection.</summary>
    internal static class RiryTypes
    {
        private static readonly Dictionary<string, Type> Cache = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>The type with this full name ("Namespace.Outer+Inner"), or null; results are cached.</summary>
        internal static Type Named(string fullName)
        {
            Type found;
            if (Cache.TryGetValue(fullName, out found)) return found;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    found = assembly.GetType(fullName, false);
                }
                catch (Exception)
                {
                    found = null;
                }
                if (found != null) break;
            }
            Cache[fullName] = found;
            return found;
        }
    }
}
