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
}
