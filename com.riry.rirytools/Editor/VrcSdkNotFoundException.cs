#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// Thrown by VRChat tools when the VRChat Avatars SDK 3.7.0 or newer is not
    /// installed. Their own assembly is then left out of the build instead of
    /// failing to compile, and their menu entries throw this instead.
    /// </summary>
    internal sealed class VrcSdkNotFoundException : InvalidOperationException
    {
        internal VrcSdkNotFoundException(string tool)
            : base("VRCSDK not found. " + tool + " needs the VRChat Avatars SDK 3.7.0 or newer " +
                "(com.vrchat.avatars), which is not in this project.")
        {
        }
    }
}
#endif
