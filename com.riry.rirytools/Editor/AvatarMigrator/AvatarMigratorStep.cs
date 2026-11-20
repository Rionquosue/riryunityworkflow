using System;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// One step of an Avatar Migration Assistant run (the copy, AvatarMigratorCopyRun, or Retarget animations,
    /// AvatarMigratorRetargetAnim): a failure is logged and reported, and the run goes on.
    /// </summary>
    internal static class AvatarMigratorStep
    {
        internal static void Run(string name, Action step, AvatarMigratorRunReport report, Transform targetRoot)
        {
            EditorUtility.DisplayProgressBar(AvatarMigratorWindow.Title, name, 0.5f);
            try
            {
                step();
            }
            catch (Exception exception)
            {
                // Named by its step; the run report lists it too, so this is the one with the stack.
                RiryLog.Exception(RiryLog.AvatarMigration, name + " failed", exception, targetRoot.gameObject);
                report.Failures.Add(name + ": " + exception.Message);
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant step
// ============================================================================
//
// AvatarMigratorStep (this file)
//   Run(name, step, report, targetRoot)  Shows the step's name on the progress bar and runs it; a failure is logged
//                           with the target as context and added to report.Failures.
