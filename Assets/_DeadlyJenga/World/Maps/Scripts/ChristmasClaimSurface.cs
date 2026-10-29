using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class ChristmasClaimSurface : MonoBehaviour, IBlockOwnership
    {
        public Renderer visual;
        public int OwnerTeam { get; private set; } = -1;
        MaterialPropertyBlock properties;
        public void SetOwner(int team,Color color)
        {
            OwnerTeam=team;
            if(!visual)return;
            properties ??= new MaterialPropertyBlock();
            visual.GetPropertyBlock(properties);properties.SetColor("_BaseColor",team<0?new Color(.66f,.86f,.89f):color);visual.SetPropertyBlock(properties);
        }
    }
}
