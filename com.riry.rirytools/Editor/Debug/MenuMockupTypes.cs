using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>The component types, and what Label and Content mean for each (null when unused).</summary>
    internal static class MenuMockupTypes
    {
        internal const string Window = "Window";
        internal const string Block = "Block";
        internal const string Tabs = "Tabs";

        internal sealed class Kind
        {
            internal string Name;
            internal string LabelName;
            internal string ContentName;
            internal string Default;
        }

        internal static readonly Kind[] All =
        {
            new Kind { Name = Block, LabelName = "Name" },
            new Kind { Name = "Label", ContentName = "Text", Default = "Label" },
            new Kind { Name = "Text Box", LabelName = "Label", ContentName = "Text", Default = "Text" },
            new Kind { Name = "Asset Selector", LabelName = "Label", ContentName = "Asset type", Default = "Object" },
            new Kind { Name = "Fuzzy Search Box", ContentName = "Text", Default = "" },
            new Kind { Name = "Popup Button", LabelName = "Label", ContentName = "Options (comma separated)", Default = "Option A, Option B" },
            new Kind { Name = "Toggle Button", LabelName = "Label", ContentName = "On (on/off)", Default = "off" },
            new Kind { Name = "Radio Button", LabelName = "Label", ContentName = "On (on/off)", Default = "off" },
            new Kind { Name = "Click Button", ContentName = "Text", Default = "Button" },
            new Kind { Name = "Slider", LabelName = "Label", ContentName = "Value (0 to 1)", Default = "0.5" },
            new Kind { Name = "Number Field", LabelName = "Label", ContentName = "Value", Default = "0" },
            new Kind { Name = "Color Field", LabelName = "Label", ContentName = "Color (#RRGGBB)", Default = "#FFFFFF" },
            new Kind { Name = "Foldout", ContentName = "Title", Default = "Foldout" },
            new Kind { Name = "Help Box", LabelName = "Kind (Info/Warning/Error)", ContentName = "Message", Default = "Message" },
            new Kind { Name = "Progress Bar", ContentName = "Title", Default = "Progress" },
            new Kind { Name = "Toolbar", ContentName = "Buttons (comma separated)", Default = "One, Two" },
            new Kind { Name = Tabs, ContentName = "Tabs (comma separated)", Default = "Tab 1, Tab 2" },
            new Kind { Name = "Scroll View", LabelName = "Name" },
            new Kind { Name = "Image", ContentName = "Caption", Default = "Image" },
            new Kind { Name = "Separator" },
            new Kind { Name = "Space" }
        };

        /// <summary>A Tabs block's tab names (at least one).</summary>
        internal static List<string> TabNames(MenuMockupBlock block)
        {
            var names = new List<string>();
            foreach (string part in block.Content.Split(','))
            {
                if (part.Trim().Length > 0) names.Add(part.Trim());
            }
            if (names.Count == 0) names.Add("Tab 1");
            return names;
        }

        /// <summary>The tab a block inside a Tabs block shows on: its Tab, kept within the tabs there are.</summary>
        internal static int TabOf(MenuMockupBlock tabs, MenuMockupBlock child)
        {
            return Math.Max(0, Math.Min(child.Tab, TabNames(tabs).Count - 1));
        }

        internal static Kind Find(string name)
        {
            foreach (Kind kind in All)
            {
                if (string.Equals(kind.Name, name, StringComparison.OrdinalIgnoreCase)) return kind;
            }
            return All[0];
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup block types
// Menu Mockup (a Debug tool): the windows are in Editor/UI/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockupTypes               The component types (Kind: Name, LabelName, ContentName, Default). Tabs: Content is the
//                           comma-separated tab names (TabNames); TabOf keeps a block's tab within them.
