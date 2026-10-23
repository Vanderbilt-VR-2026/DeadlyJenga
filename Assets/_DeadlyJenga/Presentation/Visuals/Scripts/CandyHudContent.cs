using System;
using System.Linq;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.Presentation
{
    // Text describes snapshot facts, never hidden gameplay state.
    public static class CandyHudContent
    {
        public static string Safe(string value,int max=34)
        {
            value=(value??"").Replace("<","‹").Replace(">","›").Replace('\n',' ').Replace('\r',' ');
            return value.Length>max?value.Substring(0,max-1)+"…":value;
        }
        public static string Clock(float seconds)=>seconds<0?"NO TIME LIMIT":TimeSpan.FromSeconds(Mathf.CeilToInt(seconds)).ToString(@"mm\:ss");
        public static string Mode(GameModeKind mode)
        {
            switch(mode){case GameModeKind.CaptureFlag:return "CAPTURE THE FLAG";case GameModeKind.MurderMystery:return "MURDER MYSTERY";case GameModeKind.TeamTickets:return "LAST TEAM STANDING";case GameModeKind.BlockControl:return "COLOR CONTROL";case GameModeKind.KingOfHill:return "KING OF THE HILL";case GameModeKind.Infection:return "INFECTION";case GameModeKind.Survival:return "LAST SURVIVOR";default:return "SANDBOX";}
        }
        public static InputAction CreateSelectInput()
        {
            var action=new InputAction("HUD context select",InputActionType.Button,"<XRController>{RightHand}/trigger");action.Enable();return action;
        }
        public static bool SelectPressed(InputAction action)
        {
            var session=Services.Session;var active=session?.PlayingHand==true?session.Hand:session?.Player;
            return action?.WasPressedThisFrame()==true||active?.SelectPressed==true;
        }
        public static bool SelectHeld(InputAction action)
        {
            var session=Services.Session;var active=session?.PlayingHand==true?session.Hand:session?.Player;
            return action?.IsPressed()==true||active?.SelectHeld==true;
        }
        public static bool LiveStandings(GameModeKind mode)=>mode==GameModeKind.KingOfHill;
        public static string ResultStats(MatchPlayerFrame p,GameModeKind mode)
        {
            switch(mode)
            {
                case GameModeKind.CaptureFlag:return p.captures+" captures / "+p.flagReturns+" returns / "+p.kills+" kills / "+p.deaths+" deaths";
                case GameModeKind.TeamTickets:return p.kills+" kills / "+p.deaths+" deaths";
                case GameModeKind.KingOfHill:return p.hillSeconds.ToString("0.0")+"s hill / "+p.kills+" kills";
                case GameModeKind.BlockControl:return p.blocksClaimed+" claims / "+p.kills+" kills";
                case GameModeKind.MurderMystery:return p.bodyReports+" reports / "+p.revives+" revives / "+p.kills+" kills";
                case GameModeKind.Infection:return p.survivalSeconds.ToString("0.0")+"s survivor / "+p.infections+" infected";
                case GameModeKind.Survival:return p.survivalSeconds.ToString("0.0")+"s / "+(p.alive?"SURVIVED":"ELIMINATED");
                default:return p.kills+" kills / "+p.deaths+" deaths";
            }
        }
        public static string Direction(Camera eye,Vector3 world)
        {
            if(!eye)return "";
            var delta=world-eye.transform.position;float angle=Vector3.SignedAngle(Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up),Vector3.ProjectOnPlane(delta,Vector3.up),Vector3.up);
            string direction=Mathf.Abs(angle)>140?"BEHIND":angle>35?"RIGHT >":angle< -35?"< LEFT":"AHEAD";
            return direction+" · "+Mathf.RoundToInt(delta.magnitude)+"m"+(delta.y>3?" · ABOVE":delta.y< -3?" · BELOW":"");
        }
        public static string Team(MatchFrame frame,int id)=>Safe(frame.teams.FirstOrDefault(t=>t.id==id)?.name??("Team "+(id+1)),22);
        public static FlagFrame FocusedFlag(MatchFrame frame,MatchPlayerFrame own,int index)
        {
            var carried=frame.flags.FirstOrDefault(f=>f.carrier==own.id);if(carried!=null)return carried;
            var enemies=frame.flags.Where(f=>f.team!=own.team).OrderBy(f=>f.team).ToArray();return enemies.Length==0?null:enemies[Mathf.Max(0,index)%enemies.Length];
        }
        public static string FlagStatus(MatchFrame frame,FlagFrame flag,string local)
        {
            if(flag.atHome)return "AT BASE";
            if(string.IsNullOrEmpty(flag.carrier))return frame.players.FirstOrDefault(p=>p.id==local)?.team==flag.team?"DROPPED · RECOVER TO BASE":"DROPPED · TOUCH TO TAKE";
            if(flag.carrier==local)return "YOU ARE CARRYING IT";
            var carrier=frame.players.FirstOrDefault(p=>p.id==flag.carrier);
            return "CARRIED BY "+Safe(carrier?.name??"a player",22);
        }
        public static string FlagTask(MatchFrame frame,FlagFrame flag,MatchPlayerFrame own)
        {
            if(flag.carrier==own.id)return "DELIVER TO YOUR BASE";
            if(flag.team==own.team)return flag.atHome?"DEFEND YOUR FLAG":string.IsNullOrEmpty(flag.carrier)?"RETURN YOUR FLAG":"RECOVER YOUR FLAG";
            return flag.atHome?"STEAL ENEMY FLAG":string.IsNullOrEmpty(flag.carrier)?"TAKE ENEMY FLAG":frame.players.FirstOrDefault(p=>p.id==flag.carrier)?.team==own.team?"ESCORT YOUR CARRIER":"INTERCEPT CARRIER";
        }
        public static string RoleGoal(MysteryRole role)
        {
            switch(role){case MysteryRole.Murderer:return "Eliminate everyone. Keep your identity hidden.";case MysteryRole.Sheriff:return "Find the murderer. Special attacks punish mistakes.";default:return "Stay alive. Report bodies and help revive players.";}
        }
        public static string RoleAttack(MysteryPrivateFrame own,bool vr)
        {
            if(own.role!=MysteryRole.Murderer&&own.role!=MysteryRole.Sheriff)return "Your role is private. Colored trails are clues, not proof.";
            return (own.specialAttack?"SPECIAL ATTACK ARMED":"NORMAL PUNCH")+(own.attackReadyIn>0?" · Ready in "+Mathf.CeilToInt(own.attackReadyIn)+"s":"")+"\n"+(vr?"Switch attack in the Modes menu.":"Q switches normal / special attack.");
        }
        public static string Suspects(MysteryBodyFrame body)
        {
            if(body.suspected.Length==0)return "No suspected trails yet";
            return "Suspected trails: "+string.Join(", ",body.suspected.Select(ColorName))+" (not proof)";
        }
        public static string ColorName(Color color)
        {
            Color.RGBToHSV(color,out float hue,out float saturation,out float value);
            if(value<.2f)return "black";if(saturation<.15f)return value>.8f?"white":"gray";
            return hue<.04f||hue>=.96f?"red":hue<.12f?"orange":hue<.2f?"yellow":hue<.45f?"green":hue<.55f?"cyan":hue<.72f?"blue":hue<.86f?"purple":"pink";
        }
        public static bool BodyKnown(MysteryBodyFrame body,bool nearbyVisible)=>!body.lost&&(body.reported||body.sheriff||nearbyVisible);
    }
}
