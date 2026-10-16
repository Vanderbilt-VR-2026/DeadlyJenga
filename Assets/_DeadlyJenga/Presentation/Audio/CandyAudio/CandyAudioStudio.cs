using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyAudio
{
    // Desktop Game-view authoring overlay. Session copies protect project assets.
    public sealed class CandyAudioStudio : MonoBehaviour
    {
        const float W=1280,H=800;
        public bool IsOpen {get;private set;}
        public bool ShowLauncher {get;set;}=true;
        public CandyAudioBank SessionBank=>working;
        public bool IsPreviewPlaying=>preview&&preview.isPlaying;
        CandyAudioBank original,working;CandyAudioEngine engine;ISessionInfo session;
        AudioSource preview;CandyAudioCue selected;bool previousMenu,library,spatial,quiet=true;
        int category=-1,take;float strength=.75f,distance=8,angle=30;string search="",clipSearch="",status="Live session mix • project assets stay untouched";
        Vector2 scroll,clipScroll;float[] waveform=new float[100],samples=new float[128];
        readonly List<AudioClip> clips=new List<AudioClip>();readonly List<CandyAudioCue> shown=new List<CandyAudioCue>();
        readonly Dictionary<int,Texture2D> icons=new Dictionary<int,Texture2D>();
        Texture2D rounded;GUIStyle title,heading,body,small,button,card,field;bool stylesReady;
        static readonly Color Ink=new Color(.055f,.075f,.14f),Panel=new Color(.10f,.13f,.22f),Card=new Color(.15f,.19f,.30f),Muted=new Color(.62f,.69f,.82f),Mint=new Color(.35f,.96f,.76f);
        static readonly string[] Groups={"Crates & splashes","Flying hands","Player movement","Climb & vault","Island atmosphere","Soundtrack","Emotes & reactions","Menu feedback"};
        static readonly Color[] Accents={new Color(1,.64f,.38f),new Color(.88f,.57f,1),new Color(.42f,.82f,1),new Color(.35f,.96f,.76f),new Color(.4f,.87f,.93f),new Color(1,.77f,.35f),new Color(1,.52f,.71f),new Color(.7f,.77f,1)};
        [Serializable] sealed class TakeSettings {public CandySound sound;public bool enabled;public float volume;public Vector2 pitch;public float minDistance,maxDistance,cooldown;public string[] clips;}
        [Serializable] sealed class Preset {public int version=1;public float master,world,characters,ambience,music,ui;public TakeSettings[] sounds;}
        string PresetPath=>Path.Combine(Application.persistentDataPath,"CandyAudio","studio-preset.json");
        void Awake(){engine=GetComponent<CandyAudioEngine>();var speaker=new GameObject("Studio audition (one voice)");speaker.transform.SetParent(transform,false);preview=speaker.AddComponent<AudioSource>();preview.playOnAwake=false;preview.priority=24;preview.dopplerLevel=0;preview.rolloffMode=AudioRolloffMode.Linear;}
        public void Open()
        {
            if(!engine||!engine.bank)return;
            if(!working){original=engine.bank;working=Instantiate(original);working.name=original.name+" (studio session)";engine.Configure(working);}
            session=Services.Session;previousMenu=session!=null&&session.MenuOpen;if(session!=null)session.SetMenu(true);
            GatherClips();IsOpen=true;status="Session copy • preview freely, then save a preset if you like it.";
        }
        public void Close()
        {
            preview.Stop();selected=null;library=false;IsOpen=false;if(engine)engine.auditionDucking=1;
            if(session!=null)session.SetMenu(previousMenu);
        }
        void Update()
        {
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.f8Key.wasPressedThisFrame){if(IsOpen)Close();else Open();}
            if(!IsOpen)return;
            if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame){if(library)library=false;else if(selected!=null){selected=null;preview.Stop();}else Close();}
            if(!IsOpen)return;
            if(session!=null&&!session.MenuOpen)session.SetMenu(true);
            engine.auditionDucking=quiet&&preview.isPlaying?.12f:1;
            if(preview.isPlaying&&selected!=null)
            {
                preview.volume=selected.volume*strength*working.master*working.Gain(selected.bus);
                if(spatial&&engine.Listener)preview.transform.position=engine.Listener.position+Quaternion.AngleAxis(angle,Vector3.up)*engine.Listener.forward*distance;
                preview.GetOutputData(samples,0);for(int i=0;i<waveform.Length;i++)waveform[i]=Mathf.Abs(samples[i%128]);
            }
        }
        public void Select(CandySound sound)
        {foreach(var cue in working.cues)if(cue.sound==sound){selected=cue;take=0;library=false;preview.Stop();ReadWave();break;}}
        void GatherClips()
        {
            clips.Clear();var seen=new HashSet<AudioClip>();
            void Add(AudioClip c){if(c&&seen.Add(c))clips.Add(c);}
            foreach(var cue in original.cues)foreach(var c in cue.variants)Add(c);
            foreach(var cue in working.cues)foreach(var c in cue.variants)Add(c);
            foreach(var c in Resources.LoadAll<AudioClip>("CandyAudio/Clips"))Add(c);
            clips.Sort((a,b)=>string.Compare(a.name,b.name,StringComparison.OrdinalIgnoreCase));
        }
        void ReadWave()
        {
            Array.Clear(waveform,0,waveform.Length);var clip=CurrentClip();if(!clip||clip.loadType!=AudioClipLoadType.DecompressOnLoad||clip.samples*clip.channels>400000)return;
            var data=new float[clip.samples*clip.channels];if(!clip.GetData(data,0))return;
            for(int i=0;i<waveform.Length;i++){int from=i*data.Length/waveform.Length,to=(i+1)*data.Length/waveform.Length;float maximum=0;for(int j=from;j<to;j++)maximum=Mathf.Max(maximum,Mathf.Abs(data[j]));waveform[i]=maximum;}
        }
        AudioClip CurrentClip()=>selected!=null&&selected.variants.Length>0?selected.variants[Mathf.Clamp(take,0,selected.variants.Length-1)]:null;
        public void Audition()
        {
            var clip=CurrentClip();if(!clip)return;preview.Stop();preview.clip=clip;preview.loop=selected.loop;
            preview.pitch=Mathf.Clamp((selected.pitch.x+selected.pitch.y)*.5f,.5f,1.8f);preview.spatialBlend=spatial?1:0;
            preview.minDistance=selected.minDistance;preview.maxDistance=selected.maxDistance;preview.outputAudioMixerGroup=selected.output;
            preview.volume=selected.volume*strength*working.master*working.Gain(selected.bus);
            preview.transform.position=engine.Listener?engine.Listener.position+Quaternion.AngleAxis(angle,Vector3.up)*engine.Listener.forward*distance:transform.position;
            preview.Play();status="Auditioning "+clip.name+(spatial?" in 3D":" close-up");
        }
        void ResetCue()
        {
            foreach(var cue in original.cues)if(cue.sound==selected.sound)
            {
                selected.enabled=cue.enabled;selected.volume=cue.volume;selected.pitch=cue.pitch;selected.minDistance=cue.minDistance;selected.maxDistance=cue.maxDistance;
                selected.cooldown=cue.cooldown;selected.maxVoices=cue.maxVoices;selected.priority=cue.priority;selected.output=cue.output;selected.variants=(AudioClip[])cue.variants.Clone();break;
            }
            take=0;ReadWave();engine.Configure(working);status="This sound set was reset to the original bank.";
        }
        public void SavePreset()
        {
            try
            {
                var preset=new Preset{master=working.master,world=working.world,characters=working.characters,ambience=working.ambience,music=working.music,ui=working.ui,sounds=new TakeSettings[working.cues.Length]};
                for(int i=0;i<working.cues.Length;i++){var c=working.cues[i];var names=new string[c.variants.Length];for(int j=0;j<names.Length;j++)names[j]=c.variants[j]?c.variants[j].name:"";preset.sounds[i]=new TakeSettings{sound=c.sound,enabled=c.enabled,volume=c.volume,pitch=c.pitch,minDistance=c.minDistance,maxDistance=c.maxDistance,cooldown=c.cooldown,clips=names};}
                Directory.CreateDirectory(Path.GetDirectoryName(PresetPath));File.WriteAllText(PresetPath,JsonUtility.ToJson(preset,true));status="Preset saved on this device. Original project bank unchanged.";
            }catch(Exception error){status="Could not save preset: "+error.Message;}
        }
        public void LoadPreset()
        {
            try
            {
                if(!File.Exists(PresetPath)){status="No saved preset yet. Make a mix and choose Save preset.";return;}
                var p=JsonUtility.FromJson<Preset>(File.ReadAllText(PresetPath));if(p==null||p.version!=1||p.sounds==null)throw new InvalidDataException("Unsupported preset.");
                working.master=Mathf.Clamp01(p.master);working.world=Mathf.Clamp01(p.world);working.characters=Mathf.Clamp01(p.characters);working.ambience=Mathf.Clamp01(p.ambience);working.music=Mathf.Clamp01(p.music);working.ui=Mathf.Clamp01(p.ui);
                int missing=0;
                foreach(var saved in p.sounds)foreach(var c in working.cues)if(c.sound==saved.sound)
                {
                    c.enabled=saved.enabled;c.volume=Mathf.Clamp01(saved.volume);c.pitch=new Vector2(Mathf.Clamp(saved.pitch.x,.5f,1.8f),Mathf.Clamp(saved.pitch.y,.5f,1.8f));
                    c.minDistance=Mathf.Max(.1f,saved.minDistance);c.maxDistance=Mathf.Max(c.minDistance+1,saved.maxDistance);c.cooldown=Mathf.Max(0,saved.cooldown);
                    if(saved.clips!=null&&saved.clips.Length>0&&saved.clips.Length<=8)
                    {
                        var replacement=new AudioClip[saved.clips.Length];bool complete=true;
                        for(int i=0;i<replacement.Length;i++){replacement[i]=clips.Find(clip=>clip.name==saved.clips[i]);if(!replacement[i]){missing++;complete=false;}}
                        if(complete)c.variants=replacement;
                    }
                }
                engine.Configure(working);take=0;ReadWave();status=missing==0?"Saved preset loaded into this session.":"Loaded settings; kept existing takes for "+missing+" missing clips.";
            }catch(Exception error){status="Could not load preset: "+error.Message;}
        }
        static int Group(CandySound sound)
        {
            if(CandyBreathing.IsBreath(sound))return 3;
            int id=(int)sound;if(id<=6)return 0;if(id==7||id==8||id==20)return 1;if(id>=9&&id<=13||id==23||id==24)return 2;
            if(id>=14&&id<=17||id==26)return 3;if(id==18||id==19||id==21)return 4;if(id==22)return 5;if(id>=27)return 6;return 7;
        }
        public static string Label(CandySound sound)
        {
            string raw=sound.ToString(),result="";for(int i=0;i<raw.Length;i++){if(i>0&&char.IsUpper(raw[i]))result+=" ";result+=raw[i];}return result;
        }
        public static string Description(CandySound s)=>s switch
        {
            CandySound.BlockTap=>"Small candy-crate knocks and gentle bumps.",CandySound.BlockHit=>"Solid impacts; stronger hits sound heavier.",CandySound.BlockSmash=>"Big crashes when blocks slam together.",CandySound.BlockScrape=>"Crates rubbing or sliding against a surface.",
            CandySound.StructureLean=>"A cartoon timber creak as the tower leans.",CandySound.BlockSplash=>"Heavy blocks crossing the ocean surface.",CandySound.PlayerSplash=>"A bubbly splash when a survivor hits water.",
            CandySound.RubberSlide=>"Continuous rubber friction from fingers on crates.",CandySound.RubberTouch=>"Short squeaks when a hand makes sliding contact.",CandySound.HandFlight=>"Air rushing around a moving giant hand.",
            CandySound.FootstepCandy=>"Light, springy steps on shiny crate tops.",CandySound.FootstepSand=>"Soft, grainy footsteps on the island.",CandySound.Jump=>"A small upward hop and lift-off sound.",CandySound.LandSoft=>"Gentle feet-first landings.",CandySound.LandHard=>"Heavier landings after a bigger drop.",
            CandySound.VaultGrunt=>"Playful effort chirps while vaulting a ledge.",CandySound.ClimbGrunt=>"Short effort sounds during an active climb.",CandySound.ClimbPlant=>"Suction pops as climbing hands settle into contact.",CandySound.ClimbRelease=>"A rubbery pop when the survivor lets go.",CandySound.GripWarning=>"A gentle cue when climbing grip is nearly gone.",
            CandySound.FallWind=>"Airflow that grows with falling speed.",CandySound.BeachWind=>"Breezy island air, quieter behind surrounding walls.",CandySound.Surf=>"The ocean's soft, rolling background wash.",CandySound.Music=>"The cheerful mallet, bass and percussion loop.",
            CandySound.Spawn=>"A little hello when a survivor appears.",CandySound.Respawn=>"A cheerful return after being knocked out.",CandySound.UiClick=>"A light pop for demo menu interaction.",
            CandySound.BreathExertion=>"Steady effort breathing during climbing.",CandySound.BreathRecovery=>"Calming breaths as grip recovers.",CandySound.BreathExhausted=>"Tired breathing when climbing grip runs out.",
            CandySound.FriendlyWave=>"A bright greeting when players meet.",CandySound.TotallyMeantThat=>"A smug little 'I meant to do that' celebration.",CandySound.Phew=>"Relieved chirps after a narrow escape.",CandySound.WhatWasThat=>"A puzzled reaction to the surrounding mayhem.",CandySound.GolfClap=>"A tiny, cheeky round of applause.",CandySound.JustChilling=>"A relaxed little tune while hanging around.",CandySound.NopeNopeNope=>"A rapid, silly panic reaction.",_=>"A short happy flourish for the victory dance."
        };
        void OnGUI()
        {
            if(!engine||!engine.bank||!IsOpen&&!ShowLauncher)return;Styles();var matrix=GUI.matrix;int depth=GUI.depth;Color old=GUI.color,bg=GUI.backgroundColor;
            float scale=Mathf.Min(Screen.width/W,Screen.height/H);GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-W*scale)/2,(Screen.height-H*scale)/2),Quaternion.identity,Vector3.one*scale);GUI.depth=-1000;
            if(!IsOpen){if(Btn(new Rect(W-204,15,188,39),"Sound studio  •  F8",Mint))Open();}
            else GUI.ModalWindow(GetInstanceID(),new Rect(20,18,W-40,H-36),DrawWindow,GUIContent.none,GUIStyle.none);
            GUI.matrix=matrix;GUI.depth=depth;GUI.color=old;GUI.backgroundColor=bg;
        }
        void DrawWindow(int id)
        {
            Box(new Rect(0,0,W-40,H-36),Ink);GUI.enabled=selected==null;
            GUI.DrawTexture(new Rect(23,21,36,36),Icon(5));GUI.Label(new Rect(74,16,700,35),"CANDY SOUND STUDIO",title);
            GUI.Label(new Rect(75,54,700,24),"Listen. Tweak. Find your game's sweet spot.",body);
            if(Btn(new Rect(1101,23,110,35),"Close  ×",Card))Close();
            Box(new Rect(16,98,211,581),Panel);
            if(Btn(new Rect(29,112,185,40),"All sounds  ·  "+working.cues.Length,category<0?new Color(.25f,.43f,.47f):Card)){category=-1;scroll=Vector2.zero;}
            for(int i=0;i<Groups.Length;i++)
            {
                var r=new Rect(29,165+i*58,185,49);Box(r,category==i?new Color(.25f,.31f,.44f):Panel);
                var c=GUI.color;GUI.color=Accents[i];GUI.DrawTexture(new Rect(r.x+8,r.y+9,29,29),Icon(i));GUI.color=c;
                if(GUI.Button(new Rect(r.x+45,r.y,r.width-45,r.height),Groups[i],body)){category=i;scroll=Vector2.zero;}
            }
            GUI.Label(new Rect(247,100,90,30),"FIND A SOUND",small);search=GUI.TextField(new Rect(343,98,480,34),search,field);
            if(Btn(new Rect(838,98,116,34),"Save preset",Card))SavePreset();if(Btn(new Rect(966,98,116,34),"Load preset",Card))LoadPreset();
            if(Btn(new Rect(1094,98,116,34),"Reset mix",Card))
            {preview.Stop();selected=null;Destroy(working);working=Instantiate(original);working.name=original.name+" (studio session)";engine.Configure(working);status="Original mix restored for this session.";}
            shown.Clear();foreach(var cue in working.cues)if((category<0||Group(cue.sound)==category)&&(string.IsNullOrWhiteSpace(search)||(Label(cue.sound)+Description(cue.sound)).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0))shown.Add(cue);
            scroll=GUI.BeginScrollView(new Rect(242,147,978,435),scroll,new Rect(0,0,953,Mathf.Max(430,Mathf.Ceil(shown.Count/3f)*127)));
            for(int i=0;i<shown.Count;i++)DrawCard(shown[i],new Rect((i%3)*317,(i/3)*127,305,115));GUI.EndScrollView();
            if(shown.Count==0)GUI.Label(new Rect(260,200,700,60),"No matches. Try another category or a shorter search.",heading);
            Box(new Rect(246,598,964,81),Panel);GUI.Label(new Rect(260,608,220,25),"LIVE MIX",heading);
            working.master=MiniFader("Master",working.master,260,641);working.world=MiniFader("World",working.world,416,641);working.characters=MiniFader("Voices",working.characters,572,641);
            working.ambience=MiniFader("Island",working.ambience,728,641);working.music=MiniFader("Music",working.music,884,641);working.ui=MiniFader("Menu",working.ui,1040,641);
            GUI.Label(new Rect(24,695,1190,28),status,small);GUI.Label(new Rect(24,726,1190,24),"Runtime edits use a session copy. Save preset keeps your mix on this device. F8 / Esc closes the studio.",small);
            GUI.enabled=true;
            if(selected!=null){Box(new Rect(0,0,W-40,H-36),new Color(.015f,.02f,.05f,.85f));DrawEditor();}
        }
        void DrawCard(CandyAudioCue cue,Rect r)
        {
            Box(r,Card);var accent=Accents[Group(cue.sound)];var c=GUI.color;GUI.color=accent;GUI.DrawTexture(new Rect(r.x+13,r.y+15,34,34),Icon(Group(cue.sound)));GUI.color=c;
            GUI.Label(new Rect(r.x+57,r.y+10,r.width-65,27),Label(cue.sound),heading);
            GUI.Label(new Rect(r.x+57,r.y+37,r.width-69,41),Description(cue.sound),small);
            GUI.Label(new Rect(r.x+14,r.y+83,160,20),(cue.loop?"LOOP":""+cue.variants.Length+" TAKES")+(cue.enabled?"  •  LIVE":"  •  MUTED"),small);
            if(Btn(new Rect(r.x+r.width-86,r.y+80,73,25),"Edit  ›",accent))Select(cue.sound);
        }
        void DrawEditor()
        {
            Rect r=new Rect(130,48,980,655);Box(r,Panel);var accent=Accents[Group(selected.sound)];
            var c=GUI.color;GUI.color=accent;GUI.DrawTexture(new Rect(151,68,43,43),Icon(Group(selected.sound)));GUI.color=c;
            GUI.Label(new Rect(209,65,710,35),Label(selected.sound),title);GUI.Label(new Rect(210,105,775,35),Description(selected.sound),body);
            if(Btn(new Rect(1025,65,62,34),"×",Card)){selected=null;preview.Stop();library=false;return;}
            Box(new Rect(152,155,496,170),Ink);DrawWave(new Rect(166,174,466,110),accent);
            var clip=CurrentClip();GUI.Label(new Rect(167,294,465,23),clip?clip.name+"  ·  "+clip.length.ToString("0.00")+"s":"Choose a clip from the library",small);
            if(Btn(new Rect(152,340,152,42),preview.isPlaying?"■  Stop":"▶  Preview",accent)){if(preview.isPlaying)preview.Stop();else Audition();}
            if(Btn(new Rect(317,340,153,42),"Next variation",Card)){take=(take+1)%Mathf.Max(1,selected.variants.Length);ReadWave();Audition();}
            quiet=GUI.Toggle(new Rect(484,350,163,30),quiet,"Quiet game mix");
            GUI.Label(new Rect(154,401,490,28),"TAKES  ·  click a variation to compare",small);
            for(int i=0;i<selected.variants.Length&&i<8;i++)if(Btn(new Rect(153+i*61,438,54,34),""+(i+1),take==i?accent:Card)){take=i;ReadWave();if(preview.isPlaying)Audition();}
            if(Btn(new Rect(153,488,242,37),"Change selected clip…",Card)){library=!library;clipSearch="";clipScroll=Vector2.zero;}
            if(Btn(new Rect(407,488,242,37),"Add variation",Card)&&selected.variants.Length<8){var list=new List<AudioClip>(selected.variants);list.Add(CurrentClip());selected.variants=list.ToArray();take=list.Count-1;ReadWave();}
            spatial=GUI.Toggle(new Rect(154,546,470,25),spatial,"3D preview  ·  hear distance and direction");
            distance=Fader("Distance",distance,0,Mathf.Max(2,selected.maxDistance),new Rect(154,585,230,50),"m");angle=Fader("Direction",angle,-180,180,new Rect(408,585,230,50),"°");
            if(!library)
            {
                selected.enabled=GUI.Toggle(new Rect(680,157,360,28),selected.enabled,"Enabled during gameplay");
                selected.volume=Fader("Sound level",selected.volume,0,1,new Rect(680,207,391,52),"",true);
                strength=Fader("Preview intensity",strength,.1f,1.3f,new Rect(680,272,391,52),"",true);
                selected.pitch.x=Fader("Pitch range • low",selected.pitch.x,.5f,1.6f,new Rect(680,337,187,52),"×");
                selected.pitch.y=Fader("high",selected.pitch.y,selected.pitch.x,1.8f,new Rect(884,337,187,52),"×");
                selected.minDistance=Fader("Full-volume distance",selected.minDistance,.25f,Mathf.Max(1,selected.maxDistance-1),new Rect(680,406,391,52),"m");
                selected.maxDistance=Fader("Fade-out distance",selected.maxDistance,selected.minDistance+1,120,new Rect(680,475,391,52),"m");
                GUI.Label(new Rect(681,546,390,54),"Volume updates live. Apply restarts active loops with changed clips and pitch settings.",small);
                if(Btn(new Rect(681,606,188,38),"Apply to game",Mint)){engine.Configure(working);status="Sound settings applied. Active loops will fade back in.";}
                if(Btn(new Rect(882,606,188,38),"Reset this set",Card))ResetCue();
            }
            else DrawLibrary();
        }
        void DrawLibrary()
        {
            GUI.Label(new Rect(681,157,390,30),"CLIP LIBRARY  ·  "+clips.Count,heading);
            clipSearch=GUI.TextField(new Rect(681,198,390,32),clipSearch,field);
            var filtered=clips.FindAll(c=>c.name.IndexOf(clipSearch,StringComparison.OrdinalIgnoreCase)>=0);
            clipScroll=GUI.BeginScrollView(new Rect(680,247,395,344),clipScroll,new Rect(0,0,373,Mathf.Max(340,filtered.Count*39)));
            for(int i=0;i<filtered.Count;i++)if(Btn(new Rect(0,i*39,370,33),filtered[i].name,Card))
            {selected.variants[take]=filtered[i];library=false;ReadWave();Audition();status="Selected take replaced in this session.";}
            GUI.EndScrollView();
            if(Btn(new Rect(681,607,188,37),"Refresh library",Card)){GatherClips();status="Library refreshed from sound banks and added resource clips.";}
            if(Btn(new Rect(882,607,188,37),"Back to settings",Card))library=false;
        }
        void DrawWave(Rect r,Color color)
        {
            var old=GUI.color;GUI.color=color;float width=r.width/waveform.Length;
            for(int i=0;i<waveform.Length;i++){float h=Mathf.Clamp(waveform[i]*r.height*(preview.isPlaying?4:1),2,r.height);GUI.DrawTexture(new Rect(r.x+i*width,r.center.y-h*.5f,Mathf.Max(1,width-2),h),Texture2D.whiteTexture);}GUI.color=old;
            if(preview.isPlaying&&preview.clip){float x=r.x+preview.time/preview.clip.length*r.width;GUI.DrawTexture(new Rect(x,r.y,2,r.height),Texture2D.whiteTexture);}
        }
        float MiniFader(string label,float value,float x,float y)
        {GUI.Label(new Rect(x,y-7,130,21),label+"  "+Mathf.RoundToInt(value*100)+"%",small);return GUI.HorizontalSlider(new Rect(x,y+17,133,17),value,0,1);}
        float Fader(string label,float value,float min,float max,Rect r,string unit,bool percent=false)
        {GUI.Label(new Rect(r.x,r.y,r.width,24),label+"   "+(percent?Mathf.RoundToInt(value*100)+"%":value.ToString("0.00")+unit),body);return GUI.HorizontalSlider(new Rect(r.x,r.y+33,r.width,19),Mathf.Clamp(value,min,max),min,max);}
        bool Btn(Rect r,string label,Color tint){var old=GUI.backgroundColor;GUI.backgroundColor=tint;var text=tint.grayscale<.45f?Color.white:Ink;button.normal.textColor=text;button.hover.textColor=text;bool result=GUI.Button(r,label,button);GUI.backgroundColor=old;return result;}
        void Box(Rect r,Color tint){var old=GUI.backgroundColor;GUI.backgroundColor=tint;GUI.Box(r,GUIContent.none,card);GUI.backgroundColor=old;}
        void Styles()
        {
            if(stylesReady)return;stylesReady=true;rounded=new Texture2D(32,32,TextureFormat.RGBA32,false);rounded.hideFlags=HideFlags.HideAndDontSave;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float dx=Mathf.Max(8-x,x-23),dy=Mathf.Max(8-y,y-23);rounded.SetPixel(x,y,new Color(1,1,1,dx>0&&dy>0?Mathf.Clamp01(8.5f-Mathf.Sqrt(dx*dx+dy*dy)):1));}rounded.Apply();
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title=new GUIStyle(GUI.skin.label){font=font,fontSize=27,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            heading=new GUIStyle(title){fontSize=17};body=new GUIStyle(GUI.skin.label){font=font,fontSize=15,wordWrap=true,alignment=TextAnchor.MiddleLeft,normal={textColor=Color.white}};
            small=new GUIStyle(body){fontSize=12,normal={textColor=Muted}};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=14,fontStyle=FontStyle.Bold,border=new RectOffset(8,8,8,8),normal={background=rounded,textColor=Ink},hover={background=rounded,textColor=Ink},active={background=rounded,textColor=Color.white}};
            card=new GUIStyle(GUI.skin.box){border=new RectOffset(8,8,8,8),normal={background=rounded}};
            field=new GUIStyle(GUI.skin.textField){font=font,fontSize=17,padding=new RectOffset(10,10,5,5)};
        }
        Texture2D Icon(int group)
        {
            if(icons.TryGetValue(group,out var icon))return icon;
            icon=new Texture2D(64,64,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};var pixels=new Color[4096];
            void Dot(float x,float y,float radius){for(int py=Mathf.Max(0,(int)(y-radius));py<Mathf.Min(64,y+radius+1);py++)for(int px=Mathf.Max(0,(int)(x-radius));px<Mathf.Min(64,x+radius+1);px++){float a=Mathf.Clamp01(radius+.5f-Vector2.Distance(new Vector2(px,py),new Vector2(x,y)));if(a>pixels[py*64+px].a)pixels[py*64+px]=new Color(1,1,1,a);}}
            void Line(float x,float y,float xx,float yy,float width=2){int n=Mathf.CeilToInt(Vector2.Distance(new Vector2(x,y),new Vector2(xx,yy))*2);for(int i=0;i<=n;i++){float t=(float)i/Mathf.Max(1,n);Dot(Mathf.Lerp(x,xx,t),Mathf.Lerp(y,yy,t),width);}}
            void Ring(float x,float y,float radius){for(int i=0;i<64;i++){float a=i*Mathf.PI/32;Dot(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,1.7f);}}
            if(group==0){Line(12,22,32,10);Line(32,10,53,22);Line(53,22,53,45);Line(53,45,32,56);Line(32,56,12,45);Line(12,45,12,22);Line(12,45,32,33);Line(53,45,32,33);Line(32,33,32,10);}
            else if(group==1){Ring(32,27,14);for(int i=0;i<4;i++)Line(20+i*8,33,20+i*8,51-i%2*4,3);Line(19,24,10,34,4);Line(22,12,43,12,3);}
            else if(group==2){Ring(24,23,10);Line(20,19,15,38,5);Ring(39,46,8);Line(44,43,49,27,5);}
            else if(group==3){Line(21,11,21,54);Line(43,11,43,54);for(int i=0;i<4;i++)Line(21,17+i*10,43,17+i*10);Ring(16,35,7);Ring(48,48,7);}
            else if(group==4){for(int j=0;j<3;j++)for(int i=8;i<54;i++)Dot(i,18+j*13+Mathf.Sin(i*.14f+j)*4,2);}
            else if(group==5){Line(26,19,26,49,2.5f);Line(26,49,49,54,2.5f);Line(49,54,49,25,2.5f);Dot(18,17,7);Dot(41,23,7);}
            else if(group==6){Ring(32,32,24);Dot(23,39,2.5f);Dot(41,39,2.5f);for(int i=0;i<30;i++){float a=Mathf.PI+i*Mathf.PI/29;Dot(32+Mathf.Cos(a)*13,31+Mathf.Sin(a)*12,2);}}
            else{for(int j=0;j<3;j++){Line(10,17+j*15,54,17+j*15);Dot(j%2==0?23:42,17+j*15,5);}}
            icon.SetPixels(pixels);icon.Apply();icons[group]=icon;return icon;
        }
        // Never allocate a fresh voice pool during scene teardown. A surviving engine
        // notices this bank assignment in its next Update and rebuilds normally.
        void OnDestroy(){if(engine){engine.auditionDucking=1;if(working&&engine.bank==working&&original)engine.bank=original;}if(working)Destroy(working);if(rounded)Destroy(rounded);foreach(var icon in icons.Values)if(icon)Destroy(icon);}
    }
}
