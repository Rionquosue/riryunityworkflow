using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Everything the Insertable prop tab draws, in the tab and in the Scene view. The state and the
    /// actions are in InsertablePropWindow.cs; both files are one class. Drawn inside
    /// ConstraintToolWindow and the Main Menu's Constraint Tool tab. A glossary is at the bottom.
    /// </summary>
    internal sealed partial class InsertablePropWindow
    {
        private const float ButtonWidth = 110f;
        private static readonly GUIContent[] PlatformNames = { new GUIContent("PC"), new GUIContent("Android") };

        private void OnGUI()
        {
            lastDrawn = EditorApplication.timeSinceStartup;
            bool blocked = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
            if (blocked) EditorGUILayout.HelpBox("Unavailable in Play Mode or while compiling.", MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(blocked))
            {
                DrawPair();
                EditorGUILayout.Space();
                DrawOptions();
                EditorGUILayout.Space();
                DrawAnimator();
                EditorGUILayout.Space();
                DrawHandles();
                EditorGUILayout.Space();
                DrawGenerate();
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        private void DrawPair()
        {
            EditorGUILayout.LabelField("Prop and socket", EditorStyles.boldLabel);
            avatar = RiryToolGUI.Set(this, SettingsUndo, avatar, (VRCAvatarDescriptor)EditorGUILayout.ObjectField(
                new GUIContent("Avatar root", "The avatar's root, with its Avatar Descriptor."), avatar, typeof(VRCAvatarDescriptor), true));
            propPrefab = RiryToolGUI.Set(this, SettingsUndo, propPrefab, (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Prop prefab", "The prop, as a prefab asset. It's placed as an instance; the asset is never changed."),
                propPrefab, typeof(GameObject), false));
            socket = RiryToolGUI.Set(this, SettingsUndo, socket, (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Socket", "Where the prop is seated, inside the avatar (a sheath, a holster)."), socket, typeof(Transform), true));
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            int platform = EditorGUILayout.Popup(new GUIContent("Platform", "PC drops a physics body that can be thrown. Android has no " +
                "physics on avatars, so the prop drops onto what a raycast below it hits."), (int)settings.platform, PlatformNames);
            if (platform != (int)settings.platform)
            {
                Undo.RecordObject(this, SettingsUndo);
                settings.platform = (Platform)platform;
                platformChosen = true;
            }
            settings.allowSelf = RiryToolGUI.Set(this, SettingsUndo, settings.allowSelf, EditorGUILayout.Toggle(
                new GUIContent("Wearer can grab", "The wearer can grab the prop."), settings.allowSelf));
            settings.allowOthers = RiryToolGUI.Set(this, SettingsUndo, settings.allowOthers, EditorGUILayout.Toggle(
                new GUIContent("Others can grab", "Other players can grab the prop."), settings.allowOthers));
            settings.captureRadius = RiryToolGUI.Set(this, SettingsUndo, settings.captureRadius, Mathf.Max(0.005f, EditorGUILayout.FloatField(
                new GUIContent("Capture radius", "Releasing with the tip this close to the socket opening (meters) inserts the prop. " +
                    "The aim into the socket fades in inside it."), settings.captureRadius)));
            settings.gripSmoothing = RiryToolGUI.Set(this, SettingsUndo, settings.gripSmoothing, EditorGUILayout.Slider(
                new GUIContent("Grip smoothing", "Seconds of constraint smoothing on the prop's rotation in the hand, against jitter."),
                settings.gripSmoothing, 0f, 0.5f));
            settings.insertSmoothing = RiryToolGUI.Set(this, SettingsUndo, settings.insertSmoothing, EditorGUILayout.Slider(
                new GUIContent("Insert smoothing", "Seconds of constraint smoothing for each step of the slide into the socket " +
                    "(and the Android drop)."), settings.insertSmoothing, 0.01f, 1f));
            using (new EditorGUI.DisabledScope(settings.platform != Platform.PC))
            {
                settings.stiffness = RiryToolGUI.Set(this, SettingsUndo, settings.stiffness, Mathf.Max(1f, EditorGUILayout.FloatField(
                    new GUIContent("Stiffness", "PC: spring of the joint that carries the hand's motion into a throw. Damping is set " +
                        "to critical from it."), settings.stiffness)));
            }
        }

        private void DrawAnimator()
        {
            EditorGUILayout.LabelField("Animator", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                appendController = RiryToolGUI.Set(this, SettingsUndo, appendController, (AnimatorController)EditorGUILayout.ObjectField(
                    new GUIContent("Append to controller:", "The layers and parameters go here. Empty: the avatar's FX controller."),
                    appendController, typeof(AnimatorController), false));
                if (GUILayout.Button("New controller", GUILayout.Width(ButtonWidth))) { Act(NewController); RiryToolGUI.ExitAfterModal(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                appendParameters = RiryToolGUI.Set(this, SettingsUndo, appendParameters, (VRCExpressionParameters)EditorGUILayout.ObjectField(
                    new GUIContent("Append to parameters:", "The synced state goes here. Empty: the avatar's Expression Parameters."),
                    appendParameters, typeof(VRCExpressionParameters), false));
                if (GUILayout.Button("New parameters", GUILayout.Width(ButtonWidth))) { Act(NewParameters); RiryToolGUI.ExitAfterModal(); }
            }
            if (avatar != null)
            {
                AnimatorController controller = InsertablePropAnimator.TargetController(appendController, avatar);
                VRCExpressionParameters expressions = InsertablePropAnimator.TargetParameters(appendParameters, avatar);
                EditorGUILayout.LabelField("Goes into " + (controller != null ? controller.name : "(no FX controller)") + " and " +
                    (expressions != null ? expressions.name + " (" + InsertablePropAnimator.SyncedCost(expressions) + " of 256 synced bits used)"
                        : "(no Expression Parameters)") + ".", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawHandles()
        {
            EditorGUILayout.LabelField("Handles", EditorStyles.boldLabel);
            Dictionary<string, Transform> handles = PairHandles();
            if (handles.Count == 0)
            {
                EditorGUILayout.HelpBox("Generate once to place the handles, then move them in the Scene view and Generate again.\n" +
                    "Grip: +Y toward the tip, +Z the edge. Insertion tip: the point that enters the socket. Socket opening: its blue " +
                    "(forward) axis points into the socket. Seated pose: where the prop sits when inserted.", MessageType.Info);
                return;
            }
            foreach (KeyValuePair<string, Transform> handle in handles)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(handle.Key);
                    if (GUILayout.Button("Select", GUILayout.Width(ButtonWidth))) Selection.activeTransform = handle.Value;
                }
            }
            EditorGUILayout.LabelField("Move the handles in the Scene view, then Generate again to apply them.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawGenerate()
        {
            string error = GenerateError();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            string id = Id;
            bool exists = id != null && InsertablePropRig.FindRoot(avatar.transform, id) != null;
            if (exists) EditorGUILayout.LabelField("This pair: " + InsertablePropRig.Report(avatar.transform, socket, id) + ".", EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button(exists ? "Update pair" : "Generate")) { Act(Generate); RiryToolGUI.ExitAfterModal(); }
                }
                using (new EditorGUI.DisabledScope(!exists))
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(ButtonWidth))) { Act(RemovePair); RiryToolGUI.ExitAfterModal(); }
                }
            }
        }

        // Scene handles, only while this tab has been drawn in the last two seconds.
        private void OnSceneGUI(SceneView view)
        {
            if (EditorApplication.timeSinceStartup - lastDrawn > 2.0) return;
            Dictionary<string, Transform> handles = PairHandles();
            foreach (KeyValuePair<string, Transform> handle in handles)
            {
                Transform target = handle.Value;
                // Beside the handle, not on its pivot, so the text never covers its centre and axes.
                Transform eye = view.camera.transform;
                float size = HandleUtility.GetHandleSize(target.position);
                Handles.Label(target.position + (eye.up + eye.right) * (0.3f * size), handle.Key, EditorStyles.boldLabel);
                Vector3 position = target.position;
                Quaternion rotation = target.rotation;
                EditorGUI.BeginChangeCheck();
                if (handle.Key == "Insertion tip") position = Handles.PositionHandle(position, rotation);
                else Handles.TransformHandle(ref position, ref rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "Move " + handle.Key);
                    target.SetPositionAndRotation(position, rotation);
                }
                if (handle.Key == "Socket opening")
                {
                    float radius = settings.captureRadius;
                    using (new Handles.DrawingScope(new Color(0.3f, 0.8f, 1f, 0.9f)))
                    {
                        Handles.DrawWireDisc(position, rotation * Vector3.up, radius);
                        Handles.DrawWireDisc(position, rotation * Vector3.right, radius);
                        Handles.DrawWireDisc(position, rotation * Vector3.forward, radius);
                        Handles.ArrowHandleCap(0, position, rotation, radius * 3f, EventType.Repaint);
                    }
                }
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Constraint Tool's Insertable prop tab
// Both window files are one partial class. The scene rig is InsertablePropRig.cs, the animator
// InsertablePropAnimator.cs, the math Editor/VRChat/ConstraintTools/InsertableProp/InsertablePropMath.cs.
// ============================================================================
//
// FIELDS (InsertablePropWindow.cs; [S] = serialized)
//   avatar [S]              VRCAvatarDescriptor of the avatar root (scene object).
//   propPrefab [S]          The prop prefab asset; placed as an instance, never changed.
//   socket [S]              Where the prop sits, inside the avatar.
//   settings [S]            platform (PC/Android, follows the build target until chosen: platformChosen), allowSelf,
//                           allowOthers (the PhysBone grab filter), captureRadius (0.05 m), gripSmoothing (0.08 s),
//                           insertSmoothing (0.15 s), stiffness (1500, PC joint).
//   appendController [S]    "Append to controller:"; empty uses the avatar's FX controller. New controller beside it.
//   appendParameters [S]    "Append to parameters:"; empty uses the avatar's Expression Parameters. New parameters beside it.
//   status, lastDrawn, scroll   Message (also logged by Act(action) after New controller, New parameters, Generate and
//                           Remove); when the tab last drew (scene handles show for 2 s after); scroll position.
//   UndoName "Insertable prop"; SettingsUndo "Insertable prop settings" (RiryToolGUI.Set on every field).
//
// UI (this file)
//   OnGUI                   Prop and socket, Options, Animator, Handles, Generate (Update pair once it exists) and Remove.
//   OnSceneGUI              Grip, Seated pose and Socket opening: move and rotate handles; Insertion tip: move only. The
//                           opening's capture radius is drawn, with an arrow along its forward axis (the insertion axis).
//                           Labels sit up and right of each handle. A new Opening is made 2 capture radii out of the socket
//                           (against its forward axis), so it never starts on the Seat.
//   ButtonWidth (110)       Width of New controller, New parameters, Select and Remove.
