using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // Match and room pages share the same terminal position.
    public sealed partial class IslandLobbyView
    {
        const float StationY = 1.95f, StationZ = 1f, StationScale = .0022f;
        RectTransform roomPage,joinStation,hostStation,setupStation;
        LobbyTerminalFaces setupFace;
        Text joinHint,hostHint,setupHint,hostCode;
        Keyboard codeKeyboard;
        void BuildTerminals()
        {
            var l=lobby;
            roomPage=Terminal("Room",new Vector3(0,StationY,StationZ),0,new Vector2(1100,880),CandyHudVisuals.Mint);
            roomPage.GetComponentInChildren<Text>().enabled=false;
            joinStation=RoomSection("Join a room");
            Add(roomPage,"marina-close",()=>"Back",365,CloseMarinaDetail,()=>true,-412,192,58);
            Add(joinStation,"code",()=>l.EnteredCode.Length>0?l.EnteredCode:"Enter code",170,l.FocusCode,()=>l.JoinEnabled,-90,690,95);
            Add(joinStation,"join",()=>"JOIN",170,l.Join,()=>l.JoinEnabled&&l.CodeValid,360,145,95);
            Add(joinStation,"detect-nearby",()=>"Find nearby rooms",-35,l.DetectNearby,()=>l.JoinEnabled,0,870,105);
            for(int i=0;i<3;i++)
            {int slot=i;Add(joinStation,"nearby-room-"+i,()=>l.NearbyTitle(slot),-125-i*67,()=>l.JoinNearby(slot),()=>l.JoinEnabled&&l.HasNearby(slot),0,870,54,()=>l.ShowingNearby&&l.HasNearby(slot));}
            Add(joinStation,"nearby-next",()=>"More rooms",-310,l.NextNearbyPage,()=>l.JoinEnabled,235,390,38,()=>l.ShowingNearby&&l.HasMoreNearby);
            Add(joinStation,"code-instead",()=>"Enter code",-310,l.ShowCodeEntry,()=>l.JoinEnabled,-235,390,38,()=>l.ShowingNearby);
            Add(joinStation,"accept-invite",()=>"Join "+(l.InvitationFrom??""),-155,l.AcceptInvitation,()=>l.JoinEnabled,0,870,60,()=>!l.ShowingNearby&&l.InvitationFrom!=null);
            joinHint=Label(joinStation,"",-340,14,900,28);joinHint.color=CandyHudVisuals.Muted;
            BuildKeyboard(joinStation);
            BuildRoomStation();
            BuildLobbyPortrait();
            BuildGameStation();
            Add(setupStation,"open-room",()=>"Room",467,()=>OpenMarinaDetail("room"),()=>true,-340,160,66,()=>!marina&&setupPage=="main");
            Add(setupStation,"open-join",()=>"Join",467,()=>OpenMarinaDetail("join"),()=>l.JoinEnabled,340,160,66,()=>!marina&&setupPage=="main"&&!RoomHasGuests);
            CandyChoice("marina-close").button.transform.SetAsLastSibling();
            roomPage.SetPositionAndRotation(setupStation.position,setupStation.rotation);
            roomPage.gameObject.SetActive(false);
            FitTerminal(setupStation,new Vector2(1100,820),1.22f,.74f);
            BuildControlCabinet(setupStation,true);
        }        void UpdateStations()
        {
            UpdateRoomStation();UpdateGameStation();
            hostCode.text=lobby.Connected||lobby.Busy?lobby.HostCode:"—";
            string notice=Details?.Notification;
            hostHint.text=!string.IsNullOrEmpty(lobby.HostHint)?lobby.HostHint:notice??"";
            setupHint.text=lobby.SetupHint;
            joinHint.text=lobby.Busy||lobby.JoinHint.StartsWith("Connection failed",StringComparison.Ordinal)||
                lobby.JoinHint.StartsWith("Check the six-character",StringComparison.Ordinal)?lobby.JoinHint:"";
            if(codeKeyboard!=Keyboard.current){if(codeKeyboard!=null)codeKeyboard.onTextInput-=CodeText;codeKeyboard=Keyboard.current;if(codeKeyboard!=null)codeKeyboard.onTextInput+=CodeText;}
            if(lobby.CodeFocused&&codeKeyboard!=null)
            {
                if(codeKeyboard.backspaceKey.wasPressedThisFrame)lobby.EraseCode();
                if(codeKeyboard.enterKey.wasPressedThisFrame&&lobby.CodeValid)lobby.Join();
                if(codeKeyboard.escapeKey.wasPressedThisFrame)lobby.UnfocusCode();
            }
            UpdateCandyDesign();
            RefreshLobbyPortrait();
        }
        RectTransform RoomSection(string title)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image));
            var rect=(RectTransform)go.transform;
            rect.SetParent(roomPage,false);
            rect.sizeDelta=roomPage.sizeDelta;
            rect.anchoredPosition=Vector2.zero;
            go.GetComponent<Image>().raycastTarget=false;
            Label(rect,title,310,48,800,75);
            return rect;
        }

        void FitTerminal(RectTransform screen,Vector2 size,float x,float y)
        {
            screen.sizeDelta=size;
            foreach(var rect in screen.GetComponentsInChildren<RectTransform>(true))if(rect!=screen)
            {
                rect.anchoredPosition=Vector2.Scale(rect.anchoredPosition,new Vector2(x,y));rect.sizeDelta=Vector2.Scale(rect.sizeDelta,new Vector2(x,y));
                if(rect.TryGetComponent<BoxCollider>(out var hit)){hit.size=Vector3.Scale(hit.size,new Vector3(x,y,1));hit.center=Vector3.Scale(hit.center,new Vector3(x,y,1));}
            }
            screen.GetComponent<LobbyTerminalFaces>().Refresh();
        }
        void CodeText(char value){if(lobby!=null&&lobby.CodeFocused)lobby.TypeCode(value);}
        RectTransform Terminal(string title,Vector3 position,float yaw,Vector2 size,Color accent)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Canvas),typeof(Image));go.transform.SetParent(transform,false);go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));go.transform.localScale=Vector3.one*(title=="WARDROBE"?.0022f:StationScale);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=size;var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=100;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=CandyHudVisuals.Ink;image.raycastTarget=false;
            Label(rect,title,size.y*.5f-72,68,size.x-70,105).fontStyle=FontStyle.Bold;
            var stripe=new GameObject("Candy accent",typeof(RectTransform),typeof(Image));stripe.transform.SetParent(rect,false);var color=stripe.GetComponent<Image>();color.sprite=CandyHudVisuals.Pill;color.type=Image.Type.Sliced;color.color=accent;color.raycastTarget=false;color.rectTransform.sizeDelta=new Vector2(size.x-80,8);color.rectTransform.anchoredPosition=new Vector2(0,size.y*.5f-12);
            go.AddComponent<LobbyTerminalFaces>().Build(Player.ViewCamera,title,accent,size);
            return rect;
        }
        Text Label(RectTransform parent,string text,float y,int size,float width=750,float height=56)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=DeadlyJenga.CastlePrototype.CandyHudVisuals.TextFont;t.text=text;t.fontSize=size;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=14;t.resizeTextMaxSize=size;t.rectTransform.sizeDelta=new Vector2(width,height);t.rectTransform.anchoredPosition=new Vector2(0,y);return t;
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
