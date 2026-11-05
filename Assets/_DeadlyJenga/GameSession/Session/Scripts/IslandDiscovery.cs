using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Discovery only. A received address always goes through the normal Fusion join path.
    public sealed class IslandDiscovery : IDisposable
    {
        public const int Port = 47738;
        static readonly IPAddress MulticastGroup = IPAddress.Parse("239.255.77.38");
        [Serializable] public sealed class Message
        {
            public string product="DeadlyJenga-Islands", version, sender, name, code, target, kind, room, invitation;
            public int count;
        }
        public sealed class Entry { public Message message; public IPEndPoint endpoint; public float seen; }
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        readonly Dictionary<string,(string invitation,float seen)> lastInvite=new Dictionary<string,(string,float)>();
        readonly UdpClient listener, sender;
        readonly HashSet<IPAddress> multicastInterfaces = new HashSet<IPAddress>();
        readonly int port;
        float nextReply, nextRefresh, nextExpiry;
        bool scanRequested;
        string advertisedCode="";
        int advertisedCount;
        readonly List<IPAddress> broadcasts=new List<IPAddress>{IPAddress.Broadcast};
        readonly string version;
        public readonly string Id=Guid.NewGuid().ToString("N");
        public string Status {get;private set;}="";
        public string InviteStatus {get;private set;}="";
        public Entry Invitation {get;private set;}
        float invitationUntil, nextBeacon, nextInvite, inviteAgain;int inviteRepeats;string currentRoom="";
        Message repeatedInvite;
        public int LastInviteRecipients {get;private set;}
        public IReadOnlyList<Entry> Nearby=>entries.Values.Where(e=>!string.IsNullOrEmpty(e.message.code))
            .GroupBy(e=>e.message.code).Select(room=>room.OrderByDescending(e=>e.seen).First())
            .OrderBy(e=>e.message.name).ThenBy(e=>e.message.code).ToArray();
        public IReadOnlyList<Entry> Joinable
        {
            get
            {
                var rooms=Nearby.Where(e=>e.message.code!=currentRoom&&e.message.code!=Invitation?.message.code);
                return Invitation!=null?rooms.Prepend(Invitation).ToArray():rooms.ToArray();
            }
        }
        public Entry JoinableAt(int page,int pageSize,int slot)
        {
            var list=Joinable;
            if(list.Count==0||slot<0||slot>=pageSize)return null;
            int pages=(list.Count+pageSize-1)/pageSize,index=(page%pages)*pageSize+slot;
            return index<list.Count?list[index]:null;
        }
        internal bool HasJoinable=>Invitation!=null||entries.Values.Any(entry=>
            !string.IsNullOrEmpty(entry.message.code)&&entry.message.code!=currentRoom);
        public int AvailablePlayers=>entries.Values.Count(e=>string.IsNullOrEmpty(e.message.code));
        public IslandDiscovery(string app,string protocol,int port=Port)
        {
            using(var hash=SHA256.Create())version=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(app+"/"+protocol)));
            listener = OpenSocket(port, shared: true);
            this.port = ((IPEndPoint)listener.Client.LocalEndPoint).Port;
            try { sender = OpenSocket(0, shared: false); }
            catch { listener.Dispose(); throw; }
        }

        static UdpClient OpenSocket(int port, bool shared)
        {
            var socket = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                if (shared)
                {
                    socket.ExclusiveAddressUse = false;
                    socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                }
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                socket.EnableBroadcast = true;
                socket.Client.Blocking = false;
                if (!shared)
                {
                    socket.Ttl = 1;
                    socket.MulticastLoopback = true;
                    socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
                }
                return socket;
            }
            catch { socket.Dispose(); throw; }
        }

        void RefreshInterfaces()
        {
            var localAddresses = new HashSet<IPAddress>();
            NetworkInterface[] interfaces;
            try { interfaces = NetworkInterface.GetAllNetworkInterfaces(); }
            catch (NetworkInformationException) { interfaces = Array.Empty<NetworkInterface>(); }
            catch (PlatformNotSupportedException) { interfaces = Array.Empty<NetworkInterface>(); }
            broadcasts.Clear();
            broadcasts.Add(IPAddress.Broadcast);
            foreach (var nic in interfaces)
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                try
                {
                    foreach (var address in nic.GetIPProperties().UnicastAddresses)
                    {
                        var ip = address.Address;
                        if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                        if (nic.SupportsMulticast) localAddresses.Add(ip);
                        var mask = address.IPv4Mask?.GetAddressBytes();
                        if (mask == null) continue;
                        var bytes = ip.GetAddressBytes();
                        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(bytes[i] | ~mask[i]);
                        var target = new IPAddress(bytes);
                        if (!target.Equals(ip) && !broadcasts.Contains(target)) broadcasts.Add(target);
                    }
                }
                catch (NetworkInformationException) { }
                catch (PlatformNotSupportedException) { }
            }

            if (localAddresses.Count == 0) localAddresses.Add(IPAddress.Any);
            foreach (var ip in multicastInterfaces.Where(ip=>!localAddresses.Contains(ip)).ToArray())
            {
                try { listener.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, new MulticastOption(MulticastGroup, ip)); }
                catch (SocketException) { }
                multicastInterfaces.Remove(ip);
            }
            foreach (var ip in localAddresses)
            {
                if (multicastInterfaces.Contains(ip)) continue;
                try
                {
                    listener.JoinMulticastGroup(MulticastGroup, ip);
                    multicastInterfaces.Add(ip);
                }
                catch (SocketException) { } // Some adapters only support broadcast.
            }
        }

        public void Tick(float now,string code,int count,bool hosting)
        {
            currentRoom=code??"";
            advertisedCode=hosting?currentRoom:"";
            advertisedCount=count;
            if (now >= nextExpiry)
            {
                nextExpiry = now + 1;
                foreach (var id in entries.Where(p=>now-p.Value.seen>5).Select(p=>p.Key).ToArray()) entries.Remove(id);
                foreach (var id in lastInvite.Where(p=>now-p.Value.seen>30).Select(p=>p.Key).ToArray()) lastInvite.Remove(id);
            }
            if(now>invitationUntil || Invitation?.message.code == currentRoom) Invitation=null;
            if (now >= nextRefresh || scanRequested)
            {
                nextRefresh = now + 5;
                RefreshInterfaces();
            }
            if(scanRequested){scanRequested=false;SendToLocal(Make("scan","",count));}
            if(now>=nextBeacon){nextBeacon=now+1;SendToLocal(Make("presence",advertisedCode,count));}
            if(repeatedInvite!=null && repeatedInvite.code!=currentRoom)
            {
                repeatedInvite=null;
                inviteRepeats=0;
                InviteStatus="";
            }
            if(inviteRepeats>0&&now>=inviteAgain){inviteAgain=now+.7f;inviteRepeats--;SendToLocal(repeatedInvite);}
            Drain(listener, now);
            Drain(sender, now);
        }

        void Drain(UdpClient socket, float now)
        {
            for (int i = 0; i < 64; i++)
            {
                try
                {
                    if (!socket.Client.Poll(0, SelectMode.SelectRead)) break;
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    Receive(socket.Receive(ref endpoint), endpoint, now);
                }
                catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionReset ||
                    error.SocketErrorCode == SocketError.MessageSize || error.SocketErrorCode == SocketError.Interrupted) { }
                catch (SocketException error)
                {
                    if (error.SocketErrorCode != SocketError.WouldBlock) Report(error);
                    break;
                }
            }
        }

        Message Make(string kind,string code,int count,string target="")
        {
            string name = SystemInfo.deviceName;
            if (string.IsNullOrWhiteSpace(name)) name = "Nearby player";
            return new Message{version=version,sender=Id,name=name.Substring(0,Math.Min(32,name.Length)),
                kind=kind,code=code,count=count,target=target,room=currentRoom};
        }
        public bool Receive(byte[] bytes,IPEndPoint endpoint,float now)
        {
            if(bytes==null||bytes.Length==0||bytes.Length>1024)return false;
            Message m;
            try{m=JsonUtility.FromJson<Message>(Encoding.UTF8.GetString(bytes));}catch{return false;}
            if(m==null||m.product!="DeadlyJenga-Islands"||m.version!=version||m.sender==Id||m.sender==null||m.sender.Length!=32||m.name==null||m.name.Length>32||m.count<0||m.count>10)return false;
            if(m.kind!="presence"&&m.kind!="invite"&&m.kind!="scan")return false;
            if(!string.IsNullOrEmpty(m.code)&&!IslandRoomAddress.TryParse(m.code,"us",out _,out _))return false;
            if(m.kind=="scan")
            {
                if(now>=nextReply)
                {
                    nextReply=now+.35f;
                    var reply=Make("presence",advertisedCode,advertisedCount);
                    if(endpoint!=null)TrySend(Encode(reply),endpoint,out _);
                }
                return true;
            }
            if(entries.Count>=64&&!entries.ContainsKey(m.sender))return false;
            var entry=new Entry{message=m,endpoint=endpoint,seen=now};
            if(m.kind=="presence")entries[m.sender]=entry;
            else if(!string.IsNullOrEmpty(m.code)&&m.code!=currentRoom&&(m.target==Id||m.target=="*"))
            {
                if(lastInvite.TryGetValue(m.sender,out var last)&&
                    (string.IsNullOrEmpty(m.invitation)?now-last.seen<10:last.invitation==m.invitation))return true;
                lastInvite[m.sender]=(m.invitation,now);
                Invitation=entry;
                invitationUntil=now+60;
            }
            return true;
        }
        static byte[] Encode(Message message) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
        public byte[] EncodePresence(string code,int count)=>Encode(Make("presence",code,count));
        public void Scan(){scanRequested=true;nextBeacon=0;}

        void Report(SocketException error) => Status = "Local discovery: " + error.SocketErrorCode + ". You can still enter a room code.";

        bool TrySend(byte[] data, IPEndPoint endpoint, out SocketException error)
        {
            try
            {
                sender.Send(data,data.Length,endpoint);
                error = null;
                return true;
            }
            catch (SocketException failure)
            {
                error = failure;
                return false;
            }
        }

        bool SendToLocal(Message message)
        {
            var data = Encode(message);
            var multicast = new IPEndPoint(MulticastGroup, port);
            bool sent = false;
            SocketException error = null;
            foreach (var ip in multicastInterfaces)
            {
                try
                {
                    sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, ip.GetAddressBytes());
                    sent |= TrySend(data, multicast, out error);
                }
                catch (SocketException failure) { error = failure; }
            }
            foreach (var broadcast in broadcasts)
                sent |= TrySend(data, new IPEndPoint(broadcast, port), out error);
            if (sent) Status = "";
            else if (error != null) Report(error);
            return sent;
        }
        public void InviteAll(string code,int count)
        {
            float now=Time.unscaledTime;
            if(now<nextInvite||!IslandRoomAddress.TryParse(code,"us",out _,out _))return;
            currentRoom=code;
            nextInvite=now+5;
            LastInviteRecipients=0;
            var invitation=Guid.NewGuid().ToString("N");
            foreach(var entry in entries.Values)
            {
                if(now-entry.seen>5||entry.message.room==code||entry.message.code==code||entry.endpoint==null)continue;
                var direct=Make("invite",code,count,entry.message.sender);
                direct.invitation=invitation;
                if (TrySend(Encode(direct),entry.endpoint,out _)) LastInviteRecipients++;
            }
            // A listener does not need to have sent a beacon before it can receive the invite.
            repeatedInvite=Make("invite",code,count,"*");
            repeatedInvite.invitation=invitation;
            bool sent=SendToLocal(repeatedInvite) || LastInviteRecipients>0;
            inviteRepeats=3;inviteAgain=now+.7f;
            InviteStatus=sent?"Invite sent on this local network. Room code: "+code:
                "Could not send an invite on this network. Share room code "+code+".";
        }
        public void Dismiss()=>Invitation=null;
        public void Dispose(){listener.Dispose();sender.Dispose();}
    }
}
