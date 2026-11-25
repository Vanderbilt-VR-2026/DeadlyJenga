using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The pail wheel turns while its ground pipe still feeds it. Cut off, the inlet stops
    // and the wheel spins down. Each pour falls straight down to whatever is below.
    public sealed class TycoonWheelMotion : MonoBehaviour
    {
        const float FullSpeed = 15f;
        Transform stream, inlet;
        float ground, angle, speed = FullSpeed;
        System.Func<bool> supplied;

        public float Speed => speed;
        public bool InletRunning => inlet && inlet.gameObject.activeSelf;

        public void Initialize(Transform water, float deckHeight, Transform inletWater = null)
        {
            stream = water;
            ground = deckHeight;
            inlet = inletWater;
            Update();
        }

        public void SetSupply(System.Func<bool> supply) => supplied = supply;

        void Update()
        {
            bool fed = supplied == null || supplied();
            speed = Mathf.MoveTowards(speed, fed ? FullSpeed : 0, Time.deltaTime * 5f);
            if (inlet && inlet.gameObject.activeSelf != fed) inlet.gameObject.SetActive(fed);
            angle = Mathf.Repeat(angle + speed * Time.deltaTime, 360f);
            transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            // One of the six cups enters its emptying arc every sixty degrees.
            float phase = Mathf.Repeat(angle - 100f, 60f);
            var tilt = Quaternion.AngleAxis(100f + phase, Vector3.forward);
            var lip = transform.localPosition + tilt * Vector3.up * 4.68f;
            float flow = Mathf.SmoothStep(0, 1, phase / 8) * (1 - Mathf.SmoothStep(0, 1, (phase - 40) / 12)) * (speed / FullSpeed);
            stream.localPosition = lip;
            float drop = lip.y - ground;
            int mask = ~((1 << Layers.Survivor) | (1 << Layers.Surface) | (1 << 2));
            if (Application.isPlaying && Physics.Raycast(stream.position, Vector3.down, out var hit, 30, mask, QueryTriggerInteraction.Ignore))
                drop = hit.distance;
            stream.rotation = Quaternion.identity;
            stream.localScale = new Vector3(flow, drop, flow);
        }
    }
}
