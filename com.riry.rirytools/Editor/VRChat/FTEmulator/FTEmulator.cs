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

        // Set by Editor/VRChat/FTEmulator/GestureManager/FTEmulatorGestureManager.cs when Gesture Manager is in the project.
        internal static Func<IFTEmulatorOutput> CreateOutput;
        internal static Func<Object[]> FindManagers;
        internal static Type ManagerType;

        private struct TickStep { }

        private struct LateTickStep { }

        private static FTEmulatorCatalog catalog;
        private static FTEmulatorSession session;
        private static IFTEmulatorOutput output;
        private static Object manager;
        private static FTEmulatorNativeSetWeights setWeights;
        private static FTEmulatorSessionSnapshot lastSnapshot;
        private static string lastDiagnostics;
        // The player loop is only touched while the two steps are in it.
        private static bool stepsInstalled;

        static FTEmulator()
        {
            Register();
        }

        // Domain reload off: the static constructor does not run again, and a session could be left from the last Play Mode.
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(EnterPlayModeOptions options)
        {
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
        }

        internal static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) Shutdown("Play Mode ended");
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

        private static void LateTick()
        {
            FTEmulatorSession current = session;
            if (current == null) return;
            try
            {
                current.LateTick();
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
//   CreateOutput / FindManagers / ManagerType  Set by the Gesture Manager adapter; null without Gesture Manager.
//   Catalog / ReloadCatalog()  The profile data (FTEmulatorCatalog.LoadFromPackage); each problem is logged.
//   Start(target, out problem)  A session on the Gesture Manager object's avatar; only in Play Mode. Wires the scanner, the
//                            blendshape writer, the OSC receiver, the state behavior hook and the player loop steps.
//   Stop(reason) / Shutdown(reason)  Removes the steps, releases the hook, keeps LastSnapshot and LastDiagnostics, stops
//                            the session.
//   Session / Manager / LastSnapshot / LastDiagnostics
//   TickStep / LateTickStep  Markers of the two player loop steps: first in PreUpdate (FTEmulatorSession.Tick) and first in
//                            PostLateUpdate (LateTick). Tick binds again and rescans when Gesture Manager replaced its module.
//                            An exception in either is logged with its stack and stops the session.
