using System;
using System.IO;
using System.Text;

namespace DeadlyJenga.CastleSocial
{
    [Serializable] public sealed class SocialMicrophoneProfile
    {
        public float highPass=90,lowPass=10000,compression=.2f,saturation=.04f,noise=.0001f,gate=.002f,proximity=.1f,codec=.05f;
        public static SocialMicrophoneProfile For(string name)
        {
            var p=new SocialMicrophoneProfile();
            if(name=="Laptop"){p.highPass=220;p.lowPass=6500;p.noise=.0006f;p.compression=.4f;}
            if(name=="Cheap mic"){p.lowPass=5000;p.saturation=.12f;p.codec=.2f;}
            if(name=="Muffled")p.lowPass=2300;
            if(name=="Noisy mic"){p.noise=.001f;p.gate=.003f;}
            if(name=="Gaming headset"){p.compression=.35f;p.proximity=.2f;}return p;
        }
    }
    // Stateful per speaker, so chunk boundaries do not reset filters/noise/gate.
    public sealed class SocialAudioProcessor
    {
        double low,high,previous,envelope;uint noise=19321;
        public void Process(float[] data,int sampleRate,SocialMicrophoneProfile p)
        {
            double hp=Math.Exp(-2*Math.PI*Math.Max(0,p.highPass)/sampleRate),lp=1-Math.Exp(-2*Math.PI*Math.Max(50,p.lowPass)/sampleRate);
            for(int i=0;i<data.Length;i++)
            {
                double x=data[i];if(double.IsNaN(x)||double.IsInfinity(x))x=0;
                high=hp*(high+x-previous);previous=x;low+=lp*(high-low);envelope+=.02*(Math.Abs(low)-envelope);
                double gain=envelope<p.gate?Math.Max(0,envelope/Math.Max(.00001,p.gate)):1;
                x=low*gain*(1+p.proximity*.25)/(1+Math.Abs(low)*p.compression*2);
                x=Math.Tanh(x*(1+p.saturation*2))/(1+p.saturation);
                double quantized=Math.Round(x*2048)/2048;x=x*(1-p.codec)+quantized*p.codec;
                noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;x+=((noise&65535)/32767.5-1)*p.noise*gain;
                data[i]=(float)Math.Max(-1,Math.Min(1,x));
            }
        }
    }
    public sealed class SocialPcmRing
    {
        readonly float[] data;int read,write,count;readonly object sync=new object();public int Dropped {get;private set;}
        public SocialPcmRing(int samples){data=new float[Math.Max(64,samples)];}
        public int Count{get{lock(sync)return count;}}
        public void Write(float[] samples)
        {lock(sync)foreach(var value in samples){if(count==data.Length){read=(read+1)%data.Length;count--;Dropped++;}data[write]=value;write=(write+1)%data.Length;count++;}}
        public void Read(float[] output)
        {lock(sync)for(int i=0;i<output.Length;i++){output[i]=count>0?data[read]:0;if(count>0){read=(read+1)%data.Length;count--;}}}
        public void Clear(){lock(sync){read=write=count=0;}}
    }
    public sealed class SocialAudioPacket
    {
        public string speaker;public int sequence,sampleRate=16000;public bool end;public float[] samples;
        public byte[] Encode()
        {
            if(string.IsNullOrEmpty(speaker)||speaker.Length>96||samples==null||samples.Length>1600||sampleRate!=16000)throw new InvalidDataException("Invalid voice packet.");
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Encoding.UTF8))
            {w.Write((byte)5);w.Write(speaker);w.Write(sequence);w.Write(end);w.Write(samples.Length);foreach(float f in samples)w.Write((short)(Math.Max(-1,Math.Min(1,float.IsNaN(f)?0:f))*32767));return m.ToArray();}
        }
        public static SocialAudioPacket Decode(byte[] data)
        {
            if(data==null||data.Length>3500)throw new InvalidDataException("Oversized voice packet.");
            using(var r=new BinaryReader(new MemoryStream(data),Encoding.UTF8))
            {if(r.ReadByte()!=5)throw new InvalidDataException("Not audio.");var p=new SocialAudioPacket{speaker=r.ReadString(),sequence=r.ReadInt32(),end=r.ReadBoolean()};int n=r.ReadInt32();if(p.speaker.Length>96||n<0||n>1600||r.BaseStream.Length-r.BaseStream.Position!=n*2)throw new InvalidDataException("Invalid voice payload.");p.samples=new float[n];for(int i=0;i<n;i++)p.samples[i]=r.ReadInt16()/32768f;return p;}
        }
    }
}
