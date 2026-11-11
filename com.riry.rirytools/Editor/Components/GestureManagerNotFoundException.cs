using System;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// For tools that call Gesture Manager when Gesture Manager 3.9 or newer is not
    /// installed. Its bridge assembly (Editor/VRChat/MenuTool/GestureManager) is then left
    /// out of the build instead of failing to compile, and the tools show this message instead.
    /// </summary>
    internal sealed class GestureManagerNotFoundException : InvalidOperationException
    {
        internal GestureManagerNotFoundException(string feature)
            : base("Gesture Manager not found. " + feature + " needs Gesture Manager 3.9 or newer " +
                "(vrchat.blackstartx.gesture-manager), which is not in this project.")
        {
        }
    }
}
