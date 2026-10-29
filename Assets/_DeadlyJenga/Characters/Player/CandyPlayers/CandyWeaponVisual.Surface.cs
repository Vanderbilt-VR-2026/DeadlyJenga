using System.Collections.Generic;
using UnityEngine;
namespace DeadlyJenga.CandyPlayers
{
    public sealed partial class CandyWeaponVisual
    {
        static readonly Dictionary<Material,Material> weaponSurfaces=new Dictionary<Material,Material>();
        static Texture2D finish,normal;
        static Material WeaponSurface(Material source)
        {
            if(!source)return null;if(weaponSurfaces.TryGetValue(source,out var surface)&&surface)return surface;
            if(!finish)
            {
                const int size=128;finish=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Fine brushed candy weapon finish",wrapMode=TextureWrapMode.Repeat};normal=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Machined weapon micro-normal",wrapMode=TextureWrapMode.Repeat};
                for(int y=0;y<size;y++)for(int x=0;x<size;x++){float grain=Mathf.PerlinNoise(x*.31f,y*2.1f);float level=.91f+grain*.09f;finish.SetPixel(x,y,new Color(level,level,level));normal.SetPixel(x,y,new Color(.5f+(grain-.5f)*.09f,.5f,1,1));}finish.Apply(true,true);normal.Apply(true,true);
            }
            surface=new Material(source){name=source.name+" - brushed weapon finish",enableInstancing=true};surface.SetTexture("_BaseMap",finish);surface.SetTextureScale("_BaseMap",Vector2.one*5);surface.SetTexture("_BumpMap",normal);surface.SetFloat("_BumpScale",.3f);surface.SetFloat("_Smoothness",.85f);surface.SetFloat("_Metallic",.65f);surface.EnableKeyword("_NORMALMAP");weaponSurfaces[source]=surface;return surface;
        }
    }
}
