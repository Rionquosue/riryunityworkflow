using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Refresh Scripts: imports changed files (so scripts edited outside Unity, or with Auto Refresh off, are compiled),
    /// closes every open RiryTools window, reloads the script domain, then reopens those windows at their old positions
    /// with fresh state, so a reworked window shows its new layout without restarting Unity. Docked windows reopen
    /// floating at the same spot. Unity 2022.3 has no menu item for a domain reload.
    /// </summary>
    [InitializeOnLoad]
    internal static class RefreshScripts
    {
        // The name on the Tools menu entry and the Main Menu's button.
        internal const string DisplayName = "Refresh Scripts";
        private const string Key = "RiryTools.RefreshScripts.Reopen";

        [Serializable]
        private sealed class Reopen
        {
            public List<string> Types = new List<string>();
            public List<Rect> Positions = new List<Rect>();
        }

        static RefreshScripts()
        {
            string json = SessionState.GetString(Key, "");
            if (json.Length == 0) return;
            SessionState.EraseString(Key);
            EditorApplication.delayCall += () => ReopenWindows(json);
        }

        internal static void Run()
        {
            var reopen = new Reopen();
            foreach (EditorWindow window in OpenWindows())
            {
                reopen.Types.Add(window.GetType().AssemblyQualifiedName);
                reopen.Positions.Add(window.position);
                window.Close();
            }
            if (reopen.Types.Count > 0) SessionState.SetString(Key, JsonUtility.ToJson(reopen));
            AssetDatabase.Refresh();
            EditorUtility.RequestScriptReload();
        }

        // RiryTools windows shown on screen. Windows hosted unseen in the Main Menu have no host view and are skipped:
        // the Main Menu makes them again.
        private static List<EditorWindow> OpenWindows()
        {
            FieldInfo parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
            return Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(w => w != null && w.GetType().Namespace != null && w.GetType().Namespace.StartsWith("Riry.RiryTools", StringComparison.Ordinal))
                .Where(w => parent == null || parent.GetValue(w) != null)
                .ToList();
        }

        private static void ReopenWindows(string json)
        {
            Reopen reopen;
            try { reopen = JsonUtility.FromJson<Reopen>(json); }
            catch (ArgumentException) { return; }
            for (int i = 0; i < reopen.Types.Count; i++)
            {
                Type type = Type.GetType(reopen.Types[i], false);
                if (type == null || !typeof(EditorWindow).IsAssignableFrom(type) || type.IsAbstract) continue;
                var window = (EditorWindow)ScriptableObject.CreateInstance(type);
                window.Show();
                if (i < reopen.Positions.Count) window.position = reopen.Positions[i];
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Refresh Scripts
// The menu entry, Tools > RiryTools > Refresh Scripts, is in Editor/UI/RiryToolsMenuBar.cs; the Main Menu's Tools tab
// has the same button. Both use DisplayName and call Run().
// ============================================================================
//
// RefreshScripts (this file)
//   DisplayName          "Refresh Scripts": the menu entry's and the Main Menu button's name.
//   Key                  SessionState RiryTools.RefreshScripts.Reopen: the windows to reopen after the reload (Reopen as JSON).
//   Reopen               Types (assembly-qualified names) and Positions of the windows that were open.
//   Run()                Records and closes the open RiryTools windows, imports changed files (AssetDatabase.Refresh) and
//                        requests a script reload.
//   OpenWindows()        RiryTools windows shown on screen (namespace Riry.RiryTools); windows hosted unseen in the Main
//                        Menu have no host view (m_Parent) and are skipped.
//   RefreshScripts()     [InitializeOnLoad] After the reload, reopens the recorded windows (ReopenWindows) on the next
//                        editor update. A window whose type no longer exists (renamed or removed) is skipped.
