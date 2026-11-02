using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// One entry of the type list: one checkbox in the Copy list (feature 3). Its types are
    /// found by full name across the loaded assemblies (Namespace.* takes every component in
    /// a namespace), so VRChat, DynamicBone, FinalIK and other optional types are simply
    /// left out when they are not in the project.
    /// </summary>
    internal sealed class AvatarCopyType
    {
        internal string Id = "";
        internal string Label = "";
        internal string Category = "";
        internal bool Common;
        internal bool DefaultEnabled;
        internal bool NeedsSdk;
        // A built-in rule (AvatarCopyTypes rule names), or "".
        internal string Rule = "";
        internal readonly List<string> TypeNames = new List<string>();

        private Type[] resolved;

        /// <summary>The types found in this project; empty when none is.</summary>
        internal Type[] Types
        {
            get
            {
                if (resolved != null) return resolved;
                var types = new List<Type>();
                foreach (string name in TypeNames)
                {
                    foreach (Type type in AvatarCopyTypes.Resolve(name))
                    {
                        if (!types.Contains(type)) types.Add(type);
                    }
                }
                resolved = types.ToArray();
                return resolved;
            }
        }

        internal bool Available
        {
            get { return Types.Length > 0 || Rule == AvatarCopyTypes.PrefabsRule; }
        }
    }

    /// <summary>
    /// The type list, read from Documents/RiryTools/migrator/types.yml. That file is a copy of
    /// the package's template (Editor/AvatarMigrationAssistant/types.yml, every type from
    /// Pumkin's Avatar Tools sorted by kind), made when it is missing; users edit the copy.
    /// Entries with a built-in place run in the order of feature 3 (RunOrder), the rest after
    /// them in file order.
    /// </summary>
    internal static class AvatarCopyTypes
    {
        internal const string FolderName = "migrator";
        internal const string FileName = "types.yml";
        // The GUID of the template (types.yml.meta).
        private const string TemplateGuid = "0756bf5a933b437db4405f7a01a55498";

        // Built-in rules.
        internal const string PrefabsRule = "prefabs";
        internal const string TransformsRule = "transforms";
        internal const string MeshRenderersRule = "meshrenderers";
        internal const string SkinnedMeshRenderersRule = "skinnedmeshrenderers";
        internal const string AnimatorsRule = "animators";
        internal const string CamerasRule = "cameras";
        internal const string DescriptorRule = "descriptor";
        internal const string UnityConstraintRule = "unityconstraint";

        // Type names the run handles by name.
        internal const string PhysBone = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";
        internal const string PhysBoneCollider = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider";
        internal const string ContactSender = "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender";
        internal const string ContactReceiver = "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver";
        internal const string DescriptorType = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
        internal const string PipelineManager = "VRC.Core.PipelineManager";
        internal const string VrcConstraintPrefix = "VRC.SDK3.Dynamics.Constraint.Components.VRC";

        /// <summary>Feature 3's run order, by entry id; other entries run after these, in file order.</summary>
        private static readonly string[] RunOrder =
        {
            "prefab", "particlesystem", "collider", "rigidbody", "trailrenderer", "light", "animator", "audiosource",
            "contactreceiver", "contactsender", "physbonecollider", "physbone", "vrcstation", "vrcheadchop",
            "dynamicbonecollider", "dynamicbone", "aimconstraint", "lookatconstraint", "parentconstraint", "positionconstraint",
            "rotationconstraint", "scaleconstraint", "vrcaimconstraint", "vrclookatconstraint", "vrcparentconstraint",
            "vrcpositionconstraint", "vrcrotationconstraint", "vrcscaleconstraint", "characterjoint", "configurablejoint",
            "fixedjoint", "hingejoint", "springjoint", "transform", "meshrenderer", "skinnedmeshrenderer", "camera", "finalik",
            "vrcavatardescriptor"
        };

        private static readonly Dictionary<string, Type> Cache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static List<AvatarCopyType> all;
        private static DateTime loadedStamp;

        internal static string FilePath
        {
            get { return RiryToolsPaths.Documents(Path.Combine(FolderName, FileName)); }
        }

        /// <summary>Every entry, in run order; read again when the file changes.</summary>
        internal static List<AvatarCopyType> All
        {
            get
            {
                try
                {
                    CopyTemplateIfMissing();
                    DateTime stamp = File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : DateTime.MinValue;
                    if (all != null && stamp == loadedStamp) return all;
                    loadedStamp = stamp;
                    all = Read(FilePath);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(AvatarMigrationAssistant.Title + ": could not read " + FilePath + ": " + exception.Message);
                    if (all == null) all = new List<AvatarCopyType>();
                }
                return all;
            }
        }

        /// <summary>The first entry with a built-in rule, or null.</summary>
        internal static AvatarCopyType WithRule(string rule)
        {
            return All.Find(type => type.Rule == rule);
        }

        private static void CopyTemplateIfMissing()
        {
            if (File.Exists(FilePath)) return;
            string template = AssetDatabase.GUIDToAssetPath(TemplateGuid);
            if (string.IsNullOrEmpty(template)) throw new FileNotFoundException("the package's types.yml template is missing.");
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.Copy(Path.GetFullPath(template), FilePath);
        }

        // ---- Reading the file -------------------------------------------------------

        private static List<AvatarCopyType> Read(string path)
        {
            var entries = new List<AvatarCopyType>();
            var root = YamlLite.Parse(File.ReadAllLines(path)) as List<KeyValuePair<string, object>>;
            var categories = root != null ? YamlLite.Get(root, "categories") as List<KeyValuePair<string, object>> : null;
            if (categories == null) return entries;
            foreach (KeyValuePair<string, object> category in categories)
            {
                var items = category.Value as List<object>;
                if (items == null) continue;
                foreach (object item in items)
                {
                    var map = item as List<KeyValuePair<string, object>>;
                    if (map == null) continue;
                    var entry = new AvatarCopyType
                    {
                        Category = category.Key,
                        Id = YamlLite.Text(map, "id"),
                        Label = YamlLite.Text(map, "label"),
                        Common = YamlLite.Flag(map, "common"),
                        DefaultEnabled = YamlLite.Flag(map, "enabled"),
                        NeedsSdk = YamlLite.Flag(map, "sdk"),
                        Rule = YamlLite.Text(map, "rule")
                    };
                    var names = YamlLite.Get(map, "types") as List<object>;
                    if (names != null)
                    {
                        foreach (object name in names)
                        {
                            if (name is string && ((string)name).Length > 0) entry.TypeNames.Add((string)name);
                        }
                    }
                    if (entry.Id.Length == 0) entry.Id = entry.Label;
                    if (entry.Label.Length == 0) entry.Label = entry.Id;
                    if (entry.Id.Length > 0 && !entries.Exists(other => other.Id == entry.Id)) entries.Add(entry);
                }
            }
            // Built-in places first, in feature 3's order; the rest keep their file order.
            var ordered = new List<AvatarCopyType>();
            foreach (string id in RunOrder)
            {
                AvatarCopyType found = entries.Find(entry => entry.Id == id);
                if (found != null) ordered.Add(found);
            }
            foreach (AvatarCopyType entry in entries)
            {
                if (!ordered.Contains(entry)) ordered.Add(entry);
            }
            return ordered;
        }

        // ---- Finding types ----------------------------------------------------------

        /// <summary>The component types a name stands for: one by full name, or every one in a namespace for Namespace.*.</summary>
        internal static List<Type> Resolve(string name)
        {
            var types = new List<Type>();
            if (name.EndsWith(".*", StringComparison.Ordinal))
            {
                string space = name.Substring(0, name.Length - 2);
                foreach (Type type in AllTypes())
                {
                    if (type.Namespace == space && !type.IsAbstract && typeof(Component).IsAssignableFrom(type)) types.Add(type);
                }
                return types;
            }
            Type found = Find(name);
            if (found != null && typeof(Component).IsAssignableFrom(found)) types.Add(found);
            return types;
        }

        /// <summary>A type by full name in any loaded assembly, or null.</summary>
        internal static Type Find(string fullName)
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

        /// <summary>A type by its simple name in any loaded assembly, or null (for SDK editor classes).</summary>
        internal static Type FindByName(string name)
        {
            Type found;
            if (Cache.TryGetValue("?" + name, out found)) return found;
            foreach (Type type in AllTypes())
            {
                if (type.Name != name) continue;
                found = type;
                break;
            }
            Cache["?" + name] = found;
            return found;
        }

        private static IEnumerable<Type> AllTypes()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }
                foreach (Type type in types)
                {
                    if (type != null) yield return type;
                }
            }
        }
    }

    /// <summary>
    /// The small part of YAML the type list uses: maps ("key: value", "key:" with an indented
    /// block), lists ("- value", "- key: value" starting a map) and # comments. Maps keep
    /// their order as lists of key/value pairs.
    /// </summary>
    internal static class YamlLite
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

// ============================================================================
// GLOSSARY: Avatar Migration Assistant type list (CopyTypes.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// types.yml (beside this file)  The template: every type from Pumkin's Avatar Tools, sorted by kind into categories;
//                       each entry has id, label, common, enabled, sdk, rule and types.
// Documents/RiryTools/migrator/types.yml  The copy the tool reads (made from the template when missing); users edit it.
// AvatarCopyType        One entry: Id, Label, Category, Common, DefaultEnabled, NeedsSdk, Rule, TypeNames; Types (found in
//                       this project, Namespace.* expanded), Available.
// AvatarCopyTypes
//   All / WithRule(rule)  The entries in run order (RunOrder: feature 3's order, then the rest in file order), read again
//                       when the file changes; the first entry with a built-in rule.
//   Rules               prefabs, transforms, meshrenderers, skinnedmeshrenderers, animators, cameras, descriptor,
//                       unityconstraint: the entries the run treats specially.
//   Resolve / Find / FindByName  Types by name across the loaded assemblies (cached).
// YamlLite              The YAML subset the list uses: maps, lists, "- key: value" maps, # comments.
