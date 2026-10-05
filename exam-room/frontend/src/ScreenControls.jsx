import React,{useState} from 'react';
import {toast} from '../../web/assets/common.js';
export default function ScreenControls({exam,request,refresh}) {
 const [busy,setBusy]=useState(false);
 const change=async enabled=>{setBusy(true);try{await request(`/api/exams/${exam.id}/screen-rule`,{enabled});await refresh();toast('تم حفظ مراقبة الشاشة');}catch(e){toast(e.message,true);}finally{setBusy(false);}};
 return <section className="panel padded"><h2>مراقبة شاشة الطالب</h2><label className="checkbox-row"><input type="checkbox" checked={exam.config.screenGuard===true} disabled={busy} onChange={e=>change(e.target.checked)}/> إيقاف الحل عند الخروج من ملء الشاشة أو انخفاض مساحة العرض</label><p className="help">الطالب يوافق بزر عند بدء الحل. انخفاض أكثر من ٢٥٪ أو الخروج من ملء الشاشة لمدة ٥ ثوانٍ يوقفه حتى تضغط «استكمال الطالب». الوقت يستمر والإجابات محفوظة.</p><p className="help">علامة المتصفح ليست بصمة جهاز مضمونة. قياس الشاشة والخروج إشارات يرسلها المتصفح، وقد تتأثر بتقسيم الشاشة أو إعدادات الجهاز. الكيبورد والتدوير يُراعَيان قدر الإمكان؛ الأجهزة غير الداعمة لملء الشاشة تراقَب بالمساحة فقط.</p></section>;
}
