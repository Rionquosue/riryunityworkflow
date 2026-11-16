using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace Riry.RiryTools.Editor
{
    /// <summary>
    /// What the OSCQuery service says about the avatar: its id, whether it takes native face tracking, and the tree of
    /// its Expression Parameters, the parameters VRChat's OSC input can reach. Built once and never changed, so every
    /// reply about one avatar comes from the same snapshot; a new avatar is a new advert.
    /// </summary>
    internal sealed class FTEmulatorAvatarAdvert
    {
        private const int NoValue = 0, ReadOnly = 1, WriteOnly = 2, ReadWrite = 3;

        private readonly Dictionary<string, Dictionary<string, object>> nodes = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);

        internal readonly string Id;
        internal readonly bool FullFaceTracking;
        internal readonly int Generation;
        internal readonly int ParameterCount;

        internal FTEmulatorAvatarAdvert(string id, bool fullFaceTracking, IEnumerable<FTEmulatorAvatarParameter> parameters, int generation)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "local:RiryFTEmulator" : id;
            FullFaceTracking = fullFaceTracking;
            Generation = generation;
            Dictionary<string, object> root = Container("/");
            Dictionary<string, object> avatar = Child(root, "/avatar", "avatar");
            Leaf(avatar, FTEmulatorOscCodec.AvatarChangeAddress, "change", "s", ReadOnly, Id);
            Leaf(avatar, FTEmulatorOscCodec.FullFaceTrackingAddress, "avatarfullfacetracking", "T", ReadOnly, FullFaceTracking);
            Dictionary<string, object> list = Child(avatar, "/avatar/parameters", "parameters");
            foreach (FTEmulatorAvatarParameter parameter in parameters)
            {
                if (string.IsNullOrEmpty(parameter.Name)) continue;
                // A name with slashes is a path: FT/v2/JawOpen is parameters > FT > v2 > JawOpen.
                string[] parts = parameter.Name.Split('/');
                Dictionary<string, object> parent = list;
                string path = "/avatar/parameters";
                bool usable = true;
                for (int i = 0; i < parts.Length - 1 && usable; i++)
                {
                    if (parts[i].Length == 0) { usable = false; break; }
                    path += "/" + parts[i];
                    parent = nodes.TryGetValue(path, out Dictionary<string, object> existing) ? existing : Child(parent, path, parts[i]);
                }
                string last = parts[parts.Length - 1];
                if (!usable || last.Length == 0 || nodes.ContainsKey(path + "/" + last)) continue;
                switch (parameter.Type)
                {
                    case FTEmulatorValueType.Bool: Leaf(parent, path + "/" + last, last, "T", ReadWrite, false); break;
                    case FTEmulatorValueType.Int: Leaf(parent, path + "/" + last, last, "i", ReadWrite, 0); break;
                    default: Leaf(parent, path + "/" + last, last, "f", ReadWrite, 0f); break;
                }
                ParameterCount++;
            }
            if (FullFaceTracking)
            {
                Dictionary<string, object> tracking = Child(root, "/tracking", "tracking");
                Dictionary<string, object> face = Child(tracking, "/tracking/face", "face");
                Leaf(face, FTEmulatorOscCodec.NativeFaceAddress, "v1", "b", WriteOnly, null);
            }
        }

        private Dictionary<string, object> Container(string path)
        {
            var node = new Dictionary<string, object>(StringComparer.Ordinal) { { "FULL_PATH", path }, { "ACCESS", NoValue } };
            nodes[path] = node;
            return node;
        }

        private Dictionary<string, object> Child(Dictionary<string, object> parent, string path, string name)
        {
            Dictionary<string, object> node = Container(path);
            Contents(parent)[name] = node;
            return node;
        }

        private void Leaf(Dictionary<string, object> parent, string path, string name, string type, int access, object value)
        {
            var node = new Dictionary<string, object>(StringComparer.Ordinal) { { "FULL_PATH", path }, { "ACCESS", access }, { "TYPE", type } };
            if (value != null) node["VALUE"] = new List<object> { value };
            nodes[path] = node;
            Contents(parent)[name] = node;
        }

        private static Dictionary<string, object> Contents(Dictionary<string, object> node)
        {
            if (!node.TryGetValue("CONTENTS", out object contents)) node["CONTENTS"] = contents = new Dictionary<string, object>(StringComparer.Ordinal);
            return (Dictionary<string, object>)contents;
        }

        /// <summary>The JSON of the node at path ("/", "/avatar", "/avatar/parameters/FT/v2/JawOpen"), with everything under it.</summary>
        internal bool TryGet(string path, out string json)
        {
            json = null;
            if (!nodes.TryGetValue(path, out Dictionary<string, object> node)) return false;
            json = FTEmulatorJson.Write(node);
            return true;
        }
    }

    /// <summary>
    /// The OSCQuery compatibility service VRCFaceTracking finds the emulated avatar through. It does three things with
    /// System.Net only: answers multicast DNS questions for "_oscjson._tcp.local" with a service named like a VRChat
    /// client (VRCFaceTracking looks for names that start with "VRChat-Client"), serves the avatar tree and HOST_INFO
    /// as JSON over HTTP on 127.0.0.1, and finds the tracking app's own OSC port (its HOST_INFO) so /avatar/change can be
    /// sent there. Nothing is written to VRChat's OSC folders. Its threads touch no Unity object.
    /// </summary>
    internal sealed class FTEmulatorOscQuery : IDisposable
    {
        internal const string JsonService = "_oscjson._tcp.local";
        internal const string NamePrefix = "VRChat-Client-RiryFTEmulator-";
        private const int MdnsPort = 5353;
        private const int MaxPeers = 8;
        private const double AnnounceEvery = 15.0;
        private const double QueryEveryWaiting = 3.0;
        private const double QueryEveryFound = 30.0;
        private const double HostInfoEvery = 5.0;
        private static readonly IPAddress MdnsAddress = IPAddress.Parse("224.0.0.251");

        internal sealed class Peer
        {
            internal string Name;
            internal int HttpPort;
            internal IPEndPoint Osc;
            internal double Asked;
        }

        private readonly object gate = new object();
        private readonly FTEmulatorDiagnostics diagnostics;
        private readonly List<Peer> peers = new List<Peer>();
        private readonly List<UdpClient> senders = new List<UdpClient>();
        private readonly List<IPAddress> local = new List<IPAddress>();
        private volatile bool stopping;
        private volatile FTEmulatorAvatarAdvert advert;
        private HttpListener listener;
        private UdpClient mdns;
        private UdpClient oscSender;
        private Thread httpThread;
        private Thread mdnsThread;
        private int oscPort;

        internal FTEmulatorOscQuery(FTEmulatorDiagnostics diagnostics)
        {
            this.diagnostics = diagnostics;
        }

        internal bool Running { get; private set; }
        /// <summary>The advertised name: NamePrefix and six characters that change with every start.</summary>
        internal string ServiceName { get; private set; }
        internal int HttpPort { get; private set; }
        /// <summary>False when no network interface could carry the multicast: the service still answers HTTP.</summary>
        internal bool Discoverable { get; private set; }

        internal FTEmulatorAvatarAdvert Avatar
        {
            get { return advert; }
        }

        /// <summary>The tracking apps found, as "name at address:port".</summary>
        internal List<string> Peers
        {
            get
            {
                var result = new List<string>();
                lock (gate)
                {
                    foreach (Peer peer in peers)
                    {
                        if (peer.Osc != null) result.Add(peer.Name + " at " + peer.Osc);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Starts the HTTP service and the discovery. oscPort is the receiver's bound port, the one HOST_INFO reports.
        /// Refuses outside an active session (Play Mode).
        /// </summary>
        internal void Start(int oscPort, FTEmulatorAvatarAdvert avatar, FTEmulatorSessionContext context)
        {
            if (context == null || !context.Active()) throw new InvalidOperationException("The OSCQuery service only runs during Play Mode.");
            if (Running) return;
            this.oscPort = oscPort;
            advert = avatar;
            stopping = false;
            ServiceName = NamePrefix + Guid.NewGuid().ToString("N").Substring(0, 6);
            StartHttp();
            oscSender = new UdpClient(AddressFamily.InterNetwork);
            StartMdns();
            Running = true;
        }

        private void StartHttp()
        {
            Exception last = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int port;
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                var http = new HttpListener();
                try
                {
                    http.Prefixes.Add("http://127.0.0.1:" + port + "/");
                    http.Start();
                }
                catch (Exception exception) when (exception is HttpListenerException || exception is SocketException)
                {
                    last = exception;
                    http.Close();
                    continue;
                }
                listener = http;
                HttpPort = port;
                httpThread = new Thread(HttpLoop) { IsBackground = true, Name = "RiryTools FTEmulator OSCQuery" };
                httpThread.Start(http);
                return;
            }
            diagnostics?.Record(FTEmulatorDiagnostics.QueryFault, "HTTP service could not start: " + (last != null ? last.Message : "no free port"));
            throw new InvalidOperationException("The OSCQuery HTTP service could not start: " + (last != null ? last.Message : "no free port"), last);
        }

        private void HttpLoop(object state)
        {
            try
            {
                Serve((HttpListener)state);
            }
            catch (Exception exception)
            {
                // A thread must not end without a trace.
                if (!stopping) diagnostics?.Record(FTEmulatorDiagnostics.QueryFault, "HTTP service stopped: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private void Serve(HttpListener http)
        {
            while (!stopping)
            {
                HttpListenerContext request;
                try
                {
                    request = http.GetContext();
                }
                catch (Exception exception) when (exception is HttpListenerException || exception is ObjectDisposedException || exception is InvalidOperationException)
                {
                    if (!stopping) diagnostics?.Record(FTEmulatorDiagnostics.QueryFault, "HTTP service stopped: " + exception.Message);
                    return;
                }
                try
                {
                    int status = 405;
                    string body = request.Request.HttpMethod == "GET" ? Respond(request.Request.RawUrl, out status) : null;
                    if (body == null) body = "{}";
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body);
                    request.Response.StatusCode = status;
                    request.Response.ContentType = "application/json";
                    request.Response.ContentLength64 = bytes.Length;
                    request.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    request.Response.Close();
                }
                catch (Exception exception) when (exception is HttpListenerException || exception is IOException || exception is ObjectDisposedException)
                {
                    // The asker went away before the reply was written.
                }
            }
        }

        /// <summary>
        /// The reply to GET rawUrl: HOST_INFO when the query asks for it, else the node at that path. Null (status 404)
        /// for a path the avatar does not have. Every reply about the avatar comes from one advert.
        /// </summary>
        internal string Respond(string rawUrl, out int status)
        {
            status = 200;
            string path = rawUrl ?? "/";
            string query = "";
            int mark = path.IndexOf('?');
            if (mark >= 0)
            {
                query = path.Substring(mark + 1);
                path = path.Substring(0, mark);
            }
            if (query.IndexOf("HOST_INFO", StringComparison.Ordinal) >= 0)
            {
                return FTEmulatorJson.Write(new Dictionary<string, object>
                {
                    { "NAME", ServiceName ?? NamePrefix },
                    { "EXTENSIONS", new Dictionary<string, object> { { "ACCESS", true }, { "CLIPMODE", false }, { "RANGE", true }, { "TYPE", true }, { "VALUE", true } } },
                    { "OSC_IP", "127.0.0.1" },
                    { "OSC_PORT", oscPort },
                    { "OSC_TRANSPORT", "UDP" },
                });
            }
            try { path = Uri.UnescapeDataString(path); }
            catch (UriFormatException) { status = 404; return null; }
            if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal)) path = path.TrimEnd('/');
            if (path.Length == 0) path = "/";
            FTEmulatorAvatarAdvert current = advert;
            if (current != null && current.TryGet(path, out string json)) return json;
            status = 404;
            return null;
        }

        private void StartMdns()
        {
            local.Clear();
            local.Add(IPAddress.Loopback);
            var addresses = new List<IPAddress>();
            try
            {
                foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback || !network.SupportsMulticast) continue;
                    foreach (UnicastIPAddressInformation address in network.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        addresses.Add(address.Address);
                        break;
                    }
                }
            }
            catch (NetworkInformationException exception)
            {
                diagnostics?.Record(FTEmulatorDiagnostics.DiscoveryFault, "network interfaces could not be listed: " + exception.Message);
            }
            try
            {
                var receiver = new UdpClient(AddressFamily.InterNetwork);
                try { receiver.ExclusiveAddressUse = false; }
                catch (SocketException) { }
                receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                receiver.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
                receiver.Client.ReceiveTimeout = 500;
                foreach (IPAddress address in addresses)
                {
                    try
                    {
                        receiver.JoinMulticastGroup(MdnsAddress, address);
                        var sender = new UdpClient(AddressFamily.InterNetwork);
                        try { sender.ExclusiveAddressUse = false; }
                        catch (SocketException) { }
                        sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        // From port 5353 on the interface's own address: VRCFaceTracking only reads packets that come from one.
                        sender.Client.Bind(new IPEndPoint(address, MdnsPort));
                        sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, address.GetAddressBytes());
                        sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                        senders.Add(sender);
                        local.Add(address);
                    }
                    catch (SocketException exception)
                    {
                        diagnostics?.Record(FTEmulatorDiagnostics.DiscoveryFault, "interface " + address + ": " + exception.SocketErrorCode);
                    }
                }
                mdns = receiver;
            }
            catch (SocketException exception)
            {
                diagnostics?.Record(FTEmulatorDiagnostics.DiscoveryFault, "UDP port " + MdnsPort + " could not be shared: " + exception.SocketErrorCode);
            }
            Discoverable = mdns != null && senders.Count > 0;
            if (!Discoverable)
            {
                diagnostics?.Record(FTEmulatorDiagnostics.DiscoveryFault, "no network interface for multicast DNS: VRCFaceTracking cannot find FTEmulator");
                return;
            }
            mdnsThread = new Thread(MdnsLoop) { IsBackground = true, Name = "RiryTools FTEmulator discovery" };
            mdnsThread.Start(mdns);
        }

        private void MdnsLoop(object state)
        {
            try
            {
                Discovery((UdpClient)state);
            }
            catch (Exception exception)
            {
                // A thread must not end without a trace.
                if (!stopping) diagnostics?.Record(FTEmulatorDiagnostics.DiscoveryFault, "discovery stopped: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private void Discovery(UdpClient receiver)
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            double nextAnnounce = 0, nextQuery = 0;
            int announced = 0;
            while (!stopping)
            {
                double now = FTEmulatorClock.Now;
                if (now >= nextAnnounce)
                {
                    Multicast(Announcement(120));
                    // Three times a second apart at first, as announcements are made, then at intervals.
                    nextAnnounce = now + (++announced < 3 ? 1.0 : AnnounceEvery);
                }
                if (now >= nextQuery)
                {
                    Multicast(FTEmulatorDnsPacket.Query(JsonService));
                    bool found;
                    lock (gate) found = peers.Exists(peer => peer.Osc != null);
                    nextQuery = now + (found ? QueryEveryFound : QueryEveryWaiting);
                }
                byte[] data;
                try
                {
                    data = receiver.Receive(ref remote);
                }
                catch (SocketException exception)
                {
                    if (stopping) return;
                    if (exception.SocketErrorCode == SocketError.TimedOut || exception.SocketErrorCode == SocketError.WouldBlock ||
                        exception.SocketErrorCode == SocketError.ConnectionReset)
                        continue;
                    diagnostics?.Record(FTEmulatorDiagnostics.DiscoveryFault, "discovery stopped: " + exception.SocketErrorCode);
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                // Only this machine: the avatar is not offered to, or taken from, the rest of the network.
                if (stopping || !local.Contains(remote.Address)) continue;
                if (!FTEmulatorDnsPacket.TryParse(data, data.Length, out FTEmulatorDnsPacket packet)) continue;
                if (!packet.Response)
                {
                    foreach (FTEmulatorDnsPacket.Question question in packet.Questions)
                    {
                        if (!string.Equals(question.Name, JsonService, StringComparison.OrdinalIgnoreCase)) continue;
                        if (question.Type != FTEmulatorDnsPacket.TypePtr && question.Type != FTEmulatorDnsPacket.TypeAny) continue;
                        Multicast(Announcement(120));
                        break;
                    }
                    continue;
                }
                Discover(packet);
            }
        }

        private byte[] Announcement(int ttl)
        {
            return FTEmulatorDnsPacket.Announce(JsonService, ServiceName, ServiceName + ".local", HttpPort, IPAddress.Loopback, ttl);
        }

        private void Multicast(byte[] packet)
        {
            var target = new IPEndPoint(MdnsAddress, MdnsPort);
            foreach (UdpClient sender in senders)
            {
                try { sender.Send(packet, packet.Length, target); }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }
        }

        // An answer for the OSCQuery service type from this machine: another VRChat client, or a tracking app.
        private void Discover(FTEmulatorDnsPacket packet)
        {
            foreach (FTEmulatorDnsPacket.Record answer in packet.Answers)
            {
                if (answer.Type != FTEmulatorDnsPacket.TypePtr || answer.Target == null || !string.Equals(answer.Name, JsonService, StringComparison.OrdinalIgnoreCase)) continue;
                int dot = answer.Target.IndexOf('.');
                string instance = dot > 0 ? answer.Target.Substring(0, dot) : answer.Target;
                if (instance == ServiceName) continue;
                if (instance.StartsWith("VRChat-Client", StringComparison.Ordinal))
                {
                    diagnostics?.Record(FTEmulatorDiagnostics.OtherClient, instance + ": a tracking app may talk to it instead of FTEmulator");
                    continue;
                }
                FTEmulatorDnsPacket.Record service = packet.Additional.Find(record => record.Type == FTEmulatorDnsPacket.TypeSrv) ??
                    packet.Answers.Find(record => record.Type == FTEmulatorDnsPacket.TypeSrv);
                if (service == null || service.Port <= 0) continue;
                LearnPeer(instance, service.Port);
            }
        }

        /// <summary>
        /// A tracking app's OSCQuery service was seen at an HTTP port on this machine: its HOST_INFO is asked for (at most
        /// every HostInfoEvery seconds while it has not answered), which names its OSC port.
        /// </summary>
        internal void LearnPeer(string instance, int httpPort)
        {
            Peer peer;
            bool ask;
            lock (gate)
            {
                peer = peers.Find(item => item.Name == instance);
                if (peer == null)
                {
                    if (peers.Count >= MaxPeers) return;
                    peers.Add(peer = new Peer { Name = instance, Asked = -HostInfoEvery });
                }
                double now = FTEmulatorClock.Now;
                ask = peer.HttpPort != httpPort || peer.Osc == null && now - peer.Asked >= HostInfoEvery;
                peer.HttpPort = httpPort;
                if (ask) peer.Asked = now;
            }
            if (ask) AskHostInfo(peer);
        }

        // The tracking app's HOST_INFO names the OSC port it listens on: where /avatar/change goes. Not assumed to be 9001.
        private void AskHostInfo(Peer peer)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + peer.HttpPort + "/?HOST_INFO");
                request.Timeout = 1000;
                request.ReadWriteTimeout = 1000;
                request.Proxy = null;
                string text;
                using (WebResponse response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var buffer = new char[65536];
                    int read = reader.ReadBlock(buffer, 0, buffer.Length);
                    text = new string(buffer, 0, read);
                }
                Dictionary<string, object> info = FTEmulatorJson.Object(FTEmulatorJson.Parse(text), "HOST_INFO");
                int port = (int)FTEmulatorJson.Number(info, "OSC_PORT", 0);
                if (port <= 0 || port > 65535) return;
                bool changed;
                lock (gate)
                {
                    var endpoint = new IPEndPoint(IPAddress.Loopback, port);
                    changed = peer.Osc == null || !peer.Osc.Equals(endpoint);
                    peer.Osc = endpoint;
                }
                if (changed) NotifyAvatarChange();
            }
            catch (Exception exception) when (exception is WebException || exception is IOException || exception is FormatException || exception is InvalidCastException)
            {
                diagnostics?.Record(FTEmulatorDiagnostics.DiscoveryFault, peer.Name + " did not answer HOST_INFO: " + exception.Message);
            }
        }

        /// <summary>Replaces the avatar in one step, then tells the tracking apps the avatar changed.</summary>
        internal void ReplaceAvatar(FTEmulatorAvatarAdvert avatar)
        {
            advert = avatar;
            NotifyAvatarChange();
        }

        /// <summary>Sends /avatar/change and the native capability to every tracking app found, as VRChat does after an avatar loads.</summary>
        internal void NotifyAvatarChange()
        {
            FTEmulatorAvatarAdvert current = advert;
            UdpClient sender = oscSender;
            if (current == null || sender == null || stopping) return;
            List<byte[]> packets = FTEmulatorOscCodec.EncodeStatus(current.Id, current.FullFaceTracking);
            lock (gate)
            {
                foreach (Peer peer in peers)
                {
                    if (peer.Osc == null) continue;
                    foreach (byte[] packet in packets)
                    {
                        try { sender.Send(packet, packet.Length, peer.Osc); }
                        catch (SocketException) { }
                        catch (ObjectDisposedException) { }
                    }
                }
            }
        }

        /// <summary>Withdraws the service (an announcement that expires at once), closes every socket and waits for the threads.</summary>
        internal void Stop()
        {
            if (!Running && listener == null && mdns == null) return;
            if (Discoverable && !stopping) Multicast(Announcement(0));
            stopping = true;
            Running = false;
            Discoverable = false;
            HttpListener http = listener;
            listener = null;
            if (http != null)
            {
                try { http.Abort(); }
                catch (Exception exception) when (exception is HttpListenerException || exception is ObjectDisposedException) { }
            }
            Close(mdns);
            mdns = null;
            // The threads end first: the discovery thread sends on the sockets closed below.
            Join(ref httpThread);
            Join(ref mdnsThread);
            foreach (UdpClient sender in senders) Close(sender);
            senders.Clear();
            Close(oscSender);
            oscSender = null;
            lock (gate) peers.Clear();
            HttpPort = 0;
        }

        private static void Close(UdpClient socket)
        {
            if (socket == null) return;
            try { socket.Close(); }
            catch (SocketException) { }
        }

        private static void Join(ref Thread thread)
        {
            Thread worker = thread;
            thread = null;
            if (worker != null && worker != Thread.CurrentThread) worker.Join(1500);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator OSCQuery service
// ============================================================================
//
// FTEmulatorAvatarAdvert (this file)
//   Constructor(id, fullFaceTracking, parameters, generation)  The avatar tree: /avatar/change (the id), /avatar/parameters
//                            with one node per Expression Parameter (TYPE f, i or T; a name with slashes is a path),
//                            /avatar/avatarfullfacetracking, and /tracking/face/v1 when native face tracking is taken.
//   TryGet(path, out json)   A node's JSON (FULL_PATH, ACCESS, TYPE, VALUE, CONTENTS).
//   Id / FullFaceTracking / Generation / ParameterCount
// FTEmulatorOscQuery (this file)
//   Start(oscPort, avatar, context)  HTTP on 127.0.0.1 (a free port), discovery on UDP 5353 (shared), a service named
//                            NamePrefix + six characters. Only in an active session.
//   Respond(rawUrl, out status)  HOST_INFO (NAME, EXTENSIONS, OSC_IP, OSC_PORT, OSC_TRANSPORT) or a node; null and 404 for
//                            an unknown path.
//   LearnPeer(instance, httpPort)  A tracking app's service: asks its HOST_INFO for the OSC port it listens on (never
//                            assumed to be 9001) and notifies it when the port is new.
//   ReplaceAvatar(avatar) / NotifyAvatarChange()  Swaps the advert; sends /avatar/change and /avatar/avatarfullfacetracking
//                            to each tracking app's OSC port, read from its HOST_INFO.
//   Stop() / Dispose()       Announces the service with a lifetime of 0, closes the sockets, joins the threads (1.5 s each).
//   Running / ServiceName / HttpPort / Discoverable / Avatar / Peers  For the window's OSC panel.
//   Timing                   Announce three times a second apart, then every AnnounceEvery (15 s) and to every question;
//                            ask for tracking apps every QueryEveryWaiting (3 s), QueryEveryFound (30 s) once one answered;
//                            HOST_INFO at most every HostInfoEvery (5 s) per app; at most MaxPeers (8) apps.
//   Only packets from this machine's own addresses are read. Another "VRChat-Client" service is recorded as OtherClient.
