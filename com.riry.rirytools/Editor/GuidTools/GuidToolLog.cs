using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor
{
internal sealed class GuidToolLog : IDisposable
{
    private const long ActiveLogLimit = 10L * 1024L * 1024L;
    private const long ArchiveLimit = 100L * 1024L * 1024L;
    private const string ActiveLogName = "guid-tools.log";

    private static readonly UTF8Encoding LogEncoding = new UTF8Encoding(false);

    private StreamWriter writer;
    private bool writeFailureReported;
    private string operation;
    private string activeLogPath;
    private string archiveDirectory;
    private long activeBytes;

    private GuidToolLog()
    {
    }

    internal static GuidToolLog Open(string operation)
    {
        var log = new GuidToolLog { operation = operation };

        try
        {
            string logDirectory = GuidToolRegenerate.LogDirectoryPath;
            log.archiveDirectory = Path.Combine(logDirectory, "archive");
            log.activeLogPath = Path.Combine(logDirectory, ActiveLogName);

            Directory.CreateDirectory(logDirectory);
            List<string> maintenanceMessages = log.OpenWriter();

            log.Write("BEGIN " + operation);
            log.Write("Log path: " + log.activeLogPath);
            foreach (string message in maintenanceMessages)
                log.Write(message);
        }
        catch (Exception exception)
        {
            log.writer = null;
            RiryLog.Exception(RiryLog.GuidTools, "Could not open the diagnostic log", exception);
        }

        return log;
    }

    private List<string> OpenWriter()
    {
        var maintenanceMessages = new List<string>();
        RotateActiveLog(activeLogPath, archiveDirectory, maintenanceMessages);
        TrimArchive(archiveDirectory, maintenanceMessages);

        var stream = new FileStream(
            activeLogPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);
        activeBytes = stream.Length;
        writer = new StreamWriter(stream, LogEncoding);
        writer.AutoFlush = true;
        return maintenanceMessages;
    }

    internal void Write(string message)
    {
        if (writer == null)
            return;

        try
        {
            string line = "[" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                          "] " + message;
            writer.WriteLine(line);
            activeBytes += LogEncoding.GetByteCount(line) + writer.NewLine.Length;

            // Enforce the limits during long runs, not only when a run starts.
            if (activeBytes > ActiveLogLimit)
            {
                writer.Dispose();
                writer = null;
                foreach (string maintenance in OpenWriter())
                    Write(maintenance);
            }
        }
        catch (Exception exception)
        {
            if (!writeFailureReported)
            {
                writeFailureReported = true;
                RiryLog.Exception(RiryLog.GuidTools, "Diagnostic logging failed", exception);
            }
        }
    }

    public void Dispose()
    {
        if (writer == null)
            return;

        try
        {
            Write("END " + operation);
            writer.Dispose();
        }
        catch (Exception exception)
        {
            if (!writeFailureReported)
                RiryLog.Exception(RiryLog.GuidTools, "Could not close the diagnostic log", exception);
        }
        finally
        {
            writer = null;
        }
    }

    private static void RotateActiveLog(
        string activeLogPath,
        string archiveDirectory,
        List<string> maintenanceMessages)
    {
        if (!File.Exists(activeLogPath) || new FileInfo(activeLogPath).Length <= ActiveLogLimit)
            return;

        Directory.CreateDirectory(archiveDirectory);
        string archivedName =
            "guid-tools_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) +
            ".log";
        string archivedPath = Path.Combine(archiveDirectory, archivedName);

        int suffix = 1;
        while (File.Exists(archivedPath))
        {
            archivedPath = Path.Combine(
                archiveDirectory,
                Path.GetFileNameWithoutExtension(archivedName) + "_" + suffix + ".log");
            suffix++;
        }

        File.Move(activeLogPath, archivedPath);
        maintenanceMessages.Add("Rotated active log to: " + archivedPath);
    }

    private static void TrimArchive(
        string archiveDirectory,
        List<string> maintenanceMessages)
    {
        if (!Directory.Exists(archiveDirectory))
            return;

        string[] paths = Directory.GetFiles(archiveDirectory, "*.log", SearchOption.TopDirectoryOnly);
        var files = new List<FileInfo>(paths.Length);
        long totalBytes = 0;

        foreach (string path in paths)
        {
            var file = new FileInfo(path);
            files.Add(file);
            totalBytes += file.Length;
        }

        files.Sort((left, right) =>
        {
            int timeComparison = left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc);
            return timeComparison != 0
                ? timeComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left.FullName, right.FullName);
        });

        int index = 0;
        while (totalBytes > ArchiveLimit && index < files.Count)
        {
            FileInfo oldest = files[index++];
            long length = oldest.Length;
            string path = oldest.FullName;
            oldest.Delete();
            totalBytes -= length;
            maintenanceMessages.Add("Deleted oldest archived log: " + path);
        }
    }
}
}

// ============================================================================
// GLOSSARY: GUID Tools log
// ============================================================================
//
// GuidToolLog (this file)
//   Open(operation) / Write(message) / Dispose()  Logs/RiryTools/GuidTools/guid-tools.log (GuidToolRegenerate.LogDirectoryPath).
//   ActiveLogLimit / ArchiveLimit   10 MiB / 100 MiB: the active log is rotated into archive/guid-tools_<UTC time>.log
//                                   (RotateActiveLog), and the archive trimmed (TrimArchive).
