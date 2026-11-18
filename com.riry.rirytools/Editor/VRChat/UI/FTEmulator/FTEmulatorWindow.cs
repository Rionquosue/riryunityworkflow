using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The FTEmulator window (logic in Editor/VRChat/FTEmulator/): the avatar, backend, input and hardware choices, the
    /// Start, Active listening, Freeze output and Stop controls, and the OSC connection. It shows the session's snapshot
    /// and sends commands to FTEmulator and its session; it opens no socket, reads no avatar and works out no value
    /// itself. Opening or closing it starts and stops nothing: a session a user started keeps running without it, and is
    /// shown again when the window is reopened. Settings can be changed in Edit Mode; Start needs Play Mode.
    /// Tools > RiryTools > FTEmulator, and the Main Menu's Tools tab (FTEmulator sub-tab). The tables are in
    /// FTEmulatorDebugWindow.
    /// </summary>
    internal sealed class FTEmulatorWindow : EditorWindow
    {
        internal const string Title = "FTEmulator";
        private static readonly string[] InputNames = { "VRCFT OSC", "Manual" };
        private static readonly string[] BehaviorNames = { "Strict hardware", "Documented module behavior" };

        // The Main Menu's copy (CreateHosted): never shown on its own.
        [SerializeField] private bool hosted;
        // The Gesture Manager object chosen by the user. Never filled in automatically.
        [SerializeField] private Object managerObject;
        [SerializeField] private bool showEvidence;
        [SerializeField] private Vector2 scroll;
        [NonSerialized] private string startProblem;
        [NonSerialized] private FTEmulatorSessionSnapshot snapshot;

        /// <summary>The Main Menu's copy (RiryToolsVRChatMenus sets RiryToolsMainMenu.CreateFTEmulator to this): never shown on its own.</summary>
        internal static EditorWindow CreateHosted()
        {
            var window = CreateInstance<FTEmulatorWindow>();
            window.hosted = true;
            return window;
        }

        internal static void Open()
        {
            // Never the Main Menu's copy: GetWindow could find that one and show it as a second window drawing the same tool.
            FTEmulatorWindow window = null;
            foreach (FTEmulatorWindow found in Resources.FindObjectsOfTypeAll<FTEmulatorWindow>())
            {
                if (found.hosted) continue;
                window = found;
                break;
            }
            if (window == null) window = CreateInstance<FTEmulatorWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(320f, 300f);
            window.Show();
            window.Focus();
        }

        // Ten times a second (the Main Menu passes it on to its hosted copy): a new snapshot, then a repaint.
        private void OnInspectorUpdate()
        {
            if (StopAllToolScripts.Active) return;
            FTEmulatorSession session = FTEmulator.Session;
            snapshot = session != null ? session.Snapshot() : FTEmulator.LastSnapshot;
            if (session != null && managerObject == null) managerObject = FTEmulator.Manager;
            Repaint();
        }

        private void OnGUI()
        {
            if (RiryToolGUI.Stopped()) return;
            if (hosted)
            {
                Draw();
                return;
            }
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                Draw();
            }
        }

        private void Draw()
        {
            FTEmulatorCatalog catalog = FTEmulator.Catalog;
            if (!catalog.Usable)
            {
                EditorGUILayout.HelpBox("FTEmulator's profile data could not be read.\n" + string.Join("\n", catalog.Problems.ToArray()), MessageType.Error);
                if (GUILayout.Button("Read profile data again", GUILayout.Height(24f))) FTEmulator.ReloadCatalog();
                return;
            }
            FTEmulatorSession session = FTEmulator.Session;
            if (snapshot == null) snapshot = session != null ? session.Snapshot() : FTEmulator.LastSnapshot;
            bool playing = EditorApplication.isPlaying;

            EditorGUILayout.Space(4f);
            string state = session != null && snapshot != null ? snapshot.StateText : snapshot != null && playing ? snapshot.StateText : "Stopped";
            EditorGUILayout.LabelField("State", state + (playing ? " · Play Mode" : " · Edit Mode"), EditorStyles.boldLabel);
            if (!playing) EditorGUILayout.HelpBox("Enter Play Mode to start tracking.", MessageType.Info);

            DrawAvatar(session, catalog, playing);
            EditorGUILayout.Space(6f);
            DrawInputAndHardware(session, catalog);
            EditorGUILayout.Space(6f);
            DrawControls(session, playing);
            EditorGUILayout.Space(6f);
            if (FTEmulatorSettings.Input == FTEmulatorInput.VrcftOsc) DrawOsc(session);
            else DrawManual(session);
        }

        private void DrawAvatar(FTEmulatorSession session, FTEmulatorCatalog catalog, bool playing)
        {
            if (FTEmulator.CreateOutput == null)
            {
                EditorGUILayout.HelpBox(new GestureManagerNotFoundException(Title).Message, MessageType.Info);
                return;
            }
            using (new EditorGUI.DisabledScope(session != null))
            {
                managerObject = EditorGUILayout.ObjectField(new GUIContent("Gesture Manager", "The Gesture Manager object whose avatar is driven."),
                    managerObject, FTEmulator.ManagerType ?? typeof(Object), true);
            }
            if (managerObject == null && playing && session == null) EditorGUILayout.HelpBox("Select a Gesture Manager avatar.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Avatar", session != null && snapshot != null ? snapshot.Avatar : "None");
                using (new EditorGUI.DisabledScope(session == null))
                {
                    if (GUILayout.Button(new GUIContent("Rescan", "Read the avatar's parameters and face tracking settings again."), GUILayout.Width(70f)))
                        session.Rescan();
                }
            }

            // Backend: Auto, Native, then the catalog's avatar profiles.
            var ids = new List<string> { FTEmulatorTemplateResolver.Auto, FTEmulatorTemplateResolver.NativeChoice };
            var names = new List<string> { "Auto", "Native" };
            foreach (FTEmulatorAvatarProfile profile in catalog.Avatars)
            {
                if (profile.Kind == FTEmulatorAvatarProfile.Template && profile.Backend == FTEmulatorAvatarProfile.Native) continue;
                ids.Add(profile.Id);
                names.Add(profile.Name);
            }
            int chosen = Mathf.Max(0, ids.IndexOf(FTEmulatorSettings.Backend));
            int picked = EditorGUILayout.Popup(new GUIContent("Avatar backend", "Auto detects it. A choice made here does not skip the checks."), chosen, names.ToArray());
            if (picked != chosen)
            {
                FTEmulatorSettings.Backend = ids[picked];
                if (session != null) session.SetBackend(ids[picked]);
            }
            if (session == null || snapshot == null || snapshot.Resolution == null) return;
            EditorGUILayout.LabelField(" ", snapshot.Resolution.Summary);
            showEvidence = EditorGUILayout.Foldout(showEvidence, "Evidence", true);
            if (showEvidence)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    if (snapshot.Resolution.Evidence.Count == 0) EditorGUILayout.LabelField("None", EditorStyles.wordWrappedMiniLabel);
                    foreach (string line in snapshot.Resolution.Evidence) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
                }
            }
            if (snapshot.Partial)
            {
                EditorGUILayout.HelpBox((snapshot.Resolution.Backend == null ? "Nothing is written" : "Partial preview") + "\n" +
                    string.Join("\n", snapshot.PartialReasons.ToArray()), MessageType.Warning);
            }
        }

        private void DrawInputAndHardware(FTEmulatorSession session, FTEmulatorCatalog catalog)
        {
            int input = FTEmulatorSettings.Input == FTEmulatorInput.Manual ? 1 : 0;
            int pickedInput = EditorGUILayout.Popup(new GUIContent("Input", "VRCFT OSC: VRCFaceTracking sends to FTEmulator. Manual: values typed in the debug window."), input, InputNames);
            if (pickedInput != input)
            {
                FTEmulatorInput value = pickedInput == 1 ? FTEmulatorInput.Manual : FTEmulatorInput.VrcftOsc;
                FTEmulatorSettings.Input = value;
                if (session != null) session.SetInput(value);
            }

            var labels = new string[catalog.Hardware.Count];
            int hardware = 0;
            for (int i = 0; i < catalog.Hardware.Count; i++)
            {
                labels[i] = catalog.Hardware[i].Label;
                if (catalog.Hardware[i].Id == FTEmulatorSettings.Hardware) hardware = i;
            }
            int pickedHardware = EditorGUILayout.Popup(new GUIContent("Hardware limits", "Which channels a device has. A capability list, not a model of its accuracy."), hardware, labels);
            int behavior = FTEmulatorSettings.Behavior == FTEmulatorBehavior.ModuleBehavior ? 1 : 0;
            int pickedBehavior = EditorGUILayout.Popup(new GUIContent("Behavior", "Strict: only what the sensors measure. Documented module behavior: also what the device's VRCFT module derives."),
                behavior, BehaviorNames);
            if (pickedHardware != hardware || pickedBehavior != behavior)
            {
                FTEmulatorBehavior value = pickedBehavior == 1 ? FTEmulatorBehavior.ModuleBehavior : FTEmulatorBehavior.Strict;
                FTEmulatorSettings.Hardware = catalog.Hardware[pickedHardware].Id;
                FTEmulatorSettings.Behavior = value;
                if (session != null) session.SetHardware(catalog.Hardware[pickedHardware].Id, value);
            }
            if (GUILayout.Button("View allowed channels", GUILayout.Height(22f))) EditorApplication.delayCall += () => FTEmulatorDebugWindow.Open(FTEmulatorDebugWindow.HardwareTab);
        }

        private void DrawControls(FTEmulatorSession session, bool playing)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!playing || session != null || managerObject == null || FTEmulator.CreateOutput == null))
                {
                    if (GUILayout.Button("Start", GUILayout.Height(26f)))
                    {
                        FTEmulator.Start(managerObject, out startProblem);
                        snapshot = null;
                    }
                }
                using (new EditorGUI.DisabledScope(session == null))
                {
                    if (GUILayout.Button("Stop", GUILayout.Height(26f)))
                    {
                        FTEmulator.Stop("Stop pressed");
                        snapshot = null;
                    }
                }
            }
            if (!string.IsNullOrEmpty(startProblem) && session == null) EditorGUILayout.HelpBox(startProblem, MessageType.Warning);
            using (new EditorGUI.DisabledScope(session == null))
            {
                using (new EditorGUI.DisabledScope(FTEmulatorSettings.Input != FTEmulatorInput.VrcftOsc))
                {
                    bool listening = session != null && session.Listening;
                    bool wanted = EditorGUILayout.ToggleLeft(new GUIContent("Active listening", "Off closes the receiver and keeps the last values."), listening);
                    if (session != null && wanted != listening) session.SetListening(wanted);
                }
                bool frozen = session != null && session.Frozen;
                bool freeze = EditorGUILayout.ToggleLeft(new GUIContent("Freeze output", "Keeps the values written to the avatar while input goes on being shown."), frozen);
                if (session != null && freeze != frozen) session.SetFrozen(freeze);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open debug window", GUILayout.Height(22f))) EditorApplication.delayCall += () => FTEmulatorDebugWindow.Open(-1);
                using (new EditorGUI.DisabledScope(session == null))
                {
                    if (GUILayout.Button(new GUIContent("Reset overrides", "Every row back to Live."), GUILayout.Height(22f))) session.ResetOverrides();
                }
            }
        }

        private void DrawOsc(FTEmulatorSession session)
        {
            EditorGUILayout.LabelField("OSC connection", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Bind address", "127.0.0.1");
            int port = EditorGUILayout.DelayedIntField(new GUIContent("Receive port", "The port VRCFaceTracking sends to (its OSC out port)."), FTEmulatorSettings.Port);
            if (port != FTEmulatorSettings.Port) FTEmulatorSettings.Port = port;
            bool discovery = EditorGUILayout.ToggleLeft(new GUIContent("Discovery", "The OSCQuery service VRCFaceTracking finds the avatar through."), FTEmulatorSettings.Discovery);
            if (discovery != FTEmulatorSettings.Discovery) FTEmulatorSettings.Discovery = discovery;
            if (session != null && snapshot != null)
            {
                if (!string.IsNullOrEmpty(snapshot.SourceProblem)) EditorGUILayout.HelpBox(snapshot.SourceProblem, MessageType.Error);
                EditorGUILayout.LabelField("Advertised identity", string.IsNullOrEmpty(snapshot.ServiceName) ? "Not advertised"
                    : snapshot.ServiceName + (snapshot.Discoverable ? "" : " (no network interface)"));
                EditorGUILayout.LabelField("Tracking app", snapshot.Peers.Count > 0 ? string.Join(", ", snapshot.Peers.ToArray()) : "Waiting");
                EditorGUILayout.LabelField("Last payload", snapshot.LastPayload ?? "None");
                EditorGUILayout.LabelField("Traffic", "Received " + snapshot.PacketsPerSecond + "/s · Rejected " + snapshot.Rejected +
                    (snapshot.SampleAge >= 0 ? " · Last sample " + (snapshot.SampleAge * 1000.0).ToString("0", CultureInfo.InvariantCulture) + " ms" : ""));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(session == null))
                {
                    if (GUILayout.Button(new GUIContent("Restart connection", "Close and open the receiver with the port and discovery set above."), GUILayout.Height(22f)))
                        session.RestartConnection(FTEmulatorSettings.Port, FTEmulatorSettings.Discovery);
                }
                using (new EditorGUI.DisabledScope(session == null && string.IsNullOrEmpty(FTEmulator.LastDiagnostics)))
                {
                    if (GUILayout.Button("Copy diagnostics", GUILayout.Height(22f))) EditorGUIUtility.systemCopyBuffer = FTEmulatorDebugWindow.DiagnosticsText();
                }
            }
        }

        private void DrawManual(FTEmulatorSession session)
        {
            EditorGUILayout.LabelField("Manual input", EditorStyles.boldLabel);
            bool write = EditorGUILayout.ToggleLeft(new GUIContent("Write manual values to the avatar", "Off: typed values are shown but not written."), FTEmulatorSettings.ManualOutput);
            if (write != FTEmulatorSettings.ManualOutput)
            {
                FTEmulatorSettings.ManualOutput = write;
                if (session != null) session.Options.ManualOutput = write;
            }
            EditorGUILayout.LabelField("Type values in the debug window's Hardware controls tab.", EditorStyles.wordWrappedMiniLabel);
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator window
// ============================================================================
//
// FTEmulatorWindow (this file)
//   CreateHosted()           The Main Menu's copy (hosted): drawn in the Tools tab's FTEmulator sub-tab, never shown.
//   Open()                   Tools > RiryTools > FTEmulator: the window of its own (never the hosted copy), in a scroll view.
//   OnInspectorUpdate()      Ten times a second: FTEmulatorSession.Snapshot (or FTEmulator.LastSnapshot), then Repaint.
//   Draw()                   State; Gesture Manager (an object field the user fills; the message of
//                            GestureManagerNotFoundException without Gesture Manager); Avatar and Rescan; Avatar backend
//                            (Auto, Native, the catalog's profiles) with its summary, Evidence and the Partial preview
//                            reasons; Input; Hardware limits; Behavior; View allowed channels; Start and Stop; Active
//                            listening; Freeze output; Open debug window; Reset overrides; then the OSC connection
//                            (port, Discovery, identity, tracking app, last payload, traffic, Restart connection, Copy
//                            diagnostics) or the Manual input's toggle.
//   Every choice is stored in FTEmulatorSettings and sent to the running session; the window keeps only the Gesture
//   Manager object, the Evidence foldout and its scroll position.
