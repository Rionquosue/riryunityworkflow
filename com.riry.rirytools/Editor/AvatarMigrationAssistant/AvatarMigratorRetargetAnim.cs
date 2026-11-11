using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 8, Retarget animations: the target's controllers (Avatar Descriptor playable
    /// layers that are not default, Animator components, and VRCFury in the child prefabs the
    /// user included) and every clip they use are duplicated once into the migrated folder,
    /// the clips' paths are rewritten through the map (each path is resolved on the source,
    /// its match found, and the path rebuilt on the target), the duplicated controllers'
    /// motions are pointed at the duplicated clips, and the target is pointed at the
    /// duplicates with Undo. The original files are never changed. Paths that resolve to no
    /// source object are listed in Manual matching first.
    /// </summary>
    internal sealed partial class AvatarAnimationRetarget
    {
        /// <summary>One controller in use: where its paths start on each avatar, and how to point the target at its duplicate.</summary>
        private sealed class Use
        {
            internal RuntimeAnimatorController Controller;
            internal Transform SourceRoot;
            internal Transform TargetRoot;
            internal Action<RuntimeAnimatorController> Assign;
        }

        private readonly AvatarMigrationMap map;
        private readonly AvatarMigratorSettings settings;
        private readonly AvatarMigratorRunReport report;
        private readonly HashSet<Transform> includedPrefabs;
        private readonly List<Use> uses = new List<Use>();
        // Original controller or clip to its duplicate.
        private readonly Dictionary<Object, Object> duplicates = new Dictionary<Object, Object>();
        private Dictionary<string, Transform> chosenTargets = new Dictionary<string, Transform>(StringComparer.Ordinal);

        private AvatarAnimationRetarget(AvatarMigrationMap map, AvatarMigratorSettings settings, HashSet<Transform> includedPrefabs, AvatarMigratorRunReport report)
        {
            this.map = map;
            this.settings = settings;
            this.includedPrefabs = includedPrefabs;
            this.report = report;
        }

        /// <summary>Retargets the target's animations; false when cancelled in Manual matching (nothing changed).</summary>
        internal static bool Run(AvatarMigrationMap map, AvatarMigratorSettings settings, HashSet<Transform> includedPrefabs, AvatarMigratorRunReport report)
        {
            var retarget = new AvatarAnimationRetarget(map, settings, includedPrefabs, report);
            retarget.Collect();
            var unresolved = new List<string>(retarget.Unresolved());
            if (unresolved.Count > 0)
            {
                Dictionary<string, Transform> chosen;
                if (!AvatarMigrationMatchWindow.Review(map, false, unresolved, out chosen)) return false;
                retarget.chosenTargets = chosen;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(AvatarMigrationAssistant.Title + ": Retarget animations");
            try
            {
                retarget.Step("Retargeting controllers", retarget.RetargetControllers);
                retarget.Step("VRCFury in child prefabs", retarget.RetargetVrcFury);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(group);
            }
            return true;
        }

        private void Step(string name, Action step)
        {
            EditorUtility.DisplayProgressBar(AvatarMigrationAssistant.Title, name, 0.5f);
            try
            {
                step();
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.AvatarMigration, name + " failed", exception, map.TargetRoot.gameObject);
                report.Failures.Add(name + ": " + exception.Message);
            }
        }

        // ---- Controllers in use -----------------------------------------------------

        private void Collect()
        {
            Transform targetRoot = map.TargetRoot;
            Type descriptorType = RiryToolsTypes.Named(AvatarMigratorCopyTypes.DescriptorType);
            Component descriptor = descriptorType != null ? targetRoot.GetComponent(descriptorType) : null;
            if (descriptor != null)
            {
                foreach (string list in new[] { "baseAnimationLayers", "specialAnimationLayers" })
                {
                    SerializedProperty layers = new SerializedObject(descriptor).FindProperty(list);
                    if (layers == null || !layers.isArray)
                    {
                        report.NotCopied.Add("Avatar Descriptor field '" + list + "' was not found in this SDK, so its controllers were not retargeted.");
                        continue;
                    }
                    bool reported = false;
                    for (int i = 0; i < layers.arraySize; i++)
                    {
                        SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                        SerializedProperty isDefault = layer.FindPropertyRelative("isDefault");
                        SerializedProperty controller = layer.FindPropertyRelative("animatorController");
                        if (controller == null && !reported)
                        {
                            reported = true;
                            report.NotCopied.Add("Avatar Descriptor field '" + list + ".animatorController' was not found in this SDK, " +
                                "so those controllers were not retargeted.");
                        }
                        if (controller == null || (isDefault != null && isDefault.boolValue)) continue;
                        var used = controller.objectReferenceValue as RuntimeAnimatorController;
                        if (used == null) continue;
                        string path = controller.propertyPath;
                        uses.Add(new Use
                        {
                            Controller = used, SourceRoot = map.SourceRoot, TargetRoot = targetRoot,
                            Assign = duplicate =>
                            {
                                var serialized = new SerializedObject(descriptor);
                                serialized.FindProperty(path).objectReferenceValue = duplicate;
                                serialized.ApplyModifiedProperties();
                            }
                        });
                    }
                }
            }
            var scope = new AvatarPrefabScope(targetRoot);
            foreach (Animator animator in targetRoot.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null) continue;
                Transform prefab = scope.ChildPrefabRoot(animator.transform);
                if (prefab != null && !includedPrefabs.Contains(prefab)) continue;
                Animator owner = animator;
                uses.Add(new Use
                {
                    Controller = animator.runtimeAnimatorController, SourceRoot = map.SourceOf(animator.transform), TargetRoot = animator.transform,
                    Assign = duplicate =>
                    {
                        Undo.RecordObject(owner, AvatarMigrationAssistant.Title);
                        owner.runtimeAnimatorController = duplicate;
                    }
                });
            }
        }

        // Paths in the clips that resolve to no source object and none on the target.
        private IEnumerable<string> Unresolved()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Use use in uses)
            {
                foreach (AnimationClip clip in use.Controller.animationClips)
                {
                    if (clip == null) continue;
                    foreach (string path in Paths(clip))
                    {
                        string rebuilt;
                        if (!NewPath(path, use.SourceRoot, use.TargetRoot, out rebuilt) && seen.Add(path)) yield return path;
                    }
                }
            }
        }

        private static IEnumerable<string> Paths(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)) yield return binding.path;
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) yield return binding.path;
        }

        /// <summary>
        /// A clip path rebuilt for the target: resolved on the source and mapped, else one the user chose
        /// in Manual matching; a path already on the target stays. False when it resolves nowhere.
        /// </summary>
        private bool NewPath(string path, Transform sourceRoot, Transform targetRoot, out string rebuilt)
        {
            rebuilt = path;
            Transform source = sourceRoot == null ? null : path.Length == 0 ? sourceRoot : sourceRoot.Find(path);
            Transform target = source != null ? map.TargetOf(source) : null;
            Transform chosen;
            if (target == null && chosenTargets.TryGetValue(path, out chosen)) target = chosen;
            if (target != null && (target == targetRoot || target.IsChildOf(targetRoot)))
            {
                rebuilt = AnimationUtility.CalculateTransformPath(target, targetRoot);
                return true;
            }
            return (path.Length == 0 ? targetRoot : targetRoot.Find(path)) != null;
        }

        // ---- Duplicating and rewriting ----------------------------------------------

        private void RetargetControllers()
        {
            foreach (Use use in uses)
            {
                var duplicate = DuplicateController(use.Controller, use.SourceRoot, use.TargetRoot);
                if (duplicate != null) use.Assign(duplicate);
            }
        }

        /// <summary>The controller's duplicate, made once with its clips repathed and its motions relinked.</summary>
        private RuntimeAnimatorController DuplicateController(RuntimeAnimatorController original, Transform sourceRoot, Transform targetRoot)
        {
            Object done;
            if (duplicates.TryGetValue(original, out done)) return done as RuntimeAnimatorController;
            string path = AssetDatabase.GetAssetPath(original);
            if (string.IsNullOrEmpty(path))
            {
                report.NotCopied.Add("Controller " + original.name + " is not an asset, so it was not retargeted.");
                return null;
            }
            string destination = AvatarMigratorMigratedFolder.Destination(settings.MigratedFolder, path);
            if (!AssetDatabase.CopyAsset(path, destination))
            {
                report.Failures.Add("Could not duplicate " + path + " to " + destination + ".");
                return null;
            }
            report.FilesCreated.Add(destination);
            var copy = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(destination);
            duplicates[original] = copy;

            var controller = copy as AnimatorController;
            if (controller != null)
            {
                // Clips stored inside the controller were duplicated with it and are repathed in place.
                foreach (Object part in AssetDatabase.LoadAllAssetsAtPath(destination))
                {
                    var embedded = part as AnimationClip;
                    if (embedded != null) Repath(embedded, sourceRoot, targetRoot);
                }
                foreach (AnimatorControllerLayer layer in controller.layers) Relink(layer.stateMachine, sourceRoot, targetRoot, destination);
                EditorUtility.SetDirty(controller);
            }
            var overrides = copy as AnimatorOverrideController;
            if (overrides != null)
            {
                RuntimeAnimatorController baseController = overrides.runtimeAnimatorController != null
                    ? DuplicateController(overrides.runtimeAnimatorController, sourceRoot, targetRoot)
                    : null;
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrides.GetOverrides(pairs);
                var relinked = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                if (baseController != null) overrides.runtimeAnimatorController = baseController;
                foreach (KeyValuePair<AnimationClip, AnimationClip> pair in pairs)
                {
                    AnimationClip key = pair.Key != null ? DuplicateClip(pair.Key, sourceRoot, targetRoot, destination) : null;
                    AnimationClip value = pair.Value != null ? DuplicateClip(pair.Value, sourceRoot, targetRoot, destination) : null;
                    if (key != null) relinked.Add(new KeyValuePair<AnimationClip, AnimationClip>(key, value));
                }
                overrides.ApplyOverrides(relinked);
                EditorUtility.SetDirty(overrides);
            }
            return copy;
        }

        // Every state's motion and every blend tree child in the state machine, pointed at duplicated clips.
        private void Relink(AnimatorStateMachine machine, Transform sourceRoot, Transform targetRoot, string controllerPath)
        {
            if (machine == null) return;
            foreach (ChildAnimatorState child in machine.states)
            {
                child.state.motion = RelinkMotion(child.state.motion, sourceRoot, targetRoot, controllerPath);
                EditorUtility.SetDirty(child.state);
            }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines) Relink(child.stateMachine, sourceRoot, targetRoot, controllerPath);
        }

        /// <summary>
        /// The motion pointed at duplicates. A blend tree inside the duplicated controller (controllerPath) is relinked in
        /// place, once; one stored anywhere else (a standalone asset, another controller) is never changed: it is copied into
        /// the duplicated controller and the copy is relinked.
        /// </summary>
        private Motion RelinkMotion(Motion motion, Transform sourceRoot, Transform targetRoot, string controllerPath)
        {
            var clip = motion as AnimationClip;
            if (clip != null) return DuplicateClip(clip, sourceRoot, targetRoot, controllerPath);
            var original = motion as BlendTree;
            if (original == null) return motion;
            Object done;
            if (duplicates.TryGetValue(original, out done)) return done as BlendTree;
            BlendTree tree = original;
            if (AssetDatabase.GetAssetPath(original) != controllerPath)
            {
                tree = Object.Instantiate(original);
                tree.name = original.name;
                tree.hideFlags = original.hideFlags;
                AssetDatabase.AddObjectToAsset(tree, controllerPath);
                duplicates[original] = tree;
            }
            duplicates[tree] = tree;
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++) children[i].motion = RelinkMotion(children[i].motion, sourceRoot, targetRoot, controllerPath);
            tree.children = children;
            EditorUtility.SetDirty(tree);
            return tree;
        }

        /// <summary>
        /// A clip's duplicate, made once and repathed. A clip already inside the duplicated
        /// controller (controllerPath) is the duplicate; one inside a model is saved as a .anim.
        /// </summary>
        private AnimationClip DuplicateClip(AnimationClip original, Transform sourceRoot, Transform targetRoot, string controllerPath)
        {
            string path = AssetDatabase.GetAssetPath(original);
            if (path == controllerPath) return original;
            Object done;
            if (duplicates.TryGetValue(original, out done)) return done as AnimationClip;
            if (string.IsNullOrEmpty(path)) return original;
            string destination;
            AnimationClip copy;
            if (AssetDatabase.IsMainAsset(original) && path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                destination = AvatarMigratorMigratedFolder.Destination(settings.MigratedFolder, path);
                if (!AssetDatabase.CopyAsset(path, destination))
                {
                    report.Failures.Add("Could not duplicate " + path + " to " + destination + ".");
                    return original;
                }
                copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
            }
            else
            {
                string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                destination = AvatarMigratorMigratedFolder.Destination(settings.MigratedFolder, folder + "/" + original.name + ".anim");
                copy = Object.Instantiate(original);
                copy.name = original.name;
                AssetDatabase.CreateAsset(copy, destination);
            }
            report.FilesCreated.Add(destination);
            duplicates[original] = copy;
            // A tree reached twice meets its duplicated clips again; they are already the duplicates.
            duplicates[copy] = copy;
            Repath(copy, sourceRoot, targetRoot);
            return copy;
        }

        private const string BlendshapePrefix = "blendShape.";
        // Clip, path and blendshape already reported.
        private readonly HashSet<string> reportedCurves = new HashSet<string>(StringComparer.Ordinal);

        // A blendshape curve whose name the mesh at its new path doesn't have animates nothing: reported once each.
        private void CheckBlendshapeCurve(AnimationClip clip, EditorCurveBinding binding, string path, Transform targetRoot)
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(BlendshapePrefix, StringComparison.Ordinal)) return;
            Transform at = path.Length == 0 ? targetRoot : targetRoot.Find(path);
            var renderer = at != null ? at.GetComponent<SkinnedMeshRenderer>() : null;
            if (renderer == null || renderer.sharedMesh == null) return;
            string shape = binding.propertyName.Substring(BlendshapePrefix.Length);
            if (renderer.sharedMesh.GetBlendShapeIndex(shape) >= 0 || !reportedCurves.Add(clip.name + "|" + path + "|" + shape)) return;
            report.PossibleIssues.Add("Animation " + clip.name + " animates blendshape " + shape + " on " + path + ", which the new mesh (" +
                renderer.sharedMesh.name + ") doesn't have.");
        }

        /// <summary>Rewrites the clip's binding paths for the target; old bindings are removed, new ones written.</summary>
        internal void Repath(AnimationClip clip, Transform sourceRoot, Transform targetRoot)
        {
            var floats = new List<KeyValuePair<EditorCurveBinding, AnimationCurve>>();
            var references = new List<KeyValuePair<EditorCurveBinding, ObjectReferenceKeyframe[]>>();
            var removeFloats = new List<EditorCurveBinding>();
            var removeReferences = new List<EditorCurveBinding>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                string rebuilt;
                if (!NewPath(binding.path, sourceRoot, targetRoot, out rebuilt))
                {
                    report.NotCopied.Add("Animation path " + binding.path + " in " + clip.name + " has no target; it was left as it is.");
                    continue;
                }
                CheckBlendshapeCurve(clip, binding, rebuilt, targetRoot);
                if (rebuilt == binding.path) continue;
                EditorCurveBinding moved = binding;
                moved.path = rebuilt;
                floats.Add(new KeyValuePair<EditorCurveBinding, AnimationCurve>(moved, AnimationUtility.GetEditorCurve(clip, binding)));
                removeFloats.Add(binding);
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                string rebuilt;
                if (!NewPath(binding.path, sourceRoot, targetRoot, out rebuilt))
                {
                    report.NotCopied.Add("Animation path " + binding.path + " in " + clip.name + " has no target; it was left as it is.");
                    continue;
                }
                if (rebuilt == binding.path) continue;
                EditorCurveBinding moved = binding;
                moved.path = rebuilt;
                references.Add(new KeyValuePair<EditorCurveBinding, ObjectReferenceKeyframe[]>(moved, AnimationUtility.GetObjectReferenceCurve(clip, binding)));
                removeReferences.Add(binding);
            }
            if (removeFloats.Count + removeReferences.Count == 0) return;
            foreach (EditorCurveBinding binding in removeFloats) AnimationUtility.SetEditorCurve(clip, binding, null);
            foreach (EditorCurveBinding binding in removeReferences) AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            foreach (KeyValuePair<EditorCurveBinding, AnimationCurve> curve in floats) AnimationUtility.SetEditorCurve(clip, curve.Key, curve.Value);
            foreach (KeyValuePair<EditorCurveBinding, ObjectReferenceKeyframe[]> curve in references)
                AnimationUtility.SetObjectReferenceCurve(clip, curve.Key, curve.Value);
            EditorUtility.SetDirty(clip);
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant animations (AvatarMigratorRetargetAnim.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarAnimationRetarget (this file; VRCFury in AvatarMigratorVRCFuryMover.cs)
//   Run(map, settings, includedPrefabs, report)  Collect, list unresolved paths in Manual matching (Cancel: nothing
//                        changed), then one Undo group: RetargetControllers, RetargetVrcFury, SaveAssets.
//   Collect()            Descriptor baseAnimationLayers and specialAnimationLayers not set to default (paths from the avatar
//                        root); Animator components (paths from the Animator), not inside a child prefab unless included.
//   NewPath(path, ...)   Resolved on the source and mapped; else the target chosen in Manual matching; a path already on
//                        the target stays.
//   DuplicateController  AssetDatabase.CopyAsset into the migrated folder once; embedded clips repathed in place; states and
//                        blend tree children relinked (Relink, RelinkMotion); override controllers get a duplicated base
//                        and duplicated clips (ApplyOverrides).
//   RelinkMotion         A blend tree in the duplicated controller is relinked in place, once; one stored elsewhere (a
//                        standalone asset, another controller) is copied into the duplicated controller and the copy
//                        relinked, so the source's trees are never changed.
//   DuplicateClip        Once per clip: a .anim copied, a clip inside a model saved as a new .anim; then Repath.
//   Repath(clip, ...)    GetCurveBindings / GetObjectReferenceCurveBindings rewritten: old removed, new written.
//   CheckBlendshapeCurve A blendShape.<name> curve whose mesh at the new path lacks that blendshape is reported, once each.
//   duplicates           Original controller, clip or blend tree to its duplicate (each duplicate also maps to itself, so
//                        a tree reached twice is not duplicated again); the target is pointed at duplicates with Undo.
