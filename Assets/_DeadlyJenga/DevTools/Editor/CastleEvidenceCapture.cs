using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class CastleEvidenceCapture
    {
        [MenuItem("Tools/Castle Prototype/Capture Reference Views %#F11")]
        public static void Capture()
        {
            var generator = Object.FindFirstObjectByType<CastleGenerator>();
            var go = new GameObject("Temporary evidence camera"); var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 55; camera.farClipPlane = 600; camera.nearClipPlane = .05f;
            camera.clearFlags = CameraClearFlags.Skybox; camera.backgroundColor = new Color(.12f,.19f,.26f);
            string dir = Path.GetFullPath("Temp/CastleEvidence"); Directory.CreateDirectory(dir);
            var texture = new RenderTexture(1600, 1100, 24); camera.targetTexture = texture;
            var old = RenderTexture.active;
            GameObject marker = null;
            try
            {
                void Shot(string name, Vector3 position, Vector3 target)
                {
                    camera.transform.position = position; camera.transform.LookAt(target); camera.Render();
                    RenderTexture.active = texture;
                    var png = new Texture2D(1600,1100,TextureFormat.RGB24,false);
                    png.ReadPixels(new Rect(0,0,1600,1100),0,0); png.Apply();
                    File.WriteAllBytes(Path.Combine(dir,name+".png"),png.EncodeToPNG()); Object.DestroyImmediate(png);
                }
                Vector3 origin = generator.transform.position;
                Shot("corrected-castle",origin+new Vector3(72,67,-78),origin+new Vector3(0,28,0));
                Shot("central-interior",origin+new Vector3(0,61,0),origin+new Vector3(0,25,7));
                var top = generator.Bodies.Where(b => b && b.name.StartsWith("Corner")).OrderByDescending(b => b.position.y).First();
                var box = top.GetComponent<BoxCollider>();
                float playerHeight = AssetDatabase.LoadAssetAtPath<CastleSurvivor>(ProjectPath.Find("CastleSurvivor.prefab")).StandingHeight;
                float playerRadius = .24f * (playerHeight / 1.7f);
                marker = GameObject.CreatePrimitive(PrimitiveType.Capsule); marker.name = playerHeight + " metre player scale marker";
                marker.GetComponent<Collider>().enabled = false;
                marker.transform.localScale = new Vector3(playerRadius * 2, playerHeight * .5f, playerRadius * 2);
                marker.transform.position = box.bounds.center + Vector3.up*(box.bounds.extents.y+playerHeight * .5f);
                marker.GetComponent<Renderer>().material.color = new Color(.9f,.15f,.12f);
                Shot("player-crate-scale",marker.transform.position+new Vector3(10,4,-12),marker.transform.position+Vector3.down*2);
            }
            finally
            {
                RenderTexture.active = old; camera.targetTexture = null;
                Object.DestroyImmediate(go); Object.DestroyImmediate(texture); if (marker) Object.DestroyImmediate(marker);
            }
            Debug.Log("Castle reference views saved to " + dir);
        }
    }
}
