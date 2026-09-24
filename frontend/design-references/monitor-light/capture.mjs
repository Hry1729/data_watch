import fs from 'node:fs/promises'
const targets = await fetch('http://127.0.0.1:9227/json').then(r=>r.json())
const ws = new WebSocket(targets.find(t=>t.type==='page').webSocketDebuggerUrl)
await new Promise(r=>ws.addEventListener('open',r,{once:true}))
let id=0;const pending=new Map();const errors=[]
ws.addEventListener('message',e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);m.error?p?.reject(m.error):p?.resolve(m.result)}else if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails)})
function call(method,params={}){return new Promise((resolve,reject)=>{pending.set(++id,{resolve,reject});ws.send(JSON.stringify({id,method,params}))})}
const evaluate=async expression=>(await call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true})).result?.value
await call('Runtime.enable')
await call('Page.enable')
await call('Emulation.setDeviceMetricsOverride',{width:1672,height:941,deviceScaleFactor:1,mobile:false})
await call('Page.navigate',{url:'http://127.0.0.1:5173/design-references/monitor-light/preview.html'})
for(let n=0;n<50;n++){if(await evaluate("document.querySelector('.module-state') && document.querySelector('.kpi strong')?.textContent==='120'"))break;await new Promise(r=>setTimeout(r,300))}
await new Promise(r=>setTimeout(r,800))
const screenshot=await call('Page.captureScreenshot',{format:'png'})
await fs.writeFile('frontend/design-references/monitor-light/desktop.png',Buffer.from(screenshot.data,'base64'))
console.log(JSON.stringify({errors,body:await evaluate("document.body.innerText.slice(0,3500)"),metrics:await evaluate("({width:innerWidth,height:innerHeight,scrollWidth:document.documentElement.scrollWidth,scrollHeight:document.documentElement.scrollHeight,calls:window.qaCalls,errors:window.qaErrors})")}))
ws.close()
