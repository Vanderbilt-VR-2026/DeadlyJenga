using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor : IVaultLookTargetProvider
    {
        public bool TryGetLookVaultTarget(float maxDistance,out Collider surface,out Vector3 edgePoint,out Vector3 landingPoint)
        {
            surface=null;edgePoint=landingPoint=default;
            if(!isActiveAndEnabled||!Capsule||!Capsule.enabled||!viewCamera||InputSuspended||Vaulting||((modeRules!=null&&!modeRules.canVault)||GetComponent<DeadlyJenga.CandyPlayers.CandyEquipment>()?.BlocksTraversal==true))return false;
            if(float.IsNaN(maxDistance)||float.IsInfinity(maxDistance))return false;
            float distance=Mathf.Clamp(maxDistance,0,5);if(distance<.05f)return false;
            var ray=new Ray(viewCamera.transform.position,viewCamera.transform.forward);
            int mask=TraversalCollision.Mask;
            if(!Physics.SphereCast(ray,.09f,out var hit,distance,mask,QueryTriggerInteraction.Ignore)||!(hit.collider is BoxCollider box))return false;
            if(!LookEdge(box,hit.point,out var edge))return false;
            float along=Vector3.Dot(edge-ray.origin,ray.direction);
            if(along<0||Vector3.Distance(ray.origin,edge)>distance||(edge-ray.GetPoint(along)).sqrMagnitude>.3f*.3f)return false;
            Vector3 forward=Vector3.ProjectOnPlane(edge-ray.origin,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)return false;
            Vector3 nearFeet=edge-forward*(Capsule.radius+.18f)-Vector3.up*Mathf.Min(.7f,vaultMaxRise*.6f);
            Vector3 oldGrip=candidateGrip,oldNormal=candidateNormal,oldForward=candidateForward;
            try
            {
                if(!FindLedgeFrom(nearFeet,forward,Vector3.zero,out var top,out var target)||top.collider!=box)return false;
                if(Vector3.Distance(ray.origin,candidateGrip)>distance)return false;
                surface=box;edgePoint=candidateGrip;landingPoint=target+(Feet-transform.position);return true;
            }
            finally{candidateGrip=oldGrip;candidateNormal=oldNormal;candidateForward=oldForward;}
        }
        static bool LookEdge(BoxCollider box,Vector3 point,out Vector3 edge)
        {
            edge=default;var t=box.transform;
            int up=0;float bestUp=0;
            for(int axis=0;axis<3;axis++)
            {
                var direction=Vector3.zero;direction[axis]=1;
                float vertical=Mathf.Abs(t.TransformDirection(direction).y);
                if(vertical>bestUp){up=axis;bestUp=vertical;}
            }
            if(bestUp<.85f)return false;
            var upDirection=Vector3.zero;upDirection[up]=1;
            float sign=t.TransformDirection(upDirection).y>=0?1:-1;
            int a=(up+1)%3,b=(up+2)%3;
            var half=box.size*.5f;var center=box.center+upDirection*(half[up]*sign);
            Vector3 local=t.InverseTransformPoint(point);float best=float.PositiveInfinity;
            for(int side=0;side<4;side++)
            {
                Vector3 p=center;p[a]+=side<2?(side==0?-half[a]:half[a]):Mathf.Clamp(local[a]-center[a],-half[a],half[a]);
                p[b]+=side>=2?(side==2?-half[b]:half[b]):Mathf.Clamp(local[b]-center[b],-half[b],half[b]);
                Vector3 world=t.TransformPoint(p);float cost=(point-world).sqrMagnitude;
                if(cost<best){best=cost;edge=world;}
            }
            return !float.IsPositiveInfinity(best);
        }
    }
}
