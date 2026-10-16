// Pure presentation layouts. Never mutate the graph or reverse relationship directions.
export const layouts = {
 'elk-flow': {label:'ELK · Dependency flow',description:'ELK places nodes left to right and routes connections around them. Recalculates when visible nodes or sizes change. Switch to another layout to drag nodes manually.'},
 'elk-tree': {label:'ELK · Hierarchy',description:'ELK places parents above children and routes connections through separate ports. Wide hierarchies keep readable labels: drag blank space to pan, or use the wheel to zoom. Switch to the indented hierarchy for a readable list.'},
  auto: { label: 'Recommended', description: 'Choose a containment tree for Unity, a dependency flow for code, and grouped columns for custom modules.' },
  tree: { label: 'Hierarchy tree', description: 'Readable indented hierarchy: children sit below their parent with a shared connector rail. Scroll to browse; Ctrl + wheel zooms. Locked nodes retain their positions.' },
  flow: { label: 'Dependency flow', description: 'Arrange callers on the left and dependencies on the right, ordering neighbors to reduce crossings and leaving wider routing corridors. Cycles stay together; this is not execution order.' },
  grouped: { label: 'Grouped columns', description: 'Keep nodes with the same parent or system together in separate columns.' },
  radial: { label: 'Radial overview', description: 'Spread systems around a circle to compare cross-system connections.' },
  grid: { label: 'Compact grid', description: 'Evenly spaced rows for scanning names. Does not express a dependency order.' }
};

export function resolveLayout(mode, view) {
  return mode === 'auto' ? ({structure:'tree',code:'flow',custom:'grouped'}[view] || 'grid') : mode;
}

export function arrange(nodes, edges, mode, previous = {}, styles = {}) {
  const ordered = [...nodes].sort((a,b) => a.label.localeCompare(b.label) || a.id.localeCompare(b.id));
  const ids = new Set(ordered.map(n=>n.id));
  const result = {};
  const place = (n,x,y) => { result[n.id] = {x,y}; };
  if (mode === 'radial') {
    const radius = Math.max(230, ordered.length * 46);
    ordered.forEach((n,i)=>{const angle=i*2*Math.PI/Math.max(1,ordered.length)-Math.PI/2;place(n, radius+80+Math.cos(angle)*radius, radius+170+Math.sin(angle)*radius);});
  } else if (mode === 'grouped') {
    const groups = new Map();
    for (const n of ordered) { const key=n.parent||n.group||'Project'; if(!groups.has(key))groups.set(key,[]);groups.get(key).push(n); }
    [...groups.values()].forEach((group,col)=>group.forEach((n,row)=>place(n,60+col*300,190+row*116)));
  } else if (mode === 'tree') {
    const children=new Map(),roots=[];
    for(const n of ordered) { if(n.parent && ids.has(n.parent) && n.parent!==n.id){if(!children.has(n.parent))children.set(n.parent,[]);children.get(n.parent).push(n);}else roots.push(n); }
    // Indented preorder keeps each family contiguous and names readable.
    const seen=new Set();let row=0;
    function placeTree(n,depth){if(seen.has(n.id))return;seen.add(n.id);place(n,60+depth*44,210+row++*116);for(const child of children.get(n.id)||[])placeTree(child,depth+1);}
    for(const n of [...roots,...ordered])if(!seen.has(n.id))placeTree(n,0);
  } else if (mode === 'flow') {
    // Collapse strongly connected components before ranking, so cycles cannot
    // cause infinite recursion or force arbitrary backwards dependencies.
    const adjacent=new Map(ordered.map(n=>[n.id,new Set()]));
    for(const e of edges)if(ids.has(e.source)&&ids.has(e.target)&&e.source!==e.target&&e.kind!=='contains')adjacent.get(e.source).add(e.target);
    let number=0;const indexes=new Map(),low=new Map(),stack=[],onStack=new Set(),components=[];
    function visit(id){indexes.set(id,number);low.set(id,number++);stack.push(id);onStack.add(id);for(const to of adjacent.get(id)){if(!indexes.has(to)){visit(to);low.set(id,Math.min(low.get(id),low.get(to)));}else if(onStack.has(to))low.set(id,Math.min(low.get(id),indexes.get(to)));}if(low.get(id)===indexes.get(id)){const group=[];let member;do{member=stack.pop();onStack.delete(member);group.push(member);}while(member!==id);components.push(group);}}
    for(const n of ordered)if(!indexes.has(n.id))visit(n.id);
    const owner=new Map();components.forEach((members,i)=>members.forEach(id=>owner.set(id,i)));
    const next=components.map(()=>new Set()),degree=components.map(()=>0),rank=components.map(()=>0);
    for(const [id,targets]of adjacent)for(const to of targets){const a=owner.get(id),b=owner.get(to);if(a!==b&&!next[a].has(b)){next[a].add(b);degree[b]++;}}
    const queue=degree.map((d,i)=>d===0?i:-1).filter(i=>i>=0);
    for(let q=0;q<queue.length;q++){const a=queue[q];for(const b of next[a]){rank[b]=Math.max(rank[b],rank[a]+1);if(--degree[b]===0)queue.push(b);}}
    const layers=new Map();for(const n of ordered){const col=rank[owner.get(n.id)];if(!layers.has(col))layers.set(col,[]);layers.get(col).push(n);}const neighbors=new Map(ordered.map(n=>[n.id,new Set()]));for(const [id,targets]of adjacent)for(const to of targets){neighbors.get(id).add(to);neighbors.get(to).add(id);}const slots=new Map();const refresh=()=>{for(const layer of layers.values())layer.forEach((n,i)=>slots.set(n.id,i));};refresh();
    // Alternating barycenter sweeps align related nodes across ranks.
    for(let pass=0;pass<8;pass++){const cols=[...layers.keys()].sort((a,b)=>pass%2?b-a:a-b);for(const col of cols){const score=n=>{const ns=[...neighbors.get(n.id)].filter(id=>rank[owner.get(id)]!==col);return ns.length?ns.reduce((sum,id)=>sum+slots.get(id),0)/ns.length:slots.get(n.id);};const scores=new Map(layers.get(col).map(n=>[n.id,score(n)]));layers.get(col).sort((a,b)=>scores.get(a.id)-scores.get(b.id)||a.id.localeCompare(b.id));refresh();}}
    for(const [col,layer]of layers)layer.forEach((n,row)=>place(n,60+col*440,210+row*150));
  } else {
    const columns=Math.max(1,Math.ceil(Math.sqrt(ordered.length*1.5)));
    ordered.forEach((n,i)=>place(n,60+(i%columns)*265,190+Math.floor(i/columns)*116));
  }
  // Locked positions win. Move automatic nodes away from occupied rectangles.
  const occupied=[];
  for(const n of ordered)if(styles[n.id]?.locked && Number.isFinite(previous[n.id]?.x) && Number.isFinite(previous[n.id]?.y)){result[n.id]={...previous[n.id]};occupied.push(result[n.id]);}
  for(const n of ordered){if(styles[n.id]?.locked&&previous[n.id])continue;const p=result[n.id];while(occupied.some(q=>Math.abs(q.x-p.x)<240&&Math.abs(q.y-p.y)<96))p.y+=116;occupied.push(p);}
  return result;
}

// Recompute face ports after every drag. Source and target ports share a face's allocation.
export function routeConnections(links, boxes, mode='curve') {
 const ports=new Map(), endpoints=[];
 const add=(id,side,edge,end)=>{const key=id+'|'+side;if(!ports.has(key))ports.set(key,[]);ports.get(key).push({edge,end,id,side});};
 links.forEach((l,i)=>{const a=boxes[l.a],b=boxes[l.b];const dx=b.x+b.width/2-a.x-a.width/2,dy=b.y+b.height/2-a.y-a.height/2;const horizontal=Math.abs(dx)/((a.width+b.width)/2)>=Math.abs(dy)/((a.height+b.height)/2);const side=horizontal?(dx>=0?'right':'left'):(dy>=0?'bottom':'top');const opposite={right:'left',left:'right',bottom:'top',top:'bottom'};endpoints[i]={};add(l.a,side,i,'start');add(l.b,opposite[side],i,'end');});
 for(const group of ports.values()){group.sort((a,b)=>{const la=links[a.edge],lb=links[b.edge];const oa=boxes[a.end==='start'?la.b:la.a],ob=boxes[b.end==='start'?lb.b:lb.a];const delta=a.side==='left'||a.side==='right'?(oa.y+oa.height/2)-(ob.y+ob.height/2):(oa.x+oa.width/2)-(ob.x+ob.width/2);if(Math.abs(delta)>1)return delta;return (la.a+'|'+la.b+'|'+la.e.kind+'|'+a.end).localeCompare(lb.a+'|'+lb.b+'|'+lb.e.kind+'|'+b.end);});group.forEach((p,i)=>{const b=boxes[p.id],vertical=p.side==='left'||p.side==='right',length=vertical?b.height:b.width;const offset=8+(length-16)*(i+1)/(group.length+1);const radius=b.shape==='pill'?b.height/2:b.shape==='square'?2:12;const corner=Math.max(0,radius-Math.min(offset,length-offset));const inset=corner?radius-Math.sqrt(Math.max(0,radius*radius-corner*corner)):0;const normal={left:[-1,0],right:[1,0],top:[0,-1],bottom:[0,1]}[p.side];endpoints[p.edge][p.end]={x:b.x+(vertical?(p.side==='left'?inset:b.width-inset):offset),y:b.y+(vertical?offset:(p.side==='top'?inset:b.height-inset)),normal,side:p.side};});}
 return endpoints.map(({start:a,end:b},edgeIndex)=>{const bend=Math.min(110,Math.max(32,Math.hypot(b.x-a.x,b.y-a.y)*.35));const c={x:a.x+a.normal[0]*bend,y:a.y+a.normal[1]*bend},d={x:b.x+b.normal[0]*bend,y:b.y+b.normal[1]*bend};let path=`M${a.x},${a.y}`;if(mode==='straight')path+=` L${b.x},${b.y}`;else if(mode==='orthogonal'){const points=clearCorridor(c,d,boxes,[links[edgeIndex].a,links[edgeIndex].b],edgeIndex);path+=' L'+points.map(p=>p.x+','+p.y).join(' L')+' L'+b.x+','+b.y;}else path+=` C${c.x},${c.y} ${d.x},${d.y} ${b.x},${b.y}`;return {path,start:a,end:b};});
}
// Choose a clear orthogonal corridor when a route would pass through another node.
function clearCorridor(start,end,boxes,excluded,lane){
 const obstacles=Object.entries(boxes).filter(([id])=>!excluded.includes(id)).map(([,b])=>b);
 const hit=(a,b,r)=>Math.abs(a.x-b.x)<.01?a.x>r.x-8&&a.x<r.x+r.width+8&&Math.max(a.y,b.y)>r.y-8&&Math.min(a.y,b.y)<r.y+r.height+8:Math.abs(a.y-b.y)<.01?a.y>r.y-8&&a.y<r.y+r.height+8&&Math.max(a.x,b.x)>r.x-8&&Math.min(a.x,b.x)<r.x+r.width+8:false;
 const distance=r=>Math.abs(r.x-(start.x+end.x)/2)+Math.abs(r.y-(start.y+end.y)/2);const nearby=[...obstacles].sort((a,b)=>distance(a)-distance(b)).slice(0,16);
 const shift=(lane%11)*5, xs=[(start.x+end.x)/2+shift,...nearby.flatMap(r=>[r.x-24-shift,r.x+r.width+24+shift])],ys=[(start.y+end.y)/2+shift,...nearby.flatMap(r=>[r.y-24-shift,r.y+r.height+24+shift])];
 const candidates=[...xs.map(x=>[start,{x,y:start.y},{x,y:end.y},end]),...ys.map(y=>[start,{x:start.x,y},{x:end.x,y},end])];
 let best=null,bestScore=Infinity;for(const points of candidates){let score=0;for(let i=1;i<points.length;i++){score+=Math.hypot(points[i].x-points[i-1].x,points[i].y-points[i-1].y);for(const r of obstacles)if(hit(points[i-1],points[i],r))score+=100000;}if(score<bestScore){bestScore=score;best=points;}}return best;
}
