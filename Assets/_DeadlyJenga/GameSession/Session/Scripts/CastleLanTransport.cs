using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DeadlyJenga.CastlePrototype
{
    // No Unity calls on socket threads. Only the newest unsent state is retained,
    // so a slow peer cannot build an ever-growing queue or stall the game thread.
    public sealed class CastleLanTransport : IDisposable
    {
        public const int DiscoveryPort = 47771;
        public const int MaxPacket = 4 * 1024 * 1024;
        public readonly ConcurrentQueue<Datagram> Discovery = new ConcurrentQueue<Datagram>();
        public readonly ConcurrentQueue<Link> Accepted = new ConcurrentQueue<Link>();
        public readonly ConcurrentQueue<string> Errors = new ConcurrentQueue<string>();
        public sealed class Datagram { public byte[] Data; public IPEndPoint Endpoint; }
        readonly UdpClient udp;
        readonly TcpListener listener;
        readonly ConcurrentDictionary<Link, byte> links = new ConcurrentDictionary<Link, byte>();
        volatile bool disposed;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public CastleLanTransport()
        {
            try
            {
                udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                udp.EnableBroadcast = true;
                udp.Client.SendTimeout = 200;
                listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
                StartThread(ReceiveDiscovery); StartThread(Accept);
            }
            catch { Dispose(); throw; }
        }
        static void StartThread(ThreadStart work) => new Thread(work) { IsBackground = true, Name = "Castle LAN" }.Start();
        void ReceiveDiscovery()
        {
            while (!disposed)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    var data = udp.Receive(ref endpoint);
                    if (data.Length <= 2048 && Discovery.Count < 128) Discovery.Enqueue(new Datagram { Data = data, Endpoint = endpoint });
                }
                catch (Exception e) { if (!disposed) Report(e); break; }
            }
        }
        void Accept()
        {
            while (!disposed)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    if (Accepted.Count >= 32) { client.Close(); continue; }
                    var link = new Link(client); Track(link);
                    if (disposed) { link.Dispose(); break; }
                    Accepted.Enqueue(link);
                }
                catch (Exception e) { if (!disposed) Report(e); break; }
            }
        }
        void Report(Exception e) { if (Errors.Count < 8) Errors.Enqueue(e.Message); }
        void Track(Link link)
        {
            foreach (var entry in links.Keys) if (entry.Closed) links.TryRemove(entry, out _);
            links.TryAdd(link, 0);
        }
        public void Advertise(byte[] data, IPAddress target = null)
        {
            if (disposed) return;
            try { udp.Send(data, data.Length, new IPEndPoint(target ?? IPAddress.Broadcast, DiscoveryPort)); }
            catch (Exception e) { Report(e); }
        }
        public void Connect(IPAddress address, int port, Action<Link, string> completed)
        {
            StartThread(() =>
            {
                var client = new TcpClient(AddressFamily.InterNetwork);
                try
                {
                    var pending = client.BeginConnect(address, port, null, null);
                    using (pending.AsyncWaitHandle)
                        if (!pending.AsyncWaitHandle.WaitOne(4000)) throw new IOException("Connection timed out");
                    client.EndConnect(pending);
                    if (disposed) { client.Close(); return; }
                    var link = new Link(client); Track(link);
                    if (disposed) { link.Dispose(); return; }
                    completed(link, null);
                }
                catch (Exception e) { client.Close(); if (!disposed) completed(null, e.Message); }
            });
        }
        public void Dispose()
        {
            disposed = true; udp?.Close(); listener?.Stop();
            foreach (var link in links.Keys) link.Dispose();
        }

        public sealed class Link : IDisposable
        {
            public readonly ConcurrentQueue<byte[]> Controls = new ConcurrentQueue<byte[]>();
            readonly ConcurrentQueue<byte[]> outgoing = new ConcurrentQueue<byte[]>();
            readonly TcpClient client;
            readonly NetworkStream stream;
            readonly AutoResetEvent wake = new AutoResetEvent(false);
            byte[] latestOutgoing, latestIncoming;
            int closed;
            public bool Closed => Volatile.Read(ref closed) != 0;
            public string Error { get; private set; }
            public string Address => ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            public Link(TcpClient socket)
            {
                client = socket; client.NoDelay = true;
                client.SendTimeout = 4000; client.ReceiveTimeout = 12000;
                stream = client.GetStream();
                StartThread(Read); StartThread(Write);
            }
            public void Send(byte[] data, bool state = false)
            {
                if (Closed) return;
                if (data == null || data.Length == 0 || data.Length > MaxPacket) throw new InvalidDataException("Invalid LAN frame size");
                if (state) Interlocked.Exchange(ref latestOutgoing, data);
                else if (outgoing.Count < 32) outgoing.Enqueue(data);
                else { Dispose(); return; }
                wake.Set();
            }
            public byte[] TakeState() => Interlocked.Exchange(ref latestIncoming, null);
            void Read()
            {
                try
                {
                    using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                    while (!Closed)
                    {
                        int size = reader.ReadInt32();
                        if (size < 1 || size > MaxPacket) throw new InvalidDataException("Invalid LAN frame length");
                        var data = reader.ReadBytes(size);
                        if (data.Length != size) throw new EndOfStreamException();
                        if (data[0] == 3 || data[0] == 4) Interlocked.Exchange(ref latestIncoming, data);
                        else if (Controls.Count < 32) Controls.Enqueue(data);
                        else throw new InvalidDataException("Too many LAN control messages");
                    }
                }
                catch (Exception e) { Error = e.Message; }
                finally { Dispose(); }
            }
            void Write()
            {
                try
                {
                    using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                    while (!Closed)
                    {
                        if (!outgoing.TryDequeue(out var data)) data = Interlocked.Exchange(ref latestOutgoing, null);
                        if (data == null) { wake.WaitOne(500); continue; }
                        writer.Write(data.Length); writer.Write(data); writer.Flush();
                    }
                }
                catch (Exception e) { Error = e.Message; }
                finally { Dispose(); }
            }
            public void Dispose()
            {
                if (Interlocked.Exchange(ref closed, 1) != 0) return;
                client.Close(); wake.Set();
                // The event is retained until both worker stacks release this link.
                // Disposing it here races a pending writer's WaitOne/Set.
            }
        }
    }
}
