#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor.Tests
{
    public sealed class BlendShapeSyncTests
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private BlendShapeSyncWindow window;

        [SetUp]
        public void SetUp()
        {
            if (AnimationMode.InAnimationMode()) Assert.Ignore("Stop animation preview before testing.");
            Selection.activeObject = null;
            window = ScriptableObject.CreateInstance<BlendShapeSyncWindow>();
            owned.Add(window);
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = null;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear();
            Undo.ClearAll();
        }

        [Test]
        public void NamesMatchWithoutCaseAndOnlyAsCompleteLiteralNames()
        {
            List<BlendShapeSyncWindow.IndexLink> links = Build(
                new[] { "Smile.L", "Blink[Left]", "Blink" },
                new[] { "smile.l", "SMILEXL", "blink[left]", "BlinkExtra" });
            Assert.That(links.Count, Is.EqualTo(2));
            Assert.That(links[0].Destination, Is.EqualTo(0));
            Assert.That(links[1].Destination, Is.EqualTo(2));
        }

        [Test]
        public void CaseCollisionOnEitherMeshIsRejected()
        {
            AssertCollision(new[] { "Blink" }, new[] { "Blink", "blink" });
            AssertCollision(new[] { "Blink", "blink" }, new[] { "Blink" });
        }

        [Test]
        public void DebugResolvesCaseCollisionWithExactNames()
        {
            List<BlendShapeSyncWindow.IndexLink> links = Build(
                new[] { "Blink" }, new[] { "blink", "Blink" }, null, true);
            Assert.That(links.Count, Is.EqualTo(1));
            Assert.That(links[0].Destination, Is.EqualTo(1));
        }

        [Test]
        public void ManualLinkOverridesBothSourceDefaultAndDestinationDefault()
        {
            var manual = new List<BlendShapeSyncWindow.ManualLink>
            {
                new BlendShapeSyncWindow.ManualLink { Source = "smile", Destination = "WINK" }
            };
            List<BlendShapeSyncWindow.IndexLink> links = Build(
                new[] { "Smile", "Wink", "Blink" }, new[] { "Smile", "Wink", "Blink" }, manual);
            Assert.That(links.Count, Is.EqualTo(2));
            Assert.That(links[0].Source, Is.EqualTo(0));
            Assert.That(links[0].Destination, Is.EqualTo(1));
            Assert.That(links[1].Source, Is.EqualTo(2));
            Assert.That(links[1].Destination, Is.EqualTo(2));
        }

        [Test]
        public void OneSourceCanBeManuallyLinkedToSeveralDestinations()
        {
            var manual = new List<BlendShapeSyncWindow.ManualLink>
            {
                new BlendShapeSyncWindow.ManualLink { Source = "Blink", Destination = "Left" },
                new BlendShapeSyncWindow.ManualLink { Source = "Blink", Destination = "Right" }
            };
            var links = Build(new[] { "Blink" }, new[] { "Left", "Right" }, manual);
            Assert.That(links.Count, Is.EqualTo(2));
            Assert.That(links[0].Source, Is.EqualTo(links[1].Source));
        }

        [Test]
        public void ConflictingManualDestinationIsRejected()
        {
            var manual = new List<BlendShapeSyncWindow.ManualLink>
            {
                new BlendShapeSyncWindow.ManualLink { Source = "A", Destination = "Target" },
                new BlendShapeSyncWindow.ManualLink { Source = "B", Destination = "Target" }
            };
            AssertInvalid(new[] { "A", "B" }, new[] { "Target" }, manual);
        }

        [Test]
        public void MissingAndIncompleteManualLinksAreRejected()
        {
            AssertInvalid(new[] { "Blink" }, new[] { "Blink" },
                new List<BlendShapeSyncWindow.ManualLink>
                { new BlendShapeSyncWindow.ManualLink { Source = "Typo", Destination = "Blink" } });
            AssertInvalid(new[] { "Blink" }, new[] { "Blink" },
                new List<BlendShapeSyncWindow.ManualLink>
                { new BlendShapeSyncWindow.ManualLink { Source = "Blink" } });
        }

        [Test]
        public void EveryChildUpdatesAndUnmatchedShapesStayUntouched()
        {
            var master = Renderer("Master", "Blink", "Smile");
            var first = Renderer("First", "blink", "Extra");
            var second = Renderer("Second", "SMILE", "Blink");
            master.SetBlendShapeWeight(0, -25);
            master.SetBlendShapeWeight(1, 150);
            first.SetBlendShapeWeight(1, 31);
            Assign(master, first, second);
            Call("SyncNow");
            Assert.That(first.GetBlendShapeWeight(0), Is.EqualTo(-25));
            Assert.That(first.GetBlendShapeWeight(1), Is.EqualTo(31));
            Assert.That(second.GetBlendShapeWeight(0), Is.EqualTo(150));
            Assert.That(second.GetBlendShapeWeight(1), Is.EqualTo(-25));
        }

        [Test]
        public void InvalidChildLinkPreventsWritesToEveryChild()
        {
            var master = Renderer("Master", "Blink");
            var first = Renderer("First", "Blink");
            var second = Renderer("Second", "Blink");
            master.SetBlendShapeWeight(0, 90);
            first.SetBlendShapeWeight(0, 11);
            second.SetBlendShapeWeight(0, 22);
            Assign(master, first, second);
            Get<List<BlendShapeSyncWindow.ChildEntry>>("children")[1].Links.Add(
                new BlendShapeSyncWindow.ManualLink { Source = "Missing", Destination = "Blink" });
            Call("SyncNow");
            Assert.That(first.GetBlendShapeWeight(0), Is.EqualTo(11));
            Assert.That(second.GetBlendShapeWeight(0), Is.EqualTo(22));
        }

        [Test]
        public void RestoreIncludesMasterUnlinkedShapesAndRemovedChildren()
        {
            var master = Renderer("Master", "Blink", "Extra");
            var child = Renderer("Child", "Blink", "Extra", "Unlinked");
            master.SetBlendShapeWeight(0, 70);
            master.SetBlendShapeWeight(1, 20);
            child.SetBlendShapeWeight(0, 15);
            child.SetBlendShapeWeight(2, 42);
            Assign(master, child);
            Call("SyncNow");
            master.SetBlendShapeWeight(1, 99);
            child.SetBlendShapeWeight(2, 88);
            Get<List<BlendShapeSyncWindow.ChildEntry>>("children").Clear();
            Set("runAutomatically", true);
            Call("RestoreOriginals");
            Assert.That(master.GetBlendShapeWeight(1), Is.EqualTo(20));
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(15));
            Assert.That(child.GetBlendShapeWeight(2), Is.EqualTo(42));
            Assert.That(Get<bool>("runAutomatically"), Is.False);
        }

        [Test]
        public void RepeatedSyncDoesNotReplaceTheOriginalCache()
        {
            var master = Renderer("Master", "Blink");
            var child = Renderer("Child", "Blink");
            master.SetBlendShapeWeight(0, 70);
            child.SetBlendShapeWeight(0, 15);
            Assign(master, child);
            Call("SyncNow");
            master.SetBlendShapeWeight(0, 80);
            Call("SyncNow");
            Call("RestoreOriginals");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(15));
            Assert.That(master.GetBlendShapeWeight(0), Is.EqualTo(70));
        }

        [Test]
        public void ReplacedMeshRebuildsLinksAndDoesNotUseAnOldBaseline()
        {
            var master = Renderer("Master", "Blink", "Smile");
            var child = Renderer("Child", "Blink", "Smile");
            master.SetBlendShapeWeight(0, 60);
            master.SetBlendShapeWeight(1, 80);
            Assign(master, child);
            Call("SyncNow");
            child.sharedMesh = MeshWithShapes("Smile", "Blink");
            child.SetBlendShapeWeight(0, 13);
            child.SetBlendShapeWeight(1, 24);
            Call("SyncNow");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(80));
            Assert.That(child.GetBlendShapeWeight(1), Is.EqualTo(60));
            Call("RestoreOriginals");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(13));
            Assert.That(child.GetBlendShapeWeight(1), Is.EqualTo(24));
        }

        [Test]
        public void InPlaceMeshReorderRebuildsLinksAndSkipsTheStaleBaseline()
        {
            var master = Renderer("Master", "Blink", "Smile");
            var child = Renderer("Child", "Blink", "Smile");
            master.SetBlendShapeWeight(0, 60);
            master.SetBlendShapeWeight(1, 80);
            Assign(master, child);
            Call("SyncNow");
            Mesh mesh = child.sharedMesh;
            mesh.ClearBlendShapes();
            Vector3[] delta = { Vector3.zero, Vector3.zero, Vector3.zero };
            mesh.AddBlendShapeFrame("Smile", 100, delta, delta, delta);
            mesh.AddBlendShapeFrame("Blink", 100, delta, delta, delta);
            Call("SyncNow");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(80));
            Assert.That(child.GetBlendShapeWeight(1), Is.EqualTo(60));
            Call("RestoreOriginals");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(80));
            Assert.That(child.GetBlendShapeWeight(1), Is.EqualTo(60));
        }

        [Test]
        public void EnteringPlayModeTurnsOffAutomaticSync()
        {
            Set("runAutomatically", true);
            typeof(BlendShapeSyncWindow).GetMethod("OnPlayModeChanged",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window,
                    new object[] { PlayModeStateChange.ExitingEditMode });
            Assert.That(Get<bool>("runAutomatically"), Is.False);
        }

        [Test]
        public void UndoRestoresOneWholeSyncAndStopsAutomaticSync()
        {
            var master = Renderer("Master", "Blink");
            var first = Renderer("First", "Blink");
            var second = Renderer("Second", "Blink");
            master.SetBlendShapeWeight(0, 60);
            first.SetBlendShapeWeight(0, 12);
            second.SetBlendShapeWeight(0, 23);
            Assign(master, first, second);
            Call("SyncNow");
            Set("runAutomatically", true);
            Undo.PerformUndo();
            Assert.That(first.GetBlendShapeWeight(0), Is.EqualTo(12));
            Assert.That(second.GetBlendShapeWeight(0), Is.EqualTo(23));
            Assert.That(Get<bool>("runAutomatically"), Is.False);
        }

        [Test]
        public void GraphSelectionBlocksBothSyncAndRestore()
        {
            var master = Renderer("Master", "Blink");
            var child = Renderer("Child", "Blink");
            Assign(master, child);
            Call("SyncNow");
            master.SetBlendShapeWeight(0, 80);
            child.SetBlendShapeWeight(0, 13);
            var state = ScriptableObject.CreateInstance<AnimatorState>();
            owned.Add(state);
            Selection.activeObject = state;
            Call("SyncNow");
            Call("RestoreOriginals");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(13));
            Assert.That(master.GetBlendShapeWeight(0), Is.EqualTo(80));
        }

        [Test]
        public void AnimationModeBlocksBothSyncAndRestore()
        {
            var master = Renderer("Master", "Blink");
            var child = Renderer("Child", "Blink");
            Assign(master, child);
            Call("SyncNow");
            master.SetBlendShapeWeight(0, 80);
            child.SetBlendShapeWeight(0, 13);
            AnimationMode.StartAnimationMode();
            try
            {
                Call("SyncNow");
                Call("RestoreOriginals");
                Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(13));
                Assert.That(master.GetBlendShapeWeight(0), Is.EqualTo(80));
            }
            finally { AnimationMode.StopAnimationMode(); }
        }

        [Test]
        public void TimerHonorsHalfSecondBoundaryAndSkipsCatchUpBursts()
        {
            var master = Renderer("Master", "Blink");
            var child = Renderer("Child", "Blink");
            master.SetBlendShapeWeight(0, 80);
            Assign(master, child);
            Set("runAutomatically", true);
            Set("nextTick", EditorApplication.timeSinceStartup + 30);
            Call("EditorTick");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(0));
            Set("nextTick", EditorApplication.timeSinceStartup - 30);
            Call("EditorTick");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(80));
            master.SetBlendShapeWeight(0, 10);
            Call("EditorTick");
            Assert.That(child.GetBlendShapeWeight(0), Is.EqualTo(80));
        }

        [Test]
        public void AssigningAChildKeepsAnEmptyDropRow()
        {
            var master = Renderer("Master", "Blink");
            var child = Renderer("Child", "Blink");
            Assign(master, child);
            Call("EnsureChildRow");
            var entries = Get<List<BlendShapeSyncWindow.ChildEntry>>("children");
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[1].Renderer, Is.Null);
            Call("EnsureChildRow");
            Assert.That(entries.Count, Is.EqualTo(2));
        }

        private static List<BlendShapeSyncWindow.IndexLink> Build(string[] sources,
            string[] destinations, List<BlendShapeSyncWindow.ManualLink> manual = null,
            bool exact = false)
        {
            var links = new List<BlendShapeSyncWindow.IndexLink>();
            string error;
            bool collision;
            Assert.That(BlendShapeSyncWindow.TryBuildLinks(sources, destinations, manual,
                exact, links, out error, out collision), Is.True, error);
            return links;
        }

        private static void AssertCollision(string[] sources, string[] destinations)
        {
            string error;
            bool collision;
            Assert.That(BlendShapeSyncWindow.TryBuildLinks(sources, destinations, null,
                false, new List<BlendShapeSyncWindow.IndexLink>(), out error, out collision), Is.False);
            Assert.That(collision, Is.True);
        }

        private static void AssertInvalid(string[] sources, string[] destinations,
            List<BlendShapeSyncWindow.ManualLink> links)
        {
            string error;
            bool collision;
            Assert.That(BlendShapeSyncWindow.TryBuildLinks(sources, destinations, links,
                false, new List<BlendShapeSyncWindow.IndexLink>(), out error, out collision), Is.False);
            Assert.That(collision, Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        private SkinnedMeshRenderer Renderer(string name, params string[] shapes)
        {
            var gameObject = new GameObject(name);
            owned.Add(gameObject);
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = MeshWithShapes(shapes);
            return renderer;
        }

        private Mesh MeshWithShapes(params string[] names)
        {
            var mesh = new Mesh();
            owned.Add(mesh);
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            Vector3[] delta = { new Vector3(0, 0, 0.01f), Vector3.zero, Vector3.zero };
            foreach (string name in names)
                mesh.AddBlendShapeFrame(name, 100, delta, new Vector3[3], new Vector3[3]);
            return mesh;
        }

        private void Assign(SkinnedMeshRenderer source, params SkinnedMeshRenderer[] targets)
        {
            Set("master", source);
            var entries = new List<BlendShapeSyncWindow.ChildEntry>();
            foreach (SkinnedMeshRenderer target in targets)
                entries.Add(new BlendShapeSyncWindow.ChildEntry { Renderer = target });
            Set("children", entries);
            Set("planDirty", true);
        }

        private void Set(string field, object value)
        {
            typeof(BlendShapeSyncWindow).GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, value);
        }

        private T Get<T>(string field)
        {
            return (T)typeof(BlendShapeSyncWindow).GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
        }

        private void Call(string method)
        {
            typeof(BlendShapeSyncWindow).GetMethod(method,
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
        }
    }
}
#endif
