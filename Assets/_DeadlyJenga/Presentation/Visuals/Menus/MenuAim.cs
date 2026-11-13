using System;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    internal static class MenuAim
    {
        static int consumedFrame=-1;
        static MaterialPropertyBlock beamColor;
        static Material foreground,beamMaterial;
        static readonly List<Graphic> graphics=new();
        internal static void PresentInForeground(Transform root)
        {
            if (!foreground)
            {
                var shader=Resources.Load<Shader>("CandyScenery/CandyMenuForeground");
                if (!shader) return;
                foreground=new Material(shader) { name="Foreground headset menu",hideFlags=HideFlags.DontSave };
            }
            root.GetComponentsInChildren(true,graphics);
            foreach(var graphic in graphics)
                if(graphic.material==graphic.defaultMaterial) graphic.material=foreground;
            graphics.Clear();
        }
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static bool InputConsumed=>consumedFrame==Time.frameCount;
        public static void Consume()=>consumedFrame=Time.frameCount;

        internal static void PulsePress(int deviceId)
        {
            if (deviceId <= 0 || !(InputSystem.GetDeviceById(deviceId) is XRController controller) ||
                !controller.isTracked.isPressed) return;
            foreach (var usage in controller.usages)
            {
                if (usage != CommonUsages.LeftHand && usage != CommonUsages.RightHand) continue;
                DeadlyJenga.CandyAudio.CandyFeedback.MenuPress(usage == CommonUsages.LeftHand);
                return;
            }
        }
        public static Ray Resolve(Camera eye,Transform fallbackSpace,bool preferredLeft,bool leftPressed,bool rightPressed,out bool controller,out bool left)
        {
            left=leftPressed!=rightPressed?leftPressed:preferredLeft;
            controller=PhysicalPose(eye,left,out var pose,out _,fallbackSpace);
            if(!controller)
            {
                left=!left;
                controller=PhysicalPose(eye,left,out pose,out _,fallbackSpace);
            }
            if(controller)return new Ray(pose.position,pose.rotation*Vector3.forward);

            left=leftPressed!=rightPressed?leftPressed:preferredLeft;
            if(TryHandPointer(eye,Services.Session?.Player,left,out var hand))return hand;
            left=!left;
            return TryHandPointer(eye,Services.Session?.Player,left,out hand)
                ?hand:new Ray(eye.transform.position,eye.transform.forward);
        }

        internal static bool TriggerHeld(XRController device)
        {
            var button=device.TryGetChildControl<ButtonControl>("triggerPressed");
            var axis=device.TryGetChildControl<AxisControl>("trigger");
            return button?.isPressed==true||axis!=null&&axis.ReadValue()>.65f;
        }

        internal static InputAction SelectAction(string name,bool left)
        {
            string hand=left?"LeftHand":"RightHand";
            var action=new InputAction(name,InputActionType.Button,$"<XRController>{{{hand}}}/trigger");
            AddSelectBindings(action,left);
            return action;
        }

        internal static void AddSelectBindings(InputAction action,bool left)
        {
            string hand=left?"LeftHand":"RightHand";
            action.AddBinding($"<XRController>{{{hand}}}/triggerPressed");
            action.AddBinding($"<HandInteraction>{{{hand}}}/pinchValue");
        }
        internal static Ray ResolveSelect(Camera eye,Transform space,bool preferredLeft,
            InputAction left,InputAction right,bool gesturePressed,out bool pressed,out bool tracked)
        {
            bool leftPressed=left.WasPressedThisFrame(),rightPressed=right.WasPressedThisFrame();
            var ray=Resolve(eye,space,preferredLeft,false,false,out bool controller,out _);
            tracked=controller||IsTrackedPointer(eye,ray);
            if(!leftPressed&&!rightPressed)
            {
                pressed=!controller&&tracked&&gesturePressed;
                return ray;
            }
            var action=(leftPressed&&(!rightPressed||preferredLeft))?left:right;
            pressed=TryActionPointer(eye,action,out var actionRay,out _,space);
            if(!pressed)return ray;
            tracked=true;
            return actionRay;
        }

        internal static bool IsTrackedPointer(Camera eye, Ray ray) =>
            (eye.transform.position - ray.origin).sqrMagnitude > .01f;

        internal static bool TryHandPointer(Camera eye, IPlayableCharacter player, bool left, out Ray ray)
        {
            ray = default;
            var rig = eye.GetComponentInParent<XROrigin>();
            var space = rig && rig.CameraFloorOffsetObject ? rig.CameraFloorOffsetObject.transform : eye.transform.parent;
            if (space)
            {
                foreach (var device in InputSystem.devices)
                {
                    if (device is not XRController hand || !hand.isTracked.isPressed ||
                        hand.layout.IndexOf("HandInteraction", StringComparison.OrdinalIgnoreCase) < 0 ||
                        !hand.usages.Contains(left ? CommonUsages.LeftHand : CommonUsages.RightHand)) continue;
                    var position = hand.TryGetChildControl<Vector3Control>("pointerPosition");
                    var rotation = hand.TryGetChildControl<QuaternionControl>("pointerRotation");
                    if (position == null || rotation == null) continue;
                    var origin = space.TransformPoint(position.ReadValue());
                    var orientation = rotation.ReadValue();
                    float norm = Quaternion.Dot(orientation, orientation);
                    if (!(norm > .5f && norm < 1.5f) ||
                        !((origin - eye.transform.position).sqrMagnitude < 2.25f)) continue;
                    ray = new Ray(origin, space.TransformDirection(orientation.normalized * Vector3.forward));
                    return true;
                }
            }
            if (player?.HasHandTracking != true || !(player is IModeActor actor)) return false;
            var fingers = player.gameObject.GetComponent<IFingerTips>();
            if (fingers == null || !fingers.TryIndexTip(left, out var tip)) return false;
            var palm = actor.HandPose(left).position;
            var direction = tip - palm;
            if (!(direction.sqrMagnitude > .0025f && direction.sqrMagnitude < .16f) ||
                !((tip - eye.transform.position).sqrMagnitude < 2.25f)) return false;
            ray = new Ray(tip, direction.normalized);
            return true;
        }

        internal static void PresentBeam(ref LineRenderer beam, Transform owner, Ray ray, Vector3 end, bool shown, bool actionable)
        {
            if (!shown)
            {
                if (beam) beam.enabled = false;
                return;
            }
            if (!beam)
            {
                if (!beamMaterial)
                {
                    var shader=Resources.Load<Shader>("CandyScenery/CandyHeadsetOverlay");
                    if(!shader)return;
                    beamMaterial=new Material(shader){name="Foreground UI ray",hideFlags=HideFlags.DontSave};
                }
                beam = new GameObject("Tracked UI ray").AddComponent<LineRenderer>();
                beam.transform.SetParent(owner, false);
                beam.sharedMaterial = beamMaterial;
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = .006f;
                beam.endWidth = .003f;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
                beam.sortingOrder = 550;
            }
            beam.enabled = true;
            beam.sortingOrder = 550;
            beamColor ??= new MaterialPropertyBlock();
            beamColor.SetColor(BaseColor, actionable ? CandyHudVisuals.Mint : Color.white);
            beam.SetPropertyBlock(beamColor);
            beam.SetPosition(0, ray.GetPoint(.025f));
            beam.SetPosition(1, end);
        }

        public static bool PhysicalPose(Camera eye,bool left,out Pose pose,out XRController selected,Transform fallbackSpace=null)
        {
            pose=default;selected=null;
            var rig=eye.GetComponentInParent<XROrigin>();
            var space=rig&&rig.CameraFloorOffsetObject?rig.CameraFloorOffsetObject.transform:fallbackSpace?fallbackSpace:eye.transform.parent;
            if(!space)return false;
            foreach(var device in InputSystem.devices)
            {
                if(!(device is XRController controller)||!controller.isTracked.isPressed||
                    device.layout.IndexOf("HandInteraction",StringComparison.OrdinalIgnoreCase)>=0)continue;
                bool matches=false;
                foreach(var usage in device.usages)matches|=usage==(left?CommonUsages.LeftHand:CommonUsages.RightHand);
                if(!matches)continue;
                var position=device.TryGetChildControl<Vector3Control>("pointerPosition")??controller.devicePosition;
                var rotation=device.TryGetChildControl<QuaternionControl>("pointerRotation")??controller.deviceRotation;
                var origin=space.TransformPoint(position.ReadValue());var orientation=space.rotation*rotation.ReadValue();
                float norm=Quaternion.Dot(orientation,orientation);
                if(!((origin-eye.transform.position).sqrMagnitude<9)||!(norm>.5f&&norm<1.5f))continue;
                pose=new Pose(origin,orientation.normalized);selected=controller;return true;
            }
            return false;
        }
        internal static bool TryActionPointer(Camera eye, InputAction action, out Ray ray, out int deviceId, Transform fallbackSpace=null)
        {
            ray = default;
            deviceId = 0;
            if (action.activeControl?.device is not XRController controller || !controller.isTracked.isPressed) return false;
            bool left = false;
            foreach (var usage in controller.usages) left |= usage == CommonUsages.LeftHand;
            if (controller.layout.IndexOf("HandInteraction", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if(!TryHandPointer(eye,Services.Session?.Player,left,out ray))return false;
                deviceId=controller.deviceId;
                return true;
            }
            if (!PhysicalPose(eye, left, out var pose, out var selected, fallbackSpace) || selected != controller) return false;
            ray = new Ray(pose.position, pose.rotation * Vector3.forward);
            deviceId = selected.deviceId;
            return true;
        }

        public static int PointerIdentity(Camera eye,bool preferredLeft)
        {
            if(PhysicalPose(eye,preferredLeft,out _,out var device)||PhysicalPose(eye,!preferredLeft,out _,out device))return device.deviceId;
            return -1;
        }
        public static void PlaceTablet(RectTransform panel, Camera eye, ISessionInfo session, bool dominantLeft, ref bool lobbyPoseSet)
        {
            bool fixedPose = session is ILobbyControls;
            if (fixedPose && lobbyPoseSet) return;
            PlaceTablet(panel, eye, session, dominantLeft, fixedPose);
            lobbyPoseSet = fixedPose;
        }
        public static void PlaceTablet(RectTransform panel,Camera eye,ISessionInfo session,bool dominantLeft, bool fixedPose = false)
        {
            bool holderLeft=!dominantLeft;
            Pose anchor = default;
            bool held = !fixedPose && PhysicalPose(eye, holderLeft, out anchor, out _);
            var tips=session?.Player?.gameObject.GetComponent<IFingerTips>();
            if(!fixedPose&&!held&&session?.Player?.HasHandTracking==true&&tips!=null&&tips.TryIndexTip(holderLeft,out _)&&session.Player is IModeActor actor)
            {
                anchor=actor.HandPose(holderLeft);
                float distance=(anchor.position-eye.transform.position).sqrMagnitude;
                held=distance>.01f&&distance<3;
            }
            bool dual=held&&PhysicalPose(eye,dominantLeft,out _,out _);
            float depth=1.05f,lateral=0,height=-.06f;
            if(dual)
            {
                var offset=anchor.position-eye.transform.position;
                depth=Mathf.Clamp(Vector3.Dot(offset,eye.transform.forward)+.50f,.95f,1.35f);
                lateral=Mathf.Clamp(Vector3.Dot(offset,eye.transform.right),-.28f,.28f);
                height=Mathf.Clamp(Vector3.Dot(offset,eye.transform.up)+.16f,-.22f,.12f);
            }
            float width=Mathf.Max(1,panel.rect.width),panelHeight=Mathf.Max(1,panel.rect.height);
            float vertical=Mathf.Clamp(eye.fieldOfView,20,150)*Mathf.Deg2Rad*.5f;
            float horizontal=Mathf.Atan(Mathf.Tan(vertical)*Mathf.Max(.2f,eye.aspect));
            float halfWidth=Mathf.Min(20*Mathf.Deg2Rad,horizontal*.84f);
            float halfHeight=Mathf.Atan(Mathf.Tan(halfWidth)*panelHeight/width);
            if(halfHeight>vertical*.84f)
            {
                halfHeight=vertical*.84f;
                halfWidth=Mathf.Atan(Mathf.Tan(halfHeight)*width/panelHeight);
            }
            float horizontalInset=Mathf.Max(0,horizontal*.9f-halfWidth);
            float verticalInset=Mathf.Max(0,vertical*.9f-halfHeight);
            lateral=Mathf.Clamp(lateral,-depth*Mathf.Tan(horizontalInset),depth*Mathf.Tan(horizontalInset));
            height=Mathf.Clamp(height,-depth*Mathf.Tan(verticalInset),depth*Mathf.Tan(verticalInset));
            Vector3 position=eye.transform.position+eye.transform.forward*depth+eye.transform.right*lateral+eye.transform.up*height;
            Vector3 forward=position-eye.transform.position;
            if(forward.sqrMagnitude<.01f)return;
            float scale=2*forward.magnitude*Mathf.Tan(halfWidth)/width;
            bool initialized=!fixedPose&&panel.localScale.x>.0001f&&panel.localScale.x<.0018f;
            float blend=initialized?1-Mathf.Exp(-20*Time.unscaledDeltaTime):1;
            panel.SetPositionAndRotation(Vector3.Lerp(panel.position,position,blend),Quaternion.Slerp(panel.rotation,Quaternion.LookRotation(forward.normalized,eye.transform.up),blend));
            panel.localScale=Vector3.Lerp(panel.localScale,Vector3.one*scale,blend);
        }
        public static bool TryPoint(Ray ray,RectTransform target,float range,out Vector2 local,out Vector3 hit)
        {
            local=default;hit=default;
            if(Vector3.Dot(ray.direction,target.forward)<=.01f||!new Plane(target.forward,target.position).Raycast(ray,out float distance)||distance>range)return false;
            hit=ray.GetPoint(distance);local=target.InverseTransformPoint(hit);return target.rect.Contains(local);
        }
        public static Image CreateReticle(Transform parent)
        {
            var image=new GameObject("Menu aim reticle",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent,false);image.rectTransform.sizeDelta=new Vector2(14,14);
            image.sprite=CandyHudVisuals.Pill;image.raycastTarget=false;image.gameObject.SetActive(false);return image;
        }
        public static void Present(Image dot,Vector3 point,bool shown,bool actionable)
        {
            if(!dot)return;dot.gameObject.SetActive(shown);if(!shown)return;
            dot.transform.position=point-dot.transform.forward*.0005f;dot.color=actionable?CandyHudVisuals.Mint:Color.white;
        }
    }

}
