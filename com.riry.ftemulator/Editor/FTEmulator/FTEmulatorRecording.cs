using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's recording player. Recordings are made by the companion (VRCFT-FtEmuCompanion), never in Unity: a
    /// .ftrec.jsonl file, or an .mkv with one attached (read out through the ffmpeg on PATH). Each OSC packet, avatar
    /// parameter and viseme in it is fed to the running receiver at its recorded time, scaled by Speed, as if the
    /// tracking app sent it. At the end it stops, or with Loop on (FTEmulatorSettings.Loop) starts again from the
    /// beginning. Expression frames (module recordings) are skipped: the module plays those itself.
    /// Files are read as untrusted data.
    /// </summary>
    internal static class FTEmulatorRecording
    {
        internal const int MaxPackets = 2000000;
        internal static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 1.5f, 2f };
        private const string Format = "RiryTools.FTRecorder";
        private const string ParameterPrefix = "/avatar/parameters/";

        private struct Packet
        {
            internal double Time;
            internal byte[] Data;
        }

        private static List<Packet> packets;
        private static string loadedPath;
        private static DateTime loadedWrite;
        private static int index;
        private static double playhead;
        private static double lastNow = -1;
        private static bool playing;
        private static float speed = 1f;

        internal static bool Loaded
        {
            get { return packets != null; }
        }

        internal static bool Playing
        {
            get { return playing; }
        }

        internal static float Speed
        {
            get { return speed; }
            set { speed = Math.Min(Math.Max(value, 0.05f), 8f); }
        }

        internal static int Skipped { get; private set; }

        /// <summary>Seconds into the recording (0 when stopped or rewound).</summary>
        internal static double Playhead
        {
            get { return playhead; }
        }

        internal static double Length
        {
            get { return packets != null && packets.Count > 0 ? packets[packets.Count - 1].Time : 0; }
        }

        /// <summary>"name · 12.3 / 60.0 s · n packets", or null when nothing is loaded.</summary>
        internal static string Status
        {
            get
            {
                if (packets == null) return null;
                return Path.GetFileName(loadedPath) + " · " + playhead.ToString("0.0", CultureInfo.InvariantCulture) + " / " +
                    Length.ToString("0.0", CultureInfo.InvariantCulture) + " s · " + packets.Count + " packet(s)" +
                    (Skipped > 0 ? " · " + Skipped + " expression frame(s) skipped" : "");
            }
        }

        /// <summary>Loads path unless it is already loaded and unchanged. Throws IOException or InvalidDataException.</summary>
        internal static void Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new IOException("Enter the recording's path.");
            path = Path.GetFullPath(path.Trim().Trim('"'));
            if (!File.Exists(path)) throw new FileNotFoundException("No recording at " + path, path);
            DateTime write = File.GetLastWriteTimeUtc(path);
            if (packets != null && loadedPath == path && loadedWrite == write) return;
            string jsonl = path;
            string temporary = null;
            if (IsMatroska(path))
            {
                temporary = ExtractAttachment(path);
                jsonl = temporary;
            }
            try
            {
                List<Packet> read = Read(jsonl, out int skipped);
                Unload();
                packets = read;
                Skipped = skipped;
                loadedPath = path;
                loadedWrite = write;
            }
            finally
            {
                if (temporary != null)
                {
                    try
                    {
                        Directory.Delete(Path.GetDirectoryName(temporary), true);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        internal static void Unload()
        {
            packets = null;
            loadedPath = null;
            playing = false;
            Rewind();
        }

        internal static void Play()
        {
            if (packets == null) return;
            if (index >= packets.Count) Rewind();
            playing = true;
            lastNow = -1;
        }

        internal static void Pause()
        {
            playing = false;
        }

        /// <summary>Stops and goes back to the start; the recording stays loaded.</summary>
        internal static void Stop()
        {
            playing = false;
            Rewind();
        }

        private static void Rewind()
        {
            index = 0;
            playhead = 0;
            lastNow = -1;
        }

        /// <summary>Feeds the receiver every packet due by the playhead; after the last one playback stops, or with Loop on starts again.</summary>
        internal static void Advance(double now, FTEmulatorOscReceiver receiver)
        {
            if (!playing || packets == null || receiver == null)
            {
                lastNow = -1;
                return;
            }
            if (lastNow >= 0) playhead += Math.Max(0, now - lastNow) * speed;
            lastNow = now;
            while (index < packets.Count && packets[index].Time <= playhead)
            {
                byte[] data = packets[index++].Data;
                receiver.Receive(data, data.Length, null);
            }
            if (index < packets.Count) return;
            if (FTEmulatorSettings.Loop && packets.Count > 0)
            {
                index = 0;
                playhead = 0;
            }
            else playing = false;
        }

        // ---- Reading ----

        private static bool IsMatroska(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                var magic = new byte[4];
                return stream.Read(magic, 0, 4) == 4 && magic[0] == 0x1A && magic[1] == 0x45 && magic[2] == 0xDF && magic[3] == 0xA3;
            }
        }

        // ffmpeg copies the .mkv's first attachment (recording.ftrec.jsonl) into a new temporary folder.
        private static string ExtractAttachment(string mkv)
        {
            string folder = Path.Combine(Path.GetTempPath(), "RiryTools.FTEmulator." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, "recording.ftrec.jsonl");
            var start = new ProcessStartInfo("ffmpeg")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
            };
            start.Arguments = "-hide_banner -loglevel error -nostdin -y -dump_attachment:t:0 " + Quote(target) + " -i " + Quote(mkv) + " -t 0 -f null -";
            try
            {
                using (Process process = Process.Start(start))
                {
                    process.StandardOutput.ReadToEndAsync();
                    string errors = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(120000))
                    {
                        process.Kill();
                        throw new IOException("ffmpeg took too long reading " + mkv);
                    }
                    if (!File.Exists(target)) throw new InvalidDataException(mkv + " has no attached recording. " + errors.Trim());
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new IOException("ffmpeg was not found on PATH: it is needed to read .mkv recordings. Install it from https://ffmpeg.org, or open the .ftrec.jsonl instead.");
            }
            return target;
        }

        private static string Quote(string path)
        {
            if (path.IndexOf('"') >= 0) throw new IOException("Paths with quotation marks are not supported: " + path);
            return "\"" + path + "\"";
        }

        /// <summary>
        /// Reads path exactly as Load does and decodes every packet, without changing what is loaded or playing.
        /// True with a summary, or false with what is wrong.
        /// </summary>
        internal static bool Check(string path, out string report)
        {
            string temporary = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path)) throw new IOException("Enter the recording's path.");
                path = Path.GetFullPath(path.Trim().Trim('"'));
                if (!File.Exists(path)) throw new FileNotFoundException("No recording at " + path, path);
                string jsonl = path;
                if (IsMatroska(path)) jsonl = temporary = ExtractAttachment(path);
                List<Packet> read = Read(jsonl, out int skipped);
                var messages = new List<FTEmulatorOscMessage>();
                int parameters = 0;
                for (int i = 0; i < read.Count; i++)
                {
                    messages.Clear();
                    if (!FTEmulatorOscCodec.TryDecode(read[i].Data, read[i].Data.Length, messages, out string error))
                        throw new InvalidDataException("Packet " + (i + 1) + " at " + read[i].Time.ToString("0.000", CultureInfo.InvariantCulture) + " s cannot be decoded: " + error);
                    parameters += messages.Count;
                }
                if (read.Count == 0)
                {
                    report = skipped > 0
                        ? "Nothing to play: it holds only expression frames (a module recording). Play it through the module's playback source instead."
                        : "Nothing to play: the recording has no entries.";
                    return false;
                }
                report = "Valid: " + read.Count + " packet(s), " + parameters + " message(s), " +
                    read[read.Count - 1].Time.ToString("0.0", CultureInfo.InvariantCulture) + " s" +
                    (skipped > 0 ? "; " + skipped + " expression frame(s) will be skipped" : "") + ".";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
                                              exception is ArgumentException || exception is NotSupportedException)
            {
                report = exception.Message;
                return false;
            }
            finally
            {
                if (temporary != null)
                {
                    try
                    {
                        Directory.Delete(Path.GetDirectoryName(temporary), true);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        private static List<Packet> Read(string path, out int skipped)
        {
            var result = new List<Packet>();
            skipped = 0;
            int number = 0;
            double last = 0;
            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    number++;
                    if (line.Trim().Length == 0) continue;
                    if (!FTEmulatorJson.TryParse(line, out object value, out string error) || !(value is Dictionary<string, object> entry))
                        throw new InvalidDataException(path + " line " + number + ": " + (error ?? "not a JSON object"));
                    if (number == 1)
                    {
                        if (FTEmulatorJson.Text(entry, "format") != Format) throw new InvalidDataException(path + " is not a FTRecorder recording.");
                        continue;
                    }
                    if (!entry.TryGetValue("t", out object t) || !(t is double time) || time < 0)
                        throw new InvalidDataException(path + " line " + number + " has no time.");
                    if (time < last) time = last;
                    last = time;
                    int before = result.Count;
                    if (entry.TryGetValue("osc", out object osc) && osc is string base64)
                    {
                        byte[] data;
                        try
                        {
                            data = Convert.FromBase64String(base64);
                        }
                        catch (FormatException)
                        {
                            throw new InvalidDataException(path + " line " + number + " has a damaged packet.");
                        }
                        if (data.Length == 0 || data.Length > FTEmulatorOscCodec.MaxPacketLength) throw new InvalidDataException(path + " line " + number + " has a packet of the wrong size.");
                        result.Add(new Packet { Time = time, Data = data });
                    }
                    else if (entry.TryGetValue("param", out object name) && name is string parameter && parameter.Length > 0 && parameter.Length < 256)
                    {
                        object argument = null;
                        if (entry.TryGetValue("f", out object f) && f is double floating) argument = (float)floating;
                        else if (entry.TryGetValue("i", out object i) && i is double whole) argument = (int)whole;
                        else if (entry.TryGetValue("b", out object b) && b is bool flag) argument = flag;
                        if (argument != null) result.Add(new Packet { Time = time, Data = FTEmulatorOscCodec.Encode(ParameterPrefix + parameter, argument) });
                    }
                    else if (entry.TryGetValue("viseme", out object viseme) && viseme is double visemeIndex)
                    {
                        result.Add(new Packet { Time = time, Data = FTEmulatorOscCodec.Encode(ParameterPrefix + "Viseme", (int)visemeIndex) });
                        if (entry.TryGetValue("voice", out object voice) && voice is double loudness)
                            result.Add(new Packet { Time = time, Data = FTEmulatorOscCodec.Encode(ParameterPrefix + "Voice", (float)loudness) });
                    }
                    else if (entry.ContainsKey("unified"))
                    {
                        skipped++;
                    }
                    if (result.Count > MaxPackets) throw new InvalidDataException(path + " holds more than " + MaxPackets + " packets.");
                    if (result.Count == before) continue;
                }
            }
            if (number == 0) throw new InvalidDataException(path + " is empty.");
            return result;
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator recording player
// ============================================================================
//
// FTEmulatorRecording (this file)  Plays the companion's recordings; nothing is recorded in Unity.
//   Load(path)               .ftrec.jsonl, or .mkv (its attachment copied out by ffmpeg on PATH into a temporary folder,
//                            deleted after). Skipped when the same unchanged file is loaded. Header format
//                            "RiryTools.FTRecorder". Entries: "osc" (base64 packet), "param" (i/f/b, sent as
//                            /avatar/parameters/NAME), "viseme"+"voice" (Viseme and Voice); "unified" frames are counted
//                            in Skipped. At most MaxPackets (2,000,000).
//   Check(path, out report)  The same read as Load plus decoding every packet; loaded state untouched. For the window's
//                            Check validity button.
//   Play() / Pause() / Stop()  Stop rewinds; the recording stays loaded. Unload() forgets it.
//   Speed                    Playback rate (Speeds: 0.25, 0.5, 1, 1.5, 2 in the window).
//   Advance(now, receiver)   Called each session tick: moves the playhead by elapsed time × Speed and feeds due packets.
//   Status / Length / Loaded / Playing / Skipped  For the window.
