using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class TycoonWheelMotion : MonoBehaviour
    {
        Transform stream;
        float ground;

        public void Initialize(Transform water, float deckHeight)
        {
            stream = water;
            ground = deckHeight;
            Update();
        }

        void Update()
        {
            float angle = Mathf.Repeat(Time.time * 15f, 360f);
            transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            // One of the six cups enters its emptying arc every sixty degrees.
            float phase = Mathf.Repeat(angle - 100f, 60f);
            var tilt = Quaternion.AngleAxis(100f + phase, Vector3.forward);
            var lip = transform.localPosition + tilt * Vector3.up * 4.68f;
            float flow = Mathf.SmoothStep(0, 1, phase / 8) * (1 - Mathf.SmoothStep(0, 1, (phase - 40) / 12));
            stream.localPosition = lip;
            stream.localScale = new Vector3(flow, lip.y - ground, flow);
        }
    }
}
