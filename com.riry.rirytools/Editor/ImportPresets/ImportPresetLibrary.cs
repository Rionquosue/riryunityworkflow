using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditorInternal;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Project-independent import preset configurations, stored under
    /// Documents/RiryTools/ImportPresets/&lt;configuration&gt;/. A configuration
    /// folder holds Unity .preset files and an optional config.json whose rules
    /// map file extensions to them. Presets no rule mentions apply to every file
    /// their importer type accepts.
    /// </summary>
    internal static class ImportPresetLibrary
    {
        internal const string ConfigFileName = "config.json";
        internal const string PresetExtension = ".preset";
        private const string ReadmeFileName = "README.txt";

        [Serializable]
        internal sealed class Rule
        {
            public List<string> extensions = new List<string>();
            public string preset = "";
        }

        [Serializable]
        internal sealed class Config
        {
            public List<Rule> rules = new List<Rule>();
        }

        internal struct Candidate
        {
            public string File;
            public bool FromRule;
        }

        private sealed class CachedPreset
        {
            public long Stamp;
            public Preset Preset;
        }

        private static readonly Dictionary<string, CachedPreset> presetCache =
            new Dictionary<string, CachedPreset>(StringComparer.Ordinal);
        private static readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);

        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7",
            "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        internal static string RootPath
        {
            get { return RiryToolsPaths.Documents("ImportPresets"); }
        }

        internal static string EnsureRoot()
        {
            string root = RootPath;
            Directory.CreateDirectory(root);
            string readme = Path.Combine(root, ReadmeFileName);
            if (!File.Exists(readme))
            {
                File.WriteAllText(readme,
                    "RiryTools import preset library\n\n" +
                    "Each folder here is one configuration. Put Unity .preset files for importers in it\n" +
                    "(Project window: right-click an asset > RiryTools > Save Import Preset).\n\n" +
                    "Optional config.json maps file extensions to presets; the first matching rule wins:\n" +
                    "{ \"rules\": [ { \"extensions\": [\"png\", \"tga\"], \"preset\": \"Textures.preset\" } ] }\n" +
                    "Use \"*\" to match every extension. Presets that no rule mentions apply to every\n" +
                    "file their importer type accepts.\n", new UTF8Encoding(false));
            }
            return root;
        }

        /// <summary>Error text for a configuration or preset name, or null when it is safe.</summary>
        internal static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Enter a name.";
            if (name != name.Trim()) return "Names cannot start or end with a space.";
            if (name == "." || name == ".." || name.EndsWith(".", StringComparison.Ordinal))
                return "Names cannot be '.' or '..', or end with a period.";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                return "Names cannot contain / \\ : or other characters that are invalid in file names.";
            string stem = name.Split('.')[0];
            foreach (string reserved in ReservedNames)
            {
                if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
                    return "'" + name + "' is reserved by Windows.";
            }
            return null;
        }

        internal static List<string> ConfigurationNames()
        {
            var names = new List<string>();
            string root = RootPath;
            if (!Directory.Exists(root)) return names;
            foreach (string directory in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(directory);
                if (ValidateName(name) == null) names.Add(name);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>
        /// Full path of a configuration, or null when the name is unsafe. Names
        /// match without case, so a marker written on Windows also works on Linux.
        /// </summary>
        internal static string ConfigurationPath(string name)
        {
            if (ValidateName(name) != null) return null;
            string exact = Path.Combine(RootPath, name);
            if (Directory.Exists(exact) || !Directory.Exists(RootPath)) return exact;
            foreach (string directory in Directory.GetDirectories(RootPath))
            {
                if (string.Equals(Path.GetFileName(directory), name, StringComparison.OrdinalIgnoreCase))
                    return directory;
            }
            return exact;
        }

        internal static Config ReadConfig(string configurationPath, out string error)
        {
            error = null;
            string file = Path.Combine(configurationPath, ConfigFileName);
            if (!File.Exists(file)) return new Config();
            try
            {
                Config config = JsonUtility.FromJson<Config>(File.ReadAllText(file));
                if (config == null) return new Config();
                if (config.rules == null) config.rules = new List<Rule>();
                return config;
            }
            catch (Exception exception)
            {
                error = "Could not read " + file + ": " + exception.Message;
                return new Config();
            }
        }

        internal static void WriteConfig(string configurationPath, Config config)
        {
            File.WriteAllText(Path.Combine(configurationPath, ConfigFileName),
                JsonUtility.ToJson(config, true), new UTF8Encoding(false));
        }

        internal static List<string> PresetFiles(string configurationPath)
        {
            var files = new List<string>();
            if (!Directory.Exists(configurationPath)) return files;
            foreach (string file in Directory.GetFiles(configurationPath, "*" + PresetExtension))
                files.Add(Path.GetFileName(file));
            files.Sort(StringComparer.Ordinal);
            return files;
        }

        internal static string NormalizeExtension(string extension)
        {
            return string.IsNullOrEmpty(extension) ? "" : extension.Trim().TrimStart('.').ToLowerInvariant();
        }

        /// <summary>
        /// Presets to try for one extension, in order: rules that list the
        /// extension (or "*"), then presets that no rule mentions.
        /// </summary>
        internal static List<Candidate> OrderedPresets(string extension, Config config, IList<string> presetFiles)
        {
            string wanted = NormalizeExtension(extension);
            var result = new List<Candidate>();
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Rule rule in config.rules)
            {
                if (rule == null || string.IsNullOrEmpty(rule.preset)) continue;
                string file = FindFile(presetFiles, rule.preset);
                if (file == null) continue;
                referenced.Add(file);
                if (rule.extensions == null) continue;
                foreach (string listed in rule.extensions)
                {
                    string normalized = NormalizeExtension(listed);
                    if (normalized != "*" && normalized != wanted) continue;
                    result.Add(new Candidate { File = file, FromRule = true });
                    break;
                }
            }
            foreach (string file in presetFiles)
            {
                if (!referenced.Contains(file)) result.Add(new Candidate { File = file, FromRule = false });
            }
            return result;
        }

        private static string FindFile(IList<string> presetFiles, string name)
        {
            string withExtension = name.EndsWith(PresetExtension, StringComparison.OrdinalIgnoreCase)
                ? name : name + PresetExtension;
            // Only listed file names are returned, so a rule cannot point outside the folder.
            foreach (string file in presetFiles)
            {
                if (string.Equals(file, withExtension, StringComparison.OrdinalIgnoreCase)) return file;
            }
            return null;
        }

        /// <summary>
        /// Applies one named preset of a configuration, or with presetName null,
        /// the first of its presets that suits this importer. Returns false,
        /// without changes, when none does.
        /// </summary>
        internal static bool TryApply(AssetImporter importer, string configuration, string presetName,
            out string applied)
        {
            applied = null;
            string path = ConfigurationPath(configuration);
            if (path == null || !Directory.Exists(path))
            {
                ReportOnce("missing:" + configuration, "Import preset configuration '" + configuration +
                    "' was not found in " + RootPath + ".");
                return false;
            }
            string extension = Path.GetExtension(importer.assetPath);
            List<string> files = PresetFiles(path);
            List<Candidate> candidates;
            if (presetName != null)
            {
                string file = FindFile(files, presetName);
                if (file == null)
                {
                    ReportOnce("missing:" + configuration + "/" + presetName, "Import preset '" + presetName +
                        "' was not found in configuration '" + configuration + "'.");
                    return false;
                }
                candidates = new List<Candidate> { new Candidate { File = file, FromRule = true } };
            }
            else
            {
                string error;
                Config config = ReadConfig(path, out error);
                if (error != null) ReportOnce("config:" + path, error);
                candidates = OrderedPresets(extension, config, files);
            }

            foreach (Candidate candidate in candidates)
            {
                Preset preset = LoadPreset(Path.Combine(path, candidate.File));
                if (preset == null) continue;
                if (!preset.CanBeAppliedTo(importer))
                {
                    if (candidate.FromRule)
                        ReportOnce("type:" + path + candidate.File + importer.GetType().Name,
                            "Import preset '" + candidate.File + "' in '" + configuration + "' is listed for ." +
                            NormalizeExtension(extension) + " files but is a " + preset.GetPresetType().GetManagedTypeName() +
                            " preset, not a " + importer.GetType().Name + " preset.");
                    continue;
                }
                if (!preset.ApplyTo(importer))
                {
                    ReportOnce("apply:" + path + candidate.File, "Import preset '" + candidate.File +
                        "' could not be applied to " + importer.assetPath + ".");
                    continue;
                }
                applied = candidate.File;
                return true;
            }
            return false;
        }

        private static Preset LoadPreset(string fullPath)
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists) return null;
            long stamp = info.LastWriteTimeUtc.Ticks ^ (info.Length << 1);
            CachedPreset cached;
            if (presetCache.TryGetValue(fullPath, out cached) && cached.Stamp == stamp && cached.Preset != null)
                return cached.Preset;

            Preset preset = null;
            try
            {
                // Presets outside the project are not assets, so load the file directly.
                foreach (UnityEngine.Object loaded in InternalEditorUtility.LoadSerializedFileAndForget(fullPath))
                {
                    if (preset == null && loaded is Preset) preset = (Preset)loaded;
                    else if (loaded != null) UnityEngine.Object.DestroyImmediate(loaded);
                }
            }
            catch (Exception exception)
            {
                ReportOnce("load:" + fullPath + stamp, "Could not load import preset " + fullPath + ": " +
                    exception.Message);
            }
            if (cached != null && cached.Preset != null && cached.Preset != preset)
                UnityEngine.Object.DestroyImmediate(cached.Preset);
            if (preset == null)
            {
                presetCache.Remove(fullPath);
                ReportOnce("invalid:" + fullPath + stamp, fullPath + " is not a Unity preset file.");
                return null;
            }
            preset.hideFlags = HideFlags.HideAndDontSave;
            presetCache[fullPath] = new CachedPreset { Stamp = stamp, Preset = preset };
            return preset;
        }

        internal static bool CanSave(AssetImporter importer)
        {
            return importer != null && new PresetType(importer).IsValid() &&
                !Preset.IsObjectExcludedFromPresets(importer);
        }

        /// <summary>
        /// Saves an importer's settings as presetName.preset in a configuration.
        /// With onlyThisExtension, a rule limits it to that extension; otherwise
        /// any rules for it are removed so it applies to its whole importer type.
        /// </summary>
        internal static string SavePreset(AssetImporter importer, string configuration, string presetName,
            bool onlyThisExtension)
        {
            EnsureRoot();
            string path = ConfigurationPath(configuration);
            Directory.CreateDirectory(path);
            string file = presetName + PresetExtension;
            var preset = new Preset(importer);
            try
            {
                InternalEditorUtility.SaveToSerializedFileAndForget(
                    new UnityEngine.Object[] { preset }, Path.Combine(path, file), true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }

            string error;
            Config config = ReadConfig(path, out error);
            if (error != null) throw new InvalidOperationException(error + " Fix or delete it, then save again.");
            config.rules.RemoveAll(rule => rule == null ||
                string.Equals(FindFile(new[] { file }, rule.preset ?? ""), file, StringComparison.Ordinal));
            if (onlyThisExtension)
            {
                config.rules.Add(new Rule
                {
                    extensions = new List<string> { NormalizeExtension(Path.GetExtension(importer.assetPath)) },
                    preset = file
                });
            }
            WriteConfig(path, config);
            return Path.Combine(path, file);
        }

        internal static void ReportOnce(string key, string message)
        {
            if (reported.Add(key)) Debug.LogWarning("Import Presets: " + message);
        }
    }
}
