using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using BlackStartX.GestureManager.Editor.Modules.Vrc3.Params;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The open submenus of a Gesture Manager radial (RadialMenu._menuPath and OpenMenu, internal to Gesture Manager 3.9),
    /// shared by Menu Tool's radial preview (GestureManagerRadial.cs) and its Play Mode entry state (GestureManagerEntryState.cs).
    /// </summary>
    internal static class GestureManagerMenuPath
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        // The radial's open pages, root first.
        internal static readonly FieldInfo Pages = typeof(RadialMenu).GetField("_menuPath", Members);
        private static readonly MethodInfo OpenMenu = typeof(RadialMenu).GetMethod("OpenMenu", Members, null,
            new[] { typeof(VRCExpressionsMenu), typeof(Vrc3Param), typeof(float) }, null);

        /// <summary>False when this Gesture Manager lacks either member.</summary>
        internal static bool Available
        {
            get { return Pages != null && OpenMenu != null; }
        }

        /// <summary>Clears the radial's open pages, then opens each menu of <paramref name="path"/> in order, root first.</summary>
        internal static void Open(RadialMenu radial, IList<VRCExpressionsMenu> path)
        {
            var pages = (IList)Pages.GetValue(radial);
            pages.Clear();
            foreach (VRCExpressionsMenu menu in path) OpenMenu.Invoke(radial, new object[] { menu, null, 0f });
        }
    }
}

// ============================================================================
// GLOSSARY: Gesture Manager menu path
// ============================================================================
//
// GestureManagerMenuPath (this file)
//   Pages                 RadialMenu._menuPath: the open pages (read by GestureManagerRadial.Path and its cursor).
//   Available             Both members exist in this Gesture Manager.
//   Open(radial, path)    Clears the pages and calls OpenMenu per menu: the preview's Navigate and the entry state's replay.
