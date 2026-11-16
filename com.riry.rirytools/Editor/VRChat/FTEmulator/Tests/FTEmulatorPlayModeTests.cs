using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
#if RIRYTOOLS_VRC_BIT_WIDTH
using VRC.SDK3.Avatars.Components;
#endif

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator in the Editor's own lifecycle: nothing runs in Edit Mode, a session ends with Play Mode however often
    /// Play Mode is entered, the package's data is found, the tracking keys match the SDK's, and blendshape weights are
    /// written and put back on a scene object. They run with stand-ins for the source and for Gesture Manager, so no
    /// headset, tracking app or avatar asset is needed. Each test enters and leaves Play Mode itself; values needed after
    /// that are made after it, because entering Play Mode may reload scripts.
    /// </summary>
    internal sealed class FTEmulatorPlayModeTests
    {
        [Test]
        public void EditMode_NothingStarts()
        {
            Assert.IsFalse(EditorApplication.isPlaying);
            Assert.IsFalse(FTEmulator.Start(null, out string problem));
            StringAssert.Contains("Play Mode", problem);
            Assert.IsNull(FTEmulator.Session);
            // The resource owners refuse too, not only the entry point.
            var context = new FTEmulatorSessionContext(1, () => EditorApplication.isPlaying, CancellationToken.None);
            var receiver = new FTEmulatorOscReceiver(0, 102, new FTEmulatorDiagnostics());
            Assert.Throws<System.InvalidOperationException>(() => receiver.Start(context));
            Assert.IsFalse(receiver.Running);
            var query = new FTEmulatorOscQuery(new FTEmulatorDiagnostics());
            Assert.Throws<System.InvalidOperationException>(() => query.Start(9000, new FTEmulatorAvatarAdvert("a", false, new FTEmulatorAvatarParameter[0], 1), context));
            Assert.IsFalse(query.Running);
        }

        [Test]
        public void Package_DataIsFoundAndUsable()
        {
            FTEmulatorCatalog catalog = FTEmulatorCatalog.LoadFromPackage();
            Assert.IsEmpty(catalog.Problems);
            Assert.IsTrue(catalog.Usable);
            Assert.AreEqual(FTEmulatorCoreTests.Catalog.Keys.Length, catalog.Keys.Length);
            Assert.AreEqual(FTEmulatorCoreTests.Catalog.Hardware.Count, catalog.Hardware.Count);
        }

#if RIRYTOOLS_VRC_BIT_WIDTH
        [Test]
        public void TrackingKeys_MatchTheSdk()
        {
            FTEmulatorCatalog catalog = FTEmulatorCoreTests.Catalog;
            Assert.AreEqual((int)VRCAvatarDescriptor.FaceTrackingKey.Max, catalog.Keys.Length);
            for (int key = 0; key < catalog.Keys.Length; key++) Assert.AreEqual(((VRCAvatarDescriptor.FaceTrackingKey)key).ToString(), catalog.Keys[key]);
        }
#endif

        [Test]
        public void Lifecycle_HooksAreRegisteredOnce()
        {
            // Registering again must not add a second set of handlers: one Play Mode change, one stop.
            FTEmulator.Register();
            FTEmulator.Register();
            FTEmulator.OnPlayModeChanged(PlayModeStateChange.ExitingPlayMode);
            FTEmulator.Shutdown("test");
            FTEmulator.Shutdown("test");
            Assert.IsNull(FTEmulator.Session);
        }

        [UnityTest]
        public IEnumerator Session_EndsWithPlayMode_Repeatedly()
        {
            for (int round = 0; round < 2; round++)
            {
                yield return new EnterPlayMode();
                var rig = new FTEmulatorCoreTests.Rig { Avatar = new FTEmulatorAvatarSnapshot { Name = "Test" } };
                rig.Avatar.ExpressionParameters.Add(new FTEmulatorAvatarParameter { Name = "FT/v2/JawOpen", Type = FTEmulatorValueType.Float });
                rig.Make();
                // The same gate FTEmulator uses.
                var session = new FTEmulatorSession(FTEmulatorCoreTests.Catalog, rig.Options, rig.Output, () => rig.Avatar, snapshot => rig.Mesh,
                    (options, diagnostics) => rig.Source, () => EditorApplication.isPlaying, null);
                Assert.IsTrue(session.Start(out string problem), problem);
                rig.Source.Push(FTEmulatorCoreTests.Catalog, session.Generation, ("JawOpen", 0.5f));
                session.Tick(FTEmulatorClock.Now);
                Assert.AreEqual(0.5f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
                Assert.IsTrue(rig.Source.Running);

                // Pause: nothing is applied and what arrives is dropped.
                session.SetPaused(true);
                rig.Source.Push(FTEmulatorCoreTests.Catalog, session.Generation, ("JawOpen", 1f));
                session.Tick(FTEmulatorClock.Now);
                Assert.AreEqual(0.5f, rig.Output.Values["FT/v2/JawOpen"], 1e-5f);
                session.SetPaused(false);

                yield return new ExitPlayMode();
                // The objects above survive only when scripts were not reloaded; either way nothing may be left running.
                Assert.IsFalse(EditorApplication.isPlaying);
                Assert.IsNull(FTEmulator.Session);
                if (session != null)
                {
                    session.Tick(FTEmulatorClock.Now);
                    Assert.IsFalse(session.Running, "the first tick outside Play Mode ends it");
                    Assert.IsFalse(rig.Source.Running);
                    Assert.IsTrue(rig.Output.Released);
                }
            }
        }

        [UnityTest]
        public IEnumerator Receiver_AndService_CloseWithTheSession()
        {
            yield return new EnterPlayMode();
            var rig = new FTEmulatorCoreTests.Rig { Avatar = new FTEmulatorAvatarSnapshot { Name = "Test" } };
            rig.Options.Port = 0;
            rig.Options.Discovery = true;
            FTEmulatorOscReceiver receiver = null;
            var session = new FTEmulatorSession(FTEmulatorCoreTests.Catalog, rig.Options, rig.Output, () => rig.Avatar, snapshot => rig.Mesh,
                (options, diagnostics) => receiver = new FTEmulatorOscReceiver(options.Port, FTEmulatorCoreTests.Catalog.Keys.Length, diagnostics),
                () => EditorApplication.isPlaying, null);
            Assert.IsTrue(session.Start(out string problem), problem);
            Assert.IsTrue(receiver.Running);
            FTEmulatorSessionSnapshot live = session.Snapshot();
            Assert.That(live.Port, Is.GreaterThan(0));
            StringAssert.StartsWith(FTEmulatorOscQuery.NamePrefix, live.ServiceName);
            // Active listening off closes the socket and withdraws the service; the session goes on.
            session.SetListening(false);
            Assert.IsFalse(receiver.Running);
            Assert.IsNull(session.Snapshot().ServiceName);
            Assert.IsTrue(session.Running);
            session.SetListening(true);
            session.Stop("test");
            Assert.IsFalse(session.Running);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BlendshapeWriter_WritesAndRestores_WithoutTouchingTheMesh()
        {
            yield return new EnterPlayMode();
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, triangles = new[] { 0, 1, 2 } };
            mesh.AddBlendShapeFrame("JawOpen", 100f, new[] { Vector3.up, Vector3.up, Vector3.up }, null, null);
            mesh.AddBlendShapeFrame("Other", 100f, new[] { Vector3.up, Vector3.up, Vector3.up }, null, null);
            var holder = new GameObject("FTEmulator test mesh");
            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.SetBlendShapeWeight(0, 12f);
            var writer = new FTEmulatorBlendshapeWriter(new[] { renderer, null });
            CollectionAssert.AreEqual(new[] { "Missing" }, writer.Bind(new[] { "JawOpen", "Missing" }));
            writer.Apply(new[] { 60f, 30f }, new[] { true, true });
            Assert.AreEqual(60f, renderer.GetBlendShapeWeight(0), 1e-3f);
            Assert.AreEqual(0f, renderer.GetBlendShapeWeight(1), "a blendshape it was not given is left alone");
            Assert.AreEqual(2, mesh.blendShapeCount);
            writer.Restore();
            Assert.AreEqual(12f, renderer.GetBlendShapeWeight(0), 1e-3f);
            // Something else took the blendshape after the last write: its value stays.
            writer.Apply(new[] { 60f, 30f }, new[] { true, true });
            renderer.SetBlendShapeWeight(0, 80f);
            writer.Restore();
            Assert.AreEqual(80f, renderer.GetBlendShapeWeight(0), 1e-3f);
            // A mesh destroyed during Play Mode is skipped.
            Object.DestroyImmediate(holder);
            writer.Apply(new[] { 10f, 10f }, new[] { true, true });
            writer.Restore();
            Object.DestroyImmediate(mesh);
            yield return new ExitPlayMode();
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator Play Mode tests (assembly Riry.RiryTools.Editor.FTEmulatorTests: Unity Test Framework only)
// ============================================================================
//
// FTEmulatorPlayModeTests (this file)
//   EditMode_NothingStarts   FTEmulator.Start, the OSC receiver and the OSCQuery service all refuse outside Play Mode.
//   Package_DataIsFoundAndUsable  FTEmulatorCatalog.LoadFromPackage finds the data folders by GUID.
//   TrackingKeys_MatchTheSdk With SDK 3.10.6+: the catalog's keys are VRCAvatarDescriptor.FaceTrackingKey, in order.
//   Lifecycle_HooksAreRegisteredOnce  Register twice, then a Play Mode change and Shutdown twice, leave no session.
//   Session_EndsWithPlayMode_Repeatedly  Enter Play Mode twice: a session applies input, pause drops input, and leaving
//                            Play Mode ends the session, closes its source and releases its output.
//   Receiver_AndService_CloseWithTheSession  The real receiver and OSCQuery service: open in Play Mode, closed by Active
//                            listening off and by Stop.
//   BlendshapeWriter_WritesAndRestores_WithoutTouchingTheMesh  Weights on a scene object: written, put back, left alone
//                            when something else changed them, and a destroyed mesh skipped.
