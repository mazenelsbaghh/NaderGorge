'use client';

import { useCallback, useEffect, useState } from 'react';
import { Headphones, LoaderCircle, Send, XCircle } from 'lucide-react';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { liveSupportService, type LiveSupportConversation, type LiveSupportMessage, type LiveSupportStaffBootstrap } from '@/services/live-support-service';
import { StudentContextPanel } from '@/components/live-support/student-context/StudentContextPanel';
import { useLiveSupportHub } from '@/hooks/useLiveSupportHub';
import { StaffStatusHeader } from '@/components/live-support/staff/StaffStatusHeader';
import { ConversationQueueList } from '@/components/live-support/staff/ConversationQueueList';

export default function AssistantLiveSupportPageClient() {
  const [bootstrap, setBootstrap] = useState<LiveSupportStaffBootstrap>();
  const [selected, setSelected] = useState<LiveSupportConversation>();
  const [messages, setMessages] = useState<LiveSupportMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [error, setError] = useState('');
  const connected = useLiveSupportHub(selected?.id);

  const refresh = useCallback(async () => {
    try {
      const next = await liveSupportService.getStaffBootstrap();
      setBootstrap(next);
      const current = next.conversations.find((item) => item.id === selected?.id) ?? next.conversations[0];
      setSelected(current);
      if (current) setMessages(await liveSupportService.getStaffMessages(current.id));
    } catch (cause) { setError((cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر تحميل مركز الدعم.'); }
  }, [selected?.id]);

  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), 5000);
    return () => clearInterval(timer);
  }, [refresh]);

  async function send() {
    if (!selected || !draft.trim()) return;
    const value = draft.trim(); setDraft('');
    try { const message = await liveSupportService.sendStaffMessage(selected.id, { clientMessageId: crypto.randomUUID(), content: value }); setMessages((items) => [...items, message]); }
    catch { setDraft(value); setError('فشل إرسال الرسالة.'); }
  }

  async function close() {
    if (!selected) return;
    const reason = window.prompt('اكتب سبب إغلاق المحادثة');
    if (!reason?.trim()) return;
    await liveSupportService.closeConversation(selected.id, reason.trim());
    setSelected(undefined); setMessages([]); await refresh();
  }

  async function transfer() {
    if (!selected) return;
    const reason = window.prompt('اكتب سبب تحويل المحادثة لموظف آخر');
    if (!reason?.trim()) return;
    await liveSupportService.transferConversation(selected.id, reason.trim());
    setSelected(undefined); setMessages([]); await refresh();
  }

  return <AssistantShellChrome activePath="/assistant/live-support" sectionLabel="خدمة العملاء" pageTitle="مركز الدعم المباشر" subtitle="التوزيع يتم تلقائيًا حسب الحضور والحمل والحد الأقصى المحدد لكل موظف.">
    {!bootstrap && !error ? <div className="grid min-h-80 place-items-center"><LoaderCircle className="animate-spin"/></div> : null}
    {error ? <div role="alert" className="rounded-2xl border border-red-200 bg-red-50 p-5 text-red-800">{error}</div> : null}
    {bootstrap && <div dir="rtl" className="space-y-4">
      <StaffStatusHeader state={bootstrap} connected={connected}/>
      {!bootstrap.isCheckedIn && <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm font-medium text-amber-900">سجّل الحضور أولًا حتى تستقبل محادثات جديدة.</div>}
      <div className="grid min-h-[620px] overflow-hidden rounded-3xl border border-slate-200 bg-white lg:grid-cols-[280px_1fr] xl:grid-cols-[280px_minmax(360px,1fr)_320px]">
        <ConversationQueueList conversations={bootstrap.conversations} selectedId={selected?.id} waitingCount={bootstrap.waitingCount} onSelect={(item) => { setSelected(item); void liveSupportService.getStaffMessages(item.id).then(setMessages); }}/>
        <main className="flex min-h-0 flex-col">{selected ? <><header className="flex items-center justify-between border-b border-slate-100 p-4"><div><h2 className="font-bold text-slate-900">{selected.subject || 'محادثة دعم'}</h2><p className="text-xs text-slate-500">{selected.participantType === 'Guest' ? 'زائر — يحتاج ربطًا يدويًا فقط' : 'طالب مسجل'}</p></div><div className="flex gap-2"><button type="button" onClick={() => void transfer()} className="h-10 rounded-xl border border-amber-200 px-3 text-sm font-semibold text-amber-800 hover:bg-amber-50">تحويل</button><button type="button" onClick={() => void close()} className="inline-flex h-10 items-center gap-2 rounded-xl border border-red-200 px-3 text-sm font-semibold text-red-700 hover:bg-red-50"><XCircle size={17}/>إغلاق</button></div></header><div className="min-h-0 flex-1 space-y-2 overflow-y-auto p-4">{messages.map((message) => <div key={message.id} className={`max-w-[75%] rounded-2xl px-3 py-2 text-sm ${['Staff','Admin'].includes(message.senderType) ? 'mr-auto bg-cyan-700 text-white' : 'ml-auto bg-slate-100 text-slate-800'}`}>{message.content}</div>)}</div><div className="flex gap-2 border-t border-slate-100 p-4"><input value={draft} onChange={(event) => setDraft(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); void send(); } }} className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 px-3 outline-none focus:border-cyan-600" placeholder="اكتب الرد"/><button onClick={() => void send()} aria-label="إرسال" className="grid size-11 place-items-center rounded-xl bg-cyan-700 text-white"><Send size={18}/></button></div></> : <div className="grid flex-1 place-items-center p-8 text-center text-slate-500"><div><Headphones className="mx-auto mb-3" size={36}/><p>لا توجد محادثة مسندة إليك حاليًا.</p></div></div>}</main>
        {selected && <StudentContextPanel conversation={selected} onConversationChange={(updated) => { setSelected(updated); setBootstrap((current) => current ? { ...current, conversations: current.conversations.map((item) => item.id === updated.id ? updated : item) } : current); }}/>} 
      </div>
    </div>}
  </AssistantShellChrome>;
}
