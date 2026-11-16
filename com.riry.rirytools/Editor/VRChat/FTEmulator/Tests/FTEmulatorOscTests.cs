using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// FTEmulator's OSC packets, receiver buffer, discovery packets and OSCQuery replies, on this machine only: sockets
    /// are bound to 127.0.0.1 on ports the system picks. These check the contracts FTEmulator relies on; they do not show
    /// that a VRCFaceTracking build completes the handshake, which needs VRCFaceTracking running.
    /// </summary>
    internal sealed class FTEmulatorOscTests
    {
        private static FTEmulatorSessionContext Active(int generation = 7)
        {
            return new FTEmulatorSessionContext(generation, () => true, CancellationToken.None);
        }

        // ---- Hand-built packets, independent of the encoder ----

        private static void Pad(List<byte> bytes) { while ((bytes.Count & 3) != 0) bytes.Add(0); }

        private static void Text(List<byte> bytes, string text)
        {
            bytes.AddRange(Encoding.ASCII.GetBytes(text));
            bytes.Add(0);
            Pad(bytes);
        }

        private static void Int(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }

        private static byte[] FloatMessage(string address, float value)
        {
            var bytes = new List<byte>();
            Text(bytes, address);
            Text(bytes, ",f");
            byte[] raw = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(raw);
            bytes.AddRange(raw);
            return bytes.ToArray();
        }

        private static byte[] Bundle(params byte[][] elements)
        {
            var bytes = new List<byte>();
            Text(bytes, "#bundle");
            bytes.AddRange(new byte[8]);
            foreach (byte[] element in elements)
            {
                Int(bytes, element.Length);
                bytes.AddRange(element);
            }
            return bytes.ToArray();
        }

        private static byte[] FaceBlob(int length, int index = -1, byte value = 0)
        {
            var blob = new byte[length];
            if (index >= 0) blob[index] = value;
            return FTEmulatorOscCodec.Encode(FTEmulatorOscCodec.NativeFaceAddress, blob);
        }

        private static List<FTEmulatorOscMessage> Decode(byte[] packet)
        {
            var messages = new List<FTEmulatorOscMessage>();
            Assert.IsTrue(FTEmulatorOscCodec.TryDecode(packet, packet.Length, messages, out string error), error);
            return messages;
        }

        [Test]
        public void Codec_DecodesTypedParameters()
        {
            List<FTEmulatorOscMessage> messages = Decode(FloatMessage("/avatar/parameters/FT/v2/JawX", -0.25f));
            Assert.IsTrue(FTEmulatorOscCodec.TryDecodeParameter(messages[0], out FTEmulatorParameterValue jaw));
            Assert.AreEqual("FT/v2/JawX", jaw.Name);
            Assert.AreEqual(FTEmulatorValueType.Float, jaw.Type);
            Assert.AreEqual(-0.25f, jaw.Value, "a signed value is kept");

            Assert.IsTrue(FTEmulatorOscCodec.TryDecodeParameter(Decode(FTEmulatorOscCodec.Encode("/avatar/parameters/Mode", 3))[0], out FTEmulatorParameterValue mode));
            Assert.AreEqual(FTEmulatorValueType.Int, mode.Type);
            Assert.AreEqual(3f, mode.Value);
            Assert.IsTrue(FTEmulatorOscCodec.TryDecodeParameter(Decode(FTEmulatorOscCodec.Encode("/avatar/parameters/On", true))[0], out FTEmulatorParameterValue on));
            Assert.AreEqual(FTEmulatorValueType.Bool, on.Type);
            Assert.AreEqual(1f, on.Value);
            Assert.IsTrue(FTEmulatorOscCodec.TryDecodeParameter(Decode(FTEmulatorOscCodec.Encode("/avatar/parameters/On", false))[0], out FTEmulatorParameterValue off));
            Assert.AreEqual(0f, off.Value);
            // Not a parameter: another address, a string argument, no name.
            Assert.IsFalse(FTEmulatorOscCodec.TryDecodeParameter(Decode(FTEmulatorOscCodec.Encode("/avatar/change", "avtr_x"))[0], out FTEmulatorParameterValue _));
            Assert.IsFalse(FTEmulatorOscCodec.TryDecodeParameter(Decode(FTEmulatorOscCodec.Encode("/avatar/parameters/Name", "text"))[0], out FTEmulatorParameterValue _));
            Assert.IsFalse(FTEmulatorOscCodec.TryDecodeParameter(Decode(FTEmulatorOscCodec.Encode("/avatar/parameters/", 1f))[0], out FTEmulatorParameterValue _));
        }

        [Test]
        public void Codec_BundlesInOrder_AndBoundsNesting()
        {
            byte[] inner = Bundle(FloatMessage("/avatar/parameters/B", 0.2f));
            List<FTEmulatorOscMessage> messages = Decode(Bundle(FloatMessage("/avatar/parameters/A", 0.1f), inner, FloatMessage("/avatar/parameters/C", 0.3f)));
            Assert.AreEqual(3, messages.Count);
            Assert.AreEqual("/avatar/parameters/A", messages[0].Address);
            Assert.AreEqual("/avatar/parameters/B", messages[1].Address);
            Assert.AreEqual("/avatar/parameters/C", messages[2].Address);

            byte[] deep = FloatMessage("/a", 1f);
            for (int i = 0; i < FTEmulatorOscCodec.MaxBundleDepth; i++) deep = Bundle(deep);
            Assert.AreEqual(1, Decode(deep).Count);
            deep = Bundle(deep);
            var none = new List<FTEmulatorOscMessage>();
            Assert.IsFalse(FTEmulatorOscCodec.TryDecode(deep, deep.Length, none, out string error));
            StringAssert.Contains("nested", error);
            Assert.AreEqual(0, none.Count);
        }

        [Test]
        public void Codec_RejectsMalformedPackets()
        {
            byte[] good = FloatMessage("/avatar/parameters/JawOpen", 0.5f);
            var messages = new List<FTEmulatorOscMessage>();
            void Bad(byte[] packet, int length, string why)
            {
                Assert.IsFalse(FTEmulatorOscCodec.TryDecode(packet, length, messages, out string error), why);
                Assert.IsNotEmpty(error);
                Assert.AreEqual(0, messages.Count, why);
            }
            Bad(good, good.Length - 4, "the float is cut off");
            Bad(good, good.Length - 1, "not a multiple of four");
            Bad(new byte[0], 0, "empty");
            Bad(null, 0, "no packet");
            byte[] noSlash = (byte[])good.Clone();
            noSlash[0] = (byte)'a';
            Bad(noSlash, noSlash.Length, "the address does not start with a slash");
            byte[] padding = (byte[])good.Clone();
            padding[Array.IndexOf(padding, (byte)0) + 1] = 1;
            Bad(padding, padding.Length, "padding is not zero");
            byte[] nan = FloatMessage("/a", float.NaN);
            Bad(nan, nan.Length, "NaN");
            byte[] infinite = FloatMessage("/a", float.NegativeInfinity);
            Bad(infinite, infinite.Length, "infinity");
            var unknown = new List<byte>();
            Text(unknown, "/a");
            Text(unknown, ",h");
            unknown.AddRange(new byte[8]);
            Bad(unknown.ToArray(), unknown.Count, "a type tag that is not supported");
            var extra = new List<byte>(good);
            extra.AddRange(new byte[4]);
            Bad(extra.ToArray(), extra.Count, "bytes after the arguments");
            var blob = new List<byte>();
            Text(blob, "/a");
            Text(blob, ",b");
            Int(blob, int.MaxValue);
            blob.AddRange(new byte[8]);
            Bad(blob.ToArray(), blob.Count, "a blob larger than the packet");
            // One bad element refuses the whole bundle: nothing of it is applied.
            byte[] bundle = Bundle(good, nan);
            Bad(bundle, bundle.Length, "a bundle with one bad element");
            var oversize = new List<byte>();
            Text(oversize, "#bundle");
            oversize.AddRange(new byte[8]);
            Int(oversize, 4096);
            oversize.AddRange(good);
            Bad(oversize.ToArray(), oversize.Count, "a bundle element larger than the bundle");
        }

        [Test]
        public void Codec_NativeFaceBlob()
        {
            var values = new float[102];
            FTEmulatorOscMessage message = Decode(FaceBlob(102, 36, 255))[0];
            Assert.IsTrue(FTEmulatorOscCodec.TryDecodeNativeFace(message, values, out string error), error);
            for (int i = 0; i < values.Length; i++) Assert.AreEqual(i == 36 ? 1f : 0f, values[i], "byte 36 is JawOpen");
            Assert.AreEqual(36, FTEmulatorCoreTests.Catalog.Key("JawOpen"));
            Assert.IsTrue(FTEmulatorOscCodec.TryDecodeNativeFace(Decode(FaceBlob(102, 9, 51))[0], values, out error));
            Assert.AreEqual(0.2f, values[9], 1e-6f);

            Assert.IsFalse(FTEmulatorOscCodec.TryDecodeNativeFace(Decode(FaceBlob(101))[0], values, out error), "a short blob");
            StringAssert.Contains("101", error);
            Assert.IsFalse(FTEmulatorOscCodec.TryDecodeNativeFace(Decode(FaceBlob(103))[0], values, out error), "a long blob");
            Assert.IsFalse(FTEmulatorOscCodec.TryDecodeNativeFace(Decode(FTEmulatorOscCodec.Encode(FTEmulatorOscCodec.NativeFaceAddress, 1f))[0], values, out error), "not a blob");
            var two = new List<byte>();
            Text(two, FTEmulatorOscCodec.NativeFaceAddress);
            Text(two, ",bb");
            for (int i = 0; i < 2; i++)
            {
                Int(two, 102);
                two.AddRange(new byte[104]);
            }
            Assert.IsFalse(FTEmulatorOscCodec.TryDecodeNativeFace(Decode(two.ToArray())[0], values, out error), "two blobs");
        }

        [Test]
        public void Codec_EncodesStatus()
        {
            List<byte[]> packets = FTEmulatorOscCodec.EncodeStatus("local:avatar", true);
            FTEmulatorOscMessage change = Decode(packets[0])[0];
            Assert.AreEqual("/avatar/change", change.Address);
            Assert.AreEqual("local:avatar", change.Arguments[0].Text);
            FTEmulatorOscMessage native = Decode(packets[1])[0];
            Assert.AreEqual("/avatar/avatarfullfacetracking", native.Address);
            Assert.AreEqual('T', native.Arguments[0].Tag);
            Assert.AreEqual('F', Decode(FTEmulatorOscCodec.EncodeStatus("x", false)[1])[0].Arguments[0].Tag);
        }

        // ---- Receiver ----

        [Test]
        public void Receiver_BufferIsBoundedAndNewestWins()
        {
            var diagnostics = new FTEmulatorDiagnostics();
            using (var receiver = new FTEmulatorOscReceiver(0, 102, diagnostics))
            {
                receiver.Start(Active());
                var from = new IPEndPoint(IPAddress.Loopback, 5000);
                // A flood while nothing reads: more addresses than the buffer keeps, each sent many times.
                for (int round = 0; round < 20; round++)
                {
                    for (int address = 0; address < FTEmulatorOscReceiver.MaxAddresses + 500; address++)
                    {
                        byte[] packet = FloatMessage("/avatar/parameters/P" + address, round / 100f);
                        receiver.Receive(packet, packet.Length, from);
                    }
                    byte[] face = FaceBlob(102, 36, (byte)round);
                    receiver.Receive(face, face.Length, from);
                }
                Assert.IsTrue(receiver.TryRead(out FTEmulatorExpressionFrame expressions, out FTEmulatorParameterFrame parameters));
                Assert.AreEqual(FTEmulatorOscReceiver.MaxAddresses, parameters.Values.Count);
                foreach (FTEmulatorParameterValue value in parameters.Values) Assert.AreEqual(0.19f, value.Value, 1e-6f);
                Assert.AreEqual(19 / 255f, expressions.Values[36], 1e-6f);
                Assert.AreEqual(7, expressions.Generation);
                Assert.AreEqual(1, diagnostics.Snapshot().FindAll(item => item.Code == FTEmulatorDiagnostics.AddressLimit).Count, "one line however many were dropped");
                Assert.That(diagnostics.Count(FTEmulatorDiagnostics.AddressLimit), Is.GreaterThan(1));
                Assert.IsFalse(receiver.TryRead(out expressions, out parameters), "nothing is kept after a read");
            }
        }

        [Test]
        public void Receiver_CountsMalformedPacketsUnderOneCode_AndRefusesOtherMachines()
        {
            var diagnostics = new FTEmulatorDiagnostics();
            int firstSeen = 0;
            diagnostics.FirstSeen = (code, context) => firstSeen++;
            using (var receiver = new FTEmulatorOscReceiver(0, 102, diagnostics))
            {
                receiver.Start(Active());
                var from = new IPEndPoint(IPAddress.Loopback, 5000);
                for (int i = 0; i < 500; i++) Assert.IsFalse(receiver.Receive(new byte[] { 1, 2, 3 }, 3, from));
                Assert.AreEqual(500, receiver.Rejected);
                Assert.AreEqual(500, diagnostics.Count(FTEmulatorDiagnostics.PacketRejected));
                Assert.AreEqual(1, firstSeen, "the Console hears of it once");
                byte[] good = FloatMessage("/avatar/parameters/JawOpen", 1f);
                Assert.IsFalse(receiver.Receive(good, good.Length, new IPEndPoint(IPAddress.Parse("192.0.2.10"), 5000)));
                Assert.IsFalse(receiver.TryRead(out FTEmulatorExpressionFrame _, out FTEmulatorParameterFrame _));
                // A native message of the wrong size is ignored; the rest of its packet is not.
                byte[] mixed = Bundle(FaceBlob(50), good);
                Assert.IsTrue(receiver.Receive(mixed, mixed.Length, from));
                Assert.IsTrue(receiver.TryRead(out FTEmulatorExpressionFrame expressions, out FTEmulatorParameterFrame parameters));
                Assert.IsNull(expressions);
                Assert.AreEqual(1, parameters.Values.Count);
                Assert.AreEqual(1, diagnostics.Count(FTEmulatorDiagnostics.NativeBlobRejected));
            }
        }

        [Test]
        public void Receiver_PlayModeGate_PortInUse_StopAndRestart()
        {
            var diagnostics = new FTEmulatorDiagnostics();
            var idle = new FTEmulatorOscReceiver(0, 102, diagnostics);
            Assert.Throws<InvalidOperationException>(() => idle.Start(new FTEmulatorSessionContext(1, () => false, CancellationToken.None)));
            Assert.IsFalse(idle.Running);

            var first = new FTEmulatorOscReceiver(0, 102, diagnostics);
            first.Start(Active());
            int port = first.Port;
            Assert.That(port, Is.GreaterThan(0));
            var second = new FTEmulatorOscReceiver(port, 102, diagnostics);
            var taken = Assert.Throws<FTEmulatorPortInUseException>(() => second.Start(Active()));
            Assert.AreEqual(port, taken.Port);
            Assert.IsFalse(second.Running);
            Assert.AreEqual(1, diagnostics.Count(FTEmulatorDiagnostics.PortInUse));
            Assert.IsTrue(first.Running, "the port's owner is left alone");

            // A packet over the socket reaches the buffer.
            using (var sender = new UdpClient())
            {
                byte[] packet = FloatMessage("/avatar/parameters/JawOpen", 0.75f);
                sender.Send(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, port));
            }
            FTEmulatorParameterFrame parameters = null;
            for (int i = 0; i < 200 && parameters == null; i++)
            {
                if (!first.TryRead(out FTEmulatorExpressionFrame _, out parameters)) Thread.Sleep(10);
            }
            Assert.IsNotNull(parameters, "the packet arrived");
            Assert.AreEqual(0.75f, parameters.Values[0].Value);
            Assert.AreEqual(FTEmulatorOscReceiver.LegacyKind, first.LastKind);

            // Stop returns while the thread waits in a blocking receive, and frees the port.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            first.Stop();
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(900));
            Assert.IsFalse(first.Running);
            first.Stop();
            second.Start(Active());
            Assert.AreEqual(port, second.Port);
            second.Dispose();
            Assert.AreEqual(0, diagnostics.Count(FTEmulatorDiagnostics.ReceiverFault));
        }

        // ---- Discovery packets ----

        [Test]
        public void Dns_AnnouncementIsWhatVrcftLooksFor()
        {
            byte[] bytes = FTEmulatorDnsPacket.Announce(FTEmulatorOscQuery.JsonService, "VRChat-Client-RiryFTEmulator-abc123", "VRChat-Client-RiryFTEmulator-abc123.local", 54321,
                IPAddress.Loopback);
            Assert.IsTrue(FTEmulatorDnsPacket.TryParse(bytes, bytes.Length, out FTEmulatorDnsPacket packet));
            Assert.IsTrue(packet.Response);
            // The first answer is a PTR for the service type whose target has four labels and starts with VRChat-Client.
            Assert.AreEqual(FTEmulatorDnsPacket.TypePtr, packet.Answers[0].Type);
            Assert.AreEqual("_oscjson._tcp.local", packet.Answers[0].Name);
            Assert.AreEqual(4, packet.Answers[0].Target.Split('.').Length);
            StringAssert.StartsWith("VRChat-Client", packet.Answers[0].Target);
            FTEmulatorDnsPacket.Record service = packet.Additional.Find(record => record.Type == FTEmulatorDnsPacket.TypeSrv);
            Assert.AreEqual(54321, service.Port);
            Assert.AreEqual(IPAddress.Loopback, packet.Additional.Find(record => record.Type == FTEmulatorDnsPacket.TypeA).Address);

            byte[] query = FTEmulatorDnsPacket.Query(FTEmulatorOscQuery.JsonService);
            Assert.IsTrue(FTEmulatorDnsPacket.TryParse(query, query.Length, out FTEmulatorDnsPacket question));
            Assert.IsFalse(question.Response);
            Assert.AreEqual("_oscjson._tcp.local", question.Questions[0].Name);
        }

        [Test]
        public void Dns_ReadsCompressedNames_AndRejectsBrokenPackets()
        {
            // One question for "a.local", then a PTR answer whose name and target point back at it.
            var bytes = new List<byte> { 0, 0, 0x84, 0, 0, 1, 0, 1, 0, 0, 0, 0 };
            bytes.AddRange(new byte[] { 1, (byte)'a', 5, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', 0, 0, 12, 0, 1 });
            bytes.AddRange(new byte[] { 0xC0, 12, 0, 12, 0, 1, 0, 0, 0, 120, 0, 4, 1, (byte)'b', 0xC0, 12 });
            byte[] packet = bytes.ToArray();
            Assert.IsTrue(FTEmulatorDnsPacket.TryParse(packet, packet.Length, out FTEmulatorDnsPacket parsed));
            Assert.AreEqual("a.local", parsed.Answers[0].Name);
            Assert.AreEqual("b.a.local", parsed.Answers[0].Target);

            Assert.IsFalse(FTEmulatorDnsPacket.TryParse(packet, packet.Length - 3, out parsed), "cut short");
            Assert.IsFalse(FTEmulatorDnsPacket.TryParse(new byte[5], 5, out parsed), "shorter than a header");
            byte[] loop = (byte[])packet.Clone();
            loop[12] = 0xC0;
            loop[13] = 12;
            Assert.IsFalse(FTEmulatorDnsPacket.TryParse(loop, loop.Length, out parsed), "a name that points at itself");
            byte[] many = (byte[])packet.Clone();
            many[6] = 0xFF;
            many[7] = 0xFF;
            Assert.IsFalse(FTEmulatorDnsPacket.TryParse(many, many.Length, out parsed), "more records than a packet may claim");
        }

        // ---- OSCQuery ----

        private static List<FTEmulatorAvatarParameter> Parameters()
        {
            return new List<FTEmulatorAvatarParameter>
            {
                new FTEmulatorAvatarParameter { Name = "FT/v2/JawOpen", Type = FTEmulatorValueType.Float },
                new FTEmulatorAvatarParameter { Name = "FT/v2/JawX", Type = FTEmulatorValueType.Float },
                new FTEmulatorAvatarParameter { Name = "EyeTrackingActive", Type = FTEmulatorValueType.Bool },
                new FTEmulatorAvatarParameter { Name = "Outfit", Type = FTEmulatorValueType.Int },
            };
        }

        private static Dictionary<string, object> Node(Dictionary<string, object> node, params string[] path)
        {
            foreach (string name in path) node = (Dictionary<string, object>)((Dictionary<string, object>)node["CONTENTS"])[name];
            return node;
        }

        [Test]
        public void Query_AvatarTreeIsWhatVrcftReads()
        {
            var advert = new FTEmulatorAvatarAdvert("local:avatar", false, Parameters(), 3);
            Assert.AreEqual(4, advert.ParameterCount);
            Assert.IsTrue(advert.TryGet("/avatar", out string json));
            var avatar = (Dictionary<string, object>)FTEmulatorJson.Parse(json);
            Assert.AreEqual("/avatar", avatar["FULL_PATH"]);
            Dictionary<string, object> change = Node(avatar, "change");
            Assert.AreEqual("/avatar/change", change["FULL_PATH"]);
            Assert.AreEqual("local:avatar", ((List<object>)change["VALUE"])[0]);
            Dictionary<string, object> native = Node(avatar, "avatarfullfacetracking");
            Assert.AreEqual(false, ((List<object>)native["VALUE"])[0], "native support is not claimed for a legacy avatar");
            Dictionary<string, object> jaw = Node(avatar, "parameters", "FT", "v2", "JawOpen");
            Assert.AreEqual("/avatar/parameters/FT/v2/JawOpen", jaw["FULL_PATH"]);
            Assert.AreEqual("f", jaw["TYPE"]);
            Assert.AreEqual("T", Node(avatar, "parameters", "EyeTrackingActive")["TYPE"]);
            Assert.AreEqual("i", Node(avatar, "parameters", "Outfit")["TYPE"]);
            // Each part can be asked for on its own, as VRCFaceTracking does when the whole tree fails to parse.
            Assert.IsTrue(advert.TryGet("/avatar/parameters", out string _));
            Assert.IsTrue(advert.TryGet("/avatar/avatarfullfacetracking", out string _));
            Assert.IsFalse(advert.TryGet("/tracking/face/v1", out string _), "not advertised without native support");
            Assert.IsFalse(advert.TryGet("/avatar/parameters/Missing", out string _));

            var withNative = new FTEmulatorAvatarAdvert("local:native", true, Parameters(), 4);
            Assert.IsTrue(withNative.TryGet("/tracking/face/v1", out string face));
            Assert.AreEqual("b", ((Dictionary<string, object>)FTEmulatorJson.Parse(face))["TYPE"]);
            Assert.IsTrue(withNative.TryGet("/avatar/avatarfullfacetracking", out string flag));
            Assert.AreEqual(true, ((List<object>)((Dictionary<string, object>)FTEmulatorJson.Parse(flag))["VALUE"])[0]);
        }

        private static string Get(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 3000;
            request.Proxy = null;
            using (WebResponse response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
                return reader.ReadToEnd();
        }

        [Test]
        public void Query_ServesHttpOnlyWhileRunning_AndSwapsAvatarsWhole()
        {
            var diagnostics = new FTEmulatorDiagnostics();
            var query = new FTEmulatorOscQuery(diagnostics);
            Assert.Throws<InvalidOperationException>(() => query.Start(9000, new FTEmulatorAvatarAdvert("a", false, Parameters(), 1),
                new FTEmulatorSessionContext(1, () => false, CancellationToken.None)));
            Assert.IsFalse(query.Running);

            query.Start(9123, new FTEmulatorAvatarAdvert("local:first", false, Parameters(), 1), Active());
            try
            {
                StringAssert.StartsWith("VRChat-Client", query.ServiceName);
                string root = "http://127.0.0.1:" + query.HttpPort;
                var host = (Dictionary<string, object>)FTEmulatorJson.Parse(Get(root + "/?HOST_INFO"));
                Assert.AreEqual(9123d, host["OSC_PORT"], "the receiver's own port, not an assumed one");
                Assert.AreEqual("UDP", host["OSC_TRANSPORT"]);
                Assert.AreEqual(query.ServiceName, host["NAME"]);
                var avatar = (Dictionary<string, object>)FTEmulatorJson.Parse(Get(root + "/avatar"));
                Assert.AreEqual("local:first", ((List<object>)Node(avatar, "change")["VALUE"])[0]);
                var missing = Assert.Throws<WebException>(() => Get(root + "/avatar/parameters/Missing"));
                Assert.AreEqual(HttpStatusCode.NotFound, ((HttpWebResponse)missing.Response).StatusCode);

                // Another avatar: the id, the capability and the parameters change together.
                query.ReplaceAvatar(new FTEmulatorAvatarAdvert("local:second", true, new List<FTEmulatorAvatarParameter>(), 2));
                avatar = (Dictionary<string, object>)FTEmulatorJson.Parse(Get(root + "/avatar"));
                Assert.AreEqual("local:second", ((List<object>)Node(avatar, "change")["VALUE"])[0]);
                Assert.AreEqual(true, ((List<object>)Node(avatar, "avatarfullfacetracking")["VALUE"])[0]);
                Assert.IsFalse(((Dictionary<string, object>)Node(avatar, "parameters")).ContainsKey("CONTENTS"));
                Assert.AreEqual(2, query.Avatar.Generation);
            }
            finally
            {
                int port = query.HttpPort;
                query.Stop();
                query.Stop();
                Assert.IsFalse(query.Running);
                Assert.Throws<WebException>(() => Get("http://127.0.0.1:" + port + "/avatar"), "nothing answers after Stop");
            }
        }

        [Test]
        public void Query_SendsAvatarChangeToThePortTheTrackingAppNames()
        {
            // A stand-in tracking app: an OSC socket on a port the system picked, and a HOST_INFO that names it.
            using (var appOsc = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                int oscPort = ((IPEndPoint)appOsc.Client.LocalEndPoint).Port;
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                int httpPort = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                var app = new HttpListener();
                app.Prefixes.Add("http://127.0.0.1:" + httpPort + "/");
                app.Start();
                var serve = new Thread(() =>
                {
                    try
                    {
                        HttpListenerContext request = app.GetContext();
                        byte[] body = Encoding.UTF8.GetBytes("{\"NAME\":\"VRCFT-test\",\"OSC_IP\":\"127.0.0.1\",\"OSC_PORT\":" + oscPort + ",\"OSC_TRANSPORT\":\"UDP\"}");
                        request.Response.OutputStream.Write(body, 0, body.Length);
                        request.Response.Close();
                    }
                    catch (Exception exception) when (exception is HttpListenerException || exception is ObjectDisposedException || exception is InvalidOperationException) { }
                }) { IsBackground = true };
                serve.Start();
                var query = new FTEmulatorOscQuery(new FTEmulatorDiagnostics());
                query.Start(9000, new FTEmulatorAvatarAdvert("local:avatar", true, Parameters(), 1), Active());
                try
                {
                    query.LearnPeer("VRCFT-test", httpPort);
                    appOsc.Client.ReceiveTimeout = 3000;
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    FTEmulatorOscMessage change = Decode(appOsc.Receive(ref from))[0];
                    Assert.AreEqual("/avatar/change", change.Address);
                    Assert.AreEqual("local:avatar", change.Arguments[0].Text);
                    FTEmulatorOscMessage native = Decode(appOsc.Receive(ref from))[0];
                    Assert.AreEqual("/avatar/avatarfullfacetracking", native.Address);
                    Assert.AreEqual('T', native.Arguments[0].Tag);
                    Assert.AreEqual(1, query.Peers.Count);
                    StringAssert.Contains(oscPort.ToString(), query.Peers[0]);
                    // A new avatar is announced to the same place.
                    query.ReplaceAvatar(new FTEmulatorAvatarAdvert("local:other", false, Parameters(), 2));
                    Assert.AreEqual("local:other", Decode(appOsc.Receive(ref from))[0].Arguments[0].Text);
                }
                finally
                {
                    query.Stop();
                    app.Close();
                    serve.Join(1000);
                }
            }
        }

        // ---- Diagnostics and JSON ----

        [Test]
        public void Diagnostics_AreBoundedAndExportWithoutPackets()
        {
            var diagnostics = new FTEmulatorDiagnostics();
            for (int i = 0; i < FTEmulatorDiagnostics.MaxCodes + 50; i++) diagnostics.Record("Code " + i, "context");
            Assert.AreEqual(FTEmulatorDiagnostics.MaxCodes, diagnostics.Snapshot().Count);
            diagnostics.Record("Code 0", "later");
            diagnostics.Add("Frames", 3);
            diagnostics.Set("Unity", "2022.3.22f1");
            string text = diagnostics.ExportText(new[] { "State: Live" });
            StringAssert.Contains("State: Live", text);
            StringAssert.Contains("Code 0 x2: context", text);
            StringAssert.Contains("latest: later", text);
            StringAssert.Contains("Frames: 3", text);
            StringAssert.Contains("Unity: 2022.3.22f1", text);
        }

        [Test]
        public void Json_RoundTripsAndIsBounded()
        {
            string text = FTEmulatorJson.Write(new Dictionary<string, object> { { "a", new List<object> { 1, 2.5f, true, null, "x\"y\n" } }, { "b", new Dictionary<string, object>() } });
            var parsed = (Dictionary<string, object>)FTEmulatorJson.Parse(text);
            var list = (List<object>)parsed["a"];
            Assert.AreEqual(1d, list[0]);
            Assert.AreEqual(2.5d, list[1]);
            Assert.AreEqual(true, list[2]);
            Assert.IsNull(list[3]);
            Assert.AreEqual("x\"y\n", list[4]);
            Assert.AreEqual("é", FTEmulatorJson.Parse("\"\\u00e9\""));
            Assert.IsFalse(FTEmulatorJson.TryParse("{\"a\":}", out object _, out string error));
            StringAssert.Contains("character", error);
            Assert.IsFalse(FTEmulatorJson.TryParse(new string('[', 200) + new string(']', 200), out object _, out error), "nested too deep");
            Assert.IsFalse(FTEmulatorJson.TryParse("[1] 2", out object _, out error));
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator OSC tests (assembly Riry.RiryTools.Editor.FTEmulatorTests: Unity Test Framework only)
// ============================================================================
//
// FTEmulatorOscTests (this file)
//   Codec_*                  Typed parameters, bundles in order and bounded nesting, malformed packets refused whole, the
//                            native blob (exact size, byte 36 is JawOpen), the status messages.
//   Receiver_*               The buffer under a flood (bounded, newest wins), malformed packets under one code, other
//                            machines refused, the Play Mode gate, a taken port, a packet over the socket, Stop during a
//                            blocking receive, restart on the freed port.
//   Dns_*                    The announcement VRCFaceTracking looks for, compressed names, broken packets.
//   Query_*                  The avatar tree and HOST_INFO, HTTP only while running, a whole-avatar swap, and /avatar/change
//                            sent to the OSC port a stand-in tracking app's HOST_INFO names.
//   Diagnostics_* / Json_*   Bounded codes and the export; JSON round trip and bounds.
//   Packets are built by hand here (Text, Int, FloatMessage, Bundle), so the decoder is not checked against the encoder alone.
