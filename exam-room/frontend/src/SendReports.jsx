import React, {useRef,useState} from 'react';
import {api,confirmAction,number,toast} from '../../web/assets/common.js';
import {TemplateFields,sendStates,templateText} from './WhatsApp.jsx';
const renderMessage=(template,item)=>templateText(template).replace(/\{\{\s*(\d+)\s*\}\}/g,(_,i)=>item.parameters[Number(i)-1]||'');
export default function SendReports({attempts,label='إرسال PDF عبر واتساب'}) {
  const [dialog,setDialog]=useState(null),[templateId,setTemplateId]=useState(''),[mappings,setMappings]=useState([]),[items,setItems]=useState(null),[busy,setBusy]=useState(false),[progress,setProgress]=useState('');
  const stop=useRef(false);
  const ready=attempts.filter(a=>!a.cancelled_at&&a.submitted_at&&a.pending===0);
  const open=async()=>{try{const [bootstrap,config,cache]=await Promise.all([api('/api/bootstrap'),api('/api/whatsapp/configuration'),api('/api/whatsapp/templates')]);setDialog({token:bootstrap.token,config,templates:cache.templates});setItems(null);setProgress('');}catch(e){toast(e.message,true);}};
  const chooseTemplate=id=>{setTemplateId(id);setItems(null);};
  const chooseMappings=rows=>{setMappings(rows);setItems(null);};
  const preview=async()=>{setBusy(true);try{const result=await api('/api/whatsapp/reports/preview',{attemptIds:ready.map(a=>a.id),templateId,mappings},dialog.token,{timeoutMs:60000});setItems(result.items);}catch(e){toast(e.message,true);}finally{setBusy(false);}};
  const send=async(retry=false)=>{
    const pending=items.filter(item=>retry?['failed','rejected'].includes(item.prior?.state):!item.prior);
    if(!pending.length)return;
    if(!await confirmAction(`إرسال ${number(pending.length)} تقرير على واتساب؟`,'سيُرفع PDF كل طالب ودرجته إلى Meta ويُرسل إلى الرقم الظاهر. راجع الأرقام والمعاينة قبل التأكيد.'))return;
    stop.current=false;setBusy(true);let processed=0;
    try{for(const item of pending){if(stop.current)break;setProgress(`إرسال ${number(processed+1)} من ${number(pending.length)} · ${item.name}`);const response=await api('/api/whatsapp/reports/send',{attemptId:item.attemptId,templateId,mappings,fingerprint:item.fingerprint,retry},dialog.token,{timeoutMs:150000});setItems(rows=>rows.map(row=>row.attemptId===item.attemptId?{...row,prior:response.record}:row));processed++;if(response.record.state==='unknown'){stop.current=true;toast('حالة رسالة غير مؤكدة؛ توقف الإرسال لمراجعتها',true);}}
      setProgress(`تمت معالجة ${number(processed)} من ${number(pending.length)} تقرير. راجع حالة كل رسالة أدناه.`);
    }catch(e){setItems(null);setProgress('توقف الإرسال. أعد المعاينة لقراءة السجل قبل الاستكمال.');toast(e.message,true);}finally{setBusy(false);}
  };
  return <><button className="button secondary small" onClick={open}>{label}</button>{dialog&&<div className="send-overlay"><section className="panel send-dialog" role="dialog" aria-modal="true" aria-label="إرسال تقارير واتساب"><div className="section-top"><h2>إرسال تقارير واتساب السنتر</h2><button className="text-button" disabled={busy} onClick={()=>setDialog(null)}>إغلاق</button></div><p>{number(ready.length)} تقرير مكتمل التصحيح · {number(attempts.length-ready.length)} طالب لم يسلّم أو ينتظر التصحيح أو محاولته ملغاة.</p>
    {!dialog.config.configured?<div className="notice">أكمل ربط الحساب من قائمة «واتساب السنتر»، ثم ارجع لإرسال التقارير.</div>:<><p className="help">زامن القوالب من قائمة «واتساب السنتر» قبل الإرسال. يلزم إنترنت على جهاز الإدارة.</p><TemplateFields templates={dialog.templates} templateId={templateId} setTemplateId={chooseTemplate} mappings={mappings} setMappings={chooseMappings} disabled={busy}/><div className="actions"><button className="button secondary" disabled={busy||!ready.length||!templateId} onClick={preview}>معاينة المستلمين والتقارير</button>{items&&<button className="button" disabled={busy||!items.some(item=>!item.prior)} onClick={()=>send()}>إرسال التقارير الجديدة ({number(items.filter(item=>!item.prior).length)})</button>}{items?.some(item=>['failed','rejected'].includes(item.prior?.state))&&<button className="button secondary" disabled={busy} onClick={()=>send(true)}>إعادة محاولة التقارير التي لم تُرسل</button>}{busy&&items&&<button className="button secondary" onClick={()=>{stop.current=true;setProgress('سيتوقف الإرسال بعد الطلب الحالي.');}}>إيقاف بعد الطالب الحالي</button>}</div></>}
    {progress&&<p className="notice" role="status">{progress}</p>}{items&&<div className="table-scroll"><table><thead><tr><th>الطالب والرقم</th><th>PDF والرسالة</th><th>حالة الإرسال</th></tr></thead><tbody>{items.map(item=><tr key={item.attemptId}><td><strong>{item.name}</strong><small dir="ltr">{item.phone}</small><small>الكود: {item.code} · {number(item.score)} من {number(item.maximum)}</small></td><td>{item.filename}<small className="whatsapp-message" dir="auto">{renderMessage(dialog.templates.find(t=>t.id===templateId),item)}</small></td><td>{item.prior?sendStates[item.prior.state]:'جاهز للإرسال'}<small>{item.prior?.error}</small></td></tr>)}</tbody></table></div>}
    <p className="help">يُرسل كل طالب ملفه فقط. الطلب المقبول أو غير المؤكد محفوظ لمنع إعادة إرساله. لا تغلق البرنامج أثناء الإرسال.</p></section></div>}</>;
}
