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
    /// Edit (the mockup itself, Editor/UI/MenuMockupWindows.cs) and Components (the
    /// blocks to drag in, Save Mockup and Load). Every component is a block, any
    /// block holds blocks, and blocks lay out like flexbox. Mockups are saved as
    /// annotated .yml files in Documents/RiryTools/Mockups, which Unity never compiles.
    /// The menu entry, Tools > RiryTools > Menu Mockup, is in Editor/UI/RiryMenus.cs.
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
    internal sealed class MockupBlock
    {
        public int Id;
        public int Parent = -1;
        public string Type = MockupTypes.Block;
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
    }

    /// <summary>The component types, and what Label and Content mean for each (null when unused).</summary>
    internal static class MockupTypes
    {
        internal const string Window = "Window";
        internal const string Block = "Block";

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
            new Kind { Name = "Scroll View", LabelName = "Name" },
            new Kind { Name = "Image", ContentName = "Caption", Default = "Image" },
            new Kind { Name = "Separator" },
            new Kind { Name = "Space" }
        };

        internal static Kind Find(string name)
        {
            foreach (Kind kind in All)
            {
                if (string.Equals(kind.Name, name, StringComparison.OrdinalIgnoreCase)) return kind;
            }
            return All[0];
        }
    }

    /// <summary>
    /// The saved form of a mockup: an annotated .yml file without the editing tools.
    /// Only values that differ from the defaults are written. Comments name each
    /// block and its text, so the file can be read by a person as well as loaded.
    /// </summary>
    internal static class MockupFile
    {
        internal static void Save(string path, List<MockupBlock> blocks, float width, float height)
        {
            var text = new StringBuilder();
            text.Append("# RiryTools Menu Mockup\n");
            text.Append("# One block per UI element; \"blocks\" lists the blocks inside it, in order.\n");
            text.Append("# Layout follows flexbox: direction (row/column), justify, align, grow (share of\n");
            text.Append("# the free space), padding (pixels), width/height (pixels; left out = automatic).\n");
            text.Append("# label and content are the element's texts; their meaning depends on the type.\n\n");
            text.Append("name: ").Append(Quote(Path.GetFileNameWithoutExtension(path))).Append('\n');
            text.Append("width: ").Append(Number(width)).Append('\n');
            text.Append("height: ").Append(Number(height)).Append("    # aspect ")
                .Append(MenuMockup.AspectRatio(width, height)).Append('\n');
            MockupBlock root = blocks.Find(block => block.Id == 0) ?? new MockupBlock { Id = 0, Type = MockupTypes.Window };
            text.Append("# The window\n");
            text.Append("root:\n");
            WriteBlock(text, blocks, root, "  ", false);
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }

        private static void WriteBlock(StringBuilder text, List<MockupBlock> blocks, MockupBlock block, string indent, bool item)
        {
            var defaults = new MockupBlock();
            var lines = new List<string>();
            if (block.Id != 0) lines.Add("type: " + block.Type);
            if (block.Label.Length > 0) lines.Add("label: " + Quote(block.Label));
            if (block.Content.Length > 0) lines.Add("content: " + Quote(block.Content));
            if (block.Direction != defaults.Direction) lines.Add("direction: " + block.Direction);
            if (block.Justify != defaults.Justify) lines.Add("justify: " + block.Justify);
            if (block.Align != defaults.Align) lines.Add("align: " + block.Align);
            if (block.Grow != 0) lines.Add("grow: " + Number(block.Grow));
            if (block.Padding != 0) lines.Add("padding: " + Number(block.Padding));
            if (block.Width >= 0) lines.Add("width: " + Number(block.Width));
            if (block.Height >= 0) lines.Add("height: " + Number(block.Height));
            List<MockupBlock> children = MockupModel.Children(blocks, block.Id);
            if (children.Count > 0) lines.Add("blocks:");
            if (lines.Count == 0) lines.Add("type: " + block.Type);

            for (int i = 0; i < lines.Count; i++)
            {
                string prefix = item ? (i == 0 ? indent.Substring(0, indent.Length - 2) + "- " : indent) : indent;
                text.Append(prefix).Append(lines[i]).Append('\n');
            }
            foreach (MockupBlock child in children)
            {
                text.Append(indent).Append("  # ").Append(Describe(blocks, child)).Append('\n');
                WriteBlock(text, blocks, child, indent + "    ", true);
            }
        }

        // "Click Button "Apply"", "Block "Footer", row of 2 blocks".
        private static string Describe(List<MockupBlock> blocks, MockupBlock block)
        {
            var parts = new List<string> { block.Type };
            if (block.Label.Length > 0) parts[0] += " \"" + OneLine(block.Label) + "\"";
            if (block.Content.Length > 0) parts.Add(OneLine(block.Content));
            int count = MockupModel.Children(blocks, block.Id).Count;
            if (count > 0) parts.Add(block.Direction + " of " + count + (count == 1 ? " block" : " blocks"));
            return string.Join(", ", parts);
        }

        private static string OneLine(string text)
        {
            return text.Replace("\r", "").Replace("\n", " ");
        }

        private static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"";
        }

        /// <summary>Reads a file written by Save; unknown keys are ignored. Throws on a file it cannot read.</summary>
        internal static List<MockupBlock> Load(string path, out float width, out float height)
        {
            width = 0;
            height = 0;
            var lines = new List<KeyValuePair<int, string>>();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = StripComment(raw).TrimEnd();
                if (line.Trim().Length == 0) continue;
                lines.Add(new KeyValuePair<int, string>(line.Length - line.TrimStart().Length, line.TrimStart()));
            }
            var blocks = new List<MockupBlock>();
            var root = new MockupBlock { Id = 0, Type = MockupTypes.Window };
            blocks.Add(root);
            int position = 0;
            while (position < lines.Count)
            {
                string key, value;
                Split(lines[position].Value, out key, out value);
                position++;
                if (key == "width") width = ParseFloat(value, 0);
                else if (key == "height") height = ParseFloat(value, 0);
                else if (key == "root") position = ReadBlock(lines, position, root, blocks, lines[position - 1].Key);
            }
            return blocks;
        }

        // Reads the keys of one block, indented deeper than its owner line; returns the next line.
        private static int ReadBlock(List<KeyValuePair<int, string>> lines, int position, MockupBlock block,
            List<MockupBlock> blocks, int ownerIndent)
        {
            int indent = -1;
            while (position < lines.Count)
            {
                int lineIndent = lines[position].Key;
                string text = lines[position].Value;
                bool item = text.StartsWith("- ", StringComparison.Ordinal);
                int keyIndent = item ? lineIndent + 2 : lineIndent;
                if (lineIndent <= ownerIndent) break;
                if (indent < 0) indent = keyIndent;
                if (item || keyIndent != indent) break;
                string key, value;
                Split(text, out key, out value);
                position++;
                if (key == "blocks")
                {
                    while (position < lines.Count && lines[position].Key > lineIndent &&
                           lines[position].Value.StartsWith("- ", StringComparison.Ordinal))
                    {
                        var child = new MockupBlock { Id = NextId(blocks), Parent = block.Id };
                        blocks.Add(child);
                        int itemIndent = lines[position].Key;
                        // The item's first key is on its "- " line.
                        lines[position] = new KeyValuePair<int, string>(itemIndent + 2, lines[position].Value.Substring(2));
                        position = ReadBlock(lines, position, child, blocks, itemIndent - 1 < lineIndent ? lineIndent : itemIndent - 1);
                    }
                    continue;
                }
                Apply(block, key, value);
            }
            return position;
        }

        private static void Apply(MockupBlock block, string key, string value)
        {
            switch (key)
            {
                case "type":
                    if (block.Id != 0) block.Type = MockupTypes.Find(Unquote(value)).Name;
                    break;
                case "label": block.Label = Unquote(value); break;
                case "content": block.Content = Unquote(value); break;
                case "direction": block.Direction = Unquote(value); break;
                case "justify": block.Justify = Unquote(value); break;
                case "align": block.Align = Unquote(value); break;
                case "grow": block.Grow = ParseFloat(value, 0); break;
                case "padding": block.Padding = ParseFloat(value, 0); break;
                case "width": block.Width = ParseFloat(value, -1); break;
                case "height": block.Height = ParseFloat(value, -1); break;
            }
        }

        private static int NextId(List<MockupBlock> blocks)
        {
            int id = 0;
            foreach (MockupBlock block in blocks) id = Math.Max(id, block.Id);
            return id + 1;
        }

        private static void Split(string text, out string key, out string value)
        {
            int colon = text.IndexOf(':');
            key = colon < 0 ? text.Trim() : text.Substring(0, colon).Trim();
            value = colon < 0 ? "" : text.Substring(colon + 1).Trim();
        }

        // Removes a "#" comment that is not inside quotes.
        private static string StripComment(string line)
        {
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\\' && quoted) i++;
                else if (line[i] == '"') quoted = !quoted;
                else if (line[i] == '#' && !quoted) return line.Substring(0, i);
            }
            return line;
        }

        private static string Unquote(string value)
        {
            if (value.Length < 2 || value[0] != '"' || value[value.Length - 1] != '"') return value;
            var text = new StringBuilder();
            for (int i = 1; i < value.Length - 1; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length - 1)
                {
                    i++;
                    text.Append(value[i] == 'n' ? '\n' : value[i]);
                }
                else text.Append(value[i]);
            }
            return text.ToString();
        }

        private static float ParseFloat(string value, float fallback)
        {
            float result;
            return float.TryParse(Unquote(value), NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }
    }

    /// <summary>Changes to the flat block list.</summary>
    internal static class MockupModel
    {
        internal static List<MockupBlock> Children(List<MockupBlock> blocks, int parent)
        {
            return blocks.FindAll(block => block.Parent == parent && block.Id != parent);
        }

        internal static MockupBlock Find(List<MockupBlock> blocks, int id)
        {
            return blocks.Find(block => block.Id == id);
        }

        /// <summary>Puts a block (already in the list or new) at index among the parent's other children.</summary>
        internal static void Place(List<MockupBlock> blocks, MockupBlock block, int parent, int index)
        {
            blocks.Remove(block);
            block.Parent = parent;
            List<MockupBlock> siblings = Children(blocks, parent);
            if (index >= 0 && index < siblings.Count) blocks.Insert(blocks.IndexOf(siblings[index]), block);
            else blocks.Add(block);
        }

        internal static MockupBlock Create(List<MockupBlock> blocks, string type)
        {
            int id = 0;
            foreach (MockupBlock block in blocks) id = Math.Max(id, block.Id);
            MockupTypes.Kind kind = MockupTypes.Find(type);
            return new MockupBlock { Id = id + 1, Type = kind.Name, Content = kind.Default ?? "" };
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup (model and file)
// The windows are in Editor/UI/MenuMockupWindows.cs; the menu entry is in RiryMenus.cs.
// ============================================================================
//
// MenuMockup                Open() opens Edit and Components; Folder (Documents/RiryTools/Mockups);
//                           AspectRatio(width, height): "16:9" when the reduced terms are 32 or less, else "1.6:1".
// MockupBlock               One block: Id, Parent (0 = the window), Type, Label, Content, Direction, Justify,
//                           Align, Grow, Padding, Width/Height (-1 = automatic).
// MockupTypes               The component types (Kind: Name, LabelName, ContentName, Default).
// MockupFile                Save(path, blocks, width, height) writes the annotated .yml (defaults left out);
//                           Load(path, out width, out height) reads it back.
// MockupModel               Children, Find, Place (move or insert at an index), Create.
