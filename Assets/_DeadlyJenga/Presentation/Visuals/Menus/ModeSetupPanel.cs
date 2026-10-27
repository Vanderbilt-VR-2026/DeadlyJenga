using System.Linq;
using System;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CandyArt;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // All writes are requests to the mode service; gameplay remains outside Presentation.
    public static class ModeSetupPanel
    {
        static readonly FieldInfo[] fields=typeof(ModeRules).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>f.Name!="carryingFlag"&&f.Name!="sinking"&&f.Name!="sinkProgress"&&f.Name!="downed"&&f.Name!="mystery").ToArray();
        static int ruleGroup;
        static readonly string[] groups={"Round / scoring","Combat / health","Movement / traversal","World / disasters"};
        static int Group(string name)
        {
            name=name.ToLowerInvariant();
            if(name.Contains("punch")||name.Contains("health")||name.Contains("death")||name.Contains("damage")||name.Contains("friendly")||name.Contains("dagger"))return 1;
            if(name.Contains("jump")||name.Contains("climb")||name.Contains("vault")||name.Contains("move")||name.Contains("grip")||name.Contains("stamina")||name.Contains("drain")||name.Contains("recover"))return 2;
            if(name.Contains("disaster")||name.Contains("island")||name.Contains("water")||name.Contains("environment"))return 3;
            return 0;
        }
        public static void Draw()
        {
            var match=MatchRuntime.Current;if(match==null){GUILayout.Label("Match rules are starting...");return;}
            var copy=match.Rules.Copy();bool was=GUI.enabled;GUI.enabled=match.Authority||(Services.Session?.Demo?.CanCommand==true&&copy.mode==GameModeKind.Demo);
            if(GUILayout.Button("Start / restart round"))match.StartRound();
            GUILayout.Label("Mode: "+copy.mode+"  ·  "+match.Frame.phase);
            GUI.enabled=was;
            if(GUILayout.Button(DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned?"Hide full standings after closing this menu":"Show full standings after closing this menu"))DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned=!DeadlyJenga.Presentation.CandyMatchHud.ScoreboardPinned;
            if(copy.mode==GameModeKind.CaptureFlag&&match.Frame.flags.Length>2&&GUILayout.Button("Track next enemy flag"))DeadlyJenga.Presentation.CandyMatchHud.FlagFocusIndex++;
            GUI.enabled=was&&match.Authority;
            var mystery=match as IMysteryControls;
            if(mystery?.Active==true)
            {
                GUI.enabled=was;
                if(GUILayout.Button("Next body report page after closing this menu"))DeadlyJenga.Presentation.CandyMysteryView.ReportPage++;
                GUI.enabled=was;GUILayout.Label("Your private role: "+mystery.Personal.role);
                if(match is IMatchReviewControls review&&review.DevelopmentReviewEnabled)
                {
                    GUILayout.Label("DEVELOPMENT REVIEW · ROLE SPOILERS · REMOVE FOR RELEASE");
                    foreach(var role in new[]{MysteryRole.Murderer,MysteryRole.Sheriff})
                    {
                        string id=review.PlayerInRole(role);var player=match.Frame.players.FirstOrDefault(p=>p.id==id);
                        GUI.enabled=was&&player!=null&&player.alive&&!player.dying;
                        if(GUILayout.Button("Watch "+role+": "+(player?.name??"not assigned")+(player!=null&&!player.alive?" · fallen":"")))review.WatchPlayer(id);
                    }
                    GUI.enabled=was;
                }
                if(mystery.Personal.role==MysteryRole.Murderer||mystery.Personal.role==MysteryRole.Sheriff){bool special=GUILayout.Toggle(mystery.Personal.specialAttack,"Use special punch");if(special!=mystery.Personal.specialAttack)mystery.SetSpecialAttack(special);}
                foreach(string hazard in mystery.Personal.disasterChoices)if(GUILayout.Button((mystery.Personal.veto?"Veto ":"Trigger ")+hazard))mystery.ChooseDisaster(hazard);
                GUI.enabled=was&&match.Authority;
                foreach(var role in new[]{MysteryRole.Innocent,MysteryRole.Sheriff,MysteryRole.Murderer})if(GUILayout.Button("Demo: become "+role))mystery.SetDemoRole(role);
                var tune=JsonUtility.FromJson<MysterySettings>(JsonUtility.ToJson(copy.mystery));string old=JsonUtility.ToJson(tune);
                foreach(var f in typeof(MysterySettings).GetFields())
                {float value=Convert.ToSingle(f.GetValue(tune));GUILayout.Label(Label(f.Name)+"  "+value.ToString("0.##"));float min=f.Name=="sheriffMistakes"?1:0,max=f.Name.Contains("Distance")||f.Name=="trailRadius"?5:f.Name=="sheriffMistakes"?10:180;float next=GUILayout.HorizontalSlider(value,min,max);if(f.FieldType==typeof(int))f.SetValue(tune,Mathf.RoundToInt(next));else f.SetValue(tune,next);}
                if(old!=JsonUtility.ToJson(tune))mystery.ConfigureMystery(tune);
            }
            GUI.enabled=was&&match.Authority;
            GUILayout.Label("Live HUD prioritizes the mode objective. Full standings stay available on demand; final results include contributions.");
            if(GUILayout.Button("Restore recommended defaults for this mode"))copy=ModeDefaults.Create(copy.mode);
            if(GUILayout.Button("Next game mode"))copy=ModeDefaults.Create((GameModeKind)(((int)copy.mode+1)%Enum.GetValues(typeof(GameModeKind)).Length));
            GUI.enabled=was&&(match.Authority||(Services.Session?.Demo?.CanCommand==true&&copy.mode==GameModeKind.Demo));
            if(GUILayout.Button("Scene skin: "+CandySceneSkins.Names[CandyMatchVisualsSkin(copy.environmentSkin)]))copy.environmentSkin=CandySceneSkins.Names[(CandyMatchVisualsSkin(copy.environmentSkin)+1)%CandySceneSkins.Names.Length];
            if(GUILayout.Button("Temporary team hand power"))match.ActivateHandPower(copy.handPowerSeconds);
            GUI.enabled=was&&(match.Authority||Services.Session?.Demo?.CanCommand==true);
            var spirit=Services.Session as ISeaSpiritControls;
            if(spirit!=null)
            {
                if(GUILayout.Button(spirit.SeaSpiritActive?"Return from sea spirit":"Become giant two-hand sea spirit"))spirit.SetSeaSpirit(!spirit.SeaSpiritActive);
                GUILayout.Label("Sea spirit reach  "+spirit.SeaSpiritReach.ToString("0.0"));float reach=GUILayout.HorizontalSlider(spirit.SeaSpiritReach,2,100);if(!Mathf.Approximately(reach,spirit.SeaSpiritReach))spirit.SetSeaSpiritReach(reach);
                if(GUILayout.Button("Recenter sea spirit"))spirit.RecenterSeaSpirit();
            }
            DeadlyJenga.Presentation.CandyTabletop.Draw();
            GUI.enabled=was&&(match.Authority||(Services.Session?.Demo?.CanCommand==true&&copy.mode==GameModeKind.Demo));
            GUILayout.Label("Rule changes apply to this session. Start a new round after changing team or mode setup.");
            string before=JsonUtility.ToJson(match.Rules);
            bool islandEffects=GUILayout.Toggle(copy.island!=IslandRule.Walkable,"Island effects (sinking, slowing or island death)");
            if(islandEffects!=(copy.island!=IslandRule.Walkable))copy.island=islandEffects?IslandRule.Sink:IslandRule.Walkable;
            if(!islandEffects)GUILayout.Label("Island ground is safe and movement stays normal. Ocean and other game rules still apply.");
            if(GUILayout.Button("Advanced rules: "+groups[ruleGroup]+"  ▾"))ruleGroup=(ruleGroup+1)%groups.Length;
            foreach(var f in fields)
            {
                if(f.Name=="mode"||f.Name=="environmentSkin"||Group(f.Name)!=ruleGroup)continue;string label=Label(f.Name);
                if(f.FieldType==typeof(bool))f.SetValue(copy,GUILayout.Toggle((bool)f.GetValue(copy),label));
                else if(f.FieldType.IsEnum){var value=(Enum)f.GetValue(copy);if(GUILayout.Button(label+": "+value)){var all=Enum.GetValues(f.FieldType);f.SetValue(copy,all.GetValue((Array.IndexOf(all,value)+1)%all.Length));}}
                else if(f.FieldType==typeof(float)||f.FieldType==typeof(int))
                {
                    float value=Convert.ToSingle(f.GetValue(copy));Range(f.Name,out float min,out float max);GUILayout.Label(label+"  "+value.ToString(f.FieldType==typeof(int)?"0":"0.##"));float next=GUILayout.HorizontalSlider(value,min,max);
                    if(f.FieldType==typeof(int))f.SetValue(copy,Mathf.RoundToInt(next));else f.SetValue(copy,next);
                }
            }
            GUI.enabled=was;if(JsonUtility.ToJson(copy)!=before)match.Configure(copy);
        }
        public static string Label(string name)
        {var text=new System.Text.StringBuilder();foreach(char c in name){if(char.IsUpper(c))text.Append(' ');text.Append(c);}return char.ToUpperInvariant(text[0])+text.ToString().Substring(1);}
        public static void Range(string name,out float min,out float max)
        {
            min=0;max=30;
            if(name=="disasterMinInterval"||name=="disasterMaxInterval"||name=="disasterLateInterval"){min=10;max=600;return;}
            if(name=="disasterWarningSeconds"){min=2;max=30;return;}
            if(name=="maxConcurrentDisasters"){min=1;max=4;return;}
            if(name=="teamCount"){min=2;max=8;}else if(name=="flagTarget"){min=1;max=10;}else if(name=="tickets")max=200;
            else if(name=="roundSeconds"){min=0;max=1200;}else if(name=="hillInterval"){min=2;max=60;}
            else if(name=="claimSeconds"||name=="reclaimSeconds"){min=.2f;max=30;}else if(name=="claimDecaySeconds")max=180;
            else if(name=="punchCooldown"){min=.2f;max=5;}else if(name=="punchDamage"||name=="fallDamagePerMeter")max=100;
            else if(name=="moveScale"||name=="vaultScale"){min=.1f;max=2;}else if(name=="sinkMoveScale"||name=="sinkSpeed")max=1;
            else if(name=="handPowerSeconds")max=120;
        }
        static int CandyMatchVisualsSkin(string value)=>DeadlyJenga.Presentation.CandyMatchVisuals.SkinIndex(value);
    }
}
