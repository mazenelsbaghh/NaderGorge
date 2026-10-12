import React, {useRef, useState} from 'react';

const requestNames={probe:'فحص الدخول التلقائي',student_page:'فتح صفحة الطالب',room_connection:'الاتصال بالقاعة',student_join:'طلب دخول طالب',answer_save:'حفظ أو تسليم إجابات',request_error:'طلب لم يكتمل',asset_error:'تعذّر تحميل ملف الصفحة',student_session:'تعذّر تحديث حالة الطالب',capport:'اكتشاف البوابة المعيارية'};
const failureNames={request_canceled:'الطلب اتلغى أو اتقطع من جهة الاتصال',connection_refused:'خادم الامتحان رفض الاتصال',descriptor_limit:'نفدت اتصالات النظام المتاحة',connection_reset:'الاتصال اتقطع فجأة',connection_closed:'الاتصال اتقفل قبل اكتمال الرد',timeout:'انتهت مهلة الاتصال',transport_error:'خطأ اتصال يحتاج مراجعة السجل'};

function timeLabel(value) {
  const date=new Date(value);
  return value&&!Number.isNaN(date.getTime())?date.toLocaleString('ar-EG'):'غير متاح';
}

export default function NetworkDiagnostics() {
  const [diagnostics,setDiagnostics]=useState(null);
  const [busy,setBusy]=useState(false);
  const [message,setMessage]=useState('');
  const [stale,setStale]=useState(false);
  const [ipFilter,setIpFilter]=useState('');
  const pending=useRef(false);
  const perform=async(action)=>{
    if(pending.current)return;
    pending.current=true;setBusy(true);setMessage('');
    try {
      const bridge=window.pywebview?.api;
      if(action==='read'){setDiagnostics(await bridge.network_diagnostics());setStale(false);}
      else {
        const result=await (action==='mark'?bridge.mark_network_incident():bridge.export_network_diagnostics());
        setMessage(result.message||(result.ok?'تم.':'تعذر إكمال الطلب.'));
        if(action==='mark'&&result.ok){setDiagnostics(await bridge.network_diagnostics());setStale(false);}
      }
    } catch {if(action!=='export')setStale(true);setMessage('تعذر قراءة أو حفظ التقرير. حاول مرة أخرى من برنامج مسار على الماك.');}
    finally {pending.current=false;setBusy(false);}
  };
  const timeline=diagnostics?.networkTimeline;
  const recording=timeline?.recording;
  const events=Array.isArray(diagnostics?.events)?diagnostics.events:[];
  const backend=diagnostics?.backendDiagnostics;
  const backendRecorder=backend?.snapshot?.recorder;
  const backendIncomplete=backendRecorder&&(backendRecorder.persistence!=='ready'||backendRecorder.dropped>0||backendRecorder.writeFailures>0||backendRecorder.clientRejectedBatches>0);
  const historical=backend?.historical===true;
  const backendUnavailable=!backend||backend.status==='unavailable'||historical;
  return <section className="notice network-diagnostics" aria-label="تشخيص الاتصال والأخطاء">
    <strong>تشخيص الاتصال والأخطاء</strong>
    <p className="help">التسجيل يعمل تلقائيًا في الخلفية أثناء فتح البرنامج، حتى لو خرجت من شاشة الإعدادات. عند حدوث مشكلة اضغط «سجّل وقت المشكلة»، ثم نزّل التقرير وأرسله للدعم. لا يحتاج إنترنت.</p>
    <div className="actions">
      <button className="button secondary" disabled={busy} onClick={()=>perform('mark')}>سجّل وقت المشكلة</button>
      <button className="button secondary" disabled={busy} onClick={()=>perform('export')}>تنزيل تقرير التشخيص</button>
      <button className="button ghost" disabled={busy} onClick={()=>perform('read')}>تحديث السجل</button>
    </div>
    {message&&<p role="status">{message}</p>}
    {diagnostics&&<>
      {stale&&<p role="status">المعروض آخر قراءة سابقة؛ تعذر تحديثها ولا تؤكد أن التسجيل يعمل الآن.</p>}
      <div className="network-setup-status" aria-label="حالة تسجيل التشخيص">
        <span>سجل الهاب والعناوين: <b>{stale?'قراءة سابقة':timeline?.status==='ok'&&recording?.active?'يسجل':timeline?.status==='partial'?'السجل غير مكتمل':'لم يتم التأكد'}</b></span>
        <span>سجل خادم الامتحان: <b>{stale?'قراءة سابقة':historical&&backend?.status==='partial'?'متاح من سجل سابق':backendUnavailable?'غير متاح في هذا الفحص':backendIncomplete||backend?.status==='partial'?'السجل غير مكتمل':'متاح'}</b></span>
        <span>سجل بوابة الدخول: <b>{stale?'قراءة سابقة':diagnostics.loggingInstalled?'موجود':'لم يُنشأ بعد'}</b></span>
      </div>
      <p className="help">آخر فحص مسجل للهاب: {timeLabel(recording?.lastSampleAt)}. آخر قراءة للتقرير: {timeLabel(diagnostics.capturedAt)}.</p>
      {(!timeline||timeline.status!=='ok'||recording?.writeFailureCount>0)&&<p role="status">تعذر تأكيد اكتمال تسجيل حالة الشبكة. التقرير يوضح الأجزاء المتاحة وأخطاء التسجيل؛ راجع أيضًا المساحة الحرة على الماك.</p>}
      {backendUnavailable&&<p role="status">لم نتمكن من قراءة سجل الخادم الآن. يمكنك تنزيل بقية التقرير؛ هذا لا يثبت أن الخادم كان متوقفًا وقت المشكلة.</p>}
      {backend?.historical&&backend?.status==='partial'&&<p className="help">التقرير يتضمن أحداث الخادم المحفوظة سابقًا، مع توقيت كل حدث؛ وهي لا تؤكد حالته الحالية.</p>}
      {backendIncomplete&&<p role="status">حصل تعثّر في تسجيل بعض الأحداث بسبب ضغط التسجيل أو خطأ كتابة. التقرير يتضمن أعداد الأحداث والدفعات المتأثرة وحالة التخزين.</p>}
      {!diagnostics.loggingInstalled&&<p role="status">إذا لم يظهر سجل البوابة بعد استخدام هاتف، حدّث تجهيز الماك بعد انتهاء الامتحان وإغلاق القاعات.</p>}
      <p className="help">التقرير يجمع تغيّرات الهاب وعنوان الماك وخدمة توزيع العناوين، وطلبات الدخول والحفظ والأخطاء التي وصلت من المتصفح. أحداث الهاتف غير المتصل تصل بعد رجوع الاتصال إذا أمكن تشغيل الصفحة وحفظ السجل محليًا.</p>
      <details><summary>طلبات بوابة الدخول</summary>
        <label>تصفية بعنوان الموبايل<input value={ipFilter} onChange={event=>setIpFilter(event.target.value)} placeholder="10.77.0.20" dir="ltr" /></label>
        <p className="help">آخر ٢٠٠ طلب مسجل. رد 302 يعني تجهيز التحويل؛ رد 200 لا يثبت وحده أن الهاتف استلم الرد كاملًا. الطلب الصادر من الماك نفسه قد يكون فحص البرنامج.</p>
        <div style={{overflowX:'auto',maxHeight:320}}><table><thead><tr><th>الوقت</th><th>الجهاز</th><th>الطلب</th><th>الرد</th></tr></thead><tbody>
          {events.filter(event=>!ipFilter||event.ip===ipFilter.trim()).slice(-200).reverse().map((event,index)=><tr key={`${event.time}-${index}`}><td>{timeLabel(event.time)}</td><td dir="ltr">{event.ip}</td><td>{requestNames[event.kind]||'طلب آخر'}</td><td>{event.status} · {event.durationMs}ms{event.proxyError&&<small style={{display:'block'}}>{failureNames[event.proxyError]||'خطأ اتصال غير مصنّف'}</small>}</td></tr>)}
        </tbody></table></div>
        {!events.length&&<p>لا توجد طلبات مسجلة بعد. عدم وصول طلب يحتاج فحص اتصال الهاتف بالشبكة.</p>}
      </details>
    </>}
    <p className="help">السجلات محدودة الحجم وتستبدل الأقدم تلقائيًا. التقرير لا يضم أسماء الطلاب أو أرقامهم أو أكوادهم أو إجاباتهم؛ يتضمن عناوين الشبكة المحلية. لا تغيّر متصفح الطالب أو تمسح بياناته أثناء وجود إجابات لم تصل بعد.</p>
  </section>;
}
