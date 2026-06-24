'use client';

import { useCallback, useEffect, useState } from 'react';
import { LoaderCircle } from 'lucide-react';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { liveSupportService, type LiveSupportConversation, type LiveSupportMessage, type LiveSupportStaffBootstrap } from '@/services/live-support-service';
import { StudentContextPanel } from '@/components/live-support/student-context/StudentContextPanel';
import { useLiveSupportHub } from '@/hooks/useLiveSupportHub';
import { StaffStatusHeader } from '@/components/live-support/staff/StaffStatusHeader';
import { ConversationQueueList } from '@/components/live-support/staff/ConversationQueueList';
import { StaffConversationWorkspace } from '@/components/live-support/staff/StaffConversationWorkspace';
import { StaffConversationLayout } from '@/components/live-support/staff/StaffConversationLayout';
import { useLiveSupportStore } from '@/stores/live-support-store';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantLiveSupportPageClient() {
  const [bootstrap, setBootstrap] = useState<LiveSupportStaffBootstrap>();
  const [selected, setSelected] = useState<LiveSupportConversation>();
  const [messages, setMessages] = useState<LiveSupportMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [error, setError] = useState('');
  const [needsStaffActivation, setNeedsStaffActivation] = useState(false);
  const selectedId = selected?.id;
  const selectedOwnerUserId = selected?.currentOwnerUserId;
  const ownershipLost = useLiveSupportStore(state => selectedId ? state.ownershipLost[selectedId] ?? false : false);
  const setOwnershipLost = useLiveSupportStore(state => state.setOwnershipLost);

  const refresh = useCallback(async () => {
    try {
      const next = await liveSupportService.getStaffBootstrap();
      setBootstrap(next);
      setError('');
      setNeedsStaffActivation(false);
      const refreshedSelection = next.conversations.find((item) => item.id === selectedId);
      if (selectedId && (!refreshedSelection || refreshedSelection.currentOwnerUserId !== selectedOwnerUserId)) {
        setOwnershipLost(selectedId, true);
        return;
      }
      const current = refreshedSelection ?? next.conversations[0];
      setSelected(current);
      if (current) { setOwnershipLost(current.id, false); setMessages(await liveSupportService.getStaffMessages(current.id)); }
    } catch (cause) {
      const message = (cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر تحميل مركز الدعم.';
      setError(message);
      setNeedsStaffActivation(message.includes('يستقبل محادثات') || message.includes('غير مفعّل للدعم'));
    }
  }, [selectedId, selectedOwnerUserId, setOwnershipLost]);
  const connected = useLiveSupportHub(selected?.id, () => void refresh());

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

  return <NavRouteGuard routePath="/assistant/live-support"><AssistantShellChrome activePath="/assistant/live-support" sectionLabel="خدمة العملاء" pageTitle="مركز الدعم المباشر" subtitle="التوزيع يتم تلقائيًا حسب الحضور والحمل والحد الأقصى المحدد لكل موظف.">
    {!bootstrap && !error ? <div className="grid min-h-80 place-items-center"><LoaderCircle className="animate-spin"/></div> : null}
    {error ? <div role="alert" className={`rounded-2xl p-5 ${needsStaffActivation ? 'border border-amber-200 bg-amber-50 text-amber-950' : 'border border-red-200 bg-red-50 text-red-800'}`}>
      <p className="font-bold">{needsStaffActivation ? 'الحساب لديه صلاحية، لكنه غير مضاف لتوزيع المحادثات' : error}</p>
      {needsStaffActivation && <ol className="mt-3 list-decimal space-y-1 pr-5 text-sm"><li>افتح لوحة الأدمن ثم «إدارة الدعم المباشر».</li><li>ابحث عن الموظف وفعّل «يستقبل محادثات» وحدد السعة والجدول، ثم اضغط حفظ.</li><li>ارجع هنا بعد تسجيل حضور الموظف.</li></ol>}
    </div> : null}
    {bootstrap && <div dir="rtl" className="space-y-4">
      <StaffStatusHeader state={bootstrap} connected={connected}/>
      {!bootstrap.isCheckedIn && <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm font-medium text-amber-900">سجّل الحضور أولًا حتى تستقبل محادثات جديدة.</div>}
      <StaffConversationLayout
        queue={<ConversationQueueList conversations={bootstrap.conversations} selectedId={selected?.id} waitingCount={bootstrap.waitingCount} onSelect={(item) => { setSelected(item); void liveSupportService.getStaffMessages(item.id).then(setMessages); }}/>} 
        workspace={<StaffConversationWorkspace conversation={selected} messages={messages} draft={draft} ownershipLost={ownershipLost} onDraftChange={setDraft} onSend={() => void send()} onTransfer={() => void transfer()} onClose={() => void close()}/>} 
        context={selected ? <StudentContextPanel conversation={selected} onConversationChange={(updated) => { setSelected(updated); setBootstrap((current) => current ? { ...current, conversations: current.conversations.map((item) => item.id === updated.id ? updated : item) } : current); }}/> : undefined}
      />
    </div>}
  </AssistantShellChrome></NavRouteGuard>;
}
