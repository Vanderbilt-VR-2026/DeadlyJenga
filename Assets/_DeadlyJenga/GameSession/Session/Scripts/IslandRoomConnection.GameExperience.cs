using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using DeadlyJenga.Core;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandRoomConnection : IDevelopmentExperienceSource
    {
        bool IDevelopmentExperienceSource.DevelopmentExperience => RoomGameExperience.Ensure(this).DevelopmentAllowed;
        void OnEnable(){RoomGameExperience.Ensure(this);}
        string experienceDispatchedOperation="";
        int experienceDispatchedAttempt;
        NetworkRunner experienceDispatchRunner;
        readonly Dictionary<PlayerRef,string> frameMemberIds = new Dictionary<PlayerRef,string>(10);
        readonly Dictionary<string,PlayerRef> frameUsers = new Dictionary<string,PlayerRef>(10,StringComparer.Ordinal);
        readonly Dictionary<string,string> memberHashes = new Dictionary<string,string>(10,StringComparer.Ordinal);
        NetworkRunner memberRunner;
        int memberFrame = -1;
        string[] memberRoster = Array.Empty<string>();
        static readonly string[] LocalMemberRoster = {"local"};

        void InvalidateMemberFrame() => memberFrame = -1;

        void RefreshMemberFrame(NetworkRunner expected)
        {
            if (!ReferenceEquals(memberRunner, expected))
            {
                memberRunner = expected;
                memberHashes.Clear();
                memberRoster = Array.Empty<string>();
                InvalidateMemberFrame();
            }
            if (memberFrame == Time.frameCount) return;
            memberFrame = Time.frameCount;
            frameMemberIds.Clear();
            frameUsers.Clear();
            int players = 0;
            bool isHost = expected.IsServer;
            foreach (var player in expected.ActivePlayers)
            {
                players++;
                if (!isHost && player != expected.LocalPlayer) continue;
                string user = ReadMemberUser(expected, player, isHost);
                if (string.IsNullOrEmpty(user) || user.Length > 256) continue;
                if (frameUsers.TryGetValue(user, out var other))
                {
                    frameMemberIds.Remove(other);
                    continue;
                }
                frameUsers.Add(user, player);
                if (!memberHashes.TryGetValue(user, out var id))
                {
                    if (memberHashes.Count >= 32) memberHashes.Clear();
                    try
                    {
                        using var sha = SHA256.Create();
                        id = "p:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(user))).Replace("-", "").ToLowerInvariant();
                    }
                    catch (CryptographicException) { continue; }
                    memberHashes.Add(user, id);
                }
                frameMemberIds.Add(player, id);
            }
            if (players == 0 || frameMemberIds.Count != players)
            {
                memberRoster = Array.Empty<string>();
                return;
            }
            bool changed = memberRoster.Length != players;
            if (!changed)
                foreach (var id in frameMemberIds.Values)
                    if (Array.IndexOf(memberRoster, id) < 0) { changed = true; break; }
            if (!changed) return;
            memberRoster = frameMemberIds.Values.ToArray();
            Array.Sort(memberRoster, StringComparer.Ordinal);
        }

        static string ReadMemberUser(NetworkRunner expected, PlayerRef player, bool isHost)
        {
            try
            {
                return isHost ? expected.GetPlayerUserId(player) : expected.GetPlayerUserId();
            }
            catch (InvalidOperationException) { return null; }
            catch (ArgumentException) { return null; }
        }

        internal bool GameExperienceMemberId(NetworkRunner expected, PlayerRef player, out string id)
        {
            id = null;
            if (!Connected || !ReferenceEquals(Runner, expected)) return false;
            RefreshMemberFrame(expected);
            return frameMemberIds.TryGetValue(player, out id);
        }

        internal string GameExperienceLocalId
        {
            get
            {
                if(!Connected)return Busy||Migrating?null:"local";
                return GameExperienceMemberId(Runner,Runner.LocalPlayer,out var id)?id:null;
            }
        }
        internal string[] GameExperienceRosterIds
        {
            get
            {
                if(!Connected)return Busy||Migrating?Array.Empty<string>():LocalMemberRoster;
                RefreshMemberFrame(Runner);
                return memberRoster;
            }
        }
        internal string GameExperienceActorId(string stableId)
        {
            if(!Connected)return !Busy&&!Migrating&&stableId=="local"?"local":null;
            foreach(var player in Runner.ActivePlayers)
                if(GameExperienceMemberId(Runner,player,out var id)&&id==stableId)return player.ToString();
            return null;
        }
        internal bool GameExperienceIsLeader(string stableId)
        {
            if(!Connected)return !Busy&&!Migrating&&stableId=="local";
            return GameExperienceActorId(stableId)==LeaderId;
        }

        internal bool ExperienceSelect(CuratedGameProfile profile,ModeRules rules)
        {
            if(Connected&&!IsHost||Busy||Migrating||RoomSceneBusy||Departing||SwitchingMap||!Catalog||profile==null||rules==null)return false;
            if(!ExperienceApplyVerifiedProfile(profile,rules))return false;
            PublishSelection();return true;
        }

        internal bool ExperienceApplyVerifiedProfile(CuratedGameProfile profile,ModeRules rules)
        {
            if(!Catalog||profile==null||rules==null)return false;
            int map=Array.FindIndex(Catalog.maps,value=>value!=null&&value.id==profile.mapId);
            int mode=Array.FindIndex(Catalog.modes,value=>value!=null&&value.id==profile.modeId&&value.available);
            if(map<0||mode<0||!Catalog.maps[map].SupportsMode(profile.modeId)||profile.targetPopulation<profile.minimumPopulation||
                profile.targetPopulation>profile.maximumPopulation||profile.maximumPopulation>100||profile.minimumPopulation<1)return false;
            var copy=JsonUtility.FromJson<ModeRules>(JsonUtility.ToJson(rules));
            copy.mode=DeadlyJenga.Modes.MatchModeRuntime.ParseMode(profile.modeId);
            var next=new LobbySetup{rules=copy,movement=MatchChoices.Rules,population=profile.targetPopulation,hands=0,
                sameHumanTeam=copy.sameHumanTeam,survivorPreset=MatchSetup.SurvivorPreset,handPreset=MatchSetup.HandPreset,
                disasters=Array.Empty<LobbyHazardSetup>()};
            if(!ValidSetup(next))return false;
            MapIndex=map;ModeIndex=mode;SharedDemoControls=false;ApplySetup(next);MatchSetup.MapId=profile.mapId;MatchSetup.ModeId=profile.modeId;return true;
        }

        bool ExperienceOperation(string operation,int attempt,GameFlowEffect effect,bool allowDeparture=false)
        {
            if(Connected&&!IsHost||Busy||Migrating||(Departing&&!allowDeparture)||SwitchingMap||string.IsNullOrEmpty(operation)||attempt<1||attempt>3)return false;
            var state=RoomGameExperience.Ensure(this).CaptureSnapshot();
            return state.operationId==operation&&state.attempts==attempt&&state.pending==effect&&
                state.phase==(effect==GameFlowEffect.LoadGame?GameFlowPhase.LoadingGame:GameFlowPhase.LoadingLobby);
        }
        bool ExperienceAlreadyDispatched(string operation,int attempt)
            => experienceDispatchedOperation==operation&&experienceDispatchedAttempt==attempt&&ReferenceEquals(experienceDispatchRunner,Runner);
        void ExperienceRememberDispatch(string operation,int attempt)
        {experienceDispatchedOperation=operation;experienceDispatchedAttempt=attempt;experienceDispatchRunner=Runner;}

        internal bool ExperienceLoadGame(CuratedGameProfile profile,string operation,int attempt,Action failed)
        {
            if(profile==null||!ExperienceOperation(operation,attempt,GameFlowEffect.LoadGame,true)||!Catalog)return false;
            if(ExperienceAlreadyDispatched(operation,attempt))return true;
            if(Departing||RoomSceneBusy)return false;
            int map=Array.FindIndex(Catalog.maps,value=>value!=null&&value.id==profile.mapId);
            int mode=Array.FindIndex(Catalog.modes,value=>value!=null&&value.id==profile.modeId&&value.available);
            if(map<0||mode<0||!Catalog.maps[map].SupportsMode(profile.modeId))return false;
            var state=RoomGameExperience.Ensure(this).CaptureSnapshot();if(state.profileId!=profile.id)return false;
            int index=SceneUtility.GetBuildIndexByScenePath(Catalog.maps[map].scenePath);if(index<0)return false;
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.MapId=profile.mapId;MatchSetup.ModeId=profile.modeId;
            var owner=Runner;
            if(!BeginSceneDeparture(index,Connected,"map",failed,()=>ReferenceEquals(Runner,owner)&&
                ExperienceOperation(operation,attempt,GameFlowEffect.LoadGame,true)))return false;
            ExperienceRememberDispatch(operation,attempt);return true;
        }

        internal bool ExperienceReturnLobby(string operation,int attempt,Action failed)
        {
            if(!ExperienceOperation(operation,attempt,GameFlowEffect.ReturnLobby,true))return false;
            if(ExperienceAlreadyDispatched(operation,attempt))return true;
            for(int index=0;index<SceneManager.sceneCountInBuildSettings;index++)
            {
                if(System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(index))!="Lobby")continue;
                bool alreadySelected;
                if(Connected)
                {
                    var scenes=Runner.SceneInfo;
                    alreadySelected=scenes.SceneCount>0&&scenes.Scenes[scenes.SceneCount-1]==SceneRef.FromIndex(index);
                }
                else alreadySelected=SceneManager.GetActiveScene().buildIndex==index;
                if(alreadySelected){ExperienceRememberDispatch(operation,attempt);return true;}
                if(Departing||RoomSceneBusy)return false;
                var owner=Runner;
                if(!BeginSceneDeparture(index,Connected,"lobby",failed,()=>ReferenceEquals(Runner,owner)&&
                    ExperienceOperation(operation,attempt,GameFlowEffect.ReturnLobby,true)))return false;
                ExperienceRememberDispatch(operation,attempt);return true;
            }
            failed?.Invoke();return false;
        }
    }
}
