using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// The few multicast DNS packets OSCQuery discovery needs: a question for a service type, the answer that announces
    /// one service (PTR, SRV, TXT and A records), and a reader for what other programs send. Names are written in full
    /// (no compression), which every reader accepts; compressed names are read, with a bound on the jumps.
    /// </summary>
    internal sealed class FTEmulatorDnsPacket
    {
        internal const int TypeA = 1;
        internal const int TypePtr = 12;
        internal const int TypeTxt = 16;
        internal const int TypeSrv = 33;
        internal const int TypeAny = 255;
        private const int ClassIn = 1;
        private const int MaxRecords = 64;
        private const int MaxJumps = 16;

        internal sealed class Question
        {
            internal string Name;
            internal int Type;
        }

        internal sealed class Record
        {
            internal string Name;
            internal int Type;
            /// <summary>PTR: the name pointed to. SRV: the target host.</summary>
            internal string Target;
            /// <summary>SRV only.</summary>
            internal int Port;
            /// <summary>A only.</summary>
            internal IPAddress Address;
        }

        internal bool Response;
        internal readonly List<Question> Questions = new List<Question>();
        internal readonly List<Record> Answers = new List<Record>();
        /// <summary>The authority and additional records.</summary>
        internal readonly List<Record> Additional = new List<Record>();

        /// <summary>A question for every service of a type, such as "_oscjson._tcp.local".</summary>
        internal static byte[] Query(string serviceType)
        {
            var bytes = new List<byte>(64);
            Header(bytes, false, 1, 0, 0);
            Name(bytes, serviceType);
            Short(bytes, TypePtr);
            Short(bytes, ClassIn);
            return bytes.ToArray();
        }

        /// <summary>
        /// The answer announcing one service: serviceType points to "instance.serviceType", which is at host:port, and
        /// host has address. instance must be one label (no dots).
        /// </summary>
        internal static byte[] Announce(string serviceType, string instance, string host, int port, IPAddress address, int ttlSeconds = 120)
        {
            string full = instance + "." + serviceType;
            var bytes = new List<byte>(256);
            Header(bytes, true, 0, 1, 3);
            // PTR: the service type lists this instance.
            RecordHeader(bytes, serviceType, TypePtr, ttlSeconds);
            Data(bytes, data => Name(data, full));
            // TXT, SRV and A, as additional records.
            RecordHeader(bytes, full, TypeTxt, ttlSeconds);
            Data(bytes, data =>
            {
                byte[] text = Encoding.ASCII.GetBytes("txtvers=1");
                data.Add((byte)text.Length);
                data.AddRange(text);
            });
            RecordHeader(bytes, full, TypeSrv, ttlSeconds);
            Data(bytes, data =>
            {
                Short(data, 0);
                Short(data, 0);
                Short(data, port);
                Name(data, host);
            });
            RecordHeader(bytes, host, TypeA, ttlSeconds);
            Data(bytes, data => data.AddRange(address.GetAddressBytes()));
            return bytes.ToArray();
        }

        private static void Header(List<byte> bytes, bool response, int questions, int answers, int additional)
        {
            Short(bytes, 0);
            // A response is authoritative (0x8400); a question has no flags.
            Short(bytes, response ? 0x8400 : 0);
            Short(bytes, questions);
            Short(bytes, answers);
            Short(bytes, 0);
            Short(bytes, additional);
        }

        private static void RecordHeader(List<byte> bytes, string name, int type, int ttlSeconds)
        {
            Name(bytes, name);
            Short(bytes, type);
            Short(bytes, ClassIn);
            Short(bytes, ttlSeconds >> 16);
            Short(bytes, ttlSeconds & 0xFFFF);
        }

        private static void Data(List<byte> bytes, Action<List<byte>> write)
        {
            var data = new List<byte>();
            write(data);
            Short(bytes, data.Count);
            bytes.AddRange(data);
        }

        private static void Short(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }

        private static void Name(List<byte> bytes, string name)
        {
            foreach (string label in name.Split('.'))
            {
                byte[] text = Encoding.UTF8.GetBytes(label);
                if (text.Length == 0) continue;
                if (text.Length > 63) throw new ArgumentException("A DNS label is longer than 63 bytes: " + label);
                bytes.Add((byte)text.Length);
                bytes.AddRange(text);
            }
            bytes.Add(0);
        }

        /// <summary>Reads a packet. False for anything cut short or malformed; nothing is read past length.</summary>
        internal static bool TryParse(byte[] data, int length, out FTEmulatorDnsPacket packet)
        {
            packet = null;
            if (data == null || length < 12 || length > data.Length) return false;
            var result = new FTEmulatorDnsPacket { Response = (data[2] & 0x80) != 0 };
            int questions = data[4] << 8 | data[5];
            int answers = data[6] << 8 | data[7];
            int others = (data[8] << 8 | data[9]) + (data[10] << 8 | data[11]);
            if (questions > MaxRecords || answers > MaxRecords || others > MaxRecords * 2) return false;
            int at = 12;
            for (int i = 0; i < questions; i++)
            {
                if (!ReadName(data, length, length, ref at, out string name) || at + 4 > length) return false;
                result.Questions.Add(new Question { Name = name, Type = data[at] << 8 | data[at + 1] });
                at += 4;
            }
            for (int i = 0; i < answers + others; i++)
            {
                if (!ReadName(data, length, length, ref at, out string name) || at + 10 > length) return false;
                var record = new Record { Name = name, Type = data[at] << 8 | data[at + 1] };
                int size = data[at + 8] << 8 | data[at + 9];
                at += 10;
                if (at + size > length) return false;
                int inside = at;
                if (record.Type == TypePtr)
                {
                    if (!ReadName(data, at + size, length, ref inside, out record.Target)) return false;
                }
                else if (record.Type == TypeSrv)
                {
                    if (size < 7) return false;
                    record.Port = data[at + 4] << 8 | data[at + 5];
                    inside = at + 6;
                    if (!ReadName(data, at + size, length, ref inside, out record.Target)) return false;
                }
                else if (record.Type == TypeA)
                {
                    if (size != 4) return false;
                    record.Address = new IPAddress(new[] { data[at], data[at + 1], data[at + 2], data[at + 3] });
                }
                at += size;
                (i < answers ? result.Answers : result.Additional).Add(record);
            }
            packet = result;
            return true;
        }

        // A name: labels joined with dots. A compression pointer may jump back anywhere in the packet (even from inside
        // record data), so jumps are bounded by the whole packet and counted.
        private static bool ReadName(byte[] data, int limit, int packetLength, ref int at, out string name)
        {
            name = null;
            var result = new StringBuilder();
            int position = at, jumps = 0, after = -1;
            int bound = limit;
            while (true)
            {
                if (position >= bound) return false;
                int size = data[position];
                if (size == 0) { position++; break; }
                if ((size & 0xC0) == 0xC0)
                {
                    if (position + 1 >= bound || ++jumps > MaxJumps) return false;
                    if (after < 0) after = position + 2;
                    position = (size & 0x3F) << 8 | data[position + 1];
                    // A pointed-to name lies earlier in the packet; from there on, the bound is the packet itself.
                    bound = packetLength;
                    continue;
                }
                if ((size & 0xC0) != 0 || position + 1 + size > bound) return false;
                if (result.Length > 0) result.Append('.');
                result.Append(Encoding.UTF8.GetString(data, position + 1, size));
                if (result.Length > 255) return false;
                position += 1 + size;
            }
            at = after >= 0 ? after : position;
            name = result.ToString();
            return true;
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator DNS packets
// ============================================================================
//
// FTEmulatorDnsPacket (this file)
//   Query(serviceType)       A PTR question for a service type ("_oscjson._tcp.local").
//   Announce(serviceType, instance, host, port, address, ttl)  The answer for one service: PTR serviceType ->
//                            instance.serviceType, then TXT ("txtvers=1"), SRV (port, host) and A (host -> address).
//   TryParse(data, length, out packet)  Response, Questions (Name, Type), Answers and Additional (Record: Name, Type, Target
//                            for PTR and SRV, Port for SRV, Address for A). False when malformed, cut short, or with more
//                            than MaxRecords (64) of a kind. Compressed names are followed for at most MaxJumps (16) jumps.
//   Types                    TypeA 1, TypePtr 12, TypeTxt 16, TypeSrv 33, TypeAny 255.
