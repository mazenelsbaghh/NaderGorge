export const rosterOrders = [
 ['newest','أحدث دخول أولًا'], ['oldest','أقدم دخول أولًا'],
 ['seen','أحدث ظهور أولًا'], ['submitted','أحدث تسليم أولًا'],
 ['name','الاسم من أ إلى ي'], ['code','الكود من الأصغر للأكبر'],
 ['highest','أعلى درجة بعد التصحيح'], ['lowest','أقل درجة بعد التصحيح'],
 ['deadline','الأقرب لانتهاء الوقت'],
];
const collator=new Intl.Collator('ar',{numeric:true,sensitivity:'base'});
const joinedFormatter=new Intl.DateTimeFormat('ar-EG',{day:'numeric',month:'short',hour:'2-digit',minute:'2-digit',second:'2-digit',timeZone:'Africa/Cairo'});
function orderValue(attempt,order) {
 if(order==='newest'||order==='oldest')return attempt.joined_at||null;
 if(order==='seen')return attempt.last_seen||null;
 if(order==='submitted')return attempt.submitted_at||null;
 if(order==='highest'||order==='lowest')return !attempt.cancelled_at&&attempt.submitted_at&&attempt.pending===0?attempt.score:null;
 if(order==='deadline')return !attempt.cancelled_at&&!attempt.submitted_at?attempt.deadline||null:null;
 return null;
}
export function sortRoster(attempts,order) {
 return [...attempts].sort((a,b)=>{
  let result=0;
  if(order==='name'||order==='code')result=collator.compare(String(a[order]||''),String(b[order]||''));
  else {
   const x=orderValue(a,order),y=orderValue(b,order);
   if(x===null&&y!==null)return 1;
   if(y===null&&x!==null)return -1;
   if(x!==null&&y!==null)result=['oldest','lowest','deadline'].includes(order)?x-y:y-x;
  }
  return result||collator.compare(String(a.code),String(b.code))||String(a.id).localeCompare(String(b.id));
 });
}
export function joinedLabel(timestamp) {
 if(!timestamp)return 'لم يدخل بعد';
 return joinedFormatter.format(new Date(timestamp*1000));
}

export function prioritizeRoster(attempts,problemFor) {
 const urgent=[],ordinary=[];
 for(const attempt of attempts)(problemFor(attempt)?urgent:ordinary).push(attempt);
 return [...urgent,...ordinary];
}
