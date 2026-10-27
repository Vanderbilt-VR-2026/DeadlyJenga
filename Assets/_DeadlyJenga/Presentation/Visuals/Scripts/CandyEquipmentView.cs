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
        Canvas canvas;Text status;Camera eye;bool vr;
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
                status=CandyHudCards.Create(go.GetComponent<RectTransform>(),"Equipment and swap",new Vector2(0,-220),new Vector2(600,115),19,TextAnchor.MiddleCenter);
            }
            if(eye!=session.ActiveCamera||vr!=session.IsVR){eye=session.ActiveCamera;vr=session.IsVR;CandyHudLayout.Configure(canvas,transform,eye,vr);}
            var controls=session.Player?.gameObject.GetComponent<IEquipmentControls>();var equipment=controls?.Equipment;
            var pending=System.Array.Find(match.Frame.pickups,p=>p.id==controls?.PendingPickup);
            canvas.enabled=eye&&!session.MenuOpen&&!CandyMatchHud.Expanded&&equipment!=null;
            if(pending!=null)status.text="SWAP "+Name(equipment.activePower!=EquipmentKind.None?equipment.activePower:equipment.kind)+" FOR "+Name(pending.kind)+"?\n"+(vr?"Dominant trigger: SWAP  |  Other trigger: KEEP":"E: SWAP  |  N: KEEP")+"\nYour current item drops here.";
            else if(equipment?.kind==EquipmentKind.PoweredArmor)status.text="POWERED IRON SUIT · "+(equipment.drawn?"HOVERING":"STANDBY")+"\n"+(vr?"Hold controller trigger + point: fly · Release: hover · Trigger at hip: land\nTracked hand pinch: plasma":"V: take off / land · WASD: steer · E: up · Q: down · Click: plasma");
            else if(equipment?.kind>EquipmentKind.PoweredArmor)
            {
                var spec=EquipmentSpecifications.Get(equipment.kind);string mode=equipment.kind==EquipmentKind.StockPistol?(equipment.shotgun?" / 3-ROUND BURST":" / FULL AUTO"):equipment.kind==EquipmentKind.Chainsaw?(equipment.shotgun?" / WHEEL LAUNCHER":" / CUTTING"):equipment.kind==EquipmentKind.LaserAxe?(equipment.shotgun?" / WIDE LASER":" / AXE"):"";
                status.text=Name(equipment.kind)+mode+" · "+(equipment.drawn?"READY":"HOLSTERED")+"\n"+(vr?"Trigger: draw / fire · Trigger at holster: stow":"V: draw / holster · Click / hold: attack")+(spec.modeSwitch?"\n"+(vr?"Other trigger: switch mode":"R: switch mode"):"")+(equipment.triggerHeld&&equipment.charge<1?" · CHARGING "+Mathf.RoundToInt(equipment.charge*100)+"%":"")+(equipment.builtIn?"\nDefault PvP melee":"")+(equipment.activePower!=EquipmentKind.None?" · "+Name(equipment.activePower)+" "+Mathf.CeilToInt(equipment.remaining)+"s":"");
            }
            else if(equipment?.kind>=EquipmentKind.Blaster)status.text=Name(equipment.kind)+((equipment.kind==EquipmentKind.DualRifle||equipment.kind==EquipmentKind.SheriffHammer)?(equipment.shotgun?" / RAPID SHOTGUN":equipment.kind==EquipmentKind.SheriffHammer?" / LONG-RANGE PISTOL":" / LONG RIFLE"):"")+" · "+(equipment.drawn?"DRAWN":"HOLSTERED")+"\n"+(vr?"Trigger: draw / fire · Trigger at holster: stow":"V: draw / holster · Click: attack")+((equipment.kind==EquipmentKind.DualRifle||equipment.kind==EquipmentKind.SheriffHammer)?"\n"+(equipment.drawn&&equipment.kind==EquipmentKind.DualRifle?"Jump, climb and vault locked. ":"")+(vr?"Support grip: aim · Other trigger: change mode":"R: change mode · Hold right mouse: aim / zoom"):"");
            else status.text=equipment==null||equipment.kind==EquipmentKind.None?"":Name(equipment.kind)+" · "+Mathf.CeilToInt(equipment.remaining)+"s";
            if(equipment!=null&&EquipmentSpecifications.Get(equipment.kind).magazine>0)status.text+="\n"+(equipment.reloadRemaining>0?"RELOADING · "+equipment.reloadRemaining.ToString("0.0")+"s":equipment.rounds+" / "+EquipmentSpecifications.Get(equipment.kind).magazine);
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
