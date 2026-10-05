export const rosterFilters = [
 ['all','الكل'], ['solving','يحل الآن'], ['waiting','في الانتظار'],
 ['away','خارج صفحة الامتحان'], ['disconnected','غير متصل'], ['paused','موقوف مؤقتًا'],
 ['active','لم يسلّم'], ['submitted','تم التسليم'], ['expired','انتهى وقته'],
 ['pending','بانتظار التصحيح'], ['graded','تم التصحيح'], ['cancelled','ملغي'],
];

export const screenFilters = [
 ['fullscreen','ملء الشاشة'], ['fullscreen_exit','خارج ملء الشاشة'], ['screen_reduced','مساحة عرض أقل'],
 ['device_changed','علامة متصفح مختلفة'], ['fullscreen_unsupported','ملء الشاشة غير مدعوم'], ['screen_missing','بدون بيانات شاشة'],
];

export function rosterStatus(attempt, exam, serverTime) {
 if(attempt.cancelled_at)return {key:'cancelled',tone:'disconnected',label:'ملغي'};
 if(attempt.submitted_at)return attempt.submit_reason==='timeout'
  ? {key:'expired',tone:'complete',label:'انتهى وقته · تم التسليم'}
  : {key:'submitted',tone:'complete',label:'تم التسليم'};
 if(attempt.deadline&&attempt.deadline<=serverTime)return {key:'expired',tone:'disconnected',label:'انتهى وقته'};
 if(attempt.paused)return {key:'paused',tone:'disconnected',label:'موقوف مؤقتًا'};
 if(!attempt.last_seen||serverTime-attempt.last_seen>=15)return {key:'disconnected',tone:'disconnected',label:'غير متصل'};
 if(exam.state==='waiting')return {key:'waiting',tone:'waiting',label:'في الانتظار'};
 if(exam.state==='running'&&attempt.hiddenSince)return {key:'away',tone:'disconnected',label:'خارج صفحة الامتحان'};
 if(exam.state==='running')return {key:'solving',tone:'waiting',label:'يحل الآن'};
 return {key:'active',tone:'quiet',label:'لم يسلّم'};
}

export function matchesRosterFilter(attempt, status, filter) {
 if(filter==='all')return true;
 if(screenFilters.some(([key])=>key===filter)) {
  if(attempt.cancelled_at||attempt.submitted_at)return false;
  const report=attempt.screen?.current;
  if(filter==='screen_missing')return !report;
  if(!report)return false;
  if(filter==='fullscreen')return report.fullscreen===true;
  if(filter==='fullscreen_exit')return report.fullscreenSupported===true&&report.fullscreen!==true;
  if(filter==='fullscreen_unsupported')return report.fullscreenSupported!==true;
  if(filter==='screen_reduced')return attempt.screen.issue?.startsWith('انخفض')===true;
  if(filter==='device_changed')return attempt.screen.issue==='تغيّرت علامة المتصفح';
 }

 if(filter==='active')return !attempt.cancelled_at&&!attempt.submitted_at;
 if(filter==='submitted')return !attempt.cancelled_at&&Boolean(attempt.submitted_at);
 if(filter==='pending')return !attempt.cancelled_at&&Boolean(attempt.submitted_at)&&attempt.pending>0;
 if(filter==='graded')return !attempt.cancelled_at&&Boolean(attempt.submitted_at)&&attempt.pending===0;
 return status.key===filter;
}

export function rosterProblem(attempt,exam,serverTime) {
 if(!attempt.joined_at||attempt.cancelled_at||attempt.submitted_at||!['waiting','running'].includes(exam.state))return '';
 const status=rosterStatus(attempt,exam,serverTime);
 if(['paused','away','disconnected','expired'].includes(status.key))return status.label;
 return exam.state==='running'&&exam.config.screenGuard?attempt.screen?.issue||'':'';
}
