using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Riry.RiryTools.Editor
{
    /// <summary>Thrown by every Git operation when git is not installed or not on PATH.</summary>
    internal sealed class GitNotInstalledException : InvalidOperationException
    {
        internal const string DownloadUrl = "https://git-scm.com/install/";

        internal GitNotInstalledException(Exception inner)
            : base("Git is not installed or not on PATH. Install it from " + DownloadUrl +
                " and restart Unity.", inner)
        {
        }
    }
}
