import React, {useEffect, useRef, useState} from 'react';
import PortalConfiguration from './PortalConfiguration';
import NetworkDiagnostics from './NetworkDiagnostics';
import {copyText} from '../../web/assets/common.js';
import {publishNetworkStatus,useNetworkStatus,verifiedPortalUrl} from './networkStatus.js';

export default function NetworkSetup() {
  const status=useNetworkStatus();
  const portalUrl=verifiedPortalUrl(status);
  const studentUrl=portalUrl||'http://10.77.0.1/';
  const [available,setAvailable]=useState(Boolean(window.pywebview?.api?.network_status));
  const [busy,setBusy]=useState(false);
  const [refreshing,setRefreshing]=useState(false);
  const refreshPending=useRef(false);
  const [message,setMessage]=useState('');
  const refresh=async(fresh=false)=>{
    const bridge=window.pywebview?.api;
    if(!bridge?.network_status||refreshPending.current)return;
    refreshPending.current=true;setRefreshing(true);
    try {setAvailable(true);publishNetworkStatus(await (fresh&&bridge.refresh_network_status?bridge.refresh_network_status():bridge.network_status()));}
    catch(error){setMessage(error.message||'تعذر قراءة حالة الشبكة.');}
    finally{refreshPending.current=false;setRefreshing(false);}
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
      if(result.status)publishNetworkStatus(result.status);else await refresh();
    } catch(error){setMessage(error.message||'تعذرت التهيئة.');}
    finally{setBusy(false);}
  };
  return <section className="panel network-setup"><div className="section-top"><div><span className="context-label">شبكة الامتحان بلا راوتر</span>
    <h2>تهيئة الاتصال المباشر بالـOmada</h2><p className="help">نسخة الإعداد 0.4.24 — الماك يوزّع عناوين الطلاب، والـOmada يوفّر الواي فاي. لا يحتاج الامتحان إنترنت.</p></div>
    <span className={`badge ${status?.ready?'complete':'quiet'}`}>{status?.ready?'الماك جاهز':!status||status.inspectionError?'لم يتم التأكد':'لم تكتمل التهيئة'}</span></div>
    {!available?<p className="help">افتح هذه الشاشة داخل برنامج مسار على الماك لتفعيل التهيئة.</p>:
    status&&!status.supported?<p className="help">{status.message}</p>:<>
      <ol className="network-setup-steps">
        <li><strong>وصّل الهاب والكابل أولًا</strong><p>وصّل الهاب بالماك وكابل الـOmada بمدخل Ethernet في الهاب، واترك كهرباء الـOmada موصولة. افصل كابلات Ethernet الأخرى أثناء التهيئة لتحديد منفذ الامتحان بوضوح. يمكنك إبقاء الماك على واي فاي آخر للإنترنت.</p></li>
        <li><strong>جهّز الماك</strong><p>راجع منفذ الكابل الظاهر أدناه، ثم اضغط زر التهيئة. سيطلب macOS كلمة سر الجهاز لتغيير إعداد منفذ Ethernet وتشغيل خدمتين محليتين. لا تُحفظ كلمة السر في البرنامج.</p></li>
        <li><strong>اختبر صفحة الطلاب</strong><p>اتصل بواي فاي الامتحان، ثم افتح الرابط في Chrome أو Samsung Internet: <b dir="ltr">{studentUrl}</b> أو امسح QR الموجود في شاشة الامتحان قبل تسجيل الدخول. استمر في نفس الرابط والمتصفح حتى التسليم.</p>
          {portalUrl&&<p>لو صفحة الدخول التلقائي لم تظهر، افتح نفس الرابط أعلاه. الرابط المحلي <b dir="ltr">http://10.77.0.1/</b> بديل قبل تسجيل الدخول فقط؛ لا تنتقل إليه بعد بدء المحاولة.</p>}</li>
      </ol>
      <details className="notice"><summary>سامسونج متصل لكن نافذة الدخول لم تظهر؟</summary>
        <p>لو الرابط يفتح يدويًا، اتصال الموبايل بالامتحان يعمل؛ النافذة التلقائية تعتمد على نظامه. افتح الامتحان من المتصفح العادي قبل تسجيل الدخول، واستمر في نفس المتصفح حتى التسليم.</p>
        <p>إذا كان الاتصال يتبدل: إعدادات الموبايل ← الاتصالات ← Wi-Fi ← ⋮ ← Intelligent Wi-Fi، وأوقف «Switch to mobile data» أثناء الامتحان، واختر البقاء متصلًا عند ظهور «بلا إنترنت». أسماء الخيارات تختلف حسب الموديل.</p>
        <p>لو الرابط نفسه لا يفتح، راجع وصول شبكة الطلاب إلى الماك، وإعداد Guest Network أو قواعد الحظر في Omada. نجاح الفحص على الماك وحده لا يثبت وصول الموبايلات.</p>
      </details>
      {status?.inspectionError&&<p className="notice" role="status">{status.message}</p>}
      <div className="network-setup-status"><span>منفذ الكابل: <b>{!status||status.inspectionError?'لم يتم التأكد':status.ethernetAmbiguous?'أكثر من منفذ متصل':status.ethernetActive?'متصل':'غير متصل'}</b></span>
        <span>توزيع العناوين: <b>{!status||status.inspectionError?'لم يتم التأكد':!status.dhcpRunning?'الخدمة متوقفة':status.dhcpConfigurationReady===false?'الإعداد يحتاج مراجعة':status.dhcpSocketReady===true?'المنفذ يستقبل':status.dhcpSocketReady===false?'المنفذ لا يستقبل':'لم يتم التأكد'}</b></span>
        <span>صفحة الطلاب: <b>{status?.androidProbe?.studentServiceReady?'تستجيب':'لم يتم التأكد'}</b></span></div>
      <div className="notice"><strong>فحص خدمة الدخول والامتحان</strong><p>{status?.androidProbe?.detail||'وصّل Omada وجهّز خدمات الماك لتشغيل الفحص.'}</p><p>شبكة ٢.٤: لم يتم التحقق من إعداداتها · شبكة ٥: لم يتم التحقق من إعداداتها</p><p className="help">فحص خدمة الماك لا يثبت إعداد كل تردد. يلزم قراءة إعدادات Omada وتجربة هاتف متصل بكل شبكة.</p><a className="text-button" href="https://10.77.0.2/" target="_blank" rel="noreferrer">فتح إعدادات Omada</a></div>
      {status?.ethernetService&&<p className="help">منفذ الكابل: {status.ethernetService} ({status.ethernetDevice})، العنوان: {status.ethernetIp||'غير محدد'}</p>}
      {status?.ethernetDetail&&<p className="notice" role="status">{status.ethernetDetail}</p>}
      {status?.configValid===false&&<p className="help" role="status">فحص إعداد الشبكة: {status.configDetail}</p>}
      {status?.dhcpDetail&&<p className="help" role="status">فحص توزيع العناوين: {status.dhcpDetail}</p>}
      {status?.dhcpLeases&&<div className="notice" role="status"><strong>عناوين أجهزة الطلاب</strong><p>{status.dhcpLeases.detail}</p>
        {status.dhcpLeases.dhcpLeaseCount!=null&&<p>المحجوز: {status.dhcpLeases.dhcpLeaseCount} من {status.dhcpLeases.capacity}</p>}
        {status.dhcpLeases.findings?.map(finding=><p key={finding.code}>{finding.detail}</p>)}
        <p className="help">الأجهزة المتصلة تجدّد عنوانها تلقائيًا. فحص السجل لا يثبت غياب راوتر آخر يوزّع عناوين على الشبكة.</p></div>}
      <PortalConfiguration status={status} disabled={busy} refresh={refresh} />
      <details className="notice"><summary>اختبار القاعة قبل دخول الطلاب</summary><ol>
        <li>تأكد أن الماك هو موزع العناوين الوحيد وأن شبكة الطلاب تسمح بالوصول إليه.</li>
        <li>اختبر سامسونج وآيفون وموبايلًا قديمًا على ترددي الواي فاي، من أماكن مختلفة في القاعة.</li>
        <li>اختبر إشعار الدخول، ثم دخول الامتحان وحفظ إجابة والتسليم في نفس المتصفح.</li>
        <li>اقطع اتصال هاتف التجربة وأعده، وتأكد من وصول إجابته. اختبر عددًا قريبًا من عدد الطلاب الفعلي.</li>
        <li>راجع الكهرباء والكابل، وصلاحية الشهادة إذا اخترت الدخول الآمن. احتفظ برابط الطلاب وQR كطريق إضافي للدخول.</li>
      </ol></details>
      <p className="help">لو عنوان الماك أو توزيع عناوين الطلاب محتاج إصلاح، أغلق القاعات واضغط زر التهيئة. يعيد ضبط منفذ الهاب المتصل ويختبر عنوانه وخدمة التوزيع. لو الهاب مفصول، وصّله واضغط «فحص الاتصال» أولًا.</p>
      <div className="actions"><button className="button" disabled={busy||refreshing||!status?.ethernetActive||status?.ethernetAmbiguous} onClick={prepare}>{busy?'جارٍ التهيئة…':status?.ready?'تحديث تجهيز الماك':'تهيئة الماك الآن'}</button>
        <button className="button secondary" disabled={busy||refreshing} onClick={()=>refresh(true)}>{refreshing?'جارٍ فحص الاتصال…':'فحص الاتصال'}</button>
        <button className="button ghost" onClick={()=>copyText(studentUrl).catch(error=>setMessage(error.message))}>نسخ رابط الطلاب</button></div>
      <NetworkDiagnostics />
      {message&&<p className="help" role="status">{message}</p>}
      <p className="help">لا تفعّل Portal الداخلي في الـOmada لهذا الاختبار؛ سيضيف صفحة موافقة قبل الامتحان. ظهور نافذة الدخول تلقائيًا يعتمد على نظام الموبايل، لذلك احتفظ برابط الطلاب كبديل.</p>
    </>}
  </section>;
}
