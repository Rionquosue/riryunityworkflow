using System;
using System.IO;
using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Delete GUID Reference Cache in the GUID Tools window: asks first, then deletes the derived cache through
    /// GuidToolReferenceCache (Editor/GuidTools/GuidToolReferenceCache.cs), which owns it.
    /// </summary>
    internal static class GuidToolClearReferenceCache
    {
        private const string Title = "Delete GUID Reference Cache";

        internal static void Run()
        {
            using (GuidToolLog log = GuidToolLog.Open(Title))
            {
                try
                {
                    string cachePath = GuidToolReferenceCache.CacheDirectoryPath;
                    log.Write("Resolved cache path: " + cachePath);

                    if (!Directory.Exists(cachePath))
                    {
                        log.Write("No cache directory exists; nothing was deleted.");
                        EditorUtility.DisplayDialog(Title, "No GUID reference cache currently exists.", "OK");
                        return;
                    }

                    if (!EditorUtility.DisplayDialog(
                            Title,
                            "Delete the complete GUID reference cache?\n\n" +
                            "The next GUID regeneration will perform a full scan.\n\n" +
                            cachePath,
                            "Delete Cache",
                            "Cancel"))
                    {
                        log.Write("Cache deletion cancelled by the user.");
                        return;
                    }

                    int files;
                    long bytes;
                    GuidToolReferenceCache.Measure(out files, out bytes);
                    log.Write("Deleting cache files: " + files + " | bytes: " + bytes);

                    GuidToolReferenceCache.Delete();

                    // The dialog reports it; the GUID Tools log keeps the record.
                    log.Write("Cache directory deleted successfully.");
                    EditorUtility.DisplayDialog(Title, "The GUID reference cache was deleted.", "OK");
                }
                catch (Exception exception)
                {
                    log.Write("FAILURE: " + exception);
                    RiryLog.Exception(RiryLog.GuidTools, "Could not delete the GUID reference cache", exception);
                    EditorUtility.DisplayDialog(
                        "Cache Deletion Failed",
                        exception.Message + "\n\nCheck the Console and GUID Tools log for details.",
                        "OK");
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Delete GUID Reference Cache
// ============================================================================
//
// GuidToolClearReferenceCache (this file)
//   Run()      Says so when there is no cache; otherwise asks, logs the file count and size (GuidToolReferenceCache.Measure,
//              one pass), deletes the folder (GuidToolReferenceCache.Delete) and reports it. Failures are logged and shown.
