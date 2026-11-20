using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// While files from outside Unity are dragged over a Project folder with an ImportPreset.md, a box under the folder shows
    /// the preset each file will be imported with. When a file could get more than one, the scroll wheel cycles them, and the
    /// shown choice is the one used (ImportPresetDropChoices). Nothing runs unless an outside drag is over a folder.
    /// </summary>
    [InitializeOnLoad]
    internal static class ImportPresetDragOverlay
    {
        private const float LineHeight = 16f;
        private const float Width = 340f;

        // Set by the first DragUpdated of an outside drag, so repaints without one only check this.
        private static bool dragging;
        private static string folder;
        private static string[] dragged;
        private static List<ImportPresetDropChoices.File> files;

        static ImportPresetDragOverlay()
        {
            if (StopAllToolScripts.Active) return;
            EditorApplication.projectWindowItemOnGUI += OnItem;
        }

        private static void OnItem(string guid, Rect rect)
        {
            Event current = Event.current;
            EventType type = current.type;
            if (type == EventType.DragExited)
            {
                dragging = false;
                return;
            }
            if (type != EventType.DragUpdated && type != EventType.DragPerform && type != EventType.ScrollWheel && type != EventType.Repaint) return;
            if (type != EventType.DragUpdated && !dragging) return;
            // Only a drag from outside Unity: it has paths and no objects.
            string[] paths = DragAndDrop.paths;
            dragging = paths != null && paths.Length > 0 && DragAndDrop.objectReferences.Length == 0 && type != EventType.DragPerform;
            if (paths == null || paths.Length == 0 || DragAndDrop.objectReferences.Length > 0) return;
            if (!rect.Contains(current.mousePosition)) return;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path)) return;
            if (path != folder || !ReferenceEquals(paths, dragged) && !Same(paths, dragged))
            {
                folder = path;
                dragged = paths;
                files = ImportPresetDropChoices.For(path, paths);
            }
            if (files.Count == 0) return;

            if (type == EventType.ScrollWheel)
            {
                int step = current.delta.y > 0 ? 1 : -1;
                foreach (ImportPresetDropChoices.File file in files)
                {
                    int count = file.Presets.Count;
                    if (count > 1) file.Index = (file.Index + step + count) % count;
                }
                ImportPresetDropChoices.Remember(folder, files);
                current.Use();
                if (EditorWindow.mouseOverWindow != null) EditorWindow.mouseOverWindow.Repaint();
                return;
            }
            if (type != EventType.Repaint)
            {
                ImportPresetDropChoices.Remember(folder, files);
                return;
            }
            Draw(rect);
        }

        private static void Draw(Rect folderRect)
        {
            bool choices = files.Exists(f => f.Presets.Count > 1);
            int lines = files.Count + 1 + (choices ? 1 : 0);
            var box = new Rect(folderRect.x, folderRect.yMax + 2f, Width, lines * LineHeight + 6f);
            GUI.Box(box, GUIContent.none, EditorStyles.helpBox);
            var line = new Rect(box.x + 4f, box.y + 3f, box.width - 8f, LineHeight);
            GUI.Label(line, "Import Presets", EditorStyles.miniBoldLabel);
            foreach (ImportPresetDropChoices.File file in files)
            {
                line.y += LineHeight;
                ImportPresetMarker.Target target = file.Presets[file.Index];
                GUI.Label(line, file.Name + "  →  " + target.Configuration + "/" + target.Preset +
                    (file.Presets.Count > 1 ? "  (" + (file.Index + 1) + " of " + file.Presets.Count + ")" : ""), EditorStyles.miniLabel);
            }
            if (!choices) return;
            line.y += LineHeight;
            GUI.Label(line, "Scroll to choose another preset.", EditorStyles.centeredGreyMiniLabel);
        }

        private static bool Same(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }
    }
}

// ============================================================================
// GLOSSARY: Import Preset drag overlay
// ============================================================================
//
// ImportPresetDragOverlay (this file)
//   OnItem(guid, rect)       Project window item callback: only an outside drag over a folder does anything. Works out the
//                            files' presets once per folder and drag, cycles them on the scroll wheel, keeps the picks, draws.
//   Draw(folderRect)         The box under the folder: each file and its preset, "n of m" where there is a choice.
//   Same(left, right)        The same dragged paths.
