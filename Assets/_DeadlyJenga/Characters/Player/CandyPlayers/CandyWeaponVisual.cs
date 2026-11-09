using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        bool wasEmbedded;
        IMatchRuntime pickupRuntime;
        string pickupEpoch;
        int pickupSequence = -1;
        readonly EquipmentState slungFeedState = new EquipmentState();
        readonly Transform parent;readonly Material material;Transform root,barrel;EquipmentKind kind;float barrelAngle,barrelTarget,barrelSpeed=600;bool barrelMode,barrelInitialized;
        public CandyWeaponVisual(Transform parent,Material material){this.parent=parent;this.material=material;PreparePickupModels();}
        public void Hide(){if(root)root.gameObject.SetActive(false);if(paired)paired.gameObject.SetActive(false);ResetMotion();}
        public static Pose Holster(EquipmentKind kind,Vector3 feet,Vector3 head,Quaternion yaw,float scale,bool left)
        {
            bool back=EquipmentSpecifications.Get(kind).backHolster||kind==EquipmentKind.FlamingMarshmallow;
            var position=back?new Vector3(feet.x,Mathf.Lerp(feet.y,head.y,.63f),feet.z)+yaw*new Vector3(.12f,0,-.25f)*scale:new Vector3(feet.x,Mathf.Lerp(feet.y,head.y,.40f),feet.z)+yaw*new Vector3(left?-.23f:.23f,0,.025f)*scale;
            return new Pose(position,yaw*(back?Quaternion.Euler(0,0,-35)*Quaternion.Euler(-90,0,0):Quaternion.Euler(90,0,left?-8:8)));
        }
        // Weapon shafts point along local Z and lie across the palm.
        internal static bool MeleeMount(EquipmentKind kind,bool left,out Vector3 palm,out Quaternion mount)
        {
            float side=left?1:-1;
            palm=ShaftPalm;
            switch(kind)
            {
                case EquipmentKind.MurderSword:
                case EquipmentKind.SawStaff:
                case EquipmentKind.BarbedBat:
                case EquipmentKind.LaserAxe:
                case EquipmentKind.Dagger:
                case EquipmentKind.FlamingMarshmallow:
                    mount=Quaternion.Euler(0,side*90,0);
                    return true;
                default:
                    mount=Quaternion.identity;
                    return false;
            }
        }

        internal const float ChainGunBarrelHeight = -.235f;
        internal const float ChainGunRotorStart = .39f;
        internal const float ChainGunMuzzleDepth = ChainGunRotorStart + .61f;

        internal static readonly Vector3 RocketMuzzle = new Vector3(0,.205f,1.065f);
        internal const float RocketSupportDepth = .69f;
        internal const float GrenadeDrumHeight = .12f, GrenadeChamberRadius = .115f, GrenadeDrumOffset = .16f;
        internal static readonly Vector3 GrenadeMuzzle = new Vector3(0,GrenadeDrumHeight+GrenadeChamberRadius,.80f);
        internal const float GrenadeSupportDepth = .515f;

        internal const float RifleSupportDepth = .354f;
        internal const float ChainGunRearGripDepth = .075f, ChainGunFrontGripDepth = .475f;
        static readonly Vector3 ShaftPalm = new Vector3(0, -.031f, .075f);
        internal const float SawBailHeight = .315f, SawBailDepth = .275f;
        internal const float AxeSupportDepth = .43f;

        // These are model-space measurements of the wrapped surfaces and trigger faces.
        readonly struct PistolContact
        {
            public readonly Vector3 center, trigger;
            public readonly float width, depth, rake;
            public PistolContact(float y, float z, float width, float depth, float rake,
                float triggerY = 0, float triggerZ = 0)
            {
                center = new Vector3(0, y, z);
                trigger = new Vector3(0, triggerY, triggerZ);
                this.width = width; this.depth = depth; this.rake = rake;
            }
        }

        static PistolContact PrimaryContact(EquipmentKind kind) => kind switch
        {
            EquipmentKind.Blaster => new(-.067f, .030f, .054f, .045f, .25f, -.021f, .128f),
            EquipmentKind.SheriffHammer => new(-.104f, -.045f, .055f, .047f, .34f, -.044f, .111f),
            EquipmentKind.DualRifle => new(-.074f, -.032f, .061f, .046f, .33f, -.023f, .103f),
            EquipmentKind.RocketLauncher or EquipmentKind.GrenadeLauncher =>
                new(-.082f, 0, .037f, .052f, 0, -.029f, .089f),
            EquipmentKind.DualUzi => new(-.082f, -.006f, .050f, .055f, 0, -.022f, .120f),
            EquipmentKind.NeedleDrill => new(-.087f, -.052f, .058f, .049f, .37f, .003f, .083f),
            EquipmentKind.StockPistol => new(-.055f, -.022f, .055f, .048f, .30f, .023f, .124f),
            EquipmentKind.Chainsaw => new(-.101f, -.014f, .047f, .056f, 0, -.072f, .068f),
            _ => default
        };

        static Pose ContactWrist(PistolContact grip, bool left)
        {
            float side = left ? -1 : 1;
            return new Pose(grip.center + new Vector3(side * (grip.width + .018f), -.017f, -grip.depth - .055f),
                Quaternion.Euler(0, 0, -side * 86));
        }

        static CandyHandSkeleton PistolFingers(PistolContact grip, Pose wrist, bool left, bool pressed)
        {
            float side = left ? -1 : 1;
            var inverse = Quaternion.Inverse(wrist.rotation);
            var hand = CandyHandSkeleton.Rest(left, .7f);
            void Joint(int index, Vector3 point) => hand.SetJoint(index, inverse * (point - wrist.position));
            for (int finger = 1; finger < 5; finger++)
            {
                float y = grip.center.y + (2 - finger) * .019f;
                float z = grip.center.z + (y - grip.center.y) * grip.rake;
                float outside = side * (grip.width + .008f);
                Joint(finger * 4, new Vector3(outside, y, z - grip.depth - .005f));
                if (finger == 1 && grip.trigger.z > 0)
                {
                    // Index the side of the receiver, then enter the guard from its open side.
                    float height = pressed ? grip.trigger.y : grip.trigger.y + .022f;
                    float tip = grip.trigger.z + (pressed ? 0 : .015f);
                    Joint(4, new Vector3(outside, Mathf.Lerp(y,height,.55f), tip - .054f));
                    Joint(5, new Vector3(outside, height, tip - .030f));
                    Joint(6, new Vector3(pressed ? side * .025f : outside, height, tip + (pressed ? .006f : -.009f)));
                    Joint(7, new Vector3(pressed ? 0 : outside, height, tip));
                    continue;
                }

                float front = z + grip.depth + .008f;
                Joint(finger * 4 + 1, new Vector3(outside, y, z + .012f));
                Joint(finger * 4 + 2, new Vector3(outside * .91f, y, front));
                Joint(finger * 4 + 3, new Vector3(outside * .21f, y, front));
            }
            float thumbY = grip.center.y + .047f;
            float rear = grip.center.z + .047f * grip.rake - grip.depth - .010f;
            Joint(0, new Vector3(side * (grip.width + .014f), thumbY - .024f, rear - .025f));
            Joint(1, new Vector3(side * grip.width * .50f, thumbY, rear));
            Joint(2, new Vector3(-side * grip.width * .18f, thumbY, rear));
            Joint(3, new Vector3(-side * grip.width * .60f, thumbY, rear + .007f));
            return hand;
        }

        static void PlaceContact(ref CandyHandPose hand, Pose weapon, Pose wrist, float scale)
        {
            hand.position = weapon.position + weapon.rotation * wrist.position * scale;
            hand.rotation = weapon.rotation * wrist.rotation;
            hand.skeletonTracked = true;
        }

        static CandyHandSkeleton ShaftFingers(bool left, float radius)
        {
            var hand = CandyHandSkeleton.Rest(left, .90f);
            var center = ShaftPalm;
            // Digit segments are chords. Leave room for their thickness between joints.
            float clearance = (radius + .007f) / .86f;
            for (int finger = 1; finger < 5; finger++)
            {
                int root = finger * 4;
                for (int joint = 0; joint < 4; joint++)
                {
                    float angle = (joint * 59f - 24f) * Mathf.Deg2Rad;
                    hand.SetJoint(root + joint, center + new Vector3(hand.Joint(root).x,
                        Mathf.Cos(angle) * clearance, Mathf.Sin(angle) * clearance));
                }
            }
            float side = left ? 1 : -1;
            hand.SetJoint(1, center + new Vector3(side * .037f, .016f, -clearance));
            hand.SetJoint(2, center + new Vector3(side * .020f, -.006f, -clearance));
            hand.SetJoint(3, center + new Vector3(side * .007f, -.023f, -clearance * .88f));
            return hand;
        }

        static void ShaftContact(ref CandyHandPose hand, Pose weapon, bool left, float scale,
            Vector3 center, Quaternion rotation, float radius)
        {
            PlaceContact(ref hand, weapon, new Pose(center - rotation * ShaftPalm, rotation), scale);
            hand.skeleton = ShaftFingers(left, radius);
        }

        internal static bool TryPrimaryContact(ref CandyHandPose hand, Pose weapon, EquipmentKind kind,
            bool left, bool pressed, float scale)
        {
            var grip = PrimaryContact(kind);
            if (grip.width > 0)
            {
                var wrist = ContactWrist(grip, left);
                PlaceContact(ref hand, weapon, wrist, scale);
                hand.skeleton = PistolFingers(grip, wrist, left, pressed);
                return true;
            }
            if (kind == EquipmentKind.ChainGun)
            {
                ShaftContact(ref hand, weapon, left, scale,
                    new Vector3(0, -.031f, ChainGunRearGripDepth), Quaternion.identity, .027f);
                if (pressed)
                {
                    var fingers = hand.skeleton;
                    fingers.SetJoint(7, fingers.Joint(7) + Vector3.up * .005f);
                    hand.skeleton = fingers;
                }
                return true;
            }
            return false;
        }

        internal static bool TrySupportContact(ref CandyHandPose hand, Pose weapon, EquipmentKind kind,
            bool left, float scale)
        {
            if (kind == EquipmentKind.ChainGun || kind == EquipmentKind.Chainsaw || kind == EquipmentKind.LaserAxe)
            {
                Vector3 center = kind == EquipmentKind.ChainGun ? new(0, -.031f, ChainGunFrontGripDepth) :
                    kind == EquipmentKind.Chainsaw ? new(0, SawBailHeight, SawBailDepth) : new(0, 0, AxeSupportDepth);
                var rotation = kind == EquipmentKind.LaserAxe ?
                    Quaternion.Euler(0, left ? -90 : 90, 0) : Quaternion.identity;
                ShaftContact(ref hand, weapon, left, scale, center, rotation,
                    kind == EquipmentKind.LaserAxe ? AxeWrapRadius : .027f);
                return true;
            }

            var grip = kind switch
            {
                EquipmentKind.RocketLauncher => new PistolContact(-.060f, RocketSupportDepth, .037f, .052f, 0),
                EquipmentKind.GrenadeLauncher => new PistolContact(-.060f, GrenadeSupportDepth, .037f, .052f, 0),
                EquipmentKind.DualRifle => new PistolContact(-.094f, RifleSupportDepth, .031f, .033f, 0),
                _ => default
            };
            if (grip.width == 0) return false;
            var wrist = ContactWrist(grip, left);
            PlaceContact(ref hand, weapon, wrist, scale);
            hand.skeleton = PistolFingers(grip, wrist, left, false);
            return true;
        }

        internal static CandyHandSkeleton ShaftGrip(EquipmentKind kind, bool left) => ShaftFingers(left, kind switch
        {
            EquipmentKind.LaserAxe => AxeWrapRadius,
            EquipmentKind.SawStaff => .037f,
            EquipmentKind.BarbedBat => .030f,
            EquipmentKind.MurderSword => .032f,
            EquipmentKind.Dagger => .034f,
            EquipmentKind.FlamingMarshmallow => .023f,
            _ => .030f
        });

        public bool TryOpticAnchor(EquipmentState state,out Pose anchor)
        {
            anchor=default;
            if(!root||state==null||kind!=state.kind)return false;
            string name=kind==EquipmentKind.DualRifle?(state.shotgun?"Shotgun optic":"Rifle optic"):
                kind==EquipmentKind.Blaster?"Blaster optic":kind==EquipmentKind.SheriffHammer?"Revolver optic":null;
            if(name==null)return false;
            var optic=kind==EquipmentKind.DualRifle&&barrel?barrel.Find(name):root.Find(name);
            if(!optic)return false;
            if(kind==EquipmentKind.DualRifle)
            {
                // The carriage rotates during a mode change; the held receiver must not counter-rotate with it.
                var settled=Quaternion.Euler(0,0,state.shotgun?0:180);
                anchor=new Pose(barrel.localPosition+settled*optic.localPosition,settled*optic.localRotation);
            }
            else anchor=new Pose(root.InverseTransformPoint(optic.position),Quaternion.Inverse(root.rotation)*optic.rotation);
            return true;
        }

        internal static Pose DrawnPose(EquipmentKind kind,CandyHandPose hand,bool left,float scale)
        {
            var contact = PrimaryContact(kind);
            if (contact.width > 0)
            {
                var wrist = ContactWrist(contact, left);
                var rotation = hand.rotation * Quaternion.Inverse(wrist.rotation);
                return new Pose(hand.position - rotation * wrist.position * scale, rotation);
            }
            if(!MeleeMount(kind,left,out var palm,out var mount))return new Pose(hand.position,hand.rotation);
            return new Pose(hand.position+hand.rotation*palm*scale,hand.rotation*mount);
        }

        internal bool IsPairedShot(Vector3 origin)
        {
            if(!root||!paired||kind!=EquipmentKind.DualUzi)return false;
            var muzzle=new Vector3(0,.12f,.51f);
            return (paired.TransformPoint(muzzle)-origin).sqrMagnitude<(root.TransformPoint(muzzle)-origin).sqrMagnitude;
        }

        public void Present(EquipmentState state,CandyAvatarFrame frame,bool active) =>
            Present(state,frame,active,false);

        public void Present(EquipmentState state,CandyAvatarFrame frame,bool active,bool slungForEmote)
        {
            if(parent&&parent.GetComponentInParent<IEnemyActor>()?.ArchetypeId=="mushroom"){Hide();return;}
            if (!active || state == null) { Hide(); return; }
            if (state.kind == EquipmentKind.None && root && lastRoleIssued && drawAmount > 0)
                state = new EquipmentState { actor = state.actor, kind = kind, roleIssued = true, leftHanded = lastLeft };
            if (state.kind < EquipmentKind.Blaster ||
                state.roleIssued && !state.drawn && (!motionReady || kind != state.kind || drawAmount <= 0))
            {
                Hide();
                return;
            }

            if (!root || kind != state.kind)
            {
                if (paired) Object.Destroy(paired.gameObject);
                if (root) Object.Destroy(root.gameObject);
                paired = pairedFeed = rotor = null;
                kind = state.kind;
                root = Create(kind);
                feed = root.Find(FeedAssembly);
                ResetMotion();
            }

            lastRoleIssued = state.roleIssued;
            lastLeft = state.leftHanded;
            if (!motionReady)
            {
                // Ordinary held weapons can already be drawn when their avatar enters view.
                drawAmount = state.drawn && !slungForEmote && (!state.roleIssued || string.IsNullOrEmpty(state.actor)) ? 1 : 0;
                motionReady = true;
            }

            if (state.drawn && !slungForEmote && HasNewPickupDraw(state)) drawAmount = 0;
            var feedState = slungForEmote ? slungFeedState : state;
            float target = state.drawn && !slungForEmote ? 1 : 0;
            float duration = CarryDuration(target > 0);
            drawAmount = Mathf.MoveTowards(drawAmount, target, Time.deltaTime / duration);
            if (state.roleIssued && !state.drawn && drawAmount <= 0) { Hide(); return; }

            if (target > 0)
            {
                lastCarriedFrame = frame;
                hasCarriedFrame = true;
            }
            var hand = state.leftHanded ? frame.left : frame.right;
            var pose = CarryPose(frame, hand, state.leftHanded, drawAmount, target > 0);
            if (slungForEmote)
            {
                wasEmbedded = false;
                daggerUntil = 0;
            }
            else
            {
                ReadMeleeActions(state, pose);
                pose = EmbedPose(state, pose, frame.scale);
            }
            root.gameObject.SetActive(true);
            root.localScale = new Vector3((kind == EquipmentKind.BoxingGlove || kind == EquipmentKind.RocketLauncher) && state.leftHanded ? -1 : 1, 1, 1) * frame.scale;
            root.SetPositionAndRotation(pose.position, pose.rotation);
            if (kind == EquipmentKind.WristLaser)
                root.localScale = new Vector3(1, 1, Mathf.Lerp(.78f, 1, drawAmount)) * frame.scale;
            if (kind == EquipmentKind.LaserClaws) PresentClaws(root, hand, state.leftHanded, drawAmount);
            if (kind == EquipmentKind.PoweredArmor) PresentRepulsor(root, drawAmount);
            if(rotor){spin+=Time.deltaTime*(state.triggerHeld?700+state.charge*1800:state.kind==EquipmentKind.SawStaff||state.kind==EquipmentKind.Chainsaw?500:0);rotor.localRotation=Quaternion.Euler(state.kind==EquipmentKind.SawStaff||state.kind==EquipmentKind.Chainsaw?spin:0,0,state.kind==EquipmentKind.SawStaff||state.kind==EquipmentKind.Chainsaw?0:spin);}
            if (kind == EquipmentKind.DualUzi || kind == EquipmentKind.LaserClaws || kind == EquipmentKind.PoweredArmor)
            {
                if (!paired)
                {
                    paired = Object.Instantiate(root, parent);
                    paired.name = "Visual - paired " + kind;
                    pairedFeed = paired.Find(FeedAssembly);
                    var original = root.GetComponentsInChildren<MeshRenderer>(true);
                    var copied = paired.GetComponentsInChildren<MeshRenderer>(true);
                    var properties = new MaterialPropertyBlock();
                    for (int i = 0; i < original.Length && i < copied.Length; i++)
                    {
                        original[i].GetPropertyBlock(properties);
                        copied[i].SetPropertyBlock(properties);
                    }
                }

                bool otherLeft = !state.leftHanded;
                var other = otherLeft ? frame.left : frame.right;
                var otherPose = CarryPose(frame, other, otherLeft, drawAmount, target > 0);
                paired.SetPositionAndRotation(otherPose.position, otherPose.rotation);
                paired.localScale = Vector3.one * frame.scale;
                paired.gameObject.SetActive(true);
                if (kind == EquipmentKind.LaserClaws) PresentClaws(paired, other, otherLeft, drawAmount);
                if (kind == EquipmentKind.PoweredArmor) PresentRepulsor(paired, drawAmount);
                PresentFeed(pairedFeed, feedState, otherLeft, true);
            }
            PresentFeed(feed, feedState, state.leftHanded, false);
            if(barrel)
            {
                if(!barrelInitialized){barrelMode=state.shotgun;barrelAngle=barrelTarget=state.shotgun?0:180;barrelInitialized=true;}
                if(barrelMode!=state.shotgun){barrelMode=state.shotgun;barrelTarget-=180;barrelSpeed=Mathf.Max(600,Mathf.Abs(barrelTarget-barrelAngle)/.3f);}
                barrelAngle=Mathf.MoveTowards(barrelAngle,barrelTarget,Time.deltaTime*barrelSpeed);
                barrel.localRotation=Quaternion.Euler(0,0,barrelAngle);
            }
        }
        bool HasNewPickupDraw(EquipmentState state)
        {
            if (string.IsNullOrEmpty(state.actor)) return false;
            var runtime = MatchRuntime.Current;
            var frame = runtime?.Frame;
            if (frame == null) return false;
            if (!ReferenceEquals(pickupRuntime, runtime) || pickupEpoch != frame.equipmentEpoch)
            {
                pickupRuntime = runtime;
                pickupEpoch = frame.equipmentEpoch;
                pickupSequence = -1;
            }
            if (pickupSequence >= frame.equipmentSequence) return false;
            int after = pickupSequence;
            pickupSequence = frame.equipmentSequence;
            foreach (var action in frame.equipmentActions)
                if (action != null && action.id > after && action.remaining >= .40f &&
                    action.actor == state.actor && action.kind == state.kind &&
                    (action.action == EquipmentAction.Pickup || action.action == EquipmentAction.Swap))
                    return true;
            return false;
        }

        Transform Create(EquipmentKind value)
        {
            barrel=null;barrelInitialized=false;
            var root=new GameObject("Visual - "+value).transform;root.SetParent(parent,false);
            var dark=new Color(.065f,.09f,.15f);var cyan=new Color(.1f,.95f,1);var gold=new Color(1,.65f,.12f);
            if(value==EquipmentKind.Blaster)
            {
                BuildDetailedAlienBlaster(root);
            }
            else if (value == EquipmentKind.PoweredArmor)
            {
                Part(root, "Left repulsor shutter", PrimitiveType.Cube, Vector3.zero, new Vector3(.025f,.033f,.006f), dark);
                Part(root, "Right repulsor shutter", PrimitiveType.Cube, Vector3.zero, new Vector3(.025f,.033f,.006f), dark);
                var hinges=new CandyAccessoryMeshes.Builder();
                foreach(float side in new[]{-1f,1f})
                    hinges.Box(new Vector3(side*.030f,0,-.008f),new Vector3(.027f,.036f,.020f));
                MeshPart(root,"Repulsor shutter hinge rails",hinges.Mesh("Repulsor hinge rails"),dark);
            }
            else if(value==EquipmentKind.FlyingHand)
            {
                var hand=new CandyAccessoryMeshes.Builder();hand.Sphere(Vector3.zero,new Vector3(.12f,.035f,.13f));var skeleton=CandyHandSkeleton.Rest(false);
                for(int f=0;f<5;f++)for(int j=0;j<3;j++)hand.Cone(skeleton.Joint(f*4+j)*2,skeleton.Joint(f*4+j+1)*2,.018f,.014f,8);MeshPart(root,"Flying hand power",hand.Mesh("Flying hand preview"),cyan);
            }
            else if(value==EquipmentKind.DualUzi)BuildDetailedSpikeUzi(root);
            else if(value==EquipmentKind.StockPistol)BuildStockPistol(root);
            else if(value==EquipmentKind.NeedleDrill)BuildNeedleDrill(root);
            else if(value==EquipmentKind.LaserClaws||value==EquipmentKind.FlamingMarshmallow)BuildArsenalPieces(root,value);
            else if(value>EquipmentKind.PoweredArmor)BuildArsenal(root,value);
            else if(value==EquipmentKind.DualRifle) BuildDetailedDualRifle(root);
            else if(value==EquipmentKind.SheriffHammer) BuildDetailedRevolver(root);
            else if(value==EquipmentKind.BoxingGlove)BuildDetailedBoxingGlove(root);
            else if(value==EquipmentKind.MurderSword)BuildDetailedMurderSword(root);
            else
            {
                Color color=value==EquipmentKind.MurderSword?new Color(1,.04f,.12f):new Color(.05f,.4f,1);
                Part(root,"Grip",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.065f,.11f,.065f),dark,Quaternion.Euler(90,0,0));
                Part(root,"Guard",PrimitiveType.Cube,new Vector3(0,0,.14f),new Vector3(.32f,.06f,.07f),gold);
                if(value==EquipmentKind.MurderSword){Part(root,"Blade silver fuller",PrimitiveType.Cube,new Vector3(0,.016f,.53f),new Vector3(.025f,.012f,.62f),new Color(.85f,.93f,1));Part(root,"Ruby pommel",PrimitiveType.Sphere,new Vector3(0,0,-.11f),new Vector3(.10f,.10f,.12f),color);Part(root,"Blade",PrimitiveType.Cube,new Vector3(0,0,.53f),new Vector3(.10f,.028f,.70f),color);Part(root,"Blade point",PrimitiveType.Sphere,new Vector3(0,0,.87f),new Vector3(.10f,.028f,.15f),color);}
            }
            if(value==EquipmentKind.FlamingMarshmallow)AddMarshmallowFire(root);
            return root;
        }
        void MeshPart(Transform root,string name,Mesh mesh,Color color)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=WeaponSurface(material);
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetFloat("_Smoothness",.9f);block.SetFloat("_Metallic",.35f);renderer.SetPropertyBlock(block);
        }
        void Part(Transform root,string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color,Quaternion? rotation=null)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localRotation=rotation??Quaternion.identity;go.transform.localScale=scale;
            var mesh=Resources.GetBuiltinResource<Mesh>(type+".fbx");go.GetComponent<MeshFilter>().sharedMesh=mesh;
            // Built-in FBX bounds differ between primitive types; sizes here are full dimensions.
            var size=scale;if(type==PrimitiveType.Cylinder)size.y*=2;
            var bounds=mesh.bounds;go.transform.localScale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            go.transform.localPosition=position-go.transform.localRotation*Vector3.Scale(bounds.center,go.transform.localScale);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=WeaponSurface(material);var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetFloat("_Smoothness",.9f);block.SetFloat("_Metallic",kind==EquipmentKind.BoxingGlove?.12f:.65f);renderer.SetPropertyBlock(block);
        }
    }
}
