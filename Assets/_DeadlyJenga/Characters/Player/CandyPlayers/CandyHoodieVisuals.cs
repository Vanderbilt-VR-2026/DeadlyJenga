using UnityEngine;
using Builder = DeadlyJenga.CandyPlayers.CandyAccessoryMeshes.Builder;

namespace DeadlyJenga.CandyPlayers
{
    internal static class CandyHoodieVisuals
    {
        static Vector3 P(float x, float y, float z) => new(x,y,z);

        internal static void FoldedHood(Builder b)
        {
            b.Pillow(P(-.158f,1.277f,-.146f),P(.158f,1.277f,-.146f),
                P(-.080f,1.058f,-.227f),P(.080f,1.058f,-.227f),.115f,-.049f,.009f,10,12);
            for (int side=-1;side<=1;side+=2)
            {
                float s=side;
                b.Tube(t=>P(s*(.106f+.056f*Mathf.Sin(t*Mathf.PI)),1.292f-.044f*Mathf.Sin(t*Mathf.PI),
                    Mathf.Lerp(.095f,-.154f,t)),t=>.024f+.014f*Mathf.Sin(t*Mathf.PI));
            }
        }

        internal static void Lining(Builder b)
        {
            b.Tube(t=>P(Mathf.Lerp(-.133f,.133f,t),1.266f+.024f*Mathf.Pow(t*2f-1f,2f),
                -.208f-.035f*Mathf.Sin(t*Mathf.PI)),t=>.018f,24,8);
            for (int side=-1;side<=1;side+=2)
            {
                float s=side;
                b.Tube(t=>P(s*Mathf.Lerp(.104f,.145f,t),Mathf.Lerp(.998f,.842f,t),
                    Mathf.Lerp(.199f,.174f,t)),t=>.005f,12,8);
                b.Ring(P(s*.069f,1.242f,.178f),.014f,.004f,Quaternion.identity);
            }
        }

        internal static void Pocket(Builder b)
        {
            b.Pillow(P(-.100f,1.013f,.175f),P(.100f,1.013f,.175f),P(-.149f,.816f,.141f),
                P(.149f,.816f,.141f),.061f,.017f,0f,10,12);
        }

        internal static void Drawstrings(Builder b)
        {
            for (int side=-1;side<=1;side+=2)
            {
                float s=side;
                float length=side>0?.127f:.100f;
                Vector3 End(float t)=>P(s*(.069f+.019f*Mathf.Sin(t*3.4f)),
                    1.243f-length*t,.183f+.026f*Mathf.Sin(t*Mathf.PI));
                b.Tube(End,t=>.007f,16,8);
                b.Cone(End(.91f),End(1f)+Vector3.down*.012f,.009f,.008f,10);
            }
        }
    }
}
