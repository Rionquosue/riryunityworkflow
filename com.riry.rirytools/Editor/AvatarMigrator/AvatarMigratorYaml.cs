using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The small part of YAML the type list uses: maps ("key: value", "key:" with an indented
    /// block), lists ("- value", "- key: value" starting a map) and # comments. Maps keep
    /// their order as lists of key/value pairs.
    /// </summary>
    internal static class AvatarMigratorYaml
    {
        private struct Line
        {
            internal int Indent;
            internal string Text;
        }

        internal static object Parse(string[] raw)
        {
            var lines = new List<Line>();
            foreach (string text in raw)
            {
                string line = StripComment(text).TrimEnd();
                if (line.Trim().Length == 0) continue;
                lines.Add(new Line { Indent = line.Length - line.TrimStart().Length, Text = line.Trim() });
            }
            int index = 0;
            return lines.Count == 0 ? null : Block(lines, ref index, lines[0].Indent);
        }

        private static string StripComment(string line)
        {
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"' || line[i] == '\'') quoted = !quoted;
                if (line[i] == '#' && !quoted && (i == 0 || char.IsWhiteSpace(line[i - 1]))) return line.Substring(0, i);
            }
            return line;
        }

        private static bool IsList(Line line)
        {
            return line.Text == "-" || line.Text.StartsWith("- ", StringComparison.Ordinal);
        }

        private static bool IsKey(string text)
        {
            int colon = text.IndexOf(':');
            return colon > 0 && (colon == text.Length - 1 || text[colon + 1] == ' ');
        }

        private static object Block(List<Line> lines, ref int index, int indent)
        {
            if (IsList(lines[index]))
            {
                var list = new List<object>();
                while (index < lines.Count && lines[index].Indent == indent && IsList(lines[index]))
                {
                    string rest = lines[index].Text.Substring(1).Trim();
                    if (rest.Length == 0)
                    {
                        index++;
                        list.Add(index < lines.Count && lines[index].Indent > indent ? Block(lines, ref index, lines[index].Indent) : null);
                    }
                    else if (IsKey(rest))
                    {
                        // "- key: value" starts a map two columns in.
                        lines[index] = new Line { Indent = indent + 2, Text = rest };
                        list.Add(Block(lines, ref index, indent + 2));
                    }
                    else
                    {
                        list.Add(Unquote(rest));
                        index++;
                    }
                }
                return list;
            }
            var map = new List<KeyValuePair<string, object>>();
            while (index < lines.Count && lines[index].Indent == indent && !IsList(lines[index]))
            {
                string text = lines[index].Text;
                int colon = text.IndexOf(':');
                if (colon <= 0)
                {
                    index++;
                    continue;
                }
                string key = Unquote(text.Substring(0, colon).Trim());
                string value = text.Substring(colon + 1).Trim();
                index++;
                object child = null;
                if (value.Length > 0) child = Unquote(value);
                else if (index < lines.Count && (lines[index].Indent > indent || (lines[index].Indent == indent && IsList(lines[index]))))
                    child = Block(lines, ref index, lines[index].Indent);
                map.Add(new KeyValuePair<string, object>(key, child));
            }
            return map;
        }

        private static string Unquote(string text)
        {
            if (text.Length >= 2 && ((text[0] == '"' && text[text.Length - 1] == '"') || (text[0] == '\'' && text[text.Length - 1] == '\'')))
                return text.Substring(1, text.Length - 2);
            return text;
        }

        internal static object Get(List<KeyValuePair<string, object>> map, string key)
        {
            foreach (KeyValuePair<string, object> pair in map)
            {
                if (pair.Key == key) return pair.Value;
            }
            return null;
        }

        internal static string Text(List<KeyValuePair<string, object>> map, string key)
        {
            return Get(map, key) as string ?? "";
        }

        internal static bool Flag(List<KeyValuePair<string, object>> map, string key)
        {
            return string.Equals(Text(map, key), "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
