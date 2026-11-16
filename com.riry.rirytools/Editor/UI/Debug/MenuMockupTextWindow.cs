using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riry.RiryTools.Editor
{
    /// <summary>Double-click in Edit: the block's texts, applied as you type.</summary>
    internal sealed class MenuMockupTextWindow : EditorWindow
    {
        private MenuMockupEditWindow owner;
        private int blockId;

        internal static void Open(MenuMockupEditWindow owner, MenuMockupBlock block, Vector2 screenPoint)
        {
            var window = CreateInstance<MenuMockupTextWindow>();
            window.owner = owner;
            window.blockId = block.Id;
            window.titleContent = new GUIContent("Edit Text");
            window.position = new Rect(screenPoint.x, screenPoint.y, 320f, 80f);
            window.ShowAuxWindow();
        }

        private void OnGUI()
        {
            MenuMockupBlock block = owner != null ? owner.Block(blockId) : null;
            if (block == null)
            {
                Close();
                return;
            }
            MenuMockupTypes.Kind kind = MenuMockupTypes.Find(block.Type);
            EditorGUILayout.LabelField(block.Type, EditorStyles.boldLabel);
            if (kind.LabelName == null && kind.ContentName == null)
            {
                EditorGUILayout.LabelField("This block has no text.");
                return;
            }
            string label = block.Label, content = block.Content;
            if (kind.LabelName != null) label = EditorGUILayout.TextField(kind.LabelName, label);
            if (kind.ContentName != null) content = EditorGUILayout.TextField(kind.ContentName, content);
            if (label == block.Label && content == block.Content) return;
            owner.Record("Edit Text");
            block.Label = label;
            block.Content = content;
            owner.Rebuild();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup Edit Text window
// Menu Mockup (a Debug tool): the model and file format are in Editor/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockupTextWindow ("Edit Text"): the block's Label and Content, named per type (MenuMockupTypes.Kind).
