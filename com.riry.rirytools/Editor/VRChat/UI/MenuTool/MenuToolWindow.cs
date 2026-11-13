using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Menu Tool: traces expression menu controls to their sources (descriptor, Modular Avatar, VRCFury), compares the
    /// parameter budget with and without VRCFury's Parameter Compressor (Parameter budget tab), previews the assembled
    /// radial menu in Edit Mode with Gesture Manager's radial, and plans moves that Apply makes through the system that
    /// owns each control (Menu manager tab). Planning only writes the plan JSON; Apply is one undo step.
    /// Opened from Tools > RiryTools > Menu Tool (its own window) or as the Main Menu's Menu Tool tab.
    /// </summary>
    internal sealed class MenuToolWindow : EditorWindow, IRiryHostedTool
    {
        internal const string Title = "Menu Tool";
        private static readonly string[] Tabs = { "Parameter budget", "Menu manager" };
        internal const float MinWidth = 640f;
        private const float MinHeight = 760f;
        // Gesture Manager's radial is drawn at this size.
        private const float RadialSize = 300f;
        // The target field's object picker lists only Hierarchy objects with a VRC Avatar Descriptor (prefab assets have
        // GameObject as their type, so the Assets tab finds none).
        private const string AvatarFilter = "t:VRCAvatarDescriptor";
        // Width of Unity's picker button at the right end of an object field.
        private const float PickerButton = 19f;
        // The control ID the Assign Menu Icon picker reports to (menu callbacks run outside OnGUI, so no GetControlID).
        private const int IconPicker = 0x52694963;

        [SerializeField] private GameObject target;
        [SerializeField] private int tab;
        // The Main Menu's copy (CreateHosted): never shown on its own, and its Menu manager tab opens the Menu Tool window.
        [SerializeField] private bool hosted;
        [SerializeField] private Vector2 budgetScroll;
        [SerializeField] private Vector2 planScroll;
        [SerializeField] private Vector2 managerScroll;
        [SerializeField] private bool showShared = true;
        [SerializeField] private bool showBlocks;
        [SerializeField] private bool showNotes = true;
        [SerializeField] private bool showCompressed;
        [SerializeField] private TreeViewState budgetState = new TreeViewState();
        [SerializeField] private TreeViewState managerState = new TreeViewState();

        [NonSerialized] private MenuToolScan scan;
        // Serialized so Ctrl+Z puts a plan change back (ChangePlan records it); savedPlan is the file's content as last read
        // or written, so an undo only writes the file when it changed the plan.
        [SerializeField] private MenuToolPlan plan = new MenuToolPlan();
        [NonSerialized] private string savedPlan = "";
        [NonSerialized] private MenuToolEntry planned;
        [NonSerialized] private List<MenuToolMoveCheck> checks = new List<MenuToolMoveCheck>();
        [NonSerialized] private MenuToolBudget.Result exact;
        [NonSerialized] private bool stale = true;
        // A plan change waiting for the next Layout pass (changes mid-event would unbalance the layout).
        [NonSerialized] private bool replan;
        [NonSerialized] private readonly List<KeyValuePair<string, float>> pendingWrites = new List<KeyValuePair<string, float>>();
        // When and for what the exact comparison ran (kept across rescans, which its own test builds trigger).
        [NonSerialized] private GameObject exactFor;
        [NonSerialized] private string exactTime = "";
        [NonSerialized] private MenuToolBudgetTree budgetTree;
        // The target the budget tree was last built for: rescans of the same target keep its rows' collapsed state.
        [NonSerialized] private GameObject budgetTreeFor;
        [NonSerialized] private MenuToolManagerTree managerTree;
        [NonSerialized] private SearchField search;
        [NonSerialized] private IMenuToolRadial radial;
        [NonSerialized] private MenuToolPreviewMenus preview;
        [NonSerialized] private string shownPath = "";
        // The Menu manager's scroll view on screen, from the last Repaint: the radial is only drawn while wholly inside it.
        [NonSerialized] private Rect managerView;
        [NonSerialized] private MenuToolEntry lastClicked;
        // Every parameter write since the window opened or Reset menu state, in order.
        [NonSerialized] private readonly List<MenuToolEntryState.Write> writes = new List<MenuToolEntryState.Write>();
        [SerializeField] private bool showWrites;
        [SerializeField] private Vector2 writesScroll;
        // The window it is drawn in: the Main Menu when hosted there, else itself.
        [NonSerialized] private EditorWindow surface;
        // Where this OnGUI's (0, 0) is on screen.
        [NonSerialized] private Vector2 guiOrigin;
        // Whether the last Repaint found the radial fitting and wholly in view; it is only hidden when this turns false.
        [NonSerialized] private bool radialInView;
        // The control ID the open object picker reports to, or 0.
        [NonSerialized] private int pickerId;
        // Assign Menu Icon: the icon picker's control ID (0 when none is open) and the entry path it was opened for.
        [NonSerialized] private int iconPickerId;
        [NonSerialized] private string iconFor = "";

        public EditorWindow Surface
        {
            set
            {
                surface = value;
                if (value != null && value != this) hosted = true;
            }
        }

        private EditorWindow Shown
        {
            get { return surface != null ? surface : this; }
        }

        // The object the plan file belongs to: the avatar, or the standalone target.
        private GameObject PlanRoot
        {
            get { return scan != null && scan.Avatar != null ? scan.Avatar.gameObject : target; }
        }

        internal static void Open()
        {
            Open(null, -1);
        }

        /// <summary>The Main Menu's copy: drawn in its Menu Tool tab, never shown on its own.</summary>
        internal static EditorWindow CreateHosted()
        {
            var window = CreateInstance<MenuToolWindow>();
            window.hosted = true;
            return window;
        }

        // Its own window on target (when given) and tab (when not -1). Never the Main Menu's copy: GetWindow could find
        // that one and show it as a second window drawing the same tool.
        private static void Open(GameObject target, int tab)
        {
            MenuToolWindow window = Resources.FindObjectsOfTypeAll<MenuToolWindow>().FirstOrDefault(found => !found.hosted);
            if (window == null)
            {
                window = CreateInstance<MenuToolWindow>();
                window.titleContent = new GUIContent(Title);
            }
            window.minSize = new Vector2(MinWidth, MinHeight);
            window.wantsMouseMove = true;
            if (target != null && target != window.target)
            {
                window.target = target;
                window.MarkStale();
            }
            else if (window.target == null) window.UseSelection();
            if (tab >= 0) window.tab = tab;
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            DisposePreview();
        }

        // An undone or redone plan change (or Apply) is written back to its avatar's plan file.
        private void OnUndoRedo()
        {
            if (plan != null && !string.IsNullOrEmpty(plan.Key) && plan.Json != savedPlan)
            {
                plan.Save();
                savedPlan = plan.Json;
            }
            MarkStale();
        }

        // Hosted in the Main Menu and another tab is shown: take the radial off the window.
        public void NotDrawn()
        {
            if (radial != null) radial.Hide();
        }

        private void OnHierarchyChange() { MarkStale(); }

        private void OnProjectChange() { MarkStale(); }

        private void MarkStale()
        {
            stale = true;
            Shown.Repaint();
        }

        private void UseSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;
            target = RiryToolGUI.Set(this, "Menu Tool target", target, selected);
            MarkStale();
        }

        private void DisposePreview()
        {
            if (radial != null) radial.Dispose();
            radial = null;
            if (preview != null) preview.Dispose();
            preview = null;
        }

        // ---- Scanning and planning ---------------------------------------------------------

        private void Refresh()
        {
            stale = false;
            replan = false;
            if (exactFor != target) exact = null;
            if (budgetTree == null) budgetTree = new MenuToolBudgetTree(budgetState);
            if (managerTree == null)
            {
                managerTree = new MenuToolManagerTree(managerState) { Move = AddMove, Clicked = Clicked, ContextMove = MoveToMenu };
                search = new SearchField();
            }
            try
            {
                scan = MenuToolScanner.Scan(target);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.MenuTool, "Scanning " + target.name, exception, target);
                scan = null;
                return;
            }
            plan = MenuToolPlan.Load(PlanRoot);
            savedPlan = plan.Json;
            // Every row starts open for a new target (or a first build with nothing saved); a rescan keeps what was collapsed.
            bool fresh = target != budgetTreeFor && !(budgetTreeFor == null && budgetState.expandedIDs.Count > 0);
            budgetTree.Set(scan, fresh);
            budgetTreeFor = target;
            Replan();
        }

        private void Replan()
        {
            planned = MenuToolPlanner.Planned(scan, plan, out checks);
            managerTree.Set(planned);
            string path = shownPath;
            MenuToolPreviewMenus fresh = MenuToolPreviewMenus.Build(planned, scan);
            // A preview that shows the same is kept (with the new layout's entries), so the radial is not rebuilt.
            bool same = preview != null && preview.Adopt(fresh);
            if (same) fresh.Dispose();
            else
            {
                if (preview != null) preview.Dispose();
                preview = fresh;
            }
            // The Main Menu's copy shows only the Parameter budget, so it never makes a radial.
            bool made = false;
            if (radial == null && MenuToolPreviewMenus.CreateRadial != null && !hosted)
            {
                radial = MenuToolPreviewMenus.CreateRadial();
                radial.Written += OnWritten;
                radial.ContextClicked += OnRadialContext;
                made = true;
            }
            if (radial == null) return;
            if (same && !made)
            {
                if (lastClicked != null) lastClicked = planned.Find(lastClicked.Path);
                return;
            }
            radial.Show(preview.Root, preview.Parameters);
            // Stay in the submenu shown before, when it still exists.
            Navigate(planned.Find(path) ?? planned);
            if (lastClicked != null) lastClicked = planned.Find(lastClicked.Path);
        }

        private void AddMove(string from, string to, int index)
        {
            ChangePlan("Plan a move", moves => moves.Add(new MenuToolPlannedMove { From = from, To = to ?? "", Index = index }));
        }

        /// <summary>
        /// Every plan change: the plan file is read again first (another Menu Tool view may have changed it), the window is
        /// recorded for Undo, the change made and the file written; the layout is rebuilt on the next Layout pass.
        /// </summary>
        private void ChangePlan(string undoName, Action<List<MenuToolPlannedMove>> change)
        {
            plan = MenuToolPlan.Load(PlanRoot);
            Undo.RecordObject(this, "Menu Tool: " + undoName);
            change(plan.Moves);
            plan.Save();
            savedPlan = plan.Json;
            replan = true;
            Shown.Repaint();
        }

        private static bool Same(MenuToolPlannedMove a, MenuToolPlannedMove b)
        {
            return a.From == b.From && a.To == b.To && a.Index == b.Index && a.NewMenu == b.NewMenu && a.NewControl == b.NewControl &&
                a.AssignIcon == b.AssignIcon && a.Icon == b.Icon;
        }

        // Every submenu of the planned layout, the root first: its path and how menus show it ("Root › A › B").
        private IEnumerable<KeyValuePair<string, string>> Submenus()
        {
            foreach (MenuToolEntry menu in new[] { planned }.Concat(planned.Descendants()).Where(e => e.IsSubMenu))
                yield return new KeyValuePair<string, string>(menu.Path, menu.Parent == null ? "Root" : "Root › " + menu.Path.Replace("/", " › "));
        }

        private void MoveToMenu(MenuToolEntry entry)
        {
            var menu = new GenericMenu();
            AddMoveItems(menu, entry);
            menu.ShowAsContext();
        }

        private void AddMoveItems(GenericMenu menu, MenuToolEntry entry)
        {
            int before = menu.GetItemCount();
            if (planned != entry.Parent)
            {
                menu.AddItem(new GUIContent("Move to…/Top level"), false, () => AddMove(entry.Path, "", -1));
                if (MoveDestinations(planned, entry).Any()) menu.AddSeparator("Move to…/");
            }
            AddMoveSegment(menu, entry, planned, "Move to…/");
            if (menu.GetItemCount() == before) menu.AddDisabledItem(new GUIContent("Move to…/No other submenu"));
        }

        // The submenus under a parent that the entry can move into: not itself or anything under it.
        private static IEnumerable<MenuToolEntry> MoveDestinations(MenuToolEntry parent, MenuToolEntry entry)
        {
            return parent.Children.Where(e => e.IsSubMenu && e != entry && !MenuToolScanner.IsUnder(e, entry));
        }

        // One level of Move to…: a submenu with submenus of its own expands, its own name first, then a separator and its
        // submenus; one without is a single item.
        private void AddMoveSegment(GenericMenu menu, MenuToolEntry entry, MenuToolEntry parent, string prefix)
        {
            foreach (MenuToolEntry destination in MoveDestinations(parent, entry))
            {
                string name = destination.Name.Replace("/", "∕");
                string to = destination.Path;
                bool target = destination != entry.Parent;
                if (!MoveDestinations(destination, entry).Any())
                {
                    if (target) menu.AddItem(new GUIContent(prefix + name), false, () => AddMove(entry.Path, to, -1));
                    continue;
                }
                string segment = prefix + name + "/";
                if (target)
                {
                    menu.AddItem(new GUIContent(segment + name), false, () => AddMove(entry.Path, to, -1));
                    menu.AddSeparator(segment);
                }
                AddMoveSegment(menu, entry, destination, segment);
            }
        }

        // A right-click on the radial: the list's context menu for the control that slice shows; a greyed entry for a slice
        // that shows none (Back, Quick Actions, a "Next" page).
        private void OnRadialContext(VRCExpressionsMenu.Control control)
        {
            MenuToolEntry entry = null;
            if (control != null && preview != null) preview.EntryOfControl.TryGetValue(control, out entry);
            var menu = new GenericMenu();
            if (entry != null) AddMoveItems(menu, entry);
            else menu.AddDisabledItem(new GUIContent("Move to… (not a menu control)"));
            // New items go in the right-clicked submenu, or the page it is on.
            string into = entry != null && entry.IsSubMenu ? entry.Path : shownPath;
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Add New Control…"), false, () => AddControl(into));
            menu.AddItem(new GUIContent("Add Submenu…"), false, () => AddMenu(into));
            if (entry != null && !entry.Generated)
            {
                string path = entry.Path;
                menu.AddItem(new GUIContent("Assign Menu Icon"), false, () =>
                {
                    iconFor = path;
                    iconPickerId = IconPicker;
                    EditorGUIUtility.ShowObjectPicker<Texture2D>(entry.Icon, false, "", iconPickerId);
                });
            }
            else menu.AddDisabledItem(new GUIContent("Assign Menu Icon"));
            menu.ShowAsContext();
        }

        // Assign Menu Icon: the object picker's choice, when it closes, is planned for the entry it was opened on.
        private void PickedIcon()
        {
            Event current = Event.current;
            if (iconPickerId == 0 || current.type != EventType.ExecuteCommand || current.commandName != "ObjectSelectorClosed" ||
                EditorGUIUtility.GetObjectPickerControlID() != iconPickerId) return;
            iconPickerId = 0;
            var texture = EditorGUIUtility.GetObjectPickerObject() as Texture2D;
            MenuToolEntry entry = planned != null ? planned.Find(iconFor) : null;
            if (texture == null || entry == null || entry.Icon == texture) return;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
            string path = iconFor;
            ChangePlan("Plan a menu icon", moves => moves.Add(new MenuToolPlannedMove { From = path, AssignIcon = true, Icon = guid }));
        }

        // Add New Control: the dialog asks for the control's fields and its parent (into chosen first).
        private void AddControl(string into)
        {
            string[] names = scan != null ? scan.Params.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray() : new string[0];
            MenuToolAddMenuDialog.OpenControl(Submenus().ToList(), into, names, made =>
                ChangePlan("Plan a new control", moves => moves.Add(new MenuToolPlannedMove
                {
                    To = made.Parent, NewControl = made.Name, ControlType = (int)made.Type, Parameter = made.Parameter, Value = made.Value,
                    Icon = made.Icon != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(made.Icon)) : ""
                })));
        }

        // Add menu: the dialog asks for the name and the parent submenu (the one shown in the radial at first).
        private void AddMenu(string into = null)
        {
            MenuToolAddMenuDialog.Open(Submenus().ToList(), into ?? shownPath, (name, parent) =>
                ChangePlan("Plan a new menu", moves => moves.Add(new MenuToolPlannedMove { To = parent, NewMenu = name })));
            RiryToolGUI.ExitAfterModal();
        }

        // ---- GUI ---------------------------------------------------------------------------------

        private void OnGUI()
        {
            guiOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
            PickedIcon();
            DrawWindow();
        }

        private void DrawWindow()
        {
            if (Shown == this)
            {
                wantsMouseMove = true;
                GUILayout.Space(RiryToolsMainMenu.TopMargin);
            }
            DrawHeader();
            if (target == null)
            {
                if (radial != null) radial.Hide();
                EditorGUILayout.HelpBox("Choose an avatar, a prefab, or an object inside an avatar (Use selection takes the selected one).", MessageType.Info);
                return;
            }
            if (Event.current.type == EventType.Layout)
            {
                if (stale) Refresh();
                else if (replan && scan != null)
                {
                    replan = false;
                    Replan();
                }
                ApplyWrites();
            }
            if (scan == null)
            {
                if (radial != null) radial.Hide();
                EditorGUILayout.HelpBox("The scan failed; see the Console.", MessageType.Error);
                return;
            }
            EditorGUILayout.Space(4f);
            int chosen = RiryToolsMainMenu.TabStrip(Shown, hosted ? 0 : tab, Tabs);
            if (hosted)
            {
                // In the Main Menu, the Menu manager opens in the Menu Tool window, on the same target.
                tab = 0;
                if (chosen == 1)
                {
                    GameObject on = target;
                    EditorApplication.delayCall += () => Open(on, 1);
                }
            }
            else if (chosen != tab) tab = RiryToolGUI.Set(this, "Menu Tool tab", tab, chosen);
            EditorGUILayout.Space(4f);
            if (tab == 0)
            {
                if (radial != null) radial.Hide();
                DrawBudget();
            }
            else DrawManager();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect field = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                int id = GUIUtility.GetControlID(FocusType.Passive);
                // The picker button opens a picker of Hierarchy objects with a VRC Avatar Descriptor instead of every GameObject.
                var button = new Rect(field.xMax - PickerButton, field.y, PickerButton, field.height);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && button.Contains(Event.current.mousePosition))
                {
                    pickerId = id;
                    EditorGUIUtility.ShowObjectPicker<GameObject>(target, true, AvatarFilter, id);
                    Event.current.Use();
                }
                if (Event.current.type == EventType.ExecuteCommand && Event.current.commandName == "ObjectSelectorClosed" && pickerId != 0 &&
                    EditorGUIUtility.GetObjectPickerControlID() == pickerId)
                {
                    pickerId = 0;
                    var chosen = EditorGUIUtility.GetObjectPickerObject() as GameObject;
                    if (chosen != null && chosen != target)
                    {
                        target = RiryToolGUI.Set(this, "Menu Tool target", target, chosen);
                        MarkStale();
                    }
                }
                var picked = (GameObject)EditorGUI.ObjectField(field, new GUIContent("Avatar or prefab",
                    "A prefab asset, an object in Prefab Mode or in the Hierarchy. Inside an avatar, the avatar is assembled and the object filters the budget. " +
                    "The picker lists Hierarchy objects with a VRC Avatar Descriptor."),
                    target, typeof(GameObject), true);
                if (picked != target)
                {
                    target = RiryToolGUI.Set(this, "Menu Tool target", target, picked);
                    MarkStale();
                }
                if (GUILayout.Button("Use selection", GUILayout.Width(100f))) UseSelection();
                if (GUILayout.Button(new GUIContent("Rescan", "Read the avatar again."), GUILayout.Width(70f))) MarkStale();
            }
        }

        // ---- Parameter budget --------------------------------------------------------------------

        private void DrawBudget()
        {
            budgetScroll = EditorGUILayout.BeginScrollView(budgetScroll);
            if (scan.Standalone) EditorGUILayout.HelpBox("No avatar descriptor: these are this object's own contributions. Where they appear and what they cost depend on the avatar it is added to.", MessageType.Info);
            DrawSummary();
            EditorGUILayout.Space(6f);
            Rect rect = GUILayoutUtility.GetRect(0f, 100000f, budgetTree.totalHeight, budgetTree.totalHeight);
            budgetTree.OnGUI(rect);
            EditorGUILayout.LabelField("Double-click a row to select its source.", EditorStyles.centeredGreyMiniLabel);
            DrawShared();
            DrawBlocks();
            DrawNotes();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            MenuToolBudget.Result estimate = MenuToolBudget.Estimate(scan);
            MenuToolBudget.Result shown = exact != null && exact.Exact ? exact : estimate;
            string label = exact == null ? "Estimated" : exact.Exact ? "Exact (two isolated test builds, " + exactTime + "; Compare exactly again after changes)"
                : "Unavailable: " + exact.Unavailable + " Showing the estimate.";
            EditorGUILayout.LabelField("Compression comparison: " + label, EditorStyles.boldLabel);
            if (!scan.VrcfuryInstalled) EditorGUILayout.HelpBox("VRCFury is not installed, so the compressor column is an estimate of what VRCFury's Parameter Compressor would do.", MessageType.None);
            Row("", "Compression off", "Compression on", "Change", true);
            Row("Custom parameters", shown.Off.Total, shown.On.Total, "of " + MenuToolBudgetCount.MaxParameters);
            Row("Synced", shown.Off.Synced, shown.On.Synced, "");
            Row("Local", shown.Off.Local, shown.On.Local, "");
            Row("Synced bits", shown.Off.Bits, shown.On.Bits, "of " + MenuToolBudgetCount.MaxBits);
            Row("Bits left", shown.Off.BitsLeft, shown.On.BitsLeft, "");
            Row("Parameters left", shown.Off.ParametersLeft, shown.On.ParametersLeft, "");
            if (estimate.Note.Length > 0) EditorGUILayout.HelpBox(estimate.Note, MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(scan.Avatar == null || !scan.VrcfuryInstalled || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(new GUIContent("Compare exactly", "Builds two isolated copies of the avatar through the VRChat SDK's build callbacks, " +
                        "with VRCFury's Parameter Compressor setting at Fail and then at Compress (restored afterwards), compares their parameters, then " +
                        "deletes both. The avatar is not changed. Over the limit, VRCFury shows its own \"would have failed to upload\" message for the first build."), GUILayout.Width(130f)))
                    {
                        exact = MenuToolBudget.Exact(scan.Avatar);
                        exactFor = target;
                        exactTime = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
                        RiryToolGUI.ExitAfterModal();
                    }
                }
            }
            if (shown.Compressed.Count > 0)
            {
                showCompressed = EditorGUILayout.Foldout(showCompressed, (shown.Exact ? "Compressed" : "Would be compressed") + " (" + shown.Compressed.Count + ")", true);
                if (showCompressed) EditorGUILayout.LabelField(string.Join(", ", shown.Compressed), EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void Row(string name, int off, int on, string note)
        {
            int change = on - off;
            Row(name, off.ToString(CultureInfo.InvariantCulture), on.ToString(CultureInfo.InvariantCulture),
                (change > 0 ? "+" : "") + change + (note.Length > 0 ? "  (" + note + ")" : ""), false);
        }

        private static void Row(string name, string off, string on, string change, bool header)
        {
            GUIStyle style = header ? EditorStyles.miniBoldLabel : EditorStyles.label;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(name, style, GUILayout.Width(150f));
                EditorGUILayout.LabelField(off, style, GUILayout.Width(110f));
                EditorGUILayout.LabelField(on, style, GUILayout.Width(110f));
                EditorGUILayout.LabelField(change, style, GUILayout.MinWidth(0f));
            }
        }

        private void DrawShared()
        {
            List<MenuToolParamEntry> shared = scan.Params.Values.Where(p => p.Blocks.Count > 1 || p.TypeConflict).OrderBy(p => p.Name).ToList();
            showShared = EditorGUILayout.Foldout(showShared, "Shared parameters (" + shared.Count + ", each counted once)", true);
            if (!showShared) return;
            foreach (MenuToolParamEntry param in shared)
            {
                string types = param.TypeConflict ? string.Join(" / ", param.DeclaredTypes) + " (types differ)" : param.Type.ToString();
                var content = new GUIContent(param.Name + ": " + types + (param.Synced ? ", " + param.Bits + " bits" : ", local") + "  —  " + string.Join(", ", param.Blocks));
                using (new EditorGUILayout.HorizontalScope())
                {
                    Color before = GUI.color;
                    if (param.TypeConflict) GUI.color = new Color(1f, 0.55f, 0.55f);
                    EditorGUILayout.LabelField(content, EditorStyles.wordWrappedLabel, GUILayout.MinWidth(0f));
                    GUI.color = before;
                    if (param.Sources.Count > 0 && GUILayout.Button("Select", GUILayout.Width(60f))) Select(param.Sources[0]);
                }
            }
        }

        private void DrawBlocks()
        {
            showBlocks = EditorGUILayout.Foldout(showBlocks, "Content blocks (" + scan.Blocks.Count + ")", true);
            if (!showBlocks) return;
            List<MenuToolEntry> all = scan.Root.Descendants().ToList();
            foreach (string block in scan.Blocks)
            {
                EditorGUILayout.LabelField(block, EditorStyles.boldLabel);
                foreach (MenuToolEntry entry in all.Where(e => e.Block == block && !e.IsSubMenu && scan.InFilter(e.Source)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string bindings = entry.Parameters().Any() ? string.Join(", ", entry.Parameters()) : "no parameter";
                        EditorGUILayout.LabelField(new GUIContent(entry.Path + "  —  " + bindings + (entry.Estimated ? " (estimated)" : ""),
                            entry.Source != null ? entry.Source.name : ""), GUILayout.MinWidth(0f));
                        using (new EditorGUI.DisabledScope(entry.Source == null))
                        {
                            if (GUILayout.Button("Select", GUILayout.Width(60f))) Select(entry.Source);
                        }
                    }
                }
            }
        }

        private void DrawNotes()
        {
            showNotes = EditorGUILayout.Foldout(showNotes, "Notes (" + scan.Notes.Count + ")", true);
            if (!showNotes) return;
            foreach (string note in scan.Notes) EditorGUILayout.HelpBox(note, MessageType.None);
        }

        private static void Select(UnityEngine.Object source)
        {
            if (source == null) return;
            Selection.activeObject = source;
            EditorGUIUtility.PingObject(source);
        }

        // ---- Menu manager -----------------------------------------------------------------------

        private void DrawManager()
        {
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag) Shown.Repaint();
            if (Shown.position.width < MinWidth)
                EditorGUILayout.HelpBox("The Menu manager needs a window at least " + MinWidth + " px wide.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                managerTree.searchString = search.OnGUI(GUILayoutUtility.GetRect(100f, 10000f, 18f, 18f), managerTree.searchString);
                using (new EditorGUI.DisabledScope(plan.Moves.Count == 0))
                {
                    if (GUILayout.Button("Discard plan", GUILayout.Width(90f))) Discard();
                }
                if (GUILayout.Button(new GUIContent("Add menu", "Plans a new, empty submenu: its name and the submenu it goes in."), GUILayout.Width(80f))) AddMenu();
                using (new EditorGUI.DisabledScope(plan.Moves.Count == 0 || checks.Any(c => !c.Ok)))
                {
                    if (GUILayout.Button(new GUIContent("Apply", "Makes the planned moves in their sources, as one undo step."), GUILayout.Width(70f))) ApplyPlan();
                }
            }
            float treeHeight = Mathf.Clamp(Shown.position.height * 0.28f, 140f, 420f);
            managerTree.OnGUI(GUILayoutUtility.GetRect(0f, 100000f, treeHeight, treeHeight));
            EditorGUILayout.LabelField("Drag a row by its ≡ handle, or right-click it for Move to….", EditorStyles.centeredGreyMiniLabel);
            // The rest scrolls, so a short window (the Main Menu) still reaches the radial and the last clicked control.
            managerScroll = EditorGUILayout.BeginScrollView(managerScroll);
            DrawPlan();
            DrawBreadcrumbs();
            DrawRadial();
            DrawEntryState();
            DrawBits();
            EditorGUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) managerView = GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect());
        }

        private void DrawPlan()
        {
            if (plan.Moves.Count == 0) return;
            EditorGUILayout.LabelField("Planned moves (" + plan.Moves.Count + ")", EditorStyles.boldLabel);
            float height = Mathf.Min(160f, 44f * checks.Count);
            planScroll = EditorGUILayout.BeginScrollView(planScroll, GUILayout.Height(height));
            MenuToolPlannedMove remove = null;
            for (int i = 0; i < checks.Count; i++)
            {
                MenuToolMoveCheck check = checks[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    string status = check.Ok ? "✓" : "✕";
                    // A new menu reads "New menu  →  its path".
                    EditorGUILayout.LabelField(new GUIContent(status + " " + (check.Before.Length == 0 ? "?" : check.Before) + "  →  " + (check.After.Length == 0 ? "?" : check.After),
                        string.Join("\n", check.Affected.Where(a => a != null).Select(a => a.name))), GUILayout.MinWidth(0f));
                    if (check.Candidates.Count > 1)
                    {
                        // Unchosen (ambiguous): a "Choose a source" entry first, so a choice is always explicit.
                        bool unchosen = check.Edit == MenuToolMoveEdit.Ambiguous;
                        List<string> options = check.Candidates.Select(MenuToolPlanner.Describe).ToList();
                        if (unchosen) options.Insert(0, MenuToolPlanner.Describe(MenuToolMoveEdit.Ambiguous));
                        int current = unchosen ? 0 : Mathf.Max(0, check.Candidates.IndexOf(check.Edit));
                        int picked = EditorGUILayout.Popup(current, options.ToArray(), GUILayout.Width(200f));
                        if (picked != current && !(unchosen && picked == 0))
                        {
                            string choice = check.Candidates[unchosen ? picked - 1 : picked].ToString();
                            MenuToolPlannedMove chosen = check.Move;
                            ChangePlan("Choose a source", moves =>
                            {
                                MenuToolPlannedMove move = moves.FirstOrDefault(m => Same(m, chosen));
                                if (move != null) move.Choice = choice;
                            });
                        }
                    }
                    else EditorGUILayout.LabelField(MenuToolPlanner.Describe(check.Edit), EditorStyles.miniLabel, GUILayout.Width(200f));
                    if (GUILayout.Button(new GUIContent("✕", "Remove this move from the plan."), GUILayout.Width(24f))) remove = check.Move;
                }
                foreach (string error in check.Errors) EditorGUILayout.LabelField("    " + error, ErrorStyle);
                foreach (string warning in check.Warnings) EditorGUILayout.LabelField("    " + warning, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndScrollView();
            if (remove != null)
            {
                MenuToolPlannedMove removed = remove;
                ChangePlan("Remove a planned move", moves =>
                {
                    int at = moves.FindIndex(m => Same(m, removed));
                    if (at >= 0) moves.RemoveAt(at);
                });
            }
        }

        private static GUIStyle errorStyle;

        private static GUIStyle ErrorStyle
        {
            get
            {
                if (errorStyle == null)
                {
                    errorStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
                    errorStyle.normal.textColor = new Color(0.95f, 0.35f, 0.35f);
                }
                return errorStyle;
            }
        }

        private void DrawBreadcrumbs()
        {
            MenuToolEntry at = planned.Find(shownPath) ?? planned;
            var chain = new List<MenuToolEntry>();
            for (MenuToolEntry entry = at; entry != null; entry = entry.Parent) chain.Insert(0, entry);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < chain.Count; i++)
                {
                    if (i > 0) GUILayout.Label("›", GUILayout.Width(10f));
                    string name = chain[i].Parent == null ? "Root" : chain[i].Name;
                    if (GUILayout.Button(name, EditorStyles.miniButton, GUILayout.MinWidth(0f)))
                    {
                        Navigate(chain[i]);
                        managerTree.Select(chain[i]);
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRadial()
        {
            Rect area = GUILayoutUtility.GetRect(RadialSize, RadialSize, GUILayout.ExpandWidth(true));
            // An event the list used up (dragging a row) gets placeholder rects from GetRect: the radial is not placed then.
            if (Event.current.type == EventType.Used) return;
            var square = new Rect(area.center.x - RadialSize / 2f, area.y, RadialSize, RadialSize);
            if (MenuToolPreviewMenus.CreateRadial == null)
            {
                EditorGUI.HelpBox(new Rect(area.x, area.y, area.width, 56f),
                    new GestureManagerNotFoundException("The radial menu preview").Message + " The list above still plans moves.", MessageType.Info);
                return;
            }
            if (radial == null) return;
            // The radial floats over the window and cannot be clipped: shown only when it fits and is wholly in view. Decided
            // on Repaint, where the layout is final, and hidden only when that turns false, so it does not blink off and on.
            bool fits = area.width >= RadialSize;
            if (Event.current.type == EventType.Repaint)
            {
                Rect onScreen = GUIUtility.GUIToScreenRect(square);
                bool inView = managerView.width > 0f && onScreen.xMin >= managerView.xMin - 0.5f && onScreen.xMax <= managerView.xMax + 0.5f &&
                    onScreen.yMin >= managerView.yMin - 0.5f && onScreen.yMax <= managerView.yMax + 0.5f;
                bool inViewNow = fits && inView;
                if (radialInView && !inViewNow) radial.Hide();
                radialInView = inViewNow;
            }
            if (Event.current.type != EventType.Layout && !radialInView)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(area, new Color(0f, 0f, 0f, 0.12f));
                    GUI.Label(area, fits ? "Scroll to show the whole radial menu." : "Widen the window to show the radial menu.", EditorStyles.centeredGreyMiniLabel);
                }
                return;
            }
            // Placed and fed mouse events in every event but Layout, whose rects are placeholders.
            if (Event.current.type != EventType.Layout)
            {
                // Placed in this window's root element at the spot this OnGUI draws at.
                var local = new Rect(GUIUtility.GUIToScreenPoint(square.position) - guiOrigin, square.size);
                radial.Draw(square, local, rootVisualElement);
            }
            // Follow the radial: a submenu opened there is selected in the list and the breadcrumbs.
            List<VRCExpressionsMenu> path = radial.Path;
            MenuToolEntry entry;
            string now = path.Count > 0 && preview != null && preview.EntryOf.TryGetValue(path[path.Count - 1], out entry) ? entry.Path : "";
            if (now != shownPath && Event.current.type == EventType.Repaint && path.Count > 0)
            {
                shownPath = now;
                MenuToolEntry opened = planned.Find(now);
                if (opened != null && opened.Parent != null) managerTree.Select(opened);
                Shown.Repaint();
            }
        }

        private void DrawBits()
        {
            MenuToolEntry entry = lastClicked;
            EditorGUILayout.LabelField("Last clicked: " + (entry == null ? "nothing yet" : entry.Path + " (" + MenuToolIcons.TypeName(entry.Type) + ")"), EditorStyles.boldLabel);
            if (entry != null)
            {
                foreach (string name in entry.Parameters())
                {
                    MenuToolParamEntry param = scan.Param(name);
                    string kind = param == null ? "built-in" : param.Type + (param.Synced ? ", synced, " + param.Bits + (param.Bits == 1 ? " bit" : " bits") : ", local, 0 bits") +
                        (param.Estimated ? ", estimated name" : "");
                    float value = radial != null ? radial.Value(name) : 0f;
                    EditorGUILayout.LabelField(name + "  =  " + value.ToString("0.###", CultureInfo.InvariantCulture) + "    (" + kind + ")");
                }
                if (!entry.Parameters().Any()) EditorGUILayout.LabelField("No parameter.", EditorStyles.miniLabel);
            }
            showWrites = EditorGUILayout.Foldout(showWrites, "Writes (" + writes.Count + ")", true);
            if (!showWrites) return;
            writesScroll = EditorGUILayout.BeginScrollView(writesScroll, EditorStyles.helpBox, GUILayout.Height(120f));
            foreach (MenuToolEntryState.Write write in writes)
                EditorGUILayout.LabelField(write.Name + " = " + write.Value.ToString("0.###", CultureInfo.InvariantCulture), EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        // Set as entry state, Reset entry state, Reset menu state: under the radial.
        private void DrawEntryState()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(scan.Avatar == null || radial == null))
                {
                    if (GUILayout.Button(new GUIContent("Set as entry state",
                        "In Play Mode this session, once Gesture Manager has set up this avatar, makes these writes in order and opens this submenu.")))
                        SetEntryState();
                }
                using (new EditorGUI.DisabledScope(!MenuToolEntryState.Saved))
                {
                    if (GUILayout.Button("Reset entry state")) MenuToolEntryState.Clear();
                }
                using (new EditorGUI.DisabledScope(radial == null))
                {
                    if (GUILayout.Button(new GUIContent("Reset menu state",
                        "Sets the preview's parameters back to their defaults and clears the writes. The plan is kept."))) ResetMenuState();
                }
            }
        }

        private void SetEntryState()
        {
            Transform root = scan.Avatar.transform;
            var state = new MenuToolEntryState
            {
                Scene = root.gameObject.scene.name, Avatar = HierarchyPath.Of(root), Menu = shownPath ?? "",
                Writes = new List<MenuToolEntryState.Write>(writes)
            };
            state.Save();
        }

        private void ResetMenuState()
        {
            radial.ResetValues();
            pendingWrites.Clear();
            writes.Clear();
            lastClicked = null;
        }

        // Writes arrive while the radial handles an event; they are shown from the next Layout pass.
        private void OnWritten(string name, float value)
        {
            pendingWrites.Add(new KeyValuePair<string, float>(name, value));
            Shown.Repaint();
        }

        private void ApplyWrites()
        {
            foreach (KeyValuePair<string, float> write in pendingWrites)
            {
                writes.Add(new MenuToolEntryState.Write { Name = write.Key, Value = write.Value });
                // The control that wrote it: in the submenu shown first, else anywhere.
                MenuToolEntry at = planned != null ? planned.Find(shownPath) ?? planned : null;
                if (at == null) continue;
                lastClicked = at.Children.FirstOrDefault(child => child.Parameters().Contains(write.Key))
                    ?? planned.Descendants().FirstOrDefault(child => !child.IsSubMenu && child.Parameters().Contains(write.Key)) ?? lastClicked;
            }
            pendingWrites.Clear();
        }

        private void Clicked(MenuToolEntry entry)
        {
            if (entry == null) return;
            lastClicked = entry;
            Navigate(entry.IsSubMenu ? entry : entry.Parent);
        }

        // Opens a submenu of the planned layout in the radial (the root and each submenu down to it).
        private void Navigate(MenuToolEntry submenu)
        {
            if (submenu == null) return;
            shownPath = submenu.Path;
            if (radial == null || preview == null) return;
            var chain = new List<VRCExpressionsMenu>();
            for (MenuToolEntry at = submenu; at != null; at = at.Parent)
            {
                VRCExpressionsMenu menu;
                if (preview.MenuOf.TryGetValue(at, out menu)) chain.Insert(0, menu);
            }
            radial.Navigate(chain);
        }

        private void Discard()
        {
            if (!EditorUtility.DisplayDialog(Title, "Discard the " + plan.Moves.Count + " planned moves? Nothing has been applied yet; Ctrl+Z brings them back.",
                "Discard", "Cancel"))
            {
                RiryToolGUI.ExitAfterModal();
                return;
            }
            ChangePlan("Discard plan", moves => moves.Clear());
            RiryToolGUI.ExitAfterModal();
        }

        private void ApplyPlan()
        {
            var sources = checks.SelectMany(c => c.Affected).Where(a => a != null).Distinct().Select(a => a.name).ToList();
            string message = "Apply " + plan.Moves.Count + " planned moves to " + PlanRoot.name + "?\n\nSources changed: " + string.Join(", ", sources) +
                "\n\nMenu assets other places also use are copied to Assets/RiryTools/MenuTool first. One Ctrl+Z undoes everything this does (copied files stay).";
            if (!EditorUtility.DisplayDialog(Title, message, "Apply", "Cancel"))
            {
                RiryToolGUI.ExitAfterModal();
                return;
            }
            // The plan is cleared inside Apply's undo step, so undoing Apply brings the plan back too.
            MenuToolApply.Report report = MenuToolApply.Apply(target, plan, () =>
            {
                Undo.RecordObject(this, "Apply menu layout");
                plan.Moves.Clear();
            });
            plan.Save();
            savedPlan = plan.Json;
            MarkStale();
            if (report.Problems.Count > 0)
                EditorUtility.DisplayDialog(Title, "Applied " + report.Applied + " moves. Check these:\n\n" + string.Join("\n", report.Problems.Take(12)), "OK");
            RiryToolGUI.ExitAfterModal();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool window
// The menu entry, Tools > RiryTools > Menu Tool, is in Editor/VRChat/UI/RiryToolsVRChatMenus.cs; the Main Menu hosts it as a tab.
// ============================================================================
//
// MenuToolWindow (this file)
//   Open() / Open(target, tab)  Its own window (minSize MinWidth 640 x MinHeight 760), on the selection when it has no target;
//                            found among existing windows that are not hosted (never GetWindow, which could show the Main
//                            Menu's copy as a second window).
//   CreateHosted / hosted    The Main Menu's copy ([SerializeField] hosted): it shows the Parameter budget; its Menu manager tab
//                            opens Open(target, 1) after the GUI pass instead.
//   Surface / Shown          IRiryHostedTool: the Main Menu sets itself as the surface when hosting, so tabs size to it; on
//                            its own, the window itself.
//   OnGUI / DrawWindow       guiOrigin (this OnGUI's (0, 0) on screen). The radial is hidden where a view stops drawing it
//                            (no target, failed scan, Parameter budget tab, NotDrawn) and by DrawRadial when it leaves view.
//   target                   Avatar, prefab asset, Prefab Mode or Hierarchy object ([SerializeField]; Undo via RiryToolGUI.Set).
//                            Its picker button (PickerButton wide) opens ShowObjectPicker with AvatarFilter
//                            ("t:VRCAvatarDescriptor": Hierarchy objects with a VRC Avatar Descriptor), applied when the
//                            picker closes (pickerId); dragging in and Use selection still take any object.
//   Refresh / MarkStale      Rescans (MenuToolScanner) on the next Layout after a change (hierarchy, project, Undo, Rescan),
//                            loads the plan (MenuToolPlan), rebuilds the trees and the preview (Replan). The budget tree opens
//                            every row only for a new target (budgetTreeFor); a rescan keeps collapsed rows collapsed.
//   Replan                   Planned layout (MenuToolPlanner), the list, the preview menus and the radial (never for the
//                            hosted copy), staying in the submenu shown before. Preview menus that show the same (Adopt) are
//                            kept and the radial is left as it is.
//   Parameter budget tab     DrawSummary: compression off / on / change (custom parameters, synced, local, synced bits, bits
//                            and parameters left), Estimated / Exact / Unavailable; the estimate's Note (VRCFury cannot fit
//                            the avatar, or is set to Fail); Compare exactly (MenuToolBudget.Exact). MenuToolBudgetTree; Shared
//                            parameters (types differ in red); Content blocks (each control's path, bindings, Select);
//                            Notes.
//   Menu manager tab         Search, Discard plan, Add menu, Apply (disabled until every move checks out); MenuToolManagerTree; then, in
//                            a scroll view (managerScroll; managerView its screen rect), DrawPlan
//                            (before -> after, edit, choice popup when several sources fit, errors, warnings, remove);
//                            DrawBreadcrumbs; DrawRadial (Gesture Manager, or how to get it; only when it fits and is wholly in
//                            view, decided on Repaint (radialInView) and hidden only when that turns false, else a grey box
//                            saying to scroll or widen; placed in rootVisualElement at its GUI spot less guiOrigin); DrawBits
//                            (last clicked control's parameters: type, sync, bits, live value; the last WriteLog writes).
//   plan / savedPlan         The plan ([SerializeField], so Ctrl+Z restores it) and the file's content as last read or written;
//                            OnUndoRedo writes the plan back to its avatar's file when an undo or redo changed it.
//   ChangePlan(name, change) Every plan change (plan a move, choose a source, remove one, Discard): reloads the file (another
//                            view may have changed it), records the window for Undo, changes, saves.
//   NotDrawn()               IRiryHostedTool: the Main Menu shows another tab; the radial is taken off.
//   AddMove / MoveToMenu     A planned move from drag and drop or the Move to... context menu (ChangePlan); Submenus lists every
//                            submenu (path, "Root › A › B").
//   OnRadialContext          Right-click on the radial (IMenuToolRadial.ContextClicked): the list's Move to… items (AddMoveItems)
//                            for the slice's control's entry (preview.EntryOfControl), else one greyed "Move to… (not a menu
//                            control)" entry; then Add New Control… (AddControl), Add Submenu… (AddMenu) into the right-clicked
//                            submenu or the shown page, and Assign Menu Icon (greyed without an entry or on a pagination page).
//   PickedIcon               Assign Menu Icon: the Texture2D object picker (iconPickerId, iconFor) closing plans an AssignIcon move.
//   AddControl               MenuToolAddMenuDialog.OpenControl with the avatar's parameter names; plans a NewControl move.
//   DrawRadial (Used)        Not placed on an event the list used up (a row drag): GetRect gives placeholder rects then.
//   AddMenu                  The Add menu button (between Discard plan and Apply): MenuToolAddMenuDialog with every submenu, the
//                            radial's shown submenu chosen first; Add plans a MenuToolPlannedMove with NewMenu (ChangePlan).
//   Clicked / Navigate / OnWritten  List selection opens that submenu in the radial; the radial's submenu selects the list
//                            row; a write marks the control that made it as last clicked.
//   ApplyPlan / Discard      Confirmed. Apply runs MenuToolApply (one undo step, the plan cleared inside it, so undoing Apply
//                            restores the plan); problems are shown. Discard can be undone.
