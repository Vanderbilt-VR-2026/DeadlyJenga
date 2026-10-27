using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed class EpicCitySite
    {
        public string name,kind;
        public Vector3 position;
        public float heading,width,depth;
        public int tiers;
        public readonly List<Vector3> ports=new();
        public readonly List<MayhemModule> members=new();
        public Bounds bounds;
    }
    public sealed class EpicCityRoute
    {
        public string name;
        public int from,to;
        public Vector3 a,b;
    }
    public sealed class EpicCityPlan
    {
        public readonly List<MayhemModule> modules=new();
        public readonly List<EpicCitySite> sites=new();
        public readonly List<EpicCityRoute> routes=new();
        public readonly List<Vector3> spawnPoints=new();
        public Vector2 halfSize;
        public readonly List<Vector3> bearingPads=new();
    }
    // Coordinates, stairs and bearing seats are physical geometry. Road markings are separate art.
    public static partial class EpicCityLayout
    {
        public static readonly string[] Names={"Tideway Metropolis","Double-Loop Exchange","Caldera Switchback City","Broken Crown Capital","Harbor Loom","Lantern Ravine","Canopy Borough","Bookfall Boulevard","Three Hearts City","The Rising Spine"};
        public static readonly string[] Ids={"epic-tideway","epic-double-loop","epic-caldera","epic-broken-crown","epic-harbor-loom","epic-lantern-ravine","epic-canopy-borough","epic-bookfall","epic-three-hearts","epic-rising-spine"};
        public static EpicCityPlan Build(int design)
        {
            if(design<0||design>=Names.Length)throw new ArgumentOutOfRangeException(nameof(design));
            var b=new Builder();Compose(b,design);b.WeaveStreets(design);b.Finish();return b.plan;
        }
        public static float GroundHeight(EpicCityPlan plan,Vector3 p)
        {
            float closest=float.MaxValue,height=.35f;
            foreach(var site in plan.sites)
            {
                var local=Quaternion.Inverse(Quaternion.Euler(0,site.heading,0))*(p-site.position);
                float dx=Mathf.Max(0,Mathf.Abs(local.x)-site.width*.5f-9),dz=Mathf.Max(0,Mathf.Abs(local.z)-site.depth*.5f-9);
                float distance=Mathf.Sqrt(dx*dx+dz*dz);
                if(distance<closest){closest=distance;height=site.position.y;}
            }
            float result=.35f+(height-.35f)*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(closest/22)));
            foreach(var pad in plan.bearingPads){float distance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(pad.x,pad.z));if(distance<12){result=pad.y;break;}if(distance<24)result=Mathf.Lerp(pad.y,result,Mathf.SmoothStep(0,1,(distance-12)/12));}
            foreach(var site in plan.sites)
            {
                if(site.kind!="Street fabric")continue;
                var local=Quaternion.Inverse(Quaternion.Euler(0,site.heading,0))*(p-site.position);
                float edge=Mathf.Max(Mathf.Abs(local.x)-21,Mathf.Abs(local.z)-26);
                if(edge<=0)return site.position.y;
                if(edge<1.5f)result=Mathf.Lerp(site.position.y,result,Mathf.SmoothStep(0,1,edge/1.5f));
            }
            return result;
        }
        sealed partial class Builder
        {
            public readonly EpicCityPlan plan=new();
            readonly Dictionary<MayhemModule,Bounds> geometryBounds=new();
            Bounds ModuleBounds(MayhemModule module)
            {if(!geometryBounds.TryGetValue(module,out var bounds)){bounds=MayhemStructureLayout.Bounds(new[]{module});geometryBounds.Add(module,bounds);}return bounds;}
            int district;
            public MayhemModule Module(Vector3 p,string purpose,float yaw=0)
            {var m=new MayhemModule{district=district,position=p,purpose=purpose,rotation=Quaternion.Euler(0,yaw,0)};plan.modules.Add(m);return m;}
            public void Part(MayhemModule m,Vector3 p,Vector3 size,int finish=0,Quaternion? rotation=null)
            {if(size.x>0&&size.y>0&&size.z>0)m.shapes.Add(new MayhemShape(p,size,finish,rotation??Quaternion.identity));}
            public void Flight(MayhemModule m,Vector3 a,Vector3 c,float width=3.2f)
            {
                var delta=Vector3.ProjectOnPlane(c-a,Vector3.up);float z=delta.magnitude,rise=c.y-a.y,x=width*.5f;
                if(z<.01f)return;
                if(rise<-.001f){var swap=a;a=c;c=swap;delta=-delta;rise=-rise;}
                var q=Quaternion.LookRotation(delta,Vector3.up);
                // Top is a single smooth convex ramp. Parallel lanes leave full overhead clearance.
                var v=new[]{new Vector3(x,0,0),new Vector3(-x,0,0),new Vector3(-x,rise-.28f,z),new Vector3(x,rise-.28f,z),new Vector3(x,0,0),new Vector3(-x,0,0),new Vector3(-x,rise,z),new Vector3(x,rise,z)};
                m.shapes.Add(new MayhemShape(a,new Vector3(width,Mathf.Max(.28f,rise+.28f),z),2,q,v));
            }
            void Deck(MayhemModule m,float y,float w,float depth,float lane,int finish)
            {
                float near=lane-3.8f,far=lane+3.8f,hole=5.6f;
                Part(m,new Vector3(0,y-.35f,(-depth*.5f+near)*.5f),new Vector3(w,.7f,near+depth*.5f),finish);
                Part(m,new Vector3(0,y-.35f,(far+depth*.5f)*.5f),new Vector3(w,.7f,depth*.5f-far),finish);
                foreach(int s in new[]{-1,1})Part(m,new Vector3(s*(w*.5f+hole)*.5f,y-.35f,lane),new Vector3(w*.5f-hole,.7f,7.6f),finish);
            }
            MayhemModule Cassette(Vector3 p,float w,float depth,int floors,float yaw,string purpose,bool cover=true)
            {
                var m=Module(p,purpose,yaw);float h=floors*4,lane=-depth*.5f+7.5f;
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Part(m,new Vector3(x*(w*.5f-1.2f),h*.5f,z*(depth*.5f-1.2f)),new Vector3(2.4f,h,2.4f));
                if(cover)Part(m,new Vector3(-w*.5f+1.2f,h*.5f,depth*.25f),new Vector3(2.4f,h,6),3);
                for(int f=0;f<floors;f++)
                {
                    float y=f*4;Deck(m,y+4,w-1.2f,depth-1.2f,lane,1+(district%3));
                    float x=f%2==0?-5.6f:5.6f,z=lane+(f%2==0?-2.1f:2.1f);
                    Flight(m,new Vector3(x,y,z),new Vector3(-x,y+4,z));
                }
                return m;
            }
            void Base(EpicCitySite site,float w,float depth)
            {
                var m=Module(site.position+Vector3.up*.4f,"Broad bearing raft and street entries",site.heading);
                Part(m,Vector3.zero,new Vector3(w+1,.8f,depth+1),3);
                foreach(int sign in new[]{-1,1})Flight(m,new Vector3(0,-.4f,sign*(depth*.5f+6)),new Vector3(0,.4f,sign*(depth*.5f+1)),5);
            }
            public int Site(string kind,float x,float z,int tiers=3,float yaw=0,float elevation=0,string name=null)
            {
                district=plan.sites.Count;var site=new EpicCitySite{kind=kind,name=name??kind,position=new Vector3(x,elevation+.35f,z),heading=yaw,tiers=tiers,width=28,depth=28};plan.sites.Add(site);
                if(kind=="Court"){site.width=70;site.depth=58;}
                if(kind=="Hotel"){site.width=66;site.depth=32;}
                if(kind=="Gateway"){site.width=78;site.depth=32;}
                if(kind=="Sky Tree"){site.width=66;site.depth=66;}
                if(kind=="Parking"){site.width=40;site.depth=34;}
                if(kind=="Foundry"){site.width=42;site.depth=32;}
                if(kind=="Kissing Towers"){site.width=94;site.depth=34;}
                if(kind=="Drop Stop"){site.width=54;site.depth=36;}
                if(kind=="Skygarden"){site.width=78;site.depth=28;}
                if(kind=="Book Wing"){site.width=62;site.depth=30;}
                int first=plan.modules.Count;
                if(kind=="Kissing Towers"||kind=="Drop Stop"||kind=="Saddle"||kind=="Chalice"||kind=="Relay"||kind=="Zipper"||kind=="Spiral"||kind=="Cathedral")Import(site,kind=="Drop Stop"?5:kind=="Kissing Towers"?9:kind=="Saddle"?6:kind=="Chalice"?7:kind=="Relay"?3:kind=="Zipper"?1:kind=="Spiral"?2:4);
                else if(kind=="Sky Tree")Tree(site,tiers);
                else if(kind=="Book Wing")BookWing(site);
                else if(kind=="Court")
                {
                    Stack(site,new Vector3(-25,0,0),22,48,tiers);Stack(site,new Vector3(25,0,0),22,48,tiers);
                    Stack(site,new Vector3(0,0,26),26,20,Mathf.Max(1,tiers-1));
                    InternalLink(site,new Vector3(-16,12.8f,20),new Vector3(-11,12.8f,20));InternalLink(site,new Vector3(11,12.8f,20),new Vector3(16,12.8f,20));
                }
                else if(kind=="Hotel"||kind=="Gateway")
                {
                    float offset=kind=="Hotel"?21:26;
                    Stack(site,new Vector3(-offset,0,0),kind=="Hotel"?24:26,30,tiers);Stack(site,new Vector3(offset,0,0),kind=="Hotel"?24:26,30,Mathf.Max(2,tiers-1));
                    for(int level=1;level<Mathf.Min(3,tiers);level++)InternalLink(site,new Vector3(-offset+10,level*12+.8f,6),new Vector3(offset-10,level*12+.8f,6));
                }
                else if(kind=="Skygarden")
                {
                    Stack(site,Vector3.zero,26,28,tiers);foreach(int side in new[]{-1,1}){Stack(site,new Vector3(side*27,0,0),22,28,Mathf.Max(1,tiers-2));InternalLink(site,new Vector3(side*10,12.8f,5),new Vector3(side*18,12.8f,5));}
                }
                else Stack(site,Vector3.zero,site.width,site.depth,tiers,kind=="Office");
                if(kind=="Lantern"||kind=="Waterworks")Crown(site,kind,tiers*12+.8f);
                if(kind=="Foundry")
                {
                    var roof=Module(site.position+Vector3.up*(tiers*12+.8f),"Sawtooth hall roof",yaw);
                    for(int i=-1;i<=1;i++){Part(roof,new Vector3(i*12,1.8f,0),new Vector3(12,.65f,28),3,Quaternion.Euler(0,0,14));}
                }
                var own=plan.modules.GetRange(first,plan.modules.Count-first);site.members.AddRange(own);site.bounds=MayhemStructureLayout.Bounds(own);
                site.width=Mathf.Max(site.width,site.bounds.size.x);site.depth=Mathf.Max(site.depth,site.bounds.size.z);
                foreach(float y in new[]{4.8f,8.8f,12.8f,16.8f,24.8f,36.8f})if(y<=tiers*12+.8f)site.ports.Add(site.position+Vector3.up*y);
                plan.spawnPoints.Add(site.position+Quaternion.Euler(0,yaw,0)*new Vector3(0,1,-site.depth*.5f-8));
                return district;
            }
            void Stack(EpicCitySite site,Vector3 offset,float w,float depth,int tiers,bool setback=false)
            {
                var q=Quaternion.Euler(0,site.heading,0);var sub=new EpicCitySite{position=site.position+q*offset,heading=site.heading};Base(sub,w,depth);
                for(int t=0;t<tiers;t++)Cassette(sub.position+Vector3.up*(.8f+t*12),w-(setback?t*2:0),depth-(setback?t*1.2f:0),3,site.heading,site.name+" inhabited section");
            }
            void Crown(EpicCitySite site,string kind,float y)
            {
                var m=Module(site.position+Vector3.up*y,"Open "+kind+" crown",site.heading);float w=kind=="Waterworks"?38:24;
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Part(m,new Vector3(x*10,3,z*10),new Vector3(3,6,3),0);
                foreach(int s in new[]{-1,1}){Part(m,new Vector3(s*(w*.5f-2),6,0),new Vector3(4,.8f,w),2);Part(m,new Vector3(0,6,s*(w*.5f-2)),new Vector3(w-8,.8f,4),2);}
            }
            void Tree(EpicCitySite site,int tiers)
            {
                Base(site,62,62);
                for(int t=0;t<tiers;t++)
                {
                    var root=Module(site.position+Vector3.up*(.8f+t*12),"Integral three-wing canopy",site.heading);
                    for(int arm=0;arm<3;arm++)
                    {
                        var q=Quaternion.Euler(0,120*arm,0);var wing=Cassette(Vector3.zero,18,24,3,0,"Canopy wing");
                        foreach(var s in wing.shapes)root.shapes.Add(new MayhemShape(q*(new Vector3(0,0,16)+s.center),s.size,s.finish,q*s.rotation,s.vertices));plan.modules.Remove(wing);
                    }
                    for(int f=1;f<=3;f++)Part(root,new Vector3(0,f*4-.35f,0),new Vector3(9,.7f,9),2);
                }
            }
            void BookWing(EpicCitySite site)
            {
                var q=Quaternion.Euler(0,site.heading,0);
                Stack(site,Vector3.zero,24,30,2);
                var support=plan.modules[plan.modules.Count-1];
                var wing=Cassette(site.position+q*new Vector3(21.4f,16.8f,0),18,24,2,site.heading,"Hinged inhabited book wing");
                wing.hingeSupport=support;wing.pivot=site.position+q*new Vector3(12.2f,16.8f,0);
                var catcher=Module(site.position+q*new Vector3(21.4f,.6f,0),"Broad book-wing catch",site.heading);
                Part(catcher,Vector3.zero,new Vector3(17,1.2f,26),3);
                var foot=Module(site.position+q*new Vector3(21.4f,9,0),"Removable book-wing receiving foot",site.heading);
                Part(foot,Vector3.zero,new Vector3(17,15.6f,21),2);
            }
            void Import(EpicCitySite site,int tower)
            {
                var parts=TowerExhibitionLayout.Build(false,tower);if(tower!=5&&tower!=9&&parts.Count>site.tiers+1)parts.RemoveRange(site.tiers+1,parts.Count-site.tiers-1);var q=Quaternion.Euler(0,site.heading,0);var origin=TowerExhibitionLayout.Centers[tower];
                int tier=0;foreach(var m in parts){m.position=site.position+q*(m.position-origin);if(tower==3)m.position+=Vector3.up*Mathf.Max(0,tier++-1)*.012f;m.rotation=q*m.rotation;m.pivot=site.position+q*(m.pivot-origin);m.district=district;plan.modules.Add(m);}
                site.tiers=tower==5?5:tower==9?4:site.tiers;
            }
            void InternalLink(EpicCitySite site,Vector3 a,Vector3 b)
            {var q=Quaternion.Euler(0,site.heading,0);var first=site.position+q*a;var last=site.position+q*b;
                if(Clear(first,last,3))Bridge(first,last,3,"Independent internal crossing");}
            public void Link(int from,int to,float target=8.8f,float width=4.5f,bool streetLevel=false)
            {
                var a=plan.sites[from];var b=plan.sites[to];district=from;
                if(Mathf.Abs(target-8.8f)<.01f)target=4.8f+4*(plan.routes.Count%3);
                target=Mathf.Max(target,Mathf.Max(a.position.y,b.position.y)+(streetLevel?3.8f:8.8f));
                List<Vector3> path=null;
                for(int level=0;level<5&&path==null;level++)
                {
                    float height=target+level*4;var aa=Ports(a,b.position,height);var bb=Ports(b,a.position,height);
                    foreach(var pa in aa)
                    {
                        foreach(var pb in bb)
                        {
                            if(Clear(pa,pb,width)){path=new List<Vector3>{pa,pb};break;}
                        }
                        if(path!=null)break;
                    }
                }
                if(path==null)
                {
                    // A ground connection remains useful when a crowded facade cannot accept a bridge.
                    plan.routes.Add(new EpicCityRoute{name="Ground street: "+a.name+" to "+b.name,from=from,to=to,a=a.position,b=b.position});return;
                }
                var first=path[0];var last=path[path.Count-1];
                plan.routes.Add(new EpicCityRoute{name=a.name+" to "+b.name,from=from,to=to,a=first,b=last});
                float distance=Vector3.ProjectOnPlane(last-first,Vector3.up).magnitude;int spans=Mathf.Max(1,Mathf.CeilToInt(distance/32));
                var seats=new List<Vector3>{first};
                for(int n=1;n<spans;n++)
                {
                    var point=Vector3.Lerp(first,last,n/(float)spans);if(!FootClear(point))continue;
                    Trestle(point,last-first);seats.Add(point);
                }
                seats.Add(last);
                for(int n=1;n<seats.Count;n++)Bridge(seats[n-1],seats[n],width,"Loose city street crossing",n==1,n==seats.Count-1);
            }
            bool FootClear(Vector3 p)
            {
                foreach(var site in plan.sites){var bounds=site.bounds;bounds.Expand(18);if(p.x>bounds.min.x&&p.x<bounds.max.x&&p.z>bounds.min.z&&p.z<bounds.max.z)return false;}
                foreach(var m in plan.modules)
                {
                    if(!m.purpose.Contains("crossing")&&!m.purpose.Contains("receiving foot"))continue;
                    var bounds=ModuleBounds(m);bounds.Expand(12);
                    if(p.x>bounds.min.x&&p.x<bounds.max.x&&p.z>bounds.min.z&&p.z<bounds.max.z)return false;
                }
                return true;
            }
            List<Vector3> Ports(EpicCitySite site,Vector3 toward,float target)
            {
                var candidates=new List<Vector3>();float closest=float.MaxValue;
                foreach(var m in site.members)foreach(var s in m.shapes)
                {
                    var q=m.rotation*s.rotation;var size=s.size;var offset=Vector3.zero;
                    if(s.vertices!=null){var bounds=new Bounds(s.vertices[0],Vector3.zero);foreach(var v in s.vertices)bounds.Encapsulate(v);size=bounds.size;offset=bounds.center;}
                    if(size.y>1.01f||Vector3.Dot(q*Vector3.up,Vector3.up)<.999f||size.x<3||size.z<3)continue;
                    var center=m.position+m.rotation*s.center+q*offset;float y=center.y+size.y*.5f;if(y<site.position.y+3)continue;
                    float error=Mathf.Abs(y-target);if(error>closest+.05f)continue;if(error<closest-.05f){candidates.Clear();closest=error;}
                    float hx=Mathf.Max(0,size.x*.5f-3.5f),hz=Mathf.Max(0,size.z*.5f-3.5f);
                    foreach(float x in new[]{-hx,0f,hx})foreach(float z in new[]{-hz,0f,hz})candidates.Add(center+q*new Vector3(x,size.y*.5f,z));
                }
                candidates.Sort((x,y)=>Vector3.ProjectOnPlane(x-toward,Vector3.up).sqrMagnitude.CompareTo(Vector3.ProjectOnPlane(y-toward,Vector3.up).sqrMagnitude));
                return candidates.GetRange(0,Mathf.Min(40,candidates.Count));
            }
            bool Clear(Vector3 a,Vector3 b,float width)
            {
                var approach=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized*2; a-=approach;b+=approach;
                // Include both entry ramps and walking clearance, not just the middle of the span.
                var sweep=new Bounds((a+b)*.5f+Vector3.up*.99f,new Vector3(Mathf.Abs(b.x-a.x)+width+.3f,Mathf.Abs(b.y-a.y)+1.9f,Mathf.Abs(b.z-a.z)+width+.3f));
                foreach(var m in plan.modules)
                {
                    var broad=ModuleBounds(m);broad.Expand(4);if(!broad.Intersects(sweep))continue;
                    foreach(var s in m.shapes)
                    {
                    var q=Quaternion.Inverse(m.rotation*s.rotation);var center=m.position+m.rotation*s.center;
                    var size=s.size;if(s.vertices!=null){var bounds=new Bounds(s.vertices[0],Vector3.zero);foreach(var v in s.vertices)bounds.Encapsulate(v);center+=m.rotation*s.rotation*bounds.center;size=bounds.size;if(size.y>.9f){center+=Vector3.up*.9f;size.y+=1.8f;}}
                    var start=q*(a+Vector3.up*.99f-center);var end=q*(b+Vector3.up*.99f-center);
                    var half=size*.5f+new Vector3(width*.5f+.15f,.95f,width*.5f+.15f);
                    float lo=0,hi=1;bool inside=true;
                    for(int axis=0;axis<3;axis++)
                    {
                        float delta=end[axis]-start[axis];if(Mathf.Abs(delta)<.0001f){if(Mathf.Abs(start[axis])>half[axis]){inside=false;break;}}
                        else{float x=(-half[axis]-start[axis])/delta,y=(half[axis]-start[axis])/delta;if(x>y)(x,y)=(y,x);lo=Mathf.Max(lo,x);hi=Mathf.Min(hi,y);if(lo>hi){inside=false;break;}}
                    }
                    if(inside)return false;
                    }
                }
                // Raised terrain must not cut through a lower bridge.
                for(int n=0;n<=20;n++){var p=Vector3.Lerp(a,b,n/20f);if(GroundHeight(plan,p)>p.y-.5f)return false;}
                return true;
            }
            void Trestle(Vector3 p,Vector3 direction)
            {
                float ground=GroundHeight(plan,p);plan.bearingPads.Add(new Vector3(p.x,ground,p.z));float height=p.y-ground;
                var m=Module(new Vector3(p.x,ground,p.z),"Independent intermediate crossing trestle");var q=Quaternion.LookRotation(direction,Vector3.up);
                Part(m,new Vector3(0,.3f,0),new Vector3(8,.6f,8),3);
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    var top=q*new Vector3(x*2.3f,-.6f,z*2.3f)+Vector3.up*height;
                    Part(m,new Vector3(top.x,(top.y+.6f)*.5f,top.z),new Vector3(2,top.y-.6f,2));
                }
                Part(m,Vector3.up*height-q*Vector3.up*.3f,new Vector3(8,.6f,8),3,q);
            }
            void Bridge(Vector3 a,Vector3 b,float width,string purpose,bool entry=true,bool exit=true)
            {
                var mid=(a+b)*.5f;var dir=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized;
                a+=dir*.09f;b-=dir*.09f;var m=Module(mid,purpose);var q=Quaternion.LookRotation(b-a,Vector3.up);
                Part(m,(a+b)*.5f-mid+Vector3.up*.22f,new Vector3(width,.44f,(b-a).magnitude),2,q);
                if(entry)Flight(m,a-dir*1.8f-mid,a+dir*.1f+Vector3.up*.44f-mid,width);
                if(exit)Flight(m,b+dir*1.8f-mid,b-dir*.1f+Vector3.up*.44f-mid,width);
            }
            public void Finish()
            {
                var bounds=MayhemStructureLayout.Bounds(plan.modules);
                plan.halfSize=new Vector2(Mathf.Max(180,Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x))+25),Mathf.Max(160,Mathf.Max(Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z))+25));
            }
        }
    }
}
