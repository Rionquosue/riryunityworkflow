#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riry.RiryTools.Editor.Tests
{
    public sealed class ConstraintTrainTests
    {
        private const float Tolerance = 0.0001f;
        private Scene previousActive;
        private Scene testScene;

        [SetUp]
        public void SetUp()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(scene.path) && scene.isDirty)
                    Assert.Ignore("Save or discard the untitled scene before testing.");
            }
            previousActive = SceneManager.GetActiveScene();
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(testScene);
        }

        [TearDown]
        public void TearDown()
        {
            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);
            if (testScene.IsValid()) EditorSceneManager.CloseScene(testScene, true);
            Undo.ClearAll();
        }

        [Test]
        public void RootVariantPathSitsBesideTheModel()
        {
            Assert.That(ConstraintTrainPrefabs.RootPrefabPath("Assets/Avatars/Riry/Riry.fbx"),
                Is.EqualTo("Assets/Avatars/Riry/Riry_ROOT.prefab"));
            Assert.That(ConstraintTrainPrefabs.RootPrefabPath("Assets/Model.FBX"), Is.EqualTo("Assets/Model_ROOT.prefab"));
            // A read-only package folder cannot take the variant.
            Assert.That(ConstraintTrainPrefabs.RootPrefabPath("Packages/com.vendor.avatar/Body.fbx"),
                Is.EqualTo("Assets/RiryTools/Body_ROOT.prefab"));
            Assert.That(ConstraintTrainPrefabs.IsGeneratedChain("Phys3_Tail1"), Is.True);
            Assert.That(ConstraintTrainPrefabs.IsGeneratedChain("Rest_Tail1"), Is.True);
            Assert.That(ConstraintTrainPrefabs.IsGeneratedChain("Physics"), Is.False);
        }

        [Test]
        public void GeneratedChainsAndConstraintsApplyToTheSourcePrefab()
        {
            string folder = AssetDatabase.CreateFolder("Assets", "RiryConstraintTrainTest");
            string folderPath = AssetDatabase.GUIDToAssetPath(folder);
            try
            {
                var source = new GameObject("Avatar").transform;
                Transform tail1 = Child(Child(source, "Hips", Vector3.up, 0f), "Tail1", Vector3.back, 0f);
                Child(tail1, "Tail2", Vector3.back, 0f);
                string prefabPath = folderPath + "/Avatar.prefab";
                PrefabUtility.SaveAsPrefabAsset(source.gameObject, prefabPath);
                Object.DestroyImmediate(source.gameObject);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), testScene);
                Transform bone = instance.transform.Find("Hips/Tail1");
                var real = new List<Transform>();
                string error;
                Assert.That(ConstraintTrainChains.TryCollectChain(bone, bone.Find("Tail2"), real, out error), Is.True, error);
                ConstraintTrainChains.CreateChain(real, ConstraintTrainChains.ChainPrefix(0), true, "Test");
                // A BoxCollider stands in for the VRC constraint, which needs the VRChat SDK.
                bone.gameObject.AddComponent<BoxCollider>();

                ConstraintTrainPrefabs.Changes changes = ConstraintTrainPrefabs.Collect(bone, component => component is BoxCollider);
                Assert.That(changes.Immutable, Is.False);
                Assert.That(changes.AssetPath, Is.EqualTo(prefabPath));
                Assert.That(changes.AddedChains.Count, Is.EqualTo(1));
                Assert.That(changes.AddedConstraints.Count, Is.EqualTo(1));
                ConstraintTrainPrefabs.ApplyToSource(changes, "Test");

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(asset.transform.Find("Hips/Phys1_Tail1/Phys1_Tail2/Phys1_Tail2_End"), Is.Not.Null);
                Assert.That(asset.transform.Find("Hips/Tail1").GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(ConstraintTrainPrefabs.Collect(bone, component => component is BoxCollider).Count, Is.EqualTo(0));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        // RB1..RB5 are bones 0..4. Source 0 is REST and source k is PBC1-k.
        [Test]
        public void EveryBoneStartsAtRest()
        {
            var settings = Example();
            for (int bone = 0; bone < 5; bone++)
                Assert.That(Weights(settings, bone, 0f)[0], Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void TipLeadsAndEachBoneFollowsOneStepLater()
        {
            var settings = Example();
            Assert.That(Weights(settings, 4, 0.1f)[1], Is.EqualTo(1f).Within(Tolerance), "RB5 at PBC1-1");
            Assert.That(Weights(settings, 4, 0.2f)[2], Is.EqualTo(1f).Within(Tolerance), "RB5 at PBC1-2");
            Assert.That(Weights(settings, 3, 0.2f)[1], Is.EqualTo(1f).Within(Tolerance), "RB4 at PBC1-1");
            Assert.That(Weights(settings, 2, 0.2f)[0], Is.EqualTo(1f).Within(Tolerance), "RB3 still at REST");
            Assert.That(Weights(settings, 3, 0.3f)[2], Is.EqualTo(1f).Within(Tolerance), "RB4 at PBC1-2");
        }

        [Test]
        public void EveryBoneReachesItsOwnChainBoneAtTrainEnd()
        {
            var settings = Example();
            for (int bone = 0; bone < 5; bone++)
                Assert.That(Weights(settings, bone, 0.5f)[bone + 1], Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void StepsBlendLinearlyBetweenTargets()
        {
            var settings = Example();
            float[] tip = Weights(settings, 4, 0.15f);
            Assert.That(tip[1], Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(tip[2], Is.EqualTo(0.5f).Within(Tolerance));
            float[] next = Weights(settings, 3, 0.15f);
            Assert.That(next[0], Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(next[1], Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(Weights(settings, 0, 0.45f)[0], Is.EqualTo(0.5f).Within(Tolerance), "RB1 last to leave");
        }

        [Test]
        public void ChainsBlendLinearlyAfterTrainEnd()
        {
            var settings = Example();
            float[] weights = Weights(settings, 2, 0.75f);
            Assert.That(weights[ConstraintTrainPlanner.ChainSource(2, 0)], Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(weights[ConstraintTrainPlanner.ChainSource(2, 1)], Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(Weights(settings, 2, 1f)[ConstraintTrainPlanner.ChainSource(2, 1)],
                Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void PowerCurveGivesThreeQuartersOfTheNextChainHalfway()
        {
            var settings = Example();
            settings.BlendCurve = ConstraintTrainPlanner.PowerCurve(
                ConstraintTrainPlanner.ExponentForMidpoint(0.75f));
            float[] weights = Weights(settings, 4, 0.75f);
            Assert.That(weights[ConstraintTrainPlanner.ChainSource(4, 1)], Is.EqualTo(0.75f).Within(0.01f));
        }

        [Test]
        public void ThreeChainsSplitTheRemainingRange()
        {
            var settings = Example();
            settings.ChainCount = 3;
            settings.TrainEnd = 1f / 3f;
            Assert.That(Weights(settings, 1, 2f / 3f)[ConstraintTrainPlanner.ChainSource(1, 1)],
                Is.EqualTo(1f).Within(Tolerance));
            float[] weights = Weights(settings, 1, 5f / 6f);
            Assert.That(weights[ConstraintTrainPlanner.ChainSource(1, 1)], Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(weights[ConstraintTrainPlanner.ChainSource(1, 2)], Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void OneChainHoldsAfterTheTrain()
        {
            var settings = Example();
            settings.ChainCount = 1;
            settings.TrainEnd = 1f;
            Assert.That(ConstraintTrainPlanner.Validate(settings), Is.Null);
            Assert.That(Weights(settings, 4, 1f)[5], Is.EqualTo(1f).Within(Tolerance));
            // The train now spans the whole range: RB1 leaves REST during the last step.
            Assert.That(Weights(settings, 0, 0.75f)[0], Is.EqualTo(1f).Within(Tolerance));
            Assert.That(Weights(settings, 0, 0.9f)[0], Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void WeightsStayInRangeAndSumToOne()
        {
            var settings = Example();
            settings.ChainCount = 4;
            settings.TrainEnd = 0.4f;
            settings.BlendCurve = ConstraintTrainPlanner.PowerCurve(2.5f);
            for (int bone = 0; bone < settings.BoneCount; bone++)
            {
                for (int step = 0; step <= 200; step++)
                {
                    float[] weights = Weights(settings, bone, step / 200f);
                    float sum = 0f;
                    foreach (float weight in weights)
                    {
                        Assert.That(weight, Is.InRange(0f, 1f));
                        sum += weight;
                    }
                    Assert.That(sum, Is.EqualTo(1f).Within(Tolerance));
                }
            }
        }

        [Test]
        public void BuiltCurvesFollowTheWeightsAndStayLeanWhenLinear()
        {
            var settings = Example();
            int breaks = ConstraintTrainPlanner.BreakTimes(settings).Count;
            AnimationCurve[] linear = ConstraintTrainPlanner.BuildCurves(settings, 3);
            foreach (AnimationCurve curve in linear) Assert.That(curve.length, Is.LessThanOrEqualTo(breaks));

            // The halfway-75% curve is nearly vertical where each blend starts.
            settings.BlendCurve = ConstraintTrainPlanner.PowerCurve(
                ConstraintTrainPlanner.ExponentForMidpoint(0.75f));
            settings.StepCurve = ConstraintTrainPlanner.PowerCurve(2f);
            AnimationCurve[] curves = ConstraintTrainPlanner.BuildCurves(settings, 3);
            for (int step = 0; step <= 1000; step++)
            {
                float t = step / 1000f;
                float[] weights = Weights(settings, 3, t);
                for (int s = 0; s < curves.Length; s++)
                    Assert.That(curves[s].Evaluate(t), Is.EqualTo(weights[s]).Within(0.01f), "t=" + t + " s=" + s);
            }
        }

        [Test]
        public void SourceWeightsUseTheOverflowListAfterSixteen()
        {
            Assert.That(ConstraintTrainPlanner.SourceWeightProperty(3), Is.EqualTo("Sources.source3.Weight"));
            Assert.That(ConstraintTrainPlanner.SourceWeightProperty(17),
                Is.EqualTo("Sources.overflowList.Array.data[1].Weight"));
        }

        [Test]
        public void GeneratedChainMatchesRealBonesAndAddsAnEndpoint()
        {
            var root = new GameObject("Hips").transform;
            root.localScale = new Vector3(2f, 2f, 2f);
            Transform tail1 = Child(root, "Tail1", new Vector3(0, 0.1f, 0.2f), 20f);
            Transform tail2 = Child(tail1, "Tail2", new Vector3(0, 0, 0.3f), -15f);
            Transform tail3 = Child(tail2, "Tail3", new Vector3(0.05f, 0, 0.25f), 30f);
            var real = new List<Transform>();
            string error;
            Assert.That(ConstraintTrainChains.TryCollectChain(tail1, tail3, real, out error), Is.True, error);

            Transform start = ConstraintTrainChains.CreateChain(real, ConstraintTrainChains.ChainPrefix(0), true, "Test");
            Assert.That(start.name, Is.EqualTo("Phys1_Tail1"));
            Assert.That(start.parent, Is.EqualTo(root));

            var generated = new List<Transform>();
            Assert.That(ConstraintTrainChains.TryFollowChain(start, real, generated, out error), Is.True, error);
            for (int i = 0; i < real.Count; i++)
            {
                Assert.That(generated[i].name, Is.EqualTo("Phys1_" + real[i].name));
                Assert.That(Vector3.Distance(generated[i].position, real[i].position), Is.LessThan(1e-5f));
                Assert.That(Quaternion.Angle(generated[i].rotation, real[i].rotation), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(generated[i].lossyScale, real[i].lossyScale), Is.LessThan(1e-5f));
            }

            Transform endpoint = generated[2].Find("Phys1_Tail3_End");
            Assert.That(endpoint, Is.Not.Null);
            Vector3 expected = tail3.position + (tail3.position - tail2.position);
            Assert.That(Vector3.Distance(endpoint.position, expected), Is.LessThan(1e-5f));
            Assert.That(ConstraintTrainChains.FindExisting(real, "Phys1_"), Is.EqualTo(start));
        }

        [Test]
        public void ChainLookupRejectsAnEndOutsideTheStart()
        {
            var a = new GameObject("A").transform;
            var b = new GameObject("B").transform;
            string error;
            Assert.That(ConstraintTrainChains.TryCollectChain(a, b, new List<Transform>(), out error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void ChainFollowingPicksTheChildNamedAfterTheRealBone()
        {
            var root = new GameObject("Root").transform;
            Transform real1 = Child(root, "Tail1", Vector3.forward, 0f);
            Transform real2 = Child(real1, "Tail2", Vector3.forward, 0f);
            Transform phys1 = Child(root, "Phys1_Tail1", Vector3.forward, 0f);
            Child(phys1, "Accessory", Vector3.up, 0f);
            Transform phys2 = Child(phys1, "Phys1_Tail2", Vector3.forward, 0f);
            var chain = new List<Transform>();
            string error;
            Assert.That(ConstraintTrainChains.TryFollowChain(phys1, new[] { real1, real2 }, chain, out error),
                Is.True, error);
            Assert.That(chain[1], Is.EqualTo(phys2));
        }

        [Test]
        public void NonFiniteCurvesFallBackToLinear()
        {
            var broken = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, float.NaN), new Keyframe(1, 1));
            Assert.That(ConstraintTrainPlanner.Shape(broken, 0.5f), Is.EqualTo(0.5f).Within(Tolerance));
            var infinite = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, float.PositiveInfinity));
            Assert.That(ConstraintTrainPlanner.Shape(infinite, 0.25f), Is.EqualTo(0.25f).Within(Tolerance));
            var bounce = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 3), new Keyframe(1, 1));
            Assert.That(ConstraintTrainPlanner.Shape(bounce, 0.5f), Is.InRange(0f, 1f));
        }

        [Test]
        public void AnimationPathsRejectSlashesAndShadowedNames()
        {
            var root = new GameObject("Avatar").transform;
            Transform armature = Child(root, "Armature", Vector3.zero, 0f);
            Transform good = Child(armature, "Tail", Vector3.zero, 0f);
            Assert.That(ConstraintTrainChains.AnimationPathError(good, root), Is.Null);

            Transform slashed = Child(armature, "Tail/Tip", Vector3.zero, 0f);
            Assert.That(ConstraintTrainChains.AnimationPathError(slashed, root), Does.Contain("'/'"));

            // The second "Tail" can never be reached by the path "Armature/Tail".
            Transform shadowed = Child(armature, "Tail", Vector3.zero, 0f);
            Assert.That(ConstraintTrainChains.AnimationPathError(shadowed, root), Does.Contain("earlier sibling"));
            Assert.That(ConstraintTrainChains.AnimationPathError(good, root), Is.Null);
        }

        [Test]
        public void ExistingChainsAreFoundByExactNameEvenWithSlashes()
        {
            var root = new GameObject("Hips").transform;
            Transform real = Child(root, "Tail/1", Vector3.zero, 0f);
            Transform generated = Child(root, "Phys1_Tail/1", Vector3.zero, 0f);
            Assert.That(ConstraintTrainChains.FindExisting(new[] { real }, "Phys1_"), Is.EqualTo(generated));
            Assert.That(ConstraintTrainChains.FindExisting(new[] { real }, "Phys2_"), Is.Null);
        }

        [Test]
        public void ChainFollowingPrefersTheStartsOwnPrefix()
        {
            var root = new GameObject("Root").transform;
            Transform real1 = Child(root, "A", Vector3.forward, 0f);
            Transform real2 = Child(real1, "B", Vector3.forward, 0f);
            Transform phys1 = Child(root, "Phys1_A", Vector3.forward, 0f);
            Child(phys1, "Phys2_B", Vector3.forward, 0f);
            Transform own = Child(phys1, "Phys1_B", Vector3.forward, 0f);
            var chain = new List<Transform>();
            string error;
            Assert.That(ConstraintTrainChains.TryFollowChain(phys1, new[] { real1, real2 }, chain, out error),
                Is.True, error);
            Assert.That(chain[1], Is.EqualTo(own));
        }

        [Test]
        public void SharedTargetsAndOuterAnimatorAreDetected()
        {
            var avatar = new GameObject("Avatar", typeof(Animator)).transform;
            Transform prop = Child(avatar, "Prop", Vector3.zero, 0f);
            prop.gameObject.AddComponent<Animator>();
            Transform bone = Child(prop, "Bone", Vector3.zero, 0f);
            Assert.That(ConstraintTrainChains.FindAvatarRoot(bone), Is.EqualTo(avatar));

            var first = new List<Transform> { prop, bone };
            var second = new List<Transform> { avatar, bone };
            Assert.That(ConstraintTrainChains.FindSharedTarget(new[] { first, second }), Is.EqualTo(bone));
            Assert.That(ConstraintTrainChains.FindSharedTarget(new[] { first }), Is.Null);
        }

        [Test]
        public void ShortChainFailsInsteadOfUsingItsEndpoint()
        {
            var root = new GameObject("Root").transform;
            Transform real1 = Child(root, "A", Vector3.forward, 0f);
            Transform real2 = Child(real1, "B", Vector3.forward, 0f);
            Transform real3 = Child(real2, "C", Vector3.forward, 0f);
            Transform phys1 = Child(root, "Phys1_A", Vector3.forward, 0f);
            Transform phys2 = Child(phys1, "Phys1_B", Vector3.forward, 0f);
            Child(phys2, "Phys1_B_End", Vector3.forward, 0f);
            string error;
            Assert.That(ConstraintTrainChains.TryFollowChain(phys1, new[] { real1, real2, real3 },
                new List<Transform>(), out error), Is.False);
            Assert.That(error, Does.Contain("Phys1_B"));
        }

        [Test]
        public void ChainGenerationNeedsAParentForTheStart()
        {
            var lone = new GameObject("Tail1").transform;
            Assert.Throws<System.InvalidOperationException>(() =>
                ConstraintTrainChains.CreateChain(new[] { lone }, "Phys1_", true, "Test"));
        }

        private static ConstraintTrainPlanner.Settings Example()
        {
            return new ConstraintTrainPlanner.Settings
            {
                BoneCount = 5,
                ChainCount = 2,
                TrainEnd = 0.5f,
                StepCurve = AnimationCurve.Linear(0, 0, 1, 1),
                BlendCurve = AnimationCurve.Linear(0, 0, 1, 1)
            };
        }

        private static float[] Weights(ConstraintTrainPlanner.Settings settings, int bone, float t)
        {
            var weights = new float[ConstraintTrainPlanner.SourceCount(bone, settings.ChainCount)];
            ConstraintTrainPlanner.EvaluateWeights(settings, bone, t, weights);
            return weights;
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition, float angle)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.Euler(angle, angle * 0.5f, 0f);
            return child;
        }
    }
}
#endif
