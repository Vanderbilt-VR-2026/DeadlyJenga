using System;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        string developerRoute="", developerFoldout="";
        bool developerChild,developerPhysics;
        Action<float> developerVolumeApply;
        string developerVolumeLabel;
        void DeveloperVolume(string label,Action<float> apply){developerVolumeLabel=label;developerVolumeApply=apply;}
        IDeveloperGodMode DeveloperGodMode => session.Player?.gameObject.GetComponent<IDeveloperGodMode>();
        void ToggleDeveloperGodMode()
        {
            var god=DeveloperGodMode;
            if(god?.CanSetGodMode==true)god.SetGodMode(!god.GodModeEnabled);
        }
        void OpenDeveloperRoute(string route)
        {
            ResetPages();developerRoute=route;developerChild=false;
        }
        void DeveloperChild(Action open)
        {
            ResetPages();menuCategory=menuSection=-1;developerChild=true;open();
        }
        void PointDeveloperRoute(bool pressed)
        {
            HideDisasterSliders();scaffoldButton.gameObject.SetActive(false);
            var names=new System.Collections.Generic.List<string>();
            var actions=new System.Collections.Generic.List<Action>();
            var enabled=new System.Collections.Generic.List<bool>();
            var art=new System.Collections.Generic.List<CandyHudIcon>();
            var captions=new System.Collections.Generic.List<string>();
            var pictures=new System.Collections.Generic.List<Texture>();
            void Add(string label,Action action,bool available=true,CandyHudIcon icon=CandyHudIcon.Settings,string caption="")
            {names.Add(label);actions.Add(action);enabled.Add(available);art.Add(icon);captions.Add(caption);pictures.Add(null);}
            void Fold(string key,string label,Action contents)
            {
                Add((developerFoldout==key?"▼ ":"▶ ")+label,()=>developerFoldout=developerFoldout==key?"":key);
                if(developerFoldout==key)contents();
            }
            heading.text="DEVELOPER / "+developerRoute.ToUpperInvariant();
            help.text=CanCommand?"Expand a section for extra controls. Back returns to Developer.":"Shared changes require developer permission from the host.";
            if(developerVolumeApply!=null)
            {
                heading.text=developerRoute.ToUpperInvariant()+" / "+developerVolumeLabel.ToUpperInvariant();
                Add("Close volume choices",()=>developerVolumeApply=null);
                foreach(float level in new[]{0f,.25f,.5f,.75f,1f})
                {float value=level;Add(value.ToString("P0"),()=>{developerVolumeApply(value);developerVolumeApply=null;},icon:CandyHudIcon.Sound);}
                RenderOrganizedMenuGrid(names,actions,enabled,art,captions,pictures,pressed);return;
            }
            if(developerRoute=="Bots")
            {
                var lobby=session as ILobbyControls;
                if(lobby!=null&&session is ILobbyDetails details)
                {
                    Add("Players: "+details.Setup.population+" / Hands: "+details.Setup.hands,()=>{},false,CandyHudIcon.Friends);
                    Add("- Player",()=>AdjustLobbyPopulation(lobby,details,false,-1),lobby.CanConfigure&&details.Setup.population>1);
                    Add("+ Player",()=>AdjustLobbyPopulation(lobby,details,false,1),lobby.CanConfigure&&details.Setup.population<100);
                }
                else
                {
                    Add("Survivors: "+(Demo?.SurvivorCount??0)+" / Hands: "+(Demo?.HandCount??0),()=>{},false,CandyHudIcon.Friends);
                    Add("Add survivor",()=>Demo.Run(DemoCommand.AddSurvivor),CanCommand);
                    Add("Clear survivors",()=>Demo.Run(DemoCommand.ClearSurvivors),CanCommand);
                }
                Fold("batch","Population presets",()=>{
                    foreach(int value in new[]{10,24,52,100})
                    {
                        int target=value;
                        Add("Fill to "+target+" total players",()=>{session.SurvivorBots.TargetPopulation=target;Demo.Run(DemoCommand.FillSurvivors);},lobby==null&&CanCommand&&session.SurvivorBots!=null);
                    }
                });
                Fold("hands","Flying hands",()=>{
                    if(lobby!=null&&session is ILobbyDetails d)
                    {
                        Add("- Hand",()=>AdjustLobbyPopulation(lobby,d,true,-1),lobby.CanConfigure&&d.Setup.hands>0);
                        Add("+ Hand",()=>AdjustLobbyPopulation(lobby,d,true,1),lobby.CanConfigure&&d.Setup.hands<100);
                    }
                    else
                    {
                        Add("Add flying hand",()=>Demo.Run(DemoCommand.AddHand),CanCommand);
                        Add("Clear flying hands",()=>Demo.Run(DemoCommand.ClearHands),CanCommand);
                    }
                });
                Fold("behavior","Behavior",()=>{
                    Add("Survivor: "+Demo?.SurvivorPersonality,()=>Demo.Run(DemoCommand.NextSurvivor),CanCommand);
                    Add("Flying hand: "+Demo?.HandPersonality,()=>Demo.Run(DemoCommand.NextHand),CanCommand);
                });
                Fold("equipment","Equipment",()=>Add("Everyone loadout (bots + players)",()=>DeveloperChild(()=>{giveAllEquipment=true;giveEquipmentPage=true;}),CanCommand&&MatchRuntime.Current is IDemoEquipmentControls,CandyHudIcon.Trigger));
                Fold("voices","Bot voices",()=>{
                    var social=session.Social;
                    Add("Voices: "+(social?.VoiceEnabled==true?"ON":"OFF"),()=>social.VoiceEnabled=!social.VoiceEnabled,social?.IsHost==true,CandyHudIcon.Sound);
                    Add("Bot volume: "+(social?.BotVolume??0).ToString("P0"),()=>DeveloperVolume("Bot volume",value=>social.BotVolume=value),social!=null,CandyHudIcon.Sound);
                });
            }
            else
            {
                Fold("player","Player debug",()=>{
                    Add(session.PlayingHand?"Become survivor":"Become flying hand",SwitchDemoRole,DemoMode,CandyHudIcon.Hand);
                    Add("Hand appearance",()=>CharacterPicker.Open(session),HandLooks.Names.Length>0,CandyHudIcon.Hand);
                    Add((developerPhysics?"Hide":"Show")+" hand physics",()=>developerPhysics=!developerPhysics,Settings!=null);
                    if(developerPhysics&&Settings!=null)
                    {
                        float force=Settings.CurrentRules.handPhysics?.forceNewtons??1200;
                        float friction=Settings.CurrentRules.handPhysics?.friction??2;
                        Add("Force: "+force.ToString("0")+" N / friction: "+friction.ToString("0.0"),()=>{},false);
                        Add("Force -100 N",()=>SurvivorSettingsPanel.SetHandPhysics(Settings,force-100,friction,CanCommand,Demo.ApplyStaminaRules),CanCommand);
                        Add("Force +100 N",()=>SurvivorSettingsPanel.SetHandPhysics(Settings,force+100,friction,CanCommand,Demo.ApplyStaminaRules),CanCommand);
                        Add("Friction -0.1",()=>SurvivorSettingsPanel.SetHandPhysics(Settings,force,Mathf.Max(0,friction-.1f),CanCommand,Demo.ApplyStaminaRules),CanCommand);
                        Add("Friction +0.1",()=>SurvivorSettingsPanel.SetHandPhysics(Settings,force,Mathf.Min(2,friction+.1f),CanCommand,Demo.ApplyStaminaRules),CanCommand);
                    }
                    Add("Host stamina tuning",()=>DeveloperChild(()=>tuningPage=true),Settings!=null&&CanCommand);
                    Add("Sea spirit / tabletop",()=>DeveloperChild(()=>tabletopPage=true),MatchRuntime.Current!=null);
                });
                Fold("world","World & pickups",()=>{
                    Add("Ground pickups",()=>DeveloperChild(()=>{menuCategory=2;menuSection=4;}),CanCommand&&MatchRuntime.Current is IEquipmentRuntime,CandyHudIcon.Trigger);
                    Add("Automatic powerups",()=>DeveloperChild(()=>randomPowerupPage=true),CanCommand);
                    Add("Wildlife & enemies",()=>DeveloperChild(()=>{menuCategory=1;menuSection=3;}),CanCommand&&session.SurvivorBots is IEnemyPopulation,CandyHudIcon.Friends);
                    if(Demo is IBuildUpControls build&&build.Available)Add("Deliver scaffold tier",build.AdvanceStage,build.CanAdvance);
                });
                Fold("round","Round controls",()=>{
                    Add("Round setup",()=>DeadlyJenga.Presentation.GameExperiencePanel.Open(session),DeadlyJenga.Presentation.GameExperiencePanel.Resolve(session)!=null,CandyHudIcon.Play);
                    var match=MatchRuntime.Current;
                    Add("Temporary team hand power",()=>match.ActivateHandPower(match.Rules.handPowerSeconds),CanCommand&&match?.Authority==true,CandyHudIcon.Hand);
                    if(match is IMysteryControls mystery&&mystery.Active)
                    {
                        Add("Mystery demo role: "+mystery.Personal.role,()=>mystery.SetDemoRole((MysteryRole)(1+(int)mystery.Personal.role%3)),match.Authority&&CanCommand);
                        Add("Special attack: "+(mystery.Personal.specialAttack?"ON":"OFF"),()=>mystery.SetSpecialAttack(!mystery.Personal.specialAttack),CanCommand);
                    }
                    var review=MatchRuntime.Current as IMatchReviewControls;
                    Add("End round",()=>review.EndGame(),review?.CanEndGame==true,CandyHudIcon.Play);
                });
                Fold("diagnostics","Diagnostics",()=>{
                    Add(Demo?.Status??"No active demo session",()=>{},false);
                    Add(session.Multiplayer?.Status??"Local play",()=>{},false,CandyHudIcon.Friends);
                    Add(DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned?"Hide standings":"Show standings",()=>DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned=!DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned);
                    var audio=DeadlyJenga.CandyAudio.CandyAudioEngine.Instance;
                    Add("Master volume: "+(audio&&audio.bank?audio.bank.master.ToString("P0"):"Unavailable"),()=>DeveloperVolume("Master volume",value=>audio.bank.master=value),audio&&audio.bank,CandyHudIcon.Sound);
                    Add("Music volume: "+(audio&&audio.bank?audio.bank.music.ToString("P0"):"Unavailable"),()=>DeveloperVolume("Music volume",value=>audio.bank.music=value),audio&&audio.bank,CandyHudIcon.Sound);
                    var hints=(session as Component)?.GetComponent<CandyDemoHud>();
                    if(hints)Add("Control prompts: "+hints.hintMode,()=>hints.hintMode=(CandyDemoHud.HintMode)(((int)hints.hintMode+1)%3));
                    var gun=session.Player?.gameObject.GetComponent<IBlasterControls>();
                    Add("Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),()=>gun.SetTestingEnabled(!gun.TestingEnabled),gun!=null&&CanCommand&&MatchRuntime.Current?.Authority==true,CandyHudIcon.Trigger);
                });
            }
            RenderOrganizedMenuGrid(names,actions,enabled,art,captions,pictures,pressed);
        }

        void PointDeveloperStamina(bool pressed)
        {
            HideDisasterSliders();scaffoldButton.gameObject.SetActive(false);
            var names=new System.Collections.Generic.List<string>();var actions=new System.Collections.Generic.List<Action>();
            var enabled=new System.Collections.Generic.List<bool>();var art=new System.Collections.Generic.List<CandyHudIcon>();
            var captions=new System.Collections.Generic.List<string>();var pictures=new System.Collections.Generic.List<Texture>();
            var rules=Settings.CurrentRules;
            void Add(string label,Action change)
            {
                names.Add(label);actions.Add(()=>{if(!CanCommand)return;change();Settings.ApplyMatchRules(rules);Demo.ApplyStaminaRules(Settings.SaveStaminaRules());});
                enabled.Add(CanCommand);art.Add(CandyHudIcon.Settings);captions.Add("");pictures.Add(null);
            }
            heading.text="PLAYER DEBUG / HOST STAMINA";
            Add("Grip duration: "+rules.gripSeconds.ToString("0.0")+"s (+)",()=>rules.gripSeconds=Mathf.Min(20,rules.gripSeconds+.5f));
            Add("Grip duration (-)",()=>rules.gripSeconds=Mathf.Max(.5f,rules.gripSeconds-.5f));
            Add("Drain rate: "+rules.drainRate.ToString("0.00"),()=>rules.drainRate=Cycle(rules.drainRate,new[]{0f,.25f,.5f,1f,2f,4f}));
            Add("Recovery rate: "+rules.recoveryRate.ToString("0.00"),()=>rules.recoveryRate=Cycle(rules.recoveryRate,new[]{0f,.1f,.25f,.5f,1f,2f}));
            Add("Recovery delay: "+rules.recoveryDelay.ToString("0.00"),()=>rules.recoveryDelay=Cycle(rules.recoveryDelay,new[]{0f,.25f,.5f,1f,2f,5f}));
            Add("Drain curve: "+rules.drainCurve,()=>rules.drainCurve=MatchOptions.Next(MatchOptions.Curves,rules.drainCurve));
            Add("Recovery curve: "+rules.recoveryCurve,()=>rules.recoveryCurve=MatchOptions.Next(MatchOptions.Curves,rules.recoveryCurve));
            Add("Drain degree: "+rules.drainDegree.ToString("0.0"),()=>rules.drainDegree=rules.drainDegree>=6?1:rules.drainDegree+.5f);
            Add("Recovery degree: "+rules.recoveryDegree.ToString("0.0"),()=>rules.recoveryDegree=rules.recoveryDegree>=6?1:rules.recoveryDegree+.5f);
            help.text="Host rules apply to the shared match. Select a value to step through its range.";
            RenderOrganizedMenuGrid(names,actions,enabled,art,captions,pictures,pressed);
        }

        int menuCategory=-1,menuSection=-1,equipmentOffset;
        bool batchPage,quickGunPage;
        int batchTarget;
        UnityEngine.UI.Button backButton,homeButton;
        BoxCollider backTarget,homeTarget;
        Texture2D disasterPreview;
        RectTransform menuBody;
        readonly UnityEngine.UI.Button[] tabButtons=new UnityEngine.UI.Button[4],sectionButtons=new UnityEngine.UI.Button[6];
        readonly BoxCollider[] tabTargets=new BoxCollider[4],sectionTargets=new BoxCollider[6];
        UnityEngine.UI.Text navigationPath,sectionTitle;
        readonly UnityEngine.UI.Text[] tabLabels=new UnityEngine.UI.Text[4],sectionLabels=new UnityEngine.UI.Text[6];
        bool AtHome=>!batchPage&&!mapPage&&!spectatePage&&!giveEquipmentPage&&!randomPowerupPage&&!powerupPage&&
            !tabletopPage&&!hudPage&&!modePage&&!disasterPage&&!networkPage&&!comfortPage&&!tuningPage&&menuCategory<0;

        void SizeNavigation(UnityEngine.UI.Button button,BoxCollider target,Vector2 position,Vector2 size,int font)
        {
            var rect=(RectTransform)button.transform;rect.anchoredPosition=position;rect.sizeDelta=size;
            target.size=new Vector3(size.x,size.y,4);
            var text=button.GetComponentInChildren<UnityEngine.UI.Text>();text.rectTransform.sizeDelta=size-new Vector2(12,6);text.fontSize=font;
        }
        void BuildNavigation()
        {
            var original=new System.Collections.Generic.List<Transform>();foreach(Transform child in transform)original.Add(child);
            menuBody=new GameObject("Tablet page contents",typeof(RectTransform)).GetComponent<RectTransform>();
            menuBody.SetParent(transform,false);menuBody.sizeDelta=new Vector2(600,1080);
            foreach(var child in original)child.SetParent(menuBody,false);
            menuBody.anchoredPosition=Vector2.zero;menuBody.localScale=Vector3.one;
            for(int i=0;i<tabButtons.Length;i++)
            {
                tabButtons[i]=HeaderButton(DemoMenuSections.Tabs[i],0,out tabTargets[i]);
                SizeNavigation(tabButtons[i],tabTargets[i],new Vector2(-345+i*230,490),new Vector2(216,62),26);
                tabLabels[i]=tabButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();
                tabLabels[i].supportRichText=true;tabLabels[i].resizeTextForBestFit=false;tabLabels[i].lineSpacing=.85f;
                tabLabels[i].text=DemoMenuSections.Tabs[i]+"\n<size=20>"+DemoMenuSections.TabDescriptions[i]+"</size>";
            }
            backButton=HeaderButton("Back",0,out backTarget);homeButton=HeaderButton("Demo Home",0,out homeTarget);
            SizeNavigation(backButton,backTarget,new Vector2(-345,425),new Vector2(210,46),22);
            SizeNavigation(homeButton,homeTarget,new Vector2(-115,425),new Vector2(210,46),22);
            navigationPath=Text("",new Vector2(235,425),new Vector2(430,46),22);
            sectionTitle=Text("SECTIONS",new Vector2(-350,365),new Vector2(210,32),18);sectionTitle.color=CandyHudVisuals.Muted;
            for(int i=0;i<sectionButtons.Length;i++)
            {
                sectionButtons[i]=HeaderButton("",0,out sectionTargets[i]);
                SizeNavigation(sectionButtons[i],sectionTargets[i],new Vector2(-350,315-i*58),new Vector2(210,50),21);
                sectionLabels[i]=sectionButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();
            }
        }
        void OpenNavigationTab(int index)
        {
            RestoreEquipmentWheelLayout();ResetPages();menuCategory=index;OpenMenuSection(0);
        }
        int NavigationCategory()
        {
            if(giveEquipmentPage)return giveAllEquipment?1:0;
            if(batchPage)return 1;
            if(disasterPage||mapPage||modePage||powerupPage||randomPowerupPage||tabletopPage)return 2;
            if(comfortPage||networkPage||tuningPage||hudPage)return 3;
            if(spectatePage)return 0;
            return menuCategory>=0?menuCategory:0;
        }
        string NavigationSection()
        {
            if(giveEquipmentPage)return giveAllEquipment?"Bot equipment":"Equipment";
            if(batchPage)return "Survivors";
            if(disasterPage)return "Disasters";if(mapPage)return "Maps";
            if(modePage||tabletopPage)return "Modes";if(powerupPage)return "Ground pickups";
            if(randomPowerupPage)return "Powerup spawning";if(networkPage)return "Friends";
            if(comfortPage||tuningPage||hudPage)return "Settings";if(spectatePage)return "Spectate";
            var sections=DemoMenuSections.Sections[NavigationCategory()];
            return menuSection>=0&&menuSection<sections.Length?sections[menuSection]:"Overview";
        }
        bool PointPersistentNavigation(bool pressed)
        {
            if(tabletDeveloperDetails)
            {
                for(int i=0;i<tabButtons.Length;i++)tabButtons[i].gameObject.SetActive(false);
                for(int i=0;i<sectionButtons.Length;i++)sectionButtons[i].gameObject.SetActive(false);
                if(sectionTitle)sectionTitle.gameObject.SetActive(false);
                if(navigationPath)navigationPath.gameObject.SetActive(false);
                return false;
            }
            int category=NavigationCategory();
            for(int i=0;i<tabButtons.Length;i++)
            {
                bool hover=Hit(tabTargets[i],pointerRay);bool selected=category==i;
                tabButtons[i].image.color=hover?CandyHudVisuals.Mint:selected?CandyHudVisuals.Lavender:CandyHudVisuals.Panel;
                tabLabels[i].color=hover||selected?CandyHudVisuals.Ink:Color.white;
                if(hover&&pressed){OpenNavigationTab(i);return true;}
            }
            var sections=DemoMenuSections.Sections[category];sectionTitle.text=DemoMenuSections.Tabs[category].ToUpperInvariant()+" / SECTIONS";
            for(int i=0;i<sectionButtons.Length;i++)
            {
                bool visible=i<sections.Length;sectionButtons[i].gameObject.SetActive(visible);if(!visible)continue;
                string name=sections[i];sectionLabels[i].text=name;
                bool selected=NavigationSection()==name;bool hover=Hit(sectionTargets[i],pointerRay);
                sectionButtons[i].image.color=hover?CandyHudVisuals.Mint:selected?CandyHudVisuals.Lavender:CandyHudVisuals.Card;
                sectionLabels[i].color=hover||selected?CandyHudVisuals.Ink:Color.white;
                if(hover)help.text=DemoMenuSections.Caption(name);
                if(hover&&pressed){RestoreEquipmentWheelLayout();ResetPages();menuCategory=category;OpenMenuSection(i);return true;}
            }
            navigationPath.text=DemoMenuSections.Tabs[category]+" / "+NavigationSection();
            if(disasterPage&&disasterSelection>=0)navigationPath.text="Match / Disasters / Settings";
            if(giveEquipmentPage)navigationPath.text=(giveAllEquipment?"Bots":"Personal")+" / Equipment";
            return false;
        }
        bool PointNavigation(bool pressed)
        {
            if(PointPersistentNavigation(pressed))return true;
            if(tabletDeveloperDetails)
            {
                if(backButton)backButton.gameObject.SetActive(false);
                if(homeButton)homeButton.gameObject.SetActive(false);
                return false;
            }
            bool available=!DeadlyJenga.Presentation.CandyWildlifePanel.Showing(session);
            backButton.gameObject.SetActive(available);homeButton.gameObject.SetActive(available);
            if(!available)return false;
            homeTarget.enabled=!AtHome;
            backButton.GetComponentInChildren<UnityEngine.UI.Text>().text=AtHome?"Resume":"Back";
            bool back=Hit(backTarget,pointerRay),home=Hit(homeTarget,pointerRay);
            backButton.image.color=back?CandyHudVisuals.Mint:CandyHudVisuals.Lavender;
            homeButton.image.color=home?CandyHudVisuals.Mint:homeTarget.enabled?CandyHudVisuals.Lavender:CandyHudVisuals.Card;
            homeButton.GetComponentInChildren<UnityEngine.UI.Text>().color=homeTarget.enabled?CandyHudVisuals.Ink:CandyHudVisuals.Muted;
            if(!pressed)return false;
            if(home)
            {
                if(tabletDeveloperDetails)
                {
                    CloseDeveloperDetails();
                    return true;
                }
                ResetPages();menuCategory=menuSection=-1;return true;
            }
            if(!back)return false;
            if(tabletDeveloperDetails&&disasterSelection<0&&menuSection<0)
            {
                CloseDeveloperDetails();
                return true;
            }
            if(AtHome){session.SetMenu(false);return true;}
            if(disasterPage&&disasterSelection>=0){disasterSelection=-1;return true;}
            if(batchPage&&batchTarget>0){batchTarget=0;return true;}
            bool child=networkPage||comfortPage||tuningPage||modePage||hudPage||mapPage||spectatePage||
                tabletopPage||disasterPage||giveEquipmentPage||randomPowerupPage||powerupPage||batchPage;
            ResetPages();
            if(child||menuSection>=0)menuSection=-1;
            else menuCategory=-1;
            return true;
        }
        void ResetPages()
        {
            roomLeave.Clear();
            modeWheel=false;
            networkPage=comfortPage=tuningPage=modePage=hudPage=mapPage=spectatePage=
                tabletopPage=disasterPage=giveEquipmentPage=randomPowerupPage=powerupPage=batchPage=quickGunPage=false;
            equipmentOffset=disasterOffset=mapOffset=batchTarget=0;disasterSelection=-1;
            if (disasterWheelUI) disasterWheelUI.SetActive(false);
            if (modeWheelUI) modeWheelUI.SetActive(false);
        }
        void SetRowPreview(int i,Texture image, bool twoCols = false, float btnW = 980f)
        {
            if(!image){mapThumbnails[i].gameObject.SetActive(false);return;}
            var preview=mapThumbnails[i];preview.texture=image;preview.gameObject.SetActive(true);
            sectionGlyphs[i].gameObject.SetActive(false);
            if (twoCols)
            {
                float fit=Mathf.Min(80f/image.width,42f/image.height);
                preview.rectTransform.sizeDelta=new Vector2(image.width*fit,image.height*fit);
                preview.rectTransform.anchoredPosition=new Vector2(-btnW*0.5f+50f,0);
                labels[i].rectTransform.sizeDelta=new Vector2(btnW-140f,46);
                labels[i].rectTransform.anchoredPosition=new Vector2(50f,0);
            }
            else
            {
                float fit=Mathf.Min(108f/image.width,48f/image.height);
                preview.rectTransform.sizeDelta=new Vector2(image.width*fit,image.height*fit);
                preview.rectTransform.anchoredPosition=new Vector2(-btnW*0.5f+75f,0);
                labels[i].rectTransform.sizeDelta=new Vector2(btnW-200f,60);
                labels[i].rectTransform.anchoredPosition=new Vector2(70f,0);
            }
        }
        void SwitchDemoRole()
        {
            if(!DemoMode)return;
            session.SetRole(!session.PlayingHand);
            session.SetMenu(false);
        }
        bool matchAdvancedExpanded;

        int developerMapFilter;
        bool developerMapFiltersOpen,developerMapSearchOpen;
        string developerMapSearch="";
        RectTransform developerMapKeyboard;
        UnityEngine.UI.Text developerSearchText;
        readonly System.Collections.Generic.List<TabletButton> developerSearchKeys=new System.Collections.Generic.List<TabletButton>();
        void PointDeveloperSearch(bool pressed)
        {
            if(!developerMapKeyboard)
            {
                developerMapKeyboard=new GameObject("Map search keyboard",typeof(RectTransform)).GetComponent<RectTransform>();
                developerMapKeyboard.SetParent(legacyDeveloper,false);
                developerSearchText=TabletText(developerMapKeyboard,"",new Vector2(0,176),new Vector2(960,52),30);
                string[] rows={"1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM"};
                for(int row=0;row<rows.Length;row++)for(int column=0;column<rows[row].Length;column++)
                {
                    char key=rows[row][column];float x=(column-(rows[row].Length-1)*.5f)*92;
                    TabletControl(developerSearchKeys,developerMapKeyboard,()=>key.ToString(),new Vector2(x,110-row*66),new Vector2(82,56),()=>{if(developerMapSearch.Length<40)developerMapSearch+=key;mapOffset=0;},font:30);
                }
                TabletControl(developerSearchKeys,developerMapKeyboard,()=>"Space",new Vector2(-320,-180),new Vector2(260,58),()=>{if(developerMapSearch.Length<40)developerMapSearch+=" ";mapOffset=0;},font:25);
                TabletControl(developerSearchKeys,developerMapKeyboard,()=>"Delete",new Vector2(-90,-180),new Vector2(140,58),()=>{if(developerMapSearch.Length>0)developerMapSearch=developerMapSearch.Substring(0,developerMapSearch.Length-1);mapOffset=0;},font:25);
                TabletControl(developerSearchKeys,developerMapKeyboard,()=>"Clear",new Vector2(90,-180),new Vector2(140,58),()=>{developerMapSearch="";mapOffset=0;},font:25);
                TabletControl(developerSearchKeys,developerMapKeyboard,()=>"Show results",new Vector2(320,-180),new Vector2(260,58),()=>developerMapSearchOpen=false,font:25);
                DeadlyJenga.Presentation.MenuAim.PresentInForeground(developerMapKeyboard);
            }
            developerMapKeyboard.gameObject.SetActive(true);
            foreach(var button in buttons)button.gameObject.SetActive(false);
            developerSearchText.text="Search: "+developerMapSearch+"_";
            heading.text="MAP / SEARCH";heading.rectTransform.anchoredPosition=new Vector2(0,240);heading.fontSize=28;
            help.text="Type a map name, then show results.";help.rectTransform.anchoredPosition=new Vector2(0,-320);
            PointTabletButtons(developerSearchKeys,pressed);
        }
        void PointTabletMapPage(Camera camera, bool pressed)
        {
            var lobby=session as ILobbyControls;var details=session as ILobbyDetails;var maps=Demo as IDemoMapControls;
            bool chooseMode=lobby!=null&&menuSection==0;
            string selected=chooseMode?details?.SelectedModeId:details?.SelectedMapId??maps?.CurrentMapId;
            var source=chooseMode?details?.Modes:details?.Maps??maps?.Maps;
            var options=new System.Collections.Generic.List<LobbyOption>();
            if(source!=null)foreach(var option in source)
                if((chooseMode||developerMapFilter==0||(int)MapCategoryPicker.GetMainTab(option)==developerMapFilter-1)&&
                    (chooseMode||string.IsNullOrEmpty(developerMapSearch)||option.title.IndexOf(developerMapSearch,StringComparison.OrdinalIgnoreCase)>=0))options.Add(option);
            var names=new System.Collections.Generic.List<string>();var actions=new System.Collections.Generic.List<Action>();
            var enabled=new System.Collections.Generic.List<bool>();var art=new System.Collections.Generic.List<CandyHudIcon>();
            var captions=new System.Collections.Generic.List<string>();var pictures=new System.Collections.Generic.List<Texture>();
            void Add(string label,Action action,bool available=true,Texture picture=null,string caption="")
            {names.Add(label);actions.Add(action);enabled.Add(available);art.Add(chooseMode?CandyHudIcon.Play:CandyHudIcon.Home);captions.Add(caption);pictures.Add(picture);}
            heading.text=chooseMode?"DEVELOPER / MODE":"DEVELOPER / MAP";
            string[] filters={"All maps","Play approved","In development","Archive"};
            if(!chooseMode&&developerMapSearchOpen){PointDeveloperSearch(pressed);return;}
            if(developerMapKeyboard)developerMapKeyboard.gameObject.SetActive(false);
            if(!chooseMode)Add("Search: "+(developerMapSearch.Length==0?"all names":developerMapSearch),()=>{developerMapSearchOpen=true;developerMapFiltersOpen=false;});
            if(!chooseMode)Add("Filter: "+filters[developerMapFilter]+"  v",()=>developerMapFiltersOpen=!developerMapFiltersOpen);
            if(!chooseMode&&developerMapFiltersOpen)
            {
                for(int i=0;i<filters.Length;i++){int filter=i;Add(filters[i],()=>{developerMapFilter=filter;developerMapFiltersOpen=false;mapOffset=0;});}
            }
            else
            {
                const int pageSize=10;
                mapOffset=Mathf.Clamp(mapOffset,0,Mathf.Max(0,(options.Count-1)/pageSize*pageSize));
                if(options.Count==0)Add("No available entries in this filter",()=>{},false);
                for(int i=mapOffset;i<Mathf.Min(options.Count,mapOffset+pageSize);i++)
                {
                    var option=options[i];
                    Add((option.id==selected?"Selected: ":"")+option.title,()=>{
                        if(chooseMode)lobby.SelectMode(option.id);else if(lobby!=null)lobby.SelectMap(option.id);else maps.SelectMap(option.id);
                    },(lobby?.CanConfigure??maps?.CanSelectMap==true)&&option.available&&DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session),
                    chooseMode?null:MapPreviewImages.Get(option.id),option.description);
                }
                if(mapOffset>0)Add("Previous",()=>mapOffset-=pageSize);
                if(mapOffset+pageSize<options.Count)Add("Next",()=>mapOffset+=pageSize);
            }
            help.text=chooseMode?"Select the mode for the next match.":"Selecting a map rebuilds the world. Connected players follow the host.";
            RenderOrganizedMenuGrid(names,actions,enabled,art,captions,pictures,pressed);
        }

        void PointOrganizedMenu(Camera camera,bool pressed)
        {
            HideDisasterSliders();scaffoldButton.gameObject.SetActive(false);
            if (!disasterPage && disasterWheelUI && disasterWheelUI.activeSelf) disasterWheelUI.SetActive(false);
            if (!modePage && modeWheelUI && modeWheelUI.activeSelf) modeWheelUI.SetActive(false);
            if(disasterPage){PointDisasterPage(camera,pressed,false);return;}
            if(modePage){PointModePage(camera,pressed);return;}
            if(mapPage){PointTabletMapPage(camera,pressed);return;}
            var actions=new System.Collections.Generic.List<Action>();var names=new System.Collections.Generic.List<string>();var enabled=new System.Collections.Generic.List<bool>();var art=new System.Collections.Generic.List<CandyHudIcon>();var captions=new System.Collections.Generic.List<string>();var pictures=new System.Collections.Generic.List<Texture>();
            void Add(string name,Action action,bool available=true,CandyHudIcon icon=CandyHudIcon.Key,string caption="",Texture picture=null)
            {names.Add(name);actions.Add(action);enabled.Add(available);art.Add(icon);captions.Add(caption);pictures.Add(picture);}
            var lobby = session as ILobbyControls;
            var match = lobby == null ? MatchRuntime.Current : null;
            var bots=session.SurvivorBots;
            int humans=Mathf.Max(1,session.Multiplayer?.HumanCount??1);
            bool canBatch=CanCommand&&match?.Authority==true&&bots!=null;
            var map=Demo as IDemoMapControls;
            var mapPreview=map!=null?MapPreviewImages.Get(map.CurrentMapId):null;
            if(batchPage)
            {
                heading.text="BOT POPULATION";
                Add("Current: "+(bots?.Count??0)+" bots + "+humans+" human",()=>{},false,CandyHudIcon.Friends,picture:mapPreview);
                long capacity=(long)humans+(bots?.Capacity??0);
                foreach(int value in new[]{10,25,50,100,150,200})
                {
                    if(value>capacity)continue;
                    int total=value,needed=Mathf.Max(0,total-humans-(bots?.Count??0));
                    Add("Preview "+total+" total  /  add up to "+needed+" bots",()=>batchTarget=total,canBatch,CandyHudIcon.Survivor,
                        "Total includes humans. No existing bots are removed.");
                }
                if(batchTarget>0)
                {
                    Add("Fill to "+batchTarget+" total",()=>{
                        if(!canBatch)return;bots.TargetPopulation=batchTarget;Demo.Run(DemoCommand.FillSurvivors);
                    },canBatch,CandyHudIcon.Play,"Apply the preview using the existing host fill command.");
                }
                Add("Cancel",()=>{batchPage=false;batchTarget=0;},icon:CandyHudIcon.Home);
            }
            else if(menuCategory<0){heading.text="IN-GAME CONTROLS";
                Add("CONTROLS / LEFT OR RIGHT HAND",()=>DeadlyJenga.Presentation.CandyComfortPanel.Open(session),true,CandyHudIcon.Settings,"Dominant: look / jump / fire. Other: move / sprint.");
                var experience=DeadlyJenga.Presentation.GameExperiencePanel.Resolve(session);
                Add(CanCommand?"DEMO CONTROLS: ENABLED":"ENABLE DEMO CONTROLS",()=>{
                    if(DeadlyJenga.Presentation.GameExperiencePanel.CanEnableDemo(experience))experience.SetExperience(GameExperience.Demo);
                    else DeadlyJenga.Presentation.GameExperiencePanel.Open(session);
                },true,CandyHudIcon.Settings,CanCommand?"Testing actions available to permitted players":"Enable testing tools, or inspect host permission / room status.");
                if(!disasterPreview)disasterPreview=Resources.Load<Texture2D>("LobbyModePictures/disasters");
                if (lobby == null)
                    Add("Rebuild Map",()=>session.ResetMap(),session.CanEditWorld,CandyHudIcon.Home,"Rebuild the current map. Host only.",mapPreview);
                else
                    Add(InLobbyVisit ? "Return to match" : lobby.StartPending ? "Cancel match start" : "Start match",
                        lobby.StartOrCancel, lobby.CanStart, CandyHudIcon.Play, lobby.SetupHint);
                Add("Disasters",()=>{disasterPage=true;disasterSelection=-1;},Demo!=null,CandyHudIcon.Flight,"Preview and tune disasters before launch.",disasterPreview);
                Add("Give Yourself a Gun",()=>{quickGunPage=true;giveEquipmentPage=true;giveAllEquipment=false;equipmentOffset=0;},
                    CanCommand&&!session.PlayingHand&&match?.Authority==true&&match is IDemoEquipmentControls,CandyHudIcon.Trigger,
                    "Choose a firearm. Your previous item drops at your feet.",DeadlyJenga.Presentation.CandyEquipmentView.Outline(EquipmentKind.Blaster));
                Add("Bot Population",()=>{
                    if (lobby == null) { batchPage=true; batchTarget=0; }
                    else { menuCategory=1; menuSection=0; }
                }, lobby == null ? canBatch : lobby.CanConfigure && session is ILobbyDetails,
                    CandyHudIcon.Friends,lobby == null ? "Preview a large batch before applying." : "Set the total players for the next match.");
                Add(session.PlayingHand?"Become Survivor":"Become Flying Hand",SwitchDemoRole,DemoMode,CandyHudIcon.Hand,"Switch your local demo character.");
                var hand=session.Player?.gameObject.GetComponent<IBlasterControls>();
                Add("Game / Demo & Round Setup",()=>DeadlyJenga.Presentation.GameExperiencePanel.Open(session),DeadlyJenga.Presentation.GameExperiencePanel.Resolve(session)!=null,CandyHudIcon.Play,"Profile, readiness and automatic rounds");
                Add("Hand Appearance",()=>CharacterPicker.Open(session),HandLooks.Names.Length>0,CandyHudIcon.Hand,"Choose the appearance of your flying hand.");
                var drop=match as IEquipmentDropRuntime;var carried=session.Player?.gameObject.GetComponent<IEquipmentControls>();
                Add("Drop weapon here",()=>drop?.RequestDropEquipment(),drop!=null&&!session.PlayingHand&&carried?.Equipment.kind>=EquipmentKind.Blaster,CandyHudIcon.Hand,"Drops your carried item. No controller chord is required.");
                for(int i=0;i<4;i++){int index=i;Add(DemoMenuSections.Tabs[i],()=>{menuCategory=index;menuSection=-1;},icon:DemoMenuSections.Icon(DemoMenuSections.Tabs[i]),caption:DemoMenuSections.TabDescriptions[i]);}Add("Resume",()=>session.SetMenu(false));}
            else if(menuSection<0)
            {
                heading.text=DemoMenuSections.Tabs[menuCategory];Add("All tabs",()=>menuCategory=-1,icon:CandyHudIcon.Home);
                var sections=DemoMenuSections.Sections[menuCategory];
                if(menuCategory==2) // Match
                {
                    for(int i=0;i<4;i++)
                    {
                        int index=i;
                        Add(sections[i]+"   /   "+DemoMenuSections.Caption(sections[i]),()=>OpenMenuSection(index),LobbySectionAvailable(sections[i]),DemoMenuSections.Icon(sections[i]),DemoMenuSections.Caption(sections[i]));
                    }
                    Add("Advanced Settings   " + (matchAdvancedExpanded ? "▲" : "▼"), () => matchAdvancedExpanded = !matchAdvancedExpanded, icon: CandyHudIcon.Settings, caption: "Extra options: ground pickups and automatic powerups");
                    if(matchAdvancedExpanded)
                    {
                        for(int i=4;i<sections.Length;i++)
                        {
                            int index=i;
                            Add(sections[i]+"   /   "+DemoMenuSections.Caption(sections[i]),()=>OpenMenuSection(index),LobbySectionAvailable(sections[i]),DemoMenuSections.Icon(sections[i]),DemoMenuSections.Caption(sections[i]));
                        }
                    }
                }
                else
                {
                    for(int i=0;i<sections.Length;i++){int index=i;Add(sections[i]+"   /   "+DemoMenuSections.Caption(sections[i]),()=>OpenMenuSection(index),LobbySectionAvailable(sections[i]),DemoMenuSections.Icon(sections[i]),DemoMenuSections.Caption(sections[i]));}
                }
            }
            else
            {
                string name=DemoMenuSections.Sections[menuCategory][menuSection];heading.text=DemoMenuSections.Tabs[menuCategory]+" / "+name;Add(developerChild?"Back to "+developerRoute:"Back to "+DemoMenuSections.Tabs[menuCategory],()=>{if(developerChild){ResetPages();developerChild=false;}else menuSection=-1;},icon:CandyHudIcon.Home);
                if(name=="Character")
                {
                    Add(session.PlayingHand?"Become Survivor":"Become Flying Hand",SwitchDemoRole,DemoMode,CandyHudIcon.Hand,"Switch your local demo character.");
                    Add("Respawn on top",()=>{session.SetRole(false);session.SetMenu(false);},DemoMode);
                    Add("Third person inspection",()=>session.Player?.ThirdPerson.Begin(),session.Player?.ThirdPerson?.CanInspect==true);
                    var item=session.Player.gameObject.GetComponent<IEquipmentControls>();var gun=session.Player.gameObject.GetComponent<IBlasterControls>();
                    Add(item?.Equipment.drawn==true?"Holster "+item.Equipment.kind:"Draw "+(item?.Equipment.kind.ToString()??"equipment"),()=>item?.SetEquipped(!item.Equipment.drawn),item?.Equipment.kind>=EquipmentKind.Blaster);
                    var drop=MatchRuntime.Current as IEquipmentDropRuntime;
                    Add("Drop weapon here",()=>drop?.RequestDropEquipment(),drop!=null&&!session.PlayingHand&&item?.Equipment.kind>=EquipmentKind.Blaster,CandyHudIcon.Hand);
                    Add("Dominant hand: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),()=>gun?.SetLeftHanded(!gun.LeftHanded),gun!=null);
                    Add("Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),()=>{if(MatchRuntime.Current?.Authority==true)gun?.SetTestingEnabled(!gun.TestingEnabled);},gun!=null&&CanCommand&&MatchRuntime.Current?.Authority==true);
                }
                if(name=="Survivors"||name=="Giant hands")
                {
                    bool hand = name == "Giant hands";
                    if (lobby != null && session is ILobbyDetails details)
                    {
                        var setup = details.Setup;
                        int count = hand ? setup.hands : setup.population;
                        Add((hand ? "Flying hands" : "Match players (humans + bots)") + ": " + count,
                            () => { }, false, CandyHudIcon.Friends);
                        Add("Decrease",()=>AdjustLobbyPopulation(lobby,details,hand,-1),lobby.CanConfigure && count > (hand ? 0 : 1));
                        Add("Increase",()=>AdjustLobbyPopulation(lobby,details,hand,1),lobby.CanConfigure && count < 100);
                    }
                    else
                    {
                        Add("Spawn one",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.AddHand:DemoCommand.AddSurvivor);},CanCommand);
                        if(!hand)Add("Fill to target population",()=>{if(CanCommand)Demo.Run(DemoCommand.FillSurvivors);},CanCommand);
                        Add("Next personality",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.NextHand:DemoCommand.NextSurvivor);},CanCommand);
                        Add("Clear population",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.ClearHands:DemoCommand.ClearSurvivors);},CanCommand);
                    }
                }
                if(name=="World")
                {
                    Add("Reset / rebuild map",()=>{if(session.CanEditWorld)session.ResetMap();},session.CanEditWorld);
                    if(Demo is IBuildUpControls build&&build.Available)Add("Deliver scaffold tier",()=>{if(build.CanAdvance)build.AdvanceStage();},build.CanAdvance);
                    Add(MultiplayerPanel.EndAction(session.Multiplayer),()=>{if(MatchRuntime.Current is IMatchReviewControls r&&r.CanEndGame)r.EndGame();},(MatchRuntime.Current as IMatchReviewControls)?.CanEndGame==true);
                }
                if(name=="Enemies")
                {
                    var enemies=session.SurvivorBots as IEnemyPopulation;
                    Add("Spawn jaguar enemy",()=>{if(CanCommand)enemies?.SpawnEnemy(new EnemySpawnSettings());},CanCommand&&enemies!=null);
                    Add("Spawn mushroom enemy",()=>{if(CanCommand)enemies?.SpawnEnemy(new EnemySpawnSettings{archetypeId="mushroom",hitDamage=10});},CanCommand&&enemies!=null);
                    Add("Spawn Longhand",()=>enemies?.SpawnEnemy(new EnemySpawnSettings{archetypeId="longhand",hitDamage=25}),CanCommand&&enemies!=null);
                    Add("Spawn Gum Brute",()=>enemies?.SpawnEnemy(new EnemySpawnSettings{archetypeId="gum-brute",hitDamage=40}),CanCommand&&enemies!=null);
                    Add("Clear enemy NPCs",()=>{if(CanCommand)enemies?.ClearEnemies();},CanCommand&&enemies!=null);
                }
                if(name=="Controls"){Add("VR movement / gestures",()=>comfortPage=true);
                    Add("Stamina / audio / scenery",()=>tuningPage=true);}
                if(name=="Ground pickups")
                {
                    int max=Mathf.Min(10,EquipmentSpecifications.All.Length-1-equipmentOffset);
                    for(int j=0;j<max;j++){var item=(EquipmentKind)(equipmentOffset+j+1);Add("Spawn "+EquipmentSpecifications.Get(item).name,()=>{if(CanCommand&&DemoMode)(MatchRuntime.Current as IEquipmentRuntime)?.SpawnEquipment(item);},CanCommand&&!session.PlayingHand&&MatchRuntime.Current is IEquipmentRuntime);}
                    if(equipmentOffset>0)Add("Previous items",()=>equipmentOffset=Mathf.Max(0,equipmentOffset-10));if(equipmentOffset+10<EquipmentSpecifications.All.Length-1)Add("More items",()=>equipmentOffset+=10);
                }
            }
            
            help.text=batchPage?batchTarget>0?"Preview selected: "+batchTarget+" total. Choose Fill to apply.":"Choose a target to preview; it will not spawn until you apply.":menuCategory<0?"Choose a tab. Aim at a row and select.":
                menuSection<0?"Choose a section. Each row shows what it contains.":
                DemoMenuSections.Caption(DemoMenuSections.Sections[menuCategory][menuSection])+". Shared changes may require the host.";
            RenderOrganizedMenuGrid(names, actions, enabled, art, captions, pictures, pressed);
        }

        void RenderOrganizedMenuGrid(System.Collections.Generic.List<string> names, System.Collections.Generic.List<Action> actions, System.Collections.Generic.List<bool> enabled, System.Collections.Generic.List<CandyHudIcon> art, System.Collections.Generic.List<string> captions, System.Collections.Generic.List<Texture> pictures, bool pressed)
        {
            var ray=pointerRay;
            sectionGlyphsActive=true;

            if (heading != null)
            {
                heading.rectTransform.anchoredPosition = new Vector2(0, 230);
                heading.rectTransform.sizeDelta = new Vector2(900, 50);
                heading.fontSize = 28;
                heading.fontStyle = FontStyle.Bold;
                heading.color = TabletCream;
            }
            if (help != null)
            {
                help.rectTransform.anchoredPosition = new Vector2(0, -320);
                help.rectTransform.sizeDelta = new Vector2(900, 44);
                help.fontSize = 18;
            }

            int total = names.Count;
            bool twoCols = total > 6;
            int rowsPerCol = twoCols ? (total + 1) / 2 : total;
            float btnW = twoCols ? 472f : 944f;
            float btnH = Mathf.Min(68f, 456f / Mathf.Max(1, rowsPerCol) - 12f);
            float rowHeight = twoCols ? btnH + 10f : btnH + 12f;
            float startY = 164f - btnH * .5f;

            for(int i=0;i<buttons.Length;i++)
            {
                bool visible=i<names.Count;
                buttons[i].gameObject.SetActive(visible);targets[i].enabled=visible&&enabled[i];
                if(!visible)continue;

                int col = twoCols ? (i < rowsPerCol ? 0 : 1) : 0;
                int row = twoCols ? (i < rowsPerCol ? i : i - rowsPerCol) : i;
                float x = twoCols ? (col == 0 ? -255f : 255f) : 0f;
                float y = startY - row * rowHeight;

                var rect = (RectTransform)buttons[i].transform;
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = new Vector2(btnW, btnH);
                targets[i].size = new Vector3(btnW, btnH, 4);

                labels[i].text=names[i];
                labels[i].fontSize = twoCols ? (btnH < 50 ? 17 : 20) : (total <= 5 ? 26 : 22);
                labels[i].fontStyle = FontStyle.Bold;
                labels[i].alignment = TextAnchor.MiddleLeft;
                labels[i].resizeTextForBestFit = false;
                bool hasIcon=art[i]!=CandyHudIcon.Settings;
                labels[i].rectTransform.sizeDelta = new Vector2(btnW - (hasIcon?92:40), btnH - 8);
                labels[i].rectTransform.anchoredPosition = new Vector2(hasIcon?24:0, 0);

                if(sectionGlyphs[i])
                {
                    sectionGlyphs[i].gameObject.SetActive(art[i] != CandyHudIcon.Settings);
                    sectionGlyphs[i].texture=CandyHudVisuals.Icon(art[i]);
                    sectionGlyphs[i].rectTransform.sizeDelta = new Vector2(32, 32);
                    sectionGlyphs[i].rectTransform.anchoredPosition = new Vector2(-btnW * 0.5f + 30, 0);
                }
                SetRowPreview(i,pictures[i],twoCols,btnW);

                bool hover=targets[i].enabled&&Hit(targets[i],ray);
                bool fold=names[i].StartsWith("▶")||names[i].StartsWith("▼");
                buttons[i].image.color=hover?CandyHudVisuals.Mint:fold?new Color(.32f,.14f,.42f):enabled[i]?CandyHudVisuals.Card:new Color(.21f,.12f,.28f);
                labels[i].color=hover?CandyHudVisuals.Ink:enabled[i]?TabletCream:CandyHudVisuals.Muted;
                if(hover&&!string.IsNullOrEmpty(captions[i]))help.text=captions[i];
                if(hover&&pressed){actions[i]();break;}
            }
        }

        static void AdjustLobbyPopulation(ILobbyControls lobby, ILobbyDetails details, bool hands, int step)
        {
            if (!lobby.CanConfigure) return;
            var setup = details.Setup;
            if (setup == null) return;
            if (hands) setup.hands = Mathf.Clamp(setup.hands + step, 0, 100);
            else setup.population = Mathf.Clamp(setup.population + step, 1, 100);
            details.ConfigureSetup(setup);
        }

        bool LobbySectionAvailable(string section)
        {
            if (session is not ILobbyControls) return true;
            return section switch
            {
                "Modes" or "Maps" or "Survivors" or "Giant hands" => session is ILobbyDetails,
                "Controls" or "Settings" or "Friends" => true,
                _ => false
            };
        }

        void OpenMenuSection(int index)
        {
            string name = DemoMenuSections.Sections[menuCategory][index];
            if (!LobbySectionAvailable(name)) return;
            if (session is ILobbyControls && name == "Friends") { OpenPlayerTabletPage(TabletPage.Room); return; }
            menuSection=index;equipmentOffset=0;
            if(name=="Equipment"||name=="Bot equipment"){giveAllEquipment=name=="Bot equipment";giveEquipmentPage=true;}
            if(name=="Spectate")spectatePage=true;
            if(name=="Maps" || name=="Modes" && session is ILobbyControls)mapPage=true;
            else if(name=="Modes")modePage=modeWheel=true;
            if(name=="Disasters")disasterPage=true;
            if(name=="Settings"||name=="Voice")comfortPage=true;if(name=="Friends")networkPage=true;if(name=="Powerup spawning")randomPowerupPage=true;
        }
    }
}
