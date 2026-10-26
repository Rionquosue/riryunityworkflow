#if UNITY_EDITOR

// Target editor version: Unity 2022.3.22f1.

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>
    /// Removes inactive blendshape curves and static bone transform curves from
    /// the AnimationClip chosen by the command's invocation context. Its menu
    /// entries are in Editor/UI/RiryMenus.cs; the result window and the Animation
    /// window button are in Editor/UI/CleanAnimationKeysUI.cs.
    /// </summary>
    internal static class RemoveBlankShapeKeys
    {
        internal const string DisplayName = "Clean Animation Keys";

        private const string BlendShapePrefix = "blendShape.";
        private const int BoneValueDecimalPlaces = 3;

        private enum CandidateKind
        {
            BlendShape,
            BoneTransform
        }

        private sealed class CurveRecord
        {
            internal readonly EditorCurveBinding Binding;
            internal readonly AnimationCurve Curve;

            internal CurveRecord(EditorCurveBinding binding, AnimationCurve curve)
            {
                Binding = binding;
                Curve = curve;
            }
        }

        private sealed class CandidateGroup
        {
            internal readonly CandidateKind Kind;
            internal readonly string Label;
            internal readonly string TransformFamily;
            internal readonly List<CurveRecord> Curves = new List<CurveRecord>();

            internal CandidateGroup(
                CandidateKind kind,
                string label,
                string transformFamily)
            {
                Kind = kind;
                Label = label;
                TransformFamily = transformFamily;
            }
        }

        internal static void RunFromMainMenu()
        {
            AnimationWindow animationWindow =
                EditorWindow.focusedWindow as AnimationWindow;

            if (animationWindow != null)
            {
                RunFromAnimationWindow(animationWindow);
                return;
            }

            AnimationClip clip = Selection.activeObject as AnimationClip;

            if (clip == null)
            {
                EditorUtility.DisplayDialog(
                    DisplayName,
                    "Focus an Animation window with an active clip, or select " +
                    "an AnimationClip in the Project window first.",
                    "OK");
                return;
            }

            Run(clip, "Project window selection");
        }

        internal static bool ValidateMainMenuCommand()
        {
            AnimationWindow animationWindow =
                EditorWindow.focusedWindow as AnimationWindow;

            if (animationWindow != null)
            {
                return animationWindow.animationClip != null;
            }

            return Selection.activeObject is AnimationClip;
        }

        internal sealed class WhitelistItem
        {
            internal readonly string Label;
            internal readonly string Reason;
            internal readonly EditorCurveBinding[] Bindings;

            internal WhitelistItem(
                string label,
                string reason,
                EditorCurveBinding[] bindings)
            {
                Label = label;
                Reason = reason;
                Bindings = bindings;
            }
        }

        internal sealed class AnalysisResult
        {
            internal int FloatCurveCount;
            internal int FloatKeyCount;
            internal int ObjectCurveCount;
            internal int ObjectKeyCount;
            internal int AnimationEventCount;
            internal int OtherFloatCurveCount;
            internal int RemovedGroupCount;

            internal readonly List<EditorCurveBinding> RemovableBindings =
                new List<EditorCurveBinding>();

            internal readonly List<WhitelistItem> Whitelist =
                new List<WhitelistItem>();
        }

        internal static void RunFromProjectWindow()
        {
            AnimationClip clip = Selection.activeObject as AnimationClip;

            if (clip == null)
            {
                EditorUtility.DisplayDialog(
                    DisplayName,
                    "Select an AnimationClip in the Project window first.",
                    "OK");
                return;
            }

            Run(clip, "Project window selection");
        }

        internal static bool ValidateProjectWindowCommand()
        {
            return Selection.activeObject is AnimationClip;
        }

        internal static void RunFromAnimationWindow(AnimationWindow animationWindow)
        {
            AnimationClip clip = animationWindow != null
                ? animationWindow.animationClip
                : null;

            if (clip == null)
            {
                EditorUtility.DisplayDialog(
                    DisplayName,
                    "The Animation window has no active AnimationClip.",
                    "OK");
                return;
            }

            Run(clip, "Animation window");
        }

        private static void Run(AnimationClip clip, string sourceLabel)
        {
            string validationError;
            if (!TryValidateEditableClip(clip, out validationError))
            {
                EditorUtility.DisplayDialog(
                    DisplayName,
                    validationError,
                    "OK");
                return;
            }

            AnalysisResult result;

            try
            {
                result = Analyze(clip);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    DisplayName,
                    "The clip could not be analyzed.\n\n" + exception.Message,
                    "OK");
                return;
            }

            int initiallyRemovedBindingCount = result.RemovableBindings.Count;

            if (initiallyRemovedBindingCount > 0)
            {
                string removalError;
                if (!TryRemoveBindings(
                        clip,
                        result.RemovableBindings,
                        "Remove Blank Animation Keys",
                        out removalError))
                {
                    EditorUtility.DisplayDialog(
                        DisplayName,
                        removalError,
                        "OK");
                    return;
                }
            }

            result.RemovableBindings.Clear();
            RepaintAnimationWindows();
            EditorApplication.RepaintProjectWindow();

            RemoveBlankShapeKeysResultWindow.ShowResult(
                clip,
                sourceLabel,
                result,
                initiallyRemovedBindingCount);
        }

        internal static AnalysisResult Analyze(AnimationClip clip)
        {
            AnalysisResult result = new AnalysisResult();
            Dictionary<string, CandidateGroup> groups =
                new Dictionary<string, CandidateGroup>(StringComparer.Ordinal);

            EditorCurveBinding[] floatBindings =
                AnimationUtility.GetCurveBindings(clip);

            result.FloatCurveCount = floatBindings.Length;

            for (int i = 0; i < floatBindings.Length; i++)
            {
                EditorCurveBinding binding = floatBindings[i];
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

                if (curve != null)
                {
                    result.FloatKeyCount += curve.length;
                }

                string groupKey;
                CandidateGroup group;

                if (IsBlendShape(binding))
                {
                    groupKey = BuildGroupKey(
                        "shape",
                        binding.path,
                        binding.type,
                        binding.propertyName);

                    if (!groups.TryGetValue(groupKey, out group))
                    {
                        string shapeName = binding.propertyName.Substring(
                            BlendShapePrefix.Length);

                        group = new CandidateGroup(
                            CandidateKind.BlendShape,
                            "Blendshape: " + DisplayPath(binding.path) +
                            " / " + shapeName,
                            null);

                        groups.Add(groupKey, group);
                    }
                }
                else
                {
                    string transformFamily;
                    if (!TryGetTransformFamily(binding, out transformFamily))
                    {
                        result.OtherFloatCurveCount++;
                        continue;
                    }

                    groupKey = BuildGroupKey(
                        "bone",
                        binding.path,
                        binding.type,
                        transformFamily);

                    if (!groups.TryGetValue(groupKey, out group))
                    {
                        group = new CandidateGroup(
                            CandidateKind.BoneTransform,
                            "Bone: " + DisplayPath(binding.path) +
                            " / " + transformFamily,
                            transformFamily);

                        groups.Add(groupKey, group);
                    }
                }

                group.Curves.Add(new CurveRecord(binding, curve));
            }

            EditorCurveBinding[] objectBindings =
                AnimationUtility.GetObjectReferenceCurveBindings(clip);

            result.ObjectCurveCount = objectBindings.Length;

            for (int i = 0; i < objectBindings.Length; i++)
            {
                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(
                        clip,
                        objectBindings[i]);

                if (keys != null)
                {
                    result.ObjectKeyCount += keys.Length;
                }
            }

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            result.AnimationEventCount = events != null ? events.Length : 0;

            List<CandidateGroup> orderedGroups =
                new List<CandidateGroup>(groups.Values);

            orderedGroups.Sort(delegate(CandidateGroup left, CandidateGroup right)
            {
                return string.Compare(
                    left.Label,
                    right.Label,
                    StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < orderedGroups.Count; i++)
            {
                CandidateGroup candidate = orderedGroups[i];
                string whitelistReason;

                bool whitelisted = candidate.Kind == CandidateKind.BlendShape
                    ? ShouldWhitelistBlendShape(candidate, out whitelistReason)
                    : ShouldWhitelistBoneTransform(candidate, out whitelistReason);

                EditorCurveBinding[] bindings = GetBindings(candidate);

                if (whitelisted)
                {
                    result.Whitelist.Add(new WhitelistItem(
                        candidate.Label,
                        whitelistReason,
                        bindings));
                }
                else
                {
                    result.RemovedGroupCount++;
                    result.RemovableBindings.AddRange(bindings);
                }
            }

            return result;
        }

        private static bool ShouldWhitelistBlendShape(
            CandidateGroup candidate,
            out string reason)
        {
            float largestValue = 0.0f;
            bool containsNonZeroValue = false;

            for (int curveIndex = 0;
                 curveIndex < candidate.Curves.Count;
                 curveIndex++)
            {
                AnimationCurve curve = candidate.Curves[curveIndex].Curve;
                if (curve == null)
                {
                    // A curve that cannot be read was never checked, so keep it.
                    reason = "The curve could not be read; preserved for safety.";
                    return true;
                }

                Keyframe[] keys = curve.keys;

                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    float value = keys[keyIndex].value;

                    if (!IsFinite(value) || !IsFinite(keys[keyIndex].time))
                    {
                        reason = "Contains NaN or Infinity; preserved for safety.";
                        return true;
                    }

                    // Negative weights also deform the mesh.
                    if (value != 0.0f)
                    {
                        containsNonZeroValue = true;
                        if (Math.Abs(value) > Math.Abs(largestValue))
                        {
                            largestValue = value;
                        }
                    }
                }

                if (!containsNonZeroValue && MovesBetweenKeys(keys))
                {
                    reason = "Keys are 0, but curve tangents move the value " +
                        "between keys.";
                    return true;
                }
            }

            if (containsNonZeroValue)
            {
                reason = "Contains a non-zero value (largest " +
                    largestValue.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) + ").";
                return true;
            }

            reason = null;
            return false;
        }

        private static bool ShouldWhitelistBoneTransform(
            CandidateGroup candidate,
            out string reason)
        {
            HashSet<float> keyedTimes = new HashSet<float>();
            bool endpointChanged = false;

            for (int curveIndex = 0;
                 curveIndex < candidate.Curves.Count;
                 curveIndex++)
            {
                AnimationCurve curve = candidate.Curves[curveIndex].Curve;
                if (curve == null)
                {
                    // A curve that cannot be read was never checked, so keep it.
                    reason = "The curve could not be read; preserved for safety.";
                    return true;
                }

                Keyframe[] keys = curve.keys;

                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    Keyframe key = keys[keyIndex];

                    if (!IsFinite(key.time) || !IsFinite(key.value))
                    {
                        reason = "Contains NaN or Infinity; preserved for safety.";
                        return true;
                    }

                    keyedTimes.Add(key.time);
                }

                if (keys.Length >= 2 &&
                    RoundBoneValue(keys[0].value) !=
                    RoundBoneValue(keys[keys.Length - 1].value))
                {
                    endpointChanged = true;
                }

                if (!endpointChanged && MovesBetweenKeys(keys))
                {
                    reason = candidate.TransformFamily +
                        " start and finish match, but curve tangents move " +
                        "the value between keys.";
                    return true;
                }
            }

            if (keyedTimes.Count > 2)
            {
                reason = candidate.TransformFamily + " has " +
                    keyedTimes.Count.ToString(CultureInfo.InvariantCulture) +
                    " keyed times, not only a start and finish.";
                return true;
            }

            if (endpointChanged)
            {
                reason = "Start and finish values differ after rounding to " +
                    BoneValueDecimalPlaces.ToString(
                        CultureInfo.InvariantCulture) +
                    " decimal places.";
                return true;
            }

            reason = null;
            return false;
        }

        /// <summary>
        /// True when a tangent bends the curve away from its key values inside
        /// the clip. Infinite tangents are stepped and hold the key value; the
        /// first key's in-tangent and last key's out-tangent are never evaluated.
        /// </summary>
        private static bool MovesBetweenKeys(Keyframe[] keys)
        {
            for (int i = 0; i < keys.Length - 1; i++)
            {
                float outTangent = keys[i].outTangent;
                float inTangent = keys[i + 1].inTangent;

                if ((IsFinite(outTangent) && outTangent != 0.0f) ||
                    (IsFinite(inTangent) && inTangent != 0.0f))
                {
                    return true;
                }
            }

            return false;
        }

        private static EditorCurveBinding[] GetBindings(CandidateGroup candidate)
        {
            EditorCurveBinding[] bindings =
                new EditorCurveBinding[candidate.Curves.Count];

            for (int i = 0; i < candidate.Curves.Count; i++)
            {
                bindings[i] = candidate.Curves[i].Binding;
            }

            return bindings;
        }

        private static bool IsBlendShape(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer) &&
                   binding.propertyName != null &&
                   binding.propertyName.StartsWith(
                       BlendShapePrefix,
                       StringComparison.Ordinal);
        }

        private static bool TryGetTransformFamily(
            EditorCurveBinding binding,
            out string family)
        {
            family = null;

            if (binding.type != typeof(Transform) ||
                string.IsNullOrEmpty(binding.propertyName))
            {
                return false;
            }

            string propertyName = binding.propertyName;

            if (propertyName.StartsWith(
                    "m_LocalPosition.",
                    StringComparison.Ordinal))
            {
                family = "Translation";
                return true;
            }

            if (propertyName.StartsWith(
                    "m_LocalScale.",
                    StringComparison.Ordinal))
            {
                family = "Scale";
                return true;
            }

            if (propertyName.StartsWith(
                    "m_LocalRotation.",
                    StringComparison.Ordinal) ||
                propertyName.StartsWith(
                    "localEulerAngles.",
                    StringComparison.Ordinal) ||
                propertyName.StartsWith(
                    "localEulerAnglesRaw.",
                    StringComparison.Ordinal) ||
                propertyName.StartsWith(
                    "localEulerAnglesBaked.",
                    StringComparison.Ordinal) ||
                propertyName.StartsWith(
                    "m_LocalEulerAnglesHint.",
                    StringComparison.Ordinal))
            {
                family = "Rotation";
                return true;
            }

            return false;
        }

        private static string BuildGroupKey(
            string category,
            string path,
            Type type,
            string propertyGroup)
        {
            return category + "\n" +
                   (path ?? string.Empty) + "\n" +
                   (type != null ? type.AssemblyQualifiedName : string.Empty) +
                   "\n" + (propertyGroup ?? string.Empty);
        }

        private static string DisplayPath(string path)
        {
            return string.IsNullOrEmpty(path) ? "<root>" : path;
        }

        private static double RoundBoneValue(float value)
        {
            return Math.Round(
                (double)value,
                BoneValueDecimalPlaces,
                MidpointRounding.AwayFromZero);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryValidateEditableClip(
            AnimationClip clip,
            out string error)
        {
            if (clip == null)
            {
                error = "No AnimationClip was selected.";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(clip);

            if (string.IsNullOrEmpty(assetPath))
            {
                error = "The selected clip is not a saved asset.";
                return false;
            }

            if (!AssetDatabase.IsNativeAsset(clip) ||
                (clip.hideFlags & HideFlags.NotEditable) != 0)
            {
                error =
                    "The selected clip is imported or read-only and cannot be " +
                    "edited directly. Extract or duplicate it as a native .anim " +
                    "asset first.\n\n" + assetPath;
                return false;
            }

            string editMessage;
            if (!AssetDatabase.IsOpenForEdit(clip, out editMessage))
            {
                error = string.IsNullOrEmpty(editMessage)
                    ? "The selected clip is not open for editing.\n\n" + assetPath
                    : editMessage + "\n\n" + assetPath;
                return false;
            }

            error = null;
            return true;
        }

        internal static bool TryRemoveBindings(
            AnimationClip clip,
            IList<EditorCurveBinding> bindings,
            string undoName,
            out string error)
        {
            if (bindings == null || bindings.Count == 0)
            {
                error = null;
                return true;
            }

            if (!TryValidateEditableClip(clip, out error))
            {
                return false;
            }

            EditorCurveBinding[] bindingArray =
                new EditorCurveBinding[bindings.Count];
            AnimationCurve[] originalCurves =
                new AnimationCurve[bindings.Count];
            AnimationCurve[] emptyCurves =
                new AnimationCurve[bindings.Count];

            for (int i = 0; i < bindings.Count; i++)
            {
                bindingArray[i] = bindings[i];
                originalCurves[i] = AnimationUtility.GetEditorCurve(
                    clip,
                    bindingArray[i]);
            }

            Undo.RegisterCompleteObjectUndo(clip, undoName);

            try
            {
                AnimationUtility.SetEditorCurves(
                    clip,
                    bindingArray,
                    emptyCurves);

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                string rollbackMessage = string.Empty;

                try
                {
                    AnimationUtility.SetEditorCurves(
                        clip,
                        bindingArray,
                        originalCurves);

                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssetIfDirty(clip);
                }
                catch (Exception rollbackException)
                {
                    rollbackMessage =
                        "\n\nRollback also failed: " + rollbackException.Message;
                }

                Debug.LogException(exception);
                error = "The curves could not be removed.\n\n" +
                        exception.Message + rollbackMessage;
                return false;
            }
        }

        internal static void RepaintAnimationWindows()
        {
            AnimationWindow[] windows =
                Resources.FindObjectsOfTypeAll<AnimationWindow>();

            for (int i = 0; i < windows.Length; i++)
            {
                windows[i].Repaint();
            }
        }
    }

}

#endif
