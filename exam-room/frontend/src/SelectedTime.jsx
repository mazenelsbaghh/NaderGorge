import React,{useState} from 'react';
import {number,toast} from '../../web/assets/common.js';

export default function SelectedTime({examId,attempts,request,refresh,clear}) {
 const [minutes,setMinutes]=useState(5);
 const [busy,setBusy]=useState(false);
 const extend=async duration=>{
  if(busy||!attempts.length)return;
  setBusy(true);
  try {
   await request(`/api/exams/${examId}/extend-time`,{minutes:duration,attemptIds:attempts.map(a=>a.id)});
   clear();await refresh();toast(`تمت إضافة ${number(duration)} دقيقة لـ${number(attempts.length)} طالب فقط`);
  } catch(e) {toast(e.message,true);} finally {setBusy(false);}
 };
 return <section className="selected-time"><div><strong>تمديد الوقت لطلاب معينين</strong><p className="help">حدد الطلاب من المربعات في الجدول. المحدد الآن: {number(attempts.length)}. وقت باقي الطلاب لا يتغير.</p></div>
  <div className="actions"><button className="button secondary small" disabled={busy||!attempts.length} onClick={()=>extend(2)}>＋ دقيقتين للمحددين</button><button className="button secondary small" disabled={busy||!attempts.length} onClick={()=>extend(5)}>＋ ٥ دقائق للمحددين</button><label>دقائق للمحددين<input type="number" min="1" max="120" step="1" value={minutes} onChange={e=>setMinutes(Number(e.target.value))}/></label><button className="button small" disabled={busy||!attempts.length||!Number.isInteger(minutes)||minutes<1||minutes>120} onClick={()=>extend(minutes)}>تمديد للمحددين</button>{attempts.length>0&&<button className="text-button" disabled={busy} onClick={clear}>إلغاء التحديد</button>}</div>
 </section>;
}
