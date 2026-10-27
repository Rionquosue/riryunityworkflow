#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace Riry.RiryTools.Editor.Tests
{
    public sealed class RiryToolsProjectFolderTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "riry-project-folder-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void UnpackCopiesMissingFilesAndKeepsEditedOnes()
        {
            string source = Path.Combine(root, "ProjectFolder~");
            string target = Path.Combine(root, "RiryTools");
            Directory.CreateDirectory(Path.Combine(source, "Shaders"));
            File.WriteAllText(Path.Combine(source, "README.md"), "package");
            File.WriteAllText(Path.Combine(source, "Shaders", "Toon.shader"), "shader");
            File.WriteAllText(Path.Combine(source, "Shaders", "Toon.shader.meta"), "guid: 1");

            Assert.That(RiryToolsProjectFolder.Unpack(source, target), Is.EqualTo(3));
            Assert.That(File.ReadAllText(Path.Combine(target, "Shaders", "Toon.shader.meta")), Is.EqualTo("guid: 1"));

            File.WriteAllText(Path.Combine(target, "README.md"), "edited");
            File.Delete(Path.Combine(target, "Shaders", "Toon.shader"));
            Assert.That(RiryToolsProjectFolder.Unpack(source, target), Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(target, "README.md")), Is.EqualTo("edited"));
            Assert.That(File.ReadAllText(Path.Combine(target, "Shaders", "Toon.shader")), Is.EqualTo("shader"));
            Assert.That(RiryToolsProjectFolder.Unpack(Path.Combine(root, "missing"), target), Is.EqualTo(0));
        }
    }
}
#endif
