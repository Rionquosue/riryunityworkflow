using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Avatar Migration Assistant settings, kept for this user in EditorPrefs as JSON
    /// (RiryTools.AvatarMigrator.Settings), like Pumkin's Avatar Tools' SettingsContainer.
    /// </summary>
    [Serializable]
    internal sealed class AvatarMigratorSettings
    {
        internal const string PrefsKey = "RiryTools.AvatarMigrator.Settings";
        internal const float DefaultTolerance = 0.001f;

        // Feature 1: how far apart, in avatar-root space, a structure match may be.
        public float StructureTolerance = DefaultTolerance;
        // Feature 2: open Manual matching on every run, listing every row.
        public bool ManualMatching;

        // Feature 3: one entry per component type (AvatarMigratorCopyTypes.All), and the Common/All list.
        public List<AvatarMigratorTypeSettings> Types = new List<AvatarMigratorTypeSettings>();
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

        // Feature 10: the root folder inside Assets for every file the tool creates.
        public string MigratedFolder = "";

        /// <summary>The settings of a component type, made with its defaults on first use.</summary>
        internal AvatarMigratorTypeSettings Type(AvatarMigratorCopyType type)
        {
            AvatarMigratorTypeSettings found = Types.Find(entry => entry.Id == type.Id);
            if (found != null) return found;
            found = new AvatarMigratorTypeSettings { Id = type.Id, Enabled = type.DefaultEnabled, CreateMissing = true };
            Types.Add(found);
            return found;
        }

        /// <summary>Whether the entry with a built-in rule is in the list and ticked.</summary>
        internal bool RuleEnabled(string rule)
        {
            AvatarMigratorCopyType type = AvatarMigratorCopyTypes.WithRule(rule);
            return type != null && Type(type).Enabled;
        }

        internal static AvatarMigratorSettings Load()
        {
            string json = EditorPrefs.GetString(PrefsKey, "");
            if (json.Length > 0)
            {
                try
                {
                    AvatarMigratorSettings loaded = JsonUtility.FromJson<AvatarMigratorSettings>(json);
                    if (loaded != null) return loaded;
                }
                catch (ArgumentException exception)
                {
                    RiryLog.Warn(RiryLog.AvatarMigration, "The saved settings could not be read and were reset: " + exception.Message);
                }
            }
            return new AvatarMigratorSettings();
        }

        internal void Save()
        {
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
        }
    }

    [Serializable]
    internal sealed class AvatarMigratorTypeSettings
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
// GLOSSARY: Avatar Migration Assistant settings (AvatarMigratorSettings.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorSettings (this file): EditorPrefs RiryTools.AvatarMigrator.Settings (JSON)
//   StructureTolerance   Feature 1 structure match distance in avatar-root space (default 0.001).
//   ManualMatching       Feature 2: open Manual matching on every run with every row listed.
//   Types / Type(type) / RuleEnabled(rule) / ShowAll  Feature 3 per-entry settings (starting as the entry's "enabled"),
//                        whether a built-in entry is ticked, and the Common/All list.
//   Transform*, Skinned*, RootAnimator, Descriptor*  Sub-options (DescriptorBlueprintId: "Copy original blueprint ID?").
//   IgnoreChildren       Feature 13: ignored objects' children are ignored too.
//   MigratedFolder       Feature 10: the root folder for migrated files (AvatarMigratorMigratedFolder.cs).
//   Load() / Save()
// AvatarMigratorTypeSettings     Per component type: Enabled, CreateMissing (feature 4), Replace (feature 12; off adds).
