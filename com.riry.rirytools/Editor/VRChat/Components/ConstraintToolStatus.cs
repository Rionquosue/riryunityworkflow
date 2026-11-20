using System;

namespace Riry.RiryTools.Editor
{
    /// <summary>The status line of Constraint Tools' Train, Bezier and Insertable prop tabs, put in the Console too.</summary>
    internal static class ConstraintToolStatus
    {
        /// <summary>
        /// Runs an action button and puts the status it sets in the Console too: the window's status line is gone once it
        /// closes, so this traces what the tool did. (A status set again, even to the same text, is a new string.)
        /// status reads the tab's status; tab is the tab's name, which starts the log line.
        /// </summary>
        internal static void Act(Action action, Func<string> status, string tab)
        {
            string before = status();
            action();
            string after = status();
            if (!string.IsNullOrEmpty(after) && !ReferenceEquals(after, before))
                RiryLog.Info(RiryLog.ConstraintTool, tab + ": " + after);
        }
    }
}

// ============================================================================
// GLOSSARY: Constraint Tools status
// ============================================================================
//
// ConstraintToolStatus (this file)
//   Act(action, status, tab) Runs the action; when it set a new status, logs "<tab>: <status>".
