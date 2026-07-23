'use client';

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CalendarClock, CheckCircle2, Moon, Plus, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminPageSkeleton, AdminShellChrome } from '@/components/admin';
import { EmployeeDto, hrService, ShiftAssignmentDto, ShiftTemplateDto, WorkCalendarDto } from '@/services/hr-service';

export default function HrShiftsPageClient() {
  const [templates, setTemplates] = useState<ShiftTemplateDto[]>([]);
  const [calendars, setCalendars] = useState<WorkCalendarDto[]>([]);
  const [assignments, setAssignments] = useState<ShiftAssignmentDto[]>([]);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [conflict, setConflict] = useState<string | null>(null);
  const [templateForm, setTemplateForm] = useState({ code: '', name: '', startsAt: '09:00', endsAt: '17:00', mode: 'Fixed' });
  const [assignmentForm, setAssignmentForm] = useState({ employeeId: '', shiftTemplateId: '', effectiveFrom: '', effectiveTo: '', reason: '' });
  const load = useCallback(async () => { setLoading(true); try { const [t, c, a, e] = await Promise.all([hrService.listShiftTemplates(), hrService.listWorkCalendars(), hrService.listShiftAssignments(), hrService.listEmployees()]); setTemplates(t); setCalendars(c); setAssignments(a); setEmployees(e); } catch { toast.error('تعذر تحميل تخطيط الشفتات'); } finally { setLoading(false); } }, []);
  useEffect(() => { void load(); }, [load]);
  const overnightCount = useMemo(() => templates.filter((template) => template.segments.some((segment) => segment.endsAt <= segment.startsAt)).length, [templates]);

  async function createTemplate(event: FormEvent) {
    event.preventDefault(); if (!calendars[0]) return toast.error('لا يوجد تقويم عمل نشط');
    try { await hrService.createShiftTemplate({ code: templateForm.code, name: templateForm.name, mode: templateForm.mode, workCalendarId: calendars[0].id, graceMinutes: 15, minimumBreakMinutes: 30, overtimeAfterMinutes: 480, segments: [{ sequence: 1, dayOfWeek: null, startsAt: `${templateForm.startsAt}:00`, endsAt: `${templateForm.endsAt}:00`, unpaidBreakMinutes: 30, workDateRule: 'SegmentStartDate' }] }); toast.success('تم إنشاء قالب الشفت'); setTemplateForm({ code: '', name: '', startsAt: '09:00', endsAt: '17:00', mode: 'Fixed' }); await load(); }
    catch { toast.error('تعذر إنشاء الشفت؛ راجع الفترات المتداخلة'); }
  }
  async function publishAssignment(event: FormEvent) {
    event.preventDefault(); setConflict(null);
    const payload = [{ ...assignmentForm, effectiveTo: assignmentForm.effectiveTo || null }];
    try { const validation = await hrService.validateShiftAssignments(payload); if (!validation.valid) { setConflict('التعيين يتعارض مع شفت منشور في نفس الفترة.'); return; } await hrService.publishShiftAssignments(payload); toast.success('تم نشر التعيين'); setAssignmentForm({ employeeId: '', shiftTemplateId: '', effectiveFrom: '', effectiveTo: '', reason: '' }); await load(); }
    catch { toast.error('تعذر نشر التعيين'); }
  }

  return <AdminShellChrome activePath="/admin/hr/shifts" sectionLabel="الموارد البشرية" pageTitle="تخطيط الشفتات" subtitle="قوالب ثابتة ومرنة وليلية ومتعددة الفترات مع كشف التعارض قبل النشر." action={<button type="button" onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />تحديث</button>}>
    {loading ? <AdminPageSkeleton /> : <div className="space-y-6">
      <section className="grid gap-4 sm:grid-cols-3">{[[CalendarClock, 'القوالب', templates.length], [CheckCircle2, 'التعيينات المنشورة', assignments.length], [Moon, 'الشفتات الليلية', overnightCount]].map(([Icon, label, value]) => { const Glyph = Icon as typeof CalendarClock; return <article key={String(label)} className="admin-panel"><Glyph className="h-5 w-5 text-[var(--admin-primary)]" /><p className="mt-3 text-xs font-bold text-[var(--admin-muted)]">{String(label)}</p><p className="mt-1 text-2xl font-black">{String(value)}</p></article>; })}</section>
      <div className="grid gap-6 xl:grid-cols-2"><form onSubmit={createTemplate} className="admin-panel space-y-4"><div className="flex items-center gap-2"><Plus className="h-5 w-5 text-[var(--admin-primary)]" /><h2 className="text-lg font-black">قالب شفت جديد</h2></div><div className="grid gap-3 sm:grid-cols-2"><label className="text-sm font-bold">الكود<input required value={templateForm.code} onChange={(e) => setTemplateForm((v) => ({ ...v, code: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold">الاسم<input required value={templateForm.name} onChange={(e) => setTemplateForm((v) => ({ ...v, name: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold">البداية<input aria-label="بداية الشفت" type="time" value={templateForm.startsAt} onChange={(e) => setTemplateForm((v) => ({ ...v, startsAt: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold">النهاية<input aria-label="نهاية الشفت" type="time" value={templateForm.endsAt} onChange={(e) => setTemplateForm((v) => ({ ...v, endsAt: e.target.value }))} className="admin-input mt-1 w-full" /></label></div><button className="admin-btn-primary min-h-11 w-full">حفظ القالب</button></form>
      <form onSubmit={publishAssignment} className="admin-panel space-y-4"><h2 className="text-lg font-black">نشر تعيين موظف</h2><label className="text-sm font-bold">الموظف<select required value={assignmentForm.employeeId} onChange={(e) => setAssignmentForm((v) => ({ ...v, employeeId: e.target.value }))} className="admin-input mt-1 w-full"><option value="">اختر الموظف</option>{employees.map((employee) => <option key={employee.id} value={employee.id}>{employee.fullName}</option>)}</select></label><label className="text-sm font-bold">الشفت<select required value={assignmentForm.shiftTemplateId} onChange={(e) => setAssignmentForm((v) => ({ ...v, shiftTemplateId: e.target.value }))} className="admin-input mt-1 w-full"><option value="">اختر الشفت</option>{templates.map((template) => <option key={template.id} value={template.id}>{template.name}</option>)}</select></label><div className="grid gap-3 sm:grid-cols-2"><label className="text-sm font-bold">من<input required type="date" value={assignmentForm.effectiveFrom} onChange={(e) => setAssignmentForm((v) => ({ ...v, effectiveFrom: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold">إلى<input type="date" value={assignmentForm.effectiveTo} onChange={(e) => setAssignmentForm((v) => ({ ...v, effectiveTo: e.target.value }))} className="admin-input mt-1 w-full" /></label></div><label className="text-sm font-bold">سبب التعيين<input required value={assignmentForm.reason} onChange={(e) => setAssignmentForm((v) => ({ ...v, reason: e.target.value }))} className="admin-input mt-1 w-full" /></label>{conflict && <p role="alert" className="flex items-center gap-2 rounded-2xl bg-amber-100 p-3 text-sm font-bold text-amber-800"><AlertTriangle className="h-4 w-4" />{conflict}</p>}<button className="admin-btn-primary min-h-11 w-full">فحص ونشر</button></form></div>
      <section className="space-y-3"><h2 className="text-lg font-black">الجدول المنشور</h2>{assignments.length === 0 ? <div className="admin-panel py-12 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد تعيينات منشورة.</div> : <div className="grid gap-3 md:grid-cols-2">{assignments.map((item) => <article key={item.id} className="admin-panel"><div className="flex justify-between gap-3"><div><p className="font-black">{item.employee}</p><p className="text-sm font-bold text-[var(--admin-primary)]">{item.shift}</p></div><span className="admin-badge">{item.effectiveFrom} — {item.effectiveTo ?? 'مفتوح'}</span></div><p className="mt-3 text-sm text-[var(--admin-muted)]">{item.reason}</p></article>)}</div>}</section>
    </div>}
  </AdminShellChrome>;
}
