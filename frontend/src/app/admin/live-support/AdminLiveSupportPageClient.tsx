'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Clock3, Headphones, Plus, Save, Trash2 } from 'lucide-react';
import { AdminPage } from '@/components/admin/AdminShellChrome';
import { AdminConfirmationDialog } from '@/components/admin/AdminConfirmationDialog';
import { liveSupportService, type LiveSupportAdminConfig, type LiveSupportAdminDashboard, type LiveSupportConversationTimeline, type LiveSupportScheduleWindow, type LiveSupportStaffConfig } from '@/services/live-support-service';
import { LiveOperationsBoard } from '@/components/live-support/admin/LiveOperationsBoard';
import { StaffPerformancePanel } from '@/components/live-support/admin/StaffPerformancePanel';
import { StaffConfigurationPanel } from '@/components/live-support/admin/StaffConfigurationPanel';
import { ConversationInvestigation } from '@/components/live-support/admin/ConversationInvestigation';
import { devConsole } from '@/utils/dev-console';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { createClientId } from '@/lib/client-id';
import { formatCairoDateTime } from '@/lib/cairo-time';

const days = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

export default function AdminLiveSupportPageClient() {
  const [config, setConfig] = useState<LiveSupportAdminConfig>();
  const [error, setError] = useState('');
  const [dashboard, setDashboard] = useState<LiveSupportAdminDashboard>();
  const [timeline, setTimeline] = useState<LiveSupportConversationTimeline>();
  const [conversationFilter, setConversationFilter] = useState<'all' | 'ai' | 'failed'>('all');
  const [featureConfirmationOpen, setFeatureConfirmationOpen] = useState(false);
  const [isTogglingFeature, setIsTogglingFeature] = useState(false);
  const [featureFeedback, setFeatureFeedback] = useState('');
  const [cannedRepliesBaseline, setCannedRepliesBaseline] = useState('');
  const [staffBaselines, setStaffBaselines] = useState<Record<string, string>>({});
  const [cannedRepliesFeedback, setCannedRepliesFeedback] = useState('');
  const [isSavingCannedReplies, setIsSavingCannedReplies] = useState(false);
  const [staffFeedback, setStaffFeedback] = useState<Record<string, string>>({});
  const [savingStaffId, setSavingStaffId] = useState<string>();
  const hasDirtyChanges = Boolean(config && (serializeCannedReplies(config.cannedReplies) !== cannedRepliesBaseline || config.staff.some((staff) => serializeStaff(staff) !== staffBaselines[staff.userId])));

  async function load() {
    try { const [nextConfig, nextDashboard] = await Promise.all([liveSupportService.getAdminConfig(), liveSupportService.getAdminDashboard()]); setConfig(nextConfig); setDashboard(nextDashboard); setCannedRepliesBaseline(serializeCannedReplies(nextConfig.cannedReplies)); setStaffBaselines(Object.fromEntries(nextConfig.staff.map((staff) => [staff.userId, serializeStaff(staff)]))); setError(''); }
    catch { setError('تعذر تحميل إعدادات الدعم المباشر.'); }
  }
  useEffect(() => { void load(); }, []);
  useEffect(() => {
    const cleanupDashboard = registerCacheStore('support:dashboard', () => {}, () => void load());
    const cleanupStaff = registerCacheStore('support:staff', () => {}, () => void load());
    return () => {
      cleanupDashboard();
      cleanupStaff();
    };
  }, []);
  useEffect(() => {
    const guard = (event: BeforeUnloadEvent) => { if (hasDirtyChanges) { event.preventDefault(); event.returnValue = ''; } };
    window.addEventListener('beforeunload', guard);
    return () => window.removeEventListener('beforeunload', guard);
  }, [hasDirtyChanges]);
  useEffect(() => {
    const refreshTimer = window.setInterval(() => {
      void liveSupportService.getAdminDashboard().then(setDashboard).catch((cause) => devConsole.error('تعذر تحديث لوحة الدعم المباشر:', cause));
    }, 10_000);
    return () => window.clearInterval(refreshTimer);
  }, []);

  async function toggleFeature() {
    if (!config) return;
    setIsTogglingFeature(true);
    setFeatureFeedback('');
    try {
      await liveSupportService.setFeatureEnabled(!config.featureEnabled);
      setConfig({ ...config, featureEnabled: !config.featureEnabled });
      setFeatureFeedback(config.featureEnabled ? 'تم إيقاف الدعم المباشر.' : 'تم تفعيل الدعم المباشر.');
      setFeatureConfirmationOpen(false);
    } catch {
      setFeatureFeedback('تعذر تغيير حالة الدعم المباشر. راجع الاتصال ثم أعد المحاولة.');
    } finally { setIsTogglingFeature(false); }
  }

  async function saveStaff(staff: LiveSupportStaffConfig) {
    setSavingStaffId(staff.userId);
    setStaffFeedback((current) => ({ ...current, [staff.userId]: '' }));
    try {
      const updated = await liveSupportService.updateStaffConfig(staff.userId, { enabled: staff.isEnabled, capacity: staff.maxActiveConversations, expectedVersion: staff.version, schedule: staff.schedule });
      setConfig((current) => current ? { ...current, staff: current.staff.map((item) => item.userId === updated.userId ? updated : item) } : current);
      setStaffBaselines((current) => ({ ...current, [updated.userId]: serializeStaff(updated) }));
      setStaffFeedback((current) => ({ ...current, [staff.userId]: 'تم حفظ إعدادات الموظف.' }));
    } catch (cause) { setStaffFeedback((current) => ({ ...current, [staff.userId]: (cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر حفظ إعداد الموظف. أعد المحاولة.' })); }
    finally { setSavingStaffId(undefined); }
  }

  async function saveCannedReplies() { if (!config) return; setIsSavingCannedReplies(true); setCannedRepliesFeedback(''); try { await liveSupportService.updateCannedReplies(config.cannedReplies); setCannedRepliesBaseline(serializeCannedReplies(config.cannedReplies)); setCannedRepliesFeedback('تم حفظ الردود الثابتة.'); } catch { setCannedRepliesFeedback('تعذر حفظ الردود الثابتة. أعد المحاولة.'); } finally { setIsSavingCannedReplies(false); } }
  function updateCannedReply(id: string, change: Partial<LiveSupportAdminConfig['cannedReplies'][number]>) { setConfig(current => current ? { ...current, cannedReplies: current.cannedReplies.map(reply => reply.id === id ? { ...reply, ...change } : reply) } : current); }

  function updateStaff(userId: string, change: Partial<LiveSupportStaffConfig>) {
    setConfig((current) => current ? { ...current, staff: current.staff.map((item) => item.userId === userId ? { ...item, ...change } : item) } : current);
  }

  return <AdminPage activePath="/admin/live-support" sectionLabel="خدمة العملاء" pageTitle="إدارة الدعم المباشر" subtitle="تفعيل الخدمة، تحديد السعة لكل موظف، وجدول المواعيد الذي يظهر للزوار خارج أوقات الدعم.">
    {error && <div role="alert" className="mb-4 rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-[var(--admin-danger)]">{error}</div>}
    {!config || !dashboard ? <SupportPageSkeleton /> : <div dir="rtl" className="space-y-5">
      <LiveOperationsBoard dashboard={dashboard}/>
      <section className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-[var(--admin-shadow)]"><div className="flex flex-wrap items-end justify-between gap-3 border-b border-[var(--admin-border)] p-5"><div><h2 className="font-bold text-[var(--admin-text)]">كل المحادثات والنشاط</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">تتحدث القائمة تلقائيًا كل 10 ثوانٍ، وتشمل حالات AI والـWorker الآمنة.</p></div><label className="text-sm font-semibold text-[var(--admin-text)]">تصفية النشاط<select value={conversationFilter} onChange={event => setConversationFilter(event.target.value as typeof conversationFilter)} className="mr-2 h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"><option value="all">الكل</option><option value="ai">نشاط AI</option><option value="failed">فشل AI / Worker</option></select></label></div><div className="overflow-x-auto" tabIndex={0} role="region" aria-label="جدول محادثات الدعم المباشر"><table className="w-full min-w-[540px] text-right text-sm sm:min-w-[760px]"><caption className="sr-only">جدول محادثات الدعم، اسحب أفقيًا لرؤية التفاصيل الإضافية.</caption><thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]"><tr><th className="p-3">الشخص</th><th className="p-3">الحالة</th><th className="hidden p-3 md:table-cell">AI / Worker</th><th className="hidden p-3 lg:table-cell">الموظف</th><th className="hidden p-3 md:table-cell">وقت البدء</th><th className="p-3">الانتظار</th><th className="hidden p-3 lg:table-cell">مدة التعامل</th><th className="p-3">المتابعة</th></tr></thead><tbody>{(() => { const conversations = dashboard.conversations.filter(item => conversationFilter === 'all' || (conversationFilter === 'ai' ? Boolean(item.aiTurnStatus) : item.aiTurnStatus === 'Failed'));
        return conversations.length === 0 ? <tr><td colSpan={8} className="px-4 py-12 text-center text-[var(--admin-muted)]">لا توجد محادثات مطابقة لهذه التصفية.</td></tr> : conversations.map((item) => <tr key={item.id} className="border-t border-[var(--admin-border)] transition-colors hover:bg-[var(--admin-hover)]"><td className="p-3 font-semibold text-[var(--admin-text)]">{item.participantName}<span className="mr-2 text-xs font-normal text-[var(--admin-muted)]">{item.participantType === 'Guest' ? 'زائر' : 'طالب'}</span></td><td className="p-3 text-[var(--admin-text)]">{item.status}</td><td className="hidden p-3 text-[var(--admin-text)] md:table-cell"><span className="font-semibold">{item.aiTurnStatus || 'بشري'}</span>{item.aiTurnFailureCode && <bdi dir="ltr" className="mt-1 block break-all text-xs text-[var(--admin-danger)]">{item.aiTurnFailureCode}</bdi>}</td><td className="hidden p-3 text-[var(--admin-text)] lg:table-cell">{item.ownerName || 'الطابور'}</td><td className="hidden p-3 text-[var(--admin-text)] md:table-cell">{formatCairoDateTime(item.createdAt)}</td><td className="p-3 text-[var(--admin-text)]">{formatDuration(item.waitSeconds)}</td><td className="hidden p-3 text-[var(--admin-text)] lg:table-cell">{formatDuration(item.handleSeconds)}</td><td className="p-3"><button type="button" onClick={() => void liveSupportService.getAdminTimeline(item.id).then(setTimeline)} className="min-h-10 rounded-lg bg-[var(--admin-primary)] px-3 font-semibold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">فتح المحادثة</button></td></tr>);
      })()}</tbody></table></div></section>
      <StaffPerformancePanel staff={dashboard.staffPerformance}/>
      <section className="flex flex-col gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-[var(--admin-shadow)] sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3"><span className="grid size-12 place-items-center rounded-xl bg-[var(--admin-accent-soft)] text-[var(--admin-accent)]"><Headphones/></span><div><h2 className="font-bold text-[var(--admin-text)]">حالة الدعم المباشر</h2><p className="text-sm text-[var(--admin-muted)]">عند إيقافه لن يستطيع أحد بدء محادثة.</p></div></div>
        <div className="space-y-2 text-left"><button type="button" role="switch" aria-checked={config.featureEnabled} onClick={() => { setFeatureFeedback(''); setFeatureConfirmationOpen(true); }} className={`h-11 rounded-xl px-5 font-semibold text-[var(--admin-primary-contrast)] transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] ${config.featureEnabled ? 'bg-[var(--admin-success)]' : 'bg-[var(--admin-muted)]'}`}>{config.featureEnabled ? 'مفعّل' : 'متوقف'}</button>{featureFeedback && <p role="status" className={featureFeedback.startsWith('تعذر') ? 'text-sm text-[var(--admin-danger)]' : 'text-sm text-[var(--admin-success)]'}>{featureFeedback}</p>}</div>
      </section>
      <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-[var(--admin-shadow)]"><div className="flex flex-wrap items-center justify-between gap-3"><div><h2 className="font-bold text-[var(--admin-text)]">الردود الثابتة {serializeCannedReplies(config.cannedReplies) !== cannedRepliesBaseline && <span className="mr-2 text-sm font-medium text-[var(--admin-warning)]">تغييرات غير محفوظة</span>}</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">تظهر للموظف فوق مربع الرد. اختر هل يراجعها أولًا أم تُرسل مباشرة.</p></div><button type="button" onClick={() => setConfig(current => current ? { ...current, cannedReplies: [...current.cannedReplies, { id: createClientId(), title: '', content: '', sendImmediately: false }] } : current)} className="inline-flex h-10 items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 text-sm font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]"><Plus size={16}/>إضافة رد</button></div><div className="mt-4 space-y-3">{config.cannedReplies.map(reply => <div key={reply.id} className="grid gap-2 rounded-xl bg-[var(--admin-card-soft)] p-3 lg:grid-cols-[180px_minmax(0,1fr)_170px_auto]"><input value={reply.title} onChange={event => updateCannedReply(reply.id, { title: event.target.value })} placeholder="عنوان الزر" className="h-10 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"/><input value={reply.content} onChange={event => updateCannedReply(reply.id, { content: event.target.value })} placeholder="نص الرد" className="h-10 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"/><label className="flex h-10 items-center gap-2 text-sm font-medium text-[var(--admin-text)]"><input type="checkbox" checked={reply.sendImmediately} onChange={event => updateCannedReply(reply.id, { sendImmediately: event.target.checked })} className="size-4 accent-[var(--admin-accent)]"/>إرسال مباشر</label><button type="button" onClick={() => setConfig(current => current ? { ...current, cannedReplies: current.cannedReplies.filter(item => item.id !== reply.id) } : current)} aria-label="حذف الرد" className="grid size-10 place-items-center rounded-lg text-[var(--admin-danger)] transition hover:bg-[var(--admin-danger-10)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-danger)]"><Trash2 size={17}/></button></div>)}</div>{cannedRepliesFeedback && <p role="status" className={`mt-3 text-sm ${cannedRepliesFeedback.startsWith('تعذر') ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-success)]'}`}>{cannedRepliesFeedback}</p>}<button type="button" disabled={isSavingCannedReplies} onClick={() => void saveCannedReplies()} className="mt-4 inline-flex h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-bold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-60"><Save size={17}/>{isSavingCannedReplies ? 'جارٍ الحفظ...' : cannedRepliesFeedback.startsWith('تعذر') ? 'إعادة المحاولة' : 'حفظ الردود الثابتة'}</button></section>
      <StaffConfigurationPanel>
        <div className="rounded-xl bg-[var(--admin-card-soft)] p-4 text-sm text-[var(--admin-text)]">
          <p className="font-bold">قبل تفعيل الموظف للدعم، هناك إعدادان منفصلان:</p>
          <ol className="mt-2 list-inside list-decimal space-y-1 text-[var(--admin-muted)]">
            <li><strong className="text-[var(--admin-text)]">مواعيد الدعم هنا:</strong> تحدد متى يستقبل محادثات جديدة.</li>
            <li><strong className="text-[var(--admin-text)]">شفت الحضور:</strong> يحدد حضوره وانصرافه الفعلي، وهو مطلوب لتوزيع المحادثات.</li>
          </ol>
          <Link href="/admin/hr/shifts" className="mt-3 inline-flex min-h-10 items-center rounded-lg bg-[var(--admin-primary)] px-3 font-bold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">فتح تخطيط الشفتات</Link>
        </div>
        <p className="mt-4 rounded-xl bg-[var(--admin-warning-10)] p-4 text-sm text-[var(--admin-text)]">صلاحية «إدارة الدعم المباشر» وحدها لا تجعل الموظف يستقبل محادثات. فعّل الموظف هنا وحدد سعته ومواعيد الدعم ثم احفظ.</p>
        {config.staff.map((staff) => <StaffCard key={staff.userId} staff={staff} isDirty={serializeStaff(staff) !== staffBaselines[staff.userId]} feedback={staffFeedback[staff.userId]} isSaving={savingStaffId === staff.userId} update={(change) => updateStaff(staff.userId, change)} save={() => void saveStaff(staff)}/>)}
      </StaffConfigurationPanel>
    </div>}
    {timeline && <ConversationInvestigation timeline={timeline} close={() => setTimeline(undefined)}/>}
    {config && <AdminConfirmationDialog open={featureConfirmationOpen} onClose={() => setFeatureConfirmationOpen(false)} onConfirm={() => toggleFeature()} title={config.featureEnabled ? 'تأكيد إيقاف الدعم المباشر' : 'تأكيد تفعيل الدعم المباشر'} consequence={`${config.featureEnabled ? 'لن يتمكن الزوار والطلاب من بدء محادثات جديدة حتى يُعاد تفعيل الخدمة.' : 'سيتمكن الزوار والطلاب من بدء محادثات جديدة وفق إعدادات الموظفين وساعات الدعم الحالية.'}${featureFeedback.startsWith('تعذر') ? ` ${featureFeedback}` : ''}`} confirmLabel={config.featureEnabled ? 'إيقاف الدعم' : 'تفعيل الدعم'} variant={config.featureEnabled ? 'danger' : 'primary'} isConfirming={isTogglingFeature} />}
  </AdminPage>;
}

function formatDuration(value?: number) { if (value === undefined || value === null) return '—'; const minutes = Math.floor(value / 60); const seconds = Math.round(value % 60); return `${minutes}د ${seconds}ث`; }

function serializeCannedReplies(replies: LiveSupportAdminConfig['cannedReplies']) { return JSON.stringify(replies); }
function serializeStaff(staff: LiveSupportStaffConfig) { return JSON.stringify({ isEnabled: staff.isEnabled, maxActiveConversations: staff.maxActiveConversations, schedule: staff.schedule }); }

function SupportPageSkeleton() {
  return <div className="space-y-5" aria-label="جارٍ تحميل لوحة الدعم المباشر" aria-busy="true">
    <div className="grid gap-3 sm:grid-cols-4">{Array.from({ length: 4 }, (_, index) => <div key={index} className="h-24 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />)}</div>
    <div className="h-80 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />
  </div>;
}

function StaffCard({ staff, isDirty, feedback, isSaving, update, save }: { staff: LiveSupportStaffConfig; isDirty: boolean; feedback?: string; isSaving: boolean; update: (change: Partial<LiveSupportStaffConfig>) => void; save: () => void }) {
  function updateWindow(index: number, change: Partial<LiveSupportScheduleWindow>) { update({ schedule: staff.schedule.map((window, current) => current === index ? { ...window, ...change } : window) }); }
  return <article className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-[var(--admin-shadow)]">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h3 className="font-bold text-[var(--admin-text)]">{staff.staffName} {isDirty && <span className="mr-2 text-sm font-medium text-[var(--admin-warning)]">تغييرات غير محفوظة</span>}</h3><p className="mt-1 text-xs text-[var(--admin-muted)]">{staff.isCheckedIn ? 'مسجل حضور الآن' : 'غير مسجل حضور'} · الحمل {staff.activeLoad}/{staff.maxActiveConversations}</p></div><label className="flex items-center gap-2 text-sm font-medium text-[var(--admin-text)]"><input type="checkbox" checked={staff.isEnabled} onChange={(event) => update({ isEnabled: event.target.checked })} className="size-5 accent-[var(--admin-accent)]"/>يستقبل محادثات</label></div>
    <div className="mt-4 grid gap-4 lg:grid-cols-[220px_1fr_auto]">
      <label className="text-sm font-medium text-[var(--admin-text)]">الحد الأقصى للمحادثات<input type="number" min={1} max={50} value={staff.maxActiveConversations} onChange={(event) => update({ maxActiveConversations: Number(event.target.value) })} className="mt-1 h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"/></label>
      <div><div className="mb-2 flex items-center justify-between"><span className="flex items-center gap-2 text-sm font-medium text-[var(--admin-text)]"><Clock3 size={16}/>مواعيد الدعم</span><button type="button" onClick={() => update({ schedule: [...staff.schedule, { dayOfWeek: 0, startLocalTime: '09:00:00', endLocalTime: '17:00:00' }] })} className="text-sm font-semibold text-[var(--admin-accent)] transition hover:text-[var(--admin-primary)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">+ إضافة موعد</button></div><div className="space-y-2">{staff.schedule.map((window, index) => <div key={`${index}-${window.dayOfWeek}`} className="grid grid-cols-[1fr_1fr_1fr_auto] gap-2"><select value={window.dayOfWeek} onChange={(event) => updateWindow(index, { dayOfWeek: Number(event.target.value) })} className="h-10 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2 text-[var(--admin-text)]"><option value="">اليوم</option>{days.map((day, value) => <option key={day} value={value}>{day}</option>)}</select><input type="time" value={window.startLocalTime.slice(0,5)} onChange={(event) => updateWindow(index, { startLocalTime: `${event.target.value}:00` })} className="h-10 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2 text-[var(--admin-text)]"/><input type="time" value={window.endLocalTime.slice(0,5)} onChange={(event) => updateWindow(index, { endLocalTime: `${event.target.value}:00` })} className="h-10 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2 text-[var(--admin-text)]"/><button type="button" aria-label="حذف الموعد" onClick={() => update({ schedule: staff.schedule.filter((_, current) => current !== index) })} className="size-10 rounded-lg text-[var(--admin-danger)] transition hover:bg-[var(--admin-danger-10)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-danger)]">×</button></div>)}</div></div>
      <button type="button" disabled={isSaving} onClick={save} className="mt-auto inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 font-semibold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:cursor-not-allowed disabled:opacity-60"><Save size={17}/>{isSaving ? 'جارٍ الحفظ...' : feedback?.startsWith('تعذر') ? 'إعادة المحاولة' : 'حفظ'}</button>
    </div>{feedback && <p role="status" className={`mt-3 text-sm ${feedback.startsWith('تعذر') ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-success)]'}`}>{feedback}</p>}
  </article>;
}
