using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CandyDemoHud
    {
        int subsection,categoryCursor;bool sectionPicker;int arsenalFilter;
        string SectionName=>DemoMenuSections.Sections[Mathf.Clamp(page,0,3)][Mathf.Clamp(subsection,0,DemoMenuSections.Sections[Mathf.Clamp(page,0,3)].Length-1)];
        float SectionHeight=>SectionName=="Modes"?6000:SectionName=="Maps"?MapContentHeight():SectionName=="Disasters"?DisasterContentHeight():SectionName=="Equipment"||SectionName=="Bot equipment"||SectionName=="Ground pickups"?800+Enum.GetValues(typeof(EquipmentKind)).Length*104:SectionName=="Settings"?2300:SectionName=="Voice"?1450:1200;
        void DrawSectionSelector()
        {
            var sections=DemoMenuSections.Sections[page];
            if(Button(new Rect(278,128,884,44),"Category: "+SectionName+"   [v]",CandyHudVisuals.Lavender)){sectionPicker=!sectionPicker;categoryCursor=subsection;}
            // The dropdown is drawn after the content by DrawSectionDropdown.
        }
        void DrawSectionDropdown()
        {
            if(!sectionPicker)return;var sections=DemoMenuSections.Sections[page];
            Panel(new Rect(278,175,884,sections.Length*45+10),CandyHudVisuals.Ink);
            for(int i=0;i<sections.Length;i++)if(Button(new Rect(284,180+i*45,872,42),sections[i],i==categoryCursor?CandyHudVisuals.Mint:CandyHudVisuals.Card))
            {subsection=i;sectionPicker=false;selection=0;scroll=Vector2.zero;giveEquipmentPage=false;giveAllEquipment=sections[i]=="Bot equipment";}
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
            ActionCard(119,CandyHudIcon.Hand,session.PlayingHand?"Become a survivor":"Become a giant hand",DemoMode?"Switch your local character; your mode role is restored when you return.":"The host controls development character changes during a match.",()=>{session.SetRole(!session.PlayingHand);session.SetMenu(false);},DemoMode);
            ActionCard(223,CandyHudIcon.Home,"Return to the top","Place your survivor on the highest safe support.",()=>{session.SetRole(false);session.SetMenu(false);},DemoMode);
            var inspect=CandyThirdPersonInspection.For(session);
            ActionCard(327,CandyHudIcon.Survivor,"Third person inspection","Stand still; orbit and zoom around your character.",()=>inspect.Begin(),inspect.CanInspect);
            var gun=session.Player.gameObject.GetComponent<IBlasterControls>();var item=session.Player.gameObject.GetComponent<IEquipmentControls>();
            ActionCard(431,CandyHudIcon.Hand,item?.Equipment.drawn==true?"Holster weapon":"Draw weapon","V or controller trigger at its holster.",()=>item.SetEquipped(!item.Equipment.drawn),item?.Equipment.kind>=EquipmentKind.Blaster);
            ActionCard(535,CandyHudIcon.Settings,"Dominant hand: "+(gun?.LeftHanded==true?"LEFT":"RIGHT"),"Changes the weapon hand.",()=>gun.SetLeftHanded(!gun.LeftHanded),gun!=null);
            ActionCard(639,CandyHudIcon.Play,"Blaster testing: "+(gun?.TestingEnabled==true?"ON":"OFF"),"Legacy alien-blaster test toggle.",()=>gun.SetTestingEnabled(!gun.TestingEnabled),gun!=null&&MatchRuntime.Current?.Authority==true&&DemoMode);
        }
        void WorldPage()
        {
            ActionCard(119,CandyHudIcon.Home,"Reset / rebuild map","Rebuild the current map and reposition Demo actors.",()=>{session.ResetMap();session.SetMenu(false);},EditWorld,CandyHudVisuals.Peach);
            if(Demo is IBuildUpControls build&&build.Available)ActionCard(223,CandyHudIcon.Play,"Deliver next scaffold tier",build.Delivering?"Delivery in progress":"Advance the staged scaffold map.",()=>build.AdvanceStage(),build.CanAdvance);
            var review=MatchRuntime.Current as IMatchReviewControls;
            ActionCard(327,CandyHudIcon.Play,"End game","Finish the development round and show results.",()=>review.EndGame(),review?.CanEndGame==true);
        }
        void EnemyPage()
        {
            ActionCard(431,CandyHudIcon.Survivor,"Island wildlife: twelve animals","Dot-eye animals. Spawn wild roamers or following companions. Local testing; F6 also opens this panel.",()=>DeadlyJenga.Presentation.CandyWildlifePanel.Open(session));
            var enemies=session.SurvivorBots as IEnemyPopulation;
            ActionCard(535,CandyHudIcon.Survivor,"Spawn Longhand","Long orange fingers, extended reach and a readable windup.",()=>enemies.SpawnEnemy(new EnemySpawnSettings{archetypeId="longhand",hitDamage=25}),enemies!=null&&CanCommand);
            ActionCard(639,CandyHudIcon.Survivor,"Spawn Gum Brute","Slow lime-green bruiser with heavy teal fists and a powerful shove.",()=>enemies.SpawnEnemy(new EnemySpawnSettings{archetypeId="gum-brute",hitDamage=40}),enemies!=null&&CanCommand);
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
