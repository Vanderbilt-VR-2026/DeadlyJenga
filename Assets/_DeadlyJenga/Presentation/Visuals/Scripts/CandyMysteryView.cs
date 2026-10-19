using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyMysteryView:MonoBehaviour
    {
        Canvas canvas;RectTransform root;Text role,prompt,choices;
        Camera eye;bool vr;
        readonly Dictionary<string,Text> markers=new Dictionary<string,Text>();
        readonly Dictionary<string,Transform> weapons=new Dictionary<string,Transform>();
        readonly List<Material> materials=new List<Material>();
        float nextCommand;string currentRevive;
        readonly Dictionary<int,List<MysteryTrailFrame>> trailsByBlock=new Dictionary<int,List<MysteryTrailFrame>>();
        int cachedTrails;MysteryTrailFrame firstTrail;
        readonly HashSet<string> announcedSheriffs=new HashSet<string>();
        float sheriffNoticeUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyMysteryView>())go.AddComponent<CandyMysteryView>();};
        Text Label(string title,Vector2 point,Vector2 size,int font)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Text),typeof(Outline));go.transform.SetParent(root,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.fontStyle=FontStyle.Bold;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.supportRichText=true;t.raycastTarget=false;t.rectTransform.sizeDelta=size;t.rectTransform.anchoredPosition=point;go.GetComponent<Outline>().effectColor=new Color(.06f,.01f,.1f,.95f);return t;
        }
        void Build()
        {
            var go=new GameObject("Visual - mystery clues",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.sortingOrder=30;root=go.GetComponent<RectTransform>();var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            role=Label("Your secret role",new Vector2(0,-280),new Vector2(850,40),21);prompt=Label("Mystery interaction",new Vector2(0,-320),new Vector2(950,40),18);choices=Label("Private disaster choices",new Vector2(0,140),new Vector2(1050,95),22);
        }
        void Update()
        {
            var match=MatchRuntime.Current;var mystery=match as IMysteryControls;var session=Services.Session;
            if(mystery==null||session==null)return;
            if(!canvas)Build();canvas.enabled=mystery.Active&&!session.MenuOpen;
            if(eye!=session.ActiveCamera||vr!=session.IsVR)
            {
                eye=session.ActiveCamera;vr=session.IsVR;
                if(vr&&eye){root.SetParent(eye.transform,false);canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=eye;root.sizeDelta=new Vector2(1280,720);root.localPosition=new Vector3(0,0,1.1f);root.localRotation=Quaternion.identity;root.localScale=Vector3.one*.0007f;}
                else{root.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;root.localScale=Vector3.one;}
            }
            foreach(var t in markers.Values)t.gameObject.SetActive(false);
            foreach(var w in weapons.Values)w.gameObject.SetActive(false);
            if(!mystery.Active||!eye)return;
            if(mystery.Clues.bodies.Length==0){announcedSheriffs.Clear();sheriffNoticeUntil=0;}
            var own=mystery.Personal;
            role.text="YOU ARE "+own.role.ToString().ToUpperInvariant()+(own.role==MysteryRole.Murderer||own.role==MysteryRole.Sheriff?"  ·  "+(own.specialAttack?"SPECIAL ARMED":"NORMAL PUNCH")+"  ·  Q / mode menu":"  ·  STAY ALERT");
            if(own.attackReadyIn>0)role.text+="  ·  READY IN "+Mathf.CeilToInt(own.attackReadyIn);
            bool pressed=vr?session.MenuSelectPressed:Keyboard.current?.eKey.wasPressedThisFrame==true;
            bool held=vr?session.MenuSelectHeld:Keyboard.current?.eKey.isPressed==true;
            if(!session.MenuOpen&&Keyboard.current?.qKey.wasPressedThisFrame==true)mystery.SetSpecialAttack(!own.specialAttack);
            prompt.text="";currentRevive=null;float nearest=3;MysteryBodyFrame nearbyBody=null;
            foreach(var body in mystery.Clues.bodies)
            {
                if(body.lost)continue;
                if(body.sheriff&&announcedSheriffs.Add(body.id))sheriffNoticeUntil=Time.unscaledTime+5;
                float distance=Vector3.Distance(eye.transform.position,body.position+Vector3.up*.4f);
                bool close=distance<3&&Visible(body.position+Vector3.up*.3f);
                if(close&&distance<nearest){nearest=distance;nearbyBody=body;}
                string name=!body.sheriff||close?body.name:"";
                if(body.reported)
                {
                    string suspected=string.Join(" ",body.suspected.Select(c=>"<color=#"+ColorUtility.ToHtmlStringRGB(c)+">●</color> suspected"));
                    string text=suspected+"\n<color=#FF526B>BODY FOUND</color>\n"+name+"\n<color=#"+ColorUtility.ToHtmlStringRGB(body.finderColor)+">●</color> found it";
                    Marker("body:"+body.id,body.position+Vector3.up*.8f,text,body.reportedAge>30?.4f:1);
                }
                else if(body.sheriff)Marker("body:"+body.id,body.position+Vector3.up*.8f,"<color=#7EBBFF>SHERIFF HAS FALLEN</color>\n"+name,1);
                else if(close)Marker("body:"+body.id,body.position+Vector3.up*.5f,name,1);
            }
            foreach(string id in mystery.Clues.downed)
            {
                var p=match.Frame.players.FirstOrDefault(x=>x.id==id);if(p==null)continue;
                Marker("down:"+id,p.position+Vector3.up*.8f,"<color=#70FF91>"+p.name+" Needs Revive</color>",1);
                if(id!=match.LocalActorId&&Vector3.Distance(eye.transform.position,p.position+Vector3.up*.4f)<3&&Visible(p.position+Vector3.up*.4f))currentRevive=id;
            }
            if(!session.MenuOpen)
            {
                if(currentRevive!=null){prompt.text=vr?"Hold trigger / pinch to revive":"HOLD E TO REVIVE";if(Time.unscaledTime>=nextCommand){nextCommand=Time.unscaledTime+.15f;mystery.Revive(currentRevive,held);}}
                else if(nearbyBody!=null&&(!nearbyBody.reported||nearbyBody.sheriff)){prompt.text=nearbyBody.sheriff?"E / PINCH · REPORT / RECOVER SHERIFF BADGE":vr?"Trigger / pinch to report body":"E · REPORT BODY";if(pressed)mystery.ReportBody(nearbyBody.id);}
                else if(nearbyBody?.reported==true&&nearbyBody.reportedAge<=30)
                {
                    var trail=NearestTrail(mystery,nearbyBody.position);
                    if(trail!=null){prompt.text="E / PINCH · SUSPECT <color=#"+ColorUtility.ToHtmlStringRGB(trail.color)+">● THIS TRAIL</color>";if(pressed)mystery.Suspect(nearbyBody.id,trail.sequence);}
                }
            }
            if(nearbyBody!=null){var reportLock=own.reportLocks?.FirstOrDefault(l=>l.body==nearbyBody.id);if(reportLock!=null&&reportLock.remaining>0)prompt.text="REPORT READY IN "+Mathf.CeilToInt(reportLock.remaining)+"s";}
            DrawChoices(mystery,session.MenuOpen);
            foreach(var strike in mystery.Clues.strikes)
            {
                var actor=match.FindActor(strike.actor);if(actor==null)continue;
                string key=strike.actor+":"+strike.role;if(!weapons.TryGetValue(key,out var weapon)){weapon=MakeWeapon(strike.role);weapons[key]=weapon;}
                var hand=actor.HandPose(false);weapon.gameObject.SetActive(true);weapon.SetPositionAndRotation(hand.position,hand.rotation);
            }
        }
        bool Visible(Vector3 point)
        {var delta=point-eye.transform.position;return !Physics.Raycast(eye.transform.position,delta.normalized,delta.magnitude*.9f,(1<<Layers.Block)|(1<<Layers.Surface),QueryTriggerInteraction.Ignore);}
        MysteryTrailFrame NearestTrail(IMysteryControls mystery,Vector3 body)
        {
            if(mystery.Trails.Count<cachedTrails||mystery.Trails.Count>0&&firstTrail!=mystery.Trails[0]){cachedTrails=0;trailsByBlock.Clear();}
            while(cachedTrails<mystery.Trails.Count){var t=mystery.Trails[cachedTrails++];firstTrail=mystery.Trails[0];if(!trailsByBlock.TryGetValue(t.block,out var list)){list=new List<MysteryTrailFrame>();trailsByBlock[t.block]=list;}list.Add(t);}
            MysteryTrailFrame best=null;float closest=3;var blocks=Services.World?.Blocks;if(blocks==null||!Physics.Raycast(eye.transform.position,eye.transform.forward,out var hit,3,1<<Layers.Block,QueryTriggerInteraction.Ignore)||!hit.rigidbody)return null;
            int index=-1;for(int i=0;i<blocks.Count;i++)if(blocks[i]==hit.rigidbody){index=i;break;}
            if(!trailsByBlock.TryGetValue(index,out var candidates))return null;
            foreach(var t in candidates)
            {
                if(t.block<0||t.block>=blocks.Count||!blocks[t.block])continue;
                var point=blocks[t.block].transform.TransformPoint(t.point);if((point-body).sqrMagnitude>9.4f)continue;
                var delta=point-eye.transform.position;float distance=delta.magnitude;if(distance>closest||Vector3.Dot(delta.normalized,eye.transform.forward)<.85f)continue;
                closest=distance;best=t;
            }
            return best;
        }
        void DrawChoices(IMysteryControls mystery,bool menu)
        {
            var p=mystery.Personal;choices.text=p.disasterChoices.Length==0?"":(p.veto?"VETO ONE":"CHOOSE ONE")+" · "+Mathf.CeilToInt(p.choiceRemaining)+"s\n"+string.Join("      ",p.disasterChoices.Select((s,i)=>(i+1)+" · "+s));
            if(Time.unscaledTime<sheriffNoticeUntil)choices.text="<color=#7EBBFF>SHERIFF HAS FALLEN</color>\n"+choices.text;
            if(menu)return;
            for(int i=0;i<p.disasterChoices.Length;i++)if(Keyboard.current!=null&&Keyboard.current[(Key)((int)Key.Digit1+i)].wasPressedThisFrame)mystery.ChooseDisaster(p.disasterChoices[i]);
            if(vr&&Services.Session.MenuSelectPressed&&p.disasterChoices.Length>0)
            {
                var plane=new Plane(root.forward,root.position);if(plane.Raycast(new Ray(eye.transform.position,eye.transform.forward),out float d))
                {var local=root.InverseTransformPoint(eye.transform.position+eye.transform.forward*d);if(local.y>90&&local.y<190){int index=Mathf.Clamp(Mathf.FloorToInt((local.x+525)/(1050/p.disasterChoices.Length)),0,p.disasterChoices.Length-1);mystery.ChooseDisaster(p.disasterChoices[index]);}}
            }
        }
        void Marker(string id,Vector3 position,string text,float alpha)
        {
            if(!markers.TryGetValue(id,out var label)){label=Label("Mystery marker",Vector2.zero,new Vector2(430,125),16);markers[id]=label;}
            var point=eye.WorldToViewportPoint(position);bool show=point.z>0&&point.x>0&&point.x<1&&point.y>0&&point.y<1;
            label.gameObject.SetActive(show);if(!show)return;label.rectTransform.anchoredPosition=new Vector2((point.x-.5f)*1280,(point.y-.5f)*720);label.text=text;label.color=Color.white;var group=label.GetComponent<CanvasGroup>();if(!group)group=label.gameObject.AddComponent<CanvasGroup>();group.alpha=alpha;
        }
        Transform MakeWeapon(MysteryRole role)
        {
            var root=new GameObject("Visual - mystery special weapon").transform;root.SetParent(transform,false);Color color=role==MysteryRole.Murderer?new Color(1,.04f,.12f):new Color(.05f,.4f,1);
            Part(root,"Handle",PrimitiveType.Cylinder,new Vector3(0,0,.2f),new Vector3(.035f,.25f,.035f),Quaternion.Euler(90,0,0),color);
            if(role==MysteryRole.Murderer)Part(root,"Red blade",PrimitiveType.Cube,new Vector3(0,0,.65f),new Vector3(.14f,.035f,.7f),Quaternion.identity,color);
            else Part(root,"Blue hammer",PrimitiveType.Cube,new Vector3(0,0,.6f),new Vector3(.65f,.3f,.28f),Quaternion.identity,color);return root;
        }
        void Part(Transform root,string name,PrimitiveType type,Vector3 point,Vector3 scale,Quaternion rotation,Color color)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.transform.localPosition=point;go.transform.localRotation=rotation;go.transform.localScale=scale;go.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>(type+".fbx");var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.85f);materials.Add(material);go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        void OnDestroy(){foreach(var material in materials)if(material)Destroy(material);if(root)Destroy(root.gameObject);}
    }
}
