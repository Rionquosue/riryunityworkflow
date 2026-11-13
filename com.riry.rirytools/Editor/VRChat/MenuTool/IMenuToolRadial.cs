using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// A radial menu preview the Menu Tool draws: implemented by the Gesture Manager bridge
    /// (Editor/VRChat/MenuTool/GestureManager), which only compiles when Gesture Manager is installed.
    /// </summary>
    internal interface IMenuToolRadial : IDisposable
    {
        /// <summary>Shows a menu tree; values of parameters it already had are kept.</summary>
        void Show(VRCExpressionsMenu root, VRCExpressionParameters parameters);

        /// <summary>Opens the menus from the root down to the last one.</summary>
        void Navigate(IList<VRCExpressionsMenu> path);

        /// <summary>The menus open now, from the root ("" at Gesture Manager's own main page).</summary>
        List<VRCExpressionsMenu> Path { get; }

        /// <summary>
        /// Draws it: rect in GUI coordinates of the current OnGUI (for the mouse), local the same spot in root's coordinates
        /// (where the element is placed).
        /// </summary>
        void Draw(Rect rect, Rect local, VisualElement root);

        /// <summary>Takes it off the window (another tab is shown).</summary>
        void Hide();

        float Value(string parameter);

        /// <summary>Sets every parameter of the menu back to its default.</summary>
        void ResetValues();

        /// <summary>Every parameter write the preview makes: name and value.</summary>
        event Action<string, float> Written;

        /// <summary>
        /// A right-click on a slice: the control it shows, or null for a slice that shows none (Back, Quick Actions,
        /// Gesture Manager's own pages). The click never presses the slice.
        /// </summary>
        event Action<VRCExpressionsMenu.Control> ContextClicked;
    }
}

// ============================================================================
// GLOSSARY: Menu Tool radial preview interface
// Implemented by GestureManager/GestureManagerRadial.cs (needs Gesture Manager).
// ============================================================================
//
// IMenuToolRadial (this file)           The radial preview: Show(root, parameters), Navigate(path), Path, Draw(rect, local, root), Hide, Value,
//                            Written (each parameter write), ContextClicked (a right-clicked slice's control, null for one that
//                            shows none). Implemented by GestureManagerRadial (GestureManager/ folder).
