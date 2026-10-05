import React, {useEffect, useState} from 'react';
import {api, number} from '../../web/assets/common.js';
import SendReports from './SendReports.jsx';
import DownloadReports from './DownloadReports.jsx';
export default function Sheets({catalog,exams}) {
  const [group,setGroup]=useState(''); const [exam,setExam]=useState(''); const [data,setData]=useState(null); const [error,setError]=useState('');
  const groupLabel=g=>{const c=catalog.centers.find(c=>c.id===g.centerId); const grade=catalog.grades.find(i=>i.id===c?.gradeId); return [grade?.name,c?.name,g.name].filter(Boolean).join(' · ');};
  const available=exams.filter(e=>catalog.lessons.some(l=>l.id===e.lessonId&&l.groupId===group));
  useEffect(()=>{let active=true;setData(null);setError('');if(exam)api(`/api/exams/${exam}`).then(d=>{if(active)setData(d);}).catch(e=>{if(active)setError(e.message);});return()=>{active=false;};},[exam]);
  const joined=data?.attempts.filter(a=>a.joined_at)||[];
  return <><div className="page-heading"><div><span className="context-label">نتائج المجموعات</span><h1>شيتات</h1><p>اختر المجموعة وجلسة الامتحان لتنزيل النتائج في ملف Excel.</p></div></div>
    <section className="panel padded"><div className="launcher-grid"><label>المجموعة<select value={group} onChange={e=>{setGroup(e.target.value);setExam('');}}><option value="">اختر المجموعة</option>{catalog.groups.map(g=><option key={g.id} value={g.id}>{groupLabel(g)}</option>)}</select></label>
      <label>الامتحان<select disabled={!group} value={exam} onChange={e=>setExam(e.target.value)}><option value="">اختر جلسة الامتحان</option>{available.map(e=><option key={e.id} value={e.id}>{e.title} · {catalog.lessons.find(l=>l.id===e.lessonId)?.name} · {new Date(e.createdAt*1000).toLocaleString('ar-EG')}</option>)}</select></label></div>
      {group&&!available.length&&<p className="help">لا توجد جلسات امتحان مرتبطة بهذه المجموعة.</p>}{error&&<p className="inline-error">{error}</p>}
      {exam&&!data&&!error&&<p role="status">جارٍ قراءة النتائج…</p>}{data&&<><div className="actions"><a className="button" href={`/api/exams/${exam}/results.xlsx?groupId=${encodeURIComponent(group)}`}>تنزيل شيت Excel ↓</a><DownloadReports examId={exam} attempts={joined}/><SendReports attempts={joined} label="إرسال تقارير المجموعة عبر واتساب"/></div><p className="help">{number(joined.length)} طالب حضر. الشيت يوضح التسليم والدرجات المصححة والأسئلة المتبقية للتصحيح.</p></>}
    </section>{data&&<section className="panel table-scroll"><table><thead><tr><th>الطالب</th><th>الموبايل</th><th>الدرجة</th><th>الحالة</th><th>سبب الإلغاء</th><th>التقرير</th></tr></thead><tbody>{joined.map(a=><tr key={a.id}><td>{a.name}</td><td dir="ltr">{a.phone}</td><td>{!a.cancelled_at&&a.submitted_at?`${number(a.score)} من ${number(a.maximum)} (${number(a.maximum?100*a.score/a.maximum:0)}٪)`:'—'}</td><td>{a.cancelled_at?'ملغي':!a.submitted_at?'لم يسلّم':a.pending?`متبقي ${number(a.pending)} للتصحيح`:'تم التصحيح'}</td><td className="cancellation-reason">{a.cancelReason||'—'}</td><td>{!a.cancelled_at&&a.submitted_at&&a.pending===0?<a href={`/api/attempts/${a.id}/report.pdf`}>تنزيل PDF</a>:'التقرير غير جاهز'}</td></tr>)}{!joined.length&&<tr><td colSpan="6">لم يدخل طلاب هذه الجلسة.</td></tr>}</tbody></table></section>}</>;
}
