using UnityEngine;

namespace DeadlyJenga.Presentation
{
    internal static class CandyScreenPlane
    {
        public static void Configure(Canvas canvas, Camera camera, bool vr)
        {
            var mode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            if (canvas.renderMode != mode) canvas.renderMode = mode;
            canvas.worldCamera = vr ? camera : null;
            if (vr) Fit((RectTransform)canvas.transform, camera);
        }
        public static float Fit(RectTransform plane, Camera camera)
        {
            float depth = camera.nearClipPlane + Mathf.Min(.12f, (camera.farClipPlane - camera.nearClipPlane) * .5f);
            Vector2 extent = Vector2.zero;
            bool stereo = camera.stereoEnabled;
            for (int eye = 0; eye < (stereo ? 2 : 1); eye++)
            {
                var projection = !stereo ? Camera.MonoOrStereoscopicEye.Mono :
                    eye == 0 ? Camera.MonoOrStereoscopicEye.Left : Camera.MonoOrStereoscopicEye.Right;
                for (int corner = 0; corner < 4; corner++)
                {
                    var point = camera.ViewportToWorldPoint(new Vector3(corner & 1, corner >> 1, depth), projection);
                    var local = Quaternion.Inverse(camera.transform.rotation) * (point - camera.transform.position);
                    extent = Vector2.Max(extent, new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.y)));
                }
            }
            var scale = plane.parent ? plane.parent.lossyScale : Vector3.one;
            plane.localScale = new Vector3(1 / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                1 / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            plane.SetPositionAndRotation(camera.transform.position + camera.transform.forward * depth, camera.transform.rotation);
            plane.sizeDelta = extent * 2.2f + Vector2.one * .03f;
            return plane.sizeDelta.x / plane.sizeDelta.y;
        }
    }
}
