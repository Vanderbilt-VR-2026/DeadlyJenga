using System.Linq;
using System;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        static readonly FieldInfo[] modeFields=typeof(ModeRules).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>f.Name!="carryingFlag"&&f.Name!="sinking"&&f.Name!="sinkProgress"&&f.Name!="downed"&&f.Name!="mystery").ToArray();
        int modeOffset;
        void PointModePage(Camera camera,bool pressed)
        {
            var match=MatchRuntime.Current;if(match==null){modePage=false;return;}var rules=match.Rules;
            heading.text="MATCH RULES / SCENERY\n"+rules.mode+" · "+match.Frame.phase;
            HideDisasterSliders();transform.position=camera.transform.position+openingForward*1.6f;
            bool canConfigure=match.Authority||(rules.mode==GameModeKind.Demo&&Services.Session?.Demo?.CanCommand==true);
            for(int i=0;i<buttons.Length;i++){buttons[i].gameObject.SetActive(true);targets[i].enabled=i==0||canConfigure;}
            labels[0].text="Back to demo";labels[1].text="Start / restart round";labels[2].text="Skin: "+rules.environmentSkin;
            for(int i=3;i<=9;i++){int index=modeOffset+i-3;if(index<modeFields.Length){var field=modeFields[index];labels[i].text=ModeSetupPanel.Label(field.Name)+": "+field.GetValue(rules);if(field.Name=="mode")targets[i].enabled=match.Authority;}else buttons[i].gameObject.SetActive(false);}
            labels[10].text="Stamina / audio tuning";labels[11].text="Temporary team hand power";labels[12].text="Previous settings";labels[13].text="More settings";labels[14].text="Sea spirit / tabletop AR";
            var mystery=match as IMysteryControls;
            if(mystery?.Active==true){labels[11].text="Special punch: "+(mystery.Personal.specialAttack?"ON":"OFF");labels[14].text="Demo role: "+mystery.Personal.role;targets[11].enabled=true;}
            targets[12].enabled=targets[13].enabled=targets[14].enabled=true;Physics.SyncTransforms();var ray=new Ray(camera.transform.position,camera.transform.forward);
            for(int i=0;i<buttons.Length;i++)
            {
                if(!buttons[i].gameObject.activeSelf)continue;bool hover=targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:Color.white;
                if(!hover||!pressed||!targets[i].enabled)continue;
                if(mystery?.Active==true&&i==11){mystery.SetSpecialAttack(!mystery.Personal.specialAttack);return;}
                if(mystery?.Active==true&&i==14){if(match.Authority)mystery.SetDemoRole((MysteryRole)(1+(int)mystery.Personal.role%3));return;}
                if(i==0){modePage=false;return;}if(i==12){modeOffset=Mathf.Max(0,modeOffset-7);return;}if(i==13){modeOffset=modeOffset+7>=modeFields.Length?0:modeOffset+7;return;}if(i==14){modePage=false;tabletopPage=true;return;}
                if(i==10){modePage=false;tuningPage=true;comfortPage=networkPage=false;return;}
                if(i==11){match.ActivateHandPower(rules.handPowerSeconds);return;}
                if(i==1){match.StartRound();return;}var copy=rules.Copy();
                if(i==2)copy.environmentSkin=CandySceneSkins.Names[(DeadlyJenga.Presentation.CandyMatchVisuals.SkinIndex(copy.environmentSkin)+1)%CandySceneSkins.Names.Length];
                else
                {
                    var f=modeFields[modeOffset+i-3];var type=f.FieldType;
                    if(type==typeof(bool))f.SetValue(copy,!(bool)f.GetValue(copy));
                    else if(type.IsEnum){var values=Enum.GetValues(type);var next=values.GetValue((Array.IndexOf(values,f.GetValue(copy))+1)%values.Length);if(f.Name=="mode")copy=ModeDefaults.Create((GameModeKind)next);else f.SetValue(copy,next);}
                    else if(type==typeof(float)||type==typeof(int)){ModeSetupPanel.Range(f.Name,out float min,out float max);float next=Convert.ToSingle(f.GetValue(copy))+Mathf.Max(type==typeof(int)?1:.1f,(max-min)/20);if(next>max)next=min;if(type==typeof(int))f.SetValue(copy,Mathf.RoundToInt(next));else f.SetValue(copy,next);}
                }
                match.Configure(copy);return;
            }
            help.text="Look + trigger / pinch to cycle a value. More settings reveals all mode options.";
        }
    }
}
