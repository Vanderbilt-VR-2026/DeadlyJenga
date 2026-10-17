using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.CandyPlayers;
namespace DeadlyJenga.CandyPhysicsHands
{
    // No grab detection, object joints, object parenting or object pose writes. Contacts do the work.
    public sealed class CandyPhysicalHand : MonoBehaviour
    {
        public CandyHandSettings settings;
        public bool left;
        public SkinnedMeshRenderer skin;
        public Transform[] visualBones;
        public float[] visualReferenceLengths;
        public Rigidbody[] bodies;
        public float Strength {get;private set;}
        public float MaximumAppliedForce {get;private set;}
        public CandyHandPose Target {get;private set;}
        Collider[] contacts;
        PhysicsMaterial friction;
        float builtSize;
        float[] segmentLengths;
        ConfigurableJoint[] joints;
        Quaternion[] referenceRelative;
        bool active;
        readonly Collider[] activationOverlaps=new Collider[64];
        public bool ActivationBlocked {get;private set;}
        public string ActivationReason {get;private set;}
        public bool IsInteractive=>active;
        public bool NetworkPreview {get;set;}
        bool previousNetworkPreview;
        public float GeometrySize=>builtSize;
        // AI fingers follow their joints instead of pulling independently through a contacted block.
        public bool JointDrivenFingers {get;set;}
        public void RebuildInactive(CandyHandPose pose)
        {
            if(active)throw new System.InvalidOperationException("Release contacts before resizing the hand.");
            if(bodies!=null)foreach(var b in bodies)if(b){if(Application.isPlaying)Destroy(b.gameObject);else DestroyImmediate(b.gameObject);}
            if(friction){if(Application.isPlaying)Destroy(friction);else DestroyImmediate(friction);}
            Build(pose);
        }
        public void SetTarget(CandyHandPose target,float strength){Target=target;Strength=Mathf.Clamp01(strength);}
        public void Build(CandyHandPose initial)
        {
            builtSize=settings.size;Target=initial;contacts=new Collider[16];bodies=new Rigidbody[16];segmentLengths=new float[15];joints=new ConfigurableJoint[15];referenceRelative=new Quaternion[15];
            friction=new PhysicsMaterial("Candy contact friction"){staticFriction=settings.staticFriction,dynamicFriction=settings.dynamicFriction,frictionCombine=PhysicsMaterialCombine.Average,bounciness=0};
            for(int i=0;i<16;i++)
            {
                var go=new GameObject(i==0?"Physical palm":"Physical finger "+(i-1)/3+" joint "+(i-1)%3);go.transform.SetParent(transform,false);go.layer=0;
                var rb=go.AddComponent<Rigidbody>();rb.mass=Mathf.Max(.001f,i==0?settings.palmMass:settings.fingerMass);rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.solverIterations=settings.solverIterations;rb.solverVelocityIterations=settings.solverVelocityIterations;rb.maxAngularVelocity=settings.maxAngularSpeed;rb.maxLinearVelocity=settings.maxSpeed;rb.maxDepenetrationVelocity=1;
                bodies[i]=rb;
                if(i==0){var col=go.AddComponent<BoxCollider>();col.center=new Vector3(0,0,.020f)*builtSize;col.size=new Vector3(.078f,.04f,.078f)*builtSize;contacts[i]=col;}
                else
                {
                    int f=(i-1)/3,j=(i-1)%3;Vector3 a=initial.skeleton.Joint(f*4+j),b=initial.skeleton.Joint(f*4+j+1);segmentLengths[i-1]=Mathf.Max(.005f,(b-a).magnitude);
                    float radius=((f==0?.011f:.0078f)-j*.001f)*builtSize,length=segmentLengths[i-1]*builtSize;
                    if(j==2)
                    {
                        // A broad fingertip contact patch resists rolling/ejection; the rendered skin stays rounded.
                        var pad=go.AddComponent<BoxCollider>();pad.center=Vector3.up*length*.5f;pad.size=new Vector3(radius*1.8f,length+radius*.5f,radius*1.5f);contacts[i]=pad;
                    }
                    else{var col=go.AddComponent<CapsuleCollider>();col.direction=1;col.radius=radius;col.height=length+2*radius;col.center=Vector3.up*length*.5f;contacts[i]=col;}
                }
                contacts[i].sharedMaterial=friction;contacts[i].contactOffset=Mathf.Clamp(.0005f*builtSize,.0001f,.02f);
            }
            ResetInactive(initial); // Safe only before activating contacts.
            for(int i=1;i<16;i++)
            {
                int j=(i-1)%3;var joint=bodies[i].gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=bodies[j==0?0:i-1];joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;
                joint.connectedAnchor=Quaternion.Inverse(joint.connectedBody.rotation)*(bodies[i].position-joint.connectedBody.position);
                joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
                joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Free;
                if(JointDrivenFingers)
                {
                    joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
                    joint.lowAngularXLimit=new SoftJointLimit{limit=-80};
                    joint.highAngularXLimit=new SoftJointLimit{limit=80};
                    float swing=i<=3?40:20;
                    joint.angularYLimit=new SoftJointLimit{limit=swing};joint.angularZLimit=new SoftJointLimit{limit=swing};
                }
                joint.enableCollision=false;joint.enablePreprocessing=true;joint.projectionMode=JointProjectionMode.None;
                joint.rotationDriveMode=RotationDriveMode.Slerp;joint.configuredInWorldSpace=false;joints[i-1]=joint;
                referenceRelative[i-1]=Quaternion.Inverse(joint.connectedBody.rotation)*bodies[i].rotation;
            }
            // Internal contacts produce jitter, never gripping force. Ignore only this hand's pairs.
            for(int i=0;i<16;i++)for(int j=i+1;j<16;j++)Physics.IgnoreCollision(contacts[i],contacts[j]);
            if(skin)skin.sharedMaterial=settings.skin;active=true;SetInteractive(false);
        }
        public static Quaternion SegmentRotation(Vector3 delta,Vector3 normal)
        {var forward=Vector3.ProjectOnPlane(normal,delta.normalized);if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(Vector3.right,delta.normalized);return Quaternion.LookRotation(forward.normalized,delta.normalized);}
        public void ResetInactive(CandyHandPose pose)
        {
            if(active)throw new System.InvalidOperationException("Cannot teleport an interacting physical hand.");
            for(int i=0;i<bodies.Length;i++)
            {
                Desired(pose,i,out var position,out var rotation);
                bodies[i].transform.SetPositionAndRotation(position,rotation);bodies[i].position=position;bodies[i].rotation=rotation;if(!bodies[i].isKinematic){bodies[i].linearVelocity=Vector3.zero;bodies[i].angularVelocity=Vector3.zero;}
            }
        }
        void Desired(CandyHandPose pose,int i,out Vector3 position,out Quaternion rotation)
        {
            position=pose.position;rotation=pose.rotation;if(i==0)return;
            int f=(i-1)/3,j=(i-1)%3;Vector3 a=pose.skeleton.Joint(f*4+j),b=pose.skeleton.Joint(f*4+j+1);
            position+=pose.rotation*a*builtSize;rotation=SegmentRotation(pose.rotation*(b-a),pose.rotation*Vector3.up);
        }
        public void SetInteractive(bool value)
        {
            if(contacts==null)return;
            if(previousNetworkPreview!=NetworkPreview)
            {
                foreach(var c in contacts)c.enabled=false;
                foreach(var b in bodies){if(!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}b.isKinematic=true;}
                active=false;previousNetworkPreview=NetworkPreview;
            }
            if(NetworkPreview)
            {
                active=value;if(skin)skin.enabled=value;
                foreach(var c in contacts)c.enabled=false;
                foreach(var b in bodies)b.isKinematic=true;
                return;
            }
            if(active==value){if(skin)skin.enabled=active;return;}
            if(value&&!CanActivate()){ActivationBlocked=true;if(skin)skin.enabled=false;return;}ActivationBlocked=false;active=value;if(skin)skin.enabled=value;
            foreach(var c in contacts)c.enabled=value;
            foreach(var b in bodies){if(!value&&!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}b.isKinematic=!value;}
        }
        public bool CanActivate()
        {
            ActivationReason="";
            var scene=gameObject.scene.GetPhysicsScene();
            foreach(var c in contacts)
            {
                bool wasEnabled=c.enabled;c.enabled=true;
                try
                {
                    int count=scene.OverlapSphere(c.transform.position,.18f*builtSize,activationOverlaps,~0,QueryTriggerInteraction.Ignore);
                    if(count==activationOverlaps.Length){ActivationReason="Overlap query full";return false;}
                    for(int i=0;i<count;i++)
                    {
                        var other=activationOverlaps[i];if(other.transform.IsChildOf(transform)||Physics.GetIgnoreLayerCollision(c.gameObject.layer,other.gameObject.layer))continue;
                        if(Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.001f*builtSize){ActivationReason=c.name+" intersects "+other.name+" by "+depth.ToString("F5")+"m";return false;}
                    }
                }
                finally{c.enabled=wasEnabled;}
            }
            return true;
        }
        void FixedUpdate(){StepDrives(Time.fixedDeltaTime);}
        public void StepDrives(float dt)
        {
            if(!settings||bodies==null)return;
            if(NetworkPreview)
            {
                for(int i=0;i<bodies.Length;i++)
                {Desired(Target,i,out var p,out var q);bodies[i].MovePosition(p);bodies[i].MoveRotation(q);}
                return;
            }
            if(!active)
            {
                if(Strength>0)for(int i=0;i<bodies.Length;i++)
                {Desired(Target,i,out var p,out var q);bodies[i].MovePosition(Vector3.MoveTowards(bodies[i].position,p,settings.maxSpeed*dt));bodies[i].MoveRotation(Quaternion.RotateTowards(bodies[i].rotation,q,settings.maxAngularSpeed*Mathf.Rad2Deg*dt));}
                return;
            }
            MaximumAppliedForce=0;
            friction.staticFriction=settings.staticFriction;friction.dynamicFriction=settings.dynamicFriction;
            for(int i=0;i<bodies.Length;i++)
            {
                var rb=bodies[i];Desired(Target,i,out var p,out var q);
                float denominator=1+dt*settings.positionDamping/rb.mass+dt*dt*settings.positionSpring/rb.mass;
                Vector3 force=Vector3.ClampMagnitude(((p-rb.position)*settings.positionSpring-rb.linearVelocity*settings.positionDamping)/denominator,(i==0?settings.palmForce:settings.fingerForce)*Strength);
                if(i==0||!JointDrivenFingers){rb.AddForce(force);MaximumAppliedForce=Mathf.Max(MaximumAppliedForce,force.magnitude);}
                if(i>0)
                {
                    // Drive the anatomical rotation relative to its connected hand bone.
                    // A constant world target on a rotating chain effectively counts the
                    // parent's motion twice and can turn fingers away from the tracked pose.
                    int parent=(i-1)%3==0?0:i-1;
                    Desired(Target,parent,out _,out var parentRotation);
                    Quaternion relative=Quaternion.Inverse(parentRotation)*q;
                    joints[i-1].targetRotation=Quaternion.Inverse(relative)*referenceRelative[i-1];
                    joints[i-1].slerpDrive=new JointDrive{positionSpring=settings.rotationSpring,positionDamper=settings.rotationDamping,maximumForce=settings.fingerTorque*Strength};
                    continue;
                }
                var error=q*Quaternion.Inverse(rb.rotation);if(error.w<0)error=new Quaternion(-error.x,-error.y,-error.z,-error.w);error.ToAngleAxis(out float angle,out Vector3 axis);if(angle>180)angle-=360;
                if(axis.sqrMagnitude>.001f)
                {
                    Quaternion inertiaFrame=rb.rotation*rb.inertiaTensorRotation;
                    Vector3 e=Quaternion.Inverse(inertiaFrame)*(axis*angle*Mathf.Deg2Rad),w=Quaternion.Inverse(inertiaFrame)*rb.angularVelocity;
                    Vector3 inertia=rb.inertiaTensor,torque=Vector3.zero;
                    for(int n=0;n<3;n++)torque[n]=(e[n]*settings.rotationSpring-w[n]*settings.rotationDamping)/(1+dt*settings.rotationDamping/Mathf.Max(.00000001f,inertia[n])+dt*dt*settings.rotationSpring/Mathf.Max(.00000001f,inertia[n]));
                    rb.AddTorque(Vector3.ClampMagnitude(inertiaFrame*torque,(i==0?settings.palmTorque:settings.fingerTorque)*Strength));
                }
            }
        }
        void LateUpdate()
        {
            if(!skin||visualBones==null||bodies==null)return;
            var rest=CandyHandSkeleton.Rest(left);
            for(int i=0;i<16;i++)
            {
                visualBones[i].SetPositionAndRotation(bodies[i].transform.position,bodies[i].transform.rotation);var scale=Vector3.one*builtSize;
                if(i>0){int f=(i-1)/3,j=(i-1)%3;float reference=visualReferenceLengths!=null&&visualReferenceLengths.Length==15?visualReferenceLengths[i-1]:(rest.Joint(f*4+j+1)-rest.Joint(f*4+j)).magnitude;scale.y*=segmentLengths[i-1]/Mathf.Max(.001f,reference);}
                visualBones[i].localScale=scale;
            }
            var bounds=new Bounds(bodies[0].position,Vector3.one*(.3f*builtSize));
            for(int i=1;i<16;i++){bounds.Encapsulate(bodies[i].position);bounds.Encapsulate(bodies[i].position+bodies[i].rotation*(Vector3.up*segmentLengths[i-1]*builtSize));}
            bounds.Expand(.08f*builtSize);skin.localBounds=new Bounds(transform.InverseTransformPoint(bounds.center),bounds.size);
        }
        void OnDisable(){SetInteractive(false);}
        void OnDestroy(){if(friction){if(Application.isPlaying)Destroy(friction);else DestroyImmediate(friction);}}
    }
}
