using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(1000)]
    public sealed class CandyNameplateFollow : MonoBehaviour
    {
        Camera eye;
        Transform anchor;
        IModeActor actor;
        Vector3 localOffset;
        float above;
        Transform box;
        TextMesh label;
        string fittedName;
        public void Bind(TextMesh text, Camera camera, Transform target, Vector3 focus, float height, bool useView)
        {
            eye = camera;
            if (target != anchor)
            {
                anchor = target;
                actor = useView && anchor ? anchor.GetComponent<IModeActor>() : null;
            }
            if(anchor) localOffset = anchor.InverseTransformPoint(focus);
            above = height;
            label = text;
            if (!box) box = transform.Find("Name background");
            if(box && fittedName != text.text)
            {
                fittedName = text.text;
                var bounds = text.GetComponent<MeshRenderer>().localBounds;
                box.localScale = new Vector3(bounds.size.x + .065f, bounds.size.y + .025f, 1);
                box.localPosition = new Vector3(bounds.center.x,bounds.center.y,.012f);
            }
        }
        void LateUpdate()
        {
            if (!eye || !label) return;
            if (anchor)
                transform.position = (actor?.ViewPose.position ?? anchor.TransformPoint(localOffset)) + Vector3.up * above;
            Vector3 direction = transform.position - eye.transform.position;
            if(direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
