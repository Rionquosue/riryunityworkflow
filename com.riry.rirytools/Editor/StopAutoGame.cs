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
    /// on and AutoClose off. Its menu toggle is in Editor/UI/RiryMenus.cs.
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
                EditorPrefs.SetBool(EnabledPref, value);
                Debug.Log("[RiryTools] Stop Auto Game is now " + (value ? "on" : "off") + ".");
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
                    if (!SessionState.GetBool(GameViewWasOpenKey, true))
                    {
                        var gameViews = (EditorWindow[])Resources.FindObjectsOfTypeAll(GameViewType);
                        EditorWindow[] focused = previouslyFocused ?? new EditorWindow[0];
                        if (gameViews.Any() && !gameViews.Any(window => focused.Contains(window)))
                        {
                            foreach (EditorWindow window in gameViews) window.Close();
                        }
                    }
                    previouslyFocused = null;
                    break;
            }
        }

        private static void RememberWindows()
        {
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
            string ids = SessionState.GetString(FocusedWindowsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(ids)) return;
            previouslyFocused = ids.Split(',').Select(int.Parse)
                .Select(EditorUtility.InstanceIDToObject)
                .OfType<EditorWindow>().Where(window => window != null).ToArray();
            foreach (EditorWindow window in previouslyFocused) window.Focus();
        }
    }
}
