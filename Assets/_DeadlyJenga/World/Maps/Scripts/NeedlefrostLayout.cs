using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    internal sealed partial class NeedlefrostLayout
    {
        public const string Preset = "needlefrost-keep";
        // Courses describe the cut of the member, independent of its position in the stack.
        public enum Cut { Pier, Slab, Column, Lintel, Step, Vault, Roof, Blade, LeftBlade, RightBlade, Lantern, Window, Portal, Flight, Tracery, BridgeDeck, IceWall }
        readonly List<CastlePlacement> parts = new List<CastlePlacement>(512);
        public static readonly Vector3[] Landings = {
            new Vector3(-14.6f,3,-18.2f),new Vector3(14.6f,3,-18.2f),new Vector3(-6.7f,6,-.7f),
            new Vector3(6.7f,6,-.7f),new Vector3(-10.4f,9,12.5f),new Vector3(10.4f,9,19.5f)
        };

        void Stone(float x, float bottom, float z, float w, float h, float d, Cut cut, float yaw = 0)
        {
            var section = cut == Cut.Lantern || cut == Cut.Window ? CastleSection.Gold :
                (cut >= Cut.Roof && cut <= Cut.RightBlade) || cut==Cut.IceWall ? CastleSection.Aqua : cut == Cut.Slab || cut == Cut.Step ? CastleSection.Keep : CastleSection.Wall;
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
                if (bay == 3 && side > 0 && (row == 0 || row == 5)) continue;
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
            keep.SeaArch(-8,-28.4f,4.3f);
            keep.SeaArch(8,-28.4f,6.15f);
            keep.SeaArch(-31,-10.7f,4.6f,false);
            // An exposed, loose cornice at the front-left fault gives the giant hand a reachable grip.
            keep.Stone(-28.98f,-3,-19,1.27f,5.15f,.70f,Cut.Pier);
            keep.Stone(-29.28f,2.15f,-19,2.54f,.85f,.72f,Cut.Slab);
            keep.Crossing(0, 6, -3, 8.8f, 3);
            // The hall spans the channel, with both ends of its floor bearing on the inner courts.
            keep.Crossing(0, 9, 15, 8.8f, 7);
            keep.Gallery(0,9.15f,15,9,7,7);
            // Carved frontispiece rests on the channel vault and stops under the first balcony.
            keep.Stone(0,9.15f,11.85f,6.25f,6,.65f,Cut.Portal);
            keep.Stone(0,9.15f,18.0f,6.1f,6,.65f,Cut.Portal);
            keep.Gallery(0,16.15f,15,9,7,5);
            keep.Stone(0,16.15f,11.85f,6.8f,4,.65f,Cut.Portal);
            keep.GrandKeep();
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side < 0 ? 17 : 13;
                if(side<0)
                {
                    // The western tower stands a second arch away, behind the wide coast stair.
                    keep.Court(-32,13,9);
                    keep.Court(-32,21,9);
                    keep.UpperCrossing(-16.60f,z,15);
                }
                keep.Tower(side<0?-24:16,z,9,side < 0 ? 6 : 7);
                keep.UpperCrossing(side*8.55f,z,side<0?15:16);
            }
            // The carved bridge seats stand slightly proud of the surrounding courts.
            // Short bevelled approaches connect the different floor elevations without a jump.
            for(int side=-1;side<=1;side+=2)
            {
                keep.Stone(side*4.91f,6,-3,2.5f,.15f,1.0f,Cut.Flight,-side*90);
                keep.Stone(side*4.91f,9,15,1.35f,.15f,1.0f,Cut.Flight,-side*90);
            }
            // The rails sit 1.17 m off axis. Leave clearance along both sides of each ramp.
            keep.Stone(-6.35f,15,17,1.88f,1.15f,3.7f,Cut.Flight,90);
            keep.Stone(6.35f,16,13,1.88f,.15f,3.7f,Cut.Flight,-90);
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
            // Luminous ice inside the gate provides depth behind the open front arch.
            keep.Stone(0,9.15f,17.25f,4.7f,3.7f,.55f,Cut.IceWall);
            for(int side=-1;side<=1;side+=2)
            {
                keep.Stone(side*1.86f,9.15f,16.55f,.72f,3.25f,.7f,Cut.Blade);
                keep.Stone(side*2.52f,16.15f,15.95f,.6f,2.5f,.62f,Cut.Blade);
            }
            for(int i=-2;i<=2;i++)
                keep.Stone(i*1.45f,16.15f,17.32f,.7f,3.25f,.48f,Cut.Window);
            keep.OuterTerraces();
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

        void SeaArch(float x,float back,float length,bool stair=true)
        {
            float z=back-length*.5f,top=stair?2.52f:3;
            for(int side=-1;side<=1;side+=2)
                Stone(x+side*2.50f,-3,z,1.12f,2.1f,length*.77f,Cut.Pier);
            Stone(x,-.9f,z,6,top+.24f,length,Cut.Portal);
            for(int side=-1;side<=1;side+=2)
                Stone(x+side*1.5f,top-.66f,z,2.98f,.66f,length-.02f,Cut.Slab);
            if(stair)Stone(x-1.45f,top,back-1.1f,2.35f,3-top,2.2f,Cut.Flight);
            Lamp(x+2.2f,top,back-length+.77f);
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

        void UpperCrossing(float x,float z,float deckY)
        {
            Stone(x,9,z,8.05f,deckY-10,2.48f,Cut.Portal);
            Stone(x,deckY-1,z,8.05f,1,2.6f,Cut.BridgeDeck);
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
                float outboard=Mathf.Sign(x);
                float rearWindowX=x-outboard*.55f;
                Stone(rearWindowX,y,z+2.9f,3.1f,story-1,.55f,Cut.Column);
                for(int light=-1;light<=1;light++)
                    Stone(rearWindowX+light*.95f,y,z+2.35f,.55f,story*.66f,.42f,Cut.Window);
                Lamp(x+1.65f,y,z-1.9f);
                // External return flights leave the gallery edge and its entrances open.
                float flightX=x+outboard*(level==0 ? 4.4f : 6);
                float length=level==0?10.4f:6.6f;
                float flightZ=level==0?z-1.8f:z;
                // The east stair toe has a side approach from the nine-metre court.
                if(level==0 && x>0)Stone(flightX,6,7.25f,4.8f,3,3.48f,Cut.Slab);
                Stone(flightX,y,flightZ,1.95f,story,length,Cut.Flight,level==0?0:180);
                float landingZ=z+(level==0?3.76f:-3.76f);
                Stone(flightX-(level==0?1.1f*outboard:0),y+story-.35f,landingZ,level==0?5.35f:3.15f,.35f,.72f,Cut.Lintel);
                // Both landing posts bear on the court, including the end of the upper return.
                Stone(flightX,bottom,landingZ,1.4f,y+story-.35f-bottom,.68f,Cut.Column);
                if(level==0)Stone(flightX-2.7f*outboard,bottom,landingZ,.68f,story-.35f,.68f,Cut.Column);
                if(level==0)
                {
                    // Rear wall of the outboard stair carries the upper return flight.
                    Stone(flightX+outboard*1.6f,y,z,1.2f,story-.55f,6.6f,Cut.Column);
                    Stone(flightX+outboard*1.6f,y+story-.55f,z,1.2f,.55f,6.6f,Cut.Lintel);
                }
            }
            UpperPavilion(x,z,bottom+story*2,x<0?6.25f:6.9f);
        }

        void Lamp(float x,float y,float z)
        {
            Stone(x,y,z,.5f,.72f,.5f,Cut.Column);
            Stone(x,y+.72f,z,.42f,.75f,.42f,Cut.Lantern);
        }
    }
}
