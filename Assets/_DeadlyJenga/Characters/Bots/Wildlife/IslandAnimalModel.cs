using System.Collections.Generic;
using UnityEngine;
using Builder = DeadlyJenga.Wildlife.AnimalMesh;

namespace DeadlyJenga.Wildlife
{
    // Shared meshes and materials; individual animals only move their small limb pivots.
    public sealed class IslandAnimalModel : MonoBehaviour
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly List<Transform> limbs = new List<Transform>();
        readonly List<Quaternion> rests = new List<Quaternion>();
        Transform tail, leftWing, rightWing;
        public string Species { get; private set; }
        public int PartCount => GetComponentsInChildren<MeshRenderer>().Length;
        static Color Cream => new Color(.98f,.89f,.68f);
        static Color Purple => new Color(.30f,.055f,.48f);
        static Color Teal => new Color(.015f,.65f,.70f);
        static Color Orange => new Color(1,.25f,.035f);
        static Color Gold => new Color(1,.60f,.025f);
        static Color Dark => new Color(.025f,.023f,.045f);

        public void Build(string species)
        {
            Species = species;
            switch(species)
            {
                case "otter": Otter(); break;
                case "fox": Fox(); break;
                case "tortoise": Tortoise(); break;
                case "jaguar": Jaguar(); break;
                case "penguin": Penguin(); break;
                case "ray": Ray(); break;
                case "crab": Crab(); break;
                case "frog": Frog(); break;
                case "boar": Boar(); break;
                case "monkey": Monkey(); break;
                case "seal": Seal(); break;
                case "beetle": Beetle(); break;
            }
        }
        static Material Material(Color color)
        {
            if(materials.TryGetValue(color,out var material)&&material)return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Wildlife glossy plastic",enableInstancing=true};
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.83f);material.SetFloat("_Metallic",.06f);
            materials[color]=material;return material;
        }
        Transform Part(string name, Color color, System.Action<Builder> shape, Vector3 pivot=default, bool moving=false)
        {
            string key=Species+"/"+name;
            if(!meshes.TryGetValue(key,out var mesh)||!mesh)
            {
                var builder=new Builder();shape(builder);mesh=builder.Mesh("Wildlife "+key);meshes[key]=mesh;
            }
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=pivot;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Material(color);
            if(moving){limbs.Add(go.transform);rests.Add(go.transform.localRotation);}
            return go.transform;
        }
        static void Ball(Builder b,float x,float y,float z,float rx,float ry,float rz)=>b.Sphere(new Vector3(x,y,z),new Vector3(rx,ry,rz));
        static void Stroke(Builder b, Vector3[] points, float radius)
        {
            foreach(var p in points)b.Sphere(p,Vector3.one*radius);
            for(int i=1;i<points.Length;i++)b.Beam(points[i-1],points[i],radius);
        }
        void Eyes(float x,float y,float z,float radius=.018f)
        {
            Part("Tiny inset dot eyes",Dark,b=>{Ball(b,-x,y,z,radius,radius,radius*.55f);Ball(b,x,y,z,radius,radius,radius*.55f);});
        }
        void Legs(Color color,float x,float front,float rear,float hip,float length,float width=.09f)
        {
            for(int i=0;i<4;i++)
            {
                float side=i%2==0?-1:1;float z=i<2?front:rear;
                Part("Leg "+i,color,b=>{Ball(b,0,-length*.46f,0,width,length*.55f,width);Ball(b,0,-length+.035f,.035f,width*1.15f,.065f,width*1.4f);},new Vector3(side*x,hip,z),true);
            }
        }
        void Otter()
        {
            Part("Body",Teal,b=>{Ball(b,0,.40f,-.04f,.23f,.36f,.23f);Ball(b,0,.80f,.06f,.235f,.22f,.22f);Ball(b,-.20f,.91f,.025f,.065f,.065f,.055f);Ball(b,.20f,.91f,.025f,.065f,.065f,.055f);});
            Part("Cream belly and muzzle",Cream,b=>{Ball(b,0,.42f,.163f,.155f,.255f,.07f);Ball(b,0,.73f,.245f,.17f,.115f,.07f);});
            Eyes(.105f,.83f,.255f);
            Legs(Teal,.16f,.13f,-.13f,.23f,.16f);
            tail=Part("Paddle tail",Teal,b=>Ball(b,0,0,-.24f,.12f,.08f,.31f),new Vector3(0,.17f,-.19f));
        }
        void Fox()
        {
            Part("Body",Orange,b=>{Ball(b,0,.40f,-.03f,.19f,.23f,.38f);Ball(b,0,.70f,.31f,.225f,.22f,.215f);Ball(b,-.15f,.92f,.29f,.085f,.22f,.065f);Ball(b,.15f,.92f,.29f,.085f,.22f,.065f);});
            Part("Cream muzzle and bib",Cream,b=>{Ball(b,0,.62f,.47f,.14f,.10f,.115f);Ball(b,0,.39f,.34f,.13f,.19f,.10f);});
            Eyes(.105f,.74f,.499f,.016f);Legs(Orange,.13f,.22f,-.27f,.33f,.27f,.065f);
            tail=Part("Big curved tail",Orange,b=>{Ball(b,0,.06f,-.18f,.16f,.17f,.23f);Ball(b,0,.20f,-.37f,.19f,.23f,.20f);},new Vector3(0,.40f,-.32f));
            Part("Cream tail tip",Cream,b=>Ball(b,0,.71f,-.76f,.16f,.17f,.15f)).SetParent(tail,true);
        }
        void Tortoise()
        {
            Part("Smooth shell",Gold,b=>Ball(b,0,.36f,-.04f,.39f,.29f,.43f));
            Part("Shell rim",Gold,b=>Ball(b,0,.18f,-.04f,.405f,.075f,.445f));
            Part("Head",Cream,b=>Ball(b,0,.25f,.44f,.17f,.15f,.20f));
            Eyes(.10f,.29f,.604f,.014f);Legs(Cream,.27f,.22f,-.29f,.20f,.14f,.10f);
        }
        void Jaguar()
        {
            Part("Body",Purple,b=>{Ball(b,0,.46f,-.07f,.23f,.25f,.43f);Ball(b,0,.52f,.25f,.25f,.25f,.22f);Ball(b,0,.65f,.44f,.235f,.205f,.23f);Ball(b,-.16f,.80f,.41f,.075f,.09f,.05f);Ball(b,.16f,.80f,.41f,.075f,.09f,.05f);Ball(b,0,.58f,.62f,.16f,.09f,.085f);});
            Eyes(.115f,.69f,.637f);Legs(Purple,.17f,.28f,-.35f,.40f,.34f,.085f);
            Part("Five bold spots",new Color(.67f,.30f,.85f),b=>{for(int i=0;i<4;i++)Ball(b,(i%2==0?-1:1)*.219f,.51f,-.25f+(i/2)*.28f,.018f,.075f,.08f);Ball(b,0,.701f,-.16f,.08f,.018f,.08f);});
            tail=Part("Curled tail",Purple,b=>Stroke(b,new[]{Vector3.zero,new Vector3(0,.10f,-.24f),new Vector3(0,.35f,-.40f),new Vector3(.04f,.50f,-.32f)},.052f),new Vector3(0,.50f,-.45f));
        }
        void Penguin()
        {
            var blue=new Color(.055f,.075f,.27f);
            Part("Egg body",blue,b=>Ball(b,0,.45f,0,.28f,.43f,.25f));
            Part("White belly",Cream,b=>Ball(b,0,.34f,.205f,.20f,.27f,.065f));
            Part("Beak",Orange,b=>Ball(b,0,.64f,.265f,.10f,.05f,.10f));Eyes(.10f,.70f,.19f,.015f);
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;
                Part("Flipper "+i,blue,b=>Ball(b,side*.035f,-.15f,0,.065f,.20f,.08f),new Vector3(side*.25f,.52f,0),true);
                Part("Foot "+i,Orange,b=>Ball(b,0,0,.04f,.11f,.055f,.15f),new Vector3(side*.14f,.055f,.06f),true);
            }
        }
        void Ray()
        {
            var violet=new Color(.46f,.16f,.84f);
            Part("Body",violet,b=>{Ball(b,0,.34f,0,.24f,.105f,.40f);Ball(b,0,.36f,.30f,.24f,.11f,.18f);});
            leftWing=Part("Left wing",violet,b=>Ball(b,-.32f,0,-.03f,.45f,.043f,.33f),new Vector3(-.12f,.35f,0));
            rightWing=Part("Right wing",violet,b=>Ball(b,.32f,0,-.03f,.45f,.043f,.33f),new Vector3(.12f,.35f,0));
            Part("Soft belly",new Color(.75f,.65f,1),b=>Ball(b,0,.263f,.10f,.20f,.025f,.32f));
            Eyes(.13f,.40f,.440f,.014f);
            tail=Part("Ribbon tail",violet,b=>Stroke(b,new[]{Vector3.zero,new Vector3(0,.02f,-.25f),new Vector3(.13f,.05f,-.47f),new Vector3(.24f,.08f,-.46f)},.028f),new Vector3(0,.33f,-.33f));
        }
        void Crab()
        {
            var red=new Color(.95f,.055f,.055f);
            Part("Shell",red,b=>Ball(b,0,.27f,0,.32f,.19f,.245f));
            Part("Eye stalks",red,b=>{Ball(b,-.12f,.46f,.12f,.043f,.085f,.045f);Ball(b,.12f,.46f,.12f,.043f,.085f,.045f);});Eyes(.12f,.49f,.162f,.018f);
            for(int i=0;i<6;i++)
            {
                int side=i%2==0?-1:1;float z=-.16f+(i/2)*.16f;
                Part("Crab leg "+i,red,b=>Stroke(b,new[]{Vector3.zero,new Vector3(side*.16f,-.02f,0),new Vector3(side*.22f,-.15f,.03f)},.035f),new Vector3(side*.25f,.19f,z),true);
            }
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;
                Part("Pincer "+i,red,b=>{Ball(b,side*.07f,0,.13f,.11f,.095f,.14f);Ball(b,side*.12f,.015f,.28f,.075f,.07f,.11f);Ball(b,side*-.015f,.015f,.27f,.055f,.06f,.085f);},new Vector3(side*.34f,.23f,.20f),true);
            }
        }
        void Frog()
        {
            var green=new Color(.03f,.65f,.10f);
            Part("Body",green,b=>{Ball(b,0,.28f,-.08f,.28f,.22f,.28f);Ball(b,0,.40f,.16f,.29f,.18f,.24f);Ball(b,-.17f,.53f,.16f,.09f,.08f,.09f);Ball(b,.17f,.53f,.16f,.09f,.08f,.09f);});
            Part("Belly",new Color(.53f,.82f,.16f),b=>Ball(b,0,.25f,.17f,.20f,.14f,.12f));Eyes(.17f,.55f,.237f,.025f);
            Legs(green,.23f,.21f,-.19f,.18f,.12f,.065f);
            Part("Folded spring thighs",green,b=>{Ball(b,-.28f,.21f,-.17f,.15f,.16f,.20f);Ball(b,.28f,.21f,-.17f,.15f,.16f,.20f);});
        }
        void Boar()
        {
            var plum=new Color(.38f,.08f,.26f);
            Part("Body",plum,b=>{Ball(b,0,.37f,-.08f,.31f,.31f,.39f);Ball(b,0,.40f,.29f,.25f,.23f,.23f);Ball(b,-.17f,.62f,.28f,.07f,.10f,.045f);Ball(b,.17f,.62f,.28f,.07f,.10f,.045f);});
            Part("Snout",new Color(.64f,.25f,.45f),b=>Ball(b,0,.35f,.52f,.17f,.11f,.09f));
            Part("Tusks",Cream,b=>{for(int s=-1;s<=1;s+=2)b.Cone(new Vector3(s*.17f,.30f,.44f),new Vector3(s*.22f,.44f,.54f),.04f,.008f,12);});
            Eyes(.13f,.48f,.472f,.018f);Legs(plum,.21f,.20f,-.32f,.24f,.18f,.08f);
        }
        void Monkey()
        {
            Part("Body and round head",Orange,b=>{Ball(b,0,.40f,-.05f,.19f,.24f,.28f);Ball(b,0,.70f,.26f,.23f,.22f,.20f);Ball(b,-.23f,.70f,.25f,.09f,.10f,.055f);Ball(b,.23f,.70f,.25f,.09f,.10f,.055f);});
            Part("Simple face",Cream,b=>Ball(b,0,.69f,.429f,.17f,.17f,.055f));Eyes(.075f,.73f,.48f,.014f);
            Legs(Orange,.15f,.22f,-.26f,.35f,.29f,.065f);
            tail=Part("Loop tail",Orange,b=>Stroke(b,new[]{Vector3.zero,new Vector3(0,.15f,-.20f),new Vector3(0,.42f,-.24f),new Vector3(0,.62f,-.14f),new Vector3(0,.61f,.01f),new Vector3(0,.49f,.02f)},.043f),new Vector3(0,.47f,-.30f));
        }
        void Seal()
        {
            Part("Smooth body",Teal,b=>{Ball(b,0,.22f,-.08f,.25f,.20f,.48f);Ball(b,0,.40f,.24f,.22f,.31f,.22f);Ball(b,0,.65f,.30f,.20f,.18f,.20f);});
            Eyes(.10f,.69f,.464f,.015f);
            for(int i=0;i<2;i++){int side=i==0?-1:1;Part("Flipper "+i,Teal,b=>Ball(b,side*.09f,0,0,.19f,.04f,.12f),new Vector3(side*.20f,.09f,.15f),true);}
            tail=Part("Tail flukes",Teal,b=>{Ball(b,-.10f,0,-.10f,.12f,.045f,.17f);Ball(b,.10f,0,-.10f,.12f,.045f,.17f);},new Vector3(0,.11f,-.45f));
        }
        void Beetle()
        {
            Part("Shell",Gold,b=>{Ball(b,-.115f,.29f,-.09f,.19f,.22f,.31f);Ball(b,.115f,.29f,-.09f,.19f,.22f,.31f);});
            Part("Head",Dark,b=>Ball(b,0,.21f,.25f,.18f,.14f,.18f));Eyes(.08f,.24f,.415f,.016f);
            for(int i=0;i<6;i++){int side=i%2==0?-1:1;float z=-.24f+i/2*.21f;Part("Leg "+i,Dark,b=>Stroke(b,new[]{Vector3.zero,new Vector3(side*.12f,0,.015f),new Vector3(side*.19f,-.12f,.03f)},.035f),new Vector3(side*.23f,.15f,z),true);}
            Part("Antennae",Dark,b=>{for(int s=-1;s<=1;s+=2){b.Beam(new Vector3(s*.08f,.29f,.30f),new Vector3(s*.15f,.45f,.39f),.019f);Ball(b,s*.15f,.45f,.39f,.035f,.035f,.035f);}});
        }
        public void Animate(float time,float motion)
        {
            float pace=Species=="tortoise"?2:Species=="seal"?4:7;
            for(int i=0;i<limbs.Count;i++)limbs[i].localRotation=rests[i]*Quaternion.Euler(Mathf.Sin(time*pace+(i%2)*Mathf.PI+(i/2)*Mathf.PI)*motion*19,0,0);
            if(tail)tail.localRotation=Quaternion.Euler(0,Mathf.Sin(time*2.2f)*12,0);
            if(leftWing)leftWing.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*2.5f)*17);
            if(rightWing)rightWing.localRotation=Quaternion.Euler(0,0,-Mathf.Sin(time*2.5f)*17);
        }
    }
}
