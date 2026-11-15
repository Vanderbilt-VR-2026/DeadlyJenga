using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    // Bone matrices are captured on the main thread. Workers only transform vertex data.
    sealed class VisualSkinDeformation : IDisposable
    {
        const int BatchSize = 256;
        struct Source
        {
            public Vector3 position, normal;
            public BoneWeight weights;
            public int single;
        }
        struct Extent { public Vector3 min, max; }
        NativeArray<Source> sources;
        NativeArray<int> bones;
        NativeArray<Matrix4x4> poses, normalPoses;
        NativeArray<Vector3> vertices, normals;
        NativeArray<Extent> extents;
        static readonly ProfilerMarker Schedule = new("DJ.DeathDeform.Schedule");
        static readonly ProfilerMarker Wait = new("DJ.DeathDeform.Wait");
        static readonly ProfilerMarker Upload = new("DJ.DeathDeform.Upload");

        internal VisualSkinDeformation(Vector3[] origin, Vector3[] originalNormals, BoneWeight[] weights,
            int[] boneMap, int[] single, int poseCount)
        {
            sources = new NativeArray<Source>(origin.Length, Allocator.Persistent);
            bones = new NativeArray<int>(boneMap, Allocator.Persistent);
            poses = new NativeArray<Matrix4x4>(poseCount, Allocator.Persistent);
            normalPoses = new NativeArray<Matrix4x4>(poseCount, Allocator.Persistent);
            vertices = new NativeArray<Vector3>(origin.Length, Allocator.Persistent);
            normals = new NativeArray<Vector3>(origin.Length, Allocator.Persistent);
            extents = new NativeArray<Extent>((origin.Length + BatchSize - 1) / BatchSize, Allocator.Persistent);
            for (int i = 0; i < origin.Length; i++)
                sources[i] = new Source { position = origin[i], normal = originalNormals[i], weights = weights[i], single = single[i] };
        }

        [BurstCompile(CompileSynchronously = true)]
        struct Deform : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Source> sources;
            [ReadOnly] public NativeArray<int> bones;
            [ReadOnly] public NativeArray<Matrix4x4> poses, normalPoses;
            // Each worker owns one disjoint contiguous batch of vertices.
            [Unity.Collections.LowLevel.Unsafe.NativeDisableParallelForRestriction]
            public NativeArray<Vector3> vertices;
            [Unity.Collections.LowLevel.Unsafe.NativeDisableParallelForRestriction]
            public NativeArray<Vector3> normals;
            [WriteOnly] public NativeArray<Extent> extents;
            public void Execute(int batch)
            {
                Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
                int end = Math.Min(sources.Length, (batch + 1) * BatchSize);
                for (int i = batch * BatchSize; i < end; i++)
                {
                    var source = sources[i];
                    Vector3 p, n;
                    if (source.single >= 0)
                    {
                        p = poses[source.single].MultiplyPoint3x4(source.position);
                        n = normalPoses[source.single].MultiplyVector(source.normal);
                    }
                    else
                    {
                        var w = source.weights;
                        p = n = Vector3.zero;
                        Add(source, w.boneIndex0, w.weight0, ref p, ref n);
                        Add(source, w.boneIndex1, w.weight1, ref p, ref n);
                        Add(source, w.boneIndex2, w.weight2, ref p, ref n);
                        Add(source, w.boneIndex3, w.weight3, ref p, ref n);
                        p /= w.weight0 + w.weight1 + w.weight2 + w.weight3;
                    }
                    vertices[i] = p; normals[i] = n.normalized;
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
                extents[batch] = new Extent { min = min, max = max };
            }
            void Add(Source source, int bone, float weight, ref Vector3 p, ref Vector3 n)
            {
                if (weight <= 0) return;
                int index = bones[bone];
                p += poses[index].MultiplyPoint3x4(source.position) * weight;
                n += normalPoses[index].MultiplyVector(source.normal) * weight;
            }
        }

        internal Bounds Present(Matrix4x4[] localPoses, Matrix4x4[] localNormalPoses, Mesh mesh)
        {
            JobHandle job;
            using (Schedule.Auto())
            {
                poses.CopyFrom(localPoses); normalPoses.CopyFrom(localNormalPoses);
                job = new Deform { sources = sources, bones = bones, poses = poses, normalPoses = normalPoses,
                    vertices = vertices, normals = normals, extents = extents }.Schedule(extents.Length, 1);
            }
            using (Wait.Auto()) job.Complete();
            Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
            for (int i = 0; i < extents.Length; i++)
            { min = Vector3.Min(min, extents[i].min); max = Vector3.Max(max, extents[i].max); }
            var bounds = new Bounds((min + max) * .5f, max - min);
            using (Upload.Auto())
            { mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.bounds = bounds; }
            return bounds;
        }
        public void Dispose()
        {
            if (sources.IsCreated) sources.Dispose();
            if (bones.IsCreated) bones.Dispose();
            if (poses.IsCreated) poses.Dispose();
            if (normalPoses.IsCreated) normalPoses.Dispose();
            if (vertices.IsCreated) vertices.Dispose();
            if (normals.IsCreated) normals.Dispose();
            if (extents.IsCreated) extents.Dispose();
        }
    }
}
