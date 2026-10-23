using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed class CandyWeaponVisual
    {
        static Mesh cuffStitches;
        readonly Transform parent;readonly Material material;Transform root,barrel;EquipmentKind kind;
        public CandyWeaponVisual(Transform parent,Material material){this.parent=parent;this.material=material;}
        public void Hide(){if(root)root.gameObject.SetActive(false);}
        public static Pose Holster(EquipmentKind kind,Vector3 feet,Vector3 head,Quaternion yaw,float scale,bool left)
        {
            bool back=kind==EquipmentKind.DualRifle;
            var position=back?new Vector3(head.x,Mathf.Lerp(feet.y,head.y,.65f),head.z)+yaw*new Vector3(0,0,-.24f)*scale:new Vector3(head.x,Mathf.Lerp(feet.y,head.y,.3f),head.z)+yaw*new Vector3(left?-.22f:.22f,0,0)*scale;
            return new Pose(position,yaw*(back?Quaternion.Euler(-145,0,-25):Quaternion.Euler(90,0,0)));
        }
        public void Present(EquipmentState state,CandyAvatarFrame frame,bool active)
        {
            if(state==null||state.kind<EquipmentKind.Blaster||state.kind==EquipmentKind.PoweredArmor||state.roleIssued&&!state.drawn||!active){Hide();return;}
            if(!root||kind!=state.kind){if(root)Object.Destroy(root.gameObject);kind=state.kind;root=Create(kind);}
            root.gameObject.SetActive(true);root.localScale=new Vector3(kind==EquipmentKind.BoxingGlove&&state.leftHanded?-1:1,1,1)*frame.scale;
            var hand=state.leftHanded?frame.left:frame.right;var pose=state.drawn?new Pose(hand.position,hand.rotation):Holster(kind,frame.feet,frame.head,Quaternion.Euler(0,frame.headRotation.eulerAngles.y,0),frame.scale,state.leftHanded);
            root.SetPositionAndRotation(pose.position,pose.rotation);
            if(barrel)barrel.localScale=new Vector3(1,1,state.shotgun?.68f:1);
        }
        Transform Create(EquipmentKind value)
        {
            barrel=null;if(value==EquipmentKind.Blaster)return CandyBlasterModel.Create(parent,material);
            var root=new GameObject("Visual - "+value).transform;root.SetParent(parent,false);
            var dark=new Color(.065f,.09f,.15f);var cyan=new Color(.1f,.95f,1);var gold=new Color(1,.65f,.12f);
            if(value==EquipmentKind.DualRifle)
            {
                Part(root,"Grip",PrimitiveType.Capsule,new Vector3(0,-.015f,0),new Vector3(.075f,.19f,.10f),dark);
                Part(root,"Receiver",PrimitiveType.Capsule,new Vector3(0,.09f,.22f),new Vector3(.16f,.54f,.16f),dark,Quaternion.Euler(90,0,0));
                Part(root,"Titanium receiver side plate",PrimitiveType.Cube,new Vector3(0,.09f,.22f),new Vector3(.169f,.08f,.33f),new Color(.3f,.38f,.45f));
                for(int side=-1;side<=1;side+=2)
                {
                    for(int v=0;v<5;v++)Part(root,"Heat sink vent",PrimitiveType.Cube,new Vector3(side*.088f,.10f,.16f+v*.045f),new Vector3(.008f,.035f,.018f),dark,Quaternion.Euler(0,0,side*18));
                    for(int bolt=0;bolt<3;bolt++)Part(root,"Polished fastener",PrimitiveType.Sphere,new Vector3(side*.089f,.065f,.12f+bolt*.13f),Vector3.one*.014f,gold);
                    Part(root,"Candy blue selector rail",PrimitiveType.Capsule,new Vector3(side*.09f,.14f,.21f),new Vector3(.014f,.13f,.014f),cyan,Quaternion.Euler(90,0,0));
                }
                Part(root,"Recoil butt pad",PrimitiveType.Capsule,new Vector3(0,.045f,-.36f),new Vector3(.15f,.28f,.07f),new Color(.025f,.035f,.05f));
                Part(root,"Optic lens",PrimitiveType.Sphere,new Vector3(0,.23f,.32f),new Vector3(.046f,.046f,.018f),cyan);
                Part(root,"Trigger bow",PrimitiveType.Sphere,new Vector3(0,-.04f,.105f),new Vector3(.07f,.07f,.07f),gold,Quaternion.Euler(0,90,0));
                Part(root,"Shoulder stock",PrimitiveType.Cube,new Vector3(0,.045f,-.22f),new Vector3(.12f,.24f,.27f),dark);
                Part(root,"Support grip",PrimitiveType.Cube,new Vector3(0,-.005f,.43f),new Vector3(.14f,.09f,.22f),gold);
                Part(root,"Energy cell",PrimitiveType.Cube,new Vector3(0,-.09f,.23f),new Vector3(.09f,.22f,.12f),cyan);
                Part(root,"Sight",PrimitiveType.Cube,new Vector3(0,.21f,.22f),new Vector3(.06f,.07f,.18f),gold);
                barrel=new GameObject("Telescoping twin barrels").transform;barrel.SetParent(root,false);
                for(int side=-1;side<=1;side+=2)
                {
                    Part(barrel,"Barrel",PrimitiveType.Cylinder,new Vector3(side*.055f,.09f,.70f),new Vector3(.065f,.28f,.065f),dark,Quaternion.Euler(90,0,0));
                    Part(barrel,"Muzzle bore",PrimitiveType.Cylinder,new Vector3(side*.055f,.09f,.986f),new Vector3(.043f,.003f,.043f),Color.black,Quaternion.Euler(90,0,0));
                    Part(barrel,"Crowned muzzle",PrimitiveType.Cylinder,new Vector3(side*.055f,.09f,.96f),new Vector3(.082f,.024f,.082f),new Color(.37f,.43f,.48f),Quaternion.Euler(90,0,0));
                    for(int i=0;i<3;i++)Part(barrel,"Cooling collar",PrimitiveType.Cylinder,new Vector3(side*.055f,.09f,.60f+i*.12f),new Vector3(.085f,.018f,.085f),cyan,Quaternion.Euler(90,0,0));
                }
            }
            else if(value==EquipmentKind.BoxingGlove)
            {
                Color leather=new Color(.94f,.018f,.075f),seam=new Color(.40f,.008f,.025f);
                Part(root,"Padded wrist cuff",PrimitiveType.Capsule,new Vector3(0,-.02f,-.09f),new Vector3(.29f,.25f,.23f),leather,Quaternion.Euler(90,0,0));
                Part(root,"Gold wrist strap",PrimitiveType.Cylinder,new Vector3(0,-.015f,-.10f),new Vector3(.312f,.026f,.30f),gold,Quaternion.Euler(90,0,0));
                Part(root,"Rounded leather knuckle chamber",PrimitiveType.Sphere,new Vector3(0,.045f,.18f),new Vector3(.44f,.37f,.32f),leather);
                Part(root,"Curled finger padding",PrimitiveType.Sphere,new Vector3(0,-.075f,.22f),new Vector3(.36f,.13f,.22f),new Color(.72f,.01f,.045f));
                Part(root,"Integrated curved thumb",PrimitiveType.Capsule,new Vector3(-.19f,-.06f,.12f),new Vector3(.14f,.22f,.18f),leather,Quaternion.Euler(32,0,-28));
                Part(root,"Thumb padded tip",PrimitiveType.Sphere,new Vector3(-.17f,-.125f,.23f),new Vector3(.15f,.15f,.17f),leather);
                var stitches=new CandyAccessoryMeshes.Builder();
                for(int i=0;i<24;i++)
                {
                    float angle=i*Mathf.PI*2/24;
                    var pos=new Vector3(Mathf.Cos(angle)*.148f,-.02f+Mathf.Sin(angle)*.116f,-.12f);
                    stitches.Beam(pos,pos+Vector3.forward*.022f,.0025f);
                }
                if(!cuffStitches)cuffStitches=stitches.Mesh("Boxing cuff stitches");
                MeshPart(root,"Hand sewn cuff stitches",cuffStitches,new Color(1,.83f,.45f));
                Part(root,"Gold glove crest",PrimitiveType.Sphere,new Vector3(.015f,.232f,.17f),new Vector3(.11f,.012f,.09f),gold);
                Part(root,"Inset crest",PrimitiveType.Sphere,new Vector3(.015f,.238f,.17f),new Vector3(.067f,.008f,.05f),seam);
            }
            else
            {
                Color color=value==EquipmentKind.MurderSword?new Color(1,.04f,.12f):new Color(.05f,.4f,1);
                Part(root,"Grip",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.065f,.11f,.065f),dark,Quaternion.Euler(90,0,0));
                Part(root,"Guard",PrimitiveType.Cube,new Vector3(0,0,.14f),new Vector3(.32f,.06f,.07f),gold);
                if(value==EquipmentKind.MurderSword){Part(root,"Blade silver fuller",PrimitiveType.Cube,new Vector3(0,.016f,.53f),new Vector3(.025f,.012f,.62f),new Color(.85f,.93f,1));Part(root,"Ruby pommel",PrimitiveType.Sphere,new Vector3(0,0,-.11f),new Vector3(.10f,.10f,.12f),color);Part(root,"Blade",PrimitiveType.Cube,new Vector3(0,0,.53f),new Vector3(.10f,.028f,.70f),color);Part(root,"Blade point",PrimitiveType.Sphere,new Vector3(0,0,.87f),new Vector3(.10f,.028f,.15f),color);}
                else{Part(root,"Shaft",PrimitiveType.Cylinder,new Vector3(0,0,.37f),new Vector3(.055f,.21f,.055f),gold,Quaternion.Euler(90,0,0));Part(root,"Sheriff hammer",PrimitiveType.Capsule,new Vector3(0,0,.63f),new Vector3(.27f,.56f,.27f),color,Quaternion.Euler(0,0,90));for(int side=-1;side<=1;side+=2){Part(root,"Gold hammer face",PrimitiveType.Cylinder,new Vector3(side*.28f,0,.63f),new Vector3(.29f,.022f,.29f),gold,Quaternion.Euler(0,0,90));Part(root,"Sheriff jewel",PrimitiveType.Sphere,new Vector3(side*.31f,0,.63f),new Vector3(.016f,.12f,.12f),cyan);}}
            }
            return root;
        }
        void MeshPart(Transform root,string name,Mesh mesh,Color color)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
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
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetFloat("_Smoothness",.9f);block.SetFloat("_Metallic",kind==EquipmentKind.BoxingGlove?.12f:.65f);renderer.SetPropertyBlock(block);
        }
    }
}
