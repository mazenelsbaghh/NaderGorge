const storageKey='massar-client-diagnostics:v1';
const eventNames=['startup_ready','connection_lost','connection_restored','request_failed','js_error','promise_rejection'];
const routes=['join','answers','session','presence','lounge'];
const queueLimit=100, batchLimit=10, sendInterval=15000, retryInterval=60000;

function storedEvent(candidate) {
  if(!candidate||eventNames.indexOf(candidate.event)<0||typeof candidate.at!=='string')return null;
  const timestamp=Date.parse(candidate.at);
  if(!Number.isFinite(timestamp)||timestamp<Date.UTC(2000,0,1)||timestamp>=Date.UTC(2101,0,1))return null;
  const event={event:candidate.event,at:new Date(timestamp).toISOString()};
  if(candidate.event==='request_failed') {
    if(routes.indexOf(candidate.route)<0||!Number.isInteger(candidate.status)||candidate.status<0||candidate.status>599||!Number.isInteger(candidate.durationMs)||candidate.durationMs<0||candidate.durationMs>60000)return null;
    event.route=candidate.route;event.status=candidate.status;event.durationMs=candidate.durationMs;
  }
  return event;
}
function readQueue() {
  try {
    const saved=localStorage.getItem(storageKey);
    if(!saved||saved.length>32768)return [];
    const parsed=JSON.parse(saved);
    return Array.isArray(parsed)?parsed.slice(-queueLimit).map(storedEvent).filter(Boolean):[];
  } catch {return []; /* Blocked or damaged diagnostic storage must never prevent an exam. */}
}

export function createStudentDiagnostics(sendRequest) {
  let queue=readQueue(), connected=null, sending=false, sendTimer, persistTimer, nextAttempt=0;
  function persist() {
    clearTimeout(persistTimer);persistTimer=null;
    try {localStorage.setItem(storageKey,JSON.stringify(queue));}
    catch { /* Keep the bounded in-memory queue when browser storage is unavailable. */ }
  }
  function schedule() {
    if(!connected||sending||sendTimer||!queue.length)return;
    sendTimer=setTimeout(flush,Math.max(sendInterval,nextAttempt-performance.now())+Math.floor(Math.random()*5000));
  }
  function record(event,fields={}) {
    const timestamp=new Date();
    if(!Number.isFinite(timestamp.getTime())||timestamp.getUTCFullYear()<2000||timestamp.getUTCFullYear()>2100)return;
    const previous=queue[queue.length-1];
    const elapsed=previous?Date.now()-Date.parse(previous.at):1000;
    if(previous&&previous.event===event&&previous.route===fields.route&&previous.status===fields.status&&elapsed>=0&&elapsed<1000)return;
    queue.push({event,at:timestamp.toISOString(),...fields});
    if(queue.length>queueLimit)queue.shift();
    if(!persistTimer)persistTimer=setTimeout(persist,200);
    schedule();
  }
  function connectivity(reachable) {
    if(connected!==reachable&&(!reachable||connected===false))record(reachable?'connection_restored':'connection_lost');
    connected=reachable;
    if(!connected){clearTimeout(sendTimer);sendTimer=null;}
    else schedule();
  }
  function flush() {
    sendTimer=null;
    if(!connected||sending||!queue.length)return;
    const batch=queue.slice(0,batchLimit);
    sending=true;nextAttempt=performance.now()+sendInterval;
    const finish=accepted=>{
      sending=false;
      if(accepted){queue=queue.filter(event=>batch.indexOf(event)<0);persist();}
      else nextAttempt=performance.now()+retryInterval;
      schedule();
    };
    // Reporting has its own transport so a failed report cannot recursively log itself.
    try {
      const request=new XMLHttpRequest();
      request.open('POST','/api/client-diagnostics',true);request.timeout=5000;
      request.setRequestHeader('Content-Type','application/json');request.setRequestHeader('X-Exam-Request','1');
      request.onload=()=>{
        let accepted=false;
        if(request.status===200)try{accepted=JSON.parse(request.responseText).accepted===batch.length;}catch{/* Retry an incomplete receipt later. */}
        finish(accepted);
      };
      request.onerror=request.ontimeout=request.onabort=()=>finish(false);
      request.send(JSON.stringify({events:batch}));
    } catch {finish(false); /* A browser transport restriction cannot interrupt join or save. */}
  }
  async function request(path,payload) {
    const started=performance.now();
    try {return await sendRequest(path,payload);}
    catch(error) {
      const route=path.split('?')[0].slice('/api/'.length);
      if(routes.indexOf(route)>=0)record('request_failed',{route,status:Number.isInteger(error.status)&&error.status>=0&&error.status<=599?error.status:0,durationMs:Math.min(60000,Math.max(0,Math.round(performance.now()-started)))});
      throw error;
    }
  }
  window.addEventListener('error',()=>record('js_error'));
  window.addEventListener('unhandledrejection',()=>record('promise_rejection'));
  window.addEventListener('pagehide',persist);
  return {request,connectivity,ready:()=>record('startup_ready')};
}
