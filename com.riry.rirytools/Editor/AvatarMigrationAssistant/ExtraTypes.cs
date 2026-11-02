using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Feature 14: third-party component types copied through the generic path, listed by
    /// full name (or Namespace.* for a whole namespace) in two YAML files in
    /// Documents/RiryTools/AvatarMigrationAssistant: extra_copier_types_template.yml, written
    /// by the tool with the same categories as Pumkin's Avatar Tools' template (VRM, VRC Fury,
    /// Modular Avatar, ChilloutVR CCK, Magica Cloth 2, Magica Cloth), and
    /// extra_copier_types.yml for the user's own, merged in by category. Which types are
    /// enabled is saved in the settings.
    /// </summary>
    internal static class AvatarExtraTypes
    {
        internal const string FolderName = "AvatarMigrationAssistant";
        internal const string FileName = "extra_copier_types.yml";
        internal const string TemplateName = "extra_copier_types_template.yml";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>One category of the merged lists.</summary>
        internal sealed class Category
        {
            internal string Name;
            internal readonly List<string> Entries = new List<string>();
        }

        private static readonly string[][] Template =
        {
            new[] { "VRM", "VRM.VRMSpringBone", "VRM.VRMSpringBoneColliderGroup" },
            new[] { "VRC Fury", "VF.Model.VRCFury", "VF.Component.VRCFuryGlobalCollider", "VF.Component.VRCFuryHapticSocket", "VF.Component.VRCFuryHapticPlug" },
            new[]
            {
                "Modular Avatar", "nadena.dev.modular_avatar.core.ModularAvatarMenuGroup", "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller",
                "nadena.dev.modular_avatar.core.ModularAvatarBlendshapeSync", "nadena.dev.modular_avatar.core.ModularAvatarBoneProxy",
                "nadena.dev.modular_avatar.core.ModularAvatarMenuInstallTarget", "nadena.dev.modular_avatar.core.ModularAvatarMenuItem",
                "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator", "nadena.dev.modular_avatar.core.ModularAvatarMergeArmature",
                "nadena.dev.modular_avatar.core.ModularAvatarMeshSettings", "nadena.dev.modular_avatar.core.ModularAvatarParameters",
                "nadena.dev.modular_avatar.core.ModularAvatarPBBlocker", "nadena.dev.modular_avatar.core.ModularAvatarReplaceObject",
                "nadena.dev.modular_avatar.core.ModularAvatarVisibleHeadAccessory", "nadena.dev.modular_avatar.core.ModularAvatarWorldFixedObject"
            },
            new[]
            {
                "ChilloutVR CCK", "CVRMirror", "CVRWarpPoint", "ABI.CCK.Components.ControlPoint", "ABI.CCK.Components.CVRAction",
                "ABI.CCK.Components.CVRAdvancedAvatarSettingsPointer", "ABI.CCK.Components.CVRAdvancedAvatarSettingsTrigger",
                "ABI.CCK.Components.CVRAdvancedAvatarSettingsTriggerHelper", "ABI.CCK.Components.CVRAnimatorDriver", "ABI.CCK.Components.CVRAssetInfo",
                "ABI.CCK.Components.CVRAttachment", "ABI.CCK.Components.CVRAudioDriver", "ABI.CCK.Components.CVRAudioMaterialParser",
                "ABI.CCK.Components.CVRAvatar", "ABI.CCK.Components.CVRAvatarPickupMarker", "ABI.CCK.Components.CVRBuilderSpawnable",
                "ABI.CCK.Components.CVRCameraHelper", "ABI.CCK.Components.CVRCustomRenderTextureUpdater", "ABI.CCK.Components.CVRDescription",
                "ABI.CCK.Components.CVRDistanceConstrain", "ABI.CCK.Components.CVRDistanceLod", "ABI.CCK.Components.CVRFaceTracking",
                "ABI.CCK.Components.CVRGIMaterialUpdater", "ABI.CCK.Components.CVRGlobalMaterialPropertyUpdater", "ABI.CCK.Components.CVRGlobalShaderUpdater",
                "ABI.CCK.Components.CVRHapticAreaChest", "ABI.CCK.Components.CVRHapticZone", "ABI.CCK.Components.CVRInteractable",
                "ABI.CCK.Components.CVRMaterialDriver", "ABI.CCK.Components.CVRMaterialUpdater", "ABI.CCK.Components.CVRMovementParent",
                "ABI.CCK.Components.CVRNavController", "ABI.CCK.Components.CVRObjectLibrary", "ABI.CCK.Components.CVRObjectSync",
                "ABI.CCK.Components.CVRParameterStream", "ABI.CCK.Components.CVRParticleSound", "ABI.CCK.Components.CVRPickupObject",
                "ABI.CCK.Components.CVRPointer", "ABI.CCK.Components.CVRPortalMarker", "ABI.CCK.Components.CVRSnappingPoint",
                "ABI.CCK.Components.CVRSpawnable", "ABI.CCK.Components.CVRSpawnableTrigger", "ABI.CCK.Components.CVRTexturePropertyParser",
                "ABI.CCK.Components.CVRTimelineSync", "ABI.CCK.Components.CVRToggleStatePointer", "ABI.CCK.Components.CVRToggleStateTrigger",
                "ABI.CCK.Components.CVRTranslatable", "ABI.CCK.Components.CVRVariableBuffer", "ABI.CCK.Components.CVRVideoPlayer",
                "ABI.CCK.Components.CVRWorld", "ABI.CCK.Components.CVRWorldModifiers", "ABI.CCK.Components.Damage", "ABI.CCK.Components.FluidVolume",
                "ABI.CCK.Components.ForceApplicator", "ABI.CCK.Components.FPRExclusion", "ABI.CCK.Components.GameInstanceController",
                "ABI.CCK.Components.GravityZone", "ABI.CCK.Components.GunController", "ABI.CCK.Components.ObjectHealth",
                "ABI.CCK.Components.PhysicsInfluencer", "ABI.CCK.Components.PlayerMaterialParser", "ABI.CCK.Components.ScoreBoardController",
                "ABI.CCK.Components.SpawnablePickupMarker"
            },
            new[]
            {
                "Magica Cloth 2", "MagicaCloth2.MagicaCapsuleCollider", "MagicaCloth2.MagicaPlaneCollider", "MagicaCloth2.MagicaSphereCollider",
                "MagicaCloth2.MagicaCloth", "MagicaCloth2.MagicaWindZone", "MagicaCloth2.MagicaSettings"
            },
            new[]
            {
                "Magica Cloth", "MagicaCloth.MagicaAvatar", "MagicaCloth.MagicaBoneCloth", "MagicaCloth.MagicaBoneSpring", "MagicaCloth.MagicaMeshCloth",
                "MagicaCloth.MagicaMeshSpring", "MagicaCloth.MagicaRenderDeformer", "MagicaCloth.MagicaVirtualDeformer"
            }
        };

        internal static string Folder
        {
            get { return RiryToolsPaths.Documents(FolderName); }
        }

        /// <summary>The template and the user's file merged by category; both files are made when missing.</summary>
        internal static List<Category> Categories()
        {
            var categories = new List<Category>();
            try
            {
                WriteMissingFiles();
                Merge(categories, Path.Combine(Folder, TemplateName));
                Merge(categories, Path.Combine(Folder, FileName));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(AvatarMigrationAssistant.Title + ": could not read the extra types in " + Folder + ": " + exception.Message);
            }
            return categories;
        }

        private static void WriteMissingFiles()
        {
            Directory.CreateDirectory(Folder);
            string template = Path.Combine(Folder, TemplateName);
            if (!File.Exists(template))
            {
                var text = new StringBuilder();
                text.Append("# Avatar Migration Assistant extra component types (template).\n");
                text.Append("# Put your own in ").Append(FileName).Append(" in the same layout; it is merged in by category.\n");
                text.Append("# One type per line by its full name, or Namespace.* for every component in a namespace.\n");
                text.Append("categories:\n");
                foreach (string[] category in Template)
                {
                    text.Append("  ").Append(category[0]).Append(":\n");
                    for (int i = 1; i < category.Length; i++) text.Append("    - ").Append(category[i]).Append('\n');
                }
                File.WriteAllText(template, text.ToString(), Utf8);
            }
            string own = Path.Combine(Folder, FileName);
            if (!File.Exists(own))
            {
                File.WriteAllText(own, "# Avatar Migration Assistant extra component types, merged with " + TemplateName + ".\n" +
                    "# Example:\n#   My Tools:\n#     - MyNamespace.MyComponent\n#     - OtherNamespace.*\ncategories:\n", Utf8);
            }
        }

        // Reads "categories:", then "  Name:" lines, each followed by "    - Type" lines.
        private static void Merge(List<Category> categories, string path)
        {
            if (!File.Exists(path)) return;
            Category current = null;
            bool inCategories = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Split('#')[0].TrimEnd();
                if (line.Trim().Length == 0) continue;
                int indent = line.Length - line.TrimStart().Length;
                string text = line.Trim();
                if (indent == 0)
                {
                    inCategories = text == "categories:";
                    current = null;
                    continue;
                }
                if (!inCategories) continue;
                if (text.StartsWith("-", StringComparison.Ordinal))
                {
                    string entry = text.Substring(1).Trim().Trim('"', '\'');
                    if (current != null && entry.Length > 0 && !current.Entries.Contains(entry)) current.Entries.Add(entry);
                    continue;
                }
                if (!text.EndsWith(":", StringComparison.Ordinal)) continue;
                string name = text.Substring(0, text.Length - 1).Trim().Trim('"', '\'');
                current = categories.Find(category => category.Name == name);
                if (current == null)
                {
                    current = new Category { Name = name };
                    categories.Add(current);
                }
            }
        }

        /// <summary>The component types an entry names: one by full name, or every one in a namespace for Namespace.*.</summary>
        internal static List<Type> Resolve(string entry)
        {
            var types = new List<Type>();
            if (entry.EndsWith(".*", StringComparison.Ordinal))
            {
                string space = entry.Substring(0, entry.Length - 2);
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] all;
                    try
                    {
                        all = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException exception)
                    {
                        all = exception.Types;
                    }
                    foreach (Type type in all)
                    {
                        if (type != null && type.Namespace == space && !type.IsAbstract && typeof(Component).IsAssignableFrom(type)) types.Add(type);
                    }
                }
                return types;
            }
            Type found = AvatarCopyTypes.Find(entry);
            if (found != null && typeof(Component).IsAssignableFrom(found)) types.Add(found);
            return types;
        }

        /// <summary>The enabled entries' types that are in this project.</summary>
        internal static List<Type> Enabled(AvatarMigrationSettings settings)
        {
            var types = new List<Type>();
            foreach (string entry in settings.ExtraTypesEnabled)
            {
                foreach (Type type in Resolve(entry))
                {
                    if (!types.Contains(type)) types.Add(type);
                }
            }
            return types;
        }
    }
}

// ============================================================================
// GLOSSARY: Avatar Migration Assistant extra types (ExtraTypes.cs, part of Editor/AvatarMigrationAssistant.cs)
// ============================================================================
//
// AvatarExtraTypes (this file): Documents/RiryTools/AvatarMigrationAssistant/
//   extra_copier_types_template.yml  Written when missing, with Pumkin's categories (Template).
//   extra_copier_types.yml           The user's own, same layout ("categories:", "  Name:", "    - Type"), merged by category.
//   Categories() / Resolve(entry) / Enabled(settings)  The merged lists; an entry's types (Namespace.* expands); the types
//                                    of the entries enabled in the settings (ExtraTypesEnabled).
