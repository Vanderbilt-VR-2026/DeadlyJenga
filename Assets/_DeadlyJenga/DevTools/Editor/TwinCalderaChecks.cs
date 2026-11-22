using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static class TwinCalderaChecks
    {
        const string Id="mode-twin-caldera";
        public static void Run() => MapLiveCapture.BatchMaps(new[]{Id},
            "Logs/Captures/TwinCaldera",new[]{"reference","pov|shield| -20|8|-7|-31|13|8",
            "pov|spire|25|11|0|15|14|10","pov|crossing|-11|11|0|16|11|0"});

        internal static void Wide(Camera camera,Vector3 center)
        {
            camera.fieldOfView=35;
            camera.transform.position=center+new Vector3(0,20,-80);
            camera.transform.LookAt(center+new Vector3(0,10.8f,1));
        }

        internal static void Verify(Camera cam,CastleGenerator map,string folder)
        {
            if (!map || map.settings.layoutPreset!="twin-caldera")throw new InvalidOperationException("Wrong map fixture");
            var report=new List<string>();
            int failures=0;
            void Check(bool pass,string label){report.Add((pass?"PASS ":"FAIL ")+label);if(!pass)failures++;}
            Rigidbody Find(string name)=>map.Bodies.Single(b=>b.name==name);
            void Steps(int n){for(int i=0;i<n;i++)Physics.Simulate(.02f);}
            void Shot(string name){Wide(cam,map.Center);MapLiveCapture.RenderNow(cam,1440,810,Path.Combine(folder,name+".png"));}
            var oldMode=Physics.simulationMode;
            try
            {
                Physics.simulationMode=SimulationMode.Script;
                var authored=CastleLayout.Build(map.settings);
                var bodies=map.Bodies;
                Check(bodies.Count==authored.Count && bodies.All(b=>!b.isKinematic && b.useGravity),"dynamic bodies "+bodies.Count);
                Steps(200);
                var shifted=bodies.Select((b,i)=>(b,drift:Vector3.Distance(map.transform.TransformPoint(authored[i].Position),b.position)))
                    .OrderByDescending(pair=>pair.drift).ToArray();
                Check(shifted[0].drift<.65f,"untouched worst displacement "+shifted[0].drift);
                report.AddRange(shifted.Take(24).Select(x=>x.b.name+" moved "+x.drift.ToString("F3")));
                report.Add("Awake "+bodies.Count(b=>!b.IsSleeping())+"/"+bodies.Count);
                var feet=map.SafeSpawnPoint();
                Check(feet.y>map.waterHeight+.5f && Physics.Raycast(feet+Vector3.up*.2f,Vector3.down,.45f,1<<Layers.Block),"spawn "+feet);
                var player=Services.Session.Player;
                var controller=player.gameObject.GetComponentInChildren<CharacterController>();
                if(controller)
                {
                    var west=Find("Bridge span 0");var east=Find("Bridge span 2");
                    var start=west.position+Vector3.up*.51f;var end=east.position+Vector3.up*.51f;
                    player.Place(start,Quaternion.LookRotation(Vector3.right));
                    for(int step=0;step<500 && controller.transform.position.x<end.x;step++)
                    {
                        controller.Move(new Vector3(.06f,-.035f,0));
                        Physics.Simulate(.02f);
                    }
                    Check(controller.transform.position.x>=end.x-.15f && controller.transform.position.y>end.y-.55f,
                        "survivor controller walked across all spans to "+controller.transform.position);
                    player.Place(feet,Quaternion.identity);
                }
                else Check(false,"survivor controller present");
                var slab=Find("Spire peeling slab 0");var catchRock=Find("Spire lower catch");
                var slabStart=slab.position;var bridge=Find("Bridge span 1");var bridgeStart=bridge.position;
                var bearing=Find("Spire peeling slab 0 seat 0");
                var contact=catchRock.gameObject.AddComponent<CalderaContactProbe>();contact.target=slab;
                bearing.GetComponent<CastleBlock>().Push(Vector3.back*13,map.gameObject);
                for(int n=0;n<180;n++)
                {
                    Steps(1);
                    if(n==24) Shot("right-flank-in-motion");
                }
                Shot("right-flank-aftermath");
                Check(slab.position.y<slabStart.y-.6f,"right slab descent "+(slabStart.y-slab.position.y));
                Check(contact.touched,"right slab contacted independent lower ledge");
                Check(Vector3.Distance(bridgeStart,bridge.position)<.35f,"middle span survived local failure");
                var cap=Find("Bridge cap 1");
                cap.GetComponent<CastleBlock>().Push(Vector3.forward*16,map.gameObject);
                Steps(200);Shot("bridge-bearing-aftermath");
                Check(bridge.position.y<bridgeStart.y-.65f || Quaternion.Angle(bridge.rotation,Quaternion.identity)>15,"loss of middle span "+bridge.position);
                int count=bodies.Count, before=map.Revision;string layout=map.SaveLayout();
                Check(map.LoadLayout(layout,count,new Pose(map.transform.position,map.transform.rotation),map.transform.localScale)
                    && map.Revision>before && map.Bodies.Count==count,"rebuild from synced layout and body order");
                map.SetReplica(true);Check(map.Bodies.All(b=>b.isKinematic),"replica bodies");
                map.SetReplica(false);
            }
            finally
            {
                Physics.simulationMode=oldMode;
                File.WriteAllLines(Path.Combine(folder,"TwinCalderaChecks.txt"),report);
                Debug.Log(string.Join("\n",report));
            }
            if(failures>0)throw new InvalidOperationException(failures+" Twin Caldera checks failed");
        }
    }

    public sealed class CalderaContactProbe : MonoBehaviour
    {
        internal Rigidbody target;
        internal bool touched;
        void OnCollisionEnter(Collision hit){if(hit.rigidbody==target)touched=true;}
    }
}
