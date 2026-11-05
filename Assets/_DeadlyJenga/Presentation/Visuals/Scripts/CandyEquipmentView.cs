using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyEquipmentView:MonoBehaviour
    {
        sealed class PickupVisual{public Transform root;public TextMesh label;public EquipmentKind kind;public Transform model;}
        readonly Dictionary<int,PickupVisual> pickups=new Dictionary<int,PickupVisual>();
        readonly HashSet<int> visible=new HashSet<int>();
        Canvas canvas;Text status;RawImage weaponOutline;Camera eye;bool vr;
        static readonly Dictionary<EquipmentKind,Texture2D> outlines=new();
        public static Texture2D Outline(EquipmentKind kind)
        {
            if(outlines.TryGetValue(kind,out var cached))return cached;
            if(!EquipmentModelData.Parts.TryGetValue(kind,out var parts))return null;
            const int width=100,height=54;
            var vertices=new List<Vector2>();var indices=new List<int>();
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=-min;
            foreach(var part in parts)
            {
                if(!part.mesh||!part.mesh.isReadable)continue;
                int start=vertices.Count;
                foreach(var vertex in part.mesh.vertices)
                {
                    var point=part.local.MultiplyPoint3x4(vertex);
                    var projected=new Vector2(point.z+point.x*.28f,point.y+point.x*.12f);
                    vertices.Add(projected);min=Vector2.Min(min,projected);max=Vector2.Max(max,projected);
                }
                foreach(int index in part.mesh.triangles)indices.Add(index+start);
            }
            if(vertices.Count==0)return null;
            var size=max-min;
            float scale=Mathf.Min((width-6)/Mathf.Max(.01f,size.x),(height-6)/Mathf.Max(.01f,size.y));
            var center=(min+max)*.5f;
            for(int i=0;i<vertices.Count;i++)vertices[i]=(vertices[i]-center)*scale+new Vector2(width,height)*.5f;
            var mask=new bool[width*height];
            for(int i=0;i+2<indices.Count;i+=3)
            {
                Vector2 a=vertices[indices[i]],b=vertices[indices[i+1]],c=vertices[indices[i+2]];
                float winding=Edge(a,b,c);
                if(Mathf.Abs(winding)<.0001f)continue;
                for(int y=Mathf.Max(1,Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y)));y<=Mathf.Min(height-2,Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y)));y++)
                for(int x=Mathf.Max(1,Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)));x<=Mathf.Min(width-2,Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)));x++)
                {
                    var point=new Vector2(x+.5f,y+.5f);
                    if(Edge(a,b,point)*winding>=0&&Edge(b,c,point)*winding>=0&&Edge(c,a,point)*winding>=0)
                        mask[y*width+x]=true;
                }
            }
            var pixels=new Color32[mask.Length];
            for(int y=1;y<height-1;y++)
            for(int x=1;x<width-1;x++)
            {
                int i=y*width+x;
                if(!mask[i])continue;
                bool edge=!mask[i-1]||!mask[i+1]||!mask[i-width]||!mask[i+width];
                pixels[i]=new Color32(255,255,255,edge?(byte)255:(byte)70);
            }
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=kind+" outline",hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels32(pixels);texture.Apply(false,true);outlines[kind]=texture;
            return texture;
        }
        static float Edge(Vector2 a,Vector2 b,Vector2 p)=>(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyEquipmentView>())go.AddComponent<CandyEquipmentView>();};
        public static string Name(EquipmentKind kind)
        {
            return EquipmentSpecifications.Get(kind).name;
        }
        void Update()
        {
            var match=MatchRuntime.Current;var session=Services.Session;if(match==null||session==null)return;
            if(!canvas)
            {
                var go=new GameObject("Equipment HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.sortingOrder=35;
                status=CandyHudCards.Create(go.GetComponent<RectTransform>(),"Equipment and swap",new Vector2(272,-217),new Vector2(360,75),16,TextAnchor.MiddleCenter);
                weaponOutline=new GameObject("Weapon outline",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
                weaponOutline.transform.SetParent(status.transform.parent,false);
                weaponOutline.rectTransform.anchoredPosition=new Vector2(-140,0);
                weaponOutline.rectTransform.sizeDelta=new Vector2(58,36);
                weaponOutline.color=CandyHudCards.Accent;
                weaponOutline.raycastTarget=false;
            }
            if(eye!=session.ActiveCamera||vr!=session.IsVR){eye=session.ActiveCamera;vr=session.IsVR;CandyHudLayout.Configure(canvas,transform,eye,vr);}
            var controls=session.Player?.gameObject.GetComponent<IEquipmentControls>();var equipment=controls?.Equipment;
            var pending=System.Array.Find(match.Frame.pickups,p=>p.id==controls?.PendingPickup);
            canvas.enabled=eye&&!session.MenuOpen&&!CandyMatchHud.Expanded&&equipment!=null;
            if(pending!=null)status.text="SWAP "+Name(equipment.activePower!=EquipmentKind.None?equipment.activePower:equipment.kind)+" FOR "+Name(pending.kind)+"?\n"+(vr?"Dominant trigger: SWAP  |  Other trigger: KEEP":"E: SWAP  |  N: KEEP")+"\nYour current item drops here.";
            else if(equipment?.kind==EquipmentKind.PoweredArmor)status.text="POWERED IRON SUIT · "+(equipment.drawn?"HOVERING":"STANDBY")+"\n"+(vr?"Move stick · Trigger rise · Grip descend\nB / Y land · Other trigger or pinch plasma":"V: take off / land · WASD: steer · E: up · Q: down · Click: plasma");
            else if(equipment?.kind>=EquipmentKind.Blaster)
            {
                var spec=EquipmentSpecifications.Get(equipment.kind);
                string mode=equipment.kind switch
                {
                    EquipmentKind.DualRifle=>equipment.shotgun?" / Shotgun":" / Rifle",
                    EquipmentKind.SheriffHammer=>equipment.shotgun?" / Scatter":" / Precision",
                    EquipmentKind.StockPistol=>equipment.shotgun?" / Burst":" / Auto",
                    EquipmentKind.Chainsaw=>equipment.shotgun?" / Launch":" / Saw",
                    EquipmentKind.LaserAxe=>equipment.shotgun?" / Laser":" / Axe",
                    _=>""
                };
                string ammo=spec.magazine>0?(equipment.reloadRemaining>0?"   Reloading":"   "+equipment.rounds+"/"+spec.magazine):"";
                string toggle=vr?(equipment.leftHanded?"Y":"B"):"V";
                string controlsHint=equipment.drawn
                    ?(vr?(spec.melee?"Swing":"Trigger fire"):"Click attack")+"   "+toggle+" holster"
                    :toggle+" draw";
                if(equipment.drawn&&spec.modeSwitch)controlsHint+=vr?"   Off trigger / stick click mode":"   R mode";
                if(vr&&equipment.drawn&&(equipment.kind==EquipmentKind.Blaster||equipment.kind==EquipmentKind.DualRifle||equipment.kind==EquipmentKind.SheriffHammer))
                    controlsHint+="   Grip aim";
                status.text=Name(equipment.kind)+mode+ammo+"\n"+controlsHint;
            }
            else status.text=equipment==null||equipment.kind==EquipmentKind.None?"":Name(equipment.kind)+"   "+Mathf.CeilToInt(equipment.remaining)+"s";
            weaponOutline.texture=pending==null&&equipment!=null?Outline(equipment.kind):null;
            weaponOutline.enabled=weaponOutline.texture;
            status.rectTransform.offsetMin=new Vector2(weaponOutline.enabled?66:10,4);
            CandyHudCards.Visible(status,!string.IsNullOrEmpty(status.text));
            visible.Clear();
            foreach(var pickup in match.Frame.pickups)
            {
                visible.Add(pickup.id);
                if(!pickups.TryGetValue(pickup.id,out var view)){view=CreatePickup(pickup.kind);pickups[pickup.id]=view;}
                UpdatePickupModel(view);view.root.position=pickup.position;view.root.gameObject.SetActive(pickup.availableIn<=0||pickup.station);
                view.label.text=Name(pickup.kind)+(pickup.availableIn>0?"\nREADY IN "+Mathf.CeilToInt(pickup.availableIn)+"s":"\n"+(pickup.station?"TEST STATION":"PICKUP"));
                if(eye)view.label.transform.rotation=Quaternion.LookRotation(view.label.transform.position-eye.transform.position);
            }
            var removed=new List<int>();foreach(var pair in pickups)if(!visible.Contains(pair.Key)){Destroy(pair.Value.root.gameObject);removed.Add(pair.Key);}foreach(int id in removed)pickups.Remove(id);
        }
        PickupVisual CreatePickup(EquipmentKind kind)
        {
            var root=new GameObject("Visual - "+Name(kind)+" pickup").transform;root.SetParent(transform,false);
            var color=kind==EquipmentKind.Speed?new Color(.05f,.5f,1):kind==EquipmentKind.Jump?new Color(.10f,1,.16f):new Color(.5f,.85f,1);
            BuildPickupCloud(root,color);
            var label=new GameObject("Pickup name",typeof(TextMesh)).GetComponent<TextMesh>();label.transform.SetParent(root,false);label.transform.localPosition=Vector3.up*.42f;label.anchor=TextAnchor.LowerCenter;label.alignment=TextAlignment.Center;label.fontSize=48;label.characterSize=.009f;label.color=color;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<Renderer>().sharedMaterial=label.font.material;
            return new PickupVisual{root=root,label=label,kind=kind};
        }
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);foreach(var pickup in pickups.Values)if(pickup.root)Destroy(pickup.root.gameObject);}
    }
}
