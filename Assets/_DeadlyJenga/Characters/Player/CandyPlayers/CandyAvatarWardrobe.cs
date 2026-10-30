using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    // Cosmetic shells follow the existing bones. No physics or gameplay components are added.
    public sealed partial class CandyAvatarWardrobe : MonoBehaviour
    {
        enum Tone { Primary, Dark, White, Black, Orange, Yellow, Blue, Purple, Red, Brown, Silver, Glow, Armor, Hair, Green }
        sealed class Piece { public Renderer renderer; public Tone tone; public int bone; public bool armor; }
        readonly List<Piece> pieces=new List<Piece>();
        static readonly Dictionary<Material,Material> emitterMaterials=new Dictionary<Material,Material>();
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        MaterialPropertyBlock properties;
        AvatarAppearance appearance;
        bool built;
        Mesh originalBody;
        static readonly Dictionary<Mesh,Mesh> snowBodies=new Dictionary<Mesh,Mesh>();
        Color lastTeam;
        bool lastTeamed;
        public Transform LeftPalmEmitter {get;private set;}
        public Transform RightPalmEmitter {get;private set;}
        public Transform LeftSoleEmitter {get;private set;}
        public Transform RightSoleEmitter {get;private set;}

        public void Apply(CandyPlayerRig rig,AvatarAppearance value)
        {
            value=CandyPlayerAvatar.SanitizeAppearance(value);
            if(built&&appearance.accessories==value.accessories&&CandyPlayerAvatar.StyleBits(appearance)==CandyPlayerAvatar.StyleBits(value))return;
            foreach(var p in pieces)if(p.renderer){p.renderer.gameObject.SetActive(false);Destroy(p.renderer.gameObject);}
            foreach(var t in new[]{LeftPalmEmitter,RightPalmEmitter,LeftSoleEmitter,RightSoleEmitter})if(t)Destroy(t.gameObject);
            LeftPalmEmitter=RightPalmEmitter=LeftSoleEmitter=RightSoleEmitter=null;
            pieces.Clear();appearance=value;built=true;
            if(!originalBody)originalBody=rig.bodyRenderer.sharedMesh;
            rig.bodyRenderer.sharedMesh=value.skin==AvatarSkin.Snowman?SnowBody(originalBody):originalBody;
            BuildSkin(rig);BuildExpandedSkin(rig);BuildHair(rig);BuildGlasses(rig);BuildClothes(rig);BuildExpandedClothes(rig);
            if((value.accessories&AvatarAccessory.PumpkinHelmet)!=0)Pumpkin(rig,"accessory pumpkin",Tone.Primary);
            if(value.poweredArmor)BuildArmor(rig);
            BatchPieces(rig);
            lastTeam=new Color(-1,-1,-1);RefreshPalette(rig);
        }
        static Mesh SnowBody(Mesh source)
        {
            if(snowBodies.TryGetValue(source,out var cached))return cached;
            var mesh=Instantiate(source);mesh.name=source.name+" - slender snowman arms";var vertices=mesh.vertices;var weights=mesh.boneWeights;var bindings=mesh.bindposes;
            for(int i=0;i<vertices.Length;i++)
            {
                var weight=weights[i];int bone=weight.weight0>=weight.weight1?weight.boneIndex0:weight.boneIndex1;
                if(bone!=2&&bone!=3&&bone!=5&&bone!=6)continue;
                var local=bindings[bone].MultiplyPoint3x4(vertices[i]);local.x*=.35f;local.z*=.35f;vertices[i]=bindings[bone].inverse.MultiplyPoint3x4(local);
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();snowBodies[source]=mesh;return mesh;
        }
        void Add(CandyPlayerRig rig,string id,int bone,Tone tone,System.Action<CandyAccessoryMeshes.Builder> build)
        {
            if(!meshes.TryGetValue(id,out var mesh)){var builder=new CandyAccessoryMeshes.Builder();build(builder);mesh=builder.Mesh(id);meshes[id]=mesh;}
            var go=new GameObject("Visual - "+id);go.transform.SetParent(rig.AppearanceBone(bone),false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=tone==Tone.Glow?EmitterMaterial(rig.bodyRenderer.sharedMaterial):rig.bodyRenderer.sharedMaterial;
            pieces.Add(new Piece{renderer=renderer,tone=tone,bone=bone});
        }
        static Material EmitterMaterial(Material source)
        {
            if(!source)return null;
            if(emitterMaterials.TryGetValue(source,out var material)&&material)return material;
            material=new Material(source){name=source.name+" - armor emitters",enableInstancing=true};
            material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.20f,.87f,1)*2);
            emitterMaterials[source]=material;return material;
        }
        void Ball(CandyPlayerRig rig,string id,int bone,Tone tone,Vector3 p,Vector3 size)=>Add(rig,id,bone,tone,b=>b.Sphere(p,size));
        void FaceEyes(CandyPlayerRig rig,string id,Tone tone)
        {for(int s=-1;s<=1;s+=2)Ball(rig,id+s,1,tone,new Vector3(s*.085f,.045f,.205f),new Vector3(.032f,.038f,.018f));}
        void Hat(CandyPlayerRig rig,string id,Tone tone,bool pointed)
        {Add(rig,id,1,tone,b=>{b.Cone(new Vector3(0,.18f,0),new Vector3(0,.205f,0),.31f,.31f,24);b.Cone(new Vector3(0,.20f,0),new Vector3(pointed?.07f:0,pointed?.63f:.43f,0),.17f,pointed?0:.17f,24);});}
        void Cape(CandyPlayerRig rig,string id,Tone tone,bool collar=false)
        {Add(rig,id,0,tone,b=>{b.Wedge(new Vector3(-.23f,1.25f,-.13f),new Vector3(.23f,1.25f,-.13f),new Vector3(-.37f,.32f,-.30f),new Vector3(.37f,.32f,-.30f),.025f);if(collar)b.Wedge(new Vector3(-.29f,1.45f,-.12f),new Vector3(.29f,1.45f,-.12f),new Vector3(-.17f,1.20f,-.12f),new Vector3(.17f,1.20f,-.12f),.035f);});}
        void Pants(CandyPlayerRig rig,string id,Tone tone)=>FittedGarment(rig,id,tone,true);
        void Pumpkin(CandyPlayerRig rig,string id,Tone tone)
        {
            Add(rig,id+" carved shell",1,tone,b=>b.CarvedPumpkin());
            Add(rig,id+" stem",1,Tone.Brown,b=>b.Cone(new Vector3(0,.25f,0),new Vector3(.035f,.38f,-.015f),.045f,.025f,10));
        }
        void BuildSkin(CandyPlayerRig rig)
        {
            switch(appearance.skin)
            {
                case AvatarSkin.Snowman:
                    Ball(rig,"snow belly",0,Tone.Primary,new Vector3(0,.78f,0),new Vector3(.245f,.27f,.19f));
                    Ball(rig,"snow chest",0,Tone.Primary,new Vector3(0,1.10f,0),new Vector3(.215f,.22f,.17f));FaceEyes(rig,"coal eyes",Tone.Black);Hat(rig,"snow top hat",Tone.Black,false);
                    Add(rig,"carrot",1,Tone.Orange,b=>b.Cone(new Vector3(0,-.01f,.20f),new Vector3(0,-.025f,.41f),.045f,0,12));
                    Add(rig,"coal smile",1,Tone.Black,b=>{for(int i=0;i<7;i++){float a=(i-3)*.25f;b.Sphere(new Vector3(Mathf.Sin(a)*.15f,-.085f-Mathf.Cos(a)*.045f,.20f),Vector3.one*.014f);}});
                    Add(rig,"snow buttons",0,Tone.Black,b=>{for(int i=0;i<3;i++)b.Sphere(new Vector3(0,.77f+i*.15f,.19f),Vector3.one*.025f);});
                    Add(rig,"blue scarf",0,Tone.Blue,b=>{b.Ring(new Vector3(0,1.33f,0),.16f,.045f,Quaternion.Euler(90,0,0));b.Box(new Vector3(.11f,1.12f,.16f),new Vector3(.10f,.34f,.035f));});
                    for(int s=0;s<2;s++)for(int j=0;j<2;j++){int bone=(s==0?2:5)+j;Add(rig,"snow stick "+s+j,bone,Tone.Brown,b=>{b.Beam(Vector3.zero,new Vector3(0,.28f,0),.035f);b.Beam(new Vector3(0,.12f,0),new Vector3(.08f,.20f,0),.018f);});}break;
                case AvatarSkin.Penguin:
                    Ball(rig,"penguin belly",0,Tone.Primary,new Vector3(0,.94f,.105f),new Vector3(.19f,.35f,.13f));
                    FaceEyes(rig,"penguin blue eyes",Tone.Blue);Add(rig,"penguin beak",1,Tone.Yellow,b=>b.Cone(new Vector3(0,-.06f,.19f),new Vector3(0,-.075f,.37f),.075f,0,12));
                    for(int s=0;s<2;s++)Ball(rig,"penguin orange foot"+s,s==0?10:13,Tone.Orange,new Vector3(0,0,.04f),new Vector3(.115f,.09f,.19f));break;
                case AvatarSkin.Frankenstein:
                    FaceEyes(rig,"frank eyes",Tone.Black);Add(rig,"frank bolts",1,Tone.Silver,b=>{for(int s=-1;s<=1;s+=2){b.Cone(new Vector3(s*.20f,-.06f,0),new Vector3(s*.30f,-.06f,0),.027f,.027f,8);b.Box(new Vector3(s*.30f,-.06f,0),Vector3.one*.07f);}});
                    Jacket(rig,"frank purple jacket",Tone.Purple);Pants(rig,"frank trousers",Tone.Dark);Hair(rig,AvatarHair.Spikes);break;
                case AvatarSkin.JackOLantern:Pumpkin(rig,"jack lantern",Tone.Orange);break;
                case AvatarSkin.Witch:
                    Hat(rig,"witch hat",Tone.Purple,true);Cape(rig,"witch cape",Tone.Purple);Robe(rig,"witch gown",Tone.Purple);Pants(rig,"witch boots",Tone.Dark);LongNose(rig,"witch nose");FaceEyes(rig,"witch eyes",Tone.Black);break;
                case AvatarSkin.Vampire:
                    Cape(rig,"vampire cape outer",Tone.Black,true);Add(rig,"vampire red lining",0,Tone.Red,b=>b.Wedge(new Vector3(-.21f,1.24f,-.105f),new Vector3(.21f,1.24f,-.105f),new Vector3(-.34f,.34f,-.27f),new Vector3(.34f,.34f,-.27f),.008f));
                    Jacket(rig,"vampire suit",Tone.Dark);Pants(rig,"vampire trousers",Tone.Dark);LongNose(rig,"vampire nose");FaceEyes(rig,"vampire eyes",Tone.Red);
                    Add(rig,"vampire fangs",1,Tone.White,b=>{for(int s=-1;s<=1;s+=2)b.Cone(new Vector3(s*.065f,-.09f,.204f),new Vector3(s*.065f,-.16f,.216f),.019f,0,10);});
                    Add(rig,"widows peak",1,Tone.Hair,b=>{b.Sphere(new Vector3(0,.15f,-.065f),new Vector3(.225f,.10f,.19f));b.Cone(new Vector3(0,.17f,.17f),new Vector3(0,.055f,.20f),.07f,0,12);});break;
            }
        }
        void LongNose(CandyPlayerRig rig,string id)=>Add(rig,id,1,Tone.Primary,b=>b.Cone(new Vector3(0,-.02f,.19f),new Vector3(0,-.06f,.38f),.043f,.012f,12));
        void Jacket(CandyPlayerRig rig,string id,Tone tone)=>FittedGarment(rig,id,tone,false);
        void Robe(CandyPlayerRig rig,string id,Tone tone){FittedGarment(rig,id+" tunic",tone,false);FittedSkirt(rig,id+" skirt",tone);}
        void BuildHair(CandyPlayerRig rig){if(appearance.hair!=0)Hair(rig,appearance.hair);}
        void Hair(CandyPlayerRig rig,AvatarHair hair)
        {
            Add(rig,"hair "+hair,1,Tone.Hair,b=>{
                if(hair==AvatarHair.Dreadlocks){for(int i=0;i<12;i++){float a=i*2.399963f;b.Cone(new Vector3(Mathf.Cos(a)*.20f,.18f,Mathf.Sin(a)*.18f),new Vector3(Mathf.Cos(a)*.23f,-.24f,Mathf.Sin(a)*.23f),.027f,.02f,8);}return;}
                if(hair==AvatarHair.Pompadour){b.Sphere(new Vector3(0,.24f,.06f),new Vector3(.21f,.17f,.20f));return;}
                if(hair==AvatarHair.LibertySpikes){for(int i=0;i<7;i++){float a=i*Mathf.PI/3.5f;b.Cone(new Vector3(Mathf.Cos(a)*.13f,.18f,Mathf.Sin(a)*.13f),new Vector3(Mathf.Cos(a)*.32f,.50f,Mathf.Sin(a)*.32f),.055f,0,6);}return;}
                if(hair==AvatarHair.Spikes){for(int i=0;i<13;i++){float a=i*2.4f;b.Cone(new Vector3(Mathf.Cos(a)*.16f,.17f,Mathf.Sin(a)*.14f),new Vector3(Mathf.Cos(a)*.21f,.28f+(i%3)*.035f,Mathf.Sin(a)*.18f),.065f,0,8);}return;}
                b.Sphere(new Vector3(0,.14f,-.035f),new Vector3(.226f,hair==AvatarHair.Buzz?.075f:.13f,.214f));
                if(hair==AvatarHair.Afro||hair==AvatarHair.Curls){for(int i=0;i<18;i++){float a=i*2.4f;b.Sphere(new Vector3(Mathf.Cos(a)*.21f,.13f+(i%3)*.055f,Mathf.Sin(a)*.20f),Vector3.one*(hair==AvatarHair.Afro?.115f:.065f));}}
                if(hair==AvatarHair.Bob||hair==AvatarHair.LongWaves||hair==AvatarHair.Braids){for(int s=-1;s<=1;s+=2)for(int i=0;i<(hair==AvatarHair.LongWaves?7:4);i++)b.Sphere(new Vector3(s*(.21f+(hair==AvatarHair.LongWaves?Mathf.Sin(i*1.5f)*.025f:0)),.12f-i*.075f,-.055f),new Vector3(.065f,.08f,.17f));}
                if(hair==AvatarHair.Ponytail||hair==AvatarHair.TwinTails||hair==AvatarHair.Braids){for(int s=-1;s<=1;s+=2){float x=hair==AvatarHair.Ponytail?0:s*.23f;b.Sphere(new Vector3(x,.035f,-.23f),new Vector3(.065f,.20f,.075f));if(x==0)break;}}
                if(hair==AvatarHair.Bun)b.Sphere(new Vector3(0,.30f,-.07f),Vector3.one*.105f);
                if(hair==AvatarHair.SidePart)b.Sphere(new Vector3(-.08f,.23f,.07f),new Vector3(.16f,.08f,.14f));
                if(hair==AvatarHair.CrewCut)b.Box(new Vector3(0,.21f,0),new Vector3(.31f,.08f,.27f));
            });
        }
        void BuildGlasses(CandyPlayerRig rig)
        {
            if(appearance.eyewear==0)return;var kind=appearance.eyewear;
            if(kind==AvatarEyewear.Visor||kind==AvatarEyewear.VRVisor||kind==AvatarEyewear.SkiGoggles)
            {
                Add(rig,"visor frame",1,Tone.Dark,b=>{for(int i=0;i<12;i++){float a=Mathf.Lerp(-.95f,.95f,i/12f),next=Mathf.Lerp(-.95f,.95f,(i+1)/12f);foreach(float y in new[]{-.045f,.09f})b.Beam(new Vector3(Mathf.Sin(a)*.235f,y,Mathf.Cos(a)*.235f),new Vector3(Mathf.Sin(next)*.235f,y,Mathf.Cos(next)*.235f),.01f);}});
                Add(rig,"panoramic visor lens",1,Tone.Blue,b=>{for(int i=0;i<12;i++){float a=Mathf.Lerp(-.95f,.95f,i/12f),next=Mathf.Lerp(-.95f,.95f,(i+1)/12f);var from=new Vector3(Mathf.Sin(a)*.235f,0,Mathf.Cos(a)*.235f);var to=new Vector3(Mathf.Sin(next)*.235f,0,Mathf.Cos(next)*.235f);b.Wedge(from+Vector3.up*.085f,to+Vector3.up*.085f,from-Vector3.up*.04f,to-Vector3.up*.04f,.008f);}});
                return;
            }
            Add(rig,"eyewear "+kind,1,Tone.Dark,b=>{for(int s=-1;s<=1;s+=2){if((kind==AvatarEyewear.Monocle||kind==AvatarEyewear.CyberEye)&&s==-1)continue;var c=new Vector3(s*.10f,.03f,.223f);
                if(kind==AvatarEyewear.Square){b.Box(c+Vector3.up*.055f,new Vector3(.15f,.015f,.025f));b.Box(c-Vector3.up*.055f,new Vector3(.15f,.015f,.025f));for(int edge=-1;edge<=1;edge+=2)b.Box(c+Vector3.right*edge*.07f,new Vector3(.015f,.12f,.025f));}
                else if(kind==AvatarEyewear.Heart){for(int i=0;i<24;i++){float a=i*Mathf.PI/12,aa=(i+1)*Mathf.PI/12;Vector3 Heart(float t)=>new Vector3(Mathf.Pow(Mathf.Sin(t),3)*.075f,(13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t))*.005f,0);b.Beam(c+Heart(a),c+Heart(aa),.008f);}}
                else if(kind==AvatarEyewear.Shutter){for(int slat=-2;slat<=2;slat++)b.Beam(c+new Vector3(-.065f,slat*.022f,0),c+new Vector3(.065f,slat*.022f,0),.006f);}
                else if(kind==AvatarEyewear.Star){for(int i=0;i<5;i++){float a=i*Mathf.PI*.4f,aa=a+Mathf.PI*.8f;b.Beam(c+new Vector3(Mathf.Sin(a),Mathf.Cos(a),0)*.08f,c+new Vector3(Mathf.Sin(aa),Mathf.Cos(aa),0)*.08f,.008f);}}
                else {b.Ring(c,.071f,.010f,Quaternion.identity);if(kind==AvatarEyewear.CatEye)b.Beam(c+Vector3.right*s*.04f,c+new Vector3(s*.10f,.08f,0),.012f);if(kind==AvatarEyewear.Aviator)b.Ring(c+Vector3.down*.015f,.075f,.008f,Quaternion.identity);}
                b.Beam(new Vector3(s*.17f,.04f,.21f),new Vector3(s*.22f,.04f,-.06f),.01f);}
                if(kind!=AvatarEyewear.Monocle)b.Beam(new Vector3(-.03f,.04f,.24f),new Vector3(.03f,.04f,.24f),.01f);
            });
        }
        void BuildClothes(CandyPlayerRig rig)
        {
            var clothes=appearance.clothing;
            if((clothes&AvatarClothing.MonkRobe)!=0){Robe(rig,"monk robes",Tone.Brown);Jacket(rig,"monk lapels",Tone.Brown);Add(rig,"monk belt",0,Tone.Dark,b=>b.Box(new Vector3(0,.85f,.02f),new Vector3(.40f,.055f,.29f)));}
            if((clothes&AvatarClothing.Diaper)!=0){Ball(rig,"white diaper",0,Tone.White,new Vector3(0,.64f,.005f),new Vector3(.202f,.135f,.15f));Add(rig,"silver diaper clasp",0,Tone.Silver,b=>b.Box(new Vector3(.12f,.68f,.15f),new Vector3(.055f,.025f,.025f)));}
            if((clothes&AvatarClothing.FootballPads)!=0){Ball(rig,"football chest pads",0,Tone.Silver,new Vector3(0,1.12f,0),new Vector3(.24f,.20f,.18f));for(int s=0;s<2;s++)Ball(rig,"football shoulder pad "+s,s==0?2:5,Tone.White,Vector3.zero,new Vector3(.14f,.12f,.15f));}
            if((clothes&AvatarClothing.HockeyHelmet)!=0)
            {
                Ball(rig,"hockey helmet shell",1,Tone.Primary,new Vector3(0,.15f,-.055f),new Vector3(.245f,.115f,.225f));
                Add(rig,"hockey helmet vents",1,Tone.Dark,b=>{for(int s=-1;s<=1;s+=2)for(int i=0;i<3;i++)b.Box(new Vector3(s*(.09f+i*.035f),.235f,-.04f),new Vector3(.014f,.018f,.10f));});
                Add(rig,"hockey wire cage",1,Tone.Silver,b=>{for(int i=-3;i<=3;i++){float x=i*.055f;b.Beam(new Vector3(x,.09f,.24f),new Vector3(x,-.19f,.22f),.005f);}for(int i=0;i<4;i++)b.Beam(new Vector3(-.20f,.07f-i*.075f,.235f),new Vector3(.20f,.07f-i*.075f,.235f),.005f);});
            }
            if((clothes&AvatarClothing.FootballHelmet)!=0)
            {
                Ball(rig,"football helmet crown",1,Tone.Primary,new Vector3(0,.13f,-.075f),new Vector3(.27f,.17f,.24f));
                for(int side=-1;side<=1;side+=2){Ball(rig,"football ear guard "+side,1,Tone.Primary,new Vector3(side*.23f,-.02f,-.025f),new Vector3(.052f,.15f,.13f));Ball(rig,"football ear port "+side,1,Tone.Dark,new Vector3(side*.275f,-.015f,.005f),new Vector3(.007f,.035f,.035f));}
                Add(rig,"football helmet stripe",1,Tone.White,b=>b.Box(new Vector3(0,.29f,-.055f),new Vector3(.055f,.018f,.29f)));
                Add(rig,"football face bars",1,Tone.Silver,b=>{for(int i=0;i<2;i++){float y=-.08f-i*.075f;b.Beam(new Vector3(-.21f,y,.27f),new Vector3(.21f,y,.27f),.012f);}for(int side=-1;side<=1;side+=2){b.Beam(new Vector3(side*.19f,.01f,.19f),new Vector3(side*.22f,-.17f,.27f),.012f);b.Beam(new Vector3(side*.09f,-.08f,.27f),new Vector3(side*.09f,-.16f,.27f),.009f);}});
            }
            if((clothes&AvatarClothing.GrassSkirt)!=0)Add(rig,"grass skirt",0,Tone.Primary,b=>{for(int i=0;i<28;i++){float a=i*Mathf.PI*2/28;var v=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));b.Cone(new Vector3(0,.75f,0)+v*.18f,new Vector3(0,.37f,0)+v*.27f,.014f,.006f,5);}});
            if((clothes&AvatarClothing.Tuxedo)!=0){Jacket(rig,"tux jacket",Tone.Dark);Add(rig,"tux shirt",0,Tone.White,b=>b.Box(new Vector3(0,1.10f,.155f),new Vector3(.12f,.30f,.025f)));Pants(rig,"tux pants",Tone.Dark);}
            if((clothes&(AvatarClothing.Tuxedo|AvatarClothing.BowTie))!=0)Add(rig,"bow tie",0,Tone.Red,b=>{b.Sphere(new Vector3(-.045f,1.23f,.18f),new Vector3(.045f,.025f,.025f));b.Sphere(new Vector3(.045f,1.23f,.18f),new Vector3(.045f,.025f,.025f));});
            if((clothes&AvatarClothing.Hoodie)!=0){Jacket(rig,"hoodie",Tone.Primary);Add(rig,"hoodie rim",1,Tone.Primary,b=>b.Ring(new Vector3(0,0,-.08f),.245f,.055f,Quaternion.identity));}
        }
        void BuildExpandedClothes(CandyPlayerRig rig)
        {
            var clothes=appearance.clothing;
            if((clothes&AvatarClothing.BomberJacket)!=0){Jacket(rig,"bomber jacket",Tone.Brown);Add(rig,"bomber zip",0,Tone.Silver,b=>b.Box(new Vector3(0,1.03f,.20f),new Vector3(.018f,.47f,.02f)));}
            if((clothes&AvatarClothing.SailorShirt)!=0){Jacket(rig,"sailor shirt",Tone.White);Add(rig,"sailor collar",0,Tone.Blue,b=>b.Wedge(new Vector3(-.18f,1.27f,.18f),new Vector3(.18f,1.27f,.18f),new Vector3(-.02f,1.03f,.20f),new Vector3(.02f,1.03f,.20f),.02f));}
            if((clothes&AvatarClothing.Overalls)!=0){Pants(rig,"denim overalls",Tone.Blue);Ball(rig,"overall bib",0,Tone.Blue,new Vector3(0,.94f,.15f),new Vector3(.17f,.20f,.065f));}
            if((clothes&AvatarClothing.PufferVest)!=0){Jacket(rig,"puffer vest",Tone.Primary);Add(rig,"quilted vest seams",0,Tone.Dark,b=>{for(int i=0;i<6;i++)b.Ring(new Vector3(0,.80f+i*.08f,0),.215f,.009f,Quaternion.Euler(90,0,0));});}
            if((clothes&AvatarClothing.RoyalMantle)!=0){Cape(rig,"royal velvet mantle",Tone.Purple,true);Add(rig,"royal white collar",0,Tone.White,b=>b.Ring(new Vector3(0,1.27f,0),.22f,.035f,Quaternion.Euler(90,0,0)));}
        }
        static readonly Dictionary<Tone,Material> armorSurfaces=new Dictionary<Tone,Material>();
        static Texture2D armorGrain,armorNormal;
        static Material ArmorSurface(Tone tone)
        {
            if(armorSurfaces.TryGetValue(tone,out var cached))return cached;
            if(!armorGrain)
            {
                const int size=512;armorGrain=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Armor ceramic fine directional finish",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};armorNormal=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Armor microscopic brushed normal",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
                var colors=new Color[size*size];var normals=new Color[colors.Length];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++){int i=y*size+x;float n=Mathf.PerlinNoise(x*.048f,y*.57f);float v=.91f+n*.09f;colors[i]=new Color(v,v,v,1);float nx=.5f+(n-.5f)*.035f;normals[i]=new Color(nx,.5f,1,nx);}
                armorGrain.SetPixels(colors);armorGrain.Apply(true,true);armorNormal.SetPixels(normals);armorNormal.Apply(true,true);
            }
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Detailed flying armor "+tone,enableInstancing=true};material.SetTexture("_BaseMap",armorGrain);material.SetTexture("_BumpMap",armorNormal);material.SetFloat("_BumpScale",tone==Tone.Dark?.55f:.18f);material.EnableKeyword("_NORMALMAP");material.SetFloat("_Metallic",tone==Tone.Dark?.05f:tone==Tone.Glow?.15f:.78f);material.SetFloat("_Smoothness",tone==Tone.Dark?.28f:tone==Tone.Armor?.77f:.88f);
            if(tone==Tone.Glow){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.10f,.75f,1)*1.3f);}armorSurfaces[tone]=material;return material;
        }
        void ArmorPart(CandyPlayerRig rig,string id,int bone,Tone tone,System.Func<Mesh> create)
        {
            string key="Detailed armor - "+id;if(!meshes.TryGetValue(key,out var mesh)){mesh=create();mesh.name=key;mesh.RecalculateTangents();mesh.RecalculateBounds();meshes[key]=mesh;}
            var go=new GameObject(key,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(rig.AppearanceBone(bone),false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=ArmorSurface(tone);pieces.Add(new Piece{renderer=renderer,tone=tone,bone=bone,armor=true});
        }
        void ArmorBall(CandyPlayerRig rig,string id,int bone,Tone tone,Vector3 p,Vector3 radii)
        {
            ArmorPart(rig,id,bone,tone,()=>{var mesh=Instantiate(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));var vertices=mesh.vertices;var normals=new Vector3[vertices.Length];var center=mesh.bounds.center;
                for(int i=0;i<vertices.Length;i++){var n=(vertices[i]-center).normalized;vertices[i]=p+Vector3.Scale(n,radii);normals[i]=new Vector3(n.x/radii.x,n.y/radii.y,n.z/radii.z).normalized;}mesh.vertices=vertices;mesh.normals=normals;return mesh;});
        }
        void ArmorPlate(CandyPlayerRig rig,string id,int bone,Tone tone,float z,float depth,params Vector2[] outline)
        {
            ArmorPart(rig,id,bone,tone,()=>{
                float area=0;Vector2 center=Vector2.zero;for(int i=0;i<outline.Length;i++){var a=outline[i];var b=outline[(i+1)%outline.Length];area+=a.x*b.y-b.x*a.y;center+=a;}center/=outline.Length;if(area<0)System.Array.Reverse(outline);
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var rings=new Vector3[4,outline.Length];float bevel=Mathf.Min(.008f,depth*.22f);
                for(int r=0;r<4;r++)for(int i=0;i<outline.Length;i++){var p=outline[i];if(r==0||r==3)p+=(center-p).normalized*bevel;float d=r==0?-depth*.5f:r==1?-depth*.5f+bevel:r==2?depth*.5f-bevel:depth*.5f;float sculpt=0;rings[r,i]=new Vector3(p.x,p.y,z+d+sculpt);}
                void Tri(Vector3 a,Vector3 b,Vector3 c){int i=vertices.Count;vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.x*3,a.y*3),new Vector2(b.x*3,b.y*3),new Vector2(c.x*3,c.y*3)});triangles.AddRange(new[]{i,i+1,i+2});}
                for(int i=1;i<outline.Length-1;i++){Tri(rings[0,0],rings[0,i+1],rings[0,i]);Tri(rings[3,0],rings[3,i],rings[3,i+1]);}
                for(int r=0;r<3;r++)for(int i=0;i<outline.Length;i++){int j=(i+1)%outline.Length;Tri(rings[r,i],rings[r,j],rings[r+1,j]);Tri(rings[r,i],rings[r+1,j],rings[r+1,i]);}
                var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
            });
        }
        void ArmorRing(CandyPlayerRig rig,string id,int bone,Tone tone,Vector3 p,float radius,float thickness,Quaternion rotation,Vector3 scale)
        {
            ArmorPart(rig,id,bone,tone,()=>{
                const int sides=48,tube=10;var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                for(int i=0;i<=sides;i++)for(int j=0;j<=tube;j++){float a=i*Mathf.PI*2/sides,b=j*Mathf.PI*2/tube;var n=new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(a)*Mathf.Cos(b),Mathf.Sin(b));vertices.Add(p+rotation*Vector3.Scale(new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius+n*thickness,scale));normals.Add(rotation*new Vector3(n.x/scale.x,n.y/scale.y,n.z/scale.z).normalized);uv.Add(new Vector2(i/(float)sides,j/(float)tube));if(i<sides&&j<tube){int v=i*(tube+1)+j,w=v+tube+1;triangles.AddRange(new[]{v,w,v+1,v+1,w,w+1});}}
                return new Mesh{vertices=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),triangles=triangles.ToArray()};
            });
        }
        void BuildArmor(CandyPlayerRig rig)
        {
            ArmorBall(rig,"flexible torso undersuit",0,Tone.Dark,new Vector3(0,1.00f,0),new Vector3(.245f,.35f,.18f));
            ArmorBall(rig,"continuous chest shell",0,Tone.Armor,new Vector3(0,1.16f,0),new Vector3(.245f,.206f,.185f));
            foreach(float side in new[]{-1f,1f})
            {
                ArmorPlate(rig,"sculpted pectoral "+side,0,Tone.Armor,.180f,.055f,new Vector2(side*.020f,1.29f),new Vector2(side*.185f,1.32f),new Vector2(side*.238f,1.23f),new Vector2(side*.185f,1.10f),new Vector2(side*.035f,1.08f));
                foreach(float y in new[]{1.13f,1.27f})ArmorBall(rig,"chest recessed fastener "+side+y,0,Tone.Silver,new Vector3(side*.17f,y,.211f),new Vector3(.005f,.005f,.002f));
                ArmorPlate(rig,"collar inlay "+side,0,Tone.Silver,.166f,.018f,new Vector2(side*.055f,1.33f),new Vector2(side*.17f,1.35f),new Vector2(side*.21f,1.30f),new Vector2(side*.09f,1.29f));
            }
            for(int i=0;i<4;i++)
            {
                float y=.77f+i*.078f,w=.155f+i*.009f;
                ArmorPlate(rig,"overlapping abdominal plate "+i,0,Tone.Armor,.197f,.043f,new Vector2(-w,y+.053f),new Vector2(w,y+.053f),new Vector2(w*.83f,y-.008f),new Vector2(-w*.83f,y-.008f));
                ArmorPlate(rig,"rear spinal plate "+i,0,Tone.Silver,-.166f,.025f,new Vector2(-.075f,y+.040f),new Vector2(.075f,y+.040f),new Vector2(.060f,y-.010f),new Vector2(-.060f,y-.010f));
            }
            ArmorBall(rig,"flexible waist seal",0,Tone.Dark,new Vector3(0,.706f,0),new Vector3(.222f,.115f,.165f));
            ArmorBall(rig,"fitted hip shell",0,Tone.Armor,new Vector3(0,.65f,0),new Vector3(.205f,.115f,.15f));
            ArmorRing(rig,"collar seal",0,Tone.Silver,new Vector3(0,1.34f,0),.127f,.012f,Quaternion.Euler(90,0,0),new Vector3(1,1,1));
            ArmorRing(rig,"reactor machined housing",0,Tone.Dark,new Vector3(0,1.17f,.213f),.066f,.010f,Quaternion.identity,Vector3.one);
            ArmorRing(rig,"reactor polished retaining rim",0,Tone.Silver,new Vector3(0,1.17f,.227f),.055f,.006f,Quaternion.identity,Vector3.one);
            ArmorBall(rig,"chest reactor luminous core",0,Tone.Glow,new Vector3(0,1.17f,.229f),new Vector3(.049f,.049f,.008f));
            ArmorBall(rig,"helmet outer shell",1,Tone.Armor,new Vector3(0,.015f,-.035f),new Vector3(.280f,.275f,.280f));
            ArmorPlate(rig,"angular silver faceplate",1,Tone.Silver,.255f,.080f,new Vector2(-.169f,.11f),new Vector2(-.11f,.185f),new Vector2(.11f,.185f),new Vector2(.169f,.11f),new Vector2(.155f,-.090f),new Vector2(.065f,-.172f),new Vector2(-.065f,-.172f),new Vector2(-.155f,-.090f));
            ArmorPlate(rig,"helmet brow crown",1,Tone.Armor,.283f,.035f,new Vector2(-.055f,.20f),new Vector2(.055f,.20f),new Vector2(.038f,.105f),new Vector2(-.038f,.105f));
            foreach(float side in new[]{-1f,1f})
            {
                ArmorPlate(rig,"eye recess "+side,1,Tone.Dark,.298f,.012f,new Vector2(side*.020f,.057f),new Vector2(side*.149f,.079f),new Vector2(side*.145f,.035f),new Vector2(side*.027f,.030f));
                ArmorPlate(rig,"bright optical slit "+side,1,Tone.Glow,.307f,.006f,new Vector2(side*.032f,.052f),new Vector2(side*.137f,.068f),new Vector2(side*.133f,.043f),new Vector2(side*.037f,.037f));
                ArmorBall(rig,"helmet hinge "+side,1,Tone.Silver,new Vector3(side*.252f,-.01f,-.038f),new Vector3(.018f,.063f,.063f));
            }
            ArmorPlate(rig,"chin armor",1,Tone.Armor,.260f,.035f,new Vector2(-.092f,-.134f),new Vector2(.092f,-.134f),new Vector2(.062f,-.19f),new Vector2(-.062f,-.19f));
            for(int i=0;i<3;i++)ArmorPlate(rig,"helmet breathing slot "+i,1,Tone.Dark,.298f,.004f,new Vector2(-.045f,-.087f-i*.010f),new Vector2(.045f,-.087f-i*.010f),new Vector2(.045f,-.091f-i*.010f),new Vector2(-.045f,-.091f-i*.010f));
            for(int side=0;side<2;side++)
            {
                int upper=side==0?2:5,thigh=side==0?8:11,palm=side==0?4:7,foot=side==0?10:13;
                ArmorBall(rig,"shoulder pauldron "+side,upper,Tone.Armor,new Vector3(0,.025f,0),new Vector3(.133f,.109f,.135f));
                for(int j=0;j<2;j++)
                {
                    ArmorBall(rig,"arm joint undersleeve "+side+j,upper+j,Tone.Dark,new Vector3(0,.143f,0),new Vector3(.080f,.128f,.080f));
                    ArmorBall(rig,"articulated arm plate "+side+j,upper+j,j==0?Tone.Silver:Tone.Armor,new Vector3(0,.143f,.018f),new Vector3(.083f,.108f,.085f));
                    ArmorRing(rig,"arm cuff "+side+j,upper+j,Tone.Silver,new Vector3(0,.210f,0),.070f,.006f,Quaternion.Euler(90,0,0),Vector3.one);
                    ArmorBall(rig,"leg joint undersleeve "+side+j,thigh+j,Tone.Dark,new Vector3(0,.145f,0),new Vector3(.105f,.164f,.100f));
                    ArmorBall(rig,"articulated leg plate "+side+j,thigh+j,j==0?Tone.Silver:Tone.Armor,new Vector3(0,.150f,.015f),new Vector3(.100f,.116f,.099f));
                    ArmorPlate(rig,"shin and thigh facing "+side+j,thigh+j,Tone.Armor,.124f,.025f,new Vector2(-.065f,.246f),new Vector2(.065f,.246f),new Vector2(.076f,.091f),new Vector2(0,.035f),new Vector2(-.076f,.091f));
                    foreach(float x in new[]{-.048f,.048f})ArmorBall(rig,"leg recessed fastener "+side+j+x,thigh+j,Tone.Silver,new Vector3(x,.222f,.138f),new Vector3(.004f,.004f,.002f));
                    for(int rib=0;rib<3;rib++)ArmorRing(rig,"joint rib "+side+j+rib,upper+j,Tone.Dark,new Vector3(0,.018f+rib*.012f,0),.068f,.003f,Quaternion.Euler(90,0,0),Vector3.one);
                }
                ArmorBall(rig,"sealed knee joint "+side,thigh+1,Tone.Dark,Vector3.zero,new Vector3(.111f,.077f,.106f));
                ArmorBall(rig,"knee cup "+side,thigh+1,Tone.Armor,new Vector3(0,.012f,.061f),new Vector3(.084f,.070f,.062f));
                ArmorBall(rig,"armored boot "+side,foot,Tone.Armor,new Vector3(0,0,.050f),new Vector3(.108f,.090f,.17f));
                ArmorBall(rig,"boot grip sole "+side,foot,Tone.Dark,new Vector3(0,-.063f,.052f),new Vector3(.109f,.028f,.166f));
                ArmorBall(rig,"hand shell "+side,palm,Tone.Armor,new Vector3(0,.020f,.030f),new Vector3(.061f,.039f,.080f));
                ArmorBall(rig,"gauntlet palm seal "+side,palm,Tone.Dark,new Vector3(0,-.010f,.035f),new Vector3(.058f,.023f,.082f));
                for(int finger=0;finger<4;finger++)ArmorBall(rig,"knuckle plate "+side+finger,palm,Tone.Silver,new Vector3(-.039f+finger*.026f,.033f,.079f),new Vector3(.012f,.015f,.023f));
                Emitter(rig,"armor palm emitter "+side,palm,new Vector3(0,-.031f,.025f),side==0?0:1);
                Emitter(rig,"armor sole emitter "+side,foot,new Vector3(0,-.085f,.055f),side==0?2:3);
            }
            // Back-mounted cooling and power modules stay tight to the existing torso.
            foreach(float side in new[]{-1f,1f})
            {
                ArmorBall(rig,"back power pod "+side,0,Tone.Armor,new Vector3(side*.135f,1.16f,-.145f),new Vector3(.065f,.15f,.075f));
                for(int i=0;i<5;i++)ArmorPlate(rig,"back cooling louver "+side+i,0,Tone.Dark,-.215f,.013f,new Vector2(side*.135f-.043f,1.09f+i*.027f),new Vector2(side*.135f+.043f,1.09f+i*.027f),new Vector2(side*.135f+.043f,1.098f+i*.027f),new Vector2(side*.135f-.043f,1.098f+i*.027f));
            }
        }

        void Emitter(CandyPlayerRig rig,string id,int bone,Vector3 position,int index)
        {
            ArmorRing(rig,id+" outer machined rim",bone,Tone.Dark,position,.033f,.005f,Quaternion.Euler(90,0,0),Vector3.one);
            ArmorRing(rig,id+" bright nozzle rim",bone,Tone.Silver,position+Vector3.down*.003f,.026f,.003f,Quaternion.Euler(90,0,0),Vector3.one);
            ArmorBall(rig,id+" luminous core",bone,Tone.Glow,position+Vector3.down*.005f,new Vector3(.022f,.004f,.022f));
            var anchor=new GameObject(id+" jet origin").transform;anchor.SetParent(rig.AppearanceBone(bone),false);anchor.localPosition=position;anchor.localRotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
            if(index==0)LeftPalmEmitter=anchor;else if(index==1)RightPalmEmitter=anchor;else if(index==2)LeftSoleEmitter=anchor;else RightSoleEmitter=anchor;
        }
        public void RefreshPalette(CandyPlayerRig rig)
        {
            properties??=new MaterialPropertyBlock();
            var actor=rig.GetComponentInParent<IModeActor>();var rules=actor!=null?MatchRuntime.Current?.RulesFor(actor.gameObject):null;
            bool teamed=actor!=null&&actor.Team>=0&&rules!=null&&(rules.mode==GameModeKind.CaptureFlag||rules.mode==GameModeKind.TeamTickets||rules.mode==GameModeKind.BlockControl);
            Color primary=teamed?actor.Color:appearance.skin>AvatarSkin.Vampire?ExpandedSkinColor(appearance.skin):appearance.skin==AvatarSkin.Snowman||appearance.skin==AvatarSkin.Vampire?Color.white:appearance.skin==AvatarSkin.Frankenstein||appearance.skin==AvatarSkin.Witch?new Color(.34f,.73f,.30f):appearance.skin==AvatarSkin.Penguin||appearance.skin==AvatarSkin.JackOLantern?new Color(.025f,.03f,.05f):rig.BaseAppearanceColor;
            if(primary==lastTeam&&teamed==lastTeamed)return;lastTeam=primary;lastTeamed=teamed;
            rig.ApplyBodyPalette(teamed&&appearance.skin==AvatarSkin.Penguin?primary*.10f:primary);
            foreach(var p in pieces)if(p.renderer){p.renderer.GetPropertyBlock(properties);var color=ColorFor(p.tone,primary,teamed);properties.SetColor("_BaseColor",color);properties.SetFloat("_Metallic",p.armor?p.renderer.sharedMaterial.GetFloat("_Metallic"):p.tone==Tone.Armor?.75f:p.tone==Tone.Silver?.85f:0);properties.SetFloat("_Smoothness",p.armor?p.renderer.sharedMaterial.GetFloat("_Smoothness"):p.tone==Tone.Armor||p.tone==Tone.Silver?.88f:.52f);properties.SetColor("_EmissionColor",p.tone==Tone.Glow?color*2:Color.black);p.renderer.SetPropertyBlock(properties);}
        }
        Color ColorFor(Tone tone,Color primary,bool teamed)
        {
            bool skinTeam=teamed&&appearance.skin!=AvatarSkin.Default;
            switch(tone){case Tone.Green:return skinTeam?primary:new Color(.16f,.55f,.08f);case Tone.Primary:return appearance.skin==AvatarSkin.Penguin&&!teamed?Color.white:primary;
                case Tone.Dark:return teamed?primary*.16f:new Color(.035f,.035f,.05f);
                case Tone.White:return skinTeam?primary:Color.white;
                case Tone.Hair:if(appearance.skin==AvatarSkin.RagDoll)return new Color(.8f,.055f,.02f);if(appearance.skin==AvatarSkin.MrsClaus||appearance.skin==AvatarSkin.Santa)return Color.white;return new Color(.018f,.02f,.027f);
                case Tone.Black:return skinTeam&&(appearance.skin==AvatarSkin.Penguin||appearance.skin==AvatarSkin.Witch||appearance.skin==AvatarSkin.Vampire)?primary*.10f:new Color(.018f,.02f,.027f);
                case Tone.Orange:return skinTeam&&(appearance.skin==AvatarSkin.JackOLantern||appearance.skin==AvatarSkin.Witch)?primary:new Color(1,.30f,.035f);
                case Tone.Yellow:return new Color(1,.77f,.07f);case Tone.Blue:return new Color(.32f,.75f,1);
                case Tone.Purple:return skinTeam&&appearance.skin==AvatarSkin.Witch?primary:new Color(.32f,.11f,.48f);
                case Tone.Red:return skinTeam?primary:new Color(.72f,.025f,.05f);case Tone.Brown:return new Color(.27f,.13f,.055f);
                case Tone.Silver:return new Color(.55f,.64f,.70f);case Tone.Glow:return new Color(.20f,.87f,1);case Tone.Armor:return teamed||appearance.skin>=AvatarSkin.Astronaut&&!appearance.poweredArmor?primary:new Color(.95f,.20f,.035f);default:return primary;}
        }
        public void FirstPerson(CandyPlayerRig rig,bool active)
        {foreach(var p in pieces)if(p.renderer)p.renderer.shadowCastingMode=active&&(p.bone==0||p.bone==1)?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;}
    }
}
