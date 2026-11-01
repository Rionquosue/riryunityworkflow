using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Draws an effect stack (IconCapEffects): one row per effect, which can be dragged
    /// to reorder, toggled on and off, and removed with x; Add effect below. The top of
    /// the list is applied last. Used for captures and for Icon Generator assets.
    /// </summary>
    internal sealed class IconCapEffectStackGUI
    {
        private const float RemoveWidth = 22f;
        private readonly ReorderableList list;
        private List<IconCapEffect> effects = new List<IconCapEffect>();
        private int removeAt = -1;
        private bool changed;

        internal IconCapEffectStackGUI()
        {
            list = new ReorderableList(effects, typeof(IconCapEffect), true, true, false, false)
            {
                footerHeight = 0f,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Effect stack", "(top = last)"),
                elementHeightCallback = index => Height(effects[index]),
                drawElementCallback = (rect, index, active, focused) => DrawElement(rect, effects[index], index),
                onReorderCallback = reordered => changed = true
            };
        }

        /// <summary>Draws the stack; true when it changed. undoTarget, when set, is recorded for Undo and marked dirty.</summary>
        internal bool Draw(List<IconCapEffect> stack, UnityEngine.Object undoTarget)
        {
            effects = stack;
            list.list = stack;
            changed = false;
            removeAt = -1;
            if (undoTarget != null) Undo.RecordObject(undoTarget, "Edit Effect Stack");
            EditorGUI.BeginChangeCheck();
            list.DoLayoutList();
            if (GUILayout.Button("+ Add effect", EditorStyles.miniPullDown)) AddMenu(stack, undoTarget).ShowAsContext();
            if (EditorGUI.EndChangeCheck()) changed = true;
            if (removeAt >= 0 && removeAt < stack.Count)
            {
                stack.RemoveAt(removeAt);
                changed = true;
            }
            if (changed && undoTarget != null) EditorUtility.SetDirty(undoTarget);
            return changed;
        }

        private GenericMenu AddMenu(List<IconCapEffect> stack, UnityEngine.Object undoTarget)
        {
            var menu = new GenericMenu();
            foreach (IconCapEffectType type in Enum.GetValues(typeof(IconCapEffectType)))
            {
                IconCapEffectType chosen = type;
                menu.AddItem(new GUIContent(IconCapEffects.MenuPath(type)), false, () =>
                {
                    if (undoTarget != null) Undo.RecordObject(undoTarget, "Add Effect");
                    // New effects go on top, so they apply last.
                    stack.Insert(0, IconCapEffect.Create(chosen));
                    if (undoTarget != null) EditorUtility.SetDirty(undoTarget);
                    GUI.changed = true;
                });
            }
            return menu;
        }

        private static float Line
        {
            get { return EditorGUIUtility.singleLineHeight + 2f; }
        }

        private static float Height(IconCapEffect effect)
        {
            return (1 + FieldLines(effect)) * Line + 4f;
        }

        private static int FieldLines(IconCapEffect effect)
        {
            switch (effect.Type)
            {
                case IconCapEffectType.Overlay:
                case IconCapEffectType.DrawUnder:
                case IconCapEffectType.Mask:
                case IconCapEffectType.InverseMask:
                    return effect.Textures.Count + 2;
                case IconCapEffectType.GrayOut:
                case IconCapEffectType.CornerBadge:
                    return effect.Textures.Count + 3;
                case IconCapEffectType.CustomMaterial:
                case IconCapEffectType.Padding:
                case IconCapEffectType.ColourOverlay:
                case IconCapEffectType.HueShift:
                case IconCapEffectType.Posterize:
                case IconCapEffectType.Sharpen:
                    return 1;
                case IconCapEffectType.CropAndZoom:
                case IconCapEffectType.RotateAndFlip:
                    return 2;
                case IconCapEffectType.Adjustments:
                case IconCapEffectType.Outline:
                case IconCapEffectType.DropShadow:
                    return 3;
                default:
                    return 0;
            }
        }

        private void DrawElement(Rect rect, IconCapEffect effect, int index)
        {
            rect.y += 2f;
            var line = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            var toggle = new Rect(line.x, line.y, 18f, line.height);
            effect.Enabled = EditorGUI.Toggle(toggle, effect.Enabled);
            EditorGUI.LabelField(new Rect(line.x + 20f, line.y, line.width - 20f - RemoveWidth, line.height), IconCapEffects.Name(effect.Type),
                EditorStyles.boldLabel);
            if (GUI.Button(new Rect(line.xMax - RemoveWidth, line.y, RemoveWidth, line.height), new GUIContent("x", "Remove"), EditorStyles.miniButton))
                removeAt = index;

            using (new EditorGUI.DisabledScope(!effect.Enabled))
            {
                line.x += 20f;
                line.width -= 20f;
                switch (effect.Type)
                {
                    case IconCapEffectType.Overlay:
                    case IconCapEffectType.DrawUnder:
                    case IconCapEffectType.Mask:
                    case IconCapEffectType.InverseMask:
                    case IconCapEffectType.GrayOut:
                    case IconCapEffectType.CornerBadge:
                        if (effect.Type == IconCapEffectType.CornerBadge)
                            effect.Corner = (IconCapCorner)EditorGUI.EnumPopup(Next(ref line), "Corner", effect.Corner);
                        DrawTextures(ref line, effect);
                        effect.Tint = EditorGUI.ColorField(Next(ref line), "Tint", effect.Tint);
                        if (effect.Type == IconCapEffectType.GrayOut)
                            effect.Amount = EditorGUI.Slider(Next(ref line), "Darken", effect.Amount, 0f, 1f);
                        break;
                    case IconCapEffectType.CustomMaterial:
                        var material = (Material)EditorGUI.ObjectField(Next(ref line), "Material",
                            IconCapAssets.Load<Material>(effect.Material), typeof(Material), false);
                        effect.Material = IconCapAssets.Guid(material);
                        break;
                    case IconCapEffectType.CropAndZoom:
                        effect.Zoom = EditorGUI.Slider(Next(ref line), "Zoom", effect.Zoom, 0.1f, 10f);
                        effect.Centre = EditorGUI.Vector2Field(Next(ref line), "Centre", effect.Centre);
                        break;
                    case IconCapEffectType.RotateAndFlip:
                        effect.Angle = EditorGUI.Slider(Next(ref line), "Rotate", effect.Angle, -180f, 180f);
                        Rect flips = Next(ref line);
                        effect.FlipX = EditorGUI.ToggleLeft(new Rect(flips.x, flips.y, flips.width / 2f, flips.height), "Flip horizontally", effect.FlipX);
                        effect.FlipY = EditorGUI.ToggleLeft(new Rect(flips.x + flips.width / 2f, flips.y, flips.width / 2f, flips.height),
                            "Flip vertically", effect.FlipY);
                        break;
                    case IconCapEffectType.Padding:
                        effect.Padding = EditorGUI.Slider(Next(ref line), "Padding", effect.Padding, 0f, 0.45f);
                        break;
                    case IconCapEffectType.Adjustments:
                        effect.Brightness = EditorGUI.Slider(Next(ref line), "Brightness", effect.Brightness, -1f, 1f);
                        effect.Contrast = EditorGUI.Slider(Next(ref line), "Contrast", effect.Contrast, 0f, 3f);
                        effect.Saturation = EditorGUI.Slider(Next(ref line), "Saturation", effect.Saturation, 0f, 3f);
                        break;
                    case IconCapEffectType.Outline:
                        effect.Colour = EditorGUI.ColorField(Next(ref line), "Colour", effect.Colour);
                        effect.Thickness = EditorGUI.Slider(Next(ref line), new GUIContent("Thickness", "Pixels of the final image."),
                            effect.Thickness, 0f, 64f);
                        effect.Softness = EditorGUI.Slider(Next(ref line), "Softness", effect.Softness, 0f, 1f);
                        break;
                    case IconCapEffectType.DropShadow:
                        effect.Colour = EditorGUI.ColorField(Next(ref line), "Colour", effect.Colour);
                        effect.Offset = EditorGUI.Vector2Field(Next(ref line), new GUIContent("Offset", "Pixels: x right, y down."), effect.Offset);
                        effect.Blur = EditorGUI.Slider(Next(ref line), new GUIContent("Blur", "Pixels of the final image."), effect.Blur, 0f, 64f);
                        break;
                    case IconCapEffectType.ColourOverlay:
                        effect.Colour = EditorGUI.ColorField(Next(ref line), new GUIContent("Colour", "Its alpha is the strength."), effect.Colour);
                        break;
                    case IconCapEffectType.HueShift:
                        effect.Hue = EditorGUI.Slider(Next(ref line), "Hue shift", effect.Hue, -180f, 180f);
                        break;
                    case IconCapEffectType.Posterize:
                        effect.Levels = EditorGUI.IntSlider(Next(ref line), "Levels", effect.Levels, 2, 32);
                        break;
                    case IconCapEffectType.Sharpen:
                        effect.Amount = EditorGUI.Slider(Next(ref line), "Amount", effect.Amount, 0f, 4f);
                        break;
                }
            }
        }

        // One field per texture; the empty field below adds one, clearing a field removes it.
        private static void DrawTextures(ref Rect line, IconCapEffect effect)
        {
            for (int i = 0; i <= effect.Textures.Count; i++)
            {
                Texture2D current = i < effect.Textures.Count ? IconCapAssets.Load<Texture2D>(effect.Textures[i]) : null;
                string label = effect.Textures.Count > 1 || i > 0 ? "Texture " + (i + 1) : "Texture";
                var chosen = (Texture2D)EditorGUI.ObjectField(Next(ref line), label, current, typeof(Texture2D), false);
                if (chosen == current) continue;
                if (i == effect.Textures.Count)
                {
                    if (chosen != null) effect.Textures.Add(IconCapAssets.Guid(chosen));
                }
                else if (chosen == null) effect.Textures.RemoveAt(i);
                else effect.Textures[i] = IconCapAssets.Guid(chosen);
                GUI.changed = true;
                break;
            }
        }

        private static Rect Next(ref Rect line)
        {
            line.y += Line;
            return line;
        }
    }
}

// ============================================================================
// GLOSSARY: IconCap effect stack list
// ============================================================================
//
// IconCapEffectStackGUI (this file)
//   Draw(stack, undoTarget)   The list (drag the handle to reorder; toggle; x removes), then + Add effect (a menu by
//                             group; a new effect goes on top). True when the stack changed.
//   DrawTextures              A texture layer's textures: an empty field adds one; clearing a field removes it.
//   FieldLines / Height       Each effect type's rows.
