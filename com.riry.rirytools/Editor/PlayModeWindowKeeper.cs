#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Entering Play Mode brings the Game view to the front of its dock area and
    /// focuses it. This puts the tabs that were in front back in front (and the
    /// window that had focus back in focus), and can close a Game view that Play
    /// Mode opened when the layout had none. Its menu toggles are in
    /// Editor/UI/RiryMenus.cs.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeWindowKeeper
    {
        // Stored keys keep the package's former name so saved settings survive the rename.
        private const string KeepTabsPref = "Riry.UnityWorkflow.PlayMode.KeepTabs";
        private const string CloseGameViewPref = "Riry.UnityWorkflow.PlayMode.CloseGameView";
        // Session state survives the script reload that entering Play Mode usually causes.
        private const string FrontTabsKey = "Riry.UnityWorkflow.PlayMode.FrontTabs";
        private const string FocusedKey = "Riry.UnityWorkflow.PlayMode.Focused";
        private const string HadGameViewKey = "Riry.UnityWorkflow.PlayMode.HadGameView";
        private const string GameViewType = "UnityEditor.GameView";
        // Unity raises the Game view during the first editor updates in Play Mode.
        private const int SettleUpdates = 3;

        private static int updatesLeft;

        static PlayModeWindowKeeper()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static bool KeepTabs
        {
            get { return EditorPrefs.GetBool(KeepTabsPref, true); }
            set { EditorPrefs.SetBool(KeepTabsPref, value); }
        }

        internal static bool CloseGameView
        {
            get { return EditorPrefs.GetBool(CloseGameViewPref, false); }
            set { EditorPrefs.SetBool(CloseGameViewPref, value); }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!KeepTabs && !CloseGameView) return;
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                var front = new List<int>();
                bool hadGameView = false;
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    hadGameView |= IsGameView(window);
                    if (window.hasFocus && !IsGameView(window)) front.Add(window.GetInstanceID());
                }
                SessionState.SetString(FrontTabsKey, JoinIds(front));
                SessionState.SetInt(FocusedKey, EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.GetInstanceID() : 0);
                SessionState.SetBool(HadGameViewKey, hadGameView);
            }
            else if (change == PlayModeStateChange.EnteredPlayMode)
            {
                updatesLeft = SettleUpdates;
                EditorApplication.update -= RestoreWhenSettled;
                EditorApplication.update += RestoreWhenSettled;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                // A Game view that only existed for Play Mode goes away with it.
                if (!SessionState.GetBool(HadGameViewKey, true)) CloseGameViews();
                SessionState.EraseString(FrontTabsKey);
                SessionState.EraseInt(FocusedKey);
                SessionState.EraseBool(HadGameViewKey);
            }
        }

        private static void RestoreWhenSettled()
        {
            if (--updatesLeft > 0) return;
            EditorApplication.update -= RestoreWhenSettled;
            if (!EditorApplication.isPlaying) return;

            if (CloseGameView && !SessionState.GetBool(HadGameViewKey, true))
            {
                CloseGameViews();
                return;
            }
            if (!KeepTabs) return;
            foreach (int id in SplitIds(SessionState.GetString(FrontTabsKey, "")))
            {
                var window = EditorUtility.InstanceIDToObject(id) as EditorWindow;
                if (window != null && !window.hasFocus) window.Focus();
            }
            // The window that had focus is focused last, so it keeps keyboard focus.
            var focused = EditorUtility.InstanceIDToObject(SessionState.GetInt(FocusedKey, 0)) as EditorWindow;
            if (focused != null) focused.Focus();
        }

        private static bool IsGameView(EditorWindow window)
        {
            return window != null && window.GetType().FullName == GameViewType;
        }

        private static void CloseGameViews()
        {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (IsGameView(window)) window.Close();
            }
        }

        internal static string JoinIds(IEnumerable<int> ids)
        {
            return string.Join(",", ids);
        }

        internal static List<int> SplitIds(string text)
        {
            var ids = new List<int>();
            foreach (string part in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int id;
                if (int.TryParse(part, out id)) ids.Add(id);
            }
            return ids;
        }
    }
}
#endif
