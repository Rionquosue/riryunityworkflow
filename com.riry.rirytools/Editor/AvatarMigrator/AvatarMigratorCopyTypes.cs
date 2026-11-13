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
    internal sealed class AvatarMigratorCopyType
    {
        internal string Id = "";
        internal string Label = "";
        internal string Category = "";
        internal bool Common;
        internal bool DefaultEnabled;
        internal bool NeedsSdk;
        // A built-in rule (AvatarMigratorCopyTypes rule names), or "".
        internal string Rule = "";
        internal readonly List<string> TypeNames = new List<string>();

        private Type[] resolved;
        private static readonly HashSet<string> ReportedUnknown = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The types found in this project; empty when none is.</summary>
        internal Type[] Types
        {
            get
            {
                if (resolved != null) return resolved;
                var types = new List<Type>();
                var unknown = new List<string>();
                foreach (string name in TypeNames)
                {
                    List<Type> found = AvatarMigratorCopyTypes.Resolve(name);
                    if (found.Count == 0) unknown.Add(name);
                    foreach (Type type in found)
                    {
                        if (!types.Contains(type)) types.Add(type);
                    }
                }
                // Some names found and some not: likely a typo or a rename in types.yml, said once per entry. (None found is
                // shown as "(not in project)": the package is simply not installed.)
                if (types.Count > 0 && unknown.Count > 0 && ReportedUnknown.Add(Id + "|" + string.Join(",", unknown)))
                    RiryLog.Warn(RiryLog.AvatarMigration, AvatarMigratorCopyTypes.FilePath + ", entry '" + Label + "': " + string.Join(", ", unknown) +
                        " not found in this project (misspelled, renamed or not installed); the other types of the entry are used.");
                resolved = types.ToArray();
                return resolved;
            }
        }

        internal bool Available
        {
            get { return Types.Length > 0 || Rule == AvatarMigratorCopyTypes.PrefabsRule; }
        }
    }

    /// <summary>
    /// The type list, read from Documents/RiryTools/migrator/types.yml. That file is a copy of
    /// the package's template (Editor/AvatarMigrator/types.yml, every type from
    /// Pumkin's Avatar Tools sorted by kind), made when it is missing; users edit the copy.
    /// Entries with a built-in place run in the order of feature 3 (RunOrder), the rest after
    /// them in file order.
    /// </summary>
    internal static class AvatarMigratorCopyTypes
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
        private static List<AvatarMigratorCopyType> all;
        private static DateTime loadedStamp;
        // The last read problem: All runs on every draw, so one problem is logged once, not every repaint.
        private static string lastProblem;

        internal static string FilePath
        {
            get { return RiryToolsPaths.Documents(Path.Combine(FolderName, FileName)); }
        }

        /// <summary>Every entry, in run order; read again when the file changes.</summary>
        internal static List<AvatarMigratorCopyType> All
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
                    lastProblem = null;
                }
                catch (Exception exception)
                {
                    if (exception.Message != lastProblem)
                        RiryLog.Warn(RiryLog.AvatarMigration, "Could not read " + FilePath + ": " + exception.Message);
                    lastProblem = exception.Message;
                    if (all == null) all = new List<AvatarMigratorCopyType>();
                }
                return all;
            }
        }

        /// <summary>The first entry with a built-in rule, or null.</summary>
        internal static AvatarMigratorCopyType WithRule(string rule)
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

        private static List<AvatarMigratorCopyType> Read(string path)
        {
            var entries = new List<AvatarMigratorCopyType>();
            var root = AvatarMigratorYaml.Parse(File.ReadAllLines(path)) as List<KeyValuePair<string, object>>;
            var categories = root != null ? AvatarMigratorYaml.Get(root, "categories") as List<KeyValuePair<string, object>> : null;
            if (categories == null) return entries;
            foreach (KeyValuePair<string, object> category in categories)
            {
                var items = category.Value as List<object>;
                if (items == null) continue;
                foreach (object item in items)
                {
                    var map = item as List<KeyValuePair<string, object>>;
                    if (map == null) continue;
                    var entry = new AvatarMigratorCopyType
                    {
                        Category = category.Key,
                        Id = AvatarMigratorYaml.Text(map, "id"),
                        Label = AvatarMigratorYaml.Text(map, "label"),
                        Common = AvatarMigratorYaml.Flag(map, "common"),
                        DefaultEnabled = AvatarMigratorYaml.Flag(map, "enabled"),
                        NeedsSdk = AvatarMigratorYaml.Flag(map, "sdk"),
                        Rule = AvatarMigratorYaml.Text(map, "rule")
                    };
                    var names = AvatarMigratorYaml.Get(map, "types") as List<object>;
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
            var ordered = new List<AvatarMigratorCopyType>();
            foreach (string id in RunOrder)
            {
                AvatarMigratorCopyType found = entries.Find(entry => entry.Id == id);
                if (found != null) ordered.Add(found);
            }
            foreach (AvatarMigratorCopyType entry in entries)
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
            Type found = RiryToolsTypes.Named(name);
            if (found != null && typeof(Component).IsAssignableFrom(found)) types.Add(found);
            return types;
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
}
