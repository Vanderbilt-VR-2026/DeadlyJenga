using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastleBots;

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
        string posing;          // an emote this component is posing (local player only; bots pose themselves)
        float began, duration;
        int sending;            // emote index + 1 while an emote is showing, else 0
        float sendUntil;

        public int NetworkBits => Time.time < sendUntil ? sending : 0;
        bool IsLocalPlayer => avatar && !avatar.remoteDriven && !GetComponent<CastleBotBrain>();

        void Awake() => avatar = GetComponent<CandyPlayerAvatar>();
        void OnEnable()
        {
            Emotes.LocalRequested += OnRequested;
            GameEvents.Emoted += OnEmoted;
            GameEvents.Eliminated += OnEliminated;
            Application.onBeforeRender += BeforeRender;
        }
        void OnDisable()
        {
            Emotes.LocalRequested -= OnRequested;
            GameEvents.Emoted -= OnEmoted;
            GameEvents.Eliminated -= OnEliminated;
            Application.onBeforeRender -= BeforeRender;
            posing = null;
        }

        void OnRequested(int index)
        {
            if (!IsLocalPlayer || !isActiveAndEnabled || (Services.Session?.PlayingHand ?? false)) return;
            if (index < 0 || index >= Emotes.Survivor.Length || posing != null) return;
            posing = Emotes.Survivor[index]; began = Time.time; duration = CandyEmotePoses.Duration(posing);
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
            posing = null;
        }

        void LateUpdate() => Pose();
        [BeforeRenderOrder(200)] void BeforeRender() => Pose();

        void Pose()
        {
            if (posing == null || !avatar || !avatar.Rig) return;
            float t = Time.time - began;
            if (t >= duration || !avatar.Rig.gameObject.activeInHierarchy) { posing = null; return; }
            avatar.Rig.ApplyFrame(CandyEmotePoses.Pose(avatar.LastFrame, posing, t, duration), true, 0);
        }
    }
}
