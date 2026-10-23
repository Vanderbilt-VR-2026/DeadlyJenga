using System;
using System.Collections;
using System.IO;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.Presentation;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class WeaponPresentationChecks
    {
        static int count;
        static string Report=>ProjectPath.Report("WeaponPresentation.txt");
        static void Check(bool passed,string text)
        {
            File.AppendAllText(Report,(passed?"PASS ":"FAIL ")+text+"\n");
            if(!passed)throw new Exception(text);count++;
        }
        static void CheckTint(GameObject host,Color expected,string text)
        {
            var properties=new MaterialPropertyBlock();int renderers=0;bool correct=true;
            foreach(var renderer in host.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(properties);renderers++;
                correct&=properties.GetColor("_BaseColor")==expected&&properties.GetColor("_Color")==expected;
            }
            Check(correct&&renderers>0,text);
        }
        public static IEnumerator Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
            count=0;File.WriteAllText(Report,"Weapon audio imports and pooled visual bullet fixtures; no headset or perceptual audio certification.\n");
            foreach(var sound in CandyWeaponSounds.Sounds)
            {
                var cue=CandyWeaponSounds.DefaultCue(sound);
                Check(cue.variants.Length==4,"Four authored takes: "+sound);
                foreach(var clip in cue.variants)Check(clip&&clip.channels==1&&clip.samples>0&&clip.frequency==32000,"Imported mono32k clip: "+(clip?clip.name:"MISSING"));
                Check(cue.bus==CandyBus.Character&&cue.maxVoices<=8&&cue.maxDistance<=90,"Bounded spatial Character bus: "+sound);
            }
            var host=new GameObject("Weapon visual validation fixture");
            try
            {
                var view=host.AddComponent<CandyBulletTrails>();
                var endpoints=new[]{Vector3.forward*10,new Vector3(.5f,0,10),new Vector3(-.5f,0,10),new Vector3(0,.5f,10),new Vector3(0,-.5f,10),new Vector3(.3f,.3f,10),new Vector3(-.3f,-.3f,10)};
                view.Fire(Vector3.zero,endpoints,true);
                Check(view.ActiveCount==7,"Shotgun renders seven supplied authoritative endpoints");
                CheckTint(host,new Color(1,.43f,.09f,1),"Shotgun heads and trails use amber material properties");
                Check(host.GetComponentsInChildren<Collider>(true).Length==0&&host.GetComponentsInChildren<Rigidbody>(true).Length==0,"Cosmetic bullets add no collision/damage bodies");
                for(int i=0;i<40;i++)view.Fire(Vector3.zero,endpoints,true);
                Check(view.ActiveCount==224&&view.AllocatedCount==224,"Burst is bounded to 224 pooled pellets");
                yield return new WaitForSeconds(.4f);
                Check(view.ActiveCount==0&&view.AllocatedCount==224,"Expired bullets return to reusable pool");
                view.Fire(Vector3.zero,new[]{Vector3.forward*65},false);
                Check(view.ActiveCount==1&&view.AllocatedCount==224,"Rifle reuses pooled bullet without allocation growth");
                CheckTint(host,new Color(.24f,.88f,1,1),"Reused rifle head and trail switch to cyan material properties");
                view.enabled=false;Check(view.ActiveCount==0,"Disabling clears visible bullets");
            }
            finally{Object.Destroy(host);}
            File.AppendAllText(Report,"COMPLETE checks="+count+"\n");
        }
    }
}
