using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public enum DeckShipEvent { None, Roll, Pitch, Iceberg, Righting }

    // Ship events. The hull is moved, never the cargo: blocks react through friction and inertia alone.
    // Angles: roll > 0 dips the starboard side, pitch > 0 dips the bow, yaw > 0 turns the bow to starboard.
    public sealed partial class DeckShipMap
    {
        [Header("Ship events")]
        [Tooltip("Run rolls, pitches and the iceberg by themselves.")]
        public bool autoEvents = true;
        public float calmSeconds = 20, gapMin = 16, gapMax = 28;
        [Tooltip("Rolls and pitches before the iceberg arrives.")]
        public int eventsBeforeIceberg = 3;
        [Tooltip("After the iceberg the ship rights itself and new cargo drops onto the deck.")]
        public bool newCargoAfterIceberg = true;
        public float swayRoll = 2.5f, swayPitch = 1, swayHeave = .2f;
        public float rollDegrees = 17, pitchDegrees = 7;
        public float icebergList = 7, icebergShove = 7;

        public const float RollSeconds = 18, PitchSeconds = 15, IcebergSeconds = 18, RightingSeconds = 8, IcebergImpact = 5;

        float clock, eventStart, nextEventAt;
        int eventsDone;
        float listRoll, listPitch, listFromRoll, listFromPitch;
        bool shoved;
        Transform iceberg;
        readonly HashSet<IPushable> pushed = new HashSet<IPushable>();
        readonly Collider[] deckHits = new Collider[64];

        public DeckShipEvent CurrentEvent { get; private set; }
        public float ShipTime => clock;
        public float EventTime => clock - eventStart;
        public float RollAngle { get; private set; }
        public float PitchAngle { get; private set; }

        void ResetMotion()
        {
            clock = 0; eventStart = 0; eventsDone = 0; nextEventAt = calmSeconds;
            listRoll = listPitch = 0; shoved = false;
            CurrentEvent = DeckShipEvent.None;
            if (iceberg) iceberg.gameObject.SetActive(false);
            // Jump straight to the starting pose: moving there in one physics step would fling the new cargo.
            ApplyPose(true);
        }

        // Starts an event now (tools, game modes). Replaces any event already running.
        public void StartEvent(DeckShipEvent kind)
        {
            if (replica) return;
            if (CurrentEvent == DeckShipEvent.Iceberg) FinishIceberg();
            CurrentEvent = kind; eventStart = clock; shoved = false;
            listFromRoll = listRoll; listFromPitch = listPitch;
            if (kind == DeckShipEvent.Iceberg && iceberg) iceberg.gameObject.SetActive(true);
        }

        // Advances ship time and moves the hull. Called from FixedUpdate; tests may call it with Physics.Simulate.
        public void Step(float dt)
        {
            if (!hullBody) return;
            clock += dt;
            if (autoEvents) Direct();
            ApplyPose();
        }

        void Direct()
        {
            float e = clock - eventStart;
            switch (CurrentEvent)
            {
                case DeckShipEvent.Roll when e >= RollSeconds:
                case DeckShipEvent.Pitch when e >= PitchSeconds:
                    CurrentEvent = DeckShipEvent.None; nextEventAt = clock + Random.Range(gapMin, gapMax); break;
                case DeckShipEvent.Iceberg when e >= IcebergSeconds:
                    FinishIceberg();
                    if (newCargoAfterIceberg) StartEvent(DeckShipEvent.Righting);
                    else { CurrentEvent = DeckShipEvent.None; nextEventAt = clock + gapMax; }
                    break;
                case DeckShipEvent.Righting when e >= RightingSeconds:
                    CurrentEvent = DeckShipEvent.None; listRoll = listPitch = 0;
                    NextCargo(true);
                    eventsDone = 0; nextEventAt = clock + calmSeconds; break;
                case DeckShipEvent.None when clock >= nextEventAt:
                    if (eventsDone >= eventsBeforeIceberg) StartEvent(DeckShipEvent.Iceberg);
                    else StartEvent(eventsDone % 2 == 0 ? DeckShipEvent.Roll : DeckShipEvent.Pitch);
                    eventsDone++;
                    break;
            }
        }

        void ApplyPose(bool teleport = false)
        {
            if (!hullBody) return;
            float t = clock, e = clock - eventStart;
            float roll = swayRoll * Mathf.Sin(t * 2 * Mathf.PI / 9.3f);
            float pitch = swayPitch * Mathf.Sin(t * 2 * Mathf.PI / 12.7f + 1);
            float heave = swayHeave * Mathf.Sin(t * 2 * Mathf.PI / 7.1f + .5f);
            float yaw = 0, surge = 0;
            switch (CurrentEvent)
            {
                case DeckShipEvent.Roll:
                    roll += rollDegrees * Envelope(e, RollSeconds, 3) * Mathf.Sin(e * 2 * Mathf.PI / 8);
                    break;
                case DeckShipEvent.Pitch:
                {
                    float env = Envelope(e, PitchSeconds, 3);
                    pitch += pitchDegrees * env * Mathf.Sin(e * 2 * Mathf.PI / 7);
                    heave += .6f * env * Mathf.Cos(e * 2 * Mathf.PI / 7);
                    break;
                }
                case DeckShipEvent.Iceberg:
                {
                    MoveIceberg(e);
                    float k = e - IcebergImpact;
                    if (k >= 0)
                    {
                        // A hard jolt backwards and to port, a wobble, then the ship settles leaning into the damage.
                        float jolt = k < .25f ? k / .25f : Mathf.Exp(-(k - .25f) * 1.6f);
                        surge = -1.4f * jolt;
                        yaw = -5 * jolt;
                        roll += 13 * Mathf.Exp(-.5f * k) * Mathf.Sin(k * 2 * Mathf.PI / 3.5f);
                        float sink = Mathf.SmoothStep(0, 1, k / 6);
                        listRoll = Mathf.Lerp(listFromRoll, listFromRoll + icebergList, sink);
                        listPitch = Mathf.Lerp(listFromPitch, listFromPitch + 2, sink);
                        if (!shoved) { shoved = true; ShoveDeck(); }
                    }
                    break;
                }
                case DeckShipEvent.Righting:
                {
                    float s = Mathf.SmoothStep(0, 1, e / RightingSeconds);
                    listRoll = Mathf.Lerp(listFromRoll, 0, s);
                    listPitch = Mathf.Lerp(listFromPitch, 0, s);
                    break;
                }
            }
            roll += listRoll; pitch += listPitch;
            RollAngle = roll; PitchAngle = pitch;
            var local = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(pitch, 0, 0) * Quaternion.Euler(0, 0, -roll);
            var position = transform.TransformPoint(new Vector3(0, heave, surge));
            var rotation = transform.rotation * local;
            if (!teleport && (Application.isPlaying || Physics.simulationMode == SimulationMode.Script)) { hullBody.MovePosition(position); hullBody.MoveRotation(rotation); }
            else { hullBody.position = position; hullBody.rotation = rotation; hull.SetPositionAndRotation(position, rotation); Physics.SyncTransforms(); }
        }

        // 0 -> 1 over ramp seconds at the start, back to 0 over ramp seconds at the end.
        static float Envelope(float e, float duration, float ramp) =>
            Mathf.SmoothStep(0, 1, Mathf.Clamp01(e / ramp)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((duration - e) / ramp));

        // On impact, everyone standing on the ship is thrown toward the bow (blocks slide on their own).
        void ShoveDeck()
        {
            pushed.Clear();
            var center = hullBody.position + hullBody.rotation * new Vector3(0, DeckHeight + 6, 0);
            int count = Physics.OverlapBoxNonAlloc(center, new Vector3(HalfBeam + 1, 7, 38), deckHits, hullBody.rotation, 1 << Layers.Survivor, QueryTriggerInteraction.Ignore);
            var push = hullBody.rotation * new Vector3(-.25f, .3f, 1).normalized * icebergShove;
            for (int i = 0; i < count; i++)
            {
                var target = deckHits[i].GetComponentInParent<IPushable>();
                if (target != null && pushed.Add(target)) target.Push(push, gameObject);
                deckHits[i] = null;
            }
            foreach (var body in bodies) if (body && !body.isKinematic) body.WakeUp();
        }

        // The iceberg is scenery that drifts past (no collision). It meets the starboard bow at IcebergImpact.
        void BuildIceberg()
        {
            iceberg = new GameObject("Iceberg (art)").transform;
            iceberg.SetParent(transform, false);
            void Chunk(Vector3 p, Vector3 s, Vector3 euler, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Ice";
                var col = go.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
                go.transform.SetParent(iceberg, false);
                go.transform.localPosition = p; go.transform.localScale = s; go.transform.localEulerAngles = euler;
                go.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
            Chunk(new Vector3(0, 3, 0), new Vector3(10, 12, 9), new Vector3(0, 20, 8), ice);
            Chunk(new Vector3(3, 8, 1), new Vector3(5, 9, 5), new Vector3(10, 40, -12), ice);
            Chunk(new Vector3(-3.5f, 1.5f, -2), new Vector3(7, 6, 7), new Vector3(0, 65, 0), iceShade);
            Chunk(new Vector3(1, 12, -1), new Vector3(3, 5, 3), new Vector3(15, 10, 20), ice);
            Chunk(new Vector3(0, -3, 0), new Vector3(16, 6, 14), new Vector3(0, 30, 0), iceShade);
            iceberg.gameObject.SetActive(false);
        }

        void MoveIceberg(float e)
        {
            if (!iceberg) return;
            var start = new Vector3(15, 0, 95); var hit = new Vector3(17, 0, 31); var past = new Vector3(19.5f, 0, -45);
            Vector3 p;
            if (e < IcebergImpact) p = Vector3.Lerp(start, hit, e / IcebergImpact);
            else p = Vector3.Lerp(hit, past, (e - IcebergImpact) / (IcebergSeconds - IcebergImpact));
            float sink = Mathf.Clamp01((e - (IcebergSeconds - 5)) / 5);
            p.y = -sink * 20;
            iceberg.localPosition = p;
        }

        void FinishIceberg() { if (iceberg) iceberg.gameObject.SetActive(false); }
    }
}
