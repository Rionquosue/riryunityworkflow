using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Unity 2022.3's AnimationWindow does not opt in to the public Overlay API.
    /// Add one lightweight UI Toolkit button over its existing IMGUI toolbar.
    /// Only the focused editor window is inspected, twice per second, so this
    /// also supports Animation windows opened after the script is compiled.
    /// </summary>
    [InitializeOnLoad]
    internal static class CleanAnimationKeysButton
    {
        private const string ButtonElementName =
            "rirytools-clean-animation-keys";
        private const double ScanIntervalSeconds = 0.5;
        // Where the button sits: the hierarchy pane's bottom bar, past the Dopesheet and Curves buttons.
        private const float ButtonLeft = 170.0f;
        private const float ButtonBottom = 1.0f;

        private static readonly object ButtonOwner = new object();

        private static double nextScanTime;

        static CleanAnimationKeysButton()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            EditorApplication.update += WatchForAnimationWindows;
        }

        private static void WatchForAnimationWindows()
        {
            if (EditorApplication.timeSinceStartup < nextScanTime)
            {
                return;
            }

            nextScanTime = EditorApplication.timeSinceStartup +
                           ScanIntervalSeconds;

            AnimationWindow animationWindow =
                EditorWindow.focusedWindow as AnimationWindow;

            if (animationWindow != null)
            {
                InstallButton(animationWindow);
            }
        }

        private static void InstallButton(AnimationWindow animationWindow)
        {
            if (animationWindow == null ||
                animationWindow.rootVisualElement == null)
            {
                return;
            }

            VisualElement root = animationWindow.rootVisualElement;
            Button existing = root.Q<Button>(ButtonElementName);

            if (existing != null)
            {
                if (object.ReferenceEquals(existing.userData, ButtonOwner))
                {
                    return;
                }

                existing.RemoveFromHierarchy();
            }

            Button button = new Button(delegate
            {
                CleanAnimationKeysWindow.Open(new[] { animationWindow.animationClip });
            });

            button.name = ButtonElementName;
            button.userData = ButtonOwner;
            button.text = "Clean Keys";
            button.tooltip =
                "Open Clean Animation Keys with this Animation window's active AnimationClip.";

            // The AnimationWindow toolbar is IMGUI in Unity 2022.3, so the
            // public rootVisualElement is the accessible attachment point.
            // Absolute positioning keeps this button out of the window layout. It sits in the
            // bottom bar of the hierarchy pane, right of the Dopesheet and Curves buttons, where
            // nothing is drawn; the top right would cover the time ruler and events strip.
            button.style.position = Position.Absolute;
            button.style.bottom = ButtonBottom;
            button.style.left = ButtonLeft;
            button.style.width = 78.0f;
            button.style.height = 18.0f;

            root.Add(button);
        }

        private static void BeforeAssemblyReload()
        {
            EditorApplication.update -= WatchForAnimationWindows;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        }
    }
}

// ============================================================================
// GLOSSARY: Clean Animation Keys button in the Animation window
// ============================================================================
//
// CleanAnimationKeysButton (this file): the "Clean Keys" button
//   ButtonElementName        const string    UI Toolkit name, used to find an installed button.
//   ScanIntervalSeconds      const double    How often the focused window is checked (0.5 s).
//   ButtonLeft / ButtonBottom  const float   170 / 1: the button in the hierarchy pane's bottom bar, right of Dopesheet
//                                            and Curves (not over the time ruler).
//   WatchForAnimationWindows()  Installs the button on a focused Animation window.
//   InstallButton(window)    Adds the button, which opens the window with that window's clip.
//
