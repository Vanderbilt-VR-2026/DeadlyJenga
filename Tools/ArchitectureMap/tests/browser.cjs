// Optional development dependency: npm install --no-save playwright
// Run against an already running local dashboard. Layout/policy restored in finally.
const {chromium}=require('playwright');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const base=process.env.ARCHITECTURE_URL||'http://127.0.0.1:47831';
async function main(){
 const original=await (await fetch(base+'/api/state')).json();
 const fixtureGuid=crypto.randomUUID().replaceAll('-','');
 const fixture=path.resolve(__dirname,'../../../Assets/ArchitectureMap/architecture-smoke-'+fixtureGuid+'.asset');
 const browser=await chromium.launch({headless:true, ...(process.env.ARCHITECTURE_BROWSER ? {channel:process.env.ARCHITECTURE_BROWSER} : {})});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const post=(path,value)=>fetch(base+path,{method:'POST',headers:{'Content-Type':'application/json','X-Architecture-Client':'local'},body:JSON.stringify(value)});
 let checks=0;const ok=(value,label)=>{assert.ok(value,label);console.log('PASS '+label);checks++;};
 try{
  await page.goto(base);await page.waitForSelector('.node');
  ok(await page.locator('.node').count()>0,'Dashboard loads real graph');
  await page.getByRole('button',{name:'File structure',exact:true}).click();ok(await page.locator('#heading').textContent()==='File structure','View switching');
  await page.getByRole('button',{name:'Code relationships',exact:true}).click();
  await page.locator('.node').first().dblclick();ok((await page.locator('#crumb').textContent())!=='Project','Hierarchy drill down');
  await page.locator('#search').fill('CastleLanTransport');await page.waitForTimeout(200);ok(await page.locator('.node').count()>0,'Search finds repository symbols');
  await page.locator('.node').first().click();ok(await page.locator('#inspector').isVisible(),'Selection opens inspector');
  await page.getByRole('button',{name:'Watch',exact:true}).click();await page.waitForTimeout(500);let state=await(await fetch(base+'/api/state')).json();ok(Object.values(state.intent.scopes).includes('watch'),'Scope changes persisted');
  await page.getByLabel('Notes',{exact:true}).fill('<script>window.INJECTED=true</script>');await page.getByLabel('Notes',{exact:true}).blur();await page.waitForTimeout(300);ok(!(await page.evaluate(()=>window.INJECTED)),'Notes displayed as text');
  const before=await page.locator('.node').first().getAttribute('transform');let box=await page.locator('.node').first().boundingBox();await page.mouse.move(box.x+50,box.y+30);await page.mouse.down();await page.mouse.move(box.x+150,box.y+110,{steps:8});await page.mouse.up();ok(await page.locator('.node').first().getAttribute('transform')!==before,'Dragging changes layout');
  await page.locator('#undo').click();ok(await page.locator('.node').first().getAttribute('transform')===before,'Visual undo restores position');
  ok(await page.getByRole('button',{name:'Custom architecture',exact:true}).count()===0,'Editing remains in the primary views');
  await page.locator('#settings').click();await page.getByLabel('Theme',{exact:true}).selectOption('light');ok(await page.locator('body').evaluate(e=>e.classList.contains('light')),'Light theme');
  await page.getByLabel('Connections',{exact:true}).selectOption('orthogonal');await page.locator('#close').click();ok(await page.locator('#inspector').isHidden(),'Inspector collapses');
  await page.locator('#search').fill('');await page.keyboard.press('Escape');await page.keyboard.press('/');ok(await page.locator('#search').evaluate(e=>e===document.activeElement),'Keyboard search shortcut');
  await page.locator('#git').click();await page.getByLabel('Comparison baseline',{exact:true}).fill('HEAD');await page.getByLabel('Comparison baseline',{exact:true}).blur();await page.getByLabel('Overlay branch',{exact:true}).selectOption(original.git.branch);await page.waitForFunction(()=>document.getElementById('panel').textContent.includes('changed paths'));ok((await page.locator('#panel').textContent()).includes('changed paths'),'Branch comparison control');
  await page.setViewportSize({width:390,height:844});await page.locator('#settings').click();ok(await page.locator('#inspector').isVisible(),'Narrow-screen inspector');
  await page.setViewportSize({width:1440,height:1000});await page.locator('#close').click();await page.locator('#back').click();await page.locator('#fit').click();
  const out=process.env.ARCHITECTURE_SCREENSHOT;if(out)await page.screenshot({path:out,fullPage:true});
  ok((await fetch(base+'/api/intent',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'})).status===403,'Missing mutation header blocked');
  ok((await fetch(base+'/api/state',{headers:{Origin:'https://example.invalid'}})).status===403,'Cross-origin reads blocked');
  ok((await fetch(base+'/api/source?path=Assets/../../secret')).status===400,'Source traversal blocked');
  ok((await post('/api/presentation/../../intent',{})).status!==200,'Arbitrary write path rejected');
  ok((await post('/api/intent',{scopes:{x:'delete'},rules:[]})).status===400,'Invalid scope rejected');
  async function until(predicate){const deadline=Date.now()+45000;while(Date.now()<deadline){const data=await(await fetch(base+'/api/state')).json();if(predicate(data))return;await new Promise(r=>setTimeout(r,1000));}throw Error('Live update timed out');}
  fs.writeFileSync(fixture+'.meta','fileFormatVersion: 2\nguid: '+fixtureGuid+'\n');fs.writeFileSync(fixture,'%YAML 1.1\n--- !u!1 &1\nGameObject:\n  m_Name: Live watcher fixture\n');
  await until(s=>s.graph.nodes.some(n=>n.id==='asset:'+fixtureGuid));ok(true,'Filesystem watcher discovers a new Unity asset automatically');
  fs.rmSync(fixture);fs.rmSync(fixture+'.meta');await until(s=>!s.graph.nodes.some(n=>n.id==='asset:'+fixtureGuid));ok(true,'Filesystem watcher removes deleted asset without stale nodes');
  const after=await(await fetch(base+'/api/state')).json();ok(JSON.stringify(after.graph.nodes)===JSON.stringify(original.graph.nodes)&&JSON.stringify(after.graph.edges)===JSON.stringify(original.graph.edges),'Visual and policy edits preserve actual architecture');
  ok(errors.length===0,'No browser JavaScript errors: '+errors.join('; '));
  console.log(checks+' browser/security checks passed');
 }finally{fs.rmSync(fixture,{force:true});fs.rmSync(fixture+'.meta',{force:true});await post('/api/intent',original.intent);await post('/api/presentation/personal',original.personal||{});await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
