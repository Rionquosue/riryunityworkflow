#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Riry.UnityWorkflow.Editor.Tests
{
    public sealed class GuidRepairTests
    {
        private const string First = "0123456789abcdef0123456789abcdef";
        private const string Second = "fedcba9876543210fedcba9876543210";

        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "riry-guid-repair-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            GuidRemapLedger.PathOverride = Path.Combine(root, "RiryGuidRemap.tsv");
        }

        [TearDown]
        public void TearDown()
        {
            GuidRemapLedger.PathOverride = null;
            GitAutosaveTests.DeleteDirectory(root);
        }

        [Test]
        public void OnlyGuidFieldsAreFound()
        {
            string yaml = "  m_Script: {fileID: 11500000, guid: " + First + ", type: 3}\n" +
                "  someguid: " + Second + "\n  guid: " + Second + "0\n  guid: 0123\nguid: " + Second;
            List<KeyValuePair<long, string>> found = GuidReferenceRepair.FindGuidReferences(Encoding.ASCII.GetBytes(yaml));
            Assert.That(found.Count, Is.EqualTo(2));
            Assert.That(found[0].Value, Is.EqualTo(First));
            Assert.That(yaml.Substring((int)found[0].Key, 32), Is.EqualTo(First));
            Assert.That(found[1].Key, Is.EqualTo(yaml.Length - 32));
        }

        [Test]
        public void LedgerFollowsChainsAndStopsOnLoops()
        {
            string a = new string('a', 32), b = new string('b', 32), c = new string('c', 32);
            string d = new string('d', 32), e = new string('e', 32);
            GuidRemapLedger.Append(new[] { Target(a, b, "Assets/x\ty.png") });
            GuidRemapLedger.Append(new[] { Target(b, c, "Assets/x.png"), Target(d, e, "Assets/d"), Target(e, d, "Assets/e") });

            string[] lines = File.ReadAllLines(GuidRemapLedger.PathOverride);
            Assert.That(lines.Length, Is.EqualTo(5));
            StringAssert.StartsWith("#", lines[0]);
            Assert.That(lines[1].Split('\t').Length, Is.EqualTo(4));

            Dictionary<string, string> map = GuidRemapLedger.Load();
            Assert.That(GuidRemapLedger.Resolve(a, map, guid => guid == c), Is.EqualTo(c));
            Assert.That(GuidRemapLedger.Resolve(a, map, guid => guid == b), Is.EqualTo(b));
            Assert.That(GuidRemapLedger.Resolve(a, map, guid => false), Is.Null);
            Assert.That(GuidRemapLedger.Resolve(d, map, guid => false), Is.Null);
        }

        [Test]
        public void HistoryKeepsTheNewestProjectPathForAGuid()
        {
            string repo = Path.Combine(root, "repo");
            string project = Path.Combine(repo, "Game");
            Directory.CreateDirectory(Path.Combine(project, "Assets"));
            Directory.CreateDirectory(Path.Combine(repo, "Other"));
            Git(repo, "init", "-q");
            Git(repo, "config", "user.name", "Tester");
            Git(repo, "config", "user.email", "tester@example.com");
            File.WriteAllText(Path.Combine(project, "Assets", "old.png.meta"), "fileFormatVersion: 2\nguid: " + First + "\n");
            File.WriteAllText(Path.Combine(repo, "Other", "x.meta"), "guid: " + Second + "\n");
            Git(repo, "add", "-A");
            Git(repo, "commit", "-q", "-m", "add");
            Git(repo, "mv", "Game/Assets/old.png.meta", "Game/Assets/new name.png.meta");
            Git(repo, "commit", "-q", "-m", "move");
            Git(repo, "rm", "-q", "Game/Assets/new name.png.meta");
            Git(repo, "commit", "-q", "-m", "delete");
            Directory.CreateDirectory(project);

            var parser = new MetaHistoryParser(new HashSet<string> { First, Second }, Git(project, "rev-parse", "--show-prefix"));
            GitRunner.Result result = GitRunner.Stream(project, GuidReferenceRepair.HistoryArguments, parser.Feed, () => false);
            Assert.That(result.Succeeded, Is.True, result.Describe("git log"));
            Assert.That(parser.Found[First], Is.EqualTo("Assets/new name.png"));
            Assert.That(parser.Found.ContainsKey(Second), Is.False);
        }

        private static RegenerateAssetGuid.Target Target(string oldGuid, string newGuid, string path)
        {
            return new RegenerateAssetGuid.Target { OldGuid = oldGuid, NewGuid = newGuid, AssetPath = path };
        }

        private static string Git(string directory, params string[] arguments)
        {
            GitRunner.Result result = GitRunner.Run(directory, null, 60000, arguments);
            if (result.ExitCode == GitRunner.NotFound) Assert.Ignore("Git is not installed.");
            Assert.That(result.Succeeded, Is.True, result.Describe("git " + string.Join(" ", arguments)));
            return result.Output.Trim();
        }
    }
}
#endif
