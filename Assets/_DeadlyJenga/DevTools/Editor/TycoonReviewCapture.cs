using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class TycoonReviewCapture
    {
        const string Key = "TycoonReviewCapture";


        static double captureAt;
        static double deadline;
        static Rigidbody beam, plank, adjacent;
        static Vector3 plankStart, beamStart;
        static bool observingPull;
        static string Output => Path.GetFullPath("Logs/TycoonReview");

        static TycoonReviewCapture()
        {
            EditorApplication.playModeStateChanged += PlayStateChanged;
        }

        public static void Run() => StartReview(false);
        public static void RunDestruction() => StartReview(true);

        static void StartReview(bool destruction)
        {
            SessionState.SetBool(Key+"Stress",destruction);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output,"report.txt"), "Tycoon visual review in Game.unity\n");
            SessionState.SetInt(Key+"Errors",0);
            SessionState.SetInt(Key+"Exit",1);
            MatchSetup.FromLobby = true;
            MatchSetup.StartLocalSession = false;
            MatchSetup.ModeId = "demo";
            MatchSetup.MapId = "mode-tycoon";
            MatchSetup.BotTarget = 1;
            MatchSetup.HandCount = 0;
            if (MatchSetup.SelectedMap?.mapPrefab == null)
                throw new InvalidOperationException("Tycoon did not resolve through the lobby catalog.");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            SessionState.SetBool(Key,true);
            Application.logMessageReceived += OnLog;
            EditorApplication.LockReloadAssemblies();
            EditorApplication.EnterPlaymode();
        }

        static void OnLog(string message,string trace,LogType type)
        {
            if(type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            SessionState.SetInt(Key+"Errors",SessionState.GetInt(Key+"Errors",0)+1);
            Report($"ERROR {message}\n{trace}");
        }

        static void PlayStateChanged(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false)) return;
            if(state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceived += OnLog;
                MatchSetup.FromLobby = true;
                MatchSetup.StartLocalSession = false;
                MatchSetup.ModeId = "demo";
                MatchSetup.MapId = "mode-tycoon";
                MatchSetup.BotTarget = 1;
                MatchSetup.HandCount = 0;
                // Review the authored resting layout before a physics pass.
                Time.timeScale = 0;
                captureAt = EditorApplication.timeSinceStartup + 1.5;
                deadline = captureAt + 30;
                EditorApplication.update += CaptureWhenReady;
            }
            else if(state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key,false);
                Time.timeScale = 1;
                EditorApplication.UnlockReloadAssemblies();
                Application.logMessageReceived -= OnLog;
                EditorApplication.update -= CaptureWhenReady;
                int exitCode = SessionState.GetInt(Key+"Exit",1);
                int loggedErrors = SessionState.GetInt(Key+"Errors",0);
                Report($"COMPLETE exit={exitCode}; errors={loggedErrors}");
                if(Application.isBatchMode) EditorApplication.Exit(loggedErrors == 0 ? exitCode : 1);
            }
        }

        static void CaptureWhenReady()
        {
            if (observingPull)
            {
                bool moved = !plank.isKinematic && Vector3.Distance(plank.position, plankStart) > .25f;
                if (!moved && EditorApplication.timeSinceStartup < deadline) return;
                bool ok = moved && !beam.isKinematic && adjacent.isKinematic;
                Report($"Support push: beamDynamic={!beam.isKinematic}; beamTravel={Vector3.Distance(beam.position, beamStart):F2}; plankDynamic={!plank.isKinematic}; plankDisplacement={Vector3.Distance(plank.position, plankStart):F2}; adjacentPinned={adjacent.isKinematic}");
                SessionState.SetInt(Key+"Exit",ok ? 0 : 1);
                observingPull = false;
                EditorApplication.update -= CaptureWhenReady;
                EditorApplication.ExitPlaymode();
                return;
            }
            if(EditorApplication.timeSinceStartup < captureAt) return;
            var world = Services.World as CastleGenerator;
            if (Services.Session != null && world && world.settings.layoutPreset != TycoonLayout.Preset)
            {
                MatchSetup.ApplyMap();
                Report("Applied the selected catalog map after the solo session started.");
                captureAt = EditorApplication.timeSinceStartup + 1;
                return;
            }
            var camera = Services.Session?.ActiveCamera ?? Camera.main;
            if((!world || !camera || world.Bodies.Count == 0) && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= CaptureWhenReady;
            try
            {
                if(!world || !camera || world.settings.layoutPreset != TycoonLayout.Preset || world.Bodies.Count == 0)
                    throw new InvalidOperationException($"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} did not start Tycoon. Map={MatchSetup.MapId}; world={world?.settings.layoutPreset}; bodies={world?.Bodies.Count}; camera={camera?.name}; fromLobby={MatchSetup.FromLobby}");
                Report($"Map={MatchSetup.MapId}; builder={world.name}; bodies={world.Bodies.Count}; bounds={world.PlayableBounds.size}; Unity={Application.unityVersion}");
                var settings = world.settings;
                var pieces = CastleLayout.Build(settings);
                if(pieces.Count != world.Bodies.Count || !world.SaveLayout().Contains(TycoonLayout.Preset))
                    throw new InvalidOperationException("Tycoon authored layout and runtime blocks differ.");
                if(!pieces.Any(p=>p.Section==CastleSection.Gold) || !pieces.Any(p=>p.Section==CastleSection.Aqua))
                    throw new InvalidOperationException("Tycoon chute palettes missing.");
                if(SessionState.GetBool(Key+"Stress",false))
                {
                    destructionSteps=ExerciseStructure(world,pieces);
                    EditorApplication.update+=AdvanceDestruction;
                    return;
                }
                var validation = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="DeadlyJenga.World.Editor")
                    ?.GetType("DeadlyJenga.World.Hazards.Editor.HazardValidation")?.GetMethod("ValidateCatalog");
                if(validation == null) throw new InvalidOperationException("Disaster catalog validation is unavailable.");
                validation.Invoke(null,null);
                var axle = world.GetComponentsInChildren<TycoonWheelMotion>().Single();
                int stand = pieces.FindIndex(p => p.DeckProfile.Finish == TycoonDeckFinish.Wheel);
                var sign = world.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Shark marquee");
                var bucket = world.GetComponentsInChildren<TycoonBucketMotion>().Single();
                if (stand < 0 || axle.GetComponentInParent<Rigidbody>() != world.Bodies[stand] ||
                    sign.GetComponentInParent<Rigidbody>() != bucket.GetComponentInParent<Rigidbody>() ||
                    !sign.sharedMaterial || !sign.sharedMaterial.GetTexture("_BaseMap") ||
                    (axle.transform.position - world.transform.TransformPoint(new Vector3(32,24.25f,-39.75f))).magnitude > .15f)
                    throw new InvalidOperationException("Tycoon attraction mounting or shark artwork is incorrect.");
                Report("Verified terrace wheel axle, summit billboard texture and common tipping-bucket mount.");
                Capture(world,camera);
                CheckTraversalSurfaces(world,pieces);
                int loose = pieces.FindIndex(p => p.DeckProfile.Finish == TycoonDeckFinish.LoosePeach);
                int support = pieces.FindIndex(p => (p.Position - new Vector3(0,5.8f,-41.8f)).sqrMagnitude < .001f);
                if (support < 0 || loose < 0 || !world.Bodies.All(b => b.isKinematic))
                    throw new InvalidOperationException($"Unpinned at rest: {string.Join(", ",world.Bodies.Select((b,i)=>(b,i)).Where(t=>!t.b.isKinematic).Take(15).Select(t=>pieces[t.i].Position.ToString()))}");
                plank = world.Bodies[loose]; beam = world.Bodies[support]; adjacent = world.Bodies[loose+2];
                plankStart = plank.position; beamStart = beam.position;
                beam.GetComponent<CastleBlock>().Push(-world.transform.forward*18, world.gameObject);
                Time.timeScale = 1;
                deadline = EditorApplication.timeSinceStartup + 25;
                observingPull = true;
                EditorApplication.update += CaptureWhenReady;
            }
            catch(Exception exception)
            {
                Report("FAIL "+exception);
            }
            finally
            {
                if (!observingPull && destructionSteps==null) EditorApplication.ExitPlaymode();
            }
        }

        static System.Collections.IEnumerator destructionSteps;

        static void AdvanceDestruction()
        {
            try
            {
                if(destructionSteps.MoveNext()) return;
                SessionState.SetInt(Key+"Exit",0);
            }
            catch(Exception error) { Report("FAIL "+error); }
            EditorApplication.update-=AdvanceDestruction;
            destructionSteps=null;
            EditorApplication.ExitPlaymode();
        }

        static System.Collections.IEnumerator ExerciseStructure(CastleGenerator world,System.Collections.Generic.List<CastlePlacement> layout)
        {
            var blocks=world.Bodies.ToArray();
            var rest=blocks.Select(b=>b.position).ToArray();
            var rotations=blocks.Select(b=>b.rotation).ToArray();
            var magnets=blocks.Select(b=>b.GetComponent<MagneticBlock>()).ToArray();
            var until=EditorApplication.timeSinceStartup+600;
            float start=Time.fixedTime;
            Time.timeScale=1;
            float drift=0,tilt=0;
            while(Time.fixedTime-start<15)
            {
                if(EditorApplication.timeSinceStartup>until) throw new TimeoutException("Idle physics did not advance");
                for(int i=0;i<blocks.Length;i++)
                {
                    drift=Mathf.Max(drift,Vector3.Distance(rest[i],blocks[i].position));
                    tilt=Mathf.Max(tilt,Quaternion.Angle(rotations[i],blocks[i].rotation));
                    if(!blocks[i].isKinematic || magnets[i].Broken) throw new InvalidOperationException($"Unprovoked release of {i} after {Time.fixedTime-start:F2}s");
                }
                yield return null;
            }
            Report($"Idle {Time.fixedTime-start:F2} simulated seconds: {blocks.Length} supported, peak translation={drift:F4}m peak angle={tilt:F3}deg");
            if(drift>.01f || tilt>.1f) throw new InvalidOperationException("Excessive idle drift");

            int beam=layout.FindIndex(p=>(p.Position-new Vector3(0,5.8f,-41.8f)).sqrMagnitude<.001f);
            int board=layout.FindIndex(p=>p.DeckProfile.Finish==TycoonDeckFinish.LoosePeach);
            blocks[beam].GetComponent<CastleBlock>().Push(-world.transform.forward*18,world.gameObject);
            start=Time.fixedTime;
            while(Time.fixedTime-start<3)
            {
                if(EditorApplication.timeSinceStartup>until) throw new TimeoutException("Support test timed out");
                yield return null;
            }
            float drop=rest[board].y-blocks[board].position.y;
            Report($"Local support: plank drop={drop:F2}m; beam travel={Vector3.Distance(rest[beam],blocks[beam].position):F2}m; dynamic={blocks.Count(b=>!b.isKinematic)}; adjacent pinned={blocks[board+2].isKinematic}");
            if(drop<.25f || blocks[board].isKinematic || !blocks[board+2].isKinematic) throw new InvalidOperationException("Local collapse failed");

            var foundations=magnets.Where(m=>m.foundationAnchor).ToArray();
            foreach(var m in foundations) m.Damage(m.MaxHealth,world.gameObject);
            start=Time.fixedTime;
            int fallen=0;
            while(Time.fixedTime-start<20)
            {
                if(EditorApplication.timeSinceStartup>until) throw new TimeoutException("Cascade test timed out");
                fallen=0;
                for(int i=0;i<blocks.Length;i++)
                    if(!magnets[i].foundationAnchor && rest[i].y-world.transform.position.y>10 && rest[i].y-blocks[i].position.y>1) fallen++;
                if(fallen>=20) break;
                yield return null;
            }
            Report($"Foundation removal: {foundations.Length} anchors damaged, {magnets.Count(m=>m.Broken)} broken connections, {fallen} untouched elevated pieces fell >1m after {Time.fixedTime-start:F2}s.");
            if(fallen<5 || magnets.Any(m=>!m.Broken)) throw new InvalidOperationException("Foundation failure did not propagate");
        }

        static void CheckTraversalSurfaces(CastleGenerator world,System.Collections.Generic.List<CastlePlacement> pieces)
        {
            Physics.SyncTransforms();
            int checkedPoints=0;
            float worst=0;
            for(int i=0;i<pieces.Count;i++)
            {
                var p=pieces[i].DeckProfile;
                if(p.Finish is not (TycoonDeckFinish.Flume or TycoonDeckFinish.DryChute or TycoonDeckFinish.Tube or TycoonDeckFinish.OpenDeck or TycoonDeckFinish.Walk or TycoonDeckFinish.Bridge)) continue;
                var body=world.Bodies[i].transform;
                var ring=new[]{body.TransformPoint(p.StartLeft),body.TransformPoint(p.EndLeft),
                    body.TransformPoint(p.EndRight),body.TransformPoint(p.StartRight)};
                for(int corner=0;corner<4;corner++)
                {
                    var first=ring[(corner+1)%4]-ring[corner];
                    var second=ring[(corner+2)%4]-ring[(corner+1)%4];
                    if(Vector3.Cross(first,second).y<=0) throw new InvalidOperationException($"Folded {p.Finish} panel at piece {i}");
                }
                foreach(float along in new[]{.035f,.5f,.965f})
                foreach(float across in new[]{.08f,.5f,.92f})
                {
                    var local=Vector3.Lerp(Vector3.Lerp(p.StartLeft,p.StartRight,across),
                        Vector3.Lerp(p.EndLeft,p.EndRight,across),along);
                    var pos=body.TransformPoint(local);
                    var ray=new Ray(pos+Vector3.up*1.25f,Vector3.down);
                    bool found=false;
                    foreach(var hit in Physics.RaycastAll(ray,2.5f,1<<Layers.Surface,QueryTriggerInteraction.Ignore))
                    {
                        if(!Climbables.TryGet(hit.collider,out var climb) || climb.Body!=world.Bodies[i]) continue;
                        if(hit.collider is not MeshCollider) continue;
                        worst=Mathf.Max(worst,Mathf.Abs(hit.point.y-pos.y));
                        found=true;break;
                    }
                    if(!found) throw new InvalidOperationException($"Missing {p.Finish} traversal surface at piece {i}, along={along}, across={across}");
                    checkedPoints++;
                }
            }
            Report($"Traversal panel samples={checkedPoints}; max vertical surface deviation={worst:F3} m");
            // Probe both sides of transfers in map coordinates with the live survivor collision mask.
            int transfers=0;
            void Transfer(Vector3 edge,Vector3 direction,float tolerance)
            {
                foreach(float offset in new[]{-.18f,.18f})
                {
                    var pos=world.transform.TransformPoint(edge+direction*offset);
                    bool supported=Physics.RaycastAll(pos+Vector3.up*.6f,Vector3.down,1.3f,1<<Layers.Surface)
                        .Any(hit=>Mathf.Abs(hit.point.y-pos.y)<tolerance && hit.normal.y>.4f);
                    if(!supported) throw new InvalidOperationException($"Uncovered route transfer at {edge} offset {offset}");
                }
                transfers++;
            }
            foreach(float x in new[]{-8.4f,-2.8f,2.8f,8.4f})
            {
                Transfer(new Vector3(x,60,-12.2f),Vector3.forward,.18f);
                Transfer(new Vector3(x,60,-9.2f),Vector3.forward,.18f);
            }
            foreach(float x in new[]{-18f,18f})
                Transfer(new Vector3(x,18.6f,-27.5f),Vector3.forward,.27f);
            Transfer(new Vector3(-30.5f,26,-3.5f),Vector3.forward,.22f);
            Transfer(new Vector3(-6.25f,60,3),Vector3.right,.2f);
            Transfer(new Vector3(40,18,-32),Vector3.right,.18f);
            Transfer(new Vector3(41,24,-16),Vector3.right,.18f);
            Transfer(new Vector3(8.6f,18,-6),Vector3.forward,.23f);
            Transfer(new Vector3(-8.65f,24,-6),Vector3.forward,.23f);
            Transfer(new Vector3(28,18,-28),Vector3.forward,.24f);
            int clearSpaces=0;
            void Clear(Vector3 floor)
            {
                var basePoint=world.transform.TransformPoint(floor);
                if(Physics.CheckCapsule(basePoint+Vector3.up*.35f,basePoint+Vector3.up*1.8f,.25f,1<<Layers.Surface,
                    QueryTriggerInteraction.Ignore)) throw new InvalidOperationException($"Player clearance obstructed at {floor}");
                clearSpaces++;
            }
            for(int row=0;row<6;row++)
            for(float x=-28;x<=-14;x+=2)
                Clear(new Vector3(x,19+row*1.4f,-19+row*2.8f));
            for(float x=-21;x<=21;x+=1.5f)
                Clear(new Vector3(x,18.6f,-24));
            Report($"Verified {clearSpaces} standing spaces across the bleachers and promenade.");
            var stair=world.transform.TransformPoint(new Vector3(-4.75f,59.4f,3));
            if(Physics.Raycast(stair+Vector3.up*.22f,Vector3.up,2,1<<Layers.Surface))
                throw new InvalidOperationException("Final flight headroom is obstructed");
            Report($"Verified {transfers} two-sided transfers and summit stairwell clearance.");
            if(checkedPoints==0 || worst>.42f) throw new InvalidOperationException("Traversal panel surface deviation exceeds tolerance.");
        }

        static void Capture(CastleGenerator world, Camera source)
        {
            var rig = new GameObject("Tycoon review camera");
            var camera = rig.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .07f;
            camera.farClipPlane = Mathf.Max(source.farClipPlane,1600f);
            camera.fieldOfView = 49;
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
            var target = RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,4);
            var pixels = new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            var origin = world.transform.position;
            try
            {
                camera.targetTexture = target;
                void Shot(string name,Vector3 from,Vector3 at)
                {
                    camera.transform.position = origin+from;
                    camera.transform.LookAt(origin+at);
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Output,name+".png"),pixels.EncodeToPNG());
                    Report("Captured "+name);
                }
                Shot("01-Tycoon-overview",new Vector3(5,86,-130),new Vector3(0,32,-9));
                Shot("02-Tycoon-rear",new Vector3(-122,104,138),new Vector3(0,35,0));
                Shot("03-Core-junction",new Vector3(1,19.7f,-16.5f),new Vector3(7,22,-3));
                Shot("04-West-flume",new Vector3(-34,23,-29),new Vector3(-19,26,-4));
                Shot("05-East-terrace",new Vector3(38,19.7f,-39),new Vector3(29,22,-13));
                Shot("09-Court-approaches",new Vector3(1,17,-59),new Vector3(0,12,-32));
                Shot("08-Launch-deck",new Vector3(-23,70,24),new Vector3(0,61,-4));
                Shot("07-Summit-bucket",new Vector3(16,68,-32),new Vector3(0,64,1));
                Shot("06-Plank-court",new Vector3(1,8.7f,-43),new Vector3(-4,15,-22));
                Shot("10-Island-plan",new Vector3(0,147,-3),new Vector3(0,0,0));
                Shot("11-Rear-climb",new Vector3(-42,42,49),new Vector3(-15,34,15));
                Shot("12-West-catch-basin",new Vector3(-50,14,-48),new Vector3(-37,8,-34));
                Shot("13-East-runouts",new Vector3(52,35,-29),new Vector3(31,24,-12));
                Shot("14-Boardwalk-kiosk",new Vector3(-18,10,-44),new Vector3(-27,9,-40));
                Shot("15-Terrace-pail-wheel",new Vector3(38,24,-57),new Vector3(32,24,-39));
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
                Object.Destroy(rig);
            }
        }

        static void Report(string line) => File.AppendAllText(Path.Combine(Output,"report.txt"),line+"\n");
    }
}
