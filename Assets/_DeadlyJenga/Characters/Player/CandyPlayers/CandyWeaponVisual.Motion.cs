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
        float pickupStarted = float.NegativeInfinity;
        Vector3 pickupPoint;
        bool worldPickup;
        Transform loadCell, loadCellOwner;
        float drawAmount, contactUntil, lastContactRemaining;
        Transform rocketCandy;
        MeshRenderer rocketCandyHead, rocketCandySeam;
        MaterialPropertyBlock rocketCandyTint;
        int shownCandyVariant;
        static Material rocketSugarMaterial, rocketStickMaterial;
        string lastContactVictim;
        int lastWheelCount=-1;
        float wheelFeedAt=float.NegativeInfinity;
        Vector3 wheelSeat;
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
            pickupStarted = float.NegativeInfinity;
            motionReady = false;
            hasCarriedFrame = false;
            lastRoleIssued = false;
            wasEmbedded = false;
            contactUntil = lastContactRemaining = 0;
            lastContactVictim = null;
            lastWheelCount = -1;
            wheelFeedAt = float.NegativeInfinity;
        }

        void PresentWheelFeed(EquipmentState state)
        {
            bool launcher = state.shotgun;
            int count = state.magazineInitialized ? state.rounds : EquipmentSpecifications.Get(kind).magazine;
            if (!launcher)
            {
                lastWheelCount = -1;
                wheelFeedAt = float.NegativeInfinity;
            }
            else
            {
                if (lastWheelCount >= 0 && count != lastWheelCount) wheelFeedAt = Time.time;
                lastWheelCount = count;
            }

            float age = Time.time - wheelFeedAt;
            bool loaded = !launcher || (count > 0 && age >= .085f);
            rotor.gameObject.SetActive(loaded);
            float feed = launcher ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.085f, .23f, age)) : 1;
            rotor.localPosition = wheelSeat - Vector3.forward * (.18f * (1-feed));
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

        float CarryDuration(bool drawing) => kind switch
        {
            EquipmentKind.WristLaser => .26f,
            EquipmentKind.LaserClaws => drawing ? .42f : .36f,
            EquipmentKind.PoweredArmor => drawing ? .54f : .40f,
            EquipmentKind.BoxingGlove => drawing ? .44f : .48f,
            EquipmentKind.Dagger or EquipmentKind.StockPistol or EquipmentKind.SheriffHammer => drawing ? .29f : .36f,
            EquipmentKind.DualUzi => drawing ? .40f : .44f,
            EquipmentKind.ChainGun or EquipmentKind.RocketLauncher => drawing ? .67f : .58f,
            EquipmentKind.GrenadeLauncher or EquipmentKind.DualRifle or EquipmentKind.Chainsaw => drawing ? .55f : .49f,
            EquipmentKind.MurderSword or EquipmentKind.BarbedBat or EquipmentKind.LaserAxe or
                EquipmentKind.SawStaff or EquipmentKind.FlamingMarshmallow => drawing ? .56f : .53f,
            _ => drawing ? .36f : .40f
        };

        float PickupDuration => kind switch
        {
            EquipmentKind.ChainGun or EquipmentKind.RocketLauncher or EquipmentKind.GrenadeLauncher or
                EquipmentKind.PoweredArmor => .69f,
            EquipmentKind.BoxingGlove or EquipmentKind.LaserClaws => .56f,
            EquipmentKind.DualRifle or EquipmentKind.MurderSword or EquipmentKind.BarbedBat or
                EquipmentKind.Chainsaw or EquipmentKind.LaserAxe => .59f,
            _ => .47f
        };

        void BeginPickup(EquipmentActionFrame action, CandyAvatarFrame frame)
        {
            pickupStarted = Time.time - Mathf.Clamp(.8f - action.remaining, 0, .20f);
            pickupPoint = action.position;
            // Grant events originate at eye height. Nearby world pickups can lift from their real position.
            worldPickup = (pickupPoint - frame.head).sqrMagnitude >
                .20f * .20f * frame.scale * frame.scale;
            drawAmount = 1;
        }

        Pose PosePickup(CandyAvatarFrame frame, CandyHandPose hand, bool left, Pose ready, bool secondary = false)
        {
            float t = (Time.time - pickupStarted) / PickupDuration - (secondary ? .12f : 0);
            if (t >= 1 || !float.IsFinite(pickupStarted)) return ready;
            var flat = Quaternion.Euler(0, frame.headRotation.eulerAngles.y, 0);
            float side = left ? -1 : 1;
            var lift = flat * new Vector3(side * .15f, .13f, .05f) * frame.scale;
            var gather = ready.position + flat * new Vector3(side * .12f, -.31f, .11f) * frame.scale;
            if (worldPickup && !secondary)
                gather = ready.position + Vector3.ClampMagnitude(pickupPoint - ready.position, .62f * frame.scale);
            if (kind == EquipmentKind.WristLaser)
                gather = hand.position + flat * new Vector3(side * .09f, -.095f, -.045f) * frame.scale;
            bool trackedWrist = hand.tracked && Services.Session?.IsVR == true;
            if (trackedWrist)
            {
                gather = ready.position;
                lift *= .11f;
            }
            if (kind == EquipmentKind.BoxingGlove && !trackedWrist)
            {
                gather = Vector3.Lerp(gather, hand.position - hand.rotation * Vector3.forward * .16f, .68f);
                lift = hand.rotation * new Vector3(side * .04f,.044f,.06f) * frame.scale;
            }
            var inspect = ready.position + lift;
            var pitch = ready.rotation * Quaternion.Euler(-24, side * 22, side * 28);
            var start = flat * Quaternion.Euler(kind == EquipmentKind.WristLaser ? 15 : 37, side * 24, side * 18);
            if (trackedWrist)
            {
                start = ready.rotation;
                pitch = ready.rotation * Quaternion.Euler(-7, side * 4, side * 6);
            }
            float gatherWeight = Ease(0, .59f, t);
            float settle = Ease(.46f, 1, t);
            return new Pose(Vector3.Lerp(Vector3.Lerp(gather, inspect, gatherWeight), ready.position, settle),
                Quaternion.Slerp(Quaternion.Slerp(start, pitch, gatherWeight), ready.rotation, settle));
        }

        internal void PoseDesktopCarryHand(ref CandyAvatarFrame frame, EquipmentState state, float deltaTime)
        {
            if (!motionReady || !root || kind != state.kind || kind == EquipmentKind.LaserClaws ||
                kind == EquipmentKind.PoweredArmor) return;

            bool left = state.leftHanded;
            float progress = Mathf.MoveTowards(drawAmount, state.drawn ? 1 : 0,
                deltaTime / CarryDuration(state.drawn));
            if (progress <= 0) return;
            bool pickingUp = state.drawn && Time.time - pickupStarted < PickupDuration;
            if (!pickingUp && (progress >= 1 || kind == EquipmentKind.WristLaser))
            {
                PoseDesktopReloadHand(ref frame, state);
                return;
            }

            var hand = left ? frame.left : frame.right;
            var grip = CarryPose(frame, hand, left, progress, state.drawn);
            float reach = pickingUp
                ? Ease(0, .24f, (Time.time - pickupStarted) / PickupDuration) *
                    (1 - Ease(.80f, 1, (Time.time - pickupStarted) / PickupDuration))
                : Ease(.015f, .18f, progress);
            if (pickingUp)
                grip = PosePickup(frame, hand, left, grip);
            var contact = hand;
            if (!TryPrimaryContact(ref contact, grip, kind, left, false, frame.scale))
            {
                contact.position = grip.position;
                contact.rotation = grip.rotation;
                if (MeleeMount(kind, left, out var palm, out var mount))
                {
                    contact.rotation *= Quaternion.Inverse(mount);
                    contact.position -= contact.rotation * palm * ModelScale(kind, frame.scale);
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
            if (!state.drawn || state.reloadRemaining <= 0 || kind == EquipmentKind.RocketLauncher || kind == EquipmentKind.WristLaser) return;
            var spec = EquipmentSpecifications.Get(kind);
            if (spec.reloadSeconds <= 0 || spec.melee && !state.shotgun) return;
            float phase = 1 - Mathf.Clamp01(state.reloadRemaining / spec.reloadSeconds);
            bool secondary = kind == EquipmentKind.DualUzi && phase >= .49f;
            var assembly = secondary ? pairedFeed : feed;
            bool energy = kind == EquipmentKind.WristLaser || kind == EquipmentKind.LaserAxe;
            if (energy) assembly = loadCell;
            if (!assembly) return;
            if (kind == EquipmentKind.DualUzi) phase = SpikeReloadPhase(phase, secondary);

            bool supportLeft = secondary ? state.leftHanded : !state.leftHanded;
            float side = supportLeft ? -1 : 1;
            var hand = supportLeft ? frame.left : frame.right;
            var offset = kind switch
            {
                EquipmentKind.SheriffHammer => new Vector3(0, .092f, .012f),
                EquipmentKind.GrenadeLauncher => new Vector3(0, GrenadeDrumHeight, GrenadeDrumOffset - .01f),
                EquipmentKind.ChainGun => new Vector3(.23f, -.31f, .12f),
                EquipmentKind.RocketLauncher => new Vector3(0, -.038f, .14f),
                EquipmentKind.CompactSidearm => new Vector3(0, -.215f, -.067f),
                EquipmentKind.StockPistol => StockPistolMagazineGrip,
                EquipmentKind.Chainsaw => new Vector3(0, .23f, .20f),
                EquipmentKind.DualUzi => new Vector3(0, -.16f, 0),
                EquipmentKind.NeedleDrill => new Vector3(0, -.232f, -.095f),
                EquipmentKind.WristLaser or EquipmentKind.LaserAxe => Vector3.zero,
                _ => new Vector3(0, -.055f, -.13f)
            };
            float reach = Ease(0, .16f, phase) * (1 - Ease(.88f, 1, phase));
            var contact = assembly.TransformPoint(offset + Vector3.right * (side * .050f));
            if (kind is EquipmentKind.SheriffHammer or EquipmentKind.GrenadeLauncher)
            {
                float stroke = Mathf.Sin(Ease(.36f, .78f, phase) * Mathf.PI * 6);
                contact -= assembly.forward * (.085f * Mathf.Max(0, stroke) * frame.scale);
            }
            hand.position = Vector3.Lerp(hand.position, contact, reach);
            hand.rotation = Quaternion.Slerp(hand.rotation, assembly.rotation * Quaternion.Euler(15, side * 30, side * 80), reach);
            var starting = hand.skeletonTracked ? hand.skeleton : CandyHandSkeleton.Rest(supportLeft);
            hand.skeleton = BlendGrip(starting, CandyHandSkeleton.ControllerGrip(supportLeft, .82f, .62f), reach);
            hand.skeletonTracked = true;
            if (supportLeft) frame.left = hand; else frame.right = hand;
        }

        static float SpikeReloadPhase(float phase, bool secondary) =>
            Mathf.Clamp01((phase - (secondary ? .48f : 0)) / .52f);

        void PresentEnergyReload(EquipmentState state, Transform weapon)
        {
            if (kind != EquipmentKind.WristLaser && kind != EquipmentKind.LaserAxe) return;
            bool axe = kind == EquipmentKind.LaserAxe;
            if (loadCellOwner != weapon || !loadCell)
            {
                loadCellOwner = weapon;
                loadCell = new GameObject("Removable energy cartridge").transform;
                loadCell.SetParent(weapon, false);
                var cap = new CandyAccessoryMeshes.Builder();
                cap.Cone(Vector3.back * .036f, Vector3.back * .020f, .019f, .022f, 10);
                cap.Cone(Vector3.forward * .020f, Vector3.forward * .036f, .022f, .018f, 10);
                var charge = new CandyAccessoryMeshes.Builder();
                charge.Cone(Vector3.back * .020f, Vector3.forward * .020f, .017f, .017f, 12);
                CachedMesh(loadCell, "Energy refill casing", cap, new Color(.10f,.15f,.19f));
                CachedMesh(loadCell, "Energy refill core", charge, new Color(.10f,.69f,.84f));
            }

            var spec = EquipmentSpecifications.Get(kind);
            bool reload = state.drawn && state.reloadRemaining > 0 && (!axe || state.shotgun);
            float phase = reload ? 1 - Mathf.Clamp01(state.reloadRemaining / spec.reloadSeconds) : 0;
            float side = state.leftHanded ? 1 : -1;
            var seat = axe ? new Vector3(side * .056f,.035f,.50f) : new Vector3(side * .058f,.051f,-.109f);
            float travel = phase < .40f ? Ease(.08f,.40f,phase) : 1 - Ease(.51f,.88f,phase);
            var reach = new Vector3(side * .12f,.055f,-.10f) * travel;
            loadCell.localPosition = seat + reach;
            loadCell.localRotation = Quaternion.Euler(-23 * travel,side * 45 * travel,side * 21 * travel);
            loadCell.gameObject.SetActive((!axe || state.shotgun) && (!reload || phase < .40f || phase >= .51f));
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
            if (kind == EquipmentKind.LaserAxe)
            {
                var opposite = left ? frame.right : frame.left;
                var highGripPose = DesktopAxeFromHighGrip(hand, left, frame.scale);
                var lowerGrip = opposite;
                TryAxeLowerContact(ref lowerGrip, highGripPose, !left, frame.scale);
                if ((lowerGrip.position - opposite.position).sqrMagnitude < .0036f * frame.scale * frame.scale)
                    drawn = highGripPose;
            }
            if (kind == EquipmentKind.WristLaser)
            {
                float folded = 1 - Ease(0,1,progress);
                return new Pose(drawn.position + hand.rotation * new Vector3(0,-.006f,-.042f) * (folded * frame.scale),
                    drawn.rotation * Quaternion.Euler(-12 * folded,0,0));
            }
            if (kind == EquipmentKind.LaserClaws || progress >= 1) return drawn;

            var yaw = Quaternion.Euler(0, frame.headRotation.eulerAngles.y, 0);
            var holstered = Holster(kind, frame.feet, frame.head, yaw, frame.scale, left);
            bool back = EquipmentSpecifications.Get(kind).backHolster || kind == EquipmentKind.FlamingMarshmallow;
            float side = left ? -1 : 1;
            float storedSide = back ? 1 : side;
            var clear = holstered.position + yaw * new Vector3(storedSide * (back ? .30f : .13f),
                back ? .48f : .19f, back ? -.015f : .05f) * frame.scale;
            float weight = kind is EquipmentKind.RocketLauncher or EquipmentKind.ChainGun or
                EquipmentKind.GrenadeLauncher ? 1.45f : 1;
            var present = drawn.position + yaw * new Vector3(side * .10f * weight, -.07f * weight, .071f) * frame.scale;
            clear += yaw * new Vector3(storedSide * .065f, .055f, .04f) * ((weight - 1) * frame.scale);
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
            var guide = kind == EquipmentKind.BoxingGlove
                ? drawn.rotation * Quaternion.Euler(drawing ? -32 : 26,side * 10,side * 32)
                : yaw * Quaternion.Euler(back ? -37 : 43, storedSide * (back ? 65 : 27),
                    storedSide * (drawing ? 25 : 43));
            var rotation = Quaternion.Slerp(Quaternion.Slerp(holstered.rotation, guide, t),
                Quaternion.Slerp(guide, drawn.rotation, t), t);
            return new Pose(position, rotation);
        }

        float WearableProgress(float draw, bool secondary)
        {
            float progress = Mathf.Clamp01((draw - (secondary ? .09f : 0)) / .91f);
            if (float.IsFinite(pickupStarted))
            {
                float pickup = (Time.time - pickupStarted) / PickupDuration;
                progress *= Ease(secondary ? .23f : .12f, secondary ? .96f : .83f, pickup);
            }
            return progress;
        }

        void PresentBoxingGlove(Transform weapon, float progress)
        {
            if (!float.IsFinite(pickupStarted)) return;
            float t = (Time.time - pickupStarted) / PickupDuration;
            float squeeze = Ease(.48f,.62f,t) * (1 - Ease(.66f,.88f,t)) * progress;
            weapon.localScale = Vector3.Scale(weapon.localScale,
                new Vector3(1 - .045f * squeeze, 1 + .055f * squeeze, 1 - .055f * squeeze));
        }

        void PresentRepulsor(Transform weapon, float draw, bool secondary)
        {
            float progress = WearableProgress(draw, secondary);
            float release = Ease(.08f,.43f,progress);
            float open = Ease(.27f,.93f,progress);
            float seat = Ease(.76f,.91f,progress) * (1-Ease(.91f,1,progress));
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                var shutter = weapon.GetChild(i);
                shutter.localPosition = new Vector3(side * Mathf.Lerp(.013f, .032f, open),
                    .004f * seat, .010f * release - .016f * open);
                shutter.localRotation = Quaternion.Euler(-9 * (release - open), side * (58 * open + 4 * seat), 0);
            }
        }

        struct ClawTint
        {
            public Transform weapon;
            public Color color;
        }

        ClawTint mainClawTint, pairedClawTint;
        MaterialPropertyBlock clawProperties;
        static readonly int ClawColorProperty = Shader.PropertyToID("_BaseColor");

        void MatchClawColor(Transform weapon)
        {
            var color = owningRig ? owningRig.BaseAppearanceColor : new Color(.1f,.9f,1);
            ref ClawTint tint = ref (weapon == root ? ref mainClawTint : ref pairedClawTint);
            if (tint.weapon == weapon && tint.color == color) return;
            tint.weapon = weapon;
            tint.color = color;
            clawProperties ??= new MaterialPropertyBlock();
            for (int blade = 0; blade < 3; blade++)
            {
                var renderer = weapon.GetChild(blade).GetChild(0).GetComponent<MeshRenderer>();
                renderer.GetPropertyBlock(clawProperties);
                clawProperties.SetColor(ClawColorProperty,color);
                renderer.SetPropertyBlock(clawProperties);
            }
        }

        void PresentClaws(Transform weapon, CandyHandPose hand, bool left, float draw, bool secondary)
        {
            float progress = WearableProgress(draw, secondary);
            weapon.gameObject.SetActive(progress > 0);
            MatchClawColor(weapon);
            PositionClaws(weapon, hand, left);
            bool visible = false;
            for (int i = 0; i < 3; i++)
            {
                var blade = weapon.GetChild(i);
                float delay = (i == 1 ? 0 : i == 0 ? .13f : .24f);
                float shoot = Ease(delay,.64f + delay,progress);
                float align = Ease(.45f + delay,.76f + delay,progress);
                float side = (left ? -1f : 1f) * (i - 1);
                blade.localPosition -= Vector3.forward * (.051f * (1-shoot));
                blade.localRotation *= Quaternion.Euler(-19 * (1 - align),side * 12 * (1 - align),0);
                blade.localScale = new Vector3(.74f + .26f * shoot,.74f + .26f * shoot,.025f + .975f * shoot);
                blade.gameObject.SetActive(shoot > .015f);
                visible |= shoot > .015f;
            }
            weapon.gameObject.SetActive(visible);
        }

        static Vector3 StockPistolMagazineGrip => LoadCompactStockPistol()
            ? new Vector3(0, -.05f, .146f) : new Vector3(0, -.044f, .23f);

        void PresentFeed(Transform assembly, EquipmentState state, bool left, bool secondary)
        {
            if (!assembly) return;
            var spec = EquipmentSpecifications.Get(kind);
            bool reloading = state.drawn && state.reloadRemaining > 0 && spec.reloadSeconds > 0 &&
                (!spec.melee || state.shotgun);
            float phase = reloading ? 1 - Mathf.Clamp01(state.reloadRemaining / spec.reloadSeconds) : 0;
            if (kind == EquipmentKind.RocketLauncher)
            {
                PresentRocketCandy(assembly.parent, state, reloading, phase);
                return;
            }
            if (kind == EquipmentKind.DualUzi)
                phase = SpikeReloadPhase(phase, secondary);

            float side = left ? -1 : 1;
            Vector3 travel = kind switch
            {
                EquipmentKind.SheriffHammer => new Vector3(side * .14f, -.045f, 0),
                EquipmentKind.GrenadeLauncher => new Vector3(side * .28f, -.065f, 0),
                EquipmentKind.ChainGun => new Vector3(.28f, -.19f, 0),
                EquipmentKind.NeedleDrill => new Vector3(0, -.045f, -.30f),
                EquipmentKind.DualUzi => new Vector3(side * .18f, -.27f, -.07f),
                EquipmentKind.Chainsaw => Vector3.up * .23f,
                EquipmentKind.StockPistol when LoadCompactStockPistol() => Vector3.down * .20f,
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
                    assembly.Find(SugarCorpsLauncherParts[2]) ? new Vector3(0,.052f,.277f) :
                    new Vector3(0,GrenadeDrumHeight,.12f+GrenadeDrumOffset);
                assembly.localPosition += axis - assembly.localRotation * axis;
            }
            else
            {
                float eject = Ease(.06f, .38f, phase);
                float insert = Ease(.48f, .86f, phase);
                float latch = Ease(.86f,.90f,phase) * (1 - Ease(.91f,.98f,phase));
                assembly.localPosition = oldFeed
                    ? travel * eject + Vector3.down * (.16f * eject * eject)
                    : travel * (1 - insert) + Vector3.right * (side * .105f * (1 - insert)) -
                        travel * (.055f * latch);
                assembly.localRotation = oldFeed
                    ? Quaternion.Euler(-32 * eject, 0, side * 16 * eject)
                    : Quaternion.Euler(14 * (1 - insert), side * 17 * (1 - insert), 0);
                var grip = kind switch
                {
                    EquipmentKind.CompactSidearm => new Vector3(0, -.215f, -.067f),
                    EquipmentKind.StockPistol => StockPistolMagazineGrip,
                    _ => Vector3.zero
                };
                assembly.localPosition += grip - assembly.localRotation * grip;
            }

            if (kind != EquipmentKind.NeedleDrill &&
                !PresentReviewedAmmunition(assembly, state, secondary, reloading, phase))
                PresentRounds(assembly, state, secondary, cylinder, reloading, phase);
        }

        bool PresentReviewedAmmunition(Transform assembly, EquipmentState state, bool secondary, bool reloading, float phase)
        {
            if (kind == EquipmentKind.ChainGun && assembly.Find("Looped brass feed belt opaque")) return true;
            var names = kind == EquipmentKind.GrenadeLauncher ? SugarCorpsLauncherParts : RoundedSpikerParts;
            if (kind == EquipmentKind.GrenadeLauncher && assembly.childCount > 0 &&
                assembly.Find(names[2]))
            {
                int remaining = state.magazineInitialized ? Mathf.Clamp(state.rounds, 0, 6) : 6;
                for (int i = 0; i < 6; i++)
                {
                    var cell = assembly.Find(names[i + 2]);
                    bool loading = reloading && phase >= .34f + i * .064f;
                    bool loaded = i >= 6 - remaining;
                    bool extracting = reloading && !loading;
                    float travel = extracting ? Ease(.17f, .34f, phase) : 0;
                    float insert = loading ? 1 - Ease(.34f + i * .064f, .48f + i * .064f, phase) : 0;
                    cell.gameObject.SetActive(loading || loaded && travel < 1 || extracting && phase < .34f);
                    cell.localPosition = Vector3.back * (.21f * travel + .18f * insert) +
                        Vector3.down * (.16f * travel * travel + .045f * insert);
                }
                return true;
            }

            if (kind != EquipmentKind.DualUzi || !assembly.parent.Find(names[2])) return false;
            int total = state.magazineInitialized ? Mathf.Clamp(state.rounds, 0, 60) : 60;
            int rounds = secondary ? total / 2 : (total + 1) / 2;
            int lit = (rounds + 5) / 6;
            for (int i = 0; i < 5; i++)
            {
                var crystal = assembly.parent.Find(names[i + 2]);
                float arrival = Ease(.38f + i * .065f, .54f + i * .065f, phase);
                bool visible = reloading ? arrival > .001f : i < lit;
                crystal.gameObject.SetActive(visible);
                crystal.localScale = Vector3.one;
                crystal.localPosition = reloading && visible
                    ? new Vector3((secondary ? -1 : 1) * (.035f + .012f * i), .18f, -.075f) * (1 - arrival)
                    : Vector3.zero;
            }
            return true;
        }

        void PresentRocketCandy(Transform weapon, EquipmentState state, bool reloading, float phase)
        {
            if (!rocketCandy || rocketCandy.parent != weapon)
            {
                rocketCandy = new GameObject("Front-loaded lollipop").transform;
                rocketCandy.SetParent(weapon, false);
                if(!rocketSugarMaterial)
                {
                    var shader=Shader.Find("Universal Render Pipeline/Lit");
                    rocketSugarMaterial=new Material(shader){name="Glossy rocket candy",enableInstancing=true};
                    rocketSugarMaterial.SetFloat("_Smoothness",.81f);
                    rocketStickMaterial=new Material(shader){name="White rocket candy stick",color=new Color(.92f,.90f,.87f)};
                    rocketStickMaterial.SetFloat("_Smoothness",.40f);
                }
                var head=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                rocketCandyHead=MakeCandyPart(rocketCandy,"Round lollipop",head,rocketSugarMaterial,.203f,Vector3.one*.352f);
                var cylinder=Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                rocketCandySeam=MakeCandyPart(rocketCandy,"Molded candy seam",cylinder,rocketSugarMaterial,.203f,new Vector3(.367f,.022f,.367f));
                rocketCandySeam.transform.localRotation=Quaternion.Euler(90,0,0);
                var stick=MakeCandyPart(rocketCandy,"White bore-fit stick",cylinder,rocketStickMaterial,-.265f,new Vector3(.045f,.292f,.045f));
                stick.transform.localRotation=Quaternion.Euler(90,0,0);
                rocketCandyTint ??= new MaterialPropertyBlock();
                shownCandyVariant=-1;
            }
            bool visible = reloading ? phase >= .37f : !state.magazineInitialized || state.rounds > 0;
            rocketCandy.gameObject.SetActive(visible);
            if(!visible)return;
            if(shownCandyVariant!=state.rocketCandyVariant)
            {
                shownCandyVariant=state.rocketCandyVariant;
                rocketCandyTint.SetColor("_BaseColor",RocketCandy.ColorFor(shownCandyVariant));
                rocketCandyHead.SetPropertyBlock(rocketCandyTint);
                rocketCandySeam.SetPropertyBlock(rocketCandyTint);
            }

            rocketCandy.localPosition=RocketMuzzle+(reloading?RocketCandyOffset(phase):Vector3.zero);
        }

        internal static void PoseRocketLoadingHand(ref CandyHandPose hand, Pose weapon, EquipmentState state,
            bool supportLeft, float scale)
        {
            var spec=EquipmentSpecifications.Get(EquipmentKind.RocketLauncher);
            float phase=1-Mathf.Clamp01(state.reloadRemaining/spec.reloadSeconds);
            float reach=Ease(.24f,.37f,phase)*(1-Ease(.88f,.99f,phase));
            float side=supportLeft?-1:1;
            var stickGrip=RocketMuzzle+RocketCandyOffset(phase)+new Vector3(side*.064f,-.027f,.058f);
            var target=weapon.position+weapon.rotation*stickGrip*ModelScale(EquipmentKind.RocketLauncher,scale);
            hand.position=Vector3.Lerp(hand.position,target,reach);
            hand.rotation=Quaternion.Slerp(hand.rotation,weapon.rotation*Quaternion.Euler(4,side*24,side*78),reach);
            hand.skeleton=BlendGrip(hand.skeleton,CandyHandSkeleton.ControllerGrip(supportLeft,.88f,.64f),reach);
            hand.skeletonTracked=true;
        }

        static Vector3 RocketCandyOffset(float phase)
        {
            float align=Ease(.37f,.60f,phase);
            float insert=Ease(.60f,.91f,phase);
            return Vector3.forward*(.292f*(1-insert))+Vector3.down*(.13f*(1-align));
        }

        static MeshRenderer MakeCandyPart(Transform parent,string name,Mesh mesh,Material material,float z,Vector3 scale)
        {
            var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            part.transform.SetParent(parent,false);
            part.transform.localPosition=Vector3.forward*z;
            part.transform.localScale=scale;
            part.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            return renderer;
        }

        static float Ease(float from, float to, float phase) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, phase));

        void PresentRounds(Transform assembly, EquipmentState state, bool secondary, bool cylinder, bool reloading, float phase)
        {
            bool shortMagazine = kind == EquipmentKind.CompactSidearm ||
                kind == EquipmentKind.StockPistol && LoadCompactStockPistol();
            int count = kind switch
            {
                EquipmentKind.SheriffHammer or EquipmentKind.GrenadeLauncher or EquipmentKind.ChainGun => 6,
                EquipmentKind.CompactSidearm => 1,
                EquipmentKind.StockPistol when shortMagazine => 1,
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
                    kind == EquipmentKind.NeedleDrill ? .0075f : .011f;
                float length = kind == EquipmentKind.GrenadeLauncher ? .21f :
                    kind == EquipmentKind.NeedleDrill ? .059f : .038f;
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
                        kind == EquipmentKind.NeedleDrill ? new Color(.15f,.71f,.68f) :
                        kind == EquipmentKind.DualUzi ? new Color(.35f,1,.10f) :
                        kind == EquipmentKind.GrenadeLauncher ? new Color(.30f,.33f,.24f) :
                        new Color(.85f,.72f,.50f));
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
                bool spentCase = cylinder && !loaded && reloading && phase < .34f && !loading;
                item.localScale = spentCase ? new Vector3(1,1,.65f) : Vector3.one;
                item.gameObject.SetActive(cylinder
                    ? (loading || spentCase || loaded && (!reloading || extract < 1))
                    : shortMagazine ? reloading && phase >= .48f && phase < .9f
                    : reloading && phase >= .48f || loaded);
                float angle = Mathf.PI / 2 + i * Mathf.PI / 3;
                var seat = kind switch
                {
                    EquipmentKind.SheriffHammer => new Vector3(Mathf.Cos(angle)*.048f,.072f+Mathf.Sin(angle)*.048f,.035f),
                    EquipmentKind.GrenadeLauncher => new Vector3(Mathf.Cos(angle)*GrenadeChamberRadius,GrenadeDrumHeight+Mathf.Sin(angle)*GrenadeChamberRadius,.018f+GrenadeDrumOffset),
                    EquipmentKind.ChainGun => new Vector3(.165f+i*.023f,ChainGunBarrelHeight+.105f,.18f),
                    EquipmentKind.CompactSidearm => new Vector3(0,-.139f,-.077f),
                    EquipmentKind.StockPistol when shortMagazine => new Vector3(0,.05f,.127f),
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
                item.localRotation = kind == EquipmentKind.Chainsaw ? Quaternion.Euler(90,0,0) :
                    cylinder && reloading && !loading
                        ? Quaternion.Euler(-38 * extract, (i - 2) * 9 * extract, 40 * extract)
                        : Quaternion.identity;
            }
        }
    }
}
