using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Stop Auto Game: when Play Mode starts, the windows that had focus get it
    /// back from the Game view, and a Game view that Play Mode opened (the layout
    /// had none) is closed when Play Mode ends, unless it was one of those windows. Behaves like DreadScripts' NoAutoGame with AutoUnfocus
    /// on and AutoClose off. Its menu toggle is in Editor/UI/RiryToolsMenuBar.cs.
    /// </summary>
    internal static class StopAutoGame
    {
        private const string EnabledPref = "RiryTools.StopAutoGame.Enabled";
        // Session state survives the script reload that entering Play Mode usually causes.
        private const string GameViewWasOpenKey = "RiryTools.StopAutoGame.GameViewWasOpen";
        private const string FocusedWindowsKey = "RiryTools.StopAutoGame.FocusedWindows";
        private const string GameViewTypeName = "UnityEditor.GameView, UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        // Unity focuses the Game view shortly after Play Mode starts.
        private const int HandleDelayMs = 100;

        private static Type gameViewType;
        private static readonly PropertyInfo HasFocus =
            typeof(EditorWindow).GetProperty("hasFocus", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static CancellationTokenSource cancel;
        private static EditorWindow[] previouslyFocused;

        private static Type GameViewType
        {
            get { return gameViewType ?? (gameViewType = Type.GetType(GameViewTypeName)); }
        }

        /// <summary>On by default; per user, shared by every project (EditorPrefs).</summary>
        internal static bool Enabled
        {
            get { return EditorPrefs.GetBool(EnabledPref, true); }
            set
            {
                // No log: the menu checkmark and the Tools tab toggle already show it.
                EditorPrefs.SetBool(EnabledPref, value);
                RefreshHook();
            }
        }

        [InitializeOnLoadMethod]
        private static void RefreshHook()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            if (Enabled) EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        private static void PlayModeChanged(PlayModeStateChange change)
        {
            cancel?.Cancel();
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    RememberWindows();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    cancel = new CancellationTokenSource();
                    _ = RestoreFocus(cancel.Token);
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    SessionState.EraseString(FocusedWindowsKey);
                    if (GameViewType != null && !SessionState.GetBool(GameViewWasOpenKey, true))
                    {
                        var gameViews = (EditorWindow[])Resources.FindObjectsOfTypeAll(GameViewType);
                        EditorWindow[] focused = previouslyFocused ?? new EditorWindow[0];
                        if (gameViews.Any() && !gameViews.Any(window => focused.Contains(window)))
                        {
                            foreach (EditorWindow window in gameViews) window.Close();
                            RiryLog.Info(RiryLog.StopAutoGame, "Closed the Game view that Play Mode opened (the layout had none).");
                        }
                    }
                    previouslyFocused = null;
                    break;
            }
        }

        // A reflection lookup this Unity version lacks: the part of the feature it serves is off, said once per session.
        private static void Missing(string what, string effect)
        {
            string key = "RiryTools.StopAutoGame.Missing." + what;
            if (SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, true);
            RiryLog.Warn(RiryLog.StopAutoGame, what + " was not found in this Unity version, so " + effect + ".");
        }

        private static void RememberWindows()
        {
            if (HasFocus == null) Missing("EditorWindow.hasFocus", "focus is not given back after Play Mode starts");
            if (GameViewType == null) Missing("UnityEditor.GameView", "a Game view Play Mode opens is not closed afterwards");
            if (HasFocus != null)
            {
                SessionState.SetString(FocusedWindowsKey, string.Join(",", Resources.FindObjectsOfTypeAll<EditorWindow>()
                    .Where(window => (bool)HasFocus.GetValue(window))
                    .Select(window => window.GetInstanceID().ToString())));
            }
            if (GameViewType == null) return;
            SessionState.SetBool(GameViewWasOpenKey, Resources.FindObjectsOfTypeAll(GameViewType).Length > 0);
        }

        private static async Task RestoreFocus(CancellationToken token)
        {
            try
            {
                await Task.Delay(HandleDelayMs, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            // Fire-and-forget: anything that goes wrong is logged here, or it would be lost with the task.
            try
            {
                string ids = SessionState.GetString(FocusedWindowsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(ids)) return;
                previouslyFocused = ids.Split(',').Select(int.Parse)
                    .Select(EditorUtility.InstanceIDToObject)
                    .OfType<EditorWindow>().Where(window => window != null).ToArray();
                foreach (EditorWindow window in previouslyFocused) window.Focus();
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.StopAutoGame, "Could not give focus back after Play Mode started", exception);
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Stop Auto Game
// The menu toggle is in Editor/UI/RiryToolsMenuBar.cs; the Main Menu's Tools tab has the same toggle.
// ============================================================================
//
// StopAutoGame (this file)
//   Enabled             EditorPrefs RiryTools.StopAutoGame.Enabled (on by default); setting it re-hooks Play Mode.
//   PlayModeChanged     Exiting Edit Mode: RememberWindows (focused windows, whether a Game view was open). Entered Play
//                       Mode: RestoreFocus after HandleDelayMs (100 ms; errors logged). Entered Edit Mode: closes a Game
//                       view Play Mode opened, unless it was focused before, and logs that.
//   Missing(what, effect)  EditorWindow.hasFocus or UnityEditor.GameView not found: that part is off, warned once per
//                       session.
