using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine.Networking;

namespace Riry.RiryTools.Editor
{
    /// <summary>Enables Git LFS for the repository.</summary>
    internal static class AutoSyncGitLfs
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
            GitRunner.Run(directory, null, 30000, "lfs", "install", "--local").RequireSuccess("Enabling Git LFS");
        }
    }
}
