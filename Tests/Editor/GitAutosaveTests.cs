#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor.Tests
{
    public sealed class GitAutosaveTests
    {
        private string root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) DeleteDirectory(root);
            root = null;
        }

        [Test]
        public void BranchNamesFollowGitRules()
        {
            foreach (string valid in new[] { "main", "autosave/riry", "feature/a.b", "it's" })
                Assert.That(GitAutosave.BranchNameError(valid, "Branch"), Is.Null, valid);
            foreach (string invalid in new[] { "", "@", "-x", "a..b", "a b", "a~1", "a^", "a:b", "a*", "a[1]", "a\\b",
                         "/a", "a/", "a//b", ".a", "a/.b", "a.", "x.lock", "x.lock/y", "a@{1}" })
                Assert.That(GitAutosave.BranchNameError(invalid, "Branch"), Is.Not.Null, invalid);
        }

        [Test]
        public void WorkflowTemplateTakesBaseAndAutosaveBranches()
        {
            Assert.That(GitAutosave.WorkflowPattern("backup/riry"), Is.EqualTo("backup/**"));
            Assert.That(GitAutosave.WorkflowPattern("AutoSync"), Is.EqualTo("AutoSync"));

            // The template is found through its .meta GUID, as the installer does.
            var template = AssetDatabase.LoadAssetAtPath<TextAsset>(
                AssetDatabase.GUIDToAssetPath("5a0f1f0c6b7e4f2d9c3a8e1b7d4c6f20"));
            Assert.That(template, Is.Not.Null);
            string text = GitAutosave.WorkflowText(template.text.Replace("\r\n", "\n"), "develop", "saves/it's");
            StringAssert.Contains("      - 'develop'\n", text);
            StringAssert.Contains("      - 'saves/**'\n", text);
            StringAssert.Contains("  BASE_BRANCH: 'develop'\n", text);
            StringAssert.Contains("  AUTOSAVE_BRANCHES: 'saves/**'\n", text);
            StringAssert.DoesNotContain("'AutoSync'", text);
            StringAssert.DoesNotContain("      - main\n", text);
        }

        [Test]
        public void IndexInfoReplacesChangedPathsAndRemovesDeletedOnes()
        {
            string a = new string('a', 40), b = new string('b', 40), zero = new string('0', 40);
            string diff = ":100644 100644 " + a + " " + b + " M\0x y.meta\0:100644 000000 " + a + " " + zero + " D\0gone\0";
            Assert.That(Encoding.UTF8.GetString(GitAutosaveJob.IndexInfo(diff)),
                Is.EqualTo("100644 " + b + "\tx y.meta\0" + "0 " + zero + "\tgone\0"));
        }

        [Test]
        public void SnapshotRecordsOnlyScenesPrefabsAndMetasWithoutTouchingTheCheckout()
        {
            string repo = NewRepository();
            string project = repo;
            Write(project, "Assets/a.prefab.meta", "guid: aaaa-1\n");
            Write(project, "Assets/we*ird [1].prefab", "w\n");
            File.Delete(Path.Combine(project, "Assets/s.unity"));
            Write(project, "Assets/code.cs", "class B {}\n");
            Write(project, "Assets/Ignored/x.prefab", "ignored\n");
            Git(repo, "add", "Assets/code.cs");
            GitAutosaveJob job = NewJob(project, false);

            StringAssert.StartsWith("Saved snapshot", job.Execute());
            string tip = Git(repo, "rev-parse", "refs/heads/autosave/test");
            Assert.That(Git(repo, "show", tip + ":Assets/a.prefab.meta"), Is.EqualTo("guid: aaaa-1"));
            Assert.That(Git(repo, "show", tip + ":Assets/we*ird [1].prefab"), Is.EqualTo("w"));
            Assert.That(Git(repo, "show", tip + ":Assets/code.cs"), Is.EqualTo("class A {}"));
            Assert.That(Exists(repo, tip + ":Assets/s.unity"), Is.False);
            Assert.That(Exists(repo, tip + ":Assets/Ignored/x.prefab"), Is.False);
            Assert.That(Git(repo, "symbolic-ref", "HEAD"), Is.EqualTo("refs/heads/main"));
            Assert.That(Git(repo, "diff", "--cached", "--name-only"), Is.EqualTo("Assets/code.cs"));

            StringAssert.StartsWith("No changes", job.Execute());
            Assert.That(Git(repo, "rev-parse", "refs/heads/autosave/test"), Is.EqualTo(tip));
        }

        [Test]
        public void PushReplaysLocalSnapshotsOntoARebasedRemoteBranch()
        {
            string repo = NewRepository();
            string project = repo;
            string remote = Path.Combine(root, "remote.git");
            Git(root, "init", "-q", "--bare", remote);
            Git(repo, "remote", "add", "origin", remote);
            Git(repo, "push", "-q", "origin", "main");
            GitAutosaveJob job = NewJob(project, true);
            Write(project, "Assets/a.prefab.meta", "guid: aaaa-2\n");
            StringAssert.Contains("Pushed", job.Execute());

            // Main moves, and the rebase workflow rewrites the remote autosave branch.
            string other = Path.Combine(root, "other");
            Git(root, "clone", "-q", remote, other);
            Git(other, "config", "user.name", "Other");
            Git(other, "config", "user.email", "other@example.com");
            Write(other, "Assets/a.prefab.meta", "guid: aaaa-MAIN\n");
            Write(other, "Assets/b.prefab.meta", "guid: bbbb-MAIN\n");
            Git(other, "commit", "-q", "-am", "main moves");
            Git(other, "push", "-q", "origin", "main");
            string main = Git(other, "rev-parse", "HEAD");
            Git(repo, "fetch", "-q", "origin");
            string pushed = Git(repo, "rev-parse", "refs/remotes/origin/autosave/test");
            string rewritten = job.Replay(Git(repo, "merge-base", pushed, main), pushed, main);
            Assert.That(Git(repo, "show", rewritten + ":Assets/a.prefab.meta"), Is.EqualTo("guid: aaaa-2"));
            Assert.That(Git(repo, "show", rewritten + ":Assets/b.prefab.meta"), Is.EqualTo("guid: bbbb-MAIN"));
            Assert.That(Git(repo, "log", "-1", "--format=%an", rewritten), Is.EqualTo("Tester"));
            Git(repo, "push", "-q", "--force", "origin", rewritten + ":refs/heads/autosave/test");

            Write(project, "Assets/a.prefab.meta", "guid: aaaa-3\n");
            StringAssert.Contains("Pushed", job.Execute());
            string tip = Git(remote, "rev-parse", "refs/heads/autosave/test");
            Assert.That(Git(remote, "show", tip + ":Assets/a.prefab.meta"), Is.EqualTo("guid: aaaa-3"));
            Assert.That(GitRunner.Run(remote, null, 60000, "merge-base", "--is-ancestor", main, tip).Succeeded, Is.True);
        }

        [Test]
        public void CheckedOutAutosaveBranchIsRefused()
        {
            string repo = NewRepository();
            Git(repo, "checkout", "-q", "-b", "autosave/test");
            var exception = Assert.Throws<InvalidOperationException>(() => NewJob(repo, false).Execute());
            StringAssert.Contains("checked out", exception.Message);
        }

        [Test]
        public void ProjectInsideAnotherRepositoryIsRefused()
        {
            string repo = NewRepository();
            string nested = Path.Combine(repo, "Nested");
            Write(nested, "Assets/n.prefab", "n\n");
            var exception = Assert.Throws<InvalidOperationException>(() => NewJob(nested, false).Execute());
            StringAssert.Contains("must be the project folder itself", exception.Message);
            Assert.That(GitState.Location(nested)[1], Is.EqualTo("Nested/"));
            Assert.That(GitState.Location(repo)[1], Is.Empty);
        }

        [Test]
        public void LfsCheckFindsUntrackedBinariesAndUploadsAProbe()
        {
            string repo = NewRepository();
            string project = repo;
            Git(root, "init", "-q", "--bare", Path.Combine(root, "remote.git"));
            Git(repo, "remote", "add", "origin", Path.Combine(root, "remote.git"));
            if (!GitLfs.Installed(repo)) Assert.Ignore("Git LFS is not installed.");
            GitLfs.Enable(repo);
            File.WriteAllText(Path.Combine(project, ".gitattributes"), GitTemplates.LfsLine("fbx"));
            var data = new byte[(int)GitLfs.LargeFileBytes + 1];
            new System.Random(1).NextBytes(data);
            File.WriteAllBytes(Path.Combine(project, "Assets", "a.fbx"), data);
            File.WriteAllBytes(Path.Combine(project, "Assets", "b c.psd"), data);

            GitLfs.Report report = GitLfs.Check(project, "origin");
            Assert.That(report.NotTracked, Is.EqualTo(new[] { "Assets/b c.psd" }));
            Assert.That(report.UploadWorks, Is.True, report.Upload);

            GitLfs.Track(Path.Combine(project, ".gitattributes"), report.NotTrackedExtensions);
            GitLfs.Track(Path.Combine(project, ".gitattributes"), report.NotTrackedExtensions);
            Assert.That(File.ReadAllText(Path.Combine(project, ".gitattributes")).Split(new[] { "*.psd" }, StringSplitOptions.None).Length,
                Is.EqualTo(2));
            Assert.That(GitLfs.Check(project, null).NotTracked, Is.Empty);
        }

        private string NewRepository()
        {
            root = Path.Combine(Path.GetTempPath(), "riry-autosave-" + Guid.NewGuid().ToString("N"));
            string repo = Path.Combine(root, "repo with space");
            Directory.CreateDirectory(repo);
            Git(repo, "init", "-q");
            Git(repo, "symbolic-ref", "HEAD", "refs/heads/main");
            Git(repo, "config", "user.name", "Tester");
            Git(repo, "config", "user.email", "tester@example.com");
            Write(repo, ".gitignore", "Library/\nAssets/Ignored/\n");
            Write(repo, "Assets/a.prefab.meta", "guid: aaaa\n");
            Write(repo, "Assets/b.prefab.meta", "guid: bbbb\n");
            Write(repo, "Assets/s.unity", "scene\n");
            Write(repo, "Assets/code.cs", "class A {}\n");
            Git(repo, "add", "-A");
            Git(repo, "commit", "-q", "-m", "base");
            return repo;
        }

        private static GitAutosaveJob NewJob(string project, bool push)
        {
            return new GitAutosaveJob
            {
                ProjectRoot = project,
                StateFolder = Path.Combine(project, "Library", "RiryGitAutosave"),
                Branch = "autosave/test",
                Remote = "origin",
                BaseBranch = "main",
                Push = push
            };
        }

        private static string Git(string directory, params string[] arguments)
        {
            GitRunner.Result result;
            try
            {
                result = GitRunner.Run(directory, null, 60000, arguments);
            }
            catch (GitNotInstalledException)
            {
                Assert.Ignore("Git is not installed.");
                throw;
            }
            Assert.That(result.Succeeded, Is.True, result.Describe("git " + string.Join(" ", arguments)));
            return result.Output.Trim();
        }

        private static bool Exists(string repo, string objectName)
        {
            return GitRunner.Run(repo, null, 60000, "cat-file", "-e", objectName).Succeeded;
        }

        private static void Write(string directory, string relativePath, string text)
        {
            string path = Path.Combine(directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        internal static void DeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            // Git marks its objects read-only, which blocks deletion on Windows.
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, true);
        }
    }
}
#endif
