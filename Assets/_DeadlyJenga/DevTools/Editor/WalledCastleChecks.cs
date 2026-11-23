using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadlyJenga.DevTools.Editor
{
    public static class WalledCastleChecks
    {
        const string Key = "WalledCastleChecks";
        const string MapId = "mode-walled-castle";
        static string Report => ProjectPath.Report("WalledCastle.txt");
        [Serializable] sealed class Setup { public SceneSetup[] scenes; }
        sealed class Travel : IBotObjectives
        {
            public readonly Dictionary<GameObject, BotObjective> tasks = new();
            public void Collect(GameObject actor, List<BotObjective> result)
            {
                if (tasks.TryGetValue(actor, out var task)) result.Add(task);
            }
        }

        public static void Run()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => s.isLoaded && SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("The open scene has unsaved work.");
            SessionState.SetString(Key+"Scenes", JsonUtility.ToJson(new Setup { scenes=setup }));
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key+"Code", 0);
            File.WriteAllText(Report, "Walled Castle / Game.unity / solo Demo\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Select()
        {
            if (!SessionState.GetBool(Key,false)) return;
            MatchSetup.MapId = MapId;
            MatchSetup.ModeId = "demo";
            MatchSetup.BotTarget = 1;
            MatchSetup.HandCount = 0;
        }

        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key,false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    EditorApplication.delayCall += () => new GameObject(Key).AddComponent<CastleVariantDriver>().StartCoroutine(Guard());
                if (state != PlayModeStateChange.EnteredEditMode) return;
                SessionState.EraseBool(Key);
                var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Key+"Scenes","")).scenes;
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                int code = SessionState.GetInt(Key+"Code",1);
                if (SessionState.GetString("DJWarm.id","") != "")
                {
                    Directory.CreateDirectory("Logs/Warm");
                    File.WriteAllText("Logs/Warm/exit.txt",code.ToString());
                }
                else EditorApplication.delayCall += () => EditorApplication.Exit(code);
            };
        }

        static IEnumerator Guard()
        {
            var previous = BotObjectives.Current;
            var test = Exercise();
            try
            {
                while (true)
                {
                    try { if (!test.MoveNext()) break; }
                    catch (Exception e) { Check(false,e.ToString()); break; }
                    yield return test.Current;
                }
            }
            finally { BotObjectives.Current = previous; }
            EditorApplication.ExitPlaymode();
        }

        static void Check(bool pass,string label)
        {
            File.AppendAllText(Report,(pass ? "PASS " : "FAIL ")+label+"\n");
            if (!pass) SessionState.SetInt(Key+"Code",1);
        }

        static IEnumerator Exercise()
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (!(Services.Session is CastlePlaySession) && Time.realtimeSinceStartup < deadline) yield return null;
            var session = (CastlePlaySession)Services.Session;
            MatchSetup.MapId = MapId;
            if (!(Services.World is CastleGenerator selected) || selected.settings.layoutPreset != "walled-castle")
                MatchSetup.ApplyMap();
            var map = (CastleGenerator)Services.World;
            if (map.settings.layoutPreset != "walled-castle") throw new Exception("Wrong map");
            session.SetMenu(false);
            session.SurvivorBots.AutoFill = false;
            if (session.Social != null) session.Social.VoiceEnabled = false;
            var layout = CastleLayout.Build(map.settings);
            var bodies = map.Bodies.ToArray();
            Vector3 World(Vector3 local) => map.transform.TransformPoint(local);
            Check(bodies.Length == layout.Count && bodies.All(b => !b.isKinematic && b.useGravity),
                "Dynamic bodies "+bodies.Length+" / colliders "+map.GetComponentsInChildren<Collider>().Length);
            int Index(Vector3 p) => Enumerable.Range(0,layout.Count).OrderBy(i=>(layout[i].Position-p).sqrMagnitude).First();
            var routes = new Travel();
            BotObjectives.Current = routes;
            var journeys = new[] {
                (new Vector3(0,0,-37.7f),new Vector3(0,.9f,-28)),
                (new Vector3(-16.6f,0,-20.8f),new Vector3(-16.6f,3,-15.2f)),
                (new Vector3(23.2f,0,-33),new Vector3(23.2f,3,-27)),
                (new Vector3(0,0,-18.8f),new Vector3(0,.9f,-13)),
                (new Vector3(-.8f,0,15),new Vector3(-.8f,3,21)),
            };
            var bots = new List<CastleBotBrain>();
            for (int i=0;i<journeys.Length;i++)
            {
                var bot = session.SurvivorBots.SpawnOne().GetComponent<CastleBotBrain>();
                bots.Add(bot);
                bot.Motor.Teleport(World(journeys[i].Item1)+Vector3.up*.08f,Quaternion.identity);
                routes.tasks[bot.gameObject] = new BotObjective { id="castle-route-"+i,kind=BotObjectiveKind.Travel,
                    position=World(journeys[i].Item2), radius=.65f, priority=3 };
            }
            bool[] reached = new bool[bots.Count];
            float start = Time.fixedTime;
            while (Time.fixedTime < start + 18)
            {
                for(int i=0;i<bots.Count;i++)
                    if (Vector3.Distance(bots[i].transform.position,World(journeys[i].Item2))<1 && bots[i].Motor.Grounded) reached[i]=true;
                yield return null;
            }
            void Shot(string name,Vector3 eye,Vector3 target,float fov)
            {
                var camera = session.Player.ViewCamera;
                var t = camera.transform;
                var pose = new Pose(t.position,t.rotation);
                float oldFov=camera.fieldOfView, aspect=camera.aspect;
                try
                {
                    t.SetPositionAndRotation(World(eye),Quaternion.LookRotation(World(target)-World(eye)));
                    camera.aspect=16f/9; camera.fieldOfView=fov;
                    VanderbiltCampusChecks.Capture(camera,1600,900,"WalledCastle-"+name+".png");
                }
                finally { t.SetPositionAndRotation(pose.position,pose.rotation);camera.fieldOfView=oldFov;camera.aspect=aspect; }
            }
            Shot("01-wide",new(15,53,-89),new(0,5,0),44);
            Shot("02-street",new(-12.8f,1.3f,-21),new(-12,4,4),68);
            Shot("03-rampart",new(24,10.3f,-22),new(24,9.6f,21),72);
            Shot("04-gate",new(0,1.5f,-37),new(0,5,5),72);
            for(int i=0;i<bots.Count;i++) Check(reached[i],"Bot route "+i+" end "+bots[i].transform.position);
            var drift=bodies.Select((b,i)=>Vector3.Distance(b.position,World(layout[i].Position))).ToArray();
            Check(drift.Max()<.65f,"18s intact traffic max drift "+drift.Max()+" / bodies over .25m "+drift.Count(v=>v>.25f));

            // A bounded physical pull on one bearing. Every neighbouring course remains free.
            foreach (var test in new[] {
                (new Vector3(16.5f,.75f,-30),new Vector3(16.5f,8.25f,-30),Vector3.back),
                (new Vector3(2.55f,4.25f,-30),new Vector3(0,8.75f,-30),Vector3.back)
            })
            {
                var bearing=bodies[Index(test.Item1)]; var deck=bodies[Index(test.Item2)];
                var origin=bearing.position; var above=deck.position;
                for(int i=0;i<38;i++)
                {
                    var direction=map.transform.TransformDirection(test.Item3);
                    float speed=Vector3.Dot(direction,bearing.linearVelocity);
                    bearing.AddForce(direction*Mathf.Clamp(11-speed,0,2.5f),ForceMode.VelocityChange);
                    yield return new WaitForFixedUpdate();
                }
                yield return new WaitForSeconds(2);
                Check(Vector3.Distance(bearing.position,origin)>1 && deck.position.y<above.y-.35f,
                    "Bearing "+bearing.name+" extracted "+Vector3.Distance(bearing.position,origin)+"m; deck fell "+(above.y-deck.position.y)+"m");
                if (test.Item1.x>10)
                {
                    Shot("05-breach",new(16,12,-50),new(16,4,-30),48);
                    Shot("06-alternate",new(13,1.3f,-23),new(13,1.3f,2),68);
                    var bypass=bodies[Index(new Vector3(-24,8.25f,2.5f))];
                    Check(Mathf.Abs(bypass.position.y-World(new Vector3(0,8.25f,0)).y)<.25f,"Opposite rampart remains at route height");
                }
            }
            int revision=map.Revision;
            map.Rebuild();
            Check(map.Revision>revision && map.Bodies.Count==layout.Count &&
                map.Bodies.Select((b,i)=>Vector3.Distance(b.position,World(layout[i].Position))).Max()<.01f,"Reset restores every block pose");
            Check(map.LoadLayout(map.SaveLayout(),layout.Count,new Pose(map.transform.position,map.transform.rotation),map.transform.localScale),
                "Local sync layout round trip");
            Check(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Disaster validator invoked");
        }
    }
}
