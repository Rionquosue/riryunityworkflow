using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Moves Constraint Train changes made on a prefab instance in a scene into a
    /// prefab: generated chains and constraint changes are applied to the source
    /// prefab, or, when that prefab cannot be edited (an FBX model or a read-only
    /// package prefab), saved into a "&lt;name&gt;_ROOT" Prefab Variant beside it that
    /// the scene object is then connected to.
    /// </summary>
    internal static class ConstraintTrainPrefabs
    {
        internal const string RootSuffix = "_ROOT";

        internal sealed class Changes
        {
            public GameObject InstanceRoot;
            public string AssetPath;
            /// <summary>True for a model (FBX) or read-only prefab, which cannot take overrides.</summary>
            public bool Immutable;
            public readonly List<GameObject> AddedChains = new List<GameObject>();
            public readonly List<Component> AddedConstraints = new List<Component>();
            public readonly List<Object> ChangedConstraints = new List<Object>();

            public int Count
            {
                get { return AddedChains.Count + AddedConstraints.Count + ChangedConstraints.Count; }
            }
        }

        /// <summary>
        /// The Constraint Train changes stored as overrides on the prefab instance
        /// that holds <paramref name="bone"/>, or null when the bone is not in a
        /// prefab instance in a scene. In Prefab Mode changes save with the prefab.
        /// Only this train's are collected: chains added beside the real chain start,
        /// and constraints on the real chain (<paramref name="bone"/> to
        /// <paramref name="end"/>) or inside those chains. Other overrides on the
        /// avatar are never applied.
        /// </summary>
        internal static Changes Collect(Transform bone, Transform end, Func<Component, bool> isConstraint)
        {
            if (bone == null || EditorUtility.IsPersistent(bone) || !PrefabUtility.IsPartOfPrefabInstance(bone) ||
                PrefabStageUtility.GetPrefabStage(bone.gameObject) != null)
                return null;
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(bone);
            var changes = new Changes
            {
                InstanceRoot = root,
                AssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root),
                Immutable = PrefabUtility.IsPartOfImmutablePrefab(root)
            };
            Transform holder = bone.parent;
            HashSet<Transform> scope = TrainScope(bone, end, holder);
            foreach (AddedGameObject added in PrefabUtility.GetAddedGameObjects(root))
            {
                GameObject chain = added.instanceGameObject;
                if (chain.transform.parent == holder && IsGeneratedChain(chain.name)) changes.AddedChains.Add(chain);
            }
            foreach (AddedComponent added in PrefabUtility.GetAddedComponents(root))
            {
                Component component = added.instanceComponent;
                if (isConstraint(component) && scope.Contains(component.transform)) changes.AddedConstraints.Add(component);
            }
            foreach (ObjectOverride changed in PrefabUtility.GetObjectOverrides(root))
            {
                var component = changed.instanceObject as Component;
                if (component != null && isConstraint(component) && scope.Contains(component.transform)) changes.ChangedConstraints.Add(component);
            }
            return changes;
        }

        // The real chain and every object of the generated chains beside its start.
        private static HashSet<Transform> TrainScope(Transform bone, Transform end, Transform holder)
        {
            var scope = new HashSet<Transform> { bone };
            var real = new List<Transform>();
            string error;
            if (end != null && ConstraintToolChains.TryCollectChain(bone, end, real, out error)) scope.UnionWith(real);
            if (holder == null) return scope;
            foreach (Transform child in holder)
            {
                if (IsGeneratedChain(child.name)) scope.UnionWith(child.GetComponentsInChildren<Transform>(true));
            }
            return scope;
        }

        /// <summary>The objects the changes are on, for the confirmation: at most <paramref name="max"/> names, then a count.</summary>
        internal static string Describe(Changes changes, int max = 12)
        {
            var names = new List<string>();
            foreach (GameObject chain in changes.AddedChains) names.Add(chain.name + " (chain)");
            var bones = new HashSet<string>();
            foreach (Component constraint in changes.AddedConstraints) bones.Add(constraint.name);
            foreach (Object constraint in changes.ChangedConstraints) bones.Add(((Component)constraint).name);
            names.AddRange(bones);
            if (names.Count <= max) return string.Join(", ", names);
            return string.Join(", ", names.GetRange(0, max)) + " and " + (names.Count - max) + " more";
        }

        internal static bool IsGeneratedChain(string name)
        {
            if (name.StartsWith(ConstraintTrainChains.RestPrefix, StringComparison.Ordinal)) return true;
            for (int chain = 0; chain < ConstraintTrainPlanner.MaxChains; chain++)
            {
                if (name.StartsWith(ConstraintTrainChains.ChainPrefix(chain), StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// Applies the changes to the source prefab: chains first, so constraints that
        /// point at them resolve to the prefab's copies. One Undo step.
        /// </summary>
        internal static void ApplyToSource(Changes changes, string undoName)
        {
            if (changes.Immutable) throw new InvalidOperationException(changes.AssetPath + " cannot be edited.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            // Collapsed even when an apply throws, so a partial apply is still one Ctrl+Z.
            try
            {
                if (changes.AddedChains.Count > 0)
                    PrefabUtility.ApplyAddedGameObjects(changes.AddedChains.ToArray(), changes.AssetPath, InteractionMode.UserAction);
                foreach (Component constraint in changes.AddedConstraints)
                    PrefabUtility.ApplyAddedComponent(constraint, changes.AssetPath, InteractionMode.UserAction);
                foreach (Object constraint in changes.ChangedConstraints)
                    PrefabUtility.ApplyObjectOverride(constraint, changes.AssetPath, InteractionMode.UserAction);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }

        /// <summary>"&lt;folder&gt;/&lt;model name&gt;_ROOT.prefab" beside the source; in Assets/RiryTools when the source is read-only.</summary>
        internal static string RootPrefabPath(string sourceAssetPath)
        {
            string folder = Path.GetDirectoryName(sourceAssetPath).Replace('\\', '/');
            if (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal)) folder = RiryToolsProjectFolder.AssetPath;
            return folder + "/" + Path.GetFileNameWithoutExtension(sourceAssetPath) + RootSuffix + ".prefab";
        }

        /// <summary>
        /// Saves the whole instance, overrides included, as a Prefab Variant of its
        /// source at <paramref name="path"/> and connects the scene object to it.
        /// </summary>
        internal static GameObject SaveAsRootVariant(Changes changes, string path)
        {
            if (path.StartsWith(RiryToolsProjectFolder.AssetPath + "/", StringComparison.Ordinal)) RiryToolsProjectFolder.Ensure();
            bool success;
            GameObject asset = PrefabUtility.SaveAsPrefabAssetAndConnect(changes.InstanceRoot, path, InteractionMode.UserAction,
                out success);
            if (!success || asset == null) throw new InvalidOperationException("Unity could not save " + path + ". Check the Console.");
            EditorSceneManager.MarkSceneDirty(changes.InstanceRoot.scene);
            return asset;
        }
    }
}
