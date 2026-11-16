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
    /// <summary>
    /// Components: a button per block type, clicked to add it at the bottom of Edit or
    /// dragged into place, with New, Save Mockup
    /// and Load. New starts an empty mockup. Save names a new mockup (Documents/RiryTools/Mockups/&lt;name&gt;.yml) and
    /// updates the file once it has one; Load opens a saved mockup in Edit.
    /// </summary>
    internal sealed class MenuMockupComponentsWindow : EditorWindow
    {
        private string pressedType;
        private Vector2 pressPosition;

        internal static void Open(MenuMockupEditWindow edit)
        {
            MenuMockupComponentsWindow window = GetWindow<MenuMockupComponentsWindow>(true, "Components");
            window.minSize = new Vector2(180f, 300f);
            Rect beside = edit.position;
            window.position = new Rect(beside.xMax + 8f, beside.y, 200f, Mathf.Max(beside.height, 420f));
            window.Show();
        }

        private static MenuMockupEditWindow Edit()
        {
            return HasOpenInstances<MenuMockupEditWindow>() ? GetWindow<MenuMockupEditWindow>(true, "Edit") : MenuMockupEditWindow.Open();
        }

        private void CreateGUI()
        {
            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexShrink = 0;
            var create = new Button(() => Edit().New()) { text = "New" };
            create.style.flexGrow = 1;
            var save = new Button(Save) { text = "Save Mockup" };
            save.style.flexGrow = 1;
            var load = new Button(Load) { text = "Load" };
            load.style.flexGrow = 1;
            buttons.Add(create);
            buttons.Add(save);
            buttons.Add(load);
            rootVisualElement.Add(buttons);

            var list = new ScrollView();
            list.style.flexGrow = 1;
            foreach (MenuMockupTypes.Kind kind in MenuMockupTypes.All)
            {
                string type = kind.Name;
                // Drawn as a button; its own pointer handling tells a click from a drag.
                var item = new Label(type);
                item.AddToClassList(Button.ussClassName);
                item.tooltip = "Click to add it at the bottom of Edit, or drag it into place.";
                item.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    pressedType = type;
                    pressPosition = evt.position;
                    item.CapturePointer(evt.pointerId);
                });
                item.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (pressedType != type || (evt.pressedButtons & 1) == 0) return;
                    if (((Vector2)evt.position - pressPosition).magnitude < 4f) return;
                    pressedType = null;
                    if (item.HasPointerCapture(evt.pointerId)) item.ReleasePointer(evt.pointerId);
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new UnityEngine.Object[0];
                    DragAndDrop.SetGenericData(MenuMockupEditWindow.DragKey, type);
                    DragAndDrop.StartDrag(type);
                });
                item.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (item.HasPointerCapture(evt.pointerId)) item.ReleasePointer(evt.pointerId);
                    bool clicked = pressedType == type && item.worldBound.Contains(evt.position);
                    pressedType = null;
                    if (clicked) Edit().Append(type);
                });
                list.Add(item);
            }
            rootVisualElement.Add(list);
        }

        // A named mockup is updated; a new one asks for a name first.
        private void Save()
        {
            MenuMockupEditWindow edit = Edit();
            if (!string.IsNullOrEmpty(edit.SavePath)) edit.Save(edit.SavePath);
            else MenuMockupNameWindow.Open(edit);
        }

        private void Load()
        {
            Directory.CreateDirectory(MenuMockup.Folder);
            string path = EditorUtility.OpenFilePanel("Load", MenuMockup.Folder, MenuMockup.FileExtension);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                float width, height;
                List<MenuMockupBlock> loaded = MenuMockupFile.Load(path, out width, out height);
                Edit().Load(path, loaded, width, height);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuMockup, "Could not load " + path, exception);
                EditorUtility.DisplayDialog("Load", "Could not load " + path + ":\n" + exception.Message, "OK");
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup Components window
// Menu Mockup (a Debug tool): the model and file format are in Editor/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockupComponentsWindow ("Components"): New, Save Mockup and Load, then a button per block type:
//                           click to add it at the bottom of Edit, or drag it into place.
