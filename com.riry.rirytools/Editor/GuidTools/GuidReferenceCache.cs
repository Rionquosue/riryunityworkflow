using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{

/// <summary>
/// Deletes only the derived GUID-reference cache created by RegenerateAssetGuid.
/// Its menu entry is in Editor/UI/RiryMenus.cs.
/// </summary>
internal static class GuidReferenceCache
{
    internal static void DeleteWithConfirmation()
    {
        using (GuidToolLog log = GuidToolLog.Open("Delete GUID Reference Cache"))
        {
            try
            {
                string cachePath = RegenerateAssetGuid.CacheDirectoryPath;
                log.Write("Resolved cache path: " + cachePath);

                if (!Directory.Exists(cachePath))
                {
                    log.Write("No cache directory exists; nothing was deleted.");
                    EditorUtility.DisplayDialog(
                        "Delete GUID Reference Cache",
                        "No GUID reference cache currently exists.",
                        "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog(
                        "Delete GUID Reference Cache",
                        "Delete the complete GUID reference cache?\n\n" +
                        "The next GUID regeneration will perform a full scan.\n\n" +
                        cachePath,
                        "Delete Cache",
                        "Cancel"))
                {
                    log.Write("Cache deletion cancelled by the user.");
                    return;
                }

                long bytes = GetDirectorySize(cachePath);
                int files = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories).Length;
                log.Write("Deleting cache files: " + files + " | bytes: " + bytes);

                Directory.Delete(cachePath, true);

                log.Write("Cache directory deleted successfully.");
                Debug.Log("GUID reference cache deleted:\n" + cachePath);
                EditorUtility.DisplayDialog(
                    "Delete GUID Reference Cache",
                    "The GUID reference cache was deleted.",
                    "OK");
            }
            catch (Exception exception)
            {
                log.Write("FAILURE: " + exception);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Cache Deletion Failed",
                    exception.Message + "\n\nCheck the Console and GUID Tools log for details.",
                    "OK");
            }
        }
    }

    private static long GetDirectorySize(string directory)
    {
        long bytes = 0;
        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            bytes += new FileInfo(file).Length;
        return bytes;
    }
}

}
