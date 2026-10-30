using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        bool wasEmbedded;Pose embeddedPose;
        readonly Transform parent;readonly Material material;Transform root,barrel;EquipmentKind kind;float barrelAngle,barrelTarget,barrelSpeed=600;bool barrelMode,barrelInitialized;
        public CandyWeaponVisual(Transform parent,Material material){this.parent=parent;this.material=material;PreparePickupModels();}
        public void Hide(){if(root)root.gameObject.SetActive(false);if(paired)paired.gameObject.SetActive(false);}
        public static Pose Holster(EquipmentKind kind,Vector3 feet,Vector3 head,Quaternion yaw,float scale,bool left)
        {
            bool back=EquipmentSpecifications.Get(kind).backHolster||kind==EquipmentKind.FlamingMarshmallow;
            var position=back?new Vector3(feet.x,Mathf.Lerp(feet.y,head.y,.63f),feet.z)+yaw*new Vector3(.12f,0,-.25f)*scale:new Vector3(feet.x,Mathf.Lerp(feet.y,head.y,.40f),feet.z)+yaw*new Vector3(left?-.23f:.23f,0,.025f)*scale;
            return new Pose(position,yaw*(back?Quaternion.Euler(0,0,-35)*Quaternion.Euler(-90,0,0):Quaternion.Euler(90,0,left?-8:8)));
        }
        public void Present(EquipmentState state,CandyAvatarFrame frame,bool active)
        {
            if(parent&&parent.GetComponentInParent<IEnemyActor>()?.ArchetypeId=="mushroom"){Hide();return;}
            if(state==null||state.kind<EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor||state.roleIssued&&!state.drawn||!active){Hide();return;}
            if(!root||kind!=state.kind){if(paired){Object.Destroy(paired.gameObject);paired=null;}rotor=null;if(root)Object.Destroy(root.gameObject);kind=state.kind;root=Create(kind);}
            root.gameObject.SetActive(true);root.localScale=new Vector3(kind==EquipmentKind.BoxingGlove&&state.leftHanded?-1:1,1,1)*frame.scale;
            var hand=state.leftHanded?frame.left:frame.right;var pose=state.drawn||kind==EquipmentKind.WristLaser?new Pose(hand.position,hand.rotation):Holster(kind,frame.feet,frame.head,Quaternion.Euler(0,frame.headRotation.eulerAngles.y,0),frame.scale,state.leftHanded);
            if(state.embedRemaining>0){if(!wasEmbedded)embeddedPose=new Pose(state.embeddedPoint-pose.rotation*Vector3.forward*.65f,pose.rotation);pose=embeddedPose;wasEmbedded=true;}else wasEmbedded=false;
            root.SetPositionAndRotation(pose.position,pose.rotation);
            if(kind==EquipmentKind.LaserClaws){PositionClaws(root,hand,state.leftHanded);root.gameObject.SetActive(state.drawn);}
            if(rotor){spin+=Time.deltaTime*(state.triggerHeld?700+state.charge*1800:state.kind==EquipmentKind.SawStaff||state.kind==EquipmentKind.Chainsaw?500:0);rotor.localRotation=Quaternion.Euler(state.kind==EquipmentKind.SawStaff||state.kind==EquipmentKind.Chainsaw?spin:0,0,state.kind==EquipmentKind.SawStaff||state.kind==EquipmentKind.Chainsaw?0:spin);}
            if(kind==EquipmentKind.DualUzi||kind==EquipmentKind.LaserClaws)
            {
                if(!paired){paired=Object.Instantiate(root,parent);paired.name="Visual - paired "+kind;
                    var original=root.GetComponentsInChildren<MeshRenderer>();var copied=paired.GetComponentsInChildren<MeshRenderer>();var properties=new MaterialPropertyBlock();for(int i=0;i<original.Length&&i<copied.Length;i++){original[i].GetPropertyBlock(properties);copied[i].SetPropertyBlock(properties);}}
                var other=state.leftHanded?frame.right:frame.left;paired.gameObject.SetActive(true);var otherPose=state.drawn?new Pose(other.position,other.rotation):Holster(kind,frame.feet,frame.head,Quaternion.Euler(0,frame.headRotation.eulerAngles.y,0),frame.scale,!state.leftHanded);paired.SetPositionAndRotation(otherPose.position,otherPose.rotation);paired.localScale=Vector3.one*frame.scale;
                if(kind==EquipmentKind.LaserClaws){PositionClaws(paired,other,!state.leftHanded);paired.gameObject.SetActive(state.drawn);}
            }
            else if(paired){Object.Destroy(paired.gameObject);paired=null;}
            if(barrel)
            {
                if(!barrelInitialized){barrelMode=state.shotgun;barrelAngle=barrelTarget=state.shotgun?0:180;barrelInitialized=true;}
                if(barrelMode!=state.shotgun){barrelMode=state.shotgun;barrelTarget-=180;barrelSpeed=Mathf.Max(600,Mathf.Abs(barrelTarget-barrelAngle)/.3f);}
                barrelAngle=Mathf.MoveTowards(barrelAngle,barrelTarget,Time.deltaTime*barrelSpeed);
                barrel.localRotation=Quaternion.Euler(0,0,barrelAngle);
            }
        }
        Transform Create(EquipmentKind value)
        {
            barrel=null;barrelInitialized=false;
            var root=new GameObject("Visual - "+value).transform;root.SetParent(parent,false);
            var dark=new Color(.065f,.09f,.15f);var cyan=new Color(.1f,.95f,1);var gold=new Color(1,.65f,.12f);
            if(value==EquipmentKind.Blaster)BuildDetailedAlienBlaster(root);
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
