using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Tools > Material Cleaner: scans one material, the materials the open scenes use, or every material in the project
    /// for texture, float and color entries their shader does not have (MaterialCleaner), and removes them on Clean (one Ctrl+Z per click).
    /// </summary>
    internal sealed class MaterialCleanerWindow : EditorWindow
    {
        [SerializeField] private Material material;
        [SerializeField] private Vector2 scroll;
        [NonSerialized] private List<MaterialCleaner.Result> results = new List<MaterialCleaner.Result>();
        [NonSerialized] private string scanned = "";

        internal static void Open()
        {
            Open(null);
        }

        // The Project window's Material Cleaner: a selected material fills the Material field.
        internal static void Open(Material selected)
        {
            var window = GetWindow<MaterialCleanerWindow>();
            window.titleContent = new GUIContent(MaterialCleaner.Title);
            if (selected != null) window.material = selected;
            window.Show();
        }

        private void OnGUI()
        {
            if (RiryToolGUI.Stopped()) return;
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                material = (Material)EditorGUILayout.ObjectField("Material", material, typeof(Material), false);
                using (new EditorGUI.DisabledScope(material == null))
                {
                    if (GUILayout.Button("Scan", GUILayout.Width(70f))) Scan(new List<Material> { material }, material.name);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan scene materials")) Scan(MaterialCleanerSources.InOpenScenes(), "the open scenes");
                if (GUILayout.Button("Scan all materials in project")) Scan(MaterialCleanerSources.InProject(), "the project");
            }
            if (scanned.Length == 0) return;

            List<MaterialCleaner.Result> dirty = results.Where(r => r.Material != null && r.Stale.Count > 0).ToList();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(results.Count + " material(s) in " + scanned + ", " + dirty.Count + " with entries their shader doesn't have",
                EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(dirty.Count == 0))
            {
                if (GUILayout.Button("Clean all")) Clean(dirty);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (MaterialCleaner.Result result in results)
            {
                if (result.Material == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(AssetDatabase.GetAssetPath(result.Material), string.Join(", ", result.Stale)), EditorStyles.linkLabel))
                        EditorGUIUtility.PingObject(result.Material);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(result.Stale.Count == 0 ? "Clean" : result.Stale.Count + " unused", GUILayout.Width(90f));
                    using (new EditorGUI.DisabledScope(result.Stale.Count == 0))
                    {
                        if (GUILayout.Button("Clean", GUILayout.Width(60f))) Clean(new List<MaterialCleaner.Result> { result });
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Scan(List<Material> materials, string where)
        {
            results = new List<MaterialCleaner.Result>();
            RiryToolGUI.ProgressEach(MaterialCleaner.Title, materials, material => "Scanning " + material.name,
                material => results.Add(MaterialCleaner.Scan(material)));
            scanned = where;
            GUI.FocusControl(null);
        }

        // One undo step for the click; the log lists what was removed.
        private void Clean(List<MaterialCleaner.Result> chosen)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(MaterialCleaner.Title);
            var lines = new List<string>();
            int count = 0;
            try
            {
                foreach (MaterialCleaner.Result result in chosen)
                {
                    List<string> removed = MaterialCleaner.Clean(result);
                    count += removed.Count;
                    lines.Add(AssetDatabase.GetAssetPath(result.Material) + ": " + string.Join(", ", removed));
                }
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MaterialCleaner, "Cleaning failed after " + lines.Count + " material(s)", exception);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
            RiryLog.Info(RiryLog.MaterialCleaner, "Removed " + count + " texture, float and color entr(ies) their shader doesn't have from " + lines.Count + " material(s):\n" +
                string.Join("\n", lines));
        }
    }
}

// ============================================================================
// GLOSSARY: Material Cleaner window
// ============================================================================
//
// MaterialCleanerWindow (this file)
//   Open()                   The window (Main Menu's General Tools > PopUp Tools sub-tab).
//   OnGUI                    Material field + Scan, Scan scene materials, Scan all materials in project, Clean all, a row per
//                            material (path, stale count, Clean).
//   Scan(materials, where) / Clean(chosen)  MaterialCleaner on each; Clean is one undo group and logs the slots removed.
