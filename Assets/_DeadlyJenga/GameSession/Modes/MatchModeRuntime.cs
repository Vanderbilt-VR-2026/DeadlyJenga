using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    [DefaultExecutionOrder(250)]
    public sealed partial class MatchModeRuntime : MonoBehaviour, IMatchRuntime, IBotObjectives, IBotObjectiveExecution, IMysteryControls, IMatchReviewControls
    {
        CastlePlaySession session;
        ModeSimulation simulation;
        readonly Dictionary<string,IModeActor> actors=new Dictionary<string,IModeActor>(StringComparer.Ordinal);
        readonly Dictionary<GameObject,string> actorIds=new Dictionary<GameObject,string>();
        readonly Dictionary<Rigidbody,int> blockIds=new Dictionary<Rigidbody,int>();
        readonly Dictionary<string,float> sinkStarted=new Dictionary<string,float>();
        readonly Dictionary<string,float> transitionUntil=new Dictionary<string,float>();
        readonly Dictionary<string,EliminationOutcome> delayedOutcomes=new Dictionary<string,EliminationOutcome>();
        readonly Dictionary<string,ModeRules> ruleCache=new Dictionary<string,ModeRules>();
        SessionStats stats;
        RoundStatAnalyzer analyzer;
        MatchFrame frame=new MatchFrame();
        float nextSample,nextHill;
        int lastBlocks=-1;
        bool wasAuthority;
        readonly HashSet<string> connectedHumans=new HashSet<string>();
        readonly HashSet<GameObject> localBotRoots=new HashSet<GameObject>();
        readonly Dictionary<string,float> pendingBotHealth=new Dictionary<string,float>();
        readonly Dictionary<string,float> pendingSpawns=new Dictionary<string,float>();
        public ModeRules Rules => Authority ? simulation?.Rules ?? frame.rules : frame.rules;
        public MatchFrame Frame => frame;
        public string LocalActorId => session&&session.Network?session.Network.LocalParticipantId:"local";
        public bool Authority => session&&session.HasWorldAuthority;
        public PostGameReport LatestReport {get;private set;}
        public SessionStatsSnapshot LatestStats {get;private set;}
        public event Action<string,EliminationOutcome> RemoteEliminated;
        public event Action<string,Vector3,float> RemoteImpact;
        public event Action<string> CommandRequested;
        public Camera OverrideCamera { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterDefaults()=>ModeDefaults.Create=mode=>MatchSetup.DefaultModeRules(mode,MatchSetup.SelectedMap);

        public void Initialize(CastlePlaySession owner)
        {
            session=owner;MatchRuntime.Current=this;InitializeEnemyActions();
            BotObjectives.Current=this;
            if(session.Network)session.Network.ParticipantLeft+=UnregisterRemote;
            wasAuthority=Authority;
            var initial=MatchSetup.DefaultModeRules(ParseMode(MatchSetup.ModeId),MatchSetup.SelectedMap);
            var lobby=IslandRoomConnection.Instance;
            if(MatchSetup.FromLobby&&lobby&&lobby.Setup.rules.mode==initial.mode)initial=lobby.Setup.rules;
            initial.sameHumanTeam=MatchChoices.SameHumanTeam;
            simulation=new ModeSimulation(initial);
            simulation.Eliminated+=OnEliminated;
            simulation.PhaseChanged+=OnPhase;
            simulation.OwnershipChanged+=OnOwnership;
            simulation.PowerFinished+=OnPowerFinished;
            ConfigureStatCollectors();
            RefreshBlocks();SampleActors();
            simulation.Hill=HighestSupport();
            if(Authority){simulation.StartRound();SetFlagHomes();foreach(var p in simulation.Participants)Respawn(p.frame.id,p.frame.role);frame=simulation.Snapshot();PublishEnemyActions();PublishEquipment();}
        }
        public static GameModeKind ParseMode(string id)
        {
            switch((id??"").ToLowerInvariant())
            {
                case "survival":case "classic":return GameModeKind.Survival;
                case "kingofthehill":case "king-of-hill":case "hill":return GameModeKind.KingOfHill;
                case "blockcontrol":case "red-blue":case "teams":return GameModeKind.BlockControl;
                case "captureflag":case "capture-the-flag":case "ctf":return GameModeKind.CaptureFlag;
                case "infection":return GameModeKind.Infection;
                case "teamtickets":case "last-team-standing":return GameModeKind.TeamTickets;
                case "mystery":case "murder-mystery":return GameModeKind.MurderMystery;
                default:return GameModeKind.Demo;
            }
        }
        void OnDestroy()
        {
            ResetObjectiveDirector("Session destroyed");
            ClearDemoCameras();ClearEnemyActions();equipmentAnnouncements.Clear();
            ClearMysteryBodies();
            if(ReferenceEquals(MatchRuntime.Current,this))MatchRuntime.Current=null;
            if(ReferenceEquals(BotObjectives.Current,this))BotObjectives.Current=null;
            if(session&&session.Network)session.Network.ParticipantLeft-=UnregisterRemote;
            if(simulation!=null){simulation.Eliminated-=OnEliminated;simulation.PhaseChanged-=OnPhase;simulation.OwnershipChanged-=OnOwnership;simulation.PowerFinished-=OnPowerFinished;}
        }
        public IModeActor FindActor(string id)=>id!=null&&actors.TryGetValue(id,out var actor)&&NetworkActors.Alive(actor)?actor:null;
        public Transform FindParticipantRoot(string id)
        {
            if(string.IsNullOrEmpty(id)||session==null)return null;
            if(id==LocalActorId)return session.PlayingHand?session.Hand?.transform:session.Player?.transform;
            var actor=FindActor(id);if(actor!=null&&localBotRoots.Contains(actor.gameObject))return actor.transform;
            var remote=session.Network?session.Network.ParticipantTransform(id):null;
            return remote?remote:actor?.transform;
        }
        public void RegisterRemote(string id,GameObject root)
        {
            if(!root||string.IsNullOrEmpty(id))return;
            var actor=root.GetComponentInChildren<IModeActor>(true);if(actor==null)return;
            Register(id,actor,false);
        }
        public void UnregisterRemote(string id){if(actors.TryGetValue(id,out var a)){if(NetworkActors.Alive(a)){if(Authority)DropEquipment(id,a.transform.position);actorIds.Remove(a.gameObject);}actors.Remove(id);}simulation?.Remove(id);}
        void Register(string id,IModeActor actor,bool local)
        {
            if(string.IsNullOrEmpty(id)||!NetworkActors.Alive(actor))return;
            if(actorIds.TryGetValue(actor.gameObject,out var previousId)&&previousId!=id){mystery?.Rekey(previousId,id);actors.Remove(previousId);simulation?.Remove(previousId);}
            actors[id]=actor;actorIds[actor.gameObject]=id;
            if(!Authority)return;
            bool fresh=simulation.Find(id)==null;
            var identity=!actor.IsBot&&MatchSetup.FromLobby&&IslandRoomConnection.Instance?IslandRoomConnection.Instance.IdentityFor(id):null;
            bool enemy=actor.gameObject.GetComponent<IEnemyActor>()?.IsEnemy==true;
            var p=enemy?simulation.RegisterEnemy(id,new Color(.035f,.045f,.065f)):simulation.Register(id,identity?.color??actor.Color,actor.IsBot,local);
            if(!enemy&&p!=null&&p.frame.alive&&MysteryActive&&mystery.Role(id)==MysteryRole.Random)mystery.RegisterLate(id);
            if(fresh&&identity!=null)simulation.PreserveIdentity(id,identity.noun);
            if(p!=null)actor.SetIdentity(id,p.frame.name,p.frame.team,p.frame.color);
            if(fresh&&!local&&!actor.IsBot&&p!=null&&!p.frame.dying&&p.frame.role=="survivor"&&Rules.mode!=GameModeKind.Demo)pendingSpawns[id]=Time.time+.3f;
        }
        void Update()
        {
            if(session==null||simulation==null)return;
            if(!Authority&&wasAuthority){ResetObjectiveDirector("Authority lost");ResetBlaster();}
            if(Authority&&!wasAuthority)RestoreAuthorityState(frame);
            wasAuthority=Authority;
            if(Authority)simulation.Tick(Time.deltaTime);
            if(Authority)TickMystery(Time.deltaTime);
            TickRemoteRecovery(Time.deltaTime);
            TickEquipment(Time.deltaTime);TickEnemyActions(Time.deltaTime);
            TickBlaster(Time.deltaTime);
            TickTransitions();TickSpectatorInput();
            if(Authority&&pendingSpawns.Count>0)
            {
                var ready=new List<string>();foreach(var spawn in pendingSpawns)if(Time.time>=spawn.Value)ready.Add(spawn.Key);
                foreach(var id in ready){pendingSpawns.Remove(id);ResetRemoteHealth(id);RemoteEliminated?.Invoke(id,EliminationOutcome.Respawn);}
            }
            if(Time.unscaledTime<nextSample)return;
            nextSample=Time.unscaledTime+.2f;
            RefreshBlocks();SampleActors();
            if(Authority)
            {
                ruleCache.Clear();
                TickIsland();TickFlagGravity();
                TickDisasters();
                if(Rules.mode!=GameModeKind.Demo&&Time.time>=nextHill){simulation.Hill=HighestSupport();nextHill=Time.time+Rules.hillInterval;}
                frame=simulation.Snapshot();PublishEnemyActions();
                frame.blaster=CaptureBlaster();PublishEquipment();
                if(MysteryActive)frame.mystery=MysteryFrame();
                WriteDisasterFrame();
                CaptureStats();
                if(simulation.Phase==RoundPhase.Playing)RefreshBotDirector();
            }
            ApplyFrames();TickCamera();
        }
        IWorldQuery blockWorld;
        void RefreshBlocks()
        {
            var blocks=Services.World?.Blocks;if(blocks==null)return;
            if(ReferenceEquals(blockWorld,Services.World)&&lastBlocks==blocks.Count&&blockIds.Count>0)return;
            blockWorld=Services.World;
            blockIds.Clear();for(int i=0;i<blocks.Count;i++)if(blocks[i])blockIds[blocks[i]]=i;
            lastBlocks=blocks.Count;
        }
        void SampleActors()
        {
            connectedHumans.Clear();connectedHumans.Add(LocalActorId);localBotRoots.Clear();
            if(NetworkActors.Alive(session.Player))Register(LocalActorId,session.Player.gameObject.GetComponent<IModeActor>(),true);
            if(session.SurvivorBots!=null)foreach(var member in session.SurvivorBots.Members)
            {
                if(!member)continue;localBotRoots.Add(member);var actor=member.GetComponent<IModeActor>();if(actor==null)continue;
                string id=member.GetComponent<ISocialBot>()?.NetworkId??actor.ActorId;
                if(string.IsNullOrEmpty(id))id="actor:"+member.GetInstanceID();Register(id,actor,false);RestoreBotHealth(id,actor);
            }
            foreach(var networkActor in NetworkActors.Local)
            {
                if(!NetworkActors.Alive(networkActor)||!(networkActor is Component component)||!component)continue;
                var actor=component.GetComponent<IModeActor>();if(actor==null||ReferenceEquals(actor,session.Player))continue;
                string id=actor.gameObject.GetComponent<ISocialBot>()?.NetworkId??actor.ActorId;
                if(string.IsNullOrEmpty(id))id="actor:"+component.GetInstanceID();
                Register(id,actor,actor.IsLocal);
            }
            if(session.Network)foreach(var peer in session.Network.HumanPositions())
            {
                connectedHumans.Add(peer.Key);if(peer.Key==LocalActorId)continue;
                var t=session.Network.ParticipantTransform(peer.Key);if(t)RegisterRemote(peer.Key,t.gameObject);
                var state=simulation.Find(peer.Key);
                var kind=t?t.GetComponent<INetworkActor>()?.NetworkKind:null;
                if(Authority&&state!=null&&Rules.mode==GameModeKind.Demo&&(kind==NetworkKinds.Hand||kind==NetworkKinds.Survivor))state.frame.role=kind==NetworkKinds.Hand?"hand":"survivor";
                if(Authority&&state==null&&t&&t.GetComponent<INetworkActor>()?.NetworkKind==NetworkKinds.Hand)
                {state=simulation.Register(peer.Key,ModeNames.Colors[(peer.Key.GetHashCode()&int.MaxValue)%ModeNames.Colors.Length],false,false);if(state!=null)state.frame.role="hand";}
                if(Authority&&state!=null&&state.frame.role=="hand"){state.frame.position=peer.Value;state.frame.viewPosition=peer.Value+Vector3.up*2;}
            }
            var stale=new List<string>();
            foreach(var pair in actors)
            {
                var actor=pair.Value;if(!NetworkActors.Alive(actor)){stale.Add(pair.Key);continue;}
                if(!Authority)continue;
                int standing=-1;
                if(actor.Grounded&&actor.StandingNormal.y>=.65f&&actor.StandingBody)blockIds.TryGetValue(actor.StandingBody,out standing);
                if(!actor.StandingBody||!blockIds.ContainsKey(actor.StandingBody))standing=-1;
                var pose=actor.ViewPose;
                bool local=pair.Key==LocalActorId;
                float health=local||localBotRoots.Contains(actor.gameObject)?actor.Health/Mathf.Max(1,actor.MaxHealth):RemoteHealth(pair.Key);
                if(simulation.Find(pair.Key)?.frame.dying==true)health=0;
                simulation.Observe(pair.Key,actor.transform.position,pose.rotation,actor.transform.position.y-(Services.World?.WaterHeight(actor.transform.position)??0),health,actor.Grounded,standing);
                var p=simulation.Find(pair.Key);if(p!=null)p.frame.viewPosition=pose.position;
                if(p!=null&&local&&Rules.mode==GameModeKind.Demo&&!p.frame.dying){p.frame.role=session.PlayingHand?"hand":"survivor";if(session.PlayingHand){p.frame.position=session.Hand.transform.position;p.frame.viewPosition=session.Hand.ViewCamera.transform.position;}}
                if(!local&&!actor.IsBot)ObserveRemoteFall(pair.Key,actor);
            }
            foreach(string id in stale){if(connectedHumans.Contains(id))actors.Remove(id);else UnregisterRemote(id);}
        }
        public void Configure(ModeRules rules)
        {
            if(!ModeFrameValidation.Rules(rules))return;
            if(!Authority){CommandRequested?.Invoke("configure:"+JsonUtility.ToJson(rules));return;}
            ResetObjectiveDirector("Rules configured");
            if(rules.mode!=GameModeKind.Demo)ResetBlaster();
            bool structural=simulation.Tune(rules);nextHill=0;ruleCache.Clear();RefreshBlocks();
            MatchSetup.ModeId=ModeId(Rules.mode);
            if(structural)
            {
                ConfigureStatCollectors();ResetEquipment();
                LatestReport=null;sinkStarted.Clear();DemoDisasters.StopAll(session);MatchSetup.FillRecommended(session);
                if(Rules.mode!=GameModeKind.Demo){session.SetSeaSpirit(false);if(session.PlayingHand)session.SetRole(false);}
                foreach(var b in blockIds.Keys)if(b)b.GetComponent<IBlockOwnership>()?.SetOwner(-1,Color.white);
                SampleActors();SetFlagHomes();
            }
            frame=simulation.Snapshot();PublishEnemyActions();frame.blaster=CaptureBlaster();PublishEquipment();ApplyFrames();
        }
        public void StartRound()
        {
            if(!Authority){CommandRequested?.Invoke("start");return;}
            (session.SurvivorBots as IEnemyPopulation)?.ClearEnemies();
            var enemyIds=new List<string>();foreach(var enemy in simulation.Enemies)enemyIds.Add(enemy.frame.id);
            foreach(var id in enemyIds)UnregisterRemote(id);
            ResetEquipment();plasma.Clear();plasmaImpacts.Clear();shotCooldowns.Clear();
            LatestReport=null;restoredHistory=false;pendingBotHealth.Clear();transitionUntil.Clear();delayedOutcomes.Clear();sinkStarted.Clear();
            if(Rules.mode!=GameModeKind.Demo){session.SetSeaSpirit(false);if(session.PlayingHand)session.SetRole(false);}
            DemoDisasters.StopAll(session);
            (Services.World as IRebuildable)?.Rebuild();
            session.HandBots?.MapRebuilt();session.SurvivorBots?.MapRebuilt();
            lastBlocks=-1;RefreshBlocks();ruleCache.Clear();
            SampleActors();simulation.StartRound();SetFlagHomes();
            simulation.Hill=HighestSupport();
            foreach(var p in simulation.Participants)Respawn(p.frame.id,p.frame.role);
            frame=simulation.Snapshot();PublishEnemyActions();
            frame.blaster=CaptureBlaster();PublishEquipment();
        }
        public void ActivateHandPower(float seconds)
        {if(Authority)simulation.HandPower(LocalActorId,seconds);else CommandRequested?.Invoke("hand:"+seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));}
        public void NextSpectator(int step)
        {if(Authority)simulation.NextSpectator(LocalActorId,step);else CommandRequested?.Invoke("spectate:"+step);}
        public void ReturnToLobby()
        {if(session&&session.Network)session.Network.LeaveSession();else UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");}
        public bool HandleEliminated(GameObject who,GameObject source)
        {
            if(!who||!actorIds.TryGetValue(who,out string id))return false;
            if(!Authority)return true;
            DropEquipment(id,who.transform.position);
            string killer=null;if(source)actorIds.TryGetValue(source,out killer);
            if(MysteryActive&&who.GetComponent<IEnemyActor>()?.IsEnemy!=true)mystery.NaturalDeath(id);
            simulation.Kill(id,killer);return true;
        }
        public string CaptureFrameJson()=>JsonUtility.ToJson(frame);
        public bool ReceiveFrameJson(string json)
        {
            if(Authority||string.IsNullOrEmpty(json)||json.Length>512000)return false;
            MatchFrame incoming;try{incoming=JsonUtility.FromJson<MatchFrame>(json);}catch{return false;}
            if(incoming==null||incoming.revision<=frame.revision||!ModeFrameValidation.Frame(incoming,Services.World?.Blocks?.Count??0))return false;
            frame=incoming;MatchSetup.ModeId=ModeId(frame.rules.mode);ApplyFrames();TickCamera();return true;
        }
        void ApplyFrames()
        {
            ApplyMysteryBodies();
            ruleCache.Clear();
            if(Authority)foreach(var enemy in simulation.Enemies){var body=FindActor(enemy.frame.id);if(body!=null)body.ApplyModeRules(RulesFor(body.gameObject));}
            foreach(var f in frame.players)
            {
                if(!Authority&&session.Network){var peer=session.Network.ParticipantTransform(f.id);if(peer)RegisterRemote(f.id,peer.gameObject);}
                var actor=FindActor(f.id);if(actor==null)continue;
                actor.SetIdentity(f.id,f.name,f.team,f.color);actor.ApplyModeRules(RulesFor(actor.gameObject));
            }
            foreach(var c in frame.claims)
            {
                var blocks=Services.World?.Blocks;if(blocks==null||c.block<0||c.block>=blocks.Count||!blocks[c.block])continue;
                Color color=c.owner>=0&&c.owner<frame.teams.Length?frame.teams[c.owner].color:Color.white;
                blocks[c.block].GetComponent<IBlockOwnership>()?.SetOwner(c.owner,color);
            }
        }
        public ModeRules RulesFor(GameObject actor)
        {
            string id=actor&&actorIds.TryGetValue(actor,out var found)?found:null;
            if(id!=null&&ruleCache.TryGetValue(id,out var cached))return cached;
            var rules=Rules.Copy();
            var enemy=actor?actor.GetComponent<IEnemyActor>():null;
            if(enemy?.IsEnemy==true){rules.island=IslandRule.Walkable;rules.healthEnabled=true;rules.deathEnabled=true;rules.punchEnabled=true;rules.punchDamage=enemy.HitDamage;rules.punchDistance=2;}
            MatchPlayerFrame f=null;if(Authority)f=simulation.Find(id)?.frame;else foreach(var row in frame.players)if(row.id==id){f=row;break;}
            if(f==null)return rules;
            var item=EquipmentFor(id);if(item.drawn&&item.kind==EquipmentKind.DualRifle)rules.canJump=rules.canClimb=rules.canVault=false;
            if(!Authority)rules.fallDamage=false;
            bool carrying=Authority&&simulation.CarriesFlag(id);
            if(!Authority)foreach(var flag in frame.flags)if(flag.carrier==id){carrying=true;break;}
            rules.carryingFlag=carrying;
            if(carrying){rules.punchEnabled=false;rules.canClimb=false;rules.moveScale=.65f;rules.vaultScale=.55f;}
            if(f.role=="infected"){rules.island=IslandRule.Walkable;rules.punchEnabled=true;rules.punchDamage=enemy?.IsEnemy==true?enemy.HitDamage:100;rules.punchDistance=2;}
            if(Rules.mode==GameModeKind.MurderMystery&&(Authority?mystery?.IsDowned(id)==true:Array.IndexOf(frame.mystery.downed,id)>=0)){rules.downed=true;rules.punchEnabled=false;rules.canJump=rules.canClimb=rules.canVault=false;rules.moveScale=.18f;if(rules.island!=IslandRule.Walkable)rules.island=IslandRule.Lethal;}
            if(f.dying||f.role=="spectator"||transitionUntil.ContainsKey(id)){rules.punchEnabled=false;rules.canJump=rules.canClimb=rules.canVault=false;rules.moveScale=0;}
            if(f.sinking&&rules.island==IslandRule.Sink){rules.sinking=true;rules.sinkProgress=f.sinkProgress;rules.canJump=false;rules.moveScale=rules.sinkMoveScale;}
            var phase=Authority?simulation.Phase:frame.phase;
            if(phase!=RoundPhase.Playing){rules.punchEnabled=false;rules.canJump=rules.canClimb=rules.canVault=false;rules.moveScale=0;rules.deathEnabled=false;rules.fallDamage=false;rules.sinking=false;}
            if(id!=null)ruleCache[id]=rules;return rules;
        }
        public bool CanClimb(GameObject actor,IClimbable climbable)
        {
            if(!actor||climbable==null)return false;
            var rules=RulesFor(actor);if(!rules.canClimb)return false;
            if(!rules.enemyClimbBlocked||!climbable.Body||!blockIds.TryGetValue(climbable.Body,out int block))return true;
            string id=actorIds.TryGetValue(actor,out var found)?found:null;
            if(Authority)return simulation.MayClimb(id,block);
            var a=actor.GetComponent<IModeActor>();foreach(var claim in frame.claims)if(claim.block==block)return claim.owner<0||a==null||claim.owner==a.Team;
            return true;
        }
        static string ModeId(GameModeKind mode)
        {switch(mode){case GameModeKind.Survival:return "survival";case GameModeKind.KingOfHill:return "hill";case GameModeKind.BlockControl:return "blockcontrol";case GameModeKind.CaptureFlag:return "ctf";case GameModeKind.Infection:return "infection";case GameModeKind.TeamTickets:return "teamtickets";case GameModeKind.MurderMystery:return "mystery";default:return "demo";}}
    }
}
