using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// One animation's keys that Clean would remove, each with a checkbox; APPLY
    /// adds the checked names to the whitelist.
    /// </summary>
    internal sealed class CleanAnimationKeysWhitelistWindow : EditorWindow
    {
        private CleanAnimationKeysWindow owner;
        private readonly List<CleanAnimationKeys.Group> groups = new List<CleanAnimationKeys.Group>();
        private readonly HashSet<string> chosen = new HashSet<string>();
        private Vector2 scroll;

        internal static void Open(CleanAnimationKeysWindow owner, AnimationClip clip)
        {
            CleanAnimationKeysWhitelistWindow window = GetWindow<CleanAnimationKeysWhitelistWindow>(true, "Whitelist: " + clip.name);
            window.minSize = new Vector2(360.0f, 360.0f);
            window.owner = owner;
            window.groups.Clear();
            window.chosen.Clear();
            var seen = new HashSet<string>();
            foreach (CleanAnimationKeys.Group group in CleanAnimationKeys.Removable(clip, owner.Options, owner.Whitelist))
            {
                if (seen.Add(group.Label)) window.groups.Add(group);
            }
            window.groups.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
            window.Show();
        }

        private void OnGUI()
        {
            if (owner == null)
            {
                Close();
                return;
            }
            EditorGUILayout.HelpBox("Only keys that will be removed shown", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (CleanAnimationKeys.Group group in groups)
            {
                bool on = chosen.Contains(group.Name);
                if (EditorGUILayout.ToggleLeft(group.Label, on) != on)
                {
                    if (on) chosen.Remove(group.Name);
                    else chosen.Add(group.Name);
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("APPLY", GUILayout.Height(28.0f)))
            {
                owner.AddToWhitelist(chosen);
                Close();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Clean Animation Keys whitelist popup
// ============================================================================
//
// CleanAnimationKeysWhitelistWindow (this file): the Whitelist popup
//   Open(owner, clip)        Lists the groups Clean would remove from the clip.
//   OnGUI()                  "Only keys that will be removed shown", a checkbox per key, APPLY.
//
