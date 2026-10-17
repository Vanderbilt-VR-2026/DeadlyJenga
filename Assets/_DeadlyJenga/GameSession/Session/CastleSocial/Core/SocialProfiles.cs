using System;
using System.Collections.Generic;
using System.Linq;

namespace DeadlyJenga.CastleSocial
{
    [Serializable] public sealed class SocialTrait
    {public string id,label,description;public SocialDistribution distribution=new SocialDistribution();public SocialTrait(){}public SocialTrait(string key,string text){id=key;label=text;description=text+" tendency; independent of skill.";}}
    [Serializable] public sealed class SocialMapping
    {
        public string behavior="risk",trait="boldness";public float weight=1;public float[] curve={0,.25f,.5f,.75f,1};
        public double Evaluate(double x){if(curve==null||curve.Length<2)return x;x=Math.Max(0,Math.Min(1,x))*(curve.Length-1);int i=Math.Min(curve.Length-2,(int)x);return curve[i]+(curve[i+1]-curve[i])*(x-i);}
    }
    [Serializable] public sealed class SocialConfiguration
    {
        public int seed=3821,previewCount=20,maxPopulation=20,memoryLimit=24,attentionLimit=4;
        public List<SocialTrait> traits=new List<SocialTrait>();public double[] correlations;
        public List<SocialMapping> mappings=new List<SocialMapping>();
        public SocialDistribution age=new SocialDistribution(24,8,16,65),skill=new SocialDistribution(60,18,0,100),groupSize=new SocialDistribution(3,1,2,5);
        public float maleProbability=.5f,voiceProbability=.35f,friendProbability=.45f,cohesion=.6f,sharedHistory=.55f;
        public string[] demographicNames={"Male profile","Female profile"};
        public float[] demographicWeights;
        public float spontaneous=.2f,respondHuman=.85f,respondBot=.3f,interjections=.12f,overlap=.15f,interruptSensitivity=.6f;
        public float chaos=.5f,silliness=.5f,trolling=.2f,trashTalk=.2f,reactions=.7f,screaming=.15f,verbosity=.35f,slang=.4f;
        public float tickSeconds=.2f,minCooldown=5,maxCooldown=12,maxSilence=24,eventLifetime=5,humanHold=.35f,providerTimeout=8;
        public int typicalResponses=2,maxConcurrentSpeech=2,maxEvents=64,maxSpeechQueue=12;
        public bool enableBots=true,enableVoice=true,allowProfanity,retainHumanText,recordDemonstrations;
        public static SocialConfiguration Defaults()
        {
            var c=new SocialConfiguration();string[] ids={"competitiveness","boldness","sociability","playfulness","cooperativeness","impulsiveness","reactivity","strategy"};
            string[] labels={"Competitiveness","Boldness","Sociability","Playfulness","Cooperativeness","Impulsiveness","Emotional reactivity","Strategic thinking"};
            for(int i=0;i<ids.Length;i++)c.traits.Add(new SocialTrait(ids[i],labels[i]));c.ResetCorrelations();
            c.mappings.Add(new SocialMapping{behavior="risk",trait="boldness"});c.mappings.Add(new SocialMapping{behavior="delay",trait="impulsiveness",curve=new[]{1f,.8f,.5f,.25f,0f}});
            c.mappings.Add(new SocialMapping{behavior="social",trait="sociability"});c.mappings.Add(new SocialMapping{behavior="reaction",trait="reactivity"});c.mappings.Add(new SocialMapping{behavior="help",trait="cooperativeness"});return c;
        }
        public void ResetCorrelations(){correlations=new double[traits.Count*traits.Count];for(int i=0;i<traits.Count;i++)correlations[i*traits.Count+i]=1;}
        public void Validate()
        {
            if(traits==null||traits.Count<1||traits.Count>32||traits.Select(t=>t.id).Distinct().Count()!=traits.Count||traits.Any(t=>string.IsNullOrWhiteSpace(t.id)))throw new ArgumentException("Use 1..32 uniquely named traits.");
            foreach(var t in traits)t.distribution.Calibrate();age.Calibrate();skill.Calibrate();groupSize.Calibrate();SocialRandom.Factor(correlations,traits.Count);
            if(age.minimum<0||age.maximum>120||skill.minimum<0||skill.maximum>100||groupSize.minimum<2||groupSize.maximum>32)throw new ArgumentException("Age, skill or group bounds outside supported range.");
            if(Math.Ceiling(groupSize.minimum)>Math.Floor(groupSize.maximum)||demographicNames==null||demographicNames.Length<2||demographicNames.Length>16||demographicNames.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Use valid integer group sizes and at least two named demographic profiles.");
            if(demographicWeights!=null&&demographicWeights.Length>0&&(demographicWeights.Length!=demographicNames.Length||demographicWeights.Any(x=>float.IsNaN(x)||float.IsInfinity(x)||x<0)||demographicWeights.Sum()<=0))throw new ArgumentException("Custom demographic weights must match names and have positive total weight.");
            foreach(var t in traits)if(t.distribution.minimum<0||t.distribution.maximum>100)throw new ArgumentException("Trait bounds must stay in 0..100.");
            if(maxPopulation<1||maxPopulation>100||memoryLimit<1||memoryLimit>128||attentionLimit<1||attentionLimit>16||tickSeconds<.05f||tickSeconds>2||minCooldown<.2f||maxCooldown<minCooldown||eventLifetime<.2f||maxEvents<1||maxEvents>512||maxSpeechQueue<1||maxSpeechQueue>64||maxConcurrentSpeech<1||maxConcurrentSpeech>4||typicalResponses<1||typicalResponses>3)throw new ArgumentException("Invalid simulation budgets/cooldowns.");
            if(new[]{maleProbability,voiceProbability,friendProbability,cohesion,sharedHistory,spontaneous,respondHuman,respondBot,interjections,overlap,interruptSensitivity,chaos,silliness,trolling,trashTalk,reactions,screaming,verbosity,slang}.Any(x=>float.IsNaN(x)||x<0||x>1))throw new ArgumentException("Probability controls must be 0..1.");
            if(new[]{tickSeconds,minCooldown,maxCooldown,maxSilence,eventLifetime,humanHold,providerTimeout}.Any(x=>float.IsNaN(x)||float.IsInfinity(x))||humanHold<.05f||humanHold>2||providerTimeout<.2f||providerTimeout>30||maxSilence<1)throw new ArgumentException("Invalid timing or provider budget.");
            foreach(var m in mappings)if(!traits.Any(t=>t.id==m.trait)||m.curve==null||m.curve.Length<2||m.curve.Any(v=>float.IsNaN(v)||float.IsInfinity(v))||float.IsNaN(m.weight)||Math.Abs(m.weight)>10)throw new ArgumentException("Invalid trait behavior mapping.");
        }
    }
    [Serializable] public sealed class SocialVoiceIdentity
    {public string speaker="af_heart",accent="en-US",microphone="Clean headset";public float pitch=1,rate=1,texture,expressiveness;}
    [Serializable] public sealed class SocialIdentity
    {
        public string id,name,demographic;public double age,skill;public bool voice;public int group=-1;public string[] traitIds;public double[] traits;public SocialVoiceIdentity vocal=new SocialVoiceIdentity();
        public double Trait(string key){int i=Array.IndexOf(traitIds,key);return i>=0?traits[i]/100:.5;}
        public double Behavior(SocialConfiguration c,string behavior,double fallback)
        {double sum=0,w=0;foreach(var m in c.mappings)if(m.behavior==behavior){sum+=m.weight*m.Evaluate(Trait(m.trait));w+=Math.Abs(m.weight);}return w>0?Math.Max(0,Math.Min(1,sum/w)):fallback;}
    }
    public static class SocialPopulation
    {
        static readonly string[] Names={"Pebble","Jelly","Mango","Orbit","Noodle","Echo","Waffle","Sprout","Comet","Puddle","Pixel","Mochi","Biscuit","Kite","Pickle","Nova","Fizz","Gizmo","Toast","Cloud"};
        static readonly string[] Speakers={"af_heart","af_bella","af_nicole","af_sarah","am_adam","am_michael","bf_emma","bm_george"};
        public static List<SocialIdentity> Generate(SocialConfiguration c,int count)
        {
            c.Validate();if(count<0||count>10000)throw new ArgumentException("Population preview limited to 10000.");
            var r=new Random(c.seed);int n=c.traits.Count;var l=SocialRandom.Factor(c.correlations,n);var d=c.traits.Select(t=>t.distribution.Calibrate()).ToArray();var age=c.age.Calibrate();var skill=c.skill.Calibrate();
            var list=new List<SocialIdentity>(count);var z=new double[n];
            for(int k=0;k<count;k++)
            {
                for(int i=0;i<n;i++)z[i]=SocialRandom.Normal(r);
                var b=new SocialIdentity{id="social:"+c.seed+":"+k,name=Names[k%Names.Length]+(k>=Names.Length?" "+(k/Names.Length+1):""),age=age.Sample(r),skill=skill.Sample(r),voice=r.NextDouble()<c.voiceProbability,demographic=r.NextDouble()<c.maleProbability?c.demographicNames[0]:c.demographicNames[Math.Min(1,c.demographicNames.Length-1)],traitIds=c.traits.Select(t=>t.id).ToArray(),traits=new double[n]};
                if(c.demographicWeights!=null&&c.demographicWeights.Length>0){double pick=r.NextDouble()*c.demographicWeights.Sum();for(int i=0;i<c.demographicWeights.Length;i++){pick-=c.demographicWeights[i];if(pick<=0){b.demographic=c.demographicNames[i];break;}}}
                for(int i=0;i<n;i++){double x=0;for(int j=0;j<=i;j++)x+=l[i,j]*z[j];b.traits[i]=d[i].Quantile(SocialRandom.NormalCdf(x));}
                b.vocal=new SocialVoiceIdentity{speaker=Speakers[r.Next(Speakers.Length)],rate=(float)(.9+r.NextDouble()*.2),pitch=(float)(.94+r.NextDouble()*.12),expressiveness=(float)b.Trait("reactivity"),texture=(float)r.NextDouble()*.12f,microphone=new[]{"Clean headset","Gaming headset","Laptop","Muffled","Cheap mic","Noisy mic"}[r.Next(6)]};list.Add(b);
            }
            int target=(int)Math.Round(count*c.friendProbability),assigned=0,group=0;var sizes=c.groupSize.Calibrate();
            var order=Enumerable.Range(0,count).OrderBy(_=>r.Next()).ToArray();
            while(target-assigned>=2)
            {
                int size=Math.Max((int)Math.Ceiling(c.groupSize.minimum),Math.Min((int)Math.Floor(c.groupSize.maximum),(int)Math.Round(sizes.Sample(r))));
                if(size>target-assigned)size=target-assigned;if(size<c.groupSize.minimum)break;
                for(int i=0;i<size;i++)list[order[assigned++]].group=group;group++;
            }
            return list;
        }
    }
}
