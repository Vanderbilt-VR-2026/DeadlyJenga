using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class ModePicturePickerChecks
    {
        static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static string Page(IslandLobbyView view)=>(string)typeof(IslandLobbyView).GetField("setupPage",Hidden).GetValue(view);
        static void Refresh(IslandLobbyView view)
        {
            typeof(IslandLobbyView).GetMethod("UpdateGameStation",Hidden).Invoke(view,null);
            view.Point(new Ray(Vector3.one*100,Vector3.up),false);
        }
        public static IEnumerator Run(Action<bool,string> check)
        {
            var lobby=IslandLobby.Current;
            check(lobby&&lobby.View(),"Actual Lobby initializes its mode picker");
            var view=lobby.View();var details=(ILobbyDetails)lobby;var controls=(ILobbyControls)lobby;
            string oldMode=details.SelectedModeId,oldMap=details.SelectedMapId;
            bool hosted=!controls.CanConfigure;
            if(hosted)
            {
                check(view.InvokeChoice("host"),"Real lobby host command is accepted");
                float end=Time.realtimeSinceStartup+45;
                while(lobby.Room.Busy&&Time.realtimeSinceStartup<end)yield return null;
                check(controls.CanConfigure,"Hosting unlocks mode and map setup");
            }
            try
            {
                check(view.InvokeChoice("mode-current")&&Page(view)=="modes","Mode is the first numbered selection and opens its gallery");Refresh(view);
                var terminal=view.transform.Find("MAP + MODE");
                var support=view.transform.Find("Mode controller computer/Monitor support").GetComponent<Renderer>();
                check(support.bounds.min.z>terminal.position.z,"Monitor support stays behind the interactive screen and its lower buttons");
                check(terminal.GetComponentsInChildren<LobbyModeIcon>(true).Length==0,"Mode gallery and selected mode use pictures without vector icons");
                var categories=terminal.GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();
                check(categories.Contains("PvP")&&categories.Contains("SURVIVAL"),"Mode gallery has separate PvP and Survival category columns");
                var options=typeof(IslandLobbyView).GetMethod("ModeCardOption",Hidden);
                string[] survival={"survival","infection","mystery","demo"};
                int cards=0,pictures=0;
                for(int i=0;i<8;i++)
                {
                    var option=options.Invoke(view,new object[]{i}) as LobbyOption;if(option==null)continue;cards++;
                    var card=terminal.Find("mode-card-"+i) as RectTransform;
                    check((card.anchoredPosition.x>0)==survival.Contains(option.id),option.title+" appears under its intended category");
                    check(card.GetComponentsInChildren<Text>().Any(t=>t.text==option.description&&!string.IsNullOrWhiteSpace(t.text)),option.title+" includes its brief catalog description");
                    check(card.GetComponent<BoxCollider>().isTrigger&&card.GetComponent<BoxCollider>().size.x==390,"Mode card retains a large world-space VR pointing target");
                    var image=card.GetComponentInChildren<RawImage>();
                    pictures++;check(image.texture&&image.texture.name==option.id,option.title+" uses its approved picture");
                }
                check(cards==details.Modes.Count(p=>p.available)&&pictures==8,"Gallery contains all playable modes and all eight approved artworks");
                check(!view.InvokeChoice("picker-next"),"Single-page category gallery cannot advance into an empty page");
                Capture(terminal,"ModePictures-Gallery.png");
                string selected=(options.Invoke(view,new object[]{0}) as LobbyOption).id;
                check(view.InvokeChoice("mode-card-0")&&details.SelectedModeId==selected&&Page(view)=="maps","Picking a mode opens that mode's map selection without starting a game");Refresh(view);
                check(details.Maps.Count==lobby.catalog.maps.Length,"All current maps remain available during compatibility setup");
                string map=details.Maps[0].id;
                check(view.InvokeChoice("picker-0")&&details.SelectedMapId==map&&details.SelectedModeId==selected&&Page(view)=="main","Picking a map preserves the chosen mode and returns to setup");Refresh(view);
                var mainTexts=terminal.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy).ToArray();
                var modeLabel=mainTexts.First(t=>t.text=="1 - GAME MODE");var mapLabel=mainTexts.First(t=>t.text=="2 - MAP");
                check(modeLabel.rectTransform.anchoredPosition.y>mapLabel.rectTransform.anchoredPosition.y,"Setup visibly orders game mode before map");
                Capture(terminal,"ModePictures-Setup.png");
                check(view.InvokeChoice("map-current")&&Page(view)=="maps","Map list can be reopened after mode selection");Refresh(view);Capture(terminal,"ModePictures-Maps.png");
                check(view.InvokeChoice("setup-back")&&Page(view)=="main","Map selection can return without changing the chosen mode");
            }
            finally
            {
                controls.SelectMode(oldMode);controls.SelectMap(oldMap);
                typeof(IslandLobbyView).GetField("setupPage",Hidden).SetValue(view,"main");
                Refresh(view);
                if(hosted)view.InvokeChoice("abandon-host");
            }
            if(hosted){float end=Time.realtimeSinceStartup+15;while(lobby.Room.Busy&&Time.realtimeSinceStartup<end)yield return null;}
        }
        static void Capture(Transform terminal,string name)
        {
            var go=new GameObject("Temporary mode picker capture");var camera=go.AddComponent<Camera>();camera.fieldOfView=42;
            camera.transform.position=terminal.position-terminal.forward*3.8f;camera.transform.LookAt(terminal.position);
            var rt=new RenderTexture(1400,1400,24);var old=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1400,1400,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1400,1400),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;camera.targetTexture=null;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);Object.Destroy(go);}
        }
    }
}
