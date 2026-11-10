using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.DevTools.Editor
{
    // Review-only camera for player-height captures. Never used by gameplay.
    public static class NinjaShotCamera
    {
        public static Camera Place(Vector3 position, Vector3 target, float fov = 80)
        {
            QualitySettings.SetQualityLevel(0, true);
            var go = GameObject.Find("ShotCam") ?? new GameObject("ShotCam"); var c = go.GetComponent<Camera>(); if (!c) c = go.AddComponent<Camera>();
            var source = DeadlyJenga.Core.Services.Session?.ActiveCamera; if (source) c.CopyFrom(source);
            c.enabled = false; c.fieldOfView = fov; c.farClipPlane = 1500; c.nearClipPlane = .08f;
            var data = go.GetComponent<UniversalAdditionalCameraData>(); if (!data) data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderShadows = true;
            go.transform.position = position; go.transform.LookAt(target); return c;
        }
    }
}
