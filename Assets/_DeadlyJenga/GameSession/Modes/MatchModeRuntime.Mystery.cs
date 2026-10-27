using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        MysterySimulation mystery;
        MysteryPrivateFrame personal=new MysteryPrivateFrame();
        readonly Dictionary<string,MysteryRole> rolePreferences=new Dictionary<string,MysteryRole>();
        readonly Dictionary<string,(string target,float start,float seen)> reviving=new Dictionary<string,(string,float,float)>();
        readonly HashSet<string> corpseDeaths=new HashSet<string>();
        readonly Dictionary<string,float> footprintNext=new Dictionary<string,float>();
        readonly Dictionary<string,Vector3> footprintLast=new Dictionary<string,Vector3>();
        readonly List<MysteryTrailFrame> trailFrames=new List<MysteryTrailFrame>();
        readonly HashSet<string> trailCells=new HashSet<string>();
        readonly Dictionary<string,IMysteryBody> mysteryBodies=new Dictionary<string,IMysteryBody>();
        readonly Dictionary<string,float> mysteryStrikes=new Dictionary<string,float>();
        public IReadOnlyList<MysteryTrailFrame> Trails=>trailFrames;
        public bool Active=>Rules.mode==GameModeKind.MurderMystery;
        bool MysteryActive=>Active&&mystery!=null;
        public MysteryPrivateFrame Personal=>Authority&&MysteryActive?mystery.Personal(LocalActorId):personal;
        public MysteryPublicFrame Clues=>frame.mystery;
        public string CaptureSecret(string id)=>JsonUtility.ToJson(ReplaySecret(id));
        public void ReceiveSecret(string json)
        {
            if(Authority||json==null||json.Length>8192)return;
            try{var value=JsonUtility.FromJson<MysteryPrivateFrame>(json);if(value!=null&&Enum.IsDefined(typeof(MysteryRole),value.role)&&value.disasterChoices!=null&&value.disasterChoices.Length<=3&&ModeFrameValidation.Replay(value.replay))personal=value;}catch{}
        }
        void BeginMystery()
        {
            ResetMysteryReplay();
            mystery=null;reviving.Clear();corpseDeaths.Clear();footprintNext.Clear();
            ClearMysteryBodies();trailFrames.Clear();trailCells.Clear();footprintLast.Clear();mysteryStrikes.Clear();
            witnessedMurderers.Clear();unavailableBadges.Clear();
            if(!Active)return;
            mysteryRound=UnityEngine.Random.Range(1,int.MaxValue);
            rolePreferences[LocalActorId]=MysteryChoices.RequestedRole;
            mystery=new MysterySimulation(Rules.mystery,UnityEngine.Random.Range(1,int.MaxValue));
            mystery.DisasterChosen+=id=>DemoDisasters.ExecuteForMode(session,id);
            mystery.Start(simulation.Participants.Where(p=>p.present).Select(p=>p.frame.id),DemoDisasters.Prefabs.Select(p=>p.name),rolePreferences);
        }
        void TickMystery(float seconds)
        {
            if(!Active||simulation.Phase!=RoundPhase.Playing)return;
            if(mystery==null)BeginMystery();
            mystery.Tick(seconds);
            foreach(var body in mysteryBodies)if(NetworkActors.Alive(body.Value)){var point=body.Value.Position;float water=Services.World?.WaterHeight(point)??-100;mystery.UpdateBody(body.Key,new Vector3(point.x,Mathf.Max(water+.3f,point.y),point.z),point.y<water-.5f);}else mystery.UpdateBody(body.Key,Services.World?.Center??Vector3.zero,true);
            TickMysteryTrails();
            TickMysteryBots();
            foreach(var person in simulation.Participants)if(person.present&&!person.frame.alive&&mystery.Alive(person.frame.id))mystery.NaturalDeath(person.frame.id);
            foreach(var pair in reviving.ToArray())
            {
                string helper=pair.Key,target=pair.Value.target;
                if(Time.time-pair.Value.seen>.4f||!mystery.IsDowned(target)||!mystery.Alive(helper)||!Close(helper,FindActor(target)?.transform.position??Vector3.positiveInfinity)){reviving.Remove(helper);continue;}
                if(Time.time-pair.Value.start<Mathf.Max(.2f,Rules.mystery.reviveSeconds))continue;
                if(mystery.Revive(helper,target)){simulation.Find(helper).frame.revives++;simulation.Find(target).frame.role="survivor";RestoreMysteryHealth(target);}
                reviving.Remove(helper);
            }
            if(mystery.Winner.Length>0)simulation.FinishMystery(mystery.Winner);
            else if(Rules.roundSeconds>0&&simulation.Elapsed>=Rules.roundSeconds)simulation.FinishMystery("Innocents");
        }
        bool Close(string id,Vector3 position)
        {
            var actor=FindActor(id);if(actor==null)return false;
            var start=actor.ViewPose.position;var delta=position+Vector3.up*.4f-start;
            return delta.sqrMagnitude<=Mathf.Pow(Mathf.Max(1,Rules.mystery.interactDistance)+.8f,2)&&!Physics.Raycast(start,delta.normalized,delta.magnitude*.85f,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore);
        }
        void ClearMysteryBodies(){foreach(var b in mysteryBodies.Values)if(NetworkActors.Alive(b))Destroy(b.gameObject);mysteryBodies.Clear();}
        void ApplyMysteryBodies()
        {
            if(!Active){if(mysteryBodies.Count>0)ClearMysteryBodies();return;}
            foreach(var b in frame.mystery.bodies)
            {
                if(mysteryBodies.ContainsKey(b.id)||b.lost)continue;
                var actor=FindActor(b.id);if(actor==null)continue;
                var source=actor.gameObject.GetComponentInChildren<IMysteryBodySource>(true);if(source==null)continue;
                var body=source.CreateMysteryBody();var blocks=Services.World?.Blocks;
                body.Follow(blocks!=null&&b.supportBlock>=0&&b.supportBlock<blocks.Count?blocks[b.supportBlock]:null,b.position);mysteryBodies[b.id]=body;
            }
        }
        void AnnounceMysteryStrike(string id)
        {if(MysteryActive&&mystery.Personal(id).specialAttack&&mystery.Personal(id).attackReadyIn<=0)mysteryStrikes[id]=Time.time+.45f;}
        MysteryPublicFrame MysteryFrame()
        {
            var result=mystery.Public();result.strikes=mysteryStrikes.Where(p=>p.Value>Time.time).Select(p=>new MysteryStrikeFrame{actor=p.Key,role=mystery.Role(p.Key),remaining=p.Value-Time.time}).ToArray();return result;
        }
        void TickMysteryTrails()
        {
            if(Time.time<nextMysteryTrail)return;nextMysteryTrail=Time.time+.15f;
            foreach(var pair in actors)
            {
                var actor=pair.Value;if(!NetworkActors.Alive(actor)||!mystery.Alive(pair.Key)||mystery.IsDowned(pair.Key)||!actor.Grounded)continue;
                int index=-1;if(actor.StandingBody)blockIds.TryGetValue(actor.StandingBody,out index);
                if(actor.StandingBody&&!blockIds.ContainsKey(actor.StandingBody))index=-1;
                if(footprintNext.TryGetValue(pair.Key,out var next)&&Time.time<next)continue;
                var point=actor.transform.position;if(Physics.Raycast(point+Vector3.up*.15f,Vector3.down,out var floor,.5f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))point=floor.point;
                if(footprintLast.TryGetValue(pair.Key,out var last)&&(point-last).sqrMagnitude<.36f)continue;
                footprintLast[pair.Key]=point;footprintNext[pair.Key]=Time.time+.25f;
                var local=index>=0?actor.StandingBody.transform.InverseTransformPoint(point):point;Color color=simulation.Find(pair.Key).frame.color;
                string cell=index+":"+Mathf.RoundToInt(local.x*3)+":"+Mathf.RoundToInt(local.y*3)+":"+Mathf.RoundToInt(local.z*3)+":"+ColorUtility.ToHtmlStringRGB(color);
                if(trailFrames.Count>=100000||!trailCells.Add(cell))continue;
                var trail=new MysteryTrailFrame{block=index,point=local,normal=index>=0?actor.StandingBody.transform.InverseTransformDirection(actor.StandingNormal):actor.StandingNormal,yaw=actor.transform.eulerAngles.y-(index>=0?actor.StandingBody.rotation.eulerAngles.y:0),color=color};mystery.AddTrail(trail);trailFrames.Add(trail);
            }
        }
        [Serializable] public sealed class TrailPacket {public int revision;public MysteryTrailFrame[] trails;}
        int mysteryRound;
        float nextMysteryTrail;
        public int TrailRound=>mysteryRound;
        public string CaptureTrails(int after)=>JsonUtility.ToJson(new TrailPacket{revision=mysteryRound,trails=trailFrames.Skip(Mathf.Max(0,after)).Take(64).ToArray()});
        int receivedMysteryRound=-1;
        public void ReceiveTrails(string json)
        {
            if(Authority||json==null||json.Length>64000)return;
            try{var packet=JsonUtility.FromJson<TrailPacket>(json);if(packet?.trails==null||packet.trails.Length>64)return;if(packet.revision!=receivedMysteryRound){trailFrames.Clear();receivedMysteryRound=packet.revision;}
                foreach(var t in packet.trails){if(t==null||t.sequence<=trailFrames.Count||t.block<-1||t.block>=(Services.World?.Blocks?.Count??0)||!float.IsFinite(t.point.sqrMagnitude)||!float.IsFinite(t.normal.sqrMagnitude)||!float.IsFinite(t.yaw))continue;if(t.sequence!=trailFrames.Count+1)return;trailFrames.Add(t);}}
            catch{}
        }
        void MysteryRequest(string value)
        {if(Authority)ReceiveMysteryCommand(LocalActorId,value);else CommandRequested?.Invoke("mystery:"+value);}
        public void SetSpecialAttack(bool enabled)=>MysteryRequest("special:"+(enabled?"1":"0"));
        public void ReportBody(string bodyId)=>MysteryRequest("report:"+bodyId);
        public void Revive(string playerId,bool held)=>MysteryRequest("revive:"+(held?playerId:""));
        public void Suspect(string bodyId,int trailSequence)=>MysteryRequest("suspect:"+trailSequence+":"+bodyId);
        public void ChooseDisaster(string hazardId)=>MysteryRequest("disaster:"+hazardId);
        public void SetDemoRole(MysteryRole role)
        {if(Authority&&MysteryActive&&Enum.IsDefined(typeof(MysteryRole),role))mystery.ForceRole(LocalActorId,role);}
        public void ConfigureMystery(MysterySettings value)
        {if(Authority&&value!=null&&ValidMysterySettings(value)){var copy=Rules.Copy();copy.mystery=value;Configure(copy);}}
        public static bool ValidMysterySettings(MysterySettings value)
        {
            if(value==null||value.sheriffMistakes<1||value.sheriffMistakes>10)return false;
            foreach(var f in typeof(MysterySettings).GetFields())if(f.FieldType==typeof(float)){float v=(float)f.GetValue(value);if(!float.IsFinite(v)||v<0||v>3600)return false;}
            return value.interactDistance<=5&&value.trailRadius<=15&&value.disasterMaxSeconds>=value.disasterMinSeconds;
        }
        void ReceiveMysteryCommand(string sender,string command)
        {
            if(!Active||string.IsNullOrEmpty(command)||command.Length>512)return;
            if(command.StartsWith("preference:")&&simulation.Phase!=RoundPhase.Playing&&Enum.TryParse(command.Substring(11),out MysteryRole choice)&&Enum.IsDefined(typeof(MysteryRole),choice)){rolePreferences[sender]=choice;return;}
            if(!MysteryActive||!mystery.Alive(sender))return;
            if(command.StartsWith("special:")){mystery.SetSpecial(sender,command.Substring(8)=="1");return;}
            if(command.StartsWith("revive:"))
            {
                string target=command.Substring(7);if(target.Length==0){reviving.Remove(sender);return;}
                if(!mystery.IsDowned(sender)&&mystery.IsDowned(target)&&Close(sender,FindActor(target)?.transform.position??Vector3.positiveInfinity))
                {if(reviving.TryGetValue(sender,out var current)&&current.target==target)reviving[sender]=(target,current.start,Time.time);else reviving[sender]=(target,Time.time,Time.time);}return;
            }
            if(command.StartsWith("disaster:")){mystery.Choose(sender,command.Substring(9));return;}
            if(command.StartsWith("report:"))
            {
                string id=command.Substring(7);var body=mystery.Public().bodies.FirstOrDefault(b=>b.id==id);
                if(body!=null&&Close(sender,body.position)){if(mystery.Report(sender,id,simulation.Find(sender).frame.color))simulation.Find(sender).frame.bodyReports++;}return;
            }
            if(command.StartsWith("suspect:"))
            {
                var parts=command.Substring(8).Split(new[]{':'},2);if(parts.Length!=2||!int.TryParse(parts[0],out int index))return;
                var trail=mystery.Trail(index);var body=mystery.Public().bodies.FirstOrDefault(b=>b.id==parts[1]);var blocks=Services.World?.Blocks;
                if(trail==null||body==null||blocks==null||trail.block<-1||trail.block>=blocks.Count||trail.block>=0&&!blocks[trail.block])return;
                var point=trail.block<0?trail.point:blocks[trail.block].transform.TransformPoint(trail.point);
                if(Close(sender,point)&&(point-body.position).sqrMagnitude<=Rules.mystery.trailRadius*Rules.mystery.trailRadius)mystery.Suspect(sender,body.id,index);
            }
        }
        void RestoreMysteryHealth(string id)
        {
            var actor=FindActor(id);if(actor==null)return;
            ResetRemoteHealth(id);
            if(actor.IsLocal||actor.IsBot)
            {var point=actor.transform.position;var rotation=actor.transform.rotation;actor.gameObject.GetComponent<IPlayableCharacter>()?.Place(point,rotation);}
            else RemoteEliminated?.Invoke(id,EliminationOutcome.Revive);
        }
        bool MysteryPunch(string attacker,string victim,ref float damage)
        {
            if(!MysteryActive)return false;
            var hit=mystery.Hit(attacker,victim);if(hit==MysteryHit.Blocked)return true;
            var target=FindActor(victim);if(target==null)return true;
            bool special=hit==MysteryHit.Murder||hit==MysteryHit.SheriffKill;
            float health=target.IsLocal||target.IsBot?target.Health:target.MaxHealth*RemoteHealth(victim);
            if(!special&&damage<health)return false;
            var p=simulation.Find(victim).frame;
            int support=target.StandingBody&&blockIds.TryGetValue(target.StandingBody,out int block)?block:-1;
            var body=new MysteryBodyFrame{id=victim,name=p.name,color=p.color,position=target.transform.position,supportBlock=support};
            if(support>=0&&support<Services.World.Blocks.Count&&Services.World.Blocks[support])body.supportPoint=Services.World.Blocks[support].transform.InverseTransformPoint(body.position);
            bool murdered=mystery.Role(attacker)==MysteryRole.Murderer;
            var result=mystery.Lethal(attacker,victim,special,body);
            if(result==MysteryHit.Blocked)return true;
            if(mystery.IsDowned(victim)){RestoreMysteryHealth(victim);simulation.Find(victim).frame.role="downed";}
            else
            {RecordMysteryReplay(attacker,victim,murdered);if(murdered)corpseDeaths.Add(victim);simulation.Kill(victim,attacker);FinishMysteryHealth(victim,FindActor(attacker)?.gameObject);}
            if(!mystery.Alive(attacker)){simulation.Kill(attacker,null);FinishMysteryHealth(attacker,null);}
            return true;
        }
        void FinishMysteryHealth(string id,GameObject source)
        {
            var actor=FindActor(id);var participant=simulation.Find(id);if(participant!=null)participant.frame.health=0;
            if(actor==null)return;
            if(actor.IsLocal||actor.IsBot)actor.gameObject.GetComponent<IDamageable>()?.Eliminate(source);
            else remoteHealth.Seed(id,0,true);
        }
    }
}
