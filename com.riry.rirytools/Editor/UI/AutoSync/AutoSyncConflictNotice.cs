using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Says that rebasing onto the remote's new commits conflicted, so Sync moved the saves to a new branch, checked it out
    /// and pushed it there (AutoSyncJob). Sync stays on.
    /// </summary>
    internal static class AutoSyncConflictNotice
    {
        internal static void Show(AutoSyncJob.Outcome outcome)
        {
            string text = "The remote's " + outcome.ConflictFrom + " has commits that conflict with this project's saves, so Sync moved the saves to " +
                "a new branch, " + outcome.ConflictBranch + ", checked it out and pushed it there. Sync stays on and commits to it.\n\n" +
                "Merge " + outcome.ConflictBranch + " into " + outcome.ConflictFrom + " in your Git client and fix the conflicts, then check out " +
                outcome.ConflictFrom + " again.";
            RiryLog.Warn(RiryLog.AutoSync, text);
            EditorUtility.DisplayDialog("Sync Conflict", text, "OK");
        }
    }
}

// ============================================================================
// GLOSSARY: AutoSync conflict notice
// ============================================================================
//
// AutoSyncConflictNotice (this file)
//   Show(outcome)            Logs and shows which branch the saves moved to and how to merge it back.
