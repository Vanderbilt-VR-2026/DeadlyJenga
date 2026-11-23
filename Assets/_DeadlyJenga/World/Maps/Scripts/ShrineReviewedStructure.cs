using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // Body order is shared with the reviewed Blender export.
    internal static class ShrineReviewedStructure
    {
        public const int BodyCount = 47;
        const int FirstPost = 26;
        const int FirstBeam = 44;
        const float Floor = 4.138f;
        static readonly float[] Columns = { -16, -9.5f, -3.1f, 3.1f, 9.5f, 16 };
        static readonly float[] Rows = { 13, 20, 27 };

        public static void AddHall(Action<float,float,float,float,float,float,CastleSection,float> box)
        {
            box(0, 0, 20, 41, 4, 22, CastleSection.Keep, 0);
            for (int i = 0; i < 22; i++)
                box(0, 0, 1.08f+i*.36f, 7.84f, (i+1)*4f/22, .359f, CastleSection.Keep, 0);
            box(0, Floor, 25.05f, 10, 4.82f-Floor, 3.8f, CastleSection.Keep, 0);
            box(0, 12.05f, 20, 36, .61f, 19, CastleSection.Corner, 0);
            box(0, 12.66f, 20, 24, 6.27f, 10, CastleSection.Corner, 0);
            foreach (float row in Rows)
                foreach (float column in Columns)
                    box(column, Floor, row, .70f, 11.41f-Floor, .70f, CastleSection.Wall, 0);
            foreach (float row in Rows)
                box(0, 11.41f, row, 34.5f, .64f, .68f, CastleSection.Wall, 0);
        }

        public static void Configure(int index, Rigidbody body, CastleBlock block, Vector3 position)
        {
            body.name = "Reviewed shrine " + index;
            var origin = new GameObject("Reviewed shrine art origin").transform;
            origin.SetParent(body.transform,false);
            var scale = body.transform.localScale;
            origin.localScale = new Vector3(1/scale.x,1/scale.y,1/scale.z);
            origin.localPosition = Vector3.Scale(-position,origin.localScale);
            if (!block || index >= FirstPost) return;
            var frame = block.CreateTraversalFrame();
            if (!frame) return;

            void Shape(string name, Vector3 at, Vector3 size, Quaternion rotation)
                => ShrineTownLayout.ColliderBox(frame,name,at-position,size,rotation);
            void Flat(string name, float x, float y, float z, float width, float height, float depth)
                => Shape(name,new Vector3(x,y,z),new Vector3(width,height,depth),Quaternion.identity);

            if (index == 0)
            {
                Flat("Forecourt pavers", 0, .258f, -.11f, 6.98f, .09f, 2.13f);
                Flat("Basalt stair landing", 0, 3.96f, 8.96f, 6.89f, .12f, .30f);
                Flat("Lower porch paving", 0, 4.008f, 11.04f, 41, .035f, 4.08f);
                Shape("Cedar threshold ramp", new Vector3(0,4.047f,12.44f),
                    new Vector3(5.37f,.11f,.67f),Quaternion.Euler(-9,0,0));
                foreach (float x in new[] { -12.75f, -6.35f, 6.35f, 12.75f })
                    Flat("Shoji panel", x, 6.9f, 14.25f, 5.48f, 3.05f, .17f);
                foreach (float x in new[] { -16f, 16f })
                    foreach (float z in new[] { 16.5f, 23.5f })
                        Flat("Side plaster panel",x,7.63f,z,.44f,7,6.2f);
                Flat("Sanctuary backing", 0, 7.9f, 27.3f, 30, 7.65f, .41f);
                // The floor must carry posts and the loose dais as well as survivors.
                var at = new Vector3(0,(4+Floor)*.5f,20);
                var size = new Vector3(34,Floor-4,15);
                Shape("Hinoki hall floor",at,size,Quaternion.identity);
                var physical = new GameObject("Hall floor bearing surface");
                physical.layer = body.gameObject.layer;
                physical.transform.SetParent(body.transform,false);
                var blockSize = body.transform.localScale;
                var inverse = new Vector3(1/blockSize.x,1/blockSize.y,1/blockSize.z);
                physical.transform.localPosition = Vector3.Scale(at-position,inverse);
                physical.AddComponent<BoxCollider>().size = Vector3.Scale(size,inverse);
            }
            else if (index <= 22)
            {
                // The half-scale survivor cannot step up the modeled risers.
                // Give each loose tread its own continuous slope at the nosing line.
                float rise = index < 22 ? 4f/22 : 0;
                float going = .36f;
                Shape("Stair walking slope", new Vector3(0,index*4f/22+rise*.5f+.015f,position.z),
                    new Vector3(6.8f,.035f,Mathf.Sqrt(going*going+rise*rise)+.006f),
                    Quaternion.Euler(-Mathf.Atan2(rise,going)*Mathf.Rad2Deg,0,0));
            }
            else if (index == 23)
            {
                for (int i = 0; i < 3; i++)
                {
                    float top = 4.13f+.1725f*(i+1);
                    Flat("Altar dais tread",0,(Floor+top)*.5f,22.24f+i*.36f,3.6f,top-Floor,.377f);
                }
            }
            else if (index == 24)
                AddSlopes(Shape,40,21,12.66f,6);
            else
            {
                Flat("Upper gallery floor",0,15.1f,20,26,.49f,12.5f);
                Flat("Upper veranda rail",0,15.82f,13.36f,25.6f,.56f,.18f);
                AddSlopes(Shape,32.6f,16.6f,18.93f,0);
            }
            block.RegisterTraversalShapes(frame);
        }

        static void AddSlopes(Action<string,Vector3,Vector3,Quaternion> shape,
            float width,float depth,float eave,float inner)
        {
            float half = depth*.5f;
            float Height(float z) => eave+depth*.31f*Mathf.Pow(1-z/half,1.42f)+.27f*Mathf.Pow(z/half,7);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    float a = Mathf.Lerp(inner,half,i/4f), b = Mathf.Lerp(inner,half,(i+1)/4f);
                    float y = Height(a), top = Height(b);
                    float run = b-a, rise = top-y;
                    shape("Ceramic roof traversal",new Vector3(0,(y+top)*.5f,20+side*(a+b)*.5f),
                        new Vector3(width,.15f,Mathf.Sqrt(run*run+rise*rise)+.04f),
                        Quaternion.Euler(-side*Mathf.Atan2(rise,run)*Mathf.Rad2Deg,0,0));
                }
        }

        public static void JoinFrames(List<Rigidbody> bodies)
        {
            for (int row = 0; row < Rows.Length; row++)
            {
                var beam = bodies[FirstBeam+row];
                ShrineTownLayout.Join(beam,bodies[FirstPost+row*Columns.Length]);
                ShrineTownLayout.Join(beam,bodies[FirstPost+(row+1)*Columns.Length-1]);
            }
        }
    }
}
