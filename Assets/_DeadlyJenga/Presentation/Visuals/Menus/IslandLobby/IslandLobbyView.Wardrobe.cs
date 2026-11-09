using System.Linq;
using System;

using DeadlyJenga.Core;

using UnityEngine;

using UnityEngine.InputSystem;

using UnityEngine.Rendering;

using UnityEngine.Rendering.Universal;

using UnityEngine.UI;



namespace DeadlyJenga.CastlePrototype

{

    public sealed partial class IslandLobbyView

    {

        static readonly string[] accessoryNames={"MOHAWK","GLASSES","RABBIT EARS","SKELETON MASK","TOP HAT","CAPE","ANT ANTENNAE","UNICORN HORN","DEVIL HORNS","BROCCOLI CROWN","LONG NOSE","DEFINED JAW"};

        int wardrobeCategory,wardrobePage;

        static readonly string[] wardrobeCategories={"ADD ONS","SKINS","HAIR","EYEWEAR","CLOTHING"};

        Vector3 wardrobeCenter=new Vector3(0,.65f,-5.3f);

        readonly System.Collections.Generic.List<Tuple<RectTransform,Action>> wardrobeButtons=new System.Collections.Generic.List<Tuple<RectTransform,Action>>();

        RectTransform wardrobePanel,wardrobeDesktop;

        Camera wardrobeCamera;
        readonly System.Collections.Generic.List<Renderer> wardrobeRenderers=new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<int> wardrobeLayers=new System.Collections.Generic.List<int>();

        RenderTexture wardrobeTexture,portraitTexture;
        sealed class GeometryViewCache:IDisposable
        {
            readonly WardrobeFraming.GeometrySampler sampler=new WardrobeFraming.GeometrySampler();
            AvatarAppearance appearance;int signature,readyFrame;bool identityKnown,sampled;float nextSample;
            public bool Changed {get;private set;}
            public void Invalidate(){identityKnown=false;sampled=false;readyFrame=Time.frameCount+2;}
            public bool Try(System.Collections.Generic.List<Renderer> renderers,AvatarAppearance current,out Bounds bounds)
            {
                int next=17;foreach(var renderer in renderers)if(renderer)
                {next=unchecked(next*31+renderer.GetInstanceID());next=unchecked(next*31+(renderer.enabled?1:0));if(renderer is SkinnedMeshRenderer skin&&skin.sharedMesh)next=unchecked(next*31+skin.sharedMesh.GetInstanceID());}
                Changed=!identityKnown||signature!=next||!appearance.Equals(current);
                if(Changed){identityKnown=true;signature=next;appearance=current;sampled=false;readyFrame=Time.frameCount+2;}
                if(Time.frameCount>=readyFrame&&(!sampled||Time.unscaledTime>=nextSample))
                {sampler.Refresh(renderers);sampled=true;nextSample=Time.unscaledTime+1;}
                bounds=default;return sampled&&sampler.TryBounds(renderers,out bounds,out bool conservative)&&!conservative;
            }
            public void Dispose()=>sampler.Dispose();
        }
        GeometryViewCache wardrobeGeometry,mirrorGeometry;
        readonly System.Collections.Generic.List<Renderer> mirrorRenderers=new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<int> mirrorLayers=new System.Collections.Generic.List<int>();
        readonly System.Collections.Generic.List<bool> mirrorHidden=new System.Collections.Generic.List<bool>();

        Material wardrobeMaterial;

        IAvatarAppearance wardrobeAvatar;
        Text wardrobeChoiceReason,wardrobeWorldReason;
        RectTransform wardrobeReasonViewport;
        float wardrobeReasonOffset;
        int wardrobeNoticeCategory=-1,wardrobeNoticePage=-1;
        AvatarAppearance wardrobeNoticeAppearance;
        bool wardrobeNoticeBound;
        const string WardrobeNoticeDefault="Point to a choice for details. Scroll this explanation for longer reasons.";
        string ChoiceNotice(AvatarAppearance proposed)
        {
            var rules=wardrobeAvatar as IAvatarWardrobePreview;
            if(rules==null)return "Fit assessment unavailable; selection saves your requested look.";
            var result=rules.EvaluateAppearance(proposed);
            return string.IsNullOrEmpty(result.reason)?"No layers hidden by current compatibility rules.":result.reason;
        }
        void SetWardrobeNotice(string value)
        {
            if(!wardrobeChoiceReason||wardrobeChoiceReason.text==value)return;
            wardrobeChoiceReason.text=value;wardrobeReasonOffset=0;wardrobeChoiceReason.rectTransform.anchoredPosition=Vector2.zero;
            wardrobeChoiceReason.rectTransform.sizeDelta=new Vector2(0,Mathf.Max(106,wardrobeChoiceReason.preferredHeight));
        }
        void ResetWardrobeNotice()
        {
            SetWardrobeNotice(WardrobeNoticeDefault);wardrobeNoticeBound=false;
            if(wardrobeWorldReason)wardrobeWorldReason.text="POINT TO A CHOICE FOR LAYER DETAILS. SELECT TO SAVE YOUR REQUEST.";
        }
        void UpdateWardrobeChoiceNotice(string id)
        {
            if(!wardrobeOpen||!wardrobeWorldReason||wardrobeAvatar==null)return;
            int index;
            if(id!=null&&id.StartsWith("wardrobe-",StringComparison.Ordinal)&&int.TryParse(id.Substring(9),out index)&&index>=0&&index<12&&wardrobePage*12+index<WardrobeCount)
                wardrobeWorldReason.text=ChoiceNotice(ProposedWardrobeEntry(wardrobePage*12+index));
            else wardrobeWorldReason.text="POINT TO A CHOICE FOR LAYER DETAILS. SELECT TO SAVE YOUR REQUEST.";
        }
        AvatarAppearance ProposedWardrobeEntry(int index)
        {
            var v=wardrobeAvatar.Appearance;
            switch(wardrobeCategory)
            {
                case 1:if(index==System.Enum.GetValues(typeof(AvatarSkin)).Length)v.poweredArmor=!v.poweredArmor;else v.skin=(AvatarSkin)index;break;
                case 2:v.hair=(int)v.hair==index+1?AvatarHair.None:(AvatarHair)(index+1);break;
                case 3:v.eyewear=(int)v.eyewear==index+1?AvatarEyewear.None:(AvatarEyewear)(index+1);break;
                case 4:if(index==14)v.accessories^=AvatarAccessory.PumpkinHelmet;else v.clothing^=(AvatarClothing)(1u<<index);break;
                default:int bit=index<12?index:index+3;v.accessories^=(AvatarAccessory)(1u<<bit);break;
            }
            return v;
        }


        IPlayableCharacter wardrobePlayer;

        IAvatarShowcase wardrobeShowcase;

        bool wardrobeWasShowcasing;
        IAvatarInspectionPose wardrobePose;
        bool wardrobeWasNeutral;
        bool wardrobePivotReady;
        int wardrobeFramingReadyAt;
        Vector3 wardrobePivotOffset;
        float wardrobeFitDistance;

        bool wardrobeOpen,wardrobeArmed=true,wardrobePaused;

        CursorLockMode wardrobeCursor;

        bool wardrobeCursorVisible;

        readonly CandyOrbitView wardrobeOrbit = new CandyOrbitView();

        RectTransform wardrobePreview;

        AvatarAccessory wardrobeDisplayed=(AvatarAccessory)uint.MaxValue;

        AvatarAppearance wardrobeLastAppearance;int wardrobeLastCategory=-1;

        public bool WardrobeOpen=>wardrobeOpen;

        public Vector3 WardrobePosition=>transform.TransformPoint(wardrobeCenter);



        void BuildWardrobe()

        {

            wardrobeMaterial=new Material(Resources.Load<Material>("CandyUtilityGlow")){color=new Color(.2f,1,.85f)};

            var pod=new GameObject("Visual - wardrobe pod");pod.transform.SetParent(transform,false);pod.transform.position=WardrobePosition;

            var ring=pod.AddComponent<LineRenderer>();ring.sharedMaterial=wardrobeMaterial;ring.useWorldSpace=false;ring.loop=true;ring.widthMultiplier=.035f;ring.positionCount=48;

            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*.9f,0,Mathf.Sin(a)*.9f));}

            for(int side=-1;side<=1;side+=2)

            {

                var go=new GameObject("Pod arch");go.transform.SetParent(pod.transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=wardrobeMaterial;line.useWorldSpace=false;line.widthMultiplier=.05f;line.positionCount=3;

                line.SetPositions(new[]{new Vector3(side*.9f,0,.4f),new Vector3(side*.9f,2.4f,.4f),new Vector3(0,2.65f,.4f)});

            }

            wardrobePanel=Terminal("WARDROBE",WardrobePosition+new Vector3(0,1.65f,1.25f),0,new Vector2(1080,1280),CandyHudVisuals.Mint);

            wardrobeWorldReason=Label(wardrobePanel,"POINT TO A CHOICE FOR LAYER DETAILS. SELECT TO SAVE YOUR REQUEST.",450,18,1000,110);

            Add(wardrobePanel,"wardrobe-open",()=>wardrobeOpen?"DONE / B OR Y TO EXIT":"CUSTOMIZE YOUR AVATAR",-605,()=>SetWardrobeOpen(!wardrobeOpen),()=>AppearanceTarget()!=null,0,950,60);

            for(int i=0;i<12;i++)

            {

                int item=i;Add(wardrobePanel,"wardrobe-"+i,()=>AccessoryTitle(item),-100-(i/2)*57,()=>ChooseWardrobeEntry(item),()=>wardrobeOpen,(i%2==0?-1:1)*260,500,48);

            }

            Add(wardrobePanel,"wardrobe-clear",()=>"< PAGE",-435,()=>ChangeWardrobePage(-1),()=>wardrobeOpen,-260,500,48);

            Add(wardrobePanel,"wardrobe-reset",()=>"PAGE "+(wardrobePage+1)+" / "+WardrobePages+" >",-435,()=>ChangeWardrobePage(1),()=>wardrobeOpen,260,500,48);

            string[] viewNames={"LEFT","RIGHT","UP","DOWN","CLOSER","FURTHER"};

            for(int i=0;i<6;i++){int control=i;var button=Add(wardrobePanel,"wardrobe-view-"+i,()=>viewNames[control],-40,()=>{},()=>wardrobeOpen,-425+i*170,155,44);button.slide=_=>AdjustWardrobeView(control);}

            for(int category=0;category<wardrobeCategories.Length;category++){int selected=category;Add(wardrobePanel,"wardrobe-category-"+category,()=>wardrobeCategories[selected],-485,()=>SetWardrobeCategory(selected),()=>wardrobeOpen,-400+category*200,190,42);}
            Add(wardrobePanel,"wardrobe-clear-look",()=>"CLEAR LOOK",-545,ClearAccessories,()=>wardrobeOpen,-260,500,48);
            Add(wardrobePanel,"wardrobe-reset-view",()=>"RESET VIEW",-545,ResetWardrobeView,()=>wardrobeOpen,260,500,48);
            wardrobeTexture=new RenderTexture(768,768,24){name="Wardrobe third person preview"};

            var image=new GameObject("Third person mirror",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(wardrobePanel,false);

            var raw=image.GetComponent<RawImage>();raw.texture=wardrobeTexture;raw.raycastTarget=false;raw.rectTransform.sizeDelta=new Vector2(430,430);raw.rectTransform.anchoredPosition=new Vector2(0,180);

            var cameraObject=new GameObject("Visual - wardrobe camera");cameraObject.transform.SetParent(transform,false);wardrobeCamera=cameraObject.AddComponent<Camera>();wardrobeCamera.enabled=false;wardrobeCamera.targetTexture=wardrobeTexture;wardrobeCamera.fieldOfView=44;wardrobeCamera.nearClipPlane=.05f;wardrobeCamera.farClipPlane=60;wardrobeCamera.cullingMask=1<<31;wardrobeCamera.clearFlags=CameraClearFlags.SolidColor;wardrobeCamera.backgroundColor=new Color(.07f,.045f,.12f);wardrobeCamera.GetUniversalAdditionalCameraData().allowXRRendering=false;

            foreach(var t in wardrobePanel.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;

        }

        IAvatarAppearance AppearanceTarget()=>Player?.gameObject.GetComponent<IAvatarAppearance>()??Player?.gameObject.GetComponentInChildren<IAvatarAppearance>();

        int WardrobeCount=>wardrobeCategory==1?System.Enum.GetValues(typeof(AvatarSkin)).Length+1:wardrobeCategory==2?15:wardrobeCategory==3?15:wardrobeCategory==4?15:29;
        int WardrobePages=>(WardrobeCount+11)/12;
        string AccessoryTitle(int index)
        {
            index+=wardrobePage*12;if(index>=WardrobeCount)return "â€”";
            var avatar=wardrobeAvatar??AppearanceTarget();if(avatar==null)return "â€”";var v=avatar.Appearance;string name;bool chosen;
            switch(wardrobeCategory)
            {
                case 1:bool armor=index==System.Enum.GetValues(typeof(AvatarSkin)).Length;chosen=armor?v.poweredArmor:(int)v.skin==index;name=armor?"POWERED ARMOR":PrettyName(((AvatarSkin)index).ToString());break;
                case 2:chosen=(int)v.hair==index+1;name=PrettyName(((AvatarHair)(index+1)).ToString());break;
                case 3:chosen=(int)v.eyewear==index+1;name=PrettyName(((AvatarEyewear)(index+1)).ToString());break;
                case 4:chosen=index==14?(v.accessories&AvatarAccessory.PumpkinHelmet)!=0:(v.clothing&(AvatarClothing)(1u<<index))!=0;name=index==14?"PUMPKIN HELMET":PrettyName(((AvatarClothing)(1u<<index)).ToString());break;
                default:int bit=index<12?index:index+3;chosen=(v.accessories&(AvatarAccessory)(1u<<bit))!=0;name=PrettyName(((AvatarAccessory)(1u<<bit)).ToString());break;
            }
            string notice="";
            if(wardrobeOpen&&wardrobeAvatar is IAvatarWardrobePreview rules)
            {
                var result=rules.EvaluateAppearance(ProposedWardrobeEntry(index));
                if(result.hiddenAccessories!=0||result.hiddenClothing!=0||result.hiddenHair)notice=" / COVERS LAYERS";
            }
            return (chosen?"ON  ":"OFF  ")+name+notice;
        }
        static string PrettyName(string name)=>System.Text.RegularExpressions.Regex.Replace(name=="HoneyBear"?"WinnieThePooh":name=="Sailor"?"Popeye":name=="RagDoll"?"RaggedyAnn":name=="Reindeer"?"Rudolph":name,"([a-z])([A-Z])","$1 $2").ToUpperInvariant();
        void SetWardrobeCategory(int category){wardrobeCategory=category;wardrobePage=0;wardrobeLastCategory=-1;ResetWardrobeNotice();RefreshWardrobeLabels();}
        void ChangeWardrobeCategory(int delta)=>SetWardrobeCategory((wardrobeCategory+delta+wardrobeCategories.Length)%wardrobeCategories.Length);
        void ChangeWardrobePage(int delta){wardrobePage=(wardrobePage+delta+WardrobePages)%WardrobePages;wardrobeLastCategory=-1;ResetWardrobeNotice();RefreshWardrobeLabels();}
        bool ChooseWardrobeEntry(int index)
        {
            if(!wardrobeOpen||wardrobeAvatar==null||index<0||index>=12)return false;
            index+=wardrobePage*12;if(index>=WardrobeCount)return false;var v=ProposedWardrobeEntry(index);
            wardrobeAvatar.ApplyAppearance(v);ResetWardrobeNotice();RefreshWardrobeLabels();return true;
        }
        public bool ToggleAccessory(int index)

        {

            if(!wardrobeOpen||wardrobeAvatar==null||index<0||index>=12)return false;

            var value=wardrobeAvatar.Appearance;value.accessories^=(AvatarAccessory)(1u<<index);wardrobeAvatar.ApplyAppearance(value);RefreshWardrobeLabels();return true;

        }

        void ClearAccessories(){if(wardrobeAvatar==null)return;wardrobeAvatar.ApplyAppearance(default);RefreshWardrobeLabels();}

        Vector3 wardrobePanelPosition;
        Quaternion wardrobePanelRotation;
        void PlaceWardrobeForEye()
        {
            var eye=wardrobePlayer.ViewCamera.transform;
            var forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f)forward=wardrobePlayer.transform.forward;
            wardrobePanel.SetPositionAndRotation(eye.position+forward*1.8f,Quaternion.LookRotation(forward,Vector3.up));
            Physics.SyncTransforms();
        }
        public void SetWardrobeOpen(bool open)

        {

            if(open==wardrobeOpen)return;

            if(open)

            {

                wardrobeAvatar=AppearanceTarget();if(wardrobeAvatar==null||!NetworkActors.Alive(Player))return;

                if (marina && Player.IsVR && wardrobeFromPortrait &&
                    Vector3.Distance(Player.transform.position, WardrobePosition) > 1)
                    Player.Place(WardrobePosition + Vector3.up * .04f, transform.rotation * DeadlyJenga.CandyArt.Marina.MayhemMarina.StyleScreenFacing);

                wardrobePlayer=Player;wardrobePaused=Player.InputSuspended;wardrobeCursor=Cursor.lockState;wardrobeCursorVisible=Cursor.visible;ResetWardrobeView();

                wardrobeOpen=true;ResetWardrobeNotice();wardrobeArmed=false;Player.InputSuspended=true;

                wardrobeShowcase=Player.gameObject.GetComponent<IAvatarShowcase>();

                if(wardrobeShowcase!=null){wardrobeWasShowcasing=wardrobeShowcase.Showcasing;wardrobeShowcase.Showcasing=true;}
                wardrobePose=Player.gameObject.GetComponent<IAvatarInspectionPose>();
                if(wardrobePose!=null){wardrobeWasNeutral=wardrobePose.NeutralInspection;wardrobePose.NeutralInspection=true;}

                if(!Player.IsVR){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;BuildDesktopWardrobe();}

                if(!marina&&Player.IsVR&&Player.ViewCamera)
                {
                    wardrobePanelPosition=wardrobePanel.position;wardrobePanelRotation=wardrobePanel.rotation;
                    PlaceWardrobeForEye();
                }
                PresentWardrobe();

            }

            else

            {

                wardrobeOpen=false;wardrobeFromPortrait=false;portraitReady=false;
                wardrobeGeometry?.Dispose();wardrobeGeometry=null;mirrorGeometry?.Invalidate();
                if(!marina&&NetworkActors.Alive(wardrobePlayer)&&wardrobePlayer.IsVR&&wardrobePanel)
                    wardrobePanel.SetPositionAndRotation(wardrobePanelPosition,wardrobePanelRotation);

                if(wardrobeShowcase!=null&&NetworkActors.Alive(wardrobePlayer))wardrobeShowcase.Showcasing=wardrobeWasShowcasing;

                wardrobeShowcase=null;
                if(wardrobePose!=null&&NetworkActors.Alive(wardrobePlayer))wardrobePose.NeutralInspection=wardrobeWasNeutral;
                wardrobePose=null;wardrobePivotReady=false;wardrobeFitDistance=0;

                bool currentOwner=ReferenceEquals(Services.Session,session)&&ReferenceEquals(Player,wardrobePlayer);
                if(currentOwner)session.SetMenu(session.MenuOpen);
                else if(NetworkActors.Alive(wardrobePlayer))wardrobePlayer.InputSuspended=wardrobePaused;

                if(wardrobeDesktop){wardrobeDesktop.gameObject.SetActive(false);Destroy(wardrobeDesktop.gameObject);wardrobeDesktop=null;}

                wardrobeButtons.Clear();if(Services.Session==null){Cursor.lockState=wardrobeCursor;Cursor.visible=wardrobeCursorVisible;}
                DeadlyJenga.Presentation.MenuAim.Consume();terminalRequireRelease=true;wardrobeAvatar=null;wardrobePlayer=null;

            }

        }

        void BuildDesktopWardrobe()

        {

            wardrobeDisplayed=(AvatarAccessory)uint.MaxValue;wardrobeLastCategory=-1;

            var go=new GameObject("Visual - wardrobe desktop",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(Image));go.transform.SetParent(transform,false);wardrobeDesktop=go.GetComponent<RectTransform>();

            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;

            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,800);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;go.GetComponent<Image>().color=new Color(.025f,.04f,.07f,.98f);

            var preview=new GameObject("Your existing avatar",typeof(RectTransform),typeof(RawImage));preview.transform.SetParent(wardrobeDesktop,false);var raw=preview.GetComponent<RawImage>();raw.texture=wardrobeTexture;raw.raycastTarget=false;wardrobePreview=raw.rectTransform;raw.rectTransform.sizeDelta=new Vector2(600,600);raw.rectTransform.anchoredPosition=new Vector2(-300,0);

            var panel=new GameObject("Options",typeof(RectTransform));panel.transform.SetParent(wardrobeDesktop,false);var rect=panel.GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(315,0);rect.sizeDelta=new Vector2(590,780);

            var heading=Label(rect,"YOUR WARDROBE",340,32,360,60);heading.rectTransform.anchoredPosition=new Vector2(-100,340);
            DesktopButton(rect,"DONE",340,190,175,50,()=>SetWardrobeOpen(false),"Done");Label(rect,"Select to save your requested look.\nCovered choices stay saved and restore when uncovered.",278,18,570,44);

            for(int i=0;i<12;i++){int item=i;DesktopButton(rect,AccessoryTitle(i),198-(i/2)*65,(i%2==0?-1:1)*145,275,56,()=>ChooseWardrobeEntry(item),"Accessory "+i);}

            DesktopButton(rect,"< PREVIOUS PAGE",-180,-145,275,40,()=>ChangeWardrobePage(-1),"Previous category");DesktopButton(rect,"NEXT PAGE >",-180,145,275,40,()=>ChangeWardrobePage(1),"Next category");
            for(int category=0;category<wardrobeCategories.Length;category++){int selected=category;DesktopButton(rect,wardrobeCategories[category],238,-232+category*116,112,38,()=>SetWardrobeCategory(selected),"Category "+category);}

            DesktopButton(rect,"CLEAR",-240,-145,275,56,ClearAccessories,"Clear");DesktopButton(rect,"RESET VIEW",-240,145,275,56,ResetWardrobeView,"Reset view");

            Label(rect,"Drag preview / arrows: orbit  /  Scroll / +/-: zoom",-375,18,575,34);

            var notice=new GameObject("Layer explanation viewport",typeof(RectTransform),typeof(RectMask2D));notice.transform.SetParent(rect,false);
            wardrobeReasonViewport=notice.GetComponent<RectTransform>();wardrobeReasonViewport.anchoredPosition=new Vector2(0,-304);wardrobeReasonViewport.sizeDelta=new Vector2(575,106);
            wardrobeChoiceReason=Label(rect,"Point to a choice for details. Scroll this explanation for longer reasons.",-304,18,575,106);
            wardrobeChoiceReason.transform.SetParent(wardrobeReasonViewport,false);wardrobeChoiceReason.alignment=TextAnchor.UpperLeft;
            wardrobeChoiceReason.rectTransform.anchorMin=new Vector2(0,1);wardrobeChoiceReason.rectTransform.anchorMax=new Vector2(1,1);wardrobeChoiceReason.rectTransform.pivot=new Vector2(.5f,1);wardrobeChoiceReason.rectTransform.sizeDelta=new Vector2(0,106);wardrobeChoiceReason.rectTransform.anchoredPosition=Vector2.zero;

            foreach(var child in wardrobeDesktop.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;

        }

        void DesktopButton(RectTransform parent,string title,float y,float x,float width,float height,Action action,string name)

        {

            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.sizeDelta=new Vector2(width,height);r.anchoredPosition=new Vector2(x,y);go.GetComponent<Image>().color=CandyHudVisuals.Card;Label(r,title,0,23,width-14,height-8);wardrobeButtons.Add(Tuple.Create(r,action));

        }

        void RefreshWardrobeLabels()

        {

            if(!wardrobeDesktop||wardrobeAvatar==null)return;

            var current=wardrobeAvatar.Appearance;

            if(wardrobeLastCategory==wardrobeCategory&&wardrobeLastAppearance.accessories==current.accessories&&wardrobeLastAppearance.skin==current.skin&&wardrobeLastAppearance.hair==current.hair&&wardrobeLastAppearance.eyewear==current.eyewear&&wardrobeLastAppearance.clothing==current.clothing&&wardrobeLastAppearance.poweredArmor==current.poweredArmor)return;

            wardrobeLastCategory=wardrobeCategory;wardrobeLastAppearance=current;

            wardrobeDisplayed=current.accessories;

            var next=wardrobeDesktop.Find("Options/Next category");if(next)next.GetComponentInChildren<Text>().text="PAGE "+(wardrobePage+1)+" / "+WardrobePages+" >";

            for(int i=0;i<12;i++){var t=wardrobeDesktop.Find("Options/Accessory "+i);if(t)t.GetComponentInChildren<Text>().text=AccessoryTitle(i);}

        }

        RawImage lobbyPortrait;
        bool wardrobeFromPortrait,portraitReady;
        AvatarAppearance portraitAppearance;
        string portraitName;
        float nextPortraitCheck;
        LobbyParticipant LocalPortraitIdentity=>Details?.Participants.FirstOrDefault(p=>p.id==Details.LocalParticipantId);
        void BuildLobbyPortrait()
        {
            var profile=Add(hostStation,"player-portrait",()=>portraitName??"My avatar",294,
                ()=>{wardrobeFromPortrait=true;SetWardrobeOpen(true);},()=>AppearanceTarget()!=null,-359,270,56);
            profile.label.rectTransform.sizeDelta=new Vector2(200,48);
            profile.label.rectTransform.anchoredPosition=new Vector2(27,0);
            profile.label.fontSize=18;
            var image=new GameObject("Your portrait · open wardrobe",typeof(RectTransform),typeof(RawImage));
            image.transform.SetParent(profile.button.transform,false);
            lobbyPortrait=image.GetComponent<RawImage>();
            portraitTexture=new RenderTexture(256,256,24){name="Lobby portrait only"};portraitTexture.Create();
            lobbyPortrait.texture=portraitTexture;
            lobbyPortrait.raycastTarget=false;
            lobbyPortrait.rectTransform.sizeDelta=new Vector2(48,48);
            lobbyPortrait.rectTransform.anchoredPosition=new Vector2(-104,0);
        }
        void RefreshLobbyPortrait()
        {
            if(!lobbyPortrait||!wardrobeTexture||wardrobeOpen||Time.unscaledTime<nextPortraitCheck)return;
            nextPortraitCheck=Time.unscaledTime+.5f;
            var avatar=AppearanceTarget();var identity=LocalPortraitIdentity;
            if(avatar==null||!NetworkActors.Alive(Player)||identity==null)return;
            var appearance=avatar.Appearance;
            if(portraitReady&&portraitAppearance.Equals(appearance)&&portraitName==identity.displayName)return;
            var previousPlayer=wardrobePlayer;wardrobePlayer=Player;
            try
            {
                PresentWardrobe(true);
                lobbyPortrait.texture=portraitTexture;portraitAppearance=appearance;
                portraitName=identity.displayName;portraitReady=true;
            }
            finally{wardrobePlayer=previousPlayer;}
        }
        bool UpdateWardrobe()

        {

            if(!wardrobePanel)return false;

            var location=Player.IsVR&&Player.ViewCamera?Player.ViewCamera.transform.position:Player.transform.position;

            float distance=Vector2.Distance(new Vector2(location.x,location.z),new Vector2(WardrobePosition.x,WardrobePosition.z));

            if(distance>1.3f)wardrobeArmed=true;

            if(!marina&&!wardrobeOpen&&wardrobeArmed&&distance<.75f)SetWardrobeOpen(true);

            if(!wardrobeOpen)return false;
            if(Player.IsVR&&(Player.MenuPressed||wardrobeCancel?.WasPressedThisFrame()==true)){SetWardrobeOpen(false);return true;}
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true){SetWardrobeOpen(false);return true;}

            if(!ReferenceEquals(Player,wardrobePlayer)||(!wardrobeFromPortrait&&distance>2)){SetWardrobeOpen(false);return false;}

            RefreshWardrobeLabels();
            if(!Player.IsVR&&wardrobeDesktop&&wardrobeChoiceReason&&Mouse.current!=null)
            {
                if(!wardrobeNoticeBound||wardrobeNoticeCategory!=wardrobeCategory||wardrobeNoticePage!=wardrobePage||!wardrobeNoticeAppearance.Equals(wardrobeAvatar.Appearance))
                {
                    SetWardrobeNotice(WardrobeNoticeDefault);wardrobeNoticeBound=true;wardrobeNoticeCategory=wardrobeCategory;wardrobeNoticePage=wardrobePage;wardrobeNoticeAppearance=wardrobeAvatar.Appearance;
                }
                var mouse=Mouse.current.position.ReadValue();
                for(int i=0;i<12;i++)
                {
                    int index=wardrobePage*12+i;if(index>=WardrobeCount)continue;
                    var item=wardrobeDesktop.Find("Options/Accessory "+i) as RectTransform;
                    if(item&&RectTransformUtility.RectangleContainsScreenPoint(item,mouse,null))
                    {
                        string itemName=AccessoryTitle(i);
                        if(itemName.StartsWith("ON  ",StringComparison.Ordinal))itemName=itemName.Substring(4);
                        else if(itemName.StartsWith("OFF  ",StringComparison.Ordinal))itemName=itemName.Substring(5);
                        itemName=itemName.Replace(" / COVERS LAYERS","");
                        SetWardrobeNotice("PROPOSED: "+wardrobeCategories[wardrobeCategory]+" / "+itemName+"\n"+ChoiceNotice(ProposedWardrobeEntry(index)));break;
                    }
                }
                // The named proposal remains pinned while the pointer crosses to the scrollable explanation.
            }

            if(!Player.IsVR&&wardrobeReasonViewport&&Mouse.current!=null&&RectTransformUtility.RectangleContainsScreenPoint(wardrobeReasonViewport,Mouse.current.position.ReadValue(),null))
            {
                float height=Mathf.Max(106,wardrobeChoiceReason.preferredHeight);wardrobeChoiceReason.rectTransform.sizeDelta=new Vector2(0,height);
                wardrobeReasonOffset=Mathf.Clamp(wardrobeReasonOffset+Mouse.current.scroll.y.ReadValue()*.2f,0,height-106);
                wardrobeChoiceReason.rectTransform.anchoredPosition=new Vector2(0,wardrobeReasonOffset);
            }
            if(!Player.IsVR)wardrobeOrbit.ReadDesktop(wardrobePreview && Mouse.current!=null && RectTransformUtility.RectangleContainsScreenPoint(wardrobePreview,Mouse.current.position.ReadValue(),null));

            PresentWardrobe();

            if(Player.IsVR)return false;

            if(Keyboard.current?.fKey.wasPressedThisFrame==true){SetWardrobeOpen(false);return true;}

            if(Mouse.current?.leftButton.wasPressedThisFrame==true)

            {

                var point=Mouse.current.position.ReadValue();foreach(var b in wardrobeButtons)if(RectTransformUtility.RectangleContainsScreenPoint(b.Item1,point,null)){b.Item2();break;}

            }

            return true;

        }

        void PresentLobbyPortrait()
        {
            if(!wardrobeCamera||!NetworkActors.Alive(wardrobePlayer))return;
            var visual=wardrobePlayer.gameObject.GetComponent<IModeActor>()?.Visual;if(!visual)return;
            var eye=wardrobePlayer.ViewCamera;var feet=wardrobePlayer.transform.position;
            float height=eye?Mathf.Clamp(eye.transform.position.y-feet.y,.7f,2.8f):1.6f;
            float yaw=eye?eye.transform.eulerAngles.y:wardrobePlayer.transform.eulerAngles.y;
            var rotation=Quaternion.Euler(0,yaw+180,0);
            var target=new Vector3(eye?eye.transform.position.x:feet.x,feet.y+height*.91f,eye?eye.transform.position.z:feet.z);
            var position=target-rotation*Vector3.forward*(height*.77f);
            var renderers=visual.GetComponentsInChildren<Renderer>(false);
            var layers=new int[renderers.Length];var hidden=new bool[renderers.Length];
            for(int i=0;i<renderers.Length;i++)
            {layers[i]=renderers[i].gameObject.layer;hidden[i]=renderers[i].forceRenderingOff;}
            var previousPosition=wardrobeCamera.transform.position;var previousRotation=wardrobeCamera.transform.rotation;var previousTarget=wardrobeCamera.targetTexture;
            try
            {
                for(int i=0;i<renderers.Length;i++)
                {renderers[i].gameObject.layer=31;renderers[i].forceRenderingOff=false;}
                wardrobeCamera.targetTexture=portraitTexture;
                wardrobeCamera.transform.SetPositionAndRotation(position,rotation);
                wardrobeCamera.Render();
            }
            finally
            {
                for(int i=renderers.Length-1;i>=0;i--)if(renderers[i])
                {renderers[i].gameObject.layer=layers[i];renderers[i].forceRenderingOff=hidden[i];}
                if(wardrobeCamera){wardrobeCamera.targetTexture=previousTarget;wardrobeCamera.transform.SetPositionAndRotation(previousPosition,previousRotation);}
            }
        }

        void PresentMarinaMirror()
        {
            if(!wardrobeCamera||!NetworkActors.Alive(wardrobePlayer))return;
            var visual=wardrobePlayer.gameObject.GetComponent<IModeActor>()?.Visual;if(!visual)return;
            mirrorRenderers.Clear();mirrorLayers.Clear();mirrorHidden.Clear();visual.GetComponentsInChildren(false,mirrorRenderers);
            var previousPosition=wardrobeCamera.transform.position;var previousRotation=wardrobeCamera.transform.rotation;
            var previousTarget=wardrobeCamera.targetTexture;float previousFar=wardrobeCamera.farClipPlane;
            foreach(var renderer in mirrorRenderers){mirrorLayers.Add(renderer.gameObject.layer);mirrorHidden.Add(renderer.forceRenderingOff);}
            try
            {
                foreach(var renderer in mirrorRenderers){renderer.gameObject.layer=31;renderer.forceRenderingOff=false;}
                mirrorGeometry??=new GeometryViewCache();
                bool fitted=mirrorGeometry.Try(mirrorRenderers,wardrobePlayer.gameObject.GetComponent<IAvatarAppearance>()?.Appearance??default,out var bounds);
                var feet=wardrobePlayer.transform.position;var eye=wardrobePlayer.ViewCamera;float height=eye?Mathf.Clamp(eye.transform.position.y-feet.y,.7f,2.8f):1.6f;
                var rotation=Quaternion.Euler(12,(eye?eye.transform.eulerAngles.y:wardrobePlayer.transform.eulerAngles.y)+180,0);
                var target=fitted?bounds.center:feet+Vector3.up*height*.58f;
                float distance=fitted?WardrobeFraming.RequiredDistance(bounds,target,rotation,wardrobeCamera.fieldOfView,1,wardrobeCamera.nearClipPlane,out _):height*1.6f;
                wardrobeCamera.targetTexture=wardrobeTexture;wardrobeCamera.farClipPlane=Mathf.Max(60,distance+10);
                wardrobeCamera.transform.SetPositionAndRotation(target-rotation*Vector3.forward*distance,rotation);wardrobeCamera.Render();
            }
            finally
            {
                for(int i=mirrorRenderers.Count-1;i>=0;i--)if(mirrorRenderers[i]){mirrorRenderers[i].gameObject.layer=mirrorLayers[i];mirrorRenderers[i].forceRenderingOff=mirrorHidden[i];}
                if(wardrobeCamera){wardrobeCamera.targetTexture=previousTarget;wardrobeCamera.farClipPlane=previousFar;wardrobeCamera.transform.SetPositionAndRotation(previousPosition,previousRotation);}
            }
        }

        void PresentWardrobe(bool portrait=false)

        {
            if(portrait){PresentLobbyPortrait();return;}

            if(!wardrobeCamera||!NetworkActors.Alive(wardrobePlayer))return;

            var eye=wardrobePlayer.ViewCamera;var feet=wardrobePlayer.transform.position;float height=eye?Mathf.Clamp(eye.transform.position.y-feet.y,.7f,2.8f):1.6f;

            var target=wardrobePivotReady?feet+wardrobePivotOffset:new Vector3(eye?eye.transform.position.x:feet.x,feet.y+height*.58f,eye?eye.transform.position.z:feet.z);

            var visual=wardrobePlayer.gameObject.GetComponent<IModeActor>()?.Visual;if(!visual)return;
            wardrobeRenderers.Clear();wardrobeLayers.Clear();visual.GetComponentsInChildren(false,wardrobeRenderers);
            wardrobeGeometry??=new GeometryViewCache();
            bool fitted=wardrobeGeometry.Try(wardrobeRenderers,wardrobeAvatar?.Appearance??default,out var bounds);
            if(fitted)
            {
                if(!wardrobePivotReady&&Time.frameCount>=wardrobeFramingReadyAt)
                {wardrobePivotOffset=bounds.center-feet;wardrobePivotReady=true;}
                target=wardrobePivotReady?feet+wardrobePivotOffset:bounds.center;
            }
            var position=wardrobeOrbit.Position(target,fitted||wardrobeFitDistance>0?1:height,out var rotation);
            if(fitted)
            {
                float aspect=(float)wardrobeTexture.width/wardrobeTexture.height;
                float required=WardrobeFraming.RequiredDistance(bounds,target,rotation,wardrobeCamera.fieldOfView,aspect,wardrobeCamera.nearClipPlane,out float minimum);
                if(required>wardrobeFitDistance||wardrobeFitDistance<=0)wardrobeFitDistance=required;
                else wardrobeFitDistance=Mathf.Lerp(wardrobeFitDistance,required,1-Mathf.Exp(-12*Time.unscaledDeltaTime));
                float zoom=Vector3.Distance(position,target)/2.15f;
                float distance=Mathf.Max(minimum,wardrobeFitDistance*zoom);
                position=target-rotation*Vector3.forward*distance;
                wardrobeCamera.farClipPlane=Mathf.Max(60,distance+Vector3.Distance(bounds.center,target)+bounds.extents.magnitude+1);
            }
            else if(wardrobeFitDistance>0)
            {
                float zoom=Vector3.Distance(position,target)/2.15f;position=target-rotation*Vector3.forward*wardrobeFitDistance*zoom;
            }
            wardrobeCamera.transform.SetPositionAndRotation(position,rotation);
            try
            {
                foreach(var renderer in wardrobeRenderers){wardrobeLayers.Add(renderer.gameObject.layer);renderer.gameObject.layer=31;}
                wardrobeCamera.Render();
            }
            finally
            {
                for(int i=wardrobeLayers.Count-1;i>=0;i--)if(wardrobeRenderers[i])wardrobeRenderers[i].gameObject.layer=wardrobeLayers[i];
            }

        }

        void ResetWardrobeView()
        {
            wardrobeGeometry?.Invalidate();wardrobePivotReady=false;wardrobeFitDistance=0;wardrobeFramingReadyAt=Time.frameCount+2;
            wardrobeOrbit.Reset((Player?.ViewCamera?Player.ViewCamera.transform.eulerAngles.y:0)+180,2.15f);
        }

        void AdjustWardrobeView(int control)

        {

            float step=Time.unscaledDeltaTime;

            if(control<2)wardrobeOrbit.Yaw+=(control==0?-1:1)*65*step;

            else if(control<4)wardrobeOrbit.Pitch+=(control==2?-1:1)*40*step;

            else wardrobeOrbit.Distance+=(control==4?-1:1)*step;

        }

        void OnDisable(){SetWardrobeOpen(false);wardrobeGeometry?.Dispose();wardrobeGeometry=null;mirrorGeometry?.Dispose();mirrorGeometry=null;}

        void DisposeWardrobe()

        {

            SetWardrobeOpen(false);wardrobeGeometry?.Dispose();mirrorGeometry?.Dispose();wardrobeGeometry=null;mirrorGeometry=null;
            if(portraitTexture){portraitTexture.Release();Destroy(portraitTexture);portraitTexture=null;}
            if(wardrobeCamera)Destroy(wardrobeCamera.gameObject);if(wardrobeTexture){wardrobeTexture.Release();Destroy(wardrobeTexture);}if(wardrobeMaterial)Destroy(wardrobeMaterial);

        }

    }

}

