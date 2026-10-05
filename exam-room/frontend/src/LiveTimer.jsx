import React,{useEffect,useState} from 'react';
import {number,toast} from '../../web/assets/common.js';
export default function LiveTimer({dashboard,request,refresh}) {
 const [tick,setTick]=useState(Date.now());const [extra,setExtra]=useState(5);const [busy,setBusy]=useState(false);
 useEffect(()=>{const timer=setInterval(()=>setTick(Date.now()),1000);return()=>clearInterval(timer);},[]);
 const exam=dashboard.exam;const [snapshot,setSnapshot]=useState({time:dashboard.serverTime,local:Date.now()});
 useEffect(()=>setSnapshot({time:dashboard.serverTime,local:Date.now()}),[dashboard.serverTime]);
 const serverNow=snapshot.time+(tick-snapshot.local)/1000;
 const deadlines=dashboard.attempts.filter(a=>a.joined_at&&!a.cancelled_at&&!a.submitted_at&&a.deadline>serverNow).map(a=>a.deadline);
 const deadline=Math.max(exam.config.timerMode==='shared'?exam.started_at+exam.config.minutes*60+(exam.config.extraSeconds||0):0,...deadlines);
 const seconds=Math.max(0,Math.ceil(deadline-serverNow));
 const extend=async minutes=>{if(busy)return;setBusy(true);try{await request(`/api/exams/${exam.id}/extend-time`,{minutes});await refresh();toast(`تمت إضافة ${number(minutes)} دقيقة للوقت`);}catch(e){toast(e.message,true);}finally{setBusy(false);}};
 return <section className="panel live-timer"><div><span>{exam.config.timerMode==='shared'?'أطول وقت متبقٍ (يشمل تمديد الطلاب)':'أطول وقت متبقٍ لطالب'}</span><strong dir="ltr">{String(Math.floor(seconds/60)).padStart(2,'0')}:{String(seconds%60).padStart(2,'0')}</strong></div><div className="actions"><button className="button secondary" disabled={busy} onClick={()=>extend(2)}>＋ دقيقتين للجميع</button><button className="button secondary" disabled={busy} onClick={()=>extend(5)}>＋ ٥ دقائق للجميع</button><label>دقائق إضافية<input type="number" min="1" max="120" step="1" value={extra} onChange={e=>setExtra(Number(e.target.value))}/></label><button className="button" disabled={busy||!Number.isInteger(extra)||extra<1||extra>120} onClick={()=>extend(extra)}>إضافة الوقت للجميع</button></div><p className="help">الزيادة تشمل الطلاب الذين ما زالوا يحلون والدخول المتأخر. لا تفتح محاولة سبق تسليمها.</p></section>;
}
