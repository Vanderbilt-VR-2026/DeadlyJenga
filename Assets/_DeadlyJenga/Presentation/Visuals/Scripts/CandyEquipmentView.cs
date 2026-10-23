using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyEquipmentView:MonoBehaviour
    {
        sealed class PickupVisual{public Transform root;public TextMesh label;}
        readonly Dictionary<int,PickupVisual> pickups=new Dictionary<int,PickupVisual>();
        readonly HashSet<int> visible=new HashSet<int>();
        Canvas canvas;Text status;Camera eye;bool vr;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyEquipmentView>())go.AddComponent<CandyEquipmentView>();};
        public static string Name(EquipmentKind kind)
        {
            switch(kind){case EquipmentKind.PoweredArmor:return "POWERED IRON SUIT";case EquipmentKind.Speed:return "2x SPEED";case EquipmentKind.Jump:return "3x JUMP";case EquipmentKind.Blaster:return "ALIEN BLASTER";case EquipmentKind.MurderSword:return "MURDERER SWORD";case EquipmentKind.SheriffHammer:return "SHERIFF HAMMER";case EquipmentKind.DualRifle:return "DUAL RIFLE";case EquipmentKind.BoxingGlove:return "GIANT BOXING GLOVE";default:return "EMPTY";}
        }
        void Update()
        {
            var match=MatchRuntime.Current;var session=Services.Session;if(match==null||session==null)return;
            if(!canvas)
            {
                var go=new GameObject("Equipment HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.sortingOrder=35;
                status=CandyHudCards.Create(go.GetComponent<RectTransform>(),"Equipment and swap",new Vector2(0,-220),new Vector2(600,115),19,TextAnchor.MiddleCenter);
            }
            if(eye!=session.ActiveCamera||vr!=session.IsVR){eye=session.ActiveCamera;vr=session.IsVR;CandyHudLayout.Configure(canvas,transform,eye,vr);}
            var controls=session.Player?.gameObject.GetComponent<IEquipmentControls>();var equipment=controls?.Equipment;
            var pending=System.Array.Find(match.Frame.pickups,p=>p.id==controls?.PendingPickup);
            canvas.enabled=eye&&!session.MenuOpen&&!CandyMatchHud.Expanded&&equipment!=null;
            if(pending!=null)status.text="SWAP "+Name(equipment.kind)+" FOR "+Name(pending.kind)+"?\n"+(vr?"Dominant trigger: SWAP  |  Other trigger: KEEP":"E: SWAP  |  N: KEEP")+"\nYour current item drops here.";
            else if(equipment?.kind==EquipmentKind.PoweredArmor)status.text="POWERED IRON SUIT · "+(equipment.drawn?"ACTIVE":"STANDBY")+"\n"+(vr?"Controller trigger: jets · Tracked hand pinch: plasma · Trigger at hip: standby":"V: activate · Hold Space: jets · Click: palm plasma");
            else if(equipment?.kind>=EquipmentKind.Blaster)status.text=Name(equipment.kind)+(equipment.kind==EquipmentKind.DualRifle?(equipment.shotgun?" / SHOTGUN":" / LONG RIFLE"):"")+" · "+(equipment.drawn?"DRAWN":"HOLSTERED")+"\n"+(vr?"Trigger: draw / fire · Trigger at holster: stow":"V: draw / holster · Click: attack")+(equipment.kind==EquipmentKind.DualRifle?"\n"+(equipment.drawn?"Jump, climb and vault locked. ":"")+(vr?"Support grip: aim · Other trigger: change mode":"R: change rifle mode"):"");
            else status.text=equipment==null||equipment.kind==EquipmentKind.None?"":Name(equipment.kind)+" · "+Mathf.CeilToInt(equipment.remaining)+"s";
            CandyHudCards.Visible(status,!string.IsNullOrEmpty(status.text));
            visible.Clear();
            foreach(var pickup in match.Frame.pickups)
            {
                visible.Add(pickup.id);
                if(!pickups.TryGetValue(pickup.id,out var view)){view=CreatePickup(pickup.kind);pickups[pickup.id]=view;}
                view.root.position=pickup.position;view.root.gameObject.SetActive(pickup.availableIn<=0||pickup.station);
                view.label.text=Name(pickup.kind)+(pickup.availableIn>0?"\nREADY IN "+Mathf.CeilToInt(pickup.availableIn)+"s":"\n"+(pickup.station?"TEST STATION":"PICKUP"));
                if(eye)view.label.transform.rotation=Quaternion.LookRotation(view.label.transform.position-eye.transform.position);
            }
            var removed=new List<int>();foreach(var pair in pickups)if(!visible.Contains(pair.Key)){Destroy(pair.Value.root.gameObject);removed.Add(pair.Key);}foreach(int id in removed)pickups.Remove(id);
        }
        PickupVisual CreatePickup(EquipmentKind kind)
        {
            var root=new GameObject("Visual - "+Name(kind)+" pickup").transform;root.SetParent(transform,false);
            var material=Resources.Load<Material>("CandyUtilityGlow");
            var color=Color.HSVToRGB(((int)kind*.13f)%1,.65f,1);
            // This display has no collider; proximity and ownership belong to GameSession.
            var orb=new GameObject("Pickup beacon",typeof(MeshFilter),typeof(MeshRenderer));orb.transform.SetParent(root,false);orb.transform.localScale=Vector3.one*.3f;orb.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");var renderer=orb.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);renderer.SetPropertyBlock(block);
            var label=new GameObject("Pickup name",typeof(TextMesh)).GetComponent<TextMesh>();label.transform.SetParent(root,false);label.transform.localPosition=Vector3.up*.32f;label.anchor=TextAnchor.LowerCenter;label.alignment=TextAlignment.Center;label.fontSize=48;label.characterSize=.028f;label.color=color;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<Renderer>().sharedMaterial=label.font.material;
            return new PickupVisual{root=root,label=label};
        }
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);foreach(var pickup in pickups.Values)if(pickup.root)Destroy(pickup.root.gameObject);}
    }
}
