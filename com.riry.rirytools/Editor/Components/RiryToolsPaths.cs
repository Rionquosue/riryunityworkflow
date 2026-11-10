using System;
using System.IO;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The folders RiryTools keeps its files in, all named RiryTools: Library/RiryTools
    /// (caches and working files), Logs/RiryTools (logs) and Documents/RiryTools
    /// (libraries shared by every project). Assets/RiryTools is in RiryToolsProjectFolder.
    /// </summary>
    internal static class RiryToolsPaths
    {
        private const string FolderName = "RiryTools";

        /// <summary>The Unity project folder, the one that holds Assets.</summary>
        internal static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        /// <summary>Library/RiryTools/&lt;name&gt; in the project.</summary>
        internal static string Library(string name)
        {
            return Path.Combine(ProjectRoot, "Library", FolderName, name);
        }

        /// <summary>Logs/RiryTools/&lt;name&gt; in the project.</summary>
        internal static string Logs(string name)
        {
            return Path.Combine(ProjectRoot, "Logs", FolderName, name);
        }

        /// <summary>Documents/RiryTools/&lt;name&gt; in the user's Documents folder.</summary>
        internal static string Documents(string name)
        {
            // Windows: the real Documents folder, including a OneDrive redirect.
            // Mono maps MyDocuments to the home folder elsewhere, so use ~/Documents.
            string documents = Application.platform == RuntimePlatform.WindowsEditor
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : null;
            if (string.IsNullOrEmpty(documents))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(home)) home = Environment.GetEnvironmentVariable("HOME") ?? "";
                documents = Path.Combine(home, "Documents");
            }
            return Path.Combine(documents, FolderName, name);
        }
    }
}
