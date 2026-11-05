using System;
using System.Collections;
using System.IO;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Called explicitly from an owned Game Play Mode runner, once per source version.
    public static class HandAppearancePopupChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static CharacterPicker Picker()=>Object.FindFirstObjectByType<CharacterPicker>(FindObjectsInactive.Include);
        static Canvas Panel(CharacterPicker picker)=>picker?picker.GetComponentInChildren<Canvas>(true):null;
        static void Tick(CharacterPicker picker)
        {if(picker)typeof(CharacterPicker).GetMethod("LateUpdate",Private).Invoke(picker,null);}
        static bool Visible()=>Panel(Picker())?.gameObject.activeInHierarchy==true;

        public static IEnumerator Run(ISessionInfo session,bool expectFixed,string output,Action<bool,string> check)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Actual Game Play Mode required.");
            Directory.CreateDirectory(output);
            var motor=session.Player.gameObject.GetComponent<CastleSurvivor>();
            check(motor!=null,"Real survivor supplies the role and camera fixture");
            var oldMode=motor.controlMode;
            bool oldRole=session.PlayingHand,oldMenu=session.MenuOpen;
            var settings=session.Player as ISurvivorSettings;
            var oldRules=settings?.CurrentRules;
            var hand=session.Player.gameObject.GetComponent<IBlasterControls>();
            bool oldLeft=hand?.LeftHanded??false;
            try
            {
                session.SetRole(false);
                motor.controlMode=FlyingHand.ControlMode.VR;
                motor.RefreshMode();
                session.SetMenu(false);
                yield return null;
                var camera=session.ActiveCamera;
                var savedPosition=camera.transform.position;var savedRotation=camera.transform.rotation;
                try
                {
                    camera.transform.rotation=Quaternion.Euler(0,0,0);
                    session.SetMenu(true);
                    var picker=Picker();
                    Tick(picker);
                    if(!expectFixed)
                    {
                        check(Visible(),"BEFORE any VR menu opens hand appearance without a customization request");
                        var panel=Panel(picker);
                        var forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
                        var delta=Vector3.ProjectOnPlane(panel.transform.position-camera.transform.position,Vector3.up).normalized;
                        check(Mathf.Abs(Vector3.SignedAngle(forward,delta,Vector3.up)-42)<.5f,"BEFORE popup sits 42 degrees to the right");
                        Capture(camera,Path.Combine(output,"HandAppearance-Before-AnyMenu.png"));
                        session.SetMenu(false);Tick(picker);
                        session.SetMenu(true);Tick(picker);
                        check(Visible(),"BEFORE ordinary menu reopening repeats the unsolicited popup");
                        yield break;
                    }
                    check(!Visible(),"Ordinary VR menu does not open hand appearance");
                    var open=typeof(CharacterPicker).GetMethod("Open",BindingFlags.Public|BindingFlags.Static);
                    check(open!=null,"Explicit Hand Appearance action exists");
                    foreach(bool left in new[]{false,true})
                    {
                        hand?.SetLeftHanded(left);
                        foreach(bool standing in new[]{false,true})
                        {
                            if(standing)settings?.UseStandingPreset();else settings?.UseStationaryPreset();
                            camera.transform.SetPositionAndRotation(savedPosition,Quaternion.Euler(34,23,16));
                            open.Invoke(null,new object[]{session});
                            picker=Picker();Tick(picker);
                            var panel=Panel(picker);
                            check(Visible(),"Explicit appearance opens for "+(left?"left":"right")+" hand / "+(standing?"standing":"seated"));
                            check(Mathf.Abs(panel.transform.position.y-camera.transform.position.y)<.002f&&
                                Vector3.Dot(panel.transform.up,Vector3.up)>.999f,"Panel opens centered at eye height without head roll");
                            var position=panel.transform.position;var rotation=panel.transform.rotation;
                            camera.transform.position+=new Vector3(.18f,.13f,.02f);
                            camera.transform.Rotate(8,15,11);Tick(picker);
                            check(panel.transform.position==position&&panel.transform.rotation==rotation,"Open panel stays anchored while the head moves");
                            open.Invoke(null,new object[]{session});Tick(picker);
                            check(Object.FindObjectsByType<CharacterPicker>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1&&panel.transform.position==position,"Repeated explicit open creates no duplicate or reposition");
                            camera.transform.SetPositionAndRotation(savedPosition,Quaternion.Euler(0,23,0));
                            if(!left&&!standing)Capture(camera,Path.Combine(output,"HandAppearance-After-Explicit.png"));
                            session.SetMenu(false);Tick(picker);
                            check(!Visible(),"Menu closure hides appearance");
                            session.SetMenu(true);Tick(picker);
                            check(!Visible(),"Ordinary menu reopening leaves appearance closed");
                        }
                    }
                    open.Invoke(null,new object[]{session});Tick(Picker());
                    var showing=typeof(CharacterPicker).GetMethod("Showing",BindingFlags.Public|BindingFlags.Static);
                    var lifecycle=Picker();lifecycle.enabled=false;
                    check(!Visible()&&!(bool)showing.Invoke(null,new object[]{session}),"Disabling picker clears modal ownership and its visible panel");
                    lifecycle.enabled=true;Tick(lifecycle);
                    check(!Visible(),"Reenabling picker does not reopen it");
                    open.Invoke(null,new object[]{session});Tick(lifecycle);
                    check(Visible(),"An explicit open works after component reenable");
                    if(hand!=null)
                    {
                        hand.SetLeftHanded(!hand.LeftHanded);Tick(lifecycle);
                        check(!Visible(),"Changing effective hand closes the old pointer context");
                    }
                    open.Invoke(null,new object[]{session});Tick(Picker());
                    session.SetRole(true);Tick(Picker());
                    check(!Visible(),"Role or active-camera transition closes the old appearance panel");
                    session.SetRole(false);session.SetMenu(true);Tick(Picker());
                    check(!Visible(),"Returning to survivor does not reopen appearance");
                }
                finally
                {
                    if(camera)camera.transform.SetPositionAndRotation(savedPosition,savedRotation);
                }
            }
            finally
            {
                if(hand!=null)hand.SetLeftHanded(oldLeft);
                if(settings!=null&&oldRules!=null)settings.ApplyMatchRules(oldRules);
                motor.controlMode=oldMode;motor.RefreshMode();
                session.SetMenu(false);Tick(Picker());
                session.SetRole(oldRole);session.SetMenu(oldMenu);
            }
        }
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1600,1100,24);
            var previous=RenderTexture.active;var previousTarget=camera.targetTexture;
            bool xr=camera.GetUniversalAdditionalCameraData().allowXRRendering;
            Texture2D image=null;
            try
            {
                camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previousTarget;RenderTexture.active=previous;
                camera.GetUniversalAdditionalCameraData().allowXRRendering=xr;
                target.Release();Object.Destroy(target);
                if(image)Object.Destroy(image);
            }
        }
    }
}

