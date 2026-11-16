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
            picRect.sizeDelta = new Vector2(150, 100);
            picRect.anchoredPosition = new Vector2(0, 12);
            var pic = picGo.GetComponent<RawImage>();
            pic.raycastTarget = false;

            var title = Text("", new Vector2(-16, -50), new Vector2(116, 22), 13);
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
            var playLabel = Text("▶", Vector2.zero, playSize, 13);
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

        void EnsureDisasterWheelUI(int count)
        {
            if (disasterWheelUI && disasterCards.Count == count) return;
            if (disasterWheelUI)
            {
                disasterWheelUI.SetActive(false);
                Destroy(disasterWheelUI);
            }
            disasterWheelUI = new GameObject("Disaster Wheel UI", typeof(RectTransform));
            disasterWheelUI.transform.SetParent(legacyDeveloper != null ? legacyDeveloper : transform, false);
            var rect = disasterWheelUI.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            disasterCards = new List<WheelVisualCard>();

            for (int i = 0; i < count; i++)
            {
                Vector2 cardPos = new Vector2((i % 3 - 1) * 320f, 150f - (i / 3) * 112f);
                var card = CreateWheelCard(disasterWheelUI.transform, cardPos, new Vector2(300, 102), new Vector2(64, -28), new Vector2(132, 32));
                card.picture.rectTransform.sizeDelta = new Vector2(100, 76);
                card.picture.rectTransform.anchoredPosition = new Vector2(-91, 0);
                card.title.rectTransform.sizeDelta = new Vector2(174, 44);
                card.title.rectTransform.anchoredPosition = new Vector2(54, 17);
                card.title.fontSize = 19;
                card.title.resizeTextForBestFit = false;
                card.title.color = TabletCream;
                card.playLabel.text = "▶ Launch";
                card.playLabel.fontSize = 15;
                card.playLabel.resizeTextForBestFit = false;
                disasterCards.Add(card);
            }

            disasterStopBtn = CreateWheelCenterButton(disasterWheelUI.transform, "STOP ALL DISASTERS", new Vector2(-180, -280), new Vector2(320, 44), 17, out disasterStopTarget);
            disasterBackBtn = CreateWheelCenterButton(disasterWheelUI.transform, "◄ BACK", new Vector2(180, -280), new Vector2(320, 44), 17, out disasterBackTarget);
            MenuAim.PresentInForeground(disasterWheelUI.transform);
        }

        void PointDisasterWheel(bool pressed)
        {
            int count = Demo?.DisasterCount ?? 0;
            EnsureDisasterWheelUI(count);
            disasterWheelUI.SetActive(true);
            HideDisasterSliders();
            scaffoldButton.gameObject.SetActive(false);
            foreach (var b in buttons) b.gameObject.SetActive(false);
            heading.text = "NATURAL DISASTERS\n<size=17><color=#d0b8d8>Click disaster for settings  ·  ▶ to launch immediately</color></size>";
            heading.rectTransform.anchoredPosition = new Vector2(0, 232);
            help.rectTransform.anchoredPosition = new Vector2(0, -330);
            help.rectTransform.sizeDelta = new Vector2(950, 30);
            help.fontSize = 17;
            help.text = CanCommand ? "Point at a disaster to configure its sliders, or ▶ to trigger it directly." : "The host controls disasters.";

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

                if (cardHover && !playHover && pressed)
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
            disasterStopBtn.GetComponentInChildren<Text>().color = stopHover ? CandyHudVisuals.Ink : Color.white;
            if (stopHover && pressed)
            {
                Demo?.Run(DemoCommand.StopDisasters);
                MenuAim.Consume();
                return;
            }

            bool backHover = Hit(disasterBackTarget, ray);
            disasterBackBtn.image.color = backHover ? CandyHudVisuals.Mint : TabletPanel;
            disasterBackBtn.GetComponentInChildren<Text>().color = backHover ? CandyHudVisuals.Ink : Color.white;
            if (backHover && pressed)
            {
                disasterWheelUI.SetActive(false);
                disasterPage = false;
                CloseDeveloperDetails();
                MenuAim.Consume();
                return;
            }
        }

        void EnsureModeWheelUI()
        {
            if (modeWheelUI) return;
            modeWheelUI = new GameObject("Mode Wheel UI", typeof(RectTransform));
            modeWheelUI.transform.SetParent(legacyDeveloper != null ? legacyDeveloper : transform, false);
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
            centerLabelGo.color = CandyHudVisuals.Mint;
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
                CloseDeveloperDetails();
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
        bool developerModeAdvanced;
        FieldInfo developerModeChoice;
        void PointModePage(Camera camera,bool pressed)
        {
            var match=MatchRuntime.Current;
            if(match==null){CloseDeveloperDetails();return;}
            HideDisasterSliders();
            if(modeWheelUI)modeWheelUI.SetActive(false);
            var rules=match.Rules;
            bool canConfigure=(match.Authority||(rules.mode==GameModeKind.Demo&&CanCommand))&&GameExperiencePanel.CanEditRawChoices(session);
            var names=new List<string>();var actions=new List<Action>();var enabled=new List<bool>();
            var art=new List<CandyHudIcon>();var captions=new List<string>();var pictures=new List<Texture>();
            void Add(string name,Action action,bool available=true)
            {names.Add(name);actions.Add(action);enabled.Add(available);art.Add(CandyHudIcon.Settings);captions.Add("");pictures.Add(null);}
            heading.text="DEVELOPER / MODE";
            help.text=canConfigure?"Choose a value. Advanced rules stay collapsed until needed.":"Mode rules are read only without host permission.";
            void Apply(FieldInfo field,object value)
            {
                if(!canConfigure)return;
                var copy=field.Name=="mode"?ModeDefaults.Create((GameModeKind)value):rules.Copy();
                field.SetValue(copy,value);match.Configure(copy);developerModeChoice=null;
            }
            if(developerModeChoice!=null)
            {
                var field=developerModeChoice;
                Add("Close "+ModeSetupPanel.Label(field.Name)+" choices",()=>developerModeChoice=null);
                if(field.FieldType==typeof(string))
                {
                    foreach(string value in CandySceneSkins.Names){string choice=value;Add(choice,()=>Apply(field,choice),canConfigure);}
                }
                else if(field.FieldType.IsEnum)
                {
                    foreach(var value in Enum.GetValues(field.FieldType))
                    {var choice=value;Add(choice.ToString(),()=>Apply(field,choice),canConfigure);}
                }
                else
                {
                    ModeSetupPanel.Range(field.Name,out float min,out float max);
                    float current=Convert.ToSingle(field.GetValue(rules));
                    float step=Mathf.Max(field.FieldType==typeof(int)?1:.1f,(max-min)/20);
                    Add(ModeSetupPanel.Label(field.Name)+": "+current.ToString("0.##"),()=>{},false);
                    foreach(int direction in new[]{-1,1})
                    {
                        int delta=direction;
                        Add(delta<0?"Decrease":"Increase",()=>{
                            var copy=match.Rules.Copy();float next=Mathf.Clamp(Convert.ToSingle(field.GetValue(copy))+delta*step,min,max);
                            if(field.FieldType==typeof(int))field.SetValue(copy,Mathf.RoundToInt(next));else field.SetValue(copy,next);
                            match.Configure(copy);
                        },canConfigure);
                    }
                }
            }
            else
            {
                var fields=developerModeAdvanced?modeFields.Where(f=>!commonModeFields.Contains(f.Name)).ToArray():quickModeFields;
                int start=developerModeAdvanced?modeOffset:0;
                for(int i=start;i<Mathf.Min(fields.Length,start+9);i++)
                {
                    var field=fields[i];
                    if(field.FieldType==typeof(string))continue;
                    Add(ModeSetupPanel.Label(field.Name)+": "+field.GetValue(rules)+(field.FieldType.IsEnum?"  v":""),()=>{
                        if(field.FieldType==typeof(bool))Apply(field,!(bool)field.GetValue(rules));else developerModeChoice=field;
                    },canConfigure);
                }
                if(!developerModeAdvanced)Add("Scene appearance: "+rules.environmentSkin+"  v",()=>developerModeChoice=modeFields.First(f=>f.Name=="environmentSkin"),canConfigure);
                Add((developerModeAdvanced?"Hide":"Show")+" advanced rules",()=>{developerModeAdvanced=!developerModeAdvanced;modeOffset=0;});
                if(developerModeAdvanced&&modeOffset>0)Add("Previous rules",()=>modeOffset=Mathf.Max(0,modeOffset-9));
                if(developerModeAdvanced&&modeOffset+9<fields.Length)Add("Next rules",()=>modeOffset+=9);
                Add("Start / restart round",match.StartRound,canConfigure);
            }
            RenderOrganizedMenuGrid(names,actions,enabled,art,captions,pictures,pressed);
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
