using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    public enum CastleBotRoute { Walk, Jump, Vault, Ability, Drop, Climb }
    public struct CastleBotLanding
    {
        public Transform surface;
        public Vector3 local;
        public Rigidbody body;
        public float score;
        public CastleBotRoute route;
        public Vector3 Point=>surface?surface.TransformPoint(local):Vector3.zero;
    }
    public static class CastleBotNavigation
    {
        public const int Mask=~((1<<Layers.Survivor)|(1<<Layers.Block));
        public static bool Ground(Vector3 point,float distance,out RaycastHit hit)
            =>Physics.Raycast(point,Vector3.down,out hit,distance,Mask,QueryTriggerInteraction.Ignore)&&hit.normal.y>.4f;
        public static void SurfaceSamples(Rigidbody body,BoxCollider box,List<Vector3> points)
        {
            points.Clear();
            for(int face=0;face<6;face++)
            {
                int axis=face/2;float sign=face%2==0?1:-1;
                Vector3 normal=Vector3.zero;normal[axis]=sign;
                if(body.transform.TransformDirection(normal).y<.4f)continue;
                int a=(axis+1)%3,b=(axis+2)%3;
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    Vector3 local=box.center;local[axis]+=box.size[axis]*.5f*sign;
                    local[a]+=box.size[a]*x*.37f;local[b]+=box.size[b]*z*.37f;
                    points.Add(body.transform.TransformPoint(local));
                }
                int longAxis=box.size[a]*body.transform.lossyScale[a]>=box.size[b]*body.transform.lossyScale[b]?a:b;
                int shortAxis=longAxis==a?b:a;
                if(box.size[longAxis]*body.transform.lossyScale[longAxis]>box.size[shortAxis]*body.transform.lossyScale[shortAxis]*1.5f)
                    for(int side=-1;side<=1;side+=2)
                    {
                        var local=box.center;local[axis]+=box.size[axis]*.5f*sign;local[longAxis]+=box.size[longAxis]*side*.185f;
                        points.Add(body.transform.TransformPoint(local));
                    }
            }
        }
        public static bool Room(CastleSurvivor motor,Vector3 feet)
        {
            float r=motor.Capsule.radius*.95f;
            return !Physics.CheckCapsule(feet+Vector3.up*(r+.045f),feet+Vector3.up*(motor.StandingHeight-r+.025f),r,Mask,QueryTriggerInteraction.Ignore);
        }
        public static Vector3 TopPoint(Rigidbody rb,BoxCollider box,float fraction)
        {
            var p=box.center+Vector3.up*box.size.y*.5f;
            if(box.size.x*rb.transform.lossyScale.x>=box.size.z*rb.transform.lossyScale.z)p.x+=box.size.x*fraction;
            else p.z+=box.size.z*fraction;
            return rb.position+rb.rotation*Vector3.Scale(p,rb.transform.lossyScale);
        }
        public static bool Landing(CastleSurvivor motor,Vector3 point,out RaycastHit hit)
            =>Ground(point+Vector3.up*.18f,.38f,out hit)&&Mathf.Abs(hit.point.y-point.y)<.13f&&Room(motor,hit.point+Vector3.up*.035f);
        public static bool Walkable(CastleSurvivor motor,Vector3 start,Vector3 end)
        {
            float distance=Vector3.ProjectOnPlane(end-start,Vector3.up).magnitude;
            int count=Mathf.Max(1,Mathf.CeilToInt(distance/.4f));float previous=start.y;
            for(int i=1;i<=count;i++)
            {
                var p=Vector3.Lerp(start,end,(float)i/count);
                if(!Ground(p+Vector3.up*.25f,.55f,out var hit)||Mathf.Abs(hit.point.y-previous)>Mathf.Max(.18f,distance/count*1.5f))return false;
                previous=hit.point.y;
            }
            return ClearSegment(motor,start+Vector3.up*.06f,end+Vector3.up*.06f);
        }
        public static bool ClearSegment(CastleSurvivor motor,Vector3 start,Vector3 end)
        {
            var d=end-start;float r=motor.Capsule.radius*.9f;
            return d.sqrMagnitude<.00001f||!Physics.CapsuleCast(start+Vector3.up*(r+.025f),start+Vector3.up*(motor.StandingHeight-r),r,d.normalized,d.magnitude,Mask,QueryTriggerInteraction.Ignore);
        }
        public static bool Route(CastleSurvivor motor,Vector3 end,ICastleBotTraversalAbility[] abilities,out CastleBotRoute route)
        {
            if(TryLink(motor,motor.transform.position,end,out route,out _))return true;
            if(abilities!=null)foreach(var ability in abilities)if(ability.CanReach(motor,end)){route=CastleBotRoute.Ability;return true;}
            return false;
        }
        // Evaluates hypothetical graph edges as well as the bot's current position.
        // The speed output is useful for a controlled walk-off to a lower landing.
        public static bool TryLink(CastleSurvivor motor,Vector3 start,Vector3 end,out CastleBotRoute route,out float speed)
        {
            route=CastleBotRoute.Walk;speed=motor.EffectiveWalkSpeed;
            float rise=end.y-start.y,distance=Vector3.ProjectOnPlane(end-start,Vector3.up).magnitude;
            if(distance<.05f||motor.gravity<=0||motor.EffectiveWalkSpeed<=0)return false;
            if(Mathf.Abs(rise)<Mathf.Max(.18f,distance*1.4f)&&Walkable(motor,start,end))return true;
            if(rise>0&&ClimbLink(motor,start,end,out _)){route=CastleBotRoute.Climb;speed=motor.climbSpeed;return true;}
            if(rise>motor.jumpHeight+motor.vaultMaxRise-.15f||rise<-(motor.jumpHeight+motor.StandingHeight*2))return false;
            if(rise<-.3f&&DropArc(motor,start,end,out speed)){route=CastleBotRoute.Drop;return true;}
            if(rise<-.3f&&DropCatch(motor,start,end,out speed)){route=CastleBotRoute.Drop;return true;}
            var rules=MatchRuntime.Current?.RulesFor(motor.gameObject);
            if(rules!=null&&!rules.canJump)return false;
            speed=motor.EffectiveWalkSpeed;
            float v=Mathf.Sqrt(2*motor.gravity*motor.jumpHeight),disc=v*v-2*motor.gravity*rise;
            if(disc>=0)
            {
                float duration=(v+Mathf.Sqrt(disc))/motor.gravity;
                if(distance<=motor.EffectiveWalkSpeed*duration*.96f&&JumpArc(motor,start,end,duration,v))
                {route=CastleBotRoute.Jump;speed=distance/duration;return true;}
            }
            // A real face may be out of reach at takeoff but reachable near the apex.
            // Search the actual airborne intercept instead of requiring a ground-level face.
            if((rules==null||rules.canVault)&&VaultIntercept(motor,start,end,v,out float interceptSpeed)){route=CastleBotRoute.Vault;speed=interceptSpeed;return true;}
            return false;
        }
        public static bool ClimbLink(CastleSurvivor motor,Vector3 start,Vector3 end,out RaycastHit face)
        {
            face=default;
            var rules=MatchRuntime.Current?.RulesFor(motor.gameObject);
            if(!motor.climbingEnabled||motor.CarryingFlag||rules!=null&&(!rules.canClimb||!rules.canVault))return false;
            float rise=end.y-start.y;
            if(!Ground(end+Vector3.up*.18f,.4f,out var top)||top.normal.y<.85f)return false;
            float reach=motor.Capsule.radius+.48f*motor.playerScale;
            float budget=motor.climbSpeed*Mathf.Max(.2f,motor.climbGripSeconds)*Mathf.Max(.15f,motor.GripRemaining)*.65f;
            if(rise<.2f||rise>budget)return false;
            var flat=Vector3.ProjectOnPlane(end-start,Vector3.up);if(flat.magnitude>Mathf.Max(2,reach+1))return false;
            Vector3 direction=flat.normalized;if(direction.sqrMagnitude<.1f)return false;
            if(!Physics.Raycast(start+Vector3.up*motor.StandingHeight*.85f,direction,out face,flat.magnitude+reach,1<<Layers.Surface,QueryTriggerInteraction.Ignore))return false;
            if(Mathf.Abs(face.normal.y)>.55f||!Climbables.TryGet(face.collider,out var climbable)||!climbable.CanClimb)return false;
            if(MatchRuntime.Current!=null&&!MatchRuntime.Current.CanClimb(motor.gameObject,climbable))return false;
            var normal=face.normal;Vector3 basePoint=face.point+normal*(motor.Capsule.radius+.04f);
            basePoint.y=start.y;
            for(float h=motor.StandingHeight*.5f;h<rise-.15f;h+=Mathf.Max(.2f,motor.StandingHeight*.6f))
            {
                Vector3 probe=basePoint+Vector3.up*h;
                bool found=false;
                for(int offset=-1;offset<=1&&!found;offset++)
                    found=Physics.Raycast(probe+Vector3.up*(offset*motor.climbSeamReach*motor.playerScale),-normal,out var hit,reach+.2f,1<<Layers.Surface,QueryTriggerInteraction.Ignore)
                        &&Mathf.Abs(hit.normal.y)<.55f&&Climbables.TryGet(hit.collider,out var owner)&&owner.CanClimb;
                if(!found)return false;
            }
            return true;
        }
        static bool JumpArc(CastleSurvivor motor,Vector3 start,Vector3 end,float duration,float v)
        {
            var last=start+Vector3.up*.06f;
            int steps=Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(start,end)/.3f),8,24);
            for(int i=1;i<=steps;i++)
            {
                float t=duration*i/steps;var p=Vector3.Lerp(start,end,(float)i/steps);p.y=start.y+v*t-.5f*motor.gravity*t*t+.06f;
                if(!ClearSegment(motor,last,p))return false;last=p;
            }
            return true;
        }
        static bool VaultIntercept(CastleSurvivor motor,Vector3 start,Vector3 end,float v,out float speed)
        {
            speed=0;
            var flat=Vector3.ProjectOnPlane(end-start,Vector3.up);var direction=flat.normalized;
            if(!Ground(end+Vector3.up*.18f,.4f,out var top))return false;
            var faceStart=new Vector3(start.x,top.point.y-.1f,start.z);
            if(!Physics.Raycast(faceStart,direction,out var face,flat.magnitude+.05f,Mask,QueryTriggerInteraction.Ignore)||face.collider!=top.collider||Mathf.Abs(face.normal.y)>.35f)return false;
            float faceDistance=Vector3.Dot(face.point-start,direction);
            float standOff=motor.Capsule.radius+.06f;
            if(faceDistance<standOff)return false;
            float horizontal=faceDistance-standOff;
            // Several intercept times allow a catch while ascending OR descending.
            for(int sample=0;sample<7;sample++)
            {
                float t=v/motor.gravity*Mathf.Lerp(.65f,1.65f,sample/6f);
                float y=start.y+v*t-.5f*motor.gravity*t*t,rise=top.point.y-y;
                if(rise<.08f||rise>motor.vaultMaxRise-.04f||horizontal/t>motor.EffectiveWalkSpeed*.96f)continue;
                var intercept=start+direction*horizontal;intercept.y=y;
                var last=start+Vector3.up*.06f;bool clear=true;
                int steps=Mathf.Clamp(Mathf.CeilToInt(horizontal/.3f),6,20);
                for(int i=1;i<=steps;i++)
                {
                    float time=t*i/steps;var p=Vector3.Lerp(start,intercept,(float)i/steps);p.y=start.y+v*time-.5f*motor.gravity*time*time+.06f;
                    if(!ClearSegment(motor,last,p)){clear=false;break;}last=p;
                }
                var inside=face.point+direction*(motor.Capsule.radius+.12f);inside.y=top.point.y+.04f;
                var lifted=new Vector3(intercept.x,inside.y+.06f,intercept.z);
                if(clear&&Room(motor,inside)&&ClearSegment(motor,intercept+Vector3.up*.06f,lifted)&&ClearSegment(motor,lifted,inside+Vector3.up*.06f)){speed=horizontal/t;return true;}
            }
            return false;
        }
        static bool DropArc(CastleSurvivor motor,Vector3 start,Vector3 end,out float speed)
        {
            speed=0;var flat=Vector3.ProjectOnPlane(end-start,Vector3.up);float distance=flat.magnitude;var dir=flat.normalized;
            float edge=0;bool found=false;
            for(float d=.1f;d<distance;d+=.12f)
            {
                if(!Ground(start+dir*d+Vector3.up*.2f,.42f,out var hit)||Mathf.Abs(hit.point.y-start.y)>.2f){edge=Mathf.Max(0,d-.12f);found=true;break;}
            }
            if(!found)return false;
            float clearEdge=edge+motor.Capsule.radius+.04f,fallTime=Mathf.Sqrt(2*(start.y-end.y)/motor.gravity);
            speed=(distance-clearEdge)/Mathf.Max(.01f,fallTime);
            if(speed<.35f||speed>motor.EffectiveWalkSpeed*.96f)return false;
            float leaveTime=clearEdge/speed,total=leaveTime+fallTime;var last=start+Vector3.up*.06f;
            int steps=Mathf.Clamp(Mathf.CeilToInt(distance/.22f),8,28);
            for(int i=1;i<=steps;i++)
            {
                float t=total*i/steps,fall=Mathf.Max(0,t-leaveTime);var p=start+dir*(speed*t);p.y=start.y-.5f*motor.gravity*fall*fall+.06f;
                if(!ClearSegment(motor,last,p))return false;last=p;
            }
            return true;
        }
        static bool DropCatch(CastleSurvivor motor,Vector3 start,Vector3 end,out float speed)
        {
            speed=0;var flat=Vector3.ProjectOnPlane(end-start,Vector3.up);var dir=flat.normalized;
            if(!Ground(end+Vector3.up*.18f,.4f,out var top))return false;
            var faceStart=new Vector3(start.x,top.point.y-.1f,start.z);
            if(!Physics.Raycast(faceStart,dir,out var face,flat.magnitude+.05f,Mask,QueryTriggerInteraction.Ignore)||face.collider!=top.collider||Mathf.Abs(face.normal.y)>.35f)return false;
            float exit=-1;
            for(float d=.1f;d<flat.magnitude;d+=.12f)
                if(!Ground(start+dir*d+Vector3.up*.2f,.42f,out var hit)||Mathf.Abs(hit.point.y-start.y)>.2f){exit=Mathf.Max(0,d-.12f)+motor.Capsule.radius+.04f;break;}
            if(exit<0)return false;
            float catchDistance=Vector3.Dot(face.point-start,dir)-motor.Capsule.radius-.06f;
            if(catchDistance<=exit+.08f)return false;
            for(int sample=0;sample<3;sample++)
            {
                float rise=Mathf.Lerp(.2f,motor.vaultMaxRise*.8f,sample/2f);
                float fallTime=Mathf.Sqrt(2*(start.y-top.point.y+rise)/motor.gravity);
                float trialSpeed=(catchDistance-exit)/fallTime;
                if(trialSpeed<.35f||trialSpeed>motor.EffectiveWalkSpeed*.96f)continue;
                float leave=exit/trialSpeed,total=leave+fallTime;var last=start+Vector3.up*.06f;bool clear=true;
                int steps=Mathf.Clamp(Mathf.CeilToInt(catchDistance/.22f),8,28);
                for(int i=1;i<=steps;i++)
                {
                    float t=total*i/steps,fall=Mathf.Max(0,t-leave);var p=start+dir*(trialSpeed*t);p.y=start.y-.5f*motor.gravity*fall*fall+.06f;
                    if(!ClearSegment(motor,last,p)){clear=false;break;}last=p;
                }
                var intercept=start+dir*catchDistance;intercept.y=top.point.y-rise;
                var inside=face.point+dir*(motor.Capsule.radius+.12f);inside.y=top.point.y+.04f;
                var lifted=new Vector3(intercept.x,inside.y+.06f,intercept.z);
                if(clear&&Room(motor,inside)&&ClearSegment(motor,intercept+Vector3.up*.06f,lifted)&&ClearSegment(motor,lifted,inside+Vector3.up*.06f)){speed=trialSpeed;return true;}
            }
            return false;
        }
        public static bool GroundStep(CastleBotBrain bot,out CastleBotLanding landing)
        {
            landing=default;var motor=bot.Motor;
            if(!motor.Grounded||!motor.HasWalkableIsland||!motor.StandingSurface||motor.StandingSurface.gameObject.layer!=0)return false;
            var delta=Vector3.ProjectOnPlane(bot.TripDestination-bot.transform.position,Vector3.up);if(delta.magnitude<.7f)return false;
            float best=float.NegativeInfinity;bool found=false;
            for(int bearing=0;bearing<5;bearing++)
            {
                float angle=bearing==0?0:(bearing%2==0?1:-1)*((bearing+1)/2)*35;
                for(int length=0;length<3;length++)
                {
                    float step=Mathf.Min(delta.magnitude-.35f,length==0?2.5f:length==1?1.2f:.6f);
                    var probe=bot.transform.position+Quaternion.Euler(0,angle,0)*delta.normalized*step;
                    if(!Ground(probe+Vector3.up*.5f,1,out var floor)||Mathf.Abs(floor.point.y-bot.transform.position.y)>.25f)continue;
                    if(!Room(motor,floor.point+Vector3.up*.04f)||!Walkable(motor,bot.transform.position,floor.point))continue;
                    float score=bot.JourneyProgress(floor.point)-Mathf.Abs(angle)*.01f-bot.Population.ThreatCost(floor.point);
                    if(score<=best)continue;best=score;found=true;
                    landing=new CastleBotLanding{surface=floor.transform,body=floor.rigidbody,local=floor.transform.InverseTransformPoint(floor.point),route=CastleBotRoute.Walk};
                }
            }
            return found;
        }
        public static void Nearby(CastleBotBrain bot,List<CastleBotLanding> candidates)
        {
            candidates.Clear();var motor=bot.Motor;var feet=motor.transform.position;
            var samples=new List<Vector3>(18);
            var toward=Vector3.ProjectOnPlane(bot.TripDestination-feet,Vector3.up).normalized;
            if(toward.sqrMagnitude<.1f)toward=bot.WanderDirection;
            for(int bearing=0;bearing<5;bearing++)
            {
                float angle=bearing==0?0:(bearing%2==0?1:-1)*((bearing+1)/2)*35;
                var direction=Quaternion.Euler(0,angle,0)*toward;
                var probe=feet+direction*2.5f;
                if(!Ground(probe+Vector3.up*.8f,1.5f,out var terrain))continue;
                candidates.Add(new CastleBotLanding{surface=terrain.transform,body=terrain.rigidbody,local=terrain.transform.InverseTransformPoint(terrain.point),score=bot.JourneyProgress(terrain.point)-Mathf.Abs(angle)*.01f-bot.Population.ThreatCost(terrain.point)});
            }
            foreach(var rb in bot.Population.Session.world.Blocks)
            {
                if(!rb||!rb.gameObject.activeInHierarchy)continue;
                if(Mathf.Abs(rb.position.y-feet.y)>6||Vector3.ProjectOnPlane(rb.position-feet,Vector3.up).sqrMagnitude>81)continue;
                var box=rb.GetComponent<BoxCollider>();if(!box)continue;
                SurfaceSamples(rb,box,samples);
                foreach(var p in samples)
                {
                    float distance=Vector3.ProjectOnPlane(p-feet,Vector3.up).magnitude,rise=p.y-feet.y;
                    if(distance<.3f||distance>7||rise>Mathf.Max(motor.jumpHeight+motor.vaultMaxRise-.15f,motor.climbSpeed*motor.climbGripSeconds*.65f)||rise<-(motor.jumpHeight+motor.StandingHeight*2))continue;
                    var personality=bot.Personality;
                    float danger=rb.linearVelocity.magnitude*.8f+rb.angularVelocity.magnitude*2;
                    float threat=bot.Population.ThreatCost(p),crowd=bot.Population.Crowding(p,bot);
                    // No unconditional reward for climbing: local fallbacks should not pin every bot to the roof.
                    float score=-distance*.08f-danger*(1+personality.survivalPriority*3)-threat*(1+personality.survivalPriority*2)-crowd;
                    score+=bot.JourneyProgress(p)*1.1f-bot.RecentStopCost(p)*2-bot.RevisitCost(rb)*.3f;
                    score+=Mathf.PerlinNoise(p.x*.25f+bot.Seed,p.z*.25f+Time.time*.025f)*personality.exploration*1.4f;
                    score+=Vector3.Dot((p-feet).normalized,bot.WanderDirection)*.4f;
                    if(bot.Population.Session.Player.gameObject.activeInHierarchy)score-=Mathf.Min(10,Vector3.Distance(p,bot.Population.Session.Player.transform.position))*.025f*personality.sociability;
                    candidates.Add(new CastleBotLanding{surface=rb.transform,body=rb,local=rb.transform.InverseTransformPoint(p),score=score});
                }
            }
            candidates.Sort((a,b)=>b.score.CompareTo(a.score));
        }
    }
}
