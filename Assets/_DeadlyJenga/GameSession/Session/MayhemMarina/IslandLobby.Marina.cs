using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobby
    {
        public const string LegacyLobbyPreference="DeadlyJenga.Lobby.UseLegacyBackup";
        partial void ConfigureMayhemMarinaLobby()
        {
            if(PlayerPrefs.GetInt(LegacyLobbyPreference,0)==1)return;
            lobbyLayoutCenter=new Vector3(0,0,1);lobbyLayoutTop=1.33f;lobbyLayoutRadius=37;
            lobbyLayoutBounds=new Bounds(new Vector3(0,6,1),new Vector3(66,24,66));
            lobbyLayoutSpawn=new Vector3(-10,1.37f,-8);lobbyLayoutReturnDistance=65;
        }
        static partial void MayhemMarinaSpawn(int slot,ref Vector3 point)
        {if(!Persistent||Persistent.Radius<30)return;float a=(System.Math.Abs(slot)%10)*2.399963f;var local=new Vector3(-10+Mathf.Sin(a)*2.3f,1.37f,-8+Mathf.Cos(a)*2.3f);point=Persistent.transform.TransformPoint(local);}
        partial void MayhemMarinaFloor(GameObject floor,Mesh mesh,Transform pose)
        {if(Radius<30)return;floor.layer=Layers.Surface;if(!floor.GetComponent<MarinaLobbySurface>())floor.AddComponent<MarinaLobbySurface>();}
    }
    public sealed class MarinaLobbySurface : MonoBehaviour,IClimbable
    {
        public bool CanClimb=>isActiveAndEnabled;
        public float Grip=>1;
        public Transform Anchor=>transform;
        public Rigidbody Body=>null;
        public Transform Visual=>transform;
    }
}
