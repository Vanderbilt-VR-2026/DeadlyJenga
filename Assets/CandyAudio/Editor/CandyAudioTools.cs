using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CandyAudio.Editor
{
    [InitializeOnLoad]
    public static class CandyAudioTools
    {
        const string Output="ArtDirection/CandyAudio/";
        const string TestKey="CandyAudio.ExplicitValidation";
        [MenuItem("Tools/Candy Audio/Open Sound Studio (Play Mode)")]
        public static void OpenStudio()
        {
            var studio=UnityEngine.Object.FindFirstObjectByType<CandyAudioStudio>();
            if(!Application.isPlaying||!studio)throw new InvalidOperationException("Start the castle demo first, then open Sound Studio (or press F8).");
            studio.Open();
        }
        static double checkAt;
        static CandyAudioTools()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(TestKey,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)checkAt=EditorApplication.timeSinceStartup+2;
                if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(TestKey,false);Export();}
            };
            EditorApplication.update+=()=>
            {
                if(checkAt<=0||EditorApplication.timeSinceStartup<checkAt)return;checkAt=0;
                try{CheckRuntime();}catch(Exception error){Directory.CreateDirectory(Output);File.WriteAllText(Output+"RuntimeValidation.txt","FAIL "+error);}
                finally{EditorApplication.isPlaying=false;}
            };
        }
        // Explicit command only: no requests, file watchers or automatic Play Mode.
        [MenuItem("Tools/Candy Audio/Run Checks And Return To Edit Mode %&#a")]
        public static void RunChecks()
        {
            if(Application.isPlaying||EditorApplication.isCompiling||EditorSceneManager.GetActiveScene().isDirty||EditorSceneManager.GetActiveScene().path!="Assets/Scenes/Game.unity")
                throw new InvalidOperationException("Use the saved Game scene in Edit Mode.");
            ValidateBank();SessionState.SetBool(TestKey,true);EditorApplication.EnterPlaymode();
        }
        [MenuItem("Tools/Candy Audio/Validate Bank")]
        public static void ValidateBank()
        {
            var lines=new List<string>();var bank=Resources.Load<CandyAudioBank>("CandyAudio/Default");
            void Check(bool okay,string message){lines.Add((okay?"PASS ":"FAIL ")+message);}
            Check(bank,"Default audio bank loads from Resources.");
            if(bank)
            {
                var found=new HashSet<CandySound>();int count=0;
                foreach(var cue in bank.cues)
                {
                    if(cue==null)continue;Check(found.Add(cue.sound),"Unique cue: "+cue.sound);
                    Check(cue.variants!=null&&cue.variants.Length>=(cue.loop?1:4),"Variation count: "+cue.sound);
                    if(cue.variants==null)continue;
                    foreach(var clip in cue.variants){count++;Check(clip&&clip.samples>0&&clip.channels==1,"Usable mono clip: "+(clip?clip.name:"missing"));}
                    Check(cue.maxDistance>cue.minDistance&&cue.minDistance>0,"Valid spatial range: "+cue.sound);
                }
                bool mapped=true;foreach(CandySound sound in Enum.GetValues(typeof(CandySound)))if(!found.Contains(sound)&&!CandyBreathing.IsBreath(sound))mapped=false;
                Check(mapped,"All defined gameplay cues have bank mappings or supported synthesized breathing defaults.");
                Check(bank.oneShotVoices+bank.loopVoices<=48,"Bounded default voice pool (48 or fewer).");
                lines.Add("Clips: "+count+"; cues: "+found.Count);
            }
            Directory.CreateDirectory(Output);File.WriteAllLines(Output+"BankValidation.txt",lines);
            foreach(var line in lines)if(line.StartsWith("FAIL"))Debug.LogError(line);
            Debug.Log("Candy audio bank validation saved to "+Output+"BankValidation.txt");
        }
        [MenuItem("Tools/Candy Audio/Check Runtime (Play Mode)")]
        public static void CheckRuntime()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Start the castle demo first.");
            var lines=new List<string>();var engine=CandyAudioEngine.Instance;var world=CandyAudioWorld.Instance;
            void Check(bool okay,string message)=>lines.Add((okay?"PASS ":"FAIL ")+message);
            Check(engine&&engine.bank,"Runtime engine and swappable bank exist.");Check(world,"Castle audio world exists.");
            if(engine&&engine.bank)
            {
                int pooled=0;foreach(var source in engine.GetComponentsInChildren<AudioSource>())if(source.name.StartsWith("Pooled candy voice"))pooled++;
                Check(pooled==engine.bank.oneShotVoices+engine.bank.loopVoices,"Exactly one bounded gameplay voice pool is allocated (studio preview is separate).");
                Check(engine.GetComponentsInChildren<AudioListener>().Length==0,"Audio module creates no competing listener.");
                var session=UnityEngine.Object.FindFirstObjectByType<CastlePlaySession>();int attached=0,total=0;
                if(session)foreach(var body in session.generator.Bodies)if(body){total++;if(body.GetComponent<CandyBlockAudio>())attached++;}
                Check(total>0&&attached==total,"Every live generated block has a contact observer (wait one second after rebuilding).");
                int culled=engine.Culled,played=engine.Played;
                if(engine.Listener)
                {
                    Check(engine.Play(CandySound.ClimbPlant,engine.Listener.position,.15f,198765),"A mapped effect starts a real AudioSource successfully.");
                    bool loopPlaying=false;foreach(var source in engine.GetComponentsInChildren<AudioSource>())if(source.loop&&source.isPlaying)loopPlaying=true;
                    Check(loopPlaying,"At least one ambience/music loop is actually playing.");
                    for(int i=0;i<100;i++)engine.Play(CandySound.BlockTap,engine.Listener.position+Vector3.forward,1,200000+i);
                    Check(engine.Culled>culled&&engine.Played-played<=engine.bank.eventsPerSecond,"Collision storm is bounded by the voice/event budget.");
                }
                else Check(false,"Active scene listener is available.");
                Check(!engine.GetComponent<Rigidbody>()&&!engine.GetComponent<Collider>(),"Audio root contains no physics body/collider.");
                var studio=engine.GetComponent<CandyAudioStudio>();Check(studio,"Friendly runtime sound studio is installed.");
                if(studio)
                {
                    var source=engine.bank;float originalGain=source.cues[0].volume;
                    studio.Open();Check(studio.IsOpen&&engine.bank!=source,"Studio opens on a session copy, preserving the project bank.");
                    studio.SessionBank.cues[0].volume=.123f;
                    Check(Mathf.Approximately(source.cues[0].volume,originalGain),"Editing a nested cue leaves the source bank unchanged.");
                    studio.SessionBank.cues[0].volume=originalGain;studio.Select(CandySound.BlockTap);studio.Audition();
                    Check(studio.IsPreviewPlaying,"Studio variation preview plays a real clip.");studio.Close();
                    Check(!studio.IsOpen&&!studio.IsPreviewPlaying&&Mathf.Approximately(engine.auditionDucking,1),"Closing stops audition and restores the game mix.");
                }
                lines.Add("This smoke test does not certify perceived sound quality, Quest frame time, HRTF or network event replication.");
            }
            Directory.CreateDirectory(Output);File.WriteAllLines(Output+"RuntimeValidation.txt",lines);foreach(var line in lines)Debug.Log(line);
        }
        [MenuItem("Tools/Candy Audio/Export Audio Module")]
        public static void Export()
        {
            Directory.CreateDirectory(Output);AssetDatabase.ExportPackage("Assets/CandyAudio",Output+"CandyAudio.unitypackage",ExportPackageOptions.Recurse);
            Debug.Log("Exported "+Output+"CandyAudio.unitypackage");
        }
    }
}
