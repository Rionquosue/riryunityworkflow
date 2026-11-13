using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static Riry.RiryTools.Editor.BehaviourFindReplace;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Find and Replace window (logic in Editor/VRChat/AnimatorTools/BehaviourFindReplace.cs). Animator Tools > Find and Replace: finds the strings of state behaviours (case-insensitive, FuzzyMatch "contains")
    /// in an avatar's controllers (its playable layers, VRCFury Full Controllers and Modular Avatar Merge Animators, as the
    /// Menu Tool's scan finds them) or one animator controller, and replaces whole strings on Apply, as one undo step.
    /// VRC Animator Play Audio paths can be repathed to a scene object (relative to the avatar, or to a VRCFury Full
    /// Controller's root object), and VRC Avatar Parameter Driver names mapped to one of the controller's parameters.
    /// </summary>
    internal sealed class BehaviourFindReplaceWindow : EditorWindow
    {

        [SerializeField] private Object source;
        [SerializeField] private string search = "";
        [SerializeField] private string replace = "";
        [SerializeField] private GameObject repath;
        [SerializeField] private string map = "";
        [SerializeField] private Vector2 foundScroll;
        [SerializeField] private Vector2 changedScroll;
        [NonSerialized] private List<Hit> hits = new List<Hit>();
        [NonSerialized] private List<Change> changes = new List<Change>();
        [NonSerialized] private bool searched;
        [NonSerialized] private List<string> driverParameters = new List<string>();

        internal static EditorWindow CreateHosted()
        {
            return CreateInstance<BehaviourFindReplaceWindow>();
        }

        /// <summary>The popup, with this source (the right-clicked object or controller, or none).</summary>
        internal static void OpenPopup(Object with)
        {
            var window = CreateInstance<BehaviourFindReplaceWindow>();
            window.titleContent = new GUIContent("Find and Replace");
            window.source = Usable(with) ? with : null;
            window.minSize = new Vector2(460f, 420f);
            window.ShowUtility();
        }

        private static bool Usable(Object value)
        {
            return value is GameObject || value is AnimatorController;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            Object chosen = EditorGUILayout.ObjectField(new GUIContent("Source", "An avatar or an object in one (its playable layers, VRCFury Full Controllers and Modular Avatar Merge Animators), or an animator controller."),
                source, typeof(Object), true);
            if (chosen != source && (chosen == null || Usable(chosen)))
            {
                source = chosen;
                searched = false;
                hits.Clear();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField("Search", search);
                if (GUILayout.Button("Search", GUILayout.Width(70f))) Search();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                replace = EditorGUILayout.TextField("Replace with", replace);
                using (new EditorGUI.DisabledScope(!searched || hits.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Apply", "Replaces the whole of each found string, as one undo step."), GUILayout.Width(70f))) Apply();
                }
            }

            if (searched && !(source is AnimatorController) && hits.Any(h => h.Kind == Kind.PlayAudio))
                repath = (GameObject)EditorGUILayout.ObjectField("Repath to scene object:", repath, typeof(GameObject), true);
            if (searched && hits.Any(h => h.Kind == Kind.Driver)) DrawMap();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Found components" + (searched ? " (" + hits.Count + ")" : ""), EditorStyles.boldLabel);
            foundScroll = EditorGUILayout.BeginScrollView(foundScroll, EditorStyles.helpBox, GUILayout.MinHeight(160f));
            if (searched && hits.Count == 0) EditorGUILayout.LabelField("Nothing found.", EditorStyles.miniLabel);
            foreach (Hit hit in hits)
            {
                if (GUILayout.Button(hit.Label + "  =  " + hit.Value, EditorStyles.linkLabel)) BehaviourFindReplace.Reveal(hit);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField("Changed components", EditorStyles.boldLabel);
            changedScroll = EditorGUILayout.BeginScrollView(changedScroll, EditorStyles.helpBox, GUILayout.MinHeight(160f));
            foreach (Change change in changes)
            {
                if (GUILayout.Button(change.Hit.Label + "  :  " + change.Before + "  →  " + change.After, EditorStyles.linkLabel)) BehaviourFindReplace.Reveal(change.Hit);
            }
            EditorGUILayout.EndScrollView();
        }

        // Map to parameter: typed text, with the controllers' parameters that match it below while it names none.
        private void DrawMap()
        {
            map = EditorGUILayout.TextField("Map to parameter:", map);
            if (driverParameters.Contains(map)) return;
            foreach (string name in Ranked(map).Take(6))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUIUtility.labelWidth);
                    if (GUILayout.Button(name, EditorStyles.miniButtonLeft))
                    {
                        map = name;
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private IEnumerable<string> Ranked(string typed)
        {
            return driverParameters.Select(name => new { name, score = FuzzyMatch.Score(name, typed ?? "") })
                .Where(p => p.score > 0).OrderByDescending(p => p.score).ThenBy(p => p.name.Length).ThenBy(p => p.name, StringComparer.Ordinal)
                .Select(p => p.name);
        }


        private void Search()
        {
            searched = true;
            hits = BehaviourFindReplace.Find(source, search ?? "");
            driverParameters = hits.Where(h => h.Kind == Kind.Driver).Select(h => h.Controller).Distinct()
                .SelectMany(c => c.parameters.Select(p => p.name)).Distinct().ToList();
            if (driverParameters.Count > 0 && !driverParameters.Contains(map)) map = Ranked(search).FirstOrDefault() ?? "";
            GUI.FocusControl(null);
        }

        // Apply: a prefab's controllers offer Apply to main prefab (change them) or Apply to child prefab (change copies used
        // by a Prefab Variant); otherwise shared controllers are warned about first. Users are found by
        // BehaviourFindReplaceUsers, the child prefab made by BehaviourFindReplaceChildPrefab.
        private void Apply()
        {
            bool asset = source is AnimatorController;
            List<AnimatorController> controllers = hits.Select(h => h.Controller).Distinct().ToList();
            var users = new List<string>();
            foreach (AnimatorController controller in controllers)
                users.AddRange(BehaviourFindReplaceUsers.Of(controller).Select(user => controller.name + ": " + user));
            bool packaged = controllers.Any(c => AssetDatabase.GetAssetPath(c).StartsWith("Packages/", StringComparison.Ordinal));
            string usedBy = users.Count > 0 ? "\n\nUsed by:\n" + Listed(users) : "";
            List<string> prefabs = hits.Select(BehaviourFindReplaceChildPrefab.MainPrefabOf).Where(p => p != null).Distinct().ToList();

            if (prefabs.Count > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex("Find and Replace",
                    "The controllers belong to " + Listed(prefabs) + "." + usedBy +
                    "\n\nApply to main prefab changes the controllers. Apply to child prefab copies them and makes a child prefab next to " +
                    "each prefab that uses the copies.", "Apply to main prefab", "Cancel", "Apply to child prefab");
                if (choice == 1) return;
                if (choice == 2)
                {
                    ApplyToChild(prefabs, asset);
                    return;
                }
            }
            else if ((users.Count > 1 || packaged) && !EditorUtility.DisplayDialog("Find and Replace",
                         "These controllers are shared; the change applies to everything that uses them." + usedBy, "Apply", "Cancel"))
                return;

            List<Change> made = BehaviourFindReplaceApply.Apply(hits, replace, repath, map, asset);
            if (made.Count == 0) return;
            changes = made;
            Search();
        }

        private void ApplyToChild(List<string> prefabs, bool asset)
        {
            var made = new List<Change>();
            var results = new List<BehaviourFindReplaceChildPrefab.Result>();
            try
            {
                foreach (string main in prefabs)
                {
                    List<Hit> inPrefab = hits.Where(h => BehaviourFindReplaceChildPrefab.MainPrefabOf(h) == main).ToList();
                    BehaviourFindReplaceChildPrefab.Result result = BehaviourFindReplaceChildPrefab.Make(main, inPrefab.Select(h => h.Controller));
                    results.Add(result);
                    made.AddRange(BehaviourFindReplaceApply.Apply(BehaviourFindReplaceChildPrefab.Remap(inPrefab, result.Copies), replace, repath, map, asset, false));
                }
                List<Hit> rest = hits.Where(h => BehaviourFindReplaceChildPrefab.MainPrefabOf(h) == null).ToList();
                if (rest.Count > 0) made.AddRange(BehaviourFindReplaceApply.Apply(rest, replace, repath, map, asset, false));
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.FindReplace, "Apply to child prefab failed" +
                    (results.Count > 0 ? "; files already made: " + string.Join(", ", results.SelectMany(r => r.Created)) : ""), exception);
                EditorUtility.DisplayDialog("Find and Replace", "Apply to child prefab failed: " + exception.Message, "OK");
                return;
            }
            // The files made are not removed by Ctrl+Z, so the log lists them.
            RiryLog.Info(RiryLog.FindReplace, "Changed " + made.Count + " behaviour string(s). Made " +
                string.Join(", ", results.SelectMany(r => r.Created)) + " (Ctrl+Z does not remove these files).");
            changes = made;

            if (EditorUtility.DisplayDialog("Find and Replace", "Would you like to update the active scene with the new prefab?", "Yes", "No"))
            {
                int swapped = 0;
                var skipped = new List<string>();
                foreach (BehaviourFindReplaceChildPrefab.Result result in results)
                    swapped += BehaviourFindReplaceChildPrefab.SwapInActiveScene(result.MainPath, result.VariantPath, skipped);
                RiryLog.Info(RiryLog.FindReplace, "Swapped " + swapped + " instance(s) in the active scene for the child prefab, keeping their overrides" +
                    (skipped.Count > 0 ? "; left " + skipped.Count + " nested in another prefab instance: " + string.Join(", ", skipped) : "") + ".");
            }
            Search();
        }

        private static string Listed(List<string> items)
        {
            return string.Join("\n", items.Take(10)) + (items.Count > 10 ? "\nand " + (items.Count - 10) + " more" : "");
        }
    }
}
