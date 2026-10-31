import React, {useState} from 'react';

const stateNames={not_configured:'لم يتم الربط',ready:'القراءة تعمل',partial:'البيانات غير مكتملة',unreachable:'الجهاز لا يستجيب',stale:'القراءة قديمة؛ القيم الحالية غير مؤكدة',auth_required:'أعد ربط بيانات الدخول',certificate_changed:'تغيرت شهادة الجهاز؛ راجع الربط',device_changed:'الجهاز المتصل مختلف',unsupported:'الإصدار غير مدعوم',error:'تعذر قراءة إعداد الربط'};
const display=value=>Number.isFinite(value)?value.toLocaleString('ar-EG',{maximumFractionDigits:1}):'غير متاح';
const rate=value=>Number.isFinite(value)?(value/1e6).toLocaleString('ar-EG',{maximumFractionDigits:3}):'غير متاح';

export default function OmadaStatistics({status,refresh,disabled}) {
  const monitor=status?.omada;
  const [host,setHost]=useState(null);
  const selectedHost=host??monitor?.host??'10.77.0.2';
  const [username,setUsername]=useState('');
  const [password,setPassword]=useState('');
  const [busy,setBusy]=useState(false);
  const [message,setMessage]=useState('');
  const save=async event=>{
    event.preventDefault();setBusy(true);setMessage('جارٍ التحقق من الجهاز وقراءة الإحصائيات…');
    try {
      const result=await window.pywebview.api.configure_omada_monitor(selectedHost,username,password);
      setMessage(result.message);
      if(result.ok){setPassword('');await refresh(true);}
    } catch {setMessage('تعذر الربط. راجع العنوان وبيانات الدخول.');}
    finally {setBusy(false);}
  };
  return <section className="notice" aria-label="إحصائيات Omada أثناء الامتحان">
    <strong>إحصائيات Omada أثناء الامتحان</strong>
    {monitor?.configured&&<p>عنوان الربط المحفوظ: <b dir="ltr">{monitor.host}</b></p>}
    <p role="status">{stateNames[monitor?.state]||'لم يتم التأكد من القراءة'}{monitor?.sampledAt&&<> · آخر محاولة قراءة: {new Date(monitor.sampledAt).toLocaleTimeString('ar-EG')}</>}</p>
    <p className="help">يسجل البرنامج كل ٣٠ ثانية أثناء فتحه، حتى خارج شاشة الإعدادات. التوقيت من الماك؛ أعداد المتصلين من نقطة الوصول، والحجوزات من DHCP معروضة منفصلة.</p>
    {monitor?.lastGoodAt&&!['ready','partial'].includes(monitor.state)&&<p>آخر قراءة متاحة كانت {new Date(monitor.lastGoodAt).toLocaleString('ar-EG')}؛ الأعداد الحالية غير مؤكدة.</p>}
    {['ready','partial'].includes(monitor?.state)&&<>
      <p>المعالج: {display(monitor.cpuPercent)}٪ · الذاكرة: {display(monitor.memoryPercent)}٪</p>
      <div style={{overflowX:'auto'}}><table><thead><tr><th>التردد</th><th>المتصلون</th><th>القناة</th><th>استقبال / إرسال Mbps</th><th>فقد / أخطاء منذ القراءة السابقة</th></tr></thead><tbody>
        {(monitor.radios||[]).map(radio=><tr key={radio.band}><td>{radio.band} GHz{radio.enabled===false&&' · متوقف'}</td><td>{display(radio.clients)}</td><td>{display(radio.channel)}</td>
          <td>{rate(radio.rxBitsPerSecond)} / {rate(radio.txBitsPerSecond)}</td>
          <td>{display(radio.delta?.rx_dropped)} / {display(radio.delta?.tx_dropped)} فقد · {display(radio.delta?.rx_errors)} / {display(radio.delta?.tx_errors)} أخطاء{radio.counterReset&&' · أُعيد ضبط العدادات'}</td></tr>)}
      </tbody></table></div>
      <p>كابل الشبكة: استقبال {rate(monitor.lan?.rxBitsPerSecond)} / إرسال {rate(monitor.lan?.txBitsPerSecond)} Mbps</p>
      <p>سرعة وصلة الكابل: {display(monitor.lan?.linkMbps)} Mbps{monitor.lan?.fullDuplex===true?' · Full Duplex':monitor.lan?.fullDuplex===false?' · Half Duplex':''}</p>
      <p>فقد حزم الكابل: استقبال {display(monitor.lan?.delta?.rx_dropped)} / إرسال {display(monitor.lan?.delta?.tx_dropped)} · الأخطاء: استقبال {display(monitor.lan?.delta?.rx_errors)} / إرسال {display(monitor.lan?.delta?.tx_errors)}</p>
      <p className="help">نسبة انشغال القناة اللاسلكية غير متاحة من واجهة الجهاز التي يدعمها الربط. سرعة نقل البيانات وحدها لا تقيس التداخل أو تثبت سبب المشكلة.</p>
    </>}
    <details><summary>ربط صفحة إدارة الجهاز</summary>
      <p>استخدم حساب إدارة الجهاز نفسه. الربط يقرأ الإحصائيات فقط، ولا يغير الترددات أو DHCP. يُحفظ اسم المستخدم ومشتق تسجيل الدخول وبصمة الشهادة محليًا بصلاحية خاصة؛ لا تدخل بيانات الربط في تقرير التشخيص.</p>
      <form onSubmit={save}><fieldset disabled={disabled||busy}>
        <label>عنوان Omada المحلي<input required value={selectedHost} onChange={event=>setHost(event.target.value)} dir="ltr" placeholder="10.77.0.2" /></label>
        <label>اسم مستخدم الإدارة<input required value={username} onChange={event=>setUsername(event.target.value)} autoComplete="off" dir="ltr" /></label>
        <label>كلمة سر إدارة الجهاز<input required type="password" value={password} onChange={event=>setPassword(event.target.value)} autoComplete="off" /></label>
        <button className="button secondary" disabled={!password||!username||busy}>{busy?'جارٍ الربط…':'التحقق وحفظ الربط على هذا الماك'}</button>
      </fieldset></form>
      <p className="help">جهّز الربط قبل الامتحان. إعادة تسجيل دخول الإدارة قد تنهي جلسة أخرى على بعض الإصدارات. بعد تغير العنوان أو الشهادة أو كلمة السر، أعد الربط هنا.</p>
    </details>
    {message&&<p role="status">{message}</p>}
  </section>;
}
