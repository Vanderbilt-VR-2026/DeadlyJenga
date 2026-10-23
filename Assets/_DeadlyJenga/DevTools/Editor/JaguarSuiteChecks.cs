using System;
using System.Collections;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CandyPlayers;
using UnityEngine;

namespace DeadlyJenga.DevTools
{
    // Called by an explicit Play Mode validation after spawning an actual enemy.
    public static class JaguarSuiteChecks
    {
        public static int ValidateAppearance(CandyPlayerRig rig)
        {
            int checks=0;
            void Check(bool result,string reason){if(!result)throw new InvalidOperationException("Jaguar: "+reason);checks++;}
            Check(rig&&rig.BoneCount==44,"original 44-bone rig retained");
            var appearance=rig.GetComponent<CandyJaguarAppearance>();
            Check(appearance&&appearance.enabled&&appearance.PieceCount>=20,"complete feline shell attached");
            Check(rig.GetComponentsInChildren<Collider>(true).Length==0,"cosmetics add no physics bodies or colliders");
            Check(CandyJaguarAppearance.CoatTexture&&CandyJaguarAppearance.CoatTexture.width==512,"512px authored rosette coat loaded");
            var properties=new MaterialPropertyBlock();rig.bodyRenderer.GetPropertyBlock(properties);var material=rig.bodyRenderer.sharedMaterial;
            Check(material.HasProperty("_BaseMap")&&(properties.GetTexture("_BaseMap")?properties.GetTexture("_BaseMap"):material.GetTexture("_BaseMap"))==CandyJaguarAppearance.CoatTexture,"body uses rosette coat");
            Check((properties.GetTexture("_BumpMap")||material.GetTexture("_BumpMap"))&&(material.IsKeywordEnabled("_NORMALMAP")||properties.GetFloat("_BumpScale")>0),"fine-fur normal map enabled");
            Check(rig.bodyRenderer.sharedMaterial.shader.isSupported,"fur shader supported on current renderer");
            foreach(var mesh in rig.GetComponentsInChildren<MeshFilter>(true))Check(mesh.sharedMesh&&mesh.sharedMesh.vertexCount>0,"nonempty cosmetic mesh: "+mesh.name);
            return checks;
        }
        public static IEnumerator ValidateHealthFrames(CandyPlayerRig rig,Action<int> completed)
        {
            for(int i=0;i<6;i++)yield return null;
            int checks=ValidateAppearance(rig);
            if(!rig.bodyRenderer.sharedMaterial.HasProperty("_Health"))throw new InvalidOperationException("Jaguar health test requires a health-enabled match and registered actor");
            var properties=new MaterialPropertyBlock();rig.bodyRenderer.GetPropertyBlock(properties);
            if(properties.GetTexture("_BaseMap")!=CandyJaguarAppearance.CoatTexture)throw new InvalidOperationException("Jaguar health effect lost rosette pigment");
            completed?.Invoke(checks+2);
        }
        public static int ValidateSounds()
        {
            int checks=0;
            foreach(var sound in CandyJaguarSounds.Sounds)
            {
                var cue=CandyJaguarSounds.DefaultCue(sound);
                if(cue.variants.Length!=3||cue.bus!=CandyBus.Character||cue.maxVoices>3||cue.cooldown<=0)throw new InvalidOperationException("Jaguar cue mix: "+sound);
                checks++;
                foreach(var clip in cue.variants)
                {
                    var pcm=new float[clip.samples];if(!clip.GetData(pcm,0))throw new InvalidOperationException("Unreadable original jaguar PCM");float peak=0;
                    foreach(float sample in pcm){if(float.IsNaN(sample)||float.IsInfinity(sample)||Mathf.Abs(sample)>.83f)throw new InvalidOperationException("Invalid/clipped jaguar PCM: "+sound);peak=Mathf.Max(peak,Mathf.Abs(sample));}
                    if(peak<.1f||Mathf.Abs(pcm[0])>.001f||Mathf.Abs(pcm[pcm.Length-1])>.01f)throw new InvalidOperationException("Silent/unfaded jaguar cue: "+sound);
                    checks++;
                }
            }
            return checks;
        }
    }
}
