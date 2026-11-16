using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The modal warning before saving with broken references. CONTINUE ANYWAY counts
    /// down [2], [1] and saves on the third click; ABORT or closing skips the save.
    /// </summary>
    internal sealed class GuidToolBrokenSaveWindow : EditorWindow
    {
        private const string Warning =
            "SAVING WITH BROKEN GUID'S CAN CORRUPT A SCENE. Click continue anyways three times to save regardless.";
        private const int ClicksNeeded = 3;
        private string path;
        private List<string> broken;
        private Vector2 scroll;
        private int clicksLeft = ClicksNeeded;
        private bool confirmed;
        private GUIStyle warningStyle;

        internal static bool Confirm(string path, List<string> broken)
        {
            GuidToolBrokenSaveWindow window = CreateInstance<GuidToolBrokenSaveWindow>();
            window.titleContent = new GUIContent(GuidToolReferenceRepair.Title);
            window.path = path;
            window.broken = broken;
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(main.center.x - 260f, main.center.y - 180f, 520f, 360f);
            window.minSize = new Vector2(520f, 360f);
            window.ShowModalUtility();
            return window.confirmed;
        }

        private void OnGUI()
        {
            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, wordWrap = true };
                warningStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
            }
            GUILayout.Space(8f);
            GUILayout.Label(Warning, warningStyle);
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(path + ": " + broken.Count + " broken asset reference(s).", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Broken references:", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string line in broken) EditorGUILayout.SelectableLabel(line, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("ABORT", GUILayout.Height(30f))) Close();
                string label = "CONTINUE ANYWAY" + (clicksLeft < ClicksNeeded ? " [" + clicksLeft + "]" : "");
                if (GUILayout.Button(label, GUILayout.Height(30f)) && --clicksLeft == 0)
                {
                    confirmed = true;
                    Close();
                }
            }
            GUILayout.Space(8f);
        }
    }
}

// ============================================================================
// GLOSSARY: Broken reference save prompt
// ============================================================================
//
// GuidToolBrokenReferenceSaveGuard / GuidToolBrokenSaveWindow (Editor/GuidTools/GuidToolBrokenReferenceSaveGuard.cs, Editor/UI/GuidTools/GuidToolBrokenSaveWindow.cs): interrupt saving a scene or
//   Prefab Mode prefab with broken asset references (FindBroken: empty references whose GUID is missing),
//   listing each as "<last known name> in <object > component > field> (GUID:...)";
//   CONTINUE ANYWAY needs three clicks ([2], [1]). Skipped while GuidToolBrokenReferenceMonitor.Loading.
