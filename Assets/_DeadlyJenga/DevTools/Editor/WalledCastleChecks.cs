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
                (new Vector3(0,0,-37.7f),new Vector3(0,.9f,-34.55f)),
                (new Vector3(-20.7f,0,-16.6f),new Vector3(-20.3f,3,-24.8f)),
                (new Vector3(20.7f,0,-16.6f),new Vector3(20.7f,3,-25.6f)),
                (new Vector3(0,0,-18.8f),new Vector3(0,.9f,-13)),
                (new Vector3(24f,12.08f,-12f),new Vector3(24f,12.0f,3f)),
                (new Vector3(3.9f,12.0f,17.5f),new Vector3(5.0f,14.1f,14.1f)),
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
            var frameTimes = new List<float>();
            float start = Time.fixedTime;
            while (Time.fixedTime < start + 18)
            {
                frameTimes.Add(Time.unscaledDeltaTime * 1000);
                for(int i=0;i<bots.Count;i++)
                    if (Vector3.Distance(bots[i].transform.position,World(journeys[i].Item2))<1 && bots[i].Motor.Grounded) reached[i]=true;
                yield return null;
            }
            frameTimes.Sort();
            if (frameTimes.Count > 0)
                File.AppendAllText(Report,$"Desktop traffic: {frameTimes.Count} frames, p50 {frameTimes[frameTimes.Count/2]:F2}ms, p95 {frameTimes[(frameTimes.Count-1)*95/100]:F2}ms, {bodies.Count(b=>!b.IsSleeping())} awake.\n");
            IEnumerator Shot(string name, Vector3 eye, Vector3 target, float fov)
            {
                var go = new GameObject("Walled Castle review camera");
                var camera = go.AddComponent<Camera>();
                camera.CopyFrom(session.Player.ViewCamera);
                camera.transform.SetPositionAndRotation(World(eye), Quaternion.LookRotation(World(target)-World(eye)));
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.fieldOfView = fov;
                camera.aspect = 16f/9;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 500);
                var rt = new RenderTexture(1600, 900, 24);
                camera.targetTexture = rt;
                bool rendered = false;
                void OnRendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera renderedCamera)
                {
                    if (renderedCamera == camera) rendered = true;
                }
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnRendered;
                try
                {
                    float until = Time.realtimeSinceStartup + 15;
                    while (!rendered && Time.realtimeSinceStartup < until) yield return null;
                    if (!rendered) { Check(false, "Review camera did not render " + name); yield break; }
                    var previous = RenderTexture.active;
                    var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = rt;
                        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                        png.Apply();
                        File.WriteAllBytes(ProjectPath.Report("WalledCastle-"+name+".png"), png.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(png); }
                }
                finally
                {
                    UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnRendered;
                    camera.enabled = false;
                    camera.targetTexture = null;
                    UnityEngine.Object.Destroy(go);
                    UnityEngine.Object.Destroy(rt);
                }
            }
            yield return Shot("01-wide",new(12,48,-85),new(0,5,0),42);
            yield return Shot("02-street",new(-12.8f,1.3f,-21),new(-12,4,4),68);
            yield return Shot("03-rampart",new(24,10.3f,-22),new(24,9.6f,21),72);
            yield return Shot("04-gate",new(0,1.5f,-37),new(0,5,5),72);
            for(int i=0;i<bots.Count;i++) Check(reached[i],"Bot route "+i+" end "+bots[i].transform.position);
            var drift=bodies.Select((b,i)=>Vector3.Distance(b.position,World(layout[i].Position))).ToArray();
            Check(drift.Max()<.65f,"18s intact traffic max drift "+drift.Max()+" / bodies over .25m "+drift.Count(v=>v>.25f));

            CheckCrownClearance(map, bodies);
            yield return Shot("07-rear-crown",new(2.45f,14.35f,15.6f),new(.95f,15.9f,21.7f),65);

            // A bounded physical pull on one bearing. Every neighbouring course remains free.
            foreach (var test in new[] {
                (new Vector3(16.5f,.75f,-30),new Vector3(16.5f,8.25f,-30),Vector3.back),
                (new Vector3(2.55f,4.25f,-30),new Vector3(0,8.75f,-30),Vector3.back)
            })
            {
                var bearing=bodies[Index(test.Item1)]; var deck=bodies[Index(test.Item2)];
                var origin=bearing.position; var above=deck.position;
                for(int i=0;i<200 && Vector3.Distance(bearing.position,origin)<5;i++)
                {
                    var direction=map.transform.TransformDirection(test.Item3);
                    float speed=Vector3.Dot(direction,bearing.linearVelocity);
                    bearing.GetComponent<IPushable>().Push(direction*Mathf.Clamp(11-speed,0,5),map.gameObject);
                    if (i==12 && test.Item1.x>10) yield return Shot("05-during-pull",new(16,12,-50),new(16,4,-30),48);
                    yield return new WaitForFixedUpdate();
                }
                yield return new WaitForSeconds(2);
                Check(Vector3.Distance(bearing.position,origin)>1 && deck.position.y<above.y-.35f,
                    "Bearing "+bearing.name+" extracted "+Vector3.Distance(bearing.position,origin)+"m; deck fell "+(above.y-deck.position.y)+"m");
                if (test.Item1.x>10)
                {
                    yield return Shot("05-breach",new(16,12,-50),new(16,4,-30),48);
                    yield return Shot("06-alternate",new(13,1.3f,-23),new(13,1.3f,2),68);
                    var bypass=bodies[Index(new Vector3(-24,8.25f,2.5f))];
                    Check(Mathf.Abs(bypass.position.y-World(new Vector3(0,8.25f,0)).y)<.25f,"Opposite rampart remains at route height");
                    var walker = bots[0];
                    walker.Motor.Teleport(World(new Vector3(-24,12.08f,-12)),Quaternion.identity);
                    Vector3 destination = World(new Vector3(-24,12.0f,3));
                    routes.tasks[walker.gameObject]=new BotObjective { id="west-wall-after-breach",kind=BotObjectiveKind.Travel,
                        position=destination,radius=.75f,priority=3 };
                    float until = Time.fixedTime + 18;
                    bool crossed = false;
                    while (Time.fixedTime < until && !crossed)
                    {
                        crossed = walker.Motor.Grounded && Vector3.Distance(walker.transform.position,destination)<1.1f;
                        yield return null;
                    }
                    Check(crossed,"Independent west rampart traversed after east breach");
                }
            }
            int revision=map.Revision;
            map.Rebuild();
            Check(map.Revision>revision && map.Bodies.Count==layout.Count &&
                map.Bodies.Select((b,i)=>Vector3.Distance(b.position,World(layout[i].Position))<.01f && Quaternion.Angle(b.rotation,map.transform.rotation*layout[i].Rotation)<.1f).All(restored=>restored),"Reset restores every block pose");
            Check(map.LoadLayout(map.SaveLayout(),layout.Count,new Pose(map.transform.position,map.transform.rotation),map.transform.localScale),
                "Local sync layout round trip");
            Check(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Disaster validator invoked");
        }
        static void CheckCrownClearance(CastleGenerator map, Rigidbody[] bodies)
        {
            var mapBodies = new HashSet<Rigidbody>(bodies);
            var notes = new List<string>();
            for (int step = -1; step <= 18; step++)
            {
                float z = step < 0 ? 15.84f : step == 18 ? 21.88f : 16.15f + step * .3f;
                float deck = 13.5f + .15f * Mathf.Min(18, step + 1);
                Vector3 local = new Vector3(.95f, deck + .465f, z);
                var hits = Physics.OverlapBox(map.transform.TransformPoint(local),
                    Vector3.Scale(new Vector3(.12f, .385f, .12f), map.transform.lossyScale),
                    map.transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits)
                    if (hit.attachedRigidbody && mapBodies.Contains(hit.attachedRigidbody))
                        notes.Add(step + " " + hit.attachedRigidbody.name);
            }
            Check(notes.Count == 0, "Rear crown stair and landing clearance: " +
                (notes.Count == 0 ? "20 clear survivor positions" : string.Join(", ", notes)));
        }
    }
}
