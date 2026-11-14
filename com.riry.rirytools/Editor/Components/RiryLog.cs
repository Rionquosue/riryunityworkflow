using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The one way RiryTools writes to the Console: every line starts with "[RiryTools/&lt;tool&gt;]", so it can be traced
    /// to the tool that wrote it, and takes the object it is about (context), so clicking the line pings it.
    /// Tool names are the Tool constants below.
    /// </summary>
    internal static class RiryLog
    {
        internal const string AttachTo = "Attach To";
        internal const string AutoSync = "AutoSync";
        internal const string AvatarMigration = "Avatar Migration";
        internal const string BlendshapeSync = "Blendshape Sync";
        internal const string CleanKeys = "Clean Animation Keys";
        internal const string ControllerCleaner = "Controller Cleaner";
        internal const string FindNullReferences = "Find Null references";
        internal const string FindReplace = "Find and Replace";
        internal const string ConstraintTool = "Constraint Tool";
        internal const string GuidTools = "GUID Tools";
        internal const string IconCap = "IconCap";
        internal const string ImportPresets = "Import Presets";
        internal const string MenuMockup = "Menu Mockup";
        internal const string MenuTool = "Menu Tool";
        internal const string ScriptLoadGuard = "Script Load Guard";
        internal const string StopAutoGame = "Stop Auto Game";

        internal static string Format(string tool, string message)
        {
            return "[RiryTools/" + tool + "] " + message;
        }

        internal static void Info(string tool, string message, Object context = null)
        {
            Debug.Log(Format(tool, message), context);
        }

        internal static void Warn(string tool, string message, Object context = null)
        {
            Debug.LogWarning(Format(tool, message), context);
        }

        internal static void Error(string tool, string message, Object context = null)
        {
            Debug.LogError(Format(tool, message), context);
        }

        /// <summary>
        /// An exception with its full stack (clickable in the Console), under a line saying which tool and what it was
        /// doing (<paramref name="what"/>, for example "Generate failed").
        /// </summary>
        internal static void Exception(string tool, string what, Exception exception, Object context = null)
        {
            Debug.LogException(new RiryToolsException(Format(tool, what + ": " + exception.Message), exception), context);
        }
    }

    /// <summary>Wraps an exception for RiryLog.Exception: its message names the tool, the inner exception keeps the stack.</summary>
    internal sealed class RiryToolsException : Exception
    {
        internal RiryToolsException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}

// ============================================================================
// GLOSSARY: Console logging
// ============================================================================
//
// RiryLog (this file)
//   Format(tool, message)        "[RiryTools/<tool>] <message>".
//   Info / Warn / Error(tool, message, context)  Debug.Log / LogWarning / LogError with that prefix; context is the
//                                object the line is about (clicking the line pings it), or null.
//   Exception(tool, what, exception, context)  Debug.LogException of a RiryToolsException whose message is the prefixed
//                                "<what>: <message>" and whose inner exception is the original, stack included.
//   Tool constants               AttachTo, AutoSync, AvatarMigration, BlendshapeSync, CleanKeys, ConstraintTool,
//                                GuidTools, IconCap, ImportPresets, MenuMockup, MenuTool, ProjectFolder, ScriptLoadGuard,
//                                StopAutoGame: the tool names every line uses.
// RiryToolsException             The wrapper above; never thrown.
