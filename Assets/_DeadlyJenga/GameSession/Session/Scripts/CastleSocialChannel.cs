using System;
using System.Collections.Generic;
namespace DeadlyJenga.CastlePrototype
{
    // Separate bounded voice stream: four in flight, eight queued audio frames.
    // Old speech is discarded instead of accumulating behind a slow connection.
    public sealed class CastleSocialChannel
    {
        sealed class Item { public byte[] data; public float at; }
        readonly Queue<Item> audio=new Queue<Item>(), control=new Queue<Item>();
        readonly HashSet<int> waiting=new HashSet<int>();
        int sequence;
        public int Pending=>audio.Count+control.Count;
        public int InFlight=>waiting.Count;
        public void Enqueue(byte[] data,float now)
        {
            if(data==null||data.Length<1||data.Length>16000||(data[0]!=5&&data[0]!=6))return;
            var queue=data[0]==5?audio:control;int limit=data[0]==5?8:32;
            if(queue.Count>=limit)queue.Dequeue();queue.Enqueue(new Item{data=data,at=now});
        }
        public void Ack(int serial)=>waiting.Remove(serial);
        public void Pump(float now,Action<int,byte[]> send)
        {
            while(audio.Count>0&&now-audio.Peek().at>.5f)audio.Dequeue();
            while(waiting.Count<4&&(control.Count>0||audio.Count>0))
            {var item=control.Count>0?control.Dequeue():audio.Dequeue();int id=++sequence;if(id==0)id=++sequence;waiting.Add(id);send(id,item.data);}
        }
    }
}
