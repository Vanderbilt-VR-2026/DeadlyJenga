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
            lobbyLayoutSpawn=new Vector3(0,1.95f,5.6f);lobbyLayoutReturnDistance=65;
            lobbyLayoutFacing=Quaternion.identity;   // arrivals stand on the stage, already facing the play screen
        }
        static partial void MayhemMarinaSpawn(int slot,ref Vector3 point)
        {if(!Persistent||Persistent.Radius<30)return;int i=System.Math.Abs(slot)%10;float x=(i%2==0?1:-1)*((i+1)/2)*.9f;var local=new Vector3(x,1.95f,5.6f);point=Persistent.transform.TransformPoint(local);}   // a row along the steps, centered on the screen
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
