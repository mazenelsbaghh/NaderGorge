import React, {useState} from 'react';
import {toast} from '../../web/assets/common.js';

export default function CancelAttempt({attempt,request,refresh,onClose}) {
  const [reason,setReason]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const submit=async event=>{
    event.preventDefault();
    if(busy)return;
    setBusy(true);setError('');
    try {
      await request(`/api/attempts/${attempt.id}/cancel`,{reason:reason.trim()});
      await refresh();onClose();toast('تم إلغاء المحاولة وحفظ السبب في الشيت');
    } catch(e) {setError(e.message);} finally {setBusy(false);}
  };
  return <div className="send-overlay"><section className="panel send-dialog" role="dialog" aria-modal="true" aria-labelledby="cancel-attempt-title">
    <h2 id="cancel-attempt-title">إلغاء محاولة {attempt.name}</h2>
    <p>كود الطالب: <b dir="ltr">{attempt.code}</b></p>
    <p className="notice">يتوقف الطالب عن الحل ولا يمكنه العودة لهذه المحاولة. تظل إجاباته محفوظة للمراجعة، وتظهر حالته «ملغي» وسبب الإلغاء في الشيت.</p>
    <form onSubmit={submit}><label>سبب الإلغاء<textarea autoFocus required maxLength={1000} rows={4} value={reason} onChange={e=>setReason(e.target.value)} placeholder="اكتب سبب الإلغاء…"/></label>
      {error&&<p role="alert" className="inline-error">{error}</p>}
      <div className="actions"><button className="button danger" disabled={busy||!reason.trim()}>{busy?'جارٍ الإلغاء…':'تأكيد إلغاء المحاولة'}</button><button type="button" className="button secondary" disabled={busy} onClick={onClose}>رجوع</button></div>
    </form></section></div>;
}
