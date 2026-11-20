using System;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Constraint Tool's Dual axis detector and Three axis detector tabs: the settings for TiltDetectorBuilder and its Build
    /// button. One window per tab (threeAxis tells them apart), hosted unseen by ConstraintToolWindow and drawn by calling
    /// OnGUI.
    /// </summary>
    internal sealed class TiltDetectorWindow : EditorWindow
    {
        private const string SettingsUndo = "Axis detector settings";
        private static readonly string[] PlaneNames = { "X", "Y", "Z" };
        private static readonly string[] SpaceNames = { "Local", "World" };

        [SerializeField] internal bool threeAxis;
        [SerializeField] private Transform source;
        [SerializeField] private Transform relativeTo;
        [SerializeField] private TiltDetectorBuilder.Plane plane = TiltDetectorBuilder.Plane.Y;
        [SerializeField] private TiltDetectorBuilder.Space space = TiltDetectorBuilder.Space.Local;
        [SerializeField] private float offset = TiltDetectorBuilder.DefaultOffset;
        [NonSerialized] private string status;
        private Vector2 scroll;

        // The Hierarchy entry: the right-clicked object becomes Source, another selected object (Ctrl/Cmd-click it first)
        // Relative to. Relative to is cleared when it would be the same object as Source.
        internal void UseSelection(Transform active, Transform[] selected)
        {
            if (active == null) return;
            Undo.RecordObject(this, SettingsUndo);
            source = active;
            if (selected != null)
                foreach (Transform other in selected)
                    if (other != null && other != active) { relativeTo = other; break; }
            if (relativeTo == source) relativeTo = null;
            status = null;
            Repaint();
        }

        private TiltDetectorBuilder.Settings Settings
        {
            get
            {
                return new TiltDetectorBuilder.Settings
                {
                    Source = source, RelativeTo = relativeTo, Plane = plane, Space = space, ThreeAxis = threeAxis, Offset = offset
                };
            }
        }

        private void OnGUI()
        {
            if (RiryToolGUI.Stopped()) return;
            bool blocked = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
            if (blocked) EditorGUILayout.HelpBox("Unavailable in Play Mode or while compiling.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(blocked))
            {
                EditorGUILayout.LabelField(threeAxis
                        ? "Reads how far Source moves across a flat plane on Relative to, with three Proximity receivers in a triangle 120° apart."
                        : "Reads how far Source moves across a flat plane on Relative to, with two Proximity receivers in an L (right and up).",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space();
                EditorGUI.BeginChangeCheck();
                var newSource = (Transform)EditorGUILayout.ObjectField(new GUIContent("Source",
                    "The object that moves, such as the end of the tilting bone. The sender goes in a new child of it."), source, typeof(Transform), true);
                var newRelative = (Transform)EditorGUILayout.ObjectField(new GUIContent("Relative to",
                    "The object the plane is fixed to. The receivers go in a new child of it."), relativeTo, typeof(Transform), true);
                var newPlane = (TiltDetectorBuilder.Plane)EditorGUILayout.Popup(new GUIContent("Flat plane",
                    "The axis the flat plane faces: the receivers lie across the other two."), (int)plane, PlaneNames);
                var newSpace = (TiltDetectorBuilder.Space)EditorGUILayout.Popup(new GUIContent("Space",
                    "Local: the plane follows Relative to's axes. World: the world's axes when built."), (int)space, SpaceNames);
                float newOffset = EditorGUILayout.FloatField(new GUIContent("Offset",
                    "How far each receiver is from Source's rest position, in meters. Each receiver's radius is twice this, so it reads 0.5 at rest."), offset);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, SettingsUndo);
                    source = newSource;
                    relativeTo = newRelative;
                    plane = newPlane;
                    space = newSpace;
                    offset = newOffset;
                    status = null;
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
                foreach ((string Name, Vector2 Direction) receiver in threeAxis ? TiltDetectorBuilder.Three : TiltDetectorBuilder.Dual)
                    EditorGUILayout.LabelField(receiver.Name, TiltDetectorBuilder.ParameterPrefix + receiver.Name);

                string problem = TiltDetectorBuilder.Problem(Settings);
                if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Info);
                using (new EditorGUI.DisabledScope(problem != null))
                {
                    if (GUILayout.Button(threeAxis ? "Build three axis detector" : "Build dual axis detector", GUILayout.Height(28f))) Build();
                }
            }
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        private void Build()
        {
            try
            {
                TiltDetectorBuilder.Result result = TiltDetectorBuilder.Build(Settings);
                status = "Created " + result.Receivers.name + " under " + relativeTo.name + " and " + result.Sender.name + " under " + source.name + ".";
                RiryLog.Info(RiryLog.ConstraintTool, status);
                Selection.activeGameObject = result.Receivers;
            }
            catch (Exception exception)
            {
                status = "Build failed: " + exception.Message;
                RiryLog.Exception(RiryLog.ConstraintTool, "Building the axis detector failed", exception);
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Axis detector window
// ============================================================================
//
// TiltDetectorWindow (this file)
//   threeAxis                [SerializeField] Three axis detector tab (triangle) when true, else Dual axis (L).
//   source / relativeTo / plane / space / offset  [SerializeField] The settings (one Undo step per change).
//   OnGUI()                  Description, the settings, the parameter names, the problem if any, Build, the status.
//   Build()                  TiltDetectorBuilder.Build; logs the result and selects the receivers' object.
