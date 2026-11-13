using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyEquipment
    {
        readonly WeaponBashJab bashJab = new WeaponBashJab();
        float nextBash;
        ILocalEmoteView bashEmote;
        IMatchRuntime bashViewRuntime;
        string bashEpoch;
        int bashSequence;
        float acceptedBashAt = float.NegativeInfinity;
        EquipmentKind acceptedBashKind;
        bool acceptedBashLeft;

        static bool CanBash(EquipmentState state)
        {
            if (state == null || !state.drawn || state.kind < EquipmentKind.Blaster ||
                state.kind == EquipmentKind.PoweredArmor || EquipmentSpecifications.Get(state.kind).power) return false;
            return !EquipmentSpecifications.Get(state.kind).melee ||
                state.shotgun && (state.kind == EquipmentKind.Chainsaw || state.kind == EquipmentKind.LaserAxe);
        }

        void ResetBashTracking() => bashJab.Reset();

        void CancelBashPresentation()
        {
            acceptedBashAt = float.NegativeInfinity;
            var runtime = MatchRuntime.Current;
            var frame = runtime?.Frame;
            if (frame == null) return;
            bashViewRuntime = runtime;
            bashEpoch = frame.equipmentEpoch;
            bashSequence = frame.equipmentSequence;
        }

        void UpdateWeaponBash(EquipmentState state)
        {
            ReadAcceptedWeaponMotion(state);
            if (!Local || !CanBash(state) || motor.InputSuspended || !motor.HasInputFocus || !motor.Alive ||
                motor.Climbing || motor.Vaulting || motor.CarryingFlag || Services.Session?.MenuOpen == true ||
                state.reloadRemaining > 0 || state.triggerHeld || Time.time - lastAttackTime < .22f)
            {
                ResetBashTracking();
                CancelBashPresentation();
                return;
            }
            bashEmote ??= GetComponent<ILocalEmoteView>();
            if (bashEmote?.EmoteActive == true)
            {
                ResetBashTracking();
                CancelBashPresentation();
                return;
            }

            if (!motor.IsVR)
            {
                ResetBashTracking();
                if ((Cursor.lockState != CursorLockMode.Locked&&DesktopGamepad.Current==null) || Mouse.current?.leftButton.isPressed == true ||
                    Mouse.current?.rightButton.isPressed == true||DesktopGamepad.FireHeld||DesktopGamepad.AimHeld) return;
                var keyboard = Keyboard.current;
                if (keyboard?.tabKey.isPressed == true || keyboard?.vKey.wasPressedThisFrame == true ||
                    keyboard?.rKey.wasPressedThisFrame == true) return;
                if (keyboard?.tKey.wasPressedThisFrame == true||DesktopGamepad.BashPressed)
                    RequestWeaponBash(GripPose.position, avatar.viewCamera.transform.forward, 2);
                return;
            }

            var controller = HybridXRInput.Controller(left);
            var other = HybridXRInput.Controller(!left);
            var hand = avatar.tracking ? avatar.tracking.WorldHand(left) : default;
            if (controller == null || !hand.tracked || ControlHeld(controller, "trigger") || ControlHeld(controller, "grip") ||
                other != null && (ControlHeld(other, "trigger") || ControlHeld(other, "grip")) ||
                ButtonHeld(controller, "primary2DAxisClick") || ButtonHeld(controller, "secondaryButton") ||
                other != null && (ButtonHeld(other, "primary2DAxisClick") || ButtonHeld(other, "secondaryButton")))
            {
                ResetBashTracking();
                CancelBashPresentation();
                return;
            }

            var eye = avatar.viewCamera.transform;
            bool swing = bashJab.Sample(controller.deviceId, left, hand.position, eye.position, eye.forward,
                Scale, Time.time, Time.deltaTime, out var direction, out float speed);
            if (!bashJab.Stable) return;
            bool button = other != null && other.TryGetChildControl<ButtonControl>("primaryButton")?.wasPressedThisFrame == true;
            if (!button && !swing) return;
            RequestWeaponBash(hand.position, button ? eye.forward : direction, button ? 2 : speed);
            bashJab.Consume();
        }

        float primaryKickAt=float.NegativeInfinity,pairedKickAt=float.NegativeInfinity;
        EquipmentKind kickKind;
        float sprintCarryBlend;
        int sprintCarryFrame=-1;

        void ReadAcceptedWeaponMotion(EquipmentState state)
        {
            var runtime = MatchRuntime.Current;
            var frame = runtime?.Frame;
            if (frame == null) return;
            if (!ReferenceEquals(runtime, bashViewRuntime) || frame.equipmentEpoch != bashEpoch)
            {
                bashViewRuntime = runtime;
                bashEpoch = frame.equipmentEpoch;
                bashSequence = frame.equipmentSequence;
                acceptedBashAt = float.NegativeInfinity;
                primaryKickAt=pairedKickAt=float.NegativeInfinity;
                return;
            }
            if (frame.equipmentSequence <= bashSequence) return;
            foreach (var action in frame.equipmentActions ?? System.Array.Empty<EquipmentActionFrame>())
            {
                if (action == null || action.id <= bashSequence || action.actor != state.actor) continue;
                if (action.action == EquipmentAction.Draw || action.action == EquipmentAction.Holster ||
                    action.action == EquipmentAction.Pickup || action.action == EquipmentAction.Swap ||
                    action.action == EquipmentAction.Drop || action.action == EquipmentAction.ModeSwitch ||
                    action.action == EquipmentAction.Reload)
                {
                    acceptedBashAt = float.NegativeInfinity;
                    primaryKickAt=pairedKickAt=float.NegativeInfinity;
                    continue;
                }
                if(action.kind==state.kind&&action.action==EquipmentAction.Shot&&action.remaining>=.55f&&
                    (!EquipmentSpecifications.Get(action.kind).melee||action.shotgun))
                {
                    kickKind=action.kind;
                    if(action.kind==EquipmentKind.DualUzi&&visual!=null&&visual.IsPairedShot(action.position))
                        pairedKickAt=Time.time;
                    else primaryKickAt=Time.time;
                }
                if (action.kind != state.kind || action.action != EquipmentAction.Bash || action.remaining < .40f ||
                    !CanBash(state)) continue;
                acceptedBashAt = Time.time;
                acceptedBashKind = state.kind;
                acceptedBashLeft = state.leftHanded;
            }
            bashSequence = frame.equipmentSequence;
        }

        internal static Vector3 RecoilProfile(EquipmentKind kind,bool alternate) => kind switch
        {
            EquipmentKind.RocketLauncher => new(.106f,11,.30f),
            EquipmentKind.GrenadeLauncher => new(.085f,12,.26f),
            EquipmentKind.SheriffHammer => alternate?new(.085f,17,.27f):new(.072f,14,.24f),
            EquipmentKind.DualRifle => alternate?new(.090f,12,.24f):new(.062f,8,.21f),
            EquipmentKind.ChainGun => new(.023f,2.5f,.10f),
            EquipmentKind.DualUzi => new(.018f,0,.11f),
            EquipmentKind.StockPistol => new(.045f,8,.16f),
            EquipmentKind.NeedleDrill => new(.021f,2.4f,.12f),
            EquipmentKind.WristLaser => new(.013f,1.5f,.10f),
            EquipmentKind.Blaster => new(.039f,6,.19f),
            EquipmentKind.PoweredArmor => new(.025f,4,.16f),
            EquipmentKind.LaserAxe when alternate => new(.065f,8,.25f),
            EquipmentKind.Chainsaw when alternate => new(.052f,6,.21f),
            _ => Vector3.zero
        };

        internal static float RecoilCurve(float seconds,float duration)
        {
            if(seconds<0||seconds>=duration)return 0;
            float t=seconds/duration;
            if(t<.15f)return Mathf.Lerp(.35f,1,t/.15f);
            float settle=Mathf.InverseLerp(.15f,1,t);
            return (1-settle)*(1-settle)-.11f*Mathf.Sin(settle*Mathf.PI*2)*settle;
        }

        Pose PresentedRecoil(Pose grip,EquipmentState state,bool handLeft,bool secondary=false)
        {
            if(state.kind!=kickKind)return grip;
            var profile=RecoilProfile(state.kind,state.shotgun);
            float impulse=RecoilCurve(Time.time-(secondary?pairedKickAt:primaryKickAt),profile.z);
            if(impulse==0)return grip;
            float strength=impulse*(motor.IsVR?.24f:1);
            float side=handLeft?-1:1;
            var shift=new Vector3(side*.004f,profile.x*.15f,-profile.x)*(strength*Scale);
            return new Pose(grip.position+grip.rotation*shift,
                grip.rotation*Quaternion.Euler(-profile.y*strength,0,side*profile.y*.12f*strength));
        }

        Pose PresentedSprintGrip(Pose grip,EquipmentState state,CandyAvatarFrame frame,bool handLeft)
        {
            bool vr=motor.IsVR;
            bool input=Local&&(vr?trigger?.IsPressed()==true||aimBlend>.05f:
                (Mouse.current?.leftButton.isPressed==true||DesktopGamepad.FireHeld)||(Mouse.current?.rightButton.isPressed==true||DesktopGamepad.AimHeld));
            bool shooting=state.triggerHeld||Time.time-Mathf.Max(primaryKickAt,pairedKickAt)<.24f||input;
            bool sprint=frame.sprinting&&!shooting&&state.reloadRemaining<=0;
            // Paired fire needs both weapon axes settled as soon as the trigger is pressed.
            if(shooting&&state.kind==EquipmentKind.DualUzi){sprintCarryBlend=0;return grip;}
            if(sprintCarryFrame!=Time.frameCount)
            {
                sprintCarryFrame=Time.frameCount;
                sprintCarryBlend=Mathf.MoveTowards(sprintCarryBlend,sprint?1:0,Time.deltaTime*(sprint?6:15));
            }
            if(sprintCarryBlend<=0)return grip;

            var spec=EquipmentSpecifications.Get(state.kind);
            bool heavy=spec.twoHanded||state.kind==EquipmentKind.Chainsaw;
            bool melee=spec.melee&&!state.shotgun;
            float side=handLeft?-1:1;
            float phase=frame.hasGaitPhase?frame.gaitPhase:Time.time*13;
            float sway=Mathf.Sin(phase),bounce=Mathf.Cos(phase*2);
            var offset=new Vector3(side*(.045f+sway*.023f),
                (melee?-.045f:-.092f)+(heavy?.020f:.031f)*bounce,-.070f);
            var rotation=Quaternion.Euler((melee?-9:heavy?16:28)+bounce*3,
                side*(heavy?6:12)+sway*3,side*(heavy?7:15)+sway*4);
            float weight=Mathf.SmoothStep(0,1,sprintCarryBlend)*(vr?.13f:1);
            return new Pose(grip.position+grip.rotation*(offset*(Scale*weight)),
                grip.rotation*Quaternion.Slerp(Quaternion.identity,rotation,weight));
        }

        Pose PresentedBashGrip(Pose pose, EquipmentState state)
        {
            float t = (Time.time - acceptedBashAt) / .34f;
            if (motor.IsVR || !CanBash(state) || state.reloadRemaining > 0 || t < 0 || t >= 1 ||
                state.kind != acceptedBashKind || state.leftHanded != acceptedBashLeft) return pose;
            bool heavy = EquipmentSpecifications.Get(state.kind).twoHanded;
            float side = state.leftHanded ? -1 : 1;
            float windup = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .42f, t));
            float strike = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .42f, t)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.47f, 1, t)));
            var offset = new Vector3(side * .08f * strike, .025f * strike, -.055f * windup + .16f * strike);
            var twist = Quaternion.Euler(-12 * windup, side * ((heavy ? -88 : 62) * strike - 15 * windup),
                side * (heavy ? -14 : 24) * strike);
            return new Pose(pose.position + pose.rotation * offset * Scale, pose.rotation * twist);
        }

        static bool ControlHeld(UnityEngine.InputSystem.XR.XRController controller, string name) =>
            controller.TryGetChildControl<AxisControl>(name)?.ReadValue() > .18f;

        static bool ButtonHeld(UnityEngine.InputSystem.XR.XRController controller, string name) =>
            controller.TryGetChildControl<ButtonControl>(name)?.isPressed == true;

        bool RequestWeaponBash(Vector3 origin, Vector3 direction, float speed)
        {
            if (Time.time < nextBash) return false;
            if (MatchRuntime.Current?.RequestPunch(gameObject, origin, direction, speed) != true) return false;
            nextBash = Time.time + .65f;
            return true;
        }
    }
}
