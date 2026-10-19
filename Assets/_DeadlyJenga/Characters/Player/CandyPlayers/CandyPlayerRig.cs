using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyPlayers
{
    public sealed class CandyPlayerRig : MonoBehaviour, IMysteryBodySource
    {
        public CandyPlayerStyle style;
        public SkinnedMeshRenderer bodyRenderer,headRenderer;
        public Camera ownerCamera;
        public CandyAvatarCalibration calibration;
        [SerializeField] Transform[] bones;
        [SerializeField] float[] lengths;
        MaterialPropertyBlock colorProperties;
        Material currentMaterial;
        Color currentColor;
        float currentSmoothness;
        int colorIndex=int.MinValue;
        Vector3 previousFeet;
        float walkPhase;
        float locomotionSpeed;
        public Transform LeftPalm=>bones[4];
        public Transform RightPalm=>bones[7];
        public int ColorIndex=>colorIndex;
        public int BoneCount=>bones?.Length??0;
        public IMysteryBody CreateMysteryBody()
        {
            var copy=Instantiate(this);copy.name="Candy mystery body";copy.ownerCamera=null;copy.enabled=false;
            copy.bodyRenderer.enabled=copy.headRenderer.enabled=true;copy.bodyRenderer.SetPropertyBlock(colorProperties);copy.headRenderer.SetPropertyBlock(colorProperties);
            var corpse=copy.gameObject.AddComponent<CandyMysteryBody>();corpse.Initialize(copy.bones,copy.lengths);return corpse;
        }
        void Awake(){if(!bodyRenderer||!bodyRenderer.sharedMesh)BuildModel();}
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera;}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeforeCamera;RenderPipelineManager.endCameraRendering-=AfterCamera;if(headRenderer)headRenderer.shadowCastingMode=ShadowCastingMode.On;}
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {if(headRenderer)headRenderer.shadowCastingMode=camera==ownerCamera?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On;}
        void AfterCamera(ScriptableRenderContext context,Camera camera){if(headRenderer)headRenderer.shadowCastingMode=ShadowCastingMode.On;}
        public void SetColor(int index)
        {
            if(!style)return;var color=style.ColorAt(index);
            if(index==colorIndex&&currentMaterial==style.material&&currentColor==color&&currentSmoothness==style.smoothness)return;
            colorProperties??=new MaterialPropertyBlock();
            bodyRenderer.sharedMaterial=style.material;headRenderer.sharedMaterial=style.material;
            colorProperties.SetColor("_BaseColor",color);colorProperties.SetFloat("_Smoothness",style.smoothness);
            bodyRenderer.SetPropertyBlock(colorProperties);headRenderer.SetPropertyBlock(colorProperties);
            colorIndex=index;currentMaterial=style.material;currentColor=color;currentSmoothness=style.smoothness;
        }
        public void ApplyFrame(CandyAvatarFrame frame,bool grounded=true,float deltaTime=0)
        {
            if(!bodyRenderer)BuildModel();
            if(colorIndex!=int.MinValue)SetColor(colorIndex);
            float scale=Mathf.Max(.1f,frame.scale);
            var actor=GetComponentInParent<IModeActor>();bool downed=actor!=null&&MatchRuntime.Current?.RulesFor(actor.gameObject).downed==true;
            if(downed)
            {
                frame.head=frame.feet+Vector3.up*.62f*scale+frame.headRotation*Vector3.forward*.3f*scale;
                frame.left.position=frame.feet+transform.forward*.5f*scale-transform.right*.23f*scale+Vector3.up*.12f*scale;
                frame.right.position=frame.feet+transform.forward*.5f*scale+transform.right*.23f*scale+Vector3.up*.12f*scale;
            }
            var fit=calibration;
            // Work on the frame copy; the tracked/network poses remain untouched.
            if(fit!=null)
            {
                frame.left.position+=frame.left.rotation*fit.leftPosition*scale;
                frame.left.rotation*=Quaternion.Euler(fit.leftRotation);
                frame.right.position+=frame.right.rotation*fit.rightPosition*scale;
                frame.right.rotation*=Quaternion.Euler(fit.rightRotation);
            }
            Vector3 forward=Vector3.ProjectOnPlane(frame.headRotation*Vector3.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f)forward=transform.forward;
            transform.SetPositionAndRotation(frame.feet,Quaternion.LookRotation(forward));var parentScale=transform.parent?transform.parent.lossyScale:Vector3.one;
            transform.localScale=new Vector3(scale/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),scale/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),scale/Mathf.Max(.001f,Mathf.Abs(parentScale.z)));
            float headY=transform.InverseTransformPoint(frame.head).y;
            float crouch=Mathf.Clamp(headY/1.6f,.4f,1.25f)*(fit?.bodyHeight??1f);
            float width=fit?.shoulderWidth??1f;
            Vector3 torsoOffset=fit?.bodyOffset??Vector3.zero;
            bones[0].localPosition=torsoOffset;bones[0].localRotation=Quaternion.identity;bones[0].localScale=new Vector3(width,crouch,1);
            var headOffset=frame.headRotation*((style?style.headCenterOffset:new Vector3(0,0,-.08f))+(fit?.headOffset??Vector3.zero))+Vector3.down*.14f;
            bones[1].SetPositionAndRotation(frame.head+headOffset*scale,frame.headRotation);
            bones[1].localScale=Vector3.one*(fit?.headSize??1f);
            if(deltaTime>0){locomotionSpeed=Vector3.ProjectOnPlane(frame.feet-previousFeet,Vector3.up).magnitude/deltaTime;previousFeet=frame.feet;walkPhase+=Mathf.Min(locomotionSpeed,6)*deltaTime*6;}
            float speed=locomotionSpeed;
            float stride=grounded?Mathf.Clamp01(speed/2)*.13f:0;
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1,phase=walkPhase+(side==0?0:Mathf.PI);
                Vector3 hip=transform.TransformPoint(torsoOffset+new Vector3(sign*.105f*width,.67f*crouch,0));
                Vector3 foot=transform.TransformPoint(new Vector3(sign*.12f,.075f+Mathf.Max(0,Mathf.Sin(phase))*stride*.55f,Mathf.Cos(phase)*stride+.025f));
                if(!grounded)foot=transform.TransformPoint(new Vector3(sign*.13f,.13f,.015f));
                int thigh=side==0?8:11;
                Vector3 knee=SolveJoint(hip,foot,.31f*scale,.31f*scale,transform.forward+Vector3.down*.1f);
                Segment(thigh,hip,knee);Segment(thigh+1,knee,foot);
                bones[thigh+2].SetPositionAndRotation(foot,transform.rotation);bones[thigh+2].localScale=Vector3.one;
                var hand=side==0?frame.left:frame.right;
                Vector3 shoulder=transform.TransformPoint(torsoOffset+new Vector3(sign*.21f*width,1.255f*crouch,0));
                Vector3 elbow=SolveJoint(shoulder,hand.position,.29f*scale,.27f*scale,-transform.forward*.65f+Vector3.down*.8f+transform.right*sign*.3f);
                int upper=side==0?2:5;
                Segment(upper,shoulder,elbow);Segment(upper+1,elbow,hand.position);
                bones[upper+2].SetPositionAndRotation(hand.position,hand.rotation);bones[upper+2].localScale=Vector3.one;
                var skeleton=hand.skeletonTracked?hand.skeleton:CandyHandSkeleton.Rest(side==0,.12f);
                for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++)
                {
                    Vector3 a=hand.position+hand.rotation*(skeleton.Joint(finger*4+joint)*scale);
                    Vector3 b=hand.position+hand.rotation*(skeleton.Joint(finger*4+joint+1)*scale);
                    Segment(14+side*15+finger*3+joint,a,b,hand.rotation*Vector3.up);
                }
            }
            bodyRenderer.localBounds=new Bounds(new Vector3(0,1,0),new Vector3(6,5,6));
            if(downed){bones[0].localRotation=Quaternion.Euler(65,0,0);bones[0].localPosition=new Vector3(0,.05f,.28f);}
        }
        static Quaternion Orientation(Vector3 delta,Vector3 normal)
        {
            if(delta.sqrMagnitude<.0000001f)return Quaternion.identity;
            var up=delta.normalized;var forward=Vector3.ProjectOnPlane(normal,up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.up,up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.right,up).normalized;
            return Quaternion.LookRotation(forward,up);
        }
        void Segment(int index,Vector3 a,Vector3 b,Vector3? normal=null)
        {
            Vector3 delta=b-a;
            bones[index].SetPositionAndRotation(a,delta.sqrMagnitude>.0000001f?Orientation(delta,normal??transform.forward):transform.rotation);
            float scale=Mathf.Max(.001f,transform.lossyScale.y);
            bones[index].localScale=new Vector3(1,Mathf.Max(.001f,delta.magnitude)/(Mathf.Max(.001f,lengths[index])*scale),1);
        }
        static Vector3 SolveJoint(Vector3 a,Vector3 b,float l1,float l2,Vector3 pole)
        {
            Vector3 delta=b-a;float distance=Mathf.Max(.0001f,delta.magnitude);Vector3 direction=delta/distance;
            float stretch=Mathf.Max(1,distance/(l1+l2)*1.001f);l1*=stretch;l2*=stretch;
            float along=Mathf.Clamp((l1*l1-l2*l2+distance*distance)/(2*distance),0,l1);
            float bend=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            Vector3 perpendicular=Vector3.ProjectOnPlane(pole,direction).normalized;
            if(perpendicular.sqrMagnitude<.01f)perpendicular=Vector3.Cross(direction,Vector3.right).normalized;
            return a+direction*along+perpendicular*bend;
        }
        public void BuildModel()
        {
            bones=new Transform[44];lengths=new float[44];
            Transform Bone(int index,string name,Vector3 start,Vector3 end,Vector3? normal=null)
            {
                var bone=new GameObject(name).transform;bone.SetParent(transform,false);bone.localPosition=start;
                Vector3 delta=end-start;bone.localRotation=delta.sqrMagnitude>.000001f?Orientation(delta,normal??Vector3.forward):Quaternion.identity;
                lengths[index]=Mathf.Max(.001f,delta.magnitude);bones[index]=bone;return bone;
            }
            Bone(0,"Torso",Vector3.zero,Vector3.zero);Bone(1,"Head",new Vector3(0,1.49f,0),new Vector3(0,1.49f,0));
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;string label=side==0?"Left":"Right";int upper=side==0?2:5,thigh=side==0?8:11;
                var shoulder=new Vector3(sign*.21f,1.255f,0);var elbow=new Vector3(sign*.29f,.99f,.035f);var wrist=new Vector3(sign*.3f,.75f,.09f);
                Bone(upper,label+" upper arm",shoulder,elbow);Bone(upper+1,label+" forearm",elbow,wrist);Bone(upper+2,label+" palm",wrist,wrist);
                var hip=new Vector3(sign*.105f,.67f,0);var knee=new Vector3(sign*.115f,.37f,.018f);var ankle=new Vector3(sign*.12f,.075f,.025f);
                Bone(thigh,label+" thigh",hip,knee);Bone(thigh+1,label+" shin",knee,ankle);Bone(thigh+2,label+" foot",ankle,ankle);
                var rest=CandyHandSkeleton.Rest(side==0);
                for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++)
                    Bone(14+side*15+finger*3+joint,label+" finger "+finger+" segment "+joint,wrist+rest.Joint(finger*4+joint),wrist+rest.Joint(finger*4+joint+1),Vector3.up);
            }
            var body=new SkinBuilder(transform,bones);var head=new SkinBuilder(transform,bones);
            body.Torso(0);head.Sphere(1,Vector3.zero,new Vector3(.22f,.22f,.215f),24,40);
            for(int side=0;side<2;side++)
            {
                int upper=side==0?2:5,thigh=side==0?8:11;
                body.Limb(new[]{upper,upper+1,upper+2},new[]{.067f,.051f,.035f});
                body.Append(CandySmoothHandMesh.Build(transform,bones,side));
                body.Limb(new[]{thigh,thigh+1,thigh+2},new[]{.083f,.066f,.055f});
                body.Sphere(thigh+2,new Vector3(0,0,.04f),new Vector3(.092f,.075f,.155f),14,24);
            }
            SkinnedMeshRenderer Renderer(string name,Mesh mesh)
            {
                var go=new GameObject(name);go.transform.SetParent(transform,false);var renderer=go.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh=mesh;renderer.bones=bones;renderer.rootBone=transform;renderer.updateWhenOffscreen=true;renderer.sharedMaterial=style?style.material:null;return renderer;
            }
            bodyRenderer=Renderer("Candy body and shared hands",body.Mesh("Candy body"));headRenderer=Renderer("Candy spherical head",head.Mesh("Candy head"));
        }
        sealed class SkinBuilder
        {
            readonly Transform root;readonly Transform[] bones;
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();readonly List<BoneWeight> weights=new List<BoneWeight>();readonly List<int> triangles=new List<int>();
            public SkinBuilder(Transform root,Transform[] bones){this.root=root;this.bones=bones;}
            void Vertex(int bone,Vector3 point,Vector3 normal,Vector2 texture)
            {
                Matrix4x4 m=root.worldToLocalMatrix*bones[bone].localToWorldMatrix;
                vertices.Add(m.MultiplyPoint3x4(point));normals.Add(m.MultiplyVector(normal).normalized);uv.Add(texture);
                weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});
            }
            void Grid(int start,int rings,int sides)
            {
                for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
                {int a=start+y*(sides+1)+x,b=a+1,c=a+sides+1,d=c+1;triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);}
            }
            public void Sphere(int bone,Vector3 center,Vector3 radii,int rings,int sides)
            {
                int start=vertices.Count;
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
                {
                    float theta=Mathf.PI*y/rings,angle=2*Mathf.PI*x/sides;
                    Vector3 n=new Vector3(Mathf.Sin(theta)*Mathf.Cos(angle),-Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(angle));
                    Vertex(bone,center+Vector3.Scale(n,radii),new Vector3(n.x/radii.x,n.y/radii.y,n.z/radii.z).normalized,new Vector2((float)x/sides,(float)y/rings));
                }
                Grid(start,rings,sides);
            }
            public void Capsule(int bone,float length,float radius,int rings=12,int sides=20)
            {
                int start=vertices.Count;
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
                {
                    float theta=Mathf.PI*y/rings,angle=2*Mathf.PI*x/sides;
                    Vector3 n=new Vector3(Mathf.Sin(theta)*Mathf.Cos(angle),-Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(angle));
                    Vector3 point=n*radius+Vector3.up*(y<=rings/2?0:length);
                    Vertex(bone,point,n,new Vector2((float)x/sides,(float)y/rings));
                }
                Grid(start,rings,sides);
            }
            public void Append(Mesh mesh)
            {
                int start=vertices.Count;vertices.AddRange(mesh.vertices);normals.AddRange(mesh.normals);uv.AddRange(mesh.uv);weights.AddRange(mesh.boneWeights);
                foreach(int index in mesh.triangles)triangles.Add(start+index);
                if(Application.isPlaying)Object.Destroy(mesh);else Object.DestroyImmediate(mesh);
            }
            public void Limb(int[] chain,float[] radii)
            {
                const int rings=24,sides=24;int start=vertices.Count;
                var points=new Vector3[chain.Length];for(int i=0;i<chain.Length;i++)points[i]=root.InverseTransformPoint(bones[chain[i]].position);
                for(int ring=0;ring<=rings;ring++)
                {
                    float t=(float)ring/rings*(chain.Length-1);int segment=Mathf.Min(chain.Length-2,(int)t);float f=t-segment;
                    Vector3 a=points[segment],b=points[segment+1],tangent=(b-a).normalized;
                    if(segment>0&&f<.25f)tangent=Vector3.Lerp((points[segment+1]-points[segment-1]).normalized,tangent,f/.25f).normalized;
                    if(segment<chain.Length-2&&f>.75f)tangent=Vector3.Lerp(tangent,(points[segment+2]-points[segment]).normalized,(f-.75f)/.25f).normalized;
                    Vector3 axis=Vector3.Cross(tangent,Vector3.forward).normalized;if(axis.sqrMagnitude<.1f)axis=Vector3.Cross(tangent,Vector3.right).normalized;
                    Vector3 other=Vector3.Cross(tangent,axis).normalized;
                    float radius=Mathf.Lerp(radii[segment],radii[segment+1],f);
                    radius*=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.12f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((chain.Length-1-t)/.08f));
                    Vector3 center=Vector3.Lerp(a,b,f);
                    // Blending across the joint keeps one continuous tube instead of capsule end caps.
                    float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,f));
                    if(segment>0&&f<.35f)blend=Mathf.SmoothStep(.5f,0,f/.35f);
                    int first=segment>0&&f<.35f?chain[segment-1]:chain[segment];
                    int second=segment>0&&f<.35f?chain[segment]:chain[segment+1];
                    float secondWeight=segment>0&&f<.35f?1-blend:blend*.5f;
                    if(ring==rings){first=chain[chain.Length-1];second=first;secondWeight=0;}
                    for(int x=0;x<=sides;x++)
                    {
                        float angle=2*Mathf.PI*x/sides;Vector3 n=axis*Mathf.Cos(angle)+other*Mathf.Sin(angle);
                        vertices.Add(center+n*radius);normals.Add(n);uv.Add(new Vector2((float)x/sides,(float)ring/rings));
                        weights.Add(new BoneWeight{boneIndex0=first,weight0=1-secondWeight,boneIndex1=second,weight1=secondWeight});
                    }
                }
                // Tube parameterization has the opposite angular orientation to the lathe.
                int indexStart=triangles.Count;Grid(start,rings,sides);
                for(int i=indexStart;i<triangles.Count;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            }
            public void Torso(int bone)
            {
                float[] ys={.53f,.58f,.66f,.79f,.94f,1.08f,1.2f,1.29f,1.34f};
                float[] rs={0,.12f,.18f,.17f,.155f,.2f,.21f,.16f,0};
                const int rings=48,sides=40;int start=vertices.Count;
                float Radius(float t)
                {
                    float f=Mathf.Clamp(t,0,1)*(rs.Length-1);int i=Mathf.Min(rs.Length-2,(int)f);float u=f-i;
                    float p0=rs[Mathf.Max(0,i-1)],p1=rs[i],p2=rs[i+1],p3=rs[Mathf.Min(rs.Length-1,i+2)];
                    return Mathf.Max(0,.5f*((2*p1)+(-p0+p2)*u+(2*p0-5*p1+4*p2-p3)*u*u+(-p0+3*p1-3*p2+p3)*u*u*u));
                }
                for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
                {
                    float t=(float)y/rings,a=2*Mathf.PI*x/sides,f=t*(ys.Length-1);int i=Mathf.Min(ys.Length-2,(int)f);
                    float height=Mathf.Lerp(ys[i],ys[i+1],f-i),r=Radius(t),slope=(Radius(t+.005f)-Radius(t-.005f))/.0081f;
                    Vector3 point=new Vector3(Mathf.Cos(a)*r,height,Mathf.Sin(a)*r*.72f);
                    Vertex(bone,point,new Vector3(Mathf.Cos(a),-slope,Mathf.Sin(a)/.72f).normalized,new Vector2((float)x/sides,t));
                }
                Grid(start,rings,sides);
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();
                var bindings=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)bindings[i]=bones[i].worldToLocalMatrix*root.localToWorldMatrix;
                mesh.bindposes=bindings;mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
