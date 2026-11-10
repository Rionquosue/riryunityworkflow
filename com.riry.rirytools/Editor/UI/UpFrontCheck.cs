using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Everything up front: before anything is changed, a test finds the possible issues of
    /// this run, each with what to do about it, and asks every question the run needs
    /// (SkinnedMeshRenderers, MeshRenderers, the VRC constraint upgrade). Continue runs the
    /// copy with the answers; Abort stops with nothing changed.
    /// </summary>
    internal sealed class AvatarUpFrontCheck : EditorWindow
    {
        private sealed class Question
        {
            internal string Text;
            internal bool Answer;
            internal Action<AvatarUpFrontAnswers, bool> Apply;
            // Shown and applied only while this returns true (a follow-up to another question); null: always.
            internal Func<bool> Visible;

            internal bool Shown
            {
                get { return Visible == null || Visible(); }
            }
        }

        private readonly List<string> issues = new List<string>();
        private readonly List<Question> questions = new List<Question>();
        private bool accepted;
        private Vector2 scroll;

        /// <summary>Shows the check and waits; false when aborted.</summary>
        internal static bool Run(AvatarMigrationMap map, AvatarMigrationSettings settings, HashSet<Transform> ignored, out AvatarUpFrontAnswers answers)
        {
            answers = new AvatarUpFrontAnswers();
            var check = CreateInstance<AvatarUpFrontCheck>();
            check.titleContent = new GUIContent(AvatarMigrationAssistant.Title + ": before copying");
            check.Test(map, settings, ignored);
            if (check.issues.Count == 0 && check.questions.Count == 0)
            {
                DestroyImmediate(check);
                return true;
            }
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            check.position = new Rect(main.center.x - 300f, main.center.y - 220f, 600f, 440f);
            check.minSize = new Vector2(480f, 300f);
            check.ShowModalUtility();
            if (!check.accepted) return false;
            foreach (Question question in check.questions) question.Apply(answers, question.Shown && question.Answer);
            return true;
        }

        private void Test(AvatarMigrationMap map, AvatarMigrationSettings settings, HashSet<Transform> ignored)
        {
            Transform source = map.SourceRoot;
            var scope = new AvatarPrefabScope(source);
            Func<Transform, bool> inScope = t => !ignored.Contains(t) && scope.ChildPrefabRoot(t) == null;

            // Types that need the VRChat SDK.
            if (!AvatarMigrationAssistant.SdkPresent)
            {
                var skipped = new List<string>();
                foreach (AvatarCopyType type in AvatarCopyTypes.All)
                {
                    if (type.NeedsSdk && settings.Type(type).Enabled) skipped.Add(type.Label);
                }
                if (skipped.Count > 0)
                    issues.Add("VRC SDK not detected: " + string.Join(", ", skipped) + " will be skipped. Install the VRChat Avatars SDK to copy them.");
            }

            // Unpacked prefabs, found through their skinned meshes.
            var repackProblems = new List<string>();
            List<AvatarRepack.Group> repack = AvatarRepack.Plan(map, inScope, repackProblems);
            foreach (string problem in repackProblems) issues.Add(problem);
            if (repack.Count > 0)
            {
                var models = new List<string>();
                foreach (AvatarRepack.Group group in repack)
                    models.Add(AnimationUtility.CalculateTransformPath(group.SourceRoot, source) + " (" + System.IO.Path.GetFileName(group.ModelPath) + ")");
                questions.Add(new Question
                {
                    Text = repack.Count + " unpacked prefab(s) on the old avatar have no counterpart on the new one: " + string.Join(", ", models) +
                        ". Place each model as a child prefab on the new avatar, with the old avatar's changes as overrides? (No: they " +
                        "and the components on them are not copied.)",
                    Answer = true,
                    Apply = (answers, yes) => answers.RepackPrefabs = yes
                });
            }

            // Objects of unpacked prefabs get their match when the prefab is placed again, so they don't count as unmatched.
            Func<Transform, bool> repacked = t => repack.Exists(group => t == group.SourceRoot || t.IsChildOf(group.SourceRoot));

            // Components on objects that have no match and will not be created.
            foreach (AvatarCopyType type in AvatarCopyTypes.All)
            {
                AvatarTypeSettings typeSettings = settings.Type(type);
                if (!typeSettings.Enabled || typeSettings.CreateMissing || type.Rule == AvatarCopyTypes.PrefabsRule ||
                    type.Rule == AvatarCopyTypes.TransformsRule || type.Rule == AvatarCopyTypes.SkinnedMeshRenderersRule) continue;
                int count = 0;
                foreach (Type componentType in type.Types)
                {
                    foreach (Component component in source.GetComponentsInChildren(componentType, true))
                    {
                        if (inScope(component.transform) && map.TargetOf(component.transform) == null && !repacked(component.transform)) count++;
                    }
                }
                if (count > 0)
                    issues.Add(count + " " + type.Label + " are on objects with no match on the target and will not be copied. Turn on Create missing " +
                        "objects for " + type.Label + ", or match the objects in Manual matching.");
            }

            // Child prefabs.
            List<Transform> prefabs = scope.OutermostChildPrefabs();
            if (prefabs.Count > 0 && !settings.RuleEnabled(AvatarCopyTypes.PrefabsRule))
                issues.Add(prefabs.Count + " child prefab" + (prefabs.Count == 1 ? " is" : "s are") + " not copied, and their parts are never copied " +
                    "separately. Turn on Prefabs to copy them.");
            foreach (Transform prefab in prefabs)
            {
                if (!ignored.Contains(prefab) && AvatarPrefabScope.Asset(prefab) == null)
                    issues.Add("Prefab " + AnimationUtility.CalculateTransformPath(prefab, source) + " has a missing prefab asset and will not be copied. " +
                        "Restore the asset, or copy it by hand.");
            }

            // Rest pose, for the constraint rotation correction.
            if (AnyConstraintEnabled(settings))
            {
                var moved = new List<string>();
                RestPose(source, moved);
                RestPose(map.TargetRoot, moved);
                if (moved.Count > 0)
                    issues.Add("These humanoid bones are not in their rest pose, so constraint rotations may be corrected wrongly: " +
                        string.Join(", ", moved) + ". Reset the pose (revert the bones' rotation overrides) and run again.");
            }

            // Questions.
            Question skinned = null;
            if (settings.RuleEnabled(AvatarCopyTypes.SkinnedMeshRenderersRule) && Count<SkinnedMeshRenderer>(source, inScope) > 0)
            {
                skinned = new Question
                {
                    Text = "Copy SkinnedMeshRenderer settings, materials and bounds? (" + Count<SkinnedMeshRenderer>(source, inScope) +
                        " found. No copies only their blendshape values.)",
                    Apply = (answers, yes) => answers.CopySkinnedMeshRenderers = yes
                };
                questions.Add(skinned);
            }
            // Bounds are only copied with the settings, so this follows that question's Yes.
            if (skinned != null && settings.SkinnedBounds)
            {
                int changed = 0;
                foreach (SkinnedMeshRenderer renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Transform match = inScope(renderer.transform) ? map.TargetOf(renderer.transform) : null;
                    var other = match != null ? match.GetComponent<SkinnedMeshRenderer>() : null;
                    if (other != null && other.sharedMesh != renderer.sharedMesh) changed++;
                }
                if (changed > 0)
                    questions.Add(new Question
                    {
                        Text = changed + " SkinnedMeshRenderer(s) have a different mesh on the target. Copy their old bounds anyway? " +
                            "(No keeps the new mesh's own bounds.)",
                        Visible = () => skinned.Answer,
                        Apply = (answers, yes) => answers.CopyChangedBounds = yes
                    });
            }
            int meshCount = 0;
            foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponent<MeshFilter>() != null && inScope(renderer.transform)) meshCount++;
            }
            if (settings.RuleEnabled(AvatarCopyTypes.MeshRenderersRule) && meshCount > 0)
                questions.Add(new Question
                {
                    Text = "Copy MeshRenderers with their MeshFilters? (" + meshCount + " found.)",
                    Apply = (answers, yes) => answers.CopyMeshRenderers = yes
                });
            if (AvatarMigrationAssistant.SdkPresent && UnityConstraintCount(source, settings, inScope) > 0)
                questions.Add(new Question
                {
                    Text = "Upgrade to VRCConstraints on target?",
                    Apply = (answers, yes) => answers.UpgradeConstraints = yes
                });
        }

        private static int Count<T>(Transform root, Func<Transform, bool> inScope) where T : Component
        {
            int count = 0;
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (inScope(component.transform)) count++;
            }
            return count;
        }

        private static int UnityConstraintCount(Transform root, AvatarMigrationSettings settings, Func<Transform, bool> inScope)
        {
            int count = 0;
            foreach (AvatarCopyType type in AvatarCopyTypes.All)
            {
                if (type.Rule != AvatarCopyTypes.UnityConstraintRule || !settings.Type(type).Enabled) continue;
                foreach (Type componentType in type.Types)
                {
                    foreach (Component component in root.GetComponentsInChildren(componentType, true))
                    {
                        if (inScope(component.transform)) count++;
                    }
                }
            }
            return count;
        }

        private static bool AnyConstraintEnabled(AvatarMigrationSettings settings)
        {
            foreach (AvatarCopyType type in AvatarCopyTypes.All)
            {
                bool constraint = type.Rule == AvatarCopyTypes.UnityConstraintRule ||
                    type.TypeNames.Exists(name => name.StartsWith(AvatarCopyTypes.VrcConstraintPrefix, StringComparison.Ordinal));
                if (constraint && settings.Type(type).Enabled) return true;
            }
            return false;
        }

        // Humanoid bones whose local rotation differs from their prefab source.
        private static void RestPose(Transform root, List<string> moved)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (!animator.isHuman || animator.avatar == null) continue;
                for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
                {
                    Transform transform = animator.GetBoneTransform(bone);
                    var original = transform != null ? PrefabUtility.GetCorrespondingObjectFromSource(transform) : null;
                    if (original == null) continue;
                    if (Quaternion.Angle(transform.localRotation, original.localRotation) > 0.01f) moved.Add(root.name + " " + bone);
                }
                return;
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (issues.Count > 0)
            {
                EditorGUILayout.LabelField("Possible issues", EditorStyles.boldLabel);
                foreach (string issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
            if (questions.Count > 0)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Questions", EditorStyles.boldLabel);
                // Which follow-up questions show is fixed for this event, so an answer changed now shows them from the next
                // one (the layout and the drawing then see the same controls).
                foreach (Question question in questions.FindAll(item => item.Shown))
                {
                    EditorGUILayout.LabelField(question.Text, EditorStyles.wordWrappedLabel);
                    question.Answer = GUILayout.Toolbar(question.Answer ? 0 : 1, new[] { "Yes", "No" }, GUILayout.Width(160f)) == 0;
                    EditorGUILayout.Space(4f);
                }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Abort", GUILayout.Width(90f))) Close();
                if (GUILayout.Button("Continue", GUILayout.Width(90f)))
                {
                    accepted = true;
                    Close();
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant up-front check (UpFrontCheck.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarUpFrontAnswers       CopySkinnedMeshRenderers, CopyChangedBounds, RepackPrefabs, CopyMeshRenderers, UpgradeConstraints.
// AvatarUpFrontCheck (this file)
//   Run(map, settings, ignored, out answers)  Modal; skipped when there is nothing to say. Possible issues (each with what
//                              to do): VRChat types without the SDK; components on unmatched objects with Create missing
//                              objects off; child prefabs with Prefabs off or a missing asset; humanoid bones out of rest
//                              pose (RestPose) when constraints are copied; unpacked prefabs that can't be placed again.
//                              Questions (Yes/No, default No): place unpacked prefabs as child prefabs (AvatarRepack.Plan;
//                              default Yes), SkinnedMeshRenderer settings, old bounds for changed meshes (shown and applied
//                              only while the settings question is Yes: Question.Visible / Shown), MeshRenderers,
//                              "Upgrade to VRCConstraints on target?". Continue / Abort.
