using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // The three terminals: JOIN (code, nearby rooms, invitations), HOST, and MAP + MODE.
    public sealed partial class IslandLobbyView
    {
        RectTransform joinStation,hostStation,setupStation;
        LobbyTerminalFaces joinFace,setupFace;
        Text joinHint,hostHint,setupHint,hostCode;
        Keyboard codeKeyboard;
        void BuildTerminals()
        {
            var l=lobby;
            joinStation=Terminal("JOIN",new Vector3(-3.05f,1.62f,1),-10,new Vector2(1000,760),CandyHudVisuals.Lavender);
            joinFace=joinStation.GetComponent<LobbyTerminalFaces>();
            Add(joinStation,"detect-nearby",()=>"DETECT NEARBY",220,l.DetectNearby,()=>l.JoinEnabled,0,870,108);
            Add(joinStation,"code",()=>l.EnteredCode.Length>0?l.EnteredCode:"ENTER CODE",65,l.FocusCode,()=>l.JoinEnabled,0,870,95,()=>!l.ShowingNearby);
            Add(joinStation,"join",()=>"JOIN ISLAND",-70,l.Join,()=>l.JoinEnabled&&l.CodeValid,0,870,92,()=>!l.ShowingNearby);
            for(int i=0;i<3;i++)
            {int slot=i;Add(joinStation,"nearby-room-"+i,()=>l.NearbyTitle(slot),75-i*85,()=>l.JoinNearby(slot),()=>l.JoinEnabled&&l.HasNearby(slot),0,870,73,()=>l.ShowingNearby&&l.HasNearby(slot));}
            Add(joinStation,"nearby-next",()=>"MORE ROOMS",-195,l.NextNearbyPage,()=>l.JoinEnabled,235,390,54,()=>l.ShowingNearby&&l.HasMoreNearby);
            Add(joinStation,"code-instead",()=>"ENTER A CODE",-195,l.ShowCodeEntry,()=>l.JoinEnabled,-235,390,54,()=>l.ShowingNearby);
            Add(joinStation,"accept-invite",()=>"JOIN INVITATION: "+(l.InvitationFrom??""),-165,l.AcceptInvitation,()=>l.JoinEnabled,0,870,60,()=>!l.ShowingNearby&&l.InvitationFrom!=null);
            Add(joinStation,"leave-guest",()=>l.Busy?"CANCEL JOIN":"LEAVE ISLAND",-245,l.Leave,()=>l.CanLeave,0,870,65,()=>!l.HostActive&&l.CanLeave);
            joinHint=Label(joinStation,"Use the enormous keyboard below.",-335,22,900,50);joinHint.color=CandyHudVisuals.Muted;
            BuildKeyboard(joinStation);

            hostStation=Terminal("HOST",new Vector3(0,1.62f,1.8f),0,new Vector2(800,760),CandyHudVisuals.Mint);
            Add(hostStation,"host",()=>"BECOME HOST",135,l.Host,()=>l.CanHost,0,700,150,()=>!l.HostActive);
            Add(hostStation,"region",()=>"REGION: "+l.Region.ToUpperInvariant()+"  ·  CHANGE",-30,l.NextRegion,()=>l.CanHost,0,650,65,()=>!l.HostActive);
            hostCode=Label(hostStation,"",210,49,720,85);
            Add(hostStation,"invite-all",()=>"INVITE NEARBY",75,l.InviteNearby,()=>l.CanInvite,0,700,110,()=>l.HostActive);
            Add(hostStation,"shared-controls",()=>"DEMO CONTROLS: "+(l.SharedControls?"EVERYONE":"HOST ONLY"),-70,l.ToggleSharedControls,()=>l.CanInvite&&l.DemoSelected,0,700,65,()=>l.HostActive);
            Add(hostStation,"abandon-host",()=>l.Busy?"CANCEL HOSTING":"ABANDON HOSTING",-220,l.Leave,()=>l.CanLeave,0,700,90,()=>l.HostActive);
            hostHint=Label(hostStation,"",-320,21,720,85);hostHint.color=CandyHudVisuals.Muted;

            setupStation=Terminal("MAP + MODE",new Vector3(3.05f,1.62f,1),10,new Vector2(900,760),CandyHudVisuals.Peach);
            setupFace=setupStation.GetComponent<LobbyTerminalFaces>();
            Label(setupStation,"MAP",255,21,750,40).color=CandyHudVisuals.Muted;
            Add(setupStation,"map-current",()=>l.MapTitle,175,()=>l.CycleMap(1),()=>l.CanConfigure,0,600,92);
            Add(setupStation,"map-prev",()=>"‹",175,()=>l.CycleMap(-1),()=>l.CanConfigure,-365,85,92);
            Add(setupStation,"map-next",()=>"›",175,()=>l.CycleMap(1),()=>l.CanConfigure,365,85,92);
            Label(setupStation,"GAME MODE",65,21,750,40).color=CandyHudVisuals.Muted;
            Add(setupStation,"mode-current",()=>l.ModeTitle,-10,()=>l.CycleMode(1),()=>l.CanConfigure,0,600,92);
            Add(setupStation,"mode-prev",()=>"‹",-10,()=>l.CycleMode(-1),()=>l.CanConfigure,-365,85,92);
            Add(setupStation,"mode-next",()=>"›",-10,()=>l.CycleMode(1),()=>l.CanConfigure,365,85,92);
            Add(setupStation,"launch",()=>l.StartPending?"CANCEL START":"START GAME",-210,l.StartOrCancel,()=>l.CanStart,0,780,110);
            setupHint=Label(setupStation,"Become host to choose.",-320,22,810,70);setupHint.color=CandyHudVisuals.Muted;
        }
        void UpdateStations()
        {
            joinFace.Opacity=lobby.HostActive?.25f:1;setupFace.Opacity=lobby.CanConfigure||lobby.Connected?1:.35f;
            hostCode.gameObject.SetActive(lobby.HostActive);hostCode.text=lobby.HostCode;
            hostHint.text=lobby.HostHint;setupHint.text=lobby.SetupHint;joinHint.text=lobby.JoinHint;
            if(codeKeyboard!=Keyboard.current){if(codeKeyboard!=null)codeKeyboard.onTextInput-=CodeText;codeKeyboard=Keyboard.current;if(codeKeyboard!=null)codeKeyboard.onTextInput+=CodeText;}
            if(lobby.CodeFocused&&codeKeyboard!=null)
            {
                if(codeKeyboard.backspaceKey.wasPressedThisFrame)lobby.EraseCode();
                if(codeKeyboard.enterKey.wasPressedThisFrame&&lobby.CodeValid)lobby.Join();
                if(codeKeyboard.escapeKey.wasPressedThisFrame)lobby.UnfocusCode();
            }
            UpdateKeyboardTouches();
        }
        void CodeText(char value){if(lobby!=null&&lobby.CodeFocused)lobby.TypeCode(value);}
        RectTransform Terminal(string title,Vector3 position,float yaw,Vector2 size,Color accent)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Canvas),typeof(Image));go.transform.SetParent(transform,false);go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));go.transform.localScale=Vector3.one*.0022f;
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=size;var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=100;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=CandyHudVisuals.Ink;image.raycastTarget=false;
            Label(rect,title,size.y*.5f-72,68,size.x-70,105).fontStyle=FontStyle.Bold;
            var stripe=new GameObject("Candy accent",typeof(RectTransform),typeof(Image));stripe.transform.SetParent(rect,false);var color=stripe.GetComponent<Image>();color.sprite=CandyHudVisuals.Pill;color.type=Image.Type.Sliced;color.color=accent;color.raycastTarget=false;color.rectTransform.sizeDelta=new Vector2(size.x-80,8);color.rectTransform.anchoredPosition=new Vector2(0,size.y*.5f-12);
            go.AddComponent<LobbyTerminalFaces>().Build(Player.ViewCamera,title,accent,size);
            return rect;
        }
        Text Label(RectTransform parent,string text,float y,int size,float width=750,float height=56)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=size;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=14;t.resizeTextMaxSize=size;t.rectTransform.sizeDelta=new Vector2(width,height);t.rectTransform.anchoredPosition=new Vector2(0,y);return t;
        }
        Choice Add(RectTransform parent,string id,Func<string> title,float y,Action action,Func<bool> enabled,float x=0,float width=535,float height=55,Func<bool> visible=null)
        {
            var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(Button),typeof(BoxCollider));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.sizeDelta=new Vector2(width,height);r.anchoredPosition=new Vector2(x,y);
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=CandyHudVisuals.Card;
            var box=go.GetComponent<BoxCollider>();box.size=new Vector3(width,height,8);box.isTrigger=true;
            var label=Label(r,title(),0,height>90?32:24,width-18,height-8);
            var choice=new Choice{id=id,button=go.GetComponent<Button>(),collider=box,label=label,action=action,enabled=enabled,title=title,visible=visible};choices.Add(choice);return choice;
        }
    }
}
