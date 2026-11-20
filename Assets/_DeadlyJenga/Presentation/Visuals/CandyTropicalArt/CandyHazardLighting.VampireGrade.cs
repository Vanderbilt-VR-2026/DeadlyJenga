using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.CandyArt
{
    // Vampire disaster, as the local survivor sees it:
    // in shade the world turns dim and black-and-white; in sunlight it burns bright and washed out.
    public sealed partial class CandyHazardLighting
    {
        Volume vampireGrade;
        ColorAdjustments vampireColor;
        Vignette vampireVignette;
        Bloom vampireBloom;
        UniversalAdditionalCameraData vampireCamera;
        bool vampireCameraHadPost;
        float vampireSunBlend;

        void UpdateVampireGrade(DisasterFrame frame, Camera view)
        {
            if (!vampireGrade) CreateVampireGrade();
            var data = view ? view.GetUniversalAdditionalCameraData() : null;
            if (data != vampireCamera)
            {
                RestoreVampireCamera();
                vampireCamera = data;
                if (data) { vampireCameraHadPost = data.renderPostProcessing; data.renderPostProcessing = true; }
            }

            bool exposed = VampireSunOnPlayer(frame);
            vampireSunBlend = Mathf.MoveTowards(vampireSunBlend, exposed ? 1 : 0, Time.deltaTime * 2.5f);
            float s = Mathf.SmoothStep(0, 1, vampireSunBlend);
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 7f);

            vampireGrade.weight = 1;
            vampireColor.saturation.Override(Mathf.Lerp(-100f, -20f, s));
            vampireColor.postExposure.Override(Mathf.Lerp(-1.3f, 2.4f + .35f * pulse, s));
            vampireColor.contrast.Override(Mathf.Lerp(25f, -35f, s));
            vampireColor.colorFilter.Override(Color.Lerp(new Color(.86f, .9f, 1f), new Color(1f, .93f, .8f), s));
            vampireVignette.intensity.Override(Mathf.Lerp(.5f, .2f, s));
            vampireVignette.color.Override(Color.Lerp(Color.black, new Color(1f, .95f, .85f), s));
            vampireBloom.intensity.Override(Mathf.Lerp(0f, 3.5f, s));
        }

        bool VampireSunOnPlayer(DisasterFrame frame)
        {
            var session = Services.Session;
            var who = session?.Player?.gameObject;
            if (!who || session.PlayingHand) return false;
            var actor = who.GetComponent<IModeActor>();
            if (actor == null || !actor.Alive) return false;
            var eye = actor.ViewPose.position;
            if (!new Bounds(frame.center, frame.size + Vector3.one * 10).Contains(eye)) return false;
            float elevation = (frame.values != null && frame.values.Length > 2 ? frame.values[2] : 45) * Mathf.Deg2Rad;
            var towardSun = new Vector3(.6f * Mathf.Cos(elevation), Mathf.Sin(elevation), .8f * Mathf.Cos(elevation));
            return !Physics.Raycast(eye, towardSun, 1000, 1 << Layers.Block, QueryTriggerInteraction.Ignore);
        }

        void CreateVampireGrade()
        {
            var go = new GameObject("Vampire view grade");
            go.transform.SetParent(transform, false);
            vampireGrade = go.AddComponent<Volume>();
            vampireGrade.isGlobal = true;
            vampireGrade.priority = 200;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Vampire grade (runtime)";
            vampireColor = profile.Add<ColorAdjustments>(true);
            vampireVignette = profile.Add<Vignette>(true);
            vampireVignette.smoothness.Override(.6f);
            vampireBloom = profile.Add<Bloom>(true);
            vampireBloom.threshold.Override(.9f);
            vampireGrade.sharedProfile = profile;
        }

        void RestoreVampireCamera()
        {
            if (vampireCamera) vampireCamera.renderPostProcessing = vampireCameraHadPost;
            vampireCamera = null;
        }

        void EndVampireGrade()
        {
            vampireSunBlend = 0;
            RestoreVampireCamera();
            if (vampireGrade) vampireGrade.weight = 0;
        }

        void DestroyVampireGrade()
        {
            EndVampireGrade();
            if (!vampireGrade) return;
            if (vampireGrade.sharedProfile) Destroy(vampireGrade.sharedProfile);
            Destroy(vampireGrade.gameObject);
            vampireGrade = null;
        }
    }
}
