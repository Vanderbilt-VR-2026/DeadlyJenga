using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CandyArt
{
    public sealed partial class CandyLobbyResort
    {
        void Milkshake()
        {
            if(!milkshakeGeometry)return;
            var geometry=milkshakeGeometry.Find("Shake collision geometry");if(!geometry)return;
            var wall=geometry.Find("Glass wall 0").GetComponent<BoxCollider>();
            Vector3 c=visual.InverseTransformPoint(milkshakeGeometry.position);float h=wall.size.y,r=wall.transform.localPosition.x;
            float milk=geometry.Find("Chocolate").localPosition.y+.06f;
            foreach(var renderer in geometry.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            Ring(c+Vector3.up*.14f,r,.17f,0);
            Ring(c+Vector3.up*.32f,r,.07f,0);
            GlassSide(c,r+.15f,.3f,h-.06f,false);GlassSide(c,r-.15f,.3f,h-.06f,true);
            Ring(c+Vector3.up*h,r,.15f,0);
            Ring(c+Vector3.up*(h-.07f),r+.075f,.055f,10);
            // The visible fill line is halfway down the genuinely hollow glass.
            GlassSide(c,r-.16f,.28f,milk,false,9);
            Ellipsoid(c+Vector3.up*(milk-.015f),new Vector3(r-.15f,.045f,r-.15f),9);
            Ring(c+Vector3.up*(milk+.005f),r-.19f,.022f,8);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;Vector3 p=c+new Vector3(Mathf.Cos(a)*(r+.15f),0,Mathf.Sin(a)*(r+.15f));
                Tube(new[]{p+Vector3.up*.4f,p+Vector3.up*(h-.25f)},.016f,10,6);
            }
            Vector3 logo=c+new Vector3(-r-.17f,1.5f,0);
            Rounded(logo,new Vector3(.04f,.83f,1.05f),0);
            Sign("CHOC\nSHAKE",logo+Vector3.left*.025f,new Vector2(.94f,.7f),57,1,Quaternion.Euler(0,90,0));
            var a0=c+new Vector3(.55f,milk,.2f);var a1=c+new Vector3(.72f,h+.45f,.2f);var a2=c+new Vector3(1.3f,h+.9f,.2f);
            Tube(new[]{a0,a1,a2},.13f,0,16);
            Stripe(a0,a1,0);Stripe(a1,a2,Vector3.Distance(a0,a1)/.37f*Mathf.PI*2);
            Vector3 end=(a2-a1).normalized;
            Ring(a2,.11f,.025f,0,Quaternion.FromToRotation(Vector3.up,end));
            Ellipsoid(a2-end*.012f,new Vector3(.085f,.06f,.085f),9);
            var cherry=c+new Vector3(-.52f,milk+.2f,.1f);
            Ellipsoid(cherry,new Vector3(.3f,.3f,.3f),1);
            Tube(new[]{cherry+Vector3.up*.25f,cherry+new Vector3(.07f,.5f,0),cherry+new Vector3(.18f,.56f,0)},.025f,7,8);
            // Retain the prop collider transforms as the source of art placement.
            var cooler=geometry.Find("Cooler");Vector3 cp=visual.InverseTransformPoint(cooler.position);var q=cooler.rotation;
            Rounded(cp,new Vector3(1.1f,.98f,.85f),4,q);
            Rounded(cp+Vector3.up*.49f,new Vector3(1.14f,.1f,.89f),0,q);
            foreach(float side in new[]{-1f,1f})Rounded(cp+q*new Vector3(side*.57f,.2f,0),new Vector3(.1f,.18f,.28f),0,q);
            Rounded(cp+q*new Vector3(0,.25f,-.44f),new Vector3(.15f,.25f,.04f),1,q);
            var crates=geometry.Find("Delivery crates");Vector3 bp=visual.InverseTransformPoint(crates.position);q=crates.rotation;
            for(int row=0;row<3;row++)
            {
                Vector3 p=bp+Vector3.up*((row-1)*.8533f);Rounded(p,new Vector3(.85f,.846f,.85f),8,q);
                foreach(float side in new[]{-1f,1f})
                {Rounded(p+q*new Vector3(side*.29f,0,-.43f),new Vector3(.07f,.8f,.025f),0,q);Rounded(p+q*new Vector3(side*.29f,0,.43f),new Vector3(.07f,.8f,.025f),0,q);}
                Rounded(p+q*new Vector3(0,0,-.445f),new Vector3(.27f,.2f,.018f),0,q);
            }
            var canopy=geometry.Find("Parasol canopy");Vector3 upos=visual.InverseTransformPoint(canopy.position);
            Umbrella(upos-Vector3.up*(1.85f*.94f-.06f),.94f,1,.12f/.94f);
        }
        void GlassSide(Vector3 center,float radius,float bottom,float top,bool inward,int color=10)
        {
            var b=BatchFor(color);int start=b.vertices.Count;
            for(int y=0;y<2;y++)for(int i=0;i<=64;i++)
            {float a=i*Mathf.PI/32;var n=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));b.vertices.Add(center+n*radius+Vector3.up*(y==0?bottom:top));b.normals.Add(inward?-n:n);}
            for(int i=0;i<64;i++){int n=start+i;if(inward)Quad(b,n,n+1,n+65,n+66);else Quad(b,n,n+65,n+1,n+66);}
        }
        void Stripe(Vector3 from,Vector3 to,float phase)
        {
            var dir=(to-from).normalized;var u=Vector3.Cross(dir,Vector3.forward).normalized;var v=Vector3.Cross(dir,u);
            float length=Vector3.Distance(from,to);int count=Mathf.CeilToInt(length/.015f);var b=BatchFor(1);int start=b.vertices.Count;
            for(int i=0;i<=count;i++)
            {float t=i/(float)count,a=phase+t*length/.37f*Mathf.PI*2;var normal=u*Mathf.Cos(a)+v*Mathf.Sin(a);var p=Vector3.Lerp(from,to,t)+normal*.132f;
                b.vertices.Add(p-dir*.05f);b.vertices.Add(p+dir*.05f);b.normals.Add(normal);b.normals.Add(normal);}
            for(int i=0;i<count;i++){int n=start+i*2;Quad(b,n,n+1,n+2,n+3,true);}
        }
    }
}
