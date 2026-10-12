import React, {useState} from 'react';

const modeNames={legacy:'دخول محلي بدون شهادة',https:'دخول آمن بشهادة'};

export default function PortalConfiguration({status, disabled, refresh}) {
  const portal=status?.capport;
  const desiredMode=portal?.desiredMode;
  const appliedMode=portal?.appliedMode;
  const [hostname,setHostname]=useState('exam.massar-academy.net');
  const [selecting,setSelecting]=useState(false);
  const [savingMode,setSavingMode]=useState(false);
  const [message,setMessage]=useState('');
  const selectMode=async mode=>{
    setSavingMode(true);setMessage('');
    try {
      await window.pywebview.api.select_portal_mode(mode);
      await refresh();
      setMessage('تم حفظ الاختيار. طبّقه بزر تحديث تجهيز الماك بعد إغلاق القاعات.');
    } catch(error){setMessage(error.message||'تعذر حفظ طريقة الدخول.');}
    finally{setSavingMode(false);}
  };
  const choose=async()=>{
    setSelecting(true);setMessage('اختار ملف fullchain.pem أولًا، وبعده ملف privkey.pem.');
    try {
      const selected=await window.pywebview.api.select_portal_certificate(hostname);
      setMessage(selected.saved?'تم فحص الشهادة وحفظها. طبّقها بزر تحديث تجهيز الماك بعد إغلاق القاعات.':'أُلغِي اختيار الشهادة.');
      await refresh();
    } catch(error){setMessage(error.message||'تعذر حفظ الشهادة.');}
    finally{setSelecting(false);}
  };
  return <details className="notice"><summary>طريقة دخول الطلاب</summary>
    <p>المطبّق حاليًا على الشبكة: <strong>{modeNames[appliedMode]||'لم يتم التأكد من الإعداد'}</strong></p>
    <fieldset disabled={disabled||selecting||savingMode}><legend>الطريقة المختارة للتجهيز</legend>
      <label className="multiple-rooms-option"><input type="radio" name="portal-mode" value="legacy" checked={desiredMode==='legacy'} onChange={()=>selectMode('legacy')} />
        <span><strong>دخول محلي بدون شهادة</strong><small>يعمل بعنوان 10.77.0.1 ويحاول إظهار إشعار الدخول بالطريقة التقليدية. لا يحتاج شهادة أو تجديدًا.</small></span></label>
      <label className="multiple-rooms-option"><input type="radio" name="portal-mode" value="https" checked={desiredMode==='https'} onChange={()=>selectMode('https')} />
        <span><strong>دخول آمن بشهادة</strong><small>يضيف طريقة الاكتشاف المعيارية للأجهزة الداعمة، ويحتاج شهادة موثوقة صالحة وتجديدها قبل انتهائها.</small></span></label>
    </fieldset>
    {portal?.pendingModeChange&&<p role="status">الاختيار المحفوظ لم يُطبّق بعد. أغلق القاعات ثم اضغط «تحديث تجهيز الماك». حفظ الاختيار وحده لا يغيّر إعداد الشبكة أو رابط الدخول.</p>}
    <p role="status">{portal?.detail||'جارٍ قراءة طريقة دخول الطلاب.'}</p>
    {desiredMode==='https'&&<>
    {portal?.pendingCertificateChange&&<p role="status">الشهادة المحفوظة تنتظر التطبيق بزر «تحديث تجهيز الماك» بعد إغلاق القاعات. البوابة الحالية تستمر بشهادتها المطبّقة.</p>}
    <p>اسم الموقع يوجّه الطلاب إلى الماك داخل واي فاي الامتحان. عنوان كابل الامتحان ثابت: <b dir="ltr">10.77.0.1</b>.</p>
    <p>إصدار الشهادة وتجديدها يحتاجان إنترنت وإثبات ملكية الدومين. صفحة الامتحان تعمل محليًا. افحص ساعة الموبايلات وتاريخ انتهاء الشهادة؛ التجديد ليس تلقائيًا.</p>
    {portal?.installed&&appliedMode==='https'&&<p>العنوان المُجهز: <b dir="ltr">https://{portal.installed.hostname}/</b><br/>انتهاء الشهادة: {new Date(portal.installed.expiresAt).toLocaleDateString('ar-EG')}</p>}
    {portal?.candidate&&<p>الشهادة المحفوظة: <b dir="ltr">{portal.candidate.hostname}</b> · تنتهي {new Date(portal.candidate.expiresAt).toLocaleDateString('ar-EG')}</p>}
    <label>عنوان شبكة الامتحان<input dir="ltr" value={hostname} onChange={event=>setHostname(event.target.value)} autoCapitalize="none" spellCheck={false} placeholder="exam.massar-academy.net" /></label>
    <p className="help">اختار شهادة عامة موثوقة وكاملة fullchain.pem، ثم المفتاح المطابق privkey.pem. شهادة Cloudflare Origin وحدها لا تصلح للموبايلات.</p>
    <button className="button secondary" disabled={disabled||selecting||savingMode||!hostname.trim()} onClick={choose}>{selecting?'جارٍ اختيار وفحص الشهادة…':'اختيار شهادة البوابة وتجديدها'}</button>
    </>}
    {desiredMode==='legacy'&&<p>الدخول المحلي لا تنتهي صلاحيته بانتهاء شهادة. أي شهادة محفوظة تظل موجودة إذا اخترت تفعيل الدخول الآمن لاحقًا.</p>}
    {message&&<p role="status">{message}</p>}
    <p className="help">الطريقتان تعملان داخل واي فاي الامتحان بلا إنترنت. ظهور الإشعار يعتمد على الموبايل في الحالتين؛ اختبر الأجهزة واحتفظ بالرابط والرمز للدخول.</p>
  </details>;
}
