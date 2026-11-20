using UnityEditor;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's menu entry and its place in the RiryTools Main Menu. FTEmulator is its own package; RiryTools keeps
    /// the menu path (RiryToolsMenuBar.FTEmulatorTools) and, without this package or the VRChat SDK, an entry at the same
    /// path that says why FTEmulator is not available. Keep the priorities of both in step.
    /// </summary>
    [InitializeOnLoad]
    internal static class FTEmulatorMenus
    {
        // The Main Menu's FTEmulator tab hosts the FTEmulator window.
        static FTEmulatorMenus()
        {
            RiryToolsMainMenu.CreateFTEmulator = FTEmulatorWindow.CreateHosted;
        }

        [MenuItem(RiryToolsMenuBar.FTEmulatorTools, false, 2067)]
        private static void FTEmulatorToolsItem() { FTEmulatorWindow.Open(); }


        [MenuItem(RiryToolsMenuBar.FTEmulatorTools, true)]

        private static bool FTEmulatorToolsValid() { return !StopAllToolScripts.Active; }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator menus
// ============================================================================
//
// FTEmulatorMenus (this file)
//   (static constructor)     RiryToolsMainMenu.CreateFTEmulator = FTEmulatorWindow.CreateHosted: the Main Menu's
//                            FTEmulator tab hosts FTEmulator.
//   Tools/RiryTools/FTEmulator (2067)  FTEmulatorWindow.Open; greyed out while Stop all tool scripts is on. No
//                            right-click entry. Without this package or the VRChat SDK, RiryToolsMenuBar has the same
//                            path, saying why (RiryToolsMenuBar.FTEmulatorUnavailable).
