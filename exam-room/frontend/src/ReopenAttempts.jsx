import React,{useState} from 'react';
import {confirmAction,number,toast} from '../../web/assets/common.js';

export default function ReopenAttempts({examId,attempts,request,refresh,clear}) {
  const [minutes,setMinutes]=useState(10);
  const [busy,setBusy]=useState(false);
  const reopen=async()=>{
    if(busy||!attempts.length||!Number.isInteger(minutes)||minutes<1||minutes>120)return;
    if(!await confirmAction(`إعادة فتح ${number(attempts.length)} محاولة؟`,
      `سيبدأ وقت جديد مدته ${number(minutes)} دقيقة الآن للطلاب المحددين. تبقى الإجابات الواصلة، وتُعاد مراجعة التصحيح عند التسليم الجديد.`))return;
    setBusy(true);
    try {
      await request(`/api/exams/${examId}/reopen-attempts`,{minutes,attemptIds:attempts.map(a=>a.id)});
      clear();await refresh();toast('أُعيد فتح المحاولات المحددة. على الطلاب استخدام نفس الموبايل والمتصفح.');
    } catch(error) {toast(error.message,true);} finally {setBusy(false);}
  };
  return <section className="selected-time"><div><strong>إعادة فتح المحاولات المسلّمة</strong>
    <p className="help">حدد من عمود «إعادة فتح». المحدد الآن: {number(attempts.length)}. الوقت يبدأ عند التأكيد والإجابات الواصلة محفوظة.</p></div>
    <div className="actions"><label>الوقت الجديد بالدقائق <input type="number" min="1" max="120" step="1" value={minutes} onChange={event=>setMinutes(Number(event.target.value))}/></label>
      <button className="button small" disabled={busy||!attempts.length||!Number.isInteger(minutes)||minutes<1||minutes>120} onClick={reopen}>إعادة فتح المحددين</button>
      {attempts.length>0&&<button className="text-button" disabled={busy} onClick={clear}>إلغاء التحديد</button>}</div>
  </section>;
}
