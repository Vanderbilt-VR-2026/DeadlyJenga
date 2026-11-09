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
        RectTransform marinaBack,marinaMirror,marinaStylePrompt;
        Texture2D marinaMaskTexture;
        Sprite marinaMaskSprite;
        string marinaDetail="";
        float nextMarinaMirror;
        partial void BuildMayhemMarina()
        {
            if(!(lobby is IWorldQuery world)||world.Radius<30)return;
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
                lobby.AddBlocker(marina.transform,new Vector3(x,3.40f,11.13f),new Vector3(2.08f,3.08f,.96f));
            foreach(var television in GetComponentsInChildren<LobbySpectatorTVWall>(true))
            {
                television.transform.SetLocalPositionAndRotation(new Vector3(5.45f,1.86f,9.84f),Quaternion.Euler(0,30,0));
                lobby.AddBlocker(television.transform,new Vector3(0,1.48f,.03f),new Vector3(3.76f,2.95f,.55f));
            }
            wardrobeCenter=MayhemMarina.Wardrobe;
            var pod=transform.Find("Visual - wardrobe pod");if(pod)pod.gameObject.SetActive(false);
            var detailCenter = new Vector3(0, 3.67f, 10.85f);
            MountMarinaScreen(joinStation, detailCenter, Quaternion.identity, 5.8f, 2.62f);
            MountMarinaScreen(hostStation, detailCenter, Quaternion.identity, 5.8f, 2.62f);
            MountMarinaScreen(setupStation, detailCenter, Quaternion.identity, 5.8f, 2.62f);
            MountMarinaScreen(wardrobePanel, MayhemMarina.StyleScreen, MayhemMarina.StyleScreenFacing, 2.55f, 2.75f);
            piano.SetLocalPositionAndRotation(MayhemMarina.CodeKeyboard, MayhemMarina.CodeKeyboardFacing);
            piano.localScale = Vector3.one * .00115f;
            marinaScreen=gameObject.AddComponent<MarinaScreen>();marinaScreen.Build(lobby,OpenMarinaDetail);
            var go = new GameObject("Marina detail back control", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            marinaBack = go.GetComponent<RectTransform>();
            marinaBack.localPosition = new Vector3(0, 5.23f, 10.85f);
            marinaBack.localScale = Vector3.one * .0026f;
            marinaBack.sizeDelta = new Vector2(1000, 125);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<Canvas>().overrideSorting = true;
            go.GetComponent<Canvas>().sortingOrder = 100;
            var back = Add(marinaBack, "marina-close", () => "BACK TO MARINA", 0, CloseMarinaDetail, () => true, 0, 1000, 125);
            back.button.image.color = CandyInk;
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
        void UpdateMarinaKeyboard()
        {
            if (!piano) return;
            bool visible = marinaDetail == "join" && lobby.CodeFocused;
            if (piano.gameObject.activeSelf != visible) piano.gameObject.SetActive(visible);
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
            bool code=page=="join-code",nearby=page=="join-nearby",invite=page=="host-invite";
            if(code||nearby)page="join";
            if(invite)page="host";
            if(code&&lobby.JoinEnabled){lobby.ShowCodeEntry();lobby.FocusCode();}
            if(nearby&&lobby.JoinEnabled)lobby.DetectNearby();
            if(invite&&lobby.CanInvite)lobby.InviteNearby();
            marinaDetail = page;
            joinStation.gameObject.SetActive(page == "join");
            hostStation.gameObject.SetActive(page == "host");
            setupStation.gameObject.SetActive(page == "maps" || page == "modes" || page == "settings");
            if (page == "maps" || page == "modes") OpenPicker(page);
            if (page == "settings") { setupPage = "settings"; settingsPage = 0; }
            UpdateMarinaKeyboard();
            marinaScreen.Visible = false;
            marinaBack.gameObject.SetActive(true);
        }
        void CloseMarinaDetail()
        {
            lobby?.UnfocusCode();
            terminalRequireRelease = true;
            DeadlyJenga.Presentation.MenuAim.Consume();
            marinaDetail = "";
            joinStation.gameObject.SetActive(false);
            hostStation.gameObject.SetActive(false);
            setupStation.gameObject.SetActive(false);
            piano.gameObject.SetActive(false);
            wardrobePanel.gameObject.SetActive(false);
            if (wardrobeOpen) SetWardrobeOpen(false);
            if (marinaBack) marinaBack.gameObject.SetActive(false);
            if (marinaScreen) marinaScreen.Visible = true;
        }
        partial void MarinaInteractionRange(ref float distance){if(marina)distance=11;}
        partial void UpdateMayhemMarina()
        {
            if(!marina)return;
            if(marinaDetail!=""&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)CloseMarinaDetail();
            if(marinaScreen)marinaScreen.Visible=marinaDetail==""&&!wardrobeOpen;
            if(marinaDetail!="")UpdateMarinaKeyboard();
            if(marinaStylePrompt)marinaStylePrompt.gameObject.SetActive(marinaDetail==""&&!wardrobeOpen);
            if(wardrobePanel)wardrobePanel.gameObject.SetActive(wardrobeOpen&&Player.IsVR);
            if(!wardrobeOpen&&Time.unscaledTime>=nextMarinaMirror&&Vector3.Distance(Player.transform.position,WardrobePosition)<6)
            {
                nextMarinaMirror=Time.unscaledTime+.2f;var previous=wardrobePlayer;wardrobePlayer=Player;
                try{PresentMarinaMirror();}finally{wardrobePlayer=previous;}
            }
        }
        void BuildMarinaMirror()
        {
            var go=new GameObject("Marina live wardrobe mirror",typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(Mask));go.transform.SetParent(transform,false);marinaMirror=go.GetComponent<RectTransform>();marinaMirror.localPosition=new Vector3(-13,3.4f,10.08f);marinaMirror.localScale=Vector3.one*.01f;marinaMirror.sizeDelta=new Vector2(247,348);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Canvas>().overrideSorting=true;go.GetComponent<Canvas>().sortingOrder=60;
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
        {GameEvents.Emoted -= MarinaActivityArt;if(marinaScreen)Destroy(marinaScreen);if(marina)Destroy(marina);if(marinaBack)Destroy(marinaBack.gameObject);if(marinaStylePrompt)Destroy(marinaStylePrompt.gameObject);if(marinaMirror)Destroy(marinaMirror.gameObject);if(marinaMaskSprite)Destroy(marinaMaskSprite);if(marinaMaskTexture)Destroy(marinaMaskTexture);}
    }
}
