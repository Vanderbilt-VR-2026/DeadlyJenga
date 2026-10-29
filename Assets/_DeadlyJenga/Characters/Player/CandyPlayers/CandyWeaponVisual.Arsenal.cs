using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        Transform rotor,paired;float spin;
        static readonly Dictionary<string,Mesh> arsenalMeshes=new Dictionary<string,Mesh>();
        static readonly Color ArsenalSteel=new Color(.32f,.39f,.46f),Black=new Color(.025f,.035f,.045f),Orange=new Color(1,.28f,.035f),Neon=new Color(.10f,.9f,1),Wood=new Color(.30f,.095f,.035f);
        void BuildArsenalPieces(Transform r,EquipmentKind k)
        {
            rotor=null;
            void Box(string n,Vector3 p,Vector3 s,Color c)=>Part(r,n,PrimitiveType.Cube,p,s,c);
            void Tube(string n,Vector3 p,float radius,float length,Color c)=>Part(r,n,PrimitiveType.Cylinder,p,new Vector3(radius*2,length*.5f,radius*2),c,Quaternion.Euler(90,0,0));
            void Grip(Vector3 p)=>Part(r,"Textured ergonomic grip",PrimitiveType.Cube,p,new Vector3(.085f,.20f,.10f),Black,Quaternion.Euler(-12,0,0));
            if(k==EquipmentKind.RocketLauncher)
            {
                Tube("Long armored launch tube",new Vector3(0,.20f,.30f),.14f,1.40f,ArsenalSteel);Tube("Dark open muzzle",new Vector3(0,.20f,1.01f),.115f,.035f,Black);
                foreach(float z in new[]{-.40f,-.27f,.82f,.96f})Tube("Reinforced barrel collar",new Vector3(0,.20f,z),.16f,.065f,z>.8f?Orange:Black);
                foreach(float x in new[]{-.075f,.075f}){Part(r,"Twin launch muzzle",PrimitiveType.Cylinder,new Vector3(x,.20f,1.025f),new Vector3(.135f,.04f,.135f),Black,Quaternion.Euler(90,0,0));Part(r,"Twin muzzle bore",PrimitiveType.Cylinder,new Vector3(x,.20f,1.068f),new Vector3(.10f,.006f,.10f),ArsenalSteel,Quaternion.Euler(90,0,0));}
                for(int i=0;i<6;i++)Part(r,"Hazard stripe",PrimitiveType.Cube,new Vector3(-.11f+i*.044f,.365f,.17f),new Vector3(.025f,.012f,.12f),new Color(1,.83f,.05f),Quaternion.Euler(0,25,0));
                Box("Angular central receiver",new Vector3(0,.12f,.18f),new Vector3(.31f,.30f,.40f),ArsenalSteel);Grip(Vector3.zero);Grip(new Vector3(0,-.04f,.66f));
                Box("Shoulder saddle",new Vector3(0,-.03f,-.34f),new Vector3(.23f,.14f,.36f),Black);
                Box("Carry handle top",new Vector3(0,.44f,.12f),new Vector3(.10f,.04f,.31f),Black);
                foreach(float z in new[]{-.01f,.25f})Box("Carry handle riser",new Vector3(0,.39f,z),new Vector3(.10f,.11f,.035f),Black);
                Tube("Optical sight",new Vector3(-.19f,.34f,.13f),.055f,.25f,Black);Box("Power pack",new Vector3(.19f,.16f,.20f),new Vector3(.08f,.18f,.28f),Orange);
                for(int i=0;i<8;i++)Box("Rear cooling fins",new Vector3(0,.20f,-.37f+i*.025f),new Vector3(.34f,.32f,.008f),Black);
            }
            else if(k==EquipmentKind.DualUzi||k==EquipmentKind.StockPistol)
            {
                if(k==EquipmentKind.StockPistol)Part(r,"Rounded walnut broomhandle grip",PrimitiveType.Capsule,new Vector3(0,-.01f,-.015f),new Vector3(.09f,.23f,.10f),Wood,Quaternion.Euler(-14,0,0));else Grip(Vector3.zero);Box("Machined receiver",new Vector3(0,.12f,k==EquipmentKind.StockPistol?.065f:.17f),new Vector3(.10f,.12f,k==EquipmentKind.StockPistol?.25f:.40f),ArsenalSteel);
                Tube("Barrel",new Vector3(0,.12f,k==EquipmentKind.StockPistol?.34f:.42f),k==EquipmentKind.StockPistol?.025f:.035f,k==EquipmentKind.StockPistol?.32f:.15f,Black);Box("Inset muzzle",new Vector3(0,.12f,.503f),new Vector3(.04f,.04f,.012f),Black);
                Box("Trigger guard lower",new Vector3(0,-.025f,.12f),new Vector3(.035f,.025f,.12f),ArsenalSteel);
                Box("Trigger guard forward",new Vector3(0,.025f,.17f),new Vector3(.035f,.10f,.025f),ArsenalSteel);
                if(k==EquipmentKind.DualUzi){for(int spike=0;spike<5;spike++){var crystal=new CandyAccessoryMeshes.Builder();crystal.Cone(new Vector3(-.06f,.17f,-.04f+spike*.07f),new Vector3(-.06f,.35f,-.04f+spike*.07f),.025f,0,6);CachedMesh(r,"Neon green spike cartridge "+spike,crystal,new Color(.25f,1,.015f));}Box("Extended stick magazine",new Vector3(0,-.19f,.015f),new Vector3(.055f,.22f,.075f),Black);Box("Rear folded wire stock",new Vector3(0,.07f,-.16f),new Vector3(.09f,.025f,.25f),ArsenalSteel);}
                else
                {
                    Blade(r,"Walnut shoulder stock",new[]{new Vector2(.02f,-.10f),new Vector2(-.10f,-.10f),new Vector2(-.18f,-.67f),new Vector2(.08f,-.67f)},.065f,Wood);
                    Box("Stock attachment",new Vector3(0,-.045f,-.07f),new Vector3(.055f,.10f,.10f),ArsenalSteel);Box("Wide walnut shoulder plate",new Vector3(0,-.04f,-.68f),new Vector3(.075f,.28f,.025f),Wood);
                    Box("Forward fixed magazine box",new Vector3(0,.045f,.22f),new Vector3(.085f,.15f,.12f),ArsenalSteel);Box("Rear exposed hammer",new Vector3(0,.22f,-.035f),new Vector3(.028f,.065f,.04f),Black);Box("Checkered walnut grip",new Vector3(.048f,-.015f,0),new Vector3(.012f,.14f,.075f),Wood);
                    for(int i=0;i<6;i++)Box("Slide serration",new Vector3(.055f,.12f,-.015f+i*.012f),new Vector3(.008f,.075f,.005f),Black);
                    Box("Selector lever",new Vector3(-.061f,.13f,.02f),new Vector3(.018f,.028f,.065f),Orange);
                }
                Box("Rear sight",new Vector3(0,.20f,-.015f),new Vector3(.06f,.03f,.02f),Black);Box("Front sight",new Vector3(0,k==EquipmentKind.StockPistol?.16f:.20f,k==EquipmentKind.StockPistol?.45f:.33f),new Vector3(.015f,.03f,.018f),k==EquipmentKind.StockPistol?Black:Neon);
            }
            else if(k==EquipmentKind.ChainGun)
            {
                Grip(Vector3.zero);Box("Motor housing",new Vector3(0,.18f,.20f),new Vector3(.32f,.30f,.45f),ArsenalSteel);
                rotor=new GameObject("Spinning six-barrel assembly").transform;rotor.SetParent(r,false);rotor.localPosition=new Vector3(0,.20f,.40f);
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Part(rotor,"Rotating barrel",PrimitiveType.Cylinder,new Vector3(Mathf.Cos(a)*.09f,Mathf.Sin(a)*.09f,.28f),new Vector3(.045f,.31f,.045f),Black,Quaternion.Euler(90,0,0));}
                foreach(float z in new[]{.48f,.89f})Tube("Barrel retention ring",new Vector3(0,.20f,z),.145f,.045f,ArsenalSteel);
                Box("Ammunition drum",new Vector3(.24f,.12f,.12f),new Vector3(.22f,.30f,.32f),Orange);Box("Rear shoulder rest",new Vector3(0,.10f,-.20f),new Vector3(.22f,.20f,.30f),Black);Grip(new Vector3(0,-.02f,.42f));
            }
            else if(k==EquipmentKind.GrenadeLauncher)
            {
                Grip(Vector3.zero);Tube("Large bore grenade barrel",new Vector3(0,.15f,.47f),.075f,.60f,ArsenalSteel);Tube("Recessed bore",new Vector3(0,.15f,.777f),.060f,.015f,Black);
                Part(r,"Six chamber grenade drum",PrimitiveType.Cylinder,new Vector3(0,.11f,.12f),new Vector3(.30f,.14f,.30f),Orange,Quaternion.Euler(90,0,0));
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Tube("Chamber",new Vector3(Mathf.Cos(a)*.11f,.11f+Mathf.Sin(a)*.11f,.16f),.035f,.25f,Black);}
                Box("Shoulder stock",new Vector3(0,.06f,-.26f),new Vector3(.12f,.14f,.36f),Black);Box("Raised sight rail",new Vector3(0,.29f,.26f),new Vector3(.07f,.04f,.42f),Black);
            }
            else if(k==EquipmentKind.NeedleDrill)
            {
                Grip(Vector3.zero);Part(r,"Rounded orange drill shell",PrimitiveType.Capsule,new Vector3(0,.12f,.10f),new Vector3(.18f,.34f,.20f),Orange,Quaternion.Euler(90,0,0));Box("Inset rubber motor plate",new Vector3(.092f,.13f,.07f),new Vector3(.012f,.12f,.19f),Black);
                Box("Rubber grip insert",new Vector3(.045f,-.015f,-.02f),new Vector3(.035f,.16f,.07f),Black);Box("Battery pack",new Vector3(0,-.16f,.015f),new Vector3(.21f,.10f,.24f),Black);Box("Battery charge stripe",new Vector3(0,-.104f,.015f),new Vector3(.21f,.02f,.22f),Orange);
                Tube("Torque collar",new Vector3(0,.12f,.30f),.09f,.12f,Black);Tube("Rotating chuck",new Vector3(0,.12f,.40f),.065f,.10f,ArsenalSteel);
                rotor=new GameObject("Helical spinning drill bit").transform;rotor.SetParent(r,false);rotor.localPosition=new Vector3(0,.12f,.46f);
                var bit=new CandyAccessoryMeshes.Builder();bit.Cone(Vector3.zero,Vector3.forward*.25f,.023f,0,10);
                for(int i=0;i<25;i++){float a=i*.7f;bit.Beam(new Vector3(Mathf.Cos(a)*.023f,Mathf.Sin(a)*.023f,i*.008f),new Vector3(Mathf.Cos(a+.7f)*.023f,Mathf.Sin(a+.7f)*.023f,(i+1)*.008f),.006f);}CachedMesh(rotor,"Needle drill helical bit",bit,ArsenalSteel);
                for(int i=0;i<4;i++)Box("Motor vent",new Vector3(.094f,.13f,-.025f+i*.025f),new Vector3(.008f,.085f,.010f),Black);
            }
            else if(k==EquipmentKind.LaserClaws)
            {
                for(int i=0;i<3;i++)
                {
                    var blade=new GameObject("Between-finger plasma claw "+i).transform;blade.SetParent(r,false);
                    MeshPart(blade,"White-hot plasma and cyan corona",PlasmaClaw(i),Neon);
                    var renderer=blade.GetChild(0).GetComponent<MeshRenderer>();var surface=PlasmaSurface();
                    if(surface)renderer.sharedMaterial=surface;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                PositionClaws(r,new CandyHandPose(),false);
            }
            else if(k==EquipmentKind.WristLaser)
            {
                Part(r,"Armored wrist cuff",PrimitiveType.Cylinder,new Vector3(0,0,-.10f),new Vector3(.22f,.16f,.22f),ArsenalSteel,Quaternion.Euler(90,0,0));
                Box("Forearm emitter body",new Vector3(0,.08f,-.045f),new Vector3(.22f,.09f,.32f),Black);
                Tube("Focused beam emitter",new Vector3(0,.085f,.105f),.045f,.09f,Orange);Tube("Luminous lens",new Vector3(0,.085f,.155f),.032f,.012f,Orange);
            }
            else if(k==EquipmentKind.FlamingMarshmallow)BuildRoastingFork(r);
            else if(k==EquipmentKind.SawStaff||k==EquipmentKind.BarbedBat||k==EquipmentKind.Chainsaw||k==EquipmentKind.LaserAxe||k==EquipmentKind.Dagger)
            {
                float length=k==EquipmentKind.Dagger?.15f:.75f;
                Tube("Handle",new Vector3(0,0,length*.35f),k==EquipmentKind.BarbedBat?.035f:.025f,length+.20f,k==EquipmentKind.LaserAxe?Wood:Black);
                for(int i=0;i<5;i++)Tube("Grip wrap",new Vector3(0,0,-.055f+i*.025f),.030f,.008f,ArsenalSteel);
                if(k==EquipmentKind.BarbedBat)
                {
                    Part(r,"Solid wooden bat head",PrimitiveType.Capsule,new Vector3(0,0,.58f),new Vector3(.14f,.61f,.14f),Wood,Quaternion.Euler(90,0,0));
                    var wire=new CandyAccessoryMeshes.Builder();for(int i=0;i<130;i++){float a=i*.34f;var p=new Vector3(Mathf.Cos(a)*.077f,Mathf.Sin(a)*.077f,.30f+i*.004f);var q=new Vector3(Mathf.Cos(a+.34f)*.077f,Mathf.Sin(a+.34f)*.077f,.30f+(i+1)*.004f);wire.Beam(p,q,.006f);if(i%8==0)wire.Cone(p,p+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.045f,.008f,0,4);}CachedMesh(r,"Coiled barbed wire",wire,ArsenalSteel);
                    var nails=new CandyAccessoryMeshes.Builder();for(int i=0;i<12;i++){float a=i*2.4f;var p=new Vector3(Mathf.Cos(a)*.05f,Mathf.Sin(a)*.05f,.4f+i*.035f);nails.Cone(p,p+new Vector3(Mathf.Cos(a),Mathf.Sin(a),.1f)*.1f,.013f,0,5);}CachedMesh(r,"Protruding metal nails",nails,ArsenalSteel);
                }
                if(k==EquipmentKind.SawStaff||k==EquipmentKind.Chainsaw)
                {
                    if(k==EquipmentKind.Chainsaw){Box("Orange engine casing",new Vector3(0,.03f,.23f),new Vector3(.23f,.22f,.28f),Orange);Part(r,"Rounded chainsaw bar",PrimitiveType.Capsule,new Vector3(0,0,.64f),new Vector3(.17f,.66f,.045f),ArsenalSteel,Quaternion.Euler(90,0,0));}
                    rotor=new GameObject("Spinning toothed cutting wheel").transform;rotor.SetParent(r,false);rotor.localPosition=new Vector3(0,0,k==EquipmentKind.SawStaff?.88f:.78f);
                    var teeth=new CandyAccessoryMeshes.Builder();teeth.Cone(new Vector3(-.025f,0,0),new Vector3(.025f,0,0),.24f,.24f,32);
                    for(int i=0;i<32;i++){float a=i*Mathf.PI/16;teeth.Cone(new Vector3(0,Mathf.Cos(a)*.23f,Mathf.Sin(a)*.23f),new Vector3(0,Mathf.Cos(a+.035f)*.29f,Mathf.Sin(a+.035f)*.29f),.025f,0,3);}CachedMesh(rotor,"Serrated saw wheel",teeth,ArsenalSteel);Part(rotor,"Bright wheel axle",PrimitiveType.Sphere,Vector3.zero,new Vector3(.09f,.09f,.09f),Orange);
                    if(k==EquipmentKind.Chainsaw)for(int i=0;i<12;i++)Box("Chain tooth",new Vector3(0,(i%2==0?1:-1)*.09f,.40f+i*.045f),new Vector3(.06f,.028f,.028f),Black);
                }
                if(k==EquipmentKind.Dagger){Blade(r,"Pointed steel dagger",new[]{new Vector2(-.055f,.08f),new Vector2(.055f,.08f),new Vector2(0,.39f)},.025f,ArsenalSteel);Box("Dagger guard",new Vector3(0,0,.075f),new Vector3(.16f,.035f,.035f),Orange);}
                if(k==EquipmentKind.LaserAxe){Blade(r,"Massive swept axe blade",new[]{new Vector2(-.08f,.70f),new Vector2(.08f,.70f),new Vector2(.44f,.88f),new Vector2(.38f,1.02f),new Vector2(-.38f,1.02f),new Vector2(-.44f,.88f)},.07f,ArsenalSteel);Box("Full width laser edge",new Vector3(0,0,.98f),new Vector3(.028f,.80f,.03f),Neon);Box("Rear hammer counterweight",new Vector3(0,0,.62f),new Vector3(.20f,.16f,.22f),ArsenalSteel);}
            }
        }

        static Mesh[] stockPistolMeshes;
        static Material[] stockPistolSurfaces;
        void BuildStockPistol(Transform root)
        {
            if(stockPistolMeshes==null)
            {
                var groups=new List<CombineInstance>[6];for(int i=0;i<groups.Length;i++)groups[i]=new List<CombineInstance>();
                var generated=new List<Mesh>();
                void Solid(string name,int surface,float width,float bevel,params Vector2[] outline)
                {
                    var mesh=PistolSolid(name,width,bevel,outline);generated.Add(mesh);
                    groups[surface].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});
                }
                void Box(string name,int surface,Vector3 p,Vector3 size,float bevel=.003f)
                {
                    var mesh=PistolSolid(name,size.x,bevel,new[]{new Vector2(p.y-size.y*.5f,p.z-size.z*.5f),new Vector2(p.y-size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z-size.z*.5f)});generated.Add(mesh);
                    groups[surface].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.Translate(new Vector3(p.x,0,0))});
                }
                void Cylinder(int surface,Vector3 a,Vector3 b,float radius,float endRadius=-1)
                {
                    var builder=new CandyAccessoryMeshes.Builder();builder.Cone(a,b,radius,endRadius<0?radius:endRadius,32);var mesh=builder.Mesh("Turned pistol hardware");
                    var uv=new Vector2[mesh.vertexCount];var vs=mesh.vertices;for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vs[i].z*4,vs[i].y*4);mesh.uv=uv;generated.Add(mesh);
                    groups[surface].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});
                }
                // Each outline is convex; the narrow bevel catches real scene light.
                Solid("Sculpted pistol receiver",0,.103f,.006f,new Vector2(.072f,-.075f),new Vector2(.064f,.235f),new Vector2(.105f,.265f),new Vector2(.177f,.205f),new Vector2(.188f,-.057f));
                Box("Bolt running along top",1,new Vector3(0,.174f,.068f),new Vector3(.083f,.032f,.265f));
                Box("Barrel shoulder",0,new Vector3(0,.139f,.239f),new Vector3(.082f,.078f,.058f),.005f);
                Cylinder(0,new Vector3(0,.14f,.263f),new Vector3(0,.14f,.485f),.030f,.023f);
                Cylinder(1,new Vector3(0,.14f,.478f),new Vector3(0,.14f,.506f),.028f);
                var bore=PistolBore();generated.Add(bore);groups[1].Add(new CombineInstance{mesh=bore,transform=Matrix4x4.identity});
                Cylinder(2,new Vector3(0,.14f,.475f),new Vector3(0,.14f,.476f),.015f);
                Solid("Swept full-size grip frame",0,.082f,.005f,new Vector2(-.142f,-.094f),new Vector2(-.147f,-.014f),new Vector2(.079f,.055f),new Vector2(.102f,-.047f));
                foreach(float side in new[]{-1f,1f})
                {
                    // Raised walnut panels leave a visible metal perimeter.
                    var panel=PistolSolid("Checkered walnut grip panel",.012f,.003f,new[]{new Vector2(-.126f,-.082f),new Vector2(-.129f,-.025f),new Vector2(.057f,.034f),new Vector2(.071f,-.037f)});generated.Add(panel);
                    groups[3].Add(new CombineInstance{mesh=panel,transform=Matrix4x4.Translate(new Vector3(side*.044f,0,0))});
                    foreach(float y in new[]{-.092f,.035f})
                    {
                        float z=-.044f+(y+.092f)*.28f;
                        Cylinder(1,new Vector3(side*.048f,y,z),new Vector3(side*.053f,y,z),.008f);
                        Box("Slotted grip screw",2,new Vector3(side*.054f,y,z),new Vector3(.001f,.002f,.010f),.0002f);
                    }
                    for(int i=0;i<9;i++)
                    {
                        float z=-.042f+i*.010f;
                        Box("Deep rear bolt serration",2,new Vector3(side*.043f,.175f,z),new Vector3(.003f,.026f,.003f),.0005f);
                    }
                    Box("Receiver inset side plate",1,new Vector3(side*.053f,.120f,.111f),new Vector3(.006f,.047f,.155f),.002f);
                    for(int i=0;i<3;i++)Box("Machined side vent",2,new Vector3(side*.057f,.119f,.124f+i*.021f),new Vector3(.001f,.018f,.009f),.001f);
                }
                // Open guard, a curved trigger and a separate forward magazine.
                Cylinder(0,new Vector3(0,.063f,.051f),new Vector3(0,.021f,.077f),.010f);
                Cylinder(0,new Vector3(0,.021f,.077f),new Vector3(0,-.022f,.132f),.010f);
                Cylinder(0,new Vector3(0,-.022f,.132f),new Vector3(0,-.013f,.194f),.010f);
                Cylinder(0,new Vector3(0,-.013f,.194f),new Vector3(0,.068f,.204f),.010f);
                Cylinder(1,new Vector3(0,.061f,.104f),new Vector3(0,.015f,.112f),.007f);
                Cylinder(1,new Vector3(0,.015f,.112f),new Vector3(0,.006f,.127f),.007f);
                Box("Forward magazine",0,new Vector3(0,.021f,.233f),new Vector3(.075f,.101f,.065f),.005f);
                Box("Magazine floor plate",1,new Vector3(0,-.032f,.232f),new Vector3(.084f,.014f,.076f),.003f);
                // Open rear notch and front post keep the sight line readable.
                foreach(float side in new[]{-1f,1f})Box("Rear notch ear",2,new Vector3(side*.023f,.211f,-.045f),new Vector3(.017f,.031f,.028f),.002f);
                Box("Front sight base",1,new Vector3(0,.169f,.462f),new Vector3(.032f,.014f,.033f),.002f);
                Box("Front sight blade",2,new Vector3(0,.194f,.462f),new Vector3(.011f,.040f,.020f),.001f);
                Box("Ivory sight dot",5,new Vector3(0,.202f,.450f),new Vector3(.005f,.008f,.002f),.0005f);
                Solid("Exposed cocking hammer",1,.027f,.002f,new Vector2(.163f,-.075f),new Vector2(.180f,-.042f),new Vector2(.233f,-.076f),new Vector2(.222f,-.101f));
                Cylinder(1,new Vector3(-.058f,.089f,-.030f),new Vector3(.058f,.089f,-.030f),.010f);
                Cylinder(1,new Vector3(-.057f,.117f,-.020f),new Vector3(-.063f,.117f,-.020f),.012f);
                Box("Fire selector lever",4,new Vector3(-.064f,.136f,-.006f),new Vector3(.009f,.014f,.047f),.002f);
                Box("Burst selector witness",5,new Vector3(-.058f,.153f,.021f),new Vector3(.002f,.006f,.014f),.001f);
                // Oversized wooden shoulder stock, slim neck and a generous butt.
                Box("Steel stock mounting shoe",1,new Vector3(0,-.088f,-.085f),new Vector3(.071f,.084f,.060f),.004f);
                Solid("Flowing walnut shoulder stock",3,.083f,.008f,new Vector2(-.143f,-.095f),new Vector2(-.193f,-.685f),new Vector2(.081f,-.715f),new Vector2(.070f,-.570f),new Vector2(-.034f,-.195f),new Vector2(-.065f,-.095f));
                Box("Dark recoil butt pad",2,new Vector3(0,-.055f,-.709f),new Vector3(.094f,.285f,.036f),.007f);
                Box("Polished butt rim",1,new Vector3(0,-.055f,-.686f),new Vector3(.089f,.267f,.010f),.002f);
                for(int i=0;i<10;i++)Box("Recoil pad groove",1,new Vector3(0,-.172f+i*.025f,-.728f),new Vector3(.076f,.004f,.002f),.0005f);
                foreach(float side in new[]{-1f,1f})
                {
                    Box("Inset stock accent",4,new Vector3(side*.043f,-.052f,-.568f),new Vector3(.002f,.014f,.110f),.001f);
                    Cylinder(1,new Vector3(side*.042f,-.104f,-.188f),new Vector3(side*.046f,-.104f,-.188f),.009f);
                    Cylinder(1,new Vector3(side*.043f,-.132f,-.625f),new Vector3(side*.047f,-.132f,-.625f),.009f);
                }
                stockPistolMeshes=new Mesh[groups.Length];
                for(int i=0;i<groups.Length;i++)
                {
                    var mesh=new Mesh{name="Stock pistol surface "+i};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(groups[i].ToArray(),true,true);mesh.RecalculateTangents();mesh.RecalculateBounds();stockPistolMeshes[i]=mesh;
                }
                foreach(var mesh in generated)Object.Destroy(mesh);
            }
            var surfaces=StockPistolSurfaces();
            for(int i=0;i<stockPistolMeshes.Length;i++)
            {
                var go=new GameObject(stockPistolMeshes[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.GetComponent<MeshFilter>().sharedMesh=stockPistolMeshes[i];go.GetComponent<MeshRenderer>().sharedMaterial=surfaces[i];
            }
        }
        static Mesh PistolSolid(string name,float width,float bevel,Vector2[] outline)
        {
            float area=0;for(int i=0;i<outline.Length;i++){var a=outline[i];var b=outline[(i+1)%outline.Length];area+=a.x*b.y-b.x*a.y;}
            if(area<0){outline=(Vector2[])outline.Clone();System.Array.Reverse(outline);}
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();Vector2 center=Vector2.zero;foreach(var p in outline)center+=p;center/=outline.Length;
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                int i=vertices.Count;vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.z*3,a.y*3),new Vector2(b.z*3,b.y*3),new Vector2(c.z*3,c.y*3)});triangles.AddRange(new[]{i,i+1,i+2});
            }
            var ring=new Vector3[4,outline.Length];
            for(int r=0;r<4;r++)for(int i=0;i<outline.Length;i++)
            {
                Vector2 p=outline[i];if(r==0||r==3)p+= (center-p).normalized*bevel;
                float x=r==0?-width*.5f:r==1?-width*.5f+bevel:r==2?width*.5f-bevel:width*.5f;
                ring[r,i]=new Vector3(x,p.x,p.y);
            }
            // Outlines wind counterclockwise in the side elevation.
            for(int i=1;i<outline.Length-1;i++){Triangle(ring[0,0],ring[0,i+1],ring[0,i]);Triangle(ring[3,0],ring[3,i],ring[3,i+1]);}
            for(int r=0;r<3;r++)for(int i=0;i<outline.Length;i++){int j=(i+1)%outline.Length;Triangle(ring[r,i],ring[r,j],ring[r+1,j]);Triangle(ring[r,i],ring[r+1,j],ring[r+1,i]);}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh PistolBore()
        {
            var vs=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();
            for(int ring=0;ring<3;ring++)for(int i=0;i<=48;i++)
            {
                float a=i*Mathf.PI*2/48,r=ring==0?.028f:.015f,z=ring==2?.475f:.507f;vs.Add(new Vector3(Mathf.Cos(a)*r,.14f+Mathf.Sin(a)*r,z));uv.Add(new Vector2((float)i/48,ring*.5f));
                if(ring<2&&i<48){int n=ring*49+i;tris.AddRange(new[]{n,n+1,n+49,n+1,n+50,n+49});}
            }
            var mesh=new Mesh{name="Recessed muzzle crown and dark rifled bore"};mesh.SetVertices(vs);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();return mesh;
        }
        static Material[] StockPistolSurfaces()
        {
            if(stockPistolSurfaces!=null)return stockPistolSurfaces;
            stockPistolSurfaces=new Material[6];
            var colors=new[]{new Color(.19f,.25f,.30f),new Color(.46f,.52f,.58f),new Color(.025f,.028f,.031f),Color.white,new Color(.65f,.37f,.13f),new Color(.93f,.90f,.72f)};
            const int size=512;var metal=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Fine directional brushed steel",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var wood=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Varnished walnut grain and checkering",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var normal=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Subtle steel machining normal",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var mp=new Color[size*size];var wp=new Color[mp.Length];var np=new Color[mp.Length];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(float)x/size,v=(float)y/size;
                float grain=Mathf.PerlinNoise(u*18,v*320),warp=Mathf.PerlinNoise(u*4,v*6);
                float veins=.5f+.5f*Mathf.Sin(v*310+warp*16+Mathf.Sin(u*15)*3);
                float fine=Mathf.PerlinNoise(u*22,v*180),check=Mathf.Pow(Mathf.Abs(Mathf.Sin((u+v)*180)*Mathf.Sin((u-v)*180)),.4f);
                int i=y*size+x;float m=.90f+grain*.10f;mp[i]=new Color(m,m,m,1);
                wp[i]=Color.Lerp(new Color(.12f,.036f,.014f),new Color(.46f,.22f,.075f),.25f+veins*.46f+fine*.15f)*( .90f+check*.10f);
                float n=.5f+(grain-.5f)*.045f;np[i]=new Color(n,.5f,1,n);
            }
            metal.SetPixels(mp);metal.Apply(true,true);wood.SetPixels(wp);wood.Apply(true,true);normal.SetPixels(np);normal.Apply(true,true);
            for(int i=0;i<6;i++)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit");var surface=new Material(shader){name=new[]{"Satin blued gunmetal","Polished steel edges","Matte rubber and recesses","Oiled checkered walnut","Warm brass controls","Ivory sight markers"}[i],enableInstancing=true};
                surface.SetColor("_BaseColor",colors[i]);surface.SetTexture("_BaseMap",i==3?wood:metal);surface.SetFloat("_Metallic",i==3||i==2||i==5?0:.88f);surface.SetFloat("_Smoothness",i==3?.43f:i==2?.24f:i==0?.56f:.75f);
                if(i!=2&&i!=5){surface.SetTexture("_BumpMap",normal);surface.SetFloat("_BumpScale",.22f);surface.EnableKeyword("_NORMALMAP");}
                stockPistolSurfaces[i]=surface;
            }
            return stockPistolSurfaces;
        }

        static Mesh[] needleDrillMeshes;
        static Material[] needleDrillSurfaces;
        void BuildNeedleDrill(Transform root)
        {
            if(needleDrillMeshes==null)
            {
                var groups=new List<CombineInstance>[8];for(int i=0;i<groups.Length;i++)groups[i]=new List<CombineInstance>();
                var temporary=new List<Mesh>();
                void Add(int group,Mesh mesh,Matrix4x4 transform)
                {temporary.Add(mesh);groups[group].Add(new CombineInstance{mesh=mesh,transform=transform});}
                void Box(int group,Vector3 p,Vector3 size,float bevel=.003f)
                {
                    Add(group,PistolSolid("Beveled drill molding",size.x,bevel,new[]{new Vector2(p.y-size.y*.5f,p.z-size.z*.5f),new Vector2(p.y-size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z-size.z*.5f)}),Matrix4x4.Translate(new Vector3(p.x,0,0)));
                }
                void Cylinder(int group,Vector3 a,Vector3 b,float radius,float endRadius=-1)
                {
                    var builder=new CandyAccessoryMeshes.Builder();builder.Cone(a,b,radius,endRadius<0?radius:endRadius,32);var mesh=builder.Mesh("Drill turned hardware");
                    var uv=new Vector2[mesh.vertexCount];var v=mesh.vertices;for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(v[i].z*4,v[i].y*4);mesh.uv=uv;Add(group,mesh,Matrix4x4.identity);
                }
                void Solid(int group,float x,float width,float bevel,params Vector2[] outline)
                {Add(group,PistolSolid("Contoured drill insert",width,bevel,outline),Matrix4x4.Translate(new Vector3(x,0,0)));}
                // Elliptical motor shell, shaped around the grip instead of a primitive box.
                Add(0,DrillTurned("Molded orange motor housing",new[]{-.194f,-.190f,-.177f,-.145f,-.08f,.08f,.155f,.19f,.211f},new[]{0f,.045f,.079f,.096f,.101f,.100f,.086f,.071f,.067f},.94f),Matrix4x4.Translate(new Vector3(0,.145f,0)));
                Add(1,DrillTurned("Rear impact bumper",new[]{-.199f,-.197f,-.190f,-.181f},new[]{.005f,.052f,.067f,.071f},.94f),Matrix4x4.Translate(new Vector3(0,.145f,0)));
                Solid(0,0,.095f,.013f,new Vector2(-.183f,-.135f),new Vector2(-.187f,-.042f),new Vector2(.080f,.038f),new Vector2(.101f,-.060f));
                foreach(float side in new[]{-1f,1f})
                {
                    Solid(1,side*.049f,.018f,.005f,new Vector2(-.158f,-.121f),new Vector2(-.163f,-.060f),new Vector2(.046f,.006f),new Vector2(.069f,-.042f));
                    // Recessed black side panel and orange inset make the casing read as two molded halves.
                    Solid(1,side*.096f,.014f,.005f,new Vector2(.106f,-.130f),new Vector2(.104f,.073f),new Vector2(.125f,.114f),new Vector2(.180f,.093f),new Vector2(.193f,-.105f));
                    Box(0,new Vector3(side*.106f,.148f,-.015f),new Vector3(.009f,.041f,.153f),.002f);
                    Box(2,new Vector3(side*.111f,.147f,-.015f),new Vector3(.002f,.026f,.121f),.001f);
                    // Small machined badge bars; no text or UI components on the weapon.
                    for(int i=0;i<4;i++)Box(4,new Vector3(side*.113f,.147f,-.053f+i*.021f),new Vector3(.0015f,.013f,.008f),.0003f);
                    for(int i=0;i<5;i++)Box(2,new Vector3(side*.085f,.185f,-.132f+i*.018f),new Vector3(.027f,.029f,.006f),.001f);
                    foreach(var screw in new[]{new Vector2(.095f,-.120f),new Vector2(.201f,-.105f),new Vector2(.109f,.112f)})
                    {
                        Cylinder(3,new Vector3(side*.091f,screw.x,screw.y),new Vector3(side*.097f,screw.x,screw.y),.006f);
                        Box(2,new Vector3(side*.098f,screw.x,screw.y),new Vector3(.001f,.0015f,.008f),.0002f);
                    }
                    for(int i=0;i<7;i++)Box(2,new Vector3(side*.059f,-.130f+i*.022f,-.091f+i*.006f),new Vector3(.004f,.006f,.043f),.001f);
                }
                Box(1,new Vector3(0,.028f,.049f),new Vector3(.071f,.067f,.055f),.008f);
                Box(3,new Vector3(.055f,.062f,-.004f),new Vector3(.023f,.020f,.028f),.003f);
                // Battery rail, removable-looking pack, seam, latch and charge indicator.
                Box(2,new Vector3(0,-.185f,-.081f),new Vector3(.114f,.029f,.170f),.005f);
                Box(0,new Vector3(0,-.218f,-.077f),new Vector3(.206f,.056f,.242f),.012f);
                Box(1,new Vector3(0,-.266f,-.078f),new Vector3(.216f,.060f,.254f),.012f);
                Box(2,new Vector3(0,-.245f,-.078f),new Vector3(.218f,.007f,.254f),.002f);
                Box(0,new Vector3(0,-.263f,.052f),new Vector3(.112f,.027f,.010f),.003f);
                foreach(float side in new[]{-1f,1f})
                {
                    Box(0,new Vector3(side*.110f,-.265f,-.080f),new Vector3(.007f,.034f,.140f),.003f);
                    Box(2,new Vector3(side*.114f,-.265f,-.080f),new Vector3(.003f,.020f,.110f),.001f);
                }
                Box(2,new Vector3(0,-.223f,.046f),new Vector3(.090f,.024f,.012f),.002f);
                for(int i=0;i<3;i++)Box(5,new Vector3(-.025f+i*.025f,-.221f,.054f),new Vector3(.014f,.008f,.003f),.001f);
                // Work light beneath the barrel, a rubberized torque ring, and engraved setting marks.
                Cylinder(2,new Vector3(0,.084f,.182f),new Vector3(0,.084f,.220f),.021f);
                Cylinder(4,new Vector3(0,.084f,.220f),new Vector3(0,.084f,.222f),.014f);
                Cylinder(3,new Vector3(0,.145f,.204f),new Vector3(0,.145f,.225f),.069f);
                Cylinder(1,new Vector3(0,.145f,.225f),new Vector3(0,.145f,.308f),.067f,.063f);
                Cylinder(3,new Vector3(0,.145f,.301f),new Vector3(0,.145f,.316f),.063f);
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI/12;Vector3 axis=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                    Cylinder(2,new Vector3(0,.145f,.238f)+axis*.064f,new Vector3(0,.145f,.292f)+axis*.061f,.003f);
                    if(i%2==0)Cylinder(4,new Vector3(0,.145f,.219f)+axis*.069f,new Vector3(0,.145f,.229f)+axis*.069f,.0018f);
                }
                // The chuck and bit share the existing spinning assembly and held-trigger behavior.
                Cylinder(6,new Vector3(0,0,0),new Vector3(0,0,.073f),.056f,.047f);
                Cylinder(7,new Vector3(0,0,.063f),new Vector3(0,0,.096f),.043f,.029f);
                Cylinder(6,new Vector3(0,0,.088f),new Vector3(0,0,.109f),.027f);
                for(int i=0;i<20;i++)
                {
                    float a=i*Mathf.PI/10;Vector3 axis=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                    Cylinder(7,axis*.054f+Vector3.forward*.009f,axis*.047f+Vector3.forward*.059f,.0024f);
                }
                for(int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3;Vector3 axis=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                    Cylinder(7,axis*.015f+Vector3.forward*.093f,axis*.010f+Vector3.forward*.111f,.006f);
                }
                Add(7,DrillFlutedBit(),Matrix4x4.identity);
                needleDrillMeshes=new Mesh[groups.Length];
                for(int i=0;i<groups.Length;i++)
                {
                    var mesh=new Mesh{name=i<6?"Detailed drill body surface "+i:"Rotating drill chuck surface "+i};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.CombineMeshes(groups[i].ToArray(),true,true);mesh.RecalculateTangents();mesh.RecalculateBounds();needleDrillMeshes[i]=mesh;
                }
                foreach(var mesh in temporary)Object.Destroy(mesh);
            }
            rotor=new GameObject("Spinning machined chuck and helical needle bit").transform;rotor.SetParent(root,false);rotor.localPosition=new Vector3(0,.145f,.316f);
            var surfaces=DrillSurfaces();
            for(int i=0;i<needleDrillMeshes.Length;i++)
            {
                var go=new GameObject(needleDrillMeshes[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(i<6?root:rotor,false);
                go.GetComponent<MeshFilter>().sharedMesh=needleDrillMeshes[i];go.GetComponent<MeshRenderer>().sharedMaterial=surfaces[i];
            }
        }
        static Mesh DrillTurned(string name,float[] depth,float[] radius,float squash)
        {
            const int sides=48;var vertices=new Vector3[depth.Length*(sides+1)];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int ring=0;ring<depth.Length;ring++)for(int side=0;side<=sides;side++)
            {
                float a=side*Mathf.PI*2/sides;int i=ring*(sides+1)+side;
                vertices[i]=new Vector3(Mathf.Cos(a)*radius[ring],Mathf.Sin(a)*radius[ring]*squash,depth[ring]);uv[i]=new Vector2((float)side/sides,(depth[ring]+.20f)*2.5f);
                if(ring<depth.Length-1&&side<sides){int j=i+sides+1;triangles.AddRange(new[]{i,i+1,j,i+1,j+1,j});}
            }
            var mesh=new Mesh{name=name,vertices=vertices,uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();
            var normals=mesh.normals;for(int r=0;r<depth.Length;r++){int a=r*(sides+1),b=a+sides;normals[a]=normals[b]=(normals[a]+normals[b]).normalized;}mesh.normals=normals;return mesh;
        }
        static Mesh DrillFlutedBit()
        {
            const int rings=73,sides=32;var vs=new Vector3[rings*(sides+1)];var uv=new Vector2[vs.Length];var triangles=new List<int>();
            for(int ring=0;ring<rings;ring++)for(int side=0;side<=sides;side++)
            {
                float t=(float)ring/(rings-1),a=side*Mathf.PI*2/sides,z=.102f+t*.275f;
                float flute=.68f+.32f*Mathf.Cos(2*a-t*28),tip=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1,t));
                float r=.012f*(t<.08f?1:flute)*Mathf.Max(.015f,tip);int i=ring*(sides+1)+side;
                vs[i]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z);uv[i]=new Vector2((float)side/sides,t*3);
                if(ring<rings-1&&side<sides){int j=i+sides+1;triangles.AddRange(new[]{i,i+1,j,i+1,j+1,j});}
            }
            var mesh=new Mesh{name="Continuous twin-flute twisted steel needle bit",vertices=vs,uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();return mesh;
        }
        static Material[] DrillSurfaces()
        {
            if(needleDrillSurfaces!=null)return needleDrillSurfaces;
            var steel=StockPistolSurfaces();needleDrillSurfaces=new Material[8];
            needleDrillSurfaces[2]=steel[0];needleDrillSurfaces[3]=steel[1];needleDrillSurfaces[4]=steel[5];needleDrillSurfaces[6]=steel[2];needleDrillSurfaces[7]=steel[1];
            const int size=512;
            var grain=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Molded tool polymer micrograin",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var rubber=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Diamond rubber relief",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var normal=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Injection-molded polymer fine relief",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var gp=new Color[size*size];var rp=new Color[gp.Length];var np=new Color[gp.Length];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(float)x/size,v=(float)y/size,g=Mathf.PerlinNoise(x*.41f,y*.41f);
                float shade=.94f+g*.06f,n=.5f+(g-.5f)*.06f;int i=y*size+x;gp[i]=new Color(shade,shade,shade,1);np[i]=new Color(n,.5f,1,n);
                float dx=.5f+Mathf.Cos((u+v)*110)*Mathf.Sin((u-v)*110)*.12f,dy=.5f+Mathf.Sin((u+v)*110)*Mathf.Cos((u-v)*110)*.12f;
                rp[i]=new Color(dx,dy,1,dx);
            }
            grain.SetPixels(gp);grain.Apply(true,true);normal.SetPixels(np);normal.Apply(true,true);rubber.SetPixels(rp);rubber.Apply(true,true);
            var orange=new Material(steel[0]){name="Glossy tangerine molded tool shell"};orange.SetColor("_BaseColor",new Color(.98f,.205f,.018f));orange.SetTexture("_BaseMap",grain);orange.SetTexture("_BumpMap",normal);orange.SetFloat("_Metallic",.08f);orange.SetFloat("_Smoothness",.66f);orange.SetFloat("_BumpScale",.32f);
            var grip=new Material(orange){name="Diamond-textured soft black rubber"};grip.SetColor("_BaseColor",new Color(.025f,.030f,.035f));grip.SetTexture("_BumpMap",rubber);grip.SetFloat("_Smoothness",.23f);grip.SetFloat("_Metallic",0);grip.SetFloat("_BumpScale",.42f);
            var light=new Material(orange){name="Lime battery charge glass"};light.SetColor("_BaseColor",new Color(.35f,1,.08f));light.SetFloat("_Smoothness",.85f);
            needleDrillSurfaces[0]=orange;needleDrillSurfaces[1]=grip;needleDrillSurfaces[5]=light;return needleDrillSurfaces;
        }

        static Mesh[] sheriffRevolverMeshes;
        static Material[] sheriffRevolverSurfaces;
        void BuildDetailedRevolver(Transform root)
        {
            if(sheriffRevolverMeshes==null)
            {
                var groups=new List<CombineInstance>[6];for(int i=0;i<groups.Length;i++)groups[i]=new List<CombineInstance>();var temporary=new List<Mesh>();
                void Add(int group,Mesh mesh,Vector3 p)
                {temporary.Add(mesh);groups[group].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.Translate(p)});}
                void Box(int group,Vector3 p,Vector3 size,float bevel=.003f)
                {
                    Add(group,PistolSolid("Beveled revolver frame",size.x,bevel,new[]{new Vector2(p.y-size.y*.5f,p.z-size.z*.5f),new Vector2(p.y-size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z-size.z*.5f)}),new Vector3(p.x,0,0));
                }
                void Cylinder(int group,Vector3 a,Vector3 b,float radius,float endRadius=-1)
                {
                    var builder=new CandyAccessoryMeshes.Builder();builder.Cone(a,b,radius,endRadius<0?radius:endRadius,32);var mesh=builder.Mesh("Turned revolver hardware");
                    var uv=new Vector2[mesh.vertexCount];var v=mesh.vertices;for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(v[i].z*4,v[i].y*4);mesh.uv=uv;Add(group,mesh,Vector3.zero);
                }
                void Solid(int group,float x,float width,float bevel,params Vector2[] outline)
                {Add(group,PistolSolid("Sculpted revolver side plate",width,bevel,outline),new Vector3(x,0,0));}
                // The frame surrounds the cylinder instead of filling its window.
                Box(0,new Vector3(0,.170f,.124f),new Vector3(.083f,.025f,.241f),.005f);
                Box(1,new Vector3(0,.185f,.123f),new Vector3(.073f,.008f,.234f),.002f);
                Box(0,new Vector3(0,-.030f,.117f),new Vector3(.081f,.025f,.234f),.004f);
                Solid(0,0,.095f,.006f,new Vector2(-.037f,-.024f),new Vector2(-.030f,.041f),new Vector2(.163f,.047f),new Vector2(.166f,-.026f),new Vector2(.108f,-.060f));
                Box(0,new Vector3(0,.072f,.224f),new Vector3(.065f,.171f,.032f),.005f);
                Add(0,RevolverCylinderShell(),Vector3.up*.012f);
                Add(1,RevolverCylinderFace(),Vector3.up*.012f);
                Cylinder(1,new Vector3(0,.072f,.203f),new Vector3(0,.072f,.216f),.018f);
                Cylinder(2,new Vector3(0,.072f,.216f),new Vector3(0,.072f,.217f),.008f);
                for(int chamber=0;chamber<6;chamber++)
                {
                    float a=Mathf.PI*.5f+chamber*Mathf.PI/3;var p=new Vector3(Mathf.Cos(a)*.048f,.072f+Mathf.Sin(a)*.048f,0);
                    // Deep dark bases and brass cartridge rims behind the cylinder.
                    Cylinder(2,p+Vector3.forward*.175f,p+Vector3.forward*.177f,.0125f);
                    Cylinder(4,p+Vector3.forward*.042f,p+Vector3.forward*.047f,.015f);
                    Cylinder(2,p+Vector3.forward*.040f,p+Vector3.forward*.042f,.0045f);
                }
                Add(0,RevolverOctagonalBarrel(),Vector3.zero);
                Add(1,RevolverMuzzle(),Vector3.zero);
                Cylinder(2,new Vector3(0,.12f,.476f),new Vector3(0,.12f,.478f),.022f);
                Box(1,new Vector3(0,.075f,.373f),new Vector3(.043f,.022f,.284f),.004f);
                Cylinder(1,new Vector3(.029f,.074f,.240f),new Vector3(.029f,.074f,.489f),.009f);
                Cylinder(0,new Vector3(.029f,.074f,.474f),new Vector3(.029f,.074f,.501f),.012f);
                // Curved grip, exposed metal perimeter, walnut panels and a polished heel.
                Solid(0,0,.080f,.008f,new Vector2(-.178f,-.104f),new Vector2(-.185f,-.016f),new Vector2(-.156f,.010f),new Vector2(.032f,.053f),new Vector2(.065f,-.014f),new Vector2(-.035f,-.066f));
                foreach(float side in new[]{-1f,1f})
                {
                    Solid(3,side*.043f,.014f,.005f,new Vector2(-.164f,-.089f),new Vector2(-.168f,-.023f),new Vector2(-.146f,-.004f),new Vector2(.016f,.030f),new Vector2(.037f,-.012f),new Vector2(-.037f,-.051f));
                    Cylinder(1,new Vector3(side*.049f,-.119f,-.045f),new Vector3(side*.055f,-.119f,-.045f),.007f);
                    Box(2,new Vector3(side*.056f,-.119f,-.045f),new Vector3(.001f,.002f,.009f),.0002f);
                    Add(4,RevolverBadge(side),Vector3.zero);
                    Cylinder(1,new Vector3(side*.047f,.059f,-.010f),new Vector3(side*.052f,.059f,-.010f),.008f);
                    Box(2,new Vector3(side*.053f,.059f,-.010f),new Vector3(.001f,.002f,.011f),.0002f);
                    Box(1,new Vector3(side*.049f,.099f,-.009f),new Vector3(.007f,.035f,.028f),.003f);
                    for(int i=0;i<4;i++)Box(2,new Vector3(side*.054f,.089f+i*.006f,-.009f),new Vector3(.002f,.002f,.020f),.0003f);
                    // Fine curved inlay follows the frame and barrel rather than a flat decal.
                    for(int i=0;i<9;i++)
                    {
                        float t=i/8f;Cylinder(4,new Vector3(side*.049f,.121f+t*.018f,-.021f+t*.039f),new Vector3(side*.049f,.123f+t*.018f,-.016f+t*.039f),.0012f);
                    }
                    Box(4,new Vector3(side*.037f,.145f,.371f),new Vector3(.002f,.003f,.197f),.0005f);
                }
                Box(1,new Vector3(0,-.174f,-.052f),new Vector3(.087f,.018f,.100f),.006f);
                // Actual open guard with a curved trigger inside it.
                var guard=new CandyAccessoryMeshes.Builder();guard.Ring(Vector3.zero,.05f,.008f,Quaternion.Euler(0,90,0));
                var guardMesh=guard.Mesh("Continuous oval steel trigger guard");var guardVertices=guardMesh.vertices;
                for(int i=0;i<guardVertices.Length;i++)guardVertices[i]=new Vector3(guardVertices[i].x,guardVertices[i].y*.88f-.049f,guardVertices[i].z*1.2f+.105f);
                guardMesh.vertices=guardVertices;guardMesh.RecalculateNormals();Add(1,guardMesh,Vector3.zero);
                Cylinder(2,new Vector3(0,-.012f,.093f),new Vector3(0,-.057f,.102f),.006f);
                Cylinder(2,new Vector3(0,-.057f,.102f),new Vector3(0,-.066f,.117f),.006f);
                Solid(1,0,.025f,.003f,new Vector2(.119f,-.035f),new Vector2(.143f,-.012f),new Vector2(.213f,-.057f),new Vector2(.204f,-.084f),new Vector2(.183f,-.077f));
                for(int i=0;i<4;i++)Box(2,new Vector3(0,.202f-i*.004f,-.066f-i*.004f),new Vector3(.026f,.002f,.004f),.0003f);
                // Low iron sights stay below the preserved optical sight line.
                foreach(float side in new[]{-1f,1f})Box(2,new Vector3(side*.020f,.195f,.010f),new Vector3(.012f,.025f,.024f),.002f);
                Box(1,new Vector3(0,.165f,.493f),new Vector3(.034f,.013f,.040f),.002f);
                Box(2,new Vector3(0,.186f,.496f),new Vector3(.009f,.035f,.026f),.001f);
                Box(5,new Vector3(0,.191f,.482f),new Vector3(.004f,.008f,.002f),.0003f);
                // The same optic socket sits on a compact saddle rather than a large block.
                foreach(float z in new[]{.170f,.215f})
                {
                    var ring=new CandyAccessoryMeshes.Builder();ring.Ring(new Vector3(0,.230f,z),.022f,.0035f,Quaternion.identity);Add(1,ring.Mesh("Fine polished optic rim"),Vector3.zero);
                }
                Box(0,new Vector3(0,.190f,.192f),new Vector3(.049f,.010f,.075f),.002f);
                foreach(float side in new[]{-1f,1f})Box(0,new Vector3(side*.018f,.208f,.192f),new Vector3(.008f,.030f,.025f),.002f);
                Box(5,new Vector3(0,.230f,.174f),new Vector3(.029f,.0012f,.001f),.0002f);
                Box(5,new Vector3(0,.230f,.174f),new Vector3(.0012f,.029f,.001f),.0002f);
                sheriffRevolverMeshes=new Mesh[groups.Length];
                for(int i=0;i<groups.Length;i++)
                {
                    var mesh=new Mesh{name="Detailed sheriff revolver surface "+i};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(groups[i].ToArray(),true,true);mesh.RecalculateTangents();mesh.RecalculateBounds();sheriffRevolverMeshes[i]=mesh;
                }
                foreach(var mesh in temporary)Object.Destroy(mesh);
            }
            if(sheriffRevolverSurfaces==null)
            {
                var shared=StockPistolSurfaces();sheriffRevolverSurfaces=(Material[])shared.Clone();
                const int size=512;var stone=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Royal lapis mineral enamel",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};var pixels=new Color[size*size];
                float Smooth(float low,float high,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(low,high,value));
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=(float)x/size,v=(float)y/size;
                    float cloud=Mathf.PerlinNoise(u*5.2f+17,v*5.2f+29),fine=Mathf.PerlinNoise(u*94,v*94);
                    var color=Color.Lerp(new Color(.025f,.095f,.38f),new Color(.10f,.34f,.94f),.38f+cloud*.58f);
                    color=Color.Lerp(color,new Color(.21f,.47f,.95f),Smooth(.65f,.88f,cloud)*.25f);color*=.95f+fine*.05f;
                    float warp=Mathf.PerlinNoise(u*8+39,v*8+71),vein=Mathf.Abs(Mathf.Sin((u*.8f+v)*41+warp*9));
                    float gold=Smooth(.975f,1,fine)*Smooth(.48f,.75f,cloud);
                    gold=Mathf.Max(gold,(1-Smooth(.015f,.035f,vein))*Smooth(.63f,.79f,cloud)*.45f);
                    pixels[y*size+x]=Color.Lerp(color,new Color(.88f,.61f,.18f),gold);
                }
                stone.SetPixels(pixels);stone.Apply(true,true);
                var blue=new Material(shared[0]){name="Heroic royal lapis enamel"};blue.SetColor("_BaseColor",Color.white);blue.SetTexture("_BaseMap",stone);blue.SetFloat("_Metallic",.22f);blue.SetFloat("_Smoothness",.72f);blue.SetFloat("_BumpScale",.10f);sheriffRevolverSurfaces[0]=blue;
                var grip=new Material(blue){name="Deep lapis polished grip panels"};grip.SetColor("_BaseColor",new Color(.76f,.87f,1));grip.SetFloat("_Smoothness",.56f);sheriffRevolverSurfaces[3]=grip;
                var silver=new Material(shared[1]){name="Bright platinum sheriff trim"};silver.SetColor("_BaseColor",new Color(.82f,.89f,.97f));silver.SetFloat("_Metallic",.78f);silver.SetFloat("_Smoothness",.78f);sheriffRevolverSurfaces[1]=silver;
                var goldTrim=new Material(shared[4]){name="Warm sun-gold sheriff inlay"};goldTrim.SetColor("_BaseColor",new Color(.97f,.64f,.17f));goldTrim.SetFloat("_Metallic",.8f);goldTrim.SetFloat("_Smoothness",.68f);sheriffRevolverSurfaces[4]=goldTrim;
                var dark=new Material(shared[2]){name="Midnight blue sheriff recesses"};dark.SetColor("_BaseColor",new Color(.018f,.035f,.08f));sheriffRevolverSurfaces[2]=dark;
            }
            for(int i=0;i<sheriffRevolverMeshes.Length;i++)
            {
                var go=new GameObject(sheriffRevolverMeshes[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=sheriffRevolverMeshes[i];go.GetComponent<MeshRenderer>().sharedMaterial=sheriffRevolverSurfaces[i];
            }
            var optic=new GameObject("Revolver optic").transform;optic.SetParent(root,false);optic.localPosition=new Vector3(0,.23f,.17f);
            var lens=new GameObject("Optic lens").transform;lens.SetParent(optic,false);lens.localPosition=Vector3.forward*.008f;
        }
        static Mesh RevolverCylinderShell()
        {
            const int sides=96,rings=9;var v=new Vector3[(sides+1)*rings];var uv=new Vector2[v.Length];var triangles=new List<int>();
            for(int ring=0;ring<rings;ring++)for(int side=0;side<=sides;side++)
            {
                float t=(float)ring/(rings-1),a=side*Mathf.PI*2/sides;
                float flute=Mathf.Pow(Mathf.Max(0,Mathf.Cos(6*(a-Mathf.PI*.5f))),3)*.007f*Mathf.Sin(t*Mathf.PI);
                float r=.077f-flute-(ring==0||ring==rings-1?.003f:0);int i=ring*(sides+1)+side;
                v[i]=new Vector3(Mathf.Cos(a)*r,.060f+Mathf.Sin(a)*r,.048f+t*.154f);uv[i]=new Vector2((float)side/sides,t);
                if(ring<rings-1&&side<sides){int j=i+sides+1;triangles.AddRange(new[]{i,i+1,j,i+1,j+1,j});}
            }
            var mesh=new Mesh{name="Six-flute revolver cylinder",vertices=v,uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();return mesh;
        }
        static Mesh RevolverCylinderFace()
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int i=vertices.Count;vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.x,a.y)*8,new Vector2(b.x,b.y)*8,new Vector2(c.x,c.y)*8});triangles.AddRange(new[]{i,i+1,i+2});}
            float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
            for(int chamber=0;chamber<6;chamber++)
            {
                float angle=Mathf.PI*.5f+chamber*Mathf.PI/3;var center=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.048f;
                var outer=new Vector3[49];var inner=new Vector3[49];var deep=new Vector3[49];
                for(int i=0;i<=48;i++)
                {
                    float a=i*Mathf.PI*2/48;var direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));float dot=Vector2.Dot(center,direction);
                    float distance=-dot+Mathf.Sqrt(dot*dot+.074f*.074f-center.sqrMagnitude);
                    foreach(float edge in new[]{angle-Mathf.PI/6,angle+Mathf.PI/6})
                    {
                        var boundary=new Vector2(Mathf.Cos(edge),Mathf.Sin(edge));float denom=Cross(direction,boundary);
                        if(Mathf.Abs(denom)<.00001f)continue;float hit=-Cross(center,boundary)/denom;
                        if(hit>0&&Vector2.Dot(center+direction*hit,boundary)>=0)distance=Mathf.Min(distance,hit);
                    }
                    var p=center+direction*distance;outer[i]=new Vector3(p.x,p.y+.060f,.204f);
                    p=center+direction*.014f;inner[i]=new Vector3(p.x,p.y+.060f,.204f);
                    p=center+direction*.0125f;deep[i]=new Vector3(p.x,p.y+.060f,.176f);
                }
                for(int i=0;i<48;i++)
                {
                    Triangle(inner[i],outer[i],inner[i+1]);Triangle(inner[i+1],outer[i],outer[i+1]);
                    Triangle(inner[i],inner[i+1],deep[i]);Triangle(inner[i+1],deep[i+1],deep[i]);
                }
            }
            var mesh=new Mesh{name="Six truly recessed chamber mouths"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
        static Mesh RevolverOctagonalBarrel()
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int face=0;face<8;face++)
            {
                float a=face*Mathf.PI/4+Mathf.PI/8,b=a+Mathf.PI/4;int index=vertices.Count;
                foreach(var p in new[]{new Vector3(Mathf.Cos(a)*.044f,.12f+Mathf.Sin(a)*.044f,.238f),new Vector3(Mathf.Cos(b)*.044f,.12f+Mathf.Sin(b)*.044f,.238f),new Vector3(Mathf.Cos(a)*.038f,.12f+Mathf.Sin(a)*.038f,.523f),new Vector3(Mathf.Cos(b)*.038f,.12f+Mathf.Sin(b)*.038f,.523f)}){vertices.Add(p);uv.Add(new Vector2(p.z*3,p.y*3));}
                triangles.AddRange(new[]{index,index+1,index+2,index+1,index+3,index+2});
            }
            var mesh=new Mesh{name="Eight flat machined barrel faces"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
        static Mesh RevolverMuzzle()
        {
            const int sides=64;var v=new Vector3[4*(sides+1)];var uv=new Vector2[v.Length];var triangles=new List<int>();
            for(int ring=0;ring<4;ring++)for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides,r=ring==0?.036f:ring==1?.034f:.022f,z=ring==0?.523f:ring==3?.477f:.532f;int n=ring*(sides+1)+i;
                v[n]=new Vector3(Mathf.Cos(a)*r,.12f+Mathf.Sin(a)*r,z);uv[n]=new Vector2((float)i/sides,ring/3f);
                if(ring<3&&i<sides){int j=n+sides+1;triangles.AddRange(new[]{n,n+1,j,n+1,j+1,j});}
            }
            var mesh=new Mesh{name="Beveled muzzle crown and deep barrel bore",vertices=v,uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();return mesh;
        }
        static Mesh RevolverBadge(float side)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6,ra=i%2==0?.025f:.012f,rb=(i+1)%2==0?.025f:.012f;
                int n=vertices.Count;vertices.Add(new Vector3(side*.053f,-.060f,-.025f));vertices.Add(new Vector3(side*.053f,-.060f+Mathf.Sin(a)*ra,-.025f+Mathf.Cos(a)*ra));vertices.Add(new Vector3(side*.053f,-.060f+Mathf.Sin(b)*rb,-.025f+Mathf.Cos(b)*rb));
                uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up});triangles.AddRange(side>0?new[]{n,n+2,n+1}:new[]{n,n+1,n+2});
            }
            var mesh=new Mesh{name="Six-point brass sheriff grip medallion"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }

        static Mesh[] dualRifleMeshes;
        static Material[] dualRifleSurfaces;
        void BuildDetailedDualRifle(Transform root)
        {
            if(dualRifleMeshes==null)
            {
                var groups=new List<CombineInstance>[12];for(int i=0;i<groups.Length;i++)groups[i]=new List<CombineInstance>();var temporary=new List<Mesh>();Matrix4x4 space=Matrix4x4.identity;
                void Add(int group,Mesh mesh,Matrix4x4 transform)
                {temporary.Add(mesh);groups[group].Add(new CombineInstance{mesh=mesh,transform=space*transform});}
                void Box(int group,Vector3 p,Vector3 size,float bevel=.003f)
                {
                    Add(group,PistolSolid("Machined dual rifle plate",size.x,bevel,new[]{new Vector2(p.y-size.y*.5f,p.z-size.z*.5f),new Vector2(p.y-size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z-size.z*.5f)}),Matrix4x4.Translate(new Vector3(p.x,0,0)));
                }
                void Solid(int group,float x,float width,float bevel,params Vector2[] outline)
                {Add(group,PistolSolid("Sculpted dual rifle molding",width,bevel,outline),Matrix4x4.Translate(new Vector3(x,0,0)));}
                void Cylinder(int group,Vector3 a,Vector3 b,float radius,float endRadius=-1)
                {
                    var builder=new CandyAccessoryMeshes.Builder();builder.Cone(a,b,radius,endRadius<0?radius:endRadius,32);var mesh=builder.Mesh("Dual rifle turned hardware");var v=mesh.vertices;var uv=new Vector2[v.Length];
                    for(int i=0;i<v.Length;i++)uv[i]=new Vector2(v[i].z*4,v[i].y*4);mesh.uv=uv;Add(group,mesh,Matrix4x4.identity);
                }
                void RingPart(int group,Vector3 p,float radius,float thickness,Quaternion? rotation=null)
                {var builder=new CandyAccessoryMeshes.Builder();builder.Ring(p,radius,thickness,rotation??Quaternion.identity);var mesh=builder.Mesh("Dual rifle machined ring");var v=mesh.vertices;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++)uv[i]=new Vector2(v[i].z*4,v[i].y*4);mesh.uv=uv;Add(group,mesh,Matrix4x4.identity);}
                void Tube(int group,Vector3 p,float outer,float inner,float length)
                {Add(group,WeaponBoreTube(outer,inner,length),Matrix4x4.Translate(p));}
                // Fixed receiver, lower frame and stock keep clear of the turning carriage.
                Solid(0,0,.160f,.009f,new Vector2(.027f,-.108f),new Vector2(.024f,.244f),new Vector2(.076f,.273f),new Vector2(.157f,.230f),new Vector2(.160f,-.076f));
                Box(1,new Vector3(0,.043f,.095f),new Vector3(.123f,.040f,.248f),.006f);
                Box(2,new Vector3(0,.165f,.055f),new Vector3(.104f,.012f,.246f),.002f);
                for(int i=0;i<10;i++)Box(1,new Vector3(0,.179f,-.051f+i*.024f),new Vector3(.110f,.014f,.012f),.001f);
                Solid(0,0,.094f,.008f,new Vector2(-.141f,-.094f),new Vector2(-.150f,-.011f),new Vector2(.039f,.048f),new Vector2(.064f,-.035f));
                foreach(float side in new[]{-1f,1f})
                {
                    Solid(3,side*.049f,.015f,.004f,new Vector2(-.125f,-.082f),new Vector2(-.131f,-.024f),new Vector2(.014f,.023f),new Vector2(.036f,-.032f));
                    for(int i=0;i<6;i++)Box(1,new Vector3(side*.058f,-.111f+i*.023f,-.059f+i*.006f),new Vector3(.003f,.004f,.038f),.0007f);
                    Box(1,new Vector3(side*.082f,.108f,.079f),new Vector3(.008f,.060f,.226f),.004f);
                    Box(2,new Vector3(side*.087f,.118f,.078f),new Vector3(.004f,.030f,.108f),.002f);
                    Box(1,new Vector3(side*.090f,.122f,.066f),new Vector3(.002f,.013f,.061f),.001f);
                    for(int i=0;i<4;i++)Box(4,new Vector3(side*.089f,.086f,.065f+i*.027f),new Vector3(.002f,.005f,.015f),.0005f);
                    Cylinder(2,new Vector3(side*.081f,.113f,-.042f),new Vector3(side*.089f,.113f,-.042f),.008f);
                    Box(1,new Vector3(side*.090f,.113f,-.042f),new Vector3(.001f,.002f,.011f),.0003f);
                    Cylinder(2,new Vector3(side*.070f,.048f,-.026f),new Vector3(side*.082f,.048f,-.026f),.011f);
                }
                Box(2,new Vector3(-.104f,.130f,.116f),new Vector3(.049f,.018f,.035f),.004f);
                Box(3,new Vector3(-.126f,.130f,.116f),new Vector3(.022f,.026f,.049f),.004f);
                Box(4,new Vector3(-.086f,.066f,-.022f),new Vector3(.012f,.012f,.044f),.002f);
                Box(5,new Vector3(-.082f,.079f,-.038f),new Vector3(.002f,.006f,.006f),.0005f);
                // Curved detachable magazine with stamped channels and a pronounced floor plate.
                Solid(1,0,.084f,.005f,new Vector2(-.185f,.161f),new Vector2(-.179f,.265f),new Vector2(.042f,.242f),new Vector2(.044f,.151f));
                Box(0,new Vector3(0,-.183f,.212f),new Vector3(.096f,.018f,.117f),.004f);
                foreach(float side in new[]{-1f,1f})
                {
                    for(int i=0;i<4;i++)Box(0,new Vector3(side*.044f,-.083f,.171f+i*.022f),new Vector3(.004f,.126f,.006f),.001f);
                    Box(4,new Vector3(side*.047f,-.147f,.210f),new Vector3(.002f,.010f,.057f),.001f);
                }
                Cylinder(0,new Vector3(0,.095f,-.094f),new Vector3(0,.095f,-.302f),.036f);
                Solid(0,0,.104f,.007f,new Vector2(-.029f,-.443f),new Vector2(.125f,-.443f),new Vector2(.143f,-.359f),new Vector2(.098f,-.197f),new Vector2(.049f,-.197f));
                Box(3,new Vector3(0,.131f,-.335f),new Vector3(.113f,.035f,.165f),.008f);
                Box(3,new Vector3(0,.051f,-.460f),new Vector3(.126f,.209f,.045f),.009f);
                Box(2,new Vector3(0,.051f,-.434f),new Vector3(.113f,.189f,.012f),.002f);
                for(int i=0;i<9;i++)Box(1,new Vector3(0,-.033f+i*.021f,-.484f),new Vector3(.105f,.004f,.003f),.0005f);
                foreach(float side in new[]{-1f,1f})
                {
                    Box(1,new Vector3(side*.053f,.049f,-.354f),new Vector3(.005f,.045f,.103f),.004f);
                    Cylinder(2,new Vector3(side*.054f,.054f,-.369f),new Vector3(side*.060f,.054f,-.369f),.009f);
                }
                var guard=new CandyAccessoryMeshes.Builder();guard.Ring(Vector3.zero,.047f,.007f,Quaternion.Euler(0,90,0));var gm=guard.Mesh("Continuous rifle trigger guard");var gv=gm.vertices;
                for(int i=0;i<gv.Length;i++)gv[i]=new Vector3(gv[i].x,gv[i].y*.80f-.025f,gv[i].z*1.20f+.092f);gm.vertices=gv;gm.RecalculateNormals();Add(0,gm,Matrix4x4.identity);
                Cylinder(2,new Vector3(0,.019f,.081f),new Vector3(0,-.031f,.093f),.006f);Cylinder(2,new Vector3(0,-.031f,.093f),new Vector3(0,-.041f,.108f),.006f);
                // All following geometry is local to the original rotating carriage pivot.
                RingPart(8,new Vector3(0,0,.004f),.106f,.009f);
                RingPart(7,new Vector3(0,0,.020f),.094f,.011f);
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6;Cylinder(8,new Vector3(Mathf.Cos(a)*.105f,Mathf.Sin(a)*.105f,-.003f),new Vector3(Mathf.Cos(a)*.105f,Mathf.Sin(a)*.105f,.014f),.004f);
                }
                foreach(int side in new[]{-1,1})
                {
                    bool shotgun=side==1;float y=side*.06f,length=shotgun?.43f:.70f,outer=shotgun?.0525f:.0325f,inner=shotgun?.037f:.017f;
                    Tube(6,new Vector3(0,y,.07f),outer,inner,length);
                    Tube(8,new Vector3(0,y,.07f+length-.038f),outer*1.10f,outer*.995f,.037f);
                    Box(7,new Vector3(0,y,.177f),new Vector3(outer*2.20f,outer*2.20f,.186f),.008f);
                    foreach(float flank in new[]{-1f,1f})for(int i=0;i<5;i++)Box(8,new Vector3(flank*outer*1.12f,y,.108f+i*.033f),new Vector3(.004f,outer*.90f,.012f),.001f);
                    for(int i=0;i<4;i++)Tube(7,new Vector3(0,y,.292f+i*.024f),outer*1.06f,outer*.99f,.010f);
                    if(!shotgun)for(int i=0;i<3;i++)Box(7,new Vector3(outer*1.10f,y,.07f+length-.033f+i*.010f),new Vector3(.002f,.025f,.005f),.0007f);
                    // Scope and red-dot window rotate as part of their own barrel assembly.
                    space=Matrix4x4.TRS(new Vector3(0,side*.19f,.12f),Quaternion.Euler(0,0,shotgun?0:180),Vector3.one);
                    if(shotgun)
                    {
                        foreach(float flank in new[]{-1f,1f})Box(6,new Vector3(flank*.033f,0,.017f),new Vector3(.009f,.058f,.040f),.003f);
                        foreach(float height in new[]{-.027f,.027f})Box(6,new Vector3(0,height,.017f),new Vector3(.070f,.009f,.040f),.003f);
                        Box(7,new Vector3(0,-.052f,.017f),new Vector3(.029f,.060f,.047f),.004f);
                        Box(8,new Vector3(0,-.084f,.017f),new Vector3(.055f,.010f,.073f),.002f);
                        Cylinder(11,new Vector3(0,0,.006f),new Vector3(0,0,.008f),.0022f);
                        Box(10,new Vector3(.039f,-.010f,.014f),new Vector3(.004f,.011f,.020f),.001f);
                    }
                    else
                    {
                        Tube(6,Vector3.zero,.035f,.028f,.235f);
                        Tube(8,new Vector3(0,0,-.006f),.040f,.0347f,.022f);
                        Tube(8,new Vector3(0,0,.215f),.045f,.032f,.035f);
                        for(int i=0;i<8;i++)RingPart(9,new Vector3(0,0,.027f+i*.024f),.039f,.0045f);
                        foreach(float z in new[]{.045f,.192f})
                        {
                            RingPart(8,new Vector3(0,0,z),.039f,.003f);
                            Box(7,new Vector3(0,-.074f,z),new Vector3(.032f,.060f,.029f),.003f);
                            Box(8,new Vector3(0,-.106f,z),new Vector3(.047f,.010f,.044f),.002f);
                        }
                        Cylinder(7,new Vector3(0,.035f,.120f),new Vector3(0,.059f,.120f),.019f);
                        Cylinder(8,new Vector3(0,.058f,.120f),new Vector3(0,.063f,.120f),.020f);
                        Cylinder(7,new Vector3(.035f,0,.120f),new Vector3(.057f,0,.120f),.016f);
                        Box(10,new Vector3(0,.064f,.120f),new Vector3(.003f,.002f,.022f),.0005f);
                        Box(8,new Vector3(0,0,.009f),new Vector3(.036f,.0012f,.001f),.0002f);Box(8,new Vector3(0,0,.009f),new Vector3(.0012f,.036f,.001f),.0002f);
                    }
                    space=Matrix4x4.identity;
                }
                dualRifleMeshes=new Mesh[groups.Length];
                for(int i=0;i<groups.Length;i++)
                {
                    var mesh=new Mesh{name=i<6?"Detailed dual rifle body "+i:"Detailed dual rifle carriage "+i};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(groups[i].ToArray(),true,true);mesh.RecalculateTangents();mesh.RecalculateBounds();dualRifleMeshes[i]=mesh;
                }
                foreach(var mesh in temporary)Object.Destroy(mesh);
            }
            if(dualRifleSurfaces==null)
            {
                var shared=StockPistolSurfaces();dualRifleSurfaces=new Material[6];
                var metal=new Material(shared[0]){name="Satin machined duplex gunmetal"};metal.SetColor("_BaseColor",new Color(.23f,.28f,.32f));metal.SetFloat("_Smoothness",.66f);dualRifleSurfaces[0]=metal;
                dualRifleSurfaces[1]=shared[2];var silver=new Material(shared[1]){name="Polished duplex steel edges"};silver.SetColor("_BaseColor",new Color(.66f,.73f,.80f));dualRifleSurfaces[2]=silver;dualRifleSurfaces[3]=DrillSurfaces()[1];
                var blue=new Material(shared[0]){name="Blue anodized duplex accents"};blue.SetColor("_BaseColor",new Color(.025f,.25f,.39f));dualRifleSurfaces[4]=blue;
                var red=new Material(shared[5]){name="Duplex red dot and selector"};red.SetColor("_BaseColor",new Color(.98f,.045f,.015f));red.SetColor("_EmissionColor",new Color(.8f,.015f,.005f));red.EnableKeyword("_EMISSION");dualRifleSurfaces[5]=red;
            }
            barrel=new GameObject("Rotating 180 degree barrel carriage").transform;barrel.SetParent(root,false);barrel.localPosition=new Vector3(0,.09f,.23f);barrel.localRotation=Quaternion.Euler(0,0,180);
            for(int i=0;i<dualRifleMeshes.Length;i++)
            {
                var go=new GameObject(dualRifleMeshes[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(i<6?root:barrel,false);go.GetComponent<MeshFilter>().sharedMesh=dualRifleMeshes[i];go.GetComponent<MeshRenderer>().sharedMaterial=dualRifleSurfaces[i%6];
            }
            foreach(int side in new[]{-1,1})
            {
                var optic=new GameObject(side==1?"Shotgun optic":"Rifle optic").transform;optic.SetParent(barrel,false);optic.localPosition=new Vector3(0,side*.19f,.12f);optic.localRotation=Quaternion.Euler(0,0,side==1?0:180);
                var lens=new GameObject("Optic lens").transform;lens.SetParent(optic,false);lens.localPosition=Vector3.forward*.008f;
            }
        }
        static Mesh WeaponBoreTube(float outer,float inner,float length)
        {
            const int sides=48;float lip=Mathf.Min(.003f,(outer-inner)*.3f);
            float[] radius={outer-lip,outer,outer,outer-lip,inner+lip,inner,inner,inner+lip};
            float[] depth={0,lip,length-lip,length,length,length-lip,lip,0};
            var v=new Vector3[(sides+1)*radius.Length];var uv=new Vector2[v.Length];var triangles=new List<int>();
            for(int ring=0;ring<radius.Length;ring++)for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides;int n=ring*(sides+1)+i;v[n]=new Vector3(Mathf.Cos(a)*radius[ring],Mathf.Sin(a)*radius[ring],depth[ring]);uv[n]=new Vector2((float)i/sides,depth[ring]*4);
                if(i<sides){int next=((ring+1)%radius.Length)*(sides+1)+i;triangles.AddRange(new[]{n,n+1,next,n+1,next+1,next});}
            }
            var mesh=new Mesh{name="Beveled hollow machined barrel tube",vertices=v,uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();return mesh;
        }

        static Mesh[] alienBlasterMeshes;
        static Material[] alienBlasterSurfaces;
        void BuildDetailedAlienBlaster(Transform root)
        {
            if(alienBlasterMeshes==null)
            {
                var groups=new List<CombineInstance>[7];for(int i=0;i<groups.Length;i++)groups[i]=new List<CombineInstance>();var temporary=new List<Mesh>();
                void Add(int group,Mesh mesh,Vector3 p)
                {temporary.Add(mesh);groups[group].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.Translate(p)});}
                void Box(int group,Vector3 p,Vector3 size,float bevel=.003f)
                {
                    Add(group,PistolSolid("Blaster beveled inset",size.x,bevel,new[]{new Vector2(p.y-size.y*.5f,p.z-size.z*.5f),new Vector2(p.y-size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z+size.z*.5f),new Vector2(p.y+size.y*.5f,p.z-size.z*.5f)}),new Vector3(p.x,0,0));
                }
                void Cylinder(int group,Vector3 a,Vector3 b,float radius,float endRadius=-1)
                {
                    var builder=new CandyAccessoryMeshes.Builder();builder.Cone(a,b,radius,endRadius<0?radius:endRadius,32);var mesh=builder.Mesh("Blaster turned metal");var v=mesh.vertices;var uv=new Vector2[v.Length];
                    for(int i=0;i<v.Length;i++)uv[i]=new Vector2(v[i].z*3,v[i].y*3);mesh.uv=uv;Add(group,mesh,Vector3.zero);
                }
                void RingPart(int group,Vector3 p,float radius,float thickness)
                {Add(group,AlienInductionRim(radius,thickness),p);}
                void Sphere(int group,Vector3 p,Vector3 radii)
                {
                    Mesh mesh;
                    if(Mathf.Max(radii.x,Mathf.Max(radii.y,radii.z))>=.013f)
                    {
                        mesh=Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));var vertices=mesh.vertices;var normals=new Vector3[vertices.Length];var center=mesh.bounds.center;
                        for(int i=0;i<vertices.Length;i++){var unit=(vertices[i]-center).normalized;vertices[i]=p+Vector3.Scale(unit,radii);normals[i]=new Vector3(unit.x/radii.x,unit.y/radii.y,unit.z/radii.z).normalized;}mesh.vertices=vertices;mesh.normals=normals;
                    }
                    else{var builder=new CandyAccessoryMeshes.Builder();builder.Sphere(p,radii);mesh=builder.Mesh("Blaster rounded hardware");}
                    Add(group,mesh,Vector3.zero);
                }
                // Rounded enamel shell and swept rocket fins retain the original ray-gun profile.
                Add(0,DrillTurned("Sculpted crimson ray-gun shell",new[]{-.115f,-.110f,-.095f,-.05f,.03f,.13f,.21f,.26f,.28f},new[]{0f,.025f,.055f,.088f,.100f,.096f,.083f,.058f,.045f},.94f),new Vector3(0,.125f,0));
                RingPart(3,new Vector3(0,.125f,.242f),.064f,.004f);
                Cylinder(5,new Vector3(0,.125f,.272f),new Vector3(0,.125f,.296f),.045f,.027f);
                Cylinder(2,new Vector3(0,.125f,.290f),new Vector3(0,.125f,.520f),.019f,.015f);
                Cylinder(4,new Vector3(0,.125f,.297f),new Vector3(0,.125f,.512f),.012f);
                foreach(float side in new[]{-1f,1f})
                {
                    Add(0,PistolSolid("Swept ray-gun stabilizer",.014f,.003f,new[]{new Vector2(.159f,-.062f),new Vector2(.254f,-.128f),new Vector2(.235f,.016f),new Vector2(.190f,.147f)}),new Vector3(side*.059f,0,0));
                    Cylinder(3,new Vector3(side*.059f,.251f,-.120f),new Vector3(side*.059f,.233f,.015f),.0025f);
                    Box(2,new Vector3(side*.098f,.122f,.061f),new Vector3(.008f,.063f,.132f),.003f);
                    Box(5,new Vector3(side*.104f,.122f,.061f),new Vector3(.006f,.051f,.118f),.002f);
                    for(int i=0;i<5;i++)Box(4,new Vector3(side*.108f,.122f,.014f+i*.023f),new Vector3(.002f,.036f,.008f),.001f);
                    Sphere(3,new Vector3(side*.099f,.080f,-.009f),new Vector3(.005f,.013f,.013f));
                    Sphere(4,new Vector3(side*.092f,.067f,.035f),new Vector3(.005f,.008f,.008f));
                    // Curved bright seams follow the shell instead of sitting on a large flat plate.
                    for(int i=0;i<11;i++)
                    {
                        float z=-.043f+i*.022f,next=z+.022f,r=.092f-Mathf.Max(0,z-.05f)*.14f,rNext=.092f-Mathf.Max(0,next-.05f)*.14f;
                        Cylinder(3,new Vector3(side*r*.72f,.125f+r*.62f,z),new Vector3(side*rNext*.72f,.125f+rNext*.62f,next),.0022f);
                    }
                }
                // Shaped grip with a shell perimeter, rubber panels and recessed ribs.
                Add(0,PistolSolid("Ergonomic blaster grip frame",.080f,.008f,new[]{new Vector2(-.141f,-.040f),new Vector2(-.148f,.040f),new Vector2(.065f,.097f),new Vector2(.091f,.020f)}),Vector3.zero);
                foreach(float side in new[]{-1f,1f})
                {
                    Add(1,PistolSolid("Textured alien grip insert",.012f,.004f,new[]{new Vector2(-.123f,-.026f),new Vector2(-.128f,.029f),new Vector2(.041f,.074f),new Vector2(.062f,.026f)}),new Vector3(side*.043f,0,0));
                    for(int i=0;i<6;i++)Box(5,new Vector3(side*.051f,-.104f+i*.025f,.005f+i*.006f),new Vector3(.003f,.005f,.035f),.001f);
                    Cylinder(2,new Vector3(side*.050f,-.097f,.004f),new Vector3(side*.054f,-.097f,.004f),.005f);
                }
                Box(3,new Vector3(0,-.143f,.003f),new Vector3(.090f,.016f,.100f),.005f);
                Box(0,new Vector3(0,-.155f,.003f),new Vector3(.095f,.018f,.108f),.006f);
                var guard=new CandyAccessoryMeshes.Builder();guard.Ring(Vector3.zero,.042f,.006f,Quaternion.Euler(0,90,0));var gm=guard.Mesh("Continuous alien trigger guard");var gv=gm.vertices;
                for(int i=0;i<gv.Length;i++)gv[i]=new Vector3(gv[i].x,gv[i].y*.76f-.018f,gv[i].z*1.08f+.123f);gm.vertices=gv;gm.RecalculateNormals();Add(3,gm,Vector3.zero);
                Cylinder(5,new Vector3(0,.014f,.109f),new Vector3(0,-.030f,.118f),.005f);Cylinder(5,new Vector3(0,-.030f,.118f),new Vector3(0,-.036f,.132f),.005f);
                // Three thin induction lenses, each with a metal rim, a glowing inner coil and clear center.
                for(int i=0;i<3;i++)
                {
                    float z=.320f+i*.070f,radius=.100f-i*.014f;
                    Add(6,WeaponBoreTube(radius,.025f,.006f),new Vector3(0,.125f,z-.003f));
                    RingPart(2,new Vector3(0,.125f,z),radius,.0035f);
                    RingPart(4,new Vector3(0,.125f,z),.029f,.004f);
                    for(int marker=0;marker<8;marker++)
                    {
                        float a=marker*Mathf.PI/4;Sphere(3,new Vector3(Mathf.Cos(a)*(radius-.007f),.125f+Mathf.Sin(a)*(radius-.007f),z+.0035f),new Vector3(.0025f,.0025f,.0014f));
                    }
                }
                // The spherical emitter keeps the comic reference's tip, with a glowing forward aperture.
                Sphere(0,new Vector3(0,.125f,.538f),Vector3.one*.033f);
                RingPart(3,new Vector3(0,.125f,.540f),.033f,.003f);
                RingPart(2,new Vector3(0,.125f,.567f),.019f,.003f);
                Sphere(4,new Vector3(0,.125f,.572f),new Vector3(.014f,.014f,.003f));
                // Preserve the optical sight position, but integrate its saddle into the shell.
                RingPart(2,new Vector3(0,.240f,.120f),.025f,.003f);RingPart(3,new Vector3(0,.240f,.163f),.025f,.003f);
                Box(5,new Vector3(0,.218f,.142f),new Vector3(.049f,.011f,.065f),.002f);
                Box(4,new Vector3(0,.240f,.124f),new Vector3(.032f,.0012f,.001f),.0002f);Box(4,new Vector3(0,.240f,.124f),new Vector3(.0012f,.032f,.001f),.0002f);
                alienBlasterMeshes=new Mesh[groups.Length];
                for(int i=0;i<groups.Length;i++)
                {
                    var mesh=new Mesh{name="Detailed alien blaster surface "+i};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(groups[i].ToArray(),true,true);mesh.RecalculateTangents();mesh.RecalculateBounds();alienBlasterMeshes[i]=mesh;
                }
                foreach(var mesh in temporary)Object.Destroy(mesh);
            }
            var surfaces=AlienBlasterSurfaces();
            for(int i=0;i<alienBlasterMeshes.Length;i++)
            {
                var go=new GameObject(alienBlasterMeshes[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=alienBlasterMeshes[i];var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=surfaces[i];
                // Pickup model data reads the same color that the held model uses.
                var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",surfaces[i].GetColor("_BaseColor"));renderer.SetPropertyBlock(properties);
                if(i==6)renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var optic=new GameObject("Blaster optic").transform;optic.SetParent(root,false);optic.localPosition=new Vector3(0,.24f,.12f);
            var lens=new GameObject("Optic lens").transform;lens.SetParent(optic,false);lens.localPosition=Vector3.forward*.008f;
            for(int i=0;i<3;i++){var ring=new GameObject("Blaster induction ring "+i).transform;ring.SetParent(root,false);ring.localPosition=new Vector3(0,.125f,.320f+i*.070f);}
        }
        static Mesh AlienInductionRim(float radius,float thickness)
        {
            const int major=64,minor=10;var vertices=new Vector3[(major+1)*(minor+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int a=0;a<=major;a++)for(int b=0;b<=minor;b++)
            {
                float u=a*Mathf.PI*2/major,v=b*Mathf.PI*2/minor;int i=a*(minor+1)+b;
                normals[i]=new Vector3(Mathf.Cos(u)*Mathf.Cos(v),Mathf.Sin(u)*Mathf.Cos(v),Mathf.Sin(v));
                vertices[i]=new Vector3(Mathf.Cos(u),Mathf.Sin(u),0)*radius+normals[i]*thickness;uv[i]=new Vector2((float)a/major,(float)b/minor);
                if(a<major&&b<minor){int j=i+minor+1;triangles.AddRange(new[]{i,j,i+1,i+1,j,j+1});}
            }
            return new Mesh{name="Smooth polished induction rim",vertices=vertices,normals=normals,uv=uv,triangles=triangles.ToArray()};
        }
        static Material[] AlienBlasterSurfaces()
        {
            if(alienBlasterSurfaces!=null)return alienBlasterSurfaces;
            var shared=StockPistolSurfaces();alienBlasterSurfaces=new Material[7];const int size=512;
            var enamel=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Crimson pearlescent alien enamel",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(float)x/size,v=(float)y/size,cloud=Mathf.PerlinNoise(u*7+41,v*7+19),grain=Mathf.PerlinNoise(u*180,v*180);
                float flow=.5f+.5f*Mathf.Sin(u*24+Mathf.Sin(v*11)*1.4f+cloud*3);
                pixels[y*size+x]=Color.Lerp(new Color(.57f,.018f,.025f),new Color(.97f,.10f,.045f),.35f+cloud*.45f+flow*.12f)*(.97f+grain*.03f);
            }
            enamel.SetPixels(pixels);enamel.Apply(true,true);
            var shell=new Material(shared[0]){name="Glossy crimson alien candy enamel"};shell.SetTexture("_BaseMap",enamel);shell.SetColor("_BaseColor",Color.white);shell.SetFloat("_Metallic",.18f);shell.SetFloat("_Smoothness",.88f);shell.SetFloat("_BumpScale",.08f);alienBlasterSurfaces[0]=shell;
            alienBlasterSurfaces[1]=DrillSurfaces()[1];alienBlasterSurfaces[5]=shared[2];
            var chrome=new Material(shared[1]){name="Polished alien silver"};chrome.SetColor("_BaseColor",new Color(.73f,.86f,.95f));chrome.SetFloat("_Smoothness",.86f);alienBlasterSurfaces[2]=chrome;
            var gold=new Material(shared[4]){name="Warm champagne alien inlay"};gold.SetColor("_BaseColor",new Color(.86f,.66f,.29f));gold.SetFloat("_Smoothness",.76f);alienBlasterSurfaces[3]=gold;
            var energy=new Material(shared[5]){name="Cyan induction coil ceramic"};energy.SetColor("_BaseColor",new Color(.02f,.71f,.86f));energy.SetFloat("_Metallic",.12f);energy.SetFloat("_Smoothness",.9f);energy.SetColor("_EmissionColor",new Color(.015f,.58f,.74f));energy.EnableKeyword("_EMISSION");alienBlasterSurfaces[4]=energy;
            var glass=new Material(chrome){name="Translucent cyan induction lens"};glass.SetColor("_BaseColor",new Color(.23f,.83f,.96f,.60f));glass.SetFloat("_Metallic",.15f);glass.SetFloat("_Smoothness",.94f);glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");glass.SetOverrideTag("RenderType","Transparent");glass.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;alienBlasterSurfaces[6]=glass;
            return alienBlasterSurfaces;
        }
        static Mesh[] spikeUziMeshes;
        static Material[] spikeUziSurfaces;
        void BuildDetailedSpikeUzi(Transform root)
        {
            if(spikeUziMeshes==null)
            {
                var groups=new List<CombineInstance>[6];for(int i=0;i<6;i++)groups[i]=new List<CombineInstance>();var temporary=new List<Mesh>();
                void Add(int group,Mesh mesh,Vector3 p,Quaternion rotation,Vector3 scale){temporary.Add(mesh);groups[group].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,rotation,scale)});}
                void Box(int group,Vector3 p,Vector3 size,float bevel=.003f){Add(group,PistolSolid("Beveled alien spike fitting",size.x,bevel,new[]{new Vector2(-size.y/2,-size.z/2),new Vector2(size.y/2,-size.z/2),new Vector2(size.y/2,size.z/2),new Vector2(-size.y/2,size.z/2)}),p,Quaternion.identity,Vector3.one);}
                void Tube(int group,Vector3 p,float outer,float inner,float length){Add(group,WeaponBoreTube(outer,inner,length),p,Quaternion.identity,Vector3.one);}
                void Line(int group,Vector3 a,Vector3 b,float radius){var mesh=DrillTurned("Polished alien structural tubing",new[]{0f,.002f,(b-a).magnitude-.002f,(b-a).magnitude},new[]{0f,radius,radius,0f},1);Add(group,mesh,a,Quaternion.LookRotation(b-a),Vector3.one);}
                Add(0,PistolSolid("Sculpted spike accelerator receiver",.125f,.010f,new[]{new Vector2(.064f,-.10f),new Vector2(.058f,.29f),new Vector2(.12f,.34f),new Vector2(.185f,.26f),new Vector2(.182f,-.068f)}),Vector3.zero,Quaternion.identity,Vector3.one);
                Box(2,new Vector3(0,-.020f,-.005f),new Vector3(.078f,.225f,.105f),.009f);
                foreach(float side in new[]{-1f,1f})
                {
                    Box(0,new Vector3(side*.041f,-.013f,-.011f),new Vector3(.009f,.173f,.087f),.003f);
                    for(int i=0;i<6;i++)Box(2,new Vector3(side*.047f,-.084f+i*.027f,-.008f),new Vector3(.005f,.009f,.066f),.002f);
                    Box(1,new Vector3(side*.066f,.116f,.113f),new Vector3(.009f,.066f,.266f),.003f);
                    for(int i=0;i<5;i++)Box(2,new Vector3(side*.072f,.111f,.08f+i*.032f),new Vector3(.004f,.035f,.012f),.001f);
                    Box(3,new Vector3(side*.075f,.145f,.12f),new Vector3(.003f,.005f,.20f),.0005f);
                    for(float z=-.045f;z<.30f;z+=.315f)Tube(4,new Vector3(side*.073f,.091f,z),.007f,.003f,.010f);
                    // Folded structural stock stays narrow enough for a paired weapon.
                    Line(0,new Vector3(side*.041f,.085f,-.07f),new Vector3(side*.045f,.065f,-.245f),.008f);
                    Line(1,new Vector3(side*.045f,.065f,-.245f),new Vector3(side*.045f,.15f,-.260f),.007f);
                }
                Line(2,new Vector3(-.045f,.065f,-.245f),new Vector3(.045f,.065f,-.245f),.013f);
                Box(0,new Vector3(0,-.157f,.012f),new Vector3(.065f,.13f,.095f),.005f);Box(2,new Vector3(0,-.222f,.012f),new Vector3(.084f,.025f,.114f),.004f);
                foreach(float side in new[]{-1f,1f}){Box(5,new Vector3(side*.034f,-.157f,.013f),new Vector3(.003f,.089f,.050f),.001f);for(int i=0;i<3;i++)Box(3,new Vector3(side*.036f,-.184f+i*.026f,.013f),new Vector3(.002f,.009f,.028f),.0005f);}
                Line(1,new Vector3(0,-.063f,.047f),new Vector3(0,-.063f,.15f),.007f);Line(1,new Vector3(0,-.063f,.15f),new Vector3(0,.067f,.18f),.007f);
                Line(4,new Vector3(0,.061f,.072f),new Vector3(0,-.018f,.11f),.006f);
                Tube(0,new Vector3(0,.122f,.285f),.039f,.020f,.215f);Tube(1,new Vector3(0,.122f,.470f),.045f,.022f,.032f);Tube(2,new Vector3(0,.122f,.500f),.030f,.019f,.014f);
                for(int i=0;i<3;i++)Tube(4,new Vector3(0,.122f,.334f+i*.047f),.042f,.037f,.009f);
                // Exposed crystalline spike cartridges sit in individual machined feed sockets.
                for(int i=0;i<5;i++)
                {
                    var p=new Vector3(0,.185f,-.031f+i*.060f);var q=Quaternion.Euler(-34,0,0);
                    Add(4,WeaponBoreTube(.022f,.013f,.035f),p,q,Vector3.one);
                    var b=new CandyAccessoryMeshes.Builder();b.Cone(Vector3.zero,Vector3.forward*.10f,.014f,.014f,8);b.Cone(Vector3.forward*.10f,Vector3.forward*.19f,.014f,0,8);var mesh=b.Mesh("Large faceted neon green spike cartridge");
                    var uv=new Vector2[mesh.vertexCount];var vs=mesh.vertices;for(int v=0;v<uv.Length;v++)uv[v]=new Vector2(vs[v].x*20,vs[v].z*8);mesh.uv=uv;
                    Add(3,mesh,p+q*Vector3.forward*.014f,q,Vector3.one);
                    Add(5,WeaponBoreTube(.019f,.016f,.073f),p+q*Vector3.forward*.033f,q,Vector3.one);
                }
                Box(2,new Vector3(0,.203f,-.080f),new Vector3(.065f,.032f,.030f));Box(4,new Vector3(0,.218f,-.082f),new Vector3(.025f,.004f,.012f),.001f);
                spikeUziMeshes=new Mesh[6];for(int i=0;i<6;i++){var mesh=new Mesh{name="Detailed alien spike Uzi surface "+i};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(groups[i].ToArray(),true,true);mesh.RecalculateTangents();mesh.RecalculateBounds();spikeUziMeshes[i]=mesh;}foreach(var mesh in temporary)Object.Destroy(mesh);
            }
            var surfaces=SpikeUziSurfaces();for(int i=0;i<6;i++){var go=new GameObject(spikeUziMeshes[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=spikeUziMeshes[i];var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=surfaces[i];var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",surfaces[i].GetColor("_BaseColor"));renderer.SetPropertyBlock(block);}
        }
        static Material[] SpikeUziSurfaces()
        {
            if(spikeUziSurfaces!=null)return spikeUziSurfaces;var shared=StockPistolSurfaces();spikeUziSurfaces=new Material[6];
            for(int i=0;i<6;i++)spikeUziSurfaces[i]=new Material(shared[i==0?0:i==1?1:i==2?2:i==4?4:5]){name="Alien spike Uzi "+i,enableInstancing=true};
            spikeUziSurfaces[0].SetColor("_BaseColor",new Color(.105f,.17f,.19f));spikeUziSurfaces[0].SetFloat("_Smoothness",.73f);
            spikeUziSurfaces[1].SetColor("_BaseColor",new Color(.45f,.62f,.65f));spikeUziSurfaces[1].SetFloat("_Smoothness",.83f);
            spikeUziSurfaces[2]=new Material(DrillSurfaces()[1]){name="Alien diamond rubber grip"};
            spikeUziSurfaces[3].SetColor("_BaseColor",new Color(.28f,.95f,.015f));spikeUziSurfaces[3].SetColor("_EmissionColor",new Color(.10f,.38f,.002f));spikeUziSurfaces[3].EnableKeyword("_EMISSION");spikeUziSurfaces[3].SetFloat("_Smoothness",.86f);
            spikeUziSurfaces[4].SetColor("_BaseColor",new Color(.45f,.34f,.13f));
            spikeUziSurfaces[5]=new Material(AlienBlasterSurfaces()[6]){name="Translucent green spike containment glass"};spikeUziSurfaces[5].SetColor("_BaseColor",new Color(.45f,.90f,.12f,.28f));spikeUziSurfaces[5].SetColor("_EmissionColor",new Color(.018f,.08f,.001f));spikeUziSurfaces[5].EnableKeyword("_EMISSION");return spikeUziSurfaces;
        }

        static Mesh[] murderSwordMeshes,boxingGloveMeshes;
        static Material[] murderSwordSurfaces,boxingGloveSurfaces;
        void BuildDetailedMurderSword(Transform root)=>BuildDetailedMelee(root,false);
        void BuildDetailedBoxingGlove(Transform root)=>BuildDetailedMelee(root,true);
        void BuildDetailedMelee(Transform root,bool glove)
        {
            var meshes=glove?boxingGloveMeshes:murderSwordMeshes;
            if(meshes==null)
            {
                var groups=new List<CombineInstance>[6];for(int i=0;i<6;i++)groups[i]=new List<CombineInstance>();
                var temporary=new List<Mesh>();
                void Add(int group,Mesh mesh,Vector3 p,Quaternion rotation,Vector3 scale)
                {temporary.Add(mesh);groups[group].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,rotation,scale)});}
                void Ellipsoid(int group,Vector3 p,Vector3 radii,Quaternion rotation)
                {
                    var mesh=Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));var vertices=mesh.vertices;var normals=new Vector3[vertices.Length];var center=mesh.bounds.center;
                    for(int i=0;i<vertices.Length;i++){var n=(vertices[i]-center).normalized;vertices[i]=Vector3.Scale(n,radii);normals[i]=new Vector3(n.x/radii.x,n.y/radii.y,n.z/radii.z).normalized;}
                    mesh.vertices=vertices;mesh.normals=normals;Add(group,mesh,p,rotation,Vector3.one);
                }
                void Line(int group,Vector3 a,Vector3 b,float radius)
                {
                    var mesh=DrillTurned("Rounded stitched or engraved detail",new[]{0f,.001f,(b-a).magnitude-.001f,(b-a).magnitude},new[]{0f,radius,radius,0f},1);
                    Add(group,mesh,a,Quaternion.LookRotation(b-a),Vector3.one);
                }
                void Ring(int group,Vector3 p,float radius,float thickness,Vector3 scale)
                {Add(group,AlienInductionRim(radius,thickness),p,Quaternion.identity,scale);}
                void Box(int group,Vector3 p,Vector3 size,float bevel)
                {
                    var outline=new[]{new Vector2(-size.y*.5f,-size.z*.5f),new Vector2(size.y*.5f,-size.z*.5f),new Vector2(size.y*.5f,size.z*.5f),new Vector2(-size.y*.5f,size.z*.5f)};
                    Add(group,PistolSolid("Beveled melee fitting",size.x,bevel,outline),p,Quaternion.identity,Vector3.one);
                }
                if(glove)
                {
                    Add(0,MeleeGlovePadding(),Vector3.zero,Quaternion.identity,Vector3.one);
                    // Thumb rolls into the tucked fingers, leaving the palm crease readable.
                    Ellipsoid(0,new Vector3(-.182f,-.058f,.136f),new Vector3(.078f,.083f,.148f),Quaternion.Euler(0,-19,-18));
                    Ellipsoid(3,new Vector3(0,-.125f,.192f),new Vector3(.159f,.047f,.121f),Quaternion.identity);
                    Add(3,DrillTurned("Padded oxblood wrist cuff",new[]{-.235f,-.229f,-.205f,-.065f,-.051f},new[]{.112f,.140f,.148f,.144f,.123f},.84f),Vector3.zero,Quaternion.identity,Vector3.one);
                    Add(4,WeaponBoreTube(.112f,.095f,.023f),new Vector3(0,0,-.245f),Quaternion.identity,new Vector3(1,.84f,1));
                    foreach(float z in new[]{-.224f,-.063f})Ring(2,new Vector3(0,0,z),.145f,.0035f,new Vector3(1,.84f,1));
                    // A broad wrap strap and real little thread loops, rather than a solid gold cuff.
                    Add(0,DrillTurned("Leather wrap wrist strap",new[]{-.192f,-.189f,-.114f,-.111f},new[]{.146f,.151f,.151f,.146f},.84f),Vector3.zero,Quaternion.identity,Vector3.one);
                    for(int i=0;i<44;i++)
                    {
                        float a=i*Mathf.PI*2/44;foreach(float z in new[]{-.180f,-.122f})
                        {var p=new Vector3(Mathf.Cos(a)*.1525f,Mathf.Sin(a)*.128f,z);Line(5,p,p+new Vector3(-Mathf.Sin(a)*.006f,Mathf.Cos(a)*.005f,.006f),.0012f);}
                    }
                    // Curved piping follows the perimeter of the padded finger panel.
                    for(int i=0;i<48;i++)
                    {
                        float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                        var p=new Vector3(Mathf.Cos(a)*.160f,-.126f+Mathf.Sin(a)*.014f,.192f+Mathf.Sin(a)*.116f);
                        var q=new Vector3(Mathf.Cos(b)*.160f,-.126f+Mathf.Sin(b)*.014f,.192f+Mathf.Sin(b)*.116f);
                        Line(4,p,q,.0025f);if(i%2==0)Line(5,p+new Vector3(0,-.002f,-.003f),p+new Vector3(.004f,-.002f,.002f),.001f);
                    }
                    for(int i=0;i<5;i++)
                    {
                        float z=-.207f+i*.025f;
                        foreach(float side in new[]{-1f,1f}){Ring(1,new Vector3(side*.034f,-.127f,z),.005f,.0015f,new Vector3(1,.5f,1));}
                        Line(5,new Vector3(-.035f,-.130f,z),new Vector3(.035f,-.130f,z+.020f),.002f);
                        Line(5,new Vector3(.035f,-.131f,z),new Vector3(-.035f,-.131f,z+.020f),.002f);
                    }
                    // A small inset medal on the back of the hand; all detail remains part of the glove.
                    Ellipsoid(2,new Vector3(0,.212f,.142f),new Vector3(.038f,.004f,.043f),Quaternion.identity);
                    Ellipsoid(4,new Vector3(0,.216f,.142f),new Vector3(.030f,.002f,.034f),Quaternion.identity);
                    for(int i=0;i<3;i++)Line(2,new Vector3(-.016f+i*.016f,.219f,.126f),new Vector3(-.016f+i*.016f,.219f,.155f),.0023f);
                }
                else
                {
                    Add(0,MeleeSwordBlade(false),Vector3.zero,Quaternion.identity,Vector3.one);
                    Add(1,MeleeSwordBlade(true),Vector3.zero,Quaternion.identity,Vector3.one);
                    // Full tang grip, flared quillons, raised rain guard and a pointed ruby pommel.
                    Add(3,DrillTurned("Wrapped contoured sword grip",new[]{-.126f,-.115f,-.080f,.035f,.105f,.121f},new[]{.025f,.032f,.030f,.027f,.031f,.024f},.88f),Vector3.zero,Quaternion.identity,Vector3.one);
                    foreach(float z in new[]{-.117f,.109f})Ring(2,new Vector3(0,0,z),.030f,.0035f,new Vector3(1,.88f,1));
                    for(int i=0;i<11;i++)
                    {
                        for(int j=0;j<22;j++){float a=j*Mathf.PI*2/22,b=(j+1)*Mathf.PI*2/22;float z=-.102f+i*.018f;Line(4,new Vector3(Mathf.Cos(a)*.0305f,Mathf.Sin(a)*.0275f,z+j*.018f/22),new Vector3(Mathf.Cos(b)*.0305f,Mathf.Sin(b)*.0275f,z+(j+1)*.018f/22),.0017f);}
                    }
                    foreach(float side in new[]{-1f,1f})
                    {
                        var guardPath=new Vector3[33];var guardRadius=new float[33];
                        for(int i=0;i<33;i++){float t=i/32f;guardPath[i]=new Vector3(side*(.025f+t*.145f),0,.141f+.047f*t*t);guardRadius[i]=.018f-t*.007f;}
                        Add(2,MeleeCurvedTube(guardPath,guardRadius),Vector3.zero,Quaternion.identity,Vector3.one);
                        Ellipsoid(2,new Vector3(side*.171f,0,.191f),new Vector3(.019f,.023f,.023f),Quaternion.identity);
                    }
                    Box(2,new Vector3(0,0,.151f),new Vector3(.098f,.050f,.051f),.007f);
                    foreach(float side in new[]{-1f,1f})
                    {
                        Ellipsoid(4,new Vector3(0,side*.026f,.151f),new Vector3(.029f,.004f,.020f),Quaternion.identity);
                        Ellipsoid(0,new Vector3(0,side*.031f,.151f),new Vector3(.019f,.007f,.014f),Quaternion.identity);
                        // Thin brass borders accent the crimson fuller without blunting the steel edge.
                        for(int edge=-1;edge<=1;edge+=2)Line(2,new Vector3(edge*.018f,side*.0195f,.225f),new Vector3(edge*.013f,side*.0155f,.678f),.001f);
                        for(int i=0;i<5;i++)
                        {
                            float z=.27f+i*.075f;Line(5,new Vector3(-.008f,side*.0205f,z),new Vector3(0,side*.021f,z+.012f),.0009f);Line(5,new Vector3(0,side*.021f,z+.012f),new Vector3(.008f,side*.0205f,z),.0009f);
                        }
                    }
                    Add(2,DrillTurned("Faceted pommel mounting crown",new[]{-.182f,-.171f,-.142f,-.120f},new[]{.009f,.037f,.035f,.026f},.85f),Vector3.zero,Quaternion.identity,Vector3.one);
                    Ellipsoid(0,new Vector3(0,0,-.170f),new Vector3(.027f,.025f,.038f),Quaternion.identity);
                }
                meshes=new Mesh[6];
                for(int i=0;i<6;i++){var mesh=new Mesh{name=(glove?"Detailed boxing glove surface ":"Detailed murder sword surface ")+i};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(groups[i].ToArray(),true,true);mesh.RecalculateTangents();mesh.RecalculateBounds();meshes[i]=mesh;}
                foreach(var mesh in temporary)Object.Destroy(mesh);
                if(glove)boxingGloveMeshes=meshes;else murderSwordMeshes=meshes;
            }
            var surfaces=DetailedMeleeSurfaces(glove);
            for(int i=0;i<meshes.Length;i++)
            {
                var go=new GameObject(meshes[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=meshes[i];var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=surfaces[i];
                var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",surfaces[i].GetColor("_BaseColor"));renderer.SetPropertyBlock(properties);
            }
        }
        static Mesh MeleeSwordBlade(bool bevel)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0){var t=b;b=d;d=t;}
                int start=vertices.Count;vertices.AddRange(new[]{a,b,c,d});foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2(p.x*8+.5f,p.z*2));triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            var z=new[]{.179f,.700f,.956f};var w=new[]{.065f,.050f,.0001f};var h=new[]{.018f,.014f,.0001f};
            foreach(float face in new[]{-1f,1f})for(int i=0;i<2;i++)
            {
                if(!bevel)Quad(new Vector3(-w[i]*.6f,face*h[i],z[i]),new Vector3(w[i]*.6f,face*h[i],z[i]),new Vector3(w[i+1]*.6f,face*h[i+1],z[i+1]),new Vector3(-w[i+1]*.6f,face*h[i+1],z[i+1]),Vector3.up*face);
                else foreach(float side in new[]{-1f,1f})Quad(new Vector3(side*w[i]*.6f,face*h[i],z[i]),new Vector3(side*w[i],0,z[i]),new Vector3(side*w[i+1],0,z[i+1]),new Vector3(side*w[i+1]*.6f,face*h[i+1],z[i+1]),new Vector3(side,face,0));
            }
            var mesh=new Mesh{name=bevel?"Honed reflective diamond bevels":"Crimson tapered blade fuller"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
        static Mesh MeleeGlovePadding()
        {
            const int sides=64;var z=new[]{-.100f,-.060f,0f,.10f,.21f,.29f,.35f,.372f};var rx=new[]{.110f,.130f,.185f,.215f,.218f,.190f,.105f,.0001f};var ry=new[]{.09f,.11f,.15f,.176f,.178f,.15f,.08f,.0001f};var cy=new[]{0f,0f,.020f,.035f,.040f,.020f,0f,0f};
            var profile=new List<Vector4>();
            for(int segment=0;segment<z.Length-1;segment++)for(int step=0;step<12;step++)
            {
                Vector4 Point(int i){i=Mathf.Clamp(i,0,z.Length-1);return new Vector4(z[i],rx[i],ry[i],cy[i]);}
                float t=step/12f;var a=Point(segment-1);var b=Point(segment);var c=Point(segment+1);var d=Point(segment+2);
                profile.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            profile.Add(new Vector4(z[^1],rx[^1],ry[^1],cy[^1]));
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int ring=0;ring<profile.Count;ring++)for(int j=0;j<=sides;j++)
            {
                var p=profile[ring];float a=j*Mathf.PI*2/sides,c=Mathf.Cos(a),sn=Mathf.Sin(a);vertices.Add(new Vector3(c*p.y,p.w+sn*p.z,p.x));uv.Add(new Vector2((float)j/sides*2,ring/(float)(profile.Count-1)*2));
                var delta=profile[Mathf.Min(profile.Count-1,ring+1)]-profile[Mathf.Max(0,ring-1)];var tangent=new Vector3(c*delta.y,delta.w+sn*delta.z,delta.x);normals.Add(Vector3.Cross(new Vector3(-sn*p.y,c*p.z,0),tangent).normalized);
                if(ring<profile.Count-1&&j<sides){int i=ring*(sides+1)+j,k=i+sides+1;triangles.AddRange(new[]{i,i+1,k,i+1,k+1,k});}
            }
            return new Mesh{name="Continuous rounded boxing glove padding",vertices=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray()};
        }
        static Mesh MeleeCurvedTube(Vector3[] path,float[] radii)
        {
            const int sides=24;var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int ring=0;ring<path.Length;ring++)
            {
                var tangent=(path[Mathf.Min(path.Length-1,ring+1)]-path[Mathf.Max(0,ring-1)]).normalized;var across=Vector3.Cross(tangent,Vector3.up).normalized;
                for(int side=0;side<=sides;side++)
                {
                    float a=side*Mathf.PI*2/sides;var normal=Vector3.up*Mathf.Cos(a)+across*Mathf.Sin(a);vertices.Add(path[ring]+normal*radii[ring]);normals.Add(normal);uv.Add(new Vector2(side/(float)sides,ring/(float)(path.Length-1)));
                    if(ring<path.Length-1&&side<sides){int i=ring*(sides+1)+side,k=i+sides+1;triangles.AddRange(new[]{i,i+1,k,i+1,k+1,k});}
                }
            }
            return new Mesh{name="Continuous sculpted gold quillon",vertices=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray()};
        }
        static Material[] DetailedMeleeSurfaces(bool glove)
        {
            var cached=glove?boxingGloveSurfaces:murderSwordSurfaces;if(cached!=null)return cached;
            var shared=StockPistolSurfaces();cached=new Material[6];
            for(int i=0;i<6;i++)cached[i]=new Material(shared[i]){name=(glove?"Boxing leather ":"Crimson sword ")+i,enableInstancing=true};
            const int size=512;var albedo=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Fine pebbled red leather",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};var normal=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Fine leather grain normal",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var colors=new Color[size*size];var ns=new Color[colors.Length];
            float Grain(float x,float y)=>Mathf.PerlinNoise(x*110f/size,y*110f/size)*.7f+Mathf.PerlinNoise(x*237f/size,y*237f/size)*.3f;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                int i=y*size+x;float g=Grain(x,y);colors[i]=Color.white*(.83f+g*.17f);colors[i].a=1;
                float nx=.5f+(Grain((x+1)%size,y)-Grain((x+size-1)%size,y))*.28f,ny=.5f+(Grain(x,(y+1)%size)-Grain(x,(y+size-1)%size))*.28f;ns[i]=new Color(nx,ny,1,nx);
            }
            albedo.SetPixels(colors);albedo.Apply(true,true);normal.SetPixels(ns);normal.Apply(true,true);
            foreach(int i in new[]{0,3,4})
            {
                cached[i].SetTexture("_BaseMap",albedo);cached[i].SetTexture("_BumpMap",normal);cached[i].SetFloat("_BumpScale",.35f);cached[i].EnableKeyword("_NORMALMAP");cached[i].SetFloat("_Metallic",0);cached[i].SetFloat("_Smoothness",glove?.60f:.33f);
            }
            cached[0].SetColor("_BaseColor",new Color(.77f,.018f,.045f));cached[3].SetColor("_BaseColor",glove?new Color(.25f,.007f,.019f):new Color(.055f,.013f,.019f));cached[4].SetColor("_BaseColor",new Color(.045f,.012f,.017f));
            cached[1].SetColor("_BaseColor",new Color(.72f,.81f,.90f));cached[1].SetFloat("_Smoothness",.86f);
            cached[2]=new Material(shared[4]){name="Polished warm gold melee trim"};cached[2].SetColor("_BaseColor",new Color(.84f,.57f,.20f));cached[2].SetFloat("_Smoothness",.78f);
            cached[5].SetColor("_BaseColor",new Color(.93f,.81f,.59f));cached[5].SetFloat("_Smoothness",.28f);
            if(!glove){cached[0].SetTexture("_BaseMap",shared[0].mainTexture);cached[0].SetFloat("_Metallic",.45f);cached[0].SetFloat("_Smoothness",.85f);cached[0].SetFloat("_BumpScale",.05f);}
            if(glove)boxingGloveSurfaces=cached;else murderSwordSurfaces=cached;return cached;
        }

        static Material plasmaSurface;static readonly Material[] roastingSurfaces=new Material[2];
        static Material PlasmaSurface()
        {
            if(plasmaSurface)return plasmaSurface;
            var shader=Resources.Load<Shader>("CandyScenery/CandyPlasmaClaws");if(!shader)return null;
            return plasmaSurface=new Material(shader){name="White-hot plasma with cyan corona",enableInstancing=true};
        }
        static Mesh PlasmaClaw(int claw)
        {
            string key="Curved plasma blade "+claw;if(arsenalMeshes.TryGetValue(key,out var cached))return cached;
            const int rings=33,sides=12;var vertices=new Vector3[rings*(sides+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var indices=new List<int>();
            float length=claw==1?.60f:.56f;
            for(int ring=0;ring<rings;ring++)for(int side=0;side<=sides;side++)
            {
                float t=(float)ring/(rings-1),angle=side*Mathf.PI*2/sides;
                float width=(.010f+.026f*Mathf.Sin(t*Mathf.PI*.92f))*(1-Mathf.Pow(t,4)*.995f);
                float thickness=.0035f*(1-t*.96f);
                int i=ring*(sides+1)+side;var n=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                vertices[i]=new Vector3(n.x*thickness,n.y*width+.065f*t*t,t*length);normals[i]=new Vector3(n.x/Mathf.Max(.0001f,thickness),n.y/Mathf.Max(.0001f,width),0).normalized;uv[i]=new Vector2((float)side/sides,t);
                if(ring<rings-1&&side<sides){int j=i+sides+1;indices.AddRange(new[]{i,i+1,j,i+1,j+1,j});}
            }
            var mesh=new Mesh{name=key,vertices=vertices,normals=normals,uv=uv,triangles=indices.ToArray()};mesh.RecalculateBounds();arsenalMeshes[key]=mesh;return mesh;
        }
        void BuildRoastingFork(Transform root)
        {
            Part(root,"Rounded hardwood roasting handle",PrimitiveType.Capsule,Vector3.zero,new Vector3(.046f,.28f,.046f),Wood,Quaternion.Euler(90,0,0));
            var grip=root.GetChild(root.childCount-1).GetComponent<MeshRenderer>();var wood=RoastingSurface(true);if(wood)grip.sharedMaterial=wood;
            var steel=new CandyAccessoryMeshes.Builder();
            steel.Cone(new Vector3(0,0,.12f),new Vector3(0,0,.87f),.0038f,.0038f,12);
            steel.Cone(new Vector3(0,0,.12f),new Vector3(0,0,.155f),.026f,.024f,20);
            foreach(float side in new[]{-1f,1f})
            {
                steel.Cone(new Vector3(0,0,.87f),new Vector3(side*.032f,0,.94f),.0038f,.0038f,10);
                steel.Cone(new Vector3(side*.032f,0,.94f),new Vector3(side*.032f,0,1.16f),.0038f,.001f,10);
            }
            CachedMesh(root,"Polished stainless double-prong roasting fork",steel,new Color(.72f,.78f,.82f));
            MeshPart(root,"Pillowy toasted marshmallow",RoastedMarshmallow(),Color.white);
            var body=root.GetChild(root.childCount-1);body.localPosition=new Vector3(0,0,1.065f);var surface=RoastingSurface(false);if(surface)body.GetComponent<MeshRenderer>().sharedMaterial=surface;
        }
        static Material RoastingSurface(bool wood)
        {
            int index=wood?1:0;if(roastingSurfaces[index])return roastingSurfaces[index];var shader=Resources.Load<Shader>("CandyScenery/CandyRoastedMarshmallow");if(!shader)return null;
            var surface=new Material(shader){name=wood?"Warm grained hardwood":"Powdered caramelized marshmallow",enableInstancing=true};surface.SetFloat("_Wood",index);return roastingSurfaces[index]=surface;
        }
        static Mesh RoastedMarshmallow()
        {
            const string key="Soft rounded cylinder marshmallow";if(arsenalMeshes.TryGetValue(key,out var cached))return cached;
            float[] depth={-.106f,-.104f,-.096f,-.08f,-.05f,0,.05f,.08f,.096f,.104f,.106f};
            float[] radius={0,.038f,.064f,.073f,.077f,.078f,.076f,.072f,.062f,.036f,0};
            var smoothDepth=new float[(depth.Length-1)*3+1];var smoothRadius=new float[smoothDepth.Length];
            for(int ring=0;ring<smoothDepth.Length;ring++)
            {
                float sample=ring/3f;int segment=Mathf.Min(Mathf.FloorToInt(sample),depth.Length-2);float t=sample-segment;
                float a=radius[Mathf.Max(0,segment-1)],b=radius[segment],c=radius[segment+1],d=radius[Mathf.Min(radius.Length-1,segment+2)];
                smoothDepth[ring]=Mathf.Lerp(depth[segment],depth[segment+1],t);
                smoothRadius[ring]=Mathf.Max(0,.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            depth=smoothDepth;radius=smoothRadius;
            const int sides=80;var vertices=new Vector3[depth.Length*(sides+1)];var uv=new Vector2[vertices.Length];var indices=new List<int>();
            for(int ring=0;ring<depth.Length;ring++)for(int side=0;side<=sides;side++)
            {
                float angle=side*Mathf.PI*2/sides,r=radius[ring]*(1+.024f*Mathf.Sin(angle*3+depth[ring]*21)+.016f*Mathf.Cos(angle*5-depth[ring]*17));int i=ring*(sides+1)+side;
                vertices[i]=new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r-.004f*Mathf.Sin(angle*2)*radius[ring]/.078f,depth[ring]);uv[i]=new Vector2((float)side/sides,(depth[ring]+.106f)/.212f);
                if(ring<depth.Length-1&&side<sides){int j=i+sides+1;indices.AddRange(new[]{i,i+1,j,i+1,j+1,j});}
            }
            var mesh=new Mesh{name=key,vertices=vertices,uv=uv,triangles=indices.ToArray()};mesh.RecalculateNormals();
            var normals=mesh.normals;for(int ring=0;ring<depth.Length;ring++){int first=ring*(sides+1),last=first+sides;var normal=(normals[first]+normals[last]).normalized;normals[first]=normals[last]=normal;}mesh.normals=normals;
            mesh.RecalculateBounds();arsenalMeshes[key]=mesh;return mesh;
        }
        static void PositionClaws(Transform weapon,CandyHandPose hand,bool left)
        {
            var skeleton=hand.skeletonTracked?hand.skeleton:CandyHandSkeleton.Rest(left,.12f);
            for(int i=0;i<3;i++)
            {
                var a=skeleton.Joint((i+1)*4);var b=skeleton.Joint((i+2)*4);
                weapon.GetChild(i).localPosition=(a+b)*.5f+new Vector3(0,.009f,-.012f);
                weapon.GetChild(i).localRotation=Quaternion.Euler(0,(i-1)*(left?-8f:8f),0);
            }
        }
        static Mesh marshmallowFlames;static Material marshmallowFire;
        static void AddMarshmallowFire(Transform root)
        {
            if(!marshmallowFire)
            {
                var shader=Resources.Load<Shader>("CandyScenery/CandyMarshmallowFire");if(!shader)return;
                marshmallowFire=new Material(shader){name="Animated toasted sugar fire",enableInstancing=true};
            }
            if(!marshmallowFlames)
            {
                var source=Resources.GetBuiltinResource<Mesh>("Cube.fbx");marshmallowFlames=Object.Instantiate(source);marshmallowFlames.name="Turbulent marshmallow flame volume";
                var vertices=marshmallowFlames.vertices;var bounds=source.bounds;
                for(int i=0;i<vertices.Length;i++){var p=vertices[i]-bounds.center;vertices[i]=new Vector3(p.x/bounds.size.x*.28f,p.y/bounds.size.y*.44f+.15f,p.z/bounds.size.z*.28f);}
                marshmallowFlames.vertices=vertices;
                // The shader keeps the fire rising in world space as the stick tilts.
                marshmallowFlames.bounds=new Bounds(Vector3.zero,Vector3.one*.9f);
            }
            var fire=new GameObject("Marshmallow living fire",typeof(MeshFilter),typeof(MeshRenderer));fire.transform.SetParent(root,false);fire.transform.localPosition=new Vector3(0,0,1.065f);
            fire.GetComponent<MeshFilter>().sharedMesh=marshmallowFlames;var renderer=fire.GetComponent<MeshRenderer>();renderer.sharedMaterial=marshmallowFire;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",Color.white);renderer.SetPropertyBlock(properties);
        }
        void Blade(Transform root,string name,Vector2[] outline,float thickness,Color color)
        {
            if(!arsenalMeshes.TryGetValue(name,out var mesh))
            {
                int n=outline.Length;var vertices=new Vector3[n*2];var triangles=new List<int>();
                for(int i=0;i<n;i++){vertices[i]=(name.Contains("axe")||name.Contains("stock"))?new Vector3(thickness*.5f,outline[i].x,outline[i].y):new Vector3(outline[i].x,thickness*.5f,outline[i].y);vertices[i+n]=(name.Contains("axe")||name.Contains("stock"))?new Vector3(-thickness*.5f,outline[i].x,outline[i].y):new Vector3(outline[i].x,-thickness*.5f,outline[i].y);}
                for(int i=1;i<n-1;i++){triangles.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});}
                for(int i=0;i<n;i++){int j=(i+1)%n;triangles.AddRange(new[]{i,j,j+n,i,j+n,i+n});}
                if(name.Contains("axe")||name.Contains("stock"))for(int i=0;i<triangles.Count;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
                mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();arsenalMeshes[name]=mesh;
            }
            MeshPart(root,name,mesh,color);
        }
        void CachedMesh(Transform root,string key,CandyAccessoryMeshes.Builder builder,Color color)
        {if(!arsenalMeshes.TryGetValue(key,out var mesh)){mesh=builder.Mesh(key);arsenalMeshes[key]=mesh;}MeshPart(root,key,mesh,color);}
    }
}
