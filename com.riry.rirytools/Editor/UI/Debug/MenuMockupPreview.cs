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
    /// <summary>What each block type looks like in Edit: the Unity control it stands for.</summary>
    internal static class MenuMockupPreview
    {
        internal const string Name = "mockup-preview";

        internal static VisualElement Create(MenuMockupBlock block)
        {
            string label = block.Label, content = block.Content;
            switch (block.Type)
            {
                case "Label": return new Label(content);
                case "Text Box": return new TextField(label) { value = content };
                case "Asset Selector": return new ObjectField(label) { objectType = AssetType(content) };
                case "Fuzzy Search Box": return new ToolbarSearchField { value = content };
                case "Popup Button":
                    List<string> options = Split(content);
                    if (options.Count == 0) options.Add("");
                    return new DropdownField(label, options, 0);
                case "Toggle Button": return new Toggle(label) { value = IsOn(content) };
                case "Radio Button": return new RadioButton(label) { value = IsOn(content) };
                case "Click Button": return new Button { text = content };
                case "Slider":
                    float amount;
                    if (!float.TryParse(content, NumberStyles.Float, CultureInfo.InvariantCulture, out amount)) amount = 0.5f;
                    return new Slider(label, 0f, 1f) { value = amount };
                case "Number Field": return new TextField(label) { value = content };
                case "Color Field":
                    Color color;
                    if (!ColorUtility.TryParseHtmlString(content, out color)) color = Color.white;
                    return new ColorField(label) { value = color };
                case "Foldout": return new Foldout { text = content, value = true };
                case "Help Box": return new HelpBox(content, HelpType(label));
                case "Progress Bar": return Progress(content);
                case "Toolbar":
                    var toolbar = new Toolbar();
                    foreach (string button in Split(content)) toolbar.Add(new ToolbarButton { text = button });
                    return toolbar;
                case "Scroll View":
                    var scroll = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible };
                    scroll.style.minHeight = 40;
                    return scroll;
                case "Image": return ImageBox(content);
                case "Separator":
                    var line = new VisualElement();
                    line.style.height = 1;
                    line.style.marginTop = line.style.marginBottom = 4;
                    line.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                    return line;
                case "Space":
                    var space = new VisualElement();
                    space.style.height = 8;
                    return space;
                default: return null;
            }
        }

        /// <summary>
        /// A Tabs block's strip, drawn like the Main Menu's tab strip (RiryToolsMainMenu.TabStrip): flat cells of equal
        /// width, at least TabMinWidth, wrapping onto more rows, the shown tab lighter and bold, a hover tint, a TabBorder
        /// outline. Clicking a tab calls select.
        /// </summary>
        internal static VisualElement TabStrip(MenuMockupBlock block, Action<int> select)
        {
            bool dark = EditorGUIUtility.isProSkin;
            List<string> names = MenuMockupTypes.TabNames(block);
            int active = Mathf.Clamp(block.ActiveTab, 0, names.Count - 1);
            var strip = new VisualElement();
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.flexWrap = Wrap.Wrap;
            strip.style.borderLeftWidth = strip.style.borderTopWidth = RiryToolsMainMenu.TabBorder;
            strip.style.borderLeftColor = strip.style.borderTopColor = RiryToolsMainMenu.TabSeparator;
            strip.style.marginBottom = 4;
            for (int i = 0; i < names.Count; i++)
            {
                int tab = i;
                bool on = i == active;
                Color color = on ? (dark ? RiryToolsMainMenu.DarkActiveTab : RiryToolsMainMenu.LightActiveTab)
                    : (dark ? RiryToolsMainMenu.DarkTab : RiryToolsMainMenu.LightTab);
                Color hover = dark ? color + new Color(RiryToolsMainMenu.HoverTint, RiryToolsMainMenu.HoverTint, RiryToolsMainMenu.HoverTint, 0f)
                    : color - new Color(RiryToolsMainMenu.HoverTint / 2f, RiryToolsMainMenu.HoverTint / 2f, RiryToolsMainMenu.HoverTint / 2f, 0f);
                var cell = new Label(names[i]);
                cell.style.flexGrow = 1;
                cell.style.flexBasis = RiryToolsMainMenu.TabMinWidth;
                cell.style.minHeight = RiryToolsMainMenu.TabMinHeight;
                cell.style.unityTextAlign = TextAnchor.MiddleCenter;
                cell.style.whiteSpace = WhiteSpace.Normal;
                cell.style.fontSize = 12;
                cell.style.unityFontStyleAndWeight = on ? FontStyle.Bold : FontStyle.Normal;
                cell.style.paddingLeft = cell.style.paddingRight = 4;
                cell.style.paddingTop = cell.style.paddingBottom = 3;
                cell.style.backgroundColor = color;
                cell.style.borderRightWidth = cell.style.borderBottomWidth = RiryToolsMainMenu.TabBorder;
                cell.style.borderRightColor = cell.style.borderBottomColor = RiryToolsMainMenu.TabSeparator;
                if (!on)
                {
                    cell.RegisterCallback<PointerEnterEvent>(evt => cell.style.backgroundColor = hover);
                    cell.RegisterCallback<PointerLeaveEvent>(evt => cell.style.backgroundColor = color);
                }
                cell.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    // A tab click is not the start of a block drag.
                    evt.StopPropagation();
                    select(tab);
                });
                strip.Add(cell);
            }
            // Every tab as tall as the tallest, as in the Main Menu's strip.
            strip.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float tallest = 0f;
                foreach (VisualElement cell in strip.Children()) tallest = Mathf.Max(tallest, cell.resolvedStyle.height);
                foreach (VisualElement cell in strip.Children())
                {
                    if (Mathf.Abs(cell.resolvedStyle.minHeight.value - tallest) > 0.5f) cell.style.minHeight = tallest;
                }
            });
            return strip;
        }

        private static List<string> Split(string text)
        {
            var parts = new List<string>();
            foreach (string part in text.Split(','))
            {
                if (part.Trim().Length > 0) parts.Add(part.Trim());
            }
            return parts;
        }

        private static bool IsOn(string text)
        {
            string value = text.Trim().ToLowerInvariant();
            return value == "on" || value == "true" || value == "1" || value == "yes";
        }

        private static HelpBoxMessageType HelpType(string text)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "warning": return HelpBoxMessageType.Warning;
                case "error": return HelpBoxMessageType.Error;
                default: return HelpBoxMessageType.Info;
            }
        }

        // "Texture2D" or another UnityEngine type name; Object when not found.
        private static Type AssetType(string name)
        {
            name = name.Trim();
            if (name.Length > 0)
            {
                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.GetName().Name.StartsWith("UnityEngine", StringComparison.Ordinal)) continue;
                    Type type = assembly.GetType("UnityEngine." + name);
                    if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type)) return type;
                }
            }
            return typeof(UnityEngine.Object);
        }

        private static VisualElement Progress(string title)
        {
            var bar = new VisualElement();
            bar.style.height = 18;
            bar.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            var fill = new VisualElement();
            fill.style.position = Position.Absolute;
            fill.style.left = fill.style.top = fill.style.bottom = 0;
            fill.style.width = Length.Percent(60);
            fill.style.backgroundColor = new Color(0.25f, 0.55f, 1f, 0.7f);
            bar.Add(fill);
            var text = new Label(title);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.flexGrow = 1;
            bar.Add(text);
            return bar;
        }

        private static VisualElement ImageBox(string caption)
        {
            var box = new VisualElement();
            box.style.minHeight = 48;
            box.style.justifyContent = Justify.Center;
            box.style.backgroundColor = new Color(0f, 0f, 0f, 0.15f);
            var text = new Label(caption);
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(text);
            return box;
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup block previews
// Menu Mockup (a Debug tool): the model and file format are in Editor/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockupPreview        The Unity control each block type stands for; TabStrip(block, select) a Tabs block's strip,
//                           styled from RiryToolsMainMenu's tab constants so it matches the Main Menu, every tab as tall as
//                           the tallest.
