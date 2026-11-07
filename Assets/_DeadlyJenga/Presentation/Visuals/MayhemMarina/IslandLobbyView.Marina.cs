using System;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        MayhemMarina marina;
        MarinaScreen marinaScreen;
        RectTransform marinaBack,marinaMirror;
        Texture2D marinaMaskTexture;
        Sprite marinaMaskSprite;
        string marinaDetail="";
        float nextMarinaMirror;
        struct MarinaDetailPose
        {
            public RectTransform target;public Vector3 position,scale;public Quaternion rotation;
        }
        readonly System.Collections.Generic.List<MarinaDetailPose> marinaDetailPoses=new();
        Vector3 marinaDetailEye,marinaDetailForward,marinaDetailUp;
        Quaternion marinaDetailRotation;
        float marinaDetailDistance,marinaDetailWidth;
        bool marinaKeyboardVisible;
        readonly System.Collections.Generic.Dictionary<Graphic,Material> marinaGraphicMaterials=new();
        readonly System.Collections.Generic.Dictionary<Material,Material> marinaFocusMaterials=new();
        readonly System.Collections.Generic.List<Graphic> marinaGraphics=new();
        float nextMarinaMaterials;
        void PrioritizeMarinaDetailMaterials()
        {
            if(marinaDetail==""||!isActiveAndEnabled)return;
            foreach(var root in new[]{marinaDetail=="join"?joinStation:marinaDetail=="host"?hostStation:setupStation,marinaBack,piano})
            {
                if(!root||!root.gameObject.activeInHierarchy)continue;
                marinaGraphics.Clear();root.GetComponentsInChildren(true,marinaGraphics);
                foreach(var graphic in marinaGraphics)
                {
                    if(!graphic||marinaGraphicMaterials.ContainsKey(graphic))continue;
                    var original=graphic.material;if(!original||!original.shader)continue;
                    string shader=original.shader.name;
                    if(shader!="UI/Default"&&shader!="UI/Default Font")continue;
                    if(!marinaFocusMaterials.TryGetValue(original,out var focused))
                    {
                        focused=new Material(original){name=original.name+" / focused Marina",hideFlags=HideFlags.HideAndDontSave,renderQueue=4000};
                        focused.SetInt("unity_GUIZTestMode",(int)UnityEngine.Rendering.CompareFunction.Always);
                        marinaFocusMaterials.Add(original,focused);
                    }
                    marinaGraphicMaterials.Add(graphic,original);graphic.material=focused;
                }
            }
            nextMarinaMaterials=Time.unscaledTime+.25f;
        }
        void RestoreMarinaDetailMaterials()
        {
            foreach(var pair in marinaGraphicMaterials)
                if(pair.Key&&marinaFocusMaterials.TryGetValue(pair.Value,out var focused)&&pair.Key.material==focused)pair.Key.material=pair.Value;
            marinaGraphicMaterials.Clear();
            foreach(var material in marinaFocusMaterials.Values)if(material)Destroy(material);
            marinaFocusMaterials.Clear();marinaGraphics.Clear();nextMarinaMaterials=0;
        }
        partial void BuildMayhemMarina()
        {
            if(!(lobby is IWorldQuery world)||world.Radius<30)return;
            if(marina)return;
            foreach(var resort in GetComponents<CandyLobbyResort>())
            {
                resort.enabled=false;
                var old=resort.transform.Find("Visual · Candy island resort");if(old)old.gameObject.SetActive(false);
            }
            foreach(string name in new[]{"Grounded red arcade cabinet","Host controller computer","Mode controller computer"})
            {
                var old=transform.Find(name);if(old)foreach(var r in old.GetComponentsInChildren<Renderer>())r.enabled=false;
            }
            marina=gameObject.AddComponent<MayhemMarina>();marina.Build(lobby,Environment);
            foreach(var television in GetComponentsInChildren<LobbySpectatorTVWall>(true))
            {
                television.transform.localPosition=new Vector3(-10,4.16f,20.2f);television.transform.localRotation=Quaternion.identity;
                lobby.AddBlocker(television.transform,new Vector3(0,1.48f,.03f),new Vector3(3.76f,2.95f,.55f));
            }
            wardrobeCenter=MayhemMarina.Wardrobe;
            var pod=transform.Find("Visual - wardrobe pod");if(pod)pod.gameObject.SetActive(false);
            wardrobePanel.SetPositionAndRotation(wardrobeCenter+new Vector3(2.8f,1.8f,1.3f),Quaternion.Euler(0,-35,0));
            joinStation.SetPositionAndRotation(new Vector3(-4.25f,3.5f,9.25f),Quaternion.Euler(0,-12,0));
            hostStation.SetPositionAndRotation(new Vector3(4.15f,3.5f,9.3f),Quaternion.Euler(0,12,0));
            setupStation.SetPositionAndRotation(MayhemMarina.PlayScreen+Vector3.back*.10f,Quaternion.identity);setupStation.localScale=Vector3.one*.0036f;
            piano.SetPositionAndRotation(new Vector3(-4.2f,2.65f,7.6f),Quaternion.Euler(35,-12,0));
            marinaScreen=gameObject.AddComponent<MarinaScreen>();marinaScreen.Build(lobby,OpenMarinaDetail);
            var go=new GameObject("Marina detail back control",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);marinaBack=go.GetComponent<RectTransform>();marinaBack.position=new Vector3(0,2.9f,4.15f);marinaBack.localScale=Vector3.one*.0028f;marinaBack.sizeDelta=new Vector2(1000,125);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Canvas>().overrideSorting=true;go.GetComponent<Canvas>().sortingOrder=100;
            Add(marinaBack,"marina-close",()=>"BACK TO MARINA",0,CloseMarinaDetail,()=>true,0,1000,125);
            if(wardrobeCamera)wardrobeCamera.backgroundColor=new Color(.10f,.28f,.34f);
            BuildMarinaMirror();CloseMarinaDetail();SyncMarinaBacks();
        }
        void RememberMarinaDetailLayout()
        {
            if(marinaDetailPoses.Count>0)return;
            foreach(var target in new[]{joinStation,hostStation,setupStation,piano,marinaBack})
                if(target)marinaDetailPoses.Add(new MarinaDetailPose{target=target,position=target.localPosition,rotation=target.localRotation,scale=target.localScale});
        }
        void RestoreMarinaDetailLayout()
        {
            foreach(var pose in marinaDetailPoses)
                if(pose.target){pose.target.localPosition=pose.position;pose.target.localRotation=pose.rotation;pose.target.localScale=pose.scale;}
            marinaDetailPoses.Clear();marinaKeyboardVisible=false;
        }
        static void MarinaWorldScale(RectTransform target,float scale)
        {
            var parent=target.parent?target.parent.lossyScale:Vector3.one;
            target.localScale=new Vector3(scale/Mathf.Max(.0001f,Mathf.Abs(parent.x)),scale/Mathf.Max(.0001f,Mathf.Abs(parent.y)),scale/Mathf.Max(.0001f,Mathf.Abs(parent.z)));
        }
        void FocusMarinaDetail(RectTransform panel)
        {
            var eye=Player?.ViewCamera;if(!eye||!panel||!marinaBack)return;
            RememberMarinaDetailLayout();
            marinaDetailEye=eye.transform.position;marinaDetailRotation=eye.transform.rotation;
            marinaDetailForward=eye.transform.forward;marinaDetailUp=eye.transform.up;marinaDetailDistance=2.1f;
            float width=Mathf.Max(1,panel.rect.width),height=Mathf.Max(1,panel.rect.height);
            float backRatio=width*.72f/Mathf.Max(1,marinaBack.rect.width);
            float backHeight=marinaBack.rect.height*backRatio,gap=24;
            float vertical=eye.fieldOfView*Mathf.Deg2Rad*.5f;
            float horizontal=Mathf.Atan(Mathf.Tan(vertical)*Mathf.Max(.3f,eye.aspect));
            float halfHeight=Mathf.Min(vertical*.8f,(Player.IsVR?24:29)*Mathf.Deg2Rad);
            float halfWidth=Mathf.Min(horizontal*.8f,42*Mathf.Deg2Rad);
            float scale=Mathf.Min(2*marinaDetailDistance*Mathf.Tan(halfWidth)/width,
                2*marinaDetailDistance*Mathf.Tan(halfHeight)/(height+backHeight+gap));
            var center=marinaDetailEye+marinaDetailForward*marinaDetailDistance;
            panel.SetPositionAndRotation(center-marinaDetailUp*((backHeight+gap)*scale*.5f),marinaDetailRotation);MarinaWorldScale(panel,scale);
            marinaBack.SetPositionAndRotation(panel.position+marinaDetailUp*((height*.5f+gap+backHeight*.5f)*scale)-marinaDetailForward*.01f,marinaDetailRotation);
            MarinaWorldScale(marinaBack,scale*backRatio);marinaDetailWidth=width*scale;
            marinaKeyboardVisible=false;PlaceMarinaKeyboard();PrioritizeMarinaDetailMaterials();
        }
        void PlaceMarinaKeyboard()
        {
            if(!piano)return;
            bool visible=marinaDetail=="join"&&lobby.CodeFocused;
            piano.gameObject.SetActive(visible);
            if(!visible){marinaKeyboardVisible=false;return;}
            if(marinaKeyboardVisible)return;
            marinaKeyboardVisible=true;
            float distance=Player.IsVR?.85f:1.1f;
            float width=Mathf.Min(1.65f,marinaDetailWidth*distance/Mathf.Max(.1f,marinaDetailDistance));
            float down=Mathf.Min(.42f,Mathf.Max(.18f,(marinaDetailEye.y-Player.transform.position.y)*.5f));
            var position=marinaDetailEye+marinaDetailForward*distance-marinaDetailUp*down;
            position.y=Mathf.Max(position.y,Player.transform.position.y+.35f);
            piano.SetPositionAndRotation(position,marinaDetailRotation*Quaternion.Euler(55,0,0));
            MarinaWorldScale(piano,width/Mathf.Max(1,piano.rect.width));
        }
        void OpenMarinaDetail(string page)
        {
            if(page=="style"){RestoreMarinaDetailMaterials();marinaDetail="";RestoreMarinaDetailLayout();if(Vector3.Distance(Player.transform.position,WardrobePosition)>2)Player.Place(WardrobePosition+Vector3.up*.04f,transform.rotation);SetWardrobeOpen(true);return;}
            marinaDetail=page;joinStation.gameObject.SetActive(page=="join");hostStation.gameObject.SetActive(page=="host");setupStation.gameObject.SetActive(page=="maps"||page=="modes"||page=="settings");piano.gameObject.SetActive(page=="join");
            if(page=="maps"||page=="modes")OpenPicker(page);
            if(page=="settings"){setupPage="settings";settingsPage=0;}
            marinaScreen.Visible=false;marinaBack.gameObject.SetActive(true);
            FocusMarinaDetail(page=="join"?joinStation:page=="host"?hostStation:setupStation);SyncMarinaBacks();
        }
        void CloseMarinaDetail()
        {
            lobby?.UnfocusCode();RestoreMarinaDetailMaterials();RestoreMarinaDetailLayout();terminalRequireRelease=true;DeadlyJenga.Presentation.MenuAim.Consume();
            marinaDetail="";joinStation.gameObject.SetActive(false);hostStation.gameObject.SetActive(false);setupStation.gameObject.SetActive(false);piano.gameObject.SetActive(false);wardrobePanel.gameObject.SetActive(false);
            if(wardrobeOpen)SetWardrobeOpen(false);if(marinaBack)marinaBack.gameObject.SetActive(false);if(marinaScreen)marinaScreen.Visible=true;SyncMarinaBacks();
        }
        partial void MarinaInteractionRange(ref float distance){if(marina)distance=11;}
        partial void UpdateMayhemMarina()
        {
            if(!marina)return;
            if(marinaDetail!=""&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)CloseMarinaDetail();
            if(marinaScreen)marinaScreen.Visible=marinaDetail==""&&!wardrobeOpen;
            if(marinaDetail!=""){PlaceMarinaKeyboard();if(Time.unscaledTime>=nextMarinaMaterials)PrioritizeMarinaDetailMaterials();}
            if(wardrobePanel)wardrobePanel.gameObject.SetActive(wardrobeOpen&&Player.IsVR);
            SyncMarinaBacks();
            if(!wardrobeOpen&&Time.unscaledTime>=nextMarinaMirror&&Vector3.Distance(Player.transform.position,WardrobePosition)<6)
            {
                nextMarinaMirror=Time.unscaledTime+.2f;var previous=wardrobePlayer;wardrobePlayer=Player;
                try{PresentMarinaMirror();}finally{wardrobePlayer=previous;}
            }
        }
        void SyncMarinaBacks()
        {
            void Sync(string name,RectTransform front){var back=transform.Find(name+" / readable back");if(back)back.gameObject.SetActive(front&&front.gameObject.activeInHierarchy);}
            Sync("JOIN",joinStation);Sync("HOST",hostStation);Sync("MODE + MAP",setupStation);Sync("WARDROBE",wardrobePanel);
        }
        void BuildMarinaMirror()
        {
            var go=new GameObject("Marina live wardrobe mirror",typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(Mask));go.transform.SetParent(transform,false);marinaMirror=go.GetComponent<RectTransform>();marinaMirror.position=new Vector3(-13,3.4f,10.08f);marinaMirror.localScale=Vector3.one*.01f;marinaMirror.sizeDelta=new Vector2(247,348);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Canvas>().overrideSorting=true;go.GetComponent<Canvas>().sortingOrder=60;
            marinaMaskTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*32);pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-d));}
            marinaMaskTexture.SetPixels(pixels);marinaMaskTexture.Apply();marinaMaskSprite=Sprite.Create(marinaMaskTexture,new Rect(0,0,64,64),Vector2.one*.5f);go.GetComponent<Image>().sprite=marinaMaskSprite;go.GetComponent<Image>().color=new Color(.42f,.68f,.75f);go.GetComponent<Mask>().showMaskGraphic=true;
            var image=new GameObject("Mirror image",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(go.transform,false);var raw=image.GetComponent<RawImage>();raw.texture=wardrobeTexture;raw.color=Color.white;raw.raycastTarget=false;raw.rectTransform.sizeDelta=new Vector2(348,348);
        }
        partial void DisposeMayhemMarina()
        {RestoreMarinaDetailMaterials();if(marinaScreen)Destroy(marinaScreen);if(marina)Destroy(marina);if(marinaBack)Destroy(marinaBack.gameObject);if(marinaMirror)Destroy(marinaMirror.gameObject);if(marinaMaskSprite)Destroy(marinaMaskSprite);if(marinaMaskTexture)Destroy(marinaMaskTexture);}
    }
}
