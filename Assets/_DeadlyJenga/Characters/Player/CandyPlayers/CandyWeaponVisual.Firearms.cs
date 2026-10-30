using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        static readonly System.Collections.Generic.Dictionary<Vector2,Mesh> rings=new System.Collections.Generic.Dictionary<Vector2,Mesh>();
        static Mesh scopeTube;
        static readonly Color Steel=new Color(.12f,.19f,.27f),Chrome=new Color(.55f,.72f,.85f),Blue=new Color(.025f,.22f,.58f);
        void Ring(Transform owner,string name,Vector3 position,float radius,float thickness,Color color)
        {
            var key=new Vector2(radius,thickness);
            if(!rings.TryGetValue(key,out var mesh)||!mesh){var builder=new CandyAccessoryMeshes.Builder();builder.Ring(Vector3.zero,radius,thickness,Quaternion.identity);rings[key]=mesh=builder.Mesh("Shared weapon ring");}
            MeshPart(owner,name,mesh,color);owner.Find(name).localPosition=position;
        }
        void Optic(Transform owner,string name,Vector3 position,Quaternion rotation,float radius,bool tube)
        {
            var optic=new GameObject(name).transform;optic.SetParent(owner,false);optic.localPosition=position;optic.localRotation=rotation;
            Ring(optic,"Eyepiece",Vector3.zero,radius,.006f,Chrome);
            Ring(optic,"Objective",new Vector3(0,0,tube?.15f:.045f),radius,.006f,Steel);
            if(tube)
            {
                if(!scopeTube){var builder=new CandyAccessoryMeshes.Builder();for(int i=0;i<=15;i++)builder.Ring(new Vector3(0,0,i*.01f),radius,.006f,Quaternion.identity);scopeTube=builder.Mesh("Shared hollow rifle optic tube");}
                MeshPart(optic,"Hollow rifle scope tube",scopeTube,Steel);
            }
            Part(optic,"Optic mount",PrimitiveType.Cube,new Vector3(0,-radius-.04f,.06f),new Vector3(.025f,.10f,.10f),Steel);
            // Presentation finds this visual socket; it never controls weapon damage or aim.
            var lens=new GameObject("Optic lens").transform;lens.SetParent(optic,false);lens.localPosition=new Vector3(0,0,.008f);
            if(name=="Shotgun optic")Part(optic,"Red dot",PrimitiveType.Sphere,new Vector3(0,0,.004f),Vector3.one*.003f,Color.red);
            else
            {
                Part(optic,"Horizontal reticle",PrimitiveType.Cube,new Vector3(0,0,.004f),new Vector3(radius*1.4f,.0012f,.001f),Color.cyan);
                Part(optic,"Vertical reticle",PrimitiveType.Cube,new Vector3(0,0,.004f),new Vector3(.0012f,radius*1.4f,.001f),Color.cyan);
            }
        }
        void BuildRevolver(Transform root)
        {
            Part(root,"Western blue grip",PrimitiveType.Capsule,new Vector3(0,-.055f,-.025f),new Vector3(.08f,.22f,.105f),Blue,Quaternion.Euler(-16,0,0));
            Part(root,"Polished grip heel",PrimitiveType.Sphere,new Vector3(0,-.15f,-.055f),new Vector3(.09f,.045f,.12f),Chrome);
            Part(root,"Blue gunmetal frame",PrimitiveType.Cube,new Vector3(0,.10f,.12f),new Vector3(.115f,.10f,.27f),Blue);
            Part(root,"Six chamber cylinder",PrimitiveType.Cylinder,new Vector3(0,.12f,.14f),new Vector3(.155f,.073f,.155f),Steel,Quaternion.Euler(90,0,0));
            for(int i=0;i<6;i++)
            {
                float angle=i*Mathf.PI/3;var p=new Vector3(Mathf.Cos(angle)*.055f,.12f+Mathf.Sin(angle)*.055f,.215f);
                Part(root,"Chamber "+i,PrimitiveType.Cylinder,p,new Vector3(.033f,.004f,.033f),Color.black,Quaternion.Euler(90,0,0));
                Part(root,"Cylinder flute "+i,PrimitiveType.Capsule,p-new Vector3(0,0,.075f),new Vector3(.018f,.10f,.018f),Chrome,Quaternion.Euler(90,0,0));
            }
            Part(root,"Heavy octagonal barrel",PrimitiveType.Cube,new Vector3(0,.12f,.36f),new Vector3(.095f,.095f,.33f),Blue);
            Part(root,"Crowned shotgun muzzle",PrimitiveType.Cylinder,new Vector3(0,.12f,.52f),new Vector3(.11f,.014f,.11f),Chrome,Quaternion.Euler(90,0,0));
            Part(root,"Deep muzzle bore",PrimitiveType.Cylinder,new Vector3(0,.12f,.535f),new Vector3(.071f,.002f,.071f),Color.black,Quaternion.Euler(90,0,0));
            Part(root,"Under barrel rib",PrimitiveType.Cube,new Vector3(0,.063f,.37f),new Vector3(.055f,.035f,.27f),Chrome);
            Part(root,"Cocking hammer",PrimitiveType.Cube,new Vector3(0,.17f,-.01f),new Vector3(.028f,.065f,.045f),Chrome,Quaternion.Euler(-25,0,0));
            Ring(root,"Open trigger guard",new Vector3(0,-.015f,.115f),.055f,.009f,Chrome);root.Find("Open trigger guard").localRotation=Quaternion.Euler(0,90,0);
            Part(root,"Curved trigger",PrimitiveType.Capsule,new Vector3(0,-.01f,.09f),new Vector3(.015f,.055f,.015f),Steel,Quaternion.Euler(-20,0,0));
            for(int side=-1;side<=1;side+=2)Part(root,"Sheriff medallion "+side,PrimitiveType.Sphere,new Vector3(side*.044f,-.055f,-.02f),new Vector3(.008f,.04f,.04f),new Color(1,.72f,.15f));
            Optic(root,"Revolver optic",new Vector3(0,.23f,.17f),Quaternion.identity,.022f,false);
        }
        void BuildRifle(Transform root)
        {
            Part(root,"Pistol grip",PrimitiveType.Capsule,new Vector3(0,-.025f,0),new Vector3(.075f,.21f,.10f),Steel,Quaternion.Euler(-12,0,0));
            Part(root,"Gunmetal receiver",PrimitiveType.Capsule,new Vector3(0,.09f,.20f),new Vector3(.18f,.54f,.18f),Steel,Quaternion.Euler(90,0,0));
            Part(root,"Shoulder stock",PrimitiveType.Cube,new Vector3(0,.065f,-.23f),new Vector3(.12f,.16f,.35f),Steel);
            Part(root,"Rubber recoil pad",PrimitiveType.Capsule,new Vector3(0,.065f,-.41f),new Vector3(.145f,.23f,.065f),Color.black);
            Part(root,"Energy magazine",PrimitiveType.Cube,new Vector3(0,-.07f,.21f),new Vector3(.075f,.20f,.12f),Blue);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)Part(root,"Receiver vent",PrimitiveType.Cube,new Vector3(side*.092f,.10f,.10f+i*.055f),new Vector3(.009f,.045f,.025f),Blue);
            barrel=new GameObject("Rotating 180 degree barrel carriage").transform;barrel.SetParent(root,false);barrel.localPosition=new Vector3(0,.09f,.23f);barrel.localRotation=Quaternion.Euler(0,0,180);
            for(int side=-1;side<=1;side+=2)
            {
                bool shotgun=side==1;float length=shotgun?.43f:.70f,radius=shotgun?.105f:.065f;
                Part(barrel,shotgun?"Rapid shotgun barrel":"Precision rifle barrel",PrimitiveType.Cylinder,new Vector3(0,side*.06f,.07f+length*.5f),new Vector3(radius,length*.5f,radius),Steel,Quaternion.Euler(90,0,0));
                float end=.07f+length;
                Part(barrel,"Polished muzzle "+side,PrimitiveType.Cylinder,new Vector3(0,side*.06f,end-.01f),new Vector3(radius*1.18f,.025f,radius*1.18f),Chrome,Quaternion.Euler(90,0,0));
                Part(barrel,"Muzzle bore "+side,PrimitiveType.Cylinder,new Vector3(0,side*.06f,end+.002f),new Vector3(radius*.74f,.002f,radius*.74f),Color.black,Quaternion.Euler(90,0,0));
                Optic(barrel,shotgun?"Shotgun optic":"Rifle optic",new Vector3(0,side*.19f,.12f),Quaternion.Euler(0,0,shotgun?0:180),shotgun?.027f:.037f,!shotgun);
                if(!shotgun)
                {
                    var scope=barrel.Find("Rifle optic");
                    for(int i=0;i<6;i++)Ring(scope,"Pump-scope knurl "+i,new Vector3(0,0,.025f+i*.021f),.046f,.005f,Steel);
                    Part(scope,"Scope elevation dial",PrimitiveType.Cylinder,new Vector3(0,.049f,.08f),new Vector3(.035f,.012f,.035f),Chrome);
                }
            }
            Ring(barrel,"Chrome rotation bearing",Vector3.zero,.115f,.009f,Chrome);
        }
    }
}
