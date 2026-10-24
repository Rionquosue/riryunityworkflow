#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor.Tests
{
    public sealed class ImportPresetTests
    {
        private string library;
        private string assetFolder;

        [SetUp]
        public void SetUp()
        {
            library = Path.Combine(Path.GetTempPath(), "riry-import-presets-" + Guid.NewGuid().ToString("N"));
            ImportPresetLibrary.RootOverride = library;
        }

        [TearDown]
        public void TearDown()
        {
            ImportPresetLibrary.RootOverride = null;
            if (assetFolder != null) AssetDatabase.DeleteAsset(assetFolder);
            if (Directory.Exists(library)) Directory.Delete(library, true);
        }

        [Test]
        public void GeneratedMarkerRoundTripsAndSurvivesLineEndings()
        {
            string marker = ImportPresetMarker.Generate("Textures");
            Assert.That(ImportPresetMarker.Parse(marker), Is.EqualTo("Textures"));
            Assert.That(ImportPresetMarker.IsGenerated(marker), Is.True);
            // Git may check the file out with CRLF line endings.
            Assert.That(ImportPresetMarker.IsGenerated(marker.Replace("\n", "\r\n")), Is.True);
        }

        [Test]
        public void HandEditedMarkerIsNotTreatedAsGenerated()
        {
            string marker = ImportPresetMarker.Generate("Textures");
            Assert.That(ImportPresetMarker.IsGenerated(marker + "My own note.\n"), Is.False);
            Assert.That(ImportPresetMarker.IsGenerated(marker.Replace("Textures", "Models")), Is.True,
                "Only the tool's own text counts, whatever name it holds.");
            Assert.That(ImportPresetMarker.IsGenerated("preset: Textures"), Is.False);
        }

        [Test]
        public void HandwrittenPresetLinesAreParsed()
        {
            Assert.That(ImportPresetMarker.Parse("Notes\npreset: `Textures`\n"), Is.EqualTo("Textures"));
            Assert.That(ImportPresetMarker.Parse("  Preset :  My Set  \r\n"), Is.EqualTo("My Set"));
            Assert.That(ImportPresetMarker.Parse("**preset**: Textures"), Is.Null);
            Assert.That(ImportPresetMarker.Parse("preset:   \n"), Is.Null);
            Assert.That(ImportPresetMarker.Parse(""), Is.Null);
        }

        [Test]
        public void NearestMarkerWins()
        {
            var markers = new HashSet<string> { "Assets/A/ImportPreset.md", "Assets/A/B/C/ImportPreset.md" };
            Func<string, bool> exists = markers.Contains;
            Assert.That(ImportPresetMarker.FindMarkerFolder("Assets/A/B/x.png", exists), Is.EqualTo("Assets/A"));
            Assert.That(ImportPresetMarker.FindMarkerFolder("Assets/A/B/C/D/y.png", exists), Is.EqualTo("Assets/A/B/C"));
            Assert.That(ImportPresetMarker.FindMarkerFolder("Assets/A/B/C/z.png", exists), Is.EqualTo("Assets/A/B/C"));
            Assert.That(ImportPresetMarker.FindMarkerFolder("Assets/Z/z.png", exists), Is.Null);
        }

        [Test]
        public void UnsafeNamesAreRejected()
        {
            Assert.That(ImportPresetLibrary.ValidateName("Textures"), Is.Null);
            Assert.That(ImportPresetLibrary.ValidateName("Avatar Textures v2"), Is.Null);
            foreach (string bad in new[] { "", " ", "..", ".", "../Escape", "a/b", "a\\b", "a:b", " lead",
                "trail ", "end.", "CON", "nul.preset", "com1" })
                Assert.That(ImportPresetLibrary.ValidateName(bad), Is.Not.Null, "'" + bad + "'");
            Assert.That(ImportPresetLibrary.ConfigurationPath("../Escape"), Is.Null);
        }

        [Test]
        public void RulesComeFirstThenUnreferencedPresets()
        {
            var config = new ImportPresetLibrary.Config();
            config.rules.Add(new ImportPresetLibrary.Rule
                { extensions = new List<string> { ".PNG", "tga" }, preset = "Textures" });
            config.rules.Add(new ImportPresetLibrary.Rule
                { extensions = new List<string> { "fbx" }, preset = "Models.preset" });
            config.rules.Add(new ImportPresetLibrary.Rule
                { extensions = new List<string> { "png" }, preset = "Missing.preset" });
            config.rules.Add(new ImportPresetLibrary.Rule
                { extensions = new List<string> { "*" }, preset = "Everything.preset" });
            var files = new List<string> { "Audio.preset", "Everything.preset", "Models.preset", "Textures.preset" };

            List<ImportPresetLibrary.Candidate> png = ImportPresetLibrary.OrderedPresets(".png", config, files);
            Assert.That(png.ConvertAll(c => c.File),
                Is.EqualTo(new[] { "Textures.preset", "Everything.preset", "Audio.preset" }));
            Assert.That(png[0].FromRule, Is.True);
            Assert.That(png[2].FromRule, Is.False);

            List<ImportPresetLibrary.Candidate> wav = ImportPresetLibrary.OrderedPresets("wav", config, files);
            Assert.That(wav.ConvertAll(c => c.File), Is.EqualTo(new[] { "Everything.preset", "Audio.preset" }));
        }

        [Test]
        public void RulesCannotPointOutsideTheConfiguration()
        {
            var config = new ImportPresetLibrary.Config();
            config.rules.Add(new ImportPresetLibrary.Rule
                { extensions = new List<string> { "png" }, preset = "../Other/Textures.preset" });
            var files = new List<string> { "Textures.preset" };
            List<ImportPresetLibrary.Candidate> png = ImportPresetLibrary.OrderedPresets("png", config, files);
            Assert.That(png.Count, Is.EqualTo(1));
            Assert.That(png[0].FromRule, Is.False);
        }

        [Test]
        public void ConfigurationNamesMatchWithoutCase()
        {
            Directory.CreateDirectory(Path.Combine(library, "Textures"));
            string path = ImportPresetLibrary.ConfigurationPath("textures");
            Assert.That(Directory.Exists(path), Is.True, "A marker written on Windows also resolves on Linux.");
        }

        [Test]
        public void BrokenConfigFileFallsBackToTypeMatching()
        {
            string configuration = Path.Combine(library, "Broken");
            Directory.CreateDirectory(configuration);
            File.WriteAllText(Path.Combine(configuration, ImportPresetLibrary.ConfigFileName), "{ not json");
            string error;
            ImportPresetLibrary.Config config = ImportPresetLibrary.ReadConfig(configuration, out error);
            Assert.That(error, Is.Not.Null);
            Assert.That(config.rules, Is.Empty);
        }

        [Test]
        public void LibraryListsOnlySafeConfigurationFolders()
        {
            Directory.CreateDirectory(Path.Combine(library, "Textures"));
            Directory.CreateDirectory(Path.Combine(library, "Models"));
            // Windows strips a trailing period, so use a leading space: legal on disk, rejected here.
            Directory.CreateDirectory(Path.Combine(library, " lead"));
            ImportPresetLibrary.EnsureRoot();
            Assert.That(File.Exists(Path.Combine(library, "README.txt")), Is.True);
            Assert.That(ImportPresetLibrary.ConfigurationNames(), Is.EqualTo(new[] { "Models", "Textures" }));
        }

        [Test]
        public void NewFilesGetThePresetAndManualChangesSurviveReimport()
        {
            assetFolder = "Assets/__RiryImportPresetTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(assetFolder);
            AssetDatabase.Refresh();

            // Build a preset from a configured texture, then remove that texture.
            string source = WritePng(assetFolder + "/Source.png");
            var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(source);
            sourceImporter.isReadable = true;
            sourceImporter.maxTextureSize = 64;
            sourceImporter.SaveAndReimport();
            ImportPresetLibrary.SavePreset(sourceImporter, "Test", "Texture png", true);
            AssetDatabase.DeleteAsset(source);

            string configuration = ImportPresetLibrary.ConfigurationPath("Test");
            string error;
            Assert.That(ImportPresetLibrary.ReadConfig(configuration, out error).rules.Count, Is.EqualTo(1));

            File.WriteAllText(ImportPresetMarker.PathIn(assetFolder), ImportPresetMarker.Generate("Test"));
            AssetDatabase.ImportAsset(ImportPresetMarker.PathIn(assetFolder));
            Directory.CreateDirectory(assetFolder + "/Sub");
            AssetDatabase.Refresh();

            string fresh = WritePng(assetFolder + "/Sub/Fresh.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(fresh);
            Assert.That(importer.isReadable, Is.True, "The preset applies to a new file in a subfolder.");
            Assert.That(importer.maxTextureSize, Is.EqualTo(64));

            importer.maxTextureSize = 128;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(fresh, ImportAssetOptions.ForceUpdate);
            importer = (TextureImporter)AssetImporter.GetAtPath(fresh);
            Assert.That(importer.maxTextureSize, Is.EqualTo(128), "A reimport keeps manual changes.");
        }

        private static string WritePng(string path)
        {
            var texture = new Texture2D(4, 4);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }
    }
}
#endif
