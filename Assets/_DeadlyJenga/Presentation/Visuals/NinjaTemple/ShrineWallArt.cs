using UnityEngine;
using P = DeadlyJenga.NinjaArt.NinjaPalette;

namespace DeadlyJenga.NinjaArt
{
    static class ShrineWallArt
    {
        static readonly Color Cedar = new Color(.46f, .25f, .14f);
        static readonly Color Indigo = new Color(.18f, .27f, .39f);
        static readonly Color Linen = new Color(.89f, .79f, .59f);

        public static Mesh Build(Vector3 size, int thickness)
        {
            var mesh = new NinjaMesh();
            float width = size.x, height = size.y;
            bool sanctuary = thickness == 48;
            bool worshipHall = thickness == 46;
            bool shop = thickness == 41;
            for (int face = 0; face < 2; face++)
            {
                var rotation = Quaternion.Euler(0, face * 180, 0);
                mesh.Frame = Matrix4x4.TRS(rotation * Vector3.back * (size.z / 2), rotation, Vector3.one);
                // Cedar jambs stand proud of the paper and close the ends of each bay.
                Bar(mesh, 0, 0, .014f, width, height, .028f, P.Wood);
                for (int side = -1; side <= 1; side += 2)
                    Bar(mesh, side * (width / 2 - .075f), 0, .08f, .15f, height, .16f, Cedar);

                float sill = -height * .21f, head = height * .34f;
                float opening = width - .36f;
                if (sanctuary)
                {
                    float hanging = Mathf.Min(opening * .70f, height * .68f);
                    Curtain(mesh, hanging, height * .65f, height * .06f, P.DeepRed, 1);
                }
                else
                {
                    Window(mesh, opening, sill, head);
                    if (worshipHall && face == 0)
                        Curtain(mesh, opening * .36f, height * .55f, height * .07f, P.DeepRed, 1);
                    if (shop && face == 0)
                        Curtain(mesh, opening * .91f, height * .23f, head - height * .06f, Indigo, 3);
                }
                Bar(mesh, 0, sill - .06f, .10f, width, .12f, .20f, Cedar);
                Bar(mesh, 0, head + .08f, .085f, width, .16f, .17f, Cedar);
                Bar(mesh, 0, -height * .44f, .055f, width, .15f, .11f, Cedar);
            }
            return mesh.Build("Shrine joinery " + thickness);
        }

        static void Window(NinjaMesh mesh, float width, float sill, float head)
        {
            float half = width / 2;
            var honey = P.Paper(new Color(.92f,.63f,.34f), .29f);
            var cream = P.Paper(new Color(.99f,.83f,.60f), .43f);
            mesh.Quad(new Vector3(-half,sill,-.035f), new Vector3(-half,head,-.035f),
                new Vector3(half,head,-.035f), new Vector3(half,sill,-.035f), honey, cream, cream, honey);
            int panes = Mathf.Clamp(Mathf.RoundToInt(width / .65f), 3, 22);
            for (int i = 0; i <= panes; i++)
            {
                float x = width * (i / (float)panes - .5f);
                bool stile = i == 0 || i == panes || i == panes / 2;
                Bar(mesh,x,(sill + head) / 2,.074f,stile ? .10f : .047f,head - sill,.072f,P.Wood);
            }
            int rows = Mathf.Max(2, Mathf.RoundToInt((head - sill) / .60f));
            for (int i = 1; i < rows; i++)
                Bar(mesh,0,Mathf.Lerp(sill, head, i / (float)rows),.060f,width,.046f,.042f,Cedar);
        }

        static void Curtain(NinjaMesh mesh, float width, float height, float y, Color dye, int leaves)
        {
            float top = y + height / 2;
            Bar(mesh,0,top,.195f,width + .22f,.07f,.08f,P.Wood);
            for (int i = 0; i < leaves; i++)
            {
                float span = width / leaves, center = span * (i + .5f) - width / 2;
                // The shallow pleat and split hem silhouette are visible along the street.
                float left = center - span * .48f, right = center + span * .48f;
                float hem = top - height;
                var fold = new Vector3(center, hem - .04f, -.22f);
                var seam = new Vector3(center, top, -.22f);
                mesh.Quad(new Vector3(left,hem,-.18f),new Vector3(left,top,-.18f),seam,fold,dye);
                mesh.Quad(fold,seam,new Vector3(right,top,-.18f),new Vector3(right,hem,-.18f),dye);
                // A flat dyed hemp-leaf rosette printed onto the two folded cloth planes.
                float r = Mathf.Min(span * .28f, height * .30f);
                Vector3 Print(float x, float dy) => new Vector3(center+x,y+dy,-.222f + .04f * Mathf.Abs(x)/(span*.48f));
                for (int petal = 0; petal < 6; petal++)
                {
                    float a = petal * Mathf.PI / 3;
                    Vector2 axis = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                    Vector2 wing = new Vector2(axis.y,-axis.x) * (r*.17f);
                    Vector2 mid = axis * (r*.49f);
                    var tip = Print(axis.x*r,axis.y*r);
                    mesh.Quad(Print(axis.x*r*.12f,axis.y*r*.12f),Print(mid.x-wing.x,mid.y-wing.y),
                        tip,Print(mid.x+wing.x,mid.y+wing.y),Linen);
                }
            }
        }

        static void Bar(NinjaMesh mesh, float x, float y, float depth, float w, float h, float d, Color color)
        {
            mesh.Box(new Vector3(x,y,-depth),new Vector3(w,h,d),Quaternion.identity,color);
        }
    }
}
