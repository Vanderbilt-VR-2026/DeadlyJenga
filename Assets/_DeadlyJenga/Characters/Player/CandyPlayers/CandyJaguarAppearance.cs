using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyPlayers
{
    // A coat and sculpted additions on the original avatar. Bones, tracking and colliders stay intact.
    [DefaultExecutionOrder(290)]
    public sealed class CandyJaguarAppearance : MonoBehaviour
    {
        static Material fur, cream, dark, eye, ivory;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        readonly List<MeshRenderer> pieces = new List<MeshRenderer>();
        readonly List<MeshRenderer> concealed = new List<MeshRenderer>();
        CandyPlayerRig rig;
        Transform tail;
        AvatarAccessory applied;
        Vector3 previous;
        float gait,quadruped;
        Material originalBody, originalHead;
        MaterialPropertyBlock pigment;
        public int PieceCount => pieces.Count;
        public static Texture2D CoatTexture => FurTexture();
        static Texture2D coat;

        public void Apply(CandyPlayerRig target, AvatarAccessory accessories)
        {
            rig = target;
            const AvatarAccessory feline = AvatarAccessory.PantherHead | AvatarAccessory.PantherClaws | AvatarAccessory.PantherTail;
            var requested = accessories & feline;
            if (requested == applied && pieces.Count > 0) return;
            Clear(); applied = requested;
            if (requested == 0) { enabled = false; return; }
            enabled = true; EnsureMaterials(rig.bodyRenderer.sharedMaterial);
            foreach(var mesh in new[]{rig.bodyRenderer.sharedMesh,rig.headRenderer.sharedMesh})if(mesh&&mesh.isReadable&&mesh.tangents.Length==0)mesh.RecalculateTangents();
            originalBody = rig.bodyRenderer.sharedMaterial; originalHead = rig.headRenderer.sharedMaterial;
            if ((requested & AvatarAccessory.PantherHead) != 0) Head();
            if ((requested & AvatarAccessory.PantherClaws) != 0) Paws();
            if ((requested & AvatarAccessory.PantherTail) != 0)
            {
                var anchor = new GameObject("Visual - jaguar tail pivot").transform;
                anchor.SetParent(rig.BodyAnchor, false); anchor.localPosition = new Vector3(0,.68f,-.13f); tail = anchor;
                Add("spotted tail", tail, fur, b => {
                    Vector3 a = Vector3.zero;
                    for (int i=1;i<=9;i++) { float t=i/9f;var p=new Vector3(.07f*Mathf.Sin(t*3),-.28f*Mathf.Sin(t*2.5f),-.86f*t);b.Cone(a,p,Mathf.Lerp(.055f,.025f,(i-1)/9f),Mathf.Lerp(.055f,.025f,t),12);b.Sphere(p,Vector3.one*Mathf.Lerp(.055f,.025f,t));a=p; }
                });
                Add("tail black tip", tail, dark, b => b.Sphere(new Vector3(.07f*Mathf.Sin(3),-.28f*Mathf.Sin(2.5f),-.86f),new Vector3(.03f,.035f,.075f)));
            }
            previous=transform.position;
            Refresh();
        }
        void Head()
        {
            var head=rig.HeadAnchor;
            Add("broad jaguar cheeks and ears",head,fur,b=>{
                b.Sphere(new Vector3(0,.025f,-.008f),new Vector3(.24f,.225f,.215f));
                for(int s=-1;s<=1;s+=2){b.Sphere(new Vector3(s*.19f,.205f,-.025f),new Vector3(.09f,.095f,.055f));b.Sphere(new Vector3(s*.155f,-.045f,.13f),new Vector3(.105f,.105f,.135f));b.Sphere(new Vector3(s*.095f,.065f,.18f),new Vector3(.10f,.045f,.055f));}
            });
            Add("cream muzzle and chin",head,cream,b=>{
                for(int s=-1;s<=1;s+=2)b.Sphere(new Vector3(s*.072f,-.062f,.23f),new Vector3(.09f,.068f,.093f));
                b.Sphere(new Vector3(0,-.132f,.19f),new Vector3(.13f,.045f,.09f));
            });
            Add("ear interiors nose lip and whisker pores",head,dark,b=>{
                for(int s=-1;s<=1;s+=2){b.Sphere(new Vector3(s*.19f,.213f,.022f),new Vector3(.056f,.06f,.015f));b.Sphere(new Vector3(s*.095f,.034f,.207f),new Vector3(.059f,.039f,.027f));
                    for(int i=0;i<5;i++)b.Sphere(new Vector3(s*(.055f+(i%3)*.029f),-.052f-(i/3)*.026f,.307f-(i%3)*.009f),Vector3.one*.005f);
                    b.Beam(new Vector3(s*.012f,-.095f,.311f),new Vector3(s*.075f,-.113f,.292f),.004f);
                }
                b.Sphere(new Vector3(0,-.029f,.319f),new Vector3(.047f,.028f,.027f));b.Beam(new Vector3(0,-.035f,.337f),new Vector3(0,-.097f,.315f),.004f);
            });
            Add("amber eyes",head,eye,b=>{for(int s=-1;s<=1;s+=2)b.Sphere(new Vector3(s*.101f,.039f,.229f),new Vector3(.035f,.024f,.012f));});
            Add("round feline pupils",head,dark,b=>{for(int s=-1;s<=1;s+=2)b.Sphere(new Vector3(s*.101f,.039f,.241f),new Vector3(.013f,.020f,.006f));});
            Add("eye catchlights and small fangs",head,ivory,b=>{for(int s=-1;s<=1;s+=2){b.Sphere(new Vector3(s*.096f,.048f,.246f),Vector3.one*.006f);b.Cone(new Vector3(s*.077f,-.102f,.278f),new Vector3(s*.073f,-.144f,.283f),.012f,0,10);}});
            Add("cream chest bib",rig.BodyAnchor,cream,b=>{b.Sphere(new Vector3(0,1.12f,.113f),new Vector3(.17f,.19f,.075f));b.Sphere(new Vector3(0,.86f,.125f),new Vector3(.13f,.20f,.055f));});
            for(int side=0;side<2;side++)
            {
                Add("powerful shoulder "+side,rig.AppearanceBone(side==0?2:5),fur,b=>b.Sphere(new Vector3(0,.075f,0),new Vector3(.085f,.13f,.085f)));
                Add("haunch "+side,rig.AppearanceBone(side==0?8:11),fur,b=>b.Sphere(new Vector3(0,.085f,0),new Vector3(.098f,.16f,.09f)));
            }
        }
        void Paws()
        {
            for(int side=0;side<2;side++)
            {
                var hand=side==0?rig.LeftPalm:rig.RightPalm;
                Add("paw back "+side,hand,fur,b=>b.Sphere(new Vector3(0,-.004f,.032f),new Vector3(.075f,.047f,.094f)));
                Add("paw pads "+side,hand,dark,b=>{b.Sphere(new Vector3(0,-.049f,.024f),new Vector3(.035f,.008f,.032f));for(int i=0;i<4;i++)b.Sphere(new Vector3((i-1.5f)*.032f,-.042f,.093f),new Vector3(.012f,.012f,.018f));});
                Add("curved ivory claws "+side,hand,ivory,b=>{for(int i=0;i<4;i++){float x=(i-1.5f)*.034f;var p=new Vector3(x,-.013f,.105f);var q=p+new Vector3(0,-.018f,.055f);b.Cone(p,q,.012f,.007f,10);b.Cone(q,q+new Vector3(0,-.03f,.026f),.007f,0,10);}});
                var foot=rig.AppearanceBone(side==0?10:13);
                Add("hind paw "+side,foot,fur,b=>{b.Sphere(new Vector3(0,0,.055f),new Vector3(.103f,.082f,.175f));for(int i=0;i<3;i++)b.Sphere(new Vector3((i-1)*.052f,-.015f,.165f),new Vector3(.042f,.047f,.065f));});
                Add("hind claws "+side,foot,ivory,b=>{for(int i=0;i<3;i++)b.Cone(new Vector3((i-1)*.052f,-.018f,.208f),new Vector3((i-1)*.052f,-.032f,.255f),.012f,0,10);});
            }
        }
        void Add(string id,Transform parent,Material material,System.Action<CandyAccessoryMeshes.Builder> build)
        {
            if(!meshes.TryGetValue(id,out var mesh)||!mesh)
            {
                var builder=new CandyAccessoryMeshes.Builder();build(builder);mesh=builder.Mesh("Jaguar "+id);
                var vertices=mesh.vertices;var uv=new Vector2[vertices.Length];
                for(int i=0;i<uv.Length;i++){var p=vertices[i];uv[i]=new Vector2((p.x+p.z*.47f)*3,(p.y+p.z*.63f)*3);}
                mesh.uv=uv;mesh.RecalculateTangents();meshes[id]=mesh;
            }
            var go=new GameObject("Visual - jaguar "+id);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;pieces.Add(renderer);
            if(parent==rig.HeadAnchor||parent==rig.BodyAnchor||parent==tail)concealed.Add(renderer);
        }
        public void FirstPerson(bool active){foreach(var r in concealed)if(r)r.shadowCastingMode=active?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;}
        void LateUpdate()
        {
            if(!rig)return;Refresh();
            float speed=Vector3.Distance(transform.position,previous)/Mathf.Max(.001f,Time.deltaTime);previous=transform.position;gait+=Time.deltaTime*(1.8f+Mathf.Min(speed,6)*1.3f);
            var actor=rig.GetComponentInParent<IEnemyActor>();var movement=rig.GetComponentInParent<IMovementState>();
            if(actor?.IsEnemy==true){bool upright=rig.GetComponentInParent<IEnemyPose>()?.PreparingMelee==true||movement?.Climbing==true;quadruped=Mathf.MoveTowards(quadruped,upright?0:1,Time.deltaTime*5);rig.PoseQuadruped(quadruped,gait*2,movement?.Grounded??true);}
            if(tail)tail.localRotation=Quaternion.Euler(Mathf.Sin(gait*.6f)*5,Mathf.Sin(gait)*18,Mathf.Sin(gait*.7f)*6);
        }
        void Refresh()
        {
            if((applied&AvatarAccessory.PantherHead)==0)return;
            pigment??=new MaterialPropertyBlock();
            RefreshCoat(rig.bodyRenderer);RefreshCoat(rig.headRenderer);
        }
        void RefreshCoat(SkinnedMeshRenderer renderer)
        {
            // Preserve the health shader and unrelated per-renderer properties.
            if(renderer.sharedMaterial!=fur&&(!renderer.sharedMaterial||!renderer.sharedMaterial.HasProperty("_Health")))renderer.sharedMaterial=fur;
            renderer.GetPropertyBlock(pigment);
            var normal=fur.GetTexture("_BumpMap");
            if(pigment.GetColor("_BaseColor")==Color.white&&pigment.GetTexture("_BaseMap")==FurTexture()&&pigment.GetTexture("_BumpMap")==normal&&pigment.GetFloat("_BumpScale")==.35f&&pigment.GetFloat("_Smoothness")==.24f)return;
            pigment.SetColor("_BaseColor",Color.white);pigment.SetTexture("_BaseMap",FurTexture());
            pigment.SetTexture("_BumpMap",normal);pigment.SetFloat("_BumpScale",.35f);pigment.SetFloat("_Smoothness",.24f);renderer.SetPropertyBlock(pigment);
        }
        void Clear()
        {
            foreach(var r in pieces)if(r){r.gameObject.SetActive(false);Destroy(r.gameObject);}pieces.Clear();concealed.Clear();
            if(tail){tail.gameObject.SetActive(false);Destroy(tail.gameObject);}tail=null;
            if(rig&&originalBody){rig.bodyRenderer.sharedMaterial=originalBody;rig.headRenderer.sharedMaterial=originalHead;}
        }
        static void EnsureMaterials(Material source)
        {
            if(fur)return;
            Material Make(string name,Color color,float gloss){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name="Jaguar - "+name;m.enableInstancing=true;m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",gloss);return m;}
            var authored=Resources.Load<Material>("JaguarFur");
            fur=authored?new Material(authored){name="Jaguar - golden rosette fur",enableInstancing=true}:Make("golden rosette fur",Color.white,.24f);
            fur.SetTexture("_BaseMap",FurTexture());fur.SetTextureScale("_BaseMap",new Vector2(1,1));
            var normal=Resources.Load<Texture2D>("JaguarFurNormal");
            if(normal){fur.SetTexture("_BumpMap",normal);fur.SetFloat("_BumpScale",.35f);fur.EnableKeyword("_NORMALMAP");}
            cream=Make("warm ivory underside",new Color(.94f,.83f,.60f),.28f);dark=Make("charcoal markings",new Color(.022f,.017f,.012f),.32f);
            eye=Make("amber iris",new Color(.96f,.57f,.07f),.88f);ivory=Make("polished claws",new Color(.95f,.92f,.76f),.65f);
        }
        static Texture2D FurTexture()
        {
            if(coat)return coat;
            coat=Resources.Load<Texture2D>("JaguarRosetteFur");if(coat)return coat;
            const int size=512,cells=8;var pixels=new Color32[size*size];
            var random=new System.Random(672153);var center=new Vector2[cells*cells];var radii=new Vector2[center.Length];var angles=new float[center.Length];
            for(int i=0;i<center.Length;i++){center[i]=new Vector2((i%cells+.5f+(float)(random.NextDouble()-.5)*.28f)/cells,(i/cells+.5f+(float)(random.NextDouble()-.5)*.28f)/cells);radii[i]=new Vector2(.035f+(float)random.NextDouble()*.013f,.029f+(float)random.NextDouble()*.012f);angles[i]=(float)random.NextDouble()*6.28f;}
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;int cx=x*cells/size,cy=y*cells/size;float mark=0,inside=0;
                for(int oy=-1;oy<=1;oy++)for(int ox=-1;ox<=1;ox++)
                {
                    int ix=(cx+ox+cells)%cells,iy=(cy+oy+cells)%cells,index=iy*cells+ix;var p=new Vector2(u,v)-center[index];p.x-=Mathf.Round(p.x);p.y-=Mathf.Round(p.y);
                    float a=Mathf.Atan2(p.y,p.x)+angles[index],r=new Vector2(p.x/radii[index].x,p.y/radii[index].y).magnitude;
                    float scallop=1+.12f*Mathf.Sin(a*5+angles[index]);float ring=Mathf.Abs(r-scallop);
                    // Broken scalloped rings with the jaguar's diagnostic central spot, rather than leopard dots.
                    float broken=Mathf.Sin(a*3+index)>-.73f?1:.28f;
                    mark=Mathf.Max(mark,(1-Edge(.14f,.26f,ring))*broken);
                    mark=Mathf.Max(mark,1-Edge(.10f,.23f,r));inside=Mathf.Max(inside,1-Edge(.7f,1,r));
                }
                float grain=(float)random.NextDouble()*.09f;float strand=.025f*Mathf.Sin(x*1.7f+y*.25f);
                var baseColor=Color.Lerp(new Color(.80f,.49f,.16f),new Color(.62f,.33f,.09f),inside*.6f);baseColor*=.94f+grain+strand;
                pixels[y*size+x]=Color.Lerp(baseColor,new Color(.032f,.023f,.014f),mark);
            }
            coat=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Original jaguar rosettes and fine fur",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=2};coat.SetPixels32(pixels);coat.Apply(true,true);return coat;
        }
        static float Edge(float start,float end,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,value));
    }
}
