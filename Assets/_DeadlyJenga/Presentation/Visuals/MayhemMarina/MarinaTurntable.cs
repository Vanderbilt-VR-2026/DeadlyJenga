using UnityEngine;

namespace DeadlyJenga.CandyArt.Marina
{
    // Turns a lobby display slowly around its own up axis, and frees the display's meshes when it goes away.
    public sealed class MarinaTurntable : MonoBehaviour
    {
        public float degreesPerSecond = 9f;
        MarinaMesh owned;

        public void Own(MarinaMesh mesh, float speed)
        {
            owned = mesh;
            degreesPerSecond = speed;
        }

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);

        void OnDestroy() => owned?.Dispose();
    }
}
