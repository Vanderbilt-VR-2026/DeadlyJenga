using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // A cosmetic mechanism attached to the breakaway summit module.
    public sealed class TycoonBucketMotion : MonoBehaviour
    {
        Transform bucket, stream;
        float elapsed;

        public void Initialize(Transform vessel, Transform sheet)
        {
            bucket = vessel;
            stream = sheet;
            ApplyPose();
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            ApplyPose();
        }

        void ApplyPose()
        {
            if (!bucket || !stream) return;
            float t = Mathf.Repeat(elapsed + 3, 16);
            float tilt = Mathf.SmoothStep(0, 1, (t-1)/1.3f)
                * (1-Mathf.SmoothStep(0, 1, (t-4)/2.4f));
            bucket.localRotation = Quaternion.Euler(-68*tilt, 0, 0);
            float flow = Mathf.SmoothStep(0, 1, (t-1.8f)/.9f)
                * (1-Mathf.SmoothStep(0, 1, (t-3.8f)/1.1f));
            stream.localPosition = bucket.localRotation * new Vector3(0,1.92f,-3.45f);
            stream.localScale = new Vector3(flow,flow,flow);
            if (stream.gameObject.activeSelf != (flow>.01f)) stream.gameObject.SetActive(flow>.01f);
        }
    }
}
