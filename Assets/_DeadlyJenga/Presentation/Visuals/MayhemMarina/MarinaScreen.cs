using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.CandyArt.Marina
{
    // Requests still go through the real lobby. This is only its simplified front face.
    public sealed class MarinaScreen : MonoBehaviour
    {
        sealed class Target {public string id;public BoxCollider collider;public Image face;public Action action;public Func<bool> allowed;}
        readonly List<Target> targets=new();
        ILobbyControls lobby;ISessionInfo session;Action<string> detail;
        InputAction select;RectTransform canvas;Text modeTitle,mapTitle,playTitle,partyTitle;
        RawImage modePicture,mapPicture,otherPicture;string modeId,mapId;float refreshAt;
        public bool Visible {get=>canvas&&canvas.gameObject.activeSelf;set{if(canvas)canvas.gameObject.SetActive(value);}}
        public void Build(ILobbyControls controls,Action<string> openDetail)
        {
            if(canvas)return;lobby=controls;session=Services.Session;detail=openDetail;
            var go=new GameObject("Marina · live play screen",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);canvas=go.GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(720,320);canvas.position=MayhemMarina.PlayScreen+Vector3.back*.11f;canvas.localScale=Vector3.one*.0098f;
            var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.WorldSpace;c.overrideSorting=true;c.sortingOrder=70;
            Text("MODE",new Vector2(-118,135),new Vector2(400,40),30,canvas);Text("MAP",new Vector2(236,135),new Vector2(220,40),30,canvas);
            modePicture=Card("marina-modes",new Vector2(-236,30),()=>detail?.Invoke("modes"));
            otherPicture=Card("marina-more-modes",new Vector2(0,30),()=>detail?.Invoke("modes"));
            mapPicture=Card("marina-maps",new Vector2(236,30),()=>detail?.Invoke("maps"));
            modeTitle=Text("",new Vector2(-236,-68),new Vector2(225,27),17,canvas);
            Text("BROWSE MODES",new Vector2(0,-68),new Vector2(225,27),17,canvas);
            mapTitle=Text("",new Vector2(236,-68),new Vector2(225,27),17,canvas);
            var play=Button("marina-play",new Vector2(0,-111),new Vector2(330,55),new Color(.15f,.62f,.14f),()=>lobby.StartOrCancel(),()=>lobby.CanStart);
            playTitle=Text("PLAY",Vector2.zero,new Vector2(300,53),42,play);
            string[] ids={"join","host","settings","style"};string[] captions={"JOIN","HOST","SETTINGS","STYLE"};
            for(int i=0;i<4;i++){string id=ids[i];var b=Button("marina-"+id,new Vector2(-270+i*180,-153),new Vector2(165,25),new Color(.16f,.36f,.34f),()=>detail?.Invoke(id),()=>true);Text(captions[i],Vector2.zero,new Vector2(160,24),16,b);}
            partyTitle=Text("",new Vector2(0,92),new Vector2(200,20),15,canvas);partyTitle.color=new Color(.86f,.98f,.88f);
            select=new InputAction("Marina select",InputActionType.Button,"<XRController>{RightHand}/trigger");select.Enable();Refresh();
        }
        RawImage Card(string id,Vector2 p,Action action)
        {
            var r=Button(id,p,new Vector2(220,156),new Color(1,.95f,.84f),action,()=>true);
            var go=new GameObject("Live destination image",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(r,false);var image=go.GetComponent<RawImage>();image.rectTransform.sizeDelta=new Vector2(211,147);image.raycastTarget=false;return image;
        }
        RectTransform Button(string id,Vector2 p,Vector2 size,Color color,Action action,Func<bool> allowed)
        {
            var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(BoxCollider));go.layer=5;go.transform.SetParent(canvas,false);var r=go.GetComponent<RectTransform>();r.anchoredPosition=p;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            var hit=go.GetComponent<BoxCollider>();hit.isTrigger=true;hit.size=new Vector3(size.x,size.y,6);targets.Add(new Target{id=id,collider=hit,face=image,action=action,allowed=allowed});return r;
        }
        static Text Text(string value,Vector2 p,Vector2 size,int font,Transform parent)
        {
            var go=new GameObject("Marina label · "+value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=font;t.fontStyle=FontStyle.Bold;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(1,.97f,.89f);t.raycastTarget=false;t.rectTransform.anchoredPosition=p;t.rectTransform.sizeDelta=size;return t;
        }
        void Refresh()
        {
            if(!canvas||lobby==null)return;modeTitle.text=lobby.ModeTitle.ToUpperInvariant();mapTitle.text=lobby.MapTitle.ToUpperInvariant();playTitle.text=lobby.StartPending?"CANCEL":"PLAY";
            var details=lobby as ILobbyDetails;partyTitle.text=details==null?"":details.ConnectedCount+" PLAYER"+(details.ConnectedCount==1?"":"S");
            string nextMode=details?.SelectedModeId??"mystery",nextMap=details?.SelectedMapId??"castle";
            if(modeId!=nextMode){modeId=nextMode;modePicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+modeId);otherPicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+(modeId=="ctf"?"disasters":"ctf"));}
            if(mapId!=nextMap){mapId=nextMap;mapPicture.texture=MapPreviewImages.Get(mapId);}
        }
        public bool Invoke(string id)
        {foreach(var target in targets)if(target.id==id&&Visible&&target.allowed()){target.action();return true;}return false;}
        void Update()
        {
            if(!Visible||DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session))return;if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.2f;Refresh();}
            var player=session?.Player;if(!NetworkActors.Alive(player)||!player.ViewCamera)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            bool pressed=select!=null&&select.WasPressedThisFrame()||player.SelectPressed||keyboard!=null&&keyboard.eKey.wasPressedThisFrame||mouse!=null&&mouse.leftButton.wasPressedThisFrame;
            var camera=player.ViewCamera;Ray ray=!player.IsVR&&session.MenuOpen&&mouse!=null?camera.ScreenPointToRay(mouse.position.ReadValue()):new Ray(camera.transform.position,camera.transform.forward);
            Target nearest=null;float distance=11;
            foreach(var target in targets)
            {
                target.collider.enabled=target.allowed();
                if(target.collider.enabled&&target.collider.Raycast(ray,out var hit,distance)&&Vector3.Dot(ray.direction,target.collider.transform.forward)>0){nearest=target;distance=hit.distance;}
            }
            if(pressed&&nearest!=null)Invoke(nearest.id);
        }
        void OnEnable(){select?.Enable();}
        void OnDisable(){select?.Disable();}
        void OnDestroy(){select?.Dispose();if(canvas)Destroy(canvas.gameObject);}
    }
}
