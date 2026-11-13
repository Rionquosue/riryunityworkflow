using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>Configuration: state and actions. The drawing is in AutoSyncGitConfigureWindow.UI.cs.</summary>
    internal sealed partial class AutoSyncGitConfigureWindow : EditorWindow
    {
        private const string KeepCurrent = "(keep current file)";
        private const string Detached = "(detached HEAD)";

        private string[] ignoreOptions;
        private string[] attributeOptions;
        private int ignoreChoice;
        private int attributeChoice;
        private string[] branches = new string[0];
        private int branchChoice;
        private List<string> remotes = new List<string>();
        private string selectedRemote;
        private string remoteUrl = "";
        private string currentRemoteUrl = "";
        private List<string> largeFiles = new List<string>();
        private Task<string> lfsQuery;
        private string lfsStatus;
        private string templateName = "";
        private Vector2 scroll;

        internal void Reload()
        {
            AutoSyncGitActions.Guard(AutoSyncGitActions.EnsureTemplates);
            ignoreOptions = Options(AutoSyncGitTemplates.IgnoreExtension);
            attributeOptions = Options(AutoSyncGitTemplates.AttributesExtension);
            ignoreChoice = Math.Max(0, Math.Min(ignoreChoice, ignoreOptions.Length - 1));
            attributeChoice = Math.Max(0, Math.Min(attributeChoice, attributeOptions.Length - 1));
            GitState.Refresh(true);
            branches = new string[0];
            remotes = new List<string>();
            selectedRemote = null;
            currentRemoteUrl = "";
            largeFiles = new List<string>();
            if (GitState.RepositoryExists)
            {
                AutoSyncGitActions.Guard(() =>
                {
                    string project = RiryToolsPaths.ProjectRoot;
                    remotes = AutoSyncGitRemotes.Names(project);
                    selectedRemote = AutoSyncGitRemotes.Selected(remotes);
                    if (selectedRemote != null) currentRemoteUrl = AutoSyncGitRemotes.Url(project, selectedRemote);
                    LoadBranches(project);
                    largeFiles = AutoSyncGitLargeFiles.Tracked(project);
                });
            }
            remoteUrl = currentRemoteUrl;
            StartLfsQuery();
        }

        // Local branches, then the chosen remote's branches that have no local branch
        // (git switch creates them); the current branch is first choice.
        private void LoadBranches(string project)
        {
            string current = GitRunner.Run(project, null, 10000, "symbolic-ref", "-q", "--short", "HEAD").Output.Trim();
            var names = new List<string>();
            foreach (string line in GitRunner.Run(project, null, 10000, "for-each-ref", "--format=%(refname:short)", "refs/heads")
                         .Output.Replace("\r", "").Split('\n'))
            {
                if (line.Length > 0) names.Add(line);
            }
            if (current.Length > 0 && !names.Contains(current)) names.Add(current);
            if (selectedRemote != null)
            {
                string prefix = selectedRemote + "/";
                foreach (string line in GitRunner.Run(project, null, 10000, "for-each-ref", "--format=%(refname:short)",
                             "refs/remotes/" + selectedRemote).Output.Replace("\r", "").Split('\n'))
                {
                    if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    string name = line.Substring(prefix.Length);
                    if (name != "HEAD" && !names.Contains(name)) names.Add(name);
                }
            }
            if (current.Length == 0) names.Insert(0, Detached);
            branches = names.ToArray();
            branchChoice = Math.Max(0, names.IndexOf(current.Length > 0 ? current : Detached));
        }

        // Asks the chosen remote whether Git LFS is enabled, off the main thread.
        private void StartLfsQuery()
        {
            lfsQuery = null;
            lfsStatus = null;
            if (selectedRemote == null || currentRemoteUrl.Length == 0) return;
            string project = RiryToolsPaths.ProjectRoot, remote = selectedRemote, url = currentRemoteUrl;
            lfsStatus = "Checking Git LFS on " + remote + "...";
            lfsQuery = Task.Run(() => AutoSyncGitLfsQuery.Ask(project, remote, url));
        }

        private void OnInspectorUpdate()
        {
            if (lfsQuery == null || !lfsQuery.IsCompleted) return;
            lfsStatus = lfsQuery.IsFaulted ? "Git LFS could not be checked: " + lfsQuery.Exception.GetBaseException().Message : lfsQuery.Result;
            lfsQuery = null;
            Repaint();
        }

        private static string[] Options(string extension)
        {
            var options = new List<string>();
            if (File.Exists(AutoSyncGitActions.ProjectFile(extension))) options.Add(KeepCurrent);
            options.AddRange(AutoSyncGitTemplates.Names(extension));
            return options.ToArray();
        }

        /// <summary>Commits pending saves, then checks out the chosen branch with git switch.</summary>
        private void SwitchBranch(string name)
        {
            if (name == Detached) return;
            GitState.RequireGit();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AutoSync.CommitNow();
            GitRunner.Run(RiryToolsPaths.ProjectRoot, null, 120000, "switch", name).RequireSuccess("Switching to " + name);
            AssetDatabase.Refresh();
            Reload();
        }

        private void SelectRemote(string name)
        {
            AutoSyncGitRemotes.Select(name);
            Reload();
        }

        /// <summary>Copies the chosen templates into the project, then saves the push remote when it changed.</summary>
        private void Apply()
        {
            GitState.RequireGit();
            if (ignoreOptions.Length > 0 && ignoreOptions[ignoreChoice] != KeepCurrent)
                AutoSyncGitActions.CopyTemplate(ignoreOptions[ignoreChoice], AutoSyncGitTemplates.IgnoreExtension, true);
            if (attributeOptions.Length > 0 && attributeOptions[attributeChoice] != KeepCurrent)
                AutoSyncGitActions.CopyTemplate(attributeOptions[attributeChoice], AutoSyncGitTemplates.AttributesExtension, true);

            if (remoteUrl.Length > 0 && remoteUrl != currentRemoteUrl) SaveRemote();
            Reload();
        }

        /// <summary>Adds origin when there is no remote (Add Remote), else changes the chosen remote's URL (Change Remote).</summary>
        private void SaveRemote()
        {
            GitState.RequireGit();
            string project = RiryToolsPaths.ProjectRoot;
            (selectedRemote == null
                ? GitRunner.Run(project, null, 30000, "remote", "add", AutoSyncGitRemotes.DefaultName, remoteUrl)
                : GitRunner.Run(project, null, 30000, "remote", "set-url", selectedRemote, remoteUrl)).RequireSuccess("Setting the remote");
            Reload();
        }
    }
}

// ============================================================================
// GLOSSARY: AutoSync Configuration window, fields and actions
// Drawing is in AutoSyncGitConfigureWindow.UI.cs.
// ============================================================================
//
// AutoSyncGitConfigureWindow (Configuration)
//   ignoreOptions / attributeOptions  string[]  Template popup entries.
//   ignoreChoice / attributeChoice    int       Selected entries.
//   branches / branchChoice           Local branches, the remote's other branches, the current one chosen.
//   remotes / selectedRemote          Remote names; the one Sync pushes to (AutoSyncGitRemotes.Selected).
//   remoteUrl             string     Push to remote, as edited.
//   currentRemoteUrl      string     URL of the chosen remote, or "".
//   largeFiles            List<string>  Tracked files over 100 MiB (shows the ! button).
//   lfsQuery / lfsStatus  The Git LFS query running off the main thread, and its result line.
//   templateName          string     Name for Save template.
//   scroll                Vector2    Scroll position.
//   KeepCurrent / Detached  const string  "(keep current file)" and "(detached HEAD)" popup entries.
//   Reload()              Creates default templates if needed; reloads branches, remotes, large files; starts the LFS query.
//   LoadBranches(project) Fills the Branch popup.
//   StartLfsQuery()       Asks the chosen remote whether Git LFS is enabled (AutoSyncGitLfsQuery.Ask).
//   OnInspectorUpdate()   Shows the LFS query's result when it finishes.
//   Options(extension)    Popup entries for one template type.
//   SwitchBranch(name)    Saves modified scenes if wanted, commits pending saves, runs git switch.
//   SelectRemote(name)    Stores the remote Sync pushes to and reloads.
//   Apply()               Copies the chosen templates (asks before replacing), then SaveRemote() when the URL changed.
//   SaveRemote()          Adds origin (Add Remote) or changes the chosen remote's URL (Change Remote).
