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
            Add(joinStation,"code",()=>l.EnteredCode.Length>0?l.EnteredCode:"ENTER A CODE",170,l.FocusCode,()=>l.JoinEnabled,-90,690,95);
            Add(joinStation,"join",()=>"JOIN",170,l.Join,()=>l.JoinEnabled&&l.CodeValid,360,145,95);
            Label(joinStation,"OR",60,28,870,44).color=CandyHudVisuals.Muted;
            Add(joinStation,"detect-nearby",()=>"SEARCH LOCAL",-35,l.DetectNearby,()=>l.JoinEnabled,0,870,105);
            for(int i=0;i<3;i++)
            {int slot=i;Add(joinStation,"nearby-room-"+i,()=>l.NearbyTitle(slot),-125-i*67,()=>l.JoinNearby(slot),()=>l.JoinEnabled&&l.HasNearby(slot),0,870,54,()=>l.ShowingNearby&&l.HasNearby(slot));}
            Add(joinStation,"nearby-next",()=>"MORE ROOMS",-310,l.NextNearbyPage,()=>l.JoinEnabled,235,390,38,()=>l.ShowingNearby&&l.HasMoreNearby);
            Add(joinStation,"code-instead",()=>"HIDE RESULTS",-310,l.ShowCodeEntry,()=>l.JoinEnabled,-235,390,38,()=>l.ShowingNearby);
            Add(joinStation,"accept-invite",()=>"JOIN INVITATION: "+(l.InvitationFrom??""),-155,l.AcceptInvitation,()=>l.JoinEnabled,0,870,60,()=>!l.ShowingNearby&&l.InvitationFrom!=null);
            Add(joinStation,"leave-guest",()=>l.Busy?"CANCEL JOIN":"LEAVE ISLAND",-260,l.Leave,()=>l.CanLeave,0,870,65,()=>!l.HostActive&&l.CanLeave);
            joinHint=Label(joinStation,"Use the enormous keyboard below.",-340,14,900,28);joinHint.color=CandyHudVisuals.Muted;
            BuildKeyboard(joinStation);
            StyleJoinArcade();
            BuildRoomStation();
            BuildGameStation();
        }        void UpdateStations()
        {
            joinFace.Opacity=1;setupFace.Opacity=lobby.IsHost?1:.38f;UpdateRoomStation();UpdateGameStation();
            hostCode.gameObject.SetActive(true);hostCode.text=lobby.HostCode;
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
