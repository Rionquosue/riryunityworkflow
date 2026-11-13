using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>The popup after a run: the summary, then each section of the report.</summary>
    internal sealed class AvatarMigratorRunReportWindow : EditorWindow
    {
        private AvatarMigratorRunReport report;
        private Vector2 scroll;

        internal static void Open(AvatarMigratorRunReport report)
        {
            RiryLog.Info(RiryLog.AvatarMigration, report.Text());
            var window = CreateInstance<AvatarMigratorRunReportWindow>();
            window.titleContent = new GUIContent(AvatarMigratorWindow.Title + ": done");
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
