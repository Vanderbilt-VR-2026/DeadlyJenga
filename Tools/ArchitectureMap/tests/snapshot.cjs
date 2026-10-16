const {chromium}=require('playwright');
const http=require('node:http');
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const web=path.resolve(__dirname,'../web');
const snapshot=path.resolve(__dirname,'../../../.architecture/local/export/snapshot.json');
async function main(){
 const server=http.createServer((req,res)=>{
  const name=req.url==='/'?'index.html':req.url.slice(1);
  if(!['index.html','app.js','layout.mjs','elk-layout.mjs','elk-api.js','elk-worker.min.js','style.css','snapshot.json','status-live.mp4','status-error.png','status-stale.png'].includes(name)){res.writeHead(404);res.end();return;}
  res.setHeader('Content-Type',name.endsWith('.js')||name.endsWith('.mjs')?'text/javascript':name.endsWith('.css')?'text/css':name.endsWith('.json')?'application/json':'text/html');
  fs.createReadStream(name==='snapshot.json'?snapshot:path.join(web,name)).pipe(res);
 });
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 let browser;
 try{
  browser=await chromium.launch({headless:true,...(process.env.ARCHITECTURE_BROWSER?{channel:process.env.ARCHITECTURE_BROWSER}:{})});
  const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:'+server.address().port);await page.waitForSelector('.node');
  assert.match(await page.locator('#status').textContent(),/Snapshot/);console.log('PASS Shared renderer loads real exported snapshot');
  await page.locator('#git').click();assert.match(await page.locator('#panel').textContent(),/Snapshot: Git status is from export time\. Live comparisons require the local dashboard\./);console.log('PASS Snapshot labels unavailable live Git controls');
  await page.locator('#settings').click();await page.getByLabel('Theme',{exact:true}).selectOption('light');await page.locator('#saveView').click();
  assert.equal(await page.evaluate(()=>JSON.parse(localStorage.getItem('architecture-presentation')).theme),'light');console.log('PASS Snapshot visual overrides persist locally');
  assert.equal(errors.length,0,errors.join(';'));console.log('PASS Snapshot has no JavaScript errors');
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
}
main().catch(e=>{console.error(e);process.exitCode=1;});

