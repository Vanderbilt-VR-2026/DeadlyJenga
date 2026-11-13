using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.CandyArt.Marina
{
    // Requests still go through the real lobby. This is only its simplified front face.
    public sealed class MarinaScreen : MonoBehaviour
    {
        sealed class Target {public string id;public BoxCollider collider;public Image face;public Color color;public Action action;public Func<bool> allowed;public Text actionLabel;public Graphic glyph;public Outline outline;public Shadow glow;public float pressedUntil;}
        readonly List<Target> targets=new();
        ILobbyControls lobby;ISessionInfo session;Action<string> detail;
        bool requireRelease=true,inputFocused=true;
        bool ModalBlocked=>session?.IsVR==true&&session.MenuOpen||MenuAim.InputConsumed||!inputFocused||GameExperiencePanel.Showing(session)||CandyWildlifePanel.Showing(session)||CandyComfortPanel.Showing(session)||CharacterPicker.Showing(session);
        static readonly Color Plum=new(.12f,.055f,.19f);
        static readonly Color Cream=new(1,.96f,.83f);
        static readonly Color ProjectionInk=new(.29f,.12f,.30f);
        static readonly Color Coral=new(1f,.37f,.40f);
        static readonly Color Lilac=new(.67f,.36f,.87f);
        static readonly Color Mint=new(.26f,.84f,.64f);
        InputAction select,leftSelect;
        Image aimDot;
        Canvas aimCanvas;
        void EnsureAimCanvas()
        {
            if (!aimCanvas)
            {
                var root = new GameObject("Marina aim reticle", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(transform, false);
                aimCanvas = root.GetComponent<Canvas>();
                aimCanvas.renderMode = RenderMode.WorldSpace;
                aimCanvas.overrideSorting = true;
                aimCanvas.sortingOrder = 999;
                aimDot = MenuAim.CreateReticle(root.transform);
                MenuAim.PresentInForeground(root.transform);
            }
        }
        RectTransform canvas,mainFace,joinFace,hostFace;
        Text modeTitle,mapTitle,playTitle,joinTitle,hostTitle,focusDescription,mainHeading,playerCountLabel;
        Text liveStandings,liveActivity,liveHazards;
        readonly Text[] standingRows=new Text[5],activityRows=new Text[5],hazardRows=new Text[4];
        readonly RawImage[] standingHeads=new RawImage[5],victimHeads=new RawImage[5],killerHeads=new RawImage[5],hazardArt=new RawImage[4];
        Text modeMissing,mapMissing,joinStatus,hostStatus,marquee;
        RectTransform modeCard,mapCard;
        RawImage modePicture,mapPicture;
        Texture2D projectionBackdrop;
        string modeId,mapId,focusTarget,focusMode,focusMap;
        float refreshAt;
        RectTransform focusPanel;
        public bool Visible
        {
            get => mainFace && mainFace.gameObject.activeSelf;
            set
            {
                if (!mainFace || mainFace.gameObject.activeSelf == value) return;
                if (value) Refresh();
                mainFace.gameObject.SetActive(value);
            }
        }
        bool AnyFaceVisible => Visible || (joinFace && joinFace.gameObject.activeSelf) ||
            (hostFace && hostFace.gameObject.activeSelf);

        internal static Vector3 CabinetScreen(bool host) => new Vector3(host ? 4.87f : -4.87f, 3.28f, 10.521f);

        public void SetCabinetVisibility(bool join, bool host)
        {
            if (joinFace) joinFace.gameObject.SetActive(join);
            if (hostFace) hostFace.gameObject.SetActive(host);
        }
        public void Build(ILobbyControls controls,Action<string> openDetail)
        {
            if(canvas)return;
            lobby=controls;session=controls as ISessionInfo??Services.Session;detail=openDetail;
            var go=new GameObject("Marina · live cinema terminal",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(transform,false);
            canvas=go.GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(720,320);
            canvas.localPosition=MayhemMarina.PlayScreen+Vector3.back*.20f;canvas.localScale=Vector3.one*.0095f;
            var worldCanvas=go.GetComponent<Canvas>();worldCanvas.renderMode=RenderMode.WorldSpace;
            worldCanvas.overrideSorting=true;worldCanvas.sortingOrder=70;

            mainFace = new GameObject("Marina · map and mode display", typeof(RectTransform)).GetComponent<RectTransform>();
            mainFace.SetParent(canvas,false);
            mainFace.sizeDelta=canvas.sizeDelta;
            BuildProjectedBackdrop();
            marquee=Text("D E V E L O P E R",new Vector2(0,140),new Vector2(500,22),15,mainFace);
            marquee.color=Cream;
            marquee.gameObject.SetActive(false);
            mainHeading=Text("MATCH OPTIONS",new Vector2(0,113),new Vector2(635,54),40,mainFace);
            var ruleColor=new Color(1f,.49f,.52f,.85f);
            for(int side=-1;side<=1;side+=2)
            {
                Panel("Cinema title rule",mainFace,new Vector2(side*260,113),new Vector2(103,2),ruleColor);
                Panel("Cinema title diamond",mainFace,new Vector2(side*321,113),new Vector2(7,7),Coral)
                    .rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }

            modePicture=Card("marina-modes","GAME MODE",new Vector2(-169,12),()=>detail?.Invoke("modes"),out modeTitle,out modeMissing);
            mapPicture=Card("marina-maps","MAP",new Vector2(169,12),()=>detail?.Invoke("maps"),out mapTitle,out mapMissing);
            var play=Button("marina-play",new Vector2(0,-111),new Vector2(378,36),Coral,lobby.StartOrCancel,()=>lobby.CanStart);
            playTitle=ActionText("Start match",new Vector2(355,36),28,play);
            modeCard = targets.Find(target => target.id == "marina-modes").face.rectTransform;
            mapCard = targets.Find(target => target.id == "marina-maps").face.rectTransform;

            var settingsBtn=Button("marina-settings",new Vector2(-263,-111),new Vector2(122,34),ProjectionInk,()=>detail?.Invoke("settings"),()=>true);
            var gearGo=new GameObject("Marina · settings gear",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyTerminalGlyph));
            gearGo.layer=5;
            gearGo.transform.SetParent(settingsBtn,false);
            var gear=gearGo.GetComponent<LobbyTerminalGlyph>();
            gear.symbol=LobbyTerminalGlyph.Symbol.Gear;
            gear.color=Cream;
            gear.rectTransform.anchoredPosition=Vector2.zero;
            gear.rectTransform.sizeDelta=new Vector2(24,24);
            gear.raycastTarget=false;
            gear.UseVertexMesh();
            var settingsTarget=targets.Find(target=>target.id=="marina-settings");
            if(settingsTarget!=null)settingsTarget.glyph=gear;

            var styleBtn=Button("marina-style",new Vector2(263,-111),new Vector2(122,34),ProjectionInk,()=>detail?.Invoke("settings"),()=>true);
            playerCountLabel=ActionText("10 PLAYERS",new Vector2(113,32),17,styleBtn);

            liveStandings=LiveColumn(new Vector2(-170,20),new Vector2(313,126));
            liveActivity=LiveColumn(new Vector2(169,20),new Vector2(313,126));
            liveHazards=LiveColumn(new Vector2(0,-76),new Vector2(648,46));
            BuildLiveRows();

            joinFace=CabinetFace("Lavender Join display",false);
            hostFace=CabinetFace("Mint Host display",true);
            CabinetHeading(joinFace,"JOIN");
            CabinetHeading(hostFace,"HOST");
            joinTitle=CabinetAction(joinFace,"marina-join","Room code",17,Lilac,()=>detail?.Invoke("join-code"));
            CabinetAction(joinFace,"marina-nearby","Find nearby",-27,new Color(.34f,.17f,.45f),()=>detail?.Invoke("join-nearby"),()=>lobby.JoinEnabled);
            hostTitle=CabinetAction(hostFace,"marina-host","Create room",17,Mint,()=>detail?.Invoke("host"));
            CabinetAction(hostFace,"marina-invite","Invite nearby",-27,new Color(.29f,.18f,.41f),()=>detail?.Invoke("host-invite"),()=>lobby.CanInvite);

            joinStatus=Text("",new Vector2(0,-62),new Vector2(165,23),14,joinFace);
            hostStatus=Text("",new Vector2(0,-62),new Vector2(165,23),14,hostFace);
            focusPanel=new GameObject("Marina · selection preview",typeof(RectTransform)).GetComponent<RectTransform>();
            focusPanel.SetParent(mainFace,false);focusPanel.anchoredPosition=new Vector2(0,-78);
            Panel("Marina · preview backing",focusPanel,Vector2.zero,new Vector2(674,23),Plum);
            focusDescription=Text("",Vector2.zero,new Vector2(660,22),14,focusPanel);
            focusDescription.resizeTextForBestFit=true;focusDescription.resizeTextMinSize=12;focusDescription.resizeTextMaxSize=14;
            focusPanel.gameObject.SetActive(false);
            select=MenuAim.SelectAction("Marina right select",false);select.Enable();
            leftSelect=MenuAim.SelectAction("Marina left select",true);leftSelect.Enable();
            EnsureAimCanvas();Refresh();
        }
        void BuildProjectedBackdrop()
        {
            const int width=144,height=64;
            projectionBackdrop=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Marina projection vignette",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear
            };
            var pixels=new Color[width*height];
            var grain=new System.Random(1917);
            var edge=new Color(.16f,.045f,.18f,1);
            var center=new Color(.28f,.09f,.28f,1);
            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                float u=x/(width-1f)*2-1;
                float v=y/(height-1f)*2-1;
                float light=Mathf.Clamp01(1-.49f*u*u-.44f*v*v);
                float noise=((float)grain.NextDouble()-.5f)*.032f;
                pixels[y*width+x]=Color.Lerp(edge,center,light*light)+new Color(noise,noise,noise,0);
            }
            projectionBackdrop.SetPixels(pixels);
            projectionBackdrop.Apply(false,true);

            var backdrop=new GameObject("Marina · projected picture",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            backdrop.transform.SetParent(mainFace,false);
            backdrop.rectTransform.sizeDelta=new Vector2(720,320);
            backdrop.texture=projectionBackdrop;
            backdrop.raycastTarget=false;
            var rim=Panel("Saltwater Taffy coral trim",mainFace,Vector2.zero,new Vector2(722,322),Coral);
            rim.fillCenter=false;
            var inner=Panel("Saltwater Taffy cream pinstripe",mainFace,Vector2.zero,new Vector2(704,307),Cream);
            inner.fillCenter=false;
        }
        static Image Panel(string name,RectTransform parent,Vector2 position,Vector2 size,Color color)
        {
            var panel=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(parent,false);panel.rectTransform.anchoredPosition=position;panel.rectTransform.sizeDelta=size;
            panel.sprite=CandyHudVisuals.Pill;panel.type=Image.Type.Sliced;panel.color=color;panel.raycastTarget=false;
            return panel;
        }
        void SmallButton(string id,string title,Vector2 position,Action action)
        {
            var button=Button(id,position,new Vector2(122,34),ProjectionInk,action,()=>true);
            ActionText(title,new Vector2(113,32),17,button);
        }
        RectTransform CabinetFace(string name,bool host)
        {
            var face=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            face.SetParent(canvas,false);
            face.sizeDelta=new Vector2(175,157);
            face.localPosition=(CabinetScreen(host)-canvas.localPosition)/canvas.localScale.x;
            return face;
        }
        static void CabinetHeading(RectTransform parent,string title)
        {
            Panel("Candy cabinet marquee",parent,new Vector2(0,59),new Vector2(167,32),Plum);
            var label=Text(title,new Vector2(0,59),new Vector2(165,34),21,parent);
            label.color=Cream;
        }
        Text CabinetAction(RectTransform parent,string id,string title,float y,Color color,Action action,Func<bool> allowed=null)
        {
            var button=Button(id,new Vector2(0,y),new Vector2(154,36),color,action,allowed??(()=>true),parent);
            return ActionText(title,new Vector2(147,36),18,button);
        }
        RawImage Card(string id,string section,Vector2 position,Action action,out Text title,out Text missing)
        {
            var button=Button(id,position,new Vector2(321,152),(id=="marina-modes"?Lilac:Mint),action,()=>GameExperiencePanel.CanEditRawChoices(session));
            Panel("Marina · illustration panel",button,Vector2.zero,new Vector2(313,144),ProjectionInk);
            var crop=Panel("Marina · illustration crop",button,new Vector2(0,12),new Vector2(309,116),Color.white);
            crop.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var go=new GameObject("Live destination image",typeof(RectTransform),typeof(RawImage));
            go.transform.SetParent(crop.transform,false);var image=go.GetComponent<RawImage>();
            image.rectTransform.sizeDelta=crop.rectTransform.sizeDelta;image.raycastTarget=false;
            image.color=new Color(.96f,.93f,.86f);
            missing=Text("Preview unavailable",new Vector2(0,12),new Vector2(285,32),17,button);
            Panel("Marina · category badge",button,new Vector2(-91,56),new Vector2(117,23),ProjectionInk);
            Text(section,new Vector2(-91,56),new Vector2(113,23),13,button);
            title=Text("",new Vector2(0,-59),new Vector2(299,25),22,button);
            title.resizeTextForBestFit=true;title.resizeTextMinSize=14;title.resizeTextMaxSize=22;
            return image;
        }
        RectTransform Button(string id,Vector2 p,Vector2 size,Color color,Action action,Func<bool> allowed,RectTransform parent=null)
        {
            var go=new GameObject(id,typeof(RectTransform),typeof(Image),typeof(BoxCollider));go.layer=5;go.transform.SetParent(parent?parent:mainFace,false);var r=go.GetComponent<RectTransform>();r.anchoredPosition=p;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.sprite=CandyHudVisuals.Pill;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            var hit=go.GetComponent<BoxCollider>();hit.isTrigger=true;hit.size=new Vector3(size.x,size.y,6);var outline=go.AddComponent<Outline>();outline.effectDistance=new Vector2(4,-4);outline.effectColor=Color.clear;
            var glow=go.AddComponent<Shadow>();glow.effectDistance=new Vector2(0,-8);glow.effectColor=Color.clear;
            targets.Add(new Target{id=id,collider=hit,face=image,color=color,action=action,allowed=allowed,outline=outline,glow=glow});return r;
        }
        Text ActionText(string value,Vector2 size,int font,RectTransform button)
        {
            var label=Text(value,Vector2.zero,size,font,button);
            var target=targets.Find(item=>item.face.rectTransform==button);
            if(target!=null)target.actionLabel=label;
            label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=font;
            return label;
        }
        static Text Text(string value,Vector2 p,Vector2 size,int font,Transform parent)
        {
            var go=new GameObject("Marina label · "+value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=font;t.fontStyle=FontStyle.Bold;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(1,.97f,.89f);t.raycastTarget=false;t.rectTransform.anchoredPosition=p;t.rectTransform.sizeDelta=size;return t;
        }
        Text playlistStatus;
        Image playlistStatusBacking;
        void Refresh()
        {
            if(!canvas||lobby==null)return;
            modeTitle.text=lobby.ModeTitle;mapTitle.text=lobby.MapTitle;
            var experience=GameExperiencePanel.Resolve(session);
            bool visiting=(lobby as ILobbyMatchVisitControls)?.VisitingLobby==true;
            bool playlist=experience?.Experience==GameExperience.Game&&!experience.CanConfigureExperience;
            if (!playlistStatus)
            {
                playlistStatusBacking=Panel("Play status backing",mainFace,new Vector2(0,-78),new Vector2(350,25),ProjectionInk);
                playlistStatus=Text("",new Vector2(0,-78),new Vector2(340,25),17,mainFace);
            }
            int countdownSeconds = 0;
            bool countdown = false;
            if (playlist && experience.AutomaticRounds && !lobby.CanStart && experience.GamePhase == RoomGamePhase.Selected)
            {
                countdownSeconds = Mathf.Max(0, Mathf.CeilToInt(experience?.CountdownSeconds ?? 0));
                countdown = countdownSeconds <= 10;
            }
            else if (lobby.StartPending)
            {
                countdown = true;
                countdownSeconds = 3;
                if (!string.IsNullOrEmpty(lobby.SetupHint) && lobby.SetupHint.StartsWith("MATCH STARTING IN ", StringComparison.Ordinal))
                {
                    var parts = lobby.SetupHint.Split(' ');
                    if (parts.Length >= 4 && int.TryParse(parts[3].TrimEnd('.'), out int parsed))
                        countdownSeconds = Mathf.Max(1, parsed);
                }
            }

            mainHeading.text=visiting?"SPECTATOR LOBBY":countdown?$"MATCH STARTING IN {countdownSeconds}...":playlist?"PLAY READY":"MATCH OPTIONS";
            marquee.gameObject.SetActive(false);
            playlistStatusBacking.gameObject.SetActive((playlist||countdown)&&!visiting);
            playlistStatusBacking.color=countdown?Coral:ProjectionInk;
            playlistStatusBacking.rectTransform.anchoredPosition=new Vector2(0,countdown?68:-78);
            playlistStatusBacking.rectTransform.sizeDelta=new Vector2(countdown?236:350,countdown?41:25);
            playlistStatus.rectTransform.anchoredPosition=playlistStatusBacking.rectTransform.anchoredPosition;
            playlistStatus.rectTransform.sizeDelta=playlistStatusBacking.rectTransform.sizeDelta;
            playlistStatus.fontSize=countdown?30:17;
            playlistStatus.gameObject.SetActive((playlist||countdown)&&!visiting);
            RefreshLiveMatch(visiting);
            int seconds=countdown?countdownSeconds:Mathf.Max(0,Mathf.CeilToInt(experience?.CountdownSeconds??0));
            int humans=Mathf.Max(1,session?.Multiplayer?.HumanCount??1);
            playlistStatus.text=visiting?"Live match on the spectator TVs":countdown?
                $"{seconds}s":
                lobby.Connected?$"ROOM {lobby.HostCode}    |    {humans} / {Mathf.Max(10,humans)}":
                "LOCAL PLAY";
            int totalPlayers=(lobby as ILobbyDetails)?.Setup?.population??MatchChoices.BotTarget;
            if(totalPlayers<=0)totalPlayers=10;
            if(playerCountLabel)playerCountLabel.text=$"{totalPlayers} PLAYERS";
            modeCard.gameObject.SetActive(!visiting&&(!playlist||countdown));
            mapCard.gameObject.SetActive(!visiting);
            modeCard.anchoredPosition=new Vector2(-169,countdown?-41:12);
            mapCard.anchoredPosition=new Vector2(playlist&&!countdown?0:169,countdown?-41:12);
            foreach(var target in targets)
            {
                if(target.id is "marina-settings" or "marina-style")
                    target.face.gameObject.SetActive(!playlist&&!visiting&&!countdown);
                if(target.id=="marina-play")
                    target.face.gameObject.SetActive(!visiting&&(!countdown||(lobby.StartPending&&lobby.CanStart)));
            }
            playTitle.text=visiting?(lobby.CanStart?"Return to match":"WATCH LIVE"):
                playlist?(experience.LocalReady?"READY":"GO"):lobby.StartPending?"Cancel start":"Start match";
            joinTitle.text=visiting?"Room info":"Room code";
            hostTitle.text=visiting?"Room info":lobby.Connected?(lobby.IsHost?"Manage room":"Create room"):"Create room";
            joinStatus.text=visiting?"Match in progress":lobby.Busy?"Connecting…":lobby.Connected?"In a room":"Find your friends";
            hostStatus.text=visiting?"Match in progress":lobby.Busy?"Working…":lobby.IsHost&&lobby.Connected?"You are host":lobby.Connected?"Joined as guest":"Host your friends";
            var details=lobby as ILobbyDetails;
            string nextMode=details?.SelectedModeId??"mystery",nextMap=details?.SelectedMapId??"castle";
            if(modeId!=nextMode)
            {
                modeId=nextMode;
                modePicture.texture=Resources.Load<Texture2D>("LobbyModePictures/"+modeId)??Resources.Load<Texture2D>("LobbyModePictures/demo");
                FitPreview(modePicture,modeMissing);
            }
            if(mapId!=nextMap){mapId=nextMap;mapPicture.texture=MapPreviewImages.Get(mapId);FitPreview(mapPicture,mapMissing);}
        }
        Text LiveColumn(Vector2 position,Vector2 size)
        {
            var label=Text("",position,size,17,mainFace);
            label.alignment=TextAnchor.UpperLeft;label.color=Cream;
            return label;
        }

        void BuildLiveRows()
        {
            liveStandings.text="STANDINGS";
            liveActivity.text="RECENT ELIMINATIONS";
            for(int i=0;i<standingRows.Length;i++)
            {
                float y=32-i*19;
                standingHeads[i]=LiveImage(liveStandings.transform,new Vector2(-145,y),new Vector2(18,18));
                standingRows[i]=Text("",new Vector2(13,y),new Vector2(285,19),15,liveStandings.transform);
                standingRows[i].alignment=TextAnchor.MiddleLeft;
                killerHeads[i]=LiveImage(liveActivity.transform,new Vector2(-145,y),new Vector2(18,18));
                victimHeads[i]=LiveImage(liveActivity.transform,new Vector2(-124,y),new Vector2(18,18));
                activityRows[i]=Text("",new Vector2(25,y),new Vector2(259,19),15,liveActivity.transform);
                activityRows[i].alignment=TextAnchor.MiddleLeft;
            }
            for(int i=0;i<hazardRows.Length;i++)
            {
                float x=-318+i*162;
                hazardArt[i]=LiveImage(liveHazards.transform,new Vector2(x+25,-4),new Vector2(48,30));
                hazardRows[i]=Text("",new Vector2(x+105,-4),new Vector2(105,32),14,liveHazards.transform);
                hazardRows[i].alignment=TextAnchor.MiddleLeft;
            }
        }
        static RawImage LiveImage(Transform parent,Vector2 position,Vector2 size)
        {
            var image=new GameObject("Live match art",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent,false);
            image.rectTransform.anchoredPosition=position;
            image.rectTransform.sizeDelta=size;
            image.raycastTarget=false;
            return image;
        }
        static void SetLiveImage(RawImage image,Texture texture)
        {
            image.texture=texture;
            image.enabled=texture;
        }
        void RefreshLiveMatch(bool visible)
        {
            liveStandings.gameObject.SetActive(visible);
            liveActivity.gameObject.SetActive(visible);
            liveHazards.gameObject.SetActive(visible);
            var match=MatchRuntime.Current;
            var frame=visible?match?.Frame:null;
            if(frame==null)return;
            int seconds=Mathf.Max(0,Mathf.CeilToInt(frame.remaining));
            mainHeading.text=$"LIVE MATCH    {seconds/60:00}:{seconds%60:00}";
            var owner=match as Component;
            var portraits=owner?CandyScorePortraits.For(owner.gameObject):null;
            Texture Portrait(MatchPlayerFrame player)=>player==null?null:portraits?.Get(match,player);
            MatchPlayerFrame Player(string id)=>string.IsNullOrEmpty(id)?null:Array.Find(frame.players,row=>row.id==id);
            string Name(MatchPlayerFrame player)=>CandyHudContent.Safe(player?.name??"Player",18);
            var standings=CandyResultsHud.Rank(frame.players.Where(player=>player.role!="enemy"),false,false).Take(5).ToArray();
            var eliminations=frame.eliminations.Reverse().Take(5).ToArray();
            for(int i=0;i<standingRows.Length;i++)
            {
                var standing=i<standings.Length?standings[i]:null;
                SetLiveImage(standingHeads[i],Portrait(standing));
                standingRows[i].text=standing==null?"":$"{i+1}. {Name(standing)}  {CandyResultsHud.RankStats(standing,frame.rules.mode,false)}";
                var killer=i<eliminations.Length?Player(eliminations[i].killer):null;
                var victim=i<eliminations.Length?Player(eliminations[i].victim):null;
                SetLiveImage(killerHeads[i],Portrait(killer));
                SetLiveImage(victimHeads[i],Portrait(victim));
                activityRows[i].text=i>=eliminations.Length?"":killer==null?Name(victim)+" fell":Name(killer)+" > "+Name(victim);
            }
            int slot=0;
            foreach(var source in Tracked.Waves)
            {
                var wave=source.Wave;
                if(wave.active){SetHazard(slot++,"Tsunami","Tsunami",wave.warningRemaining);break;}
            }
            foreach(var source in DisasterStates.Active)
            {
                if(slot>=hazardRows.Length)break;
                var hazard=source.Capture();
                if(hazard==null||hazard.residualOnly)continue;
                SetHazard(slot++,hazard.kind,hazard.label??hazard.kind,hazard.warning);
            }
            if(slot<hazardRows.Length&&frame.disasterWarningRemaining>0&&!string.IsNullOrEmpty(frame.disasterWarning))
                SetHazard(slot++,frame.disasterWarning,CandyDisasterWarningCard.DisplayName(frame.disasterWarning),frame.disasterWarningRemaining);
            liveHazards.text=slot==0?"DISASTERS   None active or incoming":"DISASTERS";
            for(;slot<hazardRows.Length;slot++)
            {
                SetLiveImage(hazardArt[slot],null);
                hazardRows[slot].text="";
            }
        }
        void SetHazard(int slot,string kind,string title,float warning)
        {
            SetLiveImage(hazardArt[slot],CandyDisasterWarningCard.GetImage(kind)??CandyDisasterWarningCard.GetImage(title));
            hazardRows[slot].text=CandyHudContent.Safe(title,24)+(warning>0?$" in {Mathf.CeilToInt(warning)}s":" active");
        }

        static void FitPreview(RawImage image,Text missing)
        {
            image.enabled=image.texture;
            missing.gameObject.SetActive(!image.texture);
            if(!image.texture)return;
            float source=(float)image.texture.width/image.texture.height;
            float target=image.rectTransform.sizeDelta.x/image.rectTransform.sizeDelta.y;
            float width=Mathf.Min(1,target/source),height=Mathf.Min(1,source/target);
            image.uvRect=new Rect((1-width)*.5f,(1-height)*.5f,width,height);
        }
        public bool Invoke(string id) => Activate(targets.Find(target => target.id == id), 0);

        bool Activate(Target target, int pointerDevice)
        {
            if (ModalBlocked || requireRelease || target == null ||
                !target.face.gameObject.activeInHierarchy || !target.allowed()) return false;
            target.pressedUntil = Time.unscaledTime + .16f;
            PaintTarget(target, true, true);
            requireRelease = true;
            MenuAim.Consume();
            MenuAim.PulsePress(pointerDevice);
            target.action();
            return true;
        }

        static void PaintTarget(Target target, bool hover, bool allowed)
        {
            bool pressed = allowed && Time.unscaledTime < target.pressedUntil;
            bool readOnlyPreview = target.id is "marina-maps" or "marina-modes";
            float scale = pressed ? .96f : hover && (allowed || readOnlyPreview) ? (readOnlyPreview ? 1.075f : 1.035f) : 1f;
            target.face.rectTransform.localScale = Vector3.one * scale;
            allowed |= readOnlyPreview;
            target.face.color = !allowed ? Color.Lerp(target.color, Plum, .70f) :
                pressed ? Color.Lerp(target.color, Mint, .70f) :
                hover ? Color.Lerp(target.color, readOnlyPreview ? Color.white : Cream, .40f) : target.color;
            var rim = pressed ? Cream : allowed ? CandyHudVisuals.Mint : Cream;
            float outline = hover || pressed ? 1 : 0;
            if (readOnlyPreview && hover)
            {
                target.outline.effectDistance = new Vector2(8, -8);
                target.outline.effectColor = new Color(.35f, 1f, .85f, 1f);
                target.glow.effectDistance = new Vector2(0, -12);
                target.glow.effectColor = new Color(.35f, 1f, .85f, .75f);
            }
            else
            {
                target.outline.effectDistance = new Vector2(4, -4);
                target.outline.effectColor = new Color(rim.r, rim.g, rim.b, outline);
                target.glow.effectDistance = new Vector2(0, -8);
                target.glow.effectColor = new Color(rim.r, rim.g, rim.b, outline * (pressed ? .85f : .50f));
            }
            if(target.actionLabel)
            {
                Color face=target.face.color.linear;
                float luminance=.2126f*face.r+.7152f*face.g+.0722f*face.b;
                target.actionLabel.color=allowed?(luminance>.22f?Plum:Cream):CandyHudVisuals.Muted;
            }
            if(target.glyph)
            {
                Color face=target.face.color.linear;
                float luminance=.2126f*face.r+.7152f*face.g+.0722f*face.b;
                target.glyph.color=allowed?(luminance>.22f?Plum:Cream):CandyHudVisuals.Muted;
            }
        }
        LineRenderer aimBeam;
        void LateUpdate()
        {
            if (aimBeam) aimBeam.enabled = false;
            if(!AnyFaceVisible||ModalBlocked||!NetworkActors.Alive(session?.Player)||!session.Player.gameObject.activeInHierarchy){requireRelease=true;if(aimDot)aimDot.gameObject.SetActive(false);if(focusPanel)focusPanel.gameObject.SetActive(false);return;}if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.2f;Refresh();}
            var player=session?.Player;if(!NetworkActors.Alive(player)||!player.ViewCamera){aimDot.gameObject.SetActive(false);return;}
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            bool held=player.IsVR ? select.IsPressed() || leftSelect.IsPressed() || player.SelectHeld :
                !lobby.CodeFocused && keyboard?.eKey.isPressed==true || mouse?.leftButton.isPressed==true;
            if(requireRelease&&!held)requireRelease=false;
            bool pressed=player.IsVR ? player.SelectPressed :
                !lobby.CodeFocused && keyboard?.eKey.wasPressedThisFrame==true || mouse?.leftButton.wasPressedThisFrame==true;
            int pointerDevice = 0;
            var camera=player.ViewCamera;
            Ray ray;
            bool trackedPointer = false;
            if (player.IsVR)
            {
                bool preferredLeft=player.gameObject.GetComponent<IBlasterControls>()?.LeftHanded==true;
                ray=MenuAim.Resolve(camera,camera.transform.parent,preferredLeft,false,false,out trackedPointer,out _);
                if(!trackedPointer && (lobby as ILobbyMatchVisitControls)?.VisitingLobby==true &&
                    MenuAim.TryHandPointer(camera,player,preferredLeft,out var visitorHandRay))ray=visitorHandRay;
                trackedPointer |= MenuAim.IsTrackedPointer(camera, ray);
                var controllerAction=leftSelect.WasPressedThisFrame()?leftSelect:select.WasPressedThisFrame()?select:
                    leftSelect.IsPressed()&&(!select.IsPressed()||preferredLeft)?leftSelect:select;
                if (controllerAction.IsPressed() || controllerAction.WasPressedThisFrame())
                {
                    if (MenuAim.TryActionPointer(camera,controllerAction,out var controllerRay,out pointerDevice))
                    {
                        ray=controllerRay;
                        trackedPointer=true;
                        pressed=controllerAction.WasPressedThisFrame();
                    }
                    else pressed=false;
                }
            }
            else
                ray=session.MenuOpen&&mouse!=null?camera.ScreenPointToRay(mouse.position.ReadValue()):new Ray(camera.transform.position,camera.transform.forward);
            float screenRange = 11;
            if (Physics.Raycast(ray, out var obstruction, screenRange, 1 << Layers.Surface, QueryTriggerInteraction.Ignore))
                screenRange = Mathf.Min(screenRange,obstruction.distance+.05f);
            var nearest = PickTarget(ray, screenRange);
            foreach (var target in targets)
                if (target.face.gameObject.activeInHierarchy) PaintTarget(target, target == nearest, target.collider.enabled);
            PreviewDestination(nearest?.id);
            bool hitFace = nearest != null && MenuAim.TryPoint(ray, nearest.face.rectTransform, screenRange, out _, out var point) ||
                VisibleFacePoint(ray, mainFace, screenRange, out point) ||
                VisibleFacePoint(ray, joinFace, screenRange, out point) ||
                VisibleFacePoint(ray, hostFace, screenRange, out point);
            bool actionable = nearest != null && nearest.collider.enabled;
            EnsureAimCanvas();
            aimCanvas.worldCamera = camera;
            bool showReticle = hitFace && (trackedPointer || !player.IsVR);
            if (showReticle)
            {
                float hitDistance = Vector3.Distance(ray.origin, point);
                var reticleTransform = aimCanvas.transform;
                reticleTransform.SetPositionAndRotation(point, camera.transform.rotation);
                reticleTransform.localScale = Vector3.one * Mathf.Clamp(hitDistance * .0007f, .0007f, .003f);
                MenuAim.Present(aimDot, point, true, actionable);
                if (aimDot) aimDot.rectTransform.sizeDelta = actionable ? new Vector2(18, 18) : new Vector2(12, 12);
            }
            else if (!player.IsVR && camera != null)
            {
                var centerPoint = ray.GetPoint(1.8f);
                aimCanvas.transform.SetPositionAndRotation(centerPoint, camera.transform.rotation);
                aimCanvas.transform.localScale = Vector3.one * 0.0013f;
                MenuAim.Present(aimDot, centerPoint, true, false);
                if (aimDot) aimDot.rectTransform.sizeDelta = new Vector2(12, 12);
            }
            else if (aimDot) aimDot.gameObject.SetActive(false);
            MenuAim.PresentBeam(ref aimBeam, transform, ray, point - ray.direction * .012f,
                trackedPointer && hitFace && nearest != null, actionable);
            if (pressed && !requireRelease && nearest != null) Activate(nearest, pointerDevice);
        }
        Target PickTarget(Ray ray, float range)
        {
            Target nearest = null;
            foreach (var target in targets)
            {
                if (!target.face.gameObject.activeInHierarchy) continue;
                target.collider.enabled = target.allowed();
                // A disabled control still owns its area. Never click through it to another button.
                if (!MenuAim.TryPoint(ray, target.face.rectTransform, range, out _, out var hit)) continue;
                nearest = target;
                range = Vector3.Distance(ray.origin, hit);
            }
            return nearest;
        }

        static bool VisibleFacePoint(Ray ray, RectTransform face, float distance, out Vector3 point)
        {
            point = default;
            return face.gameObject.activeInHierarchy && MenuAim.TryPoint(ray, face, distance, out _, out point);
        }

        void PreviewDestination(string id)
        {
            bool map=id=="marina-maps",mode=id=="marina-modes";
            bool blockedStart=id=="marina-play"&&!lobby.CanStart;
            focusPanel.gameObject.SetActive(map||mode||blockedStart);
            if(blockedStart)
            {
                focusTarget=null;
                focusDescription.text=lobby.Busy?"Waiting for the room to finish connecting.":
                    !lobby.IsHost?"Only the host can start the match.":"The match is not ready to start.";
                return;
            }
            if(!map&&!mode)return;
            if(!GameExperiencePanel.CanEditRawChoices(session))
            {
                focusTarget=null;
                focusDescription.text=lobby.IsHost?"This match chooses its mode and map automatically.":"The host chooses the mode and map.";
                return;
            }
            if(focusTarget==id&&focusMode==modeId&&focusMap==mapId)return;
            focusTarget=id;focusMode=modeId;focusMap=mapId;
            var details=lobby as ILobbyDetails;string selected=map?mapId:modeId;
            var options=map?details?.Maps:details?.Modes;string description="";
            if(options!=null)foreach(var option in options)if(option.id==selected){description=option.description;break;}
            if(string.IsNullOrWhiteSpace(description))description=map?"Browse maps. Point to a map for its picture and description; select to use it.":"Browse modes. Point to a mode to preview it; select to use it and choose a map.";
            focusDescription.text=description.Length>150?description.Substring(0,147)+"...":description;
        }
        void OnEnable(){requireRelease=true;select?.Enable();leftSelect?.Enable();}
        void OnApplicationFocus(bool focused){inputFocused=focused;requireRelease=true;}
        void OnDisable(){if(aimBeam)aimBeam.enabled=false;select?.Disable();leftSelect?.Disable();if(aimDot)aimDot.gameObject.SetActive(false);}
        void OnDestroy(){select?.Dispose();leftSelect?.Dispose();if(canvas)Destroy(canvas.gameObject);if(projectionBackdrop)Destroy(projectionBackdrop);}
    }
}
