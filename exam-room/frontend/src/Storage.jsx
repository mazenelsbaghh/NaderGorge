import React, {useEffect, useState} from 'react';
import {api, copyText, number, toast} from '../../web/assets/common.js';
import NetworkSetup from './NetworkSetup.jsx';
const size = bytes => bytes >= 1024 ** 3 ? `${number(bytes / 1024 ** 3)} GB` :
  bytes >= 1024 ** 2 ? `${number(bytes / 1024 ** 2)} MB` : `${number(bytes / 1024)} KB`;
const date = seconds => new Date(seconds * 1000).toLocaleString('ar-EG', {dateStyle:'medium', timeStyle:'short'});

export default function Storage({token,onWhatsApp}) {
  const [details, setDetails] = useState(null);
  const [gemini, setGemini] = useState(null);
  const [key, setKey] = useState('');
  const [integrity, setIntegrity] = useState('يمكنك تشغيل فحص بنية الملف بدون تغيير البيانات.');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const load = async () => {
    try {const [a,b] = await Promise.all([api('/api/storage'), api('/api/settings/gemini')]);
      setDetails(a); setGemini(b); setError('');}
    catch (failure) {setError(failure.message);}
  };
  useEffect(() => {load();}, []);
  const perform = async action => {setBusy(true); try {await action();} catch (failure) {toast(failure.message, true);} finally {setBusy(false);}};
  if (error) return <div className="empty"><h2>تعذر قراءة بيانات التخزين</h2><p>{error}</p>
    <button className="button" onClick={load}>إعادة المحاولة</button></div>;
  if (!details || !gemini) return <div className="loading" role="status">جارٍ قراءة بيانات التخزين…</div>;
  return <>
    <div className="page-heading"><div><span className="context-label">كل شيء على جهازك</span>
      <h1>الإعدادات والبيانات</h1><p>مفتاح التصحيح وبيانات الامتحانات وإعدادات الواتساب محفوظة على هذا الكمبيوتر.</p></div>
      <button className="button" disabled={busy} onClick={() => perform(async () => {await api('/api/storage/backup', {}, token); await load(); toast('تم إنشاء نسخة احتياطية جديدة');})}>إنشاء نسخة احتياطية</button></div>
    <NetworkSetup />
    <section className="panel gemini-settings"><div className="section-top"><div><span className="context-label">إعدادات التصحيح</span>
      <h2>مفتاح خدمة التصحيح</h2><p className="help">أدخل المفتاح مرة واحدة لتشغيل تصحيح المقالي للجميع بعد انتهاء الامتحان.</p></div>
      <span className={`badge ${gemini.configured ? 'complete' : 'quiet'}`}>{gemini.configured ? 'المفتاح محفوظ' : 'لم يُضف مفتاح'}</span></div>
      <form onSubmit={event => {event.preventDefault();perform(async () => {await api('/api/settings/gemini',{apiKey:key.trim()},token);
        setKey(''); setGemini({...gemini,configured:true});toast('تم حفظ مفتاح التصحيح');});}}>
        <label>مفتاح API<input type="password" autoComplete="new-password" required value={key}
          onChange={event => setKey(event.target.value)} placeholder="الصق المفتاح الجديد هنا" /></label>
        <button className="button" type="submit" disabled={busy}>حفظ المفتاح</button></form>
      <p className="help">المفتاح يُحفظ في ملف <code dir="ltr">.env</code> داخل مجلد البيانات، خارج SQLite والنسخ الاحتياطية. لا يظهر بعد حفظه.</p>
      <label className="path-label">مكان ملف الإعدادات<input className="file-path" readOnly dir="ltr" value={gemini.path} /></label></section>
    <section className="panel"><div className="section-top"><div><h2>واتساب السنتر الرسمي</h2><p className="help">ربط الحساب، ومزامنة القوالب، ومتابعة إرسال تقارير الطلاب من قائمة واتساب السنتر.</p></div><button className="button secondary" onClick={onWhatsApp}>فتح إعدادات واتساب</button></div></section>
    <div className="storage-layout"><section className="panel storage-main"><div className="section-top"><div><h2>مكان حفظ البيانات</h2>
      <p className="help">قاعدة محلية مستقلة عن منصة مسار.</p></div><span className="badge complete">SQLite · محلي</span></div>
      <label className="path-label">ملف قاعدة البيانات<input className="file-path" readOnly dir="ltr" value={details.databasePath} /></label>
      <div className="actions"><button className="button secondary small" disabled={busy}
        onClick={() => perform(async () => {await api('/api/storage/open-folder',{},token);toast('تم فتح مجلد البيانات');})}>فتح مجلد البيانات ↗</button>
        <button className="button ghost small" onClick={() => copyText(details.databasePath).catch(error => toast(error.message,true))}>نسخ المسار</button></div>
      <dl className="storage-facts"><div><dt>حجم البيانات مع سجل الحفظ</dt><dd dir="ltr">{size(details.databaseBytes + details.pendingBytes)}</dd></div>
        <div><dt>المساحة المتاحة على القرص</dt><dd dir="ltr">{size(details.freeBytes)}</dd></div>
        <div><dt>الامتحانات المحفوظة</dt><dd>{number(details.counts.exams)}</dd></div>
        <div><dt>محاولات الطلاب / المسلّمة</dt><dd>{number(details.counts.students)} / {number(details.counts.submissions)}</dd></div></dl>
      <div className="integrity-row"><div><strong>فحص سلامة قاعدة البيانات</strong><p className="help" role="status">{integrity}</p></div>
        <button className="button secondary small" disabled={busy} onClick={() => perform(async () => {setIntegrity('جارٍ الفحص…');
          const result=await api('/api/storage/check',{},token);setIntegrity(result.healthy ? 'بنية قاعدة البيانات سليمة.' : 'الفحص اكتشف مشكلة؛ احتفظ بنسخة وراجع الدعم.');})}>تشغيل الفحص</button></div></section>
      <aside className="storage-explainer"><h2>ما الذي يتم حفظه؟</h2><ul><li>الأسئلة والإجابات النموذجية وإعدادات الوقت.</li>
        <li>بيانات الطلاب وإجاباتهم التي وصلت للكمبيوتر.</li><li>التسليم والدرجات وملاحظات التصحيح.</li></ul>
        <div className="storage-tip"><strong>احتفظ بنسخة خارج الكمبيوتر</strong><p>النسخ الموجودة هنا على نفس القرص. نزّل نسخة إلى فلاشة أو قرص آخر بعد كل جلسة.</p></div></aside></div>
    <section className="panel backups-panel"><div className="section-top padded"><div><h2>النسخ المحفوظة</h2>
      <p className="help">كل خمس دقائق أثناء التشغيل، وعند التشغيل والإغلاق وتغيير حالة الجلسة.</p></div>
      <span className="badge quiet">{number(details.backupCount)} نسخة · {size(details.backupBytes)}</span></div>
      <div className="table-scroll"><table><thead><tr><th>تاريخ النسخة</th><th>الحجم</th><th>الملف</th><th>تنزيل</th></tr></thead>
        <tbody>{details.backups.length ? details.backups.map((backup,index) => <tr key={backup.name}>
          <td><strong>{date(backup.createdAt)}</strong>{index===0 && <small>أحدث نسخة</small>}</td><td dir="ltr">{size(backup.bytes)}</td>
          <td><span className="backup-filename" dir="ltr">{backup.name}</span></td>
          <td><a className="button secondary small" download href={`/api/backups/${encodeURIComponent(backup.name)}`}>تنزيل النسخة ↓</a></td></tr>) :
          <tr><td className="empty" colSpan="4">لا توجد نسخ بعد. أنشئ أول نسخة الآن.</td></tr>}</tbody></table></div>
      <p className="help padded">نعرض أحدث ٣٠ نسخة. النسخ الأقدم تظل في مجلد backups.</p></section>
    <details className="restore-guide"><summary>إزاي أسترجع نسخة احتياطية؟</summary><ol>
      <li>نزّل النسخة المطلوبة وافتح مجلد البيانات، ثم أغلق البرنامج.</li><li>احتفظ بالمجلد الحالي باسم مختلف، وأنشئ مجلدًا فارغًا بنفس الاسم.</li>
      <li>ضع النسخة في المجلد الجديد باسم <b dir="ltr">exams.sqlite3</b>، ثم افتح البرنامج وراجع البيانات.</li></ol></details>
  </>;
}
