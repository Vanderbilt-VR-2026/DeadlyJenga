using System;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    [CreateAssetMenu(menuName="Deadly Jenga/Island Lobby Catalog")]
    public sealed class IslandLobbyCatalog : ScriptableObject
    {
        [Serializable] public sealed class Map
        { public string id="castle", title="Candy Castle", scenePath; public GameObject mapPrefab; public int recommendedPlayers=10; public string recommendedMode="demo"; public bool walkableIsland; public string[] supportedModes=Array.Empty<string>();
            public bool SupportsMode(string modeId)=>supportedModes==null||supportedModes.Length==0||Array.IndexOf(supportedModes,modeId)>=0; }
        [Serializable] public sealed class Mode
        { public string id="demo", title="Demo", description="Full sandbox controls, AI survivors and giant hands."; public bool available=true; }
        [Tooltip("The survivor prefab (any component on it that implements IPlayableCharacter).")] public MonoBehaviour survivorPrefab;
        [Tooltip("The island's ocean, sand and sky (Presentation's environment prefab).")] public MonoBehaviour environmentPrefab;
        [Tooltip("Art profile for the island; empty uses the environment prefab's.")] public ScriptableObject art;
        public Map[] maps={new Map()};
        public Mode[] modes={new Mode(),new Mode{id="placeholder",title="Placeholder",description="Future game mode Â· not playable yet",available=false}};
        public string defaultRegion="us", protocolVersion="island-2";
        static readonly System.Collections.Generic.Dictionary<IslandLobbyCatalog,IslandLobbyCatalog> runtimeCopies=new System.Collections.Generic.Dictionary<IslandLobbyCatalog,IslandLobbyCatalog>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeCopies(){foreach(var copy in runtimeCopies.Values)if(copy)Destroy(copy);runtimeCopies.Clear();}
        public static IslandLobbyCatalog Runtime(IslandLobbyCatalog source)
        {
            if(!source)return null;
            if((source.hideFlags&HideFlags.DontSave)!=0)return source;
            if(runtimeCopies.TryGetValue(source,out var found)&&found)return found;
            var copy=Instantiate(source);copy.hideFlags=HideFlags.DontSave;copy.name=source.name+" (runtime)";
            copy.EnsureSupportedModes();copy.EnsureModeMaps();runtimeCopies[source]=copy;return copy;
        }
        public void EnsureModeMaps()
        {
            var list=new System.Collections.Generic.List<Map>(maps??Array.Empty<Map>());
            string scene=Array.Find(maps??Array.Empty<Map>(),m=>m!=null&&!string.IsNullOrEmpty(m.scenePath))?.scenePath;
            AddMap(list,"castle-gaps","Candy Castle Â· Long Blocks and Gaps","demo",10,scene);
            AddMap(list,"castle-catwalks","Candy Castle · Catwalks","demo",10,scene);
            AddMap(list,"castle-four-keeps","Candy Castle · Four Keeps","demo",10,scene);
            AddMap(list,"ctf","Lollipop Fortresses","ctf",12,scene);
            AddMap(list,"infection","Infection Spire","infection",12,scene);
            AddMap(list,"survival","Storm Cathedral","survival",12,scene);
            AddMap(list,"territory","Color Courtyards","blockcontrol",16,scene);
            AddMap(list,"koth","Crown Summit","hill",12,scene);
            AddMap(list,"tickets","Last Stand Citadel","teamtickets",12,scene);
            AddMap(list,"buildup","Beacon Scaffold Ascent","demo",10,scene);
            AddMap(list,"christmas-town","Christmas Candy Town","ctf",16,scene,true);
            AddMap(list,"powerup-test","Powerup Test Island","demo",1,scene);
            maps=list.ToArray();
        }
        static void AddMap(System.Collections.Generic.List<Map> list,string family,string title,string mode,int population,string scene,bool walkableIsland=false)
        {
            string id="mode-"+family;
            var existing=list.Find(map=>map!=null&&map.id==id);
            if(existing!=null){if(walkableIsland)existing.walkableIsland=true;return;}
            var prefab=Resources.Load<GameObject>("ModeMaps/Mode_"+family);
            if(prefab)list.Add(new Map{id=id,title=title,scenePath=scene,mapPrefab=prefab,recommendedMode=mode,recommendedPlayers=population,walkableIsland=walkableIsland});
        }

        public void EnsureSupportedModes()
        {
            var list=new System.Collections.Generic.List<Mode>(modes??Array.Empty<Mode>());
            AddMode(list,"survival","Disaster Survival","Outlast the others while disasters tear the tower apart.");
            AddMode(list,"hill","King of the Hill","Hold the crown zone to score. Push rivals off the summit.");
            AddMode(list,"blockcontrol","Color Control","Claim blocks for your team and hold territory to earn points.");
            AddMode(list,"ctf","Capture the Flag","Steal an enemy flag and bring it home while defending yours.");
            AddMode(list,"infection","Infection","Survive the infected; eliminated survivors join the hunt.");
            AddMode(list,"teamtickets","Last Team Standing","Spend team respawns wisely. Be the last team left.");
            AddMode(list,"mystery","Murder Mystery","Find the hidden murderer, protect survivors and trust the sheriff.");
            modes=list.ToArray();
        }
        static void AddMode(System.Collections.Generic.List<Mode> list,string id,string title,string description)
        {if(!list.Exists(m=>m.id==id))list.Add(new Mode{id=id,title=title,description=description,available=true});}
        public string NetworkVersion
        {
            get
            {
                var content=new System.Text.StringBuilder(protocolVersion+"|disasters-1|lobby-roster-3|avatar-attachments-1|enemies-1|plasma-1|equipment-1|low-gravity-1|avatar-styles-2|mode-metrics-2");
                foreach(var map in maps)content.Append("|map:").Append(map.id).Append(":").Append(string.Join(",",map.supportedModes??Array.Empty<string>()));
                foreach(var mode in modes)content.Append("|mode:").Append(mode.id).Append(':').Append(mode.available);
                using(var hash=System.Security.Cryptography.SHA256.Create())
                {
                    var bytes=hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content.ToString()));
                    return protocolVersion+"-"+BitConverter.ToString(bytes,0,6).Replace("-","");
                }
            }
        }
    }
}
