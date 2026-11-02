using System;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Avatar Migration Assistant settings, kept for this user in EditorPrefs as JSON
    /// (RiryTools.AvatarMigrationAssistant.Settings), like Pumkin's Avatar Tools' SettingsContainer.
    /// </summary>
    [Serializable]
    internal sealed class AvatarMigrationSettings
    {
        internal const string PrefsKey = "RiryTools.AvatarMigrationAssistant.Settings";
        internal const float DefaultTolerance = 0.001f;

        // Feature 1: how far apart, in avatar-root space, a structure match may be.
        public float StructureTolerance = DefaultTolerance;
        // Feature 2: open Manual matching on every run, listing every row.
        public bool ManualMatching;

        internal static AvatarMigrationSettings Load()
        {
            string json = EditorPrefs.GetString(PrefsKey, "");
            if (json.Length > 0)
            {
                try
                {
                    AvatarMigrationSettings loaded = JsonUtility.FromJson<AvatarMigrationSettings>(json);
                    if (loaded != null) return loaded;
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning("Avatar Migration Assistant: the saved settings could not be read and were reset: " + exception.Message);
                }
            }
            return new AvatarMigrationSettings();
        }

        internal void Save()
        {
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant settings
// ============================================================================
//
// AvatarMigrationSettings (this file): EditorPrefs RiryTools.AvatarMigrationAssistant.Settings (JSON)
//   StructureTolerance   Feature 1 structure match distance in avatar-root space (default 0.001).
//   ManualMatching       Feature 2: open Manual matching on every run with every row listed.
//   Load() / Save()
