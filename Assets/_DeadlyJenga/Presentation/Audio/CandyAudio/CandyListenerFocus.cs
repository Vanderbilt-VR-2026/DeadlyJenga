using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyAudio
{
    [DefaultExecutionOrder(1500)]
    public sealed class CandyListenerFocus : MonoBehaviour
    {
        readonly Dictionary<AudioListener,bool> originals=new Dictionary<AudioListener,bool>();
        readonly HashSet<AudioListener> owned=new HashSet<AudioListener>();
        Camera view;float scanAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=root=>{if(!root.GetComponent<CandyListenerFocus>())root.AddComponent<CandyListenerFocus>();};
        void LateUpdate()
        {
            var camera=Services.Session?.ActiveCamera;if(!camera)return;
            if(camera==view&&Time.unscaledTime<scanAt)return;view=camera;scanAt=Time.unscaledTime+1;
            var target=camera.GetComponent<AudioListener>();
            if(!target){target=camera.gameObject.AddComponent<AudioListener>();owned.Add(target);}
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {if(!owned.Contains(listener)&&!originals.ContainsKey(listener))originals[listener]=listener.enabled;listener.enabled=listener==target;}
        }
        void OnDisable()
        {
            foreach(var pair in originals)if(pair.Key)pair.Key.enabled=pair.Value;
            foreach(var listener in owned)if(listener)Destroy(listener);
            originals.Clear();owned.Clear();view=null;
        }
    }
}
