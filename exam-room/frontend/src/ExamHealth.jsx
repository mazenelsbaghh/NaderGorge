import React, {useEffect, useState} from 'react';
import {publishNetworkStatus} from './networkStatus.js';

export default function ExamHealth() {
  const [status,setStatus]=useState(null);
  const [failed,setFailed]=useState(false);
  useEffect(()=>{
    let alive=true,timer,pending=false;
    const check=async()=>{
      const bridge=window.pywebview?.api;
      if(pending||!bridge?.exam_health)return;
      pending=true;clearTimeout(timer);
      const watchdog=setTimeout(()=>{if(alive){setFailed(true);publishNetworkStatus(null);}},15000);
      try {const health=await bridge.exam_health();if(alive){setStatus(health);publishNetworkStatus(health);setFailed(false);}}
      catch {if(alive){setFailed(true);publishNetworkStatus(null);}}
      finally {clearTimeout(watchdog);pending=false;if(alive)timer=setTimeout(check,10000);}
    };
    window.addEventListener('pywebviewready',check);check();
    return()=>{alive=false;clearTimeout(timer);window.removeEventListener('pywebviewready',check);};
  },[]);
  if((!status||!status.supported)&&!failed)return null;
  const problems=[];
  if(failed)problems.push('تعذر تأكيد حالة الشبكة؛ افتح الإعدادات وافحص الاتصال.');
  else if(status?.inspectionError)problems.push(status.message||'تعذر تأكيد حالة الشبكة؛ افتح الإعدادات وافحص الاتصال.');
  else if(status?.supported){
    if(status.ethernetDetail)problems.push(status.ethernetDetail);
    else if(!status.ethernetActive)problems.push('كابل شبكة الامتحان غير متصل.');
    else if(status.ethernetAddressReady===false||status.ethernetIp!=='10.77.0.1')problems.push('عنوان شبكة الامتحان غير موجود على منفذ الهاب. افحص تجهيز الماك.');
    if(!status.dhcpRunning)problems.push('خدمة توزيع عناوين الطلاب متوقفة.');
    else if((status.dhcpConfigurationReady===false||status.dhcpSocketReady!==true)&&status.dhcpDetail!==status.ethernetDetail)problems.push(status.dhcpDetail||'تعذر التحقق من استقبال طلبات توزيع العناوين.');
    if(!status.portalRunning)problems.push('خدمة صفحة الطلاب متوقفة.');
    if(status.ethernetActive&&status.dhcpRunning&&status.portalRunning&&status.ethernetIp==='10.77.0.1'&&!status.androidProbe?.ready)problems.push(status.androidProbe?.detail||'فحص الدخول وخدمة الامتحان لم ينجح. افتح الإعدادات للتفاصيل.');
  }
  if(status?.capport?.appliedMode!=='legacy'&&['expired','expiring','error','unreachable'].includes(status?.capport?.state))problems.push(status.capport.detail);
  if(['warning','invalid'].includes(status?.dhcpLeases?.status))problems.push(status.dhcpLeases.detail);
  if(status?.sleepProtection?.supported&&!status.sleepProtection.active)problems.push('حماية الماك من النوم غير مفعلة؛ اتركه مستيقظًا وراجع الإعدادات.');
  return <aside className="notice" role={problems.length?'alert':'status'} aria-label="حالة جهاز الامتحان">
    <strong>{problems.length?'تنبيه جهاز الامتحان':'جهاز الامتحان جاهز'}</strong>
    <p>{problems.length?problems.join(' '):'خدمات الشبكة المباشرة تعمل، وحماية النوم: '+(status?.sleepProtection?.active?'مفعلة':'غير متاحة')+'.'}</p>
    {problems.length>0&&<p>رجّع الاتصال قبل متابعة الامتحان، وراجع حالة حفظ الطلاب؛ إعادة تهيئة الشبكة توقف الاتصال مؤقتًا.</p>}
  </aside>;
}
