using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CandyPlayers
{
    // Lives on every candy survivor (player and bots). It
    //  - plays the local player's emotes (body pose + GameEvents.Emoted),
    //  - remembers the current emote of this survivor so it can be sent to other players,
    //  - turns this survivor's elimination into a BodyBurst for the effects.
    [DisallowMultipleComponent]
    public sealed class SurvivorMoments : MonoBehaviour
    {
        CandyPlayerAvatar avatar;
        CastleSurvivor survivor;
        CandyEquipment equipment;
        Vector3 beganAt;
        Vector2 beganHead;
        Quaternion facing;
        bool beganInVR;
        string posing;          // an emote this component is posing (local player only; bots pose themselves)
        float began, duration;
        int sending;            // emote index + 1 while an emote is showing, else 0
        float sendUntil;

        public int NetworkBits => Time.time < sendUntil ? sending : 0;
        bool IsLocalPlayer => avatar && !avatar.remoteDriven && !GetComponent<CastleBotBrain>();

        public bool Posing => posing != null && CanPose && survivor.IsVR == beganInVR && Time.time - began < duration;

        bool CanPose
        {
            get
            {
                var session = Services.Session;
                return survivor && survivor.isActiveAndEnabled && survivor.Alive && survivor.Grounded &&
                    !survivor.Climbing && !survivor.Vaulting && !survivor.Sinking && !survivor.InputSuspended &&
                    survivor.HasInputFocus && avatar && avatar.Rig && avatar.Rig.gameObject.activeInHierarchy &&
                    session != null && ReferenceEquals(session.Player, survivor) && !session.MenuOpen &&
                    !session.PlayingHand && session.ActiveCamera == survivor.ViewCamera &&
                    (session.Demo as ISpectatorControls)?.Spectating != true &&
                    MatchRuntime.Current?.Frame?.phase != RoundPhase.Results;
            }
        }

        void Awake()
        {
            avatar = GetComponent<CandyPlayerAvatar>();
            survivor = GetComponent<CastleSurvivor>();
            equipment = GetComponent<CandyEquipment>();
        }
        void OnEnable()
        {
            Emotes.LocalRequested += OnRequested;
            GameEvents.Emoted += OnEmoted;
            GameEvents.Eliminated += OnEliminated;
        }
        void OnDisable()
        {
            Emotes.LocalRequested -= OnRequested;
            GameEvents.Emoted -= OnEmoted;
            GameEvents.Eliminated -= OnEliminated;
            Cancel();
        }

        void OnRequested(int index)
        {
            if (!IsLocalPlayer || !isActiveAndEnabled || !CanPose || index < 0 || index >= Emotes.Survivor.Length) return;
            string next = Emotes.Survivor[index];
            if (Posing && posing == next) { Cancel(); return; }

            posing = next;
            began = Time.time;
            duration = CandyEmotePoses.Loops(next) ? float.PositiveInfinity : CandyEmotePoses.Duration(next);
            beganAt = transform.position;
            beganHead = HeadOffset();
            beganInVR = survivor.IsVR;
            facing = Quaternion.Euler(0, survivor.ViewCamera.transform.eulerAngles.y, 0);
            GameEvents.RaiseEmoted(gameObject, posing);
        }

        void OnEmoted(GameObject who, string emote)
        {
            if (who != gameObject) return;
            int i = System.Array.IndexOf(Emotes.Survivor, emote);
            if (i < 0) return;
            sending = i + 1; sendUntil = Time.time + CandyEmotePoses.Duration(emote);
        }

        void OnEliminated(GameObject who, GameObject source)
        {
            if (who != gameObject || !avatar) return;
            var rig = avatar.Rig;
            var color = rig && rig.style && rig.style.colors.Length > 0 ? rig.style.colors[(avatar.ColorIndex & 0xFF) % rig.style.colors.Length] : Color.white;
            var at = rig ? rig.transform.position : transform.position;
            bool local = IsLocalPlayer;
            GameEvents.RaiseBodyBurst(gameObject, at, color, local ? "You" : gameObject.name.Replace(" [AI]", ""), local);
            Cancel();
        }

        public void Cancel() => posing = null;

        Vector2 HeadOffset()
        {
            Vector3 local = transform.InverseTransformPoint(survivor.ViewCamera.transform.position);
            return new Vector2(local.x, local.z);
        }

        void Update()
        {
            if (posing == null) return;
            if (!Posing ||
                (transform.position - beganAt).sqrMagnitude > .09f ||
                (beganInVR && (HeadOffset() - beganHead).sqrMagnitude > .04f))
                Cancel();
        }

        public CandyAvatarFrame Pose(CandyAvatarFrame frame)
        {
            if (!Posing) return frame;
            frame.headRotation = facing;
            var posed = CandyEmotePoses.Pose(frame, posing, Time.time - began, duration);
            var held = equipment ? equipment.Equipment : null;
            if (held != null && held.drawn && held.kind >= EquipmentKind.Blaster &&
                held.kind != EquipmentKind.PoweredArmor && !EquipmentSpecifications.Get(held.kind).twoHanded)
            {
                bool paired = held.kind is EquipmentKind.DualUzi or EquipmentKind.LaserClaws;
                if (held.leftHanded || paired) KeepGrip(ref posed.left, frame.left);
                if (!held.leftHanded || paired) KeepGrip(ref posed.right, frame.right);
            }
            return posed;
        }

        static void KeepGrip(ref CandyHandPose posed, CandyHandPose equipped)
        {
            if (!equipped.skeletonTracked) return;
            posed.skeleton = equipped.skeleton;
            posed.skeletonTracked = true;
        }
    }
}
