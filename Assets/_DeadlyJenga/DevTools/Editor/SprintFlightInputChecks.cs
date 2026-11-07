using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.CandyPlayers;
using FlyingHand;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace DeadlyJenga.DevTools.Editor
{
    public static class SprintFlightInputChecks
    {
        // Advance from the existing actual PlayerLoop driver, not EditorApplication.update.
        // This checks installed layout bindings, not locomotion or Quest hardware.
        public static IEnumerator CheckBindings(Action<bool,string> check)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(!GameplayBuffer())
            {check(false,"Sprint bindings require actual Play Mode, focus and gameplay input buffer");yield break;}
            var added=new List<InputDevice>();var layouts=new List<string>();
            var types=new[]{typeof(OculusTouchControllerProfile.OculusTouchController),typeof(MetaQuestTouchPlusControllerProfile.QuestTouchPlusController)};
            try
            {
                foreach(var type in types)
                {
                    string layout=type.Name;
                    if(!InputSystem.ListLayouts().Contains(layout))
                    {InputSystem.RegisterLayout(type,layout);layouts.Add(layout);}
                    var controller=InputSystem.AddDevice(layout) as XRController;
                    if(controller==null)throw new InvalidOperationException("Installed layout is not an XRController: "+layout);
                    added.Add(controller);
                    // No hand usage or tracked-state injection: gameplay cannot select these as tracked hands.
                    var click=controller.TryGetChildControl<ButtonControl>("thumbstickClicked");
                    check(click!=null,layout+" has its real installed thumbstickClicked control");
                    if(click==null)yield break;
                    check(controller.TryGetChildControl<ButtonControl>("primary2DAxisClick")==null,
                        layout+" reproduces missing legacy child-name lookup; usage is not a child alias");
                    QueueClick(controller,click,0);yield return null;
                    if(!GameplayBuffer()){check(false,layout+" neutral observation lost gameplay focus/buffer");yield break;}
                    check(!HybridXRInput.MoveClickHeld(controller)&&!HybridXRInput.MoveClickPressed(controller),layout+" starts neutral");
                    QueueClick(controller,click,1);yield return null;
                    if(!GameplayBuffer()){check(false,layout+" press observation lost gameplay focus/buffer");yield break;}
                    check(HybridXRInput.MoveClickHeld(controller)&&HybridXRInput.MoveClickPressed(controller),layout+" fresh queued click is pressed and held in gameplay buffer");
                    yield return null;
                    if(!GameplayBuffer()){check(false,layout+" held observation lost gameplay focus/buffer");yield break;}
                    check(HybridXRInput.MoveClickHeld(controller)&&!HybridXRInput.MoveClickPressed(controller),layout+" held click does not repeatedly press");
                    QueueClick(controller,click,0);yield return null;
                    if(!GameplayBuffer()){check(false,layout+" release observation lost gameplay focus/buffer");yield break;}
                    check(!HybridXRInput.MoveClickHeld(controller)&&!HybridXRInput.MoveClickPressed(controller),layout+" queued release clears held and pressed");
                }
                check(FlyingHandController.FilterControllerLift(.01f,0)==0&&FlyingHandController.FilterControllerLift(0,.01f)==0,
                    "Actual motor lift filter treats .01 controller noise as neutral in either direction");
                check(FlyingHandController.FilterControllerLift(.12f,0)==0&&FlyingHandController.FilterControllerLift(0,.12f)==0,
                    "Actual motor lift filter treats .12 deadzone boundary as neutral");
                check(FlyingHandController.FilterControllerLift(.5f,0)>0&&FlyingHandController.FilterControllerLift(0,.5f)<0,
                    "Actual motor lift filter preserves significant .5 ascent and descent");
            }
            finally
            {
                foreach(var device in added)if(device.added)InputSystem.RemoveDevice(device);
                foreach(var layout in layouts)
                {
                    bool foreign=InputSystem.devices.Any(device=>device.layout==layout);
                    if(!foreign)InputSystem.RemoveLayout(layout);
                    check(!foreign,"Owned layout "+layout+" removed unless a new unowned device now requires it");
                }
                check(added.All(device=>!device.added),"Only owned test controllers removed; no runtime input hooks installed");
            }
        }
        static bool GameplayBuffer()=>Application.isPlaying&&Application.isFocused&&InputState.currentUpdateType!=InputUpdateType.Editor;
        static void QueueClick(InputDevice device,ButtonControl click,float value)
        {
            using(var state=StateEvent.From(device,out var input))
            {click.WriteValueIntoEvent(value,input);InputSystem.QueueEvent(input);}
        }
    }
}
