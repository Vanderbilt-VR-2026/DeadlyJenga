using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyHealthFeedback:MonoBehaviour
    {
        Canvas canvas;Image rim,bar;Text label;Texture2D texture;Sprite sprite;Camera eye;
        IMatchRuntime owner;string actor;float previous=-1,pulse,healing;bool wasAlive,vr;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyHealthFeedback>())go.AddComponent<CandyHealthFeedback>();};
        void OnEnable()=>GameEvents.Damaged+=Hit;
        void OnDisable()=>GameEvents.Damaged-=Hit;
        void Hit(GameObject who,float amount)
        {if(who&&amount>0&&Services.Session?.Player!=null&&who.transform.root==Services.Session.Player.transform.root)pulse=Mathf.Max(pulse,Mathf.Clamp(.35f+amount*.02f,.35f,1));}
        void Build()
        {
            var root=new GameObject("Local health response",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.sortingOrder=65;
            texture=new Texture2D(256,160,TextureFormat.RGBA32,false){name="Soft peripheral candy impact",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<160;y++)for(int x=0;x<256;x++)
            {
                float nx=(x-127.5f)/128,ny=(y-79.5f)/80,r=Mathf.Max(Mathf.Abs(nx),Mathf.Abs(ny));
                float streak=.65f+.35f*Mathf.Pow(Mathf.Sin(Mathf.Atan2(ny,nx)*17+nx*4),8);
                texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01((r-.68f)/.32f),2)*streak*.65f));
            }
            texture.Apply();sprite=Sprite.Create(texture,new Rect(0,0,256,160),Vector2.one*.5f);
            rim=Image("Peripheral impact",root.transform,new Vector2(1280,800),Vector2.zero);rim.sprite=sprite;
            var well=Image("Health well",root.transform,new Vector2(256,16),new Vector2(0,-270));well.color=new Color(.03f,.02f,.06f,.6f);
            bar=Image("Health",well.transform,new Vector2(252,12),Vector2.zero);bar.rectTransform.pivot=new Vector2(0,.5f);bar.rectTransform.anchoredPosition=new Vector2(-126,0);
            var go=new GameObject("Health change",typeof(RectTransform),typeof(Text));go.transform.SetParent(root.transform,false);label=go.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=21;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;label.rectTransform.sizeDelta=new Vector2(300,36);label.rectTransform.anchoredPosition=new Vector2(0,-240);
        }
        static Image Image(string name,Transform parent,Vector2 size,Vector2 pos)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var i=go.GetComponent<Image>();i.raycastTarget=false;i.rectTransform.sizeDelta=size;i.rectTransform.anchoredPosition=pos;return i;}
        void Update()
        {
            var match=MatchRuntime.Current;var session=Services.Session;if(match==null||session==null){if(canvas)canvas.enabled=false;return;}
            if(!ReferenceEquals(owner,match)||actor!=match.LocalActorId){owner=match;actor=match.LocalActorId;previous=-1;pulse=healing=0;}
            var p=System.Array.Find(match.Frame.players,v=>v.id==actor);if(p==null)return;
            bool alive=p.alive&&!p.dying;
            if(previous>=0&&wasAlive&&alive)
            {
                float change=p.health-previous;
                if(change<-.001f)pulse=Mathf.Min(1,pulse+.35f-change*2);
                if(change>.001f){pulse=0;healing=Mathf.Max(.65f,Mathf.Min(1,healing+.25f+change));CandyAudioEngine.Instance?.Play(CandySound.PlayerHeal,p.position,.6f,0,1,true);}
            }
            previous=p.health;wasAlive=alive;pulse=Mathf.MoveTowards(pulse,0,Time.unscaledDeltaTime*1.3f);healing=Mathf.MoveTowards(healing,0,Time.unscaledDeltaTime);
            if(!canvas)Build();if(eye!=session.ActiveCamera||vr!=session.IsVR){eye=session.ActiveCamera;vr=session.IsVR;CandyHudLayout.Configure(canvas,transform,eye,vr);rim.rectTransform.sizeDelta=vr?new Vector2(2800,2000):CandyHudLayout.ReferenceSize;}
            canvas.enabled=eye&&!session.MenuOpen&&!session.PlayingHand&&match.Rules.healthEnabled&&(pulse>0||healing>0);
            rim.color=pulse>0?new Color(1,.025f,.06f,pulse):new Color(.18f,1,.7f,healing*.45f);
            bar.rectTransform.sizeDelta=new Vector2(252*Mathf.Clamp01(p.health),12);bar.color=pulse>0?new Color(1,.15f,.18f):new Color(.2f,1,.7f);label.color=bar.color;
            label.text=(pulse>0?"HIT · ":"RECOVERING · ")+Mathf.CeilToInt(p.health*100)+" HP";
        }
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);if(sprite)Destroy(sprite);if(texture)Destroy(texture);}
    }
}
