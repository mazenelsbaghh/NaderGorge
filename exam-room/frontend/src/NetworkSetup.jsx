import React, {useEffect, useState} from 'react';
import {copyText} from '../../web/assets/common.js';

export default function NetworkSetup() {
  const [status,setStatus]=useState(null);
  const [available,setAvailable]=useState(Boolean(window.pywebview?.api?.network_status));
  const [busy,setBusy]=useState(false);
  const [message,setMessage]=useState('');
  const refresh=async()=>{
    const bridge=window.pywebview?.api;
    if(!bridge?.network_status)return;
    try {setAvailable(true);setStatus(await bridge.network_status());}
    catch(error){setMessage(error.message||'تعذر قراءة حالة الشبكة.');}
  };
  useEffect(()=>{
    const ready=()=>{setAvailable(true);refresh();};
    window.addEventListener('pywebviewready',ready);
    refresh();
    return()=>window.removeEventListener('pywebviewready',ready);
  },[]);
  const prepare=async()=>{
    setBusy(true);setMessage('انتظر نافذة macOS واكتب كلمة سر الجهاز فيها فقط…');
    try {
      const result=await window.pywebview.api.prepare_network();
      setMessage(result.message);
      if(result.status)setStatus(result.status);else await refresh();
    } catch(error){setMessage(error.message||'تعذرت التهيئة.');}
    finally{setBusy(false);}
  };
  return <section className="panel network-setup"><div className="section-top"><div><span className="context-label">شبكة الامتحان بلا راوتر</span>
    <h2>تهيئة الاتصال المباشر بالـOmada</h2><p className="help">نسخة الإعداد 0.2.6 — الماك يوزّع عناوين الطلاب، والـOmada يوفّر الواي فاي. لا يحتاج الامتحان إنترنت.</p></div>
    <span className={`badge ${status?.ready?'complete':'quiet'}`}>{status?.ready?'الماك جاهز':'لم تكتمل التهيئة'}</span></div>
    {!available?<p className="help">افتح هذه الشاشة داخل برنامج مسار على الماك لتفعيل التهيئة.</p>:
    status&&!status.supported?<p className="help">{status.message}</p>:<>
      <ol className="network-setup-steps">
        <li><strong>جهّز الماك</strong><p>اضغط الزر مرة واحدة. سيطلب macOS كلمة سر الجهاز لتغيير إعداد منفذ Ethernet وتشغيل خدمتين محليتين. لا تُحفظ كلمة السر في البرنامج.</p></li>
        <li><strong>وصّل الكابل</strong><p>اترك كهرباء الـOmada موصولة، وانقل كابل الشبكة من الراوتر إلى منفذ Ethernet في الماك. يمكنك إبقاء الماك على واي فاي آخر للإنترنت.</p></li>
        <li><strong>اختبر صفحة الطلاب</strong><p>افصل الموبايل من شبكة الامتحان وأعد الاتصال بها. عند ظهور نافذة الدخول افتحها؛ ولو لم تظهر، افتح <b dir="ltr">http://10.77.0.1/</b> من المتصفح.</p></li>
      </ol>
      <div className="network-setup-status"><span>منفذ الكابل: <b>{status?.ethernetActive?'متصل':'غير متصل'}</b></span>
        <span>توزيع العناوين: <b>{status?.dhcpRunning?'يعمل':'متوقف'}</b></span>
        <span>صفحة الطلاب: <b>{status?.portalRunning?'جاهزة':'غير جاهزة'}</b></span></div>
      {status?.ethernetService&&<p className="help">منفذ الكابل: {status.ethernetService} ({status.ethernetDevice})، العنوان: {status.ethernetIp||'غير محدد'}</p>}
      {status?.configValid===false&&<p className="help" role="status">فحص إعداد الشبكة: {status.configDetail}</p>}
      {status&&!status.dhcpRunning&&<p className="help" role="status">سبب توقف توزيع العناوين: {status.dhcpDetail}</p>}
      <div className="actions"><button className="button" disabled={busy} onClick={prepare}>{busy?'جارٍ التهيئة…':status?.ready?'تحديث تجهيز الماك':'تهيئة الماك الآن'}</button>
        <button className="button secondary" disabled={busy} onClick={refresh}>فحص الاتصال</button>
        <button className="button ghost" onClick={()=>copyText('http://10.77.0.1/').catch(error=>setMessage(error.message))}>نسخ رابط الطلاب</button></div>
      {message&&<p className="help" role="status">{message}</p>}
      <p className="help">لا تفعّل Portal الداخلي في الـOmada لهذا الاختبار؛ سيضيف صفحة موافقة قبل الامتحان. ظهور نافذة الدخول تلقائيًا يعتمد على نظام الموبايل، لذلك احتفظ برابط الطلاب كبديل.</p>
    </>}
  </section>;
}
