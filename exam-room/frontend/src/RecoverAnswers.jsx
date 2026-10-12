import React, {useState} from 'react';
import {confirmAction, toast} from '../../web/assets/common.js';

export default function RecoverAnswers({attempt,exam,request,refresh}) {
  const [candidate,setCandidate]=useState(null);
  const [reason,setReason]=useState('');
  const [busy,setBusy]=useState(false);
  if(!attempt.submitted_at||attempt.cancelled_at)return null;
  const read=async event=>{
    const file=event.target.files[0];setCandidate(null);
    if(!file)return;
    try {
      if(file.size>1500000)throw new Error('ملف الاسترجاع أكبر من الحد المسموح.');
      const backup=JSON.parse(await file.text());
      if(backup.format!=='massar-answer-backup-v1'||backup.attemptId!==attempt.id||backup.examId!==exam.id)throw new Error('الملف لا يخص محاولة هذا الطالب في هذه الجلسة.');
      if(!backup.answers||typeof backup.answers!=='object'||Array.isArray(backup.answers))throw new Error('صيغة الإجابات غير صالحة.');
      const keys=Object.keys(backup.answers);
      if(keys.some(id=>!attempt.questionIds.includes(id)))throw new Error('النسخة تحتوي أسئلة خارج محاولة الطالب.');
      const changes=keys.filter(id=>backup.answers[id]!==attempt.answers[id]);
      if(!changes.length)throw new Error('الإجابات الموجودة في الملف وصلت بالفعل؛ لا توجد فروق.');
      setCandidate({backup,changes,version:attempt.recoveryVersion});
    } catch(error){toast(error.message,true);}
  };
  const answerText=(question,answer)=>answer==null||answer===''?'لم يجب':question.kind==='mcq'?question.options[answer]||'اختيار غير صالح':String(answer);
  const approve=async event=>{
    event.preventDefault();
    if(!await confirmAction('اعتماد الإجابات المسترجعة؟','الملف قابل للتعديل، ووقت كتابته غير مُثبت. اعتمد الفروق فقط بعد التحقق مع الطالب. سيتحدث تصحيح الاختياري وتُعاد مراجعة المقالي المتغير.'))return;
    setBusy(true);
    try {
      await request(`/api/attempts/${attempt.id}/recover-answers`,{...candidate.backup,expectedVersion:candidate.version,reason,confirmed:true});
      setCandidate(null);setReason('');await refresh();toast('تم اعتماد الاسترجاع وحفظ الورقة السابقة في سجل المراجعة.');
    }catch(error){toast(error.message,true);}finally{setBusy(false);}
  };
  return <details className="panel"><summary>استرجاع إجابات لم تصل من موبايل الطالب</summary>
    <p className="help">اطلب من الطالب تنزيل «نسخة احتياطية للإجابات». الملف ليس دليلًا على وقت كتابة الإجابة؛ راجع الفروق قبل اعتماده. لا يتغير وقت التسليم الأصلي.</p>
    <label>ملف الإجابات من الطالب<input type="file" accept="application/json,.json" onChange={read} disabled={busy}/></label>
    {candidate&&<form onSubmit={approve}>
      {candidate.changes.map(id=>{const question=exam.config.questions.find(q=>q.id===id);return <article key={id} className="review-question"><h3>{question.text}</h3>
        <p><strong>المحفوظ على الماك:</strong> {answerText(question,attempt.answers[id])}</p>
        <p><strong>نسخة الطالب:</strong> {answerText(question,candidate.backup.answers[id])}</p></article>;})}
      <label>سبب اعتماد الاسترجاع<textarea required minLength="5" maxLength="1000" value={reason} onChange={event=>setReason(event.target.value)}/></label>
      <button className="button" disabled={busy||candidate.version!==attempt.recoveryVersion}>{busy?'جارٍ الاعتماد…':'راجعت الفروق وأعتمد الاسترجاع'}</button>
      {candidate.version!==attempt.recoveryVersion&&<p role="alert">تغيرت الورقة؛ اختر الملف مرة أخرى لمراجعة أحدث الفروق.</p>}
    </form>}
  </details>;
}
