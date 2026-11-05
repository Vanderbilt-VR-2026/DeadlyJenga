using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class CandyDisasterVisuals
    {
        Mesh fireCube,fireTongueMesh,fireLowTongueMesh;Material fireShell,fireTongue,charredMaterial,smoulderMaterial;
        Shader wetOverlayShader;
        const int FlameTriangleBudget=1536,MaxFlameSubjects=192,MaxNearFireBodies=4;
        const int DetailedTongueTriangles=60,CheapTongueTriangles=4,MaxDetailedTonguesPerBody=4;
        struct FlameSubject
        {
            public Rigidbody body;public int id;public Bounds bounds;public float distanceSquared;public bool preferNear,highDetail;
        }
        readonly List<FlameSubject> flameSubjects=new List<FlameSubject>(MaxFlameSubjects);
        readonly List<Matrix4x4> fireLowTips=new List<Matrix4x4>(384);
        readonly HashSet<Rigidbody> nearFireBodies=new HashSet<Rigidbody>();
        readonly List<FlameSubject> nearFlameSubjects=new List<FlameSubject>(MaxNearFireBodies);
        readonly List<Rigidbody> oldNearFireBodies=new List<Rigidbody>(MaxNearFireBodies);
        readonly Dictionary<Rigidbody,int> detailedFireCounts=new Dictionary<Rigidbody,int>(MaxNearFireBodies);
        static readonly System.Comparison<FlameSubject> flameDistanceOrder=CompareFlameDistance,flameDrawOrder=CompareFlameDrawOrder;
        public int FireTongueTriangleCount {get;private set;}
        public int DetailedFireTongueCount=>fireTips.Count;
        public int CheapFireTongueCount=>fireLowTips.Count;
        public int FireShellCount=>fireShells.Count;
        public int FlameSubjectCount=>flameSubjects.Count;
        readonly List<Matrix4x4> fireShells=new List<Matrix4x4>(512),fireTips=new List<Matrix4x4>(384);
        readonly List<Matrix4x4> smoulders=new List<Matrix4x4>(192);
        sealed class CharredLook
        {
            public Renderer renderer;public Material[] originals,replacements;public MaterialPropertyBlock originalsProps;
            public Color color;public float burn,heat,appliedBurn=-1,appliedHeat=-1;
        }
        readonly Dictionary<Renderer,CharredLook> charred=new Dictionary<Renderer,CharredLook>();
        readonly Dictionary<Rigidbody,Renderer[]> burnRenderers=new Dictionary<Rigidbody,Renderer[]>();
        readonly HashSet<Renderer> seenCharred=new HashSet<Renderer>();
        readonly List<Renderer> oldCharred=new List<Renderer>();
        MaterialPropertyBlock burnProps;IWorldQuery fireWorld;Rigidbody fireFirst;
        void InitializeFire()
        {
            fireCube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            fireTongueMesh=CreateBlockFlameMesh();
            fireLowTongueMesh=CreateCheapBlockFlameMesh();
            wetOverlayShader=Resources.Load<Shader>("CandyScenery/CandyWetSurface");
            fireShell=ObjectMaterial("CandyBlockFire",Color.white);
            fireTongue=new Material(fireShell){name="Candy flame tongues",enableInstancing=true};fireTongue.SetFloat("_Tongue",1);fireTongue.SetFloat("_Cull",2);
            charredMaterial=ObjectMaterial("CandyCharredBlock",Color.white);
            smoulderMaterial=ObjectMaterial("CandyDisasterSmoke",new Color(.24f,.23f,.22f,.19f));burnProps=new MaterialPropertyBlock();
        }
        static Mesh CreateBlockFlameMesh()
        {
            const int sides=6,bands=4;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            var radii=new[]{.30f,.43f,.33f,.17f,.015f};
            var offsets=new[]{Vector2.zero,new Vector2(-.04f,.02f),new Vector2(.04f,-.03f),new Vector2(.10f,.015f),new Vector2(.17f,.03f)};
            for(int row=0;row<=bands;row++)for(int side=0;side<=sides;side++)
            {
                float angle=side*Mathf.PI*2/sides;
                vertices.Add(new Vector3(offsets[row].x+Mathf.Cos(angle)*radii[row],row/(float)bands-.5f,offsets[row].y+Mathf.Sin(angle)*radii[row]));
                uv.Add(new Vector2(side/(float)sides,row/(float)bands));
            }
            for(int row=0;row<bands;row++)for(int side=0;side<sides;side++)
            {
                int a=row*(sides+1)+side,b=a+sides+1;
                triangles.AddRange(new[]{a,b,b+1,a,b+1,a+1});
            }
            for(int end=0;end<2;end++)
            {
                int row=end*bands,center=vertices.Count;
                vertices.Add(new Vector3(offsets[row].x,end-.5f,offsets[row].y));uv.Add(new Vector2(.5f,end));
                for(int side=0;side<sides;side++)
                {vertices.Add(vertices[row*(sides+1)+side]);uv.Add(new Vector2(side/(float)sides,end));}
                for(int side=0;side<sides;side++)
                {
                    int a=center+1+side,b=center+1+(side+1)%sides;
                    if(end==0)triangles.AddRange(new[]{center,a,b});else triangles.AddRange(new[]{center,b,a});
                }
            }
            var mesh=new Mesh{name="Shared tapered candy flame"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            var normals=mesh.normals;
            for(int row=0;row<=bands;row++)
            {
                int first=row*(sides+1),last=first+sides;var normal=(normals[first]+normals[last]).normalized;
                normals[first]=normals[last]=normal;
            }
            mesh.normals=normals;mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.5f);mesh.bounds=bounds;
            return mesh;
        }
        static Mesh CreateCheapBlockFlameMesh()
        {
            var points=new[]{new Vector3(.4f,-.5f,0),new Vector3(-.2f,-.5f,.34641016f),new Vector3(-.2f,-.5f,-.34641016f),new Vector3(.14f,.5f,.015f)};
            var order=new[]{0,3,1,1,3,2,2,3,0,0,1,2};
            var vertices=new Vector3[order.Length];var uv=new Vector2[order.Length];var triangles=new int[order.Length];
            for(int i=0;i<order.Length;i++)
            {vertices[i]=points[order[i]];uv[i]=new Vector2(i%3*.5f,vertices[i].y+.5f);triangles[i]=i;}
            var mesh=new Mesh{name="Shared cheap tapered candy flame",vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.5f);mesh.bounds=bounds;
            return mesh;
        }
        void BeginFire()
        {
            var world=Services.World;var first=world!=null&&world.Blocks.Count>0?world.Blocks[0]:null;
            if(!ReferenceEquals(fireWorld,world)||fireFirst!=first){ClearCharred();fireWorld=world;fireFirst=first;}
            fireShells.Clear();fireTips.Clear();fireLowTips.Clear();flameSubjects.Clear();smoulders.Clear();seenCharred.Clear();FireTongueTriangleCount=0;
        }
        void FireSurfaces(DisasterFrame frame)
        {
            var world=Services.World;if(world==null)return;
            float lag=Mathf.Clamp(Time.time-frame.sampledAt,0,.35f);
            float lifetime=frame.values!=null&&frame.values.Length>5?Mathf.Max(1,frame.values[5]):30;
            foreach(var burn in frame.burns)
            {
                if(burn.block<0||burn.block>=world.Blocks.Count)continue;
                var body=world.Blocks[burn.block];if(!body||!body.gameObject.activeInHierarchy)continue;
                float remaining=Mathf.Max(0,burn.flameRemaining-lag),smokeRemaining=Mathf.Max(0,burn.smokeRemaining-lag);
                if(remaining>0&&!frame.residualOnly)Engulf(body,burn.block);
                float amount=remaining>0?Mathf.Clamp01((1-remaining/lifetime)*1.5f):1;
                if(!burnRenderers.TryGetValue(body,out var renderers))
                {
                    var visual=body.GetComponent<IClimbable>()?.Visual;
                    renderers=visual?visual.GetComponentsInChildren<Renderer>(true):System.Array.Empty<Renderer>();burnRenderers[body]=renderers;
                }
                foreach(var renderer in renderers)
                {
                    if(!renderer||!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;
                    if(IsWetOverlay(renderer))continue;
                    seenCharred.Add(renderer);
                    if(!charred.TryGetValue(renderer,out var look))charred[renderer]=look=new CharredLook{renderer=renderer};
                    look.burn=amount;look.heat=remaining>0?1:smokeRemaining>0?.08f:0;
                }
                var view=Services.Session?.ActiveCamera;
                if(smokeRemaining>0&&smoulders.Count<192&&(!view||(view.transform.position-body.position).sqrMagnitude<3025))
                {
                    var collider=body.GetComponent<Collider>();if(!collider)continue;var bounds=collider.bounds;
                    float phase=Mathf.Repeat(Time.time*.28f+Hash(burn.block+271),1),width=Mathf.Clamp(bounds.size.magnitude*.15f,.3f,1.3f);
                    float fade=Mathf.Clamp01(smokeRemaining/3);
                    Puff(smoulders,bounds.center+Vector3.up*(bounds.extents.y+.2f+phase*2.5f),width*(.6f+phase)*fade,width*(1+phase)*fade,burn.block*31);
                }
            }
        }
        bool IsWetOverlay(Renderer renderer)
        {
            if(!wetOverlayShader)return false;
            var current=renderer.sharedMaterial;
            if(current&&current.shader==wetOverlayShader)return true;
            return charred.TryGetValue(renderer,out var look)&&look.originals!=null&&look.originals.Length>0&&
                look.originals[0]&&look.originals[0].shader==wetOverlayShader;
        }
        void Engulf(Rigidbody body,int id)
        {
            var collider=body.GetComponent<Collider>();if(!collider)return;
            var bounds=collider.bounds;
            if(collider is BoxCollider box)
            {
                var scale=box.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                fireShells.Add(Matrix4x4.TRS(box.transform.TransformPoint(box.center),box.transform.rotation,Vector3.Scale(box.size,scale)+Vector3.one*.14f));
            }
            else fireShells.Add(Matrix4x4.TRS(bounds.center,Quaternion.identity,bounds.size+Vector3.one*.14f));
            var view=Services.Session?.ActiveCamera;
            float distanceSquared=view?(view.transform.position-bounds.center).sqrMagnitude:float.PositiveInfinity;
            if(view&&distanceSquared>3025)return;
            QueueFlameSubject(new FlameSubject{body=body,id=id,bounds=bounds,distanceSquared=distanceSquared,preferNear=nearFireBodies.Contains(body)&&distanceSquared<256});
            float width=Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.24f,.35f,1.8f);
            if(smokePuffs.Count<512)
            {
                float phase=Mathf.Repeat(Time.time*.28f+Hash(id+81),1);
                Puff(smokePuffs,bounds.center+Vector3.up*(bounds.extents.y+phase*3),width*(1+phase),width*(1.4f+phase),id*31);
            }
        }
        static int CompareFlameDistance(FlameSubject a,FlameSubject b)
        {int distance=a.distanceSquared.CompareTo(b.distanceSquared);return distance!=0?distance:a.id.CompareTo(b.id);}
        static int CompareFlamePriority(FlameSubject a,FlameSubject b)
        {return a.preferNear!=b.preferNear?(a.preferNear?-1:1):CompareFlameDistance(a,b);}
        static int CompareFlameDrawOrder(FlameSubject a,FlameSubject b)
        {return a.highDetail!=b.highDetail?(a.highDetail?-1:1):CompareFlameDistance(a,b);}
        // The farthest candidate stays at the heap root, so later fire frames can replace it.
        void QueueFlameSubject(FlameSubject subject)
        {
            if(flameSubjects.Count<MaxFlameSubjects)
            {
                int child=flameSubjects.Count;flameSubjects.Add(subject);
                while(child>0)
                {
                    int parent=(child-1)/2;
                    if(CompareFlamePriority(flameSubjects[parent],subject)>=0)break;
                    flameSubjects[child]=flameSubjects[parent];child=parent;
                }
                flameSubjects[child]=subject;return;
            }
            if(CompareFlamePriority(subject,flameSubjects[0])>=0)return;
            int index=0;
            while(index*2+1<flameSubjects.Count)
            {
                int child=index*2+1;
                if(child+1<flameSubjects.Count&&CompareFlamePriority(flameSubjects[child+1],flameSubjects[child])>0)child++;
                if(CompareFlamePriority(subject,flameSubjects[child])>=0)break;
                flameSubjects[index]=flameSubjects[child];index=child;
            }
            flameSubjects[index]=subject;
        }
        void BuildBudgetedFlames()
        {
            fireTips.Clear();fireLowTips.Clear();FireTongueTriangleCount=0;detailedFireCounts.Clear();oldNearFireBodies.Clear();nearFlameSubjects.Clear();
            foreach(var body in nearFireBodies)
            {
                bool present=false;FlameSubject nearest=default;
                foreach(var subject in flameSubjects)if(subject.body==body&&subject.distanceSquared<256)
                {if(!present||CompareFlameDistance(subject,nearest)<0)nearest=subject;present=true;}
                if(present)nearFlameSubjects.Add(nearest);else oldNearFireBodies.Add(body);
            }
            foreach(var body in oldNearFireBodies)nearFireBodies.Remove(body);
            flameSubjects.Sort(flameDistanceOrder);
            foreach(var subject in flameSubjects)
            {
                if(subject.distanceSquared>=144)break;
                if(nearFireBodies.Contains(subject.body))continue;
                if(nearFireBodies.Count<MaxNearFireBodies)
                {nearFireBodies.Add(subject.body);nearFlameSubjects.Add(subject);continue;}
                int farthest=0;
                for(int i=1;i<nearFlameSubjects.Count;i++)
                    if(CompareFlameDistance(nearFlameSubjects[i],nearFlameSubjects[farthest])>0)farthest=i;
                float farDistance=Mathf.Sqrt(nearFlameSubjects[farthest].distanceSquared);
                float closerLimit=farDistance-2;
                if(farDistance>=2&&subject.distanceSquared<=closerLimit*closerLimit)
                {
                    nearFireBodies.Remove(nearFlameSubjects[farthest].body);
                    nearFlameSubjects[farthest]=subject;nearFireBodies.Add(subject.body);
                }
            }
            for(int i=0;i<flameSubjects.Count;i++)
            {var subject=flameSubjects[i];subject.highDetail=nearFireBodies.Contains(subject.body);flameSubjects[i]=subject;}
            flameSubjects.Sort(flameDrawOrder);
            foreach(var subject in flameSubjects)
            {
                var bounds=subject.bounds;int id=subject.id;
                float width=Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.24f,.35f,1.8f);
                float perimeter=2*(bounds.size.x+bounds.size.z);
                const int tips=6;
                for(int j=0;j<tips&&fireTips.Count+fireLowTips.Count<512;j++)
                {
                    detailedFireCounts.TryGetValue(subject.body,out int used);
                    bool highAnchor=j==0||j==2||j==3||j==5;
                    bool detailed=subject.highDetail&&highAnchor&&used<MaxDetailedTonguesPerBody&&FireTongueTriangleCount+DetailedTongueTriangles<=FlameTriangleBudget;
                    int triangles=detailed?DetailedTongueTriangles:CheapTongueTriangles;
                    if(FireTongueTriangleCount+triangles>FlameTriangleBudget)break;
                    float distance=Mathf.Repeat((j+Hash(id)*.3f)/tips*perimeter,perimeter);
                    Vector3 edge;
                    if(distance<bounds.size.x)edge=new Vector3(-bounds.extents.x+distance,0,-bounds.extents.z);
                    else if((distance-=bounds.size.x)<bounds.size.z)edge=new Vector3(bounds.extents.x,0,-bounds.extents.z+distance);
                    else if((distance-=bounds.size.z)<bounds.size.x)edge=new Vector3(bounds.extents.x-distance,0,bounds.extents.z);
                    else edge=new Vector3(-bounds.extents.x,0,bounds.extents.z-(distance-bounds.size.x));
                    float phase=Mathf.Repeat(Time.time*(1.1f+Hash(j+id)*.7f)+Hash(id*13+j),1);
                    float height=Mathf.Max(.8f,bounds.size.y*.9f+width*1.7f)*(.75f+Hash(id*31+j)*.5f);
                    var position=bounds.center+edge*1.01f+Vector3.up*(-bounds.extents.y+height*.48f+phase*.10f);
                    var matrix=Matrix4x4.TRS(position,Quaternion.Euler(0,Hash(id*53+j)*360,0),new Vector3(width*(.8f+.2f*Mathf.Sin(Time.time*7+j)),height,width));
                    if(detailed){fireTips.Add(matrix);detailedFireCounts[subject.body]=used+1;}else fireLowTips.Add(matrix);
                    FireTongueTriangleCount+=triangles;
                }
            }
        }
        void RenderFire()
        {
            BuildBudgetedFlames();
            var lighting=GetComponent<CandyHazardLighting>();
            oldCharred.Clear();
            foreach(var pair in charred)
            {
                var renderer=pair.Key;var look=pair.Value;
                if(!renderer||!seenCharred.Contains(renderer)){RestoreCharred(look);oldCharred.Add(renderer);continue;}
                bool replaced=renderer.sharedMaterial!=charredMaterial;
                if(replaced)
                {
                    look.originals=renderer.sharedMaterials;
                    if(lighting)for(int i=0;i<look.originals.Length;i++)look.originals[i]=lighting.ResolveSourceMaterial(look.originals[i]);
                    look.originalsProps=new MaterialPropertyBlock();renderer.GetPropertyBlock(look.originalsProps);
                    var original=look.originals.Length>0?look.originals[0]:null;look.color=original&&original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):Color.white;
                    if(look.originalsProps.HasColor("_BaseColor"))look.color=look.originalsProps.GetColor("_BaseColor");
                    look.replacements=new Material[look.originals.Length];for(int i=0;i<look.replacements.Length;i++)look.replacements[i]=charredMaterial;
                    renderer.sharedMaterials=look.replacements;
                }
                if(replaced||Mathf.Abs(look.appliedBurn-look.burn)>.004f||Mathf.Abs(look.appliedHeat-look.heat)>.004f)
                {
                    renderer.GetPropertyBlock(burnProps);burnProps.SetColor("_BaseColor",look.color);burnProps.SetFloat("_BurnAmount",look.burn);burnProps.SetFloat("_Heat",look.heat);renderer.SetPropertyBlock(burnProps);
                    look.appliedBurn=look.burn;look.appliedHeat=look.heat;
                }
            }
            foreach(var renderer in oldCharred)charred.Remove(renderer);
            DrawObjects(fireShells,fireCube,fireShell);DrawObjects(fireTips,fireTongueMesh,fireTongue);DrawObjects(fireLowTips,fireLowTongueMesh,fireTongue);DrawObjects(smoulders,quad,smoulderMaterial);
        }
        void RestoreCharred(CharredLook look)
        {
            if(look.renderer&&look.renderer.sharedMaterial==charredMaterial&&look.originals!=null)
            {look.renderer.sharedMaterials=look.originals;look.renderer.SetPropertyBlock(look.originalsProps);}
        }
        void ClearCharred(){fireShells.Clear();smoulders.Clear();nearFlameSubjects.Clear();nearFireBodies.Clear();detailedFireCounts.Clear();oldNearFireBodies.Clear();flameSubjects.Clear();fireTips.Clear();fireLowTips.Clear();FireTongueTriangleCount=0;foreach(var look in charred.Values)RestoreCharred(look);charred.Clear();burnRenderers.Clear();seenCharred.Clear();}
        public void ReleaseFireSurfaces()=>ClearCharred();
        void DestroyFire(){ClearCharred();if(fireTongueMesh)Destroy(fireTongueMesh);if(fireLowTongueMesh)Destroy(fireLowTongueMesh);foreach(var material in new[]{fireShell,fireTongue,charredMaterial,smoulderMaterial})if(material)Destroy(material);}
    }
}
