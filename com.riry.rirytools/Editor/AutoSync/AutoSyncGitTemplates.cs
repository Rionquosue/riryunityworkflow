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
    /// Named .gitignore and .gitattributes templates, such as vrcproject.gitignore,
    /// kept in Documents/RiryTools/AutoSyncGitTemplates. Names follow the import preset name rules.
    /// </summary>
    internal static class AutoSyncGitTemplates
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
}
