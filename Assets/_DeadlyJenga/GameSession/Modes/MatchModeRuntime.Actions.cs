using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        public bool RequestPunch(GameObject source,Vector3 origin,Vector3 direction,float speed)
        {
            if(!source||!actorIds.TryGetValue(source,out string id)||direction.sqrMagnitude<.1f||float.IsNaN(speed)||float.IsInfinity(speed))return false;
            if(!Authority){CommandRequested?.Invoke("punch:"+JsonUtility.ToJson(new PunchCommand{origin=origin,direction=direction,speed=speed}));return true;}
            return Punch(id,origin,direction,speed);
        }
        [Serializable]public sealed class PunchCommand{public Vector3 origin,direction;public float speed;}
        public bool Punch(string id,Vector3 origin,Vector3 direction,float speed)
        {
            if(float.IsNaN(speed)||float.IsInfinity(speed)||float.IsNaN(origin.sqrMagnitude)||float.IsInfinity(origin.sqrMagnitude)||float.IsNaN(direction.sqrMagnitude)||float.IsInfinity(direction.sqrMagnitude))return false;
            var held=EquipmentFor(id);if(held.builtIn)return AttackEquipment(id,held,new EquipmentCommand{position=origin,forward=direction});if(held.drawn&&!held.roleIssued)return false;
            if(held.drawn&&held.kind==EquipmentKind.SheriffHammer)return AttackEquipment(id,held,new EquipmentCommand{action="attack",position=origin,forward=direction});
            var p=simulation.Find(id);var source=FindActor(id);
            if(p==null||source==null||!p.frame.alive||p.frame.role=="hand"||simulation.Phase!=RoundPhase.Playing||!RulesFor(source.gameObject).punchEnabled||simulation.CarriesFlag(id)||Time.time<p.nextPunch||speed<.4f||speed>50)return false;
            if((origin-source.ViewPose.position).sqrMagnitude>9||direction.sqrMagnitude<.1f)return false;
            p.nextPunch=Time.time+RulesFor(source.gameObject).punchCooldown;direction.Normalize();
            AnnounceMysteryStrike(id);
            if(held.roleIssued&&held.drawn)AnnounceEquipment(id,held,EquipmentAction.Shot,origin);
            var enemy=source.gameObject.GetComponent<IEnemyActor>();
            IModeActor target=null;float nearest=enemy?.IsEnemy==true&&enemy.ArchetypeId=="longhand"?3.4f:2.2f;
            foreach(var pair in actors)
            {
                if(pair.Key==id)continue;
                var other=pair.Value;var state=simulation.Find(pair.Key);
                if(!NetworkActors.Alive(other)||state==null||!state.frame.alive||state.frame.dying||state.frame.role=="hand"||state.frame.role=="spectator")continue;
                Vector3 delta=other.transform.position+Vector3.up*.6f-origin;
                float distance=delta.magnitude;
                if(distance>=nearest||distance<.01f||Vector3.Dot(delta/distance,direction)<.45f)continue;
                if(Physics.Raycast(origin,delta/distance,out var hit,distance,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore))continue;
                target=other;nearest=distance;
            }
            if(target==null)return true;
            string targetId=actorIds[target.gameObject];
            if(simulation.Shielded(targetId,direction))return true;
            var rules=RulesFor(source.gameObject);
            float distanceScale=rules.punchDistance;
            var push=Vector3.ProjectOnPlane(direction,Vector3.up).normalized*Mathf.Sqrt(2*9.81f*Mathf.Max(0,distanceScale))*.65f+Vector3.up*.8f;
            float damage=rules.healthEnabled?rules.punchDamage:0;
            if(source.gameObject.GetComponent<IEnemyActor>()?.IsEnemy!=true&&target.gameObject.GetComponent<IEnemyActor>()?.IsEnemy!=true&&MysteryPunch(id,targetId,ref damage))return true;
            var targetState=simulation.Find(targetId);
            if(!rules.friendlyFire && (p.frame.team>=0&&p.frame.team==targetState?.frame.team || p.frame.role=="infected"&&targetState?.frame.role=="infected"))damage=0;
            if(target.IsLocal||target.IsBot)
            {
                target.gameObject.GetComponent<IPushable>()?.Push(push,source.gameObject);
                if(damage>0)target.gameObject.GetComponent<IDamageable>()?.Damage(damage,source.gameObject);
            }
            else {if(damage>0)RecordRemoteDamage(targetId,damage,id);RemoteImpact?.Invoke(targetId,push,damage);}
            return true;
        }
        public void ReceiveCommand(string sender,string command)
        {
            if(!Authority||string.IsNullOrEmpty(command)||command.Length>8192)return;
            if(command.StartsWith("equipment:",StringComparison.Ordinal)){try{EquipmentCommandFor(sender,JsonUtility.FromJson<EquipmentCommand>(command.Substring(10)));}catch(ArgumentException){}return;}
            if(command.StartsWith("blaster:",StringComparison.Ordinal)){try{FireBlaster(sender,JsonUtility.FromJson<BlasterCommand>(command.Substring(8)));}catch(ArgumentException){}return;}
            if(command.StartsWith("mystery:")){ReceiveMysteryCommand(sender,command.Substring(8));return;}
            if(command.StartsWith("punch:"))
            {try{var p=JsonUtility.FromJson<PunchCommand>(command.Substring(6));if(p!=null)Punch(sender,p.origin,p.direction,p.speed);}catch{}return;}
            if(command.StartsWith("spectate:")){if(int.TryParse(command.Substring(9),out int step))simulation.NextSpectator(sender,step);return;}
            // Configuration and powers are accepted only by the host's local UI command path.
        }
        public void ReceiveAdminCommand(string sender,string command)
        {
            if(!Authority||Rules.mode!=GameModeKind.Demo||string.IsNullOrEmpty(command)||command.Length>8192)return;
            if(command=="start"){StartRound();return;}
            if(command.StartsWith("hand:")){if(float.TryParse(command.Substring(5),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float time)&&!float.IsNaN(time)&&!float.IsInfinity(time))simulation.HandPower(sender,Mathf.Clamp(time,1,120));return;}
            if(command.StartsWith("configure:"))try{var rules=JsonUtility.FromJson<ModeRules>(command.Substring(10));if(rules!=null&&rules.mode==GameModeKind.Demo)Configure(rules);}catch{}
        }
        void OnEliminated(string id,EliminationOutcome outcome)
        {
            var deadActor=FindActor(id);if(deadActor!=null)DropEquipment(id,deadActor.transform.position);
            if(corpseDeaths.Contains(id)){ApplyOutcome(id,outcome);return;}
            if(outcome==EliminationOutcome.FlyingHand){ApplyOutcome(id,outcome);return;}
            if(transitionUntil.ContainsKey(id))return;
            var p=simulation.Find(id);if(p!=null){p.frame.dying=true;p.frame.deathProgress=0;p.frame.deathRemaining=.9f;p.frame.pendingOutcome=outcome.ToString();}
            transitionUntil[id]=Time.time+.9f;delayedOutcomes[id]=outcome;sinkStarted.Remove(id);
            if(p!=null){p.frame.sinking=false;p.frame.sinkProgress=0;}CaptureStats();
        }
        void TickTransitions()
        {
            if(!Authority||transitionUntil.Count==0)return;
            var done=new List<string>();foreach(var p in transitionUntil){var actor=simulation.Find(p.Key);if(actor!=null){actor.frame.deathRemaining=Mathf.Max(0,p.Value-Time.time);actor.frame.deathProgress=Mathf.Clamp01(1-actor.frame.deathRemaining/.9f);}if(Time.time>=p.Value)done.Add(p.Key);}
            foreach(string id in done){transitionUntil.Remove(id);var p=simulation.Find(id);if(p!=null){p.frame.dying=false;p.frame.deathProgress=0;p.frame.deathRemaining=0;p.frame.pendingOutcome=null;}var outcome=delayedOutcomes[id];delayedOutcomes.Remove(id);ApplyOutcome(id,outcome);}
        }
        void ApplyOutcome(string id,EliminationOutcome outcome)
        {
            var enemy=FindActor(id)?.gameObject.GetComponent<IEnemyActor>();if(enemy?.IsEnemy==true){enemy.Despawn();return;}
            CaptureStats();
            if(id!=LocalActorId&&connectedHumans.Contains(id)){ResetRemoteHealth(id);RemoteEliminated?.Invoke(id,outcome);return;}
            var actor=FindActor(id);if(actor==null)return;
            if(id!=LocalActorId&&!actor.IsBot){RemoteEliminated?.Invoke(id,outcome);return;}
            if(outcome==EliminationOutcome.Spectator){actor.ApplyModeRules(RulesFor(actor.gameObject));return;}
            if(id==LocalActorId)
            {
                if(outcome==EliminationOutcome.FlyingHand){var at=session.Player.transform.position;session.SetRole(true);session.Hand.Place(at+Vector3.up*2,session.Player.transform.rotation);return;}
                if(outcome==EliminationOutcome.Spectator){actor.ApplyModeRules(RulesFor(actor.gameObject));return;}
                if(session.PlayingHand)session.SetRole(false);
            }
            Respawn(id,simulation.Find(id)?.frame.role??"survivor");CaptureStats();
        }
        void Respawn(string id,string role)
        {
            var actor=FindActor(id);var p=simulation.Find(id);if(actor==null||p==null)return;
            if(id!=LocalActorId&&!actor.IsBot){ResetRemoteHealth(id);RemoteEliminated?.Invoke(id,EliminationOutcome.Respawn);return;}
            int ordinal=ParticipantOrdinal(p);
            Vector3 point=SpawnPoint(p.frame.team,ordinal,role);
            var rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane((Services.World?.Center??Vector3.zero)-point,Vector3.up).normalized+Vector3.forward*.001f);
            if(actor.gameObject.GetComponent<IPlayableCharacter>() is IPlayableCharacter playable)playable.Place(point,rotation);
            else actor.gameObject.GetComponent<ITeleportable>()?.Teleport(point,rotation);
            actor.ApplyModeRules(RulesFor(actor.gameObject));
        }
        readonly Dictionary<string,Pose> powerReturnOverrides=new Dictionary<string,Pose>();
        public Pose OutcomeSpawn(string id)
        {
            if(powerReturnOverrides.TryGetValue(id,out var powerReturn)){powerReturnOverrides.Remove(id);return powerReturn;}
            if(MysteryActive&&mystery.IsDowned(id)){var actor=FindActor(id);if(actor!=null)return new Pose(actor.transform.position,actor.transform.rotation);}
            var p=simulation.Find(id);var point=SpawnPoint(p?.frame.team??-1,ParticipantOrdinal(p),p?.frame.role??"survivor");
            var direction=Vector3.ProjectOnPlane((Services.World?.Center??Vector3.zero)-point,Vector3.up);
            return new Pose(point,direction.sqrMagnitude>.01f?Quaternion.LookRotation(direction):Quaternion.identity);
        }
        int ParticipantOrdinal(ModeSimulation.Participant target)
        {
            int n=0;foreach(var p in simulation.Participants){if(p==target)break;if(!ModeSimulation.TeamMode(Rules.mode)||p.frame.team==target?.frame.team)n++;}return n;
        }
        void OnPowerFinished(string id)
        {
            var root=FindParticipantRoot(id);
            var flyer=root?root.GetComponent<IFlyingState>():null;
            var near=flyer?.ContactBodies!=null&&flyer.ContactBodies.Count>0&&flyer.ContactBodies[0]?flyer.ContactBodies[0].position:flyer?.Anchor?flyer.Anchor.position:root?root.position:Services.World.SafeSpawnPoint();
            var point=SafeHandReturn(near);if(id!=LocalActorId)powerReturnOverrides[id]=new Pose(point,Quaternion.identity);ApplyOutcome(id,EliminationOutcome.Respawn);
            if(id==LocalActorId)session.Player.Place(point,Quaternion.identity);
        }
        Vector3 SafeHandReturn(Vector3 near)
        {
            for(int i=0;i<37;i++)
            {
                float angle=i*2.399963f,radius=i==0?0:2+i/12*3;
                var origin=near+new Vector3(Mathf.Sin(angle)*radius,3,Mathf.Cos(angle)*radius);
                if(!Physics.Raycast(origin,Vector3.down,out var hit,160,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)||hit.normal.y<.65f)continue;
                if(Rules.island!=IslandRule.Walkable&&!Climbables.TryGet(hit.collider,out _))continue;
                var point=hit.point+Vector3.up*.06f;
                if(!Physics.CheckCapsule(point+Vector3.up*.28f,point+Vector3.up*1.5f,.25f,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))return point;
            }
            return Services.World.SafeSpawnPoint();
        }
        public void ApplyRemoteOutcome(EliminationOutcome outcome,Vector3 spawn,Quaternion rotation,string role)
        {
            if(Authority||session==null)return;
            if(outcome==EliminationOutcome.FlyingHand){var at=session.Player.transform.position;var facing=session.Player.transform.rotation;session.SetRole(true);session.Hand.Place(at+Vector3.up*2,facing);return;}
            if(outcome==EliminationOutcome.Spectator)return;
            if(session.PlayingHand)session.SetRole(false);
            session.Player.Place(spawn,rotation);
        }
        void OnPhase(RoundPhase phase)
        {
            if(phase==RoundPhase.Countdown){ResetObjectiveDirector("New round");ResetBotPerception();mystery=null;ClearMysteryBodies();}
            if(phase==RoundPhase.Waiting||phase==RoundPhase.Victory||phase==RoundPhase.Results)ResetObjectiveDirector("Round phase changed");
            if(phase==RoundPhase.Playing){ConfigureStatCollectors();BeginMystery();analyzer=new RoundStatAnalyzer(Time.timeAsDouble);sinkStarted.Clear();disasters.Reset(Rules,simulation.Elapsed);CaptureStats();GameEvents.RaiseRoundStarted();}
            if(phase==RoundPhase.Victory)
            {
                FinishMysteryReplay();
                CaptureStats();
                FinishReport();
                GameEvents.RaiseRoundEnded();
                DemoDisasters.StopAll(session);
            }
        }
        void CaptureStats()
        {
            if(!Authority||stats==null||analyzer==null)return;
            var samples=new List<SurvivorStatSample>();
            foreach(var p in simulation.Participants)if(p.present)samples.Add(new SurvivorStatSample(p.frame.id,p.frame.alive&&!p.frame.dying&&p.frame.role!="infected"&&p.frame.role!="hand"&&p.frame.role!="spectator",p.frame.bot,p.frame.height));
            LatestStats=stats.Sample(samples,Time.timeAsDouble);analyzer.Observe(LatestStats);
        }
        void OnOwnership(int block,int team)
        {
            var bodies=Services.World?.Blocks;if(bodies==null||block<0||block>=bodies.Count||!bodies[block])return;
            var color=team>=0?ModeNames.Colors[team]:Color.white;
            bodies[block].GetComponent<IBlockOwnership>()?.SetOwner(team,color);
            if(team>=0)GameEvents.RaiseEmoted(bodies[block].gameObject,"BlockClaimed");
        }
    }
}
