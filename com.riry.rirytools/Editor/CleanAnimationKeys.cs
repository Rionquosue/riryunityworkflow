using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Removes blank blendshape curves and static bone transform curves from
    /// animations that key every bone and shapekey. What is kept is decided by the
    /// options (the built-in rules), the whitelist (names), and, always, curves that
    /// cannot be read or hold NaN or Infinity. The window is in
    /// Editor/UI/CleanAnimationKeysWindow.cs; its menu entries are in Editor/UI/RiryMenus.cs.
    /// </summary>
    internal static class CleanAnimationKeys
    {
        internal const string DisplayName = "Clean Animation Keys";

        private const string BlendshapePrefix = "blendShape.";
        private const int BoneValueDecimalPlaces = 3;

        /// <summary>The built-in keep rules, each a checkbox in the window.</summary>
        [Serializable]
        internal sealed class Options
        {
            public bool KeepNonZeroShapekeys = true;
            public bool KeepShapekeyTangentMotion = true;
            public bool KeepMovingBones = true;
            public bool KeepBoneMidKeys = true;
        }

        /// <summary>The curves of one shapekey, or of one bone's translation, rotation or scale.</summary>
        internal sealed class Group
        {
            internal readonly bool IsBlendshape;
            // The shapekey name, or the bone's own name (the last part of its path): what the whitelist matches.
            internal readonly string Name;
            internal readonly List<EditorCurveBinding> Bindings = new List<EditorCurveBinding>();
            internal readonly List<AnimationCurve> Curves = new List<AnimationCurve>();

            internal Group(bool isBlendshape, string name)
            {
                IsBlendshape = isBlendshape;
                Name = name;
            }

            internal string Label
            {
                get { return (IsBlendshape ? "Shapekey: " : "Bone: ") + Name; }
            }
        }

        /// <summary>Every shapekey and bone group keyed in the clip.</summary>
        internal static List<Group> Groups(AnimationClip clip)
        {
            var groups = new Dictionary<string, Group>(StringComparer.Ordinal);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                string key;
                Group group;
                if (IsBlendshape(binding))
                {
                    key = "shape\n" + binding.path + "\n" + binding.propertyName;
                    if (!groups.TryGetValue(key, out group))
                        groups.Add(key, group = new Group(true, binding.propertyName.Substring(BlendshapePrefix.Length)));
                }
                else
                {
                    string family;
                    if (!TryGetTransformFamily(binding, out family)) continue;
                    key = "bone\n" + binding.path + "\n" + family;
                    if (!groups.TryGetValue(key, out group))
                        groups.Add(key, group = new Group(false, BoneName(binding.path)));
                }
                group.Bindings.Add(binding);
                group.Curves.Add(AnimationUtility.GetEditorCurve(clip, binding));
            }
            return new List<Group>(groups.Values);
        }

        /// <summary>The groups Clean would remove from the clip.</summary>
        internal static List<Group> Removable(AnimationClip clip, Options options, ICollection<string> whitelist)
        {
            var removable = new List<Group>();
            foreach (Group group in Groups(clip))
            {
                if (whitelist.Contains(group.Name)) continue;
                if (group.IsBlendshape ? KeepBlendshape(group, options) : KeepBone(group, options)) continue;
                removable.Add(group);
            }
            return removable;
        }

        /// <summary>
        /// Cleans every editable clip as one Undo step. Imported and read-only clips
        /// are skipped. Returns the summary line; throws when curves cannot be removed.
        /// </summary>
        internal static string Clean(IList<AnimationClip> clips, Options options, ICollection<string> whitelist)
        {
            int removedCurves = 0, cleaned = 0, skipped = 0;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(DisplayName);
            foreach (AnimationClip clip in clips)
            {
                string error;
                if (!TryValidateEditableClip(clip, out error))
                {
                    skipped++;
                    continue;
                }
                var bindings = new List<EditorCurveBinding>();
                foreach (Group group in Removable(clip, options, whitelist)) bindings.AddRange(group.Bindings);
                if (!TryRemoveBindings(clip, bindings, DisplayName, out error)) throw new InvalidOperationException(error);
                removedCurves += bindings.Count;
                cleaned++;
            }
            Undo.CollapseUndoOperations(undoGroup);
            RepaintAnimationWindows();
            EditorApplication.RepaintProjectWindow();
            return "Removed " + removedCurves + " curves from " + cleaned + (cleaned == 1 ? " animation." : " animations.") +
                (skipped > 0 ? " Skipped " + skipped + " imported or read-only " + (skipped == 1 ? "animation." : "animations.") : "");
        }

        // A curve that cannot be read, or holds NaN or Infinity, was never checked, so it is always kept.
        private static bool Unsafe(Group group)
        {
            foreach (AnimationCurve curve in group.Curves)
            {
                if (curve == null) return true;
                foreach (Keyframe key in curve.keys)
                {
                    if (!IsFinite(key.time) || !IsFinite(key.value)) return true;
                }
            }
            return false;
        }

        private static bool KeepBlendshape(Group group, Options options)
        {
            if (Unsafe(group)) return true;
            bool nonZero = false, tangentsMove = false;
            foreach (AnimationCurve curve in group.Curves)
            {
                Keyframe[] keys = curve.keys;
                // Negative weights also deform the mesh.
                foreach (Keyframe key in keys) nonZero |= key.value != 0.0f;
                tangentsMove |= MovesBetweenKeys(keys);
            }
            return (options.KeepNonZeroShapekeys && nonZero) ||
                (options.KeepShapekeyTangentMotion && !nonZero && tangentsMove);
        }

        private static bool KeepBone(Group group, Options options)
        {
            if (Unsafe(group)) return true;
            var keyedTimes = new HashSet<float>();
            bool endpointChanged = false, tangentsMove = false;
            foreach (AnimationCurve curve in group.Curves)
            {
                Keyframe[] keys = curve.keys;
                foreach (Keyframe key in keys) keyedTimes.Add(key.time);
                if (keys.Length >= 2 && RoundBoneValue(keys[0].value) != RoundBoneValue(keys[keys.Length - 1].value))
                    endpointChanged = true;
                tangentsMove |= MovesBetweenKeys(keys);
            }
            return (options.KeepMovingBones && endpointChanged) ||
                (options.KeepBoneMidKeys && (keyedTimes.Count > 2 || (!endpointChanged && tangentsMove)));
        }

        private static string BoneName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "<root>";
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
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

        private static bool IsBlendshape(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer) &&
                   binding.propertyName != null &&
                   binding.propertyName.StartsWith(
                       BlendshapePrefix,
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
