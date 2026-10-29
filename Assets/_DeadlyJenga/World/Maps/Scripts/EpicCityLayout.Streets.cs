using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public static partial class EpicCityLayout
    {
        sealed partial class Builder
        {
            // The skyline sits inside an inhabited fabric, rather than on isolated plinths.
            public void WeaveStreets(int design)
            {
                int skyline=plan.sites.Count;
                var targets=new List<Vector3>();
                foreach(var route in plan.routes)
                {
                    var a=plan.sites[route.from].position;var b=plan.sites[route.to].position;
                    targets.Add(Vector3.Lerp(a,b,.5f));
                    if(Vector3.Distance(a,b)>120){targets.Add(Vector3.Lerp(a,b,.3f));targets.Add(Vector3.Lerp(a,b,.7f));}
                }
                // Each theme bends its lower streets through the open middle as well as the edge.
                for(int i=0;i<8;i++)
                {
                    float angle=(i*45+design*13)*Mathf.Deg2Rad;
                    float radius=design==2?45:design==5?55:60;
                    targets.Add(new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
                }
                var low=new List<int>();int desired=design==4||design==6?18:16;
                foreach(var target in targets)
                {
                    if(low.Count>=desired)break;
                    if(!FindStreetSeat(target,design,low.Count,out var point,out float yaw))continue;
                    int kind=(low.Count+design)%5;
                    low.Add(LowSite(point,yaw,kind,low.Count));
                }
                // Fill remaining interior pockets without expanding the island to disguise empty space.
                for(int z=-110;z<=110&&low.Count<desired;z+=40)for(int x=-110;x<=110&&low.Count<desired;x+=40)
                {
                    if(!FindStreetSeat(new Vector3(x,0,z),design,low.Count,out var point,out float yaw))continue;
                    low.Add(LowSite(point,yaw,(low.Count+design)%5,low.Count));
                }
                var edges=new HashSet<long>();
                foreach(int index in low)
                {
                    var site=plan.sites[index];var neighbors=new List<int>();
                    for(int n=0;n<plan.sites.Count;n++)if(n!=index)neighbors.Add(n);
                    neighbors.Sort((a,b)=>(plan.sites[a].position-site.position).sqrMagnitude.CompareTo((plan.sites[b].position-site.position).sqrMagnitude));
                    int joined=0,attempted=0;
                    foreach(int other in neighbors)
                    {
                        if(Vector3.Distance(site.position,plan.sites[other].position)>115)continue;
                        long key=((long)Math.Min(index,other)<<32)|(uint)Math.Max(index,other);if(edges.Contains(key))continue;
                        if(attempted++>=6)break;
                        int before=plan.routes.Count;Link(index,other,4.8f,5,true);
                        if(plan.routes.Count>before&&!plan.routes[before].name.StartsWith("Ground street",StringComparison.Ordinal)){edges.Add(key);joined++;}
                        else if(plan.routes.Count>before)plan.routes.RemoveAt(before);
                        if(joined>=2)break;
                    }
                    // Keep a direct pedestrian relationship to a skyline entrance as well.
                    int closest=0;for(int n=1;n<skyline;n++)if((plan.sites[n].position-site.position).sqrMagnitude<(plan.sites[closest].position-site.position).sqrMagnitude)closest=n;
                    plan.routes.Add(new EpicCityRoute{name="Ground street: "+site.name+" to "+plan.sites[closest].name,from=index,to=closest,a=site.position,b=plan.sites[closest].position});
                }
            }
            bool FindStreetSeat(Vector3 target,int design,int number,out Vector3 point,out float yaw)
            {
                var occupied=new List<Bounds>();
                foreach(var m in plan.modules){var bounds=ModuleBounds(m);bounds.Expand(new Vector3(5,.08f,5));occupied.Add(bounds);}
                // Search nearby pockets, keeping the actual tower seats and bridge trestles clear.
                for(int ring=0;ring<=5;ring++)for(int turn=0;turn<(ring==0?1:8);turn++)
                {
                    float angle=turn*45*Mathf.Deg2Rad;var p=target+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*7;
                    if(Mathf.Abs(p.x)>145||Mathf.Abs(p.z)>140)continue;
                    p.y=GroundHeight(plan,p);
                    foreach(float heading in new[]{((design+number)%2)*90f,((design+number+1)%2)*90f})
                    {
                        var envelope=new Bounds(p+Vector3.up*5.5f,heading==0?new Vector3(32,11,42):new Vector3(42,11,32));
                        if(design==2||design==5||design==9)envelope.size=new Vector3(56,11,56);
                        bool clear=true;
                        foreach(var bounds in occupied)
                        {
                            if(bounds.Intersects(envelope)){clear=false;break;}
                        }
                        if(!clear)continue;
                        // A pavilion must not sit across a steep terrace shoulder.
                        foreach(var offset in new[]{new Vector3(16,0,16),new Vector3(-16,0,16),new Vector3(16,0,-16),new Vector3(-16,0,-16)})
                            if(Mathf.Abs(GroundHeight(plan,p+offset)-p.y)>2){clear=false;break;}
                        if(!clear)continue;
                        point=p;yaw=heading;return true;
                    }
                }
                point=default;yaw=0;return false;
            }
            int LowSite(Vector3 position,float yaw,int kind,int number)
            {
                district=plan.sites.Count;
                string[] kinds={"Folded street terrace","Open market arcade","Split courtyard pavilion","Branch mesa walk","Stepped portico"};
                var site=new EpicCitySite{kind="Street fabric",name=kinds[kind]+" "+(number+1),position=position,heading=yaw,tiers=1,width=32,depth=30};plan.sites.Add(site);
                int first=plan.modules.Count;var q=Quaternion.Euler(0,yaw,0);
                var sub=new EpicCitySite{position=position,heading=yaw};Base(sub,26,28);
                var hall=Cassette(position+Vector3.up*.8f,26,28,kind==1?2:1,yaw,site.name+" inhabited section",false);
                if(kind==0||kind==2||kind==4)
                {
                    var upper=Module(position+Vector3.up*.8f,"Lower street courtyard and folded terrace",yaw);
                    // The upper fragments leave the central court and the main stair slot open.
                    Part(upper,new Vector3(10,8-.35f,5),new Vector3(6,.7f,16),3);
                    foreach(int z in new[]{-1,1})Part(upper,new Vector3(11,6,5+z*6),new Vector3(2,4,2));
                    Flight(upper,new Vector3(-8,4,9),new Vector3(8,8,9),3.2f);
                    if(kind==2)
                    {
                        Part(upper,new Vector3(-10,8-.35f,5),new Vector3(6,.7f,16),1);
                        foreach(int z in new[]{-1,1})Part(upper,new Vector3(-11,6,5+z*6),new Vector3(2,4,2));
                    }
                    if(kind==0)Flight(upper,new Vector3(-8,4,4),new Vector3(8,8,4),4);
                }
                else if(kind==1)
                {
                    // Sawtooth shelter is at the back; the roof stair exits keep full headroom.
                    var canopy=Module(position+Vector3.up*9.15f,"Open market split canopy",yaw);
                    foreach(int s in new[]{-1,1})Part(canopy,new Vector3(s*6.5f,.3f,9),new Vector3(13,.6f,5),3,Quaternion.Euler(0,0,s*7));
                }
                else if(kind==3)
                {
                    for(int arm=-1;arm<=1;arm++)Part(hall,new Vector3(arm*8,4-.35f,13),new Vector3(7,.7f,8),1+(arm+1)%3,Quaternion.Euler(0,arm*20,0));
                }
                // One compact lower building is one physical piece, including its folded circulation.
                var own=plan.modules.GetRange(first,plan.modules.Count-first);
                var combined=new MayhemModule{district=district,position=position,rotation=q,purpose=site.name+" inhabited section"};
                foreach(var m in own)foreach(var shape in m.shapes)
                    combined.shapes.Add(new MayhemShape(Quaternion.Inverse(q)*(m.position+m.rotation*shape.center-position),shape.size,shape.finish,Quaternion.Inverse(q)*m.rotation*shape.rotation,shape.vertices));
                plan.modules.RemoveRange(first,own.Count);plan.modules.Add(combined);site.members.Add(combined);site.bounds=MayhemStructureLayout.Bounds(site.members);
                plan.spawnPoints.Add(position+q*new Vector3(0,1,-24));
                return district;
            }
        }
    }
}
