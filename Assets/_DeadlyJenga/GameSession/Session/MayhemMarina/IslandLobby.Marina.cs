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
            lobbyLayoutSpawn=new Vector3(0,1.92f,6.2f);lobbyLayoutReturnDistance=65;
        }
        static partial void MayhemMarinaSpawn(int slot,ref Vector3 point)
        {if(!Persistent||Persistent.Radius<30||slot<0||slot>=10)return;var local=new Vector3(-2.4f+1.2f*(slot%5),1.92f,6.2f+1.4f*(slot/5));point=Persistent.transform.TransformPoint(local);}
        partial void MayhemMarinaFloor(GameObject floor,Mesh mesh,Transform pose)
        {if(Radius<30)return;marinaSpawnFloor=floor.GetComponent<MeshCollider>();InvalidateMarinaSpawnClearance();floor.layer=Layers.Surface;if(!floor.GetComponent<MarinaLobbySurface>())floor.AddComponent<MarinaLobbySurface>();}
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
