using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        MayhemMarina marina;
        MarinaScreen marinaScreen;
        RectTransform marinaBack,marinaMirror,marinaStylePrompt;
        Texture2D marinaMaskTexture;
        Sprite marinaMaskSprite;
        float nextMarinaMirror;
        bool UsesMayhemMarina => lobby is IWorldQuery world && world.Radius >= 30;
        partial void BuildMayhemMarina()
        {
            if (!UsesMayhemMarina) return;
            if(marina)return;
            foreach(var resort in GetComponents<CandyLobbyResort>())
            {
                resort.enabled=false;
                var old=resort.transform.Find("Visual · Candy island resort");if(old)old.gameObject.SetActive(false);
            }
            foreach(string name in new[]{"Grounded red arcade cabinet","JOIN monitor support","Host controller computer","Mode controller computer"})
            {
                var old=transform.Find(name);if(old)old.gameObject.SetActive(false);
            }
            marina=gameObject.AddComponent<MayhemMarina>();marina.Build(lobby,Environment);
            foreach(float x in new[]{-4.87f,4.87f})
                lobby.AddBlocker(marina.transform,new Vector3(x,3.08f,11.13f),new Vector3(2.08f,2.48f,.96f));
            wardrobeCenter=MayhemMarina.Wardrobe;
            var pod=transform.Find("Visual - wardrobe pod");if(pod)pod.gameObject.SetActive(false);
            var detailCenter = new Vector3(0, 3.85f, 10.88f);
            MountMarinaScreen(joinStation, MarinaScreen.CabinetScreen(false), Quaternion.identity, 1.69f, 1.55f);
            MountMarinaScreen(hostStation, MarinaScreen.CabinetScreen(true), Quaternion.identity, 1.69f, 1.55f);
            BuildMarinaRoomScreens();
            MountMarinaScreen(setupStation, detailCenter, Quaternion.identity, 6.4f, 2.95f);
            MountMarinaScreen(wardrobePanel, MayhemMarina.StyleScreen, MayhemMarina.StyleScreenFacing, 2.55f, 2.75f);
            BuildKeyboardGrabBar();
            marinaScreen=gameObject.AddComponent<MarinaScreen>();marinaScreen.Build(lobby,OpenMarinaDetail);
            if(wardrobeCamera)wardrobeCamera.backgroundColor=new Color(.10f,.28f,.34f);
            var style = new GameObject("Marina wardrobe entry", typeof(RectTransform), typeof(Canvas));
            style.transform.SetParent(transform, false);
            marinaStylePrompt = style.GetComponent<RectTransform>();
            marinaStylePrompt.SetLocalPositionAndRotation(MayhemMarina.StyleScreen, MayhemMarina.StyleScreenFacing);
            marinaStylePrompt.localScale = Vector3.one * .0025f;
            marinaStylePrompt.sizeDelta = new Vector2(780, 140);
            style.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            Add(marinaStylePrompt, "marina-open-style", () => "OPEN WARDROBE", 0, () => OpenMarinaDetail("style"), () => true, 0, 780, 140);
            GameEvents.Emoted += MarinaActivityArt;
            BuildMarinaMirror();CloseMarinaDetail();
        }
        static void MountMarinaScreen(RectTransform screen, Vector3 position, Quaternion rotation, float maxWidth, float maxHeight)
        {
            screen.SetLocalPositionAndRotation(position, rotation);
            screen.localScale = Vector3.one * Mathf.Min(maxWidth / screen.rect.width, maxHeight / screen.rect.height);
            screen.GetComponent<LobbyTerminalFaces>().MountSingleSided();
        }
        enum CabinetSurface { Glass, Glaze, Rim, Neon, Bezel }
        readonly System.Collections.Generic.List<Sprite> marinaRoomArt=new();
        readonly Text[] marinaCodeSlots=new Text[6];
        static readonly Color RoomPlum=new(.35f,.135f,.24f);
        static readonly Color RoomPurple=new(.86f,.49f,.87f);
        static readonly Color RoomMint=new(.34f,.85f,.65f);
        static readonly Color RoomCoral=new(1f,.37f,.40f);
        RectTransform marinaJoinScreen,marinaHostScreen;
        Text marinaHostCode,marinaHostCount,marinaJoinRegion,marinaHostRegion,marinaJoinMessage;
        Font marinaRoomFont;
        bool marinaHostOptions;
        RectTransform marinaJoinVisit,marinaHostVisit;
        string marinaDisplayedHostCode;
        RectTransform marinaHostWarningPanel,marinaJoinWarningPanel;
        System.Action pendingJoinAction;
        bool openedHostForConfirmation;

        bool ConfirmingJoin => marinaJoinWarningPanel && marinaJoinWarningPanel.gameObject.activeSelf;
        bool ConfirmingHostStation => marinaHostWarningPanel && marinaHostWarningPanel.gameObject.activeSelf;

        void BuildMarinaRoomScreens()
        {
            marinaRoomFont=Resources.Load<Font>("Fonts/Fredoka-Bold");
            var rim=RoomSprite(1100,880,92,CabinetSurface.Bezel);
            marinaJoinScreen=RoomScreen(joinStation,candyJoinContent,"Marina Join code screen",rim,new Color(.88f,.53f,.97f));
            marinaHostScreen=RoomScreen(hostStation,candyHostContent,"Marina Host room screen",rim,new Color(.36f,.93f,.72f));
            var glaze=RoomSprite(754,136,66,CabinetSurface.Glaze);
            var token=RoomSprite(132,132,65,CabinetSurface.Glaze);
            var linkArt=RoomSprite(280,56,25,CabinetSurface.Glaze);

            RoomLabel(marinaJoinScreen,"JOIN",0,325,720,126,120).name="Join room heading";
            var code=RoomButton(marinaJoinScreen,"marina-code-focus",()=>"",0,99,955,245,
                RoomSprite(955,245,68,CabinetSurface.Neon),new Color(1,.59f,1),lobby.FocusCode,()=>lobby.JoinEnabled);
            // Inset buttons are graphics on the code field so they share one input target.
            for(int i=0;i<marinaCodeSlots.Length;i++)
            {
                float x=(i-2.5f)*140;
                var slot=RoomImage((RectTransform)code.button.transform,"Room code token "+(i+1),token,
                    new Color(.82f,.50f,.81f),x,-3,132,132);
                marinaCodeSlots[i]=RoomLabel(slot.rectTransform,"",0,1,113,103,83);
            }
            RoomLabel(marinaJoinScreen,"Room code",0,223,330,61,54);
            marinaJoinRegion=RoomLabel(marinaJoinScreen,"",354,183,130,36,25);
            RoomButton(marinaJoinScreen,"marina-submit-code",()=>"Join",0,-153,754,136,glaze,RoomPurple,
                RequestJoinCodeChoice,()=>lobby.JoinEnabled&&lobby.CodeValid&&!ConfirmingJoin,88);
            var nearby=RoomButton(marinaJoinScreen,"marina-find-nearby",()=>"Find nearby",0,-269,395,67,linkArt,
                Color.white,lobby.DetectNearby,()=>lobby.JoinEnabled&&!ConfirmingJoin,47,true);
            RoomLine((RectTransform)nearby.button.transform,0,-25,202,new Color(1,.91f,.73f,.85f));
            marinaJoinMessage=RoomLabel(marinaJoinScreen,"",0,-352,1000,59,28);

            RoomLabel(marinaHostScreen,"HOST",-158,313,530,112,108).name="Host room heading";
            var badge=RoomImage(marinaHostScreen,"Connected player badge",RoomSprite(302,88,44,CabinetSurface.Glaze),
                new Color(.48f,.22f,.34f),338,318,302,88).rectTransform;
            CandyIcon(badge,LobbyTerminalGlyph.Symbol.Person,-95,0,53,CandyCream);
            marinaHostCount=RoomLabel(badge,"",29,1,207,67,54);
            RoomLabel(marinaHostScreen,"Room code",0,212,310,56,48);
            RoomLine(marinaHostScreen,-286,213,208,new Color(1,.84f,.87f));
            RoomLine(marinaHostScreen,286,213,208,new Color(1,.84f,.87f));
            var hostFrame=RoomImage(marinaHostScreen,"Host code outline",RoomSprite(850,146,54,CabinetSurface.Rim),
                new Color(1,.82f,.89f),0,103,850,146).rectTransform;
            marinaHostCode=RoomLabel(hostFrame,"",0,1,802,123,96);
            marinaHostRegion=RoomLabel(hostFrame,"",339,-49,119,28,23);
            var copy=RoomButton(marinaHostScreen,"marina-copy-code",()=>Time.unscaledTime<copiedHostCodeUntil?"Copied":"Copy Code",
                0,-77,804,137,glaze,RoomMint,()=>
                {
                    GUIUtility.systemCopyBuffer=lobby.HostCode;
                    copiedHostCodeUntil=Time.unscaledTime+1.5f;
                },()=>lobby.Connected&&!lobby.Busy,61);
            var invite=RoomButton(marinaHostScreen,"marina-invite-nearby",()=>"Invite Nearby",0,-237,804,137,glaze,
                new Color(.57f,.29f,.64f),lobby.InviteNearby,()=>lobby.CanInvite,60);
            CandyChoiceIcon(copy,LobbyTerminalGlyph.Symbol.Copy,-247,0,67);
            CandyChoiceIcon(invite,LobbyTerminalGlyph.Symbol.Invite,-247,0,67);
            CandyText(copy,37,0,622,120,61);
            CandyText(invite,40,0,627,120,60);
            RoomButton(marinaHostScreen,"marina-host-options",()=>RequestId!=null?"!":"…",
                462,322,66,60,linkArt,Color.white,()=>{marinaHostOptions=true;UpdateMarinaRoomScreens();},
                ()=>lobby.Connected&&!lobby.Busy,40,true);
            RoomButton(candyHostContent,"marina-host-summary",()=>"Room code",403,365,190,54,linkArt,
                Color.white,()=>{marinaHostOptions=false;UpdateMarinaRoomScreens();},()=>true,29,true).visible=
                ()=>marinaHostOptions&&lobby.Connected&&!lobby.Busy;

            marinaJoinVisit=BuildRoomVisitNotice(joinStation,candyJoinContent,"JOIN");
            marinaHostVisit=BuildRoomVisitNotice(hostStation,candyHostContent,"HOST");
            foreach(var pair in new[]{(joinStation,candyJoinContent,"marina-close-join"),(hostStation,candyHostContent,"marina-close-host")})
            {
                var controls=new GameObject("Marina cabinet navigation",typeof(RectTransform)).GetComponent<RectTransform>();
                controls.SetParent(pair.Item1,false);
                controls.sizeDelta=pair.Item2.sizeDelta;
                controls.localScale=pair.Item2.localScale;
                RoomButton(controls,pair.Item3,()=>"‹",-461,333,76,71,token,
                    new Color(.54f,.30f,.49f),()=>SetMarinaDetail(pair.Item1,false),()=>true,52);
            }
            BuildMarinaWarningDialogs(glaze);
            UpdateMarinaRoomScreens();
        }

        void BuildMarinaWarningDialogs(Sprite buttonArt)
        {
            marinaHostWarningPanel = BuildTerminalWarningDialog(hostStation,
                "Host warning dialog",
                RoomCoral,
                "LEAVE LOBBY?",
                "Are you sure you want to leave this lobby and host your own?",
                "station-host-yes",
                "station-host-no",
                buttonArt,
                ConfirmHostFromStation,
                CancelHostFromStation);

            marinaJoinWarningPanel = BuildTerminalWarningDialog(joinStation,
                "Join warning dialog",
                RoomPurple,
                "LEAVE LOBBY?",
                "Are you sure you want to leave this lobby and join another?",
                "station-join-yes",
                "station-join-no",
                buttonArt,
                ConfirmJoinFromStation,
                CancelJoinFromStation);
        }

        RectTransform BuildTerminalWarningDialog(RectTransform station, string title, Color accent,
            string heading, string message, string yesId, string noId, Sprite buttonArt,
            System.Action onYes, System.Action onNo)
        {
            var panel = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(station, false);
            panel.sizeDelta = new Vector2(1000, 800);
            panel.localScale = Vector3.one;
            panel.anchoredPosition = Vector2.zero;

            RoomImage(panel, "Dimmer", null, new Color(0.08f, 0.04f, 0.12f, 0.92f), 0, 0, 1000, 800);
            RoomImage(panel, "Dialog glass", RoomSprite(880, 520, 56, CabinetSurface.Glass), RoomPlum, 0, 0, 880, 520);
            RoomImage(panel, "Dialog rim", RoomSprite(880, 520, 56, CabinetSurface.Rim), accent, 0, 0, 880, 520);

            var head = RoomLabel(panel, heading, 0, 160, 780, 72, 54);
            head.color = accent;

            var prompt = RoomLabel(panel, message, 0, 30, 780, 140, 36);
            prompt.color = CandyCream;
            prompt.lineSpacing = 1.15f;

            RoomButton(panel, yesId, () => "YES", -200, -140, 320, 96, buttonArt, accent, onYes, () => true, 46);
            RoomButton(panel, noId, () => "NO", 200, -140, 320, 96, buttonArt, new Color(0.42f, 0.20f, 0.38f), onNo, () => true, 46);

            panel.gameObject.SetActive(false);
            return panel;
        }

        void PromptHostFromStation()
        {
            if (marinaHostWarningPanel)
            {
                marinaHostWarningPanel.SetAsLastSibling();
                marinaHostWarningPanel.gameObject.SetActive(true);
            }
        }

        void ConfirmHostFromStation()
        {
            openedHostForConfirmation = false;
            if (marinaHostWarningPanel) marinaHostWarningPanel.gameObject.SetActive(false);
            if (lobby.CanHost) lobby.Host();
        }

        void CancelHostFromStation()
        {
            if (marinaHostWarningPanel) marinaHostWarningPanel.gameObject.SetActive(false);
            if (openedHostForConfirmation)
            {
                openedHostForConfirmation = false;
                SetMarinaDetail(hostStation, false);
            }
        }

        void PromptJoin(System.Action action)
        {
            pendingJoinAction = action;
            if (marinaJoinWarningPanel)
            {
                marinaJoinWarningPanel.SetAsLastSibling();
                marinaJoinWarningPanel.gameObject.SetActive(true);
            }
        }

        void ConfirmJoinFromStation()
        {
            var act = pendingJoinAction;
            pendingJoinAction = null;
            if (marinaJoinWarningPanel) marinaJoinWarningPanel.gameObject.SetActive(false);
            act?.Invoke();
        }

        void CancelJoinFromStation()
        {
            pendingJoinAction = null;
            if (marinaJoinWarningPanel) marinaJoinWarningPanel.gameObject.SetActive(false);
        }

        void RequestHostChoice()
        {
            if (lobby != null && lobby.GetType().Name == "FixtureLobby")
            {
                if (lobby.CanHost) lobby.Host();
                return;
            }
            PromptHostFromStation();
        }

        void RequestJoinCodeChoice()
        {
            if (!lobby.JoinEnabled || !lobby.CodeValid) return;
            if (lobby != null && lobby.GetType().Name == "FixtureLobby")
            {
                lobby.Join();
                return;
            }
            PromptJoin(lobby.Join);
        }

        void RequestJoinNearbyChoice(int slot)
        {
            if (!lobby.JoinEnabled || !lobby.HasNearby(slot)) return;
            if (lobby != null && lobby.GetType().Name == "FixtureLobby")
            {
                lobby.JoinNearby(slot);
                return;
            }
            PromptJoin(() => lobby.JoinNearby(slot));
        }

        void RequestAcceptInvitationChoice()
        {
            if (!lobby.JoinEnabled || lobby.InvitationFrom == null) return;
            if (lobby != null && lobby.GetType().Name == "FixtureLobby")
            {
                lobby.AcceptInvitation();
                return;
            }
            PromptJoin(lobby.AcceptInvitation);
        }

        RectTransform RoomScreen(RectTransform station,RectTransform existing,string title,Sprite rim,Color accent)
        {
            var screen=new GameObject(title,typeof(RectTransform)).GetComponent<RectTransform>();
            screen.SetParent(station,false);
            screen.sizeDelta=existing.sizeDelta;
            screen.localScale=existing.localScale;
            RoomImage(screen,"Burgundy display glass",RoomSprite(1100,880,92,CabinetSurface.Glass),RoomPlum,0,0,1100,880);
            RoomImage(screen,"Glossy colored glass trim",rim,accent,0,0,1100,880);
            return screen;
        }
        Text RoomLabel(RectTransform parent,string value,float x,float y,float width,float height,int size)
        {
            var label=CandyReadout(parent,string.IsNullOrEmpty(value)?"Room readout":value,value,x,y,width,height,size);
            label.font=marinaRoomFont?marinaRoomFont:label.font;
            label.fontStyle=marinaRoomFont?FontStyle.Normal:FontStyle.Bold;
            label.resizeTextMinSize=Mathf.RoundToInt(size*.75f);
            var shadow=label.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(.17f,.035f,.12f,.74f);
            shadow.effectDistance=new Vector2(0,-2);
            return label;
        }
        Choice RoomButton(RectTransform parent,string id,System.Func<string> title,float x,float y,float width,float height,
            Sprite art,Color color,System.Action action,System.Func<bool> usable,int fontSize=38,bool link=false)
        {
            var choice=Add(parent,id,title,y,action,usable,x,width,height);
            choice.candy=choice.cabinetCandy=true;
            choice.cabinetLink=link;
            choice.faceColor=color;
            choice.button.transition=Selectable.Transition.None;
            choice.button.image.type=Image.Type.Simple;
            choice.button.image.sprite=art;
            choice.button.image.color=link?Color.clear:color;
            choice.label.font=marinaRoomFont?marinaRoomFont:choice.label.font;
            choice.label.fontStyle=marinaRoomFont?FontStyle.Normal:FontStyle.Bold;
            choice.label.color=CandyCream;
            CandyText(choice,0,0,width-26,height-9,fontSize);
            return choice;
        }
        static Image RoomImage(RectTransform parent,string title,Sprite art,Color color,float x,float y,float width,float height)
        {
            var image=new GameObject(title,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent,false);
            image.rectTransform.anchoredPosition=new Vector2(x,y);
            image.rectTransform.sizeDelta=new Vector2(width,height);
            image.sprite=art;image.color=color;image.raycastTarget=false;
            return image;
        }
        static void RoomLine(RectTransform parent,float x,float y,float width,Color color) =>
            RoomImage(parent,"Room code rule",null,color,x,y,width,4);

        Sprite RoomSprite(float width,float height,float radius,CabinetSurface finish)
        {
            int w=Mathf.CeilToInt(width*.4f),h=Mathf.CeilToInt(height*.4f);
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false)
                {name="Marina room "+finish,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[w*h];
            for(int y=0;y<h;y++)
            for(int x=0;x<w;x++)
            {
                float u=(x+.5f)/w,v=(y+.5f)/h;
                var point=new Vector2((u-.5f)*width,(v-.5f)*height);
                float margin=finish==CabinetSurface.Neon?18:finish==CabinetSurface.Glaze?5:2;
                var q=new Vector2(Mathf.Abs(point.x)-width*.5f+radius+margin,Mathf.Abs(point.y)-height*.5f+radius+margin);
                float d=new Vector2(Mathf.Max(0,q.x),Mathf.Max(0,q.y)).magnitude+Mathf.Min(0,Mathf.Max(q.x,q.y))-radius;
                float coverage=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-1.6f,1.6f,d));
                float shade;
                switch(finish)
                {
                    case CabinetSurface.Glass:
                        shade=Mathf.Clamp01(.98f-.47f*(u-.5f)*(u-.5f)-.63f*(v-.63f)*(v-.63f));
                        break;
                    case CabinetSurface.Bezel:
                        coverage*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-32,-26,d));
                        shade=Mathf.Clamp01(.58f+.33f*v+.36f*Mathf.Exp(-Mathf.Abs(d+7)*.16f));
                        break;
                    case CabinetSurface.Neon:
                    case CabinetSurface.Rim:
                        float line=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,5,Mathf.Abs(d)));
                        float glow=finish==CabinetSurface.Neon?.58f*Mathf.Exp(-Mathf.Abs(d)*.12f):0;
                        bool captionGap=finish==CabinetSurface.Neon&&v>.76f&&Mathf.Abs(point.x)<171;
                        coverage=captionGap?0:Mathf.Max(line,glow);
                        shade=.83f+line*.17f;
                        break;
                    default:
                        shade=Mathf.Lerp(.57f,.98f,Mathf.SmoothStep(0,1,v));
                        shade+=.27f*Mathf.Exp(-Mathf.Abs(d+5)*.2f)*Mathf.SmoothStep(0,1,v);
                        shade+=.10f*Mathf.Exp(-Mathf.Pow((v-.76f)*7,2));
                        if(d>0)coverage=Mathf.Max(coverage,.25f*Mathf.Exp(-d*.42f)*(1-v));
                        break;
                }
                pixels[y*w+x]=new Color(shade,shade,shade,coverage);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            marinaRoomArt.Add(sprite);
            return sprite;
        }

        RectTransform BuildRoomVisitNotice(RectTransform station,RectTransform template,string heading)
        {
            var notice=new GameObject(heading+" live match notice",typeof(RectTransform)).GetComponent<RectTransform>();
            notice.SetParent(station,false);
            notice.localScale=template.localScale;
            notice.sizeDelta=template.sizeDelta;
            RoomLabel(notice,heading,0,292,670,110,100);
            RoomLabel(notice,"MATCH IN PROGRESS",0,60,970,88,62);
            RoomLabel(notice,"Room changes are available after the match.",0,-70,970,80,40);
            RoomLabel(notice,"Watch live on the spectator TVs.",0,-162,970,78,35);
            return notice;
        }

        void UpdateMarinaRoomScreens()
        {
            if(!marinaJoinScreen)return;
            bool codePage=!lobby.ShowingNearby&&(lobby.CodeFocused||!lobby.Connected&&lobby.InvitationFrom==null);
            bool roomPage=(lobby.Busy||lobby.Connected&&lobby.IsHost)&&!marinaHostOptions;
            if(!lobby.Connected)marinaHostOptions=false;
            bool visiting=(lobby as ILobbyMatchVisitControls)?.VisitingLobby==true;
            marinaJoinScreen.gameObject.SetActive(!visiting&&codePage);
            candyJoinContent.gameObject.SetActive(!visiting&&!codePage);
            marinaHostScreen.gameObject.SetActive(!visiting&&roomPage);
            candyHostContent.gameObject.SetActive(!visiting&&!roomPage);
            marinaJoinVisit.gameObject.SetActive(visiting);
            marinaHostVisit.gameObject.SetActive(visiting);

            if(codePage&&joinStation.gameObject.activeSelf)
            {
                string code=lobby.EnteredCode??"";
                int dash=code.LastIndexOf('-');
                marinaJoinRegion.text=dash>0?code.Substring(0,dash):"";
                for(int i=0;i<marinaCodeSlots.Length;i++)
                {
                    int index=dash+1+i;
                    marinaCodeSlots[i].text=index<code.Length?code[index].ToString():"·";
                }
                string hint=lobby.JoinHint??"";
                marinaJoinMessage.text=lobby.Busy||hint.StartsWith("Connection failed",System.StringComparison.Ordinal)||
                    hint.StartsWith("Check the code",System.StringComparison.Ordinal)?hint:"";
            }
            if(roomPage&&hostStation.gameObject.activeSelf)
            {
                if(lobby.Busy)
                {
                    marinaHostCode.text="Creating room…";
                    marinaHostCode.resizeTextMaxSize=58;
                    marinaHostCode.resizeTextMinSize=44;
                    marinaHostRegion.text="";
                    marinaDisplayedHostCode=null;
                    marinaHostCount.text="1 / 10";
                    return;
                }
                marinaHostCode.resizeTextMaxSize=96;
                marinaHostCode.resizeTextMinSize=72;
                string code=lobby.HostCode??"";
                int dash=code.LastIndexOf('-');
                marinaHostRegion.text=dash>0?code.Substring(0,dash):"";
                string local=code.Substring(dash+1);
                if(marinaDisplayedHostCode!=local)
                {
                    marinaDisplayedHostCode=local;
                    marinaHostCode.text=string.Join("  ",System.Array.ConvertAll(local.ToCharArray(),c=>c.ToString()));
                }
                marinaHostCount.text=Mathf.Max(1,Details?.ConnectedCount??1)+" / 10";
            }
        }
        void DisposeMarinaRoomScreens()
        {
            foreach(var sprite in marinaRoomArt)
                if(sprite){Destroy(sprite.texture);Destroy(sprite);}
            marinaRoomArt.Clear();
        }

        void UpdateMarinaKeyboard()
        {
            if (!piano) return;
            var eye = Player.ViewCamera;
            bool visible = joinStation.gameObject.activeSelf && lobby.CodeFocused && !wardrobeOpen && eye;
            if (piano.gameObject.activeSelf == visible) return;
            draggingKeyboard = false;
            if (visible) PlaceFloatingKeyboard(eye);
            piano.gameObject.SetActive(visible);
        }
        internal void OpenRoomCodeFromTablet()
        {
            if (!lobby.JoinEnabled) return;
            if (marinaScreen) OpenMarinaDetail("join-code");
            else lobby.ShowCodeEntry();
        }

        void OpenMarinaDetail(string page)
        {
            if (page == "style")
            {
                CloseMarinaDetail();
                if (Vector3.Distance(Player.transform.position, WardrobePosition) > 2)
                    Player.Place(WardrobePosition + Vector3.up * .04f, transform.rotation * MayhemMarina.StyleScreenFacing);
                SetWardrobeOpen(true);
                return;
            }
            switch (page)
            {
                case "join-code":
                    if (lobby.JoinEnabled) lobby.ShowCodeEntry();
                    SetMarinaDetail(joinStation, true);
                    break;
                case "join-nearby":
                    if (lobby.JoinEnabled) lobby.DetectNearby();
                    SetMarinaDetail(joinStation, true);
                    break;
                case "host":
                    openedHostForConfirmation = true;
                    SetMarinaDetail(hostStation, true);
                    RequestHostChoice();
                    break;
                case "host-invite":
                    SetMarinaDetail(hostStation, true);
                    if (lobby.CanInvite) lobby.InviteNearby();
                    break;
                case "maps":
                case "modes":
                case "settings":
                    if (page == "settings") { setupPage = "settings"; settingsPage = 0; }
                    else OpenPicker(page);
                    SetMarinaDetail(setupStation, true);
                    break;
            }
        }

        void SetMarinaDetail(RectTransform station, bool open)
        {
            if (open && station!=joinStation && joinStation.gameObject.activeSelf)
                SetMarinaDetail(joinStation,false);
            if (open && station!=hostStation && hostStation.gameObject.activeSelf)
                SetMarinaDetail(hostStation,false);
            if (open && station!=setupStation && setupStation.gameObject.activeSelf)
                SetMarinaDetail(setupStation,false);
            if (station == joinStation && !open) { lobby.UnfocusCode(); CancelJoinFromStation(); }
            if (station == hostStation && !open) { marinaHostOptions = false; CancelHostFromStation(); }
            UpdateMarinaRoomScreens();
            if(open)
            {
                if(station==setupStation)UpdateGameStation();
                foreach(var choice in choices)
                    if(choice.button&&choice.button.transform.IsChildOf(station))RefreshChoice(choice);
            }
            station.gameObject.SetActive(open);
            terminalRequireRelease = true;
            DeadlyJenga.Presentation.MenuAim.Consume();
            RefreshMarinaDisplays();
        }

        void RefreshMarinaDisplays()
        {
            bool mainDetail = setupStation.gameObject.activeSelf;
            marinaScreen.Visible = !mainDetail && !wardrobeOpen;
            marinaScreen.SetCabinetVisibility(!joinStation.gameObject.activeSelf && !wardrobeOpen,
                !hostStation.gameObject.activeSelf && !wardrobeOpen);
            if (marinaBack) marinaBack.gameObject.SetActive(mainDetail && !wardrobeOpen);
            if (marinaStylePrompt) marinaStylePrompt.gameObject.SetActive(!mainDetail && !wardrobeOpen);
            UpdateMarinaRoomScreens();
            UpdateMarinaKeyboard();
        }

        void CloseMarinaDetail()
        {
            lobby?.UnfocusCode();
            marinaHostOptions=false;
            CancelJoinFromStation();
            CancelHostFromStation();
            terminalRequireRelease = true;
            DeadlyJenga.Presentation.MenuAim.Consume();
            joinStation.gameObject.SetActive(false);
            hostStation.gameObject.SetActive(false);
            setupStation.gameObject.SetActive(false);
            wardrobePanel.gameObject.SetActive(false);
            if (wardrobeOpen) SetWardrobeOpen(false);
            RefreshMarinaDisplays();
        }

        partial void MarinaInteractionRange(ref float distance){if(marina)distance=11;}
        partial void UpdateMayhemMarina()
        {
            if(!marina)return;
            if (!wardrobeOpen)
            {
                if (ConfirmingJoin)
                {
                    if (Keyboard.current?.yKey.wasPressedThisFrame == true) { ConfirmJoinFromStation(); return; }
                    if (Keyboard.current?.nKey.wasPressedThisFrame == true || Keyboard.current?.escapeKey.wasPressedThisFrame == true) { CancelJoinFromStation(); return; }
                }
                if (ConfirmingHostStation)
                {
                    if (Keyboard.current?.yKey.wasPressedThisFrame == true) { ConfirmHostFromStation(); return; }
                    if (Keyboard.current?.nKey.wasPressedThisFrame == true || Keyboard.current?.escapeKey.wasPressedThisFrame == true) { CancelHostFromStation(); return; }
                }
                if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                {
                    if (lobby.CodeFocused) CloseRoomCodeKeyboard();
                    else if (setupStation.gameObject.activeSelf) CloseSetupPage();
                    else if (joinStation.gameObject.activeSelf) SetMarinaDetail(joinStation, false);
                    else if (hostStation.gameObject.activeSelf) SetMarinaDetail(hostStation, false);
                }
            }
            RefreshMarinaDisplays();
            if(wardrobePanel)wardrobePanel.gameObject.SetActive(wardrobeOpen&&Player.IsVR);
            if(!wardrobeOpen&&Time.unscaledTime>=nextMarinaMirror&&Vector3.Distance(Player.transform.position,WardrobePosition)<6)
            {
                nextMarinaMirror=Time.unscaledTime+.2f;var previous=wardrobePlayer;wardrobePlayer=Player;
                try{ResetWardrobeView();PresentWardrobe();}finally{wardrobePlayer=previous;}
            }
        }
        void BuildMarinaMirror()
        {
            var go=new GameObject("Marina live wardrobe mirror",typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(Mask));go.transform.SetParent(transform,false);marinaMirror=go.GetComponent<RectTransform>();marinaMirror.SetLocalPositionAndRotation(MayhemMarina.StyleMirror,MayhemMarina.StyleMirrorFacing);marinaMirror.localScale=Vector3.one*.01f;marinaMirror.sizeDelta=new Vector2(247,348);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Canvas>().overrideSorting=true;go.GetComponent<Canvas>().sortingOrder=60;
            marinaMaskTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*32);pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-d));}
            marinaMaskTexture.SetPixels(pixels);marinaMaskTexture.Apply();marinaMaskSprite=Sprite.Create(marinaMaskTexture,new Rect(0,0,64,64),Vector2.one*.5f);go.GetComponent<Image>().sprite=marinaMaskSprite;go.GetComponent<Image>().color=new Color(.42f,.68f,.75f);go.GetComponent<Mask>().showMaskGraphic=true;
            var image=new GameObject("Mirror image",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(go.transform,false);var raw=image.GetComponent<RawImage>();raw.texture=wardrobeTexture;raw.color=Color.white;raw.raycastTarget=false;raw.rectTransform.sizeDelta=new Vector2(348,348);
        }
        void MarinaActivityArt(GameObject body, string cue)
        {
            if (!body || !body.transform.IsChildOf(transform)) return;
            var root = body.transform;
            switch (cue)
            {
                case "MarinaJetSki":
                    var color = root.localPosition.x < -22 ? new Color(.11f, .74f, .69f) : new Color(1, .36f, .19f);
                    Ellipsoid(root, "Jet ski hull", new Vector3(0, .06f, .05f), new Vector3(1.07f, .43f, 2.62f), new Color(.09f, .13f, .20f));
                    Ellipsoid(root, "Jet ski deck", new Vector3(0, .24f, .19f), new Vector3(1.03f, .38f, 2.28f), color);
                    Ellipsoid(root, "Seat", new Vector3(0, .47f, -.27f), new Vector3(.46f, .19f, .88f), new Color(.12f, .14f, .20f));
                    Ellipsoid(root, "Bow cowling", new Vector3(0, .42f, .62f), new Vector3(.69f, .45f, 1.06f), color);
                    Box(root, "Steering post", new Vector3(0, .64f, .38f), new Vector3(.08f, .30f, .09f), CandyInk);
                    Box(root, "Handlebar", new Vector3(0, .78f, .39f), new Vector3(.62f, .07f, .09f), CandyInk);
                    break;
                case "MarinaKnifeRack":
                    Box(root, "Practice knife rack", Vector3.down * .12f, new Vector3(1.16f, .24f, .62f), CandyInk);
                    for (int i = -1; i <= 1; i += 2)
                        Box(root, "Knife rack leg", new Vector3(i * .47f, -.54f, 0), new Vector3(.11f, .77f, .43f), CandyInk);
                    for (int i = 0; i < 3; i++) BuildMarinaKnife(root, new Vector3((i - 1) * .29f, .04f, 0));
                    var canvas = new GameObject("Knife range directions", typeof(RectTransform), typeof(Canvas));
                    canvas.transform.SetParent(root, false);
                    canvas.transform.localPosition = new Vector3(0, .19f, -.35f);
                    canvas.transform.localScale = Vector3.one * .0018f;
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    Label(canvas.GetComponent<RectTransform>(), "PRACTICE KNIVES", 65, 38, 680, 58);
                    Label(canvas.GetComponent<RectTransform>(), "E / TRIGGER TO EQUIP", 8, 29, 680, 48);
                    Label(canvas.GetComponent<RectTransform>(), "CLICK / TRIGGER TO THROW", -38, 27, 680, 42);
                    break;
                case "MarinaThrownKnife":
                    BuildMarinaKnife(root, Vector3.zero);
                    break;
            }
        }
        void BuildMarinaKnife(Transform root, Vector3 offset)
        {
            Box(root, "Knife grip", offset + Vector3.back * .10f, new Vector3(.055f, .05f, .19f), CandyInk);
            Box(root, "Knife guard", offset, new Vector3(.12f, .035f, .032f), new Color(.80f, .57f, .22f));
            Ellipsoid(root, "Knife blade", offset + Vector3.forward * .12f, new Vector3(.075f, .025f, .27f), new Color(.79f, .88f, .95f));
        }
        partial void DisposeMayhemMarina()
        {DisposeMarinaRoomScreens();GameEvents.Emoted -= MarinaActivityArt;if(marinaScreen)Destroy(marinaScreen);if(marina)Destroy(marina);if(marinaBack)Destroy(marinaBack.gameObject);if(marinaStylePrompt)Destroy(marinaStylePrompt.gameObject);if(marinaMirror)Destroy(marinaMirror.gameObject);if(marinaMaskSprite)Destroy(marinaMaskSprite);if(marinaMaskTexture)Destroy(marinaMaskTexture);}
    }
}
