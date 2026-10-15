using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    // Isolated runtime hook: no edits to the population factory or bot brain are needed.
    public sealed class CastleBotEmoteRegistry:MonoBehaviour
    {
        float nextScan;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(FindFirstObjectByType<CastleBotEmoteRegistry>())return;
            var go=new GameObject("NPC emotes (visual only)");DontDestroyOnLoad(go);go.AddComponent<CastleBotEmoteRegistry>();
        }
        void Update()
        {
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+1;
            foreach(var bot in FindObjectsByType<CastleBotBrain>(FindObjectsSortMode.None))
                if(!bot.GetComponent<CastleBotEmotes>())bot.gameObject.AddComponent<CastleBotEmotes>();
        }
    }
}
