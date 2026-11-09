using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CastlePrototype
{
    [DisallowMultipleComponent]
    public sealed class MarinaDockActivities : MonoBehaviour
    {
        const int SolidMask = 1 | (1 << Layers.Surface);
        static readonly Vector3 DockReturn = new(-20.6f, 1.29f, -24.85f);
        static readonly Vector3 KnifeRack = new(11.5f, 4.96f, 15.25f);
        static readonly Vector3[] Berths = { new(-23, -.12f, -27.45f), new(-20.4f, -.12f, -27.45f) };
        readonly Transform[] skis = new Transform[2];
        readonly List<Knife> knives = new(3);

        sealed class Knife
        {
            public Transform body;
            public Vector3 velocity;
            public float expires;
        }

        IslandLobby lobby;
        InputAction ride, steering, throwKnife;
        Transform riderParent, mounted;
        CharacterController riderBody;
        int berth;
        float speed, nextThrow;
        bool knifeArmed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => GameEvents.LobbyStarted += Attach;
        static void Attach(GameObject owner)
        {
            if (owner && owner.TryGetComponent<IslandLobby>(out var lobby) && lobby.Radius > 30 &&
                !owner.GetComponent<MarinaDockActivities>())
                owner.AddComponent<MarinaDockActivities>();
        }

        void Start()
        {
            lobby = GetComponent<IslandLobby>();
            ride = new InputAction("Marina board or return", InputActionType.Button, "<XRController>{RightHand}/gripPressed");
            steering = new InputAction("Marina jet ski steer", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis", expectedControlType: "Vector2");
            throwKnife = new InputAction("Marina practice throw", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
            ride.Enable();
            steering.Enable();
            throwKnife.Enable();
            for (int i = 0; i < skis.Length; i++)
                skis[i] = Body("Marina jet ski " + (i + 1), Berths[i], Quaternion.Euler(0, 180, 0), "MarinaJetSki");
            Body("Marina practice knife rack", KnifeRack, Quaternion.identity, "MarinaKnifeRack");
            for (int i = 0; i < 6; i++)
            {
                var target = Body("Marina knife target " + (i + 1), new Vector3(10.5f + i * 1.4f, 5.2f, 18.04f),
                    Quaternion.identity, null);
                var hit = target.gameObject.AddComponent<BoxCollider>();
                hit.size = new Vector3(.82f, .82f, .15f);
                hit.isTrigger = true;
            }
        }

        Transform Body(string label, Vector3 localPosition, Quaternion localRotation, string cue)
        {
            var body = new GameObject(label).transform;
            body.SetParent(transform, false);
            body.SetLocalPositionAndRotation(localPosition, localRotation);
            if (cue != null) GameEvents.RaiseEmoted(body.gameObject, cue);
            return body;
        }

        void LateUpdate()
        {
            if (!lobby || !NetworkActors.Alive(lobby.Player)) return;
            bool active = ReferenceEquals(Services.Session, lobby) && !lobby.MatchResident &&
                lobby.Player.gameObject.activeInHierarchy && !lobby.StartPending && lobby.Room && !lobby.Room.Connected && !lobby.Room.Busy;
            if (!active || lobby.MenuOpen)
            {
                if (mounted) ReturnToDock(active);
                knifeArmed = false;
                if (!active) ClearKnives();
                else StepKnives();
                return;
            }

            var keyboard = Keyboard.current;
            bool interact = lobby.Player.IsVR ? ride.WasPressedThisFrame() : keyboard?.eKey.wasPressedThisFrame == true;
            if (mounted)
            {
                if (interact) ReturnToDock(true);
                else Drive(ReadSteering(keyboard), Mathf.Min(Time.deltaTime, .05f));
            }
            else if (interact) TryBoard();

            if (!mounted) CheckKnifeInput(interact);
            StepKnives();
        }

        void TryBoard()
        {
            if (lobby.Player.InputSuspended) return;
            var feet = lobby.Player.transform.position;
            int closest = -1;
            float distance = 2.3f;
            for (int i = 0; i < skis.Length; i++)
            {
                float reach = Vector3.Distance(feet, skis[i].TransformPoint(Vector3.up * .45f));
                if (reach < distance) { closest = i; distance = reach; }
            }
            if (closest < 0 || !lobby.Player.gameObject.TryGetComponent(out riderBody) || !riderBody.enabled) return;

            berth = closest;
            mounted = skis[closest];
            riderParent = lobby.Player.transform.parent;
            var seat = mounted.TransformPoint(new Vector3(0, .56f, -.28f));
            (lobby.Player as ITeleportable)?.Teleport(seat, mounted.rotation);
            riderBody.enabled = false;
            lobby.Player.InputSuspended = true;
            lobby.Player.transform.SetParent(mounted, false);
            lobby.Player.transform.SetLocalPositionAndRotation(new Vector3(0, .56f, -.28f), Quaternion.identity);
            knifeArmed = false;
            speed = 0;
        }

        Vector2 ReadSteering(Keyboard keyboard)
        {
            if (lobby.Player.IsVR) return Vector2.ClampMagnitude(steering.ReadValue<Vector2>(), 1);
            if (keyboard == null) return Vector2.zero;
            return new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
        }

        void Drive(Vector2 input, float dt)
        {
            speed = Mathf.MoveTowards(speed, input.y * (input.y >= 0 ? 8 : 2.4f), dt * 4);
            float steer = input.x * (35 + 32 * Mathf.Clamp01(Mathf.Abs(speed) / 5)) * dt;
            var rotation = mounted.rotation * Quaternion.Euler(0, steer, 0);
            var delta = rotation * Vector3.forward * (speed * dt);
            var position = mounted.position;
            var next = position + delta;
            var relative = transform.InverseTransformPoint(next);
            var motionOrigin = position + Vector3.up * .24f;
            if (delta.sqrMagnitude > .000001f && (new Vector2(relative.x, relative.z).sqrMagnitude > 48 * 48 ||
                Physics.SphereCast(motionOrigin, .42f, delta.normalized, out _, delta.magnitude + .55f, SolidMask, QueryTriggerInteraction.Ignore) ||
                Physics.Raycast(next + Vector3.up * 2, Vector3.down, 1.97f, SolidMask, QueryTriggerInteraction.Ignore)))
            {
                speed = 0;
                next = position;
            }
            mounted.SetPositionAndRotation(next, rotation);
        }

        void ReturnToDock(bool restoreInput)
        {
            if (!mounted) return;
            if (NetworkActors.Alive(lobby.Player))
            {
                var player = lobby.Player;
                player.transform.SetParent(riderParent, true);
                if (riderBody) riderBody.enabled = true;
                (player as ITeleportable)?.Teleport(transform.TransformPoint(DockReturn), transform.rotation);
                if (restoreInput) lobby.SetMenu(lobby.MenuOpen);
            }
            mounted.SetLocalPositionAndRotation(Berths[berth], Quaternion.Euler(0, 180, 0));
            mounted = null;
            riderBody = null;
            speed = 0;
        }

        void CheckKnifeInput(bool interact)
        {
            var player = lobby.Player;
            if (player.InputSuspended) return;
            var local = transform.InverseTransformPoint(player.transform.position);
            if (local.x < 9.1f || local.x > 19 || local.z < 12.8f || local.z > 18.1f || local.y < 3.7f)
            {
                knifeArmed = false;
                return;
            }

            bool shot = player.IsVR ? throwKnife.WasPressedThisFrame() : Mouse.current?.leftButton.wasPressedThisFrame == true;
            if ((player.IsVR ? shot && !knifeArmed : interact) && Vector3.Distance(local, KnifeRack) < 1.8f)
            {
                knifeArmed = !knifeArmed;
                return;
            }
            if (!knifeArmed || !shot || Time.time < nextThrow || knives.Count >= 3 || !player.ViewCamera) return;
            var aim = new Pose(player.ViewCamera.transform.position, player.ViewCamera.transform.rotation);
            if (player.IsVR && player is IModeActor actor)
            {
                var hand = actor.HandPose(false);
                if ((hand.position - aim.position).sqrMagnitude < 2.25f && hand.rotation != default && Vector3.Dot(hand.rotation * Vector3.forward, aim.rotation * Vector3.forward) > .3f) aim = hand;
            }
            LaunchKnife(aim);
        }

        void LaunchKnife(Pose aim)
        {
            nextThrow = Time.time + .32f;
            var forward = aim.rotation * Vector3.forward;
            var velocity = forward * 13 + Vector3.up * 1.1f;
            var origin = aim.position + forward * .22f;
            var body = Body("Thrown practice knife", transform.InverseTransformPoint(origin),
                Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(velocity), "MarinaThrownKnife");
            knives.Add(new Knife { body = body, velocity = velocity, expires = Time.time + 2.2f });
        }

        void StepKnives()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            for (int i = knives.Count - 1; i >= 0; i--)
            {
                var knife = knives[i];
                if (!knife.body || Time.time >= knife.expires)
                {
                    if (knife.body) Destroy(knife.body.gameObject);
                    knives.RemoveAt(i);
                    continue;
                }
                if (knife.velocity == Vector3.zero) continue;
                var position = knife.body.position;
                var delta = knife.velocity * dt;
                if (Physics.Raycast(position, delta.normalized, out var hit, delta.magnitude + .20f, SolidMask, QueryTriggerInteraction.Collide))
                {
                    knife.body.position = hit.point - delta.normalized * .17f;
                    knife.velocity = Vector3.zero;
                    knife.expires = Time.time + (hit.collider.transform.parent == transform && hit.collider.isTrigger ? 1.6f : .35f);
                    continue;
                }
                knife.velocity += Physics.gravity * dt;
                knife.body.SetPositionAndRotation(position + delta, Quaternion.LookRotation(knife.velocity));
            }
        }

        void ClearKnives()
        {
            foreach (var knife in knives) if (knife.body) Destroy(knife.body.gameObject);
            knives.Clear();
        }

        void OnDisable()
        {
            if (mounted) ReturnToDock(false);
            ClearKnives();
        }

        void OnDestroy()
        {
            ride?.Dispose();
            steering?.Dispose();
            throwKnife?.Dispose();
        }
    }
}
