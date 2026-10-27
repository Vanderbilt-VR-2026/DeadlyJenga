using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;
namespace DeadlyJenga.Presentation
{
    public sealed class CandyWeaponReticle:MonoBehaviour
    {
        Canvas canvas;Text glyph;Camera camera;bool vr;float nextQuery;Color color=Color.white;
        readonly RaycastHit[] hits=new RaycastHit[24];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>go.AddComponent<CandyWeaponReticle>();
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);}
        void Update()
        {
            if((Services.Session as ILobbyMatchVisitControls)?.VisitingLobby==true){if(canvas)canvas.enabled=false;return;}
            var session=Services.Session;var player=session?.Player;if(player==null)return;var equipment=player.gameObject.GetComponent<IEquipmentControls>()?.Equipment;
            bool active=equipment!=null&&equipment.drawn&&equipment.kind>=EquipmentKind.Blaster&&(!EquipmentSpecifications.Get(equipment.kind).melee||equipment.shotgun&&(equipment.kind==EquipmentKind.LaserAxe||equipment.kind==EquipmentKind.Chainsaw))&&!session.MenuOpen&&!session.PlayingHand;
            if(!canvas){var go=new GameObject("Weapon reticle",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.sortingOrder=34;var text=new GameObject("Precision / spread reticle",typeof(RectTransform),typeof(Text));text.transform.SetParent(go.transform,false);glyph=text.GetComponent<Text>();glyph.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");glyph.alignment=TextAnchor.MiddleCenter;glyph.raycastTarget=false;glyph.rectTransform.sizeDelta=new Vector2(48,48);}
            if(camera!=session.ActiveCamera||vr!=session.IsVR){camera=session.ActiveCamera;vr=session.IsVR;CandyHudLayout.Configure(canvas,transform,camera,vr);}canvas.enabled=active;if(!active||!camera)return;
            bool spread=equipment.kind==EquipmentKind.RocketLauncher||equipment.kind==EquipmentKind.GrenadeLauncher||equipment.kind==EquipmentKind.Blaster||equipment.kind==EquipmentKind.PoweredArmor||equipment.kind==EquipmentKind.ChainGun||equipment.kind==EquipmentKind.DualUzi||equipment.kind==EquipmentKind.DualRifle&&equipment.shotgun||equipment.kind==EquipmentKind.StockPistol&&!equipment.shotgun;
            glyph.text=spread?"○":"+";glyph.fontSize=spread?36:27;
            var aim=player.gameObject.GetComponent<IWeaponAim>()?.WeaponAim??new Pose(camera.transform.position,camera.transform.rotation);
            if(vr){var point=aim.position+aim.rotation*Vector3.forward*3;glyph.transform.position=point;glyph.transform.rotation=Quaternion.LookRotation(point-camera.transform.position);glyph.rectTransform.localScale=Vector3.one;}
            if(Time.unscaledTime>=nextQuery)
            {
                nextQuery=Time.unscaledTime+.08f;color=Color.white;int count=Physics.RaycastNonAlloc(aim.position,aim.rotation*Vector3.forward,hits,90,(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor)|1,QueryTriggerInteraction.Collide);float nearest=90;IModeActor target=null;var self=player.gameObject.GetComponent<IModeActor>();
                if(count<hits.Length)for(int i=0;i<count;i++){var hit=hits[i];var actor=hit.collider.GetComponentInParent<IModeActor>();if(actor==self||hit.collider.isTrigger&&actor==null||hit.distance>=nearest)continue;nearest=hit.distance;target=actor;}
                if(target!=null&&target.Alive&&(self==null||self.Team<0||target.Team!=self.Team))color=new Color(1,.12f,.09f);
            }
            glyph.color=Color.Lerp(glyph.color,color,1-Mathf.Exp(-20*Time.unscaledDeltaTime));
        }
    }
}
