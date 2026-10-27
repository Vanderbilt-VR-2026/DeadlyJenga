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
            public string label;
            public Func<LobbySetup,object> read;
            public Action<LobbySetup,object> write;
            public Type type;
            public float min,max;
            public string[] options;
        }
        readonly List<SetupSetting> setupSettings=new List<SetupSetting>();
        readonly List<Image> setupFills=new List<Image>();
        int settingsPage;float nextSettingSend;
        SetupSetting Setting(int slot){int index=settingsPage*6+slot;return index<setupSettings.Count?setupSettings[index]:null;}
        void BuildSetupSettings()
        {
            void Fields(Type type,Func<LobbySetup,object> owner,bool movement=false)
            {
                foreach(var f in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
                {
                    if(f.Name=="downed"||f.FieldType!=typeof(float)&&f.FieldType!=typeof(int)&&f.FieldType!=typeof(bool)&&f.FieldType!=typeof(string)&&!f.FieldType.IsEnum)continue;
                    if(f.Name=="mode"||f.Name=="carryingFlag"||f.Name=="sinking"||f.Name=="sinkProgress"||f.Name=="disasters"||f.Name=="rules"||f.FieldType==typeof(MatchRules)||f.Name=="playStyle")continue;
                    var entry=new SetupSetting{label=(movement?"Player / ":"")+ModeSetupPanel.Label(f.Name),read=s=>f.GetValue(owner(s)),write=(s,v)=>f.SetValue(owner(s),v),type=f.FieldType};
                    ModeSetupPanel.Range(f.Name,out entry.min,out entry.max);
                    if(f.Name=="population"){entry.min=1;entry.max=100;}if(f.Name=="hands"){entry.min=0;entry.max=100;}
                    if(f.Name=="survivorPreset")entry.options=MatchOptions.SurvivorPersonalities();if(f.Name=="handPreset")entry.options=MatchOptions.HandPersonalities();
                    if(entry.options!=null){entry.min=0;entry.max=entry.options.Length-1;}
                    if(f.Name=="environmentSkin")entry.options=CandySceneSkins.Names;
                    if(f.Name.EndsWith("Curve",StringComparison.Ordinal))entry.options=MatchOptions.Curves;
                    if(f.Name=="turn")entry.options=MatchOptions.Turns;if(f.FieldType==typeof(MatchRules))entry.options=MatchOptions.Movements;
                    if(movement&&f.FieldType==typeof(float)){entry.min=f.Name=="gripSeconds"?.5f:0;entry.max=f.Name=="snapAngle"?90:f.Name=="smoothDegrees"?180:f.Name.Contains("Degree")?6:f.Name=="gripSeconds"?20:f.Name=="recoveryDelay"?5:4;}
                    setupSettings.Add(entry);
                }
            }
            Fields(typeof(LobbySetup),s=>s);Fields(typeof(ModeRules),s=>s.rules);Fields(typeof(MatchRules),s=>s.movement,true);
            Fields(typeof(MysterySettings),s=>s.rules.mystery);
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
                int slot=i;float y=310-i*115;
                var choice=Add(setupStation,"setting-value-"+i,()=>SettingTitle(slot),y,()=>Adjust(slot,1),()=>lobby.CanConfigure,0,600,80,()=>setupPage=="settings"&&Setting(slot)!=null);
                choice.slide=fraction=>SetFraction(slot,fraction);
                var fill=new GameObject("Value fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(choice.button.transform,false);var image=fill.GetComponent<Image>();image.color=CandyHudVisuals.Mint;image.raycastTarget=false;image.rectTransform.pivot=new Vector2(0,.5f);image.rectTransform.anchoredPosition=new Vector2(-280,-28);image.rectTransform.sizeDelta=new Vector2(0,5);setupFills.Add(image);
                Add(setupStation,"setting-minus-"+i,()=>"−",y,()=>Adjust(slot,-1),()=>lobby.CanConfigure,-360,85,80,()=>setupPage=="settings"&&Setting(slot)!=null);
                Add(setupStation,"setting-plus-"+i,()=>"+",y,()=>Adjust(slot,1),()=>lobby.CanConfigure,360,85,80,()=>setupPage=="settings"&&Setting(slot)!=null);
            }
            Add(setupStation,"settings-prev",()=>"PREVIOUS SETTINGS",-395,()=>settingsPage=Mathf.Max(0,settingsPage-1),()=>true,-220,390,55,()=>setupPage=="settings"&&settingsPage>0);
            Add(setupStation,"settings-next",()=>"MORE SETTINGS",-395,()=>settingsPage++,()=>true,220,390,55,()=>setupPage=="settings"&&(settingsPage+1)*6<setupSettings.Count);
        }
        string SettingTitle(int slot)
        {
            var setting=Setting(slot);if(setting==null||Details==null)return "";var value=setting.read(Details.Setup);
            if(setting.type==typeof(int)&&setting.options!=null)value=setting.options[Mathf.Clamp((int)value,0,setting.options.Length-1)];
            float fill=setting.type==typeof(float)||setting.type==typeof(int)?Mathf.InverseLerp(setting.min,setting.max,Convert.ToSingle(setting.read(Details.Setup))):0;if(slot<setupFills.Count)setupFills[slot].rectTransform.sizeDelta=new Vector2(fill*760,5);
            return setting.label+"\n"+(value is float f?f.ToString("0.##"):value is bool b?b?"ON":"OFF":value?.ToString());
        }
        void Adjust(int slot,int step)
        {
            var entry=Setting(slot);if(entry==null||!lobby.CanConfigure)return;var state=Details.Setup;object value=entry.read(state);
            if(entry.type==typeof(bool))value=!(bool)value;
            else if(entry.type.IsEnum){var all=Enum.GetValues(entry.type);value=all.GetValue((Array.IndexOf(all,value)+step+all.Length)%all.Length);}
            else if(entry.type==typeof(string)){var all=entry.options;if(all==null||all.Length==0)return;value=all[(Array.IndexOf(all,(string)value)+step+all.Length)%all.Length];}
            else{float amount=entry.type==typeof(int)?1:entry.max>100?5:entry.max>20?1:.1f;float next=Mathf.Clamp(Convert.ToSingle(value)+step*amount,entry.min,entry.max);value=entry.type==typeof(int)?(object)Mathf.RoundToInt(next):next;}
            entry.write(state,value);Details.ConfigureSetup(state);
        }
        void SetFraction(int slot,float fraction)
        {
            var entry=Setting(slot);if(entry==null)return;
            if(entry.type!=typeof(float)&&entry.type!=typeof(int)){if(Time.unscaledTime>=nextSettingSend){Adjust(slot,1);nextSettingSend=Time.unscaledTime+.3f;}return;}
            if(Time.unscaledTime<nextSettingSend)return;nextSettingSend=Time.unscaledTime+.12f;
            var state=Details.Setup;float value=Mathf.Lerp(entry.min,entry.max,fraction);entry.write(state,entry.type==typeof(int)?(object)Mathf.RoundToInt(value):value);Details.ConfigureSetup(state);
        }
    }
}
