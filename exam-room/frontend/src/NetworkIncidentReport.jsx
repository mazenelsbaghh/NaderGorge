import React, {useEffect, useState, useSyncExternalStore} from 'react';

let incidentStatus={busy:false,message:''};
const subscribers=new Set();
const subscribe=notify=>{subscribers.add(notify);return()=>subscribers.delete(notify);};
const snapshot=()=>incidentStatus;
const publish=status=>{incidentStatus=status;subscribers.forEach(notify=>notify());};

export function useIncidentReportStatus() {
  return useSyncExternalStore(subscribe,snapshot);
}

async function captureIncident() {
  if(incidentStatus.busy)return;
  publish({busy:true,message:'جارٍ تسجيل وقت المشكلة وحفظ التقرير…'});
  try {
    const saved=await window.pywebview.api.capture_network_incident();
    const stamp=new Date(saved.capturedAt);
    const time=Number.isNaN(stamp.getTime())?'':`وقت المشكلة: ${stamp.toLocaleString('ar-EG')}. `;
    publish({busy:false,message:time+saved.message});
  } catch {
    publish({busy:false,message:'تعذر تأكيد حفظ التقرير. أعد المحاولة من برنامج مسار على الماك؛ الامتحان مستمر.'});
  }
}

export default function NetworkIncidentReport({disabled=false}) {
  const status=useIncidentReportStatus();
  const [available,setAvailable]=useState(Boolean(window.pywebview?.api?.capture_network_incident));
  useEffect(()=>{
    const ready=()=>setAvailable(Boolean(window.pywebview?.api?.capture_network_incident));
    window.addEventListener('pywebviewready',ready);ready();
    return()=>window.removeEventListener('pywebviewready',ready);
  },[]);
  if(!available)return null;
  return <div className="network-incident-report">
    <button className="button secondary" disabled={disabled||status.busy} onClick={captureIncident}>
      {status.busy?'جارٍ حفظ لوج المشكلة…':'سجّل المشكلة ونزّل اللوج'}
    </button>
    {status.message&&<p className="help" role="status">{status.message}</p>}
  </div>;
}
