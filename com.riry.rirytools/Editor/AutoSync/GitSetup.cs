using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine.Networking;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Whether git is installed and the project folder is a repository, cached
    /// for a few seconds. The repository is always the project
    /// folder itself (with .git beside Assets); a project that only sits inside
    /// another repository does not count.
    /// </summary>
    internal static class GitState
    {
        private const double CacheSeconds = 5.0;
        private static double checkedAt = double.MinValue;
        private static bool installed;
        private static bool repositoryExists;
        private static string enclosingRepository;

        internal static bool GitInstalled
        {
            get { Refresh(false); return installed; }
        }

        /// <summary>True when the project folder is the top folder of a repository.</summary>
        internal static bool RepositoryExists
        {
            get { Refresh(false); return repositoryExists; }
        }

        /// <summary>A repository above the project folder that contains it, or null.</summary>
        internal static string EnclosingRepository
        {
            get { Refresh(false); return enclosingRepository; }
        }

        internal static void Refresh(bool force)
        {
            if (!force && EditorApplication.timeSinceStartup - checkedAt < CacheSeconds) return;
            checkedAt = EditorApplication.timeSinceStartup;
            string project = RiryToolsPaths.ProjectRoot;
            try
            {
                installed = GitRunner.Run(project, null, 10000, "--version").Succeeded;
                string[] location = Location(project);
                repositoryExists = location != null && location[1].Length == 0;
                enclosingRepository = location != null && !repositoryExists ? location[0] : null;
            }
            catch (GitNotInstalledException)
            {
                installed = false;
                repositoryExists = false;
                enclosingRepository = null;
            }
        }

        /// <summary>
        /// The repository's top folder and the directory's path inside it ("" at the
        /// top), or null outside any repository.
        /// </summary>
        internal static string[] Location(string directory)
        {
            GitRunner.Result result = GitRunner.Run(directory, null, 10000, "rev-parse", "--show-toplevel", "--show-prefix");
            if (!result.Succeeded) return null;
            string[] lines = result.Output.Replace("\r", "").Split('\n');
            return new[] { lines[0].Trim(), lines.Length > 1 ? lines[1].Trim() : "" };
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
    /// kept in Documents/RiryTools/GitTemplates. Names follow the import preset name rules.
    /// </summary>
    internal static class GitTemplates
    {
        internal const string IgnoreExtension = ".gitignore";
        internal const string AttributesExtension = ".gitattributes";
        // The template Sync copies into a new repository.
        internal const string DefaultName = "Unity";
        internal const string UnityIgnoreUrl = "https://raw.githubusercontent.com/github/gitignore/main/Unity.gitignore";
        internal const string UnityAttributesUrl =
            "https://raw.githubusercontent.com/gitattributes/gitattributes/master/Unity.gitattributes";

        // Used when the library has no attributes template. Unity YAML stays
        // text; large binary formats go to Git LFS. Built by Lfs(), so it cannot be const.
        internal static readonly string DefaultAttributes =
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

        private static string LfsLine(string extension)
        {
            return "*." + extension + " filter=lfs diff=lfs merge=lfs -text\n";
        }

        private static string Lfs(params string[] extensions)
        {
            var text = new StringBuilder();
            foreach (string extension in extensions) text.Append(LfsLine(extension));
            return text.ToString();
        }

        internal static string RootPath
        {
            get { return RiryToolsPaths.Documents("GitTemplates"); }
        }

        internal static string EnsureRoot()
        {
            Directory.CreateDirectory(RootPath);
            return RootPath;
        }

        internal static List<string> Names(string extension)
        {
            var names = new List<string>();
            if (!Directory.Exists(RootPath)) return names;
            foreach (string file in Directory.GetFiles(RootPath, "*" + extension))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (ImportPresetLibrary.ValidateName(name) == null) names.Add(name);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        internal static string PathFor(string name, string extension)
        {
            return Path.Combine(RootPath, name + extension);
        }

        internal static void Save(string name, string extension, string text)
        {
            string error = ImportPresetLibrary.ValidateName(name);
            if (error != null) throw new ArgumentException(error);
            EnsureRoot();
            File.WriteAllText(PathFor(name, extension), text.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }

        /// <summary>
        /// With no templates, saves GitHub's Unity.gitignore as "Unity", and the
        /// gitattributes project's Unity.gitattributes as "Unity" beside the built-in
        /// "UnityLFS", so there is always a choice. The built-in template is saved
        /// even when the download fails. Returns true when it saved any.
        /// </summary>
        internal static bool EnsureDefaults()
        {
            bool saved = false;
            if (Names(IgnoreExtension).Count == 0)
            {
                Save(DefaultName, IgnoreExtension, Download(UnityIgnoreUrl));
                saved = true;
            }
            if (Names(AttributesExtension).Count > 0) return saved;
            Save("UnityLFS", AttributesExtension, DefaultAttributes);
            Save(DefaultName, AttributesExtension, Download(UnityAttributesUrl));
            return true;
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
                        if (EditorUtility.DisplayCancelableProgressBar("AutoSync", "Downloading " + url, request.downloadProgress))
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

    /// <summary>Enables Git LFS for the repository.</summary>
    internal static class GitLfs
    {
        internal static bool Installed(string directory)
        {
            return GitRunner.Run(directory, null, 10000, "lfs", "version").Succeeded;
        }

        /// <summary>Installs the LFS hooks into this repository (idempotent).</summary>
        internal static void Enable(string directory)
        {
            if (!Installed(directory))
                throw new InvalidOperationException("Git LFS is not installed. It ships with Git for Windows; " +
                    "elsewhere install the git-lfs package.");
            GitRunner.Result result = GitRunner.Run(directory, null, 30000, "lfs", "install", "--local");
            if (!result.Succeeded) throw new InvalidOperationException(result.Describe("Enabling Git LFS"));
        }
    }
}
