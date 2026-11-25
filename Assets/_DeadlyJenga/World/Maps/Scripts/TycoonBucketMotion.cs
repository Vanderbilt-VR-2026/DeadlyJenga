using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // A cosmetic mechanism attached to the breakaway summit module.
    // While supplied it fills and tips every 16 s. Once its supply is cut it finishes
    // the dump in progress, then hangs empty. The pour always falls straight down.
    public sealed class TycoonBucketMotion : MonoBehaviour
    {
        const float SheetLength = 6.1f;
        Transform bucket, stream;
        float elapsed;
        bool empty;
        System.Func<bool> supplied;

        public bool Empty => empty;

        public void Initialize(Transform vessel, Transform sheet)
        {
            bucket = vessel;
            stream = sheet;
            ApplyPose();
        }

        public void SetSupply(System.Func<bool> supply) => supplied = supply;

        void Update()
        {
            float t = Mathf.Repeat(elapsed + 3, 16);
            bool fed = supplied == null || supplied();
            if (fed) empty = false;
            else if (t < 1f || t > 6.6f) empty = true;
            if (!empty) elapsed += Time.deltaTime;
            ApplyPose();
        }

        void ApplyPose()
        {
            if (!bucket || !stream) return;
            float t = Mathf.Repeat(elapsed + 3, 16);
            float tilt = empty ? 0 : Mathf.SmoothStep(0, 1, (t-1)/1.3f)
                * (1-Mathf.SmoothStep(0, 1, (t-4)/2.4f));
            bucket.localRotation = Quaternion.Euler(-68*tilt, 0, 0);
            float flow = empty ? 0 : Mathf.SmoothStep(0, 1, (t-1.8f)/.9f)
                * (1-Mathf.SmoothStep(0, 1, (t-3.8f)/1.1f));
            if (stream.gameObject.activeSelf != (flow>.01f)) stream.gameObject.SetActive(flow>.01f);
            if (flow <= .01f) return;
            stream.localPosition = bucket.localRotation * new Vector3(0,1.92f,-3.45f);
            // Keep the sheet hanging with gravity even if the summit deck tips.
            var back = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (back.sqrMagnitude < 1e-4f) back = Vector3.forward;
            stream.rotation = Quaternion.LookRotation(back, Vector3.up);
            float drop = SheetLength;
            int mask = ~((1 << Layers.Survivor) | (1 << Layers.Surface) | (1 << 2));
            if (Application.isPlaying && Physics.Raycast(stream.position, Vector3.down, out var hit, 40, mask, QueryTriggerInteraction.Ignore))
                drop = hit.distance;
            stream.localScale = new Vector3(flow, flow * Mathf.Clamp(drop / SheetLength, .3f, 4f), flow);
        }
    }
}
