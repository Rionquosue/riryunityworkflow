#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Exports a .blend to an FBX beside it by running Blender in the background
    /// with the Better FBX add-on (bpy.ops.better_export.fbx). The add-on must be
    /// enabled in Blender's saved preferences.
    /// </summary>
    internal static class BetterFbxExporter
    {
        internal const string SourceExtension = "blend";
        private const string BlenderPathPref = "Riry.UnityWorkflow.BlenderPath";
        private const string OptionsPref = "Riry.UnityWorkflow.BetterFbxOptions";
        private const int TimeoutSeconds = 600;
        private const string SuccessMarker = "RIRY_BETTER_FBX_OK";

        // Arguments arrive through sys.argv, never through the script text, and
        // options are parsed with ast.literal_eval, so names and paths cannot inject code.
        private const string Script =
            "import ast, sys\n" +
            "import bpy\n" +
            "\n" +
            "def main():\n" +
            "    argv = sys.argv[sys.argv.index('--') + 1:]\n" +
            "    output = argv[0]\n" +
            "    options = argv[1] if len(argv) > 1 else ''\n" +
            "    kwargs = {}\n" +
            "    if options.strip():\n" +
            "        call = ast.parse('f(' + options + ')', mode='eval').body\n" +
            "        if call.args:\n" +
            "            raise ValueError('Exporter options must be name=value pairs.')\n" +
            "        for keyword in call.keywords:\n" +
            "            kwargs[keyword.arg] = ast.literal_eval(keyword.value)\n" +
            "    try:\n" +
            "        bpy.ops.better_export.fbx.get_rna_type()\n" +
            "    except (AttributeError, KeyError):\n" +
            "        raise RuntimeError('Better FBX exporter (bpy.ops.better_export.fbx) is not available. ' +\n" +
            "            'Enable the add-on in Blender and save the preferences.')\n" +
            "    result = bpy.ops.better_export.fbx(filepath=output, **kwargs)\n" +
            "    if 'FINISHED' not in result:\n" +
            "        raise RuntimeError('Better FBX export returned ' + str(result))\n" +
            "    print('" + SuccessMarker + "')\n" +
            "\n" +
            "main()\n";

        private static readonly Regex OptionsPattern = new Regex(
            @"^\s*(?:[A-Za-z_]\w*\s*=\s*(?:True|False|None|-?\d+(?:\.\d+)?|'[^'\\]*'|""[^""\\]*"")\s*(?:,\s*|$))*$",
            RegexOptions.CultureInvariant);

        internal static string BlenderPath
        {
            get { return EditorPrefs.GetString(BlenderPathPref, ""); }
            set { EditorPrefs.SetString(BlenderPathPref, value ?? ""); }
        }

        /// <summary>Extra exporter arguments such as use_selection=False, stored per user.</summary>
        internal static string Options
        {
            get { return EditorPrefs.GetString(OptionsPref, ""); }
            set { EditorPrefs.SetString(OptionsPref, value ?? ""); }
        }

        internal static string OptionsError(string options)
        {
            if (string.IsNullOrWhiteSpace(options) || OptionsPattern.IsMatch(options)) return null;
            return "Use comma-separated name=value pairs with True, False, None, numbers or quoted text, " +
                "for example: use_selection=False, scale=1.0";
        }

        /// <summary>The saved Blender path when valid, else a detected installation, else null.</summary>
        internal static string ResolveBlender()
        {
            string saved = BlenderPath;
            if (!string.IsNullOrEmpty(saved) && File.Exists(saved)) return saved;
            foreach (string candidate in BlenderCandidates())
            {
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static IEnumerable<string> BlenderCandidates()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                foreach (Environment.SpecialFolder folder in new[]
                    { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
                {
                    string root = Path.Combine(Environment.GetFolderPath(folder), "Blender Foundation");
                    if (!Directory.Exists(root)) continue;
                    // Newest version first: "Blender 4.2" sorts after "Blender 3.6".
                    string[] versions = Directory.GetDirectories(root);
                    Array.Sort(versions, CompareVersionFolders);
                    for (int i = versions.Length - 1; i >= 0; i--) yield return Path.Combine(versions[i], "blender.exe");
                }
                yield break;
            }
            if (Application.platform == RuntimePlatform.OSXEditor)
                yield return "/Applications/Blender.app/Contents/MacOS/Blender";
            string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string directory in pathVariable.Split(Path.PathSeparator))
            {
                if (directory.Length > 0) yield return Path.Combine(directory, "blender");
            }
            yield return "/snap/bin/blender";
            yield return "/usr/bin/blender";
            yield return "/usr/local/bin/blender";
        }

        internal static int CompareVersionFolders(string left, string right)
        {
            Version a = ParseVersion(Path.GetFileName(left));
            Version b = ParseVersion(Path.GetFileName(right));
            int compared = a.CompareTo(b);
            return compared != 0 ? compared : string.CompareOrdinal(left, right);
        }

        private static Version ParseVersion(string name)
        {
            Match match = Regex.Match(name, @"(\d+)\.(\d+)(?:\.(\d+))?");
            if (!match.Success) return new Version(0, 0);
            return new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value),
                match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0);
        }

        internal static string FbxPathFor(string blendPath)
        {
            return ImportPresetMarker.ParentFolder(blendPath) + "/" + Path.GetFileNameWithoutExtension(blendPath) + ".fbx";
        }

        /// <summary>
        /// Why the FBX for a .blend must not be written, or null. An FBX the tool
        /// did not write, or one changed since, is never replaced.
        /// </summary>
        internal static string BlockedReason(string blendPath, out bool upToDate)
        {
            upToDate = false;
            string fbx = FbxPathFor(blendPath);
            if (!File.Exists(fbx)) return null;
            if (!ImportPresetOwnedFiles.IsUnchanged(fbx))
                return fbx + " was not exported by this tool or was changed since, so it is kept.";
            upToDate = File.GetLastWriteTimeUtc(fbx) >= File.GetLastWriteTimeUtc(blendPath);
            return null;
        }

        /// <summary>
        /// Runs the export when needed. Returns the FBX path when it was written,
        /// or null with a message.
        /// </summary>
        internal static string Export(string blendPath, bool force, out string message)
        {
            message = null;
            bool upToDate;
            message = BlockedReason(blendPath, out upToDate);
            if (message != null) return null;
            if (upToDate && !force)
            {
                message = FbxPathFor(blendPath) + " is up to date.";
                return null;
            }
            string blender = ResolveBlender();
            if (blender == null)
            {
                message = "Blender was not found. Set its path in Rirytools > Import Preset... (Better FBX section).";
                return null;
            }
            string options = Options;
            message = OptionsError(options);
            if (message != null) return null;

            string fbx = FbxPathFor(blendPath);
            string script = Path.GetFullPath(Path.Combine("Library", "RiryImportPresets", "better_fbx_export.py"));
            Directory.CreateDirectory(Path.GetDirectoryName(script));
            File.WriteAllText(script, Script, new UTF8Encoding(false));

            string backup = File.Exists(fbx) ? Path.GetFullPath(Path.Combine("Library", "RiryImportPresets", "previous.fbx")) : null;
            if (backup != null) File.Copy(fbx, backup, true);

            // Keep Unity from importing a half-written FBX.
            AssetDatabase.DisallowAutoRefresh();
            bool succeeded = false;
            try
            {
                string output;
                int exitCode = Run(blender, new[]
                {
                    "--background", Path.GetFullPath(blendPath), "--python-exit-code", "3",
                    "--python", script, "--", Path.GetFullPath(fbx), options
                }, blendPath, out output);
                succeeded = exitCode == 0 && output.Contains(SuccessMarker) && File.Exists(fbx);
                if (!succeeded)
                {
                    message = "Better FBX export of " + blendPath + " failed (exit code " + exitCode + "):\n" +
                        Tail(output, 3000);
                    return null;
                }
            }
            catch (Exception exception)
            {
                message = "Could not run Blender (" + blender + "): " + exception.Message;
                return null;
            }
            finally
            {
                if (!succeeded && backup != null && File.Exists(backup)) File.Copy(backup, fbx, true);
                if (backup != null && File.Exists(backup)) File.Delete(backup);
                AssetDatabase.AllowAutoRefresh();
            }

            AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
            ImportPresetOwnedFiles.Record(fbx);
            message = "Exported " + blendPath + " to " + fbx + " with Better FBX.";
            return fbx;
        }

        private static int Run(string executable, string[] arguments, string label, out string output)
        {
            var info = new ProcessStartInfo(executable, JoinArguments(arguments))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var text = new StringBuilder();
            using (var process = new Process { StartInfo = info })
            {
                process.OutputDataReceived += (sender, line) => { if (line.Data != null) lock (text) text.AppendLine(line.Data); };
                process.ErrorDataReceived += (sender, line) => { if (line.Data != null) lock (text) text.AppendLine(line.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var timer = Stopwatch.StartNew();
                try
                {
                    while (!process.WaitForExit(200))
                    {
                        bool cancelled = EditorUtility.DisplayCancelableProgressBar("Better FBX export",
                            "Blender is exporting " + label + "...", (float)(timer.Elapsed.TotalSeconds / TimeoutSeconds));
                        if (cancelled || timer.Elapsed.TotalSeconds > TimeoutSeconds)
                        {
                            try { process.Kill(); } catch (InvalidOperationException) { }
                            process.WaitForExit();
                            lock (text) output = text + (cancelled ? "\nCancelled." : "\nTimed out.");
                            return -1;
                        }
                    }
                    process.WaitForExit();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
                lock (text) output = text.ToString();
                return process.ExitCode;
            }
        }

        /// <summary>Quotes arguments so Windows (CommandLineToArgvW rules) and Mono split them back exactly.</summary>
        internal static string JoinArguments(IEnumerable<string> arguments)
        {
            var line = new StringBuilder();
            foreach (string argument in arguments)
            {
                if (line.Length > 0) line.Append(' ');
                line.Append(QuoteArgument(argument ?? ""));
            }
            return line.ToString();
        }

        internal static string QuoteArgument(string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0) return argument;
            var quoted = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }
                // Backslashes before a quote are doubled, then the quote is escaped.
                quoted.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
                backslashes = 0;
                quoted.Append(character);
            }
            // Backslashes before the closing quote are doubled.
            quoted.Append('\\', backslashes * 2).Append('"');
            return quoted.ToString();
        }

        private static string Tail(string text, int length)
        {
            return text.Length <= length ? text : "..." + text.Substring(text.Length - length);
        }
    }
}
#endif
