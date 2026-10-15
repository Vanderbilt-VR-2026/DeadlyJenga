using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DeadlyJenga.CastlePrototype;

static class TransportChecks
{
    static int passed;
    static void Check(bool ok, string description)
    {
        if (!ok) throw new Exception(description);
        passed++; Console.WriteLine("PASS " + description);
    }
    static bool Until(Func<bool> predicate, int milliseconds = 5000)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < milliseconds) { if (predicate()) return true; Thread.Sleep(5); }
        return predicate();
    }
    static byte[] Take(CastleLanTransport.Link link)
    {
        byte[] data = null;
        if (!Until(() => (data = link.TakeState()) != null)) throw new Exception("No state received");
        return data;
    }
    static int Main()
    {
        try
        {
            using var host = new CastleLanTransport();
            using var joiner = new CastleLanTransport();
            host.Advertise(new byte[] { 42, 15, 77 }, IPAddress.Loopback);
            Check(Until(() => host.Discovery.Count + joiner.Discovery.Count > 0), "Local discovery socket receives UDP advertisement");
            CastleLanTransport.Link outbound = null, inbound = null;
            string error = null;
            joiner.Connect(IPAddress.Loopback, host.Port, (link, e) => { error = e; Volatile.Write(ref outbound, link); });
            Check(Until(() => Volatile.Read(ref outbound) != null || error != null) && error == null, "Direct connection needs no cloud service");
            Check(Until(() => host.Accepted.TryDequeue(out inbound)), "Running host accepts a late connection");
            outbound.Send(new byte[] { 1, 99 }); outbound.Send(new byte[] { 2, 98 });
            Check(Until(() => inbound.Controls.Count == 2), "Reliable control messages arrive");
            inbound.Controls.TryDequeue(out var first); inbound.Controls.TryDequeue(out var second);
            Check(first[0] == 1 && second[0] == 2, "Handshake control order is preserved");
            var world = Enumerable.Range(0, 160000).Select(i => (byte)(i % 251)).ToArray(); world[0] = 3;
            inbound.Send(world, true);
            Check(Take(outbound).SequenceEqual(world), "Large late-join snapshot survives TCP fragmentation");
            outbound.Send(new byte[] { 4, 11, 22 }, true);
            Check(Take(inbound).SequenceEqual(new byte[] { 4, 11, 22 }), "Player input travels back to the host");
            var time = Stopwatch.StartNew();
            for (int i = 0; i < 500; i++) inbound.Send(new byte[] { 3, (byte)i, (byte)(i >> 8) }, true);
            Check(time.ElapsedMilliseconds < 500, "Publishing 500 updates never waits on a network write");
            Check(Until(() => { var state = outbound.TakeState(); return state != null && state[1] + (state[2] << 8) == 499; }), "Latest snapshot wins after a burst; no permanent state gap");
            using var another = new CastleLanTransport();
            CastleLanTransport.Link otherOut = null, otherIn = null;
            another.Connect(IPAddress.Loopback, host.Port, (link, e) => Volatile.Write(ref otherOut, link));
            Check(Until(() => Volatile.Read(ref otherOut) != null && host.Accepted.TryDequeue(out otherIn)), "A second late joiner connects while first remains active");
            inbound.Send(new byte[] { 3, 44 }, true); otherIn.Send(new byte[] { 3, 55 }, true);
            Check(Take(outbound)[1] == 44 && Take(otherOut)[1] == 55, "Each peer has an independent state stream");
            using var bad = new TcpClient(); bad.Connect(IPAddress.Loopback, host.Port);
            CastleLanTransport.Link badLink = null;
            Check(Until(() => host.Accepted.TryDequeue(out badLink)), "Malformed-frame test connected");
            byte[] badLength = BitConverter.GetBytes(CastleLanTransport.MaxPacket + 1);
            bad.GetStream().Write(badLength, 0, badLength.Length);
            Check(Until(() => badLink.Closed), "Oversized frame is rejected before allocation");
            outbound.Dispose();
            Check(Until(() => inbound.Closed), "Disconnect promptly closes the remote link");
            otherOut.Send(new byte[] { 4, 66 }, true);
            Check(Take(otherIn)[1] == 66, "One disconnect does not interrupt another player");
            Console.WriteLine(passed + " checks passed"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + e); return 1; }
    }
}
