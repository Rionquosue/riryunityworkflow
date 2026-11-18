using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Riry.RiryTools.Editor
{
    /// <summary>The receive port could not be bound because another program has it. FTEmulator never takes a port over.</summary>
    internal sealed class FTEmulatorPortInUseException : InvalidOperationException
    {
        internal readonly int Port;

        internal FTEmulatorPortInUseException(int port, Exception inner)
            : base("UDP port " + port + " on 127.0.0.1 is in use by another program (VRChat, or Gesture Manager's own OSC, listen there too). " +
                "Close it or choose another port; VRCFaceTracking must send to the same port.", inner)
        {
            Port = port;
        }
    }

    /// <summary>
    /// The VRCFT OSC source: a UDP socket on 127.0.0.1, a thread that receives and decodes, and the small buffer that
    /// hands the newest input to the main thread. Socket, thread and buffer are one type under one lock, so stopping
    /// cannot race a packet in flight. The thread touches no Unity object.
    /// The buffer is bounded: one newest native face frame, and the newest value per parameter address (at most
    /// MaxAddresses); a flood while the Editor stalls costs no memory and the newest values win. A bundle's messages
    /// are published together.
    /// </summary>
    internal sealed class FTEmulatorOscReceiver : IFTEmulatorSource
    {
        internal const int MaxAddresses = 2048;
        internal const string NativeKind = "Native face blob";
        internal const string LegacyKind = "Legacy parameters";

        private readonly object gate = new object();
        private readonly int requestedPort;
        private readonly int keyCount;
        private readonly FTEmulatorDiagnostics diagnostics;
        // Pending input, replaced by newer input and taken by TryRead.
        private readonly Dictionary<string, FTEmulatorParameterValue> pendingParameters = new Dictionary<string, FTEmulatorParameterValue>(StringComparer.Ordinal);
        private readonly float[] pendingFace;
        private bool hasFace;
        private double pendingTime;

        private UdpClient client;
        private Thread thread;
        private volatile bool stopping;
        private FTEmulatorSessionContext context;
        private long sequence;
        // Statistics, read by the window.
        private long received;
        private long rejected;
        private double lastTime = -1;
        private string lastKind;
        private string sender;
        private double rateStart;
        private int rateCount;
        private int rate;

        internal FTEmulatorOscReceiver(int port, int keyCount, FTEmulatorDiagnostics diagnostics)
        {
            requestedPort = port;
            this.keyCount = keyCount;
            this.diagnostics = diagnostics;
            pendingFace = new float[keyCount];
        }

        public string Name
        {
            get { return "VRCFT OSC"; }
        }

        public bool Running
        {
            get { return client != null; }
        }

        /// <summary>The port bound (the one asked for, or the one the system gave for port 0); 0 when not running.</summary>
        internal int Port { get; private set; }

        internal long Received { get { lock (gate) return received; } }
        internal long Rejected { get { lock (gate) return rejected; } }
        /// <summary>FTEmulatorClock time of the last accepted packet; below 0 when none arrived.</summary>
        internal double LastTime { get { lock (gate) return lastTime; } }
        internal string LastKind { get { lock (gate) return lastKind; } }
        internal string Sender { get { lock (gate) return sender; } }

        /// <summary>Packets accepted in the last full second.</summary>
        internal int PacketsPerSecond
        {
            get { lock (gate) return FTEmulatorClock.Now - rateStart > 2.0 ? 0 : rate; }
        }

        /// <summary>
        /// Binds 127.0.0.1 and starts receiving. Refuses outside an active session (Play Mode). Throws
        /// FTEmulatorPortInUseException when the port is taken.
        /// </summary>
        public void Start(FTEmulatorSessionContext context)
        {
            if (context == null || !context.Active()) throw new InvalidOperationException("The OSC receiver only runs during Play Mode.");
            if (client != null) return;
            var socket = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                // Never share the port: a second listener would split the packets.
                try { socket.ExclusiveAddressUse = true; }
                catch (SocketException) { }
                socket.Client.Bind(new IPEndPoint(IPAddress.Loopback, requestedPort));
            }
            catch (SocketException exception)
            {
                socket.Close();
                diagnostics?.Record(FTEmulatorDiagnostics.PortInUse, "127.0.0.1:" + requestedPort + " (" + exception.SocketErrorCode + ")");
                throw new FTEmulatorPortInUseException(requestedPort, exception);
            }
            this.context = context;
            stopping = false;
            Clear();
            client = socket;
            Port = ((IPEndPoint)socket.Client.LocalEndPoint).Port;
            thread = new Thread(Loop) { IsBackground = true, Name = "RiryTools FTEmulator OSC" };
            thread.Start(socket);
        }

        private void Loop(object state)
        {
            try
            {
                ReceiveLoop((UdpClient)state);
            }
            catch (Exception exception)
            {
                // A thread must not end without a trace.
                if (!stopping) diagnostics?.Record(FTEmulatorDiagnostics.ReceiverFault, exception.GetType().Name + ": " + exception.Message);
            }
        }

        private void ReceiveLoop(UdpClient socket)
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (!stopping)
            {
                byte[] data;
                try
                {
                    data = socket.Receive(ref remote);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException exception)
                {
                    // Stop closes the socket to end the blocking receive: that is not a fault.
                    if (stopping) return;
                    // Windows reports a failed send to a closed local port on the next receive; keep listening.
                    if (exception.SocketErrorCode == SocketError.ConnectionReset) continue;
                    diagnostics?.Record(FTEmulatorDiagnostics.ReceiverFault, exception.SocketErrorCode.ToString());
                    return;
                }
                if (stopping) return;
                FTEmulatorRecording.Capture(data, data.Length);
                Receive(data, data.Length, remote);
            }
        }

        /// <summary>Decodes one packet and publishes its input. False when the packet was refused. Also the tests' way in.</summary>
        internal bool Receive(byte[] data, int length, IPEndPoint from)
        {
            // Only this machine may drive the avatar.
            if (from != null && !IPAddress.IsLoopback(from.Address))
            {
                Reject(FTEmulatorDiagnostics.PacketRejected, "sender is not this machine");
                return false;
            }
            var messages = new List<FTEmulatorOscMessage>();
            if (!FTEmulatorOscCodec.TryDecode(data, length, messages, out string error))
            {
                Reject(FTEmulatorDiagnostics.PacketRejected, error);
                return false;
            }
            Publish(messages, from);
            return true;
        }

        private void Reject(string code, string context)
        {
            lock (gate) rejected++;
            diagnostics?.Record(code, context);
        }

        /// <summary>Puts a packet's messages into the buffer as one step. Messages for other addresses are ignored.</summary>
        internal void Publish(List<FTEmulatorOscMessage> messages, IPEndPoint from)
        {
            float[] face = null;
            string faceError = null;
            foreach (FTEmulatorOscMessage message in messages)
            {
                if (message.Address != FTEmulatorOscCodec.NativeFaceAddress) continue;
                var values = new float[keyCount];
                if (FTEmulatorOscCodec.TryDecodeNativeFace(message, values, out string error)) face = values;
                else faceError = error;
            }
            if (faceError != null) Reject(FTEmulatorDiagnostics.NativeBlobRejected, faceError);
            bool overflow = false;
            lock (gate)
            {
                double now = FTEmulatorClock.Now;
                received++;
                lastTime = now;
                if (from != null) sender = from.ToString();
                if (now - rateStart >= 1.0)
                {
                    rate = rateCount;
                    rateCount = 0;
                    rateStart = now;
                }
                rateCount++;
                if (face != null)
                {
                    Array.Copy(face, pendingFace, keyCount);
                    hasFace = true;
                    pendingTime = now;
                    lastKind = NativeKind;
                }
                foreach (FTEmulatorOscMessage message in messages)
                {
                    if (!FTEmulatorOscCodec.TryDecodeParameter(message, out FTEmulatorParameterValue value)) continue;
                    if (!pendingParameters.ContainsKey(value.Name) && pendingParameters.Count >= MaxAddresses)
                    {
                        overflow = true;
                        continue;
                    }
                    pendingParameters[value.Name] = value;
                    pendingTime = now;
                    if (face == null) lastKind = LegacyKind;
                }
            }
            if (overflow) diagnostics?.Record(FTEmulatorDiagnostics.AddressLimit, "more than " + MaxAddresses + " parameter addresses pending");
        }

        /// <summary>The newest input since the last read, stamped with the session's generation.</summary>
        public bool TryRead(out FTEmulatorExpressionFrame expressions, out FTEmulatorParameterFrame parameters)
        {
            expressions = null;
            parameters = null;
            FTEmulatorSessionContext current = context;
            if (current == null) return false;
            lock (gate)
            {
                if (!hasFace && pendingParameters.Count == 0) return false;
                if (hasFace)
                {
                    var valid = new bool[keyCount];
                    // The blob has no per-key confidence: every key it carries counts as observed.
                    for (int i = 0; i < keyCount; i++) valid[i] = true;
                    expressions = FTEmulatorExpressionFrame.Create(pendingFace, valid, ++sequence, current.Generation, pendingTime);
                    hasFace = false;
                }
                if (pendingParameters.Count > 0)
                {
                    parameters = new FTEmulatorParameterFrame(new List<FTEmulatorParameterValue>(pendingParameters.Values), ++sequence, current.Generation, pendingTime);
                    pendingParameters.Clear();
                }
            }
            return expressions != null || parameters != null;
        }

        public void Clear()
        {
            lock (gate)
            {
                hasFace = false;
                pendingParameters.Clear();
            }
        }

        /// <summary>Closes the socket (which ends the blocking receive), waits for the thread and drops what was pending.</summary>
        public void Stop()
        {
            UdpClient socket = client;
            if (socket == null) return;
            stopping = true;
            client = null;
            Port = 0;
            try { socket.Close(); }
            catch (SocketException) { }
            Thread worker = thread;
            thread = null;
            if (worker != null && worker != Thread.CurrentThread) worker.Join(1000);
            Clear();
            context = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

// ============================================================================
// GLOSSARY: FTEmulator OSC receiver
// ============================================================================
//
// FTEmulatorOscReceiver (this file): IFTEmulatorSource
//   Start(context)           Binds 127.0.0.1:port (exclusive) and starts the receive thread; only in an active session.
//                            FTEmulatorPortInUseException when the port is taken (recorded as PortInUse).
//   Receive(data, length, from)  Decodes one packet and publishes it; refuses packets from another machine and malformed
//                            packets (PacketRejected). The thread calls it; tests call it directly.
//   Publish(messages, from)  One step under the lock: the newest native face frame, and the newest value per parameter
//                            address (at most MaxAddresses, 2048; more is AddressLimit). A wrong native message is
//                            NativeBlobRejected.
//   TryRead(out expressions, out parameters)  Takes what is pending, stamped with the session's generation.
//   Clear() / Stop() / Dispose()  Drops pending input; Stop closes the socket, joins the thread (1 s) and clears.
//   Port / Received / Rejected / LastTime / LastKind / Sender / PacketsPerSecond  For the window's OSC panel.
// FTEmulatorPortInUseException  The port is another program's; nothing is taken over.
