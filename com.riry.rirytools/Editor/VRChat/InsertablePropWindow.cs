using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Constraint Tool's Insertable prop tab: generates a draw, drop and reinsert system for a prop
    /// and its socket (a dagger and sheath, for example) on a VRChat avatar. The prop is grabbed by a
    /// PhysBone, turned with the holder's hand from a contact receiver, slides back into the socket
    /// when released inside its capture radius, and otherwise drops: as a physics body on PC, or onto
    /// a raycast hit on Android. Generate creates or updates the pair; its handles keep their poses.
    /// The scene side is InsertablePropRig.cs, the animator side InsertablePropAnimator.cs, the
    /// drawing and scene handles InsertablePropWindow.UI.cs. Needs VRChat SDK 3.10.6 or newer.
    /// </summary>
    internal sealed partial class InsertablePropWindow : EditorWindow
    {
        internal enum Platform { PC, Android }

        [Serializable]
        internal sealed class Settings
        {
            public Platform platform;
            public bool allowSelf = true;
            public bool allowOthers = true;
            public float captureRadius = 0.05f;
            public float gripSmoothing = 0.08f;
            public float insertSmoothing = 0.15f;
            public float stiffness = 1500f;
        }

        internal sealed class Inputs
        {
            public VRCAvatarDescriptor Avatar;
            public GameObject PropPrefab;
            public Transform Socket;
        }

        private const string UndoName = "Insertable prop";
        private const string SettingsUndo = "Insertable prop settings";

        [SerializeField] private VRCAvatarDescriptor avatar;
        [SerializeField] private GameObject propPrefab;
        [SerializeField] private Transform socket;
        [SerializeField] private Settings settings = new Settings();
        [SerializeField] private bool platformChosen;
        [SerializeField] private AnimatorController appendController;
        [SerializeField] private VRCExpressionParameters appendParameters;

        [NonSerialized] private string status;
        [NonSerialized] private double lastDrawn;
        private Vector2 scroll;

        private void OnEnable()
        {
            if (settings == null) settings = new Settings();
            // Until chosen, the platform follows the build target.
            if (!platformChosen)
                settings.platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? Platform.Android : Platform.PC;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnUndoRedo()
        {
            Repaint();
            SceneView.RepaintAll();
        }

        private string Id
        {
            get { return avatar != null && propPrefab != null ? InsertablePropRig.PairId(avatar.gameObject, propPrefab) : null; }
        }

        private Inputs CurrentInputs()
        {
            return new Inputs { Avatar = avatar, PropPrefab = propPrefab, Socket = socket };
        }

        /// <summary>Why Generate can't run yet, or null.</summary>
        private string GenerateError()
        {
            string missing = InsertablePropRig.MissingSdkFeature();
            if (missing != null) return "Needs VRChat SDK 3.10.6 or newer (" + missing + " is missing).";
            if (avatar == null) return "Choose the avatar root (its Avatar Descriptor).";
            if (EditorUtility.IsPersistent(avatar)) return "Use the avatar from the scene, not a prefab asset.";
            if (propPrefab == null) return "Choose the prop prefab.";
            if (!EditorUtility.IsPersistent(propPrefab) || PrefabUtility.GetPrefabAssetType(propPrefab) == PrefabAssetType.NotAPrefab)
                return "The prop must be a prefab asset.";
            if (socket == null) return "Choose the socket transform.";
            if (socket == avatar.transform || !socket.IsChildOf(avatar.transform)) return "The socket must be inside the avatar.";
            string avatarError = InsertablePropRig.AvatarError(avatar.GetComponent<Animator>());
            if (avatarError != null) return avatarError;
            if (InsertablePropAnimator.TargetController(appendController, avatar) == null)
                return "The avatar has no FX controller. Choose one under Append to controller, or click New controller.";
            if (InsertablePropAnimator.TargetParameters(appendParameters, avatar) == null)
                return "The avatar has no Expression Parameters. Choose one under Append to parameters, or click New parameters.";
            return null;
        }

        private void Generate()
        {
            string error = GenerateError();
            if (error != null)
            {
                status = error;
                return;
            }
            AnimatorController controller = InsertablePropAnimator.TargetController(appendController, avatar);
            VRCExpressionParameters expressions = InsertablePropAnimator.TargetParameters(appendParameters, avatar);
            if (!AssetDatabase.IsOpenForEdit(controller) || !AssetDatabase.IsOpenForEdit(expressions))
            {
                status = "The controller or parameters asset is read-only.";
                return;
            }
            // The layers are named by the pair ID only, so another avatar of the same name may own them.
            string id = Id;
            if (InsertablePropRig.FindRoot(avatar.transform, id) == null && InsertablePropAnimator.HasLayers(controller, id) &&
                !EditorUtility.DisplayDialog(UndoName, controller.name + " already has the layers of " + InsertablePropRig.RootPrefix + id +
                    ", but " + avatar.name + " has no rig for them. Another avatar with the same name may use them, and Generate " +
                    "would replace them for this avatar.\n\nRename this avatar, or choose another controller under Append to " +
                    "controller, to keep both.", "Replace them", "Cancel"))
            {
                status = "Generate cancelled; " + controller.name + " was not changed.";
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                InsertablePropRig.Rig rig = InsertablePropRig.Build(CurrentInputs(), settings, UndoName);
                InsertablePropAnimator.Apply(rig, controller, expressions, UndoName);
                int cost = InsertablePropAnimator.SyncedCost(expressions);
                status = "Generated " + InsertablePropRig.RootPrefix + rig.Id + " into " + controller.name + " and " + expressions.name + ": " +
                    InsertablePropRig.Report(avatar.transform, socket, rig.Id) + ". Synced parameters use " + cost + " of 256 bits." +
                    " Move the handles in the Scene view, then Generate again to apply them. Ctrl+Z undoes the objects, layers and " +
                    "parameters; new clip files in " + InsertablePropAnimator.ClipFolderPath(rig.Id, controller) + " stay.";
                if (cost > 256) status += " That is over VRChat's limit, so the avatar can't be uploaded until some are removed.";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                status = "Generate failed: " + exception.Message;
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }

        private void RemovePair()
        {
            string id = Id;
            if (id == null || InsertablePropRig.FindRoot(avatar.transform, id) == null)
            {
                status = "This avatar and prop have no insertable prop pair.";
                return;
            }
            AnimatorController controller = InsertablePropAnimator.TargetController(appendController, avatar);
            VRCExpressionParameters expressions = InsertablePropAnimator.TargetParameters(appendParameters, avatar);
            string folder = InsertablePropAnimator.ClipFolderPath(id, controller);
            int choice = EditorUtility.DisplayDialogComplex(UndoName, "Remove " + InsertablePropRig.RootPrefix + id + " from the avatar" +
                (controller != null ? ", " + controller.name : "") + (expressions != null ? " and " + expressions.name : "") + "?\n\n" +
                "Its clips are in " + folder + ". Undo can't bring back deleted files: after Ctrl+Z of \"Remove and delete clips\", " +
                "the layers come back with their clips missing (Generate again to remake them).", "Remove, keep clips", "Cancel",
                "Remove and delete clips");
            if (choice == 1) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove " + UndoName);
            try
            {
                int removed = InsertablePropRig.Remove(avatar.transform, socket, id);
                InsertablePropAnimator.Remove(id, controller, expressions, UndoName);
                status = "Removed " + removed + " object(s) and the pair's layers and parameters.";
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
            if (choice == 2 && AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.DeleteAsset(folder);
                status += " Deleted " + folder + ".";
            }
        }

        private void NewController()
        {
            string path = EditorUtility.SaveFilePanelInProject("New controller", (avatar != null ? avatar.name : "Avatar") + " Insertable props",
                "controller", "Choose where to save the controller.", RiryToolsProjectFolder.Ensure());
            if (string.IsNullOrEmpty(path)) return;
            AnimatorController created = AnimatorController.CreateAnimatorControllerAtPath(path);
            Undo.RecordObject(this, SettingsUndo);
            appendController = created;
            status = "Created " + path + ". The avatar only runs it once it's its FX controller, or merged in by a tool.";
        }

        private void NewParameters()
        {
            string path = EditorUtility.SaveFilePanelInProject("New parameters", (avatar != null ? avatar.name : "Avatar") + " Parameters",
                "asset", "Choose where to save the Expression Parameters.", RiryToolsProjectFolder.Ensure());
            if (string.IsNullOrEmpty(path)) return;
            var created = CreateInstance<VRCExpressionParameters>();
            created.parameters = new VRCExpressionParameters.Parameter[0];
            AssetDatabase.CreateAsset(created, path);
            Undo.RecordObject(this, SettingsUndo);
            appendParameters = created;
            status = "Created " + path + ". The avatar only uses it once it's set in its Avatar Descriptor.";
        }

        // The pair's handles, those that exist: Grip and Tip on the prop, Seat and Opening on the socket.
        private Dictionary<string, Transform> PairHandles()
        {
            var handles = new Dictionary<string, Transform>();
            string id = Id;
            if (id == null || socket == null) return handles;
            Transform root = InsertablePropRig.FindRoot(avatar.transform, id);
            Transform seat = InsertablePropRig.FindOnSocket(socket, id, "Seat");
            Transform opening = InsertablePropRig.FindOnSocket(socket, id, "Opening");
            if (root != null)
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == InsertablePropRig.GripName) handles["Grip"] = child;
                    if (child.name == InsertablePropRig.TipName) handles["Insertion tip"] = child;
                }
            }
            if (opening != null) handles["Socket opening"] = opening;
            if (seat != null) handles["Seated pose"] = seat;
            return handles;
        }
    }
}

// ============================================================================
// GLOSSARY: Insertable prop tab state and actions
// The full glossary of the window is at the bottom of InsertablePropWindow.UI.cs.
// ============================================================================
//
//   Platform / Settings / Inputs   PC or Android; the settings Generate uses; the avatar, prop and socket.
//   GenerateError()                Why Generate can't run (SDK 3.10.6, inputs, humanoid hands, controller, parameters).
//   Generate()                     InsertablePropRig.Build, then InsertablePropAnimator.Apply, as one Undo step.
//   RemovePair()                   Removes the pair's objects, layers, parameters and (if chosen) its clips folder.
//   NewController / NewParameters  Create an asset in Assets/RiryTools and fill the Append to field.
//   PairHandles()                  The pair's Grip, Insertion tip, Socket opening and Seated pose transforms.
