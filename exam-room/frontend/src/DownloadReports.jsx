import React,{useState} from 'react';
import {downloadBlob,toast,number} from '../../web/assets/common.js';
export default function DownloadReports({examId,attempts}){
 const [busy,setBusy]=useState(false);const ready=attempts.filter(a=>!a.cancelled_at&&a.submitted_at&&a.pending===0).length;
 const download=async()=>{if(busy)return;setBusy(true);try{
  const response=await fetch(`/api/exams/${examId}/reports.zip`);if(!response.ok)throw new Error((await response.json()).error);
  downloadBlob(await response.blob(),'تقارير-الطلاب.zip');toast('تم تجهيز ملف تقارير الطلاب');
 }catch(e){toast(e.message,true);}finally{setBusy(false);}};
 return <div><button className="button secondary small" disabled={busy||!ready} onClick={download}>{busy?'جارٍ تجهيز ملفات PDF…':`تنزيل تقارير PDF في ZIP (${number(ready)})`}</button><p className="help">يشمل التقارير مكتملة التصحيح فقط. اسم الملف: اسم الطالب — درجته من المجموع.</p></div>;
}
