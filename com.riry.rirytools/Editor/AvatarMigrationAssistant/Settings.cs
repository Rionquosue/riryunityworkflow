using System;
using System.Collections.Generic;
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

        // Feature 3: one entry per component type (AvatarCopyTypes.All), and the Common/All list.
        public List<AvatarTypeSettings> Types = new List<AvatarTypeSettings>();
        public bool ShowAll;

        // Transforms.
        public bool TransformPosition = true;
        public bool TransformRotation = true;
        public bool TransformScale = true;
        public bool TransformTagLayer = true;
        public bool TransformActive = true;

        // SkinnedMeshRenderers.
        public bool SkinnedSettings = true;
        public bool SkinnedBlendshapes = true;
        public bool SkinnedMaterials = true;
        public bool SkinnedBounds = true;

        // Animators: also the Animator on the avatar root.
        public bool RootAnimator;

        // Avatar Descriptor.
        public bool DescriptorViewpoint = true;
        public bool DescriptorLipSync = true;
        public bool DescriptorEyeLook = true;
        public bool DescriptorPlayableLayers = true;
        public bool DescriptorExpressions = true;
        public bool DescriptorColliders = true;
        public bool DescriptorBlueprintId;
        public bool DescriptorAvatarScale;

        // Feature 13: ignored objects' children are ignored too.
        public bool IgnoreChildren = true;

        // Feature 14: the extra type entries (full names or Namespace.*) that are copied.
        public List<string> ExtraTypesEnabled = new List<string>();

        // Feature 10: the root folder inside Assets for every file the tool creates.
        public string MigratedFolder = "";

        /// <summary>The settings of a component type, made with its defaults on first use.</summary>
        internal AvatarTypeSettings Type(AvatarCopyType type)
        {
            AvatarTypeSettings found = Types.Find(entry => entry.Id == type.Id);
            if (found != null) return found;
            found = new AvatarTypeSettings { Id = type.Id, Enabled = type.Common, CreateMissing = true };
            Types.Add(found);
            return found;
        }

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

    [Serializable]
    internal sealed class AvatarTypeSettings
    {
        public string Id = "";
        public bool Enabled;
        // Feature 4: create target objects for source objects that are still unmatched.
        public bool CreateMissing = true;
        // Feature 12: clear the target's components of this type first; off adds alongside them.
        public bool Replace;
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant settings (Settings.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarMigrationSettings (this file): EditorPrefs RiryTools.AvatarMigrationAssistant.Settings (JSON)
//   StructureTolerance   Feature 1 structure match distance in avatar-root space (default 0.001).
//   ManualMatching       Feature 2: open Manual matching on every run with every row listed.
//   Types / Type(type) / ShowAll  Feature 3 per-type settings (common types start enabled) and the Common/All list.
//   Transform*, Skinned*, RootAnimator, Descriptor*  Sub-options (DescriptorBlueprintId: "Copy original blueprint ID?").
//   IgnoreChildren       Feature 13: ignored objects' children are ignored too.
//   ExtraTypesEnabled    Feature 14: the enabled extra type entries (ExtraTypes.cs).
//   MigratedFolder       Feature 10: the root folder for migrated files (MigratedFolder.cs).
//   Load() / Save()
// AvatarTypeSettings     Per component type: Enabled, CreateMissing (feature 4), Replace (feature 12; off adds).
