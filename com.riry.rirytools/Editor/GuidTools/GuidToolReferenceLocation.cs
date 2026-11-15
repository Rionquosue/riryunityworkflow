using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Where one broken reference sits in a file: the object's type and the field (a material slot, a texture slot, a
    /// prefab override's property), read from the YAML around it, and the object itself, found on demand so it can be
    /// selected: in an open scene by GlobalObjectId, in a prefab among its loaded objects, otherwise the asset.
    /// </summary>
    internal sealed class GuidToolReferenceLocation
    {
        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)");
        private static readonly Regex TargetLine = new Regex(@"^\s*-\s+target:\s*\{fileID:\s*(-?\d+)");
        private static readonly Regex SlotIndex = new Regex(@"^m_Materials(?:\.Array\.data)?\[(\d+)\]$");

        internal string File;
        // The YAML type ("MeshRenderer", "MonoBehaviour", "PrefabInstance").
        internal string Type = "";
        // The field in words ("material slot 0", "texture slot _BumpMap", "override of m_Mesh").
        internal string Field = "";
        private long document;
        private int classId;
        // A PrefabInstance override's target: the object's file ID in the source prefab (0 for none).
        private long target;
        private bool resolved;
        private UnityEngine.Object found;

        /// <summary>The location of the reference whose "guid: " value starts at offset.</summary>
        internal static GuidToolReferenceLocation Of(string file, byte[] bytes, int offset)
        {
            var location = new GuidToolReferenceLocation { File = file };
            List<string> lines = LinesUpTo(bytes, offset, out int headerAt);
            if (headerAt < 0) return location;
            Match header = Header.Match(lines[headerAt]);
            int.TryParse(header.Groups[1].Value, out location.classId);
            long.TryParse(header.Groups[2].Value, out location.document);
            if (headerAt + 1 < lines.Count) location.Type = lines[headerAt + 1].Trim().TrimEnd(':');
            List<string> body = lines.GetRange(headerAt + 1, lines.Count - headerAt - 1);
            location.Field = location.classId == 1001 ? location.Override(body, bytes, offset) : Words(Path(body));
            return location;
        }

        /// <summary>The object holding the reference (a component, a GameObject or the asset), or null when it can't be loaded.</summary>
        internal UnityEngine.Object Object
        {
            get
            {
                // Looked up once per scan; Rescan makes new locations (after opening the scene, say).
                if (resolved) return found;
                resolved = true;
                found = Resolve();
                return found;
            }
        }

        /// <summary>"Body/Visor (MeshRenderer): material slot 0", from what was loaded.</summary>
        internal string Label
        {
            get
            {
                UnityEngine.Object item = Object;
                GameObject owner = item as GameObject ?? (item as Component)?.gameObject;
                string type = item is Component ? item.GetType().Name : Type;
                string at = owner != null ? HierarchyPath.Of(owner.transform) : System.IO.Path.GetFileName(File);
                return at + (type.Length > 0 ? " (" + type + ")" : "") + (Field.Length > 0 ? ": " + Field : "");
            }
        }

        private UnityEngine.Object Resolve()
        {
            string guid = AssetDatabase.AssetPathToGUID(File);
            if (File.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                if (document == 0) return null;
                // An override lands on the instance's object: its file ID in the source, under this PrefabInstance.
                string id = classId == 1001
                    ? "GlobalObjectId_V1-2-" + guid + "-" + (ulong)target + "-" + (ulong)document
                    : "GlobalObjectId_V1-2-" + guid + "-" + (ulong)document + "-0";
                if (classId == 1001 && target == 0) return null;
                return GlobalObjectId.TryParse(id, out GlobalObjectId parsed) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) : null;
            }
            if (File.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                // A nested instance's object gets (PrefabInstance ^ source file ID) in the prefab, as Unity numbers it.
                long want = classId == 1001 ? (document ^ target) & long.MaxValue : document;
                if (classId == 1001 && target == 0) return AssetDatabase.LoadMainAssetAtPath(File);
                foreach (UnityEngine.Object item in AssetDatabase.LoadAllAssetsAtPath(File))
                {
                    if (item != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string itemGuid, out long id) && id == want) return item;
                }
                return AssetDatabase.LoadMainAssetAtPath(File);
            }
            return AssetDatabase.LoadMainAssetAtPath(File);
        }

        // The file's lines from the start to the reference's line (its part before the reference), and the index of the
        // last document header among them (-1 for none).
        private static List<string> LinesUpTo(byte[] bytes, int offset, out int headerAt)
        {
            int end = Math.Min(offset, bytes.Length);
            int start = end;
            headerAt = -1;
            // Back to the document header's line ("--- !u!"), or the start of the file.
            while (start > 0)
            {
                int lineStart = start;
                while (lineStart > 0 && bytes[lineStart - 1] != (byte)'\n') lineStart--;
                if (lineStart + 3 <= bytes.Length && bytes[lineStart] == (byte)'-' && bytes[lineStart + 1] == (byte)'-' && bytes[lineStart + 2] == (byte)'-')
                {
                    start = lineStart;
                    break;
                }
                start = lineStart == 0 ? 0 : lineStart - 1;
            }
            var lines = Encoding.UTF8.GetString(bytes, start, end - start).Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                if (Header.IsMatch(lines[i])) headerAt = i;
            }
            return lines;
        }

        private static int Indent(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ') i++;
            return i;
        }

        private static string Key(string line)
        {
            string text = line.Trim();
            if (text.StartsWith("- ", StringComparison.Ordinal)) text = text.Substring(2).Trim();
            int colon = text.IndexOf(':');
            return colon > 0 && !text.StartsWith("{", StringComparison.Ordinal) ? text.Substring(0, colon) : "";
        }

        private static bool Item(string line)
        {
            return line.TrimStart().StartsWith("- ", StringComparison.Ordinal);
        }

        // The key path down to the last line ("m_Materials[0]", "m_SavedProperties.m_TexEnvs[3]._BumpMap.m_Texture").
        private static List<string> Path(List<string> body)
        {
            var path = new List<string>();
            if (body.Count < 2) return path;
            int last = body.Count - 1;
            string line = body[last];
            int indent = Indent(line);
            bool item = Item(line);
            AddSegment(path, body, last, item);
            for (int i = last - 1; i > 0; i--)
            {
                string above = body[i];
                if (above.Trim().Length == 0) continue;
                int at = Indent(above);
                bool aboveItem = Item(above);
                // A parent: less indented, or the key a same-indent list hangs from.
                if (at < indent || at == indent && item && !aboveItem)
                {
                    AddSegment(path, body, i, aboveItem);
                    indent = at;
                    item = aboveItem;
                }
            }
            path.Reverse();
            return path;
        }

        // A line's key, with "[n]" for a list item (its place among the items of the same indent above it).
        private static void AddSegment(List<string> path, List<string> body, int index, bool item)
        {
            string key = Key(body[index]);
            if (!item)
            {
                if (key.Length > 0) path.Add(key);
                return;
            }
            int indent = Indent(body[index]);
            int n = 0;
            for (int i = index - 1; i > 0; i--)
            {
                int at = Indent(body[i]);
                if (at < indent || at == indent && !Item(body[i])) break;
                if (at == indent && Item(body[i])) n++;
            }
            // Built bottom-up and reversed: the index ends up after the list's key, the item's own key after it.
            if (key.Length > 0) path.Add(key);
            path.Add("[" + n + "]");
        }

        // The path in words: a material slot, a texture slot, or the field path.
        private static string Words(List<string> path)
        {
            string joined = Join(path);
            Match slot = SlotIndex.Match(joined);
            if (slot.Success) return "material slot " + slot.Groups[1].Value;
            int tex = path.IndexOf("m_TexEnvs");
            if (tex >= 0 && tex + 2 < path.Count) return "texture slot " + path[tex + 2];
            if (joined == "m_Mesh") return "mesh";
            if (joined == "m_Shader") return "shader";
            return joined;
        }

        // "m_Materials", "[0]" → "m_Materials[0]".
        private static string Join(List<string> path)
        {
            var text = new StringBuilder();
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i].StartsWith("[", StringComparison.Ordinal))
                {
                    // An item marker goes after the list's key, which comes before it once reversed.
                    text.Append(path[i]);
                    continue;
                }
                if (text.Length > 0) text.Append('.');
                text.Append(path[i]);
            }
            return text.ToString();
        }

        // A PrefabInstance's reference: its source prefab, or an override (its target and property path).
        private string Override(List<string> body, byte[] bytes, int offset)
        {
            string last = body.Count > 0 ? body[body.Count - 1].Trim() : "";
            if (last.StartsWith("m_SourcePrefab", StringComparison.Ordinal)) return "source prefab";
            for (int i = body.Count - 1; i >= 0; i--)
            {
                Match match = TargetLine.Match(body[i]);
                if (!match.Success) continue;
                long.TryParse(match.Groups[1].Value, out target);
                break;
            }
            // propertyPath follows the target line, before the value: read on from the target to the reference's line.
            string property = body.Select(line => line.Trim()).LastOrDefault(line => line.StartsWith("propertyPath:", StringComparison.Ordinal));
            if (property == null) return "override";
            string name = property.Substring("propertyPath:".Length).Trim();
            Match slot = SlotIndex.Match(name);
            return "override of " + (slot.Success ? "material slot " + slot.Groups[1].Value : name);
        }
    }
}

// ============================================================================
// GLOSSARY: GUID Tools reference location
// ============================================================================
//
// GuidToolReferenceLocation (this file)
//   Of(file, bytes, offset)  The YAML type and the field in words (Path, Words; Override for a PrefabInstance) of one reference.
//   Object                   The object holding it, found once (Resolve): GlobalObjectId in an open scene, the loaded object
//                            with the file ID in a prefab (a nested instance's: PrefabInstance ^ source ID), else the asset.
//   Label                    "<hierarchy path> (<type>): <field>".
