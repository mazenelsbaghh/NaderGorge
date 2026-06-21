'use client';

import { useEffect, useState } from 'react';
import { Clock3, Headphones, LoaderCircle, Save } from 'lucide-react';
import { AdminShellChrome } from '@/components/admin/AdminShellChrome';
import { liveSupportService, type LiveSupportAdminConfig, type LiveSupportAdminDashboard, type LiveSupportConversationTimeline, type LiveSupportScheduleWindow, type LiveSupportStaffConfig } from '@/services/live-support-service';
import { LiveOperationsBoard } from '@/components/live-support/admin/LiveOperationsBoard';
import { StaffPerformancePanel } from '@/components/live-support/admin/StaffPerformancePanel';
import { StaffConfigurationPanel } from '@/components/live-support/admin/StaffConfigurationPanel';
import { ConversationInvestigation } from '@/components/live-support/admin/ConversationInvestigation';
import { devConsole } from '@/utils/dev-console';

const days = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

export default function AdminLiveSupportPageClient() {
  const [config, setConfig] = useState<LiveSupportAdminConfig>();
  const [error, setError] = useState('');
  const [dashboard, setDashboard] = useState<LiveSupportAdminDashboard>();
  const [timeline, setTimeline] = useState<LiveSupportConversationTimeline>();

  async function load() {
    try { const [nextConfig, nextDashboard] = await Promise.all([liveSupportService.getAdminConfig(), liveSupportService.getAdminDashboard()]); setConfig(nextConfig); setDashboard(nextDashboard); setError(''); }
    catch { setError('تعذر تحميل إعدادات الدعم المباشر.'); }
  }
  useEffect(() => { void load(); }, []);
  useEffect(() => {
    const refreshTimer = window.setInterval(() => {
      void liveSupportService.getAdminDashboard().then(setDashboard).catch((cause) => devConsole.error('تعذر تحديث لوحة الدعم المباشر:', cause));
    }, 10_000);
    return () => window.clearInterval(refreshTimer);
  }, []);

  async function toggleFeature() {
    if (!config) return;
    await liveSupportService.setFeatureEnabled(!config.featureEnabled);
    setConfig({ ...config, featureEnabled: !config.featureEnabled });
  }

  async function saveStaff(staff: LiveSupportStaffConfig) {
    try {
      const updated = await liveSupportService.updateStaffConfig(staff.userId, { enabled: staff.isEnabled, capacity: staff.maxActiveConversations, expectedVersion: staff.version, schedule: staff.schedule });
      setConfig((current) => current ? { ...current, staff: current.staff.map((item) => item.userId === updated.userId ? updated : item) } : current);
    } catch (cause) { setError((cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر حفظ إعداد الموظف.'); }
  }

  function updateStaff(userId: string, change: Partial<LiveSupportStaffConfig>) {
    setConfig((current) => current ? { ...current, staff: current.staff.map((item) => item.userId === userId ? { ...item, ...change } : item) } : current);
  }

  return <AdminShellChrome activePath="/admin/live-support" sectionLabel="خدمة العملاء" pageTitle="إدارة الدعم المباشر" subtitle="تفعيل الخدمة، تحديد السعة لكل موظف، وجدول المواعيد الذي يظهر للزوار خارج أوقات الدعم.">
    {error && <div role="alert" className="mb-4 rounded-2xl border border-red-200 bg-red-50 p-4 text-red-800">{error}</div>}
    {!config || !dashboard ? <div className="grid min-h-80 place-items-center"><LoaderCircle className="animate-spin"/></div> : <div dir="rtl" className="space-y-5">
      <LiveOperationsBoard dashboard={dashboard}/>
      <section className="overflow-hidden rounded-3xl border border-slate-200 bg-white"><div className="border-b border-slate-100 p-5"><h2 className="font-bold text-slate-900">كل المحادثات والنشاط</h2><p className="mt-1 text-sm text-slate-500">تتحدث القائمة تلقائيًا كل 10 ثوانٍ. افتح أي محادثة لقراءة الرسائل أو الرد باسم الإدارة.</p></div><div className="overflow-x-auto"><table className="w-full min-w-[860px] text-right text-sm"><thead className="bg-slate-50 text-xs text-slate-500"><tr><th className="p-3">الشخص</th><th className="p-3">الحالة</th><th className="p-3">الموظف</th><th className="p-3">وقت البدء</th><th className="p-3">الانتظار</th><th className="p-3">مدة التعامل</th><th className="p-3">المتابعة</th></tr></thead><tbody>{dashboard.conversations.map((item) => <tr key={item.id} className="border-t border-slate-100 hover:bg-cyan-50"><td className="p-3 font-semibold text-slate-900">{item.participantName}<span className="mr-2 text-xs font-normal text-slate-500">{item.participantType === 'Guest' ? 'زائر' : 'طالب'}</span></td><td className="p-3">{item.status}</td><td className="p-3">{item.ownerName || 'الطابور'}</td><td className="p-3">{new Date(item.createdAt).toLocaleString('ar-EG')}</td><td className="p-3">{formatDuration(item.waitSeconds)}</td><td className="p-3">{formatDuration(item.handleSeconds)}</td><td className="p-3"><button type="button" onClick={() => void liveSupportService.getAdminTimeline(item.id).then(setTimeline)} className="min-h-10 rounded-lg bg-slate-900 px-3 font-semibold text-white">فتح المحادثة</button></td></tr>)}</tbody></table></div></section>
      <StaffPerformancePanel staff={dashboard.staffPerformance}/>
      <section className="flex flex-col gap-4 rounded-3xl border border-slate-200 bg-white p-5 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3"><span className="grid size-12 place-items-center rounded-2xl bg-cyan-50 text-cyan-700"><Headphones/></span><div><h2 className="font-bold text-slate-900">حالة الدعم المباشر</h2><p className="text-sm text-slate-500">عند إيقافه لن يستطيع أحد بدء محادثة.</p></div></div>
        <button type="button" role="switch" aria-checked={config.featureEnabled} onClick={() => void toggleFeature()} className={`h-11 rounded-xl px-5 font-semibold text-white ${config.featureEnabled ? 'bg-emerald-700' : 'bg-slate-500'}`}>{config.featureEnabled ? 'مفعّل' : 'متوقف'}</button>
      </section>
      <StaffConfigurationPanel><p className="rounded-xl bg-amber-50 p-4 text-sm text-amber-950">صلاحية «إدارة الدعم المباشر» وحدها لا تجعل الموظف يستقبل محادثات. فعّل الموظف هنا وحدد سعته ثم احفظ.</p>{config.staff.map((staff) => <StaffCard key={staff.userId} staff={staff} update={(change) => updateStaff(staff.userId, change)} save={() => void saveStaff(staff)}/>)}</StaffConfigurationPanel>
    </div>}
    {timeline && <ConversationInvestigation timeline={timeline} close={() => setTimeline(undefined)}/>} 
  </AdminShellChrome>;
}

function formatDuration(value?: number) { if (value === undefined || value === null) return '—'; const minutes = Math.floor(value / 60); const seconds = Math.round(value % 60); return `${minutes}د ${seconds}ث`; }

function StaffCard({ staff, update, save }: { staff: LiveSupportStaffConfig; update: (change: Partial<LiveSupportStaffConfig>) => void; save: () => void }) {
  function updateWindow(index: number, change: Partial<LiveSupportScheduleWindow>) { update({ schedule: staff.schedule.map((window, current) => current === index ? { ...window, ...change } : window) }); }
  return <article className="rounded-3xl border border-slate-200 bg-white p-5">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h3 className="font-bold text-slate-900">{staff.staffName}</h3><p className="mt-1 text-xs text-slate-500">{staff.isCheckedIn ? 'مسجل حضور الآن' : 'غير مسجل حضور'} · الحمل {staff.activeLoad}/{staff.maxActiveConversations}</p></div><label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" checked={staff.isEnabled} onChange={(event) => update({ isEnabled: event.target.checked })} className="size-5 accent-cyan-700"/>يستقبل محادثات</label></div>
    <div className="mt-4 grid gap-4 lg:grid-cols-[220px_1fr_auto]">
      <label className="text-sm font-medium text-slate-700">الحد الأقصى للمحادثات<input type="number" min={1} max={50} value={staff.maxActiveConversations} onChange={(event) => update({ maxActiveConversations: Number(event.target.value) })} className="mt-1 h-11 w-full rounded-xl border border-slate-200 px-3"/></label>
      <div><div className="mb-2 flex items-center justify-between"><span className="flex items-center gap-2 text-sm font-medium text-slate-700"><Clock3 size={16}/>مواعيد الدعم</span><button type="button" onClick={() => update({ schedule: [...staff.schedule, { dayOfWeek: 0, startLocalTime: '09:00:00', endLocalTime: '17:00:00' }] })} className="text-sm font-semibold text-cyan-700">+ إضافة موعد</button></div><div className="space-y-2">{staff.schedule.map((window, index) => <div key={`${index}-${window.dayOfWeek}`} className="grid grid-cols-[1fr_1fr_1fr_auto] gap-2"><select value={window.dayOfWeek} onChange={(event) => updateWindow(index, { dayOfWeek: Number(event.target.value) })} className="h-10 rounded-lg border border-slate-200 px-2">{days.map((day, value) => <option key={day} value={value}>{day}</option>)}</select><input type="time" value={window.startLocalTime.slice(0,5)} onChange={(event) => updateWindow(index, { startLocalTime: `${event.target.value}:00` })} className="h-10 rounded-lg border border-slate-200 px-2"/><input type="time" value={window.endLocalTime.slice(0,5)} onChange={(event) => updateWindow(index, { endLocalTime: `${event.target.value}:00` })} className="h-10 rounded-lg border border-slate-200 px-2"/><button type="button" aria-label="حذف الموعد" onClick={() => update({ schedule: staff.schedule.filter((_, current) => current !== index) })} className="size-10 rounded-lg text-red-600 hover:bg-red-50">×</button></div>)}</div></div>
      <button type="button" onClick={save} className="mt-auto inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-slate-900 px-5 font-semibold text-white"><Save size={17}/>حفظ</button>
    </div>
  </article>;
}
