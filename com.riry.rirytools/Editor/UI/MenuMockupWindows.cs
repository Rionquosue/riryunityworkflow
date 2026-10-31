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
    /// Edit: the mockup window itself. Every component is a block with a handle in the
    /// middle of each side for resizing; blocks lay out like flexbox and hold blocks.
    /// Drag a block to move it: holding it over a spot for half a second slides the
    /// other blocks aside to show where it goes. Double-click a block to edit its text;
    /// right-click it (or the empty window) for its layout, Auto size and Delete. Every
    /// change can be undone (Edit > Undo). The aspect ratio of the mockup is shown at
    /// the top. The model and file format are in Editor/MenuMockup.cs.
    /// </summary>
    internal sealed class MenuMockupEditWindow : EditorWindow
    {
        internal const string DragKey = "RiryTools.MenuMockup.Type";
        private const float BarHeight = 20f;
        private const double HoverSeconds = 0.5;
        private const int SlideMs = 150;
        private const float HandleSize = 6f;
        private const float DragThreshold = 4f;
        private const float MinBlockSize = 12f;
        private static readonly Vector2 NewBlockSize = new Vector2(80f, 20f);

        [SerializeField] private List<MockupBlock> blocks = new List<MockupBlock>();
        [SerializeField] private string savePath;

        private Label ratio;
        private VisualElement canvas;
        private VisualElement overlay;
        private VisualElement placeholder;

        // A drop position: index among the parent's blocks (the dragged block and the gap not counted).
        private sealed class Target
        {
            internal readonly VisualElement Parent;
            internal readonly int Index;

            internal Target(VisualElement parent, int index)
            {
                Parent = parent;
                Index = index;
            }

            internal bool Same(Target other)
            {
                return other != null && other.Parent == Parent && other.Index == Index;
            }
        }

        // Moving a block, or placing a new one from Components (newType).
        private VisualElement pressed;
        private MockupBlock pressedBlock;
        private Vector2 pressPosition;
        private VisualElement dragged;
        private MockupBlock draggedBlock;
        private Vector2 grabOffset;
        private string newType;
        private Target pending;
        private double pendingSince;
        private Target placed;
        private double animateUntil;

        // Resizing with a side handle: 0 left, 1 right, 2 top, 3 bottom.
        private MockupBlock resizing;
        private VisualElement resizingElement;
        private int resizeSide;
        private Vector2 resizeStart;
        private Vector2 sizeStart;

        internal static MenuMockupEditWindow Open()
        {
            MenuMockupEditWindow window = GetWindow<MenuMockupEditWindow>(true, "Edit");
            window.minSize = new Vector2(120f, 80f);
            window.Show();
            return window;
        }

        internal string SavePath
        {
            get { return savePath; }
        }

        internal MockupBlock Block(int id)
        {
            return MockupModel.Find(blocks, id);
        }

        private MockupBlock Root
        {
            get
            {
                MockupBlock root = MockupModel.Find(blocks, 0);
                if (root != null) return root;
                root = new MockupBlock { Id = 0, Type = MockupTypes.Window, Padding = 4 };
                blocks.Insert(0, root);
                return root;
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            EndDrag();
            pressed = null;
            pressedBlock = null;
            resizing = null;
            resizingElement = null;
            Rebuild();
        }

        /// <summary>Records the mockup for Undo before a change.</summary>
        internal void Record(string action)
        {
            Undo.RecordObject(this, "Menu Mockup: " + action);
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            ratio = new Label();
            ratio.style.height = BarHeight;
            ratio.style.flexShrink = 0;
            ratio.style.unityTextAlign = TextAnchor.MiddleCenter;
            ratio.style.borderBottomWidth = 1;
            ratio.style.borderBottomColor = new Color(0f, 0f, 0f, 0.3f);
            root.Add(ratio);

            var area = new VisualElement();
            area.style.flexGrow = 1;
            canvas = new VisualElement();
            canvas.style.flexGrow = 1;
            area.Add(canvas);
            overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            area.Add(overlay);
            root.Add(area);

            canvas.RegisterCallback<GeometryChangedEvent>(evt =>
                ratio.text = MenuMockup.AspectRatio(canvas.layout.width, canvas.layout.height));
            canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            canvas.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            canvas.RegisterCallback<DragPerformEvent>(OnDragPerform);
            canvas.RegisterCallback<DragLeaveEvent>(evt =>
            {
                if (evt.target == canvas) CancelNew();
            });
            canvas.RegisterCallback<DragExitedEvent>(evt => CancelNew());
            canvas.AddManipulator(new ContextualMenuManipulator(evt => Menu(evt, Root)));
            canvas.schedule.Execute(CheckHover).Every(50);
            Rebuild();
        }

        /// <summary>Draws every block again from the model.</summary>
        internal void Rebuild()
        {
            if (canvas == null) return;
            canvas.Clear();
            MockupBlock root = Root;
            canvas.userData = root;
            Style(canvas, root);
            canvas.style.width = StyleKeyword.Auto;
            canvas.style.height = StyleKeyword.Auto;
            foreach (MockupBlock child in MockupModel.Children(blocks, 0)) canvas.Add(Build(child));
        }

        private VisualElement Build(MockupBlock block)
        {
            var element = new VisualElement { userData = block };
            Style(element, block);
            element.style.minWidth = MinBlockSize;
            element.style.minHeight = MinBlockSize;
            element.style.marginLeft = element.style.marginRight = element.style.marginTop = element.style.marginBottom = 2;
            element.style.borderLeftWidth = element.style.borderRightWidth = element.style.borderTopWidth = element.style.borderBottomWidth = 1;
            Color border = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            element.style.borderLeftColor = element.style.borderRightColor = element.style.borderTopColor = element.style.borderBottomColor = border;

            VisualElement preview = MenuMockupPreview.Create(block);
            if (preview != null)
            {
                preview.name = MenuMockupPreview.Name;
                preview.style.flexShrink = 0;
                IgnorePointer(preview);
                element.Add(preview);
            }
            foreach (MockupBlock child in MockupModel.Children(blocks, block.Id)) element.Add(Build(child));
            for (int side = 0; side < 4; side++) element.Add(Handle(element, block, side));

            element.RegisterCallback<PointerDownEvent>(evt => OnBlockDown(evt, element, block));
            element.RegisterCallback<GeometryChangedEvent>(evt => Slide(element, evt));
            element.AddManipulator(new ContextualMenuManipulator(evt => Menu(evt, block)));
            return element;
        }

        private static void Style(VisualElement element, MockupBlock block)
        {
            element.style.flexDirection = block.Direction == "row" ? FlexDirection.Row : FlexDirection.Column;
            element.style.justifyContent = JustifyOf(block.Justify);
            element.style.alignItems = AlignOf(block.Align);
            element.style.flexGrow = block.Grow;
            element.style.paddingLeft = element.style.paddingRight = element.style.paddingTop = element.style.paddingBottom = block.Padding;
            element.style.width = block.Width >= 0 ? new StyleLength(block.Width) : new StyleLength(StyleKeyword.Auto);
            element.style.height = block.Height >= 0 ? new StyleLength(block.Height) : new StyleLength(StyleKeyword.Auto);
        }

        private static Justify JustifyOf(string value)
        {
            switch (value)
            {
                case "center": return Justify.Center;
                case "end": return Justify.FlexEnd;
                case "space-between": return Justify.SpaceBetween;
                case "space-around": return Justify.SpaceAround;
                default: return Justify.FlexStart;
            }
        }

        private static Align AlignOf(string value)
        {
            switch (value)
            {
                case "start": return Align.FlexStart;
                case "center": return Align.Center;
                case "end": return Align.FlexEnd;
                default: return Align.Stretch;
            }
        }

        private static void IgnorePointer(VisualElement element)
        {
            element.pickingMode = PickingMode.Ignore;
            element.Query<VisualElement>().ForEach(child => child.pickingMode = PickingMode.Ignore);
        }

        // ---- Side handles ---------------------------------------------------

        private VisualElement Handle(VisualElement element, MockupBlock block, int side)
        {
            var handle = new VisualElement();
            handle.style.position = Position.Absolute;
            handle.style.width = handle.style.height = HandleSize;
            handle.style.backgroundColor = new Color(0.25f, 0.55f, 1f, 0.9f);
            float offset = -HandleSize / 2f;
            if (side == 0 || side == 1)
            {
                handle.style.top = Length.Percent(50);
                handle.style.marginTop = offset;
                if (side == 0) handle.style.left = offset;
                else handle.style.right = offset;
            }
            else
            {
                handle.style.left = Length.Percent(50);
                handle.style.marginLeft = offset;
                if (side == 2) handle.style.top = offset;
                else handle.style.bottom = offset;
            }
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                evt.StopPropagation();
                Record("Resize");
                resizing = block;
                resizingElement = element;
                resizeSide = side;
                resizeStart = evt.position;
                sizeStart = element.layout.size;
                canvas.CapturePointer(evt.pointerId);
            });
            return handle;
        }

        private void Resize(Vector2 at)
        {
            Vector2 moved = at - resizeStart;
            switch (resizeSide)
            {
                case 0: resizing.Width = Mathf.Max(MinBlockSize, sizeStart.x - moved.x); break;
                case 1: resizing.Width = Mathf.Max(MinBlockSize, sizeStart.x + moved.x); break;
                case 2: resizing.Height = Mathf.Max(MinBlockSize, sizeStart.y - moved.y); break;
                default: resizing.Height = Mathf.Max(MinBlockSize, sizeStart.y + moved.y); break;
            }
            Style(resizingElement, resizing);
        }

        // ---- Moving blocks --------------------------------------------------

        private void OnBlockDown(PointerDownEvent evt, VisualElement element, MockupBlock block)
        {
            if (evt.button != 0) return;
            evt.StopPropagation();
            if (evt.clickCount == 2)
            {
                MenuMockupTextWindow.Open(this, block, position.position + (Vector2)evt.position);
                return;
            }
            pressed = element;
            pressedBlock = block;
            pressPosition = evt.position;
            canvas.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (resizing != null)
            {
                Resize(evt.position);
                return;
            }
            if (pressed == null) return;
            Vector2 at = evt.position;
            if (dragged == null)
            {
                if ((at - pressPosition).magnitude < DragThreshold) return;
                BeginDrag(at);
            }
            Vector2 local = overlay.WorldToLocal(at - grabOffset);
            dragged.style.left = local.x;
            dragged.style.top = local.y;
            Track(at);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (canvas.HasPointerCapture(evt.pointerId)) canvas.ReleasePointer(evt.pointerId);
            if (resizing != null)
            {
                resizing = null;
                resizingElement = null;
                return;
            }
            if (dragged != null)
            {
                Target target = TargetAt(evt.position) ?? placed;
                Record("Move");
                MockupModel.Place(blocks, draggedBlock, ((MockupBlock)target.Parent.userData).Id, target.Index);
                EndDrag();
                Rebuild();
            }
            pressed = null;
            pressedBlock = null;
        }

        // The block leaves the layout and follows the pointer; a gap keeps its place.
        private void BeginDrag(Vector2 at)
        {
            dragged = pressed;
            draggedBlock = pressedBlock;
            Rect bounds = dragged.worldBound;
            grabOffset = at - bounds.position;
            VisualElement parent = dragged.parent;
            placeholder = Placeholder(bounds.size);
            parent.Insert(parent.IndexOf(dragged), placeholder);
            dragged.RemoveFromHierarchy();
            placed = new Target(parent, BlockIndex(parent, placeholder));
            pending = placed;
            pendingSince = EditorApplication.timeSinceStartup;
            overlay.Add(dragged);
            dragged.style.position = Position.Absolute;
            dragged.style.width = bounds.width;
            dragged.style.height = bounds.height;
            dragged.style.opacity = 0.6f;
            IgnorePointer(dragged);
        }

        private void EndDrag()
        {
            if (placeholder != null) placeholder.RemoveFromHierarchy();
            placeholder = null;
            dragged = null;
            draggedBlock = null;
            newType = null;
            pending = null;
            placed = null;
        }

        private void Track(Vector2 at)
        {
            Target target = TargetAt(at);
            if (target == null || target.Same(pending)) return;
            pending = target;
            pendingSince = EditorApplication.timeSinceStartup;
        }

        // After the pointer has stayed on one spot for half a second, the gap slides there.
        private void CheckHover()
        {
            if ((dragged == null && newType == null) || pending == null || pending.Same(placed)) return;
            if (EditorApplication.timeSinceStartup - pendingSince < HoverSeconds) return;
            if (placeholder == null) placeholder = Placeholder(NewBlockSize);
            animateUntil = EditorApplication.timeSinceStartup + 0.2;
            placeholder.RemoveFromHierarchy();
            InsertAt(pending.Parent, pending.Index, placeholder);
            placed = pending;
        }

        // Blocks that moved because the gap moved slide from where they were.
        private void Slide(VisualElement element, GeometryChangedEvent evt)
        {
            if (EditorApplication.timeSinceStartup > animateUntil || element == dragged) return;
            if (evt.oldRect.width <= 0 && evt.oldRect.height <= 0) return;
            Vector2 delta = evt.oldRect.position - evt.newRect.position;
            if (delta.sqrMagnitude < 0.25f) return;
            element.experimental.animation.Start(1f, 0f, SlideMs,
                (target, t) => target.style.translate = new Translate(delta.x * t, delta.y * t, 0));
        }

        /// <summary>
        /// Where a block dropped at the point goes: the edges of a block (along its parent's
        /// direction) are before or after it, its middle is inside it. Null outside the mockup.
        /// </summary>
        private Target TargetAt(Vector2 at)
        {
            if (canvas.panel == null) return null;
            VisualElement picked = canvas.panel.Pick(at);
            if (picked == null) return null;
            if (placeholder != null && (picked == placeholder || placeholder.Contains(picked))) return placed;
            VisualElement element = picked;
            while (element != null && element != canvas && !(element.userData is MockupBlock)) element = element.parent;
            if (element == null) return null;
            if (element == canvas) return new Target(canvas, IndexAt(canvas, at));

            VisualElement parent = element.parent;
            Rect bounds = element.worldBound;
            bool row = parent.resolvedStyle.flexDirection == FlexDirection.Row;
            float along = row ? (at.x - bounds.xMin) / Mathf.Max(1f, bounds.width) : (at.y - bounds.yMin) / Mathf.Max(1f, bounds.height);
            int index = BlockChildren(parent).IndexOf(element);
            if (along < 0.25f) return new Target(parent, index);
            if (along > 0.75f) return new Target(parent, index + 1);
            return new Target(element, IndexAt(element, at));
        }

        private static int IndexAt(VisualElement container, Vector2 at)
        {
            List<VisualElement> children = BlockChildren(container);
            bool row = container.resolvedStyle.flexDirection == FlexDirection.Row;
            for (int i = 0; i < children.Count; i++)
            {
                Vector2 center = children[i].worldBound.center;
                if (row ? at.x < center.x : at.y < center.y) return i;
            }
            return children.Count;
        }

        private static List<VisualElement> BlockChildren(VisualElement container)
        {
            var children = new List<VisualElement>();
            foreach (VisualElement child in container.Children())
            {
                if (child.userData is MockupBlock) children.Add(child);
            }
            return children;
        }

        private static int BlockIndex(VisualElement container, VisualElement element)
        {
            int index = 0;
            foreach (VisualElement child in container.Children())
            {
                if (child == element) return index;
                if (child.userData is MockupBlock) index++;
            }
            return index;
        }

        // Inserts before the index-th block, else after the last block (or the preview).
        private static void InsertAt(VisualElement container, int index, VisualElement element)
        {
            List<VisualElement> children = BlockChildren(container);
            if (index < children.Count) container.Insert(container.IndexOf(children[index]), element);
            else if (children.Count > 0) container.Insert(container.IndexOf(children[children.Count - 1]) + 1, element);
            else container.Insert(container.childCount > 0 && container[0].name == MenuMockupPreview.Name ? 1 : 0, element);
        }

        private static VisualElement Placeholder(Vector2 size)
        {
            var gap = new VisualElement { pickingMode = PickingMode.Position };
            gap.style.width = size.x;
            gap.style.height = size.y;
            gap.style.flexShrink = 0;
            gap.style.marginLeft = gap.style.marginRight = gap.style.marginTop = gap.style.marginBottom = 2;
            gap.style.backgroundColor = new Color(0.25f, 0.55f, 1f, 0.25f);
            return gap;
        }

        // ---- New blocks from Components ---------------------------------------

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            string type = DragAndDrop.GetGenericData(DragKey) as string;
            if (type == null) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (newType == null)
            {
                newType = type;
                pendingSince = EditorApplication.timeSinceStartup;
            }
            Track(evt.mousePosition);
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            string type = DragAndDrop.GetGenericData(DragKey) as string;
            if (type == null) return;
            DragAndDrop.AcceptDrag();
            Target target = TargetAt(evt.mousePosition) ?? placed ?? new Target(canvas, BlockChildren(canvas).Count);
            Record("Add " + type);
            MockupBlock block = MockupModel.Create(blocks, type);
            MockupModel.Place(blocks, block, ((MockupBlock)target.Parent.userData).Id, target.Index);
            EndDrag();
            Rebuild();
        }

        /// <summary>Components click: adds a block of the type as the window's last block.</summary>
        internal void Append(string type)
        {
            Record("Add " + type);
            MockupBlock block = MockupModel.Create(blocks, type);
            MockupModel.Place(blocks, block, 0, int.MaxValue);
            Rebuild();
        }

        private void CancelNew()
        {
            if (newType != null) EndDrag();
        }

        // ---- Right-click layout menu ------------------------------------------

        private void Menu(ContextualMenuPopulateEvent evt, MockupBlock block)
        {
            // Only the block under the pointer, not the blocks around it.
            evt.StopPropagation();
            Choices(evt.menu, "Direction/", new[] { "Row", "Column" }, new[] { "row", "column" }, block.Direction,
                value => block.Direction = value);
            Choices(evt.menu, "Justify/", new[] { "Start", "Center", "End", "Space Between", "Space Around" },
                new[] { "start", "center", "end", "space-between", "space-around" }, block.Justify, value => block.Justify = value);
            Choices(evt.menu, "Align/", new[] { "Stretch", "Start", "Center", "End" },
                new[] { "stretch", "start", "center", "end" }, block.Align, value => block.Align = value);
            string[] grow = { "0", "1", "2", "3" };
            Choices(evt.menu, "Grow/", grow, grow, Number(block.Grow), value => block.Grow = float.Parse(value, CultureInfo.InvariantCulture));
            string[] padding = { "0", "2", "4", "8", "16" };
            Choices(evt.menu, "Padding/", padding, padding, Number(block.Padding),
                value => block.Padding = float.Parse(value, CultureInfo.InvariantCulture));
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Auto size", action =>
            {
                Record("Auto size");
                block.Width = -1;
                block.Height = -1;
                Rebuild();
            }, action => block.Width >= 0 || block.Height >= 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            // The window itself cannot be deleted.
            if (block.Id == 0) return;
            evt.menu.AppendAction("Delete", action =>
            {
                Record("Delete");
                MockupModel.Delete(blocks, block);
                Rebuild();
            });
        }

        private void Choices(DropdownMenu menu, string path, string[] names, string[] values, string current, Action<string> set)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string value = values[i];
                menu.AppendAction(path + names[i], action =>
                {
                    Record(path.TrimEnd('/'));
                    set(value);
                    Rebuild();
                }, action => value == current ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }

        private static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // ---- Save and Load ----------------------------------------------------

        internal void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                MockupFile.Save(path, blocks, canvas.layout.width, canvas.layout.height);
                savePath = path;
                Debug.Log("Menu Mockup: saved " + path);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Save Mockup", "Could not save " + path + ":\n" + exception.Message, "OK");
            }
        }

        internal void Load(string path, List<MockupBlock> loaded, float width, float height)
        {
            Record("Load");
            blocks = loaded;
            savePath = path;
            Rebuild();
            if (width > 0 && height > 0) position = new Rect(position.x, position.y, width, height + BarHeight);
        }

        /// <summary>Starts an empty mockup without a name, so the next Save asks for one.</summary>
        internal void New()
        {
            Record("New");
            blocks = new List<MockupBlock>();
            savePath = null;
            Rebuild();
        }
    }

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
            foreach (MockupTypes.Kind kind in MockupTypes.All)
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
                List<MockupBlock> loaded = MockupFile.Load(path, out width, out height);
                Edit().Load(path, loaded, width, height);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Load", "Could not load " + path + ":\n" + exception.Message, "OK");
            }
        }
    }

    /// <summary>Double-click in Edit: the block's texts, applied as you type.</summary>
    internal sealed class MenuMockupTextWindow : EditorWindow
    {
        private MenuMockupEditWindow owner;
        private int blockId;

        internal static void Open(MenuMockupEditWindow owner, MockupBlock block, Vector2 screenPoint)
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
            MockupBlock block = owner != null ? owner.Block(blockId) : null;
            if (block == null)
            {
                Close();
                return;
            }
            MockupTypes.Kind kind = MockupTypes.Find(block.Type);
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

    /// <summary>Save Mockup for a mockup without a name: asks for one.</summary>
    internal sealed class MenuMockupNameWindow : EditorWindow
    {
        private MenuMockupEditWindow owner;
        private string mockupName = "";
        private string error;

        internal static void Open(MenuMockupEditWindow owner)
        {
            var window = CreateInstance<MenuMockupNameWindow>();
            window.owner = owner;
            window.titleContent = new GUIContent("Save Mockup");
            Rect over = owner.position;
            window.position = new Rect(over.center.x - 150f, over.center.y - 40f, 300f, 80f);
            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            mockupName = EditorGUILayout.TextField("Name", mockupName);
            // A name problem shows only after Save was clicked with it.
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f))) Close();
                if (GUILayout.Button("Save", GUILayout.Width(80f))) Save();
            }
        }

        private void Save()
        {
            string chosen = mockupName.Trim();
            error = ImportPresetLibrary.ValidateName(chosen);
            if (error != null || owner == null) return;
            string path = Path.Combine(MenuMockup.Folder, chosen + "." + MenuMockup.FileExtension);
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Save Mockup", chosen + "." + MenuMockup.FileExtension +
                    " exists. Replace it?", "Replace", "Cancel"))
                return;
            owner.Save(path);
            Close();
        }
    }

    /// <summary>What each block type looks like in Edit: the Unity control it stands for.</summary>
    internal static class MenuMockupPreview
    {
        internal const string Name = "mockup-preview";

        internal static VisualElement Create(MockupBlock block)
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
// GLOSSARY: Menu Mockup windows
// The model and file format are in Editor/MenuMockup.cs; the menu entry is in RiryMenus.cs.
// ============================================================================
//
// MenuMockupEditWindow ("Edit"): the mockup
//   blocks / savePath        The blocks (MockupBlock, flat list) and the file Save updates (null until named or loaded).
//   ratio / canvas / overlay  Aspect ratio label at the top; the mockup (root block); the layer the dragged block moves on.
//   HoverSeconds (0.5) / SlideMs (150)  Hover time before the gap moves; slide time of the blocks around it.
//   Rebuild() / Build(block) / Style(element, block)  Draw the blocks from the model.
//   Handle(element, block, side) / Resize(at)  The four side handles; they set Width or Height.
//   OnBlockDown / OnPointerMove / OnPointerUp  Double-click opens MenuMockupTextWindow; dragging moves a block.
//   BeginDrag / Track / CheckHover / Slide / EndDrag  The gap (placeholder) and its animation.
//   TargetAt(at)             Before / after a block at its edges, inside it in its middle, or in the window.
//   OnDragUpdated / OnDragPerform  A component dragged in from Components.
//   Append(type)             A component clicked in Components: added as the window's last block.
//   Menu(evt, block)         Right-click: Direction, Justify, Align, Grow, Padding, Auto size, Delete (not the window).
//   Record(action) / OnUndoRedo()  Undo.RecordObject before each change; Undo and Redo draw the blocks again.
//   Save(path) / Load(path, blocks, width, height) / New()  Through MockupFile; New empties the mockup and its name.
//
// MenuMockupComponentsWindow ("Components"): New, Save Mockup and Load, then a button per block type:
//                           click to add it at the bottom of Edit, or drag it into place.
// MenuMockupTextWindow ("Edit Text"): the block's Label and Content, named per type (MockupTypes.Kind).
// MenuMockupNameWindow ("Save Mockup"): asks for the name of a new mockup; a name problem shows after Save.
// MenuMockupPreview        The Unity control each block type stands for.
