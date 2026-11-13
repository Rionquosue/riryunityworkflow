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
/// <summary>
/// GUID regeneration: putting every written byte back when a run fails, and removing temporary files.
/// </summary>
internal static partial class GuidToolRegenerate
{
    internal static void RollBack(List<WrittenOperation> written, GuidToolLog log)
    {
        var byFile = new Dictionary<string, List<WrittenOperation>>(StringComparer.OrdinalIgnoreCase);
        foreach (WrittenOperation operation in written)
        {
            List<WrittenOperation> operations;
            if (!byFile.TryGetValue(operation.FullPath, out operations))
            {
                operations = new List<WrittenOperation>();
                byFile.Add(operation.FullPath, operations);
            }
            operations.Add(operation);
        }

        var check = new byte[GuidLength];
        var failures = new List<Exception>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (KeyValuePair<string, List<WrittenOperation>> file in byFile)
            {
                try
                {
                    using (var stream = new FileStream(
                               file.Key,
                               FileMode.Open,
                               FileAccess.ReadWrite,
                               FileShare.Read))
                    {
                        foreach (WrittenOperation operation in file.Value)
                        {
                            try
                            {
                                stream.Position = operation.Offset;
                                int read = stream.Read(check, 0, check.Length);
                                if (read != GuidLength)
                                    throw new IOException(
                                        "Rollback could not read the original GUID location.");

                                if (GuidToolBytes.Matches(check, 0, operation.OldBytes))
                                {
                                    log.Write("Rollback found the original GUID already present at offset " +
                                              operation.Offset + " in " + file.Key);
                                    continue;
                                }

                                if (!GuidToolBytes.Matches(check, 0, operation.NewBytes))
                                    log.Write(
                                        "Rollback found a partial or externally changed GUID at offset " +
                                        operation.Offset + " in " + file.Key +
                                        "; restoring the known original bytes.");

                                stream.Position = operation.Offset;
                                stream.Write(operation.OldBytes, 0, GuidLength);
                                log.Write("Rolled back offset " + operation.Offset +
                                          " in " + file.Key);
                            }
                            catch (Exception exception)
                            {
                                failures.Add(new IOException(
                                    "Rollback failed at offset " + operation.Offset +
                                    " in " + file.Key + ".",
                                    exception));
                            }
                        }

                        try
                        {
                            stream.Flush();
                        }
                        catch (Exception exception)
                        {
                            failures.Add(new IOException(
                                "Rollback flush failed for " + file.Key + ".",
                                exception));
                        }
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        "Rollback could not process " + file.Key + ".",
                        exception));
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        if (failures.Count > 0)
            throw new AggregateException(
                "One or more files could not be completely restored.",
                failures);
    }

    private static void ClearTemporaryData(
        List<Target> targets,
        List<FileState> files,
        List<FilePatch> patches,
        List<WrittenOperation> written)
    {
        if (targets != null)
        {
            foreach (Target target in targets)
            {
                foreach (List<long> offsets in target.Occurrences.Values)
                    offsets.Clear();
                target.Occurrences.Clear();

                if (target.Cache != null)
                {
                    foreach (CachedReference reference in target.Cache.References.Values)
                    {
                        if (reference.Offsets != null)
                            reference.Offsets.Clear();
                    }
                    target.Cache.References.Clear();
                }

                target.OldBytes = null;
                target.NewBytes = null;
                target.Cache = null;
            }
            targets.Clear();
        }

        if (files != null)
            files.Clear();
        if (patches != null)
            patches.Clear();
        if (written != null)
            written.Clear();
    }
}
}

// ============================================================================
// GLOSSARY: GUID regeneration: rollback
// GuidToolRegenerate is one partial class: Editor/GuidTools/GuidToolRegenerate(.Scan/.Patch/.Rollback).cs and Editor/UI/GuidTools/GuidToolRegenerate.Prompts.cs.
// ============================================================================
//
// GuidToolRegenerate (this part)
//   RollBack(written, log)          Puts every written byte back after a failure, checking each before it is restored.
//   ClearTemporaryData(...)         Removes temporary files the run made.
