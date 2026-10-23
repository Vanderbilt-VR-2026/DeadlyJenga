using System;
using FlyingHand;
using System.Collections;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Presentation;
using DeadlyJenga.CastlePrototype;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit Play Mode check; the integration harness supplies reporting and screenshot callbacks.
    public static class MeltControlChecks
    {
        public static IEnumerator Play(ISessionInfo session,Action<bool,string> check,Action<CandyMelt,string> capture=null)
        {
            var player=session?.Player;var avatar=player?.gameObject.GetComponent<CandyPlayerAvatar>();
            check(avatar&&avatar.Rig,"An actual player rig is available for the death snapshot");
            if(!avatar||!avatar.Rig)yield break;
            bool suspended=player.InputSuspended,menu=session.MenuOpen;player.InputSuspended=true;
            var original=avatar.Rig.GetComponentsInChildren<Renderer>();var hidden=original.Select(r=>r.forceRenderingOff).ToArray();
            CandyMelt melt=null;var motor=player as CastleSurvivor;
            bool batchMirror=Application.isBatchMode&&!session.IsVR&&motor;
            var savedMode=motor?motor.controlMode:ControlMode.Auto;
            try
            {
                // A batch editor cannot acquire desktop mouse capture. The session correctly reopens
                // its menu in that case, so use its real stationary VR mirror for this frame check.
                if(batchMirror){motor.controlMode=ControlMode.VR;motor.RefreshMode();yield return null;}
                var inspection=CandyThirdPersonInspection.For(session);session.SetMenu(false);
                check(inspection.CanInspect,"Inspection fixture is a living demo survivor with no spectator override");
                inspection.Begin();yield return null;
                check(inspection.Inspecting&&player.InputSuspended,batchMirror?"Synthetic VR mirror opens while preserving the stationary player (batch has no desktop mouse capture)":"Third person opens while preserving the stationary player");
                check(inspection.InspectionCamera&&!inspection.InspectionCamera.GetComponent<AudioListener>(),"Inspection uses a separate camera without a duplicate audio listener");
                inspection.End();
                if(batchMirror){motor.controlMode=savedMode;motor.RefreshMode();yield return null;}
                var existing=Object.FindObjectsByType<CandyMelt>(FindObjectsSortMode.None);
                CandyMelt.Spawn(avatar.Rig.transform,Color.cyan,player.transform.position.y+100);
                melt=Object.FindObjectsByType<CandyMelt>(FindObjectsSortMode.None).FirstOrDefault(m=>!existing.Contains(m));
                check(melt,"A detached cosmetic death snapshot is created");if(!melt)yield break;
                check(melt.MeltProgress==0,"Death starts at zero melt, even if the actor root floor hint is wrong");
                int sourceVertices=avatar.Rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.sharedMesh).Sum(r=>r.sharedMesh.vertexCount);
                check(melt.SkinnedSnapshotVertices==sourceVertices&&melt.SkinnedSnapshotIndices>0,"Death snapshot retains all source skinned vertices and drawable triangle indices");
                check(melt.SkinnedSnapshotBounds.size.y>melt.SnapshotBounds.size.y*.6f,"Skinned body / limbs occupy the full avatar height rather than collapsing into its root");
                check(melt.RagdollConstraintCount>30,"The real procedural 44-bone rig has articulated body and finger constraints");
                check(melt.SnapshotBounds.size.y>.3f&&(melt.CurrentBodyBounds.size-melt.SnapshotBounds.size).sqrMagnitude<.000001f,"Death snapshot preserves full original body dimensions at birth");
                var palette=new MaterialPropertyBlock();avatar.Rig.bodyRenderer.GetPropertyBlock(palette);var coat=palette.GetTexture("_BaseMap")??(avatar.Rig.bodyRenderer.sharedMaterial.HasProperty("_BaseMap")?avatar.Rig.bodyRenderer.sharedMaterial.GetTexture("_BaseMap"):null);
                if(coat)check(melt.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Any(m=>m&&m.HasProperty("_BaseMap")&&m.GetTexture("_BaseMap")==coat),"Death snapshot retains the actual source coat texture at birth");
                check(melt.GetComponentsInChildren<Collider>().Length==0&&melt.GetComponentsInChildren<Rigidbody>().Length==0,"Visual ragdoll creates no gameplay colliders or rigidbodies");
                foreach(var r in original)r.forceRenderingOff=true;
                capture?.Invoke(melt,"Death-Full-Size");
                yield return new WaitForSecondsRealtime(.55f);
                check(melt&&melt.MeltProgress==0,"The body ragdolls before liquefaction begins");
                if(melt)
                {
                    var body=melt.CurrentBodyBounds;var birth=melt.SnapshotBounds;
                    check(Finite(body.center)&&Finite(body.size)&&body.size.sqrMagnitude<birth.size.sqrMagnitude*9,"Ragdoll geometry stays within three times the original body dimensions");
                    check(Vector3.Distance(body.center,birth.center)<Mathf.Max(1,birth.size.y)*3,"Ragdoll stays near its death location during the first half-second");
                }
                if(melt)capture?.Invoke(melt,"Death-Ragdoll");
                float deadline=Time.unscaledTime+6;
                while(melt&&melt.MeltProgress<=0&&Time.unscaledTime<deadline)yield return null;
                check(melt&&melt.MeltProgress>0,"Ragdoll transitions into a gradual melt");
                if(!melt)yield break;
                check(Finite(melt.CurrentBodyBounds.center)&&Finite(melt.CurrentBodyBounds.size),"Articulated corpse remains finite after falling");
                capture?.Invoke(melt,"Death-Melt-Start");
                yield return new WaitForSecondsRealtime(CandyMelt.LiquefySeconds*.55f);
                check(melt&&melt.MeltProgress>.5f&&melt.MeltProgress<1,"Liquefaction progresses over several seconds");
                if(melt)capture?.Invoke(melt,"Death-Melt-Middle");
                yield return new WaitForSecondsRealtime(CandyMelt.LiquefySeconds*.45f+.1f);
                check(melt&&melt.MeltProgress==1,"A wet puddle remains before it dries");
                if(melt)capture?.Invoke(melt,"Death-Puddle");
                yield return new WaitForSecondsRealtime(CandyMelt.PuddleSeconds+CandyMelt.DrySeconds+.2f);
                check(!melt,"Dried corpse snapshot releases itself and owned meshes / materials");
            }
            finally
            {
                if(melt)Object.Destroy(melt.gameObject);
                for(int i=0;i<original.Length;i++)if(original[i])original[i].forceRenderingOff=hidden[i];
                CandyThirdPersonInspection.For(session)?.End();
                if(batchMirror&&motor){motor.controlMode=savedMode;motor.RefreshMode();}
                session.SetMenu(menu);player.InputSuspended=suspended;
            }
        }
        static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
    }
}
