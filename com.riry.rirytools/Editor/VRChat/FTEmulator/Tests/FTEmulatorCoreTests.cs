using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>A source the tests fill by hand.</summary>
    internal sealed class FTEmulatorTestSource : IFTEmulatorSource
    {
        internal FTEmulatorExpressionFrame Expressions;
        internal FTEmulatorParameterFrame Parameters;
        internal int Starts, Stops, Clears;
        private long sequence;

        public string Name { get { return "Test"; } }
        public bool Running { get; private set; }

        public void Start(FTEmulatorSessionContext context)
        {
            if (!context.Active()) throw new InvalidOperationException("not active");
            Running = true;
            Starts++;
        }

        public bool TryRead(out FTEmulatorExpressionFrame expressions, out FTEmulatorParameterFrame parameters)
        {
            expressions = Expressions;
            parameters = Parameters;
            Expressions = null;
            Parameters = null;
            return expressions != null || parameters != null;
        }

        internal void Push(FTEmulatorCatalog catalog, int generation, params (string key, float value)[] keys)
        {
            var values = new float[catalog.Keys.Length];
            var valid = new bool[catalog.Keys.Length];
            for (int i = 0; i < valid.Length; i++) valid[i] = true;
            foreach ((string key, float value) in keys) values[catalog.Key(key)] = value;
            Expressions = FTEmulatorExpressionFrame.Create(values, valid, ++sequence, generation, FTEmulatorClock.Now);
        }

        internal void PushParameter(int generation, string name, FTEmulatorValueType type, float value)
        {
            Parameters = new FTEmulatorParameterFrame(new List<FTEmulatorParameterValue> { new FTEmulatorParameterValue(name, type, value) }, ++sequence, generation, FTEmulatorClock.Now);
        }

        public void Clear() { Expressions = null; Parameters = null; Clears++; }
        public void Stop() { Running = false; Stops++; }
        public void Dispose() { Stop(); }
    }

    /// <summary>An output that keeps the values written to it.</summary>
    internal sealed class FTEmulatorTestOutput : IFTEmulatorOutput
    {
        internal readonly Dictionary<string, float> Values = new Dictionary<string, float>(StringComparer.Ordinal);
        internal readonly List<FTEmulatorAvatarParameter> List = new List<FTEmulatorAvatarParameter>();
        internal bool IsValid = true;
        internal bool Released;

        internal void Add(string name, FTEmulatorValueType type, float value = 0f)
        {
            Values[name] = value;
            List.Add(new FTEmulatorAvatarParameter { Name = name, Type = type });
        }

        public string Name { get { return "Test output"; } }
        public bool Bind(UnityEngine.Object manager, out string problem) { problem = null; return true; }
        public bool Valid { get { return IsValid; } }
        public GameObject Avatar { get { return null; } }
        public IReadOnlyList<FTEmulatorAvatarParameter> Parameters { get { return List; } }

        public bool Write(string name, float value)
        {
            if (!Values.ContainsKey(name)) return false;
            Values[name] = value;
            return true;
        }

        public bool TryRead(string name, out float value) { return Values.TryGetValue(name, out value); }
        public void Release() { Released = true; }
    }

    internal sealed class FTEmulatorTestMesh : IFTEmulatorMeshOutput
    {
        internal string[] Names = new string[0];
        internal float[] Weights = new float[0];
        internal int Applies, Restores;

        public List<string> Bind(string[] blendshapes) { Names = blendshapes; return new List<string>(); }
        public void Apply(float[] weights, bool[] owned) { Weights = (float[])weights.Clone(); Applies++; }
        public void Restore() { Restores++; }
    }

    /// <summary>
    /// FTEmulator's policy, mapping and hardware limits, with fixed data and stand-ins: no headset, tracking app, avatar
    /// or Play Mode is needed. The profile data is the package's own, read from the folders beside this folder.
    /// </summary>
    internal sealed class FTEmulatorCoreTests
    {
        private static FTEmulatorCatalog catalog;

        private static string Here([CallerFilePath] string path = "") { return Path.GetDirectoryName(path); }

        internal static FTEmulatorCatalog Catalog
        {
            get
            {
                if (catalog != null) return catalog;
                string root = Path.GetDirectoryName(Here());
                // A package Unity keeps outside the project has no folder at the path it was compiled with: ask Unity.
                if (!Directory.Exists(Path.Combine(root, "MappingSchemas"))) return catalog = FTEmulatorCatalog.LoadFromPackage();
                return catalog = FTEmulatorCatalog.Load(Path.Combine(root, "MappingSchemas"), Path.Combine(root, "HardwareProfiles"), Path.Combine(root, "AvatarProfiles"));
            }
        }

        private static FTEmulatorExpressionFrame Frame(float fill, params (string key, float value)[] keys)
        {
            var values = new float[Catalog.Keys.Length];
            var valid = new bool[Catalog.Keys.Length];
            for (int i = 0; i < values.Length; i++) { values[i] = fill; valid[i] = true; }
            foreach ((string key, float value) in keys) values[Catalog.Key(key)] = value;
            return FTEmulatorExpressionFrame.Create(values, valid, 1, 1, 0);
        }

        // Input through a profile: Project, every available control driven, Expand, Validate.
        private static float[] Through(string hardware, FTEmulatorBehavior behavior, FTEmulatorExpressionFrame frame, out bool[] driven, out FTEmulatorHardwareFilter filter)
        {
            FTEmulatorHardwareProfile profile = Catalog.GetHardware(hardware);
            filter = new FTEmulatorHardwareFilter(Catalog, profile, behavior);
            var controls = new float[profile.Controls.Length];
            var available = new bool[profile.Controls.Length];
            filter.Project(frame, controls, available);
            var values = new float[Catalog.Keys.Length];
            driven = new bool[Catalog.Keys.Length];
            filter.Expand(controls, available, values, driven);
            filter.Validate(values, driven);
            return values;
        }

        private static float Key(float[] values, string key) { return values[Catalog.Key(key)]; }

        // ---- Catalog ----

        [Test]
        public void Catalog_ReadsEveryProfile()
        {
            Assert.IsEmpty(Catalog.Problems);
            Assert.IsTrue(Catalog.Usable);
            Assert.AreEqual(102, Catalog.Keys.Length);
            Assert.AreEqual(36, Catalog.Key("JawOpen"));
            Assert.AreEqual(0, Catalog.Key("EyeLookOutRight"));
            Assert.AreEqual(101, Catalog.Key("NeckFlexLeft"));
            foreach (string id in new[] { "unrestricted", "quest-pro-base-fb1", "quest-pro-official-fb2", "babble-lower-v2.1.2", "vive-pro-stock",
                "vive-pro-eye", "vive-facial-tracker", "vive-pro-eye-plus-facial" })
                Assert.IsNotNull(Catalog.GetHardware(id), id);
            Assert.AreEqual(63, Catalog.GetHardware("quest-pro-base-fb1").Controls.Length);
            Assert.AreEqual(64, Catalog.GetHardware("quest-pro-official-fb2").Controls.Length);
            Assert.AreEqual(45, Catalog.GetHardware("babble-lower-v2.1.2").Controls.Length);
            Assert.AreEqual(0, Catalog.GetHardware("vive-pro-stock").Controls.Length);
            Assert.AreEqual(102, Catalog.GetHardware("unrestricted").Controls.Length);
            foreach (string id in new[] { "vrcft-unified-v2", "vrchat-native", "pawlygon-native", "pawlygon-legacy", "adjerry-legacy" })
                Assert.IsNotNull(Catalog.GetAvatarProfile(id), id);
        }

        private static List<KeyValuePair<string, string>> File(string name, string json)
        {
            return new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(name, json) };
        }

        private const string Schema = "{\"schema\":1,\"id\":\"s\",\"evidence\":[\"e\"],\"blobLength\":2,\"keys\":[{\"name\":\"A\",\"region\":\"eye\"},{\"name\":\"B\"}]}";

        [TestCase("{\"schema\":2,\"id\":\"x\",\"name\":\"X\",\"evidence\":[\"e\"]}", "schema")]
        [TestCase("{\"schema\":1,\"id\":\"x\",\"name\":\"X\",\"evidence\":[]}", "evidence")]
        [TestCase("{\"schema\":1,\"id\":\"x\",\"name\":\"X\",\"evidence\":[\"e\"],\"controls\":[{\"name\":\"c\",\"from\":\"Nope\"}]}", "unknown name")]
        [TestCase("{\"schema\":1,\"id\":\"x\",\"name\":\"X\",\"evidence\":[\"e\"],\"controls\":[{\"name\":\"c\",\"from\":\"A\"},{\"name\":\"d\",\"from\":\"c\"}]}", "unknown name")]
        [TestCase("{\"schema\":1,\"id\":\"x\",\"name\":\"X\",\"evidence\":[\"e\"],\"controls\":[{\"name\":\"c\"}],\"outputs\":[{\"key\":\"A\",\"value\":\"c\"},{\"key\":\"A\",\"value\":\"c\"}]}", "two outputs")]
        [TestCase("{\"schema\":1,\"id\":\"x\",\"name\":\"X\",\"evidence\":[\"e\"],\"controls\":[{\"name\":\"c\",\"min\":1,\"max\":0}]}", "range")]
        [TestCase("not json", "JSON")]
        public void Catalog_RejectsBadHardwareProfile(string json, string expected)
        {
            FTEmulatorCatalog parsed = FTEmulatorCatalog.Parse(File("s.json", Schema), File("x.json", json), new List<KeyValuePair<string, string>>());
            Assert.AreEqual(0, parsed.Hardware.Count);
            Assert.AreEqual(1, parsed.Problems.Count);
            StringAssert.Contains(expected, parsed.Problems[0]);
        }

        [Test]
        public void Catalog_RejectsDuplicateIdsAndKeepsEarlierCatalog()
        {
            string profile = "{\"schema\":1,\"id\":\"x\",\"name\":\"X\",\"evidence\":[\"e\"],\"controls\":[{\"name\":\"c\",\"from\":\"A\"}],\"outputs\":[{\"key\":\"A\",\"value\":\"c\"}]}";
            var two = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("a.json", profile), new KeyValuePair<string, string>("b.json", profile) };
            FTEmulatorCatalog first = FTEmulatorCatalog.Parse(File("s.json", Schema), two, new List<KeyValuePair<string, string>>());
            Assert.AreEqual(1, first.Hardware.Count);
            StringAssert.Contains("used by another", first.Problems[0]);
            // Reading again makes a new catalog: the one a session holds is not touched.
            FTEmulatorCatalog second = FTEmulatorCatalog.Parse(File("s.json", Schema), new List<KeyValuePair<string, string>>(), new List<KeyValuePair<string, string>>());
            Assert.AreEqual(1, first.Hardware.Count);
            Assert.AreEqual(0, second.Hardware.Count);
        }

        // ---- Expressions ----

        [TestCase("1 + 2 * 3", 7f)]
        [TestCase("(1 + 2) * 3", 9f)]
        [TestCase("-(1 - 3)", 2f)]
        [TestCase("--2", 2f)]
        [TestCase("2 - -1", 3f)]
        [TestCase("clamp(5, 0, 1)", 1f)]
        [TestCase("min(2, 3) + max(2, 3)", 5f)]
        [TestCase("abs(-2.5)", 2.5f)]
        [TestCase(".5 * 4", 2f)]
        [TestCase("1 / 0", 0f)]
        [TestCase("JawRight - JawLeft", -1f)]
        public void Expression_Evaluates(string text, float expected)
        {
            FTEmulatorExpression expression = FTEmulatorExpression.Compile(text, Catalog.Key, out string error);
            Assert.IsNotNull(expression, error);
            var values = new float[Catalog.Keys.Length];
            values[Catalog.Key("JawLeft")] = 1f;
            Assert.AreEqual(expected, expression.Evaluate(values), 1e-5f);
        }

        [TestCase("")]
        [TestCase("1 +")]
        [TestCase("1 2")]
        [TestCase("(1")]
        [TestCase("1)")]
        [TestCase("foo(1)")]
        [TestCase("min(1)")]
        [TestCase("clamp(1, 2)")]
        [TestCase("jawopen")]
        [TestCase("1 $ 2")]
        [TestCase("* 2")]
        [TestCase("20000")]
        public void Expression_RejectsMalformed(string text)
        {
            Assert.IsNull(FTEmulatorExpression.Compile(text, Catalog.Key, out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void Expression_HoldsToLimits()
        {
            Assert.IsNull(FTEmulatorExpression.Compile(new string('1', 1) + string.Concat(System.Linq.Enumerable.Repeat(" + 1", 300)), Catalog.Key, out string length));
            StringAssert.Contains("characters", length);
            Assert.IsNull(FTEmulatorExpression.Compile(string.Concat(System.Linq.Enumerable.Repeat("1+", 140)) + "1", Catalog.Key, out string tokens));
            StringAssert.Contains("tokens", tokens);
            Assert.IsNull(FTEmulatorExpression.Compile(new string('(', 33) + "1" + new string(')', 33), Catalog.Key, out string nesting));
            StringAssert.Contains("nests", nesting);
            Assert.IsNotNull(FTEmulatorExpression.Compile(new string('(', 32) + "1" + new string(')', 32), Catalog.Key, out string _));
            FTEmulatorExpression used = FTEmulatorExpression.Compile("JawOpen + JawOpen * JawLeft", Catalog.Key, out string _);
            CollectionAssert.AreEqual(new[] { Catalog.Key("JawOpen"), Catalog.Key("JawLeft") }, used.Dependencies);
        }

        // ---- Frames ----

        [Test]
        public void Frame_RejectsNonFiniteAndCopies()
        {
            var values = new float[] { 0f, float.NaN };
            Assert.IsNull(FTEmulatorExpressionFrame.Create(values, new bool[2], 1, 1, 0));
            values[1] = float.PositiveInfinity;
            Assert.IsNull(FTEmulatorExpressionFrame.Create(values, new bool[2], 1, 1, 0));
            Assert.IsNull(FTEmulatorExpressionFrame.Create(new float[2], new bool[3], 1, 1, 0));
            values[1] = -0.5f;
            FTEmulatorExpressionFrame frame = FTEmulatorExpressionFrame.Create(values, new bool[2], 1, 1, 0);
            values[1] = 9f;
            Assert.AreEqual(-0.5f, frame.Values[1]);
        }

        // ---- Hardware limits ----

        [TestCase("unrestricted")]
        [TestCase("quest-pro-base-fb1")]
        [TestCase("quest-pro-official-fb2")]
        [TestCase("babble-lower-v2.1.2")]
        [TestCase("vive-pro-stock")]
        [TestCase("vive-pro-eye")]
        [TestCase("vive-facial-tracker")]
        [TestCase("vive-pro-eye-plus-facial")]
        public void Hardware_NoProhibitedKeyReachesOutput(string id)
        {
            foreach (FTEmulatorBehavior behavior in new[] { FTEmulatorBehavior.Strict, FTEmulatorBehavior.ModuleBehavior })
            {
                float[] values = Through(id, behavior, Frame(1f), out bool[] driven, out FTEmulatorHardwareFilter filter);
                for (int key = 0; key < values.Length; key++)
                {
                    if (!filter.Allowed[key]) Assert.AreEqual(0f, values[key], Catalog.Keys[key] + " under " + id);
                    Assert.That(values[key], Is.InRange(0f, 1f));
                }
                // The last check also clears a value put there after the controls (an output override).
                values[0] = 1f;
                for (int key = 0; key < values.Length; key++) values[key] = 1f;
                filter.Validate(values, driven);
                for (int key = 0; key < values.Length; key++)
                {
                    if (!filter.Allowed[key]) Assert.AreEqual(0f, values[key]);
                }
            }
        }

        [Test]
        public void Hardware_QuestPuckerStaysCoupled()
        {
            float[] values = Through("quest-pro-base-fb1", FTEmulatorBehavior.Strict, Frame(0f, ("LipPuckerUpperLeft", 1f)), out bool[] _, out FTEmulatorHardwareFilter _);
            Assert.AreEqual(0.5f, Key(values, "LipPuckerUpperLeft"), 1e-5f);
            Assert.AreEqual(Key(values, "LipPuckerUpperLeft"), Key(values, "LipPuckerLowerLeft"));
            Assert.AreEqual(0f, Key(values, "LipPuckerUpperRight"));
        }

        [Test]
        public void Hardware_QuestBaseHasNoTongueOrPupil_OfficialHasTongueOut()
        {
            FTEmulatorExpressionFrame frame = Frame(0f, ("TongueOut", 1f), ("TongueTwistLeft", 1f), ("EyeDilationLeft", 1f));
            float[] fb1 = Through("quest-pro-base-fb1", FTEmulatorBehavior.Strict, frame, out bool[] _, out FTEmulatorHardwareFilter filter1);
            Assert.AreEqual(0f, Key(fb1, "TongueOut"));
            Assert.AreEqual(0f, Key(fb1, "EyeDilationLeft"));
            StringAssert.Contains("TongueTwistLeft", filter1.BlockedReason(Catalog.Key("TongueTwistLeft")));
            float[] fb2 = Through("quest-pro-official-fb2", FTEmulatorBehavior.Strict, frame, out bool[] _, out FTEmulatorHardwareFilter filter2);
            Assert.AreEqual(1f, Key(fb2, "TongueOut"));
            Assert.AreEqual(0f, Key(fb2, "TongueTwistLeft"));
            Assert.IsFalse(filter2.Allowed[Catalog.Key("TongueTwistLeft")]);
            // Brow pinch is only derived from the brow lowerer.
            Assert.IsFalse(filter1.Allowed[Catalog.Key("BrowPinchLeft")]);
            float[] module = Through("quest-pro-base-fb1", FTEmulatorBehavior.ModuleBehavior, Frame(0f, ("BrowLowererLeft", 0.8f)), out bool[] _, out FTEmulatorHardwareFilter derived);
            Assert.AreEqual(0.8f, Key(module, "BrowPinchLeft"), 1e-5f);
            Assert.IsTrue(derived.IsDerived(Catalog.Key("BrowPinchLeft")));
        }

        [Test]
        public void Hardware_BabbleHasNoEyes_FunnelDrivesFourQuadrants()
        {
            FTEmulatorHardwareProfile babble = Catalog.GetHardware("babble-lower-v2.1.2");
            Assert.IsFalse(babble.EyeTracking);
            float[] values = Through(babble.Id, FTEmulatorBehavior.Strict, Frame(0f, ("EyeClosedLeft", 1f), ("JawOpen", 0.4f)), out bool[] driven, out FTEmulatorHardwareFilter filter);
            Assert.AreEqual(0f, Key(values, "EyeClosedLeft"));
            // No eye tracking at all: the eye keys are not driven, not even as zeros.
            Assert.IsFalse(driven[Catalog.Key("EyeClosedLeft")]);
            Assert.AreEqual(0.4f, Key(values, "JawOpen"), 1e-5f);

            // A manual mouthFunnel: the four quadrants move together.
            var controls = new float[babble.Controls.Length];
            var drivenControls = new bool[babble.Controls.Length];
            controls[babble.ControlIndex["mouthFunnel"]] = 0.7f;
            drivenControls[babble.ControlIndex["mouthFunnel"]] = true;
            filter.Expand(controls, drivenControls, values, driven);
            foreach (string key in new[] { "LipFunnelUpperLeft", "LipFunnelUpperRight", "LipFunnelLowerLeft", "LipFunnelLowerRight" })
                Assert.AreEqual(0.7f, Key(values, key), 1e-5f, key);
            Assert.AreEqual(4, filter.KeysOf(babble.ControlIndex["mouthFunnel"]).Count);
        }

        [Test]
        public void Hardware_ViveConfigurations()
        {
            FTEmulatorExpressionFrame frame = Frame(0f, ("JawOpen", 1f), ("TongueOut", 1f), ("EyeLookInLeft", 0.6f), ("EyeClosedRight", 1f), ("EyeSquintLeft", 0.5f),
                ("EyeDilationLeft", 0.5f));
            float[] eye = Through("vive-pro-eye", FTEmulatorBehavior.Strict, frame, out bool[] eyeDriven, out FTEmulatorHardwareFilter eyeFilter);
            Assert.AreEqual(0f, Key(eye, "JawOpen"));
            Assert.IsFalse(eyeDriven[Catalog.Key("JawOpen")]);
            Assert.AreEqual(0.6f, Key(eye, "EyeLookInLeft"), 1e-5f);
            Assert.AreEqual(0f, Key(eye, "EyeLookOutLeft"), 1e-5f);
            Assert.AreEqual(1f, Key(eye, "EyeClosedRight"), 1e-5f);
            Assert.AreEqual(0.5f, Key(eye, "EyeDilationLeft"), 1e-4f);
            // Strict: brows are not sensed. Module behavior: they follow the squeeze, marked Derived.
            Assert.IsFalse(eyeFilter.Allowed[Catalog.Key("BrowLowererLeft")]);
            float[] module = Through("vive-pro-eye", FTEmulatorBehavior.ModuleBehavior, frame, out bool[] _, out FTEmulatorHardwareFilter moduleFilter);
            Assert.AreEqual(0.5f, Key(module, "BrowLowererLeft"), 1e-5f);
            Assert.IsTrue(moduleFilter.IsDerived(Catalog.Key("BrowLowererLeft")));

            float[] face = Through("vive-facial-tracker", FTEmulatorBehavior.Strict, frame, out bool[] faceDriven, out FTEmulatorHardwareFilter _);
            Assert.AreEqual(0f, Key(face, "EyeLookInLeft"));
            Assert.IsFalse(faceDriven[Catalog.Key("EyeLookInLeft")]);
            Assert.AreEqual(1f, Key(face, "JawOpen"), 1e-5f);
            Assert.AreEqual(1f, Key(face, "TongueOut"), 1e-5f);
            Assert.AreEqual(0f, Key(face, "TongueTwistLeft"));

            float[] stock = Through("vive-pro-stock", FTEmulatorBehavior.ModuleBehavior, Frame(1f), out bool[] stockDriven, out FTEmulatorHardwareFilter _);
            for (int key = 0; key < stock.Length; key++)
            {
                Assert.AreEqual(0f, stock[key]);
                Assert.IsFalse(stockDriven[key]);
            }
        }

        // ---- Store ----

        [Test]
        public void Store_ManualBeatsLive_HoldKeeps_DisabledDrivesNothing()
        {
            var store = new FTEmulatorParameterStore();
            store.Ensure("control:a", FTEmulatorValueType.Float, 0f, 1f, 0f);
            store.SetLive("control:a", 0.2f, 10.0);
            Assert.IsTrue(store.TryEffective("control:a", 10.0, out float live));
            Assert.AreEqual(0.2f, live);
            Assert.IsTrue(store.SetManual("control:a", 5f));
            store.SetLive("control:a", 0.3f, 10.1);
            store.TryEffective("control:a", 10.1, out float manual);
            Assert.AreEqual(1f, manual, "held to the row's range");
            Assert.IsTrue(store.SetMode("control:a", FTEmulatorRowMode.Live, 10.1));
            Assert.IsTrue(store.SetMode("control:a", FTEmulatorRowMode.Held, 10.1));
            store.SetLive("control:a", 0.9f, 10.2);
            store.TryEffective("control:a", 10.2, out float held);
            Assert.AreEqual(0.3f, held);
            store.SetMode("control:a", FTEmulatorRowMode.Disabled, 10.2);
            Assert.IsFalse(store.TryEffective("control:a", 10.2, out float _));
            Assert.IsFalse(store.SetManual("control:missing", 1f));
        }

        [Test]
        public void Store_BlockedRowRefusesOverrides()
        {
            var store = new FTEmulatorParameterStore();
            store.Ensure("parameter:p", FTEmulatorValueType.Float, -1f, 1f, 0f, false);
            store.SetManual("parameter:p", 0.5f);
            store.SetBlocked("parameter:p", "Blocked: no channel");
            Assert.IsFalse(store.TryEffective("parameter:p", 0, out float _));
            Assert.IsFalse(store.SetManual("parameter:p", 0.5f));
            Assert.IsFalse(store.SetMode("parameter:p", FTEmulatorRowMode.Held, 0));
            Assert.AreEqual(FTEmulatorRowMode.Live, store.Find("parameter:p").Mode);
        }

        [Test]
        public void Store_FreezeHoldsAndResumeWaitsForFreshSample()
        {
            var store = new FTEmulatorParameterStore();
            store.Ensure("control:a", FTEmulatorValueType.Float, 0f, 1f, 0f);
            store.SetLive("control:a", 0.4f, 1.0);
            store.FreezeAll(1.0);
            store.SetLive("control:a", 0.9f, 1.1);
            store.TryEffective("control:a", 1.1, out float frozen);
            Assert.AreEqual(0.4f, frozen);
            Assert.AreEqual(0.9f, store.Find("control:a").Live, "the live value is still shown");
            store.ResumeLive(5.0);
            // Nothing recorded during the freeze is replayed: the held value stays until a new sample.
            Assert.IsTrue(store.TryEffective("control:a", 5.0, out float waiting));
            Assert.AreEqual(0.4f, waiting);
            store.SetLive("control:a", 0.1f, 5.1);
            store.TryEffective("control:a", 5.1, out float fresh);
            Assert.AreEqual(0.1f, fresh);
        }

        [Test]
        public void Store_StaleInputFadesToNeutral_ThenIsNotDriven()
        {
            var store = new FTEmulatorParameterStore();
            store.Ensure("control:a", FTEmulatorValueType.Float, 0f, 1f, 0f);
            store.Ensure("parameter:p", FTEmulatorValueType.Float, -1f, 1f, 0f, false);
            store.SetLive("control:a", 1f, 0.0);
            store.SetLive("parameter:p", 1f, 0.0);
            store.TryEffective("control:a", FTEmulatorParameterStore.GraceSeconds, out float grace);
            Assert.AreEqual(1f, grace);
            store.TryEffective("control:a", FTEmulatorParameterStore.GraceSeconds + FTEmulatorParameterStore.FadeSeconds / 2, out float half);
            Assert.AreEqual(0.5f, half, 1e-4f);
            Assert.IsFalse(store.TryEffective("control:a", 1.0, out float _));
            // A value its source sends only on change does not go stale.
            Assert.IsTrue(store.TryEffective("parameter:p", 100.0, out float kept));
            Assert.AreEqual(1f, kept);
            store.Retain(key => key.StartsWith("parameter:"));
            Assert.IsNull(store.Find("control:a"));
        }

        // ---- Legacy mapping ----

        private static List<FTEmulatorAvatarParameter> Parameters(params (string name, FTEmulatorValueType type)[] list)
        {
            var result = new List<FTEmulatorAvatarParameter>();
            foreach ((string name, FTEmulatorValueType type) in list) result.Add(new FTEmulatorAvatarParameter { Name = name, Type = type });
            return result;
        }

        private static Dictionary<string, float> Map(FTEmulatorLegacyMapper mapper, bool eye, bool face, params (string key, float value)[] keys)
        {
            var values = new float[Catalog.Keys.Length];
            var driven = new bool[Catalog.Keys.Length];
            for (int i = 0; i < driven.Length; i++) driven[i] = true;
            foreach ((string key, float value) in keys) values[Catalog.Key(key)] = value;
            var output = new List<FTEmulatorParameterValue>();
            mapper.Map(values, driven, eye, face, output);
            var result = new Dictionary<string, float>();
            foreach (FTEmulatorParameterValue value in output) result[value.Name] = value.Value;
            return result;
        }

        [Test]
        public void Legacy_BindsExactNames_SignedAxes_EyeOpenNeutral_Status()
        {
            FTEmulatorAvatarProfile set = Catalog.GetAvatarProfile("vrcft-unified-v2");
            var mapper = new FTEmulatorLegacyMapper(Catalog, set, Parameters(("FT/v2/JawOpen", FTEmulatorValueType.Float), ("FT/v2/JawX", FTEmulatorValueType.Float),
                ("FT/v2/EyeLidLeft", FTEmulatorValueType.Float), ("OSCm/Proxy/FT/v2/JawOpen", FTEmulatorValueType.Float), ("v2/TongueOut", FTEmulatorValueType.Float),
                ("FT/v2/JawOpenExtra", FTEmulatorValueType.Float), ("FT/v2/MouthClosed", FTEmulatorValueType.Int), ("EyeTrackingActive", FTEmulatorValueType.Bool),
                ("FT/LipTrackingActive", FTEmulatorValueType.Bool), ("Unrelated", FTEmulatorValueType.Float)), null, null);
            Assert.IsNotNull(mapper.Find("FT/v2/JawOpen"));
            Assert.IsNotNull(mapper.Find("v2/TongueOut"));
            Assert.IsNull(mapper.Find("OSCm/Proxy/FT/v2/JawOpen"), "a template's own output is never bound");
            Assert.IsNull(mapper.Find("FT/v2/JawOpenExtra"));
            Assert.IsNull(mapper.Find("Unrelated"));
            Assert.IsNull(mapper.Find("FT/v2/MouthClosed"));
            Assert.AreEqual(1, mapper.Unmapped.Count);
            Dictionary<string, float> values = Map(mapper, true, false, ("JawOpen", 0.6f), ("JawLeft", 1f));
            Assert.AreEqual(0.6f, values["FT/v2/JawOpen"], 1e-5f);
            Assert.AreEqual(-1f, values["FT/v2/JawX"], 1e-5f);
            Assert.AreEqual(0.75f, values["FT/v2/EyeLidLeft"], 1e-5f, "open eyes are not zero");
            Assert.AreEqual(1f, values["EyeTrackingActive"]);
            Assert.AreEqual(0f, values["FT/LipTrackingActive"]);
        }

        [Test]
        public void Legacy_BinaryParameters()
        {
            FTEmulatorAvatarProfile set = Catalog.GetAvatarProfile("vrcft-unified-v2");
            var mapper = new FTEmulatorLegacyMapper(Catalog, set, Parameters(("FT/v2/JawOpen1", FTEmulatorValueType.Bool), ("FT/v2/JawOpen2", FTEmulatorValueType.Bool),
                ("FT/v2/JawOpen4", FTEmulatorValueType.Bool), ("FT/v2/JawOpen3", FTEmulatorValueType.Bool), ("FT/v2/JawX1", FTEmulatorValueType.Bool),
                ("FT/v2/JawX2", FTEmulatorValueType.Bool), ("FT/v2/JawXNegative", FTEmulatorValueType.Bool), ("FT/v2/TongueX1", FTEmulatorValueType.Bool)), null, null);
            Assert.IsNull(mapper.Find("FT/v2/JawOpen3"), "3 is not a power of two");
            // 0.5 of three bits: 4 of 8.
            Dictionary<string, float> half = Map(mapper, true, true, ("JawOpen", 0.5f), ("JawLeft", 0.5f), ("TongueLeft", 1f));
            Assert.AreEqual(0f, half["FT/v2/JawOpen1"]);
            Assert.AreEqual(0f, half["FT/v2/JawOpen2"]);
            Assert.AreEqual(1f, half["FT/v2/JawOpen4"]);
            // JawX is -0.5: the sign is in Negative, the size (2 of 4) in the bits.
            Assert.AreEqual(1f, half["FT/v2/JawXNegative"]);
            Assert.AreEqual(0f, half["FT/v2/JawX1"]);
            Assert.AreEqual(1f, half["FT/v2/JawX2"]);
            // Without a Negative parameter a negative value is cut.
            Assert.AreEqual(0f, half["FT/v2/TongueX1"]);
            Dictionary<string, float> full = Map(mapper, true, true, ("JawOpen", 1f));
            Assert.AreEqual(1f, full["FT/v2/JawOpen1"]);
            Assert.AreEqual(1f, full["FT/v2/JawOpen2"]);
            Assert.AreEqual(1f, full["FT/v2/JawOpen4"]);
        }

        [Test]
        public void Legacy_UndrivenKeyLeavesParameterOut_CustomMappingReplaces()
        {
            FTEmulatorAvatarProfile set = Catalog.GetAvatarProfile("vrcft-unified-v2");
            var custom = new Dictionary<string, string> { { "MyJaw", "JawOpen * 0.5" }, { "Broken", "JawOpen +" } };
            var mapper = new FTEmulatorLegacyMapper(Catalog, set, Parameters(("FT/v2/JawOpen", FTEmulatorValueType.Float), ("MyJaw", FTEmulatorValueType.Float),
                ("Broken", FTEmulatorValueType.Float)), custom, name => name == "FT/v2/JawOpen");
            Assert.IsNull(mapper.Find("FT/v2/JawOpen"), "a parameter with another owner is not bound");
            Assert.AreEqual(FTEmulatorLegacyMapper.BindingKind.Custom, mapper.Find("MyJaw").Kind);
            Assert.AreEqual(1, mapper.Unmapped.Count);
            var values = new float[Catalog.Keys.Length];
            values[Catalog.Key("JawOpen")] = 0.8f;
            var output = new List<FTEmulatorParameterValue>();
            Assert.AreEqual(1, mapper.Map(values, new bool[Catalog.Keys.Length], true, true, output));
            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void Legacy_PassthroughIsCheckedAgainstHardware()
        {
            FTEmulatorAvatarProfile set = Catalog.GetAvatarProfile("vrcft-unified-v2");
            var mapper = new FTEmulatorLegacyMapper(Catalog, set, Parameters(("FT/v2/JawOpen", FTEmulatorValueType.Float), ("FT/v2/EyeLidLeft", FTEmulatorValueType.Float),
                ("FT/v2/JawZ", FTEmulatorValueType.Float), ("FT/v2/TongueOut", FTEmulatorValueType.Float), ("EyeTrackingActive", FTEmulatorValueType.Bool)), null, null);
            FTEmulatorHardwareProfile quest = Catalog.GetHardware("quest-pro-base-fb1");
            bool[] allowed = new FTEmulatorHardwareFilter(Catalog, quest, FTEmulatorBehavior.Strict).Allowed;
            Assert.IsNull(mapper.ValidatePassthrough("FT/v2/JawOpen", allowed, quest, Catalog.Keys));
            StringAssert.StartsWith("Blocked", mapper.ValidatePassthrough("FT/v2/TongueOut", allowed, quest, Catalog.Keys));
            // JawZ mixes JawForward (sensed) with JawBackward (not): the combined value cannot be split.
            StringAssert.StartsWith("Strict", mapper.ValidatePassthrough("FT/v2/JawZ", allowed, quest, Catalog.Keys));
            StringAssert.StartsWith("Strict", mapper.ValidatePassthrough("Something/Else", allowed, quest, Catalog.Keys));
            StringAssert.StartsWith("Set by FTEmulator", mapper.ValidatePassthrough("EyeTrackingActive", allowed, quest, Catalog.Keys));
            FTEmulatorHardwareProfile babble = Catalog.GetHardware("babble-lower-v2.1.2");
            bool[] lower = new FTEmulatorHardwareFilter(Catalog, babble, FTEmulatorBehavior.Strict).Allowed;
            StringAssert.StartsWith("Blocked", mapper.ValidatePassthrough("FT/v2/EyeLidLeft", lower, babble, Catalog.Keys));
            FTEmulatorHardwareProfile free = Catalog.GetHardware("unrestricted");
            Assert.IsNull(mapper.ValidatePassthrough("Something/Else", new FTEmulatorHardwareFilter(Catalog, free, FTEmulatorBehavior.Strict).Allowed, free, Catalog.Keys));
        }

        // ---- Native evaluation ----

        private static FTEmulatorNativeConfig.Set Set(string name, float weight, params (string key, string blendshape, float multiplier)[] mappings)
        {
            var set = new FTEmulatorNativeConfig.Set { Name = name, DefaultWeight = weight, KeyBlendshapes = new string[Catalog.Keys.Length], KeyMultipliers = new float[Catalog.Keys.Length] };
            foreach ((string key, string blendshape, float multiplier) in mappings)
            {
                set.KeyBlendshapes[Catalog.Key(key)] = blendshape;
                set.KeyMultipliers[Catalog.Key(key)] = multiplier;
            }
            return set;
        }

        private static float[] Keys(params (string key, float value)[] keys)
        {
            var values = new float[Catalog.Keys.Length];
            foreach ((string key, float value) in keys) values[Catalog.Key(key)] = value;
            return values;
        }

        [Test]
        public void Native_SetsShareByHighestValue_WeightsAndLimits()
        {
            var config = new FTEmulatorNativeConfig { Enabled = true, UseAllTrackingKeys = true };
            config.Sets.Add(Set("Main", 1f, ("JawOpen", "Jaw", 1f), ("MouthClosed", "Closed", 1f)));
            config.Sets.Add(Set("Soft", 0.5f, ("JawOpen", "Jaw", 1f), ("TongueOut", "Tongue", 1f)));
            config.Behaviors.Add(new FTEmulatorNativeConfig.Behavior
            {
                TargetMode = FTEmulatorNativeConfig.LimitBlendshapeMax, TargetBlendshape = "Closed", Inputs = new[] { Catalog.Key("JawOpen") },
                Inverted = new[] { true }, CombineMode = FTEmulatorNativeConfig.CombineMax
            });
            config.Behaviors.Add(new FTEmulatorNativeConfig.Behavior
            {
                TargetMode = FTEmulatorNativeConfig.ApplyBlendSetWeight, TargetSet = "Soft", Inputs = new[] { Catalog.Key("CheekPuffLeft") },
                CombineMode = FTEmulatorNativeConfig.CombineMax
            });
            var evaluator = new FTEmulatorNativeEvaluator(config, Catalog);
            Assert.IsFalse(evaluator.Partial);
            var weights = new float[evaluator.Blendshapes.Length];
            evaluator.Evaluate(Keys(("JawOpen", 0.6f), ("MouthClosed", 1f), ("TongueOut", 1f), ("CheekPuffLeft", 0.25f)), weights, new float[0]);
            float Weight(string name) { return weights[Array.IndexOf(evaluator.Blendshapes, name)]; }
            Assert.AreEqual(60f, Weight("Jaw"), 1e-3f, "the highest of 0.6 and 0.6 x 0.25, not their sum");
            Assert.AreEqual(40f, Weight("Closed"), 1e-3f, "limited to 1 - JawOpen");
            Assert.AreEqual(25f, Weight("Tongue"), 1e-3f, "the set's weight follows the behavior");
            evaluator.SetWeight("Main", 0f);
            evaluator.Evaluate(Keys(("JawOpen", 1f), ("CheekPuffLeft", 0.5f)), weights, new float[0]);
            Assert.AreEqual(50f, Weight("Jaw"), 1e-3f);
        }

        [Test]
        public void Native_ForwardsSignedCustomAndReportsWhatIsNotEmulated()
        {
            var config = new FTEmulatorNativeConfig { Enabled = true, UseAllTrackingKeys = true, VisemeDampen = true };
            config.Behaviors.Add(new FTEmulatorNativeConfig.Behavior
            { TargetMode = FTEmulatorNativeConfig.ForwardToAnimator, OutputParameter = "FT/v2/JawX", CombineMode = FTEmulatorNativeConfig.CombineCustom, Expression = "(JawRight - JawLeft) * 3" });
            config.Behaviors.Add(new FTEmulatorNativeConfig.Behavior
            {
                TargetMode = FTEmulatorNativeConfig.ForwardToAnimator, OutputParameter = "Sum", CombineMode = FTEmulatorNativeConfig.CombineAdd,
                Inputs = new[] { Catalog.Key("JawOpen"), Catalog.Key("TongueOut") }
            });
            config.Behaviors.Add(new FTEmulatorNativeConfig.Behavior
            { TargetMode = FTEmulatorNativeConfig.ForwardToAnimator, OutputParameter = "Loud", CombineMode = FTEmulatorNativeConfig.CombineCustom, Expression = "Voice * JawOpen" });
            config.Behaviors.Add(new FTEmulatorNativeConfig.Behavior
            { TargetMode = FTEmulatorNativeConfig.ForwardToAnimator, OutputParameter = "Bad", CombineMode = FTEmulatorNativeConfig.CombineCustom, Expression = "JawOpen +" });
            var evaluator = new FTEmulatorNativeEvaluator(config, Catalog);
            CollectionAssert.AreEqual(new[] { "FT/v2/JawX", "Sum", "Loud" }, evaluator.Forwarded);
            Assert.AreEqual(1, evaluator.Ignored.Count);
            Assert.IsTrue(evaluator.Partial);
            Assert.AreEqual(2, evaluator.Unsupported.Count, "Voice and Viseme Dampen");
            var forwarded = new float[3];
            evaluator.Evaluate(Keys(("JawLeft", 1f), ("JawOpen", 0.8f), ("TongueOut", 0.7f)), new float[0], forwarded);
            Assert.AreEqual(-1f, forwarded[0], "signed, held to -1..1");
            Assert.AreEqual(1f, forwarded[1], "held to 0..1");
            Assert.AreEqual(0f, forwarded[2], "Voice reads 0");

            // Without Use All Tracking Keys only eye look and blink reach the avatar.
            var basic = new FTEmulatorNativeConfig { Enabled = true };
            basic.Sets.Add(Set("Main", 1f, ("JawOpen", "Jaw", 1f), ("EyeClosedLeft", "Blink", 1f)));
            var limited = new FTEmulatorNativeEvaluator(basic, Catalog);
            CollectionAssert.AreEqual(new[] { "Blink" }, limited.Blendshapes);
        }

        // ---- Template detection ----

        private static FTEmulatorAvatarSnapshot Snapshot(bool native, params string[] parameters)
        {
            var snapshot = new FTEmulatorAvatarSnapshot { Name = "Avatar", Id = "local:test", NativeSupported = true };
            foreach (string name in parameters)
            {
                snapshot.ControllerParameters[name] = FTEmulatorValueType.Float;
                // The stand-in avatar declares its FT/ parameters; a tracking-active flag is a Bool.
                if (name.StartsWith("FT/"))
                    snapshot.ExpressionParameters.Add(new FTEmulatorAvatarParameter { Name = name, Type = name.EndsWith("Active") ? FTEmulatorValueType.Bool : FTEmulatorValueType.Float });
            }
            if (native)
            {
                snapshot.Native = new FTEmulatorNativeConfig { Enabled = true, UseAllTrackingKeys = true };
                snapshot.Native.Sets.Add(Set("Main", 1f, ("JawOpen", "JawOpen", 1f)));
            }
            return snapshot;
        }

        [Test]
        public void Resolver_NativeLegacyMixedUnknownAndNeedsBuildPreview()
        {
            FTEmulatorResolution native = FTEmulatorTemplateResolver.Resolve(Snapshot(true), Catalog, FTEmulatorTemplateResolver.Auto);
            Assert.AreEqual("native", native.Backend);
            Assert.AreEqual(FTEmulatorResolution.Confirmed, native.Confidence);
            Assert.IsFalse(native.NativeWithLegacy);

            FTEmulatorResolution family = FTEmulatorTemplateResolver.Resolve(Snapshot(false, "FT/v2/JawOpen"), Catalog, FTEmulatorTemplateResolver.Auto);
            Assert.AreEqual("legacy", family.Backend);
            Assert.AreEqual(FTEmulatorResolution.Probable, family.Confidence, "a lone FT/v2 prefix names the family, not the author");
            Assert.AreEqual("vrcft-unified-v2", family.ParameterSet.Id);

            FTEmulatorResolution adjerry = FTEmulatorTemplateResolver.Resolve(Snapshot(false, "FT/v2/JawOpen", "OSCm/Smooth/FT/v2/JawOpen"), Catalog, FTEmulatorTemplateResolver.Auto);
            Assert.AreEqual("Adjerry legacy", adjerry.Template);
            Assert.AreEqual(FTEmulatorResolution.Confirmed, adjerry.Confidence);

            FTEmulatorResolution pawlygon = FTEmulatorTemplateResolver.Resolve(Snapshot(false, "FT/v2/JawOpen", "OSCm/Local/Smooth/FT/v2/JawOpen"), Catalog, FTEmulatorTemplateResolver.Auto);
            Assert.AreEqual("Pawlygon 1.x", pawlygon.Template);

            FTEmulatorResolution mixed = FTEmulatorTemplateResolver.Resolve(Snapshot(false, "FT/v2/JawOpen", "OSCm/Smooth/FT/v2/JawOpen", "OSCm/Binary/FT/v2/JawOpen"), Catalog,
                FTEmulatorTemplateResolver.Auto);
            Assert.AreEqual(FTEmulatorResolution.Mixed, mixed.Confidence);

            FTEmulatorResolution both = FTEmulatorTemplateResolver.Resolve(Snapshot(true, "FT/v2/JawOpen"), Catalog, FTEmulatorTemplateResolver.Auto);
            Assert.AreEqual("native", both.Backend);
            Assert.IsTrue(both.NativeWithLegacy);

            FTEmulatorResolution unknown = FTEmulatorTemplateResolver.Resolve(Snapshot(false, "Smile", "FT/SomethingElse"), Catalog, FTEmulatorTemplateResolver.Auto);
            Assert.IsNull(unknown.Backend);
            Assert.AreEqual(FTEmulatorResolution.Unknown, unknown.Confidence);

            FTEmulatorAvatarSnapshot unbuilt = Snapshot(false);
            unbuilt.Components.Add("Pawlygon.FaceTracking.PawlygonFaceTracking");
            FTEmulatorResolution pending = FTEmulatorTemplateResolver.Resolve(unbuilt, Catalog, FTEmulatorTemplateResolver.Auto);
            Assert.IsNull(pending.Backend);
            Assert.AreEqual(FTEmulatorResolution.NeedsBuildPreview, pending.Confidence);

            FTEmulatorAvatarSnapshot built = Snapshot(true);
            built.Packages["net.pawlygon.vrc-facetracking"] = "2.0.7";
            Assert.AreEqual("Native / Pawlygon 2.x", FTEmulatorTemplateResolver.Resolve(built, Catalog, FTEmulatorTemplateResolver.Auto).Template);

            // A choice made by hand does not skip the checks.
            FTEmulatorResolution forced = FTEmulatorTemplateResolver.Resolve(Snapshot(false, "FT/v2/JawOpen"), Catalog, FTEmulatorTemplateResolver.NativeChoice);
            Assert.IsNull(forced.Backend);
            Assert.IsNotEmpty(forced.Missing);
            FTEmulatorAvatarSnapshot conflict = Snapshot(false, "FT/v2/JawOpen");
            conflict.TypeConflicts.Add("Odd");
            Assert.IsTrue(FTEmulatorTemplateResolver.Resolve(conflict, Catalog, "adjerry-legacy").Missing.Exists(line => line.Contains("Odd")));
        }

        // ---- Session, with stand-ins ----

        internal sealed class Rig
        {
            internal FTEmulatorTestSource Source = new FTEmulatorTestSource();
            internal FTEmulatorTestOutput Output = new FTEmulatorTestOutput();
            internal FTEmulatorTestMesh Mesh = new FTEmulatorTestMesh();
            internal FTEmulatorSessionOptions Options = new FTEmulatorSessionOptions { Discovery = false };
            internal FTEmulatorAvatarSnapshot Avatar;
            internal bool Playing = true;
            internal double Now = 100.0;
            internal FTEmulatorSession Session;

            internal FTEmulatorSession Make()
            {
                foreach (FTEmulatorAvatarParameter parameter in Avatar.ExpressionParameters) Output.Add(parameter.Name, parameter.Type);
                Session = new FTEmulatorSession(Catalog, Options, Output, () => Avatar, snapshot => Mesh, (options, diagnostics) => Source, () => Playing, null);
                return Session;
            }

            internal void Tick(double seconds = 0.016)
            {
                Now += seconds;
                Session.Tick(Now);
                Session.LateTick();
            }
        }

        private static Rig LegacyRig(params string[] parameters)
        {
            var rig = new Rig { Avatar = Snapshot(false, parameters) };
            rig.Make();
            return rig;
        }

        [Test]
        public void Session_RefusesOutsidePlayMode_AndWithoutAvatar()
        {
            Rig rig = LegacyRig("FT/v2/JawOpen");
            rig.Playing = false;
            Assert.IsFalse(rig.Session.Start(out string problem));
            StringAssert.Contains("Play Mode", problem);
            Assert.AreEqual(0, rig.Source.Starts);
            Assert.AreEqual(FTEmulatorState.Stopped, rig.Session.State);
            Rig invalid = LegacyRig("FT/v2/JawOpen");
            invalid.Output.IsValid = false;
            Assert.IsFalse(invalid.Session.Start(out string noAvatar));
            StringAssert.Contains("Gesture Manager", noAvatar);
        }

        [Test]
        public void Session_LegacyPipeline_Manual_Freeze_Resume_Stop()
        {
            Rig rig = LegacyRig("FT/v2/JawOpen", "FT/v2/JawX");
            rig.Output.Values["FT/v2/JawOpen"] = 0.11f;
            Assert.IsTrue(rig.Session.Start(out string problem), problem);
            Assert.AreEqual(1, rig.Source.Starts);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.6f), ("JawRight", 1f));
            rig.Tick();
            Assert.AreEqual(0.6f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            Assert.AreEqual(1f, rig.Output.Values["FT/v2/JawX"], 1e-5f);

            // A typed value takes over that row only.
            Assert.IsTrue(rig.Session.SetRowManual("control:JawOpen", 0.9f));
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.2f), ("JawLeft", 1f));
            rig.Tick();
            Assert.AreEqual(0.9f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            Assert.AreEqual(-1f, rig.Output.Values["FT/v2/JawX"], 1e-5f);
            rig.Session.ResetOverrides();

            // Freeze output: input goes on arriving, the output keeps its values.
            rig.Session.SetFrozen(true);
            Assert.AreEqual(FTEmulatorState.Frozen, rig.Session.State);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.05f));
            rig.Tick(1.0);
            Assert.AreEqual(0.2f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            FTEmulatorSessionSnapshot frozen = rig.Session.Snapshot();
            FTEmulatorDebugRow row = frozen.Controls.Find(item => item.Name == "JawOpen");
            Assert.AreEqual(0.05f, row.Live, 1e-5f);
            Assert.AreEqual(0.2f, row.Effective, 1e-5f);
            Assert.AreEqual("Held", row.Owner);

            // Resume: what queued up is dropped, the held value stays until a new sample.
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.07f));
            rig.Session.SetFrozen(false);
            rig.Tick();
            Assert.AreEqual(0.2f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.3f));
            rig.Tick();
            Assert.AreEqual(0.3f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);

            // Stop puts back what the parameters held before, closes the source, and can be called again.
            rig.Session.Stop("test");
            rig.Session.Stop("test");
            Assert.AreEqual(0.11f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            Assert.IsFalse(rig.Source.Running);
            Assert.IsTrue(rig.Output.Released);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 1f));
            rig.Tick();
            Assert.AreEqual(0.11f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f, "nothing is written after Stop");
        }

        [Test]
        public void Session_DropsInputOfAnotherGeneration_AndStopsWhenPlayModeEnds()
        {
            Rig rig = LegacyRig("FT/v2/JawOpen");
            rig.Session.Start(out string _);
            rig.Source.Push(Catalog, rig.Session.Generation - 1, ("JawOpen", 1f));
            rig.Tick();
            Assert.AreEqual(0f, rig.Output.Values["FT/v2/JawOpen"]);
            Assert.AreEqual(1, rig.Session.Diagnostics.Count(FTEmulatorDiagnostics.StaleGeneration));
            rig.Playing = false;
            rig.Tick();
            Assert.IsFalse(rig.Session.Running);
            Assert.IsFalse(rig.Source.Running);
        }

        [Test]
        public void Session_ListeningOffHolds_PauseDiscards_StaleInputReleases()
        {
            Rig rig = LegacyRig("FT/v2/JawOpen");
            rig.Session.Start(out string _);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.5f));
            rig.Tick();
            rig.Session.SetListening(false);
            Assert.AreEqual(FTEmulatorState.ListeningOff, rig.Session.State);
            Assert.IsFalse(rig.Source.Running);
            rig.Tick(5.0);
            Assert.AreEqual(0.5f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f, "held while the source is closed");
            rig.Session.SetListening(true);
            Assert.IsTrue(rig.Source.Running);

            rig.Session.SetPaused(true);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 1f));
            rig.Tick();
            rig.Session.SetPaused(false);
            rig.Tick();
            Assert.AreNotEqual(1f, rig.Output.Values["FT/v2/JawOpen"], "what arrived while paused was dropped");

            // Input that stops arriving fades out, then the parameter goes back to what it held.
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.8f));
            rig.Tick();
            Assert.AreEqual(0.8f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            rig.Tick(1.0);
            Assert.AreEqual(0f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
        }

        [Test]
        public void Session_ProfileSwitchDropsWhatTheNewDeviceLacks()
        {
            Rig rig = LegacyRig("FT/v2/TongueOut", "FT/v2/JawOpen");
            rig.Options.Input = FTEmulatorInput.Manual;
            rig.Session.Start(out string _);
            Assert.IsTrue(rig.Session.SetRowManual("control:TongueOut", 1f));
            Assert.IsTrue(rig.Session.SetRowManual("control:JawOpen", 0.5f));
            rig.Tick();
            Assert.AreEqual(1f, rig.Output.Values["FT/v2/TongueOut"]);
            rig.Session.SetHardware("quest-pro-base-fb1", FTEmulatorBehavior.Strict);
            Assert.IsFalse(rig.Session.SetRowManual("control:TongueOut", 1f), "the device has no such control");
            rig.Tick();
            Assert.AreEqual(0f, rig.Output.Values["FT/v2/TongueOut"], "the old device's value is not left latched");
            FTEmulatorSessionSnapshot snapshot = rig.Session.Snapshot();
            Assert.IsTrue(snapshot.Expressions.Find(row => row.Name == "TongueOut").Blocked);
            Assert.IsNull(snapshot.Controls.Find(row => row.Name == "TongueOut"));
            // Stock Vive Pro: everything FTEmulator drove is released.
            rig.Session.SetRowManual("control:JawDrop", 0.7f);
            rig.Tick();
            Assert.AreEqual(0.7f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            rig.Session.SetHardware("vive-pro-stock", FTEmulatorBehavior.Strict);
            rig.Tick();
            Assert.AreEqual(0f, rig.Output.Values["FT/v2/JawOpen"]);
            Assert.AreEqual(0, rig.Session.Snapshot().Applied);
        }

        [Test]
        public void Session_PassthroughParametersKeepTypeAndRespectHardware()
        {
            Rig rig = LegacyRig("FT/v2/JawOpen", "FT/v2/EyeLidLeft", "FT/LipTrackingActive", "FT/EyeTrackingActive");
            rig.Options.HardwareId = "babble-lower-v2.1.2";
            rig.Session.Start(out string _);
            rig.Tick();
            Assert.AreEqual(0f, rig.Output.Values["FT/LipTrackingActive"], "nothing is tracked yet");
            rig.Source.PushParameter(rig.Session.Generation, "FT/v2/JawOpen", FTEmulatorValueType.Float, 0.4f);
            rig.Tick();
            Assert.AreEqual(1f, rig.Output.Values["FT/LipTrackingActive"], "already mapped values count as tracking");
            // The flag is the session's own: an incoming one is not taken.
            rig.Source.PushParameter(rig.Session.Generation, "FT/EyeTrackingActive", FTEmulatorValueType.Bool, 1f);
            rig.Tick();
            Assert.AreEqual(0f, rig.Output.Values["FT/EyeTrackingActive"], "Babble tracks no eyes");
            rig.Source.PushParameter(rig.Session.Generation, "FT/v2/EyeLidLeft", FTEmulatorValueType.Float, 0.9f);
            rig.Tick(2.0);
            Assert.AreEqual(0.4f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f, "sent once, kept");
            Assert.AreEqual(0f, rig.Output.Values["FT/v2/EyeLidLeft"], "Babble has no eyes");
            Assert.AreEqual(2, rig.Session.Diagnostics.Count(FTEmulatorDiagnostics.ProfileBlock), "the eye lid value and the incoming status flag");
            FTEmulatorDebugRow blocked = rig.Session.Snapshot().Parameters.Find(row => row.Key == "parameter:FT/v2/EyeLidLeft");
            Assert.IsTrue(blocked.Blocked);
            Assert.IsFalse(rig.Session.SetRowManual("parameter:FT/v2/EyeLidLeft", 1f), "a manual value cannot get past the limits");
        }

        [Test]
        public void Session_NativeDrivesMeshForwardsAndStatus()
        {
            var rig = new Rig { Avatar = Snapshot(true) };
            rig.Avatar.Native.Behaviors.Add(new FTEmulatorNativeConfig.Behavior
            { TargetMode = FTEmulatorNativeConfig.ForwardToAnimator, OutputParameter = "FT/v2/JawOpen", CombineMode = FTEmulatorNativeConfig.CombineMax, Inputs = new[] { Catalog.Key("JawOpen") } });
            rig.Make();
            rig.Output.Add("FT/v2/JawOpen", FTEmulatorValueType.Float);
            rig.Output.Add(FTEmulatorSession.UserFaceTracking, FTEmulatorValueType.Bool);
            rig.Session.Start(out string _);
            rig.Tick();
            Assert.AreEqual(0, rig.Mesh.Applies, "nothing is tracked yet");
            Assert.AreEqual(0f, rig.Output.Values[FTEmulatorSession.UserFaceTracking]);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.5f));
            rig.Tick();
            Assert.AreEqual(1, rig.Mesh.Applies);
            Assert.AreEqual(50f, rig.Mesh.Weights[Array.IndexOf(rig.Mesh.Names, "JawOpen")], 1e-3f);
            Assert.AreEqual(0.5f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            Assert.AreEqual(1f, rig.Output.Values[FTEmulatorSession.UserFaceTracking]);
            // An already mapped value for a parameter the native forwarder drives is not taken.
            rig.Source.PushParameter(rig.Session.Generation, "FT/v2/JawOpen", FTEmulatorValueType.Float, 0.9f);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 0.5f));
            rig.Tick();
            Assert.AreEqual(0.5f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
            // A state behavior's set weight is reached over time.
            rig.Session.RequestSetWeight("Main", 0f, 1f);
            rig.Source.Push(Catalog, rig.Session.Generation, ("JawOpen", 1f));
            rig.Tick(0.05);
            Assert.That(rig.Session.Evaluator.GetWeight("Main"), Is.InRange(0.9f, 0.99f));
            rig.Session.Stop("test");
            Assert.That(rig.Mesh.Restores, Is.GreaterThan(0));
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator core tests (assembly Riry.RiryTools.Editor.FTEmulatorTests: Unity Test Framework only)
// ============================================================================
//
// FTEmulatorTestSource / FTEmulatorTestOutput / FTEmulatorTestMesh  Stand-ins for a source, Gesture Manager and the meshes.
// FTEmulatorCoreTests (this file)
//   Catalog                  The package's profile data, read from the folders beside Tests/ (found from this file's path),
//                            or through FTEmulatorCatalog.LoadFromPackage when that path is not a folder.
//   Catalog_*                Every profile reads; bad files are rejected with the reason; a new read is a new catalog.
//   Expression_*             Arithmetic and precedence, malformed text, the client's limits, dependencies.
//   Frame_*                  NaN and infinity are refused; arrays are copied.
//   Hardware_*               Every profile with every key injected: no prohibited key reaches the output; Quest pucker
//                            coupling; Quest FB1 against FB2; Babble without eyes and with a shared funnel; the Vive
//                            configurations; strict against documented module behavior.
//   Store_*                  Manual over Live, Hold, Disabled, blocked rows, freeze and resume, stale input.
//   Legacy_*                 Exact name binding, signed axes, eye-open neutral, status, binary Bools, custom mappings,
//                            passthrough checks.
//   Native_*                 Sets sharing a blendshape, set weights, limits, forwards, what is not emulated.
//   Resolver_*               Native, legacy family, each template, mixed, unknown, needs build preview, a choice by hand.
//   Session_*                The whole pipeline with stand-ins: Play Mode gate, manual, freeze, resume, stop, another
//                            generation's input, listening off, pause, stale input, a profile switch, passthrough, native.
