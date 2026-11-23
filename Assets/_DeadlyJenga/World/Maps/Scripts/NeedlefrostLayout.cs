using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed class NeedlefrostLayout
    {
        public const string Preset = "needlefrost-keep";
        // Courses describe the cut of the member, independent of its position in the stack.
        public enum Cut { Pier, Slab, Column, Lintel, Step, Vault, Roof, Blade, LeftBlade, RightBlade, Lantern, Window, Portal, Flight, Tracery, BridgeDeck }
        readonly List<CastlePlacement> parts = new List<CastlePlacement>(512);
        public static readonly Vector3[] Landings = {
            new Vector3(-16, 3, -15), new Vector3(16, 3, -15), new Vector3(-8, 6, 1),
            new Vector3(8, 6, 1), new Vector3(-16, 9, 17), new Vector3(16, 9, 17)
        };

        void Stone(float x, float bottom, float z, float w, float h, float d, Cut cut, float yaw = 0)
        {
            var section = cut == Cut.Lantern || cut == Cut.Window ? CastleSection.Gold :
                cut >= Cut.Roof && cut <= Cut.RightBlade ? CastleSection.Aqua : cut == Cut.Slab || cut == Cut.Step ? CastleSection.Keep : CastleSection.Wall;
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
                if (bay == 3 && (row == 0 || (row == 5 && side > 0))) continue;
                float x = side * bay * 8, z = -19 + row * 8, top = 3 + 3 * (row / 2);
                keep.Court(x,z,top);
                // Recessed walk-in grotto between the back of the lower court and the next tier.
                if(row==2 || row==4)keep.Grotto(x,z,top);
                if(row==0 && (bay==1 || (side<0 && bay==2)))
                    keep.Headland(x,z-4,top,bay==1?4.6f:2.8f);
                if(row==1 || row==3)
                {
                    if(bay==1 || (side<0 && bay==2))keep.Stair(x,top,z,8,3,3.6f);
                    if(side>0 && bay==3)keep.Switchback(top,z,row==1);
                }
                if ((bay == 3 || row == 0) && row % 2 == 0)
                    keep.Lamp(x + side * 3, top, z - 2.8f);
            }
            // An exposed, loose cornice at the front-left fault gives the giant hand a reachable grip.
            keep.Stone(-20.98f,-3,-19,1.27f,5.15f,.70f,Cut.Pier);
            keep.Stone(-21.28f,2.15f,-19,2.54f,.85f,.72f,Cut.Slab);
            keep.Crossing(0, 6, -3, 8.8f, 3);
            // The hall spans the channel, with both ends of its floor bearing on the inner courts.
            keep.Crossing(0, 9, 15, 8.8f, 7);
            keep.Gallery(0,9.15f,15,9,7,7);
            // Carved frontispiece rests on the channel vault and stops under the first balcony.
            keep.Stone(0,9.15f,11.85f,6.25f,6,.65f,Cut.Portal);
            keep.Stone(0,9.15f,18.0f,6.1f,6,.65f,Cut.Portal);
            keep.Gallery(0,16.15f,15,9,7,5);
            keep.Stone(0,21.15f,15,5,4,6,Cut.Roof);
            keep.Stone(0,25.15f,15,2.95f,11,2.7f,Cut.Blade);
            for (int side = -1; side <= 1; side += 2)
            {
                keep.Stone(side*3.65f,21.15f,15,2.35f,10,2.5f,side < 0 ? Cut.LeftBlade : Cut.RightBlade);
                float z = side < 0 ? 17 : 13;
                keep.Tower(side*16,z,9,side < 0 ? 6 : 7);
                keep.Stone(side*8.55f, (side < 0 ? 15 : 16)-1, z, 8.05f, 1, 2.6f,Cut.BridgeDeck);
                // Bridge seats stand on the upper court; the central hall is reached by its side gallery.
                for(int end=-1;end<=1;end+=2)
                    keep.Stone(side*8.55f + end*3.35f,9,z,1, (side < 0 ? 15 : 16)-10,2.5f,Cut.Column);
            }
            // The carved bridge seats stand slightly proud of the surrounding courts.
            // Short bevelled approaches connect the different floor elevations without a jump.
            for(int side=-1;side<=1;side+=2)
            {
                keep.Stone(side*4.91f,6,-3,2.5f,.15f,1.0f,Cut.Flight,-side*90);
                keep.Stone(side*4.91f,9,15,1.35f,.15f,1.0f,Cut.Flight,-side*90);
            }
            keep.Stone(-6.35f,15,17,2.45f,1.15f,3.7f,Cut.Flight,90);
            keep.Stone(6.35f,16,13,2.45f,.15f,3.7f,Cut.Flight,-90);
            // A lower camp under the right shelf, open to the ravine and sea.
            for(int tent=0;tent<2;tent++)
            {
                float x=11.8f+tent*4.4f;
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
            keep.Stone(0,16.15f,17.9f,6.2f,4,.58f,Cut.Column);
            for(int i=-2;i<=2;i++)
                keep.Stone(i*1.45f,16.15f,17.32f,.7f,3.25f,.48f,Cut.Window);
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

        void Headland(float x,float back,float top,float depth)
        {
            for(int i=-1;i<=1;i+=2)
            {
                float length=depth+(i<0?.8f:0),cx=x+i*1.45f,z=back-length/2;
                Stone(cx,-3,z,1.9f,top+1.95f,length*.62f,Cut.Pier);
                Stone(cx,top-1.05f,z,2.88f,1.05f,length-.02f,Cut.Slab);
            }
        }

        void Grotto(float x,float z,float top)
        {
            float floor=top-3,center=z-3.15f;
            Stone(x,-3,center,3.6f,floor+2.4f,1.3f,Cut.Pier);
            Stone(x,floor-.6f,center,7.9f,.6f,1.65f,Cut.Slab);
            // Open aisle below the upper slab. Slender jambs carry the pointed mouth.
            Stone(x,floor,z-3.65f,6.4f,1.7f,.36f,Cut.Tracery);
        }

        void Stair(float x,float bottom,float z,float length,float rise,float width)
        {
            Stone(x,bottom,z,width,rise,length,Cut.Flight);
        }

        void Switchback(float bottom,float z,bool eastbound)
        {
            // The outside route turns across the coastal shelf. Its landing meets the next court at the back.
            float direction=eastbound?1:-1;
            Stone(20,bottom,z+2.2f,3.55f,3,7,Cut.Flight,90*direction);
            Stone(20+direction*4.55f,bottom,z+2.2f,2.1f,3,3.58f,Cut.Slab);
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
                // Rear lancets and a pointed arcade give each open balcony a lit interior.
                Stone(x,y,z-2.83f,4.1f,story-1,.44f,Cut.Tracery);
                Stone(x,y,z+2.9f,4.25f,story-1,.55f,Cut.Column);
                for(int light=-1;light<=1;light++)
                    Stone(x+light*1.2f,y,z+2.35f,.55f,story*.66f,.42f,Cut.Window);
                Lamp(x+1.65f,y,z-1.9f);
                // External return flights leave the gallery edge and its entrances open.
                float outboard=Mathf.Sign(x);
                float flightX=x+outboard*(level==0 ? 4.4f : 6);
                Stone(flightX,y,z,1.95f,story,6.6f,Cut.Flight,level==0?0:180);
                Stone(flightX,y+story-.35f,z+(level==0?3.76f:-3.76f),3.15f,.35f,.7f,Cut.Lintel);
                // Both landing posts bear on the court, including the end of the upper return.
                Stone(flightX,bottom,z+(level==0?3.76f:-3.76f),1.4f,y+story-.35f-bottom,.68f,Cut.Column);
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
