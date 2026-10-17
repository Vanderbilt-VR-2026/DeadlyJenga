using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobby
    {
        RectTransform joinStation,hostStation,setupStation;
        LobbyTerminalFaces joinFace,setupFace;
        Text joinHint,hostHint,setupHint,hostCode;
        bool showingNearby,codeFocused;
        int nearbyPage;
        float scanningUntil;
        Keyboard codeKeyboard;
        public bool JoinStationEnabled=>Room&&!Room.HostSelected&&!Room.Connected&&!Room.Busy;
        public bool SetupStationEnabled=>Room&&Room.CanConfigure;
        bool HostActive=>Room.HostSelected||Room.IsHost;
        bool CanLeave=>Room.Connected||Room.Busy;
        void BuildTerminals()
        {
            joinStation=Terminal("JOIN",new Vector3(-3.05f,1.62f,1),-10,new Vector2(1000,760),CandyHudVisuals.Lavender);
            joinFace=joinStation.GetComponent<LobbyTerminalFaces>();
            Add(joinStation,"detect-nearby",()=>"DETECT NEARBY",220,DetectNearby,()=>JoinStationEnabled,0,870,108);
            Add(joinStation,"code",()=>EnteredCode.Length>0?EnteredCode:"ENTER CODE",65,FocusCode,()=>JoinStationEnabled,0,870,95,()=>!showingNearby);
            Add(joinStation,"join",()=>"JOIN ISLAND",-70,()=>_=Room.Join(EnteredCode),()=>JoinStationEnabled&&ValidCode,0,870,92,()=>!showingNearby);
            for(int i=0;i<3;i++)
            {int slot=i;Add(joinStation,"nearby-room-"+i,()=>NearbyTitle(slot),75-i*85,()=>JoinNearbySlot(slot),()=>JoinStationEnabled&&NearbyEntry(slot)!=null,0,870,73,()=>showingNearby&&NearbyEntry(slot)!=null);}
            Add(joinStation,"nearby-next",()=>"MORE ROOMS",-195,()=>nearbyPage++,()=>JoinStationEnabled,235,390,54,()=>showingNearby&&(Room.Discovery?.Nearby.Count??0)>3);
            Add(joinStation,"code-instead",()=>"ENTER A CODE",-195,()=>{showingNearby=false;FocusCode();},()=>JoinStationEnabled,-235,390,54,()=>showingNearby);
            Add(joinStation,"accept-invite",()=>"JOIN INVITATION: "+(Room.Discovery?.Invitation?.message.name??""),-165,Room.AcceptInvite,()=>JoinStationEnabled,0,870,60,()=>!showingNearby&&Room.Discovery?.Invitation!=null);
            Add(joinStation,"leave-guest",()=>Room.Busy?"CANCEL JOIN":"LEAVE ISLAND",-245,()=>_=Room.Leave(),()=>CanLeave,0,870,65,()=>!HostActive&&CanLeave);
            joinHint=Label(joinStation,"Use the enormous keyboard below.",-335,22,900,50);joinHint.color=CandyHudVisuals.Muted;
            BuildKeyboard(joinStation);

            hostStation=Terminal("HOST",new Vector3(0,1.62f,1.8f),0,new Vector2(800,760),CandyHudVisuals.Mint);
            Add(hostStation,"host",()=>"BECOME HOST",135,()=>_=Room.Host(),()=>!Room.Busy&&!Room.Connected,0,700,150,()=>!HostActive);
            Add(hostStation,"region",()=>"REGION: "+Room.Region.ToUpperInvariant()+"  ·  CHANGE",-30,()=>Room.Region=IslandRoomAddress.Regions[(Array.IndexOf(IslandRoomAddress.Regions,Room.Region)+1)%IslandRoomAddress.Regions.Length],()=>!Room.Busy&&!Room.Connected,0,650,65,()=>!HostActive);
            hostCode=Label(hostStation,"",210,49,720,85);
            Add(hostStation,"invite-all",()=>"INVITE NEARBY",75,Room.InviteNearby,()=>Room.IsHost&&!Room.Busy,0,700,110,()=>HostActive);
            Add(hostStation,"shared-controls",()=>"DEMO CONTROLS: "+(Room.SharedDemoControls?"EVERYONE":"HOST ONLY"),-70,()=>Room.SetSharedControls(!Room.SharedDemoControls),()=>Room.IsHost&&!Room.Busy&&catalog.modes[Room.ModeIndex].id=="demo",0,700,65,()=>HostActive);
            Add(hostStation,"abandon-host",()=>Room.Busy?"CANCEL HOSTING":"ABANDON HOSTING",-220,()=>_=Room.Leave(),()=>CanLeave,0,700,90,()=>HostActive);
            hostHint=Label(hostStation,"",-320,21,720,85);hostHint.color=CandyHudVisuals.Muted;

            setupStation=Terminal("MAP + MODE",new Vector3(3.05f,1.62f,1),10,new Vector2(900,760),CandyHudVisuals.Peach);
            setupFace=setupStation.GetComponent<LobbyTerminalFaces>();
            Label(setupStation,"MAP",255,21,750,40).color=CandyHudVisuals.Muted;
            Add(setupStation,"map-current",()=>catalog.maps[Room.MapIndex].title,175,()=>CycleMap(1),()=>Room.CanConfigure,0,600,92);
            Add(setupStation,"map-prev",()=>"‹",175,()=>CycleMap(-1),()=>Room.CanConfigure,-365,85,92);
            Add(setupStation,"map-next",()=>"›",175,()=>CycleMap(1),()=>Room.CanConfigure,365,85,92);
            Label(setupStation,"GAME MODE",65,21,750,40).color=CandyHudVisuals.Muted;
            Add(setupStation,"mode-current",()=>catalog.modes[Room.ModeIndex].title,-10,()=>CycleMode(1),()=>Room.CanConfigure,0,600,92);
            Add(setupStation,"mode-prev",()=>"‹",-10,()=>CycleMode(-1),()=>Room.CanConfigure,-365,85,92);
            Add(setupStation,"mode-next",()=>"›",-10,()=>CycleMode(1),()=>Room.CanConfigure,365,85,92);
            Add(setupStation,"launch",()=>Room.LaunchPending?"CANCEL START":"START GAME",-210,()=>{if(Room.LaunchPending)Room.CancelLaunch();else Room.Launch();},()=>Room.CanLaunch||Room.LaunchPending&&Room.IsHost,0,780,110);
            setupHint=Label(setupStation,"Become host to choose.",-320,22,810,70);setupHint.color=CandyHudVisuals.Muted;
        }
        bool ValidCode=>IslandRoomAddress.TryParse(EnteredCode,Room.Region,out _,out _);
        void CycleMap(int step)=>Room.SelectMap((Room.MapIndex+step+catalog.maps.Length)%catalog.maps.Length);
        void CycleMode(int step)=>Room.SelectMode((Room.ModeIndex+step+catalog.modes.Length)%catalog.modes.Length);
        void FocusCode(){if(!JoinStationEnabled)return;showingNearby=false;codeFocused=true;if(!Player.IsVR)SetMenu(true);}
        void DetectNearby(){Room.StartDiscovery();showingNearby=true;codeFocused=false;nearbyPage=0;scanningUntil=Time.unscaledTime+2;}
        IslandDiscovery.Entry NearbyEntry(int slot)
        {
            var list=Room.Discovery?.Nearby;if(list==null||list.Count==0)return null;
            int pages=(list.Count+2)/3,index=(nearbyPage%pages)*3+slot;return index<list.Count?list[index]:null;
        }
        string NearbyTitle(int slot){var entry=NearbyEntry(slot);return entry==null?"":entry.message.name+" · "+entry.message.count+" players\nJOIN "+entry.message.code;}
        void JoinNearbySlot(int slot){var entry=NearbyEntry(slot);if(entry==null)return;EnteredCode=entry.message.code;_=Room.Join(EnteredCode);}
        void UpdateStations(CandyTrackingSource tracking)
        {
            joinFace.Opacity=HostActive ? .25f : 1;setupFace.Opacity=Room.CanConfigure||Room.Connected?1:.35f;
            hostCode.gameObject.SetActive(HostActive);hostCode.text=Room.Busy?"CREATING ISLAND…":Room.Code;
            hostHint.text=HostActive?(Room.Busy?"You can cancel while connecting.":"Your room · "+Room.HumanCount+" / 10 players\nInvite friends, then choose a map and mode."):Room.Connected?"You are a guest on the host's island.":"Create an island for your friends.\nJoining switches off while you host.";
            setupHint.text=Room.LaunchPending?"Waiting for players to confirm…":!catalog.modes[Room.ModeIndex].available?"This mode is coming later.":Room.IsHost?"Ready when you are.":Room.Connected?"The host chooses. Everyone sees the same setup.":"Become host to choose.";
            joinHint.text=HostActive?"JOIN IS OFF WHILE YOU HOST":Room.Busy?"Connecting… cancel above if needed.":Room.Connected?"Joined "+Room.Code:showingNearby?(Room.Discovery==null?Room.DiscoveryStatus:Time.unscaledTime<scanningUntil?"Detecting nearby islands…":(Room.Discovery.Nearby.Count==0?"No rooms yet. Same Wi-Fi + Internet required.\nYou can still enter a code.":"Tap a room to join.")):"Type, point, or play the giant keyboard.\nFull codes look like US-ABC234.";
            if(codeKeyboard!=Keyboard.current){if(codeKeyboard!=null)codeKeyboard.onTextInput-=CodeText;codeKeyboard=Keyboard.current;if(codeKeyboard!=null)codeKeyboard.onTextInput+=CodeText;}
            if(!JoinStationEnabled)codeFocused=false;
            if(codeFocused&&codeKeyboard!=null)
            {
                if(codeKeyboard.backspaceKey.wasPressedThisFrame)EraseCode();
                if(codeKeyboard.enterKey.wasPressedThisFrame&&ValidCode)_=Room.Join(EnteredCode);
                if(codeKeyboard.escapeKey.wasPressedThisFrame)codeFocused=false;
            }
            if(!HostActive&&!Room.Busy&&!Room.Connected&&Room.Status.StartsWith("Connection failed",StringComparison.Ordinal))joinHint.text=hostHint.text=Room.Status;
            else if(JoinStationEnabled&&!showingNearby&&EnteredCode.Length>=6&&!ValidCode)joinHint.text="Check the code. Example: US-ABC234.\nUse the host's region prefix, or six room characters.";
            UpdateKeyboardTouches(tracking);
        }
        void CodeText(char value)
        {if(!codeFocused||!JoinStationEnabled)return;value=char.ToUpperInvariant(value);if((value>='A'&&value<='Z'||value>='0'&&value<='9'||value=='-')&&EnteredCode.Length<12)EnteredCode+=value;}
        void EraseCode(){if(EnteredCode.Length>0)EnteredCode=EnteredCode.Substring(0,EnteredCode.Length-1);}
        RectTransform Terminal(string title,Vector3 position,float yaw,Vector2 size,Color accent)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Canvas),typeof(Image));go.transform.SetParent(transform,false);go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));go.transform.localScale=Vector3.one*.0022f;
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=size;var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=100;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=CandyHudVisuals.Ink;image.raycastTarget=false;
            Label(rect,title,size.y*.5f-72,68,size.x-70,105).fontStyle=FontStyle.Bold;
            var stripe=new GameObject("Candy accent",typeof(RectTransform),typeof(Image));stripe.transform.SetParent(rect,false);var color=stripe.GetComponent<Image>();color.sprite=CandyHudVisuals.Pill;color.type=Image.Type.Sliced;color.color=accent;color.raycastTarget=false;color.rectTransform.sizeDelta=new Vector2(size.x-80,8);color.rectTransform.anchoredPosition=new Vector2(0,size.y*.5f-12);
            go.AddComponent<LobbyTerminalFaces>().Build(Player.viewCamera,title,accent,size);
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
