using System;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    [CreateAssetMenu(menuName="Deadly Jenga/Map Disaster Profile")]
    public sealed class MapDisasterProfile : ScriptableObject
    {
        public string layoutSignature, layoutPreset;
        public bool includeGeneral;
        [Serializable] sealed class Layout { public string layoutPreset; }
        public GameObject[] allowed=Array.Empty<GameObject>();
        public GameObject[] mapOnly=Array.Empty<GameObject>();
        static MapDisasterProfile[] profiles;
        static GameObject[] source, filtered;
        static string signature;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){profiles=null;source=filtered=null;signature=null;}
        public static GameObject[] Filter(GameObject[] catalog)
        {
            var current=(Services.World as ISyncedWorld)?.SaveLayout()??"";
            if(ReferenceEquals(source,catalog)&&signature==current&&filtered!=null)return filtered;
            profiles??=Resources.LoadAll<MapDisasterProfile>("MapDisasterProfiles");
            string preset = current.StartsWith("{", StringComparison.Ordinal) ? JsonUtility.FromJson<Layout>(current)?.layoutPreset : null;
            var profile=Array.Find(profiles,p=>p&&((!string.IsNullOrEmpty(p.layoutSignature)&&p.layoutSignature==current)||
                (!string.IsNullOrEmpty(p.layoutPreset)&&p.layoutPreset==preset)));
            source=catalog;signature=current;
            return filtered=catalog.Where(p=>p&&((profile && (Array.IndexOf(profile.allowed,p)>=0 || Array.IndexOf(profile.mapOnly,p)>=0)) || ((!profile || profile.includeGeneral) && !profiles.Any(other=>other&&Array.IndexOf(other.mapOnly,p)>=0)))).ToArray();
        }
    }
}
