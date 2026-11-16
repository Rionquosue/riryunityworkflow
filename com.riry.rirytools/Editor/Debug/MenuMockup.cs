using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Menu Mockup: lay out an example editor window from blocks. Opens two windows,
    /// Edit (the mockup itself, Editor/UI/Debug/MenuMockupEditWindow.cs) and Components (a button per
    /// block type to click or drag in, New, Save Mockup and Load). Every component is a block, any
    /// block holds blocks, and blocks lay out like flexbox. Mockups are saved as
    /// annotated .yml files in Documents/RiryTools/Mockups, which Unity never compiles.
    /// The menu entry, Tools > RiryTools > Menu Mockup, is in Editor/UI/RiryToolsMenuBar.cs.
    /// </summary>
    internal static class MenuMockup
    {
        internal const string FileExtension = "yml";

        internal static string Folder
        {
            get { return RiryToolsPaths.Documents("Mockups"); }
        }

        internal static void Open()
        {
            MenuMockupEditWindow edit = MenuMockupEditWindow.Open();
            MenuMockupComponentsWindow.Open(edit);
        }

        /// <summary>
        /// The window's shape: the smallest whole-number ratio when its terms are small
        /// (1280 x 720 is 16:9), otherwise the longer side over the shorter to one decimal
        /// (1000 x 631 is 1.6:1).
        /// </summary>
        internal static string AspectRatio(float width, float height)
        {
            int w = Mathf.RoundToInt(width), h = Mathf.RoundToInt(height);
            if (w <= 0 || h <= 0) return "";
            int divisor = Gcd(w, h);
            if (w / divisor <= MaxRatioTerm && h / divisor <= MaxRatioTerm) return (w / divisor) + ":" + (h / divisor);
            return w >= h
                ? ((float)w / h).ToString("0.0", CultureInfo.InvariantCulture) + ":1"
                : "1:" + ((float)h / w).ToString("0.0", CultureInfo.InvariantCulture);
        }

        // Largest term shown as a whole ratio, e.g. 21:9.
        private const int MaxRatioTerm = 32;

        private static int Gcd(int a, int b)
        {
            while (b != 0)
            {
                int rest = a % b;
                a = b;
                b = rest;
            }
            return a;
        }
    }

    /// <summary>
    /// One block. Blocks are kept in one flat list; a block's children are the blocks
    /// whose Parent is its Id, in list order. The window is the root block, Id 0.
    /// </summary>
    [Serializable]
    internal sealed class MenuMockupBlock
    {
        public int Id;
        public int Parent = -1;
        public string Type = MenuMockupTypes.Block;
        public string Label = "";
        public string Content = "";
        // Flexbox: "row" or "column"; "start", "center", "end", "space-between", "space-around";
        // "stretch", "start", "center", "end"; grow (span); padding (buffer) in pixels.
        public string Direction = "column";
        public string Justify = "start";
        public string Align = "stretch";
        public float Grow;
        public float Padding;
        // Set by the side handles; -1 is automatic.
        public float Width = -1;
        public float Height = -1;
        // In a Tabs block: the tab (0 is the first) this block is on.
        public int Tab;
        // A Tabs block: the tab shown.
        public int ActiveTab;
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup
// Menu Mockup (a Debug tool): the windows are in Editor/UI/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockup                Open() opens Edit and Components; Folder (Documents/RiryTools/Mockups);
//                           AspectRatio(width, height): "16:9" when the reduced terms are 32 or less, else "1.6:1".
// MenuMockupBlock               One block: Id, Parent (0 = the window), Type, Label, Content, Direction, Justify,
//                           Align, Grow, Padding, Width/Height (-1 = automatic), Tab (the tab it is on inside a Tabs
//                           block), ActiveTab (a Tabs block's shown tab).
