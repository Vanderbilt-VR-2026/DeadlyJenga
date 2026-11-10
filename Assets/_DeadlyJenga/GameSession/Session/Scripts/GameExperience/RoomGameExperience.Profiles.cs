using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class RoomGameExperience
    {
        [Serializable] sealed class ProfileList { public CuratedGameProfile[] profiles; }
        CuratedGameProfile[] profiles=Array.Empty<CuratedGameProfile>();
        readonly Dictionary<string,string> signatures=new Dictionary<string,string>(StringComparer.Ordinal);
        bool profilesLoaded;
        void EnsureProfiles()
        {
            if(profilesLoaded||!room||!room.Catalog)return;
            var asset=Resources.Load<TextAsset>("GameExperience/PrototypeProfiles");
            if(!asset)return;
            ProfileList list;
            try{list=JsonUtility.FromJson<ProfileList>(asset.text);}catch(ArgumentException){return;}
            var catalog = room.Catalog;
            var playable = from mode in catalog.modes
                           where mode!=null&&mode.available&&(mode.id=="survival"||mode.id=="teamtickets")
                           from map in catalog.maps
                           where map!=null&&map.SupportsMode(mode.id)&&
                               map.id=="castle"&&
                               SceneUtility.GetBuildIndexByScenePath(map.scenePath)>=0
                           select new CuratedGameProfile
                           {
                               id="play:"+mode.id+"/"+map.id, modeId=mode.id, mapId=map.id,
                               enabled=true, evidence="Catalog-compatible Play pair",
                               minimumPopulation=1, maximumPopulation=mode.id=="survival"?25:30,
                               targetPopulation=mode.id=="survival"?25:30
                           };
            profiles=(list?.profiles??Array.Empty<CuratedGameProfile>()).Concat(playable).ToArray();
            // These trial profiles pin the first observed authored prefab structure in this room.
            // This is not a measured shipping cohort or a source-code hash.
            foreach(var p in profiles)
            {
                var map=MapFor(p?.mapId);if(p==null||map==null)continue;
                p.layoutSignature=IsPlayProfile(p)?"catalog:"+map.id:StructureSignature(map);
                signatures[p.id]=p.layoutSignature;
            }
            profilesLoaded=true;
        }
        IslandLobbyCatalog.Map MapFor(string id)=>room&&room.Catalog?Array.Find(room.Catalog.maps,m=>m!=null&&m.id==id):null;
        CuratedGameProfile FindProfile(string id)
        {EnsureProfiles();return string.IsNullOrEmpty(id)?null:Array.Find(profiles,p=>p!=null&&p.id==id);}
        bool ProfileAvailable(string id,int humans)
        {
            var p=FindProfile(id);if(p==null||!signatures.TryGetValue(id,out var pinned))return false;
            var map=MapFor(p.mapId);
            if(map==null||(!map.mapPrefab&&map.id!="castle")||
                (!IsPlayProfile(p)&&StructureSignature(map)!=pinned)||SceneUtility.GetBuildIndexByScenePath(map.scenePath)<0)return false;
            return CuratedGamePolicy.Eligible(p,pinned,humans,true,(mapId,modeId)=>
                map.id==mapId&&map.SupportsMode(modeId)&&Array.Exists(room.Catalog.modes,m=>m!=null&&m.id==modeId&&m.available),out _);
        }
        internal CuratedGameProfile[] PlayPairs()
        {
            EnsureProfiles();
            return profiles.Where(IsPlayProfile).ToArray();
        }
        static bool IsPlayProfile(CuratedGameProfile profile)=>profile!=null&&profile.id.StartsWith("play:",StringComparison.Ordinal);
        internal bool CanChoosePlayMode(string id)=>PlayPairs().Any(pair=>pair.modeId==id);
        internal bool CanChoosePlayPair(string map,string mode)=>FindProfile("play:"+mode+"/"+map)!=null;
        void EnsureFirstRoundSelection()
        {
            if(CanChoosePlayPair(room.Catalog.maps[room.MapIndex].id,room.Catalog.modes[room.ModeIndex].id))return;
            var first=PlayPairs().FirstOrDefault();
            if(first!=null)room.ExperienceSelect(first,ProfileRules(first));
        }
        internal bool StartQuickPlay(string profileId)
        {
            if(!PrepareHomeLobbyEntry(false))return false;
            preparedHomeRoom="";
            UpdateRoster();
            return StartPlayerFirstRound(profileId);
        }
        internal bool StartPlayerFirstRound(string profileId)
        {
            var state=State;
            if(!IsAuthority||!CanChooseFirstRound||WaitingForRoster||room.Busy||room.RoomSceneBusy||
                room.Departing||room.Migrating||TransportRecovering)return false;
            var profile=FindProfile(profileId);
            if(!IsPlayProfile(profile)||!ProfileAvailable(profileId,state.humanIds.Length)||
                !room.ExperienceSelect(profile,ProfileRules(profile)))return false;
            if(!flow.StartPlayerFirstRound(state.authorityTerm,profileId,true))return false;
            CommitFlow();
            return true;
        }

        internal ModeRules ProfileRules(CuratedGameProfile p)
        {
            var rules=MatchSetup.DefaultModeRules(MatchModeRuntime.ParseMode(p.modeId),MapFor(p.mapId));
            if(IsPlayProfile(p))
            {
                if (p.modeId=="teamtickets") { rules.teamCount=3; rules.sameHumanTeam=false; }
                return rules;
            }
            rules.teamCount=2;rules.sameHumanTeam=false;rules.automaticDisasters=false;
            rules.roundSeconds=120;rules.countdownSeconds=3;rules.victorySeconds=5;
            rules.starterInfected=p.modeId=="infection";
            return rules;
        }
        void SelectNext()
        {
            EnsureProfiles();var s=State;
            var candidates=profiles.Where(p=>p!=null&&IsPlayProfile(p)==s.playerPlaylist&&ProfileAvailable(p.id,s.humanIds.Length)).ToArray();
            if(candidates.Length==0)
            {
                if(s.pending==GameFlowEffect.ChooseProfile&&flow.ChooseCompleted(s.authorityTerm,s.operationId,s.attempts,"",false))CommitFlow();
                else if(s.pending==GameFlowEffect.None&&flow.RejectSetup(s.authorityTerm,"No compatible game is available for this room."))CommitFlow();
                return;
            }
            // The random room identity seeds a choice that stays stable after replay or migration.
            var profile=candidates[ProfileIndex(s.roomId,s.round,candidates.Length)];
            if(!room.ExperienceSelect(profile,ProfileRules(profile)))return;
            bool changed=s.pending==GameFlowEffect.ChooseProfile?
                flow.ChooseCompleted(s.authorityTerm,s.operationId,s.attempts,profile.id,true):flow.Select(s.authorityTerm,profile.id,true);
            if(changed)CommitFlow();
        }
        internal static int ProfileIndex(string roomId,int round,int count)
        {
            if(string.IsNullOrEmpty(roomId)||round<0||count<1)throw new ArgumentException("Invalid profile selection identity.");
            using(var hash=SHA256.Create())
            {
                var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(roomId+"/"+round.ToString(CultureInfo.InvariantCulture)));
                uint value=((uint)bytes[0]<<24)|((uint)bytes[1]<<16)|((uint)bytes[2]<<8)|bytes[3];
                return (int)(value%(uint)count);
            }
        }
        static string StructureSignature(IslandLobbyCatalog.Map map)
        {
            if(map==null||!map.mapPrefab)return "";
            var data=new StringBuilder(map.id+"|prototype-structure-v1|");
            foreach(var t in map.mapPrefab.GetComponentsInChildren<Transform>(true))
            {
                var names=new Stack<string>();for(var at=t;at;at=at.parent)names.Push(at.name);
                data.Append(string.Join("/",names.ToArray())).Append('|');
                Append(data,t.localPosition);Append(data,t.localRotation);Append(data,t.localScale);
                foreach(var c in t.GetComponents<Component>())
                {
                    if(!c)continue;data.Append(c.GetType().FullName).Append('|');
                    foreach(var f in c.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public).OrderBy(v=>v.Name,StringComparer.Ordinal))
                    {
                        var type=f.FieldType;
                        if(type.IsPrimitive||type.IsEnum||type==typeof(string)||type==typeof(Vector3)||type==typeof(Quaternion))
                        {data.Append(f.Name).Append('=');Append(data,f.GetValue(c));}
                    }
                    if(c is BoxCollider box){Append(data,box.center);Append(data,box.size);}
                    if(c is SphereCollider sphere){Append(data,sphere.center);Append(data,sphere.radius);}
                    if(c is CapsuleCollider capsule){Append(data,capsule.center);Append(data,capsule.radius);Append(data,capsule.height);}
                }
            }
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(data.ToString()))).Replace("-","");
        }
        static void Append(StringBuilder text,object value)
        {
            if(value is Vector3 v){Append(text,v.x);Append(text,v.y);Append(text,v.z);return;}
            if(value is Quaternion q){Append(text,q.x);Append(text,q.y);Append(text,q.z);Append(text,q.w);return;}
            text.Append(value is float number?number.ToString("R",CultureInfo.InvariantCulture):value is double wide?wide.ToString("R",CultureInfo.InvariantCulture):value is IFormattable f?f.ToString(null,CultureInfo.InvariantCulture):value?.ToString()??"").Append('|');
        }
    }
}
