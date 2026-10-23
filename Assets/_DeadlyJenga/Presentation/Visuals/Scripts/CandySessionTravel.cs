using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandySessionTravel:MonoBehaviour
    {
        static CandySessionTravel current;
        Canvas canvas;Image curtain;Material liquid;
        float started,duration,arrived=-1;bool finishing;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen(){current=null;GameEvents.DepartureStarted+=Begin;}
        static void Begin(float seconds)
        {
            if(current)return;
            var go=new GameObject("Visual - lobby flight curtain");DontDestroyOnLoad(go);
            current=go.AddComponent<CandySessionTravel>();current.started=Time.unscaledTime;current.duration=Mathf.Max(.3f,seconds);
            current.Build();
        }
        void OnEnable(){GameEvents.Emoted+=Emoted;GameEvents.LobbyStarted+=Lobby;}
        void Emoted(GameObject who,string emote)
        {if(emote=="TravelArrived"||emote=="DepartureCancelled"){finishing=true;arrived=Time.unscaledTime;}}
        void Lobby(GameObject who){finishing=true;arrived=Time.unscaledTime;}
        void Build()
        {
            var go=new GameObject("White flow",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.sortingOrder=2000;
            var image=new GameObject("Flow curtain",typeof(RectTransform),typeof(Image));image.transform.SetParent(go.transform,false);
            curtain=image.GetComponent<Image>();curtain.raycastTarget=false;
            var rect=curtain.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var shader=Resources.Load<Shader>("CandyScenery/CandyLiquidCurtain");
            if(shader){liquid=new Material(shader);curtain.material=liquid;}
            Render();
        }
        void LateUpdate(){Render();if(finishing&&Time.unscaledTime-arrived>.8f)Destroy(gameObject);}
        void Render()
        {
            var session=Services.Session;var view=session?.ActiveCamera;
            if(view)
            {
                var rect=canvas.GetComponent<RectTransform>();
                if(session.IsVR)
                {
                    canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;
                    rect.sizeDelta=new Vector2(4,4);rect.localScale=Vector3.one;
                    rect.SetPositionAndRotation(view.transform.position+view.transform.forward*.15f,view.transform.rotation);
                }
                else canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            }
            float progress=finishing?1-Mathf.SmoothStep(0,1,(Time.unscaledTime-arrived)/.8f):Mathf.SmoothStep(0,1,(Time.unscaledTime-started)/(duration*.7f));
            if(liquid)liquid.SetFloat("_Progress",Mathf.Lerp(-.1f,1.1f,progress));
            curtain.color=new Color(.97f,.99f,1,liquid?1:progress);
        }
        void OnDestroy()
        {GameEvents.Emoted-=Emoted;GameEvents.LobbyStarted-=Lobby;if(liquid)Destroy(liquid);if(current==this)current=null;}
    }
}
