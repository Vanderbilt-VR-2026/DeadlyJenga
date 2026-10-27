#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using DeadlyJenga.Core;
using DeadlyJenga.Destruction;

namespace DeadlyJenga.CastlePrototype
{
    // Editor keys for the destruction systems.
    // F5 break the block in the middle of the screen, F6 rewind 4 s, F7 play the replay, F8 breaking on/off.
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
            if (k.f5Key.wasPressedThisFrame) BreakAimed();
            if (k.f6Key.wasPressedThisFrame)
                Debug.Log(Services.Rewind != null && Services.Rewind.Rewind(4) ? "Rewinding 4 s" : "Can't rewind (not host, or nothing recorded yet)");
            if (k.f7Key.wasPressedThisFrame && CollapseReplay.Instance) CollapseReplay.Instance.PlayNow();
            if (k.f8Key.wasPressedThisFrame && BlockStressEngine.Instance)
            {
                var engine = BlockStressEngine.Instance;
                engine.breakingEnabled = !engine.breakingEnabled;
                Debug.Log("Block breaking " + (engine.breakingEnabled ? "on" : "off"));
            }
        }

        static void BreakAimed()
        {
            var cam = Services.Session?.ActiveCamera;
            if (!cam) cam = Camera.main;
            if (!cam) return;
            var ray = cam.ViewportPointToRay(new Vector3(.5f, .5f));
            if (Physics.Raycast(ray, out var hit, 500, 1 << Layers.Block, QueryTriggerInteraction.Ignore))
                hit.collider.GetComponentInParent<IBreakable>()?.Break(null);
        }
    }
}
#endif
