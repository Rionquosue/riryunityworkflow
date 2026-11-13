using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Avatar Migration Assistant feature 2, Manual matching: each row is a source path
    /// with its target path as an editable text field, an object picker limited to the
    /// target hierarchy, and how it was matched. Automatic mode lists the unmatched rows;
    /// manual mode lists every row. Animation paths that resolve to no source object are
    /// listed too. Continue applies the edits to the map; Cancel stops the run with
    /// nothing changed. A row left empty stays unmatched. After each edit, automatic
    /// matching runs again for every row the user has not edited, using the targets now
    /// set above it, so the objects below a parent matched by hand fill in by themselves
    /// and can still be corrected.
    /// </summary>
    internal sealed class AvatarMigratorMatchWindow : EditorWindow
    {
        private const string Title = "Manual matching";

        private sealed class Row
        {
            internal AvatarMigratorMatch Match;
            // Set instead of Match for an animation path that resolves to no source object.
            internal string AnimationPath;
            internal string Original = "";
            internal string Path = "";
            // True once the user typed or picked in this row; the row is then manual.
            internal bool Edited;
            // How the row's current path was found when it is not edited.
            internal AvatarMigratorMatchMethod Auto;
        }

        private AvatarMigratorMatchObjects map;
        private readonly List<Row> rows = new List<Row>();
        private readonly Dictionary<Transform, Row> rowOf = new Dictionary<Transform, Row>();
        private bool accepted;
        private Vector2 scroll;
        private GUIStyle invalidStyle;

        /// <summary>
        /// Shows the rows and waits. True when Continue was clicked: the map holds the edits
        /// and animationTargets each listed animation path's chosen target (null for none).
        /// </summary>
        internal static bool Review(AvatarMigratorMatchObjects map, bool everyRow, IList<string> animationPaths,
            out Dictionary<string, Transform> animationTargets)
        {
            animationTargets = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var window = CreateInstance<AvatarMigratorMatchWindow>();
            window.titleContent = new GUIContent(Title);
            window.map = map;
            foreach (AvatarMigratorMatch match in map.Matches)
            {
                if (match.Source == map.SourceRoot || (!everyRow && match.Target != null)) continue;
                string path = map.TargetPath(match.Target);
                var row = new Row { Match = match, Original = path, Path = path, Auto = match.Method };
                window.rows.Add(row);
                window.rowOf[match.Source] = row;
            }
            if (animationPaths != null)
            {
                foreach (string path in animationPaths) window.rows.Add(new Row { AnimationPath = path });
            }
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(main.center.x - 400f, main.center.y - 250f, 800f, 500f);
            window.minSize = new Vector2(600f, 300f);
            window.ShowModalUtility();
            if (!window.accepted) return false;

            foreach (Row row in window.rows)
            {
                string path = row.Path.Trim();
                Transform target = path.Length == 0 ? null : map.FindOnTarget(path);
                if (row.Match == null)
                {
                    animationTargets[row.AnimationPath] = target;
                    continue;
                }
                if (!row.Edited && path == row.Original) continue;
                row.Match.Target = target;
                row.Match.Method = target == null ? AvatarMigratorMatchMethod.None : row.Edited ? AvatarMigratorMatchMethod.Manual : row.Auto;
            }
            return true;
        }

        // The target the source object has now: its row's path, else the map's match.
        private Transform CurrentTarget(Transform source)
        {
            Row row;
            if (source == null || !rowOf.TryGetValue(source, out row)) return map.TargetOf(source);
            string path = row.Path.Trim();
            return path.Length == 0 ? null : map.FindOnTarget(path);
        }

        // Automatic matching again for every row not edited, parents first (rows are in map order).
        private void Rematch()
        {
            foreach (Row row in rows)
            {
                if (row.Match == null || row.Edited) continue;
                AvatarMigratorMatchMethod method;
                Transform own = row.Match.Source;
                // Taken: the current choice of any other row, as edited in this window.
                Transform found = map.AutoMatch(own, CurrentTarget(own.parent), out method,
                    target => rows.Exists(other => other.Match != null && other.Match.Source != own && CurrentTarget(other.Match.Source) == target));
                row.Path = map.TargetPath(found);
                row.Auto = method;
            }
        }

        private bool Invalid(Row row)
        {
            string path = row.Path.Trim();
            return path.Length > 0 && map.FindOnTarget(path) == null;
        }

        private void OnGUI()
        {
            if (map == null || map.TargetRoot == null)
            {
                Close();
                return;
            }
            if (invalidStyle == null)
            {
                invalidStyle = new GUIStyle(EditorStyles.textField);
                invalidStyle.normal.textColor = invalidStyle.focused.textColor = new Color(1f, 0.35f, 0.35f);
            }
            EditorGUILayout.LabelField("Source: " + map.SourceRoot.name + "    Target: " + map.TargetRoot.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Type a path from the target root, or pick an object on the target. Leave a row empty to keep it unmatched.",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Source path", EditorStyles.miniBoldLabel, GUILayout.Width(position.width * 0.35f));
                GUILayout.Label("Target path", EditorStyles.miniBoldLabel);
                GUILayout.Label("Matched by", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
                // The rows' scroll bar is always shown, so the headers keep the same room for it.
                GUILayout.Space(GUI.skin.verticalScrollbar.fixedWidth);
            }

            bool anyInvalid = false;
            bool changed = false;
            scroll = EditorGUILayout.BeginScrollView(scroll, false, true);
            foreach (Row row in rows)
            {
                bool invalid = Invalid(row);
                anyInvalid |= invalid;
                using (new EditorGUILayout.HorizontalScope())
                {
                    string source = row.Match != null ? row.Match.SourcePath : row.AnimationPath + " (animation path)";
                    EditorGUILayout.SelectableLabel(source, GUILayout.Width(position.width * 0.35f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    string typed = EditorGUILayout.TextField(row.Path, invalid ? invalidStyle : EditorStyles.textField);
                    if (typed != row.Path)
                    {
                        row.Path = typed;
                        row.Edited = true;
                        changed = true;
                    }
                    Transform current = invalid || row.Path.Trim().Length == 0 ? null : map.FindOnTarget(row.Path.Trim());
                    var picked = (Transform)EditorGUILayout.ObjectField(current, typeof(Transform), true, GUILayout.Width(150f));
                    if (picked != current)
                    {
                        if (picked == null)
                        {
                            row.Path = "";
                            row.Edited = true;
                            changed = true;
                        }
                        else if (picked == map.TargetRoot || picked.IsChildOf(map.TargetRoot))
                        {
                            row.Path = map.TargetPath(picked);
                            row.Edited = true;
                            changed = true;
                            GUI.FocusControl(null);
                        }
                        else ShowNotification(new GUIContent("Pick an object under " + map.TargetRoot.name + "."));
                    }
                    GUILayout.Label(Method(row), EditorStyles.miniLabel, GUILayout.Width(80f));
                }
            }
            EditorGUILayout.EndScrollView();
            if (changed) Rematch();
            if (anyInvalid) EditorGUILayout.HelpBox("Paths in red are not on the target.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(90f))) Close();
                using (new EditorGUI.DisabledScope(anyInvalid))
                {
                    if (GUILayout.Button(new GUIContent("Continue", anyInvalid ? "Fix or clear the paths in red first." : ""), GUILayout.Width(90f)))
                    {
                        accepted = true;
                        Close();
                    }
                }
            }
        }

        private static string Method(Row row)
        {
            string path = row.Path.Trim();
            if (row.Match == null || row.Edited) return path.Length > 0 ? "Manual" : "None";
            switch (row.Auto)
            {
                case AvatarMigratorMatchMethod.NamePath: return "Name path";
                case AvatarMigratorMatchMethod.Mesh: return "Mesh";
                default: return row.Match.Method.ToString();
            }
        }
    }
}

// ============================================================================
// GLOSSARY: Manual matching window (AvatarMigratorMatchWindow.cs, window: Editor/UI/AvatarMigrator/AvatarMigratorWindow.cs)
// ============================================================================
//
// AvatarMigratorMatchWindow (this file)
//   Review(map, everyRow, animationPaths, out animationTargets)  Modal. Rows: source path, target path (red when not on
//                        the target), object picker (target hierarchy only), Matched by (Name path, Humanoid, Structure,
//                        Manual, None). everyRow: manual mode, every row; else only unmatched rows. Continue (disabled
//                        while a path is red) writes the edits into the map; Cancel returns false with nothing changed.
//   Animation paths      Listed with "(animation path)"; their chosen targets come back in animationTargets.
//   Rematch()            After each edit: AvatarMigratorMatchObjects.AutoMatch again for every row not edited, with the targets
//                        now set above it; Matched by shows the method it found. Edited rows are Manual.
