using System;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;



namespace DeadlyJenga.CastlePrototype

{

    public sealed partial class IslandLobbyView

    {

        const int WardrobePageSize=12;
        static readonly string[] wardrobeCategories={"Skin","Hair","Headgear","Eyewear","Outfit","Back","Extras"};
        enum WardrobeSlot { Skin, Hair, Headgear, Eyewear, Outfit, Back, Extras }
        readonly struct WardrobeItem
        {
            public readonly string name;
            public readonly AvatarAccessory accessory;
            public readonly AvatarClothing clothing;
            public readonly int style;
            public readonly bool armor;
            public WardrobeItem(string name,AvatarAccessory accessory=0,AvatarClothing clothing=0,int style=0,bool armor=false)
            {this.name=name;this.accessory=accessory;this.clothing=clothing;this.style=style;this.armor=armor;}
        }
        static readonly WardrobeItem[][] wardrobeItems=BuildWardrobeItems();
        int wardrobeCategory,wardrobePage;
        int WardrobePages=>(wardrobeItems[wardrobeCategory].Length+WardrobePageSize-1)/WardrobePageSize;

        static WardrobeItem[][] BuildWardrobeItems()
        {
            WardrobeItem Accessory(AvatarAccessory item)=>new WardrobeItem(PrettyName(item.ToString()),accessory:item);
            WardrobeItem Clothing(AvatarClothing item)=>new WardrobeItem(PrettyName(item.ToString()),clothing:item);
            return new[]
            {
                Enum.GetValues(typeof(AvatarSkin)).Cast<AvatarSkin>().Select(item=>new WardrobeItem(PrettyName(item.ToString()),style:(int)item)).ToArray(),
                Enum.GetValues(typeof(AvatarHair)).Cast<AvatarHair>().Where(item=>item!=AvatarHair.None)
                    .Select(item=>new WardrobeItem(PrettyName(item.ToString()),style:(int)item)).Append(Accessory(AvatarAccessory.Mohawk)).ToArray(),
                new[]{AvatarAccessory.TopHat,AvatarAccessory.Crown,AvatarAccessory.Beanie,AvatarAccessory.CowboyHat,AvatarAccessory.PirateHat,
                    AvatarAccessory.ChefHat,AvatarAccessory.VikingHelmet,AvatarAccessory.PartyHat,AvatarAccessory.PropellerCap,AvatarAccessory.FlowerCrown,
                    AvatarAccessory.PumpkinHelmet,AvatarAccessory.SkeletonMask,AvatarAccessory.PantherHead,AvatarAccessory.RabbitEars,AvatarAccessory.CatEars,
                    AvatarAccessory.AntAntennae,AvatarAccessory.UnicornHorn,AvatarAccessory.DevilHorns,AvatarAccessory.Broccoli,AvatarAccessory.Halo,
                    AvatarAccessory.Headphones,AvatarAccessory.RobotAntenna}.Select(Accessory)
                    .Concat(new[]{Clothing(AvatarClothing.HockeyHelmet),Clothing(AvatarClothing.FootballHelmet)}).ToArray(),
                // These are the distinct models. The remaining names reuse Round or Visor geometry.
                new[]{AvatarEyewear.Round,AvatarEyewear.Square,AvatarEyewear.Aviator,AvatarEyewear.CatEye,AvatarEyewear.Visor,
                    AvatarEyewear.Monocle,AvatarEyewear.Star,AvatarEyewear.Heart,AvatarEyewear.CyberEye,AvatarEyewear.Shutter}
                    .Select(item=>new WardrobeItem(PrettyName(item.ToString()),style:(int)item)).Append(Accessory(AvatarAccessory.Glasses)).ToArray(),
                new[]{AvatarClothing.MonkRobe,AvatarClothing.Diaper,AvatarClothing.FootballPads,AvatarClothing.GrassSkirt,AvatarClothing.Tuxedo,
                    AvatarClothing.Hoodie,AvatarClothing.BomberJacket,AvatarClothing.SailorShirt,AvatarClothing.Overalls,AvatarClothing.PufferVest}
                    .Select(Clothing).Append(new WardrobeItem("Powered Armor",armor:true)).ToArray(),
                new[]{AvatarAccessory.Cape,AvatarAccessory.ButterflyWings,AvatarAccessory.AngelWings,AvatarAccessory.DragonWings}
                    .Select(Accessory).Append(Clothing(AvatarClothing.RoyalMantle)).ToArray(),
                new[]{Accessory(AvatarAccessory.LongNose),Accessory(AvatarAccessory.StrongJaw),Clothing(AvatarClothing.BowTie)}
            };
        }

        Vector3 wardrobeCenter=new Vector3(0,.65f,-5.3f);
        const float WardrobeEnterRadius=1.25f, WardrobeExitRadius=1.65f;
        bool wardrobeVisited;

        readonly System.Collections.Generic.List<Tuple<RectTransform,Action>> wardrobeButtons=new System.Collections.Generic.List<Tuple<RectTransform,Action>>();

        RectTransform wardrobePanel,wardrobeDesktop;

        Camera wardrobeCamera;
        readonly System.Collections.Generic.List<Renderer> wardrobeRenderers=new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<int> wardrobeLayers=new System.Collections.Generic.List<int>();

        RenderTexture wardrobeTexture;

        Material wardrobeMaterial;

        IAvatarAppearance wardrobeAvatar;
        RectTransform fallbackWardrobeMirror;
        int wardrobeDragDevice;
        Vector2 wardrobeDragPoint;
        bool desktopWardrobeDrag;
        RectTransform WardrobeMirror=>marinaMirror?marinaMirror:fallbackWardrobeMirror;

        IPlayableCharacter wardrobePlayer;

        IAvatarShowcase wardrobeShowcase;

        bool wardrobeWasShowcasing;
        IAvatarInspectionPose wardrobePose;
        bool wardrobeWasNeutral;
        bool wardrobePivotReady;
        int wardrobeFramingReadyAt;
        Vector3 wardrobePivotOffset;
        float wardrobeFitDistance;

        bool wardrobeOpen,wardrobePaused;

        CursorLockMode wardrobeCursor;

        bool wardrobeCursorVisible;

        readonly CandyOrbitView wardrobeOrbit = new CandyOrbitView();

        RectTransform wardrobePreview;

        AvatarAppearance wardrobeLastAppearance;int wardrobeLastCategory=-1,wardrobeLastPage=-1;

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

            wardrobePanel=Terminal("Wardrobe",WardrobePosition+new Vector3(.7f,1.65f,1.35f),25,new Vector2(1120,910),CandyHudVisuals.Mint);
            for(int category=0;category<wardrobeCategories.Length;category++)
            {
                int selected=category;
                var tab=Add(wardrobePanel,"wardrobe-category-"+category,()=>wardrobeCategories[selected],260,
                    ()=>SetWardrobeCategory(selected),()=>wardrobeOpen,(category-3)*150,142,54);
                tab.selected=()=>wardrobeCategory==selected;
                CandyStyle(tab);
            }
            for(int i=0;i<WardrobePageSize;i++)
            {
                int item=i;
                var choice=Add(wardrobePanel,"wardrobe-"+i,()=>ItemName(item),162-(i/2)*66,
                    ()=>ChooseWardrobeEntry(item),()=>wardrobeOpen,i%2==0?-263:263,506,56,()=>HasWardrobeItem(item));
                choice.selected=()=>HasWardrobeItem(item)&&IsEquipped(wardrobeAvatar.Appearance,(WardrobeSlot)wardrobeCategory,ItemAt(item));
                CandyStyle(choice);
            }
            CandyStyle(Add(wardrobePanel,"wardrobe-clear",()=>"Previous",-240,()=>ChangeWardrobePage(-1),()=>wardrobePage>0,-263,250,48,()=>WardrobePages>1));
            CandyStyle(Add(wardrobePanel,"wardrobe-reset",()=>"Next",-240,()=>ChangeWardrobePage(1),()=>wardrobePage+1<WardrobePages,263,250,48,()=>WardrobePages>1));
            wardrobePageLabel=Label(wardrobePanel,"",-240,23,240,48);
            CandyStyle(Add(wardrobePanel,"wardrobe-clear-look",()=>"Reset outfit",-389,ClearAccessories,()=>wardrobeOpen,-263,506,62));
            var done=Add(wardrobePanel,"wardrobe-open",()=>"Done",-389,()=>SetWardrobeOpen(false),()=>wardrobeOpen,263,506,62);
            CandyStyle(done);CandyPrimary(done);

            wardrobeTexture=new RenderTexture(768,768,24){name="Wardrobe third person preview"};
            var mirror=new GameObject("Wardrobe mirror preview",typeof(RectTransform),typeof(Canvas),typeof(RawImage));
            mirror.transform.SetParent(transform,false);
            fallbackWardrobeMirror=(RectTransform)mirror.transform;
            fallbackWardrobeMirror.SetPositionAndRotation(WardrobePosition+new Vector3(-1.55f,1.65f,1.2f),Quaternion.Euler(0,-52,0));
            fallbackWardrobeMirror.localScale=Vector3.one*.0022f;
            fallbackWardrobeMirror.sizeDelta=new Vector2(670,670);
            mirror.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var raw=mirror.GetComponent<RawImage>();raw.texture=wardrobeTexture;raw.raycastTarget=false;
            BuildMirrorRotateIcon(fallbackWardrobeMirror,-275,95);
            mirror.SetActive(false);

            var cameraObject=new GameObject("Visual - wardrobe camera");cameraObject.transform.SetParent(transform,false);wardrobeCamera=cameraObject.AddComponent<Camera>();wardrobeCamera.enabled=false;wardrobeCamera.targetTexture=wardrobeTexture;wardrobeCamera.fieldOfView=44;wardrobeCamera.nearClipPlane=.05f;wardrobeCamera.farClipPlane=60;wardrobeCamera.cullingMask=1<<31;wardrobeCamera.clearFlags=CameraClearFlags.SolidColor;wardrobeCamera.backgroundColor=new Color(.07f,.045f,.12f);wardrobeCamera.GetUniversalAdditionalCameraData().allowXRRendering=false;

            foreach(var t in wardrobePanel.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            wardrobePanel.gameObject.SetActive(false);
            var rear=transform.Cast<Transform>().FirstOrDefault(child=>child.name=="Wardrobe / readable back");
            if(rear)rear.gameObject.SetActive(false);

        }

        IAvatarAppearance AppearanceTarget()=>Player?.gameObject.GetComponent<IAvatarAppearance>()??Player?.gameObject.GetComponentInChildren<IAvatarAppearance>();

        Text wardrobePageLabel;
        bool HasWardrobeItem(int slot)=>wardrobeAvatar!=null&&wardrobePage*WardrobePageSize+slot<wardrobeItems[wardrobeCategory].Length;
        WardrobeItem ItemAt(int slot)=>wardrobeItems[wardrobeCategory][wardrobePage*WardrobePageSize+slot];
        string ItemName(int slot)=>HasWardrobeItem(slot)?ItemAt(slot).name:"";

        static string PrettyName(string name)=>System.Text.RegularExpressions.Regex.Replace(
            name=="HoneyBear"?"WinnieThePooh":name=="Sailor"?"Popeye":name=="RagDoll"?"RaggedyAnn":name=="Reindeer"?"Rudolph":name,
            "([a-z])([A-Z])","$1 $2");

        static bool IsEquipped(AvatarAppearance value,WardrobeSlot slot,WardrobeItem item)
        {
            if(item.accessory!=0)return (value.accessories&item.accessory)!=0;
            if(item.clothing!=0)return (value.clothing&item.clothing)!=0;
            if(item.armor)return value.poweredArmor;
            return slot==WardrobeSlot.Skin?(int)value.skin==item.style:
                slot==WardrobeSlot.Hair?(int)value.hair==item.style:(int)VisibleEyewear(value.eyewear)==item.style;
        }
        static AvatarEyewear VisibleEyewear(AvatarEyewear eyewear)=>eyewear switch
        {
            AvatarEyewear.SkiGoggles or AvatarEyewear.VRVisor=>AvatarEyewear.Visor,
            AvatarEyewear.SafetyGoggles or AvatarEyewear.RoundSunglasses or AvatarEyewear.HalfMoon=>AvatarEyewear.Round,
            _=>eyewear
        };
        static AvatarAppearance SetItem(AvatarAppearance value,WardrobeSlot slot,WardrobeItem item,bool equip)
        {
            if(slot==WardrobeSlot.Skin){value.skin=(AvatarSkin)item.style;return value;}
            if(slot==WardrobeSlot.Hair)value.hair=AvatarHair.None;
            if(slot==WardrobeSlot.Eyewear)value.eyewear=AvatarEyewear.None;
            if(slot==WardrobeSlot.Outfit)value.poweredArmor=false;
            foreach(var other in wardrobeItems[(int)slot])
            {
                if(slot==WardrobeSlot.Extras&&other.accessory!=item.accessory)continue;
                value.accessories&=~other.accessory;
                value.clothing&=~other.clothing;
            }
            if(!equip)return value;
            value.accessories|=item.accessory;
            value.clothing|=item.clothing;
            if(item.armor)value.poweredArmor=true;
            if(slot==WardrobeSlot.Hair&&item.accessory==0)value.hair=(AvatarHair)item.style;
            if(slot==WardrobeSlot.Eyewear&&item.accessory==0)value.eyewear=(AvatarEyewear)item.style;
            return value;
        }
        void NormalizeWardrobeSlots()
        {
            var original=wardrobeAvatar.Appearance;
            var visible=(wardrobeAvatar as IAvatarWardrobePreview)?.EvaluateAppearance(original).effective??original;
            var value=original;
            for(int i=(int)WardrobeSlot.Hair;i<=(int)WardrobeSlot.Back;i++)
            {
                var slot=(WardrobeSlot)i;
                var item=wardrobeItems[i].FirstOrDefault(option=>IsEquipped(original,slot,option)&&IsEquipped(visible,slot,option));
                if(item.name==null)item=wardrobeItems[i].FirstOrDefault(option=>IsEquipped(original,slot,option));
                if(item.name!=null)value=SetItem(value,slot,item,true);
            }
            if(!value.Equals(original))wardrobeAvatar.ApplyAppearance(value);
        }
        void SetWardrobeCategory(int category){wardrobeCategory=category;wardrobePage=0;RefreshWardrobeLabels();}
        void ChangeWardrobePage(int delta){wardrobePage=Mathf.Clamp(wardrobePage+delta,0,WardrobePages-1);RefreshWardrobeLabels();}
        bool ChooseWardrobeEntry(int index)
        {
            if(!wardrobeOpen||index<0||index>=WardrobePageSize||!HasWardrobeItem(index))return false;
            EquipWardrobeItem((WardrobeSlot)wardrobeCategory,ItemAt(index));
            return true;
        }
        void EquipWardrobeItem(WardrobeSlot slot,WardrobeItem item)
        {
            var value=wardrobeAvatar.Appearance;
            wardrobeAvatar.ApplyAppearance(SetItem(value,slot,item,!IsEquipped(value,slot,item)));
            RefreshWardrobeLabels();
        }
        public bool ToggleAccessory(int index)
        {
            if(!wardrobeOpen||wardrobeAvatar==null||index<0||index>=12)return false;
            var accessory=(AvatarAccessory)(1u<<index);
            for(int i=0;i<wardrobeItems.Length;i++)
            {
                foreach(var item in wardrobeItems[i])
                {
                    if(item.accessory!=accessory)continue;
                    EquipWardrobeItem((WardrobeSlot)i,item);
                    return true;
                }
            }
            return false;
        }

        void ClearAccessories(){if(wardrobeAvatar==null)return;wardrobeAvatar.ApplyAppearance(default);RefreshWardrobeLabels();}

        float DistanceToWardrobe()
        {
            var location=Player.IsVR&&Player.ViewCamera?Player.ViewCamera.transform.position:Player.transform.position;
            var offset=location-WardrobePosition;
            return new Vector2(offset.x,offset.z).magnitude;
        }
        public void SetWardrobeOpen(bool open)

        {

            if(open==wardrobeOpen)return;

            if(open)

            {

                if(!NetworkActors.Alive(Player)||DistanceToWardrobe()>WardrobeEnterRadius)return;
                wardrobeAvatar=AppearanceTarget();if(wardrobeAvatar==null)return;

                wardrobePlayer=Player;wardrobePaused=Player.InputSuspended;wardrobeCursor=Cursor.lockState;wardrobeCursorVisible=Cursor.visible;ResetWardrobeView();

                wardrobeOpen=true;wardrobeVisited=true;NormalizeWardrobeSlots();Player.InputSuspended=true;

                wardrobeShowcase=Player.gameObject.GetComponent<IAvatarShowcase>();

                if(wardrobeShowcase!=null){wardrobeWasShowcasing=wardrobeShowcase.Showcasing;wardrobeShowcase.Showcasing=true;}
                wardrobePose=Player.gameObject.GetComponent<IAvatarInspectionPose>();
                if(wardrobePose!=null){wardrobeWasNeutral=wardrobePose.NeutralInspection;wardrobePose.NeutralInspection=true;}

                if(!Player.IsVR){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;BuildDesktopWardrobe();}

                wardrobePanel.gameObject.SetActive(Player.IsVR);
                fallbackWardrobeMirror.gameObject.SetActive(Player.IsVR&&!marinaMirror);
                RefreshWardrobeLabels();
                PresentWardrobe();

            }

            else

            {

                wardrobeOpen=false;portraitReady=false;wardrobeDragDevice=0;desktopWardrobeDrag=false;
                if(fallbackWardrobeMirror)fallbackWardrobeMirror.gameObject.SetActive(false);
                if(wardrobePanel)wardrobePanel.gameObject.SetActive(false);

                if(wardrobeShowcase!=null&&NetworkActors.Alive(wardrobePlayer))wardrobeShowcase.Showcasing=wardrobeWasShowcasing;

                wardrobeShowcase=null;
                if(wardrobePose!=null&&NetworkActors.Alive(wardrobePlayer))wardrobePose.NeutralInspection=wardrobeWasNeutral;
                wardrobePose=null;wardrobePivotReady=false;wardrobeFitDistance=0;

                if(NetworkActors.Alive(wardrobePlayer))wardrobePlayer.InputSuspended=wardrobePaused;

                if(wardrobeDesktop){wardrobeDesktop.gameObject.SetActive(false);Destroy(wardrobeDesktop.gameObject);wardrobeDesktop=null;}

                wardrobeButtons.Clear();Cursor.lockState=wardrobeCursor;Cursor.visible=wardrobeCursorVisible;wardrobeAvatar=null;wardrobePlayer=null;

            }

        }

        void BuildDesktopWardrobe()
        {
            wardrobeLastCategory=-1;
            var go=new GameObject("Visual - wardrobe desktop",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(Image));
            go.transform.SetParent(transform,false);
            wardrobeDesktop=go.GetComponent<RectTransform>();
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,800);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            go.GetComponent<Image>().color=CandyHudVisuals.Ink;

            var preview=new GameObject("Wardrobe mirror",typeof(RectTransform),typeof(RawImage));
            preview.transform.SetParent(wardrobeDesktop,false);
            var raw=preview.GetComponent<RawImage>();raw.texture=wardrobeTexture;raw.raycastTarget=false;
            wardrobePreview=raw.rectTransform;wardrobePreview.sizeDelta=new Vector2(530,530);wardrobePreview.anchoredPosition=new Vector2(-315,15);
            BuildMirrorRotateIcon(wardrobePreview,-221,70);

            var panel=new GameObject("Options",typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(wardrobeDesktop,false);panel.anchoredPosition=new Vector2(305,0);panel.sizeDelta=new Vector2(590,760);
            Label(panel,"Wardrobe",312,44,570,64).fontStyle=FontStyle.Bold;
            for(int i=0;i<wardrobeCategories.Length;i++)
            {
                int category=i;
                DesktopButton(panel,wardrobeCategories[i],242,(i-3)*82,79,46,()=>SetWardrobeCategory(category),"Category "+i);
            }
            for(int i=0;i<WardrobePageSize;i++)
            {
                int item=i;
                DesktopButton(panel,ItemName(i),173-i/2*59,i%2==0?-145:145,277,51,()=>ChooseWardrobeEntry(item),"Accessory "+i);
            }
            DesktopButton(panel,"Previous",-192,-145,180,46,()=>ChangeWardrobePage(-1),"Previous category");
            DesktopButton(panel,"Next",-192,145,180,46,()=>ChangeWardrobePage(1),"Next category");
            desktopPageLabel=Label(panel,"",-192,21,110,46);
            DesktopButton(panel,"Reset outfit",-327,-145,277,54,ClearAccessories,"Clear");
            var done=DesktopButton(panel,"Done",-327,145,277,54,()=>SetWardrobeOpen(false),"Done");
            done.GetComponent<Image>().color=CandyHudVisuals.Mint;
            done.GetComponentInChildren<Text>().color=CandyHudVisuals.Ink;
        }

        void BuildMirrorRotateIcon(RectTransform mirror,float y,float size)
        {
            var badge=CandyPanel(mirror,"Mirror rotation",0,y-size*.1f,size,size*.65f,CandyHudVisuals.Ink);
            CandyIcon(badge,LobbyTerminalGlyph.Symbol.Rotate,0,0,size*.68f,CandyHudVisuals.Mint);
        }

        RectTransform DesktopButton(RectTransform parent,string title,float y,float x,float width,float height,Action action,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(x,y);
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=CandyHudVisuals.Card;
            Label(rect,title,0,23,width-8,height-6);
            wardrobeButtons.Add(Tuple.Create(rect,action));
            return rect;
        }

        Text desktopPageLabel;
        void RefreshWardrobeLabels()
        {
            if(wardrobeAvatar==null)return;
            string page=WardrobePages>1?$"{wardrobePage+1} / {WardrobePages}":"";
            wardrobePageLabel.text=page;
            if(!wardrobeDesktop)return;
            var current=wardrobeAvatar.Appearance;
            if(wardrobeLastCategory==wardrobeCategory&&wardrobeLastPage==wardrobePage&&wardrobeLastAppearance.Equals(current))return;
            wardrobeLastCategory=wardrobeCategory;wardrobeLastPage=wardrobePage;wardrobeLastAppearance=current;
            var options=wardrobeDesktop.Find("Options");
            desktopPageLabel.text=page;
            options.Find("Previous category").gameObject.SetActive(wardrobePage>0);
            options.Find("Next category").gameObject.SetActive(wardrobePage+1<WardrobePages);
            for(int i=0;i<wardrobeCategories.Length;i++)Paint(options.Find("Category "+i),wardrobeCategory==i);
            for(int i=0;i<WardrobePageSize;i++)
            {
                var button=options.Find("Accessory "+i);
                bool shown=HasWardrobeItem(i);
                button.gameObject.SetActive(shown);
                if(!shown)continue;
                button.GetComponentInChildren<Text>().text=ItemName(i);
                Paint(button,IsEquipped(current,(WardrobeSlot)wardrobeCategory,ItemAt(i)));
            }

            static void Paint(Transform button,bool selected)
            {
                button.GetComponent<Image>().color=selected?CandyHudVisuals.Mint:CandyHudVisuals.Card;
                button.GetComponentInChildren<Text>().color=selected?CandyHudVisuals.Ink:Color.white;
            }
        }

        RawImage lobbyPortrait;
        Text lobbyPortraitName;
        bool portraitReady;
        AvatarAppearance portraitAppearance;
        string portraitName;
        float nextPortraitCheck;
        LobbyParticipant LocalPortraitIdentity=>Details?.Participants.FirstOrDefault(p=>p.id==Details.LocalParticipantId);
        void BuildLobbyPortrait()
        {
            lobbyPortraitName=Label(hostStation,"My avatar",294,24,190,48);
            lobbyPortraitName.rectTransform.anchoredPosition=new Vector2(-312,294);lobbyPortraitName.alignment=TextAnchor.MiddleLeft;lobbyPortraitName.resizeTextForBestFit=true;lobbyPortraitName.resizeTextMinSize=16;lobbyPortraitName.resizeTextMaxSize=24;
            var image=new GameObject("Your portrait",typeof(RectTransform),typeof(RawImage));
            image.transform.SetParent(hostStation,false);
            lobbyPortrait=image.GetComponent<RawImage>();
            lobbyPortrait.raycastTarget=false;
            lobbyPortrait.rectTransform.sizeDelta=new Vector2(60,60);
            lobbyPortrait.rectTransform.anchoredPosition=new Vector2(-452,294);
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
                lobbyPortrait.texture=wardrobeTexture;portraitAppearance=appearance;
                portraitName=identity.displayName;lobbyPortraitName.text=portraitName;portraitReady=true;
            }
            finally{wardrobePlayer=previousPlayer;}
        }
        bool UpdateWardrobe()

        {

            if(!wardrobePanel)return false;

            float distance=DistanceToWardrobe();
            if(distance>WardrobeExitRadius)wardrobeVisited=false;
            if(!wardrobeOpen&&!wardrobeVisited&&distance<=WardrobeEnterRadius&&!lobby.CodeFocused)
                SetWardrobeOpen(true);
            if(!wardrobeOpen)return false;
            if(Player.IsVR&&(Player.MenuPressed||wardrobeCancel?.WasPressedThisFrame()==true)){SetWardrobeOpen(false);return true;}
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true){SetWardrobeOpen(false);return true;}

            if(!ReferenceEquals(Player,wardrobePlayer)||distance>WardrobeExitRadius){SetWardrobeOpen(false);return false;}

            RefreshWardrobeLabels();
            if(Player.IsVR){PresentWardrobe();return false;}
            if(Keyboard.current?.fKey.wasPressedThisFrame==true){SetWardrobeOpen(false);return true;}
            var mouse=Mouse.current;
            if(mouse!=null)
            {
                var point=mouse.position.ReadValue();
                if(mouse.leftButton.wasPressedThisFrame)
                {
                    desktopWardrobeDrag=wardrobePreview&&RectTransformUtility.RectangleContainsScreenPoint(wardrobePreview,point,null);
                    if(!desktopWardrobeDrag)
                        foreach(var button in wardrobeButtons)
                            if(button.Item1.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(button.Item1,point,null))
                            {button.Item2();break;}
                }
                if(!mouse.leftButton.isPressed)desktopWardrobeDrag=false;
                if(desktopWardrobeDrag)
                {
                    var delta=mouse.delta.ReadValue();
                    wardrobeOrbit.Yaw+=delta.x*.25f;
                    wardrobeOrbit.Pitch-=delta.y*.2f;
                }
            }
            if(wardrobeOpen)PresentWardrobe();
            return true;
        }

        float WardrobePanelHit(Ray ray,float range)
        {
            float controls=MenuPointer.PanelHit(ray,wardrobePanel,range);
            float mirror=MenuPointer.PanelHit(ray,WardrobeMirror,range);
            return mirror>0&&(controls<=0||mirror<controls)?mirror:controls;
        }
        bool BeginWardrobeDrag(MenuPointer.Sample input)
        {
            if(!wardrobeOpen||!input.Pressed||input.Device==0)return false;
            var mirror=WardrobeMirror;
            float hit=MenuPointer.PanelHit(input.Ray,mirror);
            if(hit<=0)return false;
            float controls=MenuPointer.PanelHit(input.Ray,wardrobePanel);
            if(controls>0&&controls<hit)return false;
            wardrobeDragDevice=input.Device;
            wardrobeDragPoint=MirrorDragPoint(mirror,input.Ray.GetPoint(hit));
            return true;
        }
        bool UpdateWardrobeDrag(Camera eye)
        {
            if(wardrobeDragDevice==0)return false;
            if(!wardrobeOpen||!MenuPointer.TryReadDevice(eye,wardrobeDragDevice,out var input)||!input.Held)
            {wardrobeDragDevice=0;return false;}
            var mirror=WardrobeMirror;
            if(!mirror||!new Plane(mirror.forward,mirror.position).Raycast(input.Ray,out float hit)||hit>MenuPointer.PanelRange)return true;
            var point=MirrorDragPoint(mirror,input.Ray.GetPoint(hit));
            var delta=point-wardrobeDragPoint;
            wardrobeDragPoint=point;
            wardrobeOrbit.Yaw+=delta.x*210;
            wardrobeOrbit.Pitch-=delta.y*100;
            return true;
        }
        static Vector2 MirrorDragPoint(RectTransform mirror,Vector3 point)
        {
            var local=mirror.InverseTransformPoint(point);
            return new Vector2(Mathf.Clamp(local.x/mirror.rect.width,-1,1),Mathf.Clamp(local.y/mirror.rect.height,-1,1));
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
            var previousPosition=wardrobeCamera.transform.position;var previousRotation=wardrobeCamera.transform.rotation;
            try
            {
                for(int i=0;i<renderers.Length;i++)
                {renderers[i].gameObject.layer=31;renderers[i].forceRenderingOff=false;}
                wardrobeCamera.transform.SetPositionAndRotation(position,rotation);
                wardrobeCamera.Render();
            }
            finally
            {
                for(int i=renderers.Length-1;i>=0;i--)if(renderers[i])
                {renderers[i].gameObject.layer=layers[i];renderers[i].forceRenderingOff=hidden[i];}
                if(wardrobeCamera)wardrobeCamera.transform.SetPositionAndRotation(previousPosition,previousRotation);
            }
        }

        void PresentWardrobe(bool portrait=false)

        {
            if(portrait){PresentLobbyPortrait();return;}

            if(!wardrobeCamera||!NetworkActors.Alive(wardrobePlayer))return;

            var eye=wardrobePlayer.ViewCamera;var feet=wardrobePlayer.transform.position;float height=eye?Mathf.Clamp(eye.transform.position.y-feet.y,.7f,2.8f):1.6f;

            var target=new Vector3(eye?eye.transform.position.x:feet.x,feet.y+height*.58f,eye?eye.transform.position.z:feet.z);

            var visual=wardrobePlayer.gameObject.GetComponent<IModeActor>()?.Visual;if(!visual)return;
            wardrobeRenderers.Clear();wardrobeLayers.Clear();visual.GetComponentsInChildren(false,wardrobeRenderers);
            bool fitted=WardrobeFraming.TryBounds(wardrobeRenderers,out var bounds);
            if(fitted)
            {
                if(!wardrobePivotReady&&Time.frameCount>=wardrobeFramingReadyAt)
                {wardrobePivotOffset=bounds.center-feet;wardrobePivotReady=true;}
                target=wardrobePivotReady?feet+wardrobePivotOffset:bounds.center;
            }
            var position=wardrobeOrbit.Position(target,fitted?1:height,out var rotation);
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
            wardrobePivotReady=false;wardrobeFitDistance=0;wardrobeFramingReadyAt=Time.frameCount+2;
            wardrobeOrbit.Reset((Player?.ViewCamera?Player.ViewCamera.transform.eulerAngles.y:0)+180,2.15f);
        }

        void OnDisable()=>SetWardrobeOpen(false);

        void DisposeWardrobe()

        {

            SetWardrobeOpen(false);if(wardrobeCamera)Destroy(wardrobeCamera.gameObject);if(wardrobeTexture){wardrobeTexture.Release();Destroy(wardrobeTexture);}if(wardrobeMaterial)Destroy(wardrobeMaterial);

        }

    }

}

