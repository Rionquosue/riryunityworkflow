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
        /// </summary>
        internal static Changes Collect(Transform bone, Func<Component, bool> isConstraint)
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
            foreach (AddedGameObject added in PrefabUtility.GetAddedGameObjects(root))
            {
                if (IsGeneratedChain(added.instanceGameObject.name)) changes.AddedChains.Add(added.instanceGameObject);
            }
            foreach (AddedComponent added in PrefabUtility.GetAddedComponents(root))
            {
                if (isConstraint(added.instanceComponent)) changes.AddedConstraints.Add(added.instanceComponent);
            }
            foreach (ObjectOverride changed in PrefabUtility.GetObjectOverrides(root))
            {
                var component = changed.instanceObject as Component;
                if (component != null && isConstraint(component)) changes.ChangedConstraints.Add(component);
            }
            return changes;
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
            if (changes.AddedChains.Count > 0)
                PrefabUtility.ApplyAddedGameObjects(changes.AddedChains.ToArray(), changes.AssetPath, InteractionMode.UserAction);
            foreach (Component constraint in changes.AddedConstraints)
                PrefabUtility.ApplyAddedComponent(constraint, changes.AssetPath, InteractionMode.UserAction);
            foreach (Object constraint in changes.ChangedConstraints)
                PrefabUtility.ApplyObjectOverride(constraint, changes.AssetPath, InteractionMode.UserAction);
            Undo.CollapseUndoOperations(group);
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
