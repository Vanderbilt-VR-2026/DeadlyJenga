using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        enum TabletPage { Comfort, Controls, Room, Developer, Nearby, Profile, ToolSearch }
        sealed class TabletButton
        {
            public Button Button;
            public BoxCollider Target;
            public Text Label;
            public Outline Focus;
            public Func<string> Title;
            public Action Select;
            public Func<bool> Selected, Allowed;
            public Color Accent;
            public Vector2 BasePosition;
            public bool ToolTile;
        }

        static Sprite toolTileSprite;
        static Sprite ToolTileSprite
        {
            get
            {
                if(toolTileSprite)return toolTileSprite;
                const int width=160,height=48;const float radius=10;
                var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Raised tools tile",wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color32[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    var q=new Vector2(Mathf.Abs(x+.5f-width*.5f)-(width*.5f-radius),Mathf.Abs(y+.5f-height*.5f)-(height*.5f-radius));
                    float edge=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
                    float t=(float)y/(height-1);
                    var color=Color.Lerp(new Color(.25f,.07f,.36f),new Color(.46f,.16f,.58f),t);
                    if(edge>-3)color=Color.Lerp(color,new Color(.75f,.48f,.88f),Mathf.Clamp01((edge+3)/2)*Mathf.Lerp(.4f,.8f,t));
                    color.a=Mathf.Clamp01(-edge);pixels[y*width+x]=color;
                }
                texture.SetPixels32(pixels);texture.Apply(false,true);
                toolTileSprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),100);
                return toolTileSprite;
            }
        }

        static Sprite juicyCircleSprite;
        static Sprite JuicyCircleSprite
        {
            get
            {
                if (juicyCircleSprite) return juicyCircleSprite;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "JuicyCircle", wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                float radius = (size - 4) * 0.5f;
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
                Vector2 lightPos = center + new Vector2(-radius * 0.35f, radius * 0.35f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = Vector2.Distance(pos, center);
                    if (dist > radius + 1f) { pixels[y * size + x] = Color.clear; continue; }
                    float edgeAlpha = Mathf.Clamp01(radius + 1f - dist);
                    float normDist = Mathf.Clamp01(dist / radius);
                    float z = Mathf.Sqrt(Mathf.Max(0, 1f - normDist * normDist));
                    Vector3 normal = new Vector3((pos.x - center.x) / radius, (pos.y - center.y) / radius, z);
                    float specDist = Vector2.Distance(pos, lightPos) / (radius * 0.55f);
                    float specular = Mathf.Pow(Mathf.Clamp01(1f - specDist), 3f) * 0.85f;
                    float lightDot = Mathf.Clamp01(Vector3.Dot(normal, new Vector3(-0.35f, 0.45f, 0.82f).normalized));
                    Color baseRed = Color.Lerp(new Color(0.70f, 0.05f, 0.12f), new Color(0.98f, 0.22f, 0.28f), lightDot);
                    Color finalColor = Color.Lerp(baseRed, Color.white, specular);
                    if (dist > radius - 1.5f) finalColor = Color.Lerp(finalColor, new Color(0.45f, 0.02f, 0.08f), 0.5f);
                    finalColor.a = edgeAlpha;
                    pixels[y * size + x] = finalColor;
                }
                tex.SetPixels32(pixels); tex.Apply();
                juicyCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
                return juicyCircleSprite;
            }
        }

        static readonly Color TabletPlum = new(.20f,.055f,.22f);
        static readonly Color TabletPanel = new(.34f,.15f,.38f);
        static readonly Color TabletCoral = new(.93f,.36f,.35f);
        static readonly Color TabletCream = new(1f,.95f,.83f);
        static readonly Color JuicyRed = new(.92f,.12f,.18f);
        readonly List<TabletButton> tabletTabs = new();
        readonly List<TabletButton> tabletActions = new();
        readonly RectTransform[] tabletPages = new RectTransform[7];
        RectTransform legacyDeveloper, tabletNav;
        Text tabletInputLayout, leftControllerHelp, rightControllerHelp, tabletRoomStatus;
        Text leftInputTitle, rightInputTitle;
        RawImage leftController, rightController;
        TabletPage tabletPage;
        bool tabletDeveloperDetails;
        float tabletRefreshAt;
        RectTransform tabletDeveloperGrid, tabletDeveloperWeapons;
        bool tabletWeaponsOpen;
        int tabletWeaponCategory, tabletWeaponOffset;
        EquipmentKind tabletWeaponSelected = EquipmentKind.Blaster;
        bool tabletGiveAll, tabletRepeat;
        Text tabletSelectedWeaponTitle, tabletSelectedWeaponStats;
        RawImage tabletCenterWeaponPicture;
        TabletButton[] tabletWheelSlotButtons;
        RawImage[] tabletWheelSlotPictures;
        TabletButton tabletDevBackButton;

        internal const string TabletOpenedPreference = "DeadlyJenga.PlayerTabletOpened";

        bool DeveloperTabAvailable => session is ILobbyControls
            ? SessionPresentation.LobbyDeveloperAvailable(session)
            : DemoMode && GameExperiencePanel.Resolve(session)?.Experience != GameExperience.Game;


        void BuildPlayerTablet()
        {
            var old = new List<Transform>();
            foreach (Transform child in transform) old.Add(child);
            legacyDeveloper = TabletRoot("Developer detail controls");
            legacyDeveloper.localScale = Vector3.one;
            legacyDeveloper.anchoredPosition = Vector2.zero;
            foreach (var child in old) child.SetParent(legacyDeveloper,false);

            var nav = TabletRoot("Player tablet tabs");
            tabletNav = nav;

            tabletDevBackButton = TabletControl(tabletTabs, nav, () => "◄", new Vector2(-485, 316), new Vector2(56, 56),
                OnDeveloperBackClicked, allowed: IsDevBackButtonActive, font: 28, accent: TabletPanel);
            tabletDevBackButton.Button.image.sprite = JuicyCircleSprite;
            tabletDevBackButton.Button.image.type = Image.Type.Simple;

            AddTab(TabletPage.Comfort,"MOVEMENT SETTINGS",-330,width:240);
            AddTab(TabletPage.Controls,"CONTROLS",-114,width:160);
            AddTab(TabletPage.Room,"ROOM",52,width:140);
            var devTab = AddTab(TabletPage.Developer,"DEVELOPER",238,()=>DeveloperTabAvailable,width:200);
            devTab.Button.image.sprite = null;
            devTab.Button.image.type = Image.Type.Simple;

            var close = TabletControl(tabletTabs,nav,()=>"✕",new(450,316),new(58,58),()=>session.SetMenu(false),accent:JuicyRed,font:34);
            close.Button.image.sprite = JuicyCircleSprite;
            close.Button.image.type = Image.Type.Simple;

            // Popout Resume button tab at bottom edge of tablet frame
            TabletControl(tabletTabs,nav,()=>"RESUME",new(0,-366),new(220,50),()=>session.SetMenu(false),accent:TabletPanel,font:22);

            TabletRule(nav,new Vector2(0,270),new Vector2(1010,4),TabletCoral);

            for (int i=0;i<tabletPages.Length;i++)
                tabletPages[i] = TabletRoot(((TabletPage)i)+" page");
            BuildTabletComfort(tabletPages[(int)TabletPage.Comfort]);
            BuildTabletControls(tabletPages[(int)TabletPage.Controls]);
            BuildTabletRoom(tabletPages[(int)TabletPage.Room]);
            BuildTabletNearby(tabletPages[(int)TabletPage.Nearby]);
            BuildTabletProfile(tabletPages[(int)TabletPage.Profile]);
            BuildTabletToolSearch(tabletPages[(int)TabletPage.ToolSearch]);
            BuildTabletDeveloper(tabletPages[(int)TabletPage.Developer]);

            OpenPlayerTabletPage(TabletPage.Comfort);
        }

        TabletButton AddTab(TabletPage page,string title,float x,Func<bool> allowed=null,float width=160)
        {
            return TabletControl(tabletTabs,tabletNav,()=>title,new(x,316),new(width,68),()=>OpenPlayerTabletPage(page),
                ()=>tabletPage==page,allowed);
        }

        void OpenPlayerTabletPage(TabletPage page)
        {
            if (page==TabletPage.Developer&&!DeveloperTabAvailable) return;
            RestoreEquipmentWheelLayout();
            ResetPages();
            menuCategory=menuSection=-1;
            developerRoute="";developerChild=false;developerVolumeApply=null;
            tabletPage=page;
            tabletDeveloperDetails=false;
            tabletWeaponsOpen=false;
            comfortSliderOpen=false;
            if (disasterWheelUI) disasterWheelUI.SetActive(false);
            if (modeWheelUI) modeWheelUI.SetActive(false);
            if(tabletDeveloperGrid)tabletDeveloperGrid.gameObject.SetActive(true);
            if(tabletDeveloperWeapons)tabletDeveloperWeapons.gameObject.SetActive(false);
            tabletRefreshAt=0;
            legacyDeveloper.gameObject.SetActive(false);
            if(tabletNav)tabletNav.gameObject.SetActive(true);
            for (int i=0;i<tabletPages.Length;i++)
                tabletPages[i].gameObject.SetActive(i==(int)page);
            UpdateDevBackButtonState();
        }

        RectTransform TabletRoot(string name)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform,false);
            return rect;
        }

        Text TabletText(RectTransform parent,string value,Vector2 p,Vector2 size,int font,
            TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var label=Text(value,p,size,font);
            label.rectTransform.SetParent(parent,false);
            label.rectTransform.anchoredPosition=p;
            label.fontStyle=FontStyle.Bold;
            label.alignment=alignment;
            label.color=TabletCream;
            label.raycastTarget=false;
            return label;
        }

        void TabletRule(RectTransform parent,Vector2 p,Vector2 size,Color color,bool sliced=false)
        {
            var image=PointerImage("Candy tablet trim",size);
            image.rectTransform.SetParent(parent,false);
            image.rectTransform.anchoredPosition=p;
            image.color=color;
            if(sliced){image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=.44f;}
        }

        TabletButton TabletControl(List<TabletButton> controls,RectTransform parent,Func<string> title,Vector2 p,
            Vector2 size,Action select,Func<bool> selected=null,Func<bool> allowed=null,Color? accent=null,int font=30)
        {
            var button=HeaderButton(title(),0,out var target);
            var rect=(RectTransform)button.transform;
            rect.SetParent(parent,false);
            SizeNavigation(button,target,p,size,font);
            var label=button.GetComponentInChildren<Text>();
            label.fontStyle=FontStyle.Bold;
            label.resizeTextForBestFit=true;
            label.resizeTextMinSize=Mathf.Min(12,font);
            label.resizeTextMaxSize=font;
            button.image.pixelsPerUnitMultiplier=.44f;
            var outline=button.gameObject.AddComponent<Outline>();
            outline.effectDistance=new Vector2(3,-3);
            outline.effectColor=Color.clear;
            var entry=new TabletButton {Button=button,Target=target,Label=label,Title=title,Focus=outline,
                Select=select,Selected=selected,Allowed=allowed,Accent=accent??TabletPanel,BasePosition=p};
            controls.Add(entry);
            return entry;
        }

        bool SprintHold => PlayerPrefs.GetInt("SurvivorVR.SprintHold", 0) != 0;
        void SetTabletSprintHold(bool hold)
        {
            PlayerPrefs.SetInt("SurvivorVR.SprintHold", hold ? 1 : 0);
            PlayerPrefs.Save();
            tabletRefreshAt = 0;
        }

        bool ClimbToggle => PlayerPrefs.GetInt("SurvivorVR.ClimbToggle", 0) != 0;
        void SetTabletClimbToggle(bool toggle)
        {
            PlayerPrefs.SetInt("SurvivorVR.ClimbToggle", toggle ? 1 : 0);
            PlayerPrefs.Save();
            tabletRefreshAt = 0;
        }

        bool InvertStick => PlayerPrefs.GetInt("SurvivorVR.InvertLookY", 0) != 0;
        void SetTabletInvertStick(bool invert)
        {
            PlayerPrefs.SetInt("SurvivorVR.InvertLookY", invert ? 1 : 0);
            PlayerPrefs.Save();
            tabletRefreshAt = 0;
        }

        bool VignetteEnabled => PlayerPrefs.GetInt("SurvivorVR.Vignette", 0) != 0;
        void SetTabletVignette(bool enabled)
        {
            PlayerPrefs.SetInt("SurvivorVR.Vignette", enabled ? 1 : 0);
            PlayerPrefs.Save();
            tabletRefreshAt = 0;
        }

        bool HapticsEnabled => PlayerPrefs.GetInt("SurvivorVR.Haptics", 1) != 0;
        void SetTabletHaptics(bool enabled)
        {
            PlayerPrefs.SetInt("SurvivorVR.Haptics", enabled ? 1 : 0);
            PlayerPrefs.Save();
            tabletRefreshAt = 0;
        }

        bool HeadBobEnabled => PlayerPrefs.GetInt("SurvivorVR.HeadBob", 0) != 0;
        void SetTabletHeadBob(bool enabled)
        {
            PlayerPrefs.SetInt("SurvivorVR.HeadBob", enabled ? 1 : 0);
            PlayerPrefs.Save();
            tabletRefreshAt = 0;
        }

        static readonly float[] SmoothTurnPresets = { 15f, 30f, 45f, 60f, 90f, 120f, 180f, 240f, 270f, 360f };
        static readonly float[] SnapAnglePresets = { 15f, 30f, 45f, 60f, 90f };

        void StepTurnPreset(bool increment)
        {
            var settings = Settings;
            if (settings == null) return;
            bool isSnap = settings.TurnLabel == "Snap";
            float current = isSnap ? settings.SnapAngle : settings.SmoothTurnSpeed;
            float[] presets = isSnap ? SnapAnglePresets : SmoothTurnPresets;
            int closest = 0;
            float minDiff = float.MaxValue;
            for (int i = 0; i < presets.Length; i++)
            {
                float diff = Mathf.Abs(presets[i] - current);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = i;
                }
            }
            int next = increment ? Mathf.Min(presets.Length - 1, closest + 1) : Mathf.Max(0, closest - 1);
            if (isSnap) ApplyTabletSnapAngle(presets[next]);
            else ApplyTabletSmoothSpeed(presets[next]);
        }

        void ApplyTabletSmoothSpeed(float speed)
        {
            var settings = Settings;
            if (settings == null) return;
            speed = Mathf.Clamp(speed, 15f, 360f);
            var rules = JsonUtility.FromJson<MatchRules>(JsonUtility.ToJson(settings.CurrentRules));
            rules.smoothDegrees = speed;
            settings.ApplyMatchRules(rules);
            PlayerPrefs.SetFloat("SurvivorVR.SmoothDegrees", speed);
            PlayerPrefs.Save();
            tabletRefreshAt = 0;
        }

        void ApplyTabletSnapAngle(float angle)
        {
            var settings = Settings;
            if (settings == null) return;
            angle = Mathf.Clamp(angle, 15f, 90f);
            var rules = JsonUtility.FromJson<MatchRules>(JsonUtility.ToJson(settings.CurrentRules));
            rules.snapAngle = angle;
            settings.ApplyMatchRules(rules);
            PlayerPrefs.SetFloat("SurvivorVR.SnapAngle", angle);
            PlayerPrefs.Save();
            tabletRefreshAt = 0;
        }

        void ResetSeatedHeightDefault()
        {
            session.Player?.gameObject.SendMessage("ResetSeatedHeightDefault", SendMessageOptions.DontRequireReceiver);
            Settings?.UseStationaryPreset();
            tabletRefreshAt = 0;
        }

        bool comfortSliderOpen;
        RectTransform comfortMainGroup, comfortSliderGroup;
        Text comfortSliderTitle, comfortSliderValueText;

        void OpenComfortSlider()
        {
            comfortSliderOpen = true;
            UpdateComfortSliderUI();
            tabletRefreshAt = 0;
        }

        void CloseComfortSlider()
        {
            comfortSliderOpen = false;
            UpdateComfortSliderUI();
            tabletRefreshAt = 0;
        }

        void AdjustComfortValue(float delta)
        {
            var settings = Settings;
            if (settings == null) return;
            bool isSnap = settings.TurnLabel == "Snap";
            if (isSnap)
            {
                float angle = Mathf.Clamp(settings.SnapAngle + delta, 15f, 90f);
                ApplyTabletSnapAngle(angle);
            }
            else
            {
                float speed = Mathf.Clamp(settings.SmoothTurnSpeed + delta, 15f, 360f);
                ApplyTabletSmoothSpeed(speed);
            }
            UpdateComfortSliderUI();
        }

        void UpdateComfortSliderUI()
        {
            if (comfortSliderGroup) comfortSliderGroup.gameObject.SetActive(comfortSliderOpen);
            if (comfortMainGroup) comfortMainGroup.gameObject.SetActive(!comfortSliderOpen);
            if (!comfortSliderOpen) return;
            bool isSnap = Settings?.TurnLabel == "Snap";
            float val = isSnap ? (Settings?.SnapAngle ?? 30f) : (Settings?.SmoothTurnSpeed ?? 120f);
            if (comfortSliderTitle)
                comfortSliderTitle.text = isSnap ? "SNAP ANGLE FINE TUNING" : "TURN SPEED FINE TUNING";
            if (comfortSliderValueText)
                comfortSliderValueText.text = isSnap ? $"<color=#52ffc6>{val:0}°</color>" : $"<color=#52ffc6>{val:0}°/s</color>";
        }

        void BuildTabletComfort(RectTransform page)
        {
            comfortMainGroup = new GameObject("ComfortMain", typeof(RectTransform)).GetComponent<RectTransform>();
            comfortMainGroup.SetParent(page, false);
            comfortMainGroup.anchorMin = Vector2.zero; comfortMainGroup.anchorMax = Vector2.one;
            comfortMainGroup.sizeDelta = Vector2.zero;

            TabletText(comfortMainGroup, "MOVEMENT SETTINGS", new Vector2(0, 222), new Vector2(900, 52), 42);

            string[] rowTitles = { "Play position", "Dominant hand", "Sprint mode", "Climb mode", "Turn style (seated)" };
            float[] rowYs = { 152, 84, 16, -52, -122 };

            for (int i = 0; i < 5; i++)
            {
                float y = rowYs[i];
                TabletRule(comfortMainGroup, new Vector2(0, y), new Vector2(990, 60), new Color(.27f, .10f, .30f),true);
                MovementIcon(i, new Vector2(-453, y), 68);
                TabletText(comfortMainGroup, rowTitles[i], new Vector2(-326, y), new Vector2(180, 50), 21, TextAnchor.MiddleLeft);
            }

            // Row 0: Play position (Seated, Standing, Auto-detect)
            TabletControl(tabletActions, comfortMainGroup, () => "Seated", new Vector2(-158, 152), new Vector2(128, 48),
                () => { PlayerPrefs.SetInt("SurvivorVR.PlayPositionAuto", 0); Settings?.UseStationaryPreset(); },
                () => Settings != null && !Settings.StandingPlay && PlayerPrefs.GetInt("SurvivorVR.PlayPositionAuto", 1) == 0,
                () => Settings != null, TabletCoral, font: 22);

            TabletControl(tabletActions, comfortMainGroup, () => "Standing", new Vector2(-18, 152), new Vector2(128, 48),
                () => { PlayerPrefs.SetInt("SurvivorVR.PlayPositionAuto", 0); Settings?.UseStandingPreset(); },
                () => Settings?.StandingPlay == true && PlayerPrefs.GetInt("SurvivorVR.PlayPositionAuto", 1) == 0,
                () => Settings != null, TabletCoral, font: 22);

            TabletControl(tabletActions, comfortMainGroup, () => "Auto", new Vector2(122, 152), new Vector2(128, 48),
                () => {
                    PlayerPrefs.SetInt("SurvivorVR.PlayPositionAuto", 1);
                    PlayerPrefs.Save();
                    session.Player?.gameObject.SendMessage("SetAutoDetectHybrid", true, SendMessageOptions.DontRequireReceiver);
                    tabletRefreshAt = 0;
                },
                () => PlayerPrefs.GetInt("SurvivorVR.PlayPositionAuto", 1) != 0,
                () => true, TabletCoral, font: 22);

            // Row 1: Dominant hand (Left, Right)
            TabletControl(tabletActions, comfortMainGroup, () => "Left", new Vector2(-148, 84), new Vector2(150, 48),
                () => SetTabletHand(true), () => DominantLeft, () => session.Player != null, TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => "Right", new Vector2(22, 84), new Vector2(150, 48),
                () => SetTabletHand(false), () => !DominantLeft, () => session.Player != null, TabletCoral, font: 24);

            // Row 2: Sprint mode (Toggle, Hold)
            TabletControl(tabletActions, comfortMainGroup, () => "Toggle", new Vector2(-148, 16), new Vector2(150, 48),
                () => SetTabletSprintHold(false), () => !SprintHold, () => true, TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => "Hold", new Vector2(22, 16), new Vector2(150, 48),
                () => SetTabletSprintHold(true), () => SprintHold, () => true, TabletCoral, font: 24);

            // Row 3: Climb mode (Toggle, Hold)
            TabletControl(tabletActions, comfortMainGroup, () => "Toggle", new Vector2(-148, -52), new Vector2(150, 48),
                () => SetTabletClimbToggle(true), () => ClimbToggle, () => true, TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => "Hold", new Vector2(22, -52), new Vector2(150, 48),
                () => SetTabletClimbToggle(false), () => !ClimbToggle, () => true, TabletCoral, font: 24);

            // Row 4: Turn style (seated) (Snap, Smooth)
            TabletControl(tabletActions, comfortMainGroup, () => "Snap", new Vector2(-148, -122), new Vector2(150, 48),
                () => SetTabletTurn("Snap"), () => Settings?.TurnLabel == "Snap", () => Settings != null, TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => "Smooth", new Vector2(22, -122), new Vector2(150, 48),
                () => SetTabletTurn("Smooth"), () => Settings?.TurnLabel == "Smooth", () => Settings != null, TabletCoral, font: 24);

            MovementToggle("Invert stick", 5, 152, () => InvertStick, SetTabletInvertStick);
            MovementToggle("Vignette", 6, 84, () => VignetteEnabled, SetTabletVignette);
            MovementToggle("Haptics", 7, 16, () => HapticsEnabled, SetTabletHaptics);
            MovementToggle("Head bob", 8, -52, () => HeadBobEnabled, SetTabletHeadBob);

            // Bottom Row (y = -215):
            // Left: Speed / Snap Angle Traversal with stacked ▲ / ▼ buttons to the right
            TabletControl(tabletActions, comfortMainGroup, () => Settings?.TurnLabel == "Snap"
                ? $"Snap angle  {Settings.SnapAngle:0}°" : Settings?.TurnLabel == "PhysicalOnly"
                ? "Physical turning" : $"Turn speed  {Settings?.SmoothTurnSpeed:0}°/s",
                new Vector2(-310, -215), new Vector2(300, 64),
                () => OpenComfortSlider(),
                allowed: () => Settings != null && Settings.TurnLabel != "PhysicalOnly",
                accent: TabletCoral, font: 22);

            // Stacked directly to the right: ▲ (up) directly above ▼ (down)
            TabletControl(tabletActions, comfortMainGroup, () => "▲", new Vector2(-125, -199), new Vector2(50, 30),
                () => StepTurnPreset(true), allowed: () => Settings != null && Settings.TurnLabel != "PhysicalOnly",
                accent: TabletCoral, font: 18);

            TabletControl(tabletActions, comfortMainGroup, () => "▼", new Vector2(-125, -231), new Vector2(50, 30),
                () => StepTurnPreset(false), allowed: () => Settings != null && Settings.TurnLabel != "PhysicalOnly",
                accent: TabletCoral, font: 18);

            // Right: Recenter height & Reset to default (blue)
            TabletControl(tabletActions, comfortMainGroup, () => "Recenter height", new Vector2(105, -215), new Vector2(230, 64),
                () => Settings?.RecalibrateSeatedHeight(), allowed: () => Settings != null, accent: TabletCoral, font: 22);

            TabletControl(tabletActions, comfortMainGroup, () => "Reset default", new Vector2(360, -215), new Vector2(210, 64),
                () => ResetSeatedHeightDefault(), allowed: () => Settings != null, accent: new Color(.18f, .55f, .95f), font: 22);

            BuildComfortSliderOverlay(page);
            SharpenMovementUI(comfortMainGroup);
            SharpenMovementUI(comfortSliderGroup);
            SharpenMovementUI(tabletNav);

        }

        void MovementIcon(int index, Vector2 position, float size)
        {
            var image = new GameObject("Movement icon " + index, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(comfortMainGroup, false);
            image.texture = Resources.Load<Texture2D>("PlayerTabletArt/MovementIcons");
            image.uvRect = new Rect((index % 3) / 3f, (2 - index / 3) / 3f, 1f / 3, 1f / 3);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.raycastTarget = false;
        }

        void MovementToggle(string title, int icon, float y, Func<bool> value, Action<bool> set)
        {
            MovementIcon(icon, new Vector2(226, y), 58);
            TabletText(comfortMainGroup, title, new Vector2(310, y), new Vector2(110, 48), 20, TextAnchor.MiddleLeft);
            var control = TabletControl(tabletActions, comfortMainGroup, () => value() ? "On" : "Off",
                new Vector2(425, y), new Vector2(100, 48), () => set(!value()), value,
                accent: TabletPanel, font: 22);
            control.Button.name = title + " toggle";
        }

        static Sprite movementPill;
        static void SharpenMovementUI(RectTransform root)
        {
            if (!movementPill)
            {
                const int size = 128;
                const float radius = 40;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { name = "Movement rounded controls", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var q = new Vector2(Mathf.Abs(x + .5f - size / 2f), Mathf.Abs(y + .5f - size / 2f)) - Vector2.one * (size / 2f - radius);
                    float edge = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - edge));
                }
                texture.SetPixels32(pixels); texture.Apply(false, true);
                movementPill = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.sprite == CandyHudVisuals.Pill)
                {
                    image.sprite = movementPill;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 1.76f;
                }
            // Fredoka is already a bold font; synthetic bold blurs its small counters.
            foreach (var text in root.GetComponentsInChildren<Text>(true)) text.fontStyle = FontStyle.Normal;
        }

        void BuildComfortSliderOverlay(RectTransform page)
        {
            comfortSliderGroup = new GameObject("ComfortSliderOverlay", typeof(RectTransform)).GetComponent<RectTransform>();
            comfortSliderGroup.SetParent(page, false);
            comfortSliderGroup.anchorMin = Vector2.zero; comfortSliderGroup.anchorMax = Vector2.one;
            comfortSliderGroup.sizeDelta = Vector2.zero;

            TabletRule(comfortSliderGroup, Vector2.zero, new Vector2(990, 580), new Color(.12f, .05f, .18f, .98f),true);

            comfortSliderTitle = TabletText(comfortSliderGroup, "TURN SPEED FINE TUNING", new Vector2(0, 215), new Vector2(900, 50), 38);
            comfortSliderValueText = TabletText(comfortSliderGroup, "120°/s", new Vector2(0, 140), new Vector2(700, 68), 58);
            comfortSliderValueText.supportRichText = true;

            // 1-degree & 10-degree step buttons
            TabletControl(tabletActions, comfortSliderGroup, () => "-10°", new Vector2(-300, 55), new Vector2(130, 58),
                () => AdjustComfortValue(-10f), font: 24, accent: TabletCoral);

            TabletControl(tabletActions, comfortSliderGroup, () => "-1°", new Vector2(-110, 55), new Vector2(120, 58),
                () => AdjustComfortValue(-1f), font: 24, accent: TabletCoral);

            TabletControl(tabletActions, comfortSliderGroup, () => "+1°", new Vector2(110, 55), new Vector2(120, 58),
                () => AdjustComfortValue(1f), font: 24, accent: TabletCoral);

            TabletControl(tabletActions, comfortSliderGroup, () => "+10°", new Vector2(300, 55), new Vector2(130, 58),
                () => AdjustComfortValue(10f), font: 24, accent: TabletCoral);

            // Preset shortcut row
            float[] presets = { 30, 60, 90, 120, 180, 240, 360 };
            for (int i = 0; i < presets.Length; i++)
            {
                float p = presets[i];
                TabletControl(tabletActions, comfortSliderGroup, () => $"{p:0}°", new Vector2(-390 + i * 130, -25), new Vector2(115, 52),
                    () => {
                        if (Settings?.TurnLabel == "Snap") ApplyTabletSnapAngle(Mathf.Clamp(p, 15f, 90f));
                        else ApplyTabletSmoothSpeed(p);
                        UpdateComfortSliderUI();
                    },
                    () => {
                        float cur = Settings?.TurnLabel == "Snap" ? (Settings?.SnapAngle ?? 0) : (Settings?.SmoothTurnSpeed ?? 0);
                        return Mathf.Approximately(cur, p);
                    },
                    font: 22, accent: TabletPanel);
            }

            // Scrub step buttons
            TabletControl(tabletActions, comfortSliderGroup, () => "<< MIN 15°", new Vector2(-280, -100), new Vector2(165, 52),
                () => {
                    if (Settings?.TurnLabel == "Snap") ApplyTabletSnapAngle(15f);
                    else ApplyTabletSmoothSpeed(15f);
                    UpdateComfortSliderUI();
                }, font: 20, accent: TabletPanel);

            TabletControl(tabletActions, comfortSliderGroup, () => "-25°", new Vector2(-95, -100), new Vector2(125, 52),
                () => AdjustComfortValue(-25f), font: 20, accent: TabletPanel);

            TabletControl(tabletActions, comfortSliderGroup, () => "+25°", new Vector2(95, -100), new Vector2(125, 52),
                () => AdjustComfortValue(25f), font: 20, accent: TabletPanel);

            TabletControl(tabletActions, comfortSliderGroup, () => "MAX >>", new Vector2(280, -100), new Vector2(165, 52),
                () => {
                    if (Settings?.TurnLabel == "Snap") ApplyTabletSnapAngle(90f);
                    else ApplyTabletSmoothSpeed(360f);
                    UpdateComfortSliderUI();
                }, font: 20, accent: TabletPanel);

            // Back / Done Button
            TabletControl(tabletActions, comfortSliderGroup, () => "◄ Done", new Vector2(0, -195), new Vector2(440, 64),
                CloseComfortSlider, accent: CandyHudVisuals.Mint, font: 28);

            comfortSliderGroup.gameObject.SetActive(false);
        }

        void SetTabletTurn(string turn)
        {
            var settings=Settings;
            if(settings==null||settings.TurnLabel==turn) return;
            var rules=JsonUtility.FromJson<MatchRules>(JsonUtility.ToJson(settings.CurrentRules));
            rules.turn=turn;
            settings.ApplyMatchRules(rules);
        }

        void SetTabletHand(bool left)
        {
            var controls=session.Player?.gameObject.GetComponent<IBlasterControls>()??session as IBlasterControls;
            controls?.SetLeftHanded(left);
            tabletRefreshAt=0;
        }

        void BuildTabletControls(RectTransform page)
        {
            TabletText(page,"CONTROLS",new(0,211),new(960,60),42);
            tabletInputLayout=TabletText(page,"",new(0,157),new(900,38),23);
            tabletInputLayout.color=CandyHudVisuals.Muted;
            TabletRule(page,new(-245,-35),new(466,330),TabletPanel,true);
            TabletRule(page,new(245,-35),new(466,330),TabletPanel,true);
            leftInputTitle=TabletText(page,"LEFT HAND",new(-245,89),new(420,38),27);
            rightInputTitle=TabletText(page,"RIGHT HAND",new(245,89),new(420,38),27);
            leftController=TabletPicture(page,CandyHudVisuals.ControllerDiagram(0,true),new(-245,-10),new(125,126));
            rightController=TabletPicture(page,CandyHudVisuals.ControllerDiagram(0,false),new(245,-10),new(125,126));
            leftControllerHelp=TabletText(page,"",new(-245,-125),new(426,106),24);
            rightControllerHelp=TabletText(page,"",new(245,-125),new(426,106),24);
            if (session is not ILobbyControls)
                TabletControl(tabletActions,page,()=>"Replay quick tutorial",new(0,-251),new(480,58),()=>{
                    (session as Component)?.GetComponent<CandyDemoHud>()?.ReplayIntro();
                    session.SetMenu(false);
                },accent:TabletCream);
        }

        RawImage TabletPicture(RectTransform parent,Texture texture,Vector2 p,Vector2 size)
        {
            var picture=new GameObject("Tablet illustration",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            picture.rectTransform.SetParent(parent,false);
            picture.rectTransform.anchoredPosition=p;
            picture.rectTransform.sizeDelta=size;
            picture.texture=texture;
            picture.raycastTarget=false;
            return picture;
        }

        void BuildTabletRoom(RectTransform page)
        {
            TabletText(page,"ROOM",new(0,211),new(960,60),42);
            TabletControl(tabletActions,page,()=>"Nearby requests ("+(NearbyRoomUi.Controls?.JoinRequests.Count??0)+")",new(-244,-220),new(456,66),()=>{showJoinRequests=true;bluetoothPage=0;OpenPlayerTabletPage(TabletPage.Nearby);});
            TabletControl(tabletActions,page,()=>"My player name",new(244,-220),new(456,66),OpenNearbyProfile);
            TabletRule(page,new(0,125),new(944,94),TabletPanel,true);
            tabletRoomStatus=TabletText(page,"",new(0,125),new(900,82),26);
            TabletText(page,"ROOM ACTIONS",new(0,49),new(940,30),19).color=CandyHudVisuals.Muted;
            TabletText(page,"PLAYER & REQUESTS",new(0,-165),new(940,30),19).color=CandyHudVisuals.Muted;
            TabletControl(tabletActions,page,()=>"Invite nearby",new(-244,-12),new(456,68),
                ()=>session.Multiplayer?.InviteNearby(),allowed:()=>session.Multiplayer?.CanInviteNearby==true);
            TabletControl(tabletActions,page,()=>"Copy room code",new(244,-12),new(456,68),()=>{
                GUIUtility.systemCopyBuffer=MultiplayerPanel.JoinCode(session.Multiplayer);
            },allowed:()=>session.Multiplayer!=null&&!string.IsNullOrEmpty(MultiplayerPanel.JoinCode(session.Multiplayer)));
            TabletControl(tabletActions,page,()=>InLobbyVisit?"Return to match":
                session is ILobbyControls?"Join by room code":MultiplayerPanel.LobbyAction(session.Multiplayer),
                new(-244,-94),new(456,68),TabletRoomTravel,
                allowed:()=>session is ILobbyControls lobby?(InLobbyVisit?lobby.CanStart:lobby.JoinEnabled):MatchRuntime.Current!=null);
            TabletControl(tabletActions,page,()=>{
                var room=session.Multiplayer;
                if(room==null||!room.CanLeave)return "Leave room";
                if(!room.IsClient)return "Leave / transfer host";
                return roomLeave.Pending(room,MultiplayerPanel.JoinCode(room),Time.unscaledTime)?"Confirm disconnect":"Leave room";
            },new(244,-94),new(456,68),()=>{
                var room=session.Multiplayer;
                if(room!=null&&room.CanLeave&&roomLeave.Select(room,MultiplayerPanel.JoinCode(room),room.IsClient,Time.unscaledTime))
                    room.LeaveSession();
            },allowed:()=>session.Multiplayer?.CanLeave==true,accent:TabletCoral);
        }

        bool InLobbyVisit => session is ILobbyControls && (session as ILobbyMatchVisitControls)?.VisitingLobby == true;

        internal void ResetLobbyContext()
        {
            OpenPlayerTabletPage(DeveloperTabAvailable && tabletPage == TabletPage.Developer
                ? TabletPage.Developer : TabletPage.Comfort);
        }

        void TabletRoomTravel()
        {
            if (session is ILobbyControls lobby)
            {
                if (InLobbyVisit) { lobby.StartOrCancel(); return; }
                session.SetMenu(false);
                (session as Component)?.GetComponent<IslandLobbyView>()?.OpenRoomCodeFromTablet();
            }
            else MatchRuntime.Current?.ReturnToLobby();
        }

        void BuildTabletDeveloper(RectTransform page)
        {
            developerTiles.Clear();
            tabletDeveloperGrid = new GameObject("Developer grid", typeof(RectTransform)).GetComponent<RectTransform>();
            tabletDeveloperGrid.SetParent(page, false);
            tabletDeveloperGrid.anchoredPosition = Vector2.zero;

            TabletText(tabletDeveloperGrid,"DEVELOPER",new(-232,211),new(520,72),54,TextAnchor.MiddleLeft);
            TabletText(tabletDeveloperGrid,"Quick tools",new(396,208),new(190,56),27);

            DeveloperTile(tabletDeveloperGrid,"Weapons",0,CandyHudIcon.Trigger,OpenWeaponWheel,
                ()=>CanCommand&&MatchRuntime.Current is IDemoEquipmentControls,
                CandyEquipmentView.Outline(EquipmentKind.Blaster),opensDetails:false);
            DeveloperTile(tabletDeveloperGrid,"Bots",1,CandyHudIcon.Survivor,()=>OpenDeveloperRoute("Bots"));
            DeveloperTile(tabletDeveloperGrid,"Disasters",2,CandyHudIcon.Flight,()=>{ResetPages();disasterPage=true;},()=>Demo!=null);
            DeveloperTile(tabletDeveloperGrid,"Map",3,CandyHudIcon.Home,()=>{ResetPages();mapPage=true;menuSection=1;});
            DeveloperTile(tabletDeveloperGrid,"Mode",4,CandyHudIcon.Play,()=>{ResetPages();if(session is ILobbyControls){mapPage=true;menuSection=0;}else modePage=modeWheel=true;});
            DeveloperTile(tabletDeveloperGrid,"Third person",5,CandyHudIcon.Look,()=>session.Player?.ThirdPerson?.Begin(),
                ()=>session.Player?.ThirdPerson?.CanInspect==true,opensDetails:false);
            DeveloperTile(tabletDeveloperGrid,"God mode",6,CandyHudIcon.Survivor,ToggleDeveloperGodMode,
                ()=>DeveloperGodMode?.CanSetGodMode==true,opensDetails:false,title:()=>"God mode: "+(DeveloperGodMode?.GodModeEnabled==true?"ON":"OFF"));
            DeveloperTile(tabletDeveloperGrid,"Become Hand",7,CandyHudIcon.Hand,SwitchDemoRole,()=>DemoMode,opensDetails:false,
                title:()=>session.PlayingHand?"Become Survivor":"Become Hand");
            DeveloperTile(tabletDeveloperGrid,"Search tools",8,CandyHudIcon.Help,OpenToolSearch,opensDetails:false);
            DeveloperTile(tabletDeveloperGrid,"Rebuild",9,CandyHudIcon.Home,()=>session.ResetMap(),()=>session.CanEditWorld,opensDetails:false);
            DeveloperTile(tabletDeveloperGrid,"Spectate",10,CandyHudIcon.Look,()=>{ResetPages();spectatePage=true;},()=>session is not ILobbyControls);
            DeveloperTile(tabletDeveloperGrid,"More tools",11,CandyHudIcon.Settings,()=>OpenDeveloperRoute("More tools"));

            TabletText(tabletDeveloperGrid,"God flight: look + move / hold sprint for fast / release to brake",new(0,-278),new(990,32),19);
            BuildTabletWeaponWheel(page);
        }

        static string CleanWeaponSlotName(EquipmentKind kind) => kind switch
        {
            EquipmentKind.BoxingGlove => "Boxing",
            EquipmentKind.MurderSword => "Murderer",
            EquipmentKind.Dagger => "Dagger",
            EquipmentKind.SawStaff => "Saw Staff",
            EquipmentKind.LaserAxe => "Laser Axe",
            EquipmentKind.BarbedBat => "Bat",
            EquipmentKind.Chainsaw => "Chainsaw",
            EquipmentKind.LaserClaws => "Claws",
            EquipmentKind.FlamingMarshmallow => "Flaming",
            EquipmentKind.NeedleDrill => "Drill",
            EquipmentKind.SheriffHammer => "Sheriff",
            EquipmentKind.StockPistol => "Pistol",
            EquipmentKind.DualUzi => "Spiker Uzis",
            EquipmentKind.DualRifle => "Rifle",
            EquipmentKind.RocketLauncher => "Rocket",
            EquipmentKind.GrenadeLauncher => "Grenade",
            EquipmentKind.ChainGun => "Chain Gun",
            EquipmentKind.WristLaser => "Wrist Laser",
            EquipmentKind.Blaster => "Blaster",
            _ => EquipmentSpecifications.Get(kind).name
        };

        void BuildTabletWeaponWheel(RectTransform page)
        {
            tabletDeveloperWeapons = new GameObject("Developer weapons wheel", typeof(RectTransform)).GetComponent<RectTransform>();
            tabletDeveloperWeapons.SetParent(page, false);
            tabletDeveloperWeapons.anchoredPosition = Vector2.zero;
            tabletDeveloperWeapons.gameObject.SetActive(false);

            TabletText(tabletDeveloperWeapons, "RANGED WEAPONS", new Vector2(-310, 218), new Vector2(280, 36), 22);
            TabletText(tabletDeveloperWeapons, "MELEE WEAPONS", new Vector2(65, 218), new Vector2(280, 36), 22);
            TabletText(tabletDeveloperWeapons, "POWERUPS", new Vector2(420, 218), new Vector2(200, 36), 22);

            var rangedItems = WheelGroups[0];
            var meleeItems = WheelGroups[1];
            var powerupItems = WheelGroups[2];

            int totalSlots = rangedItems.Length + meleeItems.Length + powerupItems.Length;
            tabletWheelSlotButtons = new TabletButton[totalSlots];
            tabletWheelSlotPictures = new RawImage[totalSlots];
            int slotIdx = 0;

            // Keep the existing three groups visible with readable, aligned choices.
            Vector2 rangedCenter = new Vector2(420, -218);
            for (int i = 0; i < rangedItems.Length; i++)
            {
                var kind = rangedItems[i];
                Vector2 pos = new Vector2(-402 + (i % 2) * 184, 154 - (i / 2) * 74);
                string name = CleanWeaponSlotName(kind);
                int cur = slotIdx++;
                var slot = TabletControl(tabletActions, tabletDeveloperWeapons, () => name,
                    pos, new Vector2(176, 66), () => SelectWeaponKind(kind),
                    selected: () => tabletWeaponSelected == kind, font: 16);
                slot.Label.resizeTextForBestFit = true;
                slot.Label.resizeTextMinSize = 15;
                slot.Label.resizeTextMaxSize = 16;
                slot.Label.rectTransform.anchoredPosition = new Vector2(28, 0);
                slot.Label.rectTransform.sizeDelta = new Vector2(112, 56);
                var pic = TabletPicture((RectTransform)slot.Button.transform, null, new Vector2(-57, 0), new Vector2(52, 46));
                tabletWheelSlotButtons[cur] = slot;
                tabletWheelSlotPictures[cur] = pic;
                SetPictureSprite(pic, CandyWeaponIcons.Get(kind), kind);
            }

            TabletControl(tabletActions, tabletDeveloperWeapons, () => tabletGiveAll ? "Target: everyone" : "Target: myself",
                rangedCenter, new Vector2(210, 40), () => {
                    tabletGiveAll = !tabletGiveAll;
                    if (!tabletGiveAll) tabletRepeat = false;
                }, font: 17, accent: TabletPanel);

            Vector2 meleeCenter = new Vector2(-430, -214);
            for (int i = 0; i < meleeItems.Length; i++)
            {
                var kind = meleeItems[i];
                Vector2 pos = new Vector2(-26 + (i % 2) * 184, 154 - (i / 2) * 74);
                string name = CleanWeaponSlotName(kind);
                int cur = slotIdx++;
                var slot = TabletControl(tabletActions, tabletDeveloperWeapons, () => name,
                    pos, new Vector2(176, 66), () => SelectWeaponKind(kind),
                    selected: () => tabletWeaponSelected == kind, font: 16);
                slot.Label.resizeTextForBestFit = true;
                slot.Label.resizeTextMinSize = 15;
                slot.Label.resizeTextMaxSize = 16;
                slot.Label.rectTransform.anchoredPosition = new Vector2(28, 0);
                slot.Label.rectTransform.sizeDelta = new Vector2(112, 56);
                var pic = TabletPicture((RectTransform)slot.Button.transform, null, new Vector2(-57, 0), new Vector2(52, 46));
                tabletWheelSlotButtons[cur] = slot;
                tabletWheelSlotPictures[cur] = pic;
                SetPictureSprite(pic, CandyWeaponIcons.Get(kind), kind);
            }

            var centerBtn = TabletControl(tabletActions, tabletDeveloperWeapons, () => "",
                meleeCenter, new Vector2(96, 60), GiveSelectedWeapon, font: 11, accent: TabletPanel);
            tabletCenterWeaponPicture = TabletPicture((RectTransform)centerBtn.Button.transform, null, Vector2.zero, new Vector2(80, 48));

            // Powerups Column on Far Right (4 slots)
            for (int i = 0; i < powerupItems.Length; i++)
            {
                var kind = powerupItems[i];
                string name = EquipmentSpecifications.Get(kind).name;
                int cur = slotIdx++;
                var slot = TabletControl(tabletActions, tabletDeveloperWeapons, () => name,
                    new Vector2(420, 140 - i * 58), new Vector2(210, 52), () => SelectWeaponKind(kind),
                    selected: () => tabletWeaponSelected == kind, font: 17);
                slot.Label.rectTransform.anchoredPosition = new Vector2(25, 0);
                slot.Label.rectTransform.sizeDelta = new Vector2(150, 44);
                slot.Label.alignment = TextAnchor.MiddleLeft;
                var pic = TabletPicture((RectTransform)slot.Button.transform, null, new Vector2(-75, 0), new Vector2(42, 42));
                tabletWheelSlotButtons[cur] = slot;
                tabletWheelSlotPictures[cur] = pic;
                SetPictureSprite(pic, CandyWeaponIcons.Get(kind), kind);
            }

            // Action Buttons under Powerups Column
            TabletControl(tabletActions, tabletDeveloperWeapons, () => "GIVE SELECTED",
                new Vector2(420, -112), new Vector2(210, 52), GiveSelectedWeapon,
                allowed: CanGiveSelectedWeapon, accent: CandyHudVisuals.Mint, font: 22);

            TabletControl(tabletActions, tabletDeveloperWeapons, () => IsWeaponDrawn() ? "Holster weapon" : "Draw weapon",
                new Vector2(420, -170), new Vector2(210, 44), ToggleHolster,
                allowed: CanToggleHolster, font: 17, accent: TabletCoral);

            // Selection info & stats
            tabletSelectedWeaponTitle = TabletText(tabletDeveloperWeapons, "", new Vector2(-95, -202), new Vector2(540, 30), 20);
            tabletSelectedWeaponStats = TabletText(tabletDeveloperWeapons, "", new Vector2(-95, -230), new Vector2(540, 30), 15);

            // Bottom Merged Row: Back to Developer Tools
            TabletControl(tabletActions, tabletDeveloperWeapons, () => "◄ Back to Developer Tools",
                new Vector2(0, -287), new Vector2(980, 48), CloseWeaponWheel, accent: TabletPanel, font: 24);
        }

        void SelectWeaponKind(EquipmentKind kind)
        {
            tabletWeaponSelected = kind;
            SetPictureSprite(tabletCenterWeaponPicture, CandyWeaponIcons.Get(tabletWeaponSelected), tabletWeaponSelected);
            RefreshWeaponWheelStats();
        }

        void OpenWeaponWheel()
        {
            tabletWeaponsOpen = true;
            if (tabletDeveloperGrid) tabletDeveloperGrid.gameObject.SetActive(false);
            if (tabletDeveloperWeapons) tabletDeveloperWeapons.gameObject.SetActive(true);
            UpdateWeaponWheelIcons();
            UpdateDevBackButtonState();
        }

        void CloseWeaponWheel()
        {
            tabletWeaponsOpen = false;
            if (tabletDeveloperGrid) tabletDeveloperGrid.gameObject.SetActive(true);
            if (tabletDeveloperWeapons) tabletDeveloperWeapons.gameObject.SetActive(false);
            UpdateDevBackButtonState();
        }

        void UpdateWeaponWheelIcons()
        {
            SetPictureSprite(tabletCenterWeaponPicture, CandyWeaponIcons.Get(tabletWeaponSelected), tabletWeaponSelected);
            RefreshWeaponWheelStats();
        }

        void SetPictureSprite(RawImage image, Sprite sprite, EquipmentKind kind)
        {
            if (image == null) return;
            if (sprite != null && sprite.texture != null)
            {
                image.texture = sprite.texture;
                var r = sprite.rect;
                float tw = sprite.texture.width, th = sprite.texture.height;
                image.uvRect = new Rect(r.x / tw, r.y / th, r.width / tw, r.height / th);
                image.color = Color.white;
                image.gameObject.SetActive(true);
            }
            else
            {
                var outline = CandyEquipmentView.Outline(kind);
                if (outline != null)
                {
                    image.texture = outline;
                    image.uvRect = new Rect(0, 0, 1, 1);
                    image.color = Color.white;
                    image.gameObject.SetActive(true);
                }
                else
                {
                    var hudIcon = kind == EquipmentKind.Jump ? CandyHudIcon.Jump :
                                  kind == EquipmentKind.Speed ? CandyHudIcon.Move :
                                  kind == EquipmentKind.FlyingHand ? CandyHudIcon.Hand : CandyHudIcon.Key;
                    image.texture = CandyHudVisuals.Icon(hudIcon);
                    image.uvRect = new Rect(0, 0, 1, 1);
                    image.color = kind == EquipmentKind.Jump ? new Color(.25f, 1, .3f) :
                                  kind == EquipmentKind.Speed ? new Color(.25f, .7f, 1) : CandyHudVisuals.Mint;
                    image.gameObject.SetActive(true);
                }
            }
        }

        void RefreshWeaponWheelStats()
        {
            if (tabletSelectedWeaponTitle == null || tabletSelectedWeaponStats == null) return;
            var spec = EquipmentSpecifications.Get(tabletWeaponSelected);
            tabletSelectedWeaponTitle.text = spec.name;
            if (spec.power)
            {
                tabletSelectedWeaponStats.text = tabletWeaponSelected == EquipmentKind.FlyingHand
                    ? "Giant hand transformation\n30s duration"
                    : "Powerup booster\n20s duration";
            }
            else if (tabletWeaponSelected == EquipmentKind.PoweredArmor)
            {
                tabletSelectedWeaponStats.text = "Powered Iron Suit\nFlight & supersonic sprint";
            }
            else if (spec.damage > 0)
            {
                string mag = spec.magazine > 0 ? spec.magazine.ToString() : "∞";
                tabletSelectedWeaponStats.text = $"Damage: {spec.damage:0}   Range: {spec.range:0}m\nMag: {mag}   Rate: {1f/Mathf.Max(0.05f, spec.cooldown):0.0}/s";
            }
            else
            {
                tabletSelectedWeaponStats.text = "Standard equipment";
            }
        }

        bool CanGiveSelectedWeapon()
        {
            if (!CanCommand) return false;
            var runtime = MatchRuntime.Current;
            if (runtime == null) return false;
            if (tabletGiveAll)
            {
                return runtime is IDemoLoadoutControls && tabletWeaponSelected != EquipmentKind.FlyingHand;
            }
            return runtime is IDemoEquipmentControls && !session.PlayingHand && (Demo as ISpectatorControls)?.Spectating != true;
        }

        void GiveSelectedWeapon()
        {
            var runtime = MatchRuntime.Current;
            var self = runtime as IDemoEquipmentControls;
            var everyone = runtime as IDemoLoadoutControls;
            if (tabletGiveAll)
            {
                if (everyone != null && everyone.GiveEveryoneEquipment(tabletWeaponSelected, tabletRepeat))
                    session.SetMenu(false);
            }
            else
            {
                if (self != null && self.GiveEquipment(tabletWeaponSelected))
                    session.SetMenu(false);
            }
        }

        void StopRespawnGrant()
        {
            (MatchRuntime.Current as IDemoLoadoutControls)?.GiveEveryoneEquipment(EquipmentKind.None, false);
        }

        bool IsWeaponDrawn() => session.Player?.gameObject.GetComponent<IEquipmentControls>()?.Equipment.drawn == true;
        bool CanToggleHolster() => session.Player?.gameObject.GetComponent<IEquipmentControls>()?.Equipment.kind >= EquipmentKind.Blaster;
        void ToggleHolster()
        {
            var equipment = session.Player?.gameObject.GetComponent<IEquipmentControls>();
            if (equipment != null) equipment.SetEquipped(!equipment.Equipment.drawn);
        }

        void DeveloperTile(RectTransform page,string label,int slot,CandyHudIcon icon,Action action,
            Func<bool> allowed=null,Texture picture=null,bool opensDetails=true,Func<string> title=null)
        {
            Vector2 position=new((slot%3-1)*344,122-slot/3*108);
            var tile=TabletControl(tabletActions,page,title??(()=>label),position,new(312,94),
                ()=>{if(opensDetails)ShowDeveloperDetails(action);else action();},allowed:allowed,font:27);
            tile.Label.rectTransform.anchoredPosition=new Vector2(46,0);
            tile.Label.rectTransform.sizeDelta=new Vector2(206,82);
            developerTiles.Add((label,action,allowed,opensDetails,title));
            string artName=label switch {"Become Hand"=>"BecomeHand","Search tools"=>"SearchTools","Map"=>"MapMode","Bots"=>"BotsV2","Weapons"=>"WeaponsV2","God mode"=>"GodMode","Third person"=>"ThirdPerson","More tools"=>"MoreTools",_=>label};
            var approved=Resources.Load<Texture2D>("PlayerTabletArt/"+artName);
            Texture art=approved?approved:picture?picture:CandyHudVisuals.Icon(icon);
            float scale=Mathf.Min(72f/art.width,72f/art.height);
            TabletPicture((RectTransform)tile.Button.transform,art,new(-105,0),
                new(art.width*scale,art.height*scale));
            if(label=="More tools")
            {
                tile.ToolTile=true;tile.Button.image.sprite=ToolTileSprite;tile.Button.image.type=Image.Type.Simple;
                tile.Label.rectTransform.anchoredPosition=new Vector2(29,0);tile.Label.rectTransform.sizeDelta=new Vector2(184,82);
                var chevron=TabletText((RectTransform)tile.Button.transform,"›",new(133,0),new(32,64),40);
                chevron.color=CandyHudVisuals.Mint;chevron.raycastTarget=false;
            }
        }

        void ShowDeveloperDetails(Action action)
        {
            if (!DeveloperTabAvailable) return;
            action();
            tabletDeveloperDetails=true;
            tabletPages[(int)TabletPage.Developer].gameObject.SetActive(false);
            if(tabletNav)tabletNav.gameObject.SetActive(true);
            legacyDeveloper.gameObject.SetActive(true);
            UpdateDevBackButtonState();
        }

        void CloseDeveloperDetails()
        {
            developerRoute="";developerChild=false;developerVolumeApply=null;
            RestoreEquipmentWheelLayout();
            ResetPages();
            tabletDeveloperDetails = false;
            disasterPage = false;
            disasterSelection = -1;
            modePage = modeWheel = false;
            mapPage = false;
            spectatePage = false;
            batchPage = false;
            menuCategory = menuSection = -1;
            if (disasterWheelUI) disasterWheelUI.SetActive(false);
            if (modeWheelUI) modeWheelUI.SetActive(false);
            if (legacyDeveloper) legacyDeveloper.gameObject.SetActive(false);
            if (tabletNav) tabletNav.gameObject.SetActive(true);
            if (tabletPages[(int)TabletPage.Developer]) tabletPages[(int)TabletPage.Developer].gameObject.SetActive(true);
            if (tabletDeveloperGrid) tabletDeveloperGrid.gameObject.SetActive(true);
            if (tabletDeveloperWeapons) tabletDeveloperWeapons.gameObject.SetActive(false);
            tabletRefreshAt = 0;
            UpdateDevBackButtonState();
        }

        bool IsDevBackButtonActive()
        {
            if (tabletPage != TabletPage.Developer) return false;
            return tabletWeaponsOpen || tabletDeveloperDetails || disasterPage || modePage || mapPage ||
                   spectatePage || batchPage || menuCategory >= 0 || menuSection >= 0 || disasterSelection >= 0 || comfortSliderOpen;
        }

        void UpdateDevBackButtonState()
        {
            if (tabletDevBackButton == null || tabletDevBackButton.Button == null) return;
            bool active = IsDevBackButtonActive();
            tabletDevBackButton.Button.interactable = active;
            tabletDevBackButton.Button.image.color = active ? CandyHudVisuals.Mint : new Color(0.25f, 0.15f, 0.35f, 0.35f);
            if (tabletDevBackButton.Label != null)
                tabletDevBackButton.Label.color = active ? CandyHudVisuals.Ink : new Color(1f, 1f, 1f, 0.35f);
        }

        void OnDeveloperBackClicked()
        {
            if (!IsDevBackButtonActive()) return;
            if(developerVolumeApply!=null){developerVolumeApply=null;return;}
            if(randomPowerupPage&&developerPowerupChoice>=0){developerPowerupChoice=-1;return;}
            if(mapPage&&developerMapSearchOpen){developerMapSearchOpen=false;return;}
            if(mapPage&&developerMapFiltersOpen){developerMapFiltersOpen=false;return;}
            if(modePage&&developerModeChoice!=null){developerModeChoice=null;return;}
            if(developerRoute.Length>0){if(developerChild){RestoreEquipmentWheelLayout();ResetPages();menuCategory=menuSection=-1;developerChild=false;}else CloseDeveloperDetails();return;}

            if (comfortSliderOpen)
            {
                CloseComfortSlider();
                return;
            }
            if (tabletWeaponsOpen)
            {
                CloseWeaponWheel();
                return;
            }
            if (disasterPage)
            {
                if (disasterSelection >= 0)
                {
                    disasterSelection = -1;
                    UpdateDevBackButtonState();
                    return;
                }
                CloseDeveloperDetails();
                return;
            }
            if (modePage)
            {
                CloseDeveloperDetails();
                return;
            }
            if (mapPage)
            {
                mapPage = false;
                CloseDeveloperDetails();
                return;
            }
            if (spectatePage)
            {
                spectatePage = false;
                CloseDeveloperDetails();
                return;
            }
            if (batchPage)
            {
                batchPage = false;
                batchTarget = 0;
                UpdateDevBackButtonState();
                return;
            }
            if (menuSection >= 0)
            {
                menuSection = -1;
                UpdateDevBackButtonState();
                return;
            }
            if (menuCategory >= 0)
            {
                CloseDeveloperDetails();
                return;
            }
            if (tabletDeveloperDetails)
            {
                CloseDeveloperDetails();
                return;
            }
        }

        void PointPlayerTablet(Camera camera,bool pressed,bool held)
        {
            if(tabletPage==TabletPage.Developer&&!DeveloperTabAvailable) OpenPlayerTabletPage(TabletPage.Comfort);
            UpdateDevBackButtonState();
            if(PointTabletButtons(tabletTabs,pressed)) return;
            if(tabletDeveloperDetails){PointPage(camera,pressed,held);return;}
            if(Time.unscaledTime>=tabletRefreshAt)
            {
                tabletRefreshAt=Time.unscaledTime+.2f;
                RefreshTabletNearby();
                RefreshToolSearch();
                if(tabletPage==TabletPage.Controls) RefreshTabletControls(camera);
                if(tabletPage==TabletPage.Room)
                {
                    var room=session.Multiplayer;
                    tabletRoomStatus.text=room==null||string.IsNullOrWhiteSpace(room.RoomInfo)?"Local play":room.RoomInfo;
                    if(!string.IsNullOrWhiteSpace(room?.InvitationFrom))
                        tabletRoomStatus.text+="\nInvite from "+room.InvitationFrom;
                }
                if (tabletPage == TabletPage.Developer && tabletWeaponsOpen) RefreshWeaponWheelStats();
                if(tabletPage==TabletPage.Comfort)
                {
                    UpdateComfortSliderUI();
                }
            }
            PointTabletButtons(tabletActions,pressed);
        }

        bool PointTabletButtons(List<TabletButton> controls,bool pressed)
        {
            foreach(var control in controls)
            {
                if(ReferenceEquals(controls,tabletTabs))
                    control.Button.gameObject.SetActive(control.Allowed?.Invoke()!=false);
                if(!control.Button.gameObject.activeInHierarchy) continue;
                bool allowed=control.Allowed?.Invoke()!=false;
                control.Button.interactable=allowed;
                control.Target.enabled=allowed;
                bool hover=Hit(control.Target,pointerRay),selected=control.Selected?.Invoke()==true;
                bool isTab=ReferenceEquals(controls,tabletTabs);
                bool developerTile=tabletPage==TabletPage.Developer&&ReferenceEquals(controls,tabletActions)&&!tabletWeaponsOpen;
                bool juicyClose=control.Accent==JuicyRed;

                if(isTab && control.BasePosition.y > 200 && !juicyClose)
                {
                    if(selected)
                    {
                        float bounce=Mathf.Sin(Time.unscaledTime*5f)*4f;
                        ((RectTransform)control.Button.transform).anchoredPosition=new Vector2(control.BasePosition.x, control.BasePosition.y+bounce);
                        control.Focus.effectDistance=new Vector2(4,-4);
                        control.Focus.effectColor=new Color(CandyHudVisuals.Mint.r,CandyHudVisuals.Mint.g,CandyHudVisuals.Mint.b,0.65f+Mathf.Sin(Time.unscaledTime*6f)*0.3f);
                    }
                    else
                    {
                        ((RectTransform)control.Button.transform).anchoredPosition=control.BasePosition;
                        control.Focus.effectDistance=new Vector2(3,-3);
                        control.Focus.effectColor=hover?CandyHudVisuals.Mint:Color.clear;
                    }
                }
                else
                {
                    control.Focus.effectDistance=new Vector2(3,-3);
                    control.Focus.effectColor=hover?CandyHudVisuals.Mint:control.ToolTile?new Color(.65f,.38f,.8f,.55f):Color.clear;
                }

                if(control.ToolTile)
                {
                    control.Button.image.color=hover?new Color(1.12f,1.12f,1.12f,1):Color.white;
                    control.Label.color=TabletCream;
                }
                else if(juicyClose)
                {
                    control.Button.image.color=hover?new Color(1.18f,1.18f,1.18f,1f):Color.white;
                    control.Label.color=Color.white;
                }
                else if(control == tabletDevBackButton)
                {
                    bool active = IsDevBackButtonActive();
                    control.Button.interactable = active;
                    control.Target.enabled = active;
                    control.Button.image.color = !active ? new Color(0.25f, 0.15f, 0.35f, 0.35f) : (hover ? CandyHudVisuals.Mint : TabletPanel);
                    control.Label.color = !active ? new Color(1f, 1f, 1f, 0.30f) : (hover ? CandyHudVisuals.Ink : CandyHudVisuals.Mint);
                }
                else
                {
                    control.Button.image.color=hover&&developerTile?Color.Lerp(control.Accent,CandyHudVisuals.Mint,.19f):
                        hover||selected?CandyHudVisuals.Mint:
                        allowed?control.Accent:Color.Lerp(control.Accent,TabletPlum,.65f);
                    control.Label.color=hover&&!developerTile||selected||control.Accent==TabletCream&&allowed?TabletPlum:
                        allowed?TabletCream:CandyHudVisuals.Muted;
                }

                string title=(selected&&!isTab?"✓ ":"")+control.Title();
                if(control.Label.text!=title) control.Label.text=title;
                if(!hover||!pressed)continue;
                control.Select();
                MenuAim.Consume();
                return true;
            }
            return false;
        }

        void RefreshTabletControls(Camera camera)
        {
            if (!session.IsVR)
            {
                tabletInputLayout.text="Keyboard + mouse";
                leftInputTitle.text="KEYBOARD"; rightInputTitle.text="MOUSE";
                leftController.enabled=rightController.enabled=true;
                leftController.texture=CandyHudVisuals.Icon(CandyHudIcon.Key);
                rightController.texture=CandyHudVisuals.Icon(CandyHudIcon.Look);
                leftControllerHelp.text="WASD  ·  Move\nShift  ·  Sprint\nSpace  ·  Jump / hold to climb";
                rightControllerHelp.text="Mouse  ·  Look\nLeft click  ·  Use weapon\nEsc  ·  Open menu";
                return;
            }
            bool left=ControllerPose(camera,true,out _,out _),right=ControllerPose(camera,false,out _,out _);
            bool tracked=session.Player?.HasHandTracking==true;
            tabletInputLayout.text=left&&right
                ?"Two controllers  ·  "+(DominantLeft?"Left":"Right")+" dominant"
                :left||right?(left?"Left":"Right")+" controller"+(tracked?" + tracked hand":""):
                tracked?"Hand tracking":"Waiting for tracking";
            leftInputTitle.text="LEFT HAND"; rightInputTitle.text="RIGHT HAND";
            bool reference=!left&&!right&&!tracked;
            leftController.enabled=left||reference;
            rightController.enabled=right||reference;
            if(reference)tabletInputLayout.text="Controller reference · waiting for tracking";
            leftController.texture=CandyHudVisuals.ControllerDiagram(left==DominantLeft?3:1,true);
            rightController.texture=CandyHudVisuals.ControllerDiagram(!DominantLeft?3:1,false);
            leftControllerHelp.text=TabletControllerHelp(true,left||reference,right||reference,tracked);
            rightControllerHelp.text=TabletControllerHelp(false,right||reference,left||reference,tracked);
        }

        string TabletControllerHelp(bool left,bool present,bool other,bool tracked)
        {
            if(!present)return tracked?"Tracked hand\nPinch to select":"Controller inactive";
            if(!other)return "Stick · Move\nGrip + stick · Look\n"+(left?"X · Jump  /  Y · Menu":"A · Jump  /  Grip + B · Menu");
            string roles=left==DominantLeft?"Stick · Look\n"+(left?"X":"A")+" · Jump\nTrigger · Use weapon":
                "Stick · Move\nStick click · Sprint";
            return left?roles+"\nY · Open menu":roles;
        }
    }
}
