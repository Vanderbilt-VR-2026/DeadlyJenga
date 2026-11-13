using System;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
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
            menuBody.anchoredPosition=new Vector2(120,-50);menuBody.localScale=Vector3.one*.8f;
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
                    tabletDeveloperDetails=false;
                    legacyDeveloper.gameObject.SetActive(false);
                    if(tabletNav)tabletNav.gameObject.SetActive(true);
                    tabletPages[(int)TabletPage.Developer].gameObject.SetActive(true);
                    return true;
                }
                ResetPages();menuCategory=menuSection=-1;return true;
            }
            if(!back)return false;
            if(tabletDeveloperDetails&&disasterSelection<0&&menuSection<0)
            {
                tabletDeveloperDetails=false;
                legacyDeveloper.gameObject.SetActive(false);
                if(tabletNav)tabletNav.gameObject.SetActive(true);
                tabletPages[(int)TabletPage.Developer].gameObject.SetActive(true);
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
        }
        void SetRowPreview(int i,Texture image)
        {
            if(!image)return;
            var preview=mapThumbnails[i];preview.texture=image;preview.gameObject.SetActive(true);
            float fit=Mathf.Min(108f/image.width,43f/image.height);
            preview.rectTransform.sizeDelta=new Vector2(image.width*fit,image.height*fit);
            labels[i].rectTransform.sizeDelta=new Vector2(350,46);labels[i].rectTransform.anchoredPosition=new Vector2(-34,0);
        }
        void SwitchDemoRole()
        {
            if(!DemoMode)return;
            session.SetRole(!session.PlayingHand);
            session.SetMenu(false);
        }
        bool matchAdvancedExpanded;

        void PointTabletMapPage(Camera camera, bool pressed)
        {
            var maps = Demo as IDemoMapControls;
            vrMapPicker.Update(maps?.Maps, maps?.CurrentMapId);
            var options = vrMapPicker.Rows;
            heading.text = "MAP SELECTION\n<size=17><color=#d0b8d8>" + (maps?.MapStatus ?? "Choose a map to preview and launch") + "</color></size>";
            var actions = new System.Collections.Generic.List<Action>();
            var names = new System.Collections.Generic.List<string>();
            var enabled = new System.Collections.Generic.List<bool>();
            var art = new System.Collections.Generic.List<CandyHudIcon>();
            var captions = new System.Collections.Generic.List<string>();
            var pictures = new System.Collections.Generic.List<Texture>();

            void Add(string name, Action action, bool available = true, CandyHudIcon icon = CandyHudIcon.Key, string caption = "", Texture picture = null)
            { names.Add(name); actions.Add(action); enabled.Add(available); art.Add(icon); captions.Add(caption); pictures.Add(picture); }

            Add("◄ Back to Match", () => { mapPage = false; menuSection = -1; }, icon: CandyHudIcon.Home);

            if (options != null)
            {
                int max = Mathf.Min(12, options.Count - mapOffset);
                for (int i = 0; i < max; i++)
                {
                    int index = mapOffset + i;
                    var row = options[index];
                    Texture preview = row.map != null ? MapPreviewImages.Get(row.map.id) : null;
                    bool canSelect = row.map == null || (DeadlyJenga.Presentation.GameExperiencePanel.CanEditRawChoices(session) && maps.CanSelectMap && row.map.available);
                    string title = row.map == null ? vrMapPicker.Title(row) : (row.map.id == maps.CurrentMapId ? "★ " : "") + row.map.title;
                    Add(title, () => {
                        if (row.map == null) { vrMapPicker.Toggle(row); mapOffset = vrMapPicker.HeaderIndex / 12 * 12; }
                        else maps.SelectMap(row.map.id);
                    }, canSelect, CandyHudIcon.Home, row.map?.description ?? "Browse category", preview);
                }
                if (mapOffset > 0)
                    Add("Previous maps", () => mapOffset = Mathf.Max(0, mapOffset - 12), icon: CandyHudIcon.Home);
                if (mapOffset + 12 < options.Count)
                    Add("More maps", () => mapOffset += 12, icon: CandyHudIcon.Home);
            }

            help.text = "Aim at a map to preview, or trigger to select. Connected players follow the host.";
            RenderOrganizedMenuGrid(names, actions, enabled, art, captions, pictures, pressed);
        }

        void PointOrganizedMenu(Camera camera,bool pressed)
        {
            HideDisasterSliders();scaffoldButton.gameObject.SetActive(false);
            if(disasterPage){PointDisasterPage(camera,pressed,false);return;}
            if(modePage){PointModePage(camera,pressed);return;}
            if(mapPage){PointTabletMapPage(camera,pressed);return;}
            var actions=new System.Collections.Generic.List<Action>();var names=new System.Collections.Generic.List<string>();var enabled=new System.Collections.Generic.List<bool>();var art=new System.Collections.Generic.List<CandyHudIcon>();var captions=new System.Collections.Generic.List<string>();var pictures=new System.Collections.Generic.List<Texture>();
            void Add(string name,Action action,bool available=true,CandyHudIcon icon=CandyHudIcon.Key,string caption="",Texture picture=null)
            {names.Add(name);actions.Add(action);enabled.Add(available);art.Add(icon);captions.Add(caption);pictures.Add(picture);}
            var match=MatchRuntime.Current;
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
                Add("Rebuild Map",()=>session.ResetMap(),session.CanEditWorld,CandyHudIcon.Home,"Rebuild the current map. Host only.",mapPreview);
                Add("Disasters",()=>{disasterPage=true;disasterSelection=-1;},Demo!=null,CandyHudIcon.Flight,"Preview and tune disasters before launch.",disasterPreview);
                Add("Give Yourself a Gun",()=>{quickGunPage=true;giveEquipmentPage=true;giveAllEquipment=false;equipmentOffset=0;},
                    CanCommand&&!session.PlayingHand&&match?.Authority==true&&match is IDemoEquipmentControls,CandyHudIcon.Trigger,
                    "Choose a firearm. Your previous item drops at your feet.",DeadlyJenga.Presentation.CandyEquipmentView.Outline(EquipmentKind.Blaster));
                Add("Bot Population",()=>{batchPage=true;batchTarget=0;},canBatch,CandyHudIcon.Friends,"Preview a large batch before applying.");
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
                        Add(sections[i]+"   /   "+DemoMenuSections.Caption(sections[i]),()=>OpenMenuSection(index),icon:DemoMenuSections.Icon(sections[i]),caption:DemoMenuSections.Caption(sections[i]));
                    }
                    Add("Advanced Settings   " + (matchAdvancedExpanded ? "▲" : "▼"), () => matchAdvancedExpanded = !matchAdvancedExpanded, icon: CandyHudIcon.Settings, caption: "Extra options: ground pickups and automatic powerups");
                    if(matchAdvancedExpanded)
                    {
                        for(int i=4;i<sections.Length;i++)
                        {
                            int index=i;
                            Add(sections[i]+"   /   "+DemoMenuSections.Caption(sections[i]),()=>OpenMenuSection(index),icon:DemoMenuSections.Icon(sections[i]),caption:DemoMenuSections.Caption(sections[i]));
                        }
                    }
                }
                else
                {
                    for(int i=0;i<sections.Length;i++){int index=i;Add(sections[i]+"   /   "+DemoMenuSections.Caption(sections[i]),()=>OpenMenuSection(index),icon:DemoMenuSections.Icon(sections[i]),caption:DemoMenuSections.Caption(sections[i]));}
                }
            }
            else
            {
                string name=DemoMenuSections.Sections[menuCategory][menuSection];heading.text=DemoMenuSections.Tabs[menuCategory]+" / "+name;Add("Back to "+DemoMenuSections.Tabs[menuCategory],()=>menuSection=-1,icon:CandyHudIcon.Home);
                if(name=="Character")
                {
                    Add(session.PlayingHand?"Become Survivor":"Become Flying Hand",SwitchDemoRole,DemoMode,CandyHudIcon.Hand,"Switch your local demo character.");
                    Add("Respawn on top",()=>{session.SetRole(false);session.SetMenu(false);},DemoMode);
                    Add("Third person inspection",()=>CandyThirdPersonInspection.For(session).Begin(),CandyThirdPersonInspection.For(session)?.CanInspect==true);
                    var item=session.Player.gameObject.GetComponent<IEquipmentControls>();var gun=session.Player.gameObject.GetComponent<IBlasterControls>();
                    Add(item?.Equipment.drawn==true?"Holster "+item.Equipment.kind:"Draw "+(item?.Equipment.kind.ToString()??"equipment"),()=>item?.SetEquipped(!item.Equipment.drawn),item?.Equipment.kind>=EquipmentKind.Blaster);
                    var drop=MatchRuntime.Current as IEquipmentDropRuntime;
                    Add("Drop weapon here",()=>drop?.RequestDropEquipment(),drop!=null&&!session.PlayingHand&&item?.Equipment.kind>=EquipmentKind.Blaster,CandyHudIcon.Hand);
                    Add("Dominant hand: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),()=>gun?.SetLeftHanded(!gun.LeftHanded),gun!=null);
                    Add("Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),()=>{if(MatchRuntime.Current?.Authority==true)gun?.SetTestingEnabled(!gun.TestingEnabled);},gun!=null&&CanCommand&&MatchRuntime.Current?.Authority==true);
                }
                if(name=="Survivors"||name=="Giant hands")
                {
                    bool hand=name=="Giant hands";Add("Spawn one",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.AddHand:DemoCommand.AddSurvivor);},CanCommand);
                    if(!hand)Add("Fill to target population",()=>{if(CanCommand)Demo.Run(DemoCommand.FillSurvivors);},CanCommand);
                    Add("Next personality",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.NextHand:DemoCommand.NextSurvivor);},CanCommand);
                    Add("Clear population",()=>{if(CanCommand)Demo.Run(hand?DemoCommand.ClearHands:DemoCommand.ClearSurvivors);},CanCommand);
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
                    var equipment=session.Player.gameObject.GetComponent<IEquipmentControls>();
                    var hand=session.Player.gameObject.GetComponent<IBlasterControls>();
                    Add("Dominant hand: "+(hand?.LeftHanded==true?"LEFT":"RIGHT"),()=>hand?.SetLeftHanded(!hand.LeftHanded),hand!=null);
                    Add(equipment?.Equipment.drawn==true?"Holster "+equipment.Equipment.kind:"Draw "+(equipment?.Equipment.kind.ToString()??"equipment"),()=>equipment?.SetEquipped(!equipment.Equipment.drawn),equipment?.Equipment.kind>=EquipmentKind.Blaster);
                    var drop=MatchRuntime.Current as IEquipmentDropRuntime;
                    Add("Drop weapon here",()=>drop?.RequestDropEquipment(),drop!=null&&!session.PlayingHand&&equipment?.Equipment.kind>=EquipmentKind.Blaster,CandyHudIcon.Hand);
                    Add("Jump: dominant lower button / pinch. Hold to climb.",()=>{},false);Add("Stamina / audio / scenery",()=>tuningPage=true);}
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
            int total = names.Count;
            bool twoCols = total > 6;
            int rowsPerCol = twoCols ? (total + 1) / 2 : total;
            float startY = twoCols ? 180f : Mathf.Min(180f, (total - 1) * 28f);
            float rowHeight = twoCols ? 58f : 54f;
            float btnW = twoCols ? 460f : 560f;
            float btnH = twoCols ? 50f : 48f;

            for(int i=0;i<buttons.Length;i++)
            {
                bool visible=i<names.Count;
                buttons[i].gameObject.SetActive(visible);targets[i].enabled=visible&&enabled[i];
                if(!visible)continue;

                int col = twoCols ? (i < rowsPerCol ? 0 : 1) : 0;
                int row = twoCols ? (i < rowsPerCol ? i : i - rowsPerCol) : i;
                float x = twoCols ? (col == 0 ? -245f : 245f) : 0f;
                float y = startY - row * rowHeight;

                var rect = (RectTransform)buttons[i].transform;
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = new Vector2(btnW, btnH);
                targets[i].size = new Vector3(btnW, btnH, 4);

                labels[i].text=names[i];
                labels[i].rectTransform.sizeDelta = new Vector2(btnW - 65, btnH);
                labels[i].rectTransform.anchoredPosition = new Vector2(25, 0);

                if(sectionGlyphs[i])
                {
                    sectionGlyphs[i].gameObject.SetActive(true);
                    sectionGlyphs[i].texture=CandyHudVisuals.Icon(art[i]);
                    sectionGlyphs[i].rectTransform.anchoredPosition = new Vector2(-btnW * 0.5f + 25, 0);
                }
                SetRowPreview(i,pictures[i]);

                bool hover=targets[i].enabled&&Hit(targets[i],ray);
                buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;
                labels[i].color=hover?CandyHudVisuals.Ink:enabled[i]?Color.white:CandyHudVisuals.Muted;
                if(hover&&!string.IsNullOrEmpty(captions[i]))help.text=captions[i];
                if(hover&&pressed){actions[i]();break;}
            }
        }

        void OpenMenuSection(int index)
        {
            menuSection=index;string name=DemoMenuSections.Sections[menuCategory][index];equipmentOffset=0;
            if(name=="Equipment"||name=="Bot equipment"){giveAllEquipment=name=="Bot equipment";giveEquipmentPage=true;}
            if(name=="Spectate")spectatePage=true;if(name=="Maps")mapPage=true;if(name=="Modes")modePage=modeWheel=true;if(name=="Disasters")disasterPage=true;
            if(name=="Settings"||name=="Voice")comfortPage=true;if(name=="Friends")networkPage=true;if(name=="Powerup spawning")randomPowerupPage=true;
        }
    }
}
