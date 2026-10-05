import React, {useEffect, useRef, useState} from 'react';
import {createRoot} from 'react-dom/client';
import {api, copyText, downloadBlob, confirmAction, number, timeLabel, stateLabels, toast} from '../../web/assets/common.js';
import {QrCode} from '../../web/assets/qr.js';
import Editor from './Editor.jsx';
import Storage from './Storage.jsx';
import WhatsApp from './WhatsApp.jsx';
import CenterSetup from './CenterSetup.jsx';
import Sheets from './Sheets.jsx';
import LiveTimer from './LiveTimer.jsx';
import {rosterFilters,screenFilters,rosterStatus,matchesRosterFilter,rosterProblem} from './rosterStatus.js';
import SelectedTime from './SelectedTime.jsx';
import CancelAttempt from './CancelAttempt.jsx';
import {rosterOrders,sortRoster,joinedLabel,prioritizeRoster} from './rosterOrder.js';
import RosterCards from './RosterCards.jsx';
import ScreenControls from './ScreenControls.jsx';
import AbsenceControls from './AbsenceControls.jsx';
import DownloadReports from './DownloadReports.jsx';
import SendReports from './SendReports.jsx';

const labels = {whatsapp:'واتساب السنتر',sheets:'شيتات',room:'قاعة السنتر',catalog:'تنظيم السنتر',exams:'مكتبة الامتحانات',storage:'الإعدادات والبيانات',guide:'دليل التشغيل',editor:'تجهيز امتحان'};
const activeStates = ['waiting','running'];
const badge = state => <span className={`badge ${state}`}>{stateLabels[state]}</span>;
function QR({url}) {
  const qr = QrCode.encodeText(url, QrCode.Ecc.MEDIUM);
  const cells = [];
  for (let y=0; y<qr.size; y++) for (let x=0; x<qr.size; x++)
    if (qr.getModule(x,y)) cells.push(`M${x+4},${y+4}h1v1h-1z`);
  return <svg className="qr" viewBox={`0 0 ${qr.size+8} ${qr.size+8}`} role="img" aria-label="رمز دخول الطلاب">
    <rect width="100%" height="100%" fill="white"/><path d={cells.join('')} fill="#0A1D3D"/></svg>;
}

function AdminApp() {
  const [bootstrap,setBootstrap] = useState(null);
  const [exams,setExams] = useState([]);
  const [templates,setTemplates] = useState([]);
  const [catalog,setCatalog] = useState({grades:[],centers:[],groups:[],lessons:[]});
  const [templateId,setTemplateId] = useState(null);
  const [dashboard,setDashboard] = useState(null);
  const [selectedId,setSelectedId] = useState(null);
  const [view,setView] = useState(new URLSearchParams(location.search).get('view')==='storage'?'storage':'room');
  const [editorConfig,setEditorConfig] = useState(null);
  const [editorDirty,setEditorDirty] = useState(false);
  const [busy,setBusy] = useState(false);
  const [connected,setConnected] = useState(true);
  const [startError,setStartError] = useState('');
  const tokenRef = useRef(null);
  const request = (path,payload) => api(path,payload,tokenRef.current);
  const list = async () => {const response=await api('/api/exams');setExams(response.exams);return response.exams;};
  const loadTemplates=async()=>{const response=await api('/api/templates');setTemplates(response.templates);return response.templates;};
  const loadCatalog=async()=>{const response=await api('/api/catalog');setCatalog(response);return response;};
  const loadExam = async examId => {const result=await api(`/api/exams/${examId}`);setDashboard(result);
    setSelectedId(examId);setView('room');setEditorDirty(false);};
  const run = async action => {if (busy) return;setBusy(true);try {await action();}catch (error){toast(error.message||'تعذر الاتصال بالجهاز',true);}finally{setBusy(false);}};
  const switchView = async next => {
    if (view==='editor' && editorDirty && !await confirmAction('مغادرة التعديلات بدون حفظ؟','التغييرات التي كتبتها لم تُحفظ.')) return;
    if(next==='sheets'){await Promise.all([list(),loadCatalog()]);} if (next==='exams') await loadTemplates();if(next==='catalog')await loadCatalog();setView(next);setEditorDirty(false);
  };
  useEffect(() => {let alive=true;(async()=>{try{
    const boot=await api('/api/bootstrap');if(!alive)return;tokenRef.current=boot.token;setBootstrap(boot);
    const [rows,templateRows,catalogRows]=await Promise.all([api('/api/exams'),api('/api/templates'),api('/api/catalog')]);if(!alive)return;setExams(rows.exams);setTemplates(templateRows.templates);setCatalog(catalogRows);
    const sessions=rows.exams;
    if(sessions.length){const chosen=sessions.find(exam=>activeStates.includes(exam.state))||sessions[0];
      const result=await api(`/api/exams/${chosen.id}`);if(alive){setDashboard(result);setSelectedId(chosen.id);}}
  }catch(error){if(alive)setStartError(error.message);}})();return()=>{alive=false;};},[]);
  useEffect(()=>{document.querySelectorAll('.nav-item').forEach(button=>{
    button.classList.toggle('active',button.dataset.view===view);
  });const page=document.getElementById('page-location');if(page)page.textContent=labels[view];},[view]);
  useEffect(()=>{const navigation=event=>{const button=event.target.closest('.nav-item');if(button)run(()=>switchView(button.dataset.view));};
    const nav=document.querySelector('.sidebar nav');nav?.addEventListener('click',navigation);return()=>nav?.removeEventListener('click',navigation);
  },[view,editorDirty,busy]);
  useEffect(()=>{const backup=document.getElementById('backup');if(!backup)return;
    const click=()=>run(async()=>{const response=await fetch('/api/backup',{method:'POST',headers:{'Content-Type':'application/json','X-Exam-Request':'1','X-Admin-Token':tokenRef.current},body:'{}'});
      if(!response.ok)throw new Error((await response.json()).error);downloadBlob(await response.blob(),`massar-backup-${new Date().toISOString().slice(0,10)}.sqlite3`);toast('تم تجهيز النسخة الاحتياطية');});
    backup.addEventListener('click',click);return()=>backup.removeEventListener('click',click);
  },[busy]);
  useEffect(()=>{if(!bootstrap)return;let live=true;const tick=async()=>{try{
    const status=await api('/api/status');
    if(live&&Array.isArray(status.studentUrls))setBootstrap(current=>{
      if(!current||JSON.stringify(current.studentUrls)===JSON.stringify(status.studentUrls))return current;
      return {...current,studentUrls:status.studentUrls};
    });
    if(view==='room'&&selectedId&&!busy){const result=await api(`/api/exams/${selectedId}`);
      if(live){if(dashboard&&result.exam.state!==dashboard.exam.state)await list();setDashboard(result);}}
    if(live)setConnected(true);
  }catch{if(live)setConnected(false);}};const timer=setInterval(tick,3000);return()=>{live=false;clearInterval(timer);};},[bootstrap,view,selectedId,busy,dashboard?.exam.state]);
  useEffect(()=>{const element=document.getElementById('admin-connection');if(!element)return;
    element.classList.toggle('offline',!connected);element.textContent=connected?'● متصل بجهاز الإدارة':'انقطع الاتصال بجهاز الإدارة';},[connected]);
  useEffect(()=>{const before=event=>{if(view==='editor'&&editorDirty){event.preventDefault();event.returnValue='';}};
    window.addEventListener('beforeunload',before);return()=>window.removeEventListener('beforeunload',before);},[view,editorDirty]);
  const saveExam=async config=>{setBusy(true);try{await request(templateId?`/api/templates/${templateId}/save`:'/api/templates',config);
    await loadTemplates();setView('exams');setEditorDirty(false);toast('تم حفظ الامتحان في المكتبة');}
    finally{setBusy(false);}};
  const openRoom=async(lessonId,selectedTemplateId)=>{setBusy(true);try{const created=await request('/api/room/open',{lessonId,templateId:selectedTemplateId});
    await list();await loadExam(created.id);toast('تم فتح قاعة السنتر للحصة المختارة');}
    catch(error){toast(error.message,true);}finally{setBusy(false);}};
  const changeState=action=>run(async()=>{if(action==='start'&&!await confirmAction('بدء الامتحان الآن؟','سيبدأ الوقت لكل الطلاب الموجودين. تأكد أن الطلاب مستعدون.'))return;
    if(action==='close'&&!await confirmAction('إنهاء هذه القاعة؟','ستُقفل محاولات طلاب هذه القاعة فقط، وتُعتمد آخر إجابات وصلت للجهاز.'))return;
    await request(`/api/exams/${selectedId}/${action}`,{});await list();await loadExam(selectedId);
    toast(action==='start'?'بدأ الامتحان':action==='publish'?'قاعة الانتظار مفتوحة':'انتهى الامتحان وحُفظت الإجابات');});
  const edit=template=>{setTemplateId(template?.id||null);setEditorConfig(template?.config||null);setView('editor');setEditorDirty(false);};
  if(startError)return <div className="empty"><h1>تعذر فتح الإدارة</h1><p>{startError}</p><a className="button" href="/">إعادة المحاولة</a></div>;
  if(!bootstrap)return <div className="loading">جارٍ فتح قاعة السنتر…</div>;
  if(view==='sheets')return <Sheets catalog={catalog} exams={exams}/>;
  if(view==='storage')return <Storage token={bootstrap.token} onWhatsApp={()=>setView('whatsapp')}/>;
  if(view==='whatsapp')return <WhatsApp token={bootstrap.token}/>;
  if(view==='guide')return <Guide/>;
  if(view==='catalog')return <CenterSetup catalog={catalog} request={request} refresh={loadCatalog}/>;
  if(view==='editor')return <Editor key={templateId||'new'} config={editorConfig} onDirty={setEditorDirty}
    onSave={saveExam} onCancel={()=>switchView('exams')}/>;
  if(view==='exams')return <Library templates={templates} onEdit={edit} onNew={()=>edit(null)}/>;
  if(!dashboard)return <RoomLauncher catalog={catalog} templates={templates} exams={exams} onOpen={openRoom} onHistory={loadExam} busy={busy} request={request}/>;
  return <Room key={selectedId} dashboard={dashboard} bootstrap={bootstrap} busy={busy} onState={changeState}
    request={request} refresh={()=>loadExam(selectedId)} launcher={<RoomLauncher catalog={catalog} templates={templates} exams={exams} onOpen={openRoom} onHistory={loadExam} busy={busy} request={request} currentId={selectedId}/>}/>;
}
function Library({templates,onEdit,onNew}) {
  const [search,setSearch]=useState('');
  const shown=templates.filter(item=>String(item.config.title||'').toLowerCase().includes(search.toLowerCase()));
  return <><div className="page-heading"><div><span className="context-label">جهّز الامتحان مرة واستخدمه في أي حصة</span><h1>مكتبة الامتحانات</h1>
    <p>كل امتحان محفوظ باسمه وأسئلته. تختار الحصة والامتحان عند فتح قاعة السنتر.</p></div><button className="button" onClick={onNew}>＋ إنشاء امتحان</button></div>
    <section className="panel library-panel"><div className="library-toolbar"><label className="search-field"><span className="sr-only">بحث في الامتحانات</span>
      <input placeholder="ابحث باسم الامتحان…" value={search} onChange={event=>setSearch(event.target.value)}/></label>
      <span className="muted">{number(templates.length)} امتحان محفوظ</span></div>
      <div className="exam-list">{shown.length?shown.map(item=><button className="exam-list-row" key={item.id} onClick={()=>onEdit(item)}>
        <div><strong>{item.config.title}</strong><small>{number(item.config.questions?.length||0)} سؤال · {number(item.config.minutes)} دقيقة</small></div>
        <span className="exam-open-label">فتح وتعديل ←</span></button>):
        <div className="empty"><h2>لا يوجد امتحان بهذا الاسم</h2><p>أنشئ امتحانًا جديدًا أو غيّر البحث.</p></div>}</div></section></>;
}
function RoomLauncher({catalog,templates,exams,onOpen,onHistory,busy,currentId,request}) {
  const [grade,setGrade]=useState('');const [center,setCenter]=useState('');const [group,setGroup]=useState('');
  const [lesson,setLesson]=useState('');const [template,setTemplate]=useState('');
  const by=(kind,key,value)=>(catalog[kind]||[]).filter(item=>item[key]===value);
  const active=exams.filter(item=>activeStates.includes(item.state));
  const [multiple,setMultiple]=useState(null);const [settingBusy,setSettingBusy]=useState(false);
  useEffect(()=>{let alive=true;api('/api/settings/rooms').then(s=>{if(alive)setMultiple(s.multipleRooms);}).catch(e=>toast(e.message,true));return()=>{alive=false;};},[]);
  const changeMultiple=async enabled=>{setSettingBusy(true);try{const settings=await request('/api/settings/rooms',{multipleRooms:enabled});setMultiple(settings.multipleRooms);toast(enabled?'تم تفعيل تشغيل أكثر من قاعة':'تم الرجوع لقاعة واحدة');}catch(e){toast(e.message,true);}finally{setSettingBusy(false);}};
  const label=id=>{const l=catalog.lessons?.find(item=>item.id===id);if(!l)return 'جلسة قديمة';
    const g=catalog.groups?.find(item=>item.id===l.groupId);const c=catalog.centers?.find(item=>item.id===g?.centerId);
    return [c?.name,g?.name,l.name].filter(Boolean).join(' · ');};
  return <><div className="page-heading"><div><span className="context-label">قاعة السنتر</span><h1>اختر الحصة والامتحان</h1>
    <p>الامتحانات تُجهّز في المكتبة. هنا تربط نسخة من الامتحان بالحصة وتفتحها للطلاب.</p></div></div>
    <section className="panel launcher-panel"><div className="section-top"><div><h2>فتح جلسة جديدة</h2><p className="help">{multiple?'يمكن فتح أكثر من قاعة؛ الطلاب الجدد يختارون القاعة عند وجود قاعتين أو أكثر.':'يمكن فتح جلسة واحدة في الوقت نفسه.'}</p></div>
      {active.length>0&&<span className="badge waiting">{number(active.length)} قاعة مفتوحة</span>}</div>
      <label className="multiple-rooms-option"><input type="checkbox" checked={multiple===true} disabled={busy||settingBusy||multiple===null} onChange={e=>changeMultiple(e.target.checked)}/><span><strong>تشغيل أكثر من قاعة في نفس الوقت</strong><small>قاعة واحدة: دخول مباشر. أكثر من قاعة: اختيار إجباري للطلاب الجدد. الطلاب الموجودون يظلون في قاعتهم.</small></span></label>
      <div className="launcher-grid"><label>الصف الدراسي<select value={grade} onChange={e=>{setGrade(e.target.value);setCenter('');setGroup('');setLesson('');}}><option value="">اختر الصف</option>{catalog.grades?.map(i=><option key={i.id} value={i.id}>{i.name}</option>)}</select></label>
        <label>السنتر<select value={center} disabled={!grade} onChange={e=>{setCenter(e.target.value);setGroup('');setLesson('');}}><option value="">اختر السنتر</option>{by('centers','gradeId',grade).map(i=><option key={i.id} value={i.id}>{i.name}</option>)}</select></label>
        <label>المجموعة<select value={group} disabled={!center} onChange={e=>{setGroup(e.target.value);setLesson('');}}><option value="">اختر المجموعة</option>{by('groups','centerId',center).map(i=><option key={i.id} value={i.id}>{i.name}</option>)}</select></label>
        <label>الحصة<select value={lesson} disabled={!group} onChange={e=>setLesson(e.target.value)}><option value="">اختر الحصة</option>{by('lessons','groupId',group).map(i=><option key={i.id} value={i.id}>{i.name}</option>)}</select></label>
        <label>اسم الامتحان<select value={template} onChange={e=>setTemplate(e.target.value)}><option value="">اختر الامتحان</option>{templates.map(i=><option key={i.id} value={i.id}>{i.config.title}</option>)}</select></label></div>
      <div className="actions launcher-actions"><button className="button" disabled={busy||settingBusy||multiple===null||!lesson||!template||(!multiple&&active.length>0)} onClick={()=>onOpen(lesson,template)}>فتح قاعة السنتر ←</button>
        {(!catalog.grades?.length||!catalog.lessons?.length)&&<span className="help">أضف الصف والسنتر والمجموعة والحصة من «تنظيم السنتر» أولًا.</span>}
        {!templates.length&&<span className="help">أنشئ امتحانًا من «مكتبة الامتحانات» أولًا.</span>}</div></section>
    {exams.length>0&&<section className="panel history-panel"><div className="padded section-top"><h2>سجل الجلسات والنتائج</h2><span className="muted">{number(exams.length)} جلسة</span></div>
      {exams.map(item=><button className={`exam-list-row ${item.id===currentId?'selected':''}`} key={item.id} onClick={()=>onHistory(item.id)}>
        <div><strong>{item.title}</strong><small>{label(item.lessonId)} · {new Date(item.createdAt*1000).toLocaleDateString('ar-EG')} · {number(item.submitted)} تسليم</small></div>{badge(item.state)}
        <span className="exam-open-label">عرض ←</span></button>)}</section>}</>;
}
function Room({dashboard,bootstrap,busy,onState,request,refresh,launcher}) {
  const exam=dashboard.exam;
  const [tab,setTab]=useState(exam.state==='closed'?'results':'students');
  const [chosenUrl,setChosenUrl]=useState('');
  useEffect(()=>{if(exam.state==='closed')setTab('results');},[exam.state]);
  const joined=dashboard.attempts.filter(attempt=>attempt.joined_at);
  const online=joined.filter(attempt=>dashboard.serverTime-attempt.last_seen<15).length;
  const submitted=joined.filter(attempt=>!attempt.cancelled_at&&attempt.submitted_at).length;
  const pending=joined.filter(attempt=>!attempt.cancelled_at&&attempt.submitted_at&&attempt.pending>0).length;
  const titles={draft:'جهّز امتحانك، ثم افتح القاعة.',waiting:'القاعة جاهزة لاستقبال الطلاب.',running:'الامتحان جارٍ. تابع القاعة.',closed:'انتهى الامتحان. راجع النتائج.'};
  const studentUrl=bootstrap.studentUrls.includes(chosenUrl)?chosenUrl:bootstrap.studentUrls[0];
  return <>{launcher}<div className="page-heading room-current-heading"><div><span className="context-label">{titles[exam.state]}</span>
    <h1>{exam.config.title}</h1><div className="exam-meta">{badge(exam.state)}<span>{number(exam.config.questionCount)} أسئلة</span>
      <span>{number(exam.config.minutes)} دقيقة</span><span>{exam.config.timerMode==='shared'?'نهاية موحدة':'وقت مستقل لكل طالب'}</span></div></div>
    <div className="actions">{exam.state==='draft'&&<button className="button" disabled={busy} onClick={()=>onState('publish')}>فتح قاعة الانتظار ←</button>}
      {exam.state==='waiting'&&<button className="button" disabled={busy} onClick={()=>onState('start')}>ابدأ الامتحان للجميع ←</button>}
      {activeStates.includes(exam.state)&&<button className="button secondary danger-text" disabled={busy} onClick={()=>onState('close')}>إنهاء الجلسة</button>}
</div></div>
    {exam.state==='running'&&<LiveTimer dashboard={dashboard} request={request} refresh={refresh}/>}
    {activeStates.includes(exam.state)&&<><AbsenceControls exam={exam} request={request} refresh={refresh}/><ScreenControls exam={exam} request={request} refresh={refresh}/></>}
    <ol className="exam-steps" aria-label="مراحل الامتحان">{[['draft','تجهيز الأسئلة'],['waiting','استقبال الطلاب'],['running','حل الامتحان'],['closed','مراجعة النتائج']].map(([state,label],index)=><li className={exam.state===state?'current':''} key={state}><span>{number(index+1)}</span>{label}</li>)}</ol>
    <div className={`room-overview room-state-${exam.state}`}><section className="join-panel"><div><span className="context-label">دخول الطلاب</span>
      <h2>رابط دخول الطلاب</h2><p>الطالب يتصل بالواي فاي، يمسح الرمز، ثم يدخل اسمه ورقمه وكوده.</p>
      {bootstrap.studentUrls.length>1&&<label>اختر عنوان الشبكة المتصل بها الطلاب
        <select value={studentUrl} onChange={event=>setChosenUrl(event.target.value)}>
          {bootstrap.studentUrls.map(url=><option key={url} value={url}>{url}</option>)}
        </select></label>}
      {studentUrl?<div className="link-field"><input readOnly dir="ltr" value={studentUrl}/><button className="button secondary small" onClick={()=>copyText(studentUrl).catch(error=>toast(error.message,true))}>نسخ الرابط</button></div>:
        <p className="inline-error">لا يوجد عنوان شبكة محلية للكمبيوتر. اتصل بشبكة الهوت سبوت أو الراوتر أولًا.</p>}
      <a className="text-link" href={bootstrap.localStudentUrl} target="_blank" rel="noopener">فتح صفحة طالب على هذا الجهاز ↗</a>
      <p className="help">شارك الرابط بعد اتصال الطلاب بنفس الشبكة. إذا كان الهوت سبوت بلا إنترنت، يختار الطالب «البقاء متصلًا بالشبكة» ويوقف التحويل التلقائي لبيانات الهاتف، ويفتح الرابط بصيغة http://. يتحدث العنوان تلقائيًا عند تغيير الشبكة.</p></div>{studentUrl&&<QR url={studentUrl}/>}</section>
      <section className="attendance-summary" aria-label="ملخص الحضور"><div><span>دخل القاعة</span><strong>{number(joined.length)}</strong></div>
        <div><span>متصل الآن</span><strong>{number(online)}</strong></div><div><span>سلّم الامتحان</span><strong>{number(submitted)}</strong></div>
        <div><span>ينتظر تصحيح المقالي</span><strong>{number(pending)}</strong></div></section></div>
    <section className="panel roster-panel"><div className="panel-toolbar"><div className="tabs" role="group" aria-label="عرض القاعة">
      {[['students','الطلاب والحضور'],['questions','الأسئلة'],['results','التصحيح والنتائج']].map(([value,label])=><button
        key={value} className={tab===value?'active':''} onClick={()=>setTab(value)}>{label}</button>)}</div>
      <span className="muted">{number(dashboard.attempts.length)} طالب في الجلسة</span></div>
      {tab==='questions'?<Questions exam={exam}/>:
        <Roster key={tab} dashboard={dashboard} tab={tab} request={request} refresh={refresh} busy={busy}/>}</section>
  </>;
}
function Questions({exam}) {return <><div className="section-top padded"><p className="muted">
  الأسئلة نسخة محفوظة لهذه الجلسة. أي تعديل في المكتبة يؤثر في الجلسات الجديدة فقط.</p>
</div>
  <div className="question-previews">{exam.config.questions.map((question,index)=><article key={question.id}>
    <div className="section-top"><h3>{number(index+1)}. {question.text}</h3><span className="badge quiet">{number(question.points)} درجات</span></div>
    <p className="model-answer-preview">الإجابة النموذجية: {question.kind==='mcq'?question.options[question.correct]:question.modelAnswer}</p></article>)}</div></>}
function Roster({dashboard,tab,request,refresh,busy}) {
  const [search,setSearch]=useState('');const [order,setOrder]=useState('newest');const [problemsFirst,setProblemsFirst]=useState(true);const [filter,setFilter]=useState('all');const [reviewId,setReviewId]=useState(null);const [cancelTarget,setCancelTarget]=useState(null);const [selectedIds,setSelectedIds]=useState([]);
  const exam=dashboard.exam;
  const query=search.toLowerCase().trim();
  const searched=dashboard.attempts.filter(a=>`${a.name||''} ${a.code} ${a.phone||''}`.toLowerCase().includes(query));
  const statusFor=a=>rosterStatus(a,exam,dashboard.serverTime);
  const showScreenCards=exam.config.screenGuard===true||dashboard.attempts.some(a=>a.screen);
  const cardFilters=showScreenCards?[...rosterFilters,...screenFilters]:rosterFilters;
  const counts=Object.fromEntries(cardFilters.map(([key])=>[key,searched.filter(a=>matchesRosterFilter(a,statusFor(a),key)).length]));
  const currentFilter=cardFilters.some(([key])=>key===filter)?filter:'all';
  const ordered=sortRoster(searched.filter(a=>matchesRosterFilter(a,statusFor(a),currentFilter)),order);
  const problemFor=a=>rosterProblem(a,exam,dashboard.serverTime);
  const problemsCount=ordered.filter(problemFor).length;
  const attempts=problemsFirst?prioritizeRoster(ordered,problemFor):ordered;
  const canExtend=a=>exam.state==='running'&&a.joined_at&&!a.cancelled_at&&!a.submitted_at&&a.deadline>dashboard.serverTime;
  const selected=dashboard.attempts.filter(a=>selectedIds.includes(a.id)&&canExtend(a));
  const visibleEligible=attempts.filter(canExtend);
  const toggle=(id,checked)=>setSelectedIds(ids=>checked?[...new Set([...ids,id])]:ids.filter(value=>value!==id));
  const reset=async attempt=>{if(!await confirmAction('استعادة دخول الطالب؟','سيتم تسجيل خروج الجلسة القديمة. يدخل الطالب بنفس الكود والرقم، وتبقى الإجابات والوقت كما هما.'))return;
    try{await request(`/api/attempts/${attempt.id}/reset-login`,{});toast('يمكن للطالب الدخول مجددًا بنفس الكود والرقم');await refresh();}
    catch(error){toast(error.message,true);}};
  return <div id="room-content"><div className="roster-tools"><label className="search-field"><span className="sr-only">بحث بالاسم أو الكود</span>
    <input placeholder="ابحث باسم الطالب أو كوده…" value={search} onChange={event=>setSearch(event.target.value)}/></label>
    <label className="roster-order">ترتيب الطلاب<select value={order} onChange={event=>setOrder(event.target.value)}>{rosterOrders.map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label>
    <label className="roster-priority" title="الموقوف، خارج الصفحة، غير المتصل، أو عنده تنبيه شاشة حالي"><input type="checkbox" checked={problemsFirst} onChange={event=>setProblemsFirst(event.target.checked)}/>المشاكل أولًا <span className="badge quiet">{number(problemsCount)}</span></label>
    <a className="button secondary small" download href={`/api/exams/${exam.id}/${tab==='students'?'students.csv':'results.csv'}`}>
      {tab==='students'?'تنزيل كشف الطلاب ↓':'تنزيل النتائج CSV ↓'}</a></div>
    {tab==='results'&&<><p className="results-note">الاختيارات تُصحح تلقائيًا. راجع المقالي قبل طباعة التقارير. الدرجات لا تظهر للطلاب.</p>
      <EssayBatch exam={exam} dashboard={dashboard} request={request} refresh={refresh}/></>}
    {tab==='results'&&<div className="whatsapp-send-row"><DownloadReports examId={exam.id} attempts={dashboard.attempts}/><SendReports attempts={dashboard.attempts.filter(a=>a.joined_at)} label="إرسال تقارير كل الطلاب عبر واتساب"/></div>}
    <RosterCards counts={counts} filter={currentFilter} onChange={setFilter} searching={Boolean(query)} showScreenCards={showScreenCards}/>
    {exam.state==='running'&&<SelectedTime examId={exam.id} attempts={selected} request={request} refresh={refresh} clear={()=>setSelectedIds([])}/>}
    <div className="table-scroll"><table><thead><tr>{exam.state==='running'&&<th><input type="checkbox" aria-label="تحديد الطلاب الظاهرين المتاح تمديدهم" disabled={!visibleEligible.length} checked={visibleEligible.length>0&&visibleEligible.every(a=>selectedIds.includes(a.id))} onChange={e=>setSelectedIds(ids=>e.target.checked?[...new Set([...ids,...visibleEligible.map(a=>a.id)])]:ids.filter(id=>!visibleEligible.some(a=>a.id===id)))}/></th>}<th>الطالب</th><th>كود الطالب</th><th>الحالة</th>
      <th>{tab==='results'?'الدرجة':'آخر ظهور'}</th><th>الإجراء</th></tr></thead><tbody>
      {attempts.length?attempts.map(attempt=>{
        const state=statusFor(attempt);const problem=problemFor(attempt);
        const seen=attempt.last_seen?(dashboard.serverTime-attempt.last_seen<15?'الآن':`منذ ${number(Math.floor((dashboard.serverTime-attempt.last_seen)/60))} دقيقة`):'—';
        return <tr key={attempt.id} className={problem?'roster-problem-row':''}>{exam.state==='running'&&<td><input type="checkbox" aria-label={`تحديد ${attempt.name} لتمديد الوقت`} disabled={!canExtend(attempt)} checked={canExtend(attempt)&&selectedIds.includes(attempt.id)} onChange={e=>toggle(attempt.id,e.target.checked)}/></td>}<td><strong>{attempt.name||'طالب'}</strong><small dir="ltr">{attempt.phone||'لم تُسجل بياناته'}</small><small>الدخول: {joinedLabel(attempt.joined_at)}</small></td>
          <td><button className="code-button" title="نسخ الكود" onClick={()=>copyText(attempt.code).catch(error=>toast(error.message,true))}>{attempt.code}</button></td>
          <td><span className={`badge ${state.tone}`}>{state.label}</span>{problem&&<small className="roster-problem-note">يحتاج متابعة: {problem}</small>}{canExtend(attempt)&&<small>الوقت المتبقي: <b dir="ltr">{timeLabel(Math.ceil(Math.max(0,attempt.deadline-dashboard.serverTime)))}</b></small>}{attempt.cancelled_at&&<small className="cancellation-reason">سبب الإلغاء: {attempt.cancelReason}</small>}{attempt.screen&&<small className="screen-details">علامة المتصفح: <b dir="ltr">{attempt.screen.current.deviceId.slice(0,8)}</b><br/>الشاشة: <b dir="ltr">{attempt.screen.current.screenWidth} × {attempt.screen.current.screenHeight}</b> · العرض: <b dir="ltr">{attempt.screen.current.width} × {attempt.screen.current.height}</b><br/>{attempt.screen.current.fullscreenSupported?(attempt.screen.current.fullscreen?'ملء الشاشة نشط':'خارج ملء الشاشة'):'ملء الشاشة غير مدعوم'}{attempt.screen.lastEvent&&<><br/>آخر تنبيه: {attempt.screen.lastEvent} · {number(attempt.screen.events)} مرة</>}</small>}{exam.config.screenGuard&&!attempt.screen&&!attempt.submitted_at&&!attempt.cancelled_at&&<small>في انتظار موافقة مراقبة الشاشة</small>}{attempt.departures>0&&<small>غادر الصفحة {number(attempt.departures)} مرة</small>}</td>
          <td>{tab==='results'?(attempt.cancelled_at?'ملغي':attempt.submitted_at?<>{number(attempt.score)} / {number(attempt.maximum)}
            {attempt.pending>0&&<small>متبقي {number(attempt.pending)} مقالي</small>}</>:'لم يسلّم'):seen}</td>
          <td>{!attempt.cancelled_at&&tab==='results'&&attempt.submitted_at?<button className="text-button" onClick={()=>setReviewId(attempt.id)}>مراجعة وتصحيح ←</button>:
            !attempt.cancelled_at&&attempt.joined_at&&!attempt.submitted_at?<button className="text-button" disabled={busy} onClick={()=>reset(attempt)}>استعادة الدخول</button>:
            <span className="muted">—</span>}{!attempt.cancelled_at&&exam.state==='running'&&attempt.joined_at&&!attempt.submitted_at&&<button className="text-button" disabled={busy} onClick={async()=>{try{await request(`/api/attempts/${attempt.id}/${attempt.paused?'resume':'pause'}`,{});await refresh();toast(attempt.paused?'تم استكمال الطالب':'تم إيقاف الطالب');}catch(e){toast(e.message,true);}}}>{attempt.paused?'استكمال الطالب':'إيقاف الطالب'}</button>}{!attempt.cancelled_at&&attempt.joined_at&&<button className="text-button cancellation-action" disabled={busy} onClick={()=>setCancelTarget(attempt)}>إلغاء المحاولة</button>}</td></tr>;
      }):<tr><td colSpan={exam.state==='running'?6:5} className="empty">{search||filter!=='all'?'لا يوجد طلاب مطابقون للبحث أو التصفية.':'الطالب يظهر هنا بعد دخوله باسمه ورقمه وكوده.'}</td></tr>}</tbody></table></div>
    {cancelTarget&&<CancelAttempt attempt={cancelTarget} request={request} refresh={refresh} onClose={()=>setCancelTarget(null)}/>}{reviewId&&<Review attempt={dashboard.attempts.find(a=>a.id===reviewId)} exam={exam} request={request}
      onClose={()=>setReviewId(null)} refresh={refresh}/>}</div>;
}
function EssayBatch({exam,dashboard,request,refresh}) {
  const [partialCredit,setPartialCredit]=useState(false);
  const [status,setStatus]=useState(null);const [showKey,setShowKey]=useState(false);const [key,setKey]=useState('');const [busy,setBusy]=useState(false);
  const pending=dashboard.attempts.reduce((sum,attempt)=>sum+(attempt.cancelled_at?0:attempt.pending||0),0);
  useEffect(()=>{let active=true;request(`/api/exams/${exam.id}/essay-batch`).then(result=>{if(active)setStatus(result);})
    .catch(error=>{if(active)toast(error.message,true);});return()=>{active=false;};},[exam.id,dashboard.serverTime]);
  const launch=async payload=>{setBusy(true);try{await request(`/api/exams/${exam.id}/essay-batch`,{...payload,partialCredit});
    setShowKey(false);setKey('');toast('بدأ تصحيح المقالي. يمكن متابعة التقدم هنا.');
    setStatus(await request(`/api/exams/${exam.id}/essay-batch`));}
    catch(error){toast(error.message,true);}finally{setBusy(false);}};
  const job=status?.job;const running=job?.state==='running';
  const summary=!job?`${number(pending)} إجابة مقالية تنتظر التصحيح.`:
    `${{running:'التصحيح جارٍ',completed:'اكتمل التصحيح',partial:'اكتمل مع إجابات للمراجعة',interrupted:'توقف التصحيح'}[job.state]||'حالة التصحيح'} · ${number(job.processed)} من ${number(job.total)} سؤال · ${number(job.graded)} صُحح · ${number(job.skipped)} كان مصححًا · ${number(job.needs_review+job.failed)} يحتاج مراجعة. ${job.message||''}`;
  return <section className="essay-batch-panel" aria-label="التصحيح الجماعي للمقالي"><div className="section-top"><div>
    <span className="context-label">تصحيح جماعي</span><h2>المقالي لكل الطلاب</h2>
    <p className="help">يصحح الإجابات المسلّمة غير المصححة فقط. يمكنك اختيار درجات جزئية وتعديل كل درجة يدويًا.</p></div>
    <button className="button" disabled={exam.state!=='closed'||running||pending===0||busy||!status}
      onClick={()=>status?.keyConfigured?launch({}):setShowKey(true)}>تصحيح المقالي للجميع ✦</button></div>
    <label className="check-label"><input type="checkbox" checked={partialCredit} disabled={running||busy} onChange={e=>setPartialCredit(e.target.checked)}/>منح درجات جزئية للأجزاء الصحيحة في المقالي</label><p className="help">{partialCredit?'توزيع درجة السؤال على الأجزاء المطلوبة، مع قبول الإجابات بنفس المعنى.':'التقدير الحالي: صح أو غلط فقط.'} ينطبق على الإجابات التي لم تُصحح بعد.</p>
    <p className="help" role="status">{status?summary:'جارٍ قراءة حالة التصحيح…'} {number(pending)} إجابة ما زالت بدون درجة.
      {running&&<button className="text-button" onClick={async()=>{try{await request(`/api/exams/${exam.id}/essay-batch/stop`,{});toast('سيقف التصحيح بعد السؤال الحالي');}
        catch(error){toast(error.message,true);}}}>إيقاف بعد السؤال الحالي</button>}</p>
    {showKey&&<form onSubmit={event=>{event.preventDefault();launch({apiKey:key.trim()});}}><label>مفتاح خدمة التصحيح
      <input type="password" autoComplete="off" value={key} onChange={event=>setKey(event.target.value)} placeholder="الصق المفتاح هنا"/></label>
      <p className="help">يحتاج إنترنت على جهاز الإدارة. يُرسل نص السؤال والإجابة النموذجية وإجابة الطالب فقط. المفتاح يُحفظ في .env خارج SQLite.</p>
      <div className="actions"><button className="button" disabled={busy} type="submit">ابدأ التصحيح</button>
        <button className="button secondary" type="button" onClick={()=>launch({})}>استخدم المفتاح المحفوظ</button>
        <button className="button secondary" type="button" onClick={()=>setShowKey(false)}>إلغاء</button></div></form>}</section>;
}
function Review({attempt,exam,request,onClose,refresh}) {
  const [busy,setBusy]=useState(false);
  if(!attempt)return null;
  const questions=attempt.questionIds.map(id=>exam.config.questions.find(question=>question.id===id)).filter(Boolean);
  const grade=async(event,question)=>{event.preventDefault();const form=event.currentTarget;setBusy(true);
    try{await request(`/api/attempts/${attempt.id}/grade`,{questionId:question.id,score:Number(form.elements.score.value),
      feedback:form.elements.feedback.value});await refresh();toast('تم حفظ التصحيح');}
    catch(error){toast(error.message,true);}finally{setBusy(false);}};
  return <div id="review-panel"><div className="review-heading"><div><h2>ورقة {attempt.name}</h2>
    <p>الدرجة المصححة: {number(attempt.score)} من {number(attempt.maximum)}</p></div>
    <div className="actions">{attempt.pending?<span className="badge waiting">أكمل المقالي لفتح التقرير</span>:
      <a className="button secondary small" href={`/report/${attempt.id}`}>فتح تقرير الطالب / تنزيل PDF ↗</a>}
      <SendReports attempts={[attempt]}/><button className="text-button" onClick={onClose}>إغلاق</button></div></div>
    {questions.map((question,index)=>{const answer=attempt.answers[question.id];const mark=attempt.grades[question.id];
      return <article className="review-question" key={question.id}><h3>{number(index+1)}. {question.text}</h3>
        <p className="answer-text"><strong>إجابة الطالب:</strong> {question.kind==='mcq'?(answer==null?'لم يجب':question.options[answer]):answer||'لم يجب'}</p>
        <p className="model-answer-preview"><strong>الإجابة النموذجية:</strong> {question.kind==='mcq'?question.options[question.correct]:question.modelAnswer}</p>
        {question.kind==='essay'?<form className="grade-form" key={`${question.id}:${mark?.score}:${mark?.feedback}`} onSubmit={event=>grade(event,question)}>
          <label>الدرجة من {number(question.points)}<input name="score" type="number" required min="0" max={question.points}
            step="0.5" defaultValue={mark?.score??''}/></label>
          <label>ملاحظة التصحيح<input name="feedback" maxLength="2000" defaultValue={(mark?.feedback||'').replace(/^Gemini:\s*/i,'')}/></label>
          <button className="button small" type="submit" disabled={busy}>حفظ الدرجة</button></form>:
          <span className="badge complete">{number(mark?.score||0)} / {number(question.points)}</span>}</article>;
    })}</div>;
}
function Guide(){return <><div className="page-heading"><div><span className="context-label">قبل أول تجربة</span>
  <h1>جهّز القاعة بخطوات بسيطة.</h1><p>ابدأ بعدد قليل من الموبايلات، ثم زوّد العدد تدريجيًا.</p></div></div>
  <section className="panel guide"><ol><li><h2>وصّل الكمبيوتر والطلاب بنفس الشبكة</h2>
    <p>مع الـOmada المباشر، افتح «الإعدادات والبيانات» ثم «تهيئة الماك الآن». بعد اكتمالها، وصّل كابل الـOmada بالماك واترك محوّل كهربائه موصولًا. الطلاب يتصلون بواي فاي الامتحان؛ قد تظهر لهم صفحة الدخول تلقائيًا. لو لم تظهر، يفتحون رابط الطلاب المحلي. يمكن إبقاء الماك متصلًا بواي فاي آخر للإنترنت؛ شبكة الامتحان نفسها لا تحتاج إنترنت. لو ظهر للموبايل تنبيه «بلا إنترنت»، اختر البقاء متصلًا بهذه الشبكة.</p></li>
    <li><h2>جهّز الامتحان وافتح القاعة</h2><p>الطالب يكتب اسمه ورقمه وكوده الحالي، ثم ينتظر بدء الامتحان.</p></li>
    <li><h2>ابدأ وتابع الحفظ</h2><p>راقب المتصلين وحالة حفظ الإجابات. عند تغيير الموبايل استخدم «استعادة الدخول».</p></li>
    <li><h2>راجع واحتفظ بنسخة</h2><p>بعد انتهاء الامتحان راجع المقالي واطبع تقرير كل طالب أو احفظه PDF، ثم نزّل نسخة احتياطية.</p></li></ol>
    <div className="notice"><strong>حدود نسخة التجربة</strong><p>تصحيح المقالي الذكي يحتاج إنترنت على جهاز الإدارة. إرسال تقارير واتساب يحتاج حسابًا رسميًا وقالب مستند معتمدًا وإنترنت على جهاز الإدارة. أبقِ الكمبيوتر مفتوحًا طوال الامتحان.</p></div></section></>}

createRoot(document.getElementById('main')).render(<AdminApp/>);
