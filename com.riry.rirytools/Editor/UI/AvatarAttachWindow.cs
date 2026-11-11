using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The Attach To popup, opened from the Hierarchy's right-click RiryTools > Attach To:
    /// Target avatar at the top, then a button per listed bone the avatar has, then Add bone.
    /// Clicking a bone attaches the objects selected when the popup opened, then closes it.
    /// </summary>
    internal sealed class AvatarAttachWindow : EditorWindow
    {
        [SerializeField] private GameObject avatar;
        [SerializeField] private List<Transform> objects = new List<Transform>();

        private Dictionary<HumanBodyBones, Transform> bones;
        private GameObject bonesFor;
        private Vector2 scroll;
        private const float RemoveWidth = 64f;

        internal static void Open()
        {
            var window = GetWindow<AvatarAttachWindow>(true, AttachTo.UndoName);
            window.objects = AttachTo.Selected();
            window.avatar = AttachToAvatarTracker.LastAvatar;
            window.bones = null;
            window.minSize = new Vector2(260f, 160f);
            window.Show();
        }

        private void OnHierarchyChange()
        {
            bones = null;
            Repaint();
        }

        private void OnGUI()
        {
            GameObject picked = (GameObject)EditorGUILayout.ObjectField("Target avatar", avatar, typeof(GameObject), true);
            if (picked == null || !EditorUtility.IsPersistent(picked)) avatar = picked;

            objects.RemoveAll(each => each == null);
            if (objects.Count == 0)
            {
                EditorGUILayout.HelpBox("Select the objects to attach, then right-click > RiryTools > Attach To.", MessageType.Info);
                return;
            }
            if (avatar == null)
            {
                EditorGUILayout.HelpBox("Choose a target avatar.", MessageType.Info);
                return;
            }
            if (bones == null || bonesFor != avatar)
            {
                AttachToSource source;
                bones = AttachToBones.Find(avatar, objects, out source);
                bonesFor = avatar;
            }
            if (bones.Count == 0)
            {
                EditorGUILayout.HelpBox("No bones found under " + avatar.name + ".", MessageType.Warning);
                return;
            }

            List<HumanBodyBones> listed = AttachTo.ListedBones();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (HumanBodyBones bone in listed)
            {
                Transform found;
                if (!bones.TryGetValue(bone, out found) || found == null) continue;
                string label = AttachTo.Label(bone);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent(label, found.name)))
                {
                    List<Transform> attaching = objects.ToList();
                    AttachTo.Attach(attaching, found, label);
                    Close();
                    GUIUtility.ExitGUI();
                }
                // Remove: only on bones added with Add bone.
                if (AttachTo.IsAdded(bone) && GUILayout.Button(new GUIContent("Remove", "Take " + label + " off the list"), GUILayout.Width(RemoveWidth)))
                {
                    AttachTo.RemoveBone(bone);
                    EditorGUILayout.EndHorizontal();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // Add bone: every found bone not yet listed.
            HumanBodyBones[] more = bones.Keys.Where(bone => !listed.Contains(bone)).OrderBy(bone => bone).ToArray();
            if (more.Length == 0) return;
            int chosen = EditorGUILayout.Popup(new GUIContent("Add bone"), -1, more.Select(bone => new GUIContent(AttachTo.Label(bone))).ToArray());
            if (chosen >= 0) AttachTo.AddBone(more[chosen]);
        }
    }
}

// ============================================================================
// GLOSSARY: Attach To popup
// ============================================================================
//
// AvatarAttachWindow       Utility window "Attach To", opened by Open() from GameObject/RiryTools/Attach To.
//   avatar             [SerializeField] Target avatar: set to AttachToAvatarTracker.LastAvatar on open; any scene GameObject.
//   objects            [SerializeField] The top-level scene objects selected when it opened; the ones attached.
//   bones / bonesFor   AttachToBones.Find's result and the avatar it was found for; cleared when the hierarchy changes.
//   Bone buttons       One per AttachTo.ListedBones() bone the avatar has; the tooltip is the bone's object name.
//                      Clicking runs AttachTo.Attach, then closes the popup.
//   Remove             Beside each bone added with Add bone (RemoveWidth 64): takes it off the list (AttachTo.RemoveBone).
//   Add bone           Every found bone not listed yet; picking one adds it for good (AttachTo.AddBone).
