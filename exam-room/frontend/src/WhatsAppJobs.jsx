import React,{useEffect,useState} from 'react';
import {api,number,toast} from '../../web/assets/common.js';
const states={queued:'في دور الإرسال',running:'جارٍ الإرسال',completed:'اكتمل الإرسال',partial:'توقف ويحتاج متابعة',stopped:'تم الإيقاف',interrupted:'توقف عند إغلاق البرنامج'};
export default function WhatsAppJobs(){
 const [jobs,setJobs]=useState([]),[token,setToken]=useState(''),[busy,setBusy]=useState('');
 const load=async()=>{const result=await api('/api/whatsapp/jobs');setJobs(result.items);};
 useEffect(()=>{
  let live=true,inFlight=false;
  const refresh=async()=>{if(inFlight)return;inFlight=true;try{const result=await api('/api/whatsapp/jobs');if(live)setJobs(result.items);}catch{}finally{inFlight=false;}};
  api('/api/bootstrap').then(result=>{if(live)setToken(result.token);}).catch(()=>{});
  refresh();const timer=setInterval(refresh,3000);
  window.addEventListener('whatsapp-jobs-changed',refresh);
  return()=>{live=false;clearInterval(timer);window.removeEventListener('whatsapp-jobs-changed',refresh);};
 },[]);
 const resume=async id=>{setBusy(id);try{await api('/api/whatsapp/jobs/resume',{jobId:id},token);await load();}catch(e){toast(e.message,true);}finally{setBusy('');}};
 const stop=async id=>{setBusy(id);try{await api('/api/whatsapp/jobs/stop',{jobId:id},token);await load();}catch(e){toast(e.message,true);}finally{setBusy('');}};
 if(!jobs.length)return null;
 const active=jobs.filter(j=>['queued','running'].includes(j.state));
 const shown=[...active,...jobs.filter(j=>!active.includes(j)).slice(0,2)];
 return <section className="panel" aria-label="إرسال واتساب في الخلفية"><div className="section-top"><strong>إرسال واتساب في الخلفية · {number(active.length)} عملية نشطة</strong><span className="help">يمكنك متابعة العمل في أي امتحان</span></div>{shown.map(job=><div className="notice" key={job.id}><div className="section-top"><strong>{job.title} · {job.stopping&&job.state==='running'?'يتوقف بعد الدفعة الحالية':states[job.state]}</strong>{['queued','running'].includes(job.state)&&<button className="text-button" disabled={!token||busy===job.id||job.stopping} onClick={()=>stop(job.id)}>{job.state==='queued'?'إلغاء من الدور':'إيقاف بعد الدفعة الحالية'}</button>}{['partial','stopped','interrupted'].includes(job.state)&&<button className="text-button" disabled={!token||busy===job.id} onClick={()=>resume(job.id)}>استكمال الباقي بدون تكرار المقبول</button>}</div><p>تمت معالجة {number(job.processed)} من {number(job.total)} · قبل واتساب {number(job.accepted)} · لم يتأكد إرسال {number(job.failed)}</p>{job.message&&<p className="help">{job.message}</p>}</div>)}<p className="help">٥ تقارير معًا، ثم ٥ ثوانٍ بين الدفعات. لو انقطع الإنترنت ينتظر ويستكمل تلقائيًا. الرسائل المقبولة لا تُرسل مرة أخرى، والرسالة غير المؤكدة تحتاج متابعة. أبقِ البرنامج مفتوحًا. قبول الطلب يختلف عن وصول الرسالة.</p></section>;
}
