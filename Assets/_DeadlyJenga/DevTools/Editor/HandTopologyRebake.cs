using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeadlyJenga.CandyPlayers;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class HandTopologyRebake
    {
        public static string LastAudit { get; private set; }
        public static Mesh LastCandidate { get; private set; }
        public static Mesh LastFlyingCandidate { get; private set; }
        public static CandyPlayerRig LastPreviewRig { get; private set; }
        public static CandyPlayerRig LastUnchangedRig { get; private set; }
        public static SkinnedMeshRenderer LastFlyingPreview { get; private set; }
        public static SkinnedMeshRenderer LastFlyingSourcePreview { get; private set; }
        public static Scene LastScratchScene { get; private set; }

        sealed class HandData
        {
            public Mesh mesh;
            public Vector3[] positions, normals;
            public Vector4[] tangents;
            public Vector2[] uv;
            public BoneWeight[] weights;
            public int[] indices;
            public int firstVertex;
        }

        sealed class Components
        {
            readonly int[] parents;
            public Components(int count)
            {
                parents = new int[count];
                for (int i = 0; i < count; i++) parents[i] = i;
            }
            public int Root(int vertex)
            {
                while (parents[vertex] != vertex)
                {
                    parents[vertex] = parents[parents[vertex]];
                    vertex = parents[vertex];
                }
                return vertex;
            }
            public void Join(int a, int b)
            {
                a = Root(a); b = Root(b);
                if (a != b) parents[Math.Max(a, b)] = Math.Min(a, b);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static bool SameMatrix(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++)
                if (!float.IsFinite(a[i]) || !float.IsFinite(b[i]) || Mathf.Abs(a[i] - b[i]) > .0002f) return false;
            return true;
        }

        static bool Finger(int bone, int side) => bone >= 14 + side * 15 && bone < 29 + side * 15;
        static bool HasFinger(BoneWeight weight, int side) =>
            weight.weight0 > 0 && Finger(weight.boneIndex0, side) ||
            weight.weight1 > 0 && Finger(weight.boneIndex1, side) ||
            weight.weight2 > 0 && Finger(weight.boneIndex2, side) ||
            weight.weight3 > 0 && Finger(weight.boneIndex3, side);

        static void ValidateWeight(BoneWeight weight, int side) =>
            ValidateWeight(weight, side == 0 ? 4 : 7, 14 + side * 15);

        static void ValidateWeight(BoneWeight weight, int palm, int firstFinger)
        {
            void Check(int bone, float value)
            {
                Require(float.IsFinite(value) && value >= 0, "Non-finite or negative hand influence");
                Require(value <= 0 || bone == palm || bone >= firstFinger && bone < firstFinger + 15,
                    "Hand component includes a non-hand bone");
            }
            Check(weight.boneIndex0, weight.weight0); Check(weight.boneIndex1, weight.weight1);
            Check(weight.boneIndex2, weight.weight2); Check(weight.boneIndex3, weight.weight3);
            float sum = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
            Require(Mathf.Abs(sum - 1) < .0001f, "Hand weights do not sum to one");
        }

        static byte[] ReadStream(Mesh mesh, int stream)
        {
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var buffer = data[0].GetVertexData<byte>(stream);
                Require(buffer.Length >= mesh.vertexCount * mesh.GetVertexBufferStride(stream), "Incomplete native vertex stream");
                return buffer.ToArray();
            }
        }

        static void WriteWord(byte[] data, int offset, uint word)
        {
            data[offset] = (byte)word; data[offset + 1] = (byte)(word >> 8);
            data[offset + 2] = (byte)(word >> 16); data[offset + 3] = (byte)(word >> 24);
        }

        static Vector4 Attribute(HandData hand, int vertex, VertexAttribute attribute)
        {
            var weight = hand.weights[vertex];
            switch (attribute)
            {
                case VertexAttribute.Position: return hand.positions[vertex];
                case VertexAttribute.Normal: return hand.normals[vertex];
                case VertexAttribute.Tangent: return hand.tangents[vertex];
                case VertexAttribute.TexCoord0: return hand.uv[vertex];
                case VertexAttribute.BlendWeight:
                    return new Vector4(weight.weight0, weight.weight1, weight.weight2, weight.weight3);
                case VertexAttribute.BlendIndices:
                    return new Vector4(weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3);
                default: throw new InvalidOperationException("Unsupported source attribute " + attribute);
            }
        }

        static void ValidateLayout(Mesh source, VertexAttributeDescriptor[] layout)
        {
            Require(layout.Length == 6 && source.vertexBufferCount == 3, "Unexpected body vertex layout");
            foreach (var descriptor in layout)
            {
                bool skinIndex = descriptor.attribute == VertexAttribute.BlendIndices;
                Require(descriptor.format == (skinIndex ? VertexAttributeFormat.UInt32 : VertexAttributeFormat.Float32),
                    "Unsupported body vertex format " + descriptor.attribute);
                int dimension = descriptor.attribute == VertexAttribute.Position || descriptor.attribute == VertexAttribute.Normal ? 3 :
                    descriptor.attribute == VertexAttribute.TexCoord0 ? 2 : 4;
                Require(descriptor.dimension == dimension, "Unexpected body attribute dimension");
                // Validate attribute support before allocating the replacement mesh.
                Require(descriptor.attribute == VertexAttribute.Position || descriptor.attribute == VertexAttribute.Normal ||
                    descriptor.attribute == VertexAttribute.Tangent || descriptor.attribute == VertexAttribute.TexCoord0 ||
                    descriptor.attribute == VertexAttribute.BlendWeight || skinIndex, "Unsupported body attribute");
            }
        }

        static HandData Generate(Transform root, Transform[] bones, int side, int firstVertex)
        {
            var mesh = CandySmoothHandMesh.Build(root, bones, side);
            try
            {
                mesh.RecalculateTangents();
                var hand = new HandData
                {
                    mesh = mesh, positions = mesh.vertices, normals = mesh.normals, tangents = mesh.tangents,
                    uv = mesh.uv, weights = mesh.boneWeights, indices = mesh.triangles, firstVertex = firstVertex
                };
                Require(hand.normals.Length == mesh.vertexCount && hand.tangents.Length == mesh.vertexCount &&
                    hand.uv.Length == mesh.vertexCount && hand.weights.Length == mesh.vertexCount, "Incomplete generated hand channels");
                for (int vertex = 0; vertex < mesh.vertexCount; vertex++) ValidateWeight(hand.weights[vertex], side);
                return hand;
            }
            catch { Object.DestroyImmediate(mesh); throw; }
        }

        static int DominantDigit(BoneWeight weight, int firstFinger)
        {
            float best = .55f; int chosen = -1;
            for (int digit = 0; digit < 5; digit++)
            {
                int first = firstFinger + digit * 3;
                float Sum(int bone, float value) => bone >= first && bone < first + 3 ? value : 0;
                float sum = Sum(weight.boneIndex0, weight.weight0) + Sum(weight.boneIndex1, weight.weight1) +
                    Sum(weight.boneIndex2, weight.weight2) + Sum(weight.boneIndex3, weight.weight3);
                if (sum > best) { best = sum; chosen = digit; }
            }
            return chosen;
        }

        public static int DistalCrossDigitTriangles(Mesh mesh, int side, Matrix4x4 palmFromRoot) =>
            CountDistalCrossDigitTriangles(mesh, 14 + side * 15, palmFromRoot);

        public static int FlyingDistalCrossDigitTriangles(Mesh mesh, Matrix4x4 palmFromRoot) =>
            CountDistalCrossDigitTriangles(mesh, 1, palmFromRoot);

        static int CountDistalCrossDigitTriangles(Mesh mesh, int firstFinger, Matrix4x4 palmFromRoot)
        {
            var points = mesh.vertices; var weights = mesh.boneWeights; var indices = mesh.triangles;
            int count = 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                int da = DominantDigit(weights[a], firstFinger), db = DominantDigit(weights[b], firstFinger), dc = DominantDigit(weights[c], firstFinger);
                if (da < 0 || db < 0 || dc < 0 || da == db && db == dc) continue;
                if (palmFromRoot.MultiplyPoint3x4(points[a]).z > .064f &&
                    palmFromRoot.MultiplyPoint3x4(points[b]).z > .064f &&
                    palmFromRoot.MultiplyPoint3x4(points[c]).z > .064f) count++;
            }
            return count;
        }

        public static Mesh BuildCandidate(Mesh source, Transform root, Transform[] orderedBones)
        {
            LastAudit = "Generating transient hand-only candidate; native checks pending";
            Require(source && source.isReadable && root, "Readable source mesh and rest root required");
            Require(source.blendShapeCount == 0, "Blendshape mesh needs a separate preservation plan");
            var bindings = source.bindposes;
            Require(bindings.Length == 44 && orderedBones != null && orderedBones.Length == 44, "Expected all 44 ordered bones");
            for (int i = 0; i < 44; i++)
                Require(orderedBones[i] && SameMatrix(bindings[i], orderedBones[i].worldToLocalMatrix * root.localToWorldMatrix),
                    "Input rig is not in the source bind rest pose at bone " + i);
            for (int side = 0; side < 2; side++)
            {
                int palm = side == 0 ? 4 : 7;
                var reference = CandyHandSkeleton.Rest(side == 0);
                for (int finger = 0; finger < 5; finger++)
                    for (int joint = 0; joint < 3; joint++)
                    {
                        int bone = 14 + side * 15 + finger * 3 + joint;
                        Vector3 actual = orderedBones[palm].InverseTransformPoint(orderedBones[bone].position);
                        Require(Vector3.Distance(actual, reference.Joint(finger * 4 + joint)) < .0002f,
                            "Current generator skeleton differs from source bind rest at bone " + bone);
                    }
            }
            var layout = source.GetVertexAttributes(); ValidateLayout(source, layout);
            var weights = source.boneWeights;
            Require(weights.Length == source.vertexCount, "Source skin weight count mismatch");
            var components = new Components(source.vertexCount);
            var originalIndices = new int[source.subMeshCount][];
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                Require(source.GetTopology(submesh) == MeshTopology.Triangles, "Only triangle submeshes supported");
                var indices = source.GetIndices(submesh, true); originalIndices[submesh] = indices;
                Require(indices.Length % 3 == 0, "Invalid source triangle count");
                for (int i = 0; i < indices.Length; i += 3)
                { components.Join(indices[i], indices[i + 1]); components.Join(indices[i], indices[i + 2]); }
            }
            var handRoots = new[] { new HashSet<int>(), new HashSet<int>() };
            for (int vertex = 0; vertex < source.vertexCount; vertex++)
                for (int side = 0; side < 2; side++)
                    if (HasFinger(weights[vertex], side)) handRoots[side].Add(components.Root(vertex));
            Require(handRoots[0].Count == 1 && handRoots[1].Count == 1, "Each hand must be exactly one connected component");
            int[] selectedRoots = new int[2];
            for (int side = 0; side < 2; side++) foreach (int component in handRoots[side]) selectedRoots[side] = component;
            Require(selectedRoots[0] != selectedRoots[1], "Left and right hands share a component");
            int[] selection = new int[source.vertexCount], remap = new int[source.vertexCount];
            int retained = 0; int[] removedVertices = new int[2];
            for (int vertex = 0; vertex < source.vertexCount; vertex++)
            {
                int component = components.Root(vertex);
                int side = component == selectedRoots[0] ? 0 : component == selectedRoots[1] ? 1 : -1;
                selection[vertex] = side;
                if (side >= 0) { ValidateWeight(weights[vertex], side); remap[vertex] = -1; removedVertices[side]++; }
                else remap[vertex] = retained++;
            }
            var originalStreams = new byte[source.vertexBufferCount][];
            for (int stream = 0; stream < originalStreams.Length; stream++) originalStreams[stream] = ReadStream(source, stream);
            Mesh candidate = null; HandData left = null, right = null;
            try
            {
                left = Generate(root, orderedBones, 0, retained);
                right = Generate(root, orderedBones, 1, retained + left.positions.Length);
                var hands = new[] { left, right };
                int vertexCount = retained + left.positions.Length + right.positions.Length;
                Require(source.indexFormat != IndexFormat.UInt16 || vertexCount <= ushort.MaxValue,
                    "Replacement would require a wider index format");
                for (int side = 0; side < 2; side++)
                    Require(DistalCrossDigitTriangles(hands[side].mesh, side, bindings[side == 0 ? 4 : 7]) == 0,
                        "Generated hand still contains distal cross-digit triangles");
                var outputIndices = new List<int>[source.subMeshCount];
                var handSubmeshes = new[] { new HashSet<int>(), new HashSet<int>() };
                for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                {
                    var output = outputIndices[submesh] = new List<int>();
                    var input = originalIndices[submesh];
                    for (int i = 0; i < input.Length; i += 3)
                    {
                        int side = selection[input[i]];
                        Require(selection[input[i + 1]] == side && selection[input[i + 2]] == side,
                            "A triangle crosses a selected and retained component boundary");
                        if (side >= 0) { handSubmeshes[side].Add(submesh); continue; }
                        output.Add(remap[input[i]]); output.Add(remap[input[i + 1]]); output.Add(remap[input[i + 2]]);
                    }
                }
                for (int side = 0; side < 2; side++)
                {
                    Require(handSubmeshes[side].Count == 1, "Hand spans more than one material submesh");
                    foreach (int submesh in handSubmeshes[side])
                        foreach (int index in hands[side].indices) outputIndices[submesh].Add(index + hands[side].firstVertex);
                }
                candidate = Object.Instantiate(source); candidate.name = source.name + " (hand-only scratch rebake)";
                candidate.SetVertexBufferParams(vertexCount, layout);
                var uploadedLayout = candidate.GetVertexAttributes();
                Require(uploadedLayout.Length == layout.Length, "Vertex attribute count changed");
                for (int i = 0; i < layout.Length; i++)
                {
                    Require(uploadedLayout[i].Equals(layout[i]), "Vertex attribute layout changed");
                    Require(candidate.GetVertexAttributeOffset(layout[i].attribute) == source.GetVertexAttributeOffset(layout[i].attribute),
                        "Vertex attribute offset changed");
                }
                for (int stream = 0; stream < originalStreams.Length; stream++)
                {
                    int stride = source.GetVertexBufferStride(stream);
                    Require(candidate.GetVertexBufferStride(stream) == stride, "Vertex stream stride changed");
                    var bytes = new byte[vertexCount * stride];
                    for (int oldVertex = 0; oldVertex < remap.Length; oldVertex++)
                        if (remap[oldVertex] >= 0) Buffer.BlockCopy(originalStreams[stream], oldVertex * stride, bytes, remap[oldVertex] * stride, stride);
                    foreach (var hand in hands)
                        for (int vertex = 0; vertex < hand.positions.Length; vertex++)
                            foreach (var descriptor in layout)
                            {
                                if (descriptor.stream != stream) continue;
                                Vector4 values = Attribute(hand, vertex, descriptor.attribute);
                                int offset = (hand.firstVertex + vertex) * stride + source.GetVertexAttributeOffset(descriptor.attribute);
                                for (int component = 0; component < descriptor.dimension; component++)
                                {
                                    float value = values[component]; Require(float.IsFinite(value), "Generated hand contains a non-finite attribute");
                                    uint word = descriptor.format == VertexAttributeFormat.UInt32 ? (uint)value :
                                        unchecked((uint)BitConverter.SingleToInt32Bits(value));
                                    WriteWord(bytes, offset + component * 4, word);
                                }
                            }
                    using (var buffer = new NativeArray<byte>(bytes, Allocator.Temp))
                        candidate.SetVertexBufferData(buffer, 0, 0, buffer.Length, stream, MeshUpdateFlags.DontRecalculateBounds);
                }
                int indexCount = 0; foreach (var indices in outputIndices) indexCount += indices.Count;
                candidate.SetIndexBufferParams(indexCount, source.indexFormat);
                int cursor = 0;
                if (source.indexFormat == IndexFormat.UInt16)
                {
                    var indices = new ushort[indexCount];
                    foreach (var list in outputIndices) foreach (int index in list) indices[cursor++] = checked((ushort)index);
                    candidate.SetIndexBufferData(indices, 0, 0, indices.Length, MeshUpdateFlags.DontRecalculateBounds);
                }
                else
                {
                    var indices = new int[indexCount];
                    foreach (var list in outputIndices) foreach (int index in list) indices[cursor++] = index;
                    candidate.SetIndexBufferData(indices, 0, 0, indices.Length, MeshUpdateFlags.DontRecalculateBounds);
                }
                candidate.subMeshCount = source.subMeshCount; cursor = 0;
                for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                {
                    var descriptor = new SubMeshDescriptor(cursor, outputIndices[submesh].Count, MeshTopology.Triangles)
                    { baseVertex = 0, firstVertex = 0, vertexCount = vertexCount, bounds = source.GetSubMesh(submesh).bounds };
                    candidate.SetSubMesh(submesh, descriptor, MeshUpdateFlags.DontRecalculateBounds);
                    cursor += descriptor.indexCount;
                }
                candidate.bindposes = bindings; candidate.bounds = source.bounds;
                Require(candidate.isReadable && candidate.indexFormat == source.indexFormat, "Readability or index width changed");
                var actualWeights = candidate.boneWeights;
                Require(actualWeights.Length == vertexCount, "Native replacement skin metadata has the wrong vertex count");
                for (int side = 0; side < 2; side++)
                    for (int i = 0; i < hands[side].positions.Length; i++)
                    {
                        var expected = hands[side].weights[i]; var actual = actualWeights[hands[side].firstVertex + i];
                        ValidateWeight(actual, side);
                        Require(SameWeight(expected, actual), "Native generated skin weights differ from uploaded weights");
                        Require(source.bounds.Contains(hands[side].positions[i]), "Generated hand exceeds original bounds");
                    }
                for (int stream = 0; stream < originalStreams.Length; stream++)
                {
                    int stride = source.GetVertexBufferStride(stream); var result = ReadStream(candidate, stream);
                    for (int oldVertex = 0; oldVertex < remap.Length; oldVertex++)
                        if (remap[oldVertex] >= 0)
                            for (int b = 0; b < stride; b++)
                                Require(result[remap[oldVertex] * stride + b] == originalStreams[stream][oldVertex * stride + b],
                                    "A retained body vertex byte changed");
                    Require(EqualBytes(ReadStream(source, stream), originalStreams[stream]), "Source mesh was mutated");
                }
                for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                {
                    var actual = candidate.GetIndices(submesh, true); var expected = outputIndices[submesh];
                    Require(actual.Length == expected.Count, "Submesh triangle count changed after upload");
                    for (int i = 0; i < actual.Length; i++) Require(actual[i] == expected[i], "Triangle order or winding changed after upload");
                }
                var actualBindings = candidate.bindposes;
                for (int i = 0; i < 44; i++)
                    for (int element = 0; element < 16; element++)
                        Require(BitConverter.SingleToInt32Bits(bindings[i][element]) == BitConverter.SingleToInt32Bits(actualBindings[i][element]),
                            "A bind pose byte changed");
                LastAudit = "PASS transient hand-only candidate. sourceVertices=" + source.vertexCount + " retainedVertices=" + retained +
                    " removedLeft=" + removedVertices[0] + " removedRight=" + removedVertices[1] +
                    " candidateVertices=" + vertexCount + " candidateTriangles=" + indexCount / 3 +
                    " generatedLeft=" + left.positions.Length + " generatedRight=" + right.positions.Length +
                    " sourceBridgesLeft=" + DistalCrossDigitTriangles(source, 0, bindings[4]) +
                    " sourceBridgesRight=" + DistalCrossDigitTriangles(source, 1, bindings[7]) +
                    " generatedDistalBridges=0 bodyVertexBytes=identical retainedTriangleOrder=identical bindposes=44 indexFormat=" + source.indexFormat;
                return candidate;
            }
            catch (Exception error)
            {
                LastAudit = "FAIL transient hand-only candidate: " + error.Message;
                if (candidate) Object.DestroyImmediate(candidate);
                throw;
            }
            finally
            {
                if (left != null && left.mesh) Object.DestroyImmediate(left.mesh);
                if (right != null && right.mesh) Object.DestroyImmediate(right.mesh);
            }
        }

        public static Mesh BuildFlyingCandidate(Mesh source, Transform root, Transform[] ordered16Bones)
        {
            LastAudit = "Generating transient flying-hand candidate; native checks pending";
            Require(source && source.isReadable && root, "Readable flying hand and rest root required");
            Require(source.blendShapeCount == 0 && source.subMeshCount == 1 && source.GetTopology(0) == MeshTopology.Triangles,
                "Flying hand must be one triangle material submesh without blendshapes");
            var bindings = source.bindposes;
            Require(bindings.Length == 16 && ordered16Bones != null && ordered16Bones.Length == 16,
                "Expected all 16 ordered flying-hand bones");
            for (int i = 0; i < 16; i++)
                Require(ordered16Bones[i] && SameMatrix(bindings[i], ordered16Bones[i].worldToLocalMatrix * root.localToWorldMatrix),
                    "Flying rig is not in the source bind rest at bone " + i);
            var rest = CandyHandSkeleton.Rest(false);
            for (int finger = 0; finger < 5; finger++)
                for (int joint = 0; joint < 3; joint++)
                {
                    int bone = 1 + finger * 3 + joint;
                    Vector3 actual = ordered16Bones[0].InverseTransformPoint(ordered16Bones[bone].position);
                    Require(Vector3.Distance(actual, rest.Joint(finger * 4 + joint)) < .0002f,
                        "Current right-hand generator differs from flying bind rest at bone " + bone);
                }
            var originalWeights = source.boneWeights;
            Require(originalWeights.Length == source.vertexCount, "Flying source skin count mismatch");
            foreach (var weight in originalWeights) ValidateWeight(weight, 0, 1);
            var originalStreams = new byte[source.vertexBufferCount][];
            for (int stream = 0; stream < originalStreams.Length; stream++) originalStreams[stream] = ReadStream(source, stream);
            var originalIndices = source.GetIndices(0, true);
            var mappedBones = new Transform[44]; mappedBones[7] = ordered16Bones[0];
            for (int i = 1; i < 16; i++) mappedBones[28 + i] = ordered16Bones[i];
            HandData generated = null;
            try
            {
                // The generator reads the right palm transform; remap its skin IDs before exposing the candidate.
                generated = Generate(root, mappedBones, 1, 0);
                var mesh = generated.mesh;
                Require(source.indexFormat != IndexFormat.UInt16 || mesh.vertexCount <= ushort.MaxValue,
                    "Flying replacement would require a wider index format");
                var remapped = new BoneWeight[generated.weights.Length];
                int Bone(int index, float weight)
                {
                    if (weight <= 0 || index == 7) return 0;
                    Require(index >= 29 && index < 44, "Unexpected generated flying-hand bone");
                    return index - 28;
                }
                for (int i = 0; i < remapped.Length; i++)
                {
                    var weight = generated.weights[i];
                    weight.boneIndex0 = Bone(weight.boneIndex0, weight.weight0);
                    weight.boneIndex1 = Bone(weight.boneIndex1, weight.weight1);
                    weight.boneIndex2 = Bone(weight.boneIndex2, weight.weight2);
                    weight.boneIndex3 = Bone(weight.boneIndex3, weight.weight3);
                    ValidateWeight(weight, 0, 1); remapped[i] = weight;
                }
                if (mesh.indexFormat != source.indexFormat)
                {
                    mesh.indexFormat = source.indexFormat;
                    mesh.SetTriangles(generated.indices, 0, false);
                }
                mesh.boneWeights = remapped; mesh.bindposes = bindings;
                mesh.name = source.name + " (flying-hand scratch rebake)";
                Require(mesh.isReadable && mesh.indexFormat == source.indexFormat && mesh.subMeshCount == 1,
                    "Flying candidate changed readability, index width or material count");
                var actualWeights = mesh.boneWeights;
                Require(actualWeights.Length == remapped.Length, "Flying native skin metadata has wrong count");
                for (int i = 0; i < actualWeights.Length; i++)
                    Require(SameWeight(actualWeights[i], remapped[i]), "Flying native skin weights differ after remap");
                var actualBindings = mesh.bindposes;
                for (int i = 0; i < 16; i++)
                    for (int element = 0; element < 16; element++)
                        Require(BitConverter.SingleToInt32Bits(bindings[i][element]) == BitConverter.SingleToInt32Bits(actualBindings[i][element]),
                            "A flying-hand bind pose byte changed");
                Require(FlyingDistalCrossDigitTriangles(mesh, bindings[0]) == 0,
                    "Generated flying hand still has distal cross-digit triangles");
                for (int stream = 0; stream < originalStreams.Length; stream++)
                    Require(EqualBytes(originalStreams[stream], ReadStream(source, stream)), "Flying source vertex data mutated");
                var actualIndices = source.GetIndices(0, true);
                Require(actualIndices.Length == originalIndices.Length, "Flying source index count mutated");
                for (int i = 0; i < originalIndices.Length; i++) Require(actualIndices[i] == originalIndices[i], "Flying source indices mutated");
                LastAudit = "PASS transient flying-hand candidate. sourceVertices=" + source.vertexCount +
                    " candidateVertices=" + mesh.vertexCount + " candidateTriangles=" + generated.indices.Length / 3 +
                    " sourceDistalBridges=" + FlyingDistalCrossDigitTriangles(source, bindings[0]) +
                    " generatedDistalBridges=0 skinBones=16 materialSubmeshes=1 indexFormat=" + source.indexFormat;
                generated.mesh = null;
                return mesh;
            }
            catch (Exception error)
            {
                LastAudit = "FAIL transient flying-hand candidate: " + error.Message;
                throw;
            }
            finally { if (generated != null && generated.mesh) Object.DestroyImmediate(generated.mesh); }
        }

        static bool SameWeight(BoneWeight a, BoneWeight b) =>
            a.boneIndex0 == b.boneIndex0 && a.boneIndex1 == b.boneIndex1 && a.boneIndex2 == b.boneIndex2 && a.boneIndex3 == b.boneIndex3 &&
            a.weight0 == b.weight0 && a.weight1 == b.weight1 && a.weight2 == b.weight2 && a.weight3 == b.weight3;

        static bool EqualBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        [MenuItem("Tools/Deadly Jenga/Validation/Generate hand topology scratch")]
        public static void GenerateScratch() => GenerateScratch(false);

        public static void GenerateScratch(bool includeFlyingHand)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Generate scratch only in Edit Mode");
            LastCandidate = LastFlyingCandidate = null;
            LastPreviewRig = LastUnchangedRig = null;
            LastFlyingPreview = LastFlyingSourcePreview = null;
            LastScratchScene = default;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CandyPlayerVisual.prefab"));
            Require(prefab, "Production visual prefab is missing");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Mesh candidate = null, flyingCandidate = null;
            try
            {
                var before = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                before.name = "Scratch - unchanged source hands";
                var beforeRig = before.GetComponentInChildren<CandyPlayerRig>();
                var bones = RestoreSourceRest(beforeRig);
                var source = beforeRig.bodyRenderer.sharedMesh;
                Require(Path.GetFileName(AssetDatabase.GetAssetPath(source)) == "Candy Body.asset", "Unexpected body source asset");
                candidate = BuildCandidate(source, beforeRig.transform, bones);
                string audit = LastAudit;
                var preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                preview.name = "Scratch - hand topology candidate";
                var rig = preview.GetComponentInChildren<CandyPlayerRig>();
                RestoreSourceRest(rig);
                rig.bodyRenderer.sharedMesh = candidate;
                before.transform.position = Vector3.left * .8f;
                preview.transform.position = Vector3.right * .8f;
                if (includeFlyingHand)
                {
                    var flyingSource = AssetDatabase.LoadAssetAtPath<Mesh>(ProjectPath.Find("Continuous Hand.asset"));
                    Require(flyingSource, "Production continuous flying-hand mesh is missing");
                    var flyingBefore = MakeFlyingPreview(flyingSource, beforeRig.bodyRenderer.sharedMaterial, scene,
                        "Scratch - unchanged flying hand", new Vector3(-.3f, 1.1f, -.5f));
                    flyingCandidate = BuildFlyingCandidate(flyingSource, flyingBefore.transform, flyingBefore.bones);
                    var flyingAfter = MakeFlyingPreview(flyingCandidate, beforeRig.bodyRenderer.sharedMaterial, scene,
                        "Scratch - flying hand candidate", new Vector3(.3f, 1.1f, -.5f));
                    LastFlyingSourcePreview = flyingBefore; LastFlyingPreview = flyingAfter;
                    audit += "\n" + LastAudit + "\nflyingSourcePath=" + AssetDatabase.GetAssetPath(flyingSource) +
                        " flyingSourceGUID=" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(flyingSource));
                }
                LastCandidate = candidate; LastFlyingCandidate = flyingCandidate;
                LastPreviewRig = rig; LastUnchangedRig = beforeRig; LastScratchScene = scene; LastAudit = audit;
                File.WriteAllText(ProjectPath.Report("HandTopologyScratch.txt"), LastAudit + "\nsourcePath=" +
                    AssetDatabase.GetAssetPath(source) + " sourceGUID=" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) +
                    "\nScratch rest-pose previews only. Existing mesh, metadata, prefabs and scenes have not been saved. Native visual inspection and KEEP remain required.\n");
                Selection.activeGameObject = preview;
            }
            catch
            {
                if (candidate) Object.DestroyImmediate(candidate);
                if (flyingCandidate) Object.DestroyImmediate(flyingCandidate);
                LastCandidate = LastFlyingCandidate = null;
                LastPreviewRig = LastUnchangedRig = null;
                LastFlyingPreview = LastFlyingSourcePreview = null;
                LastScratchScene = default;
                EditorSceneManager.CloseScene(scene, true);
                throw;
            }
        }

        static SkinnedMeshRenderer MakeFlyingPreview(Mesh mesh, Material material, Scene scene, string name, Vector3 position)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = position;
            var bindings = mesh.bindposes; var bones = new Transform[bindings.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = new GameObject(i == 0 ? "Palm" : "Finger joint " + i).transform;
                bone.SetParent(root.transform, false); var local = bindings[i].inverse;
                bone.localPosition = local.GetColumn(3); bone.localRotation = local.rotation; bone.localScale = local.lossyScale;
                bones[i] = bone;
                Require(SameMatrix(bindings[i], bone.worldToLocalMatrix * root.transform.localToWorldMatrix),
                    "Flying preview source bind rest reconstruction failed");
            }
            var renderer = root.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material; renderer.bones = bones; renderer.rootBone = root.transform;
            renderer.localBounds = mesh.bounds; renderer.updateWhenOffscreen = true;
            return renderer;
        }

        static Transform[] RestoreSourceRest(CandyPlayerRig rig)
        {
            Require(rig && rig.bodyRenderer && rig.BoneCount == 44, "Production visual rig is incomplete");
            var source = rig.bodyRenderer.sharedMesh;
            Require(source && source.bindposes.Length == 44 && rig.bodyRenderer.bones.Length == 44, "Body skin binding count mismatch");
            var bones = new Transform[44]; var bindings = source.bindposes;
            for (int i = 0; i < 44; i++)
            {
                bones[i] = rig.AppearanceBone(i);
                Require(bones[i] == rig.bodyRenderer.bones[i], "Visual rig and renderer bone orders differ");
                Require(bones[i].parent == rig.transform, "Rest reconstruction expects production flat bone hierarchy");
                var local = bindings[i].inverse;
                bones[i].localPosition = local.GetColumn(3); bones[i].localRotation = local.rotation; bones[i].localScale = local.lossyScale;
                Require(SameMatrix(bindings[i], bones[i].worldToLocalMatrix * rig.transform.localToWorldMatrix), "Source bind rest reconstruction failed");
            }
            return bones;
        }
    }
}
