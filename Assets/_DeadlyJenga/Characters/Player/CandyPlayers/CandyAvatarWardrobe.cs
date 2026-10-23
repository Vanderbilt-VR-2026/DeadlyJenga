using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    // Cosmetic shells follow the existing bones. No physics or gameplay components are added.
    public sealed class CandyAvatarWardrobe : MonoBehaviour
    {
        enum Tone { Primary, Dark, White, Black, Orange, Yellow, Blue, Purple, Red, Brown, Silver, Glow, Armor, Hair }
        sealed class Piece { public MeshRenderer renderer; public Tone tone; public int bone; }
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
            BuildSkin(rig);BuildHair(rig);BuildGlasses(rig);BuildClothes(rig);
            if((value.accessories&AvatarAccessory.PumpkinHelmet)!=0)Pumpkin(rig,"accessory pumpkin",Tone.Primary);
            if(value.poweredArmor)BuildArmor(rig);
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
        void Pants(CandyPlayerRig rig,string id,Tone tone)
        {
            Add(rig,id+" waist",0,tone,b=>b.Cone(new Vector3(0,.52f,0),new Vector3(0,.81f,0),.16f,.20f,24));
            for(int s=0;s<2;s++)
            {
                int thigh=s==0?8:11;
                for(int j=0;j<2;j++)
                {
                    bool shin=j==1;
                    Add(rig,id+s+j,thigh+j,tone,b=>{b.Cone(new Vector3(0,-.025f,0),new Vector3(0,.335f,0),shin?.080f:.094f,shin?.068f:.080f,20);b.Sphere(Vector3.zero,Vector3.one*(shin?.080f:.094f));});
                }
                Ball(rig,id+"shoe"+s,thigh+2,tone,new Vector3(0,0,.04f),new Vector3(.105f,.09f,.175f));
            }
        }
        void Pumpkin(CandyPlayerRig rig,string id,Tone tone)
        {
            // Open ribs frame the original face instead of replacing the head with an opaque ball.
            Add(rig,id,1,tone,b=>{for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;if(Mathf.Sin(a)>.72f)continue;b.Sphere(new Vector3(Mathf.Cos(a)*.22f,0,Mathf.Sin(a)*.20f),new Vector3(.095f,.27f,.095f));}b.Ring(new Vector3(0,.19f,0),.19f,.06f,Quaternion.Euler(90,0,0));b.Ring(new Vector3(0,-.19f,0),.19f,.06f,Quaternion.Euler(90,0,0));});
            Add(rig,id+" stalk",1,Tone.Brown,b=>b.Cone(new Vector3(0,.23f,0),new Vector3(.025f,.34f,0),.045f,.027f,9));
            Add(rig,id+" tooth",1,tone,b=>{for(int i=-1;i<=1;i++)b.Box(new Vector3(i*.085f,-.12f,.24f),new Vector3(.04f,.065f,.025f));});
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
        void Jacket(CandyPlayerRig rig,string id,Tone tone)=>Add(rig,id,0,tone,b=>{for(int s=-1;s<=1;s+=2){b.Box(new Vector3(s*.135f,.99f,.025f),new Vector3(.12f,.55f,.25f));b.Wedge(new Vector3(s*.05f,1.22f,.17f),new Vector3(s*.16f,1.21f,.17f),new Vector3(s*.04f,.96f,.18f),new Vector3(s*.09f,1.03f,.18f),.02f);}});
        void Robe(CandyPlayerRig rig,string id,Tone tone)=>Add(rig,id,0,tone,b=>{b.Cone(new Vector3(0,.30f,0),new Vector3(0,.87f,0),.28f,.205f,24);b.Cone(new Vector3(0,.85f,0),new Vector3(0,1.22f,0),.205f,.235f,24);b.Cone(new Vector3(0,1.22f,0),new Vector3(0,1.34f,0),.235f,.15f,24);});
        void BuildHair(CandyPlayerRig rig){if(appearance.hair!=0)Hair(rig,appearance.hair);}
        void Hair(CandyPlayerRig rig,AvatarHair hair)
        {
            Add(rig,"hair "+hair,1,Tone.Hair,b=>{
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
            if(kind==AvatarEyewear.Visor)
            {
                Add(rig,"visor frame",1,Tone.Dark,b=>{for(int i=0;i<12;i++){float a=Mathf.Lerp(-.95f,.95f,i/12f),next=Mathf.Lerp(-.95f,.95f,(i+1)/12f);foreach(float y in new[]{-.045f,.09f})b.Beam(new Vector3(Mathf.Sin(a)*.235f,y,Mathf.Cos(a)*.235f),new Vector3(Mathf.Sin(next)*.235f,y,Mathf.Cos(next)*.235f),.01f);}});
                Add(rig,"panoramic visor lens",1,Tone.Blue,b=>{for(int i=0;i<12;i++){float a=Mathf.Lerp(-.95f,.95f,i/12f),next=Mathf.Lerp(-.95f,.95f,(i+1)/12f);var from=new Vector3(Mathf.Sin(a)*.235f,0,Mathf.Cos(a)*.235f);var to=new Vector3(Mathf.Sin(next)*.235f,0,Mathf.Cos(next)*.235f);b.Wedge(from+Vector3.up*.085f,to+Vector3.up*.085f,from-Vector3.up*.04f,to-Vector3.up*.04f,.008f);}});
                return;
            }
            Add(rig,"eyewear "+kind,1,Tone.Dark,b=>{for(int s=-1;s<=1;s+=2){if(kind==AvatarEyewear.Monocle&&s==-1)continue;var c=new Vector3(s*.10f,.03f,.223f);
                if(kind==AvatarEyewear.Square){b.Box(c+Vector3.up*.055f,new Vector3(.15f,.015f,.025f));b.Box(c-Vector3.up*.055f,new Vector3(.15f,.015f,.025f));for(int edge=-1;edge<=1;edge+=2)b.Box(c+Vector3.right*edge*.07f,new Vector3(.015f,.12f,.025f));}
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
        void BuildArmor(CandyPlayerRig rig)
        {
            Ball(rig,"armor chest",0,Tone.Armor,new Vector3(0,1.14f,0),new Vector3(.245f,.20f,.185f));
            Add(rig,"armor abdomen plates",0,Tone.Armor,b=>{for(int i=0;i<4;i++)b.Box(new Vector3(0,.72f+i*.075f,.13f),new Vector3(.29f+i*.015f,.06f,.075f));});
            Ball(rig,"armor hip shell",0,Tone.Armor,new Vector3(0,.63f,0),new Vector3(.205f,.12f,.15f));
            Ball(rig,"armor helmet",1,Tone.Armor,new Vector3(0,.015f,-.035f),new Vector3(.245f,.25f,.225f));
            Ball(rig,"armor faceplate",1,Tone.Silver,new Vector3(0,.01f,.163f),new Vector3(.194f,.184f,.075f));
            Add(rig,"armor eye slits",1,Tone.Glow,b=>{for(int s=-1;s<=1;s+=2)b.Box(new Vector3(s*.09f,.045f,.232f),new Vector3(.12f,.022f,.016f));});
            Add(rig,"armor chest reactor rim",0,Tone.Silver,b=>b.Ring(new Vector3(0,1.17f,.19f),.068f,.015f,Quaternion.identity));
            Ball(rig,"armor chest reactor",0,Tone.Glow,new Vector3(0,1.17f,.193f),new Vector3(.052f,.052f,.016f));
            for(int s=0;s<2;s++)
            {
                int upper=s==0?2:5,thigh=s==0?8:11,palm=s==0?4:7,foot=s==0?10:13;
                Ball(rig,"armor shoulder "+s,upper,Tone.Armor,Vector3.zero,new Vector3(.125f,.105f,.13f));
                for(int j=0;j<2;j++){Ball(rig,"armor arm "+s+j,upper+j,j==0?Tone.Silver:Tone.Armor,new Vector3(0,.15f,0),new Vector3(.077f,.125f,.078f));Ball(rig,"armor leg "+s+j,thigh+j,j==0?Tone.Silver:Tone.Armor,new Vector3(0,.15f,0),new Vector3(.094f,.135f,.095f));}
                Ball(rig,"armor boot "+s,foot,Tone.Armor,new Vector3(0,0,.045f),new Vector3(.104f,.087f,.168f));
                Ball(rig,"armor gauntlet "+s,palm,Tone.Armor,new Vector3(0,.018f,.025f),new Vector3(.055f,.035f,.075f));
                Emitter(rig,"armor palm emitter "+s,palm,new Vector3(0,-.031f,.025f),s==0?0:1);
                Emitter(rig,"armor sole emitter "+s,foot,new Vector3(0,-.085f,.055f),s==0?2:3);
            }
        }
        void Emitter(CandyPlayerRig rig,string id,int bone,Vector3 position,int index)
        {
            Add(rig,id+" rim",bone,Tone.Silver,b=>b.Ring(position,.032f,.008f,Quaternion.Euler(90,0,0)));
            Ball(rig,id+" core",bone,Tone.Glow,position,new Vector3(.025f,.007f,.025f));
            var anchor=new GameObject(id+" jet origin").transform;anchor.SetParent(rig.AppearanceBone(bone),false);anchor.localPosition=position;anchor.localRotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
            if(index==0)LeftPalmEmitter=anchor;else if(index==1)RightPalmEmitter=anchor;else if(index==2)LeftSoleEmitter=anchor;else RightSoleEmitter=anchor;
        }
        public void RefreshPalette(CandyPlayerRig rig)
        {
            properties??=new MaterialPropertyBlock();
            var actor=rig.GetComponentInParent<IModeActor>();var rules=actor!=null?MatchRuntime.Current?.RulesFor(actor.gameObject):null;
            bool teamed=actor!=null&&actor.Team>=0&&rules!=null&&(rules.mode==GameModeKind.CaptureFlag||rules.mode==GameModeKind.TeamTickets||rules.mode==GameModeKind.BlockControl);
            Color primary=teamed?actor.Color:appearance.skin==AvatarSkin.Snowman||appearance.skin==AvatarSkin.Vampire?Color.white:appearance.skin==AvatarSkin.Frankenstein||appearance.skin==AvatarSkin.Witch?new Color(.34f,.73f,.30f):appearance.skin==AvatarSkin.Penguin||appearance.skin==AvatarSkin.JackOLantern?new Color(.025f,.03f,.05f):rig.BaseAppearanceColor;
            if(primary==lastTeam&&teamed==lastTeamed)return;lastTeam=primary;lastTeamed=teamed;
            rig.ApplyBodyPalette(teamed&&appearance.skin==AvatarSkin.Penguin?primary*.10f:primary);
            foreach(var p in pieces)if(p.renderer){p.renderer.GetPropertyBlock(properties);var color=ColorFor(p.tone,primary,teamed);properties.SetColor("_BaseColor",color);properties.SetFloat("_Metallic",p.tone==Tone.Armor?.75f:p.tone==Tone.Silver?.85f:0);properties.SetFloat("_Smoothness",p.tone==Tone.Armor||p.tone==Tone.Silver?.88f:.52f);properties.SetColor("_EmissionColor",p.tone==Tone.Glow?color*2:Color.black);p.renderer.SetPropertyBlock(properties);}
        }
        Color ColorFor(Tone tone,Color primary,bool teamed)
        {
            bool skinTeam=teamed&&appearance.skin!=AvatarSkin.Default;
            switch(tone){case Tone.Primary:return appearance.skin==AvatarSkin.Penguin&&!teamed?Color.white:primary;
                case Tone.Dark:return teamed?primary*.16f:new Color(.035f,.035f,.05f);
                case Tone.White:return skinTeam?primary:Color.white;
                case Tone.Hair:return new Color(.018f,.02f,.027f);
                case Tone.Black:return skinTeam&&(appearance.skin==AvatarSkin.Penguin||appearance.skin==AvatarSkin.Witch||appearance.skin==AvatarSkin.Vampire)?primary*.10f:new Color(.018f,.02f,.027f);
                case Tone.Orange:return skinTeam&&(appearance.skin==AvatarSkin.JackOLantern||appearance.skin==AvatarSkin.Witch)?primary:new Color(1,.30f,.035f);
                case Tone.Yellow:return new Color(1,.77f,.07f);case Tone.Blue:return new Color(.32f,.75f,1);
                case Tone.Purple:return skinTeam&&appearance.skin==AvatarSkin.Witch?primary:new Color(.32f,.11f,.48f);
                case Tone.Red:return skinTeam?primary:new Color(.72f,.025f,.05f);case Tone.Brown:return new Color(.27f,.13f,.055f);
                case Tone.Silver:return new Color(.55f,.64f,.70f);case Tone.Glow:return new Color(.20f,.87f,1);case Tone.Armor:return teamed?primary:new Color(.95f,.20f,.035f);default:return primary;}
        }
        public void FirstPerson(CandyPlayerRig rig,bool active)
        {foreach(var p in pieces)if(p.renderer)p.renderer.shadowCastingMode=active&&(p.bone==0||p.bone==1)?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;}
    }
}
