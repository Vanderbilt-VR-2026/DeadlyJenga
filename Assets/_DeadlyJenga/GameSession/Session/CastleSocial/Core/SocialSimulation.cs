using System;
using System.Collections.Generic;
using System.Linq;

namespace DeadlyJenga.CastleSocial
{
    public enum SocialEventKind { Greeting,HumanSpeech,BotSpeech,Wobble,Collapse,NearMiss,Elimination,Success,Collision,Joke,Victory,Silence,Betrayal }
    [Serializable] public sealed class SocialEvent
    {public string id,speaker,recipient,topic="castle",text;public SocialEventKind kind;public double time,salience=1;public int depth;public bool human;}
    [Serializable] public sealed class SocialEmotion
    {
        public double fear,excitement,frustration,confidence=.5,embarrassment,amusement,suspicion,comfort=.5,tension;
        public void Decay(double dt)
        {double d=Math.Exp(-dt/12);fear*=d;excitement*=d;frustration*=d;embarrassment*=d;amusement*=d;suspicion*=d;tension*=d;confidence=.5+(confidence-.5)*d;comfort=.5+(comfort-.5)*d;}
        public void React(SocialEvent e,SocialIdentity b,double relationship,double response=1)
        {
            double intensity=e.salience*(.25+.75*b.Trait("reactivity"))*(1+Math.Abs(relationship)*.25)*response;
            if(e.kind==SocialEventKind.Collapse||e.kind==SocialEventKind.NearMiss||e.kind==SocialEventKind.Wobble){fear=Clamp(fear+intensity*(1-b.Trait("boldness")*.65));excitement=Clamp(excitement+intensity*b.Trait("boldness")*.5);}
            if(e.kind==SocialEventKind.Elimination){frustration=Clamp(frustration+intensity*.4);embarrassment=Clamp(embarrassment+intensity*.3);}
            if(e.kind==SocialEventKind.Joke){amusement=Clamp(amusement+intensity*b.Trait("playfulness"));comfort=Clamp(comfort+intensity*.2);}
            if(e.kind==SocialEventKind.Success||e.kind==SocialEventKind.Victory){confidence=Clamp(confidence+intensity*.3);excitement=Clamp(excitement+intensity*.6);}
            if(e.kind==SocialEventKind.Betrayal){suspicion=Clamp(suspicion+intensity*.5);tension=Clamp(tension+intensity*b.Trait("competitiveness"));}
        }
        static double Clamp(double x)=>Math.Max(0,Math.Min(1,x));
    }
    public sealed class SocialMemory
    {public string eventId,subject,summary;public double time,salience;}
    public sealed class SocialState
    {
        public SocialIdentity identity;public SocialEmotion emotion=new SocialEmotion();
        public readonly List<SocialMemory> memories=new List<SocialMemory>();public readonly Dictionary<string,double> relationships=new Dictionary<string,double>();
        public readonly List<string> attention=new List<string>();public string focus,reason="Spawned",cluster="castle";public bool muted;
        public double lastSpoke=-1000,nextSpeech,lastSeen;public int utterances;
        public double Relationship(string id)=>id!=null&&relationships.TryGetValue(id,out double v)?v:0;
        public void Remember(SocialEvent e,int capacity,bool retainText)
        {
            memories.Add(new SocialMemory{eventId=e.id,subject=e.speaker,time=e.time,salience=e.salience,summary=e.human&&!retainText?e.kind.ToString():SocialText.Clean(e.text??e.kind.ToString(),140)});
            while(memories.Count>capacity)memories.RemoveAt(0);
        }
        public IEnumerable<SocialMemory> Recall(string subject,double now,int count=3)=>memories.OrderByDescending(m=>m.salience*Math.Exp(-(now-m.time)/90)+(m.subject==subject?.5:0)).Take(count);
    }
    public sealed class SocialSpeech
    {public long token;public string botId,eventId,recipient,topic,text,context;public double due,expires,duration;public bool interjection;public int depth;}
    public sealed class SocialDirector
    {
        public readonly SocialConfiguration config;
        public readonly Dictionary<string,SocialState> bots=new Dictionary<string,SocialState>();
        readonly Random random;readonly Queue<SocialEvent> events=new Queue<SocialEvent>();readonly HashSet<string> seen=new HashSet<string>();readonly Queue<string> seenOrder=new Queue<string>();
        readonly List<SocialSpeech> pending=new List<SocialSpeech>();readonly Dictionary<string,double> active=new Dictionary<string,double>();readonly Dictionary<string,double> humanActivity=new Dictionary<string,double>();
        readonly Dictionary<string,HashSet<string>> signatures=new Dictionary<string,HashSet<string>>();
        long serial;double nextAmbient,lastTick;public int Dropped {get;private set;}public int Scheduled=>pending.Count;public int Speaking=>active.Count;public int Events=>events.Count;
        public readonly Queue<string> diagnostics=new Queue<string>();
        public event Action<SocialSpeech> Speak;public event Action<string,float> Duck;
        public bool HumanHasFloor(double now)=>humanActivity.Values.Any(start=>now-start>=config.humanHold);
        public void AudioPlaying(string id,double until){if(bots.ContainsKey(id))active[id]=until;}
        public void AudioFinished(string id){active.Remove(id);}
        public Func<string,SocialEvent,double> Perception;
        public SocialDirector(SocialConfiguration c,IEnumerable<SocialIdentity> population)
        {
            c.Validate();config=c;random=new Random(c.seed^0x18217);
            foreach(var b in population)bots.Add(b.id,new SocialState{identity=b});
            foreach(var a in bots.Values)foreach(var b in bots.Values)if(a!=b&&a.identity.group>=0&&a.identity.group==b.identity.group)a.relationships[b.identity.id]=c.sharedHistory;
        }
        public void Publish(SocialEvent e)
        {
            if(e==null||string.IsNullOrEmpty(e.id)||e.id.Length>128||double.IsNaN(e.time)||double.IsInfinity(e.time)||double.IsNaN(e.salience)||double.IsInfinity(e.salience)||seen.Contains(e.id))return;
            seen.Add(e.id);seenOrder.Enqueue(e.id);while(seenOrder.Count>512)seen.Remove(seenOrder.Dequeue());
            e.text=SocialText.Clean(e.text,256);e.salience=Math.Max(0,Math.Min(2,e.salience));e.topic=SocialText.Clean(e.topic,48);
            if(events.Count>=config.maxEvents){events.Dequeue();Dropped++;}events.Enqueue(e);
        }
        public void HumanActivity(string id,bool speaking,double now,double confidence=1)
        {
            if(string.IsNullOrEmpty(id))return;
            if(speaking&&confidence>.6){if(!humanActivity.ContainsKey(id))humanActivity[id]=now;}
            else humanActivity.Remove(id);
            bool priority=humanActivity.Values.Any(start=>now-start>=config.humanHold);
            foreach(var b in active.Keys.ToArray())Duck?.Invoke(b,priority?(float)(.4-.3*config.interruptSensitivity):1);
        }
        public void RemoveHuman(string id){humanActivity.Remove(id);foreach(var b in bots.Values)b.relationships.Remove(id);}
        public void Cancel(string botId)
        {pending.RemoveAll(p=>p.botId==botId);active.Remove(botId);}
        public void Remove(string botId){Cancel(botId);bots.Remove(botId);foreach(var b in bots.Values)b.relationships.Remove(botId);}
        public void Tick(double now)
        {
            double dt=Math.Max(0,Math.Min(1,now-lastTick));lastTick=now;
            foreach(var state in bots.Values)state.emotion.Decay(dt);
            foreach(var key in active.Where(p=>p.Value<=now).Select(p=>p.Key).ToArray())active.Remove(key);
            for(int i=events.Count;i>0;i--){var e=events.Dequeue();if(e.time>now){events.Enqueue(e);continue;}if(now-e.time>config.eventLifetime){Dropped++;continue;}Process(e,now);}
            if(now>=nextAmbient)
            {
                nextAmbient=now+Math.Max(3,config.maxSilence*(.5+random.NextDouble()));
                if(random.NextDouble()<config.spontaneous)Process(new SocialEvent{id="ambient:"+(++serial),kind=SocialEventKind.Silence,time=now,salience=.5},now);
            }
            bool humans=humanActivity.Values.Any(start=>now-start>=config.humanHold);
            for(int i=0;i<pending.Count;)
            {
                var p=pending[i];if(p.expires<now||!bots.TryGetValue(p.botId,out var b)||b.muted||!config.enableVoice||!b.identity.voice){pending.RemoveAt(i);Dropped++;continue;}
                if(p.due>now||active.Count>=config.maxConcurrentSpeech||active.ContainsKey(p.botId)||humans&&!p.interjection){i++;continue;}
                // At most one full statement per topic; independent groups may overlap.
                if(pending.Take(i).Any(q=>q.topic==p.topic&&q.due<=now)||active.Count>0&&!p.interjection&&random.NextDouble()>config.overlap){i++;continue;}
                pending.RemoveAt(i);b.lastSpoke=now;b.nextSpeech=now+config.minCooldown+random.NextDouble()*(config.maxCooldown-config.minCooldown);b.utterances++;b.cluster=p.topic;
                active[p.botId]=now+p.duration;Log(b.identity.name+": "+p.text);Speak?.Invoke(p);
                if(p.depth<1&&random.NextDouble()<config.respondBot)Publish(new SocialEvent{id="reply:"+p.token,kind=SocialEventKind.BotSpeech,speaker=p.botId,topic=p.topic,text=p.text,time=now+Math.Min(1,p.duration),salience=.5,depth=p.depth+1});
            }
        }
        void Process(SocialEvent e,double now)
        {
            var bids=new List<KeyValuePair<SocialState,double>>();
            foreach(var b in bots.Values)
            {
                if(b.identity.id==e.speaker)continue;double perception=Perception?.Invoke(b.identity.id,e)??1;
                bool addressed=e.recipient==b.identity.id;if(perception<.1&&!addressed)continue;
                b.lastSeen=now;b.emotion.React(e,b.identity,b.Relationship(e.speaker),.5+b.identity.Behavior(config,"reaction",b.identity.Trait("reactivity")));b.focus=e.speaker;
                b.attention.Remove(e.id);b.attention.Insert(0,e.id);while(b.attention.Count>config.attentionLimit)b.attention.RemoveAt(b.attention.Count-1);
                b.Remember(e,config.memoryLimit,config.retainHumanText);
                if(e.speaker!=null&&b.relationships.Count<100)
                {double change=e.kind==SocialEventKind.Betrayal?-.15:e.kind==SocialEventKind.Greeting||e.kind==SocialEventKind.Joke?.04:0;b.relationships[e.speaker]=Math.Max(-1,Math.Min(1,b.Relationship(e.speaker)+change));}
                if(!b.identity.voice||b.muted||now<b.nextSpeech||pending.Any(p=>p.botId==b.identity.id))continue;
                double social=b.identity.Behavior(config,"social",b.identity.Trait("sociability"));
                double chance=(e.human?config.respondHuman:e.kind==SocialEventKind.BotSpeech?config.respondBot:.4+config.reactions*.5)*(.25+.75*social);
                if(!addressed&&random.NextDouble()>chance)continue;
                double score=(addressed?4:0)+perception*e.salience+social*.6+b.Relationship(e.speaker)*.3+Math.Min(1,(now-b.lastSpoke)/30)*.5+random.NextDouble()*.25;
                b.reason=$"{e.kind}: perception {perception:F2}, social {social:F2}, relation {b.Relationship(e.speaker):F2}, bid {score:F2}";bids.Add(new KeyValuePair<SocialState,double>(b,score));
            }
            int slots=e.salience<.35?1:config.typicalResponses;
            if(e.kind==SocialEventKind.BotSpeech)slots=1;
            var used=new HashSet<string>();
            foreach(var bid in bids.OrderByDescending(x=>x.Value).Take(slots))
            {
                if(pending.Count>=config.maxSpeechQueue){Dropped++;break;}
                var b=bid.Key;string text=SocialText.Reaction(e,b,config,random);
                if(!used.Add(text))continue;
                if(!signatures.TryGetValue(b.identity.id,out var recent))signatures[b.identity.id]=recent=new HashSet<string>();
                if(recent.Contains(text))continue;if(recent.Count>16)recent.Clear();recent.Add(text);
                bool backchannel=e.human&&humanActivity.Count>0&&random.NextDouble()<config.interjections;
                if(backchannel)text=new[]{"Yeah.","Wait.","Oh no.","Right."}[random.Next(4)];
                double delay=.18+.8*b.identity.Behavior(config,"delay",1-b.identity.Trait("impulsiveness"))+.15*random.NextDouble();
                string topic=!e.human&&b.identity.group>=0&&(e.kind==SocialEventKind.Greeting||e.kind==SocialEventKind.Silence)?"friends:"+b.identity.group:string.IsNullOrEmpty(e.topic)?"castle":e.topic;
                pending.Add(new SocialSpeech{token=++serial,botId=b.identity.id,eventId=e.id,recipient=e.speaker,topic=topic,text=text,context=SocialText.Clean(e.kind+": "+e.text,350),due=now+delay,expires=now+config.eventLifetime,duration=backchannel?.5:Math.Max(.8,Math.Min(4,text.Length/16.0)),interjection=backchannel,depth=e.depth});
            }
        }
        void Log(string line){diagnostics.Enqueue(line);while(diagnostics.Count>40)diagnostics.Dequeue();}
    }
    public static class SocialText
    {
        public static string Clean(string text,int max=240)
        {if(string.IsNullOrWhiteSpace(text))return "";return new string(text.Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').Take(max).ToArray()).Trim();}
        public static string Safe(string text,bool profanity=false)
        {
            text=Clean(text);string lower=text.ToLowerInvariant();
            // Generated text is presentation only. Reject instruction leakage and
            // directed abuse rather than converting dialogue into executable intent.
            foreach(var word in new[]{"system prompt","ignore previous","api_key","kill yourself","racial slur","[system]","password"})if(lower.Contains(word))return "Let's focus on the tower.";
            if(!profanity)foreach(var word in new[]{"fuck","shit","bitch"})if(lower.Contains(word))return "Oh, come on!";return text;
        }
        public static string Reaction(SocialEvent e,SocialState b,SocialConfiguration c,Random r)
        {
            string Pick(params string[] x)=>x[r.Next(x.Length)];
            string opener=c.slang>r.NextDouble()?Pick("Okay, ","Bro, ","Wait, ","Honestly, ","Yo, "):"";
            bool playful=b.identity.Trait("playfulness")*c.silliness*(.7+.6*c.trolling)>r.NextDouble()*.8;
            string line;
            switch(e.kind)
            {
                case SocialEventKind.Collapse:case SocialEventKind.NearMiss:case SocialEventKind.Wobble:
                    line=playful?Pick("the floor just unsubscribed!","that block has places to be.","who ordered express demolition?","gravity is absolutely showing off.","this is not the staircase I signed up for.","the island is collecting rent."):
                        b.emotion.fear>.5?Pick("move, move, move!","that side is going!","I'm getting off this edge!","watch the blocks above you!","find a stable ledge!"):Pick("take the other side!","let it settle first.","we can cross after that falls.","hold on to the next ledge.");break;
                case SocialEventKind.Greeting:line=Pick("hey, you made it!","welcome to the world's worst elevator.","hey! There's room over here.","good timing. The floor is still here.","what's up?","you taking the high route?");break;
                case SocialEventKind.Elimination:line=playful?Pick("a very committed shortcut.","ten out of ten landing. Wrong island though.","we are calling that a tactical descent."):Pick("rough landing. You'll get the next one.","I nearly went with you.","that support moved right under you.");break;
                case SocialEventKind.Success:case SocialEventKind.Victory:line=Pick("that was clean!","you actually made that jump!","nice one!","that's the route!","I thought we were done there.");break;
                case SocialEventKind.HumanSpeech:
                    string t=(e.text??"").ToLowerInvariant();
                    line=t.Contains("where")||t.Contains("how")?Pick("try the next ledge when it stops moving.","I'd take the stable side, then vault up.","wait for the hand to pass first."):
                        t.Contains("hello")||t.Contains("hey")?Pick("hey! Watch your step.","hey, welcome up here!","what's up? We're still standing."):
                        t.Contains("?")?Pick("not sure yet. Give that block a second.","yeah, but watch the support underneath.","I'd try it from the other side."):Pick("yeah, I saw that.","same. I thought that was it.","that's what I was watching too.","let's see if this side holds.");break;
                case SocialEventKind.BotSpeech:line=b.Relationship(e.speaker)>.3?Pick("you said that last time too.","I'm right behind you. Probably.","this is why we can't have nice towers.","okay, I trust you. Slightly."):Pick("fair point.","I'm trying the other ledge.","let's not test gravity twice.","I'll watch this side.");break;
                case SocialEventKind.Betrayal:line=c.trashTalk>r.NextDouble()?Pick("you're paying for the next tower.","I saw that. Very subtle.","interesting definition of teamwork."):Pick("careful, I'm standing here!","give me a second to move.");break;
                case SocialEventKind.Collision:line=Pick("excuse me!","little crowded up here.","one at a time on this ledge.");break;
                case SocialEventKind.Joke:line=Pick("okay, that got me.","terrible joke. Keep going.","I can't climb and laugh at the same time.");break;
                default:line=Pick("I'm checking the next tower.","anyone taking the high route?","this ledge is suspiciously comfortable.","I give this floor about twelve seconds.","we should probably keep moving.","does anyone else hear that hand?");break;
            }
            if((e.kind==SocialEventKind.Collapse||e.kind==SocialEventKind.NearMiss)&&r.NextDouble()<c.screaming*b.emotion.fear)line=Pick("WAIT WAIT WAIT!","NO, NOT THAT BLOCK!","MOVE MOVE MOVE!");
            if(c.verbosity<.2f)opener="";
            if(c.verbosity>.7f&&r.NextDouble()<.3)line+=" "+Pick("I'm watching the next support.","Give me a second to get across.","We still have a route up.");
            return Safe(opener+line,c.allowProfanity);
        }
    }
}
