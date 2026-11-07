using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        bool wasEmbedded;
        static Mesh binocularRimMesh;
        Transform binocularRim,binocularSocket;
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
        // Weapon geometry points along local Z. A shaft lies across the palm,
        // while the chainsaw's pistol-style handle runs along local Y.
        internal static bool MeleeMount(EquipmentKind kind,bool left,out Vector3 palm,out Quaternion mount)
        {
            float side=left?1:-1;
            palm=new Vector3(0,-.031f,.075f);
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
                case EquipmentKind.Chainsaw:
                    mount=Quaternion.Euler(0,0,-side*90);
                    return true;
                default:
                    mount=Quaternion.identity;
                    return false;
            }
        }

        public static Vector3 SupportGripOffset(EquipmentKind kind,bool leftHanded)
        {
            if(kind==EquipmentKind.RocketLauncher)return new Vector3(0,-.015f,.69f);
            if(kind==EquipmentKind.ChainGun)return new Vector3(leftHanded?.245f:-.245f,.10f,.45f);
            return new Vector3(0,-.015f,.43f);
        }

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

        static Pose DrawnPose(EquipmentKind kind,CandyHandPose hand,bool left,float scale)
        {
            if(!MeleeMount(kind,left,out var palm,out var mount))return new Pose(hand.position,hand.rotation);
            return new Pose(hand.position+hand.rotation*palm*scale,hand.rotation*mount);
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

            var feedState = slungForEmote ? slungFeedState : state;
            float target = state.drawn && !slungForEmote ? 1 : 0;
            bool back = EquipmentSpecifications.Get(kind).backHolster || kind == EquipmentKind.FlamingMarshmallow;
            float duration = kind == EquipmentKind.LaserClaws || kind == EquipmentKind.WristLaser || kind == EquipmentKind.PoweredArmor ? .17f : back ? .34f : .23f;
            drawAmount = Mathf.MoveTowards(drawAmount, target, Time.deltaTime / duration);
            if (state.roleIssued && !state.drawn && drawAmount <= 0) { Hide(); return; }

            var hand = state.leftHanded ? frame.left : frame.right;
            var pose = CarryPose(frame, hand, state.leftHanded, drawAmount);
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
                var otherPose = CarryPose(frame, other, otherLeft, drawAmount);
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
            PresentBinocularEyepiece();
        }
        void PresentBinocularEyepiece()
        {
            if(kind!=EquipmentKind.DualRifle&&kind!=EquipmentKind.Blaster&&kind!=EquipmentKind.SheriffHammer)return;
            if(!binocularRim)
            {
                var optic=kind==EquipmentKind.DualRifle?barrel.Find("Rifle optic"):root.Find(kind==EquipmentKind.Blaster?"Blaster optic":"Revolver optic");
                if(!optic)return;
                binocularSocket=optic.Find("Optic lens");if(!binocularSocket)return;
                if(!binocularRimMesh)
                {
                    const int sides=48;var vertices=new Vector3[(sides+1)*4];var triangles=new int[sides*4*6];
                    for(int ring=0;ring<4;ring++)for(int i=0;i<=sides;i++)
                    {
                        float angle=i*Mathf.PI*2/sides,radius=ring<2?1.12f:1;
                        int n=ring*(sides+1)+i;vertices[n]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,ring==0||ring==3?-.5f:.5f);
                        if(i==sides)continue;int next=((ring+1)%4)*(sides+1)+i,t=(ring*sides+i)*6;
                        triangles[t]=n;triangles[t+1]=n+1;triangles[t+2]=next;triangles[t+3]=n+1;triangles[t+4]=next+1;triangles[t+5]=next;
                    }
                    binocularRimMesh=new Mesh{name="Binocular rear eyecup rim",hideFlags=HideFlags.DontSave};
                    binocularRimMesh.vertices=vertices;binocularRimMesh.triangles=triangles;binocularRimMesh.RecalculateNormals();binocularRimMesh.RecalculateBounds();
                }
                MeshPart(optic,"Binocular rear eyecup",binocularRimMesh,new Color(.075f,.095f,.12f));binocularRim=optic.Find("Binocular rear eyecup");
            }
            float scale=Mathf.Max(.001f,Mathf.Abs(binocularRim.parent.lossyScale.z));
            // The exit pupil fits human eye spacing even when the avatar is small.
            binocularRim.localPosition=Vector3.back*(.020f/scale);
            binocularRim.localScale=new Vector3(.055f/scale,.025f/scale,.012f/scale);
            binocularSocket.localPosition=binocularRim.localPosition;
            binocularSocket.localScale=new Vector3(.055f/scale,.025f/scale,1/scale);
        }
        Transform Create(EquipmentKind value)
        {
            binocularRim=binocularSocket=null;
            barrel=null;barrelInitialized=false;
            var root=new GameObject("Visual - "+value).transform;root.SetParent(parent,false);
            var dark=new Color(.065f,.09f,.15f);var cyan=new Color(.1f,.95f,1);var gold=new Color(1,.65f,.12f);
            if(value==EquipmentKind.Blaster)BuildDetailedAlienBlaster(root);
            else if (value == EquipmentKind.PoweredArmor)
            {
                Part(root, "Left repulsor shutter", PrimitiveType.Cube, Vector3.zero, new Vector3(.025f,.033f,.006f), dark);
                Part(root, "Right repulsor shutter", PrimitiveType.Cube, Vector3.zero, new Vector3(.025f,.033f,.006f), dark);
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
