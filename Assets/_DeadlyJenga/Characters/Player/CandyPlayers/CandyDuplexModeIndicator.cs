using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    sealed class CandyDuplexModeIndicator : MonoBehaviour
    {
        Transform carriage, rifle, shotgun;

        public void Initialize(Transform rotor, Transform rifleDisplay, Transform shotgunDisplay)
        {
            carriage = rotor;
            rifle = rifleDisplay;
            shotgun = shotgunDisplay;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (!carriage) return;
            bool shotgunUp = (carriage.localRotation * Vector3.up).y >= 0;
            if (shotgun.gameObject.activeSelf == shotgunUp) return;
            shotgun.gameObject.SetActive(shotgunUp);
            rifle.gameObject.SetActive(!shotgunUp);
        }
    }
}
