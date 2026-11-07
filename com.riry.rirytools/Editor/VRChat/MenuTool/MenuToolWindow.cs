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
        private const int WriteLog = 8;

        [SerializeField] private GameObject target;
        [SerializeField] private int tab;
        [SerializeField] private Vector2 budgetScroll;
        [SerializeField] private Vector2 planScroll;
        [SerializeField] private bool includeBools;
        [SerializeField] private bool includePuppets;
        [SerializeField] private bool showShared = true;
        [SerializeField] private bool showBlocks;
        [SerializeField] private bool showNotes = true;
        [SerializeField] private bool showCompressed;
        [SerializeField] private TreeViewState budgetState = new TreeViewState();
        [SerializeField] private TreeViewState managerState = new TreeViewState();

        [NonSerialized] private MenuScan scan;
        [NonSerialized] private MenuPlan plan;
        [NonSerialized] private MenuEntry planned;
        [NonSerialized] private List<MoveCheck> checks = new List<MoveCheck>();
        [NonSerialized] private MenuToolBudget.Result exact;
        [NonSerialized] private bool stale = true;
        // A plan change waiting for the next Layout pass (changes mid-event would unbalance the layout).
        [NonSerialized] private bool replan;
        [NonSerialized] private readonly List<KeyValuePair<string, float>> pendingWrites = new List<KeyValuePair<string, float>>();
        // When and for what the exact comparison ran (kept across rescans, which its own test builds trigger).
        [NonSerialized] private GameObject exactFor;
        [NonSerialized] private string exactTime = "";
        [NonSerialized] private BudgetTree budgetTree;
        [NonSerialized] private ManagerTree managerTree;
        [NonSerialized] private SearchField search;
        [NonSerialized] private IMenuToolRadial radial;
        [NonSerialized] private MenuToolPreviewMenus preview;
        [NonSerialized] private string shownPath = "";
        [NonSerialized] private MenuEntry lastClicked;
        [NonSerialized] private readonly List<string> writes = new List<string>();
        // The window it is drawn in: the Main Menu when hosted there, else itself.
        [NonSerialized] private EditorWindow surface;

        public EditorWindow Surface
        {
            set { surface = value; }
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
            MenuToolWindow window = GetWindow<MenuToolWindow>(Title);
            window.minSize = new Vector2(MinWidth, MinHeight);
            window.wantsMouseMove = true;
            if (window.target == null) window.UseSelection();
            window.Show();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += MarkStale;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkStale;
            DisposePreview();
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
            target = RiryGui.Set(this, "Menu Tool target", target, selected);
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
            if (budgetTree == null) budgetTree = new BudgetTree(budgetState);
            if (managerTree == null)
            {
                managerTree = new ManagerTree(managerState) { Move = AddMove, Clicked = Clicked, ContextMove = MoveToMenu };
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
            plan = MenuPlan.Load(PlanRoot);
            budgetTree.Set(scan);
            Replan();
        }

        private void Replan()
        {
            planned = MenuToolPlanner.Planned(scan, plan, out checks);
            managerTree.Set(planned);
            string path = shownPath;
            if (preview != null) preview.Dispose();
            preview = MenuToolPreviewMenus.Build(planned, scan);
            if (radial == null && MenuToolPreviewMenus.CreateRadial != null)
            {
                radial = MenuToolPreviewMenus.CreateRadial();
                radial.Written += OnWritten;
            }
            if (radial == null) return;
            radial.Show(preview.Root, preview.Parameters);
            // Stay in the submenu shown before, when it still exists.
            Navigate(planned.Find(path) ?? planned);
            if (lastClicked != null) lastClicked = planned.Find(lastClicked.Path);
        }

        private void AddMove(string from, string to, int index)
        {
            plan.Moves.Add(new PlannedMove { From = from, To = to ?? "", Index = index });
            plan.Save(PlanRoot);
            replan = true;
            Shown.Repaint();
        }

        private void MoveToMenu(MenuEntry entry)
        {
            var menu = new GenericMenu();
            foreach (MenuEntry destination in new[] { planned }.Concat(planned.Descendants()).Where(e => e.IsSubMenu))
            {
                if (destination == entry || MenuToolScanner.IsUnder(destination, entry) || destination == entry.Parent) continue;
                string label = destination.Parent == null ? "Root" : "Root › " + destination.Path.Replace("/", " › ");
                string to = destination.Path;
                menu.AddItem(new GUIContent("Move to…/" + label.Replace("/", "∕")), false, () => AddMove(entry.Path, to, -1));
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("Move to…/No other submenu"));
            menu.ShowAsContext();
        }

        // ---- GUI ---------------------------------------------------------------------------------

        private void OnGUI()
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
                EditorGUILayout.HelpBox("The scan failed; see the Console.", MessageType.Error);
                return;
            }
            EditorGUILayout.Space(4f);
            int chosen = RiryToolsMainMenu.TabStrip(Shown, tab, Tabs);
            if (chosen != tab) tab = RiryGui.Set(this, "Menu Tool tab", tab, chosen);
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
                var picked = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Avatar or prefab",
                    "A prefab asset, an object in Prefab Mode or in the Hierarchy. Inside an avatar, the avatar is assembled and the object filters the budget."),
                    target, typeof(GameObject), true);
                if (picked != target)
                {
                    target = RiryGui.Set(this, "Menu Tool target", target, picked);
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
            bool onAvatar = scan.Compressor;
            bool bools = onAvatar ? scan.CompressorBools : includeBools;
            bool puppets = onAvatar ? scan.CompressorPuppets : includePuppets;
            MenuToolBudget.Result estimate = MenuToolBudget.Estimate(scan, bools, puppets);
            MenuToolBudget.Result shown = exact != null && exact.Exact ? exact : estimate;
            string label = exact == null ? "Estimated" : exact.Exact ? "Exact (two isolated test builds, " + exactTime + "; Compare exactly again after changes)"
                : "Unavailable: " + exact.Unavailable + " Showing the estimate.";
            EditorGUILayout.LabelField("Compression comparison: " + label, EditorStyles.boldLabel);
            if (!scan.VrcfuryInstalled) EditorGUILayout.HelpBox("VRCFury is not installed, so the compressor column is an estimate of what VRCFury's Parameter Compressor would do.", MessageType.None);
            Row("", "Compression off", "Compression on", "Change", true);
            Row("Custom parameters", shown.Off.Total, shown.On.Total, "of " + BudgetCount.MaxParameters);
            Row("Synced", shown.Off.Synced, shown.On.Synced, "");
            Row("Local", shown.Off.Local, shown.On.Local, "");
            Row("Synced bits", shown.Off.Bits, shown.On.Bits, "of " + BudgetCount.MaxBits);
            Row("Bits left", shown.Off.BitsLeft, shown.On.BitsLeft, "");
            Row("Parameters left", shown.Off.ParametersLeft, shown.On.ParametersLeft, "");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(onAvatar))
                {
                    bool newBools = EditorGUILayout.ToggleLeft(new GUIContent("Include Bools", onAvatar ? "Set by the Parameter Compressor on the avatar." : "As VRCFury's Include Bools."), bools, GUILayout.Width(110f));
                    bool newPuppets = EditorGUILayout.ToggleLeft(new GUIContent("Include Puppets", onAvatar ? "Set by the Parameter Compressor on the avatar." : "As VRCFury's Include Puppets (two- and four-axis)."), puppets, GUILayout.Width(120f));
                    if (!onAvatar)
                    {
                        includeBools = RiryGui.Set(this, "Menu Tool Include Bools", includeBools, newBools);
                        includePuppets = RiryGui.Set(this, "Menu Tool Include Puppets", includePuppets, newPuppets);
                    }
                }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(scan.Avatar == null || !scan.VrcfuryInstalled))
                {
                    if (GUILayout.Button(new GUIContent("Compare exactly", "Builds two isolated copies of the avatar, without and with the Parameter Compressor, " +
                        "through the VRChat SDK's build callbacks, compares their parameters, then deletes both. The avatar is not changed."), GUILayout.Width(130f)))
                    {
                        exact = MenuToolBudget.Exact(scan.Avatar, bools, puppets);
                        exactFor = target;
                        exactTime = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
                        RiryGui.ExitAfterModal();
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
            List<ParamEntry> shared = scan.Params.Values.Where(p => p.Blocks.Count > 1 || p.TypeConflict).OrderBy(p => p.Name).ToList();
            showShared = EditorGUILayout.Foldout(showShared, "Shared parameters (" + shared.Count + ", each counted once)", true);
            if (!showShared) return;
            foreach (ParamEntry param in shared)
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
            List<MenuEntry> all = scan.Root.Descendants().ToList();
            foreach (string block in scan.Blocks)
            {
                EditorGUILayout.LabelField(block, EditorStyles.boldLabel);
                foreach (MenuEntry entry in all.Where(e => e.Block == block && !e.IsSubMenu && scan.InFilter(e.Source)))
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
                using (new EditorGUI.DisabledScope(plan.Moves.Count == 0 || checks.Any(c => !c.Ok)))
                {
                    if (GUILayout.Button(new GUIContent("Apply", "Makes the planned moves in their sources, as one undo step."), GUILayout.Width(70f))) ApplyPlan();
                }
            }
            float treeHeight = Mathf.Clamp(Shown.position.height * 0.28f, 140f, 420f);
            managerTree.OnGUI(GUILayoutUtility.GetRect(0f, 100000f, treeHeight, treeHeight));
            EditorGUILayout.LabelField("Drag a row by its ≡ handle, or right-click it for Move to…. Changes are planned until Apply.", EditorStyles.centeredGreyMiniLabel);
            DrawPlan();
            DrawBreadcrumbs();
            DrawRadial();
            DrawBits();
        }

        private void DrawPlan()
        {
            if (plan.Moves.Count == 0) return;
            EditorGUILayout.LabelField("Planned moves (" + plan.Moves.Count + ")", EditorStyles.boldLabel);
            float height = Mathf.Min(160f, 44f * checks.Count);
            planScroll = EditorGUILayout.BeginScrollView(planScroll, GUILayout.Height(height));
            int remove = -1;
            for (int i = 0; i < checks.Count; i++)
            {
                MoveCheck check = checks[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    string status = check.Ok ? "✓" : "✕";
                    EditorGUILayout.LabelField(new GUIContent(status + " " + (check.Before.Length == 0 ? "?" : check.Before) + "  →  " + (check.After.Length == 0 ? "?" : check.After),
                        string.Join("\n", check.Affected.Where(a => a != null).Select(a => a.name))), GUILayout.MinWidth(0f));
                    if (check.Candidates.Count > 1)
                    {
                        // Unchosen (ambiguous): a "Choose a source" entry first, so a choice is always explicit.
                        bool unchosen = check.Edit == MoveEdit.Ambiguous;
                        List<string> options = check.Candidates.Select(MenuToolPlanner.Describe).ToList();
                        if (unchosen) options.Insert(0, MenuToolPlanner.Describe(MoveEdit.Ambiguous));
                        int current = unchosen ? 0 : Mathf.Max(0, check.Candidates.IndexOf(check.Edit));
                        int picked = EditorGUILayout.Popup(current, options.ToArray(), GUILayout.Width(200f));
                        if (picked != current && !(unchosen && picked == 0))
                        {
                            check.Move.Choice = check.Candidates[unchosen ? picked - 1 : picked].ToString();
                            plan.Save(PlanRoot);
                            replan = true;
                        }
                    }
                    else EditorGUILayout.LabelField(MenuToolPlanner.Describe(check.Edit), EditorStyles.miniLabel, GUILayout.Width(200f));
                    if (GUILayout.Button(new GUIContent("✕", "Remove this move from the plan."), GUILayout.Width(24f))) remove = i;
                }
                foreach (string error in check.Errors) EditorGUILayout.LabelField("    " + error, ErrorStyle);
                foreach (string warning in check.Warnings) EditorGUILayout.LabelField("    " + warning, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndScrollView();
            if (remove >= 0)
            {
                plan.Moves.RemoveAt(remove);
                plan.Save(PlanRoot);
                replan = true;
                Shown.Repaint();
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
            MenuEntry at = planned.Find(shownPath) ?? planned;
            var chain = new List<MenuEntry>();
            for (MenuEntry entry = at; entry != null; entry = entry.Parent) chain.Insert(0, entry);
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
            var square = new Rect(area.center.x - RadialSize / 2f, area.y, RadialSize, RadialSize);
            if (MenuToolPreviewMenus.CreateRadial == null)
            {
                EditorGUI.HelpBox(new Rect(area.x, area.y, area.width, 40f),
                    "Install Gesture Manager (3.9 or newer) to preview the radial menu here. The list above still plans moves.", MessageType.Info);
                return;
            }
            if (radial == null) return;
            // Placed and fed mouse events in every event but Layout, whose rects are placeholders.
            if (Event.current.type != EventType.Layout) radial.Draw(square, Shown);
            // Follow the radial: a submenu opened there is selected in the list and the breadcrumbs.
            List<VRCExpressionsMenu> path = radial.Path;
            MenuEntry entry;
            string now = path.Count > 0 && preview != null && preview.EntryOf.TryGetValue(path[path.Count - 1], out entry) ? entry.Path : "";
            if (now != shownPath && Event.current.type == EventType.Repaint && path.Count > 0)
            {
                shownPath = now;
                MenuEntry opened = planned.Find(now);
                if (opened != null && opened.Parent != null) managerTree.Select(opened);
                Shown.Repaint();
            }
        }

        private void DrawBits()
        {
            MenuEntry entry = lastClicked;
            EditorGUILayout.LabelField("Last clicked: " + (entry == null ? "nothing yet" : entry.Path + " (" + MenuToolIcons.TypeName(entry.Type) + ")"), EditorStyles.boldLabel);
            if (entry != null)
            {
                foreach (string name in entry.Parameters())
                {
                    ParamEntry param = scan.Param(name);
                    string kind = param == null ? "built-in" : param.Type + (param.Synced ? ", synced, " + param.Bits + (param.Bits == 1 ? " bit" : " bits") : ", local, 0 bits") +
                        (param.Estimated ? ", estimated name" : "");
                    float value = radial != null ? radial.Value(name) : 0f;
                    EditorGUILayout.LabelField(name + "  =  " + value.ToString("0.###", CultureInfo.InvariantCulture) + "    (" + kind + ")");
                }
                if (!entry.Parameters().Any()) EditorGUILayout.LabelField("No parameter.", EditorStyles.miniLabel);
            }
            if (writes.Count > 0) EditorGUILayout.LabelField("Writes: " + string.Join("   ", writes), EditorStyles.wordWrappedMiniLabel);
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
                writes.Insert(0, write.Key + " = " + write.Value.ToString("0.###", CultureInfo.InvariantCulture));
                if (writes.Count > WriteLog) writes.RemoveAt(writes.Count - 1);
                // The control that wrote it: in the submenu shown first, else anywhere.
                MenuEntry at = planned != null ? planned.Find(shownPath) ?? planned : null;
                if (at == null) continue;
                lastClicked = at.Children.FirstOrDefault(child => child.Parameters().Contains(write.Key))
                    ?? planned.Descendants().FirstOrDefault(child => !child.IsSubMenu && child.Parameters().Contains(write.Key)) ?? lastClicked;
            }
            pendingWrites.Clear();
        }

        private void Clicked(MenuEntry entry)
        {
            if (entry == null) return;
            lastClicked = entry;
            Navigate(entry.IsSubMenu ? entry : entry.Parent);
        }

        // Opens a submenu of the planned layout in the radial (the root and each submenu down to it).
        private void Navigate(MenuEntry submenu)
        {
            if (submenu == null) return;
            shownPath = submenu.Path;
            if (radial == null || preview == null) return;
            var chain = new List<VRCExpressionsMenu>();
            for (MenuEntry at = submenu; at != null; at = at.Parent)
            {
                VRCExpressionsMenu menu;
                if (preview.MenuOf.TryGetValue(at, out menu)) chain.Insert(0, menu);
            }
            radial.Navigate(chain);
        }

        private void Discard()
        {
            if (!EditorUtility.DisplayDialog(Title, "Discard the " + plan.Moves.Count + " planned moves? Nothing has been applied yet.", "Discard", "Cancel"))
            {
                RiryGui.ExitAfterModal();
                return;
            }
            plan.Moves.Clear();
            plan.Save(PlanRoot);
            replan = true;
            RiryGui.ExitAfterModal();
        }

        private void ApplyPlan()
        {
            var sources = checks.SelectMany(c => c.Affected).Where(a => a != null).Distinct().Select(a => a.name).ToList();
            string message = "Apply " + plan.Moves.Count + " planned moves to " + PlanRoot.name + "?\n\nSources changed: " + string.Join(", ", sources) +
                "\n\nMenu assets other places also use are copied to Assets/RiryTools/MenuTool first. One Ctrl+Z undoes everything this does (copied files stay).";
            if (!EditorUtility.DisplayDialog(Title, message, "Apply", "Cancel"))
            {
                RiryGui.ExitAfterModal();
                return;
            }
            MenuToolApply.Report report = MenuToolApply.Apply(target, plan);
            plan.Moves.Clear();
            plan.Save(PlanRoot);
            MarkStale();
            if (report.Problems.Count > 0)
                EditorUtility.DisplayDialog(Title, "Applied " + report.Applied + " moves. Check these:\n\n" + string.Join("\n", report.Problems.Take(12)), "OK");
            RiryGui.ExitAfterModal();
        }
    }
}

// ============================================================================
// GLOSSARY: Menu Tool window
// The menu entry, Tools > RiryTools > Menu Tool, is in Editor/VRChat/RiryVRChatMenus.cs; the Main Menu hosts it as a tab.
// ============================================================================
//
// MenuToolWindow (this file)
//   Open()                   Its own window (minSize MinWidth 640 x MinHeight 760), on the selection when it has no target.
//   Surface / Shown          IRiryHostedTool: the Main Menu sets itself as the surface when hosting, so tabs size to it and
//                            the radial is placed in it; on its own, the window itself.
//   target                   Avatar, prefab asset, Prefab Mode or Hierarchy object ([SerializeField]; Undo via RiryGui.Set).
//   Refresh / MarkStale      Rescans (MenuToolScanner) on the next Layout after a change (hierarchy, project, Undo, Rescan),
//                            loads the plan (MenuPlan), rebuilds the trees and the preview (Replan).
//   Replan                   Planned layout (MenuToolPlanner), the list, the preview menus and the radial, staying in the
//                            submenu shown before.
//   Parameter budget tab     DrawSummary: compression off / on / change (custom parameters, synced, local, synced bits, bits
//                            and parameters left), Estimated / Exact / Unavailable; Include Bools / Puppets (the avatar's
//                            compressor's when it has one); Compare exactly (MenuToolBudget.Exact). BudgetTree; Shared
//                            parameters (types differ in red); Content blocks (each control's path, bindings, Select);
//                            Notes.
//   Menu manager tab         Search, Discard plan, Apply (disabled until every move checks out); ManagerTree; DrawPlan
//                            (before -> after, edit, choice popup when several sources fit, errors, warnings, remove);
//                            DrawBreadcrumbs; DrawRadial (Gesture Manager, or how to get it); DrawBits (last clicked control's
//                            parameters: type, sync, bits, live value; the last WriteLog writes).
//   AddMove / MoveToMenu     A planned move from drag and drop or the Move to... context menu, saved to the plan JSON.
//   Clicked / Navigate / OnWritten  List selection opens that submenu in the radial; the radial's submenu selects the list
//                            row; a write marks the control that made it as last clicked.
//   ApplyPlan / Discard      Confirmed; Apply runs MenuToolApply (one undo step) and clears the plan; problems are shown.
