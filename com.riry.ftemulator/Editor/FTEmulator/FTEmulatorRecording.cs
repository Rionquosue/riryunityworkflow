using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's OSC recording: every packet the receiver takes from the network is kept with its time, and saved when
    /// recording stops (or Play Mode ends) to Documents/RiryTools/FTEmulator/Recordings. Playback feeds a saved recording's
    /// packets to the running receiver at their recorded times, as if the tracking app sent them.
    /// </summary>
    internal static class FTEmulatorRecording
    {
        internal const string Extension = ".ftrec";
        // File header and limits: a recording is read back as untrusted data.
        private const int Magic = 0x52465452; // "RTFR"
        private const int Version = 1;
        internal const int MaxPackets = 2000000;

        private struct Packet
        {
            internal double Time;
            internal byte[] Data;
        }

        private static readonly object Gate = new object();
        private static List<Packet> recording;
        private static double recordStart;
        private static List<Packet> playback;
        private static int playIndex;
        private static double playStart = -1;
        private static string playName;

        internal static string Folder
        {
            get { return Path.Combine(RiryToolsPaths.Documents("FTEmulator"), "Recordings"); }
        }

        internal static bool Recording
        {
            get { lock (Gate) return recording != null; }
        }

        internal static int RecordedPackets
        {
            get { lock (Gate) return recording != null ? recording.Count : 0; }
        }

        internal static bool Playing
        {
            get { return playback != null; }
        }

        /// <summary>"name: n of m packets" while playing, else null.</summary>
        internal static string PlayStatus
        {
            get { return playback == null ? null : playName + ": " + playIndex + " of " + playback.Count + " packets"; }
        }

        internal static void StartRecording()
        {
            lock (Gate)
            {
                recording = new List<Packet>();
                recordStart = FTEmulatorClock.Now;
            }
        }

        /// <summary>The receive thread's copy of one packet from the network, while recording.</summary>
        internal static void Capture(byte[] data, int length)
        {
            lock (Gate)
            {
                if (recording == null || recording.Count >= MaxPackets || data == null) return;
                var copy = new byte[length];
                Buffer.BlockCopy(data, 0, copy, 0, length);
                recording.Add(new Packet { Time = FTEmulatorClock.Now - recordStart, Data = copy });
            }
        }

        /// <summary>Stops recording and saves it. Returns the file written, or null when nothing was recorded.</summary>
        internal static string StopRecording()
        {
            List<Packet> packets;
            lock (Gate)
            {
                packets = recording;
                recording = null;
            }
            if (packets == null || packets.Count == 0) return null;
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + Extension);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(packets.Count);
                foreach (Packet packet in packets)
                {
                    writer.Write(packet.Time);
                    writer.Write(packet.Data.Length);
                    writer.Write(packet.Data);
                }
            }
            return path;
        }

        /// <summary>Loads a recording and starts playing it on the next tick. Throws when the file is not a valid recording.</summary>
        internal static void Play(string path)
        {
            var packets = new List<Packet>();
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) throw new InvalidDataException(path + " is not an FTEmulator recording.");
                int count = reader.ReadInt32();
                if (count < 0 || count > MaxPackets) throw new InvalidDataException(path + " holds an invalid packet count.");
                double last = 0;
                for (int i = 0; i < count; i++)
                {
                    double time = reader.ReadDouble();
                    int length = reader.ReadInt32();
                    if (double.IsNaN(time) || time < last || length <= 0 || length > FTEmulatorOscCodec.MaxPacketLength)
                        throw new InvalidDataException(path + " is damaged at packet " + (i + 1) + ".");
                    byte[] data = reader.ReadBytes(length);
                    if (data.Length != length) throw new InvalidDataException(path + " ends early.");
                    packets.Add(new Packet { Time = time, Data = data });
                    last = time;
                }
            }
            playback = packets;
            playIndex = 0;
            playStart = -1;
            playName = Path.GetFileNameWithoutExtension(path);
        }

        internal static void StopPlayback()
        {
            playback = null;
            playName = null;
        }

        /// <summary>Feeds the receiver every packet due by now (FTEmulatorClock time); playback ends after the last one.</summary>
        internal static void Advance(double now, FTEmulatorOscReceiver receiver)
        {
            if (playback == null || receiver == null) return;
            if (playStart < 0) playStart = now;
            while (playIndex < playback.Count && playback[playIndex].Time <= now - playStart)
            {
                byte[] data = playback[playIndex++].Data;
                receiver.Receive(data, data.Length, null);
            }
            if (playIndex >= playback.Count) StopPlayback();
        }

        /// <summary>Play Mode or the session ended: saves a recording in progress and stops playback. Returns the saved file or null.</summary>
        internal static string StopAll()
        {
            StopPlayback();
            return StopRecording();
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator recording
// ============================================================================
//
// FTEmulatorRecording (this file)
//   Extension                ".ftrec".
//   MaxPackets               2,000,000: the most packets one recording keeps or loads.
//   Folder                   Documents/RiryTools/FTEmulator/Recordings.
//   Recording / RecordedPackets / Playing / PlayStatus  State for the window.
//   StartRecording()         Starts keeping every network packet the receiver takes.
//   Capture(data, length)    Called by the receive thread for each packet.
//   StopRecording()          Saves the packets with their times; returns the file.
//   Play(path) / StopPlayback()  Loads a recording (checked as untrusted) / stops it.
//   Advance(now, receiver)   Feeds the receiver the packets due by now.
//   StopAll()                Saves a recording in progress and stops playback.
