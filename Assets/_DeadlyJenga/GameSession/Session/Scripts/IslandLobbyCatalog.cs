using System;
using UnityEngine;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.CastlePrototype
{
    [CreateAssetMenu(menuName="Deadly Jenga/Island Lobby Catalog")]
    public sealed class IslandLobbyCatalog : ScriptableObject
    {
        [Serializable] public sealed class Map
        { public string id="castle", title="Candy Castle", scenePath; }
        [Serializable] public sealed class Mode
        { public string id="demo", title="Demo", description="Full sandbox controls, AI survivors and giant hands."; public bool available=true; }
        public CastleSurvivor survivorPrefab;
        public CandyEnvironment environmentPrefab;
        public CandyArtProfile art;
        public Map[] maps={new Map()};
        public Mode[] modes={new Mode(),new Mode{id="placeholder",title="Placeholder",description="Future game mode · not playable yet",available=false}};
        public string defaultRegion="us", protocolVersion="island-2";
        public string NetworkVersion
        {
            get
            {
                var content=new System.Text.StringBuilder(protocolVersion+"|disasters-1");
                foreach(var map in maps)content.Append("|map:").Append(map.id);
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
