using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The FTEmulator window (logic in com.riry.ftemulator/Editor/FTEmulator/): the avatar, backend and hardware choices, the
    /// mode tabs (Playback, OSC, Manual: each picks the input), the Start, Active listening, Freeze output and Stop
    /// controls. It shows the session's snapshot
    /// and sends commands to FTEmulator and its session; it opens no socket, reads no avatar and works out no value
    /// itself. Opening or closing it starts and stops nothing: a session a user started keeps running without it, and is
    /// shown again when the window is reopened. Settings can be changed in Edit Mode; Start needs Play Mode.
    /// Tools > RiryTools > FTEmulator, and the Main Menu's FTEmulator tab. The tables are in
    /// FTEmulatorDebugWindow.
    /// </summary>
    internal sealed class FTEmulatorWindow : EditorWindow, IRiryHostedTool
    {
        internal const string Title = "FTEmulator";
        // In FTEmulatorMode's order.
        private static readonly string[] ModeNames = { "Playback", "OSC", "Manual" };
        private static readonly string[] BehaviorNames = { "Strict hardware", "Documented module behavior" };

        // The Main Menu's copy (CreateHosted): never shown on its own.
        [SerializeField] private bool hosted;
        // The avatar picked by the user; FTEmulatorAvatarFinder keeps its name to find it again after VRCFury's build.
        [SerializeField] private GameObject avatarObject;
        [NonSerialized] private string checkReport;
        [NonSerialized] private bool checkPassed;
        [NonSerialized] private string driverText;
        [NonSerialized] private bool gestureManagerRunning;
        [SerializeField] private bool showEvidence;
        [SerializeField] private Vector2 scroll;
        [NonSerialized] private string startProblem;
        [NonSerialized] private FTEmulatorSessionSnapshot snapshot;
        // The Main Menu while this window is hosted in its FTEmulator tab (IRiryHostedTool); null on its own.
        [NonSerialized] private EditorWindow surface;

        public EditorWindow Surface
        {
            set { surface = value; }
        }

        // Nothing is drawn outside IMGUI.
        public void NotDrawn()
        {
        }

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
            driverText = FTEmulator.DriverText();
            gestureManagerRunning = FTEmulator.GestureManagerRunning();
            // The picked avatar was replaced (VRCFury's build): show the one found again by name.
            if (avatarObject == null && EditorApplication.isPlaying && FTEmulatorAvatarFinder.HasChoice) avatarObject = FTEmulatorAvatarFinder.Find();
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
            DrawHardware(session, catalog);
            EditorGUILayout.Space(4f);
            DrawModes(session);
            EditorGUILayout.Space(6f);
            DrawControls(session, playing);
        }

        // The mode tabs, below View allowed channels. The tab picks the input: Playback and OSC use VRCFT OSC (a recording
        // plays through the OSC receiver), Manual the Manual input.
        private void DrawModes(FTEmulatorSession session)
        {
            int mode = (int)FTEmulatorSettings.Mode;
            int picked = RiryToolsMainMenu.TabStrip(surface != null ? surface : this, mode, ModeNames, hosted ? GUI.skin.verticalScrollbar.fixedWidth + 4f : 0f,
                RiryToolsMainMenu.ToolTabs);
            if (picked != mode)
            {
                FTEmulatorMode value = (FTEmulatorMode)picked;
                FTEmulatorSettings.Mode = value;
                FTEmulatorInput input = value == FTEmulatorMode.Manual ? FTEmulatorInput.Manual : FTEmulatorInput.VrcftOsc;
                FTEmulatorSettings.Input = input;
                if (session != null) session.SetInput(input);
            }
            EditorGUILayout.Space(4f);
            switch (FTEmulatorSettings.Mode)
            {
                case FTEmulatorMode.Playback: DrawRecording(session); break;
                case FTEmulatorMode.Osc: DrawOsc(session); break;
                default: DrawManual(session); break;
            }
        }

        private void DrawAvatar(FTEmulatorSession session, FTEmulatorCatalog catalog, bool playing)
        {
            // Always editable, in Edit or Play Mode, with or without a session.
            if (avatarObject == null && FTEmulatorAvatarFinder.Chosen != null) avatarObject = FTEmulatorAvatarFinder.Chosen;
            GameObject pickedAvatar = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Avatar",
                    "The avatar to drive. After VRCFury's build replaces it in Play Mode, it is found again by name."), avatarObject, typeof(GameObject), true);
            if (pickedAvatar != avatarObject)
            {
                avatarObject = pickedAvatar;
                FTEmulatorAvatarFinder.Choose(pickedAvatar);
            }
            if (avatarObject == null && FTEmulatorSettings.AvatarName.Length > 0)
                EditorGUILayout.LabelField(" ", "Looking for \"" + FTEmulatorSettings.AvatarName + "\"", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(new GUIContent("Driver", "Found automatically in the scene."), new GUIContent(driverText ?? (driverText = FTEmulator.DriverText())));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Driving", session != null && snapshot != null ? snapshot.Avatar : "None");
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

        private void DrawHardware(FTEmulatorSession session, FTEmulatorCatalog catalog)
        {
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
                using (new EditorGUI.DisabledScope(!playing || session != null || !FTEmulatorAvatarFinder.HasChoice))
                {
                    if (GUILayout.Button("Start", GUILayout.Height(26f)))
                    {
                        FTEmulator.StartChosen(out startProblem);
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
            if (playing && FTEmulatorSettings.Autoplay && !string.IsNullOrEmpty(FTEmulator.AutoplayProblem))
                EditorGUILayout.HelpBox("Autoplay: " + FTEmulator.AutoplayProblem, MessageType.Info);
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

        // The Playback tab: a companion recording (.mkv or .ftrec.jsonl) anywhere on this PC, played through the
        // receiver. Recording itself is the companion's job.
        private void DrawRecording(FTEmulatorSession session)
        {
            EditorGUILayout.LabelField("Recording playback", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string path = EditorGUILayout.DelayedTextField(new GUIContent("Recording", "A recording made by the companion: .mkv or .ftrec.jsonl, anywhere on this PC."),
                    FTEmulatorSettings.RecordingPath);
                if (path != FTEmulatorSettings.RecordingPath) SetRecordingPath(path);
                if (GUILayout.Button("Browse…", GUILayout.Width(70f)))
                {
                    string folder = string.IsNullOrEmpty(FTEmulatorSettings.RecordingPath) ? "" : System.IO.Path.GetDirectoryName(FTEmulatorSettings.RecordingPath);
                    string chosen = EditorUtility.OpenFilePanelWithFilters("Recording", folder, new[] { "Recordings", "mkv,jsonl", "All files", "*" });
                    if (!string.IsNullOrEmpty(chosen)) SetRecordingPath(chosen);
                    GUIUtility.ExitGUI();
                }
            }
            AnimationClip clip = FTEmulator.Animation;
            var pickedClip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Animation",
                "Plays with the recording, looping on its own length: put on the avatar each frame after Gesture Manager's animation. Needs a recording."),
                clip, typeof(AnimationClip), false);
            if (pickedClip != clip) FTEmulator.Animation = pickedClip;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(FTEmulatorSettings.RecordingPath)))
                {
                    if (GUILayout.Button(new GUIContent("Check validity", "Reads the whole recording and decodes every packet, without playing it."), GUILayout.Height(22f)))
                        checkPassed = FTEmulatorRecording.Check(FTEmulatorSettings.RecordingPath, out checkReport);
                }
            }
            if (!string.IsNullOrEmpty(checkReport)) EditorGUILayout.HelpBox(checkReport, checkPassed ? MessageType.Info : MessageType.Error);
            if (FTEmulatorRecording.Loaded) EditorGUILayout.LabelField(FTEmulatorRecording.Playing ? "Playing" : "Loaded", FTEmulatorRecording.Status);
            // Play/Pause and Stop only in Play Mode with Gesture Manager running an avatar; Loop, Autoplay and Speed always.
            bool ready = EditorApplication.isPlaying && gestureManagerRunning;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!ready || session == null || string.IsNullOrWhiteSpace(FTEmulatorSettings.RecordingPath)))
                {
                    if (!FTEmulatorRecording.Playing)
                    {
                        if (GUILayout.Button("Play", GUILayout.Height(22f))) PlayRecording();
                    }
                    else if (GUILayout.Button("Pause", GUILayout.Height(22f))) FTEmulatorRecording.Pause();
                }
                using (new EditorGUI.DisabledScope(!ready || !FTEmulatorRecording.Loaded))
                {
                    if (GUILayout.Button(new GUIContent("Stop", "Stops and rewinds."), GUILayout.Height(22f))) FTEmulatorRecording.Stop();
                }
                bool loop = GUILayout.Toggle(FTEmulatorSettings.Loop, new GUIContent("Loop", "On: playback starts again from the beginning when the recording ends."),
                    "Button", GUILayout.Height(22f));
                if (loop != FTEmulatorSettings.Loop) FTEmulatorSettings.Loop = loop;
                bool autoplay = EditorGUILayout.ToggleLeft(new GUIContent("Autoplay", "On: entering Play Mode starts FTEmulator on the avatar and plays the recording."),
                    FTEmulatorSettings.Autoplay, GUILayout.Width(80f));
                if (autoplay != FTEmulatorSettings.Autoplay) FTEmulatorSettings.Autoplay = autoplay;
            }
            if (FTEmulatorSettings.Autoplay) EditorGUILayout.LabelField("Plays from the start each time Play Mode starts.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Speed", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                foreach (float rate in FTEmulatorRecording.Speeds)
                {
                    bool on = Mathf.Approximately(FTEmulatorRecording.Speed, rate);
                    if (GUILayout.Toggle(on, rate.ToString("0.##", CultureInfo.InvariantCulture) + "×", "Button") && !on) FTEmulatorRecording.Speed = rate;
                }
            }
            if (session == null) EditorGUILayout.LabelField("Start FTEmulator in Play Mode to play.", EditorStyles.wordWrappedMiniLabel);
        }

        private void SetRecordingPath(string path)
        {
            FTEmulatorSettings.RecordingPath = path.Trim().Trim('"');
            checkReport = null;
        }

        // Loads (only when changed) and plays from the playhead.
        private void PlayRecording()
        {
            try
            {
                FTEmulatorRecording.Load(FTEmulatorSettings.RecordingPath);
                FTEmulatorRecording.Play();
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is System.IO.InvalidDataException ||
                                              exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                checkPassed = false;
                checkReport = exception.Message;
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
//   CreateHosted()           The Main Menu's copy (hosted): drawn in the Main Menu's FTEmulator tab, never shown.
//   Open()                   Tools > RiryTools > FTEmulator: the window of its own (never the hosted copy), in a scroll view.
//   OnInspectorUpdate()      Ten times a second: FTEmulatorSession.Snapshot (or FTEmulator.LastSnapshot), then Repaint.
//   Draw()                   State; Avatar (a GameObject field, always editable; FTEmulatorAvatarFinder.Choose keeps its
//                            name and path to find it again after VRCFury's build); Driver (FTEmulator.DriverText, found
//                            automatically); Driving and Rescan; Avatar backend with its summary, Evidence and Partial
//                            preview reasons; Input; Hardware limits; Behavior; View allowed channels; Start
//                            (FTEmulator.StartChosen) and Stop; the Autoplay problem; Active listening; Freeze output; Open
//                            debug window; Reset overrides; then the OSC connection and Recording playback, or the Manual
//                            input's toggle.
//   DrawRecording            Recording (path + Browse…, any folder; .mkv or .ftrec.jsonl), Check validity
//                            (FTEmulatorRecording.Check, result shown), Autoplay toggle; without Autoplay: Play/Pause,
//                            Stop (rewind) and Speed 0.25–2×. No recording is made in Unity.
//   Every choice is stored in FTEmulatorSettings and sent to the running session; the window keeps only the
//   avatar field, the Check validity result, the Evidence foldout and its scroll position.
