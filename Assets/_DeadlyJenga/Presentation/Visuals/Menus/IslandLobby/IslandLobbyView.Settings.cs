using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        sealed class SetupSetting
        {
            public string label,id;
            public bool mysteryOnly;
            public Func<LobbySetup,object> read;
            public Action<LobbySetup,object> write;
            public Type type;
            public float min,max;
            public string[] options;
            public bool Numeric=>options==null&&(type==typeof(float)||type==typeof(int));
        }
        readonly List<SetupSetting> setupSettings=new List<SetupSetting>();
        readonly Text[] settingNames=new Text[6],settingNumbers=new Text[6],settingChoices=new Text[6];
        int settingsPage;
        bool ShowSetting(SetupSetting setting)=>!setting.mysteryOnly||Details?.SelectedModeId=="mystery";
        SetupSetting Setting(int slot)
        {
            int index=settingsPage*6+slot;
            foreach(var setting in setupSettings)
                if(ShowSetting(setting)&&index--==0)return setting;
            return null;
        }
        void BuildSetupSettings()
        {
            void Fields(Type type,Func<LobbySetup,object> owner,bool movement=false)
            {
                foreach(var f in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
                {
                    if(f.Name=="downed"||f.FieldType!=typeof(float)&&f.FieldType!=typeof(int)&&f.FieldType!=typeof(bool)&&f.FieldType!=typeof(string)&&!f.FieldType.IsEnum)continue;
                    if(f.Name=="mode"||f.Name=="carryingFlag"||f.Name=="sinking"||f.Name=="sinkProgress"||f.Name=="disasters"||f.Name=="rules"||f.FieldType==typeof(MatchRules)||f.Name=="playStyle")continue;
                    var entry=new SetupSetting{id=f.Name,mysteryOnly=type==typeof(MysterySettings),label=(movement?"Player / ":"")+ModeSetupPanel.Label(f.Name),read=s=>f.GetValue(owner(s)),write=(s,v)=>f.SetValue(owner(s),v),type=f.FieldType};
                    ModeSetupPanel.Range(f.Name,out entry.min,out entry.max);
                    if(f.Name=="population"){entry.min=1;entry.max=100;}if(f.Name=="hands"){entry.min=0;entry.max=100;}
                    if(f.Name=="survivorPreset")entry.options=MatchOptions.SurvivorPersonalities();if(f.Name=="handPreset")entry.options=MatchOptions.HandPersonalities();
                    if(entry.options!=null){entry.min=0;entry.max=entry.options.Length-1;}
                    if(f.Name=="environmentSkin")entry.options=CandySceneSkins.Names;
                    if(f.Name.EndsWith("Curve",StringComparison.Ordinal))entry.options=MatchOptions.Curves;
                    if(f.Name=="turn")entry.options=MatchOptions.Turns;if(movement&&f.Name=="movement")entry.options=MatchOptions.Movements;
                    if(movement&&f.FieldType==typeof(float)){entry.min=f.Name=="gripSeconds"?.5f:0;entry.max=f.Name=="snapAngle"?90:f.Name=="smoothDegrees"?180:f.Name.Contains("Degree")?6:f.Name=="gripSeconds"?20:f.Name=="recoveryDelay"?5:4;}
                    if(entry.type!=typeof(string)||entry.options?.Length>0)setupSettings.Add(entry);
                }
            }
            Fields(typeof(LobbySetup),s=>s);Fields(typeof(ModeRules),s=>s.rules);Fields(typeof(MatchRules),s=>s.movement,true);
            Fields(typeof(MysterySettings),s=>s.rules.mystery);
            string[] common={"population","hands","survivorPreset","healthEnabled","friendlyFire","punchEnabled"};
            var ordered=setupSettings.OrderBy(setting=>
            {
                int index=Array.IndexOf(common,setting.id);
                return index>=0?index:common.Length;
            }).ToArray();
            setupSettings.Clear();setupSettings.AddRange(ordered);
            var hazards=Resources.Load<HazardCatalog>("DisasterCatalog");
            if(hazards)foreach(var prefab in hazards.prefabs)
            {
                var hazard=prefab.GetComponent<IDemoHazard>();if(hazard==null)continue;
                for(int i=0;i<hazard.Parameters.Count;i++)
                {
                    int parameter=i;string id=prefab.name;var p=hazard.Parameters[i];
                    setupSettings.Add(new SetupSetting{label=(prefab.GetComponent<IHazard>()?.Type?.name??id)+" / "+p.label,type=typeof(float),min=p.min,max=p.max,read=s=>s.disasters.FirstOrDefault(h=>h.id==id)?.values[parameter]??p.initial,write=(s,v)=>{var h=s.disasters.FirstOrDefault(h=>h.id==id);if(h!=null)h.values[parameter]=p.Clamp((float)v);}});
                }
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
            Add(setupStation,"settings-prev",()=>"Previous",-395,()=>settingsPage=Mathf.Max(0,settingsPage-1),()=>true,-220,390,55,()=>setupPage=="settings"&&settingsPage>0);
            Add(setupStation,"settings-next",()=>"Next",-395,()=>settingsPage++,()=>true,220,390,55,()=>setupPage=="settings"&&(settingsPage+1)*6<setupSettings.Count(ShowSetting));
        }
        bool SettingVisible(int slot)=>setupPage=="settings"&&Setting(slot)!=null;
        void BuildSettingRows()
        {
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
            var state=setupPage=="settings"?Details?.Setup:null;
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
                if(setting.Numeric)settingNumbers[i].text=display;
                else settingChoices[i].text=display;
            }
        }
        void Adjust(int slot,int step)
        {
            var entry=Setting(slot);if(entry==null||!lobby.CanConfigure)return;var state=Details.Setup;object value=entry.read(state),previous=value;
            if(entry.type==typeof(bool))value=!(bool)value;
            else if(entry.type.IsEnum){var all=Enum.GetValues(entry.type);value=all.GetValue((Array.IndexOf(all,value)+step+all.Length)%all.Length);}
            else if(entry.type==typeof(string)){var all=entry.options;if(all==null||all.Length==0)return;value=all[(Array.IndexOf(all,(string)value)+step+all.Length)%all.Length];}
            else if(entry.type==typeof(int)&&entry.options?.Length>0)
                value=(Mathf.Clamp((int)value,0,entry.options.Length-1)+step+entry.options.Length)%entry.options.Length;
            else{float amount=entry.type==typeof(int)?1:entry.max>100?5:entry.max>20?1:.1f;float next=Mathf.Clamp(Convert.ToSingle(value)+step*amount,entry.min,entry.max);value=entry.type==typeof(int)?(object)Mathf.RoundToInt(next):next;}
            if(Equals(previous,value))return;
            entry.write(state,value);Details.ConfigureSetup(state);
        }
    }
}
