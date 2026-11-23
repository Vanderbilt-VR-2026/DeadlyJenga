using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed class NeedlefrostLayout
    {
        public const string Preset = "needlefrost-keep";
        // Courses describe the cut of the member, independent of its position in the stack.
        public enum Cut { Pier, Slab, Column, Lintel, Step, Vault, Roof, Blade, LeftBlade, RightBlade, Lantern, Window, Portal }
        readonly List<CastlePlacement> parts = new List<CastlePlacement>(512);
        public static readonly Vector3[] Landings = {
            new Vector3(-16, 3, -15), new Vector3(16, 3, -15), new Vector3(-8, 6, 1),
            new Vector3(8, 6, 1), new Vector3(-16, 9, 17), new Vector3(16, 9, 17)
        };

        void Stone(float x, float bottom, float z, float w, float h, float d, Cut cut, float yaw = 0)
        {
            var section = cut == Cut.Lantern || cut == Cut.Window ? CastleSection.Gold :
                cut >= Cut.Roof && cut != Cut.Portal ? CastleSection.Aqua : cut == Cut.Slab || cut == Cut.Step ? CastleSection.Keep : CastleSection.Wall;
            parts.Add(new CastlePlacement(new Vector3(x, bottom + h / 2, z), Quaternion.Euler(0,yaw,0),
                section, (int)cut, new Vector3(w,h,d)));
        }

        public static List<CastlePlacement> Build()
        {
            var keep = new NeedlefrostLayout();
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 6; row++)
            for (int bay = 1; bay <= 3; bay++)
            {
                if (bay == 3 && (row == 0 || row == 5)) continue;
                float x = side * bay * 8, z = -19 + row * 8, top = 3 + 3 * (row / 2);
                keep.Court(x, z, top);
                if ((row == 1 || row == 3) && (bay == 1 || bay == (side < 0 ? 2 : 3)))
                    keep.Stair(x, top, z, 8, 3, 3.6f);
                if ((bay == 3 || row == 0) && row % 2 == 0)
                    keep.Lamp(x + side * 3, top, z - 2.8f);
            }
            keep.Crossing(0, 6, -3, 8.8f, 3);
            // The hall spans the channel, with both ends of its floor bearing on the inner courts.
            keep.Crossing(0, 9, 15, 8.8f, 7);
            keep.Gallery(0,9.15f,15,9,7,7);
            // Carved frontispiece rests on the channel vault and stops under the first balcony.
            keep.Stone(0,9.15f,11.85f,6.25f,6,.65f,Cut.Portal);
            keep.Gallery(0,16.15f,15,9,7,5);
            keep.Stone(0,21.15f,15,5,4,6,Cut.Roof);
            keep.Stone(0,25.15f,15,2.95f,11,2.7f,Cut.Blade);
            for (int side = -1; side <= 1; side += 2)
            {
                keep.Stone(side*3.65f,21.15f,15,2.35f,10,2.5f,side < 0 ? Cut.LeftBlade : Cut.RightBlade);
                float z = side < 0 ? 17 : 13;
                keep.Tower(side*16,z,9,side < 0 ? 6 : 7);
                keep.Stone(side*8.55f, (side < 0 ? 15 : 16)-1, z, 8.05f, 1, 2.6f,Cut.Lintel);
                // Bridge seats stand on the upper court; the central hall is reached by its side gallery.
                for(int end=-1;end<=1;end+=2)
                    keep.Stone(side*8.55f + end*3.35f,9,z,1, (side < 0 ? 15 : 16)-10,2.5f,Cut.Column);
            }
            // A lower camp under the right shelf, open to the ravine and sea.
            for(int tent=0;tent<2;tent++)
            {
                float x=11+tent*5;
                for(int a=-1;a<=1;a+=2)
                for(int b=-1;b<=1;b+=2)
                    keep.Stone(x+a*1.15f,3,-13+b*.8f,.28f,1.9f,.28f,Cut.Column);
                keep.Stone(x,4.9f,-13,2.9f,.95f,2.2f,Cut.Roof);
            }
            // Faceted jambs, warm sconces and paired lancets frame the tall entry.
            for(int side=-1;side<=1;side+=2)
            {
                keep.Lamp(side*2.8f,9.15f,13.25f);
                keep.Stone(side*2.7f,9.15f,12.65f,.65f,4.7f,.7f,Cut.Column);
                keep.Stone(side*2.7f,13.85f,12.65f,.77f,1.25f,.85f,Cut.Blade);
            }
            for(int i=-2;i<=2;i++)
                keep.Stone(i*1.45f,16.15f,11.85f,.52f,2.25f,.45f,Cut.Window);
            return keep.parts;
        }

        void Court(float x,float z,float top)
        {
            for(int half=-1;half<=1;half+=2)
            {
                float cx=x+half*2;
                Stone(cx,-3,z,2.65f,top+1.7f,4.6f,Cut.Pier);
                Stone(cx,top-1.3f,z,3.98f,1.3f,7.98f,Cut.Slab);
            }
        }

        void Stair(float x,float bottom,float z,float length,float rise,float width)
        {
            int steps=Mathf.CeilToInt(rise/.22f);
            for(int i=0;i<steps;i++)
                Stone(x,bottom,z-length/2+(i+.5f)*length/steps,width,rise*(i+1)/steps,length/steps-.008f,Cut.Step);
        }

        void Crossing(float x,float top,float z,float width,float depth)
        {
            // One carved ice arch springs off the two banks. Its soffit is open in the middle.
            Stone(x,top-1.65f,z,width,1.8f,depth,Cut.Vault);
        }

        void Gallery(float x,float baseY,float z,float w,float d,float height)
        {
            for(int a=-1;a<=1;a+=2)
            for(int b=-1;b<=1;b+=2)
                Stone(x+a*(w/2-.65f),baseY,z+b*(d/2-.65f),1.15f,height-1,1.15f,Cut.Column);
            Stone(x,baseY+height-1,z,w,1,d,Cut.Lintel);
        }

        void Tower(float x,float z,float bottom,float story)
        {
            for(int level=0;level<2;level++)
            {
                float y=bottom+story*level;
                Gallery(x,y,z,6.8f,6.8f,story);
                Lamp(x+1.9f,y,z-2.5f);
                // External return flights leave the gallery edge and its entrances open.
                float outboard=Mathf.Sign(x);
                float flightX=x+outboard*(level==0 ? 4.4f : 6);
                for(int i=0;i<26;i++)
                {
                    float treadZ=z-3.3f+(i+.5f)*6.6f/26;
                    if(level%2==1)treadZ=2*z-treadZ;
                    Stone(flightX,y,treadZ,1.95f,story*(i+1)/26,6.6f/26-.005f,Cut.Step);
                }
                Stone(flightX,y+story-.35f,z+(level==0?3.65f:-3.65f),3.15f,.35f,.7f,Cut.Lintel);
                Stone(flightX,y,z+(level==0?3.65f:-3.65f),1.4f,story-.35f,.68f,Cut.Column);
                if(level==0)
                {
                    // Rear wall of the outboard stair carries the upper return flight.
                    Stone(flightX+outboard*1.6f,y,z,1.2f,story-.55f,6.6f,Cut.Column);
                    Stone(flightX+outboard*1.6f,y+story-.55f,z,1.2f,.55f,6.6f,Cut.Lintel);
                }
            }
            float roof=bottom+story*2;
            // The four small pinnacles have separate bearing pads beyond the main roof.
            Stone(x,roof,z,5.15f,7.4f,5.15f,Cut.Roof);
            Stone(x,roof+7.4f,z,1.25f,3.7f,1.25f,Cut.Blade);
            for(int a=-1;a<=1;a+=2)
            for(int b=-1;b<=1;b+=2)
                Stone(x+a*2.85f,roof,z+b*2.85f,.7f,3.4f,.7f,Cut.Blade);
        }

        void Lamp(float x,float y,float z)
        {
            Stone(x,y,z,.5f,.72f,.5f,Cut.Column);
            Stone(x,y+.72f,z,.42f,.75f,.42f,Cut.Lantern);
        }
    }
}
