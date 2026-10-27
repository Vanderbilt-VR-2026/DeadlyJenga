using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyPowerupSpawnPanel:MonoBehaviour
    {
        bool open;Vector2 scroll;Rect window=new Rect(30,60,610,650);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>{if(!go.GetComponent<CandyPowerupSpawnPanel>())go.AddComponent<CandyPowerupSpawnPanel>();};
        public static void Open(){var panel=FindFirstObjectByType<CandyPowerupSpawnPanel>();if(panel)panel.open=true;}
        void OnGUI()
        {
            if(!open||Services.Session?.MenuOpen!=true||Services.Session.IsVR)return;
            window=GUI.Window(GetInstanceID(),window,Draw,"RANDOM BLOCK POWERUPS");
        }
        void Draw(int id)
        {
            var runtime=MatchRuntime.Current;var controls=runtime as IPowerupSpawnControls;if(controls==null){GUILayout.Label("Session is loading.");return;}
            var settings=controls.SpawnSettings;bool changed=false;GUI.enabled=runtime.Authority;
            bool enabled=GUILayout.Toggle(settings.enabled,"Random spawns enabled");changed|=enabled!=settings.enabled;settings.enabled=enabled;
            GUILayout.Label("Samples every block height. Blocked, submerged or unstable surfaces are skipped.");
            float interval=Slider("Seconds between spawn attempts",settings.interval,1,300);changed|=interval!=settings.interval;settings.interval=interval;
            int maximum=Mathf.RoundToInt(Slider("Maximum random pickups alive",settings.maximum,0,32));changed|=maximum!=settings.maximum;settings.maximum=maximum;
            float lifetime=Slider("Seconds before an uncollected random pickup expires",settings.lifetime,5,600);changed|=lifetime!=settings.lifetime;settings.lifetime=lifetime;
            float jitter=Slider("Timing variation",settings.jitter,0,.9f,false);changed|=jitter!=settings.jitter;settings.jitter=jitter;
            GUILayout.Label($"Random pickups: {controls.RandomPickupCount} / {settings.maximum} · Next attempt: {controls.NextSpawnIn:F1}s");
            float total=0;foreach(var type in settings.types)total+=type.weight;
            GUILayout.Label("Type weights (normalized percentages). Zero disables a type; all zero pauses spawning.");
            scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(260));
            foreach(var type in settings.types)
            {
                GUILayout.BeginHorizontal();GUILayout.Label(CandyEquipmentView.Name(type.kind),GUILayout.Width(185));
                float weight=Mathf.Round(GUILayout.HorizontalSlider(type.weight,0,100));
                GUILayout.Label($"{weight:F0} · {(total>0?100*weight/total:0):F1}%",GUILayout.Width(105));GUILayout.EndHorizontal();changed|=weight!=type.weight;type.weight=weight;
            }
            GUILayout.EndScrollView();
            if(changed)controls.ConfigurePowerupSpawns(settings);
            GUI.enabled=true;if(!runtime.Authority)GUILayout.Label("The host controls these settings.");
            if(GUILayout.Button("Close"))open=false;GUI.DragWindow(new Rect(0,0,610,24));
        }
        static float Slider(string label,float value,float min,float max,bool whole=true)
        {GUILayout.Label(label+": "+(whole?value.ToString("F0"):value.ToString("P0")));float next=GUILayout.HorizontalSlider(value,min,max);return whole?Mathf.Round(next):Mathf.Round(next*100)/100;}
    }
}
