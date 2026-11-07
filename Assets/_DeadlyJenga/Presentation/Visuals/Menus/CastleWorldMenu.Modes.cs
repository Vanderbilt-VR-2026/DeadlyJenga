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
        static readonly string[] commonModeFields={"mode","healthEnabled","friendlyFire","punchEnabled","automaticDisasters","roundSeconds","island"};
        static readonly FieldInfo[] quickModeFields=commonModeFields.Select(name=>modeFields.First(field=>field.Name==name)).ToArray();
        int modeOffset;bool hudPage;
        void PointModePage(Camera camera,bool pressed)
        {
            var match=MatchRuntime.Current;if(match==null){modePage=false;return;}var rules=match.Rules;
            var fields=modeOffset==0?quickModeFields:modeFields;
            int pageStart=modeOffset==0?0:modeOffset-1;
            heading.text="MATCH RULES / SCENERY\n"+rules.mode+" · "+match.Frame.phase;
            HideDisasterSliders();bool canConfigure=match.Authority||(rules.mode==GameModeKind.Demo&&Services.Session?.Demo?.CanCommand==true);
            for(int i=0;i<buttons.Length;i++){buttons[i].gameObject.SetActive(true);targets[i].enabled=i==0||canConfigure;}
            labels[0].text="Back to demo";labels[1].text="Start / restart round";labels[2].text="Skin: "+rules.environmentSkin;
            for(int i=3;i<=9;i++){int index=pageStart+i-3;if(index<fields.Length){var field=fields[index];labels[i].text=ModeSetupPanel.Label(field.Name)+": "+field.GetValue(rules);if(field.Name=="mode")targets[i].enabled=match.Authority;}else buttons[i].gameObject.SetActive(false);}
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
