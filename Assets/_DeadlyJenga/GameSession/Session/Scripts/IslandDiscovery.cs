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
        [Serializable] public sealed class Message
        {
            public string product="DeadlyJenga-Islands", version, sender, name, code, target, kind, room;
            public int count;
        }
        public sealed class Entry { public Message message; public IPEndPoint endpoint; public float seen; }
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        readonly Dictionary<string,float> lastInvite=new Dictionary<string,float>();
        readonly UdpClient socket;
        readonly int port;
        float nextReply;
        bool scanRequested;
        string advertisedCode="";
        int advertisedCount;
        readonly List<IPAddress> broadcasts=new List<IPAddress>{IPAddress.Broadcast};
        readonly string version;
        public readonly string Id=Guid.NewGuid().ToString("N");
        public string Status {get;private set;}="Looking on this Wi-Fi network";
        public Entry Invitation {get;private set;}
        float invitationUntil, nextBeacon, nextInvite, inviteAgain;int inviteRepeats;string currentRoom="";
        Message repeatedInvite;
        public int LastInviteRecipients {get;private set;}
        public IReadOnlyList<Entry> Nearby=>entries.Values.Where(e=>!string.IsNullOrEmpty(e.message.code)).OrderBy(e=>e.message.name).ToArray();
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
        public int AvailablePlayers=>entries.Values.Count(e=>string.IsNullOrEmpty(e.message.code));
        public IslandDiscovery(string app,string protocol,int port=Port)
        {
            using(var hash=SHA256.Create())version=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(app+"/"+protocol)));
            socket=new UdpClient(AddressFamily.InterNetwork);socket.ExclusiveAddressUse=false;
            socket.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);
            socket.Client.Bind(new IPEndPoint(IPAddress.Any,port));this.port=((IPEndPoint)socket.Client.LocalEndPoint).Port;socket.EnableBroadcast=true;socket.Client.Blocking=false;
            try{foreach(var nic in NetworkInterface.GetAllNetworkInterfaces())if(nic.OperationalStatus==OperationalStatus.Up)
                foreach(var address in nic.GetIPProperties().UnicastAddresses)if(address.Address.AddressFamily==AddressFamily.InterNetwork&&address.IPv4Mask!=null)
                {var ip=address.Address.GetAddressBytes();var mask=address.IPv4Mask.GetAddressBytes();for(int i=0;i<4;i++)ip[i]=(byte)(ip[i]|~mask[i]);var target=new IPAddress(ip);if(!broadcasts.Contains(target))broadcasts.Add(target);}}
            catch(Exception){}
        }
        public void Tick(float now,string code,int count,bool hosting)
        {
            currentRoom=code??"";
            advertisedCode=hosting?currentRoom:"";
            advertisedCount=count;
            foreach(var id in entries.Where(p=>now-p.Value.seen>5).Select(p=>p.Key).ToArray())entries.Remove(id);
            if(now>invitationUntil)Invitation=null;
            if(scanRequested){scanRequested=false;Broadcast(Make("scan","",count));}
            if(now>=nextBeacon){nextBeacon=now+1;Broadcast(Make("presence",advertisedCode,count));}
            if(inviteRepeats>0&&now>=inviteAgain){inviteAgain=now+.7f;inviteRepeats--;Broadcast(repeatedInvite);}
            for(int i=0;i<32&&socket.Available>0;i++)
            {
                var endpoint=new IPEndPoint(IPAddress.Any,0);
                try{Receive(socket.Receive(ref endpoint),endpoint,now);}catch(SocketException){break;}
            }
        }
        Message Make(string kind,string code,int count,string target="")=>new Message{version=version,sender=Id,name=SystemInfo.deviceName.Substring(0,Math.Min(32,SystemInfo.deviceName.Length)),kind=kind,code=code,count=count,target=target,room=currentRoom};
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
                    if(endpoint!=null)Send(Encoding.UTF8.GetBytes(JsonUtility.ToJson(reply)),endpoint);
                    Broadcast(reply);
                }
                return true;
            }
            if(entries.Count>=64&&!entries.ContainsKey(m.sender))return false;
            var entry=new Entry{message=m,endpoint=endpoint,seen=now};
            if(m.kind=="presence")entries[m.sender]=entry;
            else if(!string.IsNullOrEmpty(m.code)&&m.code!=currentRoom&&(m.target==Id||m.target=="*")&&(!lastInvite.TryGetValue(m.sender,out float last)||now-last>10))
            {lastInvite[m.sender]=now;Invitation=entry;invitationUntil=now+20;}
            foreach(var id in lastInvite.Where(p=>now-p.Value>30).Select(p=>p.Key).ToArray())lastInvite.Remove(id);
            return true;
        }
        public byte[] EncodePresence(string code,int count)=>Encoding.UTF8.GetBytes(JsonUtility.ToJson(Make("presence",code,count)));
        public void Scan(){scanRequested=true;nextBeacon=0;}
        void Send(byte[] data,IPEndPoint endpoint)
        {
            try{socket.Send(data,data.Length,endpoint);}
            catch(SocketException e){Status="Wi-Fi discovery: "+e.SocketErrorCode+". Room codes still work.";}
        }
        void Broadcast(Message m)
        {
            var data=Encoding.UTF8.GetBytes(JsonUtility.ToJson(m));
            foreach(var address in broadcasts)Send(data,new IPEndPoint(address,port));
        }
        public void InviteAll(string code,int count)
        {
            float now=Time.unscaledTime;
            if(now<nextInvite||!IslandRoomAddress.TryParse(code,"us",out _,out _))return;
            currentRoom=code;
            nextInvite=now+5;
            LastInviteRecipients=0;
            foreach(var entry in entries.Values)
            {
                if(now-entry.seen>5||entry.message.room==code||entry.message.code==code||entry.endpoint==null)continue;
                LastInviteRecipients++;
                var direct=Make("invite",code,count,entry.message.sender);
                Send(Encoding.UTF8.GetBytes(JsonUtility.ToJson(direct)),entry.endpoint);
            }
            // A listener does not need to have sent a beacon before it can receive the invite.
            repeatedInvite=Make("invite",code,count,"*");
            Broadcast(repeatedInvite);
            inviteRepeats=3;inviteAgain=now+.7f;
            Status="Invite broadcast on this Wi-Fi. Share "+code+" if it does not appear.";
        }
        public void Dismiss()=>Invitation=null;
        public void Dispose()=>socket.Dispose();
    }
}
