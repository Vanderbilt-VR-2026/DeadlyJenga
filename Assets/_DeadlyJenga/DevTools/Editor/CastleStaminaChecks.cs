using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CastleBots;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class CastleStaminaChecks
    {
        [MenuItem("Tools/Castle Prototype/Install Stamina Breathing")]
        public static void InstallAudio()
        {
            string directory=ProjectPath.FolderOf("CandyAudioEngine.cs")+"Audio/Breathing";Directory.CreateDirectory(directory);
            var bank=Resources.Load<CandyAudioBank>("CandyAudio/Default");if(!bank)throw new InvalidOperationException("Default candy audio bank missing");
            var cues=new List<CandyAudioCue>(bank.cues);
            foreach(var sound in CandyBreathing.Sounds)
            {
                if(cues.Exists(c=>c!=null&&c.sound==sound))continue;
                var cue=CandyBreathing.DefaultCue(sound);var clip=cue.variants[0];string path=directory+"/"+sound+".wav";
                if(!File.Exists(path))
                {
                    var samples=new float[clip.samples];clip.GetData(samples,0);
                    using(var file=new BinaryWriter(File.Create(path)))
                    {
                        file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+samples.Length*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);
                        file.Write(clip.frequency);file.Write(clip.frequency*2);file.Write((short)2);file.Write((short)16);file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(samples.Length*2);
                        foreach(float sample in samples)file.Write((short)(Mathf.Clamp(sample,-1,1)*32767));
                    }
                }
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);cue.variants=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>(path)};cues.Add(cue);
            }
            bank.cues=cues.ToArray();EditorUtility.SetDirty(bank);AssetDatabase.SaveAssetIfDirty(bank);
        }
        sealed class Hold : ICastleSurvivorDriver
        {public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)=>new CastleBotCommand{look=Vector3.forward,climbHeld=true};}
        public static IEnumerator Check()
        {
            yield return null;
            var lines=new List<string>();void Assert(bool ok,string message){lines.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines("ArtDirection/StaminaChecks.txt",lines);}
            foreach(StaminaCurve curve in Enum.GetValues(typeof(StaminaCurve)))foreach(float degree in new[]{1f,2f,4f,6f})
            {
                float x=0;for(int i=0;i<90;i++)x=CastleStaminaCurve.Advance(x,1f/180,curve,degree);
                float expected=CastleStaminaCurve.Evaluate(.5f,curve,degree);
                Assert(Mathf.Abs(x-expected)<.005f,curve+" degree "+degree+" is stable across 90 integration steps.");
                Assert(CastleStaminaCurve.Advance(.25f,2,curve,degree)==1,curve+" degree "+degree+" reaches full in finite time.");
            }
            Assert(CastleStaminaCurve.Advance(.43f,0,StaminaCurve.Exponential,4)==.43f,"Changing curves while idle never changes current stamina.");
            Assert(CandyStaminaHud.TargetOpacity(true,.4f,false)==1&&CandyStaminaHud.TargetOpacity(false,.4f,false)==.46f&&CandyStaminaHud.TargetOpacity(false,1,false)==0,"HUD is opaque while climbing, translucent until full, then hidden.");
            Assert(CandyStaminaHud.TargetOpacity(true,.4f,true)==0,"Suspended/hidden view hides the HUD.");
            Assert(CandyBreathing.State(true,.4f)==CandySound.BreathExertion&&CandyBreathing.State(false,.4f)==CandySound.BreathRecovery&&CandyBreathing.State(false,0)==CandySound.BreathExhausted&&CandyBreathing.State(false,1)==null,"Breathing follows climbing, recovery, exhaustion and silence at full.");
            foreach(var sound in CandyBreathing.Sounds)
            {
                var cue=CandyBreathing.DefaultCue(sound);var data=new float[cue.variants[0].samples];cue.variants[0].GetData(data,0);float energy=0;foreach(float f in data)energy+=f*f;
                Assert(energy>1&&Mathf.Abs(data[0])+Mathf.Abs(data[data.Length-1])<.0001f,sound+" has audible PCM and silent loop seams.");
            }
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            var staging=new GameObject("Stamina test staging");staging.SetActive(false);
            var motor=Object.Instantiate(session.survivorPrefab,staging.transform).GetComponent<CastleSurvivor>();motor.externallyDriven=true;motor.enabled=false;
            foreach(var camera in motor.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var listener in motor.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            staging.SetActive(true);motor.RefreshMode();
            var block=Object.Instantiate(Object.FindAnyObjectByType<CastleGenerator>().blockPrefab).GetComponent<CastleBlock>();var body=block.GetComponent<Rigidbody>();body.isKinematic=true;
            block.transform.SetPositionAndRotation(new Vector3(120,45,2),Quaternion.identity);block.transform.localScale=new Vector3(6,10,2);block.SyncSurface();block.RefreshAppearance();Physics.SyncTransforms();
            void Tick(int n,Vector2 input,bool held){for(int i=0;i<n;i++)motor.Simulate(input,false,1f/60,held);}
            void Setup(){motor.Teleport(new Vector3(120,42,.82f),Quaternion.identity);Tick(15,Vector2.zero,true);}
            Setup();float start=motor.GripRemaining;Tick(30,Vector2.up,true);float spent=start-motor.GripRemaining;
            Assert(motor.Climbing&&Mathf.Abs(spent-.125f)<.002f,"Default capacity drains one effort unit per moving second.");
            motor.climbGripSeconds=8;start=motor.GripRemaining;Tick(30,Vector2.up,true);
            Assert(Mathf.Abs(start-motor.GripRemaining-spent*.5f)<.002f,"Live doubling of capacity halves drain without refilling.");
            motor.climbDrainPerSecond=2;start=motor.GripRemaining;Tick(30,Vector2.up,true);
            Assert(Mathf.Abs(start-motor.GripRemaining-spent)<.002f,"Live drain-rate multiplier is applied by the actual climbing motor.");
            motor.climbDrainPerSecond=0;start=motor.GripRemaining;Tick(30,Vector2.zero,true);
            Assert(motor.GripRemaining==start,"Zero drain pauses consumption without restoring stamina.");
            motor.climbDrainPerSecond=1;motor.climbGripSeconds=4;motor.climbDrainCurve=StaminaCurve.Exponential;motor.climbDrainDegree=3;
            start=motor.GripRemaining;Tick(20,Vector2.zero,true);Assert(motor.Climbing&&motor.GripRemaining<start,"Exponential drain runs on the live motor.");
            motor.climbDrainCurve=StaminaCurve.Linear;Tick(1,Vector2.zero,false);start=motor.GripRemaining;Tick(12,Vector2.zero,false);
            Assert(motor.GripRemaining==start,"Airborne release never regenerates stamina.");
            // Render an actual latched survivor with the actual HUD, not a static UI mockup.
            Setup();Tick(110,Vector2.zero,true);motor.InputDriver=new Hold();motor.enabled=true;
            session.enabled=false;session.Player.viewCamera.enabled=false;motor.viewCamera.enabled=true;motor.InputSuspended=false;
            var hud=motor.gameObject.AddComponent<CandyStaminaHud>();hud.Present(motor,1);Canvas.ForceUpdateCanvases();
            yield return Application.isBatchMode?null:new WaitForEndOfFrame();hud.Present(motor,1);Assert(hud.Opacity==1,"Actual desktop Canvas appears at full opacity during real climbing.");
            if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot("ArtDirection/StaminaClimbing.png");yield return null;yield return Application.isBatchMode?null:new WaitForEndOfFrame();
            motor.InputDriver=null;motor.enabled=false;motor.climbRecoveryPerSecond=.12f;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=motor.transform.position-Vector3.up*.5f;floor.transform.localScale=new Vector3(20,.5f,20);Physics.SyncTransforms();
            Tick(80,Vector2.zero,false);Assert(motor.StaminaRecovering,"Secure footing enables the actual recovery state after the rest delay.");
            motor.enabled=true;hud.Present(motor,1);
            yield return Application.isBatchMode?null:new WaitForEndOfFrame();Assert(Mathf.Abs(hud.Opacity-.46f)<.001f,"Actual Canvas turns translucent during recovery.");if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot("ArtDirection/StaminaRecovery.png");yield return null;
            motor.enabled=false;motor.climbRecoveryPerSecond=0;start=motor.GripRemaining;Tick(120,Vector2.zero,false);
            Assert(motor.GripRemaining==start,"Live zero recovery rate prevents regeneration on secure footing.");
            motor.climbRecoveryPerSecond=.65f;motor.climbRecoveryCurve=StaminaCurve.Exponential;motor.climbRecoveryDegree=3;Tick(150,Vector2.zero,false);
            Assert(motor.GripRemaining==1,"Live exponential recovery reaches full on secure footing.");
            motor.enabled=true;hud.Present(motor,1);Assert(hud.Opacity==0,"Actual Canvas disappears once full.");Object.Destroy(floor);
            motor.enabled=false;motor.viewCamera.enabled=false;session.Player.viewCamera.enabled=true;session.enabled=true;
            var engine=CandyAudioEngine.Instance;int voices=engine.GetComponentsInChildren<AudioSource>().Length;
            foreach(var sound in CandyBreathing.Sounds)
            {
                bool playing=false;
                for(int attempt=0;attempt<30&&!playing;attempt++)
                {
                    engine.Loop(sound,session.Player.transform,.5f,true);yield return null;
                    foreach(var source in engine.GetComponentsInChildren<AudioSource>())if(source.isPlaying&&source.clip&&source.clip.name.Contains(sound.ToString())&&source.spatialBlend==0)playing=true;
                }
                Assert(playing,sound+" plays through the existing local audio pool.");
                if(!playing)
                {
                    foreach(var source in engine.GetComponentsInChildren<AudioSource>())if(source.loop)
                        lines.Add("INFO "+source.name+" / "+(source.clip?source.clip.name+" / "+source.clip.loadState:"no clip")+" / playing="+source.isPlaying+" / spatial="+source.spatialBlend+" / priority="+source.priority);
                    File.WriteAllLines("ArtDirection/StaminaChecks.txt",lines);
                }
            }
            Assert(engine.GetComponentsInChildren<AudioSource>().Length==voices,"Breathing allocates no extra audio voices.");
            Object.Destroy(staging);Object.Destroy(block.gameObject);
            DeadlyJenga.CandyAudio.Editor.CandyAudioTools.ValidateBank();
            Debug.Log(string.Join("\n",lines));yield return CastleClimbChecks.Check();
        }
    }
}
