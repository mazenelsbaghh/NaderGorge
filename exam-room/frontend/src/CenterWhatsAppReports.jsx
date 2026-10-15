import React,{useEffect,useState} from 'react';
import {api,number} from '../../web/assets/common.js';
import SendReports from './SendReports.jsx';
import WhatsAppJobs from './WhatsAppJobs.jsx';
import {whatsappViewHref} from './whatsappTemplateFields.js';

const messageKinds={followup:'متابعة الحضور والواجب والدرجة',attendance:'الحضور والغياب',homework:'عمل الواجب أو لم يعمل',grade:'درجة الطالب'};
function validPhone(raw){
 let value=String(raw||'').replace(/[٠-٩۰-۹]/g,c=>String(c.charCodeAt(0)-(c<='٩'?1632:1776))).replace(/[\s()-]/g,'');
 if(value.startsWith('0020'))value='0'+value.slice(4);else if(value.startsWith('+20'))value='0'+value.slice(3);else if(value.startsWith('20')&&value.length===12)value='0'+value.slice(2);
 return /^01[0125]\d{8}$/.test(value);
}
export default function CenterWhatsAppReports({batchId}){
 const [batch,setBatch]=useState(null),[error,setError]=useState(''),[selected,setSelected]=useState(new Set()),[recipient,setRecipient]=useState('guardian');
 useEffect(()=>{
  let alive=true;setBatch(null);setError('');
  api(`/api/whatsapp/center/batches/${encodeURIComponent(batchId)}`).then(result=>{if(alive){setBatch(result);setSelected(new Set(result.records.map(row=>row.id)));}}).catch(reason=>{if(alive)setError(reason.message);});
  return()=>{alive=false;};
 },[batchId]);
 if(error)return <section className="panel padded"><h1>تعذر فتح رسائل السنتر</h1><p role="alert">{error}</p></section>;
 if(!batch)return <p className="loading">جارٍ تحميل الطلاب وبيانات الرسائل…</p>;
 const phone=row=>recipient==='guardian'?row.guardianPhone:row.phone;
 const chosen=batch.records.filter(row=>selected.has(row.id));
 const ready=chosen.filter(row=>validPhone(phone(row)));
 const all=batch.records.length>0&&batch.records.every(row=>selected.has(row.id));
 const reportKind=batch.messageKind||'followup';
 return <>
  <header className="page-heading"><div><h1>رسائل واتساب</h1><p>{batch.title} · {messageKinds[reportKind]||messageKinds.followup}</p></div><a className="button secondary" href={whatsappViewHref('whatsapp',batchId)}>إعداد حساب واتساب والقوالب</a></header>
  <section className="panel padded">
   <div className="section-top"><label>المستلم<select value={recipient} onChange={event=>setRecipient(event.target.value)}><option value="guardian">ولي الأمر</option><option value="student">الطالب</option></select></label><p role="status">{number(ready.length)} رسالة برقم صالح · {number(chosen.length-ready.length)} رقم مفقود أو غير صالح</p></div>
   <details><summary>مراجعة الطلاب المحددين ({number(chosen.length)})</summary>
    <label><input type="checkbox" checked={all} onChange={event=>setSelected(event.target.checked?new Set(batch.records.map(row=>row.id)):new Set())}/> تحديد الكل</label>
    <div className="table-scroll"><table><thead><tr><th>اختيار</th><th>الطالب والكود</th><th>الرقم</th><th>المجموعة والحصة</th><th>الحضور</th><th>الواجب</th><th>الدرجة</th></tr></thead><tbody>{batch.records.map(row=><tr key={row.id}><td><input aria-label={`اختيار ${row.name}`} type="checkbox" checked={selected.has(row.id)} onChange={event=>setSelected(previous=>{const next=new Set(previous);if(event.target.checked)next.add(row.id);else next.delete(row.id);return next;})}/></td><td>{row.name}<small>{row.code}</small></td><td dir="ltr">{phone(row)||'غير مسجل'}<small>{validPhone(phone(row))?'':'لن يدخل الإرسال'}</small></td><td>{row.group}<small>{row.lesson} · {row.exam}</small></td><td>{row.attendance||'غير مسجل'}</td><td>{row.homework||'غير مسجل'}</td><td>{row.score==null?'لم تُرصد':`${row.score} من ${row.maximum??'غير معروفة'}`}</td></tr>)}</tbody></table></div>
   </details>
  </section>
  <SendReports attempts={ready} centerBatchId={batchId} recipientKind={recipient} reportKind={reportKind}/>
  <WhatsAppJobs/>
 </>;
}
