'use client';

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CalendarClock, CheckCircle2, Moon, Pencil, Plus, RefreshCw, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminPageSkeleton, AdminPage } from '@/components/admin';
import { EmployeeDto, hrService, ShiftAssignmentDto, ShiftTemplateDto, WorkCalendarDto } from '@/services/hr-service';

const weekDays = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

export default function HrShiftsPageClient() {
  const [templates, setTemplates] = useState<ShiftTemplateDto[]>([]);
  const [calendars, setCalendars] = useState<WorkCalendarDto[]>([]);
  const [assignments, setAssignments] = useState<ShiftAssignmentDto[]>([]);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [conflict, setConflict] = useState<string | null>(null);
  const [calendarDrafts, setCalendarDrafts] = useState<Record<string, number>>({});
  const [editingAssignmentId, setEditingAssignmentId] = useState<string | null>(null);
  const [templateForm, setTemplateForm] = useState({ code: '', name: '', startsAt: '09:00', endsAt: '17:00', mode: 'Fixed', workCalendarId: '' });
  const [assignmentForm, setAssignmentForm] = useState({ employeeId: '', shiftTemplateId: '', effectiveFrom: '', effectiveTo: '', reason: '' });
  const [weeklyForm, setWeeklyForm] = useState({ employeeId: '', effectiveFrom: '', effectiveTo: '', reason: 'جدول أسبوعي للموظف', days: Array<string>(7).fill('') });
  const load = useCallback(async () => { setLoading(true); try { const [t, c, a, e] = await Promise.all([hrService.listShiftTemplates(), hrService.listWorkCalendars(), hrService.listShiftAssignments(), hrService.listEmployees()]); setTemplates(t); setCalendars(c); setAssignments(a); setEmployees(e); setCalendarDrafts(Object.fromEntries(c.map((calendar) => [calendar.id, calendar.workingDaysMask]))); setTemplateForm((current) => ({ ...current, workCalendarId: current.workCalendarId || c[0]?.id || '' })); } catch { toast.error('تعذر تحميل تخطيط الشفتات'); } finally { setLoading(false); } }, []);
  useEffect(() => { void load(); }, [load]);
  const overnightCount = useMemo(() => templates.filter((template) => template.segments.some((segment) => segment.endsAt <= segment.startsAt)).length, [templates]);

  async function createTemplate(event: FormEvent) {
    event.preventDefault(); if (!templateForm.workCalendarId) return toast.error('اختر تقويم العمل وأيام الراحة');
    try { await hrService.createShiftTemplate({ code: templateForm.code, name: templateForm.name, mode: templateForm.mode, workCalendarId: templateForm.workCalendarId, graceMinutes: 15, minimumBreakMinutes: 30, overtimeAfterMinutes: 480, segments: [{ sequence: 1, dayOfWeek: null, startsAt: `${templateForm.startsAt}:00`, endsAt: `${templateForm.endsAt}:00`, unpaidBreakMinutes: 30, workDateRule: 'SegmentStartDate' }] }); toast.success('تم إنشاء قالب الشفت'); setTemplateForm((current) => ({ code: '', name: '', startsAt: '09:00', endsAt: '17:00', mode: 'Fixed', workCalendarId: current.workCalendarId })); await load(); }
    catch { toast.error('تعذر إنشاء الشفت؛ راجع الفترات المتداخلة'); }
  }
  async function publishAssignment(event: FormEvent) {
    event.preventDefault(); setConflict(null);
    const payload = [{ ...assignmentForm, effectiveTo: assignmentForm.effectiveTo || null }];
    try { const validation = await hrService.validateShiftAssignments(payload); if (!validation.valid) { setConflict('التعيين يتعارض مع شفت منشور في نفس الفترة.'); return; } await hrService.publishShiftAssignments(payload); toast.success('تم نشر التعيين'); setAssignmentForm({ employeeId: '', shiftTemplateId: '', effectiveFrom: '', effectiveTo: '', reason: '' }); await load(); }
    catch { toast.error('تعذر نشر التعيين'); }
  }
  async function publishWeeklySchedule(event: FormEvent) {
    event.preventDefault();
    const selectedDays = weeklyForm.days.map((templateId, dayOfWeek) => ({ templateId, dayOfWeek })).filter((day) => day.templateId);
    if (!weeklyForm.employeeId || !weeklyForm.effectiveFrom || !weeklyForm.reason.trim()) return toast.error('اختر الموظف وتاريخ بدء الجدول وسبب التعيين');
    if (selectedDays.length === 0) return toast.error('اختر شفتاً ليوم عمل واحد على الأقل');
    const sourceTemplates = selectedDays.map((day) => templates.find((template) => template.id === day.templateId)).filter((template): template is ShiftTemplateDto => Boolean(template));
    const calendarId = sourceTemplates[0]?.workCalendarId;
    if (!calendarId || sourceTemplates.some((template) => template.workCalendarId !== calendarId)) return toast.error('اختر شفتات مرتبطة بتقويم عمل واحد');
    const segments = selectedDays.flatMap(({ templateId, dayOfWeek }) => {
      const source = templates.find((template) => template.id === templateId);
      const segment = source?.segments.find((item) => item.dayOfWeek === dayOfWeek) ?? source?.segments[0];
      return segment ? [{ ...segment, sequence: dayOfWeek + 1, dayOfWeek }] : [];
    });
    if (segments.length !== selectedDays.length) return toast.error('أحد الشفتات المحددة لا يحتوي على توقيت صالح');
    const employeeCode = weeklyForm.employeeId.replaceAll('-', '').slice(0, 8).toUpperCase();
    const dateCode = weeklyForm.effectiveFrom.replaceAll('-', '');
    try {
      if (editingAssignmentId) {
        await hrService.updateShiftAssignment(editingAssignmentId, {
          effectiveFrom: weeklyForm.effectiveFrom,
          effectiveTo: weeklyForm.effectiveTo || null,
          reason: weeklyForm.reason.trim(),
          segments,
        });
        toast.success('تم تحديث الجدول المنشور');
        setEditingAssignmentId(null);
        setWeeklyForm({ employeeId: '', effectiveFrom: '', effectiveTo: '', reason: 'جدول أسبوعي للموظف', days: Array<string>(7).fill('') });
        await load();
        return;
      }
      const existingSchedule = await hrService.validateShiftAssignments([{ employeeId: weeklyForm.employeeId, shiftTemplateId: sourceTemplates[0].id, effectiveFrom: weeklyForm.effectiveFrom, effectiveTo: weeklyForm.effectiveTo || null, reason: weeklyForm.reason.trim() }]);
      if (!existingSchedule.valid) return setConflict('يوجد شفت منشور للموظف في هذه الفترة. عدّل الجدول المنشور أو اختر تاريخاً آخر.');
      const created = await hrService.createShiftTemplate({ code: `WEEK-${employeeCode}-${dateCode}`, name: `جدول أسبوعي: ${employees.find((employee) => employee.id === weeklyForm.employeeId)?.fullName ?? ''}`, mode: 'Fixed', workCalendarId: calendarId, graceMinutes: 15, minimumBreakMinutes: 30, overtimeAfterMinutes: 480, segments });
      const shiftTemplateId = created.data;
      if (!shiftTemplateId) throw new Error('SHIFT_TEMPLATE_NOT_CREATED');
      const payload = [{ employeeId: weeklyForm.employeeId, shiftTemplateId, effectiveFrom: weeklyForm.effectiveFrom, effectiveTo: weeklyForm.effectiveTo || null, reason: weeklyForm.reason.trim() }];
      await hrService.publishShiftAssignments(payload);
      toast.success('تم نشر الجدول الأسبوعي للموظف');
      setWeeklyForm({ employeeId: '', effectiveFrom: '', effectiveTo: '', reason: 'جدول أسبوعي للموظف', days: Array<string>(7).fill('') });
      await load();
    } catch { toast.error('تعذر حفظ الجدول الأسبوعي'); }
  }
  function editPublishedSchedule(assignment: ShiftAssignmentDto) {
    const days = Array<string>(7).fill('');
    for (const segment of assignment.segments) {
      if (segment.dayOfWeek == null) continue;
      const matchingTemplate = templates.find((template) => template.segments.some((candidate) =>
        (candidate.dayOfWeek === segment.dayOfWeek || candidate.dayOfWeek == null) &&
        candidate.startsAt === segment.startsAt && candidate.endsAt === segment.endsAt));
      days[segment.dayOfWeek] = matchingTemplate?.id ?? '';
    }
    setEditingAssignmentId(assignment.id);
    setWeeklyForm({ employeeId: assignment.employeeId, effectiveFrom: assignment.effectiveFrom, effectiveTo: assignment.effectiveTo ?? '', reason: assignment.reason, days });
    setConflict(null);
    document.getElementById('weekly-schedule-editor')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
  function cancelScheduleEdit() {
    setEditingAssignmentId(null);
    setWeeklyForm({ employeeId: '', effectiveFrom: '', effectiveTo: '', reason: 'جدول أسبوعي للموظف', days: Array<string>(7).fill('') });
    setConflict(null);
  }
  function toggleCalendarDay(calendarId: string, dayOfWeek: number) {
    setCalendarDrafts((current) => ({
      ...current,
      [calendarId]: (current[calendarId] ?? 0) ^ (1 << dayOfWeek),
    }));
  }
  async function saveCalendar(calendarId: string) {
    const workingDaysMask = calendarDrafts[calendarId] ?? 0;
    try { await hrService.updateWorkCalendar(calendarId, workingDaysMask); toast.success('تم حفظ أيام العمل والراحة'); await load(); }
    catch { toast.error('اختر يوم عمل واحدًا على الأقل ثم أعد المحاولة'); }
  }
  const selectedCalendar = calendars.find((calendar) => calendar.id === templateForm.workCalendarId);
  const selectedRestDays = selectedCalendar
    ? weekDays.filter((_, dayOfWeek) => (selectedCalendar.workingDaysMask & (1 << dayOfWeek)) === 0)
    : [];

  return <AdminPage activePath="/admin/hr/shifts" sectionLabel="الموارد البشرية" pageTitle="تخطيط الشفتات" subtitle="قوالب ثابتة ومرنة وليلية ومتعددة الفترات مع كشف التعارض قبل النشر." action={<button type="button" onClick={() => void load()} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />تحديث</button>}>
    {loading ? <AdminPageSkeleton /> : <div className="space-y-6">
      <section className="grid gap-4 sm:grid-cols-3">{[[CalendarClock, 'القوالب', templates.length], [CheckCircle2, 'التعيينات المنشورة', assignments.length], [Moon, 'الشفتات الليلية', overnightCount]].map(([Icon, label, value]) => { const Glyph = Icon as typeof CalendarClock; return <article key={String(label)} className="admin-panel"><Glyph className="h-5 w-5 text-[var(--admin-primary)]" /><p className="mt-3 text-xs font-bold text-[var(--admin-muted)]">{String(label)}</p><p className="mt-1 text-2xl font-black">{String(value)}</p></article>; })}</section>
      <section className="space-y-3"><div><h2 className="text-lg font-black">تقويمات العمل وأيام الراحة</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">حدد أيام العمل لكل تقويم؛ الأيام غير المحددة تُحسب راحة أسبوعية لكل الشيفتات المرتبطة به.</p></div><div className="grid gap-3 lg:grid-cols-2">{calendars.map((calendar) => <article key={calendar.id} className="admin-panel"><div className="flex flex-wrap items-center justify-between gap-3"><div><p className="font-black">{calendar.name}</p><p className="text-xs font-bold text-[var(--admin-muted)]">{calendar.timeZoneId}</p></div><button type="button" onClick={() => void saveCalendar(calendar.id)} className="admin-btn-primary min-h-10">حفظ الأيام</button></div><div className="mt-4 grid grid-cols-4 gap-2 sm:grid-cols-7">{weekDays.map((day, dayOfWeek) => { const isWorking = ((calendarDrafts[calendar.id] ?? 0) & (1 << dayOfWeek)) !== 0; return <label key={day} className={`flex min-h-16 cursor-pointer flex-col items-center justify-center rounded-xl border px-2 text-xs font-bold ${isWorking ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)]'}`}><input type="checkbox" checked={isWorking} onChange={() => toggleCalendarDay(calendar.id, dayOfWeek)} className="sr-only" /><span>{day}</span><span className="mt-1 text-sm">{isWorking ? 'عمل' : 'راحة'}</span></label>; })}</div></article>)}</div></section>
      <div className="grid gap-6 xl:grid-cols-2"><form onSubmit={createTemplate} className="admin-panel space-y-4"><div className="flex items-center gap-2"><Plus className="h-5 w-5 text-[var(--admin-primary)]" /><h2 className="text-lg font-black">قالب شفت جديد</h2></div><div className="grid gap-3 sm:grid-cols-2"><label className="text-sm font-bold">الكود<input required value={templateForm.code} onChange={(e) => setTemplateForm((v) => ({ ...v, code: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold">الاسم<input required value={templateForm.name} onChange={(e) => setTemplateForm((v) => ({ ...v, name: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold">البداية<input aria-label="بداية الشفت" type="time" value={templateForm.startsAt} onChange={(e) => setTemplateForm((v) => ({ ...v, startsAt: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold">النهاية<input aria-label="نهاية الشفت" type="time" value={templateForm.endsAt} onChange={(e) => setTemplateForm((v) => ({ ...v, endsAt: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold sm:col-span-2">تقويم العمل وأيام الراحة<select required value={templateForm.workCalendarId} onChange={(e) => setTemplateForm((v) => ({ ...v, workCalendarId: e.target.value }))} className="admin-input mt-1 w-full"><option value="">اختر تقويم العمل</option>{calendars.map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.name}</option>)}</select></label></div>{selectedCalendar && <p className="rounded-xl bg-[var(--admin-bg)] p-3 text-xs font-bold text-[var(--admin-muted)]">أيام الراحة في هذا التقويم: <span className="text-[var(--admin-text)]">{selectedRestDays.length > 0 ? selectedRestDays.join('، ') : 'لا توجد'}</span></p>}<button className="admin-btn-primary min-h-11 w-full">حفظ القالب</button></form>
      <form onSubmit={publishAssignment} className="admin-panel space-y-4"><h2 className="text-lg font-black">نشر تعيين موظف</h2><label className="text-sm font-bold">الموظف<select required value={assignmentForm.employeeId} onChange={(e) => setAssignmentForm((v) => ({ ...v, employeeId: e.target.value }))} className="admin-input mt-1 w-full"><option value="">اختر الموظف</option>{employees.map((employee) => <option key={employee.id} value={employee.id}>{employee.fullName}</option>)}</select></label><label className="text-sm font-bold">الشفت<select required value={assignmentForm.shiftTemplateId} onChange={(e) => setAssignmentForm((v) => ({ ...v, shiftTemplateId: e.target.value }))} className="admin-input mt-1 w-full"><option value="">اختر الشفت</option>{templates.map((template) => <option key={template.id} value={template.id}>{template.name}</option>)}</select></label><div className="grid gap-3 sm:grid-cols-2"><label className="text-sm font-bold">من<input required type="date" value={assignmentForm.effectiveFrom} onChange={(e) => setAssignmentForm((v) => ({ ...v, effectiveFrom: e.target.value }))} className="admin-input mt-1 w-full" /></label><label className="text-sm font-bold">إلى<input type="date" value={assignmentForm.effectiveTo} onChange={(e) => setAssignmentForm((v) => ({ ...v, effectiveTo: e.target.value }))} className="admin-input mt-1 w-full" /></label></div><label className="text-sm font-bold">سبب التعيين<input required value={assignmentForm.reason} onChange={(e) => setAssignmentForm((v) => ({ ...v, reason: e.target.value }))} className="admin-input mt-1 w-full" /></label>{conflict && <p role="alert" className="flex items-center gap-2 rounded-2xl bg-amber-100 p-3 text-sm font-bold text-amber-800"><AlertTriangle className="h-4 w-4" />{conflict}</p>}<button className="admin-btn-primary min-h-11 w-full">فحص ونشر</button></form></div>
      <form id="weekly-schedule-editor" onSubmit={publishWeeklySchedule} className="admin-panel scroll-mt-24 space-y-4"><div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-lg font-black">{editingAssignmentId ? 'تعديل الجدول الأسبوعي المنشور' : 'الجدول الأسبوعي للموظف'}</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">اختر شفت كل يوم. اليوم الذي تتركه «إجازة» لن يقبل فيه النظام تسجيل حضور.</p></div>{editingAssignmentId && <button type="button" onClick={cancelScheduleEdit} className="admin-btn-secondary inline-flex min-h-10 items-center gap-2"><X className="h-4 w-4" />إلغاء التعديل</button>}</div><div className="grid gap-3 md:grid-cols-3"><label className="text-sm font-bold md:col-span-2">الموظف<select required disabled={Boolean(editingAssignmentId)} value={weeklyForm.employeeId} onChange={(e) => setWeeklyForm((current) => ({ ...current, employeeId: e.target.value }))} className="admin-input mt-1 disabled:cursor-not-allowed disabled:opacity-70"><option value="">اختر الموظف</option>{employees.map((employee) => <option key={employee.id} value={employee.id}>{employee.fullName}</option>)}</select></label><label className="text-sm font-bold">يبدأ من<input required type="date" value={weeklyForm.effectiveFrom} onChange={(e) => setWeeklyForm((current) => ({ ...current, effectiveFrom: e.target.value }))} className="admin-input mt-1" /></label><label className="text-sm font-bold">ينتهي في (اختياري)<input type="date" value={weeklyForm.effectiveTo} onChange={(e) => setWeeklyForm((current) => ({ ...current, effectiveTo: e.target.value }))} className="admin-input mt-1" /></label><label className="text-sm font-bold md:col-span-2">سبب التعيين<input required value={weeklyForm.reason} onChange={(e) => setWeeklyForm((current) => ({ ...current, reason: e.target.value }))} className="admin-input mt-1" /></label></div><div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">{weekDays.map((day, dayOfWeek) => <label key={day} className="rounded-xl bg-[var(--admin-bg)] p-3 text-sm font-bold">{day}<select value={weeklyForm.days[dayOfWeek]} onChange={(e) => setWeeklyForm((current) => ({ ...current, days: current.days.map((value, index) => index === dayOfWeek ? e.target.value : value) }))} className="admin-input mt-2"><option value="">إجازة</option>{templates.map((template) => { const segment = template.segments.find((item) => item.dayOfWeek === dayOfWeek) ?? template.segments[0]; return <option key={template.id} value={template.id}>{template.name}{segment ? ` (${segment.startsAt.slice(0, 5)} - ${segment.endsAt.slice(0, 5)})` : ''}</option>; })}</select></label>)}</div>{conflict && <p role="alert" className="flex items-center gap-2 rounded-xl bg-amber-100 p-3 text-sm font-bold text-amber-800"><AlertTriangle className="h-4 w-4" />{conflict}</p>}<button className="admin-btn-primary min-h-11">{editingAssignmentId ? 'حفظ تعديلات الجدول' : 'حفظ الجدول الأسبوعي'}</button></form>
      <section className="space-y-3"><div><h2 className="text-lg font-black">الجدول المنشور</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">كل أيام الأسبوع ظاهرة هنا، ويمكن فتح أي جدول للتعديل.</p></div>{assignments.length === 0 ? <div className="admin-panel py-12 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد تعيينات منشورة.</div> : <div className="space-y-3">{assignments.map((item) => <article key={item.id} className="admin-panel"><div className="flex flex-wrap justify-between gap-3"><div><p className="font-black">{item.employee}</p><p className="text-sm font-bold text-[var(--admin-primary)]">{item.shift}</p></div><div className="flex flex-wrap items-center gap-2"><span className="admin-badge">{item.effectiveFrom} — {item.effectiveTo ?? 'مفتوح'}</span><button type="button" onClick={() => editPublishedSchedule(item)} className="admin-btn-secondary inline-flex min-h-10 items-center gap-2"><Pencil className="h-4 w-4" />تعديل الجدول</button></div></div><div className="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7">{weekDays.map((day, dayOfWeek) => { const segment = item.segments.find((row) => row.dayOfWeek === dayOfWeek); return <div key={day} className={`rounded-xl p-3 ${segment ? 'bg-[var(--admin-primary-15)] text-[var(--admin-text)]' : 'bg-[var(--admin-bg)] text-[var(--admin-muted)]'}`}><p className="text-xs font-black">{day}</p><p className="mt-1 text-xs font-bold">{segment ? `${segment.startsAt.slice(0, 5)} - ${segment.endsAt.slice(0, 5)}` : 'إجازة'}</p></div>; })}</div><p className="mt-3 text-sm text-[var(--admin-muted)]">{item.reason}</p></article>)}</div>}</section>
    </div>}
  </AdminPage>;
}
