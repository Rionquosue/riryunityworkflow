#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine.Networking;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>Whether git is installed and the project is in a repository with a remote, cached for a few seconds.</summary>
    internal static class GitState
    {
        private const double CacheSeconds = 5.0;
        private static double checkedAt = double.MinValue;
        private static bool installed;
        private static string repositoryRoot;
        private static bool hasRemote;

        internal static bool GitInstalled
        {
            get { Refresh(false); return installed; }
        }

        /// <summary>The repository's top folder, or null when the project is not in one.</summary>
        internal static string RepositoryRoot
        {
            get { Refresh(false); return repositoryRoot; }
        }

        internal static bool HasRemote
        {
            get { Refresh(false); return hasRemote; }
        }

        internal static void Refresh(bool force)
        {
            if (!force && EditorApplication.timeSinceStartup - checkedAt < CacheSeconds) return;
            checkedAt = EditorApplication.timeSinceStartup;
            string project = RegenerateAssetGuid.ProjectRootPath;
            try
            {
                installed = GitRunner.Run(project, null, 10000, "--version").Succeeded;
                GitRunner.Result top = GitRunner.Run(project, null, 10000, "rev-parse", "--show-toplevel");
                repositoryRoot = top.Succeeded ? top.Output.Trim() : null;
                hasRemote = repositoryRoot != null && GitRunner.Run(project, null, 10000, "remote").Output.Trim().Length > 0;
            }
            catch (GitNotInstalledException)
            {
                installed = false;
                repositoryRoot = null;
                hasRemote = false;
            }
        }

        /// <summary>Throws GitNotInstalledException when git is missing.</summary>
        internal static void RequireGit()
        {
            Refresh(true);
            if (!installed) throw new GitNotInstalledException(null);
        }
    }

    /// <summary>
    /// Named .gitignore and .gitattributes templates, such as vrcproject.gitignore,
    /// kept beside the import preset configurations in Documents/UnityTools/ImportPresets.
    /// </summary>
    internal static class GitTemplates
    {
        internal const string IgnoreExtension = ".gitignore";
        internal const string AttributesExtension = ".gitattributes";
        internal const string UnityIgnoreUrl = "https://raw.githubusercontent.com/github/gitignore/main/Unity.gitignore";

        // Used when the library has no attributes template. Unity YAML stays
        // text; large binary formats go to Git LFS.
        internal const string DefaultAttributes =
            "# Unity: text assets stay diffable, large binary files go to Git LFS.\n" +
            "* text=auto\n" +
            "*.cs diff=csharp text\n" +
            "\n" +
            "# 3D models\n" + Lfs("fbx", "obj", "blend", "blend1", "dae", "3ds", "max", "ma", "mb", "c4d", "glb", "gltf", "vrm", "abc") +
            "# Images\n" + Lfs("png", "jpg", "jpeg", "tga", "tif", "tiff", "psd", "psb", "exr", "hdr", "bmp", "gif", "iff", "kra", "webp") +
            "# Audio and video\n" + Lfs("wav", "mp3", "ogg", "aif", "aiff", "flac", "mp4", "mov", "avi", "webm") +
            "# Fonts, archives and binaries\n" + Lfs("ttf", "otf", "unitypackage", "zip", "7z", "rar", "dll", "so", "dylib", "exe", "pdb", "bytes", "bin") +
            "# Binary Unity data\n" +
            "LightingData.asset filter=lfs diff=lfs merge=lfs -text\n" +
            "*.[Tt]errain[Dd]ata.asset filter=lfs diff=lfs merge=lfs -text\n";

        internal static string LfsLine(string extension)
        {
            return "*." + extension + " filter=lfs diff=lfs merge=lfs -text\n";
        }

        private static string Lfs(params string[] extensions)
        {
            var text = new StringBuilder();
            foreach (string extension in extensions) text.Append(LfsLine(extension));
            return text.ToString();
        }

        internal static List<string> Names(string extension)
        {
            var names = new List<string>();
            if (!Directory.Exists(ImportPresetLibrary.RootPath)) return names;
            foreach (string file in Directory.GetFiles(ImportPresetLibrary.RootPath, "*" + extension))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (ImportPresetLibrary.ValidateName(name) == null) names.Add(name);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        internal static string PathFor(string name, string extension)
        {
            return Path.Combine(ImportPresetLibrary.RootPath, name + extension);
        }

        internal static void Save(string name, string extension, string text)
        {
            string error = ImportPresetLibrary.ValidateName(name);
            if (error != null) throw new ArgumentException(error);
            ImportPresetLibrary.EnsureRoot();
            File.WriteAllText(PathFor(name, extension), text.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }

        /// <summary>
        /// With no templates, saves GitHub's Unity.gitignore as "Unity" and the
        /// built-in LFS attributes as "UnityLFS", so there is always a choice.
        /// </summary>
        internal static void EnsureDefaults()
        {
            if (Names(IgnoreExtension).Count == 0) Save("Unity", IgnoreExtension, Download(UnityIgnoreUrl));
            if (Names(AttributesExtension).Count == 0) Save("UnityLFS", AttributesExtension, DefaultAttributes);
        }

        private static string Download(string url)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                try
                {
                    while (!operation.isDone)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Configure Sync", "Downloading " + url, request.downloadProgress))
                        {
                            request.Abort();
                            throw new OperationCanceledException();
                        }
                        Thread.Sleep(50);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException("Could not download " + url + ": " + request.error);
                return request.downloadHandler.text;
            }
        }
    }

    /// <summary>Enables Git LFS for the repository and checks that large files use it and can be uploaded.</summary>
    internal static class GitLfs
    {
        internal const long LargeFileBytes = 1024L * 1024L;
        // Cloudflare's proxied request limit on Free and Pro plans.
        internal const long ProxyLimitBytes = 100L * 1024L * 1024L;
        private const string ProbeContent = "Riry AutoSync Git LFS upload check\n";

        internal sealed class Report
        {
            public readonly List<string> NotTracked = new List<string>();
            public readonly HashSet<string> NotTrackedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> OverProxyLimit = new List<string>();
            public string Upload;
            public bool UploadWorks;
        }

        internal static bool Installed(string directory)
        {
            return GitRunner.Run(directory, null, 10000, "lfs", "version").Succeeded;
        }

        /// <summary>Installs the LFS hooks into this repository (idempotent).</summary>
        internal static void Enable(string directory)
        {
            if (!Installed(directory))
                throw new InvalidOperationException("Git LFS is not installed. It ships with Git for Windows; " +
                    "elsewhere install the git-lfs package. See GIT > Help.");
            GitRunner.Result result = GitRunner.Run(directory, null, 30000, "lfs", "install", "--local");
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe("Enabling Git LFS"));
        }

        /// <summary>
        /// Lists binary files of 1 MiB or more under Assets and Packages that the
        /// attributes do not send to LFS, files over the proxy limit, and, with a
        /// remote, whether an LFS object can actually be uploaded.
        /// </summary>
        internal static Report Check(string projectRoot, string remote)
        {
            var report = new Report();
            var binary = new List<string>();
            var sample = new byte[4096];
            foreach (string root in new[] { "Assets", "Packages" })
            {
                if (!Directory.Exists(Path.Combine(projectRoot, root))) continue;
                foreach (string file in Directory.EnumerateFiles(Path.Combine(projectRoot, root), "*", SearchOption.AllDirectories))
                {
                    long length = new FileInfo(file).Length;
                    if (length < LargeFileBytes || file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    string relative = file.Substring(projectRoot.Length + 1).Replace('\\', '/');
                    // Text or binary, a single file this large cannot pass the proxy.
                    if (length > ProxyLimitBytes) report.OverProxyLimit.Add(relative);
                    if (!RegenerateAssetGuid.LooksLikeText(file, sample)) binary.Add(relative);
                }
            }

            if (binary.Count > 0)
            {
                GitRunner.Result attributes = GitRunner.RunWithInput(projectRoot, null,
                    Encoding.UTF8.GetBytes(string.Join("\0", binary) + "\0"), 60000, "check-attr", "-z", "--stdin", "filter");
                if (!attributes.Succeeded) throw new InvalidOperationException(attributes.Describe("Reading LFS attributes"));
                // Output: path NUL attribute NUL value NUL, per file.
                string[] fields = attributes.Output.Split('\0');
                for (int i = 0; i + 2 < fields.Length; i += 3)
                {
                    if (fields[i + 2] == "lfs") continue;
                    report.NotTracked.Add(fields[i]);
                    string extension = Path.GetExtension(fields[i]).TrimStart('.');
                    if (extension.Length > 0) report.NotTrackedExtensions.Add(extension.ToLowerInvariant());
                }
            }

            if (string.IsNullOrEmpty(remote)) return report;
            // Upload one tiny, fixed object; the server deduplicates it on later checks.
            GitRunner.Result pointer = GitRunner.RunWithInput(projectRoot, null, Encoding.UTF8.GetBytes(ProbeContent), 30000,
                "lfs", "clean", "--", "riry-autosync-probe.bin");
            int oid = pointer.Output.IndexOf("oid sha256:", StringComparison.Ordinal);
            if (!pointer.Succeeded || oid < 0)
            {
                report.Upload = pointer.Describe("Creating the LFS test object");
                return report;
            }
            GitRunner.Result upload = GitRunner.Run(projectRoot, null, 120000, "lfs", "push", "--object-id", remote,
                pointer.Output.Substring(oid + "oid sha256:".Length, 64));
            report.UploadWorks = upload.Succeeded;
            report.Upload = upload.Succeeded
                ? "A test object was uploaded to " + remote + " through Git LFS."
                : upload.Describe("Uploading a test object to " + remote);
            return report;
        }

        /// <summary>Appends an LFS rule for each extension not already listed.</summary>
        internal static void Track(string attributesPath, IEnumerable<string> extensions)
        {
            string text = File.Exists(attributesPath) ? File.ReadAllText(attributesPath) : "";
            var added = new StringBuilder(text.Length == 0 || text.EndsWith("\n", StringComparison.Ordinal) ? "" : "\n");
            foreach (string extension in extensions)
            {
                if (text.IndexOf(GitTemplates.LfsLine(extension).TrimEnd('\n'), StringComparison.OrdinalIgnoreCase) < 0)
                    added.Append(GitTemplates.LfsLine(extension));
            }
            File.AppendAllText(attributesPath, added.ToString(), new UTF8Encoding(false));
        }
    }
}
#endif
