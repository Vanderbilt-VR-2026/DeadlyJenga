using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt.Marina;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class DeveloperUiChecks
    {
        static IEnumerator LobbyTerminalChecks()
        {
            File.AppendAllText(Report, "Lobby terminal pass. Real lobby and presentation; connected/request/guest screens explicitly use existing presentation stand-in, not live networking.\n");
            float until = Time.realtimeSinceStartup + 60;
            while (!(Services.Session is IslandLobby) && Time.realtimeSinceStartup < until) yield return null;
            var lobby = Services.Session as IslandLobby;
            Check(lobby, "Actual Lobby initializes");
            var view = lobby.GetComponent<IslandLobbyView>();
            Check(view, "Production lobby view exists");
            var marina = view.GetComponent<MarinaScreen>();
            Check(marina, "Current Marina terminals exist");
            File.AppendAllText(Report, "Presentation MVID " + typeof(IslandLobbyView).Assembly.ManifestModule.ModuleVersionId + "\n");
            GameExperiencePanel.Resolve(lobby)?.SetExperience(GameExperience.Demo);
            lobby.Player.InputSuspended = true;
            var join = Field<RectTransform>(view,"joinStation");
            var host = Field<RectTransform>(view,"hostStation");
            var setup = Field<RectTransform>(view,"setupStation");
            var issues = new List<string>();
            void Sync()
            {
                Call(view,"UpdateStations"); Call(view,"UpdateMarinaRoomScreens");
                view.Point(new Ray(Vector3.one * 100, Vector3.up), false);
                Canvas.ForceUpdateCanvases(); Physics.SyncTransforms();
            }
            void Shot(RectTransform screen, string file)
            {
                Sync(); CaptureTerminal(screen,file);
                var buttons = screen.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy).ToArray();
                Rect Bounds(RectTransform rect)
                {
                    var corners=new Vector3[4]; rect.GetWorldCorners(corners);
                    var pts=corners.Select(screen.InverseTransformPoint).ToArray();
                    return Rect.MinMaxRect(pts.Min(x=>x.x),pts.Min(x=>x.y),pts.Max(x=>x.x),pts.Max(x=>x.y));
                }
                foreach (var button in buttons)
                {
                    var rect=(RectTransform)button.transform;var hit=button.GetComponent<BoxCollider>();
                    Check(hit && Vector2.Distance(new Vector2(hit.size.x,hit.size.y),rect.rect.size)<.1f, "Button hitbox matches visual: " + file + "/" + button.name);
                    var bounds=Bounds(rect);var area=screen.rect;area.xMin-=1;area.xMax+=1;area.yMin-=1;area.yMax+=1;
                    if(!area.Contains(bounds.min)||!area.Contains(bounds.max)) issues.Add(file+" outside screen: "+button.name);
                }
                for(int i=0;i<buttons.Length;i++) for(int j=i+1;j<buttons.Length;j++)
                {
                    if(buttons[i].transform.IsChildOf(buttons[j].transform)||buttons[j].transform.IsChildOf(buttons[i].transform))continue;
                    var a=Bounds((RectTransform)buttons[i].transform);var b=Bounds((RectTransform)buttons[j].transform);
                    float w=Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin),h=Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin);
                    if(w>1&&h>1) issues.Add(file+" overlaps: "+buttons[i].name+" / "+buttons[j].name);
                }
            }
            yield return null;
            Shot(Field<RectTransform>(marina,"joinFace"),"40-join-front.png");
            Shot(Field<RectTransform>(marina,"hostFace"),"41-host-front.png");
            Shot(Field<RectTransform>(marina,"mainFace"),"42-match-front.png");
            Call(view,"SetMarinaDetail",join,true); yield return null;
            Shot(join,"43-join-code.png");
            Call(view,"SetMarinaDetail",host,true); yield return null;
            Shot(host,"44-host-offline.png");
            Call(view,"SetMarinaDetail",setup,true); yield return null;
            Shot(setup,"45-match-setup.png");
            foreach(string page in new[]{"modes","maps","settings"})
            {
                Call(view,"OpenMarinaDetail",page);yield return null;
                Shot(setup,"46-"+page+".png");
            }
            var original=Field<ILobbyControls>(view,"lobby");
            var fixtureType=typeof(LobbyTerminalDesignChecks).GetNestedType("FixtureLobby",BindingFlags.NonPublic);
            var fixture=Activator.CreateInstance(fixtureType, original, original as ILobbyDetails);
            void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
            try
            {
                Set(view,"lobby",fixture);
                Set(fixture,"nearby",true);
                Call(view,"SetMarinaDetail",join,true);yield return null;
                Shot(join,"47-staged-nearby.png");
                Set(fixture,"nearby",false);Set(fixture,"connected",true);Set(fixture,"host",true);
                Call(view,"SetMarinaDetail",host,true);yield return null;
                Shot(host,"48-staged-host-code.png");
                Set(view,"marinaHostOptions",true);yield return null;
                Shot(host,"49-staged-host-options.png");
                Field<List<string>>(fixture,"requests").Add("friend");yield return null;
                Shot(host,"50-staged-host-request.png");
                Set(fixture,"host",false);yield return null;
                Shot(host,"51-staged-guest.png");
                Call(view,"SetMarinaDetail",join,true);
                var warning=Field<RectTransform>(view,"marinaJoinWarningPanel");
                warning.gameObject.SetActive(true);yield return null;
                CaptureTerminal(join,"52-staged-join-confirmation.png");warning.gameObject.SetActive(false);
            }
            finally {Set(view,"lobby",original);Call(view,"CloseMarinaDetail");}
            foreach(string issue in issues)File.AppendAllText(Report,"LAYOUT "+issue+"\n");
            Check(issues.Count==0,"All captured terminal buttons are contained and nonoverlapping");
        }

        static void CaptureTerminal(RectTransform screen,string filename)
        {
            var go=new GameObject("Terminal inspection camera");var camera=go.AddComponent<Camera>();
            camera.enabled=false;camera.fieldOfView=40;camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            float width=screen.rect.width*Mathf.Abs(screen.lossyScale.x),height=screen.rect.height*Mathf.Abs(screen.lossyScale.y);
            bool wide=width/height>1.5f;int w=wide?1800:1100,h=wide?1000:1200;
            camera.aspect=w/(float)h;
            float distance=Mathf.Max(height,width/camera.aspect)*.56f/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            camera.transform.position=screen.position-screen.forward*distance;camera.transform.rotation=screen.rotation;
            var rt=new RenderTexture(w,h,24){antiAliasing=4};var previous=RenderTexture.active;
            Texture2D pixels=null;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                pixels=new Texture2D(w,h,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,w,h),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output,filename),pixels.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(pixels);Object.Destroy(go);}
        }
    }
}
