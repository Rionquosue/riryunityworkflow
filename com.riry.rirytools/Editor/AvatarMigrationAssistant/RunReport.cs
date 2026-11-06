using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>What a run did, and what needs a look; shown after the run and logged.</summary>
    internal sealed class AvatarRunReport
    {
        internal int Copied;
        internal int CreatedObjects;
        internal int CopiedPrefabs;
        internal int PrefabsAlreadyThere;
        internal int SkippedIdentical;
        // "Possible issues with": things the user should investigate (prefab overrides, missing meshes, the upgrade).
        internal readonly List<string> PossibleIssues = new List<string>();
        internal readonly List<string> NotCopied = new List<string>();
        internal readonly List<string> ReferencesCleared = new List<string>();
        internal readonly List<string> Failures = new List<string>();
        // Unpacked prefabs placed again as child prefabs (RepackPrefabs.cs).
        internal readonly List<string> Repacked = new List<string>();
        // Their instance roots, which the window ticks for Retarget animations.
        internal readonly List<GameObject> RepackedRoots = new List<GameObject>();
        // Feature 15: files written to disk, which Undo does not cover.
        internal readonly List<string> FilesCreated = new List<string>();

        internal string Summary
        {
            get
            {
                return Copied + " component" + (Copied == 1 ? "" : "s") + " copied, " + CopiedPrefabs + " prefab" + (CopiedPrefabs == 1 ? "" : "s") +
                    " copied (" + PrefabsAlreadyThere + " already on the target), " + CreatedObjects + " object" + (CreatedObjects == 1 ? "" : "s") +
                    " created, " + SkippedIdentical + " identical component" + (SkippedIdentical == 1 ? "" : "s") + " skipped, " +
                    FilesCreated.Count + " file" + (FilesCreated.Count == 1 ? "" : "s") + " created.";
            }
        }

        internal string Text()
        {
            var text = new StringBuilder(Summary);
            Section(text, "Unpacked prefabs placed as child prefabs (changes kept as overrides):", Repacked);
            Section(text, "Possible issues with:", PossibleIssues);
            Section(text, "Not copied:", NotCopied);
            Section(text, "References set to none:", ReferencesCleared);
            Section(text, "Failed steps:", Failures);
            Section(text, "Files created (not covered by Undo):", FilesCreated);
            return text.ToString();
        }

        private static void Section(StringBuilder text, string title, List<string> lines)
        {
            if (lines.Count == 0) return;
            text.Append("\n\n").Append(title);
            foreach (string line in lines) text.Append("\n- ").Append(line);
        }
    }

    /// <summary>The popup after a run: the summary, then each section of the report.</summary>
    internal sealed class AvatarRunReportWindow : EditorWindow
    {
        private AvatarRunReport report;
        private Vector2 scroll;

        internal static void Open(AvatarRunReport report)
        {
            RiryLog.Info(RiryLog.AvatarMigration, report.Text());
            var window = CreateInstance<AvatarRunReportWindow>();
            window.titleContent = new GUIContent(AvatarMigrationAssistant.Title + ": done");
            window.report = report;
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(main.center.x - 320f, main.center.y - 240f, 640f, 480f);
            // Small enough to move aside, never so small the summary and Close are cut off.
            window.minSize = new Vector2(420f, 260f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (report == null)
            {
                Close();
                return;
            }
            EditorGUILayout.LabelField(report.Summary, EditorStyles.wordWrappedLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Section("Possible issues with:", report.PossibleIssues, MessageType.Warning);
            Section("Not copied:", report.NotCopied, MessageType.Info);
            Section("References set to none:", report.ReferencesCleared, MessageType.Info);
            Section("Failed steps:", report.Failures, MessageType.Error);
            Section("Files created (not covered by Undo):", report.FilesCreated, MessageType.None);
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(90f))) Close();
            }
        }

        private static void Section(string title, List<string> lines, MessageType type)
        {
            if (lines.Count == 0) return;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string line in lines) EditorGUILayout.HelpBox(line, type);
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant report (RunReport.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarRunReport            Counts (Copied, CreatedObjects, CopiedPrefabs, PrefabsAlreadyThere, SkippedIdentical) and lines:
//                            PossibleIssues ("Possible issues with:", to investigate), NotCopied, ReferencesCleared,
//                            Failures, FilesCreated (feature 15: files on disk are not covered by Undo). Summary / Text().
// AvatarRunReportWindow      Show(report): logs the text, then a popup with the summary and each section; Close. minSize 420 x 260.
