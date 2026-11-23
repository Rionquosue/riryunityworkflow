using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's debug window: the tables of what arrives, what is applied and why. Three lists that are easy to
    /// confuse are kept apart: Parameters (the avatar's exact parameter and blendshape names), Expressions (the tracking
    /// keys) and Hardware controls (the channels the selected device measures, where values are held or typed). Mappings
    /// shows each destination's formula and takes the user's own; Diagnostics the counted problems.
    /// It draws the session's snapshot and sends row commands to the session; searching and filtering change only what
    /// is listed. A popup of its own (its columns need the width), opened from the FTEmulator window.
    /// </summary>
    internal sealed class FTEmulatorDebugWindow : EditorWindow
    {
        internal const string Title = "FTEmulator Debug";
        internal const int ParametersTab = 0;
        internal const int ExpressionsTab = 1;
        internal const int HardwareTab = 2;
        internal const int MappingsTab = 3;
        private static readonly string[] Tabs = { "Parameters", "Expressions", "Hardware controls", "Mappings", "Diagnostics" };
        private static readonly string[] MappedNames = { "All", "Mapped", "Unmapped" };
        private static readonly string[] ModeNames = { "Live", "Hold", "Manual", "Off" };
        private const float RowHeight = 20f;
        private const float TypeWidth = 74f, ValueWidth = 58f, OwnerWidth = 64f, ActionWidth = 420f;
        // A row counts as changed for this long after its value last moved.
        private const double ChangedSeconds = 2.0;

        [SerializeField] private int tab;
        [SerializeField] private string search = "";
        [SerializeField] private int mapped;
        [SerializeField] private bool showBlocked = true;
        [SerializeField] private bool changedOnly;
        [SerializeField] private Vector2 scroll;
        [SerializeField] private Vector2 textScroll;
        [SerializeField] private string mappingParameter = "";
        [SerializeField] private string mappingFormula = "";
        [NonSerialized] private string mappingProblem;
        [NonSerialized] private FTEmulatorSessionSnapshot snapshot;
        [NonSerialized] private readonly Dictionary<string, KeyValuePair<float, double>> lastValues = new Dictionary<string, KeyValuePair<float, double>>(StringComparer.Ordinal);
        [NonSerialized] private readonly List<FTEmulatorDebugRow> visible = new List<FTEmulatorDebugRow>();

        /// <summary>Opens the window on that tab; a tab below 0 keeps the one it had.</summary>
        internal static void Open(int tab)
        {
            FTEmulatorDebugWindow window = GetWindow<FTEmulatorDebugWindow>(false, Title);
            window.minSize = new Vector2(960f, 420f);
            if (tab >= 0) window.tab = tab;
            window.Show();
        }

        /// <summary>The diagnostics of the running session, or of the last one, as text.</summary>
        internal static string DiagnosticsText()
        {
            FTEmulatorSession session = FTEmulator.Session;
            if (session == null) return FTEmulator.LastDiagnostics ?? "RiryTools FTEmulator diagnostics\nNo session has run.";
            FTEmulatorSessionSnapshot now = session.Snapshot();
            var header = new List<string>
            {
                "State: " + now.StateText,
                "Avatar: " + now.Avatar,
                "Backend: " + (now.Resolution != null ? now.Resolution.Summary : "None"),
                "Hardware: " + (now.Hardware != null ? now.Hardware.Label : "None") + ", " + now.Behavior,
                "Applied " + now.Applied + " · Blocked " + now.Blocked + " · Unmapped " + now.Unmapped + " · Held " + now.Held,
            };
            if (now.Resolution != null)
            {
                foreach (string line in now.Resolution.Evidence) header.Add("Evidence: " + line);
            }
            foreach (string line in now.PartialReasons) header.Add("Partial preview: " + line);
            if (session.Evaluator != null)
            {
                foreach (string line in session.Evaluator.Ignored) header.Add("Ignored, as the client ignores it: " + line);
                foreach (string line in FTEmulatorNativeEvaluator.Assumptions) header.Add("Assumed: " + line);
            }
            return session.Diagnostics.ExportText(header);
        }

        // Ten times a second: a new snapshot, then a repaint.
        private void OnInspectorUpdate()
        {
            if (StopAllToolScripts.Active) return;
            FTEmulatorSession session = FTEmulator.Session;
            snapshot = session != null ? session.Snapshot() : FTEmulator.LastSnapshot;
            if (snapshot != null)
            {
                double now = FTEmulatorClock.Now;
                Track(snapshot.Parameters, "p:", now);
                Track(snapshot.Expressions, "e:", now);
                Track(snapshot.Controls, "c:", now);
            }
            Repaint();
        }

        private void Track(List<FTEmulatorDebugRow> rows, string prefix, double now)
        {
            foreach (FTEmulatorDebugRow row in rows)
            {
                string key = prefix + row.Name;
                float value = row.HasEffective ? row.Effective : float.NaN;
                if (lastValues.TryGetValue(key, out KeyValuePair<float, double> last) && (last.Key == value || float.IsNaN(last.Key) && float.IsNaN(value))) continue;
                lastValues[key] = new KeyValuePair<float, double>(value, now);
            }
        }

        private void OnGUI()
        {
            if (RiryToolGUI.Stopped()) return;
            GUILayout.Space(RiryToolsMainMenu.TopMargin);
            tab = RiryToolsMainMenu.TabStrip(this, tab, Tabs, 0f, RiryToolsMainMenu.SubTabs);
            EditorGUILayout.Space(4f);
            FTEmulatorSession session = FTEmulator.Session;
            if (snapshot == null) snapshot = session != null ? session.Snapshot() : FTEmulator.LastSnapshot;
            if (snapshot == null)
            {
                EditorGUILayout.HelpBox("No session has run. Start FTEmulator in Play Mode.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField(snapshot.StateText + " · " + snapshot.Avatar, EditorStyles.miniLabel);
            switch (tab)
            {
                case ParametersTab: DrawTable(session, snapshot.Parameters, "p:", "Avatar parameter / blendshape"); break;
                case ExpressionsTab: DrawTable(session, snapshot.Expressions, "e:", "Tracking key"); break;
                case HardwareTab: DrawHardware(session); break;
                case MappingsTab: DrawMappings(session); break;
                default: DrawDiagnostics(); break;
            }
            EditorGUILayout.LabelField("Applied " + snapshot.Applied + " · Blocked " + snapshot.Blocked + " · Unmapped " + snapshot.Unmapped + " · Held " + snapshot.Held,
                EditorStyles.miniLabel);
        }

        private void DrawFilter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));
                mapped = EditorGUILayout.Popup(mapped, MappedNames, GUILayout.Width(96f));
                showBlocked = GUILayout.Toggle(showBlocked, "Show blocked", GUILayout.Width(104f));
                changedOnly = GUILayout.Toggle(changedOnly, "Changed only", GUILayout.Width(104f));
            }
        }

        private void DrawHardware(FTEmulatorSession session)
        {
            if (snapshot.Hardware != null)
            {
                EditorGUILayout.LabelField(snapshot.Hardware.Label + " · " + (snapshot.Behavior == FTEmulatorBehavior.Strict ? "Strict hardware" : "Documented module behavior") +
                    " · Eye tracking " + (snapshot.EyeTracking ? "on" : "off") + " · Face tracking " + (snapshot.FaceTracking ? "on" : "off"), EditorStyles.boldLabel);
                foreach (string note in snapshot.Hardware.Notes) EditorGUILayout.LabelField(note, EditorStyles.wordWrappedMiniLabel);
            }
            DrawTable(session, snapshot.Controls, "c:", "Hardware control");
        }

        private void DrawTable(FTEmulatorSession session, List<FTEmulatorDebugRow> rows, string prefix, string nameHeader)
        {
            DrawFilter();
            double now = FTEmulatorClock.Now;
            visible.Clear();
            foreach (FTEmulatorDebugRow row in rows)
            {
                if (!string.IsNullOrEmpty(search) && row.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (mapped == 1 && !row.Mapped || mapped == 2 && row.Mapped) continue;
                if (!showBlocked && row.Blocked) continue;
                if (changedOnly && (!lastValues.TryGetValue(prefix + row.Name, out KeyValuePair<float, double> last) || now - last.Value > ChangedSeconds)) continue;
                visible.Add(row);
            }

            // The flexible columns share what the fixed ones leave.
            float flexible = Mathf.Max(240f, position.width - TypeWidth - 2f * ValueWidth - OwnerWidth - ActionWidth - GUI.skin.verticalScrollbar.fixedWidth - 12f);
            float nameWidth = flexible * 0.4f, hardwareWidth = flexible * 0.32f, sourceWidth = flexible * 0.28f;
            Rect header = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            float x = header.x + 4f;
            Cell(ref x, header, nameWidth, nameHeader, EditorStyles.miniBoldLabel);
            Cell(ref x, header, TypeWidth, "Type", EditorStyles.miniBoldLabel);
            Cell(ref x, header, ValueWidth, "Live", EditorStyles.miniBoldLabel);
            Cell(ref x, header, ValueWidth, "Effective", EditorStyles.miniBoldLabel);
            Cell(ref x, header, OwnerWidth, "Owner", EditorStyles.miniBoldLabel);
            Cell(ref x, header, hardwareWidth, "Hardware", EditorStyles.miniBoldLabel);
            Cell(ref x, header, sourceWidth, "Source", EditorStyles.miniBoldLabel);
            Cell(ref x, header, ActionWidth, "Action", EditorStyles.miniBoldLabel);

            using (var view = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.ExpandHeight(true)))
            {
                scroll = view.scrollPosition;
                Rect area = GUILayoutUtility.GetRect(0f, Mathf.Max(RowHeight, visible.Count * RowHeight), GUILayout.ExpandWidth(true));
                if (visible.Count == 0) EditorGUI.LabelField(new Rect(area.x + 4f, area.y, area.width, RowHeight), "Nothing to list.", EditorStyles.miniLabel);
                // Only the rows in view are drawn.
                int first = Mathf.Clamp(Mathf.FloorToInt(scroll.y / RowHeight), 0, Mathf.Max(0, visible.Count - 1));
                int last = Mathf.Min(visible.Count, first + Mathf.CeilToInt(position.height / RowHeight) + 1);
                for (int i = first; i < last; i++)
                {
                    var rect = new Rect(area.x, area.y + i * RowHeight, area.width, RowHeight);
                    if ((i & 1) == 1 && Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.08f));
                    DrawRow(session, visible[i], rect, nameWidth, hardwareWidth, sourceWidth);
                }
            }
        }

        private void DrawRow(FTEmulatorSession session, FTEmulatorDebugRow row, Rect rect, float nameWidth, float hardwareWidth, float sourceWidth)
        {
            float x = rect.x + 4f;
            using (new EditorGUI.DisabledScope(row.Blocked))
            {
                Cell(ref x, rect, nameWidth, new GUIContent(string.IsNullOrEmpty(row.Group) ? row.Name : row.Name, string.IsNullOrEmpty(row.Note) ? row.Name : row.Name + "\n" + row.Note), EditorStyles.label);
                Cell(ref x, rect, TypeWidth, row.Type, EditorStyles.miniLabel);
                Cell(ref x, rect, ValueWidth, row.HasLive ? Number(row.Live, row.Type) : "—", EditorStyles.miniLabel);
                Cell(ref x, rect, ValueWidth, row.HasEffective ? Number(row.Effective, row.Type) : "—", EditorStyles.miniLabel);
                Cell(ref x, rect, OwnerWidth, row.Owner, EditorStyles.miniLabel);
            }
            Cell(ref x, rect, hardwareWidth, new GUIContent(row.Hardware, row.Hardware), EditorStyles.miniLabel);
            Cell(ref x, rect, sourceWidth, new GUIContent(row.Source, string.IsNullOrEmpty(row.Note) ? row.Source : row.Source + "\n" + row.Note), EditorStyles.miniLabel);
            var action = new Rect(x, rect.y + 1f, ActionWidth - 8f, rect.height - 2f);
            if (row.Key != null && !row.Blocked) DrawAction(session, row, action, tab == HardwareTab);
            else if (!string.IsNullOrEmpty(row.LinkedControl) && tab != HardwareTab)
            {
                // A value that follows a device channel is changed there, so everything linked to it moves together.
                if (GUI.Button(new Rect(action.x, action.y, Mathf.Min(action.width, 220f), action.height), new GUIContent("Edit " + row.LinkedControl, "Show this channel under Hardware controls."), EditorStyles.miniButton))
                {
                    tab = HardwareTab;
                    search = row.LinkedControl;
                    scroll = Vector2.zero;
                    GUI.FocusControl(null);
                }
            }
            else if (row.Owner == "Session") EditorGUI.LabelField(action, "Read-only", EditorStyles.miniLabel);
            else if (row.Blocked) EditorGUI.LabelField(action, "Disabled", EditorStyles.miniLabel);
        }

        // Live / Hold / Manual / Off, and the value while Manual. Typing a value makes the row Manual. slider: every Float row
        // gets a slider with its number field (Hardware controls); otherwise only ranges up to 2 do.
        private static void DrawAction(FTEmulatorSession session, FTEmulatorDebugRow row, Rect rect, bool slider)
        {
            using (new EditorGUI.DisabledScope(session == null))
            {
                int mode = (int)row.Mode;
                int picked = GUI.Toolbar(new Rect(rect.x, rect.y, 190f, rect.height), mode, ModeNames, EditorStyles.miniButton);
                if (picked != mode && session != null && session.SetRowMode(row.Key, (FTEmulatorRowMode)picked)) row.Mode = (FTEmulatorRowMode)picked;
                var field = new Rect(rect.x + 196f, rect.y, rect.width - 196f, rect.height);
                float shown = row.HasEffective ? row.Effective : 0f;
                if (row.Type == "Bool")
                {
                    bool value = EditorGUI.Toggle(field, shown != 0f);
                    if (value != (shown != 0f) && session != null) Typed(session, row, value ? 1f : 0f);
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    float value = (slider || row.Max - row.Min <= 2f) && row.Type == "Float" && row.Max > row.Min
                        ? EditorGUI.Slider(field, shown, row.Min, row.Max)
                        : EditorGUI.DelayedFloatField(field, shown);
                    if (EditorGUI.EndChangeCheck() && session != null) Typed(session, row, value);
                }
            }
        }

        // The row shows the typed value at once, not only from the next snapshot, so the control does not jump back.
        private static void Typed(FTEmulatorSession session, FTEmulatorDebugRow row, float value)
        {
            if (!session.SetRowManual(row.Key, value)) return;
            row.Mode = FTEmulatorRowMode.Manual;
            row.Effective = value;
            row.HasEffective = true;
        }

        private static string Number(float value, string type)
        {
            if (type == "Bool") return value != 0f ? "true" : "false";
            if (type == "Int") return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
            return value.ToString(type == "Blendshape" ? "0.0" : "0.00", CultureInfo.InvariantCulture);
        }

        private static void Cell(ref float x, Rect row, float width, string text, GUIStyle style)
        {
            Cell(ref x, row, width, new GUIContent(text), style);
        }

        private static void Cell(ref float x, Rect row, float width, GUIContent content, GUIStyle style)
        {
            // A little room between columns, so long text does not run into its neighbour.
            EditorGUI.LabelField(new Rect(x, row.y, width - 6f, row.height), content, style);
            x += width;
        }

        private void DrawMappings(FTEmulatorSession session)
        {
            EditorGUILayout.LabelField("Mapping inspector", EditorStyles.boldLabel);
            if (snapshot.Resolution != null)
            {
                EditorGUILayout.LabelField("Profile", (snapshot.Hardware != null ? snapshot.Hardware.Name : "None") + " / " + snapshot.Resolution.Summary);
                EditorGUILayout.LabelField("Reason selected", snapshot.Resolution.Evidence.Count > 0 ? snapshot.Resolution.Evidence[0] : "None", EditorStyles.wordWrappedLabel);
            }
            using (var view = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.ExpandHeight(true)))
            {
                scroll = view.scrollPosition;
                foreach (FTEmulatorDebugRow row in snapshot.Parameters)
                {
                    if (row.Key != null || string.IsNullOrEmpty(row.Source)) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent(row.Name, row.Name), GUILayout.MinWidth(180f));
                        EditorGUILayout.LabelField(new GUIContent(row.Source, row.Source), EditorStyles.miniLabel, GUILayout.MinWidth(150f));
                        EditorGUILayout.LabelField(new GUIContent(row.Note, row.Note), EditorStyles.miniLabel, GUILayout.MinWidth(220f));
                        EditorGUILayout.LabelField(row.Mapped ? row.Blocked ? "Blocked" : "Available" : "Unmapped", EditorStyles.miniLabel, GUILayout.Width(70f));
                    }
                }
            }
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Override mapping", EditorStyles.boldLabel);
            mappingParameter = EditorGUILayout.TextField(new GUIContent("Destination", "The avatar parameter's exact name. It must be in Expression Parameters."), mappingParameter);
            mappingFormula = EditorGUILayout.TextField(new GUIContent("Transform", "A formula of tracking keys: + - * / ( ) abs() min() max() clamp(). Example: JawRight - JawLeft"), mappingFormula);
            if (!string.IsNullOrEmpty(mappingProblem)) EditorGUILayout.HelpBox(mappingProblem, MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(mappingParameter) || string.IsNullOrWhiteSpace(mappingFormula)))
                {
                    if (GUILayout.Button("Override mapping", GUILayout.Height(22f))) OverrideMapping(session);
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(mappingParameter) || !FTEmulatorSettings.CustomMappings.ContainsKey(mappingParameter.Trim())))
                {
                    if (GUILayout.Button(new GUIContent("Reset to detected", "Remove this parameter's own mapping."), GUILayout.Height(22f)))
                    {
                        Dictionary<string, string> mappings = FTEmulatorSettings.CustomMappings;
                        mappings.Remove(mappingParameter.Trim());
                        FTEmulatorSettings.CustomMappings = mappings;
                        if (session != null) session.SetCustomMappings(mappings);
                        mappingProblem = null;
                    }
                }
            }
            foreach (KeyValuePair<string, string> pair in FTEmulatorSettings.CustomMappings)
                EditorGUILayout.LabelField(pair.Key + " = " + pair.Value, EditorStyles.miniLabel);
        }

        private void OverrideMapping(FTEmulatorSession session)
        {
            FTEmulatorCatalog catalog = FTEmulator.Catalog;
            if (FTEmulatorExpression.Compile(mappingFormula, catalog.Key, out string error) == null)
            {
                mappingProblem = "Transform: " + error;
                return;
            }
            mappingProblem = null;
            Dictionary<string, string> mappings = FTEmulatorSettings.CustomMappings;
            mappings[mappingParameter.Trim()] = mappingFormula.Trim();
            FTEmulatorSettings.CustomMappings = mappings;
            if (session != null) session.SetCustomMappings(mappings);
        }

        private void DrawDiagnostics()
        {
            string text = DiagnosticsText();
            if (GUILayout.Button("Copy diagnostics", GUILayout.Height(22f), GUILayout.Width(160f))) EditorGUIUtility.systemCopyBuffer = text;
            using (var view = new EditorGUILayout.ScrollViewScope(textScroll, GUILayout.ExpandHeight(true)))
            {
                textScroll = view.scrollPosition;
                EditorGUILayout.SelectableLabel(text, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true),
                    GUILayout.MinHeight(EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(text), Mathf.Max(200f, position.width - 24f))));
            }
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator debug window
// ============================================================================
//
// FTEmulatorDebugWindow (this file)
//   Open(tab)                The popup (minimum 960 x 420) on a tab; below 0 keeps the tab it had.
//   DiagnosticsText()        The running session's diagnostics (FTEmulatorDiagnostics.ExportText with state, avatar, backend,
//                            hardware, counts, evidence, partial preview reasons, ignored settings and assumptions), or the
//                            last session's.
//   Tabs                     Parameters (ParametersTab), Expressions (ExpressionsTab), Hardware controls (HardwareTab),
//                            Mappings (MappingsTab), Diagnostics, drawn with RiryToolsMainMenu.TabStrip.
//   OnInspectorUpdate()      Ten times a second: a new snapshot; Track notes when each row's value last changed.
//   DrawTable                The filter (search, All / Mapped / Unmapped, Show blocked, Changed only: changed within
//                            ChangedSeconds, 2 s), the header, and only the rows in view (RowHeight 20). Columns: name, Type,
//                            Live, Effective, Owner, Hardware, Source, Action.
//   DrawAction               A row with a key: Live / Hold / Manual / Off (FTEmulatorSession.SetRowMode) and its value
//                            (SetRowManual: typing makes the row Manual). A row that follows one hardware control has an
//                            "Edit <control>" button that shows it under Hardware controls. Status rows are read-only.
//   DrawHardware             The profile's label, behavior, tracking availability and notes above its controls.
//   DrawMappings             Each destination's source, formula and status; Override mapping (Destination, Transform:
//                            checked with FTEmulatorExpression.Compile, stored in FTEmulatorSettings.CustomMappings) and
//                            Reset to detected.
//   DrawDiagnostics          DiagnosticsText, selectable, and Copy diagnostics.
