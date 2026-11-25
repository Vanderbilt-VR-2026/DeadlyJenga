using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum TycoonDeckFinish { Plain, OpenDeck, Flume, DryChute, Splash, Peach, Mint, Basin, Tube, Gate, Railed, Awning, Parasol, Bucket, LoosePeach, Walk, Launch, Wheel, Bridge, River, RiverPump }

    [System.Flags]
    public enum TycoonRailOpening { None=0, Front=1, Rear=2 }

    public readonly struct TycoonDeckProfile
    {
        public readonly TycoonDeckFinish Finish;
        public readonly TycoonRailOpening Openings;
        public readonly Vector3 StartLeft, StartRight, EndLeft, EndRight;

        public TycoonDeckProfile(TycoonDeckFinish finish, Vector3 startLeft, Vector3 startRight,
            Vector3 endLeft, Vector3 endRight, TycoonRailOpening openings=TycoonRailOpening.None)
        {
            Finish = finish;
            Openings = openings;
            StartLeft = startLeft;
            StartRight = startRight;
            EndLeft = endLeft;
            EndRight = endRight;
        }

        public static TycoonDeckProfile Rectangle(TycoonDeckFinish finish, TycoonRailOpening openings=TycoonRailOpening.None) => new TycoonDeckProfile(finish,
            new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,.5f,-.5f),
            new Vector3(-.5f,.5f,.5f), new Vector3(.5f,.5f,.5f),openings);
    }

    // One rendered module follows its existing box body through a collapse.
    public sealed partial class TycoonDeckVisual : MonoBehaviour
    {
        Mesh mesh;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int>[] faces = {new List<int>(),new List<int>(),new List<int>(),new List<int>()};

        Vector3 vertical;
        public void Build(TycoonDeckProfile profile, Material shell, Material accent, Vector3 size)
        {
            vertical = transform.InverseTransformVector(Vector3.up);
            var a = profile.StartLeft;
            var b = profile.StartRight;
            var c = profile.EndLeft;
            var d = profile.EndRight;
            BeveledDeck(a,b,c,d,size,profile.Finish);
            if (profile.Finish is TycoonDeckFinish.Flume or TycoonDeckFinish.DryChute)
            {
                float inset = Mathf.Min(.22f,.74f/size.x);
                // The trough rises gradually to the rounded rim on each bank.
                foreach(bool right in new[]{false,true})
                {
                    var start=right?b:a; var end=right?d:c;
                    var innerStart=Vector3.Lerp(start,right?a:b,inset);
                    var innerEnd=Vector3.Lerp(end,right?c:d,inset);
                    for(int k=0;k<5;k++)
                    {
                        Vector3 Bank(Vector3 inner,Vector3 outer,int step)
                        {
                            float t=step/5f;
                            return Vector3.Lerp(inner,outer,Mathf.Sin(t*Mathf.PI*.5f))
                                +vertical*(.02f+.76f*(1-Mathf.Cos(t*Mathf.PI*.5f)));
                        }
                        var n=Bank(innerStart,start,k); var f=Bank(innerEnd,end,k);
                        var n2=Bank(innerStart,start,k+1); var f2=Bank(innerEnd,end,k+1);
                        if(right) Quad(n,f,f2,n2,0); else Quad(n,n2,f2,f,0);
                    }
                    var rise=vertical*.78f;
                    if(right) Quad(start,start+rise,end+rise,end,0);
                    else Quad(start,end,end+rise,start+rise,0);
                }
            }
            else if (profile.Finish == TycoonDeckFinish.Splash)
            {
                Splash(size);
            }

            AddParkDetails(profile,size);
            bool river = profile.Finish is TycoonDeckFinish.River or TycoonDeckFinish.RiverPump;
            Material body = profile.Finish is TycoonDeckFinish.Peach or TycoonDeckFinish.LoosePeach or TycoonDeckFinish.Walk ? Peach
                : profile.Finish == TycoonDeckFinish.Mint ? Mint : river ? Cream : shell;
            var palette = new[] {body, accent ? accent : shell, Cream, Coral};
            FinishMesh(palette);
            if (river) BuildRiverWater(profile, palette);
            if (profile.Finish == TycoonDeckFinish.Bucket) BuildTippingBucket(size, palette);
            if (profile.Finish == TycoonDeckFinish.Bucket) BuildSharkSign(size);
            if (profile.Finish == TycoonDeckFinish.Wheel) BuildWheel(size);
        }

        void FinishMesh(Material[] palette)
        {
            mesh = new Mesh {name = "Tycoon molded deck"};
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uv);
            mesh.subMeshCount = faces.Length;
            for(int i=0;i<faces.Length;i++) mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterials = palette;
        }

        void BeveledDeck(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 size,TycoonDeckFinish finish)
        {
            float ix = Mathf.Min(.15f,.07f/size.x);
            float iz = Mathf.Min(.15f,.07f/size.z);
            var leftNear = Vector3.Lerp(Vector3.Lerp(a,b,ix),Vector3.Lerp(c,d,ix),iz);
            var rightNear = Vector3.Lerp(Vector3.Lerp(b,a,ix),Vector3.Lerp(d,c,ix),iz);
            var leftFar = Vector3.Lerp(Vector3.Lerp(c,d,ix),Vector3.Lerp(a,b,ix),iz);
            var rightFar = Vector3.Lerp(Vector3.Lerp(d,c,ix),Vector3.Lerp(b,a,ix),iz);
            var bevel = Vector3.down*(.055f/size.y);
            var down = Vector3.down;
            if(finish==TycoonDeckFinish.Flume)
            {
                float inset=Mathf.Min(.15f,.36f/size.x);
                var nL=Vector3.Lerp(a,b,inset); var nR=Vector3.Lerp(b,a,inset);
                var fL=Vector3.Lerp(c,d,inset); var fR=Vector3.Lerp(d,c,inset);
                Quad(a,c,fL,nL,0);
                Quad(nL,fL,fR,nR,1);
                Quad(nR,fR,d,b,0);
                // The trough top is one continuous surface. The bevel remains on the underside and exposed walls.
                leftNear=a; rightNear=b; leftFar=c; rightFar=d;
            }
            else if(finish==TycoonDeckFinish.DryChute || finish==TycoonDeckFinish.Tube)
            {
                // Adjacent molded panels share a flush transverse edge down the full run.
                Quad(a,c,d,b,0);
                leftNear=a; rightNear=b; leftFar=c; rightFar=d;
            }
            else Quad(leftNear,leftFar,rightFar,rightNear,0);
            Quad(a+bevel,leftNear,rightNear,b+bevel,0);
            Quad(c+bevel,d+bevel,rightFar,leftFar,0);
            Quad(a+bevel,c+bevel,leftFar,leftNear,0);
            Quad(b+bevel,rightNear,rightFar,d+bevel,0);
            Quad(a+down,b+down,d+down,c+down,0);
            Quad(a+bevel,b+bevel,b+down,a+down,0);
            Quad(c+down,d+down,d+bevel,c+bevel,0);
            Quad(a+down,c+down,c+bevel,a+bevel,0);
            Quad(b+bevel,d+bevel,d+down,b+down,0);
        }
        void Prism(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float depth,int material)
        {
            var down = Vector3.down*depth;
            Quad(a,c,d,b,material);
            Quad(a+down,b+down,d+down,c+down,material);
            Quad(a,b,b+down,a+down,material);
            Quad(c+down,d+down,d,c,material);
            Quad(a+down,c+down,c,a,material);
            Quad(b,d,d+down,b+down,material);
        }

        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
            var indices = faces[material];
            indices.Add(start); indices.Add(start+1); indices.Add(start+2);
            indices.Add(start); indices.Add(start+2); indices.Add(start+3);
        }

        void Splash(Vector3 size)
        {
            float y = .5f+.022f/size.y;
            var center = new Vector3(0,y,0);
            int hub = vertices.Count;
            vertices.Add(center);
            uv.Add(Vector2.one*.5f);
            const int count = 64;
            for (int i=0;i<count;i++)
            {
                float angle = i*Mathf.PI*2/count;
                float radius = .23f + .067f*Mathf.Sin(7*angle) + .034f*Mathf.Cos(11*angle);
                vertices.Add(new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius));
                uv.Add(Vector2.one*.5f);
            }
            for (int i=0;i<count;i++)
            {
                faces[1].Add(hub);
                faces[1].Add(hub+1+(i+1)%count);
                faces[1].Add(hub+1+i);
            }
            for (int i=0;i<11;i++)
            {
                float angle = i*2.39996f;
                var offset = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var tangent = Vector3.Cross(Vector3.up,offset);
                var spot = center+offset*(.31f+(i%3)*.028f);
                float longSide = .024f+(i%4)*.006f;
                Quad(spot-offset*longSide-tangent*.014f,spot+offset*longSide-tangent*.014f,
                    spot+offset*longSide+tangent*.014f,spot-offset*longSide+tangent*.014f,1);
            }
        }

        void OnDestroy()
        {
            if(!mesh)return;
            if(Application.isPlaying)Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
