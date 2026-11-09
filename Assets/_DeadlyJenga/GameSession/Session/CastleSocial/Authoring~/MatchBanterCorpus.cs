using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DeadlyJenga.CastleSocial;
using static DeadlyJenga.CastleSocial.SocialEventKind;

static class Program
{
    static int checkedCases;
    static int errors;
    static void Check(bool pass,string message)
    {
        checkedCases++;
        if(pass)return;
        errors++;
        Console.WriteLine("FAIL "+message);
    }

    static void Main()
    {
        var samples=new (SocialEventKind kind,string fact,string topic,bool sober)[]
        {
            (Collapse,"A tower block broke apart on my right. I saw it happen.","block",false),
            (Collapse,"Several tower blocks shifted together below me. I saw it happen.","below",false),
            (Wobble,"A block cracked above me. I saw it happen.","cracked",false),
            (NearMiss,"Someone suddenly dropped below a ledge on my left. I saw it happen.","ledge",true),
            (Success,"Someone vaulted onto a ledge in front of me. I saw it happen.","vaulted",false),
            (Damage,"This happened to me: someone took a hit.","I took a hit",true),
            (Damage,"Someone took a hit on my right. I saw it happen.","hit",true),
            (Collision,"This happened to me: someone got knocked back.","I got knocked back",true),
            (Collision,"Someone got knocked back above me. I saw it happen.","knocked back",true),
            (Elimination,"Someone was eliminated below me. I saw it happen.","eliminated",true),
            (Hazard,"Flood started below me. I saw it happen.","Flood",false),
            (Hazard,"Tornado ended on my right. I saw it happen.","Tornado",false),
            (Hazard,"The public disaster warning reads: Rising water.","Rising water",false),
            (Combat,"A shot went off on my left. I saw it happen.","shot",true),
            (Combat,"Someone swung a weapon above me. I saw it happen.","swung",true),
            (Equipment,"Someone picked up equipment in front of me. I saw it happen.","picked up",false),
            (Equipment,"Someone dropped equipment on my left. I saw it happen.","dropped",false),
            (Objective,"I scored a confirmed flag capture.","flag",false),
            (Objective,"I returned a flag to its base.","returned",false),
            (Objective,"I claimed a block for my team.","claimed",false),
            (Objective,"I revived a player.","revived",false),
            (Objective,"I infected a player.","infected",false),
            (Objective,"I got a confirmed elimination.","elimination",false),
            (Objective,"I put five seconds on the hill.","hill",false),
            (Objective,"I made it to a minute.","minute",false),
            (Round,"The public match display says the round has started.","started",false),
            (Round,"The public match display says the round is over.","over",false),
            (Rewind,"The rewind ended.","rewind",false),
            (Effect,"This happened to me: someone got hit by an effect.","effect",false),
            (Joke,"Someone struck a pose on my left. I saw it happen.","pose",false),
            (Greeting,"A teammate arrived.","arrived",false),
            (Silence,"The arena is quiet.","quiet",false),
            (Victory,"The round ended.","ended",false),
            (Betrayal,"I do not know who acted.","I do not know",false)
        };

        foreach(var row in samples)
        {
            var turn=new ObservedSpeech{Kind=row.kind,Fact=row.fact};
            string dry=MatchBanter.Compose(turn,0,false);
            var options=Enumerable.Range(0,3).Select(i=>MatchBanter.Compose(turn,i,true)).ToArray();
            bool selfContact=row.fact.StartsWith("This happened to me:",StringComparison.Ordinal)&&
                (row.kind==Damage||row.kind==Collision);
            Check((selfContact?dry.Contains(row.topic,StringComparison.OrdinalIgnoreCase):
                options.All(line=>line.StartsWith(dry,StringComparison.Ordinal)&&line.Contains(row.topic,StringComparison.OrdinalIgnoreCase))),
                row.kind+" preserves fact");
            Check(options.All(line=>line.Split(' ').Length<=18),row.kind+" line length");
            if(row.sober) Check(options.All(line=>line.Count(c=>c=='.')==1),row.kind+" no added punchline during danger");
            if(selfContact)Check(options.Distinct().Count()==3,row.kind+" varies personal contact wording");
            if(row.kind==Hazard) Check(options.All(line=>line==dry),"Disaster callouts stay plain");
            Check(!MatchBanter.CanQuip(turn,0,1),row.kind+" first callout has no aside");
            Console.WriteLine($"{row.kind,-12} {options[0]}");
        }

        foreach(var row in new[]{("on my right","your right"),("on my left","your left"),("above me","above you"),("below me","below you")})
        {
            var turn=new ObservedSpeech{Kind=Collapse,Fact="I heard a crash nearby. I could not see what happened.",HeardOnly=true,
                HeardText="A tower block broke apart "+row.Item1+"."};
            foreach(bool playful in new[]{false,true})
            {
                string reply=MatchBanter.Compose(turn,1,playful);
                Check(reply.Contains(row.Item2)&&!reply.Contains("I saw")&&!reply.Contains("on above")&&!reply.Contains("on below"),"collapse reply to "+row.Item1);
                Console.WriteLine("Reply        "+reply);
            }
        }
        foreach(var kind in new[]{Collapse,Wobble,Combat})
        {
            var turn=new ObservedSpeech{Kind=kind,Fact="I heard a crash nearby. I could not see what happened.",HeardOnly=true};
            Check(MatchBanter.Compose(turn,0,true)==(kind==Collapse?"Whoa, what was that?":"I heard a crash nearby."),
                kind+" hearing-only remains hearing-only");
        }
        foreach(var kind in Enum.GetValues<SocialEventKind>())
        {
            var turn=new ObservedSpeech{Kind=kind,Fact="I did not witness the event.",HeardOnly=true,HeardText="Something happened."};
            string reply=MatchBanter.Compose(turn,0,false);
            Check(!reply.Contains("I saw")&&!reply.Contains("I watched"),kind+" reply cannot manufacture sight");
        }
        foreach(var pair in new[]{
            (Equipment,"Someone dropped equipment.","drop"),
            (Equipment,"Someone picked up equipment.","pick"),
            (Round,"Round just started.","Ready"),
            (Round,"Round's over.","Good game"),
            (Objective,"Flag scored.","Flag"),
            (Combat,"A shot went off.","way"),
            (NearMiss,"Someone dropped below the ledge.","okay"),
            (Effect,"Someone got hit by an effect.","last"),
            (Hazard,"Flood started nearby.","close"),
            (Rewind,"The rewind ended.","another go")})
        {
            string reply=MatchBanter.Compose(new ObservedSpeech{Kind=pair.Item1,HeardText=pair.Item2},0,false);
            Check(reply.Contains(pair.Item3,StringComparison.OrdinalIgnoreCase),pair.Item1+" heard-line response");
        }
        var rewind=new ObservedSpeech{Kind=Rewind,Fact="The rewind ended."};
        Check(MatchBanter.Compose(rewind,2,true)=="The rewind ended. Wait, does that count as a do-over?",
            "Meta aside is grounded in the confirmed rewind");
        Check(MatchBanter.CanQuip(rewind,2,.8)&&!MatchBanter.CanQuip(rewind,2,.4),
            "Rare asides depend on personality");
        rewind.HeardOnly=true;
        Check(!MatchBanter.CanQuip(rewind,2,1),"A listener doesn't joke about unseen facts");
        Check(MatchBanter.ContainsWords("Wait, Jelly!", "Jelly")&&!MatchBanter.ContainsWords("jellyfish", "Jelly"),
            "Addressing uses whole names");
        Check(MatchBanter.IsQuestion("Wait, where did it go")&&!MatchBanter.IsQuestion("I'm just talking to my friend."),
            "Questions without ASR punctuation invite a turn, side talk does not");
        Check(MatchBanter.IsBotQuestion("Jelly, are you a real person?", "Jelly")&&!MatchBanter.IsBotQuestion("I didn't ask are you a bot", "Jelly"),
            "Direct fourth-wall question gets a plain bot answer");
        Check(MatchBanter.DirectPlayerReply("Jelly, did you get hit just then?", "Jelly",
            "This happened to me: someone took a hit.")=="Yep, felt that.",
            "Direct personal damage question uses confirmed observation");
        Check(MatchBanter.DirectPlayerReply("Did you get hit?", "Jelly", "Someone took a hit on my right.")==null,
            "Seeing another player get hit does not confirm a self hit");
        Check(MatchBanter.DirectPlayerReply("Did you get hit by Jelly?", "Pebble",
            "This happened to me: someone took a hit.")==null,
            "A self hit alone doesn't confirm who caused it");
        Check(MatchBanter.DirectPlayerReply("Jelly, this game is chaos. One more round?", "Jelly", "")=="Yeah, run it back.",
            "A rematch invitation gets a social reply without claiming to start a round");
        Check(!MatchBanter.IsPlayerReply("I'm a bot. Fragments are fine.", "One more round?")&&
            !MatchBanter.IsPlayerReply("One more round?", "This game is chaos. One more round?")&&
            MatchBanter.IsPlayerReply("I'm down.", "This game is chaos. One more round?"),
            "Open replies reject instruction echoes and parroting the mic");
        var memory=new DialogueRecency();
        memory.Remember("Pebble","The block just fell on my right!",5,true);
        memory.Remember("Pebble","Hold on. I heard your question.",6,false);
        Check(DialogueRecency.Repeats("THE block just fell on my right.",memory.Snapshot("Jelly",7,true),false),"Shared exact repeat ignores punctuation and case");
        Check(DialogueRecency.Repeats("Well, the block just fell on my right.",memory.Snapshot("Jelly",7,true),true),"Model near-copy is recognized");
        Check(!DialogueRecency.Repeats("The block just fell on my left.",memory.Snapshot("Jelly",7,true),true),"Changed direction remains novel");
        Check(!DialogueRecency.Repeats("Hold on. I heard your question.",memory.Snapshot("Jelly",7,true),false),"Private reply is outside match history");
        Check(DialogueRecency.Repeats("Hold on. I heard your question.",memory.Snapshot("Pebble",7,false),false),"Own prior model reply is remembered");
        Check(!DialogueRecency.Repeats("The block just fell on my right.",memory.Snapshot("Jelly",71,true),false),"Old match dialogue expires");
        memory.Clear();
        Check(memory.Snapshot("Pebble",7,true).Length==0,"Reset clears voice memory");
        var measured=new ObservedSpeech{Kind=Collapse,Fact=samples[0].fact};
        for(int i=0;i<2000;i++)MatchBanter.Compose(measured,i,true);
        const int count=20000;
        long[] ticks=new long[count];
        for(int i=0;i<count;i++)
        {
            long start=Stopwatch.GetTimestamp();
            _=MatchBanter.Compose(measured,i,true);
            ticks[i]=Stopwatch.GetTimestamp()-start;
        }
        Array.Sort(ticks);
        Console.WriteLine($"Composer only p50 {ticks[count/2]*1000.0/Stopwatch.Frequency:F4} ms, p95 {ticks[count*95/100]*1000.0/Stopwatch.Frequency:F4} ms over {count} calls.");
        Console.WriteLine($"{checkedCases-errors}/{checkedCases} assertions passed across {samples.Length} facts, four reply directions and all event kinds.");
        if(errors>0) Environment.Exit(1);
    }
}
