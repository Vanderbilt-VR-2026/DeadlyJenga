using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.DevTools.Editor.TrailerStudio
{
    // The director's view of the stage: an orbit camera that draws into a texture the Studio window shows.
    // It is not a shot camera (those come in Phase 5); it is how you look around while you work.
    public static class StudioCamera
    {
        public static Camera Camera { get; private set; }
        public static RenderTexture Image { get; private set; }
        public static Vector3 Target;             // world point the camera circles
        public static float Yaw = 35, Pitch = 22, Distance = 22;

        public static void Create(StudioStage stage)
        {
            Destroy();
            var rig = new GameObject("Trailer Studio viewport camera");
            Object.DontDestroyOnLoad(rig);
            Camera = rig.AddComponent<Camera>();
            Camera.nearClipPlane = .08f; Camera.farClipPlane = 1600;
            Camera.clearFlags = CameraClearFlags.Skybox; Camera.allowHDR = true;
            var data = Camera.GetUniversalAdditionalCameraData();
            data.allowXRRendering = false;
            data.renderPostProcessing = true;
            rig.AddComponent<AudioListener>();
            Resize(960, 540);
            Target = stage.anchor + Vector3.up;
            Distance = Mathf.Clamp(stage.world.Radius * .9f, 10, 80);
            Place();
        }

        public static void Resize(int width, int height)
        {
            width = Mathf.Clamp(width, 64, 3840); height = Mathf.Clamp(height, 64, 2160);
            if (Image && Image.width == width && Image.height == height) return;
            if (Camera) Camera.targetTexture = null;
            if (Image) Object.DestroyImmediate(Image);
            Image = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Trailer Studio viewport", hideFlags = HideFlags.HideAndDontSave };
            if (Camera) Camera.targetTexture = Image;
        }

        // Only one set of ears at a time: the director's camera, or the performer's own head.
        public static void Listen(bool on)
        {
            var ears = Camera ? Camera.GetComponent<AudioListener>() : null;
            if (ears) ears.enabled = on;
        }

        public static void Orbit(float yaw, float pitch)
        {
            Yaw += yaw; Pitch = Mathf.Clamp(Pitch + pitch, -85, 88);
            Place();
        }

        public static void Zoom(float factor)
        {
            Distance = Mathf.Clamp(Distance * factor, 1.5f, 400);
            Place();
        }

        // Slide the target sideways/up in the camera's own plane.
        public static void Pan(Vector2 screen)
        {
            if (!Camera) return;
            Target += (Camera.transform.right * -screen.x + Camera.transform.up * screen.y) * (Distance * .0016f);
            Place();
        }

        public static void Place()
        {
            if (!Camera) return;
            var rotation = Quaternion.Euler(Pitch, Yaw, 0);
            Camera.transform.SetPositionAndRotation(Target - rotation * Vector3.forward * Distance, rotation);
        }

        // Draw the current view and save it as a PNG (used for thumbnails and review pictures).
        public static void SavePng(string file)
        {
            if (!Camera || !Image) return;
            Camera.Render();
            var old = RenderTexture.active;
            var picture = new Texture2D(Image.width, Image.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = Image;
                picture.ReadPixels(new Rect(0, 0, Image.width, Image.height), 0, 0);
                picture.Apply(false);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, picture.EncodeToPNG());
            }
            finally { RenderTexture.active = old; Object.DestroyImmediate(picture); }
        }

        public static void Destroy()
        {
            if (Camera) { Camera.targetTexture = null; Object.Destroy(Camera.gameObject); }
            if (Image) Object.DestroyImmediate(Image);
            Camera = null; Image = null;
        }
    }
}
