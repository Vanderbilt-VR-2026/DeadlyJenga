using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class DeveloperUiChecks
    {
        static Type AimType=>typeof(MenuPointer).Assembly.GetType("DeadlyJenga.Presentation.MenuAim");
        static object Aim(string name,params object[] args)=>AimType.GetMethod(name,Flags).Invoke(null,args);
        static IEnumerator PointerAuditChecks()
        {
            File.AppendAllText(Report,"Pointer audit: production UI routes and scripted rays; synthetic occlusion render. No physical XR or independent peer evidence.\n");
            var sequence=LobbyTerminalChecks();while(sequence.MoveNext())yield return sequence.Current;
            sequence=WardrobeLayoutChecks();while(sequence.MoveNext())yield return sequence.Current;
            var lobby=(IslandLobby)Services.Session;
            var motor=(CastleSurvivor)lobby.Player;motor.controlMode=FlyingHand.ControlMode.Desktop;motor.RefreshMode();
            var view=lobby.GetComponent<IslandLobbyView>();
            motor.Place(view.WardrobePosition,Quaternion.identity);view.SetWardrobeOpen(true);
            Canvas.ForceUpdateCanvases();
            var buttons=Field<System.Collections.Generic.List<Tuple<RectTransform,Action>>>(view,"wardrobeButtons");
            var button=buttons.First(b=>b.Item1.gameObject.activeInHierarchy).Item1;
            var screen=RectTransformUtility.WorldToScreenPoint(null,button.position);
            Call(view,"PaintWardrobePointer",screen,false);
            Check(button.GetComponent<Outline>().enabled,"Desktop wardrobe shows hover rim without changing selection");
            Call(view,"PaintWardrobePointer",screen,true);
            Check(button.GetComponent<Outline>().effectColor==CandyHudVisuals.Peach,"Desktop wardrobe press has distinct feedback");
            Call(view,"PaintWardrobePointer",new Vector2(-10000,-10000),false);
            Check(!button.GetComponent<Outline>().enabled,"Wardrobe hover clears when pointer leaves");
            view.SetWardrobeOpen(false);motor.Place(new Vector3(0,.2f,-8),Quaternion.identity);
            BeamRenderChecks();
            GameExperiencePanel.Resolve(lobby)?.SetExperience(GameExperience.Demo);
            lobby.SelectMode("demo");lobby.SelectMap("castle");
            var setup=JsonUtility.FromJson<LobbySetup>(JsonUtility.ToJson(((ILobbyDetails)lobby).Setup));
            setup.population=1;setup.hands=0;setup.rules.automaticDisasters=false;((ILobbyDetails)lobby).ConfigureSetup(setup);
            lobby.StartOrCancel();
            yield return TerminalUntil(()=>Services.Session is CastlePlaySession s&&s.Modes&&s.Player!=null,"Pointer audit enters actual match");
            sequence=Checks();while(sequence.MoveNext())yield return sequence.Current;
            SessionState.SetBool(Key+"Tabs",true);
            sequence=Checks();while(sequence.MoveNext())yield return sequence.Current;
        }
        static void TabletPointerChecks(CastleWorldMenu menu,Camera camera,CastlePlaySession session)
        {
            menu.Show(true,camera);
            var rect=(RectTransform)menu.transform;
            var ray=new Ray(rect.position-rect.forward*2,rect.forward);
            typeof(CastleWorldMenu).GetField("pointerRay",Flags).SetValue(menu,ray);
            typeof(CastleWorldMenu).GetField("usingController",Flags).SetValue(menu,false);
            Call(menu,"PresentPointer");
            Check(Field<Image>(menu,"pointerDot").gameObject.activeSelf,"Desktop tablet has a visible reticle on its panel");
            Check(!Field<LineRenderer>(menu,"pointerBeam")||!Field<LineRenderer>(menu,"pointerBeam").enabled,"Desktop tablet never displays a VR beam");
            Call(menu,"SetInputFocused",false);menu.Point(camera,true,true);
            Check(!Field<Image>(menu,"pointerDot").gameObject.activeSelf,"Focus loss clears tablet pointer and cannot redraw it");
            Call(menu,"SetInputFocused",true);
            typeof(CastleWorldMenu).GetField("pointerRay",Flags).SetValue(menu,new Ray(ray.origin,-ray.direction));Call(menu,"PresentPointer");
            Check(!Field<Image>(menu,"pointerDot").gameObject.activeSelf,"Tablet reticle clears when pointing away");
            menu.Show(false,camera);
            Check(!Field<Image>(menu,"pointerDot").gameObject.activeSelf,"Closing tablet clears pointer");
        }
        static void BeamRenderChecks()
        {
            var root=new GameObject("Pointer occlusion fixture");root.transform.position=Vector3.one*10000;
            var camera=root.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;camera.aspect=1.6f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;
            camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var target=new RenderTexture(640,400,24);var pixels=new Texture2D(640,400,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;Material obstacleMaterial=null;
            try
            {
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(root.transform,false);cube.transform.localPosition=new Vector3(0,0,1.5f);cube.transform.localScale=new Vector3(3,1.8f,.1f);cube.layer=30;
                obstacleMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));obstacleMaterial.SetColor("_BaseColor",Color.red);cube.GetComponent<Renderer>().sharedMaterial=obstacleMaterial;
                var panel=new GameObject("Foreground UI occluder",typeof(RectTransform),typeof(Canvas),typeof(Image));panel.transform.SetParent(root.transform,false);panel.layer=30;
                var rect=(RectTransform)panel.transform;rect.sizeDelta=new Vector2(3,1.8f);rect.localPosition=new Vector3(0,0,2);
                var canvas=panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=2000;panel.GetComponent<Image>().color=Color.red;
                Aim("PresentInForeground",panel.transform);
                var ray=new Ray(root.transform.position+new Vector3(-1,0,3),Vector3.right);var end=ray.GetPoint(2);
                object[] args={null,root.transform,ray,end,true,true};Aim("PresentBeam",args);var beam=(LineRenderer)args[0];beam.gameObject.layer=30;
                Check(beam.enabled&&beam.sortingOrder>canvas.sortingOrder,"Shared beam sorts above foreground UI");
                Check(beam.sharedMaterial.shader.name=="Candy Tropical/Headset Overlay","Shared beam uses depth-independent overlay shader");
                camera.targetTexture=target;
                int Render(string file)
                {
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,400),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Output,file),pixels.EncodeToPNG());
                    return pixels.GetPixels().Count(c=>c.g>.65f&&c.r<.65f&&c.b>.3f);
                }
                Check(Render("90-beam-over-geometry-and-ui.png")>80,"Rendered beam stays visible behind opaque geometry and foreground UI");
                args[4]=false;Aim("PresentBeam",args);
                Check(!beam.enabled&&Render("91-beam-hidden.png")==0,"Hidden beam leaves no rendered pixels");
                args[4]=true;root.SetActive(false);Aim("PresentBeam",args);Check(!beam.enabled,"Inactive owner cannot leave a visible beam");root.SetActive(true);
                object[] pointArgs={new Ray(rect.position-Vector3.forward,Vector3.forward),rect,4f,null,null};
                Check((bool)Aim("TryPoint",pointArgs),"Panel center accepts pointer");
                pointArgs[0]=new Ray(rect.position+new Vector3(4,0,-1),Vector3.forward);Check(!(bool)Aim("TryPoint",pointArgs),"Outside panel rejects infinite-plane pointer");
                panel.SetActive(false);pointArgs[0]=new Ray(rect.position-Vector3.forward,Vector3.forward);Check(!(bool)Aim("TryPoint",pointArgs),"Inactive panel rejects pointer");
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(pixels);Object.Destroy(obstacleMaterial);Object.Destroy(root);}
        }
    }
}
