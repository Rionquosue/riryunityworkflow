using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Animator Tools > Controller Cleaner: scans one controller, the controllers the open scenes use, or every controller
    /// in the project for unused sub-assets (ControllerCleanerScan), and removes them on Clean (ControllerCleanerClean).
    /// </summary>
    internal sealed class ControllerCleanerWindow : EditorWindow
    {
        internal const string Title = "Controller Cleaner";

        [SerializeField] private AnimatorController controller;
        [SerializeField] private Vector2 scroll;
        [NonSerialized] private List<ControllerCleanerScan.Result> results = new List<ControllerCleanerScan.Result>();
        [NonSerialized] private string scanned = "";

        internal static void Open()
        {
            var window = GetWindow<ControllerCleanerWindow>();
            window.titleContent = new GUIContent(Title);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                controller = (AnimatorController)EditorGUILayout.ObjectField("Controller", controller, typeof(AnimatorController), false);
                using (new EditorGUI.DisabledScope(controller == null))
                {
                    if (GUILayout.Button("Scan", GUILayout.Width(70f))) Scan(new List<AnimatorController> { controller }, controller.name);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan scene controllers")) Scan(ControllerCleanerSources.InOpenScenes(), "the open scenes");
                if (GUILayout.Button("Scan all controllers in project")) Scan(ControllerCleanerSources.InProject(), "the project");
            }
            if (scanned.Length == 0) return;

            List<ControllerCleanerScan.Result> dirty = results.Where(r => r.Unused.Count > 0).ToList();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(results.Count + " controller(s) in " + scanned + ", " + dirty.Count + " with unused sub-assets", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(dirty.Count == 0))
            {
                if (GUILayout.Button("Clean all")) Clean(dirty);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ControllerCleanerScan.Result result in results)
            {
                if (result.Controller == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(AssetDatabase.GetAssetPath(result.Controller), EditorStyles.linkLabel)) EditorGUIUtility.PingObject(result.Controller);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(result.Unused.Count == 0 ? "Clean" : result.Unused.Count + " unused", GUILayout.Width(90f));
                    using (new EditorGUI.DisabledScope(result.Unused.Count == 0))
                    {
                        if (GUILayout.Button("Clean", GUILayout.Width(60f))) Clean(new List<ControllerCleanerScan.Result> { result });
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Scan(List<AnimatorController> controllers, string where)
        {
            results = new List<ControllerCleanerScan.Result>();
            try
            {
                for (int i = 0; i < controllers.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(Title, "Scanning " + controllers[i].name, (float)i / controllers.Count)) break;
                    results.Add(ControllerCleanerScan.Scan(controllers[i]));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            scanned = where;
            GUI.FocusControl(null);
        }

        // Removing sub-assets can't be undone with Ctrl+Z: asks first, and the log lists everything removed.
        private void Clean(List<ControllerCleanerScan.Result> chosen)
        {
            int count = chosen.Sum(r => r.Unused.Count);
            if (!EditorUtility.DisplayDialog(Title, "Remove " + count + " unused sub-asset(s) from " + chosen.Count + " controller(s)?\n\nCtrl+Z can't undo this.",
                    "Clean", "Cancel"))
                return;
            var lines = new List<string>();
            try
            {
                foreach (ControllerCleanerScan.Result result in chosen)
                {
                    List<string> removed = ControllerCleanerClean.Clean(result);
                    lines.Add(AssetDatabase.GetAssetPath(result.Controller) + ": " + string.Join(", ", removed));
                }
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.ControllerCleaner, "Cleaning failed after " + lines.Count + " controller(s)", exception);
            }
            RiryLog.Info(RiryLog.ControllerCleaner, "Removed " + count + " unused sub-asset(s) from " + lines.Count + " controller(s) (Ctrl+Z can't undo this):\n" +
                string.Join("\n", lines));
        }
    }
}
