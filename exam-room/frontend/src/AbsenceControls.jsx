import React,{useEffect,useState} from 'react';
import {toast,number} from '../../web/assets/common.js';
export default function AbsenceControls({exam,request,refresh}) {
 const [seconds,setSeconds]=useState(exam.config.absenceSeconds||0);const [busy,setBusy]=useState(false);
 useEffect(()=>setSeconds(exam.config.absenceSeconds||0),[exam.config.absenceSeconds]);
 const save=async()=>{setBusy(true);try{await request(`/api/exams/${exam.id}/absence-rule`,{seconds});await refresh();toast('تم حفظ إعداد الغياب');}catch(e){toast(e.message,true);}finally{setBusy(false);}};
 return <section className="panel padded"><div className="section-top"><div><h2>الخروج من صفحة الامتحان</h2><p className="help">حدد مدة الغياب بنفسك. صفر يعني أن الإيقاف يدوي فقط.</p></div><span className="badge quiet">{exam.config.absenceSeconds?`إيقاف بعد ${number(exam.config.absenceSeconds)} ثانية`:'إيقاف يدوي'}</span></div><div className="actions"><label>مدة الغياب بالثواني<input type="number" min="0" max="3600" step="1" value={seconds} onChange={e=>setSeconds(Number(e.target.value))}/></label><button className="button secondary" disabled={busy||!Number.isInteger(seconds)||seconds<0||seconds>3600||seconds>0&&seconds<5} onClick={save}>حفظ مدة الغياب</button></div><p className="help">الإيقاف يمنع الحل والتسليم، والتايمر يظل شغالًا. تغيير التطبيق أو انقطاع الاتصال قد يُحسب غيابًا. الاستكمال بقرارك من صف الطالب.</p></section>;
}
