import React, {useEffect,useRef,useState} from 'react';
import {api,confirmAction,number,toast} from '../../web/assets/common.js';
import {TemplateFields,sendStates} from './WhatsApp.jsx';
import {initialTemplateMappings,renderTemplateMessage as renderMessage,savedTemplateBinding,validTemplateMappings} from './whatsappTemplateFields.js';

export default function SendReports({attempts,label='إرسال عبر واتساب',centerBatchId='',recipientKind='student',reportKind='exam'}) {
  const inline=Boolean(centerBatchId);
  const [dialog,setDialog]=useState(null),[templateId,setTemplateId]=useState(''),[mappings,setMappings]=useState([]),[items,setItems]=useState(null),[busy,setBusy]=useState(false),[progress,setProgress]=useState(''),[loadError,setLoadError]=useState('');
  const ready=inline?attempts:attempts.filter(a=>!a.cancelled_at&&a.submitted_at&&a.pending===0);
  const audienceKey=JSON.stringify([centerBatchId,recipientKind,reportKind,ready.map(row=>row.id)]);
  const selectedTemplate=dialog?.templates.find(template=>template.id===templateId&&template.reportSupported);
  const schemaKey=JSON.stringify([templateId,selectedTemplate?.bindingSchema,recipientKind,reportKind]);
  const binding=savedTemplateBinding(selectedTemplate,recipientKind,reportKind);
  const mappingSaved=Boolean(binding&&JSON.stringify(binding.mappings)===JSON.stringify(mappings));
  const mappingsReady=Boolean(selectedTemplate&&validTemplateMappings(selectedTemplate,mappings));
  const requestKey=JSON.stringify([audienceKey,templateId,selectedTemplate?.bindingSchema,mappings]);
  const currentRequest=useRef(requestKey);currentRequest.current=requestKey;
  const [previewRequest,setPreviewRequest]=useState('');
  const previewCurrent=items&&previewRequest===requestKey;

  const open=async()=>{
    setLoadError('');
    try{
      const [bootstrap,config,cache]=await Promise.all([api('/api/bootstrap'),api('/api/whatsapp/configuration'),api('/api/whatsapp/templates',undefined,undefined,{timeoutMs:120000})]);
      setDialog({token:bootstrap.token,config,templates:cache.templates,syncError:cache.syncError});setItems(null);setProgress('');
    }catch(error){setLoadError(error.message);toast(error.message,true);}
  };
  useEffect(()=>{if(inline)open();},[centerBatchId]);
  useEffect(()=>{setMappings(initialTemplateMappings(selectedTemplate,recipientKind,reportKind));setItems(null);},[schemaKey]);
  useEffect(()=>{setItems(null);},[audienceKey]);
  useEffect(()=>{
    if(!dialog||busy)return;
    let live=true;
    const timer=setInterval(()=>{api('/api/whatsapp/templates',undefined,undefined,{timeoutMs:120000}).then(cache=>{if(live)setDialog(previous=>previous?{...previous,templates:cache.templates,syncError:cache.syncError}:previous);}).catch(error=>{if(live)setProgress(error.message);});},60000);
    return()=>{live=false;clearInterval(timer);};
  },[Boolean(dialog),busy]);

  const chooseTemplate=id=>{setTemplateId(id);setItems(null);setProgress('');};
  const chooseMappings=rows=>{setMappings(rows);setItems(null);};
  const saveBindings=async()=>{
    setBusy(true);
    try{
      const saved=await api('/api/whatsapp/template-bindings',{templateId,bindingSchema:selectedTemplate.bindingSchema,recipientKind,reportKind,mappings},dialog.token);
      setDialog(previous=>({...previous,templates:previous.templates.map(template=>template.id===templateId?{...template,savedBindings:[...(template.savedBindings||[]).filter(row=>row.recipientKind!==recipientKind||row.reportKind!==reportKind),saved]}:template)}));
      setProgress('تم تثبيت الربط لهذا القالب ونوع الرسالة والمستلم. تُملأ بيانات كل طالب تلقائيًا في المرات القادمة.');
    }catch(error){toast(error.message,true);}finally{setBusy(false);}
  };
  const preview=async()=>{
    const requested=requestKey;
    setBusy(true);
    try{
      const result=await api('/api/whatsapp/reports/preview',{attemptIds:ready.map(row=>row.id),templateId,bindingSchema:selectedTemplate.bindingSchema,mappings,centerBatchId,recipientKind},dialog.token,{timeoutMs:120000});
      if(currentRequest.current===requested){setItems(result.items);setPreviewRequest(requested);}
    }catch(error){toast(error.message,true);}finally{setBusy(false);}
  };
  const send=async(retry=false)=>{
    if(!previewCurrent)return;
    const pending=items.filter(item=>!item.error&&(retry?['failed','rejected'].includes(item.prior?.state):!item.prior));
    if(!pending.length||!await confirmAction(`إرسال ${number(pending.length)} رسالة على واتساب؟`,'راجع المعاينة والأرقام. كل مستلم تصله بيانات الطالب الظاهرة فقط.'))return;
    if(currentRequest.current!==requestKey)return;
    setBusy(true);
    try{
      await api('/api/whatsapp/jobs',{items:pending.map(item=>({attemptId:item.attemptId,fingerprint:item.fingerprint})),templateId,bindingSchema:selectedTemplate.bindingSchema,mappings,retry,centerBatchId,recipientKind},dialog.token,{timeoutMs:120000});
      if(!inline)setDialog(null);setItems(null);
      window.dispatchEvent(new Event('whatsapp-jobs-changed'));
      setProgress('أُضيفت الرسائل لدور الإرسال. تابع حالتها في سجل الإرسال.');
      toast('أُضيفت الرسائل لدور الإرسال.');
    }catch(error){setItems(null);setProgress('أعد المعاينة لقراءة السجل قبل الاستكمال.');toast(error.message,true);}finally{setBusy(false);}
  };

  if(inline&&!dialog)return <section className="panel padded"><h2>قالب الرسالة ومعاينتها</h2>{loadError?<><p className="notice" role="alert">{loadError}</p><button className="button secondary" onClick={open}>إعادة تحميل القوالب</button></>:<p className="loading">جارٍ تحميل قوالب الحساب…</p>}</section>;
  const form=dialog&&<section className={`panel ${inline?'padded':'send-dialog'}`} {...(inline?{}:{role:'dialog','aria-modal':true,'aria-label':'إرسال تقارير واتساب'})}>
    <div className="section-top"><h2>قالب الرسالة ومعاينتها</h2>{!inline&&<button className="text-button" disabled={busy} onClick={()=>setDialog(null)}>إغلاق</button>}</div>
    <p>{inline?`${number(ready.length)} طالب محدد برقم صالح`:`${number(ready.length)} تقرير مكتمل التصحيح · ${number(attempts.length-ready.length)} طالب لم يسلّم أو ينتظر التصحيح أو محاولته ملغاة.`}</p>
    {!dialog.config.configured?<div className="notice">افتح «إعداد حساب واتساب والقوالب» لربط الحساب أول مرة.</div>:<>
      {dialog.syncError&&<p className="notice">{dialog.syncError}</p>}
      <TemplateFields templates={dialog.templates} templateId={templateId} setTemplateId={chooseTemplate} mappings={mappings} setMappings={chooseMappings} disabled={busy} recipientKind={recipientKind} reportKind={reportKind}/>
      {selectedTemplate&&<div className="actions"><button className="button secondary" disabled={busy||!mappingsReady||mappingSaved} onClick={saveBindings}>{mappingSaved?'ربط المتغيرات مثبت':'تثبيت ربط المتغيرات'}</button><p className="help">اختار مصدر كل متغير مرة واحدة. تُقرأ الدرجة والنهاية العظمى من سجل الطالب، ومنها الورقي من ١٠ والإلكتروني من ١٢. نص القالب الثابت يظل كما اعتمدته Meta؛ اختار قالب الحضور أو الغياب المناسب.</p></div>}
      <div className="actions"><button className="button secondary" disabled={busy||!ready.length||!mappingsReady} onClick={preview}>معاينة الرسائل ({number(ready.length)})</button>{previewCurrent&&<button className="button" disabled={busy||!items.some(item=>!item.error&&!item.prior)} onClick={()=>send()}>إرسال الرسائل الجديدة ({number(items.filter(item=>!item.error&&!item.prior).length)})</button>}{previewCurrent&&items.some(item=>['failed','rejected'].includes(item.prior?.state))&&<button className="button secondary" disabled={busy} onClick={()=>send(true)}>إعادة محاولة الرسائل التي لم تُرسل</button>}</div>
    </>}
    {progress&&<p className="notice" role="status">{progress}</p>}
    {previewCurrent&&items.some(item=>item.error)&&<p className="notice" role="alert">{number(items.filter(item=>item.error).length)} طالب لن يدخل الإرسال لأن القالب لا يطابق حالته. غيّر القالب أو تحديد الطلاب ثم أعد المعاينة.</p>}
    {previewCurrent&&<div className="table-scroll"><table><thead><tr><th>الطالب والرقم</th><th>معاينة الرسالة</th><th>حالة الإرسال</th></tr></thead><tbody>{items.map(item=><tr key={item.attemptId}><td><strong>{item.name}</strong><small dir="ltr">{item.phone}</small><small>الكود: {item.code} · {item.score==null?'لم تُرصد':number(item.score)} من {item.maximum==null?'غير معروفة':number(item.maximum)}</small></td><td><small className="whatsapp-message" dir="auto">{item.error?'لن تُرسل لهذا الطالب':renderMessage(selectedTemplate,item)}</small>{!inline&&<small>{item.filename}</small>}</td><td>{item.error?'مستبعد من الإرسال':item.prior?sendStates[item.prior.state]:'جاهز للإرسال'}<small>{item.error||item.prior?.error}</small></td></tr>)}</tbody></table></div>}
    <p className="help">الإرسال حتى ٥ رسائل معًا، والمحاولة تتكرر للأخطاء المؤقتة المؤكدة. الرسائل المقبولة أو غير المؤكدة محفوظة لمنع تكرارها. أبقِ البرنامج مفتوحًا ومتصلًا بالإنترنت.</p>
  </section>;
  return inline?form:<><button className="button secondary small" onClick={open}>{label}</button>{dialog&&<div className="send-overlay">{form}</div>}</>;
}
