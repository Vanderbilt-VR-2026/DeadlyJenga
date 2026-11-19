#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using DeadlyJenga.Core;
using DeadlyJenga.Destruction;

namespace DeadlyJenga.CastlePrototype
{
    // F6 rewinds, F7 plays the replay, F10 restarts the tutorial.
    public sealed class DestructionTestKeys : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DestructionTestKeys>()) return;
            var go = new GameObject("Destruction test keys");
            DontDestroyOnLoad(go);
            go.AddComponent<DestructionTestKeys>();
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.f6Key.wasPressedThisFrame)
                Debug.Log(Services.Rewind != null && Services.Rewind.Rewind(4) ? "Rewinding 4 s" : "Can't rewind (not host, or nothing recorded yet)");
            if (k.f7Key.wasPressedThisFrame && CollapseReplay.Instance) CollapseReplay.Instance.PlayNow();
            if (k.f10Key.wasPressedThisFrame) { TutorialCoach.Restart(); Debug.Log("Tutorial restarting"); }
        }
    }
}
#endif
