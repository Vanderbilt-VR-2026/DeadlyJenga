using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class MysteryRuleChecks
    {
        static string Report=>ProjectPath.Report("MysteryRules.txt");
        static MysteryRuleChecks(){EditorApplication.update+=()=>{var request=ProjectPath.Report("MysteryRules.request");if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){try{File.Delete(request);}catch(IOException){return;}Run();}};}
        static void Check(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static MysterySimulation Sample()
        {var s=new MysterySimulation();s.Start(new[]{"m","s","a","b","c"},new[]{"fire","quake","tsunami","ice"},new Dictionary<string,MysteryRole>{{"m",MysteryRole.Murderer},{"s",MysteryRole.Sheriff}});return s;}
        [MenuItem("Tools/Deadly Jenga/Validation/Check Mystery Rules")]
        public static void Run()
        {
            File.WriteAllText(Report,"Murder Mystery host rule checks\n");
            try
            {
                var s=Sample();Check(s.Role("m")==MysteryRole.Murderer&&s.Role("s")==MysteryRole.Sheriff,"Requested roles assigned with one murderer and one sheriff");
                Check(s.Role("a")==MysteryRole.Innocent&&s.Personal("a").role==MysteryRole.Innocent,"Each player receives only their personal role");
                Check(s.Hit("m","a")==MysteryHit.Blocked,"Startup grace prevents attacks");s.Tick(10);
                Check(s.Hit("m","a")==MysteryHit.Ordinary&&s.Hit("s","a")==MysteryHit.Ordinary,"Ordinary punches do not reveal hidden roles");
                s.SetSpecial("m",true);Check(s.Hit("m","a")==MysteryHit.Murder,"Murderer toggles special attack");
                s.Lethal("m","a",true,new MysteryBodyFrame{name="YellowTrainer",color=Color.yellow});
                Check(!s.Alive("a")&&s.Public().bodies.Length==1,"Murderer kill creates a public corpse without a role field");
                Check(!s.Report("m","a",Color.magenta)&&s.ReportCooldown("m","a")==5,"Murderer cannot instantly report their own kill");
                Check(s.Report("b","a",Color.green),"Another player can report the body immediately");
                int trail=s.AddTrail(new MysteryTrailFrame{block=0,color=Color.magenta});Check(s.Suspect("b","a",trail)&&!s.Suspect("b","a",trail),"Suspicion allows one vote per player");
                s.Tick(31);Check(!s.Suspect("c","a",trail)&&s.Public().bodies[0].suspected.Length==1,"Voting closes after thirty seconds while clues persist");
                var view=s.Public();view.bodies[0].suspected[0]=Color.black;Check(s.Public().bodies[0].suspected[0]==Color.magenta,"Public snapshots cannot mutate authoritative suspicion state");
                s=Sample();s.Tick(10);Check(s.Lethal("a","b",false,null)==MysteryHit.InnocentMistake&&!s.Alive("a")&&s.IsDowned("b"),"Wrong innocent kill punishes attacker and downs victim");
                Check(!s.Revive("a","b")&&s.Revive("c","b")&&!s.IsDowned("b"),"Only a living helper can revive a downed player");
                s=Sample();s.Tick(10);s.SetSpecial("s",true);s.Lethal("s","a",true,null);Check(s.Alive("s")&&s.Personal("s").mistakes==1,"First sheriff mistake is counted");s.Lethal("s","b",true,null);Check(!s.Alive("s"),"Second sheriff innocent kill eliminates sheriff");
                s=Sample();s.Tick(10);s.SetSpecial("m",true);s.Lethal("m","s",true,new MysteryBodyFrame());Check(s.SheriffFallen&&s.Public().bodies[0].sheriff,"Sheriff murder creates the public fallen-sheriff clue");Check(s.RecoverSheriff("a","s")&&s.Role("a")==MysteryRole.Sheriff&&!s.RecoverSheriff("b","s"),"Sheriff weapon can be recovered only once by an innocent");
                s=Sample();s.Tick(35);Check(s.Personal("s").veto&&s.Personal("s").disasterChoices.Length==3&&s.Personal("m").disasterChoices.Length==0,"At thirty-five seconds only sheriff sees three veto choices");Check(!s.Choose("a","fire"),"Innocent cannot forge a disaster choice");s.Tick(10);Check(s.Personal("m").disasterChoices.Length==2&&!s.Personal("m").veto,"At forty-five seconds timeout veto leaves two murderer choices");
                string picked=null;s.DisasterChosen+=id=>picked=id;Check(s.Choose("m",s.Personal("m").disasterChoices[0])&&picked!=null,"Murderer choice triggers exactly an offered disaster");for(int i=0;i<59;i++)s.Tick(1);Check(s.Personal("m").disasterChoices.Length==0,"Murderer offer cannot arrive before sixty seconds");bool arrived=false;for(int i=0;i<61;i++){s.Tick(1);if(s.Personal("m").disasterChoices.Length==2){arrived=true;break;}}Check(arrived,"Next murderer offer arrives within one hundred twenty seconds");
                s=Sample();s.Tick(10);s.NaturalDeath("m");Check(s.Winner=="Innocents","Murderer natural death wins for innocents");
                s=Sample();s.Tick(10);foreach(var id in new[]{"s","a","b","c"})s.NaturalDeath(id);Check(s.Winner=="Murderer","No surviving innocent wins for murderer");
                s=Sample();s.ForceRole("a",MysteryRole.Murderer);Check(s.Role("a")==MysteryRole.Murderer&&s.Role("m")==MysteryRole.Innocent,"Demo role reassignment preserves role uniqueness");
                s.Rekey("a","stable-a");Check(s.Role("stable-a")==MysteryRole.Murderer&&s.Role("a")==MysteryRole.Random,"Permanent bot identity replaces temporary identity without losing its secret role");s.RegisterLate("late");Check(s.Role("late")==MysteryRole.Innocent,"Late registered startup participant receives an ordinary role");
                s=Sample();s.Tick(35);s.Choose("s",s.Personal("s").disasterChoices[0]);Check(s.Personal("m").disasterChoices.Length==0,"An early sheriff veto cannot reveal murderer choices before forty-five seconds");s.Tick(10);Check(s.Personal("m").disasterChoices.Length==2,"Early veto choices unlock at forty-five seconds");
                File.AppendAllText(Report,"COMPLETE: rule model only; gameplay, visuals and multiplayer still require integration checks.\n");
            }
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Debug.LogException(e);}
        }
    }
}
