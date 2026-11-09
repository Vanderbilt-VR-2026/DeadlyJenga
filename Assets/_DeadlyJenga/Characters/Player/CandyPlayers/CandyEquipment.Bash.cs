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
            ReadAcceptedBashes(state);
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
                if (Cursor.lockState != CursorLockMode.Locked || Mouse.current?.leftButton.isPressed == true ||
                    Mouse.current?.rightButton.isPressed == true) return;
                var keyboard = Keyboard.current;
                if (keyboard?.tabKey.isPressed == true || keyboard?.vKey.wasPressedThisFrame == true ||
                    keyboard?.rKey.wasPressedThisFrame == true) return;
                if (keyboard?.tKey.wasPressedThisFrame == true)
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

        void ReadAcceptedBashes(EquipmentState state)
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
                    continue;
                }
                if (action.kind != state.kind || action.action != EquipmentAction.Bash || action.remaining < .40f ||
                    !CanBash(state)) continue;
                acceptedBashAt = Time.time;
                acceptedBashKind = state.kind;
                acceptedBashLeft = state.leftHanded;
            }
            bashSequence = frame.equipmentSequence;
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
