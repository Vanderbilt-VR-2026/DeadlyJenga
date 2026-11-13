using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        static readonly FieldInfo[] modeFields=typeof(ModeRules).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>f.Name!="carryingFlag"&&f.Name!="sinking"&&f.Name!="sinkProgress"&&f.Name!="downed"&&f.Name!="mystery").ToArray();
        static readonly string[] commonModeFields={"mode","healthEnabled","friendlyFire","punchEnabled","automaticDisasters","roundSeconds","island"};
        static readonly FieldInfo[] quickModeFields=commonModeFields.Select(name=>modeFields.First(field=>field.Name==name)).ToArray();
        int modeOffset;bool hudPage,modeWheel;

        sealed class WheelVisualCard
        {
            public RectTransform root;
            public Button cardButton;
            public BoxCollider cardTarget;
            public Image cardImage;
            public RawImage picture;
            public Text title;
            public Button playButton;
            public BoxCollider playTarget;
            public Image playImage;
            public Text playLabel;
        }

        GameObject disasterWheelUI;
        List<WheelVisualCard> disasterCards;
        Button disasterStopBtn, disasterBackBtn;
        BoxCollider disasterStopTarget, disasterBackTarget;

        GameObject modeWheelUI;
        List<WheelVisualCard> modeCards;
        Button modeStartBtn, modeBackBtn, modeSettingsBtn;
        BoxCollider modeStartTarget, modeBackTarget, modeSettingsTarget;
        Text modeCenterLabel;

        WheelVisualCard CreateWheelCard(Transform parent, Vector2 pos, Vector2 size, Vector2 playOffset, Vector2 playSize)
        {
            var go = new GameObject("WheelCard", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;

            var cardGo = new GameObject("CardBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider));
            cardGo.transform.SetParent(rect, false);
            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.sizeDelta = size;
            cardRect.anchoredPosition = Vector2.zero;
            var cardImg = cardGo.GetComponent<Image>();
            cardImg.sprite = CandyHudVisuals.Pill;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = CandyHudVisuals.Card;
            var cardTarget = cardGo.GetComponent<BoxCollider>();
            cardTarget.size = new Vector3(size.x, size.y, 4);
            cardTarget.isTrigger = true;

            var picGo = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
            picGo.transform.SetParent(cardGo.transform, false);
            var picRect = picGo.GetComponent<RectTransform>();
            picRect.sizeDelta = new Vector2(size.x - 8, size.y - 24);
            picRect.anchoredPosition = new Vector2(0, 10);
            var pic = picGo.GetComponent<RawImage>();
            pic.raycastTarget = false;

            var title = Text("", new Vector2(0, -size.y * 0.5f + 11), new Vector2(size.x - 4, 20), 12);
            title.transform.SetParent(cardGo.transform, false);
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            title.alignment = TextAnchor.MiddleCenter;

            var playGo = new GameObject("PlayBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider));
            playGo.transform.SetParent(rect, false);
            var playRect = playGo.GetComponent<RectTransform>();
            playRect.sizeDelta = playSize;
            playRect.anchoredPosition = playOffset;
            var playImg = playGo.GetComponent<Image>();
            playImg.sprite = CandyHudVisuals.Pill;
            playImg.type = Image.Type.Sliced;
            playImg.color = new Color(0.24f, 0.12f, 0.32f);
            var playTarget = playGo.GetComponent<BoxCollider>();
            playTarget.size = new Vector3(playSize.x, playSize.y, 4);
            playTarget.isTrigger = true;
            var playLabel = Text("▶", Vector2.zero, playSize, 14);
            playLabel.transform.SetParent(playGo.transform, false);
            playLabel.fontStyle = FontStyle.Bold;
            playLabel.color = CandyHudVisuals.Mint;
            playLabel.alignment = TextAnchor.MiddleCenter;

            return new WheelVisualCard {
                root = rect,
                cardButton = cardGo.GetComponent<Button>(),
                cardTarget = cardTarget,
                cardImage = cardImg,
                picture = pic,
                title = title,
                playButton = playGo.GetComponent<Button>(),
                playTarget = playTarget,
                playImage = playImg,
                playLabel = playLabel
            };
        }

        Button CreateWheelCenterButton(Transform parent, string label, Vector2 pos, Vector2 size, int font, out BoxCollider target)
        {
            var btn = HeaderButton(label, 0, out target);
            var r = (RectTransform)btn.transform;
            r.SetParent(parent, false);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            target.size = new Vector3(size.x, size.y, 4);
            var t = btn.GetComponentInChildren<Text>();
            t.text = label;
            t.fontSize = font;
            t.fontStyle = FontStyle.Bold;
            return btn;
        }

        void EnsureDisasterWheelUI()
        {
            if (disasterWheelUI) return;
            disasterWheelUI = new GameObject("Disaster Wheel UI", typeof(RectTransform));
            disasterWheelUI.transform.SetParent(transform, false);
            var rect = disasterWheelUI.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            disasterCards = new List<WheelVisualCard>();

            int count = Demo?.DisasterCount ?? 12;
            for (int i = 0; i < count; i++)
            {
                float angle = (90f - i * (360f / count)) * Mathf.Deg2Rad;
                Vector2 cardPos = new Vector2(Mathf.Cos(angle) * 355f, 25f + Mathf.Sin(angle) * 195f);
                var card = CreateWheelCard(disasterWheelUI.transform, cardPos, new Vector2(114, 64), new Vector2(0, -45), new Vector2(38, 24));
                disasterCards.Add(card);
            }

            disasterStopBtn = CreateWheelCenterButton(disasterWheelUI.transform, "STOP ALL DISASTERS", new Vector2(0, 20), new Vector2(230, 44), 17, out disasterStopTarget);
            disasterBackBtn = CreateWheelCenterButton(disasterWheelUI.transform, "◄ BACK", new Vector2(0, -32), new Vector2(140, 38), 16, out disasterBackTarget);
        }

        void PointDisasterWheel(bool pressed)
        {
            EnsureDisasterWheelUI();
            disasterWheelUI.SetActive(true);
            HideDisasterSliders();
            scaffoldButton.gameObject.SetActive(false);
            foreach (var b in buttons) b.gameObject.SetActive(false);
            heading.text = "NATURAL DISASTERS\n<size=17><color=#d0b8d8>Click disaster for settings  ·  ▶ to launch immediately</color></size>";
            help.text = CanCommand ? "Point at a disaster to configure its sliders, or ▶ to trigger it directly." : "The host controls disasters.";

            int count = Demo?.DisasterCount ?? 0;
            var ray = pointerRay;

            for (int i = 0; i < disasterCards.Count && i < count; i++)
            {
                var card = disasterCards[i];
                string name = Demo.DisasterLabel(i);
                card.title.text = CandyDisasterWarningCard.DisplayName(name);
                var tex = CandyDisasterWarningCard.GetImage(name);
                if (tex) { card.picture.texture = tex; card.picture.color = Color.white; }
                else { card.picture.texture = null; card.picture.color = Color.clear; }

                bool cardHover = Hit(card.cardTarget, ray);
                bool playHover = Hit(card.playTarget, ray);
                card.cardImage.color = cardHover ? CandyHudVisuals.Mint : CandyHudVisuals.Card;
                card.title.color = cardHover ? CandyHudVisuals.Ink : Color.white;
                card.playImage.color = playHover ? CandyHudVisuals.Mint : new Color(0.24f, 0.12f, 0.32f);
                card.playLabel.color = playHover ? CandyHudVisuals.Ink : CandyHudVisuals.Mint;

                if (cardHover && pressed)
                {
                    disasterWheelUI.SetActive(false);
                    disasterSelection = i;
                    disasterStatus = "";
                    MenuAim.Consume();
                    return;
                }
                if (playHover && pressed)
                {
                    if (CanCommand)
                        disasterStatus = Demo.LaunchDisaster(i);
                    MenuAim.Consume();
                    return;
                }
            }

            bool stopHover = Hit(disasterStopTarget, ray);
            disasterStopBtn.image.color = stopHover ? CandyHudVisuals.Mint : TabletPanel;
            if (stopHover && pressed)
            {
                Demo?.Run(DemoCommand.StopDisasters);
                MenuAim.Consume();
                return;
            }

            bool backHover = Hit(disasterBackTarget, ray);
            disasterBackBtn.image.color = backHover ? CandyHudVisuals.Mint : TabletPanel;
            if (backHover && pressed)
            {
                disasterWheelUI.SetActive(false);
                disasterPage = false;
                if (tabletDeveloperDetails)
                {
                    tabletDeveloperDetails = false;
                    legacyDeveloper.gameObject.SetActive(false);
                    if (tabletNav) tabletNav.gameObject.SetActive(true);
                    tabletPages[(int)TabletPage.Developer].gameObject.SetActive(true);
                }
                MenuAim.Consume();
                return;
            }
        }

        void EnsureModeWheelUI()
        {
            if (modeWheelUI) return;
            modeWheelUI = new GameObject("Mode Wheel UI", typeof(RectTransform));
            modeWheelUI.transform.SetParent(transform, false);
            var rect = modeWheelUI.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            modeCards = new List<WheelVisualCard>();

            int count = ModeSetupPanel.WheelModes.Length;
            for (int i = 0; i < count; i++)
            {
                float angle = (90f - i * (360f / count)) * Mathf.Deg2Rad;
                Vector2 cardPos = new Vector2(Mathf.Cos(angle) * 260f, 25f + Mathf.Sin(angle) * 175f);
                var card = CreateWheelCard(modeWheelUI.transform, cardPos, new Vector2(132, 74), new Vector2(0, -50), new Vector2(42, 28));
                modeCards.Add(card);
            }

            var centerLabelGo = Text("", new Vector2(0, 52), new Vector2(220, 36), 18);
            centerLabelGo.transform.SetParent(modeWheelUI.transform, false);
            centerLabelGo.fontStyle = FontStyle.Bold;
            centerLabelGo.color = CandyMint;
            centerLabelGo.alignment = TextAnchor.MiddleCenter;
            modeCenterLabel = centerLabelGo;

            modeStartBtn = CreateWheelCenterButton(modeWheelUI.transform, "START ROUND", new Vector2(0, 8), new Vector2(210, 44), 18, out modeStartTarget);
            modeBackBtn = CreateWheelCenterButton(modeWheelUI.transform, "◄ BACK", new Vector2(-75, -45), new Vector2(120, 38), 16, out modeBackTarget);
            modeSettingsBtn = CreateWheelCenterButton(modeWheelUI.transform, "SETTINGS", new Vector2(75, -45), new Vector2(120, 38), 16, out modeSettingsTarget);
        }

        void PointModeWheel(IMatchRuntime match, bool pressed)
        {
            EnsureModeWheelUI();
            modeWheelUI.SetActive(true);
            HideDisasterSliders();
            scaffoldButton.gameObject.SetActive(false);
            foreach (var b in buttons) b.gameObject.SetActive(false);
            heading.text = "GAME MODES\n<size=17><color=#d0b8d8>Click mode to configure  ·  ▶ to start immediately</color></size>";
            help.text = match.Authority ? "Point at a mode for settings, or ▶ to launch round." : "The host selects the mode.";
            modeCenterLabel.text = "CURRENT: " + match.Rules.mode.ToString().ToUpperInvariant();

            int count = ModeSetupPanel.WheelModes.Length;
            var ray = pointerRay;

            for (int i = 0; i < modeCards.Count && i < count; i++)
            {
                var card = modeCards[i];
                var mode = ModeSetupPanel.WheelModes[i];
                card.title.text = DeadlyJenga.Presentation.CandyHudContent.Mode(mode);
                var tex = ModeSetupPanel.WheelModePicture(mode);
                if (tex) { card.picture.texture = tex; card.picture.color = Color.white; }
                else { card.picture.texture = null; card.picture.color = Color.clear; }

                bool isSelected = mode == match.Rules.mode;
                bool cardHover = Hit(card.cardTarget, ray);
                bool playHover = Hit(card.playTarget, ray);

                card.cardImage.color = cardHover ? CandyHudVisuals.Mint : isSelected ? CandyHudVisuals.Lavender : CandyHudVisuals.Card;
                card.title.color = cardHover ? CandyHudVisuals.Ink : Color.white;
                card.playImage.color = playHover ? CandyHudVisuals.Mint : new Color(0.24f, 0.12f, 0.32f);
                card.playLabel.color = playHover ? CandyHudVisuals.Ink : CandyHudVisuals.Mint;

                if (cardHover && pressed)
                {
                    if (mode != match.Rules.mode) match.Configure(ModeDefaults.Create(mode));
                    modeWheelUI.SetActive(false);
                    modeWheel = false;
                    MenuAim.Consume();
                    return;
                }
                if (playHover && pressed)
                {
                    if (match.Authority)
                    {
                        if (mode != match.Rules.mode) match.Configure(ModeDefaults.Create(mode));
                        match.StartRound();
                    }
                    MenuAim.Consume();
                    return;
                }
            }

            bool startHover = Hit(modeStartTarget, ray);
            modeStartBtn.image.color = startHover ? CandyHudVisuals.Mint : TabletPanel;
            if (startHover && pressed)
            {
                if (match.Authority) match.StartRound();
                MenuAim.Consume();
                return;
            }

            bool backHover = Hit(modeBackTarget, ray);
            modeBackBtn.image.color = backHover ? CandyHudVisuals.Mint : TabletPanel;
            if (backHover && pressed)
            {
                modeWheelUI.SetActive(false);
                modePage = modeWheel = false;
                MenuAim.Consume();
                return;
            }

            bool settingsHover = Hit(modeSettingsTarget, ray);
            modeSettingsBtn.image.color = settingsHover ? CandyHudVisuals.Mint : TabletPanel;
            if (settingsHover && pressed)
            {
                modeWheelUI.SetActive(false);
                modeWheel = false;
                MenuAim.Consume();
                return;
            }
        }
        void PointModePage(Camera camera,bool pressed)
        {
            var match=MatchRuntime.Current;if(match==null){modePage=modeWheel=false;return;}
            if(modeWheel){PointModeWheel(match,pressed);return;}
            var rules=match.Rules;
            var fields=modeOffset==0?quickModeFields:modeFields;
            int pageStart=modeOffset==0?0:modeOffset-1;
            heading.text="MATCH RULES / SCENERY\n"+rules.mode+" · "+match.Frame.phase;
            HideDisasterSliders();bool canConfigure=match.Authority||(rules.mode==GameModeKind.Demo&&Services.Session?.Demo?.CanCommand==true);
            for(int i=0;i<buttons.Length;i++){buttons[i].gameObject.SetActive(true);targets[i].enabled=i==0||canConfigure;}
            labels[0].text="Back to demo";labels[1].text="Start / restart round";labels[2].text="Skin: "+rules.environmentSkin;
            for(int i=3;i<=9;i++){int index=pageStart+i-3;if(index<fields.Length){var field=fields[index];labels[i].text=ModeSetupPanel.Label(field.Name)+": "+field.GetValue(rules);if(field.Name=="mode"){labels[i].text="Open game mode wheel";targets[i].enabled=match.Authority;}}else buttons[i].gameObject.SetActive(false);}
            labels[10].text="HUD / standings / tuning";labels[11].text="Temporary team hand power";labels[12].text=modeOffset==0?"Common settings":"Previous settings";labels[13].text=modeOffset==0?"Advanced settings":"More settings";labels[14].text="Sea spirit / tabletop AR";
            var mystery=match as IMysteryControls;
            if(mystery?.Active==true){labels[11].text="Special punch: "+(mystery.Personal.specialAttack?"ON":"OFF");labels[14].text="Demo role: "+mystery.Personal.role;targets[11].enabled=true;}
            targets[10].enabled=targets[12].enabled=targets[13].enabled=targets[14].enabled=true;
            if(!DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session))
            {
                heading.text="CURATED GAME PROFILE / READ ONLY";
                for(int i=0;i<targets.Length;i++)if(i!=0&&i!=10&&i!=12&&i!=13&&!(i==14&&mystery?.Active!=true))targets[i].enabled=false;
            }
            var ray=pointerRay;
            for(int i=0;i<buttons.Length;i++)
            {
                if(!buttons[i].gameObject.activeSelf)continue;bool hover=Hit(targets[i],ray);buttons[i].image.color=!targets[i].enabled?CandyHudVisuals.Ink:hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=!targets[i].enabled?CandyHudVisuals.Muted:hover?CandyHudVisuals.Ink:Color.white;
                if(!hover||!pressed||!targets[i].enabled)continue;
                if(mystery?.Active==true&&i==11){mystery.SetSpecialAttack(!mystery.Personal.specialAttack);return;}
                if(mystery?.Active==true&&i==14){if(match.Authority)mystery.SetDemoRole((MysteryRole)(1+(int)mystery.Personal.role%3));return;}
                if(i==0){modePage=false;return;}if(i==12){modeOffset=Mathf.Max(0,modeOffset-7);return;}if(i==13){modeOffset=modeOffset==0?1:modeOffset+7>modeFields.Length?0:modeOffset+7;return;}if(i==14){modePage=false;tabletopPage=true;return;}
                if(i==10){modePage=false;hudPage=true;return;}
                if(i==11){match.ActivateHandPower(rules.handPowerSeconds);return;}
                if(i==1){match.StartRound();return;}var copy=rules.Copy();
                if(i==2)copy.environmentSkin=CandySceneSkins.Names[(DeadlyJenga.Presentation.CandyMatchVisuals.SkinIndex(copy.environmentSkin)+1)%CandySceneSkins.Names.Length];
                else
                {
                    var f=fields[pageStart+i-3];var type=f.FieldType;
                    if(f.Name=="mode"){modeWheel=true;return;}
                    if(type==typeof(bool))f.SetValue(copy,!(bool)f.GetValue(copy));
                    else if(type.IsEnum){var values=Enum.GetValues(type);var next=values.GetValue((Array.IndexOf(values,f.GetValue(copy))+1)%values.Length);if(f.Name=="mode")copy=ModeDefaults.Create((GameModeKind)next);else f.SetValue(copy,next);}
                    else if(type==typeof(float)||type==typeof(int)){ModeSetupPanel.Range(f.Name,out float min,out float max);float next=Convert.ToSingle(f.GetValue(copy))+Mathf.Max(type==typeof(int)?1:.1f,(max-min)/20);if(next>max)next=min;if(type==typeof(int))f.SetValue(copy,Mathf.RoundToInt(next));else f.SetValue(copy,next);}
                }
                match.Configure(copy);return;
            }
            help.text="Look + trigger / pinch to cycle a value. More settings reveals all mode options.";
        }
        void PointHudPage(Camera camera,bool pressed)
        {
            var match=MatchRuntime.Current;if(match==null){hudPage=false;return;}
            heading.text="HUD / STANDINGS";HideDisasterSliders();bool mystery=(match as IMysteryControls)?.Active==true,flags=match.Rules.mode==GameModeKind.CaptureFlag&&match.Frame.flags.Length>2;
            var hints=Services.Session?.Player?.gameObject.GetComponentInParent<CandyDemoHud>();
            if(!hints&&Services.Session is Component owner)hints=owner.GetComponent<CandyDemoHud>();
            string[] titles={"Back to match rules",DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned?"Hide full standings":"Show full standings",mystery?"Next body report page":"Track next enemy flag","Movement / stamina / audio","Scene skin: "+match.Rules.environmentSkin,"Control prompts: "+(hints?hints.hintMode.ToString():"Unavailable")};
            for(int i=0;i<buttons.Length;i++)
            {
                bool visible=i<titles.Length&&(i!=2||mystery||flags)&&(i!=5||hints);buttons[i].gameObject.SetActive(visible);targets[i].enabled=visible&&(i!=4||match.Authority||match.Rules.mode==GameModeKind.Demo&&Services.Session?.Demo?.CanCommand==true);if(visible)labels[i].text=titles[i];
            }
            var ray=pointerRay;
            for(int i=0;i<titles.Length;i++)
            {
                if(!buttons[i].gameObject.activeSelf)continue;bool hover=Hit(targets[i],ray);buttons[i].image.color=!targets[i].enabled?CandyHudVisuals.Ink:hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=!targets[i].enabled?CandyHudVisuals.Muted:hover?CandyHudVisuals.Ink:Color.white;
                if(!hover||!pressed||!targets[i].enabled)continue;
                if(i==0){hudPage=false;modePage=true;return;}
                if(i==1){DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned=!DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned;Services.Session.SetMenu(false);return;}
                if(i==2){if(mystery)DeadlyJenga.Presentation.CandyMysteryView.ReportPage++;else DeadlyJenga.Presentation.CandyMatchHud.FlagFocusIndex++;Services.Session.SetMenu(false);return;}
                if(i==3){hudPage=false;tuningPage=true;comfortPage=networkPage=false;return;}
                if(i==5){hints.hintMode=(CandyDemoHud.HintMode)(((int)hints.hintMode+1)%3);labels[i].text="Control prompts: "+hints.hintMode;return;}
                var copy=match.Rules.Copy();copy.environmentSkin=CandySceneSkins.Names[(DeadlyJenga.Presentation.CandyMatchVisuals.SkinIndex(copy.environmentSkin)+1)%CandySceneSkins.Names.Length];match.Configure(copy);return;
            }
            help.text="Standings pages cycle every 5s in VR. Hold the look-stick click with no grip, or pin standings here. Reopen to hide. Body reports retain numbered markers.";
        }
    }
}
