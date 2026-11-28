using System;
using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public static class DemoDisasters
    {
        static readonly Dictionary<int,Dictionary<string,float[]>> nextValues=new Dictionary<int,Dictionary<string,float[]>>();
        static HazardCatalog catalog;
        static ResourceRequest catalogRequest;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetValues()
        {
            nextValues.Clear();
            catalog=null;
            catalogRequest=null;
        }

        internal static void WarmCatalog()
        {
            if(!catalog&&catalogRequest==null)
                catalogRequest=Resources.LoadAsync<HazardCatalog>("DisasterCatalog");
        }
        public static IReadOnlyList<DisasterParameter> Parameters(GameObject prefab)=>prefab.GetComponent<IDemoHazard>()?.Parameters??Array.Empty<DisasterParameter>();
        public static float[] Values(CastlePlaySession session,GameObject prefab)
            => session&&prefab?(float[])MutableValues(session,prefab).Clone():Array.Empty<float>();
        static float[] MutableValues(CastlePlaySession session,GameObject prefab)
        {
            if(!nextValues.TryGetValue(session.GetInstanceID(),out var settings))nextValues[session.GetInstanceID()]=settings=new Dictionary<string,float[]>();
            var p=Parameters(prefab);
            if(!settings.TryGetValue(prefab.name,out var v)||v.Length!=p.Count){v=new float[p.Count];for(int i=0;i<v.Length;i++)v[i]=p[i].initial;settings[prefab.name]=v;}
            if(prefab.GetComponent<IWaveTuning>()!=null&&v.Length>=2){v[0]=session.tsunamiSpeed;v[1]=session.tsunamiForce;}
            return v;
        }
        public static void SetValue(CastlePlaySession session,GameObject prefab,int index,float value)
        {
            if(!SessionDevelopmentPolicy.CanRequestLocal(session)||!prefab||float.IsNaN(value)||float.IsInfinity(value))return;
            var p=Parameters(prefab);if(index<0||index>=p.Count)return;
            var v=MutableValues(session,prefab);v[index]=p[index].Clamp(value);
            if(prefab.GetComponent<IWaveTuning>()!=null){if(index==0)session.tsunamiSpeed=v[index];if(index==1)session.tsunamiForce=v[index];}
        }
        public static string Status
        {
            get
            {
                string first=WaveStatus,warning=first.Contains(" IN ")?first:"";int count=first.Length>0?1:0;
                foreach(var source in DisasterStates.Active)
                {
                    var f=source.Capture();if(f==null||f.residualOnly)continue;count++;
                    string line=f.label+(f.warning>0?" IN "+Mathf.CeilToInt(f.warning)+" s":f.remaining<0?" · ACTIVE":" · "+Mathf.CeilToInt(f.remaining)+" s");
                    if(first.Length==0)first=line;if(f.warning>0&&warning.Length==0)warning=line;
                }
                return (warning.Length>0?warning:first)+(count>1?"  + "+(count-1)+" other disasters":"");
            }
        }
        public static string WaveStatus
        {
            get
            {
                foreach (var source in Tracked.Waves)
                {
                    var wave = source.Wave;
                    if (wave.active) return wave.warningRemaining > 0 ? "TSUNAMI IN " + Mathf.CeilToInt(wave.warningRemaining) + " s" : "TSUNAMI · " + Mathf.CeilToInt(wave.remaining) + " s remaining";
                }
                return string.Empty;
            }
        }
        public static GameObject[] Prefabs
        {
            get
            {
                if(!catalog)
                    catalog=catalogRequest?.isDone==true?catalogRequest.asset as HazardCatalog:
                        Resources.Load<HazardCatalog>("DisasterCatalog");
                return MapDisasterProfile.Filter(catalog && catalog.prefabs != null ? catalog.prefabs : Array.Empty<GameObject>());
            }
        }

        public static string Label(GameObject prefab) => prefab ? prefab.GetComponent<IHazard>()?.Type?.name ?? prefab.name : "Missing disaster prefab";

        public static string Description(GameObject prefab)
        {
            if (!prefab) return "Missing disaster prefab.";
            string key = (prefab.GetComponent<IHazard>()?.Type?.name ?? prefab.name ?? "").ToLowerInvariant();
            if (key.Contains("acid")) return "Corrosive rain erodes exposed blocks and drains nearby players until clear skies return.";
            if (key.Contains("antigrav") || key.Contains("anti-grav") || key.Contains("anti grav")) return "Reverses gravity across the arena, lifting loose blocks and survivors skyward.";
            if (key.Contains("quake")) return "Rolling tremors shake every tower, rattling joints loose and toppling unstable blocks.";
            if (key.Contains("fire")) return "Ignites blocks and spreads flames across nearby structures until burned out or stopped.";
            if (key.Contains("ice") || key.Contains("frost")) return "Coats towers in slick ice and hanging icicles, making every ledge treacherous.";
            if (key.Contains("planet") || key.Contains("killer")) return "Calls down a colossal meteor whose shockwave shatters wide sections of the arena.";
            if (key.Contains("meteor")) return "Rains flaming meteors from the sky, smashing towers on impact and scattering debris.";
            if (key.Contains("outage") || key.Contains("power")) return "Cuts arena lighting into darkness while survivors scramble to hold their footing.";
            if (key.Contains("tornado")) return "A sweeping funnel cloud tears across the island, ripping blocks loose into the sky.";
            if (key.Contains("tsunami") || key.Contains("wave")) return "A towering ocean wall surges over the arena, sweeping away low structures and players.";
            if (key.Contains("kraken")) return "Giant tentacles erupt from the water, slamming towers and dragging debris into the sea.";
            if (key.Contains("vampire")) return "A blood-moon curse drains survivors caught in the open away from safe high ground.";
            if (key.Contains("dragon")) return "A swooping dragon strafes the towers with fiery breath and knocks loose high perches.";
            return "Triggers an arena-wide hazard that damages structures and tests surviving players.";
        }
        public static bool CanTrigger(CastlePlaySession session) => SessionDevelopmentPolicy.CanRequestLocal(session);

        public static string Request(CastlePlaySession session, GameObject prefab)
        {
            if (!CanTrigger(session)) return "The host controls disasters unless shared demo controls are enabled.";
            if (!prefab || !(prefab.GetComponent<IHazard>() is IMapHazard hazard)) return "Disaster setup is missing its map contract.";
            if (!hazard.TryValidate(Services.World, out var error)) return error;
            var network = session.GetComponent<CastleFusionSession>();
            if (!network)
                return Execute(session, prefab.name,values:Values(session,prefab)) ? Label(prefab) + ": triggered / toggled." : "Disaster is already active or setup was rejected.";
            network.Request(new CastleDemoCommand{action=CastleDemoAction.Disaster,rules=prefab.name,hazardValues=Values(session,prefab)});
            return Label(prefab) + (network && !network.IsHost ? ": request sent to host." : ": trigger requested.");
        }

        public static bool Execute(CastlePlaySession session, string id,float? speed=null,float? force=null,float[] values=null)
            => SessionDevelopmentPolicy.CanExecuteLocal(session)&&ExecuteInternal(session,id,speed,force,values,out _);

        internal static bool ExecuteDevelopmentAuthorized(CastlePlaySession session,string id,float? speed=null,float? force=null,float[] values=null)
            => SessionDevelopmentPolicy.CanExecuteServer(session)&&ExecuteInternal(session,id,speed,force,values,out _);

        public static bool ExecuteForMode(CastlePlaySession session,string id,float[] values=null)
            => ExecuteForMode(session,id,values,out _);

        internal static bool ExecuteForMode(CastlePlaySession session,string id,float[] values,out IHazard started)
            => ExecuteInternal(session,id,null,null,values,out started);

        static bool ExecuteInternal(CastlePlaySession session,string id,float? speed,float? force,float[] values,out IHazard started)
        {
            started=null;
            if(values!=null&&values.Length==0)values=null;
            if (!session||!session.HasWorldAuthority || string.IsNullOrEmpty(id)) return false;
            if(speed.HasValue&&(float.IsNaN(speed.Value)||float.IsInfinity(speed.Value))||force.HasValue&&(float.IsNaN(force.Value)||float.IsInfinity(force.Value)))return false;
            var network = session.GetComponent<CastleFusionSession>();
            if (network && !network.IsHost) return false;
            var prefab = Array.Find(Prefabs, item => item && item.name == id);
            if (!prefab || !(prefab.GetComponent<IHazard>() is IMapHazard definition)) return false;
            var options=prefab.GetComponent<IDemoHazard>();
            if(values!=null){if(options==null||values.Length!=options.Parameters.Count)return false;foreach(float v in values)if(float.IsNaN(v)||float.IsInfinity(v))return false;}
            foreach (var component in session.GetComponentsInChildren<MonoBehaviour>())
                if (component is IHazard running && running.Active && running.Type == definition.Type)
                {if(options!=null&&options.Toggle){running.End();return true;}return false;}
            if (!definition.TryValidate(Services.World, out var error))
            { Debug.LogError($"{id}: {error}", prefab); return false; }
            var position = Services.World.PlayableBounds.center;
            var instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            instance.transform.SetParent(session.transform, true);
            var hazard = instance.GetComponent<IHazard>();
            // Demo-only spawn overrides; the prefab and an already running wave stay untouched.
            if (speed.HasValue && force.HasValue)
                instance.GetComponent<IWaveTuning>()?.Configure(speed.Value, force.Value);
            if(values!=null)instance.GetComponent<IDemoHazard>()?.Configure(values);
            hazard.Begin();
            if (!hazard.Active)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }
            started=hazard;
            instance.AddComponent<DemoDisasterLifetime>();
            return true;
        }

        public static void StopAll(CastlePlaySession session)
        {
            foreach (var component in session.GetComponentsInChildren<MonoBehaviour>())
                if (component is IHazard hazard) hazard.End();
        }
    }
}
