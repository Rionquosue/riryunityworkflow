using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Find Null references: reads the Console's NullReferenceException entries, shows where each was thrown (the first
    /// frame in a project or package file), and scans the open scenes' components of the types that error's frames were
    /// handed for empty or missing references. If the log gives nothing, it scans every component in the open scenes.
    /// Runs once; its popup is Editor/UI/Debug/FindNullReferencesWindow.cs.
    /// </summary>
    internal static class FindNullReferences
    {
        internal sealed class Report
        {
            internal string Title = "";
            internal string Location = "";
            internal string Source = "";
            internal readonly List<string> Hits = new List<string>();
        }

        // "(at ./Packages/x/File.cs:27)" at the end of a stack frame.
        private static readonly Regex FrameAt = new Regex(@"\(at (?<path>[^()]+?):(?<line>\d+)\)\s*$");

        internal static List<Report> Find()
        {
            var reports = new List<Report>();
            foreach (string message in NullReferenceEntries())
            {
                var report = new Report();
                var types = new HashSet<Type>();
                string[] lines = message.Split('\n');
                report.Title = lines[0].Trim();
                foreach (string raw in lines.Skip(1))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    foreach (Type type in ArgumentTypes(line)) types.Add(type);
                    Match at = FrameAt.Match(line);
                    if (report.Location.Length > 0 || !at.Success || !InProject(at.Groups["path"].Value)) continue;
                    string path = at.Groups["path"].Value;
                    int number = int.Parse(at.Groups["line"].Value);
                    report.Location = Method(line) + "  (" + path + ":" + number + ")";
                    report.Source = SourceLine(path, number);
                }
                foreach (Type type in types)
                {
                    foreach (Component component in SceneComponents().Where(c => c != null && type.IsInstanceOfType(c)))
                        report.Hits.AddRange(Scan(component, true));
                }
                report.Hits.Sort(StringComparer.Ordinal);
                reports.Add(report);
            }

            if (reports.All(r => r.Hits.Count == 0))
            {
                var scene = new Report { Title = reports.Count == 0
                    ? "No NullReferenceException in the Console: whole scene scanned."
                    : "Nothing found from the log: whole scene scanned." };
                foreach (GameObject root in Roots())
                {
                    foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        foreach (Component component in transform.GetComponents<Component>())
                        {
                            if (component == null) scene.Hits.Add(HierarchyPath.Of(transform, " > ") + " > Missing script");
                            else scene.Hits.AddRange(Scan(component, false));
                        }
                    }
                }
                reports.Add(scene);
            }
            int hits = reports.Sum(r => r.Hits.Count);
            RiryLog.Info(RiryLog.FindNullReferences, reports.Count + " report(s), " + hits + " empty or missing reference(s).");
            return reports;
        }

        // The Console's entries whose text holds a NullReferenceException, each once (UnityEditor.LogEntries is internal).
        private static List<string> NullReferenceEntries()
        {
            var found = new List<string>();
            Assembly editor = typeof(EditorWindow).Assembly;
            Type entries = editor.GetType("UnityEditor.LogEntries");
            Type entryType = editor.GetType("UnityEditor.LogEntry");
            if (entries == null || entryType == null) return found;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo start = entries.GetMethod("StartGettingEntries", flags);
            MethodInfo end = entries.GetMethod("EndGettingEntries", flags);
            MethodInfo get = entries.GetMethod("GetEntryInternal", flags);
            FieldInfo text = entryType.GetField("message", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (start == null || end == null || get == null || text == null) return found;
            object entry = Activator.CreateInstance(entryType);
            int count = (int)start.Invoke(null, null);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    get.Invoke(null, new[] { i, entry });
                    var message = text.GetValue(entry) as string;
                    if (message != null && message.Contains("NullReferenceException") && !found.Contains(message)) found.Add(message);
                }
            }
            finally
            {
                end.Invoke(null, null);
            }
            return found;
        }

        private static bool InProject(string path)
        {
            string p = path.Replace('\\', '/');
            if (p.StartsWith("./")) p = p.Substring(2);
            return p.StartsWith("Assets/") || p.StartsWith("Packages/") || p.StartsWith("Library/PackageCache/");
        }

        private static string SourceLine(string path, int number)
        {
            try
            {
                string full = Path.GetFullPath(path);
                if (!File.Exists(full)) return "";
                string[] lines = File.ReadAllLines(full);
                return number >= 1 && number <= lines.Length ? lines[number - 1].Trim() : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        // The frame's method, without its arguments and location.
        private static string Method(string frame)
        {
            int paren = frame.IndexOf('(');
            return (paren > 0 ? frame.Substring(0, paren) : frame).Trim();
        }

        // The UnityEngine.Object types among a frame's arguments: "(Ns.Type name, ...)" or "(Ns.Type,...)".
        private static IEnumerable<Type> ArgumentTypes(string frame)
        {
            int open = frame.IndexOf('(');
            if (open < 0) yield break;
            int close = frame.IndexOf(')', open);
            if (close < 0) yield break;
            foreach (string argument in frame.Substring(open + 1, close - open - 1).Split(','))
            {
                string name = argument.Trim().Split(' ')[0];
                if (name.Length == 0) continue;
                Type type = Resolve(name);
                if (type != null && typeof(Object).IsAssignableFrom(type)) yield return type;
            }
        }

        // Stack traces write nested types as "Outer/Inner".
        private static Type Resolve(string name)
        {
            return RiryToolsTypes.Named(name.Replace('/', '+'));
        }

        private static IEnumerable<GameObject> Roots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects()) yield return root;
            }
        }

        private static IEnumerable<Component> SceneComponents()
        {
            return Roots().SelectMany(root => root.GetComponentsInChildren<Component>(true));
        }

        // A component's broken references: missing ones (pointing at something deleted), null serialized fields, and,
        // when empty is true, empty object fields (except under a layer marked as default, which has none by design).
        private static IEnumerable<string> Scan(Component component, bool empty)
        {
            string owner = HierarchyPath.Of(component.transform, " > ") + " > " + ObjectNames.NicifyVariableName(component.GetType().Name);
            var hits = new List<string>();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type type = component.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    bool serialized = (field.IsPublic && !field.IsNotSerialized) || field.IsDefined(typeof(SerializeField), true)
                        || field.IsDefined(typeof(SerializeReference), true);
                    if (!serialized || field.FieldType.IsValueType || typeof(Object).IsAssignableFrom(field.FieldType)) continue;
                    object value;
                    try { value = field.GetValue(component); }
                    catch (Exception) { continue; }
                    if (value == null) hits.Add(owner + " > " + ObjectNames.NicifyVariableName(field.Name) + ": null");
                }
            }

            var serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != null) continue;
                if (property.name == "m_Script" || property.name == "m_GameObject" || property.name == "m_PrefabInstance"
                    || property.name == "m_PrefabAsset" || property.name == "m_CorrespondingSourceObject") continue;
                bool missing = property.objectReferenceInstanceIDValue != 0;
                if (!missing && (!empty || UnderDefault(serializedObject, property.propertyPath))) continue;
                hits.Add(owner + " > " + Label(serializedObject, property.propertyPath) + (missing ? ": missing" : ": empty"));
            }
            return hits;
        }

        // Whether an array element above the property has an isDefault field set (a VRChat playable layer left default).
        private static bool UnderDefault(SerializedObject serializedObject, string path)
        {
            foreach (string element in Elements(path))
            {
                SerializedProperty flag = serializedObject.FindProperty(element + ".isDefault");
                if (flag != null && flag.propertyType == SerializedPropertyType.Boolean && flag.boolValue) return true;
            }
            return false;
        }

        // "a.Array.data[4].b" gives "a.Array.data[4]".
        private static IEnumerable<string> Elements(string path)
        {
            int index = 0;
            while ((index = path.IndexOf(".Array.data[", index, StringComparison.Ordinal)) >= 0)
            {
                int close = path.IndexOf(']', index);
                if (close < 0) yield break;
                yield return path.Substring(0, close + 1);
                index = close;
            }
        }

        // The property's display names from the component down, an array element named by its "type" enum when it has
        // one ("FX layer"), else "Element n".
        private static string Label(SerializedObject serializedObject, string path)
        {
            var parts = new List<string>();
            string[] pieces = path.Split('.');
            string at = "";
            for (int i = 0; i < pieces.Length; i++)
            {
                string piece = pieces[i];
                if (piece == "Array" && i + 1 < pieces.Length && pieces[i + 1].StartsWith("data["))
                {
                    at += ".Array." + pieces[i + 1];
                    i++;
                    SerializedProperty kind = serializedObject.FindProperty(at + ".type");
                    if (kind != null && kind.propertyType == SerializedPropertyType.Enum && kind.enumValueIndex >= 0
                        && kind.enumValueIndex < kind.enumDisplayNames.Length)
                        parts[parts.Count - 1] = kind.enumDisplayNames[kind.enumValueIndex] + " layer";
                    else
                        parts.Add("Element " + pieces[i].Substring(5).TrimEnd(']'));
                    continue;
                }
                at = at.Length == 0 ? piece : at + "." + piece;
                SerializedProperty property = serializedObject.FindProperty(at);
                parts.Add(property != null ? property.displayName : piece);
            }
            return string.Join(" > ", parts);
        }
    }
}

// ============================================================================
// GLOSSARY: Find Null references
// ============================================================================
//
// FindNullReferences        Popup: Run() reads the Console once and shows the reports.
//   Find                    One report per NullReferenceException entry (first project/package frame, its source line, hits on
//                           the scene components of the frames' argument types); a whole-scene report when none has hits.
//   NullReferenceEntries    The Console's NullReferenceException texts (UnityEditor.LogEntries by reflection), each once.
//   Scan                    A component's null serialized fields, missing references and (log reports) empty references.
//   Label / PathOf          "Avatar > VRC Avatar Descriptor > FX layer > Animator Controller" style names.
