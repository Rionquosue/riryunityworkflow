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
    /// the top. The model and file format are in Editor/Debug/MenuMockup*.cs.
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

        [SerializeField] private List<MenuMockupBlock> blocks = new List<MenuMockupBlock>();
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
        private MenuMockupBlock pressedBlock;
        private Vector2 pressPosition;
        private VisualElement dragged;
        private MenuMockupBlock draggedBlock;
        private Vector2 grabOffset;
        private string newType;
        private Target pending;
        private double pendingSince;
        private Target placed;
        private double animateUntil;

        // Resizing with a side handle: 0 left, 1 right, 2 top, 3 bottom.
        private MenuMockupBlock resizing;
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

        internal MenuMockupBlock Block(int id)
        {
            return MenuMockupModel.Find(blocks, id);
        }

        private MenuMockupBlock Root
        {
            get
            {
                MenuMockupBlock root = MenuMockupModel.Find(blocks, 0);
                if (root != null) return root;
                root = new MenuMockupBlock { Id = 0, Type = MenuMockupTypes.Window, Padding = 4 };
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
            MenuMockupBlock root = Root;
            canvas.userData = root;
            Style(canvas, root);
            canvas.style.width = StyleKeyword.Auto;
            canvas.style.height = StyleKeyword.Auto;
            foreach (MenuMockupBlock child in MenuMockupModel.Children(blocks, 0)) canvas.Add(Build(child));
        }

        private VisualElement Build(MenuMockupBlock block)
        {
            var element = new VisualElement { userData = block };
            Style(element, block);
            element.style.minWidth = MinBlockSize;
            element.style.minHeight = MinBlockSize;
            element.style.marginLeft = element.style.marginRight = element.style.marginTop = element.style.marginBottom = 2;
            element.style.borderLeftWidth = element.style.borderRightWidth = element.style.borderTopWidth = element.style.borderBottomWidth = 1;
            Color border = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            element.style.borderLeftColor = element.style.borderRightColor = element.style.borderTopColor = element.style.borderBottomColor = border;

            bool tabs = block.Type == MenuMockupTypes.Tabs;
            // A Tabs block's strip stays clickable: a tab shows its blocks.
            VisualElement preview = tabs ? MenuMockupPreview.TabStrip(block, tab => ShowTab(block, tab)) : MenuMockupPreview.Create(block);
            if (preview != null)
            {
                preview.name = MenuMockupPreview.Name;
                preview.style.flexShrink = 0;
                if (!tabs) IgnorePointer(preview);
                element.Add(preview);
            }
            // A Tabs block keeps its strip on top and lays its blocks out in a content area below it, in its own direction.
            VisualElement holder = element;
            if (tabs)
            {
                holder = new VisualElement { name = TabsContentName, userData = new TabsContent(block) };
                holder.style.flexGrow = 1;
                StyleContent(holder, block);
                element.Add(holder);
            }
            foreach (MenuMockupBlock child in MenuMockupModel.Children(blocks, block.Id))
            {
                // In a Tabs block, only the blocks on its shown tab.
                if (tabs && MenuMockupTypes.TabOf(block, child) != Mathf.Clamp(block.ActiveTab, 0, MenuMockupTypes.TabNames(block).Count - 1)) continue;
                holder.Add(Build(child));
            }
            for (int side = 0; side < 4; side++) element.Add(Handle(element, block, side));

            element.RegisterCallback<PointerDownEvent>(evt => OnBlockDown(evt, element, block));
            element.RegisterCallback<GeometryChangedEvent>(evt => Slide(element, evt));
            element.AddManipulator(new ContextualMenuManipulator(evt => Menu(evt, block)));
            return element;
        }

        private void ShowTab(MenuMockupBlock block, int tab)
        {
            if (block.ActiveTab == tab) return;
            Record("Show tab");
            block.ActiveTab = tab;
            Rebuild();
        }

        // Marks a Tabs block's content area: drops there go into the Tabs block, on its shown tab.
        private sealed class TabsContent
        {
            internal readonly MenuMockupBlock Block;

            internal TabsContent(MenuMockupBlock block)
            {
                Block = block;
            }
        }

        private const string TabsContentName = "mockup-tabs-content";

        // The block a container element holds blocks for: a block, the window, or a Tabs block's content area.
        private static MenuMockupBlock BlockOf(VisualElement container)
        {
            var content = container.userData as TabsContent;
            return content != null ? content.Block : (MenuMockupBlock)container.userData;
        }

        private static void Style(VisualElement element, MenuMockupBlock block)
        {
            // A Tabs block itself is always a column (strip, then content); its direction is its content area's.
            bool tabs = block.Type == MenuMockupTypes.Tabs && element.userData is MenuMockupBlock && block.Id != 0;
            element.style.flexDirection = tabs || block.Direction != "row" ? FlexDirection.Column : FlexDirection.Row;
            element.style.justifyContent = tabs ? Justify.FlexStart : JustifyOf(block.Justify);
            element.style.alignItems = tabs ? Align.Stretch : AlignOf(block.Align);
            element.style.flexGrow = block.Grow;
            element.style.paddingLeft = element.style.paddingRight = element.style.paddingTop = element.style.paddingBottom = block.Padding;
            element.style.width = block.Width >= 0 ? new StyleLength(block.Width) : new StyleLength(StyleKeyword.Auto);
            element.style.height = block.Height >= 0 ? new StyleLength(block.Height) : new StyleLength(StyleKeyword.Auto);
        }

        private static void StyleContent(VisualElement content, MenuMockupBlock block)
        {
            content.style.flexDirection = block.Direction == "row" ? FlexDirection.Row : FlexDirection.Column;
            content.style.justifyContent = JustifyOf(block.Justify);
            content.style.alignItems = AlignOf(block.Align);
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

        private VisualElement Handle(VisualElement element, MenuMockupBlock block, int side)
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

        private void OnBlockDown(PointerDownEvent evt, VisualElement element, MenuMockupBlock block)
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
                MenuMockupModel.PlaceShown(blocks, draggedBlock, BlockOf(target.Parent).Id, target.Index);
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
            while (element != null && element != canvas && !(element.userData is MenuMockupBlock) && !(element.userData is TabsContent)) element = element.parent;
            if (element == null) return null;
            if (element == canvas) return new Target(canvas, IndexAt(canvas, at));
            if (element.userData is TabsContent) return new Target(element, IndexAt(element, at));

            VisualElement parent = element.parent;
            Rect bounds = element.worldBound;
            bool row = parent.resolvedStyle.flexDirection == FlexDirection.Row;
            float along = row ? (at.x - bounds.xMin) / Mathf.Max(1f, bounds.width) : (at.y - bounds.yMin) / Mathf.Max(1f, bounds.height);
            int index = BlockChildren(parent).IndexOf(element);
            if (along < 0.25f) return new Target(parent, index);
            if (along > 0.75f) return new Target(parent, index + 1);
            // The middle of a Tabs block: its content area.
            VisualElement inside = ((MenuMockupBlock)element.userData).Type == MenuMockupTypes.Tabs ? element.Q<VisualElement>(TabsContentName) ?? element : element;
            return new Target(inside, IndexAt(inside, at));
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
                if (child.userData is MenuMockupBlock) children.Add(child);
            }
            return children;
        }

        private static int BlockIndex(VisualElement container, VisualElement element)
        {
            int index = 0;
            foreach (VisualElement child in container.Children())
            {
                if (child == element) return index;
                if (child.userData is MenuMockupBlock) index++;
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
            MenuMockupBlock block = MenuMockupModel.Create(blocks, type);
            MenuMockupModel.PlaceShown(blocks, block, BlockOf(target.Parent).Id, target.Index);
            EndDrag();
            Rebuild();
        }

        /// <summary>Components click: adds a block of the type as the window's last block.</summary>
        internal void Append(string type)
        {
            Record("Add " + type);
            MenuMockupBlock block = MenuMockupModel.Create(blocks, type);
            MenuMockupModel.Place(blocks, block, 0, int.MaxValue);
            Rebuild();
        }

        private void CancelNew()
        {
            if (newType != null) EndDrag();
        }

        // ---- Right-click layout menu ------------------------------------------

        private void Menu(ContextualMenuPopulateEvent evt, MenuMockupBlock block)
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
            // A block inside a Tabs block can go to another of its tabs.
            MenuMockupBlock owner = MenuMockupModel.Find(blocks, block.Parent);
            if (block.Id != 0 && owner != null && owner.Type == MenuMockupTypes.Tabs)
            {
                List<string> names = MenuMockupTypes.TabNames(owner);
                int on = MenuMockupTypes.TabOf(owner, block);
                for (int i = 0; i < names.Count; i++)
                {
                    int tab = i;
                    evt.menu.AppendAction("Tab/" + names[i].Replace("/", "\u2215"), action =>
                    {
                        Record("Move to tab");
                        block.Tab = tab;
                        Rebuild();
                    }, action => tab == on ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }
            }
            // The window itself cannot be deleted.
            if (block.Id == 0) return;
            // A Tabs block holds blocks on tabs not shown: the entry says how many go with it.
            int inside = Descendants(block.Id);
            string delete = block.Type == MenuMockupTypes.Tabs && inside > 0 ? "Delete (with the " + inside + (inside == 1 ? " block" : " blocks") + " on its tabs)" : "Delete";
            evt.menu.AppendAction(delete, action =>
            {
                Record("Delete");
                MenuMockupModel.Delete(blocks, block);
                Rebuild();
            });
        }

        private int Descendants(int id)
        {
            int count = 0;
            foreach (MenuMockupBlock child in MenuMockupModel.Children(blocks, id)) count += 1 + Descendants(child.Id);
            return count;
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
                MenuMockupFile.Save(path, blocks, canvas.layout.width, canvas.layout.height);
                savePath = path;
                RiryLog.Info(RiryLog.MenuMockup, "Saved " + path);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuMockup, "Could not save " + path, exception);
                EditorUtility.DisplayDialog("Save Mockup", "Could not save " + path + ":\n" + exception.Message, "OK");
            }
        }

        internal void Load(string path, List<MenuMockupBlock> loaded, float width, float height)
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
            blocks = new List<MenuMockupBlock>();
            savePath = null;
            Rebuild();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Mockup Edit window
// Menu Mockup (a Debug tool): the model and file format are in Editor/Debug/MenuMockup*.cs; the menu entry is in Editor/UI/RiryToolsMenuBar.cs.
// ============================================================================
//
// MenuMockupEditWindow ("Edit"): the mockup
//   blocks / savePath        The blocks (MenuMockupBlock, flat list) and the file Save updates (null until named or loaded).
//   ratio / canvas / overlay  Aspect ratio label at the top; the mockup (root block); the layer the dragged block moves on.
//   HoverSeconds (0.5) / SlideMs (150)  Hover time before the gap moves; slide time of the blocks around it.
//   Rebuild() / Build(block) / Style(element, block)  Draw the blocks from the model.
//   Handle(element, block, side) / Resize(at)  The four side handles; they set Width or Height.
//   OnBlockDown / OnPointerMove / OnPointerUp  Double-click opens MenuMockupTextWindow; dragging moves a block.
//   BeginDrag / Track / CheckHover / Slide / EndDrag  The gap (placeholder) and its animation.
//   TargetAt(at)             Before / after a block at its edges, inside it in its middle, or in the window.
//   OnDragUpdated / OnDragPerform  A component dragged in from Components.
//   Append(type)             A component clicked in Components: added as the window's last block.
//   Menu(evt, block)         Right-click: Direction, Justify, Align, Grow, Padding, Auto size, Tab (a block inside a Tabs
//                           block: the tab it is on), Delete (not the window).
//   Tabs block               Build draws its strip (MenuMockupPreview.TabStrip, clickable) on top and, below it, a content area
//                           (TabsContentName, userData TabsContent; StyleContent gives it the block's direction, justify and
//                           align, the block itself staying a column) with only the blocks on its shown tab (ActiveTab);
//                           ShowTab switches it (undoable). TargetAt drops into the content area; BlockOf reads the block of
//                           any container; MenuMockupModel.PlaceShown puts a dropped block on the shown tab. Delete says how many
//                           blocks a Tabs block holds on its tabs (Descendants).
//   Record(action) / OnUndoRedo()  Undo.RecordObject before each change; Undo and Redo draw the blocks again.
//   Save(path) / Load(path, blocks, width, height) / New()  Through MenuMockupFile; New empties the mockup and its name.
