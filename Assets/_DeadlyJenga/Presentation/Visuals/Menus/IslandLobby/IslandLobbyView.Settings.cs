using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        sealed class SetupSetting
        {
            public string label;
            public bool teamOnly, whole;
            public string unit, zeroLabel;
            public Func<LobbySetup,object> read;
            public Action<LobbySetup,object> write;
            public Type type;
            public float min,max,increment;
            public string[] options;
            public bool Numeric=>options==null&&(type==typeof(float)||type==typeof(int));
        }
        const int SettingsPageSize=6;
        readonly List<SetupSetting> matchSettings=new List<SetupSetting>();
        readonly List<(string title, SetupSetting[] rows)> devSettings=new List<(string, SetupSetting[])>();
        Text devSettingsSection,devSettingsPageLabel;
        readonly Text[] settingNames=new Text[6],settingNumbers=new Text[6],settingChoices=new Text[6];
        int settingsPage;
        bool DevSettingsOpen=>setupPage=="dev-settings"&&lobby.CanConfigure;
        bool SettingsOpen=>setupPage=="settings"||DevSettingsOpen;
        string SettingsHeading=>DevSettingsOpen?"Dev Settings":"Match Rules";

        SetupSetting Setting(int slot)
        {
            if(DevSettingsOpen)
            {
                var rows=devSettings[settingsPage].rows;
                return slot<rows.Length?rows[slot]:null;
            }
            bool teamMode=Details?.SelectedModeId is "blockcontrol" or "ctf" or "teamtickets";
            foreach(var setting in matchSettings)
                if((!setting.teamOnly||teamMode)&&slot--==0)return setting;
            return null;
        }

        void OpenMatchSettings()
        {
            setupPage="settings";
            settingsPage=0;
        }

        static SetupSetting Field<T>(Func<LobbySetup,T> owner,string name,string label=null)
        {
            var field=typeof(T).GetField(name,BindingFlags.Public|BindingFlags.Instance);
            var setting=new SetupSetting
            {
                label=label??ModeSetupPanel.Label(name),
                read=state=>field.GetValue(owner(state)),
                write=(state,value)=>field.SetValue(owner(state),value),
                type=field.FieldType
            };
            ModeSetupPanel.Range(name,out setting.min,out setting.max);
            return setting;
        }

        void AddDevPages(string title,SetupSetting[] settings)
        {
            for(int start=0;start<settings.Length;start+=SettingsPageSize)
                devSettings.Add((title,settings.Skip(start).Take(SettingsPageSize).ToArray()));
        }

        void BuildSetupSettings()
        {
            SetupSetting Lobby(string name,string label=null)=>Field(s=>s,name,label);
            SetupSetting Rule(string name,string label=null)=>Field(s=>s.rules,name,label);
            void Rules(string title,params string[] names)=>AddDevPages(title,names.Select(name=>Rule(name)).ToArray());

            var population=Lobby(nameof(LobbySetup.population),"Total players");
            population.min=1;
            population.max=100;
            var hands=Lobby(nameof(LobbySetup.hands),"AI hands");
            hands.max=100;
            var round=Rule(nameof(ModeRules.roundSeconds),"Round length");
            round.unit="s";
            round.zeroLabel="No limit";
            round.increment=10;
            var teams=Rule(nameof(ModeRules.teamCount),"Teams");
            var friendlyFire=Rule(nameof(ModeRules.friendlyFire));
            var sameTeam=Lobby(nameof(LobbySetup.sameHumanTeam),"Keep humans on one team");
            teams.teamOnly=friendlyFire.teamOnly=sameTeam.teamOnly=true;
            matchSettings.AddRange(new[]{population,hands,round,teams,friendlyFire,sameTeam});

            var survivorPreset=Lobby(nameof(LobbySetup.survivorPreset),"Survivor personality");
            survivorPreset.options=MatchOptions.SurvivorPersonalities();
            var handPreset=Lobby(nameof(LobbySetup.handPreset),"Hand personality");
            handPreset.options=MatchOptions.HandPersonalities();
            AddDevPages("Bots",new[]{survivorPreset,handPreset}.Where(setting=>setting.options?.Length>0).ToArray());

            Rules("Combat",nameof(ModeRules.punchEnabled),nameof(ModeRules.punchDistance),nameof(ModeRules.punchDamage),
                nameof(ModeRules.punchCooldown),nameof(ModeRules.healthEnabled),nameof(ModeRules.deathEnabled),
                nameof(ModeRules.fallDamage),nameof(ModeRules.fallDamageStart),nameof(ModeRules.fallDamagePerMeter),nameof(ModeRules.defaultDagger));
            Rules("Round flow",nameof(ModeRules.countdownSeconds),nameof(ModeRules.victorySeconds),nameof(ModeRules.handPowerSeconds));
            Rules("Island",nameof(ModeRules.island),nameof(ModeRules.blockDestruction),nameof(ModeRules.sinkGraceSeconds),
                nameof(ModeRules.sinkSpeed),nameof(ModeRules.sinkMoveScale));
            Rules("King of the Hill",nameof(ModeRules.hillRadius),nameof(ModeRules.hillInterval));
            Rules("Color Control",nameof(ModeRules.claimSeconds),nameof(ModeRules.reclaimSeconds),nameof(ModeRules.claimDecaySeconds),
                nameof(ModeRules.pointsPerBlockSecond),nameof(ModeRules.enemyClimbBlocked));
            Rules("Other modes",nameof(ModeRules.flagTarget),nameof(ModeRules.flagWinByTwo),nameof(ModeRules.tickets),nameof(ModeRules.starterInfected));

            string[] mysteryFields={nameof(MysterySettings.attackGraceSeconds),nameof(MysterySettings.reviveSeconds),
                nameof(MysterySettings.interactDistance),nameof(MysterySettings.reportDelaySeconds),nameof(MysterySettings.suspicionSeconds),
                nameof(MysterySettings.trailRadius),nameof(MysterySettings.firstDisasterSeconds),nameof(MysterySettings.vetoSeconds),
                nameof(MysterySettings.chooseSeconds),nameof(MysterySettings.disasterMinSeconds),nameof(MysterySettings.disasterMaxSeconds),
                nameof(MysterySettings.sheriffMistakes)};
            AddDevPages("Murder Mystery",mysteryFields.Select(name=>
            {
                var setting=Field(s=>s.rules.mystery,name);
                if(name==nameof(MysterySettings.sheriffMistakes)){setting.min=1;setting.max=10;}
                else if(name==nameof(MysterySettings.interactDistance))setting.max=5;
                else if(name==nameof(MysterySettings.trailRadius))setting.max=15;
                else setting.max=180;
                return setting;
            }).ToArray());

            Rules("Disaster timing",nameof(ModeRules.automaticDisasters),nameof(ModeRules.disasterMinInterval),
                nameof(ModeRules.disasterMaxInterval),nameof(ModeRules.disasterLateInterval),nameof(ModeRules.disasterWarningSeconds),
                nameof(ModeRules.maxConcurrentDisasters));

            var hazards=Resources.Load<HazardCatalog>("DisasterCatalog");
            if(hazards)foreach(var prefab in hazards.prefabs)
            {
                if(!prefab||prefab.GetComponent<IDemoHazard>() is not IDemoHazard hazard)continue;
                var rows=new SetupSetting[hazard.Parameters.Count];
                for(int i=0;i<rows.Length;i++)
                {
                    int parameter=i;
                    string id=prefab.name;
                    var p=hazard.Parameters[i];
                    rows[i]=new SetupSetting
                    {
                        label=p.label,type=typeof(float),min=p.min,max=p.max,whole=p.whole,unit=p.unit,
                        read=s=>s.disasters.FirstOrDefault(h=>h.id==id)?.values[parameter]??p.initial,
                        write=(s,v)=>{var h=s.disasters.FirstOrDefault(h=>h.id==id);if(h!=null)h.values[parameter]=p.Clamp((float)v);}
                    };
                }
                AddDevPages(prefab.GetComponent<IHazard>()?.Type?.name??prefab.name,rows);
            }

            for(int i=0;i<6;i++)
            {
                int slot=i;float y=225-i*84;
                Add(setupStation,"setting-value-"+i,()=>settingChoices[slot]?.text??"",y,()=>Adjust(slot,1),()=>lobby.CanConfigure,
                    270,460,62,()=>SettingVisible(slot)&&!Setting(slot).Numeric);
                Add(setupStation,"setting-minus-"+i,()=>"−",y,()=>Adjust(slot,-1),()=>lobby.CanConfigure,
                    100,90,62,()=>SettingVisible(slot)&&Setting(slot).Numeric);
                Add(setupStation,"setting-plus-"+i,()=>"+",y,()=>Adjust(slot,1),()=>lobby.CanConfigure,
                    440,90,62,()=>SettingVisible(slot)&&Setting(slot).Numeric);
            }
            Add(setupStation,"settings-dev",()=>"Dev Settings",-360,()=>
            {
                if(setupPage!="settings"||!lobby.CanConfigure)return;
                settingsPage=0;
                setupPage="dev-settings";
            },()=>lobby.CanConfigure,0,270,48,()=>setupPage=="settings"&&lobby.CanConfigure);
            Add(setupStation,"settings-prev",()=>"Previous",-290,()=>settingsPage--,()=>settingsPage>0,
                -310,300,50,()=>DevSettingsOpen);
            Add(setupStation,"settings-next",()=>"Next",-290,()=>settingsPage++,()=>settingsPage+1<devSettings.Count,
                310,300,50,()=>DevSettingsOpen);
        }

        bool SettingVisible(int slot)=>SettingsOpen&&Setting(slot)!=null;
        void BuildSettingRows()
        {
            devSettingsSection=CandyReadout(candySetupContent,"Dev settings section","",0,285,900,42,30);
            devSettingsSection.color=CandyMuted;
            devSettingsPageLabel=CandyReadout(candySetupContent,"Dev settings page","",0,-290,200,50,24);
            devSettingsPageLabel.color=CandyMuted;
            CandyPlace("settings-dev",0,-360,270,48);
            CandyPlace("settings-prev",-310,-290,300,50);
            CandyPlace("settings-next",310,-290,300,50);
            for(int i=0;i<6;i++)
            {
                float y=225-i*84;
                settingNames[i]=CandyReadout(candySetupContent,"Setting name","",-255,y,490,70,28);
                settingNames[i].alignment=TextAnchor.MiddleLeft;
                var number=CandyPanel(candySetupContent,"Numeric setting",270,y,216,62,CandyInk);
                settingNumbers[i]=CandyReadout(number,"Setting number","",0,0,196,54,30);
                var choice=CandyPlace("setting-value-"+i,270,y,430,62);
                CandyText(choice,0,0,402,54,27);
                settingChoices[i]=choice.label;
                CandyPlace("setting-minus-"+i,100,y,88,62);
                CandyPlace("setting-plus-"+i,440,y,88,62);
            }
        }
        void UpdateSettingRows()
        {
            if(setupPage=="dev-settings"&&!lobby.CanConfigure)OpenMatchSettings();
            bool dev=DevSettingsOpen;
            devSettingsSection.gameObject.SetActive(dev);
            devSettingsPageLabel.gameObject.SetActive(dev);
            if(dev)
            {
                devSettingsSection.text=devSettings[settingsPage].title;
                devSettingsPageLabel.text=$"{settingsPage+1} / {devSettings.Count}";
            }
            var state=SettingsOpen?Details?.Setup:null;
            for(int i=0;i<settingNames.Length;i++)
            {
                var setting=state!=null?Setting(i):null;
                settingNames[i].gameObject.SetActive(setting!=null);
                settingNumbers[i].transform.parent.gameObject.SetActive(setting?.Numeric==true);
                if(setting==null)continue;
                settingNames[i].text=setting.label;
                var value=setting.read(state);
                if(value is int index&&setting.options?.Length>0)
                    value=setting.options[Mathf.Clamp(index,0,setting.options.Length-1)];
                string display=value is float number?number.ToString("0.##"):value is bool flag?flag?"On":"Off":value?.ToString();
                if(setting.Numeric)
                {
                    if(setting.zeroLabel!=null&&value is float seconds&&seconds==0)display=setting.zeroLabel;
                    else if(!string.IsNullOrEmpty(setting.unit))display+=" "+setting.unit;
                    settingNumbers[i].text=display;
                }
                else settingChoices[i].text=display;
            }
        }
        void Adjust(int slot,int step)
        {
            if(!SettingsOpen||!lobby.CanConfigure)return;
            var entry=Setting(slot);var state=Details?.Setup;
            if(entry==null||state==null)return;
            object value=entry.read(state),previous=value;
            if(entry.type==typeof(bool))value=!(bool)value;
            else if(entry.type.IsEnum){var all=Enum.GetValues(entry.type);value=all.GetValue((Array.IndexOf(all,value)+step+all.Length)%all.Length);}
            else if(entry.type==typeof(int)&&entry.options?.Length>0)
                value=(Mathf.Clamp((int)value,0,entry.options.Length-1)+step+entry.options.Length)%entry.options.Length;
            else
            {
                float amount=entry.increment;
                if(amount<=0)amount=entry.type==typeof(int)||entry.whole?1:entry.max>100?5:entry.max>20?1:.1f;
                float next=Mathf.Clamp(Convert.ToSingle(value)+step*amount,entry.min,entry.max);
                value=entry.type==typeof(int)?(object)Mathf.RoundToInt(next):next;
            }
            if(Equals(previous,value))return;
            entry.write(state,value);Details.ConfigureSetup(state);
        }
    }
}
