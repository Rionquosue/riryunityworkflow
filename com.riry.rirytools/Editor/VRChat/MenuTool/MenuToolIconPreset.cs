using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditorInternal;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Menu Tools' icon size fix: the VRChat SDK only uploads expression menu icons up to 256 px, so an icon over that has
    /// its import settings changed by the menu_icon import preset (in the Import Presets folder), which sets the max size to
    /// 256. Only the max size settings of the preset are applied; the texture file itself is not changed.
    /// </summary>
    internal static class MenuToolIconPreset
    {
        internal const int MaxSize = 256;
        internal const string PresetName = "menu_icon";

        internal static string PresetPath
        {
            get { return Path.Combine(ImportPresetLibrary.RootPath, PresetName + ImportPresetLibrary.PresetExtension); }
        }

        internal static bool TooLarge(Texture2D texture)
        {
            return texture != null && (texture.width > MaxSize || texture.height > MaxSize);
        }

        /// <summary>
        /// Sets each texture's max size from the menu_icon preset (made from the first texture's importer with every max size
        /// at 256 when it doesn't exist yet) and reimports it. Textures outside Assets are skipped. Returns the paths changed.
        /// </summary>
        internal static List<string> Apply(IEnumerable<Texture2D> textures)
        {
            var changed = new List<string>();
            List<TextureImporter> importers = textures.Where(t => t != null).Select(AssetDatabase.GetAssetPath).Distinct()
                .Where(path => path.StartsWith("Assets/", System.StringComparison.Ordinal))
                .Select(path => AssetImporter.GetAtPath(path) as TextureImporter).Where(i => i != null).ToList();
            if (importers.Count == 0) return changed;
            Preset preset = ImportPresetLibrary.LoadPreset(PresetPath) ?? Create(importers[0]);
            string[] sizes = preset.PropertyModifications.Select(m => m.propertyPath).Where(p => p.EndsWith("m_MaxTextureSize", System.StringComparison.Ordinal))
                .Distinct().ToArray();
            if (sizes.Length == 0) throw new System.InvalidOperationException(PresetPath + " has no max size setting; delete it and try again to remake it.");
            foreach (TextureImporter importer in importers)
            {
                if (!preset.ApplyTo(importer, sizes)) continue;
                importer.SaveAndReimport();
                changed.Add(importer.assetPath);
            }
            return changed;
        }

        // A preset of the source importer with every max size set to 256, saved as menu_icon.preset.
        private static Preset Create(TextureImporter source)
        {
            var preset = new Preset(source);
            using (var serialized = new SerializedObject(preset))
            {
                SerializedProperty properties = serialized.FindProperty("m_Properties");
                for (int i = 0; properties != null && i < properties.arraySize; i++)
                {
                    SerializedProperty modification = properties.GetArrayElementAtIndex(i);
                    if (!modification.FindPropertyRelative("propertyPath").stringValue.EndsWith("m_MaxTextureSize", System.StringComparison.Ordinal)) continue;
                    modification.FindPropertyRelative("value").stringValue = MaxSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            ImportPresetLibrary.EnsureRoot();
            InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { preset }, PresetPath, true);
            preset.hideFlags = HideFlags.HideAndDontSave;
            return preset;
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tools icon preset
// ============================================================================
//
// MenuToolIconPreset (this file)
//   MaxSize                  256: the largest icon the VRChat SDK uploads.
//   PresetName               "menu_icon": the import preset's name in the Import Presets folder.
//   PresetPath               <Import Presets folder>/menu_icon.preset.
//   TooLarge(texture)        Wider or taller than MaxSize.
//   Apply(textures)          Applies the preset's max size settings to each texture in Assets and reimports it.
//   Create(source)           Makes the preset from an importer with every max size at 256 and saves it.
