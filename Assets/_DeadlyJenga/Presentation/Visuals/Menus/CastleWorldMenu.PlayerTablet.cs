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
        enum TabletPage { Comfort, Controls, Room, Developer }
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
        }

        static readonly Color TabletPlum = new(.20f,.055f,.22f);
        static readonly Color TabletPanel = new(.34f,.15f,.38f);
        static readonly Color TabletCoral = new(.93f,.36f,.35f);
        static readonly Color TabletCream = new(1f,.95f,.83f);
        static readonly Color JuicyRed = new(.92f,.12f,.18f);
        readonly List<TabletButton> tabletTabs = new();
        readonly List<TabletButton> tabletActions = new();
        readonly RectTransform[] tabletPages = new RectTransform[4];
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

        internal const string TabletOpenedPreference = "DeadlyJenga.PlayerTabletOpened";

        bool DeveloperTabAvailable => session is not ILobbyControls && DemoMode &&
            GameExperiencePanel.Resolve(session)?.Experience != GameExperience.Game;

        void BuildPlayerTablet()
        {
            var old = new List<Transform>();
            foreach (Transform child in transform) old.Add(child);
            legacyDeveloper = TabletRoot("Developer detail controls");
            legacyDeveloper.localScale = Vector3.one * .84f;
            legacyDeveloper.anchoredPosition = new Vector2(0,-48);
            foreach (var child in old) child.SetParent(legacyDeveloper,false);

            var nav = TabletRoot("Player tablet tabs");
            tabletNav = nav;
            TabletControl(tabletTabs,nav,()=>"Resume",new(-415,316),new(150,68),()=>session.SetMenu(false));
            AddTab(TabletPage.Comfort,"Comfort",-248,width:160);
            AddTab(TabletPage.Controls,"Controls",-82,width:160);
            AddTab(TabletPage.Room,"Room",84,width:160);
            AddTab(TabletPage.Developer,"Developer",250,()=>DeveloperTabAvailable,width:160);
            TabletControl(tabletTabs,nav,()=>"✕",new(465,316),new(58,58),()=>session.SetMenu(false),accent:JuicyRed,font:38);
            TabletRule(nav,new Vector2(0,270),new Vector2(1010,4),TabletCoral);

            for (int i=0;i<tabletPages.Length;i++)
                tabletPages[i] = TabletRoot(((TabletPage)i)+" page");
            BuildTabletComfort(tabletPages[(int)TabletPage.Comfort]);
            BuildTabletControls(tabletPages[(int)TabletPage.Controls]);
            BuildTabletRoom(tabletPages[(int)TabletPage.Room]);
            BuildTabletDeveloper(tabletPages[(int)TabletPage.Developer]);
            OpenPlayerTabletPage(TabletPage.Comfort);
        }

        void AddTab(TabletPage page,string title,float x,Func<bool> allowed=null,float width=160)
        {
            var nav=(RectTransform)tabletTabs[0].Button.transform.parent;
            TabletControl(tabletTabs,nav,()=>title,new(x,316),new(width,68),()=>OpenPlayerTabletPage(page),
                ()=>tabletPage==page,allowed);
        }

        void OpenPlayerTabletPage(TabletPage page)
        {
            if (page==TabletPage.Developer&&!DeveloperTabAvailable) return;
            RestoreEquipmentWheelLayout();
            ResetPages();
            menuCategory=menuSection=-1;
            tabletPage=page;
            tabletDeveloperDetails=false;
            tabletWeaponsOpen=false;
            comfortSliderOpen=false;
            if(tabletDeveloperGrid)tabletDeveloperGrid.gameObject.SetActive(true);
            if(tabletDeveloperWeapons)tabletDeveloperWeapons.gameObject.SetActive(false);
            tabletRefreshAt=0;
            legacyDeveloper.gameObject.SetActive(false);
            if(tabletNav)tabletNav.gameObject.SetActive(true);
            for (int i=0;i<tabletPages.Length;i++)
                tabletPages[i].gameObject.SetActive(i==(int)page);
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

        void TabletRule(RectTransform parent,Vector2 p,Vector2 size,Color color)
        {
            var image=PointerImage("Candy tablet trim",size);
            image.rectTransform.SetParent(parent,false);
            image.rectTransform.anchoredPosition=p;
            image.color=color;
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
                Select=select,Selected=selected,Allowed=allowed,Accent=accent??TabletPanel};
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

            TabletText(comfortMainGroup, "COMFORT", new Vector2(0, 222), new Vector2(900, 52), 48);

            string[] rowTitles = { "Play position", "Dominant hand", "Sprint mode", "Climb mode", "Turn style (seated)" };
            float[] rowYs = { 152, 84, 16, -52, -122 };

            for (int i = 0; i < 5; i++)
            {
                float y = rowYs[i];
                TabletRule(comfortMainGroup, new Vector2(0, y), new Vector2(990, 60), new Color(.27f, .10f, .30f));
                TabletText(comfortMainGroup, rowTitles[i], new Vector2(-365, y), new Vector2(240, 50), 24, TextAnchor.MiddleLeft);
            }

            // Row 0: Play position (Seated, Standing, Auto-detect)
            TabletControl(tabletActions, comfortMainGroup, () => "Seated", new Vector2(-180, 152), new Vector2(110, 48),
                () => { PlayerPrefs.SetInt("SurvivorVR.PlayPositionAuto", 0); Settings?.UseStationaryPreset(); },
                () => Settings != null && !Settings.StandingPlay && PlayerPrefs.GetInt("SurvivorVR.PlayPositionAuto", 1) == 0,
                () => Settings != null, TabletCoral, font: 22);

            TabletControl(tabletActions, comfortMainGroup, () => "Standing", new Vector2(-60, 152), new Vector2(110, 48),
                () => { PlayerPrefs.SetInt("SurvivorVR.PlayPositionAuto", 0); Settings?.UseStandingPreset(); },
                () => Settings?.StandingPlay == true && PlayerPrefs.GetInt("SurvivorVR.PlayPositionAuto", 1) == 0,
                () => Settings != null, TabletCoral, font: 22);

            TabletControl(tabletActions, comfortMainGroup, () => "Auto", new Vector2(60, 152), new Vector2(110, 48),
                () => {
                    PlayerPrefs.SetInt("SurvivorVR.PlayPositionAuto", 1);
                    PlayerPrefs.Save();
                    session.Player?.gameObject.SendMessage("SetAutoDetectHybrid", true, SendMessageOptions.DontRequireReceiver);
                    tabletRefreshAt = 0;
                },
                () => PlayerPrefs.GetInt("SurvivorVR.PlayPositionAuto", 1) != 0,
                () => true, TabletCoral, font: 22);

            // Row 1: Dominant hand (Left, Right)
            TabletControl(tabletActions, comfortMainGroup, () => "Left", new Vector2(-160, 84), new Vector2(135, 48),
                () => SetTabletHand(true), () => DominantLeft, () => session.Player != null, TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => "Right", new Vector2(-15, 84), new Vector2(135, 48),
                () => SetTabletHand(false), () => !DominantLeft, () => session.Player != null, TabletCoral, font: 24);

            // Row 2: Sprint mode (Toggle, Hold)
            TabletControl(tabletActions, comfortMainGroup, () => "Toggle", new Vector2(-160, 16), new Vector2(135, 48),
                () => SetTabletSprintHold(false), () => !SprintHold, () => true, TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => "Hold", new Vector2(-15, 16), new Vector2(135, 48),
                () => SetTabletSprintHold(true), () => SprintHold, () => true, TabletCoral, font: 24);

            // Row 3: Climb mode (Toggle, Hold)
            TabletControl(tabletActions, comfortMainGroup, () => "Toggle", new Vector2(-160, -52), new Vector2(135, 48),
                () => SetTabletClimbToggle(true), () => ClimbToggle, () => true, TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => "Hold", new Vector2(-15, -52), new Vector2(135, 48),
                () => SetTabletClimbToggle(false), () => !ClimbToggle, () => true, TabletCoral, font: 24);

            // Row 4: Turn style (seated) (Snap, Smooth)
            TabletControl(tabletActions, comfortMainGroup, () => "Snap", new Vector2(-160, -122), new Vector2(135, 48),
                () => SetTabletTurn("Snap"), () => Settings?.TurnLabel == "Snap", () => Settings != null, TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => "Smooth", new Vector2(-15, -122), new Vector2(135, 48),
                () => SetTabletTurn("Smooth"), () => Settings?.TurnLabel == "Smooth", () => Settings != null, TabletCoral, font: 24);

            // Right side of Rows 0-3: Sub-labels & [Y] [N] options
            // Row 0: Invert stick
            TabletText(comfortMainGroup, "Invert stick", new Vector2(250, 152), new Vector2(170, 48), 20, TextAnchor.MiddleRight);
            TabletControl(tabletActions, comfortMainGroup, () => "Y", new Vector2(380, 152), new Vector2(58, 44),
                () => SetTabletInvertStick(true), () => InvertStick, () => true, TabletPanel, font: 20);
            TabletControl(tabletActions, comfortMainGroup, () => "N", new Vector2(448, 152), new Vector2(58, 44),
                () => SetTabletInvertStick(false), () => !InvertStick, () => true, TabletPanel, font: 20);

            // Row 1: Vignette
            TabletText(comfortMainGroup, "Vignette", new Vector2(250, 84), new Vector2(170, 48), 20, TextAnchor.MiddleRight);
            TabletControl(tabletActions, comfortMainGroup, () => "Y", new Vector2(380, 84), new Vector2(58, 44),
                () => SetTabletVignette(true), () => VignetteEnabled, () => true, TabletPanel, font: 20);
            TabletControl(tabletActions, comfortMainGroup, () => "N", new Vector2(448, 84), new Vector2(58, 44),
                () => SetTabletVignette(false), () => !VignetteEnabled, () => true, TabletPanel, font: 20);

            // Row 2: Haptics
            TabletText(comfortMainGroup, "Haptics", new Vector2(250, 16), new Vector2(170, 48), 20, TextAnchor.MiddleRight);
            TabletControl(tabletActions, comfortMainGroup, () => "Y", new Vector2(380, 16), new Vector2(58, 44),
                () => SetTabletHaptics(true), () => HapticsEnabled, () => true, TabletPanel, font: 20);
            TabletControl(tabletActions, comfortMainGroup, () => "N", new Vector2(448, 16), new Vector2(58, 44),
                () => SetTabletHaptics(false), () => !HapticsEnabled, () => true, TabletPanel, font: 20);

            // Row 3: Head bob
            TabletText(comfortMainGroup, "Head bob", new Vector2(250, -52), new Vector2(170, 48), 20, TextAnchor.MiddleRight);
            TabletControl(tabletActions, comfortMainGroup, () => "Y", new Vector2(380, -52), new Vector2(58, 44),
                () => SetTabletHeadBob(true), () => HeadBobEnabled, () => true, TabletPanel, font: 20);
            TabletControl(tabletActions, comfortMainGroup, () => "N", new Vector2(448, -52), new Vector2(58, 44),
                () => SetTabletHeadBob(false), () => !HeadBobEnabled, () => true, TabletPanel, font: 20);

            // Bottom Row (y = -215):
            // Left: Speed / Snap Angle Traversal (▼, Value, ▲)
            TabletControl(tabletActions, comfortMainGroup, () => "▼", new Vector2(-446, -215), new Vector2(52, 64),
                () => StepTurnPreset(false), allowed: () => Settings != null && Settings.TurnLabel != "PhysicalOnly",
                accent: TabletCoral, font: 24);

            TabletControl(tabletActions, comfortMainGroup, () => Settings?.TurnLabel == "Snap"
                ? $"Snap angle  {Settings.SnapAngle:0}°" : Settings?.TurnLabel == "PhysicalOnly"
                ? "Physical turning" : $"Turn speed  {Settings?.SmoothTurnSpeed:0}°/s",
                new Vector2(-262, -215), new Vector2(296, 64),
                () => OpenComfortSlider(),
                allowed: () => Settings != null && Settings.TurnLabel != "PhysicalOnly",
                accent: TabletCoral, font: 25);

            TabletControl(tabletActions, comfortMainGroup, () => "▲", new Vector2(-78, -215), new Vector2(52, 64),
                () => StepTurnPreset(true), allowed: () => Settings != null && Settings.TurnLabel != "PhysicalOnly",
                accent: TabletCoral, font: 24);

            // Right: Recenter height & Reset to default (blue)
            TabletControl(tabletActions, comfortMainGroup, () => "Recenter height", new Vector2(115, -215), new Vector2(220, 64),
                () => Settings?.RecalibrateSeatedHeight(), allowed: () => Settings != null, accent: TabletCoral, font: 25);

            TabletControl(tabletActions, comfortMainGroup, () => "Reset default", new Vector2(357, -215), new Vector2(204, 64),
                () => ResetSeatedHeightDefault(), allowed: () => Settings != null, accent: new Color(.18f, .52f, .92f), font: 25);

            BuildComfortSliderOverlay(page);
        }

        void BuildComfortSliderOverlay(RectTransform page)
        {
            comfortSliderGroup = new GameObject("ComfortSliderOverlay", typeof(RectTransform)).GetComponent<RectTransform>();
            comfortSliderGroup.SetParent(page, false);
            comfortSliderGroup.anchorMin = Vector2.zero; comfortSliderGroup.anchorMax = Vector2.one;
            comfortSliderGroup.sizeDelta = Vector2.zero;

            TabletRule(comfortSliderGroup, Vector2.zero, new Vector2(990, 580), new Color(.12f, .05f, .18f, .98f));

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
            TabletText(page,"HOW TO PLAY",new(0,211),new(960,70),54);
            TabletRule(page,new(0,143),new(685,47),TabletCream);
            tabletInputLayout=TabletText(page,"",new(0,143),new(650,44),25);
            tabletInputLayout.color=TabletPlum;
            leftInputTitle=TabletText(page,"LEFT HAND",new(-245,83),new(360,38),30);
            rightInputTitle=TabletText(page,"RIGHT HAND",new(245,83),new(360,38),30);
            leftController=TabletPicture(page,CandyHudVisuals.ControllerDiagram(0,true),new(-245,-28),new(158,175));
            rightController=TabletPicture(page,CandyHudVisuals.ControllerDiagram(0,false),new(245,-28),new(158,175));
            leftControllerHelp=TabletText(page,"",new(-245,-164),new(460,94),29);
            rightControllerHelp=TabletText(page,"",new(245,-164),new(460,94),29);
            if (session is not ILobbyControls)
                TabletControl(tabletActions,page,()=>"Replay quick tutorial",new(0,-271),new(480,63),()=>{
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
            TabletText(page,"ROOM",new(0,211),new(960,74),54);
            tabletRoomStatus=TabletText(page,"",new(0,105),new(945,104),32);
            TabletControl(tabletActions,page,()=>"Invite nearby",new(-244,-10),new(456,76),
                ()=>session.Multiplayer?.InviteNearby(),allowed:()=>session.Multiplayer?.CanInviteNearby==true);
            TabletControl(tabletActions,page,()=>"Copy room code",new(244,-10),new(456,76),()=>{
                GUIUtility.systemCopyBuffer=MultiplayerPanel.JoinCode(session.Multiplayer);
            },allowed:()=>session.Multiplayer!=null&&!string.IsNullOrEmpty(MultiplayerPanel.JoinCode(session.Multiplayer)));
            TabletControl(tabletActions,page,()=>session is ILobbyControls?"Join by room code":MultiplayerPanel.LobbyAction(session.Multiplayer),
                new(-244,-121),new(456,76),TabletRoomTravel,
                allowed:()=>session is ILobbyControls lobby?lobby.JoinEnabled:MatchRuntime.Current!=null);
            TabletControl(tabletActions,page,()=>{
                var room=session.Multiplayer;
                if(room==null||!room.CanLeave)return "Leave room";
                if(!room.IsClient)return "Leave / transfer host";
                return roomLeave.Pending(room,MultiplayerPanel.JoinCode(room),Time.unscaledTime)?"Confirm disconnect":"Leave room";
            },new(244,-121),new(456,76),()=>{
                var room=session.Multiplayer;
                if(room!=null&&room.CanLeave&&roomLeave.Select(room,MultiplayerPanel.JoinCode(room),room.IsClient,Time.unscaledTime))
                    room.LeaveSession();
            },allowed:()=>session.Multiplayer?.CanLeave==true,accent:TabletCoral);
        }

        void TabletRoomTravel()
        {
            if (session is ILobbyControls)
            {
                session.SetMenu(false);
                (session as Component)?.GetComponent<IslandLobbyView>()?.OpenRoomCodeFromTablet();
            }
            else MatchRuntime.Current?.ReturnToLobby();
        }

        void BuildTabletDeveloper(RectTransform page)
        {
            TabletText(page,"DEVELOPER",new(-232,211),new(520,72),54,TextAnchor.MiddleLeft);
            TabletText(page,"Quick tools",new(396,208),new(190,56),27);

            tabletDeveloperGrid = new GameObject("Developer grid", typeof(RectTransform)).GetComponent<RectTransform>();
            tabletDeveloperGrid.SetParent(page, false);
            tabletDeveloperGrid.anchoredPosition = Vector2.zero;

            DeveloperTile(tabletDeveloperGrid,"Weapons",0,CandyHudIcon.Trigger,OpenWeaponWheel,
                ()=>CanCommand&&MatchRuntime.Current is IDemoEquipmentControls,
                CandyEquipmentView.Outline(EquipmentKind.Blaster),opensDetails:false);
            DeveloperTile(tabletDeveloperGrid,"Bots",1,CandyHudIcon.Survivor,()=>{menuCategory=1;menuSection=-1;});
            DeveloperTile(tabletDeveloperGrid,"Disasters",2,CandyHudIcon.Flight,()=>{disasterPage=true;disasterSelection=-1;},
                picture:Resources.Load<Texture2D>("LobbyModePictures/disasters"));
            DeveloperTile(tabletDeveloperGrid,"Map & Mode",3,CandyHudIcon.Home,()=>{menuCategory=2;menuSection=-1;});
            DeveloperTile(tabletDeveloperGrid,"Rebuild",4,CandyHudIcon.Turn,()=>session.ResetMap(),()=>session.CanEditWorld,opensDetails:false);
            DeveloperTile(tabletDeveloperGrid,"Spectate",5,CandyHudIcon.Look,()=>spectatePage=true);

            // Row 3: Big merged cell across both columns for "More tools"
            TabletControl(tabletActions,tabletDeveloperGrid,()=>"More tools",new Vector2(0,-286),new Vector2(1004,104),
                ()=>ShowDeveloperDetails(()=>{}),accent:CandyHudVisuals.Lavender,font:39);

            BuildTabletWeaponWheel(page);
        }

        void BuildTabletWeaponWheel(RectTransform page)
        {
            tabletDeveloperWeapons = new GameObject("Developer weapons wheel", typeof(RectTransform)).GetComponent<RectTransform>();
            tabletDeveloperWeapons.SetParent(page, false);
            tabletDeveloperWeapons.anchoredPosition = Vector2.zero;
            tabletDeveloperWeapons.gameObject.SetActive(false);

            // Top category selector pills
            for (int c = 0; c < 3; c++)
            {
                int categoryIndex = c;
                float x = -200 + c * 200;
                TabletControl(tabletActions, tabletDeveloperWeapons, () => WheelCategories[categoryIndex],
                    new Vector2(x, 145), new Vector2(180, 44), () => {
                        tabletWeaponCategory = categoryIndex;
                        tabletWeaponOffset = 0;
                        var items = WheelGroups[categoryIndex];
                        if (items.Length > 0) tabletWeaponSelected = items[0];
                        UpdateWeaponWheelIcons();
                    }, selected: () => tabletWeaponCategory == categoryIndex, font: 20);
            }

            // Radial weapon wheel slots (6 slots around center preview at (0, -50))
            Vector2 wheelCenter = new Vector2(0, -50);
            Vector2[] wheelOffsets = {
                new(0, 110),
                new(108, 55),
                new(108, -55),
                new(0, -110),
                new(-108, -55),
                new(-108, 55)
            };
            tabletWheelSlotButtons = new TabletButton[6];
            tabletWheelSlotPictures = new RawImage[6];
            for (int i = 0; i < 6; i++)
            {
                int slotIndex = i;
                var slot = TabletControl(tabletActions, tabletDeveloperWeapons, () => GetWheelSlotLabel(slotIndex),
                    wheelCenter + wheelOffsets[slotIndex], new Vector2(104, 74), () => SelectWheelSlot(slotIndex),
                    selected: () => IsWheelSlotSelected(slotIndex),
                    allowed: () => IsWheelSlotVisible(slotIndex), font: 13);
                slot.Label.rectTransform.anchoredPosition = new Vector2(0, -22);
                slot.Label.rectTransform.sizeDelta = new Vector2(98, 24);
                var pic = TabletPicture((RectTransform)slot.Button.transform, null, new Vector2(0, 10), new Vector2(76, 44));
                tabletWheelSlotButtons[i] = slot;
                tabletWheelSlotPictures[i] = pic;
            }

            // Center Preview of Selected Weapon
            var centerBtn = TabletControl(tabletActions, tabletDeveloperWeapons, () => "",
                wheelCenter, new Vector2(110, 80), GiveSelectedWeapon, font: 12, accent: TabletPanel);
            tabletCenterWeaponPicture = TabletPicture((RectTransform)centerBtn.Button.transform, null, Vector2.zero, new Vector2(90, 52));

            // Left Column Controls (Target & Pagination)
            TabletControl(tabletActions, tabletDeveloperWeapons, () => "Target: " + (tabletGiveAll ? "EVERYONE" : "MYSELF"),
                new Vector2(-360, 60), new Vector2(230, 54), () => {
                    tabletGiveAll = !tabletGiveAll;
                    if (!tabletGiveAll) tabletRepeat = false;
                }, font: 20, accent: TabletPanel);

            TabletControl(tabletActions, tabletDeveloperWeapons, () => tabletGiveAll ? (tabletRepeat ? "NOW + RESPAWN" : "THIS LIFE ONLY") : "SELF ONLY",
                new Vector2(-360, -2), new Vector2(230, 52), () => tabletRepeat = !tabletRepeat,
                allowed: () => tabletGiveAll && tabletWeaponSelected != EquipmentKind.FlyingHand, font: 18, accent: TabletPanel);

            TabletControl(tabletActions, tabletDeveloperWeapons, () => "Clear respawn grants",
                new Vector2(-360, -64), new Vector2(230, 50), StopRespawnGrant,
                allowed: () => (MatchRuntime.Current as IDemoLoadoutControls)?.RespawnEquipment != EquipmentKind.None,
                font: 17, accent: TabletPanel);

            TabletControl(tabletActions, tabletDeveloperWeapons, () => "< PREV",
                new Vector2(-420, -126), new Vector2(108, 48), () => {
                    tabletWeaponOffset = Mathf.Max(0, tabletWeaponOffset - 6);
                    UpdateWeaponWheelIcons();
                }, allowed: () => tabletWeaponOffset > 0, font: 18, accent: TabletPanel);

            TabletControl(tabletActions, tabletDeveloperWeapons, () => "NEXT >",
                new Vector2(-300, -126), new Vector2(108, 48), () => {
                    var items = WheelGroups[tabletWeaponCategory];
                    if (tabletWeaponOffset + 6 < items.Length) {
                        tabletWeaponOffset += 6;
                        UpdateWeaponWheelIcons();
                    }
                }, allowed: () => {
                    var items = WheelGroups[tabletWeaponCategory];
                    return tabletWeaponOffset + 6 < items.Length;
                }, font: 18, accent: TabletPanel);

            // Right Column Controls (Selection Info & Give)
            tabletSelectedWeaponTitle = TabletText(tabletDeveloperWeapons, "", new Vector2(360, 75), new Vector2(245, 40), 22);
            tabletSelectedWeaponStats = TabletText(tabletDeveloperWeapons, "", new Vector2(360, 22), new Vector2(245, 52), 16);
            tabletSelectedWeaponStats.lineSpacing = 1.1f;

            TabletControl(tabletActions, tabletDeveloperWeapons, () => "GIVE SELECTED",
                new Vector2(360, -48), new Vector2(240, 60), GiveSelectedWeapon,
                allowed: CanGiveSelectedWeapon, accent: CandyHudVisuals.Mint, font: 24);

            TabletControl(tabletActions, tabletDeveloperWeapons, () => IsWeaponDrawn() ? "Holster weapon" : "Draw weapon",
                new Vector2(360, -118), new Vector2(240, 48), ToggleHolster,
                allowed: CanToggleHolster, font: 18, accent: TabletCoral);

            // Bottom Merged Row: Back to Developer Tools
            TabletControl(tabletActions, tabletDeveloperWeapons, () => "◄ Back to Developer Tools",
                new Vector2(0, -286), new Vector2(1004, 104), CloseWeaponWheel, accent: TabletPanel, font: 36);
        }

        void OpenWeaponWheel()
        {
            tabletWeaponsOpen = true;
            if (tabletDeveloperGrid) tabletDeveloperGrid.gameObject.SetActive(false);
            if (tabletDeveloperWeapons) tabletDeveloperWeapons.gameObject.SetActive(true);
            UpdateWeaponWheelIcons();
        }

        void CloseWeaponWheel()
        {
            tabletWeaponsOpen = false;
            if (tabletDeveloperGrid) tabletDeveloperGrid.gameObject.SetActive(true);
            if (tabletDeveloperWeapons) tabletDeveloperWeapons.gameObject.SetActive(false);
        }

        string GetWheelSlotLabel(int slotIndex)
        {
            var items = WheelGroups[tabletWeaponCategory];
            int itemIndex = tabletWeaponOffset + slotIndex;
            if (itemIndex >= 0 && itemIndex < items.Length)
            {
                string name = EquipmentSpecifications.Get(items[itemIndex]).name;
                int slash = name.IndexOf('/');
                if (slash > 0) name = name.Substring(0, slash).Trim();
                return name;
            }
            return "";
        }

        bool IsWheelSlotVisible(int slotIndex)
        {
            var items = WheelGroups[tabletWeaponCategory];
            return tabletWeaponOffset + slotIndex < items.Length;
        }

        bool IsWheelSlotSelected(int slotIndex)
        {
            var items = WheelGroups[tabletWeaponCategory];
            int itemIndex = tabletWeaponOffset + slotIndex;
            return itemIndex < items.Length && items[itemIndex] == tabletWeaponSelected;
        }

        void SelectWheelSlot(int slotIndex)
        {
            var items = WheelGroups[tabletWeaponCategory];
            int itemIndex = tabletWeaponOffset + slotIndex;
            if (itemIndex >= 0 && itemIndex < items.Length)
            {
                tabletWeaponSelected = items[itemIndex];
                UpdateWeaponWheelIcons();
            }
        }

        void UpdateWeaponWheelIcons()
        {
            var items = WheelGroups[tabletWeaponCategory];
            for (int i = 0; i < 6; i++)
            {
                int itemIndex = tabletWeaponOffset + i;
                bool visible = itemIndex < items.Length;
                if (tabletWheelSlotButtons != null && i < tabletWheelSlotButtons.Length)
                {
                    tabletWheelSlotButtons[i].Button.gameObject.SetActive(visible);
                    if (visible)
                    {
                        var kind = items[itemIndex];
                        SetPictureSprite(tabletWheelSlotPictures[i], CandyWeaponIcons.Get(kind), kind);
                    }
                }
            }
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
            Func<bool> allowed=null,Texture picture=null,bool opensDetails=true)
        {
            Vector2 position=new(slot%2==0?-257:257,98-slot/2*128);
            var tile=TabletControl(tabletActions,page,()=>label,position,new(490,104),
                ()=>{if(opensDetails)ShowDeveloperDetails(action);else action();},allowed:allowed,font:39);
            tile.Label.rectTransform.anchoredPosition=new Vector2(52,0);
            tile.Label.rectTransform.sizeDelta=new Vector2(345,88);
            var approved=Resources.Load<Texture2D>("PlayerTabletArt/"+(slot==3?"MapMode":label));
            Texture art=approved?approved:picture?picture:CandyHudVisuals.Icon(icon);
            float scale=Mathf.Min(137f/art.width,90f/art.height);
            TabletPicture((RectTransform)tile.Button.transform,art,new(-163,0),
                new(art.width*scale,art.height*scale));
        }

        void ShowDeveloperDetails(Action action)
        {
            if (!DeveloperTabAvailable) return;
            action();
            tabletDeveloperDetails=true;
            tabletPages[(int)TabletPage.Developer].gameObject.SetActive(false);
            if(tabletNav)tabletNav.gameObject.SetActive(true);
            legacyDeveloper.gameObject.SetActive(true);
        }

        void PointPlayerTablet(Camera camera,bool pressed,bool held)
        {
            if(tabletPage==TabletPage.Developer&&!DeveloperTabAvailable) OpenPlayerTabletPage(TabletPage.Comfort);
            if(PointTabletButtons(tabletTabs,pressed)) return;
            if(tabletDeveloperDetails){PointPage(camera,pressed,held);return;}
            if(Time.unscaledTime>=tabletRefreshAt)
            {
                tabletRefreshAt=Time.unscaledTime+.2f;
                if(tabletPage==TabletPage.Controls) RefreshTabletControls(camera);
                if(tabletPage==TabletPage.Room)
                {
                    var room=session.Multiplayer;
                    tabletRoomStatus.text=room==null||string.IsNullOrWhiteSpace(room.RoomInfo)?"Local play":room.RoomInfo;
                    if(!string.IsNullOrWhiteSpace(room?.InvitationFrom))
                        tabletRoomStatus.text+="\nInvite from "+room.InvitationFrom;
                }
                if(tabletPage==TabletPage.Developer&&tabletWeaponsOpen)
                {
                    RefreshWeaponWheelStats();
                }
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
                bool developerTile=tabletPage==TabletPage.Developer&&ReferenceEquals(controls,tabletActions)&&!tabletWeaponsOpen;
                bool juicyClose=control.Accent==JuicyRed;
                control.Focus.effectColor=hover?CandyHudVisuals.Mint:Color.clear;
                control.Button.image.color=juicyClose?(hover?new Color(1f,.26f,.32f):JuicyRed):
                    hover&&developerTile?Color.Lerp(control.Accent,CandyHudVisuals.Mint,.19f):
                    hover||selected?CandyHudVisuals.Mint:
                    allowed?control.Accent:Color.Lerp(control.Accent,TabletPlum,.65f);
                control.Label.color=juicyClose?Color.white:
                    hover&&!developerTile||selected||control.Accent==TabletCream&&allowed?TabletPlum:
                    allowed?TabletCream:CandyHudVisuals.Muted;
                string title=(selected&&!ReferenceEquals(controls,tabletTabs)?"✓ ":"")+control.Title();
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
            leftController.enabled=left;
            rightController.enabled=right;
            leftController.texture=CandyHudVisuals.ControllerDiagram(left==DominantLeft?3:1,true);
            rightController.texture=CandyHudVisuals.ControllerDiagram(!DominantLeft?3:1,false);
            leftControllerHelp.text=TabletControllerHelp(true,left,right,tracked);
            rightControllerHelp.text=TabletControllerHelp(false,right,left,tracked);
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
