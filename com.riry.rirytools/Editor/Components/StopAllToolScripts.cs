using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Stop all tool scripts: an emergency switch that keeps every RiryTools script from running. While it is on, nothing
    /// starts on load (each tool's InitializeOnLoad and editor hooks return at once), importers and save processors pass
    /// files through untouched, every RiryTools window draws only a notice, and every menu entry is greyed out but this
    /// toggle, the Main Menu and Documentation. Saved for this user in this project (UserSettings), so it stays on through
    /// script reloads and restarts until turned off. Turning it on or off reloads scripts, so every tool stops or starts
    /// cleanly.
    /// </summary>
    internal static class StopAllToolScripts
    {
        internal const string DisplayName = "Stop all tool scripts";
        private const string Key = "RiryTools.StopAllToolScripts.Enabled";

        private static bool read;
        private static bool active;

        /// <summary>True while every RiryTools script is stopped.</summary>
        internal static bool Active
        {
            get
            {
                if (!read)
                {
                    active = EditorUserSettings.GetConfigValue(Key) == "1";
                    read = true;
                }
                return active;
            }
        }

        /// <summary>Turns the stop on or off, logs it and reloads scripts.</summary>
        internal static void Set(bool on)
        {
            if (on == Active) return;
            EditorUserSettings.SetConfigValue(Key, on ? "1" : "0");
            active = on;
            RiryLog.Warn(RiryLog.StopAllToolScripts, on
                ? "Every RiryTools script is stopped until " + DisplayName + " is turned off (Tools > RiryTools > Debug Tools)."
                : "RiryTools scripts run again.");
            EditorUtility.RequestScriptReload();
        }
    }
}

// ============================================================================
// GLOSSARY: Stop all tool scripts
// ============================================================================
//
// StopAllToolScripts (this file)
//   DisplayName              "Stop all tool scripts": menu entry and Main Menu toggle.
//   Active                   Read once per domain from UserSettings ("RiryTools.StopAllToolScripts.Enabled" = "1").
//   Set(on)                  Saves it, logs it, and reloads scripts so every tool stops or starts.
