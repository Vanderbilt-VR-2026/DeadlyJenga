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

        RenderTexture wardrobeTexture;

        Material wardrobeMaterial;

        IAvatarAppearance wardrobeAvatar;

        IPlayableCharacter wardrobePlayer;

        IAvatarShowcase wardrobeShowcase;

        bool wardrobeWasShowcasing;

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

            Label(wardrobePanel,"STEP INTO THE POD TO TRY ATTACHMENTS",420,23,1000,40);

            Add(wardrobePanel,"wardrobe-open",()=>wardrobeOpen?"SAVE & LEAVE PREVIEW":"CUSTOMIZE YOUR AVATAR",-605,()=>SetWardrobeOpen(!wardrobeOpen),()=>AppearanceTarget()!=null,0,950,60);

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
            index+=wardrobePage*12;if(index>=WardrobeCount)return "—";
            var avatar=wardrobeAvatar??AppearanceTarget();if(avatar==null)return "—";var v=avatar.Appearance;string name;bool chosen;
            switch(wardrobeCategory)
            {
                case 1:bool armor=index==System.Enum.GetValues(typeof(AvatarSkin)).Length;chosen=armor?v.poweredArmor:(int)v.skin==index;name=armor?"POWERED ARMOR":PrettyName(((AvatarSkin)index).ToString());break;
                case 2:chosen=(int)v.hair==index+1;name=PrettyName(((AvatarHair)(index+1)).ToString());break;
                case 3:chosen=(int)v.eyewear==index+1;name=PrettyName(((AvatarEyewear)(index+1)).ToString());break;
                case 4:chosen=index==14?(v.accessories&AvatarAccessory.PumpkinHelmet)!=0:(v.clothing&(AvatarClothing)(1u<<index))!=0;name=index==14?"PUMPKIN HELMET":PrettyName(((AvatarClothing)(1u<<index)).ToString());break;
                default:int bit=index<12?index:index+3;chosen=(v.accessories&(AvatarAccessory)(1u<<bit))!=0;name=PrettyName(((AvatarAccessory)(1u<<bit)).ToString());break;
            }
            return (chosen?"ON  ":"OFF  ")+name;
        }
        static string PrettyName(string name)=>System.Text.RegularExpressions.Regex.Replace(name=="HoneyBear"?"WinnieThePooh":name=="Sailor"?"Popeye":name=="RagDoll"?"RaggedyAnn":name=="Reindeer"?"Rudolph":name,"([a-z])([A-Z])","$1 $2").ToUpperInvariant();
        void SetWardrobeCategory(int category){wardrobeCategory=category;wardrobePage=0;wardrobeLastCategory=-1;RefreshWardrobeLabels();}
        void ChangeWardrobeCategory(int delta)=>SetWardrobeCategory((wardrobeCategory+delta+wardrobeCategories.Length)%wardrobeCategories.Length);
        void ChangeWardrobePage(int delta){wardrobePage=(wardrobePage+delta+WardrobePages)%WardrobePages;wardrobeLastCategory=-1;RefreshWardrobeLabels();}
        bool ChooseWardrobeEntry(int index)
        {
            if(!wardrobeOpen||wardrobeAvatar==null||index<0||index>=12)return false;
            index+=wardrobePage*12;if(index>=WardrobeCount)return false;var v=wardrobeAvatar.Appearance;
            switch(wardrobeCategory)
            {
                case 1:if(index==System.Enum.GetValues(typeof(AvatarSkin)).Length)v.poweredArmor=!v.poweredArmor;else v.skin=(AvatarSkin)index;break;
                case 2:v.hair=(int)v.hair==index+1?AvatarHair.None:(AvatarHair)(index+1);break;
                case 3:v.eyewear=(int)v.eyewear==index+1?AvatarEyewear.None:(AvatarEyewear)(index+1);break;
                case 4:if(index==14)v.accessories^=AvatarAccessory.PumpkinHelmet;else v.clothing^=(AvatarClothing)(1u<<index);break;
                default:int bit=index<12?index:index+3;v.accessories^=(AvatarAccessory)(1u<<bit);break;
            }
            wardrobeAvatar.ApplyAppearance(v);RefreshWardrobeLabels();return true;
        }
        public bool ToggleAccessory(int index)

        {

            if(!wardrobeOpen||wardrobeAvatar==null||index<0||index>=12)return false;

            var value=wardrobeAvatar.Appearance;value.accessories^=(AvatarAccessory)(1u<<index);wardrobeAvatar.ApplyAppearance(value);RefreshWardrobeLabels();return true;

        }

        void ClearAccessories(){if(wardrobeAvatar==null)return;wardrobeAvatar.ApplyAppearance(default);RefreshWardrobeLabels();}

        public void SetWardrobeOpen(bool open)

        {

            if(open==wardrobeOpen)return;

            if(open)

            {

                wardrobeAvatar=AppearanceTarget();if(wardrobeAvatar==null||!NetworkActors.Alive(Player))return;

                wardrobePlayer=Player;wardrobePaused=Player.InputSuspended;wardrobeCursor=Cursor.lockState;wardrobeCursorVisible=Cursor.visible;ResetWardrobeView();

                wardrobeOpen=true;wardrobeArmed=false;Player.InputSuspended=true;

                wardrobeShowcase=Player.gameObject.GetComponent<IAvatarShowcase>();

                if(wardrobeShowcase!=null){wardrobeWasShowcasing=wardrobeShowcase.Showcasing;wardrobeShowcase.Showcasing=true;}

                if(!Player.IsVR){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;BuildDesktopWardrobe();}

                PresentWardrobe();

            }

            else

            {

                wardrobeOpen=false;

                if(wardrobeShowcase!=null&&NetworkActors.Alive(wardrobePlayer))wardrobeShowcase.Showcasing=wardrobeWasShowcasing;

                wardrobeShowcase=null;

                if(NetworkActors.Alive(wardrobePlayer))wardrobePlayer.InputSuspended=wardrobePaused;

                if(wardrobeDesktop){wardrobeDesktop.gameObject.SetActive(false);Destroy(wardrobeDesktop.gameObject);wardrobeDesktop=null;}

                wardrobeButtons.Clear();Cursor.lockState=wardrobeCursor;Cursor.visible=wardrobeCursorVisible;wardrobeAvatar=null;wardrobePlayer=null;

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

            Label(rect,"YOUR WARDROBE",340,42,570,65);Label(rect,"Skins, armor and accessories.\nChanges save automatically.",278,18,570,40);

            for(int i=0;i<12;i++){int item=i;DesktopButton(rect,AccessoryTitle(i),198-(i/2)*65,(i%2==0?-1:1)*145,275,56,()=>ChooseWardrobeEntry(item),"Accessory "+i);}

            DesktopButton(rect,"< PREVIOUS PAGE",-180,-145,275,40,()=>ChangeWardrobePage(-1),"Previous category");DesktopButton(rect,"NEXT PAGE >",-180,145,275,40,()=>ChangeWardrobePage(1),"Next category");
            for(int category=0;category<wardrobeCategories.Length;category++){int selected=category;DesktopButton(rect,wardrobeCategories[category],238,-232+category*116,112,38,()=>SetWardrobeCategory(selected),"Category "+category);}

            DesktopButton(rect,"CLEAR",-240,-145,275,56,ClearAccessories,"Clear");DesktopButton(rect,"RESET VIEW",-240,145,275,56,ResetWardrobeView,"Reset view");

            Label(rect,"Drag preview / arrows: orbit · Scroll / +/-: zoom",-375,18,575,34);

            DesktopButton(rect,"DONE  ·  F TO CLOSE",-315,0,565,70,()=>SetWardrobeOpen(false),"Done");

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

        bool UpdateWardrobe()

        {

            if(!wardrobePanel)return false;

            var location=Player.IsVR&&Player.ViewCamera?Player.ViewCamera.transform.position:Player.transform.position;

            float distance=Vector2.Distance(new Vector2(location.x,location.z),new Vector2(WardrobePosition.x,WardrobePosition.z));

            if(distance>1.3f)wardrobeArmed=true;

            if(!wardrobeOpen&&wardrobeArmed&&distance<.75f)SetWardrobeOpen(true);

            if(!wardrobeOpen)return false;

            if(!ReferenceEquals(Player,wardrobePlayer)||distance>2){SetWardrobeOpen(false);return false;}

            RefreshWardrobeLabels();

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

        void PresentWardrobe()

        {

            if(!wardrobeCamera||!NetworkActors.Alive(wardrobePlayer))return;

            var eye=wardrobePlayer.ViewCamera;var feet=wardrobePlayer.transform.position;float height=eye?Mathf.Clamp(eye.transform.position.y-feet.y,.7f,2.8f):1.6f;

            var target=new Vector3(eye?eye.transform.position.x:feet.x,feet.y+height*.58f,eye?eye.transform.position.z:feet.z);

            var position=wardrobeOrbit.Position(target,height,out var rotation);

            wardrobeCamera.transform.SetPositionAndRotation(position,rotation);
            var visual=wardrobePlayer.gameObject.GetComponent<IModeActor>()?.Visual;if(!visual)return;
            wardrobeRenderers.Clear();wardrobeLayers.Clear();visual.GetComponentsInChildren(false,wardrobeRenderers);
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

        void ResetWardrobeView()=>wardrobeOrbit.Reset((Player?.ViewCamera?Player.ViewCamera.transform.eulerAngles.y:0)+180,2.15f);

        void AdjustWardrobeView(int control)

        {

            float step=Time.unscaledDeltaTime;

            if(control<2)wardrobeOrbit.Yaw+=(control==0?-1:1)*65*step;

            else if(control<4)wardrobeOrbit.Pitch+=(control==2?-1:1)*40*step;

            else wardrobeOrbit.Distance+=(control==4?-1:1)*step;

        }

        void DisposeWardrobe()

        {

            SetWardrobeOpen(false);if(wardrobeCamera)Destroy(wardrobeCamera.gameObject);if(wardrobeTexture){wardrobeTexture.Release();Destroy(wardrobeTexture);}if(wardrobeMaterial)Destroy(wardrobeMaterial);

        }

    }

}

