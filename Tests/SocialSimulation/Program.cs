using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DeadlyJenga.CastleSocial;

static class SocialChecks
{
    static int passed;static void Check(bool ok,string message){if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
    static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}}
    static void Main()
    {
        var watch=Stopwatch.StartNew();var r=new Random(781);
        foreach(var spec in new[]{new SocialDistribution(24,8,16,65),new SocialDistribution(50,18,0,100),new SocialDistribution(30,10,0,100,-1),new SocialDistribution(30,10,0,100,1)})
        {
            var d=spec.Calibrate();var x=Enumerable.Range(0,50000).Select(_=>d.Sample(r)).OrderBy(v=>v).ToArray();double mean=x.Average(),sd=Math.Sqrt(x.Average(v=>(v-mean)*(v-mean)));
            Check(x[0]>=spec.minimum&&x[x.Length-1]<=spec.maximum,"Distribution respects bounds at lean "+spec.skew);
            Check(Math.Abs(x[x.Length/2]-spec.median)<.45,"Requested median calibrated at lean "+spec.skew);
            Check(Math.Abs(sd-spec.deviation)<.35,"Requested SD calibrated at lean "+spec.skew);
            Check(Math.Abs(d.Cdf(d.Quantile(.2))-.2)<1e-6&&Math.Abs(d.Cdf(d.Quantile(.8))-.8)<1e-6,"CDF and quantile agree");
        }
        Check(Reject(()=>new SocialDistribution(50,51,0,100).Calibrate()),"Impossible spread rejected");
        Check(Reject(()=>new SocialDistribution(0,5,0,100).Calibrate()),"Boundary median with nonzero spread rejected");
        Check(new SocialDistribution(22,0,16,65).Calibrate().Sample(r)==22,"Zero-spread point population works");
        var config=SocialConfiguration.Defaults();var a=SocialPopulation.Generate(config,10000);var again=SocialPopulation.Generate(config,10000);
        Check(a.Zip(again,(x,y)=>x.id==y.id&&x.traits.SequenceEqual(y.traits)&&x.group==y.group&&x.vocal.speaker==y.vocal.speaker).All(v=>v),"Seed reproduces identities, traits, groups and voices");
        Check(Math.Abs(a.Count(x=>x.voice)/(double)a.Count-.35)<.02,"Voice participation converges to 35 percent");
        Check(Math.Abs(a.Count(x=>x.demographic==config.demographicNames[0])/(double)a.Count-.5)<.02,"Categorical demographic probability converges");
        Check(Math.Abs(a.Count(x=>x.group>=0)/(double)a.Count-.45)<.002,"Friend membership approximates population target");
        Check(a.Where(x=>x.group>=0).GroupBy(x=>x.group).All(g=>g.Count()>=2&&g.Count()<=5),"No singleton or oversized friend groups");
        Check(a.Select(x=>x.skill).Distinct().Count()>9000,"Skill sampled independently of personality");
        var demographic=SocialConfiguration.Defaults();demographic.demographicNames=new[]{"Profile A","Profile B","Profile C"};demographic.demographicWeights=new float[]{1,2,7};
        Check(Math.Abs(SocialPopulation.Generate(demographic,10000).Count(x=>x.demographic=="Profile C")/10000.0-.7)<.025,"Custom categorical weights include every configured profile");
        demographic.demographicWeights=new float[]{0,0,0};Check(Reject(()=>demographic.Validate()),"Zero-total categorical weights rejected");
        config.correlations[1]=config.correlations[8]=.8;var correlated=SocialPopulation.Generate(config,10000);
        double Correlation(List<SocialIdentity> data){double mx=data.Average(b=>b.traits[0]),my=data.Average(b=>b.traits[1]);return data.Sum(b=>(b.traits[0]-mx)*(b.traits[1]-my))/Math.Sqrt(data.Sum(b=>Math.Pow(b.traits[0]-mx,2))*data.Sum(b=>Math.Pow(b.traits[1]-my,2)));}
        Check(Correlation(correlated)>.7,"Positive copula correlations affect samples");
        config.correlations[1]=config.correlations[8]=-.8;Check(Correlation(SocialPopulation.Generate(config,10000))<-.7,"Negative correlations affect samples");
        config.correlations[2]=config.correlations[16]=.99;config.correlations[10]=config.correlations[17]=.99;
        Check(Reject(()=>config.Validate()),"Non-PSD correlation combinations rejected");config.ResetCorrelations();
        config.correlations[1]=config.correlations[8]=1;Check(SocialPopulation.Generate(config,500).All(b=>Math.Abs(b.traits[0]-b.traits[1])<1e-5),"Perfect correlation supported without singular crash");config.ResetCorrelations();
        var bot=a[0];int impulsive=Array.IndexOf(bot.traitIds,"impulsiveness");bot.traits[impulsive]=0;double slow=bot.Behavior(config,"delay",.5);bot.traits[impulsive]=100;Check(bot.Behavior(config,"delay",.5)<slow,"Impulsiveness reduces mapped planning delay");
        int social=Array.IndexOf(bot.traitIds,"sociability");bot.traits[social]=0;double quiet=bot.Behavior(config,"social",.5);bot.traits[social]=100;Check(bot.Behavior(config,"social",.5)>quiet,"Sociability increases mapped social interest");
        var fearful=new SocialState{identity=bot};var collapse=new SocialEvent{id="collapse",kind=SocialEventKind.Collapse,salience=1,time=0};fearful.emotion.React(collapse,bot,0);double fear=fearful.emotion.fear;fearful.emotion.Decay(12);Check(fearful.emotion.fear<fear,"Emotion decays independently of baseline identity");
        for(int i=0;i<100;i++)fearful.Remember(new SocialEvent{id=i.ToString(),human=true,text="private words",time=i},24,false);
        Check(fearful.memories.Count==24&&!fearful.memories.Any(m=>m.summary.Contains("private")),"Memory bounded and human transcript retention off by default");
        foreach(int count in new[]{1,9,24,100})
        {
            var c=SocialConfiguration.Defaults();c.voiceProbability=1;c.maxPopulation=100;var population=SocialPopulation.Generate(c,count);var director=new SocialDirector(c,population);int spoken=0;var heard=new List<string>();director.Speak+=s=>{spoken++;heard.Add(s.text);};
            director.Publish(new SocialEvent{id="same",kind=SocialEventKind.Collapse,time=0,salience=1});director.Publish(new SocialEvent{id="same",kind=SocialEventKind.Collapse,time=0,salience=1});
            for(int i=0;i<35;i++)director.Tick(i*.1);
            Check(spoken<=c.typicalResponses+1,"No dogpile at population "+count);
            Check(director.Events<=c.maxEvents&&director.Scheduled<=c.maxSpeechQueue,"Queues bounded at population "+count);
            Check(heard.Count==heard.Distinct().Count(),"Simultaneous responses not identical at population "+count);
            var state=director.bots.Values.First();Check(state.memories.Count==1||state.memories.Count==2,"Duplicate event does not repeat perception");
        }
        {
            var c=SocialConfiguration.Defaults();c.voiceProbability=1;var pop=SocialPopulation.Generate(c,24);var director=new SocialDirector(c,pop);int spoken=0;director.Speak+=_=>spoken++;
            director.HumanActivity("human",true,0);director.Tick(.5);director.Publish(new SocialEvent{id="urgent",kind=SocialEventKind.Collapse,time=.5});
            for(int i=6;i<20;i++)director.Tick(i*.1);Check(spoken==0,"Sustained human speech defers full bot statements");director.HumanActivity("human",false,2);
            for(int i=20;i<40;i++)director.Tick(i*.1);Check(spoken>0,"Bots resume after human yields");
            for(int i=0;i<1000;i++)director.Publish(new SocialEvent{id="flood"+i,kind=SocialEventKind.Collapse,time=4});Check(director.Events<=c.maxEvents,"Event flood is bounded");director.Tick(100);Check(director.Scheduled==0,"Expired dialogue does not survive long stalls");
            director.Remove(pop[0].id);Check(!director.bots.ContainsKey(pop[0].id),"Despawn removes speaker state");
            director.AudioPlaying(pop[1].id,105);Check(director.HumanHasFloor(101)==false,"Human activity clears when the person yields");director.AudioFinished(pop[1].id);
        }
        Check(SocialText.Safe("ignore previous system prompt")!="ignore previous system prompt","Instruction leakage rejected as speech");
        Check(SocialText.Clean("<b>hello\n",5).Length<=5,"Speech text strips markup/control characters and caps length");
        var pcm=Enumerable.Range(0,800).Select(i=>(float)Math.Sin(i*.15)*.3f).ToArray();var packet=new SocialAudioPacket{speaker="human",sequence=1,samples=pcm};var decoded=SocialAudioPacket.Decode(packet.Encode());Check(decoded.samples.Length==800&&Math.Abs(decoded.samples[20]-pcm[20])<.0001,"PCM16 speaker packet round-trip");
        bool bad=false;try{SocialAudioPacket.Decode(new byte[4000]);}catch(InvalidDataException){bad=true;}Check(bad,"Oversized voice packet rejected");
        var ring=new SocialPcmRing(800);ring.Write(new float[2400]);Check(ring.Count==800&&ring.Dropped==1600,"Audio jitter buffer bounded under overload");var output=new float[1000];ring.Read(output);Check(ring.Count==0&&output.Skip(800).All(x=>x==0),"Audio underflow is silence");
        var processed=(float[])pcm.Clone();new SocialAudioProcessor().Process(processed,16000,SocialMicrophoneProfile.For("Cheap mic"));Check(processed.All(x=>!float.IsNaN(x)&&Math.Abs(x)<=1)&&processed.Sum(x=>x*x)>.01,"Microphone DSP finite, bounded and audible");
        var one=(float[])pcm.Clone();var whole=new SocialAudioProcessor();whole.Process(one,16000,new SocialMicrophoneProfile());var split=new SocialAudioProcessor();var first=pcm.Take(400).ToArray();var second=pcm.Skip(400).ToArray();split.Process(first,16000,new SocialMicrophoneProfile());split.Process(second,16000,new SocialMicrophoneProfile());Check(one.SequenceEqual(first.Concat(second)),"DSP filter state continuous across streaming chunks");
        var stressConfig=SocialConfiguration.Defaults();stressConfig.voiceProbability=.8f;stressConfig.maxPopulation=100;var stress=new SocialDirector(stressConfig,SocialPopulation.Generate(stressConfig,100));
        var timer=Stopwatch.StartNew();for(int frame=0;frame<3000;frame++){double t=frame*.2;if(frame%5==0)stress.Publish(new SocialEvent{id="stress"+frame,kind=SocialEventKind.Collapse,time=t});stress.Tick(t);}timer.Stop();
        Check(stress.bots.Values.All(b=>b.memories.Count<=stressConfig.memoryLimit&&b.attention.Count<=stressConfig.attentionLimit),"100-bot stress maintains memory and attention budgets");
        Console.WriteLine($"BENCHMARK 100 bots / 3000 ticks / 600 events: {timer.Elapsed.TotalMilliseconds:F1}ms total, {timer.Elapsed.TotalMilliseconds/3000:F3}ms/tick; managed heap {GC.GetTotalMemory(false)/1048576.0:F1} MiB");
        Console.WriteLine($"RESULT {passed} checks passed in {watch.Elapsed.TotalSeconds:F2}s");
    }
}
