using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using Object = UnityEngine.Object;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's entry point: a face tracking emulator for Play Mode, paired with Gesture Manager. It owns the editor
    /// lifecycle hooks and at most one session. Loading scripts only registers the hooks: nothing is opened until Start
    /// is called, and Start refuses outside Play Mode. Leaving Play Mode, a script reload and closing the Editor stop the
    /// session, so no receiver, service or avatar write outlives Play Mode. With domain reload off the statics survive
    /// into the next Play Mode, so entering it stops anything left and registers the hooks again, once.
    /// The session runs from two steps added to Unity's player loop for as long as it lives: one at the start of
    /// PreUpdate (parameter writes, before animation) and one at the start of PostLateUpdate (blendshape weights, after
    /// animation and before the skinned meshes update). Neither runs while Unity is paused.
    /// </summary>
    [InitializeOnLoad]
    internal static class FTEmulator
    {
        internal const string DisplayName = "FTEmulator";

        // Set by com.riry.ftemulator/Editor/FTEmulator/GestureManager/FTEmulatorGestureManager.cs when Gesture Manager is in the project.
        internal static Func<IFTEmulatorOutput> CreateOutput;
        internal static Func<Object[]> FindManagers;
        internal static Type ManagerType;
        internal static Func<Object, GameObject> ManagerAvatar;

        // Autoplay retries Start this often until Gesture Manager has the avatar loaded.
        private const double AutoplayRetry = 0.5;
        private static double nextAutoplay;
        private static bool autoplayDone;
        private static string autoplayProblem;

        private struct TickStep { }

        private struct LateTickStep { }

        private static FTEmulatorCatalog catalog;
        private static FTEmulatorSession session;
        private static IFTEmulatorOutput output;
        private static Object manager;
        private static FTEmulatorNativeSetWeights setWeights;
        private static FTEmulatorSessionSnapshot lastSnapshot;
        // The animation played with the recording, read again when FTEmulatorSettings.AnimationId changes.
        private static AnimationClip animation;
        private static string animationId;
        private static string lastDiagnostics;
        // The player loop is only touched while the two steps are in it.
        private static bool stepsInstalled;

        static FTEmulator()
        {
            if (StopAllToolScripts.Active) return;
            Register();
        }

        // Domain reload off: the static constructor does not run again, and a session could be left from the last Play Mode.
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(EnterPlayModeOptions options)
        {
            if (StopAllToolScripts.Active) return;
            Shutdown("Play Mode restarted");
            Register();
        }

        /// <summary>Registers the lifecycle hooks, once however often it is called. Opens nothing.</summary>
        internal static void Register()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.pauseStateChanged -= OnPauseChanged;
            EditorApplication.pauseStateChanged += OnPauseChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.quitting += OnQuitting;
            EditorApplication.update -= AutoplayUpdate;
            EditorApplication.update += AutoplayUpdate;
        }

        internal static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) Shutdown("Play Mode ended");
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                autoplayDone = false;
                autoplayProblem = null;
                nextAutoplay = 0;
            }
        }

        private static void OnPauseChanged(PauseState state)
        {
            if (session != null) session.SetPaused(state == PauseState.Paused);
        }

        private static void OnBeforeReload()
        {
            Shutdown("scripts reloading");
        }

        private static void OnQuitting()
        {
            Shutdown("Editor closing");
        }

        /// <summary>The profile data, read from the package the first time it is asked for.</summary>
        internal static FTEmulatorCatalog Catalog
        {
            get
            {
                if (catalog == null) ReloadCatalog();
                return catalog;
            }
        }

        /// <summary>Reads the profile data again. A running session keeps the catalog it started with.</summary>
        internal static void ReloadCatalog()
        {
            catalog = FTEmulatorCatalog.LoadFromPackage();
            foreach (string problem in catalog.Problems) RiryLog.Warn(RiryLog.FTEmulator, "Profile data: " + problem);
        }

        /// <summary>The running session, or null.</summary>
        internal static FTEmulatorSession Session
        {
            get { return session != null && session.Running ? session : null; }
        }

        /// <summary>The Gesture Manager object the running session is bound to.</summary>
        internal static Object Manager
        {
            get { return manager; }
        }

        /// <summary>What the last session showed when it stopped: still viewable, no longer applied.</summary>
        internal static FTEmulatorSessionSnapshot LastSnapshot
        {
            get { return lastSnapshot; }
        }

        internal static string LastDiagnostics
        {
            get { return lastDiagnostics; }
        }

        /// <summary>The AnimationClip chosen to play with the recording (FTEmulatorSettings.AnimationId), or null.</summary>
        internal static AnimationClip Animation
        {
            get
            {
                string id = FTEmulatorSettings.AnimationId;
                if (id == animationId && (animation != null || id.Length == 0)) return animation;
                animationId = id;
                animation = id.Length > 0 && GlobalObjectId.TryParse(id, out GlobalObjectId parsed)
                    ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as AnimationClip : null;
                return animation;
            }
            set
            {
                FTEmulatorSettings.AnimationId = value != null ? GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() : "";
                animationId = FTEmulatorSettings.AnimationId;
                animation = value;
            }
        }

        /// <summary>Why Autoplay has not started yet, or null.</summary>
        internal static string AutoplayProblem
        {
            get { return autoplayProblem; }
        }

        private const string Av3Unsupported = "Avatar 3.0 Emulator was found, but FTEmulator can only drive avatars through Gesture Manager so far.";

        /// <summary>Which avatar driver is in the scene, for the window.</summary>
        internal static string DriverText()
        {
            if (CreateOutput != null && FindManagers != null)
            {
                Object[] managers = FindManagers();
                if (managers.Length > 0) return "Gesture Manager (found" + (managers.Length > 1 ? ", " + managers.Length : "") + ")";
                if (Av3EmulatorPresent()) return "Avatar 3.0 Emulator (found, not supported yet)";
                return "Gesture Manager installed, none in the scene yet";
            }
            return Av3EmulatorPresent() ? "Avatar 3.0 Emulator (found, not supported yet)" : "None found";
        }

        /// <summary>True in Play Mode when a Gesture Manager in the scene runs an avatar.</summary>
        internal static bool GestureManagerRunning()
        {
            if (!EditorApplication.isPlaying || FindManagers == null || ManagerAvatar == null) return false;
            foreach (Object found in FindManagers())
            {
                if (ManagerAvatar(found) != null) return true;
            }
            return false;
        }

        // Avatar 3.0 Emulator is only detected, by its runtime component's type name: FTEmulator cannot write its parameters.
        private static bool Av3EmulatorPresent()
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == "LyumaAv3Runtime") return true;
            }
            return false;
        }

        /// <summary>
        /// The Gesture Manager that runs avatar (or, when there is only one, that one when it runs it). Null, with the
        /// reason, when none does.
        /// </summary>
        internal static Object FindManagerFor(GameObject avatar, out string problem)
        {
            problem = null;
            if (CreateOutput == null || FindManagers == null)
            {
                problem = Av3EmulatorPresent() ? Av3Unsupported : new GestureManagerNotFoundException(DisplayName).Message;
                return null;
            }
            Object[] managers = FindManagers();
            if (managers.Length == 0)
            {
                problem = Av3EmulatorPresent() ? Av3Unsupported : "No Gesture Manager in the scene yet.";
                return null;
            }
            foreach (Object found in managers)
            {
                GameObject running = ManagerAvatar != null ? ManagerAvatar(found) : null;
                if (running != null && FTEmulatorAvatarFinder.Same(running, avatar)) return found;
            }
            if (managers.Length == 1)
            {
                GameObject running = ManagerAvatar != null ? ManagerAvatar(managers[0]) : null;
                problem = running == null ? "Gesture Manager has not loaded an avatar yet."
                    : "Gesture Manager runs " + running.name + ", not " + avatar.name + ". Select " + avatar.name + " in Gesture Manager.";
                return null;
            }
            problem = "No Gesture Manager runs " + avatar.name + ".";
            return null;
        }

        /// <summary>Starts a session on the chosen avatar (FTEmulatorAvatarFinder), through the Gesture Manager running it.</summary>
        internal static bool StartChosen(out string problem)
        {
            if (!EditorApplication.isPlaying)
            {
                problem = "Enter Play Mode to start tracking.";
                return false;
            }
            if (!FTEmulatorAvatarFinder.HasChoice)
            {
                problem = "Choose the avatar.";
                return false;
            }
            GameObject avatar = FTEmulatorAvatarFinder.Find();
            if (avatar == null)
            {
                problem = "The avatar \"" + FTEmulatorSettings.AvatarName + "\" was not found in the scene. Choose it again.";
                return false;
            }
            Object found = FindManagerFor(avatar, out problem);
            return found != null && Start(found, out problem);
        }

        // Autoplay: in Play Mode, start a session and play the recording, retrying until Gesture Manager is ready. Once
        // per Play Mode, so a Stop pressed afterwards is respected.
        private static void AutoplayUpdate()
        {
            if (StopAllToolScripts.Active || !EditorApplication.isPlaying || autoplayDone || !FTEmulatorSettings.Autoplay) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < nextAutoplay) return;
            nextAutoplay = now + AutoplayRetry;
            if (Session == null && !StartChosen(out autoplayProblem)) return;
            autoplayDone = true;
            autoplayProblem = null;
            if (FTEmulatorSettings.Input != FTEmulatorInput.VrcftOsc)
            {
                autoplayProblem = "Autoplay needs the VRCFT OSC input: recordings play through the OSC receiver.";
                return;
            }
            try
            {
                FTEmulatorRecording.Load(FTEmulatorSettings.RecordingPath);
                FTEmulatorRecording.Stop();
                FTEmulatorRecording.Play();
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is System.IO.InvalidDataException ||
                                              exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                autoplayProblem = "Recording not played: " + exception.Message;
                RiryLog.Warn(RiryLog.FTEmulator, autoplayProblem);
            }
        }

        /// <summary>
        /// Starts a session on the avatar the given Gesture Manager runs. False, with the reason, outside Play Mode,
        /// without Gesture Manager, or when that Gesture Manager has no avatar.
        /// </summary>
        internal static bool Start(Object target, out string problem)
        {
            if (!EditorApplication.isPlaying)
            {
                problem = "Enter Play Mode to start tracking.";
                return false;
            }
            if (CreateOutput == null)
            {
                problem = new GestureManagerNotFoundException(DisplayName).Message;
                return false;
            }
            Shutdown("restarted");
            IFTEmulatorOutput created = CreateOutput();
            if (!created.Bind(target, out problem)) return false;
            FTEmulatorCatalog data = Catalog;
            var diagnostics = new FTEmulatorDiagnostics();
            // Each kind of problem reaches the Console once; the counts are in the debug window.
            diagnostics.FirstSeen = (code, context) => RiryLog.Warn(RiryLog.FTEmulator, code + ": " + context);
            var started = new FTEmulatorSession(data, FTEmulatorSettings.Capture(), created,
                () => FTEmulatorAvatarScanner.Scan(created.Avatar, data, created.Parameters),
                snapshot => new FTEmulatorBlendshapeWriter(snapshot.NativeMeshes),
                (options, record) => new FTEmulatorOscReceiver(options.Port, data.Keys.Length, record),
                () => EditorApplication.isPlaying, diagnostics);
            GameObject avatar = created.Avatar;
            started.Info = message => RiryLog.Info(RiryLog.FTEmulator, message, avatar);
            if (!started.Start(out problem))
            {
                created.Release();
                return false;
            }
            Versions(diagnostics, created);
            session = started;
            output = created;
            manager = target;
            BindSetWeights();
            InstallSteps();
            return true;
        }

        private static void BindSetWeights()
        {
            if (setWeights != null) setWeights.Release();
            IFTEmulatorOutput bound = output;
            FTEmulatorSession current = session;
            setWeights = new FTEmulatorNativeSetWeights(bound.Avatar, name => bound.TryRead(name, out float value) ? value : (float?)null, current.RequestSetWeight);
            setWeights.Bind();
        }

        // The versions a diagnostics export needs to be read later.
        private static void Versions(FTEmulatorDiagnostics diagnostics, IFTEmulatorOutput created)
        {
            diagnostics.Set("Unity", Application.unityVersion);
            diagnostics.Set("Output", created.Name);
            var wanted = new HashSet<string>
            {
                "com.riry.rirytools", "com.vrchat.avatars", "com.vrchat.base", "vrchat.blackstartx.gesture-manager", "com.vrcfury.vrcfury",
                "nadena.dev.modular-avatar", "net.pawlygon.vrc-facetracking", "adjerry91.vrcft.templates"
            };
            foreach (UnityEditor.PackageManager.PackageInfo package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (wanted.Contains(package.name)) diagnostics.Set("Package " + package.name, package.version);
            }
        }

        /// <summary>Stops the session, if one runs.</summary>
        internal static void Stop(string reason)
        {
            Shutdown(reason);
        }

        /// <summary>Stops everything this type started: the player loop steps, the state behavior hook and the session. Safe to call at any time.</summary>
        internal static void Shutdown(string reason)
        {
            RemoveSteps();
            if (setWeights != null) setWeights.Release();
            setWeights = null;
            FTEmulatorSession stopping = session;
            session = null;
            output = null;
            manager = null;
            if (stopping == null) return;
            try
            {
                lastSnapshot = stopping.Snapshot();
                lastSnapshot.State = FTEmulatorState.Stopped;
                lastSnapshot.StateText = "Stopped (" + reason + ") · last values shown, nothing is applied";
                lastDiagnostics = stopping.Diagnostics.ExportText(null);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.FTEmulator, "Could not keep the last session's snapshot", exception);
            }
            try
            {
                stopping.Stop(reason);
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.FTEmulator, "Stop failed", exception);
            }
        }

        // ---- The player loop steps ----

        private static void InstallSteps()
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            Insert(ref loop, typeof(PreUpdate), typeof(TickStep), Tick);
            Insert(ref loop, typeof(PostLateUpdate), typeof(LateTickStep), LateTick);
            PlayerLoop.SetPlayerLoop(loop);
            stepsInstalled = true;
        }

        private static void RemoveSteps()
        {
            if (!stepsInstalled) return;
            stepsInstalled = false;
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            bool changed = Insert(ref loop, typeof(PreUpdate), typeof(TickStep), null);
            changed |= Insert(ref loop, typeof(PostLateUpdate), typeof(LateTickStep), null);
            if (changed) PlayerLoop.SetPlayerLoop(loop);
        }

        // Puts step first in phase, replacing an earlier one of the same marker; with no step it only takes the marker out.
        // Returns whether the loop was changed.
        private static bool Insert(ref PlayerLoopSystem loop, Type phase, Type marker, PlayerLoopSystem.UpdateFunction step)
        {
            if (loop.subSystemList == null) return false;
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != phase) continue;
                var steps = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList ?? new PlayerLoopSystem[0]);
                int removed = steps.RemoveAll(item => item.type == marker);
                if (step != null) steps.Insert(0, new PlayerLoopSystem { type = marker, updateDelegate = step });
                else if (removed == 0) return false;
                loop.subSystemList[i].subSystemList = steps.ToArray();
                return true;
            }
            return false;
        }

        private static void Tick()
        {
            FTEmulatorSession current = session;
            if (current == null) return;
            try
            {
                // Gesture Manager replaced its module (another avatar, a rebuilt graph): bind to the new one and read it.
                // VRCFury or Gesture Manager replaced the avatar: find the chosen avatar's Gesture Manager again first.
                if (output != null && !output.Valid && FTEmulatorAvatarFinder.HasChoice)
                {
                    GameObject avatar = FTEmulatorAvatarFinder.Find();
                    Object found = avatar != null ? FindManagerFor(avatar, out string _) : null;
                    if (found != null) manager = found;
                }
                if (output != null && !output.Valid && manager != null && output.Bind(manager, out string _))
                {
                    current.Rescan();
                    BindSetWeights();
                }
                current.Tick(FTEmulatorClock.Now);
                if (!current.Running) Shutdown("session ended");
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.FTEmulator, "Tracking stopped after an error", exception);
                Shutdown("an error");
            }
        }

        // The animation chosen to play with the recording, sampled onto the avatar after Gesture Manager's animation: only
        // while a recording plays or is paused part way, at the recording's time on the clip's own loop.
        private static void SampleAnimation()
        {
            if (output == null || !output.Valid || !FTEmulatorRecording.Loaded) return;
            if (!FTEmulatorRecording.Playing && FTEmulatorRecording.Playhead <= 0) return;
            AnimationClip clip = Animation;
            GameObject avatar = output.Avatar;
            if (clip == null || avatar == null) return;
            float time = clip.length > 0f ? (float)(FTEmulatorRecording.Playhead % clip.length) : 0f;
            clip.SampleAnimation(avatar, time);
        }

        private static void LateTick()
        {
            FTEmulatorSession current = session;
            if (current == null) return;
            try
            {
                current.LateTick();
                SampleAnimation();
            }
            catch (Exception exception)
            {
                RiryLog.Exception(RiryLog.FTEmulator, "Tracking stopped after an error", exception);
                Shutdown("an error");
            }
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator (entry point and lifecycle)
// ============================================================================
//
// FTEmulator (this file)
//   static constructor       [InitializeOnLoad]: Register only. No device, socket or service is opened here.
//   Register()               Adds each lifecycle handler once (removing it first): playModeStateChanged, pauseStateChanged,
//                            AssemblyReloadEvents.beforeAssemblyReload, EditorApplication.quitting.
//   OnEnterPlayMode          [InitializeOnEnterPlayMode]: with domain reload off, stops what is left and registers again.
//   OnPlayModeChanged(state) ExitingPlayMode and EnteredEditMode stop the session.
//   CreateOutput / FindManagers / ManagerType / ManagerAvatar  Set by the Gesture Manager adapter; null without it.
//   DriverText()             Which driver is in the scene: Gesture Manager, or Avatar 3.0 Emulator (detected by its
//                            LyumaAv3Runtime type name only; it cannot be driven yet).
//   FindManagerFor(avatar)   The Gesture Manager whose running avatar is FTEmulatorAvatarFinder.Same as avatar; otherwise
//                            null with the reason (none, none loaded, another avatar, Avatar 3.0 Emulator only).
//   StartChosen(out problem) Start on the chosen avatar's Gesture Manager: what the window's Start and Autoplay call.
//   AutoplayUpdate           EditorApplication.update: with Autoplay on, in Play Mode, StartChosen every 0.5 s until it
//                            works, then Load, rewind and Play the recording (VRCFT OSC input only). Once per Play Mode;
//                            AutoplayProblem holds why it has not started.
//   Catalog / ReloadCatalog()  The profile data (FTEmulatorCatalog.LoadFromPackage); each problem is logged.
//   Start(target, out problem)  A session on the Gesture Manager object's avatar; only in Play Mode. Wires the scanner, the
//                            blendshape writer, the OSC receiver, the state behavior hook and the player loop steps.
//   Stop(reason) / Shutdown(reason)  Removes the steps, releases the hook, keeps LastSnapshot and LastDiagnostics, stops
//                            the session.
//   Session / Manager / LastSnapshot / LastDiagnostics
//   TickStep / LateTickStep  Markers of the two player loop steps: first in PreUpdate (FTEmulatorSession.Tick) and first in
//                            PostLateUpdate (LateTick). Tick finds the chosen avatar's Gesture Manager again, then binds
//                            and rescans, when Gesture Manager or VRCFury replaced the avatar or its module.
//                            An exception in either is logged with its stack and stops the session.
