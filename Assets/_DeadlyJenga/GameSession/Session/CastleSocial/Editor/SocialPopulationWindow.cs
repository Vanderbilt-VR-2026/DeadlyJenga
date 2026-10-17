using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CastleSocial.Editor
{
    public sealed class SocialPopulationWindow : EditorWindow
    {
        SocialSettings profile;Vector2 scroll;List<SocialIdentity> preview;SocialDirector simulation;int page,trait,selected,count=9;bool advanced,graph;
        string warning="",transcript="Hey, how do we get to the next tower?";double simulatedTime;bool aggregate;Vector2 matrixScroll;
        readonly string[] pages={"Population","Personalities","Age & identity","Friends","Gameplay","Voice","Conversations","Memory","Mappings","Simulation"};
        SocialConfiguration C=>profile.configuration;
        [MenuItem("Tools/Castle Social/Population & Social Simulation")]
        public static void Open()=>GetWindow<SocialPopulationWindow>("AI Player Studio");
        static string ProfilePath=>Path.GetDirectoryName(ProjectPath.FolderOf("SocialSettings.cs").TrimEnd('/')).Replace('\\','/')+"/Resources/CastleSocial/Default.asset";
        void OnEnable(){minSize=new Vector2(620,520);profile=AssetDatabase.LoadAssetAtPath<SocialSettings>(ProfilePath);}
        public static SocialSettings EnsureProfile()
        {
            string path=ProfilePath;
            var p=AssetDatabase.LoadAssetAtPath<SocialSettings>(path);if(p)return p;
            Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();p=CreateInstance<SocialSettings>();AssetDatabase.CreateAsset(p,path);AssetDatabase.SaveAssets();return p;
        }
        void OnGUI()
        {
            EditorGUILayout.Space(8);EditorGUILayout.LabelField("AI PLAYER POPULATION & SOCIAL SIMULATION",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Regular survivor bots · shared gameplay, personality and social context",EditorStyles.miniLabel);
            profile=(SocialSettings)EditorGUILayout.ObjectField("Population profile",profile,typeof(SocialSettings),false);
            if(!profile){if(GUILayout.Button("Create default profile"))profile=EnsureProfile();return;}
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Save")){EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
            if(GUILayout.Button("Duplicate")){string path=EditorUtility.SaveFilePanelInProject("Duplicate profile","Social Population","asset","");if(path!=""){var clone=Instantiate(profile);AssetDatabase.CreateAsset(clone,path);profile=clone;}}
            if(GUILayout.Button("Export JSON")){string path=EditorUtility.SaveFilePanel("Export configuration","","social-population.json","json");if(path!="")File.WriteAllText(path,JsonUtility.ToJson(C,true));}
            if(GUILayout.Button("Import JSON")){string path=EditorUtility.OpenFilePanel("Import configuration","","json");if(path!="")try{var candidate=JsonUtility.FromJson<SocialConfiguration>(File.ReadAllText(path));candidate.Validate();Undo.RecordObject(profile,"Import population");profile.configuration=candidate;preview=null;}catch(Exception e){warning=e.Message;}}
            if(GUILayout.Button("Reset")){Undo.RecordObject(profile,"Reset population");profile.ResetDefaults();preview=null;}
            EditorGUILayout.EndHorizontal();
            page=GUILayout.SelectionGrid(page,pages,5,EditorStyles.toolbarButton);scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.Space(8);
            EditorGUI.BeginChangeCheck();
            switch(page)
            {
                case 0:Population();break;case 1:Personalities();break;case 2:Demographics();break;case 3:Friends();break;case 4:Gameplay();break;case 5:Voice();break;case 6:Conversation();break;case 7:Memory();break;case 8:Mappings();break;case 9:Simulation();break;
            }
            if(EditorGUI.EndChangeCheck()){EditorUtility.SetDirty(profile);try{C.Validate();warning="";}catch(Exception e){warning=e.Message;}}
            if(warning!="")EditorGUILayout.HelpBox(warning,MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }
        float Probability(string label,float value,string tip)=>EditorGUILayout.Slider(new GUIContent(label,tip),value,0,1);
        void Population()
        {
            C.seed=EditorGUILayout.IntField(new GUIContent("Deterministic seed","Reproduces identities and baseline distributions; runtime physics remains nondeterministic."),C.seed);
            C.previewCount=EditorGUILayout.IntSlider("Preview size",C.previewCount,1,100);
            C.maxPopulation=EditorGUILayout.IntSlider("Social identity capacity",C.maxPopulation,1,100);
            C.voiceProbability=Probability("Voice-enabled probability",C.voiceProbability,"Independent lifetime assignment when each identity is generated.");
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Generate preview"))Generate();if(GUILayout.Button("New seed")){C.seed=UnityEngine.Random.Range(1,int.MaxValue);Generate();}EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Tiny populations will not exactly match percentages. Voice-enabled status is assigned once; silent bots retain gameplay and social behavior.",MessageType.Info);Preview();
        }
        void Personalities()
        {
            if(C.traits.Count==0)return;trait=EditorGUILayout.Popup("Dimension",Mathf.Clamp(trait,0,C.traits.Count-1),C.traits.Select(t=>t.label).ToArray());var t=C.traits[trait];
            t.label=EditorGUILayout.TextField("Display name",t.label);t.description=EditorGUILayout.TextField("Description",t.description);Distribution(t.distribution);
            advanced=EditorGUILayout.Foldout(advanced,"Trait schema & correlation matrix",true);
            if(advanced)
            {
                EditorGUILayout.HelpBox("Matrix entries are latent Gaussian copula correlations, not a guarantee of Pearson correlation after nonlinear sampling. Invalid PSD combinations are rejected.",MessageType.Info);
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Independent"))C.ResetCorrelations();if(GUILayout.Button("Social cluster")){C.ResetCorrelations();Correlate("sociability","cooperativeness",.45);Correlate("playfulness","impulsiveness",.35);}EditorGUILayout.EndHorizontal();
                int n=C.traits.Count;matrixScroll=EditorGUILayout.BeginScrollView(matrixScroll,GUILayout.Height(Mathf.Min(320,24*n+24)));for(int i=0;i<n;i++){EditorGUILayout.BeginHorizontal();GUILayout.Label(C.traits[i].label,GUILayout.Width(145));for(int j=0;j<n;j++)using(new EditorGUI.DisabledScope(i==j)){float v=EditorGUILayout.FloatField((float)C.correlations[i*n+j],GUILayout.Width(46));C.correlations[i*n+j]=Mathf.Clamp(v,-1,1);}EditorGUILayout.EndHorizontal();}EditorGUILayout.EndScrollView();
                EditorGUILayout.HelpBox("Rows and columns follow the dimension selector order. Set both mirrored entries; asymmetric edits are invalid until corrected.",MessageType.None);
                if(GUILayout.Button("Add custom trait")){C.traits.Add(new SocialTrait("custom_"+C.traits.Count,"Custom trait"));C.ResetCorrelations();}
                if(C.traits.Count>1&&GUILayout.Button("Remove selected trait")){string id=t.id;C.traits.RemoveAt(trait);C.mappings.RemoveAll(m=>m.trait==id);trait=0;C.ResetCorrelations();}
            }
        }
        void Correlate(string a,string b,double value){int i=C.traits.FindIndex(t=>t.id==a),j=C.traits.FindIndex(t=>t.id==b);if(i>=0&&j>=0)C.correlations[i*C.traits.Count+j]=C.correlations[j*C.traits.Count+i]=value;}
        void Distribution(SocialDistribution d)
        {
            d.median=EditorGUILayout.Slider(new GUIContent("True median","The 50th percentile, not a normal-distribution location parameter."),(float)d.median,(float)d.minimum,(float)d.maximum);
            d.deviation=EditorGUILayout.Slider(new GUIContent("Standard deviation","Solved analytically/numerically while preserving the requested median."),(float)d.deviation,0,(float)Math.Max(.01,(d.maximum-d.minimum)*.5));
            d.skew=EditorGUILayout.Slider(new GUIContent("Tail lean","Relative tail curvature. Actual standardized skewness is shown below; asymmetric bounds can skew even at zero lean."),(float)d.skew,-2,2);
            EditorGUILayout.BeginHorizontal();d.minimum=EditorGUILayout.DoubleField("Minimum",d.minimum);d.maximum=EditorGUILayout.DoubleField("Maximum",d.maximum);EditorGUILayout.EndHorizontal();
            try
            {
                var calibrated=d.Calibrate();Rect rect=GUILayoutUtility.GetRect(300,130,GUILayout.ExpandWidth(true));DrawDensity(rect,calibrated);
                EditorGUILayout.LabelField($"Median {calibrated.Median:F2}   SD {calibrated.Deviation:F2}   Mean {calibrated.Mean:F2}   Actual skew {calibrated.Skewness:F2}",EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Centered")){d.median=(d.minimum+d.maximum)*.5;d.deviation=(d.maximum-d.minimum)*.18;d.skew=0;}if(GUILayout.Button("Randomize shape")){d.median=UnityEngine.Random.Range((float)(d.minimum+.2*(d.maximum-d.minimum)),(float)(d.maximum-.2*(d.maximum-d.minimum)));d.skew=UnityEngine.Random.Range(-1f,1f);}EditorGUILayout.EndHorizontal();
            }catch(Exception e){EditorGUILayout.HelpBox(e.Message,MessageType.Warning);}
        }
        static void DrawDensity(Rect rect,CalibratedDistribution d)
        {
            EditorGUI.DrawRect(rect,EditorGUIUtility.isProSkin?new Color(.12f,.13f,.15f):new Color(.92f,.93f,.94f));
            var points=new Vector3[81];double[] density=new double[81];double max=1e-9,step=(d.Maximum-d.Minimum)/80;
            for(int i=0;i<81;i++){double x=d.Minimum+step*i;density[i]=step>0?(d.Cdf(x+step*.5)-d.Cdf(x-step*.5))/step:0;max=Math.Max(max,density[i]);}
            for(int i=0;i<81;i++)points[i]=new Vector3(rect.x+rect.width*i/80,rect.yMax-12-(float)(density[i]/max)*(rect.height-30));
            Handles.BeginGUI();Handles.color=new Color(.3f,.72f,.85f);Handles.DrawAAPolyLine(2,points);float median=rect.x+(float)((d.Median-d.Minimum)/Math.Max(.0001,d.Maximum-d.Minimum))*rect.width;Handles.color=Color.gray;Handles.DrawLine(new Vector3(median,rect.y+8),new Vector3(median,rect.yMax-10));Handles.EndGUI();
            GUI.Label(new Rect(rect.x+4,rect.y+2,160,20),"Probability density · median |",EditorStyles.miniLabel);
        }
        void Demographics()
        {
            EditorGUILayout.LabelField("Age",EditorStyles.boldLabel);Distribution(C.age);
            C.maleProbability=Probability("Male profile likelihood",C.maleProbability,"Categorical profile assignment; unrelated to skill, personality and voice bank selection.");
            EditorGUILayout.LabelField($"Male profile {C.maleProbability:P0} / Female profile {1-C.maleProbability:P0}");
            EditorGUILayout.HelpBox("Age is an identity characteristic, not an intelligence/skill shortcut. Demographic names are extensible in the profile inspector/JSON.",MessageType.Info);
        }
        void Friends()
        {
            C.friendProbability=Probability("Arriving with friends",C.friendProbability,"Population target subject to valid whole group sizes.");C.cohesion=Probability("Group cohesion",C.cohesion,"Bias toward legal routes to friends, never a permanent tether.");C.sharedHistory=Probability("Shared history strength",C.sharedHistory,"Initial directed affinity for group members.");
            Distribution(C.groupSize);graph=EditorGUILayout.Toggle("Relationship preview graph",graph);if(preview==null&&GUILayout.Button("Generate group preview"))Generate();if(graph&&preview!=null)DrawGroups();
        }
        void Gameplay()
        {
            EditorGUILayout.LabelField("Independent gameplay skill",EditorStyles.boldLabel);Distribution(C.skill);
            C.chaos=Probability("Chaos interest",C.chaos,"Reactivity and attention to mayhem.");C.silliness=Probability("Silliness",C.silliness,"Playful movement and phrase selection.");C.trolling=Probability("Harmless teasing",C.trolling,"Bounded contextual teasing; no illegal block manipulation.");
            C.tickSeconds=EditorGUILayout.Slider("Social tick interval",C.tickSeconds,.05f,1);C.reactions=Probability("Reaction intensity",C.reactions,"Emotion and emote response magnitude.");
        }
        void Voice()
        {
            profile.mode=(SocialOperatingMode)EditorGUILayout.EnumPopup("Operating profile",profile.mode);profile.serviceUrl=EditorGUILayout.TextField("Self-hosted bridge URL",profile.serviceUrl);
            C.voiceProbability=Probability("Voice participation",C.voiceProbability,"Assignment probability for newly generated identities.");C.enableVoice=EditorGUILayout.Toggle("Bot voices enabled",C.enableVoice);
            profile.botVolume=EditorGUILayout.Slider("Bot volume",profile.botVolume,0,1);profile.humanVolume=EditorGUILayout.Slider("Human volume",profile.humanVolume,0,1);profile.spatialBlend=EditorGUILayout.Slider("Spatial blend (default global)",profile.spatialBlend,0,1);
            profile.usePerIdentityMic=EditorGUILayout.Toggle("Per-identity mic character",profile.usePerIdentityMic);
            var serialized=new SerializedObject(profile);EditorGUILayout.PropertyField(serialized.FindProperty("microphoneEffects"),true);serialized.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Offline = captions/simulation. Local Development = optional Windows/Kokoro speech bridge. Full additionally requests configured local dialogue generation. No model is installed by selecting a profile. Microphone capture is opt-in at runtime.",MessageType.Info);
            if(preview!=null&&preview.Count>0&&GUILayout.Button("Audition selected identity in running scene"))
            {var world=FindFirstObjectByType<SocialWorld>();if(world&&world.Director.bots.TryGetValue(preview[Math.Min(selected,preview.Count-1)].id,out var b))world.Speech.Speak(new SocialSpeech{botId=b.identity.id,text="Hey, watch that next ledge. We can make this."},b.identity);else warning="Generate/apply the matching live population first.";}
        }
        void Conversation()
        {
            C.respondHuman=Probability("Human response tendency",C.respondHuman,"When relevant; not a promise of a response to every utterance.");C.respondBot=Probability("Bot conversation tendency",C.respondBot,"One bounded follow-up level prevents feedback loops.");C.spontaneous=Probability("Spontaneous comments",C.spontaneous,"Sampled on the ambient schedule, not every frame.");C.interjections=Probability("Brief interjections",C.interjections,"Short acknowledgments may overlap sustained human speech.");C.overlap=Probability("Overlap tolerance",C.overlap,"Allows independent group exchanges to overlap.");
            C.verbosity=Probability("Verbosity",C.verbosity,"Short gamer remarks favored; external provider output capped.");C.slang=Probability("Slang",C.slang,"Optional casual opener frequency.");C.trashTalk=Probability("Trash talk",C.trashTalk,"Light teasing; directed abuse rejected.");C.screaming=Probability("Panic reactions",C.screaming,"Urgent brief text/voice reaction tendency.");
            C.interruptSensitivity=Probability("Human interruption sensitivity",C.interruptSensitivity,"Higher values duck active bot speech more strongly after human speech holds the floor.");C.humanHold=EditorGUILayout.Slider("Human floor hold (seconds)",C.humanHold,.1f,1);C.eventLifetime=EditorGUILayout.Slider("Stale speech lifetime (seconds)",C.eventLifetime,2,15);C.providerTimeout=EditorGUILayout.Slider("Speech provider timeout",C.providerTimeout,1,20);
            C.typicalResponses=EditorGUILayout.IntSlider("Typical respondents",C.typicalResponses,1,3);C.maxConcurrentSpeech=EditorGUILayout.IntSlider("Concurrent bot voices",C.maxConcurrentSpeech,1,4);C.minCooldown=EditorGUILayout.Slider("Minimum cooldown",C.minCooldown,1,20);C.maxCooldown=EditorGUILayout.Slider("Maximum cooldown",C.maxCooldown,C.minCooldown,40);C.maxSilence=EditorGUILayout.Slider("Ambient interval",C.maxSilence,5,90);C.allowProfanity=EditorGUILayout.Toggle("Allow mild profanity",C.allowProfanity);
        }
        void Memory()
        {
            C.memoryLimit=EditorGUILayout.IntSlider("Memories per bot",C.memoryLimit,4,128);C.attentionLimit=EditorGUILayout.IntSlider("Attention slots",C.attentionLimit,1,16);C.retainHumanText=EditorGUILayout.Toggle("Retain human text in session memory",C.retainHumanText);C.recordDemonstrations=EditorGUILayout.Toggle("Record consented movement demonstrations",C.recordDemonstrations);
            EditorGUILayout.HelpBox("Human speech is not recorded for training. Text retention is session-only and off by default. Demonstrations record poses/events only when explicitly enabled.",MessageType.Info);Preview();
        }
        void Mappings()
        {
            foreach(var m in C.mappings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);m.behavior=EditorGUILayout.TextField("Behavior key",m.behavior);int index=Math.Max(0,C.traits.FindIndex(t=>t.id==m.trait));index=EditorGUILayout.Popup("Trait",index,C.traits.Select(t=>t.label).ToArray());m.trait=C.traits[index].id;m.weight=EditorGUILayout.Slider("Weight",m.weight,-2,2);
                var curve=new AnimationCurve(m.curve.Select((v,i)=>new Keyframe(i/(float)(m.curve.Length-1),v)).ToArray());EditorGUI.BeginChangeCheck();curve=EditorGUILayout.CurveField("Influence curve",curve);if(EditorGUI.EndChangeCheck())for(int i=0;i<m.curve.Length;i++)m.curve[i]=curve.Evaluate(i/(float)(m.curve.Length-1));EditorGUILayout.EndVertical();
            }
            if(GUILayout.Button("Add mapping"))C.mappings.Add(new SocialMapping{trait=C.traits[0].id});EditorGUILayout.HelpBox("Built-in behavior keys: risk, delay, social, reaction, help. Curves are sampled into portable points; weighted influences are normalized consistently.",MessageType.None);
        }
        void Generate()
        {try{preview=SocialPopulation.Generate(C,C.previewCount);simulation=new SocialDirector(C,preview);simulatedTime=0;warning="";}catch(Exception e){warning=e.Message;}}
        void Preview()
        {
            if(preview==null)return;
            EditorGUILayout.LabelField($"Sample: {preview.Count} · voice {preview.Count(b=>b.voice)} · grouped {preview.Count(b=>b.group>=0)} · median age {preview.OrderBy(b=>b.age).ElementAt(preview.Count/2).age:F1}",EditorStyles.miniLabel);
            aggregate=EditorGUILayout.Foldout(aggregate,"Configured vs sampled trait statistics",true);
            if(aggregate)
            {
                EditorGUILayout.LabelField("Dimension · sampled median / SD · requested median / SD",EditorStyles.miniLabel);
                foreach(var dimension in C.traits)
                {
                    var values=preview.Where(p=>Array.IndexOf(p.traitIds,dimension.id)>=0).Select(p=>p.Trait(dimension.id)*100).OrderBy(v=>v).ToArray();if(values.Length==0)continue;
                    double mean=values.Average(),sd=Math.Sqrt(values.Average(v=>(v-mean)*(v-mean))),median=(values[(values.Length-1)/2]+values[values.Length/2])/2;
                    EditorGUILayout.LabelField($"{dimension.label}: {median:F1} / {sd:F1} · {dimension.distribution.median:F1} / {dimension.distribution.deviation:F1}",EditorStyles.miniLabel);
                }
                EditorGUILayout.HelpBox("Small samples vary. Regenerate after changing the profile; these statistics describe the existing preview sample.",MessageType.None);
            }
            selected=EditorGUILayout.Popup("Inspect identity",Mathf.Clamp(selected,0,preview.Count-1),preview.Select(b=>b.name+" [AI]").ToArray());var b=preview[selected];
            EditorGUILayout.LabelField($"Age {b.age:F1} · {b.demographic} · skill {b.skill:F0} · group {b.group} · voice {b.voice}");EditorGUILayout.LabelField($"{b.vocal.speaker} · {b.vocal.microphone} · rate {b.vocal.rate:F2}");
            for(int i=0;i<b.traits.Length;i++){Rect rect=EditorGUILayout.GetControlRect();EditorGUI.ProgressBar(rect,(float)b.traits[i]/100,b.traitIds[i]+" · "+b.traits[i].ToString("F0"));}
            if(simulation!=null&&simulation.bots.TryGetValue(b.id,out var state))EditorGUILayout.LabelField($"Fear {state.emotion.fear:F2} · excitement {state.emotion.excitement:F2} · memories {state.memories.Count} · {state.reason}",EditorStyles.wordWrappedMiniLabel);
        }
        void DrawGroups()
        {
            var bots=preview.Take(40).ToArray();Rect rect=GUILayoutUtility.GetRect(350,240,GUILayout.ExpandWidth(true));var positions=bots.Select((b,i)=>rect.center+new Vector2(Mathf.Cos(i*Mathf.PI*2/bots.Length)*rect.width*.35f,Mathf.Sin(i*Mathf.PI*2/bots.Length)*90)).ToArray();
            Handles.BeginGUI();Handles.color=new Color(.35f,.7f,.8f,.35f);for(int i=0;i<bots.Length;i++)for(int j=i+1;j<bots.Length;j++)if(bots[i].group>=0&&bots[i].group==bots[j].group)Handles.DrawLine(positions[i],positions[j]);Handles.EndGUI();for(int i=0;i<bots.Length;i++)GUI.Label(new Rect(positions[i].x-25,positions[i].y-10,80,20),bots[i].name,EditorStyles.miniLabel);
        }
        void Simulation()
        {
            count=EditorGUILayout.IntSlider("Live bots requested",count,1,20);var world=FindFirstObjectByType<SocialWorld>();
            using(new EditorGUI.DisabledScope(!Application.isPlaying||!world||!world.IsAuthority))
            {
                if(GUILayout.Button("Apply profile & spawn regular player bots")){world.Settings.configuration=JsonUtility.FromJson<SocialConfiguration>(JsonUtility.ToJson(C));world.Settings.mode=profile.mode;world.Settings.serviceUrl=profile.serviceUrl;if(world.ApplyConfiguration()){world.Session.Bots.targetPopulation=Mathf.Max(world.Session.Bots.targetPopulation,count+1);for(int i=world.Session.Bots.Count;i<count;i++)if(!world.Session.Bots.SpawnBot())break;}else warning=world.LastError;}
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Collapse stimulus"))world.Publish(SocialEventKind.Collapse,"tower");if(GUILayout.Button("Greeting"))world.Publish(SocialEventKind.Greeting,"human");if(GUILayout.Button("Clear survivors"))world.Session.Bots.ClearBots();EditorGUILayout.EndHorizontal();
                if(GUILayout.Button("Physically remove lower supports")){var bodies=world.Session.generator.Bodies.Where(b=>b&&b.gameObject.activeSelf).OrderBy(b=>b.position.y).Take(4).ToArray();foreach(var body in bodies)body.gameObject.SetActive(false);world.Publish(SocialEventKind.Collapse,"tower");}
                transcript=EditorGUILayout.TextField("Simulated human text",transcript);if(GUILayout.Button("Inject transcript (test only)"))world.HumanTranscript("test-human",transcript,Guid.NewGuid().ToString("N"),true);
                if(GUILayout.Button("Simulate 2s human interruption"))world.StartCoroutine(Interrupt(world));
                if(GUILayout.Button("Disable inference (keep gameplay & captions)")){world.Settings.mode=SocialOperatingMode.Offline;world.Speech.CancelAll();}
                if(GUILayout.Button("Dissolve current friend groups")){foreach(var agent in world.Agents){agent.State.identity.group=-1;agent.State.relationships.Clear();}}
            }
            if(world){EditorGUILayout.LabelField($"{world.Agents.Count} AI · speech queue {world.Director.Scheduled} · inference {world.Speech.ActiveRequests} · audio sources {world.Playback.SpeakerCount}");EditorGUILayout.LabelField(world.Caption,EditorStyles.wordWrappedLabel);foreach(var a in world.Agents.Where(a=>a))EditorGUILayout.LabelField(a.State.identity.name+": "+a.Brain.Intent+" · "+a.State.reason,EditorStyles.wordWrappedMiniLabel);}
            if(GUILayout.Button("Generate offline sample"))Generate();if(simulation!=null){if(GUILayout.Button("Advance 20-second chaos scenario")){for(int i=0;i<100;i++){simulatedTime+=.2;if(i%20==0)simulation.Publish(new SocialEvent{id=Guid.NewGuid().ToString("N"),kind=SocialEventKind.Collapse,time=simulatedTime});simulation.Tick(simulatedTime);}}foreach(var line in simulation.diagnostics)EditorGUILayout.LabelField(line,EditorStyles.wordWrappedMiniLabel);Preview();}
        }
        static System.Collections.IEnumerator Interrupt(SocialWorld world)
        {world.Director.HumanActivity("test-human",true,Time.unscaledTime);yield return new WaitForSecondsRealtime(2);if(world)world.Director.HumanActivity("test-human",false,Time.unscaledTime);}
    }
}
