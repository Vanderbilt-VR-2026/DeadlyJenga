using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CandyDemoHud
    {
        int subsection,arsenalFilter;
        bool tameWildlife;
        IWildlifeControls Wildlife=>(session as Component)?.GetComponent<IWildlifeControls>();
        string SectionName=>DemoMenuSections.Sections[Mathf.Clamp(page,0,3)][Mathf.Clamp(subsection,0,DemoMenuSections.Sections[Mathf.Clamp(page,0,3)].Length-1)];
        float SectionHeight=>SectionName=="World"&&Wildlife!=null?800+Wildlife.Species.Count*104:
            SectionName=="Modes"?6000:SectionName=="Maps"?MapContentHeight():SectionName=="Disasters"?DisasterContentHeight():SectionName=="Equipment"||SectionName=="Bot equipment"||SectionName=="Ground pickups"?800+Enum.GetValues(typeof(EquipmentKind)).Length*104:SectionName=="Settings"?2300:SectionName=="Voice"?1450:1200;
        void SelectSection(int index)
        {
            subsection=index;selection=0;scroll=Vector2.zero;giveEquipmentPage=false;activate=false;
        }

        void DrawSectionSelector()
        {
            var sections=DemoMenuSections.Sections[page];
            for(int i=0;i<sections.Length;i++)
            {
                string name=sections[i];
                var rect=new Rect(278+i%3*296,122+i/3*59,286,53);
                bool selected=i==subsection;
                if(Button(rect,"",selected?new Color(.40f,.25f,.61f):CandyHudVisuals.Panel))SelectSection(i);
                if(selected)Panel(new Rect(rect.x+4,rect.y+9,3,35),CandyHudVisuals.Mint);
                Icon(new Rect(rect.x+12,rect.y+11,30,30),DemoMenuSections.Icon(name),selected?CandyHudVisuals.Mint:CandyHudVisuals.Lavender);
                GUI.Label(new Rect(rect.x+50,rect.y+1,226,27),name,heading);
                GUI.Label(new Rect(rect.x+50,rect.y+27,226,24),DemoMenuSections.Caption(name),small);
            }
        }

        readonly System.Collections.Generic.Dictionary<string,Texture2D> sectionPictures=new();
        Texture2D SectionPicture()
        {
            if(SectionName=="Maps")return MapPreviewImages.Get((Demo as IDemoMapControls)?.CurrentMapId);
            string id=SectionName switch
            {
                "Modes"=>"ctf","Disasters"=>"survival","Enemies"=>"infection",
                "Survivors"=>"teamtickets","Spectate"=>"hill",_=>null
            };
            if(id==null)return null;
            if(!sectionPictures.TryGetValue(id,out var picture))
                sectionPictures[id]=picture=Resources.Load<Texture2D>("LobbyModePictures/"+id);
            return picture;
        }
        void DrawSection()
        {
            switch(SectionName)
            {
                case "Character":PersonalPage();break;
                case "Equipment":giveAllEquipment=false;GiveEquipmentPage();break;
                case "Bot equipment":giveAllEquipment=true;GiveEquipmentPage();break;
                case "Spectate":SpectatePage();break;case "Settings":SettingsPage();break;case "Controls":ControlsPage();break;
                case "Survivors":BotsPage();break;case "Giant hands":HandsPage();break;case "Voice":VoicePage();break;
                case "Friends":FriendsPage();break;case "Modes":ModesPage();break;case "Maps":MapsPage();break;case "Disasters":DisastersPage();break;
                case "Enemies":EnemyPage();break;case "World":WorldPage();break;case "Ground pickups":GroundPickupsPage();break;
                case "Powerup spawning":ActionCard(119,CandyHudIcon.Settings,"Random block powerup settings","Interval, limit, expiry and weighted item types.",()=>DeadlyJenga.Presentation.CandyPowerupSpawnPanel.Open(),MatchRuntime.Current is IPowerupSpawnControls);break;
            }
        }
        void PersonalPage()
        {
            ActionCard(119,CandyHudIcon.Hand,session.PlayingHand?"Become Survivor":"Become Flying Hand",DemoMode?"Switch your local character; your mode role is restored when you return.":"The host controls development character changes during a match.",()=>{session.SetRole(!session.PlayingHand);session.SetMenu(false);},DemoMode);
            ActionCard(223,CandyHudIcon.Home,"Return to the top","Place your survivor on the highest safe support.",()=>{session.SetRole(false);session.SetMenu(false);},DemoMode);
            var inspect=CandyThirdPersonInspection.For(session);
            ActionCard(327,CandyHudIcon.Survivor,"Third person inspection","Stand still; orbit and zoom around your character.",()=>inspect.Begin(),inspect.CanInspect);
            var gun=session.Player.gameObject.GetComponent<IBlasterControls>();var item=session.Player.gameObject.GetComponent<IEquipmentControls>();
            ActionCard(431,CandyHudIcon.Hand,item?.Equipment.drawn==true?"Holster weapon":"Draw weapon","V or dominant controller upper face button.",()=>item.SetEquipped(!item.Equipment.drawn),item?.Equipment.kind>=EquipmentKind.Blaster);
            ActionCard(535,CandyHudIcon.Play,"Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),"Legacy alien-blaster test toggle.",()=>gun.SetTestingEnabled(!gun.TestingEnabled),gun!=null&&MatchRuntime.Current?.Authority==true&&DemoMode);
            var drop=MatchRuntime.Current as IEquipmentDropRuntime;
            ActionCard(639,CandyHudIcon.Hand,"Drop weapon here","Leaves your carried item for someone else. Keyboard: Delete. No XR drop chord.",()=>drop.RequestDropEquipment(),drop!=null&&!session.PlayingHand&&item?.Equipment.kind>=EquipmentKind.Blaster);
        }
        void WorldPage()
        {
            ActionCard(119,CandyHudIcon.Home,"Reset / rebuild map","Rebuild the current map and reposition Demo actors.",()=>{session.ResetMap();session.SetMenu(false);},EditWorld,CandyHudVisuals.Peach);
            if(Demo is IBuildUpControls build&&build.Available)ActionCard(223,CandyHudIcon.Play,"Deliver next scaffold tier",build.Delivering?"Delivery in progress":"Advance the staged scaffold map.",()=>build.AdvanceStage(),build.CanAdvance);
            var review=MatchRuntime.Current as IMatchReviewControls;
            if(review?.DevelopmentReviewEnabled==true)
                ActionCard(327,CandyHudIcon.Play,MultiplayerPanel.EndAction(session.Multiplayer),"Show results for the current round.",()=>review.EndGame(),review.CanEndGame);
            var wildlife=Wildlife;
            if(wildlife==null)return;
            GUI.Label(new Rect(8,430,840,36),"Island wildlife",heading);
            bool available=wildlife.Available;
            ActionCard(474,CandyHudIcon.Survivor,tameWildlife?"Companion":"Wild roaming",wildlife.Status,()=>tameWildlife=!tameWildlife,available);
            ActionCard(578,CandyHudIcon.Play,"Spawn all","",()=>{foreach(var species in wildlife.Species)wildlife.Spawn(species,tameWildlife);},available);
            ActionCard(682,CandyHudIcon.Home,"Clear wildlife","",wildlife.Clear,available);
            int y=786;
            foreach(string species in wildlife.Species)
            {
                ActionCard(y,CandyHudIcon.Survivor,"Spawn "+species,"",()=>wildlife.Spawn(species,tameWildlife),available);
                y+=104;
            }
        }
        void EnemyPage()
        {
            var enemies=session.SurvivorBots as IEnemyPopulation;
            ActionCard(431,CandyHudIcon.Survivor,"Spawn Longhand","Long orange fingers, extended reach and a readable windup.",()=>enemies.SpawnEnemy(new EnemySpawnSettings{archetypeId="longhand",hitDamage=25}),enemies!=null&&CanCommand);
            ActionCard(535,CandyHudIcon.Survivor,"Spawn Gum Brute","Slow lime-green bruiser with heavy teal fists and a powerful shove.",()=>enemies.SpawnEnemy(new EnemySpawnSettings{archetypeId="gum-brute",hitDamage=40}),enemies!=null&&CanCommand);
            ActionCard(119,CandyHudIcon.Survivor,"Spawn jaguar enemy","Perimeter spawn with reduced hit damage.",()=>enemies.SpawnEnemy(new EnemySpawnSettings()),enemies!=null&&CanCommand);
            ActionCard(222,CandyHudIcon.Survivor,"Spawn mushroom enemy","Short, high-jumping spore attacker; no climbing.",()=>enemies.SpawnEnemy(new EnemySpawnSettings{archetypeId="mushroom",hitDamage=10}),enemies!=null&&CanCommand);
            ActionCard(327,CandyHudIcon.Home,"Clear enemy NPCs","Remove enemy NPCs while keeping survivor bots.",()=>enemies.ClearEnemies(),enemies!=null&&CanCommand);
        }
        void GroundPickupsPage()
        {
            int row=0;foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
            {if(kind==EquipmentKind.None)continue;var item=kind;ActionCard(119+row++*104,CandyHudIcon.Play,"Spawn "+DeadlyJenga.Presentation.CandyEquipmentView.Name(kind),"Place one collectible ahead of your survivor.",()=>EquipmentAction(()=>(MatchRuntime.Current as IEquipmentRuntime)?.SpawnEquipment(item)==true),DemoMode&&CanCommand&&!session.PlayingHand);}
        }
    }
}
