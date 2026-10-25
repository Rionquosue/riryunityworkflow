#if UNITY_EDITOR
// Target editor: Unity 2022.3 LTS (including 2022.3.22f1).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Riry.UnityWorkflow.Editor
{
    /// <summary>Thrown by every Git operation when git is not installed or not on PATH.</summary>
    internal sealed class GitNotInstalledException : InvalidOperationException
    {
        internal const string DownloadUrl = "https://git-scm.com/install/";

        internal GitNotInstalledException(Exception inner)
            : base("Git is not installed or not on PATH. Install it from " + DownloadUrl +
                " and restart Unity. See GIT > Help.", inner)
        {
        }
    }

    /// <summary>
    /// Runs git without a console window or credential prompts. Safe to call
    /// from a worker thread: it uses no Unity API.
    /// </summary>
    internal static class GitRunner
    {
        internal sealed class Result
        {
            public int ExitCode;
            public string Output = "";
            public string Error = "";

            public bool Succeeded
            {
                get { return ExitCode == 0; }
            }

            public string Describe(string action)
            {
                string detail = (Error.Trim().Length > 0 ? Error : Output).Trim();
                return action + " failed (git exit code " + ExitCode + ")" + (detail.Length > 0 ? ":\n" + detail : ".");
            }
        }

        internal const int TimedOut = -1;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        internal static Result Run(string workingDirectory, IDictionary<string, string> environment, int timeoutMs,
            params string[] arguments)
        {
            return Execute(workingDirectory, environment, arguments, null, null, null, timeoutMs);
        }

        /// <summary>Runs git with <paramref name="input"/> written to its standard input.</summary>
        internal static Result RunWithInput(string workingDirectory, IDictionary<string, string> environment,
            byte[] input, int timeoutMs, params string[] arguments)
        {
            return Execute(workingDirectory, environment, arguments, input, null, null, timeoutMs);
        }

        /// <summary>
        /// Runs git and hands each output line to <paramref name="onLine"/> on the
        /// calling thread. Returning true from <paramref name="cancelled"/> stops git.
        /// </summary>
        internal static Result Stream(string workingDirectory, string[] arguments, Action<string> onLine,
            Func<bool> cancelled)
        {
            return Execute(workingDirectory, null, arguments, null, onLine, cancelled, int.MaxValue);
        }

        private static Result Execute(string workingDirectory, IDictionary<string, string> environment,
            string[] arguments, byte[] input, Action<string> onLine, Func<bool> cancelled, int timeoutMs)
        {
            var all = new List<string> { "-c", "core.quotepath=off" };
            all.AddRange(arguments);
            var info = new ProcessStartInfo("git", BetterFbxExporter.JoinArguments(all))
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = input != null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Utf8,
                StandardErrorEncoding = Utf8
            };
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            info.EnvironmentVariables["GCM_INTERACTIVE"] = "never";
            if (environment != null)
            {
                foreach (KeyValuePair<string, string> variable in environment)
                    info.EnvironmentVariables[variable.Key] = variable.Value;
            }

            var result = new Result();
            using (var process = new Process { StartInfo = info })
            {
                var error = new StringBuilder();
                var lines = new ConcurrentQueue<string>();
                process.ErrorDataReceived += (sender, line) => { if (line.Data != null) lock (error) error.AppendLine(line.Data); };
                if (onLine != null)
                    process.OutputDataReceived += (sender, line) => { if (line.Data != null) lines.Enqueue(line.Data); };
                try
                {
                    process.Start();
                }
                catch (Win32Exception exception)
                {
                    throw new GitNotInstalledException(exception);
                }

                process.BeginErrorReadLine();
                // Raw output keeps NUL-separated (-z) listings intact.
                Task<string> raw = null;
                if (onLine != null) process.BeginOutputReadLine();
                else raw = process.StandardOutput.ReadToEndAsync();
                if (input != null)
                {
                    // A git that exits early closes the pipe; its exit code reports why.
                    try
                    {
                        process.StandardInput.BaseStream.Write(input, 0, input.Length);
                        process.StandardInput.Close();
                    }
                    catch (IOException)
                    {
                    }
                }

                var timer = Stopwatch.StartNew();
                while (!process.WaitForExit(100))
                {
                    Drain(lines, onLine);
                    bool stop = cancelled != null && cancelled();
                    if (!stop && timer.ElapsedMilliseconds <= timeoutMs) continue;
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    process.WaitForExit();
                    result.ExitCode = TimedOut;
                    result.Error = stop ? "Cancelled." : "Timed out after " + timeoutMs / 1000 + " s.";
                    return result;
                }
                process.WaitForExit();
                Drain(lines, onLine);
                result.ExitCode = process.ExitCode;
                if (raw != null) result.Output = raw.Result;
                lock (error) result.Error = error.ToString();
            }
            return result;
        }

        private static void Drain(ConcurrentQueue<string> lines, Action<string> onLine)
        {
            string line;
            while (onLine != null && lines.TryDequeue(out line)) onLine(line);
        }
    }
}
#endif
