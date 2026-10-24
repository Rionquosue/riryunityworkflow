#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor.Tests
{
    public sealed class CleanAnimationKeysTests
    {
        private AnimationClip clip;

        [SetUp]
        public void SetUp()
        {
            clip = new AnimationClip();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void ZeroBlendShapeCurveIsRemoved()
        {
            Shape(new Keyframe(0, 0), new Keyframe(1, 0));
            AssertRemovable(1, 0);
        }

        [Test]
        public void NegativeBlendShapeValueIsPreserved()
        {
            Shape(new Keyframe(0, 0), new Keyframe(1, -30));
            AssertRemovable(0, 1);
        }

        [Test]
        public void BlendShapeTangentsThatLeaveZeroArePreserved()
        {
            Shape(new Keyframe(0, 0, 0, 50), new Keyframe(1, 0, -50, 0));
            AssertRemovable(0, 1);
        }

        [Test]
        public void SteppedZeroBlendShapeIsRemoved()
        {
            Shape(new Keyframe(0, 0, float.PositiveInfinity, float.PositiveInfinity),
                new Keyframe(1, 0, float.PositiveInfinity, float.PositiveInfinity));
            AssertRemovable(1, 0);
        }

        [Test]
        public void StaticTwoKeyBoneCurveIsRemoved()
        {
            Bone("m_LocalPosition.x", new Keyframe(0, 0.5f), new Keyframe(1, 0.5f));
            AssertRemovable(1, 0);
        }

        [Test]
        public void BoneEndpointsThatDifferArePreserved()
        {
            Bone("m_LocalPosition.x", new Keyframe(0, 0), new Keyframe(1, 1));
            AssertRemovable(0, 1);
        }

        [Test]
        public void BoneEndpointsEqualAfterRoundingAreRemoved()
        {
            Bone("m_LocalScale.y", new Keyframe(0, 1.0001f), new Keyframe(1, 1.0002f));
            AssertRemovable(1, 0);
        }

        [Test]
        public void BoneWithThreeKeyedTimesIsPreserved()
        {
            Bone("m_LocalPosition.x", new Keyframe(0, 0), new Keyframe(0.5f, 0), new Keyframe(1, 0));
            AssertRemovable(0, 1);
        }

        [Test]
        public void StaticBoneWithBendingTangentsIsPreserved()
        {
            Bone("m_LocalPosition.x", new Keyframe(0, 0.5f, 0, 2), new Keyframe(1, 0.5f, -2, 0));
            AssertRemovable(0, 1);
        }

        [Test]
        public void OtherFloatCurvesAreLeftUntouched()
        {
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Body", typeof(MeshRenderer), "m_Enabled"),
                new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0)));
            RemoveBlankShapeKeys.AnalysisResult result = RemoveBlankShapeKeys.Analyze(clip);
            Assert.That(result.OtherFloatCurveCount, Is.EqualTo(1));
            Assert.That(result.RemovableBindings, Is.Empty);
        }

        private void Shape(params Keyframe[] keys)
        {
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile"),
                new AnimationCurve(keys));
        }

        private void Bone(string property, params Keyframe[] keys)
        {
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Armature/Hips", typeof(Transform), property),
                new AnimationCurve(keys));
        }

        private void AssertRemovable(int removable, int preserved)
        {
            RemoveBlankShapeKeys.AnalysisResult result = RemoveBlankShapeKeys.Analyze(clip);
            Assert.That(result.RemovableBindings.Count, Is.EqualTo(removable));
            Assert.That(result.Whitelist.Count, Is.EqualTo(preserved));
        }
    }
}
#endif
