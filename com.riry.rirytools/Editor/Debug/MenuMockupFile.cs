using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The saved form of a mockup: an annotated .yml file without the editing tools.
    /// Only values that differ from the defaults are written. Comments name each
    /// block and its text, so the file can be read by a person as well as loaded.
    /// </summary>
    internal static class MenuMockupFile
    {
        internal static void Save(string path, List<MenuMockupBlock> blocks, float width, float height)
        {
            var text = new StringBuilder();
            text.Append("# RiryTools Menu Mockup\n");
            text.Append("# One block per UI element; \"blocks\" lists the blocks inside it, in order.\n");
            text.Append("# Layout follows flexbox: direction (row/column), justify, align, grow (share of\n");
            text.Append("# the free space), padding (pixels), width/height (pixels; left out = automatic).\n");
            text.Append("# label and content are the element's texts; their meaning depends on the type.\n");
            text.Append("# In a Tabs block, tab is the tab (0 is the first) a block is on; activeTab is the tab shown.\n\n");
            text.Append("name: ").Append(Quote(Path.GetFileNameWithoutExtension(path))).Append('\n');
            text.Append("width: ").Append(Number(width)).Append('\n');
            text.Append("height: ").Append(Number(height)).Append("    # aspect ")
                .Append(MenuMockup.AspectRatio(width, height)).Append('\n');
            MenuMockupBlock root = blocks.Find(block => block.Id == 0) ?? new MenuMockupBlock { Id = 0, Type = MenuMockupTypes.Window };
            text.Append("# The window\n");
            text.Append("root:\n");
            WriteBlock(text, blocks, root, "  ", false);
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }

        private static void WriteBlock(StringBuilder text, List<MenuMockupBlock> blocks, MenuMockupBlock block, string indent, bool item)
        {
            var defaults = new MenuMockupBlock();
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
            if (block.Tab != 0) lines.Add("tab: " + block.Tab);
            if (block.ActiveTab != 0) lines.Add("activeTab: " + block.ActiveTab);
            List<MenuMockupBlock> children = MenuMockupModel.Children(blocks, block.Id);
            if (children.Count > 0) lines.Add("blocks:");
            if (lines.Count == 0) lines.Add("type: " + block.Type);

            for (int i = 0; i < lines.Count; i++)
            {
                string prefix = item ? (i == 0 ? indent.Substring(0, indent.Length - 2) + "- " : indent) : indent;
                text.Append(prefix).Append(lines[i]).Append('\n');
            }
            foreach (MenuMockupBlock child in children)
            {
                text.Append(indent).Append("  # ").Append(Describe(blocks, child)).Append('\n');
                WriteBlock(text, blocks, child, indent + "    ", true);
            }
        }

        // "Click Button "Apply"", "Block "Footer", row of 2 blocks".
        private static string Describe(List<MenuMockupBlock> blocks, MenuMockupBlock block)
        {
            var parts = new List<string> { block.Type };
            if (block.Label.Length > 0) parts[0] += " \"" + OneLine(block.Label) + "\"";
            if (block.Content.Length > 0) parts.Add(OneLine(block.Content));
            int count = MenuMockupModel.Children(blocks, block.Id).Count;
            if (count > 0) parts.Add(block.Direction + " of " + count + (count == 1 ? " block" : " blocks"));
            MenuMockupBlock parent = MenuMockupModel.Find(blocks, block.Parent);
            if (parent != null && parent.Type == MenuMockupTypes.Tabs) parts.Add("on tab \"" + MenuMockupTypes.TabNames(parent)[MenuMockupTypes.TabOf(parent, block)] + "\"");
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
        internal static List<MenuMockupBlock> Load(string path, out float width, out float height)
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
            var blocks = new List<MenuMockupBlock>();
            var root = new MenuMockupBlock { Id = 0, Type = MenuMockupTypes.Window };
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
        private static int ReadBlock(List<KeyValuePair<int, string>> lines, int position, MenuMockupBlock block,
            List<MenuMockupBlock> blocks, int ownerIndent)
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
                        var child = new MenuMockupBlock { Id = NextId(blocks), Parent = block.Id };
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

        private static void Apply(MenuMockupBlock block, string key, string value)
        {
            switch (key)
            {
                case "type":
                    if (block.Id != 0) block.Type = MenuMockupTypes.Find(Unquote(value)).Name;
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
                case "tab": block.Tab = (int)ParseFloat(value, 0); break;
                case "activeTab": block.ActiveTab = (int)ParseFloat(value, 0); break;
            }
        }

        private static int NextId(List<MenuMockupBlock> blocks)
        {
            int id = 0;
            foreach (MenuMockupBlock block in blocks) id = Math.Max(id, block.Id);
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
}

// ============================================================================
// GLOSSARY: Menu Mockup file
// Menu Mockup (a Debug tool): the windows are in Editor/UI/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockupFile                Save(path, blocks, width, height) writes the annotated .yml (defaults left out);
//                           Load(path, out width, out height) reads it back.
