using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    [CreateAssetMenu(menuName="Candy Players/Style")]
    public sealed class CandyPlayerStyle : ScriptableObject
    {
        public Material material;
        public Color[] colors={new Color(.97f,.1f,.16f),new Color(1,.72f,.015f),new Color(.04f,.35f,1),new Color(.04f,.78f,.28f),new Color(.96f,.06f,.56f),new Color(.58f,.19f,.98f),new Color(1,.3f,.025f),new Color(.025f,.77f,.94f)};
        [Tooltip("Head mesh center relative to the tracked eye pose, in avatar metres.")]
        public Vector3 headCenterOffset = new Vector3(0, 0, -.08f);
        public Vector3 controllerPalmOffset=new Vector3(0,0,.025f);
        public Vector3 leftControllerRotation,rightControllerRotation;
        [Range(0,1)]public float smoothness=.78f;
        public bool lockHandsDuringVault=true;
        public Color ColorAt(int index)=>colors!=null&&colors.Length>0?colors[(index%colors.Length+colors.Length)%colors.Length]:Color.yellow;
    }
}
