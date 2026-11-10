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
            if (!searched) EditorGUILayout.LabelField("Search to list behaviour strings.", EditorStyles.miniLabel);
            else if (hits.Count == 0) EditorGUILayout.LabelField("Nothing found.", EditorStyles.miniLabel);
            foreach (Hit hit in hits)
            {
                if (GUILayout.Button(hit.Label + "  =  " + hit.Value, EditorStyles.linkLabel)) BehaviourFindReplace.Reveal(hit);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField("Changed components", EditorStyles.boldLabel);
            changedScroll = EditorGUILayout.BeginScrollView(changedScroll, EditorStyles.helpBox, GUILayout.MinHeight(160f));
            if (changes.Count == 0) EditorGUILayout.LabelField("The last Apply's changes are listed here.", EditorStyles.miniLabel);
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

        private void Apply()
        {
            List<Change> made = BehaviourFindReplace.Apply(hits, replace, repath, map, source is AnimatorController);
            if (made.Count == 0) return;
            changes = made;
            Search();
        }
    }
}
