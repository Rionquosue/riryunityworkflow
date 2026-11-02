using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// One Avatar Migration Assistant run: copies the chosen component types from the
    /// source avatar to the target in run order (AvatarCopyTypes.All), then re-points
    /// references (RepointReferences.cs), corrects rotation (CorrectRotation.cs) and
    /// scale (CorrectScale.cs), and drops duplicates of components the target already had.
    /// The whole run is one Undo group; the source is never written to. Every step has its
    /// own try/catch, so one failure is logged and the rest goes on. Split into parts by
    /// what they do: this file runs the steps and copies components generically.
    /// </summary>
    internal sealed partial class AvatarCopyRun
    {
        private readonly AvatarMigrationMap map;
        private readonly AvatarMigrationSettings settings;
        private readonly AvatarUpFrontAnswers answers;
        private readonly Transform sourceRoot;
        private readonly Transform targetRoot;
        private readonly HashSet<Transform> ignored;
        private readonly AvatarPrefabScope scope;

        // Source component to the target component it was copied into.
        private readonly Dictionary<Component, Component> copied = new Dictionary<Component, Component>();
        // Components this run added to the target.
        private readonly List<Component> made = new List<Component>();
        // Objects of the temporary constraint copy, to the source objects they copy (UpgradeConstraints.cs).
        private readonly Dictionary<Object, Object> alias = new Dictionary<Object, Object>();

        private readonly AvatarRunReport report = new AvatarRunReport();

        private AvatarCopyRun(AvatarMigrationMap map, AvatarMigrationSettings settings, AvatarUpFrontAnswers answers, HashSet<Transform> ignored)
        {
            this.map = map;
            this.settings = settings;
            this.answers = answers;
            this.ignored = ignored;
            sourceRoot = map.SourceRoot;
            targetRoot = map.TargetRoot;
            scope = new AvatarPrefabScope(sourceRoot);
        }

        /// <summary>Runs the copy and shows the report. Returns the report.</summary>
        internal static AvatarRunReport Execute(AvatarMigrationMap map, AvatarMigrationSettings settings, AvatarUpFrontAnswers answers,
            HashSet<Transform> ignored)
        {
            var run = new AvatarCopyRun(map, settings, answers, ignored);
            run.Run();
            return run.report;
        }

        private void Run()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(AvatarMigrationAssistant.Title);
            Undo.RegisterFullObjectHierarchyUndo(targetRoot.gameObject, AvatarMigrationAssistant.Title);
            try
            {
                foreach (AvatarCopyType type in AvatarCopyTypes.All)
                {
                    AvatarTypeSettings typeSettings = settings.Type(type);
                    if (typeSettings.Enabled && type.Available && (!type.NeedsSdk || AvatarMigrationAssistant.SdkPresent))
                        Step(type.Label, () => CopyStep(type, typeSettings));
                    // Active state, tag and layer (Transforms sub-options) have their place after Cameras.
                    if (type.Rule == AvatarCopyTypes.CamerasRule) Step("Active state, tag and layer", CopyActiveTagLayer);
                }
                Step("Re-pointing references", RepointReferences);
                Step("Correcting rotation", CorrectRotation);
                Step("Correcting scale", CorrectScale);
                Step("Removing duplicates", RemoveDuplicates);
            }
            finally
            {
                Step("Removing the temporary constraint copy", RemoveConstraintCopy);
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(targetRoot.gameObject);
                EditorSceneManager.MarkSceneDirty(targetRoot.gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            report.Copied = copied.Count;
        }

        // One step: logged and reported on failure, the run goes on.
        private void Step(string name, Action step)
        {
            EditorUtility.DisplayProgressBar(AvatarMigrationAssistant.Title, name, 0.5f);
            try
            {
                step();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                report.Failures.Add(name + ": " + exception.Message);
            }
        }

        private void CopyStep(AvatarCopyType type, AvatarTypeSettings typeSettings)
        {
            switch (type.Rule)
            {
                case AvatarCopyTypes.PrefabsRule:
                    CopyPrefabs(typeSettings);
                    return;
                case AvatarCopyTypes.TransformsRule:
                    CopyTransforms(typeSettings);
                    return;
                case AvatarCopyTypes.MeshRenderersRule:
                    if (answers.CopyMeshRenderers) CopyMeshRenderers(typeSettings);
                    return;
                case AvatarCopyTypes.SkinnedMeshRenderersRule:
                    CopySkinnedMeshRenderers();
                    return;
                case AvatarCopyTypes.DescriptorRule:
                    CopyAvatarDescriptor();
                    return;
            }
            if (type.Rule == AvatarCopyTypes.UnityConstraintRule && answers.UpgradeConstraints)
            {
                CopyUpgradedConstraints(type, typeSettings);
                return;
            }
            foreach (Type component in type.Types) CopyComponents(type, typeSettings, component);
        }

        // ---- Generic copying ------------------------------------------------------

        /// <summary>Copies every source component of the type, after clearing the target's when Replace is on.</summary>
        private void CopyComponents(AvatarCopyType type, AvatarTypeSettings typeSettings, Type componentType)
        {
            if (typeSettings.Replace) ClearTarget(componentType);
            foreach (Component source in sourceRoot.GetComponentsInChildren(componentType, true))
            {
                if (!InScope(source.transform) || !TypeRuleAllows(type, source)) continue;
                Transform target = TargetFor(source.transform, typeSettings.CreateMissing);
                if (target == null)
                {
                    report.NotCopied.Add(Describe(source) + ": its object has no match on the target (Create missing objects is off).");
                    continue;
                }
                if (!TargetRuleAllows(type, source, target)) continue;
                CopyComponent(source, target.gameObject);
            }
        }

        /// <summary>
        /// Copies one component's values through Unity's component clipboard onto the target
        /// object: into the existing one for types Unity allows once per object, else into a
        /// new one. Its references are re-pointed later.
        /// </summary>
        private Component CopyComponent(Component source, GameObject target)
        {
            Type type = source.GetType();
            Component destination = OncePerObject(type) ? target.GetComponent(type) : null;
            if (destination == null)
            {
                destination = Undo.AddComponent(target, type);
                if (destination == null)
                {
                    report.NotCopied.Add(Describe(source) + ": Unity did not allow adding it to " + target.name + ".");
                    return null;
                }
                made.Add(destination);
            }
            else Undo.RecordObject(destination, AvatarMigrationAssistant.Title);
            if (!ComponentUtility.CopyComponent(source) || !ComponentUtility.PasteComponentValues(destination))
            {
                report.NotCopied.Add(Describe(source) + ": its values could not be copied.");
                return destination;
            }
            copied[source] = destination;
            // A particle system's renderer is a separate component that comes with it.
            var particles = source as ParticleSystem;
            if (particles != null)
            {
                var sourceRenderer = source.GetComponent<ParticleSystemRenderer>();
                var targetRenderer = target.GetComponent<ParticleSystemRenderer>();
                if (sourceRenderer != null && targetRenderer != null && ComponentUtility.CopyComponent(sourceRenderer))
                {
                    Undo.RecordObject(targetRenderer, AvatarMigrationAssistant.Title);
                    ComponentUtility.PasteComponentValues(targetRenderer);
                    copied[sourceRenderer] = targetRenderer;
                }
            }
            return destination;
        }

        // Types Unity allows only once per GameObject: "add" pastes into the existing one.
        private static bool OncePerObject(Type type)
        {
            if (typeof(Transform).IsAssignableFrom(type) || typeof(Rigidbody).IsAssignableFrom(type) || typeof(Renderer).IsAssignableFrom(type) ||
                typeof(MeshFilter).IsAssignableFrom(type) || typeof(Animator).IsAssignableFrom(type) || typeof(ParticleSystem).IsAssignableFrom(type))
                return true;
            for (Type current = type; current != null && current != typeof(Component); current = current.BaseType)
            {
                if (current.GetCustomAttributes(typeof(DisallowMultipleComponent), false).Length > 0) return true;
            }
            return false;
        }

        /// <summary>Replace: removes the target's components of the type, never the source's.</summary>
        private void ClearTarget(Type componentType)
        {
            if (typeof(Transform).IsAssignableFrom(componentType)) return;
            foreach (Component existing in targetRoot.GetComponentsInChildren(componentType, true))
            {
                if (existing == null || made.Contains(existing)) continue;
                try
                {
                    Undo.DestroyObjectImmediate(existing);
                }
                catch (Exception exception)
                {
                    report.NotCopied.Add(Describe(existing) + " on the target could not be removed for Replace: " + exception.Message);
                }
            }
        }

        // ---- Objects --------------------------------------------------------------

        /// <summary>
        /// False for objects the copiers leave alone: ignored ones, and objects inside a child
        /// prefab instance, which is copied whole by the Prefabs step instead (CopyPrefabs.cs).
        /// </summary>
        private bool InScope(Transform source)
        {
            return source != null && !ignored.Contains(source) && scope.ChildPrefabRoot(source) == null;
        }

        /// <summary>
        /// The target object for a source object: its match, or, with createMissing, a new
        /// object made for each missing step below the nearest matched parent (feature 4).
        /// </summary>
        private Transform TargetFor(Transform source, bool createMissing)
        {
            Transform target = map.TargetOf(source);
            if (target != null || !createMissing) return target;
            var missing = new List<Transform>();
            Transform current = source;
            while (current != null && map.TargetOf(current) == null)
            {
                missing.Add(current);
                current = current.parent;
            }
            if (current == null) return null;
            Transform parent = map.TargetOf(current);
            for (int i = missing.Count - 1; i >= 0; i--)
            {
                Transform step = missing[i];
                var created = new GameObject(step.name);
                Undo.RegisterCreatedObjectUndo(created, AvatarMigrationAssistant.Title);
                created.transform.SetParent(parent, false);
                created.transform.localPosition = step.localPosition;
                created.transform.localRotation = step.localRotation;
                created.transform.localScale = step.localScale;
                created.transform.SetSiblingIndex(Mathf.Min(step.GetSiblingIndex(), parent.childCount - 1));
                map.AddCreated(step, created.transform);
                report.CreatedObjects++;
                parent = created.transform;
            }
            return parent;
        }

        private string Describe(Component component)
        {
            if (component == null) return "A missing component";
            Transform root = component.transform.IsChildOf(targetRoot) ? targetRoot : sourceRoot;
            string path = AnimationUtility.CalculateTransformPath(component.transform, root);
            return ObjectNames.NicifyVariableName(component.GetType().Name) + " on " + (path.Length == 0 ? component.name : path);
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant run (CopyRun.cs, part of Editor/AvatarMigrationAssistant.cs)
// The run is one partial class split by job: CopyRun.cs (steps, generic copy, objects), TypeRules.cs (per-type
// rules), CopyPrefabs.cs (feature 11), UpgradeConstraints.cs (Unity to VRC constraints), RepointReferences.cs
// (feature 5 and duplicates), CorrectRotation.cs (feature 7), CorrectScale.cs (feature 6).
// ============================================================================
//
// AvatarCopyRun
//   CopyStep(type, settings)  The entry's built-in rule (prefabs, transforms, meshrenderers, skinnedmeshrenderers,
//                        descriptor, unityconstraint with the upgrade), else every one of its types copied generically.
//   Execute(map, settings, answers, ignored)  The run: one Undo group (RegisterFullObjectHierarchyUndo on the target),
//                        each type's step in run order, active state/tag/layer after Cameras, then references, rotation,
//                        scale and duplicates. Each step in its own try/catch (Step). Returns the AvatarRunReport.
//   copied / made / alias  Source component to its target copy; components added this run; temporary-copy objects to
//                        the source objects they copy.
//   CopyComponents(type, settings, componentType)  Replace clears the target's first (ClearTarget); then every source
//                        component in scope (InScope: not ignored, not inside a child prefab) is copied onto its target
//                        (TargetFor: its match, or new objects when Create missing objects is on, added to the map).
//   CopyComponent(source, target)  Unity's component clipboard; pastes into the existing component for types allowed once
//                        per object (OncePerObject), else into a new one; a ParticleSystem brings its renderer's values.
