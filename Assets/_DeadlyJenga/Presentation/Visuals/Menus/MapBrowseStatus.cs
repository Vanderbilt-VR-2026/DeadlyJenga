using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal static class MapBrowseStatus
    {
        [Serializable] sealed class Rules
        {
            public string[] mainMaps=Array.Empty<string>(),experimentalMaps=Array.Empty<string>(),experimentalPrefixes=Array.Empty<string>();
        }
        static Rules rules;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => rules=null;
        internal static bool Experimental(LobbyOption map)
        {
            if(map==null)return false;
            if(rules==null)
            {
                var data=Resources.Load<TextAsset>("MapBrowserStatus");
                rules=data?JsonUtility.FromJson<Rules>(data.text):new Rules();
                rules=rules??new Rules();
                rules.mainMaps=rules.mainMaps??Array.Empty<string>();
                rules.experimentalMaps=rules.experimentalMaps??Array.Empty<string>();
                rules.experimentalPrefixes=rules.experimentalPrefixes??Array.Empty<string>();
            }
            string id=(map.id??"").ToLowerInvariant();
            if(id.StartsWith("mode-",StringComparison.Ordinal))id=id.Substring(5);
            if(!map.available)return true;
            if(Array.IndexOf(rules.mainMaps,id)>=0)return false;
            if(Array.IndexOf(rules.experimentalMaps,id)>=0)return true;
            foreach(var prefix in rules.experimentalPrefixes)if(id.StartsWith(prefix,StringComparison.Ordinal))return true;
            string label=(map.title??"").ToLowerInvariant();
            return id.Contains("test")||id.Contains("prototype")||label.Contains("unfinished")||label.Contains("experimental");
        }
    }
}
