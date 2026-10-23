using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastleBots
{
    // Bounded, frame-sliced graph search. Shared geometry sampling is cheap; expensive
    // collision/trajectory checks are lazy and limited per bot per frame.
    public sealed class CastleBotJourneyPlanner
    {
        sealed class Snapshot
        {
            public IWorldQuery generator;
            public Rigidbody first;
            public float at,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
            public readonly List<CastleBotLanding> nodes=new List<CastleBotLanding>();
            public readonly Dictionary<Vector3Int,List<int>> cells=new Dictionary<Vector3Int,List<int>>();
        }
        static Snapshot shared;
        static Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/3),Mathf.FloorToInt(p.y/3),Mathf.FloorToInt(p.z/3));
        static Snapshot Geometry(IWorldQuery generator)
        {
            var first=generator.Blocks.Count>0?generator.Blocks[0]:null;
            if(shared!=null&&shared.generator==generator&&shared.first==first&&Time.time-shared.at<1)return shared;
            var graph=new Snapshot{generator=generator,first=first,at=Time.time};
            var samples=new List<Vector3>(18);
            foreach(var rb in generator.Blocks)
            {
                if(!rb||!rb.gameObject.activeInHierarchy||!rb.isKinematic&&rb.linearVelocity.magnitude>1.2f)continue;
                var box=rb.GetComponent<BoxCollider>();if(!box)continue;
                CastleBotNavigation.SurfaceSamples(rb,box,samples);
                foreach(var p in samples)
                {
                    var local=rb.transform.InverseTransformPoint(p);
                    int id=graph.nodes.Count;graph.nodes.Add(new CastleBotLanding{surface=rb.transform,body=rb,local=local});
                    var cell=Cell(p);if(!graph.cells.TryGetValue(cell,out var list))graph.cells[cell]=list=new List<int>();list.Add(id);
                    graph.minY=Mathf.Min(graph.minY,p.y);graph.maxY=Mathf.Max(graph.maxY,p.y);
                }
            }
            shared=graph;return graph;
        }
        public static Vector3 ChooseDestination(CastleBotBrain bot,int trip)
        {
            var graph=Geometry(bot.Population.Session.world);var p=bot.Personality;var feet=bot.transform.position;
            float fraction=Mathf.Repeat(bot.Seed*.173f+trip*.618034f,1);
            float targetY=Mathf.Lerp(feet.y,Mathf.Lerp(graph.minY,graph.maxY,fraction),p.verticalVariety);
            Vector3 bearing=Quaternion.Euler(0,trip*137.508f,0)*bot.WanderDirection;
            var ranked=new List<CastleBotLanding>();
            foreach(var node in graph.nodes)
            {
                var point=node.Point;if(Vector3.Distance(point,feet)<3)continue;
                var candidate=node;
                candidate.score=-Mathf.Abs(point.y-targetY)*.45f+Vector3.Dot(Vector3.ProjectOnPlane(point-feet,Vector3.up).normalized,bearing)*2;
                candidate.score-=bot.Population.ThreatCost(point)*(1+p.survivalPriority)+bot.RevisitCost(node.body);
                candidate.score+=Mathf.Min(16,Vector3.Distance(point,feet))*.025f*p.exploration;
                ranked.Add(candidate);
            }
            ranked.Sort((a,b)=>b.score.CompareTo(a.score));
            for(int i=0;i<Mathf.Min(24,ranked.Count);i++)if(CastleBotNavigation.Landing(bot.Motor,ranked[i].Point,out _))return ranked[i].Point;
            return feet+bearing*5;
        }
        public static bool RecoveryStage(CastleBotBrain bot,Vector3 destination,List<Vector3> recent,out Vector3 stage)
        {
            stage=default;if(!BotPlanningBudget.TrySense(Mathf.RoundToInt(bot.Seed/7.193f)-1,bot.Population.Count))return false;
            var graph=Geometry(bot.Population.Session.world);var feet=bot.transform.position;
            var candidates=new List<CastleBotLanding>();
            foreach(var node in graph.nodes)
            {
                var p=node.Point;float rise=p.y-feet.y;float distance=Vector3.ProjectOnPlane(p-feet,Vector3.up).magnitude;
                if(rise<.6f||rise>bot.Motor.jumpHeight+bot.Motor.vaultMaxRise-.15f||distance<3||distance>Mathf.Clamp(bot.Population.Session.world.PlayableBounds.size.magnitude*.6f,30,80))continue;
                bool visited=false;foreach(var old in recent)if((old-p).sqrMagnitude<9){visited=true;break;}if(visited)continue;
                var candidate=node;candidate.score=distance+Vector3.ProjectOnPlane(destination-p,Vector3.up).magnitude;candidates.Add(candidate);
            }
            candidates.Sort((a,b)=>a.score.CompareTo(b.score));
            int tested=0,inspected=0;
            foreach(var candidate in candidates)
            {
                if(inspected++>=2048||tested>=32)break;var p=candidate.Point;
                if(!BotTraversalCache.Landing(bot.Motor,p)||Physics.Raycast(p+Vector3.up*.15f,Vector3.up,8,CastleBotNavigation.Mask,QueryTriggerInteraction.Ignore))continue;
                tested++;
                // Open consecutive steps distinguish an ascending approach from another roof.
                var cell=Cell(p);bool ascending=false;
                for(int x=-1;x<=1&&!ascending;x++)for(int y=0;y<=1&&!ascending;y++)for(int z=-1;z<=1&&!ascending;z++)
                {
                    if(!graph.cells.TryGetValue(cell+new Vector3Int(x,y,z),out var indices))continue;
                    foreach(int index in indices)
                    {
                        var q=graph.nodes[index].Point;float rise=q.y-p.y;
                        if(rise<.8f||rise>2.5f||Vector3.ProjectOnPlane(q-p,Vector3.up).sqrMagnitude>12||!BotTraversalCache.Landing(bot.Motor,q))continue;
                        if(Physics.Raycast(q+Vector3.up*.15f,Vector3.up,6,CastleBotNavigation.Mask,QueryTriggerInteraction.Ignore))continue;
                        if(CastleBotNavigation.TryLink(bot.Motor,p,q,out _,out _)){ascending=true;break;}
                    }
                }
                if(ascending&&(!bot.Motor.CarryingFlag||AscendingCorridor(bot,graph,p,destination))){stage=p;bot.RecoveryDiagnostics="candidates="+candidates.Count+" inspected="+inspected+" open="+tested+" stage="+stage;return true;}
            }
            bot.RecoveryDiagnostics="candidates="+candidates.Count+" inspected="+inspected+" open="+tested+" no ascending stage";
            return false;
        }
        static bool AscendingCorridor(CastleBotBrain bot,Snapshot graph,Vector3 start,Vector3 destination)
        {
            var cursor=start;float required=Mathf.Min(destination.y-.2f,start.y+5.5f);
            var options=new List<CastleBotLanding>();
            for(int step=0;step<3;step++)
            {
                options.Clear();var cell=Cell(cursor);
                float before=Vector3.ProjectOnPlane(destination-cursor,Vector3.up).magnitude;
                for(int x=-1;x<=1;x++)for(int y=0;y<=1;y++)for(int z=-1;z<=1;z++)
                {
                    if(!graph.cells.TryGetValue(cell+new Vector3Int(x,y,z),out var indices))continue;
                    foreach(int index in indices)
                    {
                        var node=graph.nodes[index];var q=node.Point;float rise=q.y-cursor.y;
                        if(rise<.8f||rise>2.5f||Vector3.ProjectOnPlane(q-cursor,Vector3.up).sqrMagnitude>12)continue;
                        float after=Vector3.ProjectOnPlane(destination-q,Vector3.up).magnitude;
                        if(before>12&&after>before+.5f)continue;
                        node.score=after+Vector3.Distance(cursor,q)*.1f;options.Add(node);
                    }
                }
                options.Sort((a,b)=>a.score.CompareTo(b.score));bool advanced=false;int inspected=0;
                foreach(var node in options)
                {
                    if(inspected++>=16)break;var q=node.Point;
                    if(!BotTraversalCache.Landing(bot.Motor,q)||Physics.Raycast(q+Vector3.up*.15f,Vector3.up,6,CastleBotNavigation.Mask,QueryTriggerInteraction.Ignore))continue;
                    if(!BotTraversalCache.TryLink(bot.Motor,cursor,q,out _,out _))continue;
                    cursor=q;advanced=true;break;
                }
                if(!advanced)return false;
                if(cursor.y>=required)return true;
            }
            return false;
        }
        public static bool DepartureStage(CastleBotBrain bot,Vector3 destination,out Vector3 stage)
            => DepartureStage(bot,destination,out stage,out _,out _);
        public static bool DepartureStage(CastleBotBrain bot,Vector3 destination,out Vector3 stage,out Vector3 approach,out CastleBotRoute traversal)
        {
            stage=default;approach=default;traversal=CastleBotRoute.Walk;
            var graph=Geometry(bot.Population.Session.world);var feet=bot.transform.position;
            if(bot.Motor.HasWalkableIsland)
            {
                var toward=Vector3.ProjectOnPlane(destination-feet,Vector3.up).normalized;
                for(int bearing=0;bearing<5;bearing++)for(int length=0;length<4;length++)
                {
                    float angle=bearing==0?0:(bearing%2==0?1:-1)*((bearing+1)/2)*40;
                    var direction=Quaternion.Euler(0,angle,0)*toward;
                    var probe=feet+direction*(3+length*2);
                    if(!CastleBotNavigation.Ground(probe+Vector3.up*.4f,CastleBotNavigation.MaximumDrop(bot.Motor)+.5f,out var floor))continue;
                    float drop=feet.y-floor.point.y;
                    if(drop<.5f||drop>CastleBotNavigation.MaximumDrop(bot.Motor)||!CastleBotNavigation.Room(bot.Motor,floor.point+Vector3.up*.04f))continue;
                    for(int launch=0;launch<4;launch++)
                    {
                        var start=feet+direction*launch;
                        if(!CastleBotNavigation.Ground(start+Vector3.up*.3f,.6f,out var top)||Mathf.Abs(top.point.y-feet.y)>.2f||!CastleBotNavigation.Walkable(bot.Motor,feet,top.point))continue;
                        if(!CastleBotNavigation.TryLink(bot.Motor,top.point,floor.point,out var route,out _))continue;
                        stage=floor.point;approach=top.point;traversal=route;bot.RecoveryDiagnostics="departure terrain: "+approach+" -> "+stage;return true;
                    }
                }
            }
            var lower=new List<CastleBotLanding>();
            foreach(var node in graph.nodes)
            {
                var p=node.Point;float drop=feet.y-p.y;
                if(drop<.6f||drop>CastleBotNavigation.MaximumDrop(bot.Motor)||Vector3.ProjectOnPlane(p-feet,Vector3.up).sqrMagnitude>20*20)continue;
                var candidate=node;candidate.score=Vector3.ProjectOnPlane(p-destination,Vector3.up).magnitude*.3f+Vector3.Distance(feet,p)*.1f+drop*.1f;
                lower.Add(candidate);
            }
            lower.Sort((a,b)=>a.score.CompareTo(b.score));
            int inspected=0,links=0;
            foreach(var candidate in lower)
            {
                if(inspected++>1024||links>=32)break;
                var end=candidate.Point;
                if(!BotTraversalCache.Landing(bot.Motor,end))continue;
                var cell=Cell(end);
                for(int x=-2;x<=2;x++)for(int y=0;y<=4;y++)for(int z=-2;z<=2;z++)
                {
                    if(!graph.cells.TryGetValue(cell+new Vector3Int(x,y,z),out var nodes))continue;
                    foreach(int index in nodes)
                    {
                        var start=graph.nodes[index].Point;
                        if(Mathf.Abs(start.y-feet.y)>.2f||Vector3.ProjectOnPlane(start-end,Vector3.up).sqrMagnitude>49||!BotTraversalCache.Landing(bot.Motor,start))continue;
                        if(!CastleBotNavigation.Walkable(bot.Motor,feet,start)&&!bot.Population.WalkingRoutes.TryWaypoint(bot,start,out _))continue;
                        if(++links>32){bot.RecoveryDiagnostics="departure: link limit; lower="+lower.Count+" inspected="+inspected;return false;}
                        if(!BotTraversalCache.TryLink(bot.Motor,start,end,out var route,out _))continue;
                        stage=end;approach=start;traversal=route;bot.RecoveryDiagnostics="departure: "+start+" -> "+end+" inspected="+inspected+" links="+links;return true;
                    }
                }
            }
            bot.RecoveryDiagnostics="departure: no route; lower="+lower.Count+" inspected="+inspected+" links="+links;
            return false;
        }
        struct SearchNode {public float cost;public int parent;public CastleBotRoute route;}
        struct Candidate {public int id;public float priority;}
        readonly CastleBotBrain bot;
        readonly Snapshot graph;
        readonly Vector3 start,destination;
        readonly Dictionary<int,SearchNode> discovered=new Dictionary<int,SearchNode>();
        readonly Dictionary<int,bool> validity=new Dictionary<int,bool>();
        readonly List<int> open=new List<int>();
        readonly HashSet<int> closed=new HashSet<int>();
        readonly List<Candidate> neighbors=new List<Candidate>();
        int expanding=-1,neighborCursor,best=-1,escapeBest=-1,expanded,checkedEdges;
        float escapeScore=float.PositiveInfinity;
        float bestDistance;
        public float StartedAt { get; } = Time.time;
        public float Age => Time.time - StartedAt;
        bool haveExpansion;
        public bool Complete {get;private set;}
        public string Diagnostics=>"expanded="+expanded+" checked="+checkedEdges+" best="+(best>=0?Point(best).ToString():"none")+" escape="+(escapeBest>=0?Point(escapeBest).ToString():"none")+" result="+Result.Count;
        public readonly List<CastleBotLanding> Result=new List<CastleBotLanding>();
        public CastleBotJourneyPlanner(CastleBotBrain bot,Vector3 destination)
        {
            this.bot=bot;this.destination=destination;start=bot.transform.position;graph=Geometry(bot.Population.Session.world);
            bestDistance=Heuristic(start);discovered[-1]=new SearchNode{parent=-1};open.Add(-1);
        }
        float Heuristic(Vector3 p)
        {var delta=p-destination;return Vector3.ProjectOnPlane(delta,Vector3.up).magnitude*.3f+Mathf.Abs(delta.y)*.5f;}
        Vector3 Point(int id)=>id<0?start:graph.nodes[id].Point;
        bool Valid(int id)
        {
            if(validity.TryGetValue(id,out bool valid))return valid;
            var node=graph.nodes[id];valid=node.surface&&node.surface.gameObject.activeInHierarchy&&node.body&&(node.body.isKinematic||node.body.linearVelocity.magnitude<1.5f)&&BotTraversalCache.Landing(bot.Motor,node.Point);
            validity[id]=valid;return valid;
        }
        void Neighbors(int id)
        {
            neighbors.Clear();neighborCursor=0;var p=Point(id);var cell=Cell(p);
            float v=Mathf.Sqrt(2*bot.Motor.gravity*bot.Motor.jumpHeight);
            float range=Mathf.Clamp(bot.Motor.EffectiveWalkSpeed*v*2/bot.Motor.gravity+bot.Motor.vaultReach,2,7);
            int cells=Mathf.CeilToInt(range/3);
            for(int x=-cells;x<=cells;x++)for(int y=-Mathf.CeilToInt(CastleBotNavigation.MaximumDrop(bot.Motor)/3);y<=2;y++)for(int z=-cells;z<=cells;z++)
            {
                if(!graph.cells.TryGetValue(cell+new Vector3Int(x,y,z),out var list))continue;
                foreach(int next in list)
                {
                    if(next==id||closed.Contains(next))continue;var point=graph.nodes[next].Point;var delta=point-p;
                    if(delta.y>Mathf.Max(.1f+bot.Motor.jumpHeight+bot.Motor.vaultMaxRise,bot.Motor.climbSpeed*bot.Motor.climbGripSeconds*.65f)||delta.y< -CastleBotNavigation.MaximumDrop(bot.Motor)||Vector3.ProjectOnPlane(delta,Vector3.up).sqrMagnitude>range*range||delta.sqrMagnitude<.08f)continue;
                    float priority=Heuristic(point)+delta.magnitude*.1f+bot.RevisitCost(graph.nodes[next].body)*.2f;
                    neighbors.Add(new Candidate{id=next,priority=priority});
                }
            }
            neighbors.Sort((a,b)=>a.priority.CompareTo(b.priority));
            // Keep several nearby stepping stones, not just nodes nearer the destination.
            // Otherwise a required sideways/backwards step would be pruned at every corner.
            if(neighbors.Count>48)
            {
                var local=new List<Candidate>(neighbors);local.Sort((a,b)=>(Point(a.id)-p).sqrMagnitude.CompareTo((Point(b.id)-p).sqrMagnitude));
                neighbors.RemoveRange(36,neighbors.Count-36);
                for(int i=0;i<12;i++)if(!neighbors.Exists(n=>n.id==local[i].id))neighbors.Add(local[i]);
            }
        }
        public void Advance(int edgeBudget=12)
        {
            BotPlanningBudget.BeginWork();
            try { AdvanceWork(edgeBudget); }
            finally { BotPlanningBudget.EndWork(); }
        }
        void AdvanceWork(int edgeBudget)
        {
            if(Complete)return;
            if(Age>2){Finish();return;}
            int validations=0,operations=0;
            while(validations<edgeBudget&&operations++<64)
            {
                if(!haveExpansion)
                {
                    if(open.Count==0||expanded>=48||checkedEdges>=256){Finish();return;}
                    int selected=0;float score=float.PositiveInfinity;
                    for(int i=0;i<open.Count;i++){int key=open[i];float s=discovered[key].cost+Heuristic(Point(key));if(s<score){score=s;selected=i;}}
                    expanding=open[selected];open.RemoveAt(selected);if(closed.Contains(expanding))continue;closed.Add(expanding);expanded++;
                    float progressScore=Heuristic(Point(expanding));
                    if(expanding>=0&&progressScore<bestDistance-.2f){best=expanding;bestDistance=progressScore;}
                    if(expanding>=0&&Vector3.Distance(Point(expanding),destination)<1){Finish();return;}
                    Neighbors(expanding);haveExpansion=true;
                }
                if(neighborCursor>=neighbors.Count){haveExpansion=false;continue;}
                if(!BotPlanningBudget.TryEdge(Mathf.RoundToInt(bot.Seed/7.193f)-1,bot.Population.PlanningCount))return;
                int next=neighbors[neighborCursor++].id;validations++;
                if(!Valid(next))continue;
                var from=Point(expanding);var to=Point(next);
                float drop=from.y-to.y;
                if(drop>CastleBotNavigation.MaximumDrop(bot.Motor))continue;
                // Blocked/covered surface samples must not consume the entire
                // route-search budget before real stepping stones are tested.
                checkedEdges++;
                CastleBotRoute kind;bool linked;
                // The bot may move on a local fallback while this search runs.
                // Every edge must use its recorded source, including the root.
                linked=BotTraversalCache.TryLink(bot.Motor,from,to,out kind,out _);
                if(!linked&&expanding<0&&Vector3.Distance(bot.transform.position,start)<.15f)
                    linked=CastleBotNavigation.Route(bot.Motor,to,bot.Abilities,out kind);
                if(!linked)continue;
                float cost=discovered[expanding].cost+Vector3.Distance(from,to)*.15f+.15f;
                if(kind!=CastleBotRoute.Walk)cost+=.3f+(1-bot.Personality.bravery)*.3f;
                cost+=bot.Population.Crowding(to,bot)*.5f;
                cost+=bot.Population.ThreatCost(to)*(1+bot.Personality.survivalPriority)+bot.RevisitCost(graph.nodes[next].body)*.2f+bot.RecentStopCost(to)*.6f;
                if(discovered.TryGetValue(next,out var previous)&&previous.cost<=cost)continue;
                discovered[next]=new SearchNode{parent=expanding,cost=cost,route=kind};if(!open.Contains(next))open.Add(next);
                // A useful partial route is preferable to standing still waiting for a
                // full trip across many storeys to fit in one bounded search.
                float h=Heuristic(to);if(h<bestDistance-.2f){best=next;bestDistance=h;}
                // Some routes first descend or circle away to reach the next staircase.
                // Keep a real, safe alternative rather than demanding immediate height progress.
                if(graph.nodes[next].body!=bot.Motor.StandingBody&&Vector3.Distance(to,start)>1)
                {
                    float alternative=h+bot.RevisitCost(graph.nodes[next].body)*4+bot.RecentStopCost(to)*6;
                    if(alternative<escapeScore){escapeScore=alternative;escapeBest=next;}
                }
            }
            if(checkedEdges>=256)Finish();
        }
        void Finish()
        {
            Complete=true;
            if(escapeBest>=0&&(best<0||graph.nodes[best].body==bot.Motor.StandingBody&&Vector3.Distance(Point(best),start)<2))best=escapeBest;
            if(best<0)return;
            int node=best,guard=0;
            while(node>=0&&guard++<64)
            {var landing=graph.nodes[node];var state=discovered[node];landing.route=state.route;Result.Add(landing);node=state.parent;}
            Result.Reverse();
        }
    }
}
