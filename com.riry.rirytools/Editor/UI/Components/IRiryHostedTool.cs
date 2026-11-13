using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// A tool window that draws parts of itself outside IMGUI (Menu Tool's radial preview): hosted in a tab, it is told
    /// the window it is actually drawn in.
    /// </summary>
    internal interface IRiryHostedTool
    {
        EditorWindow Surface { set; }

        /// <summary>Another tab is shown: take anything drawn outside IMGUI off the window.</summary>
        void NotDrawn();
    }
}

// ============================================================================
// GLOSSARY: Hosted tool interface
// ============================================================================
//
// IRiryHostedTool (this file)
//   Surface              The window the tool is actually drawn in (the Main Menu), set on every draw.
//   NotDrawn()           Another tab was drawn: take anything drawn outside IMGUI off the window (Menu Tool's radial).
//   Implemented by MenuToolWindow (Editor/VRChat/UI/MenuTool/); used by RiryToolsMainMenu (Editor/UI/RiryToolsMainMenu.cs).
