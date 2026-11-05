using System;
using System.IO;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class HandWeightRegression
    {
        public static void Run()
        {
            string report = ProjectPath.Report("HandWeightRegression.txt");
            File.WriteAllText(report, "Actual Unity Mesh/BoneWeight filter regression. No tracking or rendered geometry claim.\n");
            var filter = typeof(CandySmoothHandMesh).GetMethod("SeparateDigitWeights", BindingFlags.Static | BindingFlags.NonPublic);
            if (filter == null) throw new InvalidOperationException("Digit-weight filter is missing.");

            Check(filter, report, "Two retained", new BoneWeight { boneIndex0 = 14, weight0 = .6f, boneIndex1 = 4, weight1 = .4f }, 14, 10, 2, .4f);
            Check(filter, report, "Three retained", new BoneWeight { boneIndex0 = 14, weight0 = .5f, boneIndex1 = 15, weight1 = .3f, boneIndex2 = 4, weight2 = .2f }, 14, 10, 3, .2f);
            Check(filter, report, "Four retained", new BoneWeight { boneIndex0 = 14, weight0 = .4f, boneIndex1 = 15, weight1 = .3f, boneIndex2 = 16, weight2 = .2f, boneIndex3 = 4, weight3 = .1f }, 14, 10, 4, .1f);
            Check(filter, report, "Four equal", new BoneWeight { boneIndex0 = 14, weight0 = .25f, boneIndex1 = 15, weight1 = .25f, boneIndex2 = 16, weight2 = .25f, boneIndex3 = 4, weight3 = .25f }, 14, 10, 4, .25f);
            Check(filter, report, "Neighbor removed, palm retained", new BoneWeight { boneIndex0 = 14, weight0 = .4f, boneIndex1 = 17, weight1 = .3f, boneIndex2 = 4, weight2 = .2f, boneIndex3 = 16, weight3 = .1f }, 14, 10, 3, 2f / 7);
            Check(filter, report, "Flying hand layout", new BoneWeight { boneIndex0 = 1, weight0 = .4f, boneIndex1 = 2, weight1 = .3f, boneIndex2 = 3, weight2 = .2f, boneIndex3 = 0, weight3 = .1f }, 1, 5, 4, .1f);
            File.AppendAllText(report, "COMPLETE checks=6 passed=True\n");
        }

        static void Check(MethodInfo filter, string report, string name, BoneWeight input, int first, int digits, int expectedCount, float expectedPalm)
        {
            var source = new Mesh
            {
                name = "Hand weight regression fixture",
                vertices = new Vector3[2],
                bindposes = new Matrix4x4[first + digits * 3]
            };
            var bindings = source.bindposes;
            for (int i = 0; i < bindings.Length; i++) bindings[i] = Matrix4x4.identity;
            source.bindposes = bindings;
            source.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = first, weight0 = .5f, boneIndex1 = first + 3, weight1 = .5f },
                input
            };
            Mesh result = null;
            try
            {
                result = (Mesh)filter.Invoke(null, new object[] { source, first, digits });
                if (!result || result == source) throw new InvalidOperationException(name + " did not clean mixed digit weights.");
                var actual = result.boneWeights[1];
                var weights = new[] { actual.weight0, actual.weight1, actual.weight2, actual.weight3 };
                var bones = new[] { actual.boneIndex0, actual.boneIndex1, actual.boneIndex2, actual.boneIndex3 };
                float sum = 0;
                int count = 0;
                for (int i = 0; i < 4; i++)
                {
                    sum += weights[i];
                    if (weights[i] <= 0) continue;
                    count++;
                    if (bones[i] >= first && bones[i] < first + digits * 3 && (bones[i] - first) / 3 != 0)
                        throw new InvalidOperationException(name + " retains another digit.");
                }
                if (Mathf.Abs(sum - 1) > .00001f || count != expectedCount)
                    throw new InvalidOperationException(name + " lost an influence or normalized weight.");
                int palm = first == 1 ? 0 : 4;
                float actualPalm = 0;
                for (int i = 0; i < 4; i++) if (bones[i] == palm) actualPalm += weights[i];
                if (Mathf.Abs(expectedPalm - actualPalm) > .00001f)
                    throw new InvalidOperationException(name + " changed the retained palm weight.");
                var original = source.boneWeights[1];
                if (original.weight0 != input.weight0 || original.weight1 != input.weight1 || original.weight2 != input.weight2 || original.weight3 != input.weight3)
                    throw new InvalidOperationException(name + " mutated the source asset.");
                if ((Mesh)filter.Invoke(null, new object[] { source, first, digits }) != result)
                    throw new InvalidOperationException(name + " failed to reuse its cached result.");
                File.AppendAllText(report, "PASS " + name + "\n");
            }
            finally
            {
                if (result && result != source) Object.DestroyImmediate(result);
                Object.DestroyImmediate(source);
            }
        }
    }
}
