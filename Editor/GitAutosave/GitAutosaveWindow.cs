#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    internal sealed class GitAutosaveWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Rirytools/Git Autosave";

        [MenuItem(MenuPath, false, 2050)]
        private static void Open()
        {
            GitAutosaveWindow window = GetWindow<GitAutosaveWindow>();
            window.titleContent = new GUIContent("Git Autosave");
            window.minSize = new Vector2(380f, 300f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Commits scenes, prefabs and .meta files to the autosave branch a few seconds after they change, " +
                "through a separate Git index: the checked-out branch and staged changes are never touched.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("Enabled", GitAutosave.Enabled);
            string branch = EditorGUILayout.DelayedTextField("Autosave branch", GitAutosave.Branch).Trim();
            string baseBranch = EditorGUILayout.DelayedTextField("Base branch", GitAutosave.BaseBranch).Trim();
            bool push = EditorGUILayout.Toggle("Push", GitAutosave.PushEnabled);
            string remote;
            using (new EditorGUI.DisabledScope(!push))
                remote = EditorGUILayout.DelayedTextField("Remote", GitAutosave.Remote).Trim();
            if (EditorGUI.EndChangeCheck())
            {
                GitAutosave.Enabled = enabled;
                GitAutosave.Branch = branch;
                GitAutosave.BaseBranch = baseBranch;
                GitAutosave.PushEnabled = push;
                GitAutosave.Remote = remote;
                if (enabled) GitAutosave.ScheduleNow();
            }

            if (GitAutosave.SettingsError() != null)
                EditorGUILayout.HelpBox(GitAutosave.SettingsError(), MessageType.Error);
            EditorGUILayout.LabelField("Last run", GitAutosave.Status ?? "Not run in this session.", EditorStyles.wordWrappedLabel);

            using (new EditorGUI.DisabledScope(GitAutosave.SettingsError() != null))
            {
                if (GUILayout.Button("Snapshot Now")) GitAutosave.ScheduleNow();
                GUILayout.Space(8f);
                EditorGUILayout.LabelField("Rebase workflow (GitHub Actions)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Replays every " + GitAutosave.WorkflowPattern(GitAutosave.Branch) + " branch onto " +
                    GitAutosave.BaseBranch + " whenever either is pushed. Commit and push the installed file.",
                    EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Install Rebase Workflow")) Install();
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private static void Install()
        {
            try
            {
                string path = GitAutosave.InstallWorkflow(existing => EditorUtility.DisplayDialog("Replace Rebase Workflow",
                    existing + " already exists with different content. Replace it?", "Replace", "Keep"));
                if (path != null)
                    EditorUtility.DisplayDialog("Rebase Workflow Installed",
                        "Written to:\n" + path + "\n\nCommit and push it to the base branch to activate it.", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Rebase Workflow Not Installed", exception.Message, "OK");
            }
        }
    }
}
#endif
