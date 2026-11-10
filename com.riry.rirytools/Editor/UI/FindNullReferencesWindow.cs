using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>The Find Null references popup: runs FindNullReferences once and lists its reports.</summary>
    internal sealed class FindNullReferencesWindow : EditorWindow
    {
        private List<FindNullReferences.Report> reports = new List<FindNullReferences.Report>();
        private Vector2 scroll;

        internal static void Run()
        {
            var window = CreateInstance<FindNullReferencesWindow>();
            window.titleContent = new GUIContent("Find Null references");
            window.reports = FindNullReferences.Find();
            window.minSize = new Vector2(480f, 240f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (FindNullReferences.Report report in reports)
            {
                EditorGUILayout.LabelField(report.Title, EditorStyles.boldLabel);
                if (report.Location.Length > 0) EditorGUILayout.SelectableLabel(report.Location, EditorStyles.wordWrappedLabel);
                if (report.Source.Length > 0) EditorGUILayout.SelectableLabel(report.Source, EditorStyles.helpBox);
                if (report.Hits.Count == 0) EditorGUILayout.LabelField("No empty or missing references found.", EditorStyles.miniLabel);
                foreach (string hit in report.Hits)
                    EditorGUILayout.SelectableLabel(hit, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.Space(8f);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
