using System;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class UnderneathTreeTrain : MonoBehaviour
    {
        const float MetresPerSecond = 4.5f;
        Vector3[] path;
        float[] distance;
        UnderneathTreeGiftPanel[] track;
        float progress;
        Transform[] cars = new Transform[0];
        float[] carsBehind = new float[0];

        internal void SetUp(Vector3[] course, UnderneathTreeGiftPanel[] sections, float startFraction = 0f)
        {
            path = course;
            track = sections;
            distance = new float[course.Length + 1];
            for (int i = 0; i < course.Length; i++)
                distance[i + 1] = distance[i] + Vector3.Distance(course[i], course[(i + 1) % course.Length]);
            progress = Mathf.Repeat(startFraction, 1f) * distance[distance.Length - 1];
            PoseTrain();
        }

        // Coaches follow the engine at fixed distances along the rails.
        internal void AddCars(Transform[] coaches, float[] behind)
        {
            cars = coaches;
            carsBehind = behind;
            PoseTrain();
        }

        void Update()
        {
            if (path == null || path.Length < 3)
                return;
            int next = SectionAt(progress + 2.6f);
            if (!TrackIntact(track[next]) || !TrackIntact(track[SectionAt(progress)]))
                return;
            // A coach over a broken stretch holds the whole train.
            foreach (float behind in carsBehind)
                if (!TrackIntact(track[SectionAt(progress - behind)]))
                    return;
            progress = Mathf.Repeat(progress + Time.deltaTime * MetresPerSecond, distance[distance.Length - 1]);
            PoseTrain();
        }

        void PoseTrain()
        {
            Pose(transform, progress);
            for (int i = 0; i < cars.Length; i++)
                if (cars[i])
                    Pose(cars[i], progress - carsBehind[i]);
        }

        void Pose(Transform body, float travelled)
        {
            var point = Sample(travelled);
            var nose = Sample(travelled + 1.4f);
            var tail = Sample(travelled - 1.4f);
            body.SetLocalPositionAndRotation(point + Vector3.up * .35f, Quaternion.LookRotation(nose - tail));
        }

        static bool TrackIntact(UnderneathTreeGiftPanel segment) =>
            segment && segment.gameObject.activeInHierarchy && !segment.IsLoose &&
            segment.IsAtWrappedPose();

        int SectionAt(float travelled)
        {
            travelled = Mathf.Repeat(travelled, distance[distance.Length - 1]);
            int index = Array.BinarySearch(distance, travelled);
            return index < 0 ? ~index - 1 : index;
        }

        Vector3 Sample(float travelled)
        {
            travelled = Mathf.Repeat(travelled, distance[distance.Length - 1]);
            int index = SectionAt(travelled);
            float t = Mathf.InverseLerp(distance[index], distance[index + 1], travelled);
            return Vector3.Lerp(path[index], path[(index + 1) % path.Length], t);
        }
    }
}
