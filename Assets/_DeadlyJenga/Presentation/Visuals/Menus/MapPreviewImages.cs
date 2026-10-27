using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal static class MapPreviewImages
    {
        static readonly Dictionary<string,Texture2D> images=new Dictionary<string,Texture2D>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>images.Clear();
        internal static Texture2D Get(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            if(!images.TryGetValue(id,out var image)){image=Resources.Load<Texture2D>("LobbyPreviews/"+id);images[id]=image;}
            return image;
        }
    }
}
