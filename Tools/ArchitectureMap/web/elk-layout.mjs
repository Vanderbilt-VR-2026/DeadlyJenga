// Local ELK 0.12.0 adapter. The renderer consumes these paths without rerouting.
export const edgeKey = e => JSON.stringify([e.a,e.b,e.e.kind,e.e.diffBranch??null,e.e.change??null]);
export function layoutSignature(nodes, links, mode, labels) {
  return JSON.stringify([mode,labels,nodes.map(n=>[n.id,n.width,n.height]).sort(),links.map(e=>[edgeKey(e),e.count]).sort()]);
}
export function makeElkGraph(nodes, links, mode, labels=false) {
  const children=nodes.map(n=>({id:n.id,width:n.width,height:n.height,ports:[],layoutOptions:{'elk.portConstraints':'FREE'}}));
  const by=new Map(children.map(n=>[n.id,n]));
  const edges=links.map((e,i)=>{const source='port-source-'+i,target='port-target-'+i;
    by.get(e.a).ports.push({id:source,width:0,height:0});by.get(e.b).ports.push({id:target,width:0,height:0});
    const text=(e.e.change?'B'+(e.e.diffBranch+1)+' '+(e.e.change==='added'?'+':'−')+' ':'')+e.e.kind+(e.count>1?' ×'+e.count:'');
    return {id:'edge-'+i,sources:[source],targets:[target],labels:labels?[{text,width:text.length*6.2,height:15}]:[]};
  });
  return {id:'elk-root',layoutOptions:{'elk.algorithm':'layered','elk.direction':mode==='elk-tree'?'DOWN':'RIGHT','elk.edgeRouting':'ORTHOGONAL','elk.spacing.nodeNode':'40','elk.layered.spacing.nodeNodeBetweenLayers':'90','elk.spacing.edgeNode':'18','elk.spacing.edgeEdge':'12','elk.layered.spacing.edgeEdgeBetweenLayers':'14','elk.padding':'[top=24,left=24,bottom=24,right=24]','elk.randomSeed':'1','elk.layered.considerModelOrder.strategy':'NODES_AND_EDGES','elk.layered.mergeEdges':'false'},children,edges};
}
export function readElkResult(result,links) {
  const positions={},routes={};const point=p=>({x:p.x+40,y:p.y+200});
  for(const n of result.children||[]){if(!Number.isFinite(n.x)||!Number.isFinite(n.y))throw Error('ELK returned an invalid node position.');positions[n.id]=point(n);}
  for(const e of result.edges||[]){const i=Number(e.id.slice(5));if(!links[i])throw Error('ELK returned an unknown edge.');
    const sections=e.sections||[];if(!sections.length)throw Error('ELK did not route a connection.');
    const paths=sections.map(s=>[s.startPoint,...s.bendPoints||[],s.endPoint].map(point));
    if(paths.flat().some(p=>!Number.isFinite(p.x)||!Number.isFinite(p.y)))throw Error('ELK returned invalid edge coordinates.');
    const label=e.labels?.[0];routes[edgeKey(links[i])]={path:paths.map(ps=>'M'+ps.map(p=>p.x+','+p.y).join(' L')).join(' '),start:paths[0][0],end:paths.at(-1).at(-1),label:label&&Number.isFinite(label.x)?point({x:label.x,y:label.y+12}):null};
  }
  return {positions,routes};
}
let active=null;
export function cancelElk(){if(active){active.engine.terminateWorker();active.reject(new Error('Layout cancelled.'));active=null;}}
export async function runElk(nodes,links,mode,labels=false) {
  cancelElk();if(!globalThis.ELK)throw Error('Local ELK library did not load. Restart the launcher and refresh.');
  const engine=new globalThis.ELK({workerFactory:()=>new Worker(new URL('./elk-worker.min.js',import.meta.url))});
  try{return await new Promise((resolve,reject)=>{const timer=setTimeout(()=>{engine.terminateWorker();reject(Error('ELK exceeded 30 seconds. Reduce visible depth or narrow the view.'));},30000);
    active={engine,reject:error=>{clearTimeout(timer);reject(error);}};
    engine.layout(makeElkGraph(nodes,links,mode,labels)).then(result=>{clearTimeout(timer);try{resolve(readElkResult(result,links));}catch(error){reject(error);}},error=>{clearTimeout(timer);reject(error);});
  });}finally{engine.terminateWorker();if(active?.engine===engine)active=null;}
}
