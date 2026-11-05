using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        const string FeedAssembly = "Animated ammunition assembly";
        Transform feed, pairedFeed, embeddedTarget;
        string actionEpoch;
        int actionSequence;
        IMatchRuntime actionRuntime;
        float drawAmount, daggerUntil;
        CandyAvatarWardrobe suitWardrobe;
        bool motionReady, lastRoleIssued, lastLeft;
        Vector3 embeddedPoint, embeddedWorldPoint;
        Quaternion embeddedRotation, embeddedWorldRotation;

        static Transform MakeFeedAssembly(Transform owner)
        {
            var assembly = new GameObject(FeedAssembly).transform;
            assembly.SetParent(owner, false);
            return assembly;
        }

        static void MountFeedMesh(Mesh mesh, Material material, Transform owner)
        {
            if (!mesh) return;
            var part = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(owner, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        void ResetMotion()
        {
            motionReady = false;
            lastRoleIssued = false;
            wasEmbedded = false;
            daggerUntil = 0;
        }

        void ReadMeleeActions(EquipmentState state, Pose pose)
        {
            var runtime = MatchRuntime.Current;
            var match = runtime?.Frame;
            if (match == null || string.IsNullOrEmpty(state.actor)) return;
            if (!ReferenceEquals(runtime, actionRuntime) || actionEpoch != match.equipmentEpoch)
            {
                actionRuntime = runtime;
                actionEpoch = match.equipmentEpoch;
                actionSequence = match.equipmentSequence;
                return;
            }

            if (match.equipmentSequence <= actionSequence) return;
            if (state.drawn && EquipmentSpecifications.Get(kind).melee)
            {
                foreach (var shot in match.equipmentActions ?? Array.Empty<EquipmentActionFrame>())
                {
                    if (shot == null || shot.id <= actionSequence || shot.actor != state.actor ||
                        shot.kind != kind || shot.action != EquipmentAction.Shot || shot.shotgun ||
                        shot.remaining <= 0 || shot.endpoints == null || shot.endpoints.Length == 0) continue;

                    var travel = shot.endpoints[0] - shot.position;
                    var strikeDirection = travel.sqrMagnitude > .0001f ? travel.normalized : pose.rotation * Vector3.forward;
                    if (kind != EquipmentKind.Dagger || travel.magnitude >= EquipmentSpecifications.Get(kind).range - .015f) continue;

                    // Play the brief hold on receipt so snapshot latency cannot erase it.
                    if (shot.remaining < .40f) continue;
                    daggerUntil = Time.time + .19f;
                    CaptureEmbed(shot.endpoints[0], strikeDirection, null);
                }
            }
            actionSequence = match.equipmentSequence;
        }

        void CaptureEmbed(Vector3 point, Vector3 direction, string victim)
        {
            embeddedTarget = string.IsNullOrEmpty(victim) ? null : MatchRuntime.Current?.FindParticipantRoot(victim);
            embeddedRotation = Quaternion.LookRotation(direction);
            embeddedWorldPoint = point;
            embeddedWorldRotation = embeddedRotation;
            embeddedPoint = embeddedTarget ? embeddedTarget.InverseTransformPoint(point) : point;
            if (embeddedTarget) embeddedRotation = Quaternion.Inverse(embeddedTarget.rotation) * embeddedRotation;
        }

        Pose EmbedPose(EquipmentState state, Pose handPose, float scale)
        {
            bool heavy = state.drawn && !state.shotgun && state.embedRemaining > 0;
            if (heavy && !wasEmbedded)
            {
                var delta = state.embeddedPoint - handPose.position;
                var direction = delta.sqrMagnitude > .01f ? delta.normalized : handPose.rotation * Vector3.forward;
                CaptureEmbed(state.embeddedPoint, direction, state.embeddedVictim);
            }
            wasEmbedded = heavy;
            float remaining = heavy ? state.embedRemaining : kind == EquipmentKind.Dagger ? daggerUntil - Time.time : 0;
            if (remaining <= 0 || !state.drawn) return handPose;

            float tip = kind switch
            {
                EquipmentKind.Dagger => .34f,
                EquipmentKind.SawStaff => .96f,
                EquipmentKind.Chainsaw => .89f,
                _ => .93f
            };
            var rotation = embeddedTarget ? embeddedTarget.rotation * embeddedRotation : embeddedWorldRotation;
            var point = embeddedTarget ? embeddedTarget.TransformPoint(embeddedPoint) : embeddedWorldPoint;
            embeddedWorldPoint = point;
            embeddedWorldRotation = rotation;
            float release = Mathf.SmoothStep(0, 1, remaining / .07f);
            var position = point - rotation * Vector3.forward * (tip * scale);
            return new Pose(Vector3.Lerp(handPose.position, position, release),
                Quaternion.Slerp(handPose.rotation, rotation, release));
        }

        Pose CarryPose(CandyAvatarFrame frame, CandyHandPose hand, bool left, float progress)
        {
            if (kind == EquipmentKind.PoweredArmor)
            {
                if (!suitWardrobe) suitWardrobe = parent.GetComponentInChildren<CandyAvatarWardrobe>();
                var emitter = suitWardrobe ? (left ? suitWardrobe.LeftPalmEmitter : suitWardrobe.RightPalmEmitter) : null;
                if (emitter)
                    return new Pose(emitter.position + emitter.forward * (.009f * frame.scale), emitter.rotation);
                return new Pose(hand.position + hand.rotation * new Vector3(0, -.04f, .025f) * frame.scale,
                    hand.rotation * Quaternion.Euler(90, 0, 0));
            }

            var drawn = DrawnPose(kind, hand, left, frame.scale);
            if (kind == EquipmentKind.LaserClaws || kind == EquipmentKind.WristLaser || progress >= 1) return drawn;

            var yaw = Quaternion.Euler(0, frame.headRotation.eulerAngles.y, 0);
            var holstered = Holster(kind, frame.feet, frame.head, yaw, frame.scale, left);
            float t = Mathf.SmoothStep(0, 1, progress);
            float clearance = Mathf.Sin(t * Mathf.PI);
            bool back = EquipmentSpecifications.Get(kind).backHolster || kind == EquipmentKind.FlamingMarshmallow;
            var lift = yaw * new Vector3((left && !back ? -1 : 1) * (back ? .29f : .17f),
                back ? .37f : .14f, back ? -.10f : .09f) * (clearance * frame.scale);
            return new Pose(Vector3.Lerp(holstered.position, drawn.position, t) + lift,
                Quaternion.Slerp(holstered.rotation, drawn.rotation, t));
        }

        static void PresentRepulsor(Transform weapon, float progress)
        {
            float open = Mathf.SmoothStep(0, 1, progress);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                var shutter = weapon.GetChild(i);
                shutter.localPosition = new Vector3(side * Mathf.Lerp(.013f, .032f, open), 0, -.006f * open);
                shutter.localRotation = Quaternion.Euler(0, side * 58 * open, 0);
            }
        }

        void PresentClaws(Transform weapon, CandyHandPose hand, bool left, float progress)
        {
            weapon.gameObject.SetActive(progress > 0);
            PositionClaws(weapon, hand, left);
            float extension = Mathf.Lerp(.02f, 1, Mathf.SmoothStep(0, 1, progress));
            for (int i = 0; i < 3; i++)
                weapon.GetChild(i).localScale = new Vector3(1, 1, extension);
        }

        void PresentFeed(Transform assembly, EquipmentState state, bool left, bool secondary)
        {
            if (!assembly) return;
            var spec = EquipmentSpecifications.Get(kind);
            bool reloading = state.drawn && state.reloadRemaining > 0 && spec.reloadSeconds > 0 &&
                (!spec.melee || state.shotgun);
            float phase = reloading ? 1 - Mathf.Clamp01(state.reloadRemaining / spec.reloadSeconds) : 0;
            if (kind == EquipmentKind.DualUzi)
                phase = Mathf.Clamp01((phase - (secondary ? .28f : 0)) / .72f);

            float open = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.09f, .35f, phase)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.56f, .85f, phase)));
            float side = left ? -1 : 1;
            Vector3 travel = kind switch
            {
                EquipmentKind.SheriffHammer => new Vector3(side * .15f, -.045f, 0),
                EquipmentKind.GrenadeLauncher => new Vector3(side * .30f, -.065f, 0),
                EquipmentKind.ChainGun => new Vector3(.28f, -.19f, 0),
                EquipmentKind.NeedleDrill => new Vector3(0, -.09f, -.24f),
                EquipmentKind.WristLaser => Vector3.up * .15f,
                EquipmentKind.RocketLauncher => new Vector3(0, -.32f, -.08f),
                EquipmentKind.Chainsaw or EquipmentKind.LaserAxe => Vector3.up * .23f,
                _ => Vector3.down * .24f
            };
            bool chamber = kind == EquipmentKind.SheriffHammer || kind == EquipmentKind.GrenadeLauncher;
            // The old magazine drops clear. The replacement enters along the same rail.
            assembly.gameObject.SetActive(chamber || phase <= .38f || phase >= .53f);
            assembly.localPosition = travel * open;
            assembly.localRotation = Quaternion.Euler(chamber ? 0 : -13 * open, chamber ? 12 * side * open : 0, 0);
        }
    }
}
