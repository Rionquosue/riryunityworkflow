using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Riry.RiryTools.Editor.BehaviourFindReplace;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Find and Replace's Apply to child prefab: copies the changed controllers next to the main prefab, makes a child
    /// prefab (a Prefab Variant) of it there whose components use the copies (as overrides), and moves the found
    /// behaviours onto the copies so Apply changes those. Then, when asked, swaps the active scene's instances of the main
    /// prefab for the child prefab with Unity's ReplacePrefabAssetOfPrefabInstance, which keeps their scene overrides.
    /// </summary>
    internal static class BehaviourFindReplaceChildPrefab
    {
        internal sealed class Result
        {
            internal string MainPath = "";
            internal string VariantPath = "";
            internal readonly Dictionary<AnimatorController, AnimatorController> Copies = new Dictionary<AnimatorController, AnimatorController>();
            // Files made; Ctrl+Z does not remove them.
            internal readonly List<string> Created = new List<string>();
        }

        /// <summary>
        /// The prefab asset whose instance holds the component using the hit's controller, or null: a controller asset, an
        /// object outside any prefab instance, or a component added as a scene override.
        /// </summary>
        internal static string MainPrefabOf(Hit hit)
        {
            Component source = hit.Source;
            if (source == null || !PrefabUtility.IsPartOfPrefabInstance(source) || PrefabUtility.IsAddedComponentOverride(source)) return null;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        internal static Result Make(string mainPath, IEnumerable<AnimatorController> controllers)
        {
            var result = new Result { MainPath = mainPath };
            var main = AssetDatabase.LoadAssetAtPath<GameObject>(mainPath);
            if (main == null) throw new InvalidOperationException(mainPath + " could not be loaded.");
            string folder = Path.GetDirectoryName(mainPath).Replace('\\', '/');

            foreach (AnimatorController controller in controllers.Distinct())
            {
                string from = AssetDatabase.GetAssetPath(controller);
                string to = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + controller.name + " Variant.controller");
                if (!AssetDatabase.CopyAsset(from, to)) throw new IOException("Could not copy " + from + " to " + to + ".");
                result.Created.Add(to);
                result.Copies[controller] = AssetDatabase.LoadAssetAtPath<AnimatorController>(to);
            }

            // Saving an instance of the main prefab as a new asset makes a Prefab Variant of it.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(main);
            try
            {
                result.VariantPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + main.name + " Variant.prefab");
                PrefabUtility.SaveAsPrefabAsset(instance, result.VariantPath, out bool saved);
                if (!saved) throw new IOException("Could not save " + result.VariantPath + ".");
                result.Created.Add(result.VariantPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            // Every reference to a copied controller in the child prefab (descriptor layers, VRCFury Full Controller, Modular
            // Avatar Merge Animator, any other component) points at the copy, as an override on the child prefab.
            GameObject contents = PrefabUtility.LoadPrefabContents(result.VariantPath);
            try
            {
                foreach (Component component in contents.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool changed = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (property.objectReferenceValue is AnimatorController old && result.Copies.TryGetValue(old, out AnimatorController copy))
                        {
                            property.objectReferenceValue = copy;
                            changed = true;
                        }
                    }
                    if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(contents, result.VariantPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return result;
        }

        /// <summary>The hits moved onto the copied controllers (the same behaviour, by its place); hits of other controllers as they are.</summary>
        internal static List<Hit> Remap(List<Hit> hits, Dictionary<AnimatorController, AnimatorController> copies)
        {
            var remapped = new List<Hit>();
            foreach (IGrouping<AnimatorController, Hit> group in hits.GroupBy(h => h.Controller))
            {
                if (!copies.TryGetValue(group.Key, out AnimatorController copy))
                {
                    remapped.AddRange(group);
                    continue;
                }
                List<Place> before = Places(group.Key);
                List<Place> after = Places(copy);
                foreach (Hit hit in group)
                {
                    int index = before.FindIndex(place => place.Behaviour == hit.Behaviour);
                    if (index < 0 || index >= after.Count || after[index].Behaviour == null)
                        throw new InvalidOperationException("The copy of " + group.Key.name + " does not match it; nothing was changed in the copy.");
                    Place place = after[index];
                    remapped.Add(new Hit
                    {
                        Controller = copy, Layer = place.Layer, Machines = place.Machines, Owner = place.Owner, Behaviour = place.Behaviour,
                        Property = hit.Property, Value = hit.Value, Kind = hit.Kind, Root = hit.Root, Source = hit.Source,
                        Label = hit.Label + " (child prefab)"
                    });
                }
            }
            return remapped;
        }

        /// <summary>
        /// Swaps the active scene's outermost instances of the main prefab for the child prefab, keeping their overrides, as
        /// one undo step. Instances nested in another prefab instance can't be swapped without unpacking it, so they are left.
        /// Returns how many were swapped; skipped receives the names of nested ones.
        /// </summary>
        internal static int SwapInActiveScene(string mainPath, string variantPath, List<string> skipped)
        {
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Scene scene = SceneManager.GetActiveScene();
            var instances = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform each in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject gameObject = each.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(gameObject) || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject) != mainPath) continue;
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject)) instances.Add(gameObject);
                    else skipped.Add(HierarchyPath.Of(each));
                }
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Find and Replace: use the child prefab");
            foreach (GameObject instance in instances) PrefabUtility.ReplacePrefabAssetOfPrefabInstance(instance, variant, InteractionMode.UserAction);
            Undo.CollapseUndoOperations(group);
            if (instances.Count > 0) EditorSceneManager.MarkSceneDirty(scene);
            return instances.Count;
        }
    }
}
