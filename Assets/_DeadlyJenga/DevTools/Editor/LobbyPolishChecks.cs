using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class LobbyPolishChecks
    {
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
        public static IEnumerator Run(Action<bool,string> check)
        {
            var lobby=IslandLobby.Current;check(lobby,"Lobby exists for actual wardrobe/terminal checks");
            var view=lobby.View();var player=lobby.Player;
            var join=(RectTransform)view.transform.Find("JOIN");var host=(RectTransform)view.transform.Find("HOST");var mode=(RectTransform)view.transform.Find("MAP + MODE");
            check(Mathf.Abs(join.position.y-host.position.y)<.001f&&Mathf.Abs(join.position.y-mode.position.y)<.001f&&Mathf.Abs(join.position.z-mode.position.z)<.001f&&Quaternion.Angle(join.rotation,mode.rotation)<.01f&&Quaternion.Angle(join.rotation,host.rotation)<.01f,"Three terminal centers and monitor faces align horizontally and in parallel");
            var keyboard=(RectTransform)view.GiantKeyboard;
            check(Mathf.Abs(keyboard.position.y-.94f)<.001f&&Mathf.Abs(keyboard.rect.width*keyboard.lossyScale.x-join.rect.width*join.lossyScale.x)<.001f,"Keyboard keeps its height and proportionally matches the JOIN screen width");
            foreach(string name in new[]{"Host controller computer","Mode controller computer"})
            {
                var cabinet=view.transform.Find(name);check(cabinet&&cabinet.GetComponentsInChildren<Collider>().Length==0,"Decorative "+name+" has no Presentation-owned solid colliders");
                var pedestal=cabinet.Find("Grounded computer pedestal");check(pedestal.position.y-pedestal.lossyScale.y*.5f<0,"Computer pedestal extends into the ground: "+name);
            }
            var hostBody=view.transform.Find("Host controller computer");var gameBody=view.transform.Find("Mode controller computer");
            check(hostBody.Find("D-pad horizontal").localPosition.x<0&&hostBody.Find("Scroll wheel").localPosition.x>0,"Host D-pad and wheel occupy opposite console halves");
            check(gameBody.Find("Joystick candy grip").localPosition.x<0&&new[]{"A","B","X","Y"}.All(n=>gameBody.Find("Decorative "+n+" button").localPosition.x>0),"Mode joystick and four colored ABXY buttons occupy opposite console halves");
            Capture(view,new Vector3(0,3.3f,-3.6f),new Vector3(0,1.6f,1),"LobbyPolish-Terminals.png");
            var position=player.transform.position;var rotation=player.transform.rotation;bool paused=player.InputSuspended;bool menu=lobby.MenuOpen;
            try
            {
                player.Place(view.WardrobePosition,Quaternion.identity);player.InputSuspended=paused;view.SetWardrobeOpen(true);yield return null;
                var avatar=player.gameObject.GetComponent<CandyPlayerAvatar>();var showcase=player.gameObject.GetComponent<IAvatarShowcase>();
                check(view.WardrobeOpen&&showcase?.Showcasing==true&&player.InputSuspended,"Opening wardrobe pauses locomotion and opts existing avatar into showcase pose");
                var resting=avatar.ReadFrame();check(resting.left.position.y-resting.feet.y<resting.scale&&resting.right.position.y-resting.feet.y<resting.scale,"Wardrobe begins at full standing height with both arms down");
                typeof(CandyPlayerAvatar).GetField("showcaseStarted",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(avatar,Time.unscaledTime-4.4f);
                var waving=avatar.ReadFrame();check(waving.right.position.y-waving.feet.y>1.5f*waving.scale&&waving.left.position.y-waving.feet.y<waving.scale,"Periodic showcase wave raises one hand and leaves the other relaxed");
                typeof(CandyPlayerAvatar).GetField("showcaseStarted",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(avatar,Time.unscaledTime-6.1f);
                var orbit=Field<CandyOrbitView>(view,"wardrobeOrbit");var camera=Field<Camera>(view,"wardrobeCamera");var eyePosition=player.ViewCamera.transform.position;var before=camera.transform.position;
                orbit.Yaw+=90;orbit.Distance=1.5f;
                for(int i=0;i<15;i++)yield return null;
                check(Vector3.Distance(before,camera.transform.position)>.3f&&Vector3.Distance(eyePosition,player.ViewCamera.transform.position)<.02f,"Smooth shared orbit/zoom moves only the inspection camera");
                var texture=Field<RenderTexture>(view,"wardrobeTexture");Save(texture,"LobbyPolish-Wardrobe.png");
                check(view.GetComponentsInChildren<Text>(true).All(t=>t.text!="ROTATE PREVIEW"&&t.text!="ROTATE"),"Wardrobe no longer offers snap rotation");
                view.SetWardrobeOpen(false);check(!showcase.Showcasing&&player.InputSuspended==paused,"Closing wardrobe restores avatar pose and prior input state");
            }
            finally {view.SetWardrobeOpen(false);player.Place(position,rotation);lobby.SetMenu(menu);player.InputSuspended=paused;}
        }
        static void Capture(IslandLobbyView view,Vector3 position,Vector3 target,string name)
        {
            var eye=Services.Session.Player.ViewCamera;var oldPosition=eye.transform.position;var oldRotation=eye.transform.rotation;
            var go=new GameObject("Temporary lobby polish capture");var camera=go.AddComponent<Camera>();camera.fieldOfView=72;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.transform.position=position;camera.transform.LookAt(target);
            var texture=new RenderTexture(1280,960,24);
            try {eye.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);foreach(var face in view.GetComponentsInChildren<LobbyTerminalFaces>())face.Refresh();camera.targetTexture=texture;camera.Render();Save(texture,name);}
            finally {eye.transform.SetPositionAndRotation(oldPosition,oldRotation);foreach(var face in view.GetComponentsInChildren<LobbyTerminalFaces>())face.Refresh();camera.targetTexture=null;texture.Release();Object.Destroy(texture);Object.Destroy(go);}
        }
        static void Save(RenderTexture texture,string name)
        {
            var previous=RenderTexture.active;Texture2D pixels=null;
            try {RenderTexture.active=texture;pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());}
            finally {RenderTexture.active=previous;if(pixels)Object.Destroy(pixels);}
        }
    }
}
