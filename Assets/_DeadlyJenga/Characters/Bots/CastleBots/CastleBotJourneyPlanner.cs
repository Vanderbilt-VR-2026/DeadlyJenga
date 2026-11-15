using Unity.Profiling;
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
        static readonly ProfilerMarker DJBotDestinationMarker = new ProfilerMarker("DJ.BotDestination");
        static readonly ProfilerMarker DJBotGeometryMarker = new ProfilerMarker("DJ.BotGeometry");
        static readonly ProfilerMarker NeighborsMarker = new("DJ.Bot.Neighbors");
        sealed class Snapshot
        {
            public IWorldQuery generator;
            public Rigidbody first;
            public float at,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
            public readonly List<(Rigidbody body,int version,bool included)> bodies=new();
            public readonly List<CastleBotLanding> nodes=new List<CastleBotLanding>();
            public readonly Dictionary<Vector3Int,List<int>> cells=new Dictionary<Vector3Int,List<int>>();
        }
        const int DenseGeometrySize = 256;
        const int GeometryBodiesPerFrame = 48;
        static Snapshot shared, pending;
        static int geometryCursor, lastGeometryFrame = -1;
        static bool checkingGeometry;
        static Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/3),Mathf.FloorToInt(p.y/3),Mathf.FloorToInt(p.z/3));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGeometry()
        {
            shared = pending = null;
            geometryCursor = 0;
            lastGeometryFrame = -1;
        }

        static Snapshot Geometry(IWorldQuery generator)
        {
            var blocks = generator.Blocks;
            var first = blocks.Count > 0 ? blocks[0] : null;
            if (shared == null || !ReferenceEquals(shared.generator, generator) || shared.first != first)
            {
                shared = new Snapshot { generator = generator, first = first, at = float.NegativeInfinity };
                pending = null;
            }
            if (pending == null && Time.time - shared.at >= 1)
            {
                pending = new Snapshot { generator = generator, first = first };
                checkingGeometry = shared.bodies.Count == blocks.Count;
                geometryCursor = 0;
            }
            AdvanceGeometry(generator);
            return shared;
        }

        // Continue even when no bot asks for a new route on this frame.
        internal static void AdvanceGeometry(IWorldQuery generator)
        {
            if (pending == null || !ReferenceEquals(pending.generator, generator)) return;
            var bodies = generator.Blocks;
            if (bodies.Count > 0 && pending.first != bodies[0] || geometryCursor > bodies.Count)
            {
                pending = null;
                return;
            }
            bool dense = bodies.Count >= DenseGeometrySize;
            if (dense && lastGeometryFrame == Time.frameCount) return;
            lastGeometryFrame = Time.frameCount;
            using var measurement = DJBotGeometryMarker.Auto();
            int limit = dense ? GeometryBodiesPerFrame : int.MaxValue;
            for (int work = 0; work < limit && geometryCursor < bodies.Count; work++)
            {
                var body = bodies[geometryCursor];
                bool included = body && body.gameObject.activeInHierarchy &&
                    (body.isKinematic || body.linearVelocity.sqrMagnitude <= 1.44f);
                if (included) BotGeometrySamples.Fill(body, geometryScratch);
                int version = body ? BotGeometrySamples.Version(body) : 0;
                if (checkingGeometry)
                {
                    if (geometryCursor >= shared.bodies.Count ||
                        shared.bodies[geometryCursor] != (body, version, included))
                    {
                        checkingGeometry = false;
                        geometryCursor = 0;
                        continue;
                    }
                }
                else
                {
                    pending.bodies.Add((body, version, included));
                    if (included) AddBodyGeometry(pending, body);
                }
                geometryCursor++;
            }
            if (geometryCursor != bodies.Count) return;
            if (!checkingGeometry) shared = pending;
            shared.at = Time.time;
            pending = null;
        }

        static void AddBodyGeometry(Snapshot graph, Rigidbody body)
        {
            var anchor = body.transform;
            var inverse = anchor.worldToLocalMatrix;
            foreach (var point in geometryScratch)
            {
                int id = graph.nodes.Count;
                graph.nodes.Add(new CastleBotLanding
                {
                    surface = anchor, body = body, local = inverse.MultiplyPoint3x4(point)
                });
                var cell = Cell(point);
                if (!graph.cells.TryGetValue(cell, out var list)) graph.cells[cell] = list = new List<int>();
                list.Add(id);
                graph.minY = Mathf.Min(graph.minY, point.y);
                graph.maxY = Mathf.Max(graph.maxY, point.y);
            }
        }
        static readonly List<Vector3> geometryScratch=new(64);
        static readonly List<CastleBotLanding> destinationRanks=new(25);
        public static Vector3 ChooseDestination(CastleBotBrain bot,int trip)
        {
            using var measurement = DJBotDestinationMarker.Auto();
            var graph=Geometry(bot.Population.Session.world);var p=bot.Personality;var feet=bot.transform.position;
            Vector3 bearing=Quaternion.Euler(0,trip*137.508f,0)*bot.WanderDirection;
            if (graph.nodes.Count == 0) return feet + bearing * 5;
            float fraction=Mathf.Repeat(bot.Seed*.173f+trip*.618034f,1);
            float targetY=Mathf.Lerp(feet.y,Mathf.Lerp(graph.minY,graph.maxY,fraction),p.verticalVariety);
            var ranked=destinationRanks;
            ranked.Clear();
            Rigidbody currentBody = null;
            Matrix4x4 matrix = default;
            bool hasSurface = false;
            foreach(var node in graph.nodes)
            {
                // AddBodyGeometry emits each body's samples together on the same anchor.
                if (!ReferenceEquals(node.body, currentBody))
                {
                    currentBody = node.body;
                    hasSurface = currentBody && node.surface;
                    if (hasSurface) matrix = node.surface.localToWorldMatrix;
                }
                if (!hasSurface) continue;
                var point = matrix.MultiplyPoint3x4(node.local);
                var offset = point - feet;
                float distance = offset.magnitude;
                if (distance < 3) continue;
                offset.y = 0;
                var candidate=node;
                candidate.score=-Mathf.Abs(point.y-targetY)*.45f+Vector3.Dot(offset.normalized,bearing)*2;
                float exploration=Mathf.Min(16,distance)*.025f*p.exploration;
                if(ranked.Count==24&&candidate.score+exploration<ranked[23].score-.0001f)continue;
                candidate.score-=bot.Population.ThreatCost(point)*(1+p.survivalPriority)+bot.RevisitCost(node.body);
                candidate.score+=exploration;
                if(ranked.Count==24&&candidate.score<=ranked[23].score)continue;
                int insert=ranked.Count;
                while(insert>0&&candidate.score>ranked[insert-1].score)insert--;
                ranked.Insert(insert,candidate);
                if(ranked.Count>24)ranked.RemoveAt(24);
            }
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
                if(!BotTraversalCache.Landing(bot.Motor,p)||Physics.Raycast(p+Vector3.up*.15f,Vector3.up,8,CastleBotNavigation.QueryMask,QueryTriggerInteraction.Ignore))continue;
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
                        if(Physics.Raycast(q+Vector3.up*.15f,Vector3.up,6,CastleBotNavigation.QueryMask,QueryTriggerInteraction.Ignore))continue;
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
                    if(!BotTraversalCache.Landing(bot.Motor,q)||Physics.Raycast(q+Vector3.up*.15f,Vector3.up,6,CastleBotNavigation.QueryMask,QueryTriggerInteraction.Ignore))continue;
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
            var toward=Vector3.ProjectOnPlane(destination-feet,Vector3.up).normalized;
            if(toward.sqrMagnitude>.01f&&feet.y-destination.y<8)
            {
                for(int bearing=0;bearing<5;bearing++)for(int length=0;length<4;length++)
                {
                    float angle=bearing==0?0:(bearing%2==0?1:-1)*((bearing+1)/2)*40;
                    var direction=Quaternion.Euler(0,angle,0)*toward;
                    var probe=feet+direction*(3+length*2);
                    if(!CastleBotNavigation.Ground(probe+Vector3.up*.4f,CastleBotNavigation.MaximumDrop(bot.Motor)+.5f,out var floor))continue;
                    if(!bot.Motor.HasWalkableIsland&&floor.collider.gameObject.layer!=Layers.Block&&
                        !Climbables.TryGet(floor.collider,out _))continue;
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
            float maximumDrop=CastleBotNavigation.MaximumDrop(bot.Motor,true);
            foreach(var node in graph.nodes)
            {
                var p=node.Point;float drop=feet.y-p.y;
                if(drop<.6f||drop>maximumDrop||p.y<destination.y-8||
                    Vector3.ProjectOnPlane(p-feet,Vector3.up).sqrMagnitude>20*20)continue;
                var candidate=node;candidate.score=Vector3.Distance(p,destination)*.7f+Vector3.Distance(feet,p)*.1f;
                lower.Add(candidate);
            }
            lower.Sort((a,b)=>a.score.CompareTo(b.score));
            var launchPoints=new List<Vector3>{feet};
            var feetCell=Cell(feet);
            for(int x=-3;x<=3;x++)for(int y=-1;y<=1;y++)for(int z=-3;z<=3;z++)
            {
                if(!graph.cells.TryGetValue(feetCell+new Vector3Int(x,y,z),out var nodes))continue;
                foreach(int index in nodes)
                {
                    var p=graph.nodes[index].Point;
                    if(Mathf.Abs(p.y-feet.y)>.25f||(p-feet).sqrMagnitude>64||(p-feet).sqrMagnitude<.06f)continue;
                    launchPoints.Add(p);
                }
            }
            int links=0;
            var inspected=new List<Vector3>(12);
            int candidatesChecked=0;
            foreach(var candidate in lower)
            {
                if(links>=48||inspected.Count>=24||candidatesChecked++>=384)break;
                var end=candidate.Point;
                if(inspected.Exists(point=>(point-end).sqrMagnitude<.8f)||
                    !BotTraversalCache.Landing(bot.Motor,end))continue;
                inspected.Add(end);
                float fall=feet.y-end.y;
                float range=Mathf.Min(14,bot.Motor.EffectiveWalkSpeed*(Mathf.Sqrt(2*fall/bot.Motor.gravity)+.48f));
                launchPoints.Sort((a,b)=>LaunchScore(a).CompareTo(LaunchScore(b)));
                float LaunchScore(Vector3 point)=>Vector3.ProjectOnPlane(point-end,Vector3.up).sqrMagnitude+(point-feet).sqrMagnitude*.28f;
                int forLanding=0;
                foreach(var start in launchPoints)
                {
                    if(Vector3.ProjectOnPlane(start-end,Vector3.up).sqrMagnitude>range*range)continue;
                    if(start!=feet&&(!BotTraversalCache.Landing(bot.Motor,start)||
                        !CastleBotNavigation.Walkable(bot.Motor,feet,start)))continue;
                    links++;
                    if(BotTraversalCache.TryLink(bot.Motor,start,end,out var route,out _))
                    {
                        stage=end;approach=start;traversal=route;
                        bot.RecoveryDiagnostics="departure: "+start+" -> "+end+" via "+route;
                        return true;
                    }
                    if(++forLanding>=6||links>=48)break;
                }
            }
            bot.RecoveryDiagnostics="departure: no route; lower="+lower.Count+" inspected="+inspected.Count+" links="+links;
            return false;
        }
        struct SearchNode {public float cost;public int parent;public CastleBotRoute route;}
        struct Candidate {public int id;public float priority,distanceSquared;}
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
            using var measurement=NeighborsMarker.Auto();
            neighbors.Clear();neighborCursor=0;var p=Point(id);var cell=Cell(p);
            float v=Mathf.Sqrt(2*bot.Motor.gravity*bot.Motor.jumpHeight);
            float range=Mathf.Clamp(bot.Motor.EffectiveWalkSpeed*v*2/bot.Motor.gravity+bot.Motor.vaultReach,2,7);
            int cells=Mathf.CeilToInt(range/3);
            float maximumDrop=CastleBotNavigation.MaximumDrop(bot.Motor);
            float maximumRise=Mathf.Max(.1f+bot.Motor.jumpHeight+bot.Motor.vaultMaxRise,bot.Motor.climbSpeed*bot.Motor.climbGripSeconds*.65f);
            for(int x=-cells;x<=cells;x++)for(int y=-Mathf.CeilToInt(maximumDrop/3);y<=2;y++)for(int z=-cells;z<=cells;z++)
            {
                if(!graph.cells.TryGetValue(cell+new Vector3Int(x,y,z),out var list))continue;
                foreach(int next in list)
                {
                    if(next==id||closed.Contains(next))continue;var point=graph.nodes[next].Point;var delta=point-p;
                    if(delta.y>maximumRise||delta.y< -maximumDrop||Vector3.ProjectOnPlane(delta,Vector3.up).sqrMagnitude>range*range||delta.sqrMagnitude<.08f)continue;
                    float priority=Heuristic(point)+delta.magnitude*.1f+bot.RevisitCost(graph.nodes[next].body)*.2f;
                    neighbors.Add(new Candidate{id=next,priority=priority,distanceSquared=delta.sqrMagnitude});
                }
            }
            neighbors.Sort((a,b)=>a.priority.CompareTo(b.priority));
            // Keep several nearby stepping stones, not just nodes nearer the destination.
            // Otherwise a required sideways/backwards step would be pruned at every corner.
            if(neighbors.Count>48)
            {
                var local=new List<Candidate>(neighbors);local.Sort((a,b)=>a.distanceSquared.CompareTo(b.distanceSquared));
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
