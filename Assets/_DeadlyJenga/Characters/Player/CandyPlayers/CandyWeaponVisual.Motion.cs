using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        const string FeedAssembly = "Animated ammunition assembly";
        Transform feed, pairedFeed, embeddedTarget;
        Transform primaryRoundFeed, secondaryRoundFeed;
        Transform[] primaryRounds, secondaryRounds;
        float drawAmount, contactUntil, lastContactRemaining;
        string lastContactVictim;
        CandyAvatarWardrobe suitWardrobe;
        bool motionReady, lastRoleIssued, lastLeft, hasCarriedFrame;
        CandyAvatarFrame lastCarriedFrame;
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
            hasCarriedFrame = false;
            lastRoleIssued = false;
            wasEmbedded = false;
            contactUntil = lastContactRemaining = 0;
            lastContactVictim = null;
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
            bool contacting = state.drawn && !state.shotgun && state.embedRemaining > 0 &&
                !string.IsNullOrEmpty(state.embeddedVictim);
            if (contacting && (!wasEmbedded || state.embeddedVictim != lastContactVictim ||
                state.embedRemaining > lastContactRemaining + .04f))
            {
                var delta = state.embeddedPoint - handPose.position;
                var direction = delta.sqrMagnitude > .01f ? delta.normalized : handPose.rotation * Vector3.forward;
                CaptureEmbed(state.embeddedPoint, direction, state.embeddedVictim);
                contactUntil = Time.time + Mathf.Max(.12f, state.embedRemaining);
            }
            wasEmbedded = contacting;
            lastContactRemaining = state.embedRemaining;
            lastContactVictim = state.embeddedVictim;
            float remaining = contactUntil - Time.time;
            if (remaining <= 0 || !state.drawn || state.shotgun) return handPose;

            float tip = kind switch
            {
                EquipmentKind.Dagger => .34f,
                EquipmentKind.MurderSword => .91f,
                EquipmentKind.SawStaff => .96f,
                EquipmentKind.Chainsaw => .89f,
                EquipmentKind.LaserClaws => .28f,
                EquipmentKind.FlamingMarshmallow => .49f,
                _ => .93f
            };
            var rotation = embeddedTarget ? embeddedTarget.rotation * embeddedRotation : embeddedWorldRotation;
            var point = embeddedTarget ? embeddedTarget.TransformPoint(embeddedPoint) : embeddedWorldPoint;
            embeddedWorldPoint = point;
            embeddedWorldRotation = rotation;
            float release = Mathf.SmoothStep(0, 1, remaining / .07f);
            var position = point - rotation * Vector3.forward * ((tip - .11f) * scale);
            return new Pose(Vector3.Lerp(handPose.position, position, release),
                Quaternion.Slerp(handPose.rotation, rotation, release));
        }

        float CarryDuration(bool drawing)
        {
            if (kind == EquipmentKind.LaserClaws || kind == EquipmentKind.WristLaser || kind == EquipmentKind.PoweredArmor)
                return .17f;
            bool back = EquipmentSpecifications.Get(kind).backHolster || kind == EquipmentKind.FlamingMarshmallow;
            return back ? (drawing ? .46f : .42f) : (drawing ? .29f : .33f);
        }

        internal void PoseDesktopCarryHand(ref CandyAvatarFrame frame, EquipmentState state, float deltaTime)
        {
            if (!motionReady || !root || kind != state.kind || kind == EquipmentKind.LaserClaws ||
                kind == EquipmentKind.WristLaser || kind == EquipmentKind.PoweredArmor) return;

            bool left = state.leftHanded;
            float progress = Mathf.MoveTowards(drawAmount, state.drawn ? 1 : 0,
                deltaTime / CarryDuration(state.drawn));
            if (progress <= 0) return;
            if (progress >= 1)
            {
                PoseDesktopReloadHand(ref frame, state);
                return;
            }

            var hand = left ? frame.left : frame.right;
            var grip = CarryPose(frame, hand, left, progress, state.drawn);
            float reach = Ease(.015f, .18f, progress);
            var contact = hand;
            if (!TryPrimaryContact(ref contact, grip, kind, left, false, frame.scale))
            {
                contact.position = grip.position;
                contact.rotation = grip.rotation;
                if (MeleeMount(kind, left, out var palm, out var mount))
                {
                    contact.rotation *= Quaternion.Inverse(mount);
                    contact.position -= contact.rotation * palm * frame.scale;
                    contact.skeleton = ShaftGrip(kind, left);
                }
                else contact.skeleton = CandyHandSkeleton.Rest(left, .96f);
            }
            var starting = hand.skeletonTracked ? hand.skeleton : CandyHandSkeleton.Rest(left);
            hand.position = Vector3.Lerp(hand.position, contact.position, reach);
            hand.rotation = Quaternion.Slerp(hand.rotation, contact.rotation, reach);
            hand.skeleton = BlendGrip(starting, contact.skeleton, reach);
            hand.skeletonTracked = true;
            if (left) frame.left = hand; else frame.right = hand;
        }

        static CandyHandSkeleton BlendGrip(CandyHandSkeleton start, CandyHandSkeleton target, float weight)
        {
            if (weight <= 0) return start;
            if (weight >= 1) return target;
            var result = start;
            for (int finger = 0; finger < 5; finger++)
            {
                int root = finger * 4;
                Vector3 point = Vector3.Lerp(start.Joint(root), target.Joint(root), weight);
                result.SetJoint(root, point);
                for (int joint = root; joint < root + 3; joint++)
                {
                    var from = start.Joint(joint + 1) - start.Joint(joint);
                    var to = target.Joint(joint + 1) - target.Joint(joint);
                    point += Vector3.Slerp(from, to, weight);
                    result.SetJoint(joint + 1, point);
                }
            }
            return result;
        }

        void PoseDesktopReloadHand(ref CandyAvatarFrame frame, EquipmentState state)
        {
            if (!state.drawn || !feed || state.reloadRemaining <= 0 ||
                kind == EquipmentKind.DualUzi || kind == EquipmentKind.WristLaser || kind == EquipmentKind.LaserAxe)
                return;
            var spec = EquipmentSpecifications.Get(kind);
            if (spec.reloadSeconds <= 0 || spec.melee && !state.shotgun) return;
            float phase = 1 - Mathf.Clamp01(state.reloadRemaining / spec.reloadSeconds);
            float reach = Ease(0, .15f, phase) * (1 - Ease(.82f, 1, phase));
            bool looseRounds = kind == EquipmentKind.SheriffHammer || kind == EquipmentKind.GrenadeLauncher;
            if (!looseRounds) reach *= 1 - Ease(.31f,.38f,phase) + Ease(.48f,.57f,phase);
            bool supportLeft = !state.leftHanded;
            var hand = supportLeft ? frame.left : frame.right;
            var offset = kind switch
            {
                EquipmentKind.SheriffHammer => new Vector3(0, .042f, .04f),
                EquipmentKind.GrenadeLauncher => new Vector3(0, .025f, .08f + GrenadeDrumOffset),
                EquipmentKind.ChainGun => new Vector3(.23f, -.31f, .12f),
                EquipmentKind.RocketLauncher => new Vector3(0, -.038f, .14f),
                EquipmentKind.StockPistol => new Vector3(0, -.044f, .23f),
                EquipmentKind.Chainsaw => new Vector3(0, .23f, .20f),
                _ => new Vector3(0, -.055f, -.13f)
            };
            float side = supportLeft ? -1 : 1;
            hand.position = Vector3.Lerp(hand.position, feed.TransformPoint(offset + Vector3.right * (side * .05f)), reach);
            hand.rotation = Quaternion.Slerp(hand.rotation, feed.rotation * Quaternion.Euler(12, side * 32, side * 80), reach);
            hand.skeleton = CandyHandSkeleton.ControllerGrip(supportLeft, reach * .78f, reach * .5f);
            hand.skeletonTracked = true;
            if (supportLeft) frame.left = hand; else frame.right = hand;
        }

        Pose CarryPose(CandyAvatarFrame frame, CandyHandPose hand, bool left, float progress, bool drawing)
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

            if (!drawing && hasCarriedFrame)
            {
                var previousYaw = Quaternion.Euler(0, lastCarriedFrame.headRotation.eulerAngles.y, 0);
                var currentYaw = Quaternion.Euler(0, frame.headRotation.eulerAngles.y, 0);
                var delta = currentYaw * Quaternion.Inverse(previousYaw);
                var previous = left ? lastCarriedFrame.left : lastCarriedFrame.right;
                hand.position = frame.head + delta * (previous.position - lastCarriedFrame.head);
                hand.rotation = delta * previous.rotation;
            }

            var drawn = DrawnPose(kind, hand, left, frame.scale);
            if (kind == EquipmentKind.LaserClaws || kind == EquipmentKind.WristLaser || progress >= 1) return drawn;

            var yaw = Quaternion.Euler(0, frame.headRotation.eulerAngles.y, 0);
            var holstered = Holster(kind, frame.feet, frame.head, yaw, frame.scale, left);
            bool back = EquipmentSpecifications.Get(kind).backHolster || kind == EquipmentKind.FlamingMarshmallow;
            float side = left ? -1 : 1;
            float storedSide = back ? 1 : side;
            var clear = holstered.position + yaw * new Vector3(storedSide * (back ? .30f : .13f),
                back ? .48f : .19f, back ? -.015f : .05f) * frame.scale;
            var present = drawn.position + yaw * new Vector3(side * .09f, -.07f, .055f) * frame.scale;
            // Clear the holster, bring the weapon around the body, then settle the grip.
            if (!drawing)
            {
                clear += yaw * new Vector3(storedSide * .045f, -.045f, -.035f) * frame.scale;
                present += yaw * new Vector3(side * .045f, -.075f, .02f) * frame.scale;
            }
            float t = Mathf.SmoothStep(0, 1, progress);
            float u = 1 - t;
            var position = u*u*u*holstered.position + 3*u*u*t*clear +
                3*u*t*t*present + t*t*t*drawn.position;
            var guide = yaw * Quaternion.Euler(back ? -37 : 43, storedSide * (back ? 65 : 27),
                storedSide * (drawing ? 25 : 43));
            var rotation = Quaternion.Slerp(Quaternion.Slerp(holstered.rotation, guide, t),
                Quaternion.Slerp(guide, drawn.rotation, t), t);
            return new Pose(position, rotation);
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

            float side = left ? -1 : 1;
            Vector3 travel = kind switch
            {
                EquipmentKind.SheriffHammer => new Vector3(side * .14f, -.045f, 0),
                EquipmentKind.GrenadeLauncher => new Vector3(side * .28f, -.065f, 0),
                EquipmentKind.ChainGun => new Vector3(.28f, -.19f, 0),
                EquipmentKind.NeedleDrill => new Vector3(0, -.09f, -.24f),
                EquipmentKind.RocketLauncher => new Vector3(0, -.32f, -.08f),
                EquipmentKind.Chainsaw => Vector3.up * .23f,
                _ => Vector3.down * .24f
            };
            bool cylinder = kind == EquipmentKind.SheriffHammer || kind == EquipmentKind.GrenadeLauncher;
            bool energy = kind == EquipmentKind.WristLaser || kind == EquipmentKind.LaserAxe;
            bool oldFeed = phase < .40f;
            float open = Ease(.08f, .30f, phase) * (1 - Ease(.78f, .98f, phase));
            assembly.gameObject.SetActive(energy || cylinder || !reloading || phase < .38f || phase >= .48f);
            if (energy)
            {
                assembly.localPosition = Vector3.zero;
                assembly.localRotation = Quaternion.identity;
                return;
            }
            if (cylinder)
            {
                assembly.localPosition = travel * open;
                float chamberStep = kind == EquipmentKind.GrenadeLauncher ? -60 : 60;
                assembly.localRotation = Quaternion.Euler(0, 29 * side * open,
                    (spec.magazine - Mathf.Clamp(state.rounds, 0, spec.magazine)) * chamberStep *
                    (state.magazineInitialized && !reloading ? 1 : 0));
                // Spin about the cylinder axis instead of the weapon origin.
                var axis = kind == EquipmentKind.SheriffHammer ? new Vector3(0,.072f,.12f) :
                    new Vector3(0,GrenadeDrumHeight,.12f+GrenadeDrumOffset);
                assembly.localPosition += axis - assembly.localRotation * axis;
            }
            else
            {
                float eject = Ease(.07f, .37f, phase);
                float insert = Ease(.51f, .94f, phase);
                assembly.localPosition = oldFeed
                    ? travel * eject + Vector3.down * (.16f * eject * eject)
                    : travel * (1 - insert) + Vector3.right * (side * .105f * (1 - insert));
                assembly.localRotation = oldFeed
                    ? Quaternion.Euler(-32 * eject, 0, side * 16 * eject)
                    : Quaternion.Euler(14 * (1 - insert), side * 17 * (1 - insert), 0);
            }

            PresentRounds(assembly, state, secondary, cylinder, reloading, phase);
        }

        static float Ease(float from, float to, float phase) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, phase));

        void PresentRounds(Transform assembly, EquipmentState state, bool secondary, bool cylinder, bool reloading, float phase)
        {
            int count = kind switch
            {
                EquipmentKind.SheriffHammer or EquipmentKind.GrenadeLauncher or EquipmentKind.ChainGun => 6,
                EquipmentKind.RocketLauncher => 5,
                EquipmentKind.StockPistol or EquipmentKind.DualUzi or EquipmentKind.NeedleDrill => 5,
                EquipmentKind.Chainsaw => 4,
                _ => 0
            };
            if (count == 0) return;

            var rounds = secondary ? secondaryRounds : primaryRounds;
            if ((secondary ? secondaryRoundFeed : primaryRoundFeed) != assembly)
            {
                rounds = new Transform[count];
                var body = new CandyAccessoryMeshes.Builder();
                var tip = new CandyAccessoryMeshes.Builder();
                float radius = kind == EquipmentKind.GrenadeLauncher ? .034f :
                    kind == EquipmentKind.RocketLauncher ? .022f : .011f;
                float length = kind == EquipmentKind.GrenadeLauncher ? .21f :
                    kind == EquipmentKind.RocketLauncher ? .075f : .038f;
                if (kind == EquipmentKind.Chainsaw)
                {
                    body.Cone(Vector3.zero, Vector3.forward * .014f, .023f, .023f, 12);
                    for (int tooth = 0; tooth < 8; tooth++)
                    {
                        float a = tooth * Mathf.PI / 4;
                        var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                        tip.Cone(radial * .02f, radial * .029f + Vector3.forward * .008f, .006f, .001f, 4);
                    }
                }
                else
                {
                    body.Cone(Vector3.zero, Vector3.forward * (length * .78f), radius, radius * .92f, 10);
                    tip.Cone(Vector3.forward * (length * .78f), Vector3.forward * length, radius * .92f,
                        kind == EquipmentKind.GrenadeLauncher ? radius * .75f : radius * .18f, 10);
                }
                for (int i = 0; i < count; i++)
                {
                    var item = new GameObject("Visible round " + (i + 1)).transform;
                    item.SetParent(assembly, false);
                    CachedMesh(item, "Round casing " + kind, body, new Color(.76f,.44f,.14f));
                    CachedMesh(item, "Round tip " + kind, tip,
                        kind == EquipmentKind.DualUzi ? new Color(.35f,1,.10f) :
                        kind == EquipmentKind.GrenadeLauncher ? new Color(.30f,.33f,.24f) :
                        kind == EquipmentKind.RocketLauncher ? new Color(.29f,.33f,.28f) : new Color(.85f,.72f,.50f));
                    rounds[i] = item;
                }
                if (secondary) { secondaryRoundFeed = assembly; secondaryRounds = rounds; }
                else { primaryRoundFeed = assembly; primaryRounds = rounds; }
            }

            int capacity = EquipmentSpecifications.Get(kind).magazine;
            int remaining = state.magazineInitialized ? Mathf.Clamp(state.rounds, 0, capacity) : capacity;
            if (kind == EquipmentKind.DualUzi)
                remaining = capacity / 2 - (capacity - remaining + (secondary ? 0 : 1)) / 2;

            for (int i = 0; i < rounds.Length; i++)
            {
                float insert = Ease(.34f + i * .064f, .48f + i * .064f, phase);
                bool loading = cylinder && reloading && phase >= .34f + i * .064f;
                float extract = Ease(.17f, .34f, phase);
                var item = rounds[i];
                bool loaded = kind == EquipmentKind.GrenadeLauncher ? i >= count - remaining : i < remaining;
                bool spentCase = kind == EquipmentKind.SheriffHammer && !loaded &&
                    reloading && phase >= .17f && extract < 1;
                item.localScale = spentCase ? new Vector3(1,1,.65f) : Vector3.one;
                item.gameObject.SetActive(cylinder
                    ? (loading || spentCase || loaded && (!reloading || extract < 1))
                    : (reloading && phase >= .48f || loaded));
                float angle = Mathf.PI / 2 + i * Mathf.PI / 3;
                var seat = kind switch
                {
                    EquipmentKind.SheriffHammer => new Vector3(Mathf.Cos(angle)*.048f,.072f+Mathf.Sin(angle)*.048f,.035f),
                    EquipmentKind.GrenadeLauncher => new Vector3(Mathf.Cos(angle)*GrenadeChamberRadius,GrenadeDrumHeight+Mathf.Sin(angle)*GrenadeChamberRadius,.018f+GrenadeDrumOffset),
                    EquipmentKind.ChainGun => new Vector3(.165f+i*.023f,ChainGunBarrelHeight+.105f,.18f),
                    EquipmentKind.RocketLauncher => new Vector3(.106f,.044f,-.111f+i*.081f),
                    EquipmentKind.StockPistol => new Vector3(.040f,-.017f+i*.017f,.209f),
                    EquipmentKind.DualUzi => new Vector3(.038f,-.204f+i*.020f,-.012f),
                    EquipmentKind.Chainsaw => new Vector3(.025f,.221f,.095f+i*.064f),
                    _ => new Vector3(.058f,.038f,-.175f+i*.028f)
                };
                if (kind == EquipmentKind.ChainGun && !reloading && remaining > 0)
                    seat.z += Mathf.Sin(remaining * 1.8f) * .011f;
                item.localPosition = seat + (cylinder && reloading
                    ? Vector3.back * (loading ? (1-insert)*.18f : extract*.21f) +
                        Vector3.down * (loading ? (1-insert)*.045f : extract*extract*.16f)
                    : Vector3.zero);
                item.localRotation = kind == EquipmentKind.Chainsaw ? Quaternion.Euler(90,0,0) : Quaternion.identity;
            }
        }
    }
}
