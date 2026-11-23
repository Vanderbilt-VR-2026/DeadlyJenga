using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed partial class FantasyMapArt
    {
        GameObject rainwaterSource;
        Material rainwaterPaint;

        bool DressReviewedRainwater(Transform bodyVisual, string site, string purpose)
        {
            bool bridge = purpose == "Rainwater gallery crossing";
            if (!bridge && (site != "Canyon south-east tower" || !purpose.Contains(" floor ")))
                return false;

            if (!rainwaterSource) rainwaterSource = Resources.Load<GameObject>("FantasyMaps/RainwaterWorks");
            if (!rainwaterSource) return false;
            Transform origin = rainwaterSource.transform.Find("Origin");
            Transform north = rainwaterSource.transform.Find("North");
            Transform up = rainwaterSource.transform.Find("Up");
            Transform east = rainwaterSource.transform.Find("East");
            if (!origin || !north || !up || !east) return false;

            int level = bridge ? 0 : purpose[purpose.Length - 1] - '1';
            if (level < 0 || level > 6) return false;
            string name = bridge ? "Bridge_" : "Floor" + level + "_";
            Vector3 authoredPivot = bridge ? new Vector3(0,9.15f,-25.2f) : new Vector3(27,1.15f + level*4,-17);
            var root = new GameObject("Fantasy decorations");
            root.transform.SetParent(bodyVisual, false);
            root.transform.localPosition = -authoredPivot + Vector3.up * (bridge ? .42f : level == 0 ? -.08f : 0);
            bodyObjects.Add(root);
            Vector3 upDirection = up.position - origin.position;
            Quaternion align = Quaternion.Inverse(Quaternion.LookRotation(north.position-origin.position,upDirection));
            var basis = new GameObject("Review 02 decoration coordinates").transform;
            basis.SetParent(root.transform, false);
            if (Vector3.Dot(align * (east.position-origin.position), Vector3.right) < 0)
                basis.localScale = new Vector3(-1,1,1);
            if (!rainwaterPaint)
            {
                rainwaterPaint = new Material(cityCandyMaterial) { name = "Rainwater Works Blender paint" };
                rainwaterPaint.SetColor("_BaseColor", Color.white);
                rainwaterPaint.SetFloat("_Pattern", 8);
                rainwaterPaint.SetFloat("_DetailStrength", 0);
                rainwaterPaint.SetFloat("_Smoothness", .42f);
                rainwaterPaint.SetFloat("_Glow", 0);
            }
            float units = 1 / upDirection.magnitude;
            foreach (var piece in rainwaterSource.GetComponentsInChildren<MeshFilter>())
            {
                if (!piece.name.StartsWith(name, System.StringComparison.Ordinal) &&
                    !(level == 6 && piece.name.StartsWith("Roof_", System.StringComparison.Ordinal))) continue;
                var instance = new GameObject(piece.name, typeof(MeshFilter), typeof(MeshRenderer));
                instance.transform.SetParent(basis, false);
                instance.transform.localPosition = align * (piece.transform.position-origin.position) * units;
                instance.transform.localRotation = align * piece.transform.rotation;
                instance.transform.localScale = piece.transform.lossyScale * units;
                instance.GetComponent<MeshFilter>().sharedMesh = piece.sharedMesh;
                var renderer = instance.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = rainwaterPaint;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (!bridge && level < 6)
            {
                // The rain returns have a coupling at each body seam.
                var pipe = new ArtMesh();
                var copper = new Color(.66f,.40f,.18f);
                foreach(float x in new[]{-8.75f,-6.25f})
                    pipe.Cylinder(new Vector3(x,.03f,-10.8f), Quaternion.identity,.28f,3.94f,0,copper,12);
                float start = level % 2 == 0 ? 4f : -3.36f;
                float finish = level % 2 == 0 ? -3.36f : 4f;
                float lane = level % 2 == 0 ? 6.05f : 8.6f;
                for (int step = 0; step < 23; step++)
                {
                    float depth = Mathf.Lerp(start,finish,(step+.5f)/23);
                    float rise = (step+1) * (4f/23);
                    pipe.Box(new Vector3(lane,rise-.107f,depth),
                        new Vector3(2.32f,.25f,.34f),Quaternion.identity,0,new Color(.60f,.37f,.19f));
                }
                Model("Rain returns and oak stair treads",pipe,bodyVisual,new[]{cityCandyMaterial},true);
            }
            return true;
        }
    }
}
