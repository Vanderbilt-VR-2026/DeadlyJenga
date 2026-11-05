using System;
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

        internal bool GameExperienceMemberId(NetworkRunner expected,PlayerRef player,out string id)
        {
            id=null;
            if(!Connected||!ReferenceEquals(Runner,expected)||!expected.ActivePlayers.Contains(player))return false;
            if(!expected.IsServer&&player!=expected.LocalPlayer)return false;
            try
            {
                string user=expected.IsServer?expected.GetPlayerUserId(player):expected.GetPlayerUserId();
                if(string.IsNullOrEmpty(user)||user.Length>256)return false;
                if(expected.IsServer)
                    foreach(var other in expected.ActivePlayers)
                        if(other!=player&&expected.GetPlayerUserId(other)==user)return false;
                using(var sha=SHA256.Create())
                    id="p:"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(user))).Replace("-","").ToLowerInvariant();
                return true;
            }
            catch(InvalidOperationException){return false;}
            catch(ArgumentException){return false;}
            catch(CryptographicException){return false;}
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
                if(!Connected)return Busy||Migrating?Array.Empty<string>():new[]{"local"};
                var result=new System.Collections.Generic.List<string>();
                foreach(var player in Runner.ActivePlayers)
                    if(GameExperienceMemberId(Runner,player,out var id))result.Add(id);
                    else return Array.Empty<string>();
                result.Sort(StringComparer.Ordinal);return result.ToArray();
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
            copy.mode=DeadlyJenga.Modes.MatchModeRuntime.ParseMode(profile.modeId);copy.automaticDisasters=false;
            var next=new LobbySetup{rules=copy,movement=MatchChoices.Rules,population=profile.targetPopulation,hands=0,
                sameHumanTeam=copy.sameHumanTeam,survivorPreset=MatchSetup.SurvivorPreset,handPreset=MatchSetup.HandPreset,
                disasters=Array.Empty<LobbyHazardSetup>()};
            if(!ValidSetup(next))return false;
            MapIndex=map;ModeIndex=mode;SharedDemoControls=false;ApplySetup(next);MatchSetup.MapId=profile.mapId;MatchSetup.ModeId=profile.modeId;return true;
        }

        bool ExperienceOperation(string operation,int attempt,GameFlowEffect effect)
        {
            if(Connected&&!IsHost||Busy||Migrating||Departing||SwitchingMap||string.IsNullOrEmpty(operation)||attempt<1||attempt>3)return false;
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
            if(profile==null||!ExperienceOperation(operation,attempt,GameFlowEffect.LoadGame)||!Catalog)return false;
            if(ExperienceAlreadyDispatched(operation,attempt))return true;
            if(RoomSceneBusy)return false;
            int map=Array.FindIndex(Catalog.maps,value=>value!=null&&value.id==profile.mapId);
            int mode=Array.FindIndex(Catalog.modes,value=>value!=null&&value.id==profile.modeId&&value.available);
            if(map<0||mode<0||!Catalog.maps[map].SupportsMode(profile.modeId))return false;
            var state=RoomGameExperience.Ensure(this).CaptureSnapshot();if(state.profileId!=profile.id)return false;
            int index=SceneUtility.GetBuildIndexByScenePath(Catalog.maps[map].scenePath);if(index<0)return false;
            MatchSetup.FromLobby=true;MatchSetup.StartLocalSession=false;MatchSetup.MapId=profile.mapId;MatchSetup.ModeId=profile.modeId;
            ExperienceRememberDispatch(operation,attempt);
            if(Connected)return LoadRoomScene(index,"map",failed);
            try{SceneManager.LoadScene(index);return true;}
            catch(Exception){failed?.Invoke();return false;}
        }

        internal bool ExperienceReturnLobby(string operation,int attempt,Action failed)
        {
            if(!ExperienceOperation(operation,attempt,GameFlowEffect.ReturnLobby))return false;
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
                if(RoomSceneBusy)return false;
                ExperienceRememberDispatch(operation,attempt);
                // Same authority/scene resolver as ReturnMatchToLobby, with the actual failure callback retained.
                if(Connected)return LoadRoomScene(index,"lobby",failed);
                try{SceneManager.LoadScene(index);return true;}
                catch(Exception){failed?.Invoke();return false;}
            }
            failed?.Invoke();return false;
        }
    }
}
