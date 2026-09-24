'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import axios from 'axios';
import { useAdminTheme } from '@/components/admin/useAdminTheme';
import apiClient from '@/services/api-client';

export default function EmthntakManagementPage() {
  const frame = useRef<HTMLIFrameElement>(null);
  const [frameUrl,setFrameUrl]=useState('');
  const [error,setError]=useState('');
  const { mode, themeVars } = useAdminTheme();
  const sendTheme = useCallback(() => {
    if (!frameUrl) return;
    frame.current?.contentWindow?.postMessage({channel:'emthntak-theme',mode,colors:themeVars},new URL(frameUrl).origin);
  },[frameUrl,mode,themeVars]);
  useEffect(sendTheme,[sendTheme]);
  useEffect(()=> {
    let active=true;
    apiClient.get<{frameUrl:string}>('/admin/emthntak/configuration').then(response=> {
      if (!active) return;
      const url=new URL(response.data.frameUrl);
      url.searchParams.set('managed','massar');
      url.searchParams.set('parent_origin',window.location.origin);
      setFrameUrl(url.toString());
    }).catch(()=> { if(active) setError('تعذّر فتح إدارة امتحاناتك. راجع إعداد الاتصال وصلاحيات حسابك.'); });
    return ()=> { active=false; };
  },[]);
  useEffect(()=> {
    if(!frameUrl) return;
    const origin=new URL(frameUrl).origin;
    async function receive(event:MessageEvent) {
      if(event.origin!==origin || event.source!==frame.current?.contentWindow) return;
      if(event.data?.channel==='emthntak-theme-ready') { sendTheme(); return; }
      if(event.data?.channel!=='emthntak-request') return;
      const {id,path,method,body}=event.data;
      if(typeof id!=='string' || typeof path!=='string' || !['GET','POST','PUT'].includes(method)) return;
      let status=503, payload:unknown={error:'تعذّر الاتصال بامتحاناتك.'};
      try {
        const response=await apiClient.post('/admin/emthntak/request',{path,method,body},{validateStatus:()=>true,suppressErrorToast:true});
        status=response.status; payload=response.data;
      } catch (failure) { if (!axios.isAxiosError(failure)) throw failure; }
      frame.current?.contentWindow?.postMessage({channel:'emthntak-response',id,status,payload},origin);
    }
    window.addEventListener('message',receive);
    return ()=>window.removeEventListener('message',receive);
  },[frameUrl,sendTheme]);
  return <div className="space-y-4" dir="rtl">
    <div className="admin-panel p-5"><h1 className="text-2xl font-bold">إدارة امتحاناتك</h1><p>المدرسون والطلاب والكتب والاشتراكات هنا تخص تطبيق امتحاناتك المستقل.</p></div>
    {error && <div role="alert" className="admin-panel p-5">{error}</div>}
    {!frameUrl && !error && <p role="status">جاري فتح الإدارة…</p>}
    {frameUrl && <iframe onLoad={sendTheme} ref={frame} src={frameUrl} title="لوحة إدارة امتحاناتك" className="w-full min-h-[80vh] rounded-2xl border-0" sandbox="allow-scripts allow-same-origin allow-forms allow-downloads" />}
  </div>;
}
