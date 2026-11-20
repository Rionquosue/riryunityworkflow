using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Prefab Cleaner popup (the Main Menu's General Tools > PopUp Tools): picks what to clean, then removes unused
    /// prefab overrides from it through GuidToolBrokenReferenceWindow.ScrubUnusedOverrides, which asks first.
    /// </summary>
    internal sealed class GuidToolPrefabCleanerWindow : EditorWindow
    {
        private enum Target { Scene, Project, Prefab }

        private static readonly string[] TargetNames = { "Scene", "Project", "Prefab" };

        [SerializeField] private Target target;
        [SerializeField] private GameObject prefab;

        // Starts from the selection: a selected prefab asset is the Prefab target, other selected Project files the
        // Project target, otherwise the open scenes.
        internal static void Open()
        {
            var window = CreateInstance<GuidToolPrefabCleanerWindow>();
            window.titleContent = new GUIContent(GuidToolUnusedOverrides.Title);
            var selected = Selection.activeObject as GameObject;
            if (selected != null && EditorUtility.IsPersistent(selected) && PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                window.target = Target.Prefab;
                window.prefab = selected;
            }
            else window.target = GuidToolRegenerate.HasValidSelection() ? Target.Project : Target.Scene;
            window.minSize = new Vector2(360f, 130f);
            window.ShowUtility();
        }

        private void OnSelectionChange() { Repaint(); }

        private void OnGUI()
        {
            if (RiryToolGUI.Stopped()) return;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Removes prefab overrides left pointing at objects or properties that no longer exist.",
                EditorStyles.wordWrappedLabel);
            target = (Target)EditorGUILayout.Popup(new GUIContent("Target",
                "Scene: the open scenes and the prefab in Prefab Mode. Project: the scenes and prefabs selected in the Project " +
                "window, and those in selected folders. Prefab: one prefab asset."), (int)target, TargetNames);
            if (target == Target.Prefab)
                prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
            List<string> files = Files();
            EditorGUILayout.LabelField(files.Count + " file(s): " + string.Join(", ", files.GetRange(0, Mathf.Min(5, files.Count))) +
                (files.Count > 5 ? " and " + (files.Count - 5) + " more" : ""), EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(files.Count == 0))
            {
                if (GUILayout.Button("Clean", GUILayout.Height(28f)))
                {
                    GuidToolBrokenReferenceWindow.ScrubUnusedOverrides(files);
                    RiryToolGUI.ExitAfterModal();
                }
            }
        }

        // The scenes and prefabs the target names; empty when there are none (a non-prefab in the Prefab field).
        private List<string> Files()
        {
            switch (target)
            {
                case Target.Project:
                    return GuidToolRegenerate.HasValidSelection() ? GuidToolReferenceRepair.SelectedFiles() : new List<string>();
                case Target.Prefab:
                    string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
                    return !string.IsNullOrEmpty(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)
                        ? new List<string> { path } : new List<string>();
                default:
                    return GuidToolBrokenReferenceWindow.OpenPaths();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: GuidToolPrefabCleanerWindow
//   Target / TargetNames     Scene (GuidToolBrokenReferenceWindow.OpenPaths: open scenes and the Prefab Mode prefab), Project
//                            (GuidToolReferenceRepair.SelectedFiles), Prefab (one prefab asset in the Prefab field).
//   Open()                   The popup (ShowUtility), its target from the selection; from the Main Menu's General Tools >
//                            PopUp Tools.
//   OnGUI()                  Description, Target, Prefab field, the files it names, Clean (asks first, then cleans).
//   Files()                  The paths the target names.
// ============================================================================
