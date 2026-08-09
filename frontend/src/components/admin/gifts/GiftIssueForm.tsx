'use client';

import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import { Check, Loader2, Search, Send, X } from 'lucide-react';
import { useRouter } from 'next/navigation';
import toast from 'react-hot-toast';
import {
  adminGiftsService,
  giftTargetLabels,
  type GiftLookupDto,
  type GiftTargetType,
} from '@/services/admin-gifts-service';
import { getAcademicScopeLabel } from '@/lib/academic-labels';
import { createClientId } from '@/lib/client-id';
import { cairoDateTimeLocalToUtcISOString } from '@/lib/cairo-time';

const targetTypes = Object.keys(giftTargetLabels) as GiftTargetType[];
const isBalance = (type: GiftTargetType) => type === 'GeneralBalance' || type === 'TeacherBalance';

export function GiftIssueForm() {
  const router = useRouter();
  const [targetType, setTargetType] = useState<GiftTargetType>('Package');
  const [students, setStudents] = useState<GiftLookupDto[]>([]);
  const [teachers, setTeachers] = useState<GiftLookupDto[]>([]);
  const [targets, setTargets] = useState<GiftLookupDto[]>([]);
  const [studentSearch, setStudentSearch] = useState('');
  const [targetSearch, setTargetSearch] = useState('');
  const [selectedStudents, setSelectedStudents] = useState<string[]>([]);
  const [teacherId, setTeacherId] = useState('');
  const [targetId, setTargetId] = useState('');
  const [amount, setAmount] = useState('');
  const [expiresAt, setExpiresAt] = useState('');
  const [maxUses, setMaxUses] = useState('');
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const requestIdRef = useRef<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    Promise.all([adminGiftsService.students(studentSearch), adminGiftsService.teachers()])
      .then(([studentRows, teacherRows]) => {
        if (!active) return;
        setStudents(studentRows);
        setTeachers(teacherRows);
      })
      .catch(() => toast.error('تعذر تحميل قوائم الاختيار.'))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [studentSearch]);

  useEffect(() => {
    setTargetId('');
    if (isBalance(targetType)) {
      setTargets([]);
      return;
    }
    let active = true;
    adminGiftsService.targets(targetType, teacherId || undefined, targetSearch)
      .then((rows) => active && setTargets(rows))
      .catch(() => active && setTargets([]));
    return () => { active = false; };
  }, [targetType, teacherId, targetSearch]);

  const selectedStudentRows = useMemo(
    () => selectedStudents.map((id) => students.find((student) => student.id === id)).filter(Boolean) as GiftLookupDto[],
    [selectedStudents, students],
  );
  const selectedTarget = useMemo(
    () => targets.find((target) => target.id === targetId) ?? null,
    [targetId, targets],
  );

  const toggleStudent = (id: string) => {
    setSelectedStudents((current) => current.includes(id) ? current.filter((value) => value !== id) : [...current, id].slice(0, 100));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (selectedStudents.length === 0) return toast.error('اختر طالباً واحداً على الأقل.');
    if (!isBalance(targetType) && !targetId) return toast.error('اختر المحتوى المستهدف.');
    if (targetType === 'TeacherBalance' && !teacherId) return toast.error('اختر المدرس المرتبط بالرصيد.');
    if (isBalance(targetType) && Number(amount) <= 0) return toast.error('أدخل قيمة رصيد صحيحة.');
    if (!reason.trim()) return toast.error('سبب الهدية مطلوب.');

    try {
      setSaving(true);
      requestIdRef.current ??= createClientId();
      const result = await adminGiftsService.issue({
        requestId: requestIdRef.current,
        targetType,
        targetId: isBalance(targetType) ? null : targetId,
        teacherId: targetType === 'TeacherBalance' ? teacherId : null,
        amount: isBalance(targetType) ? Number(amount) : null,
        expiresAt: targetType === 'GeneralBalance' ? null : expiresAt ? cairoDateTimeLocalToUtcISOString(expiresAt) : null,
        maxUses: targetType === 'GeneralBalance' ? null : maxUses ? Number(maxUses) : null,
        studentIds: selectedStudents,
        reason: reason.trim(),
      });
      requestIdRef.current = null;
      toast.success('تم إصدار الهدية وتسجيل نتائج الطلاب.');
      router.push(`/admin/gifts/${result.id}`);
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="space-y-6">
      <section className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
        <h2 className="text-base font-black text-[var(--admin-text)]">نوع الهدية</h2>
        <div className="mt-4 grid grid-cols-2 gap-2 md:grid-cols-3 lg:grid-cols-6">
          {targetTypes.map((type) => (
            <button key={type} type="button" onClick={() => { setTargetType(type); setTeacherId(''); setMaxUses(''); }} className={`min-h-11 rounded-lg border px-3 text-sm font-bold transition ${targetType === type ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'}`}>
              {giftTargetLabels[type]}
            </button>
          ))}
        </div>

        {targetType === 'TeacherBalance' ? (
          <label className="mt-5 block text-sm font-bold text-[var(--admin-text)]">المدرس
            <select className="admin-input mt-2" value={teacherId} onChange={(event) => setTeacherId(event.target.value)} required>
              <option value="">اختر المدرس</option>
              {teachers.map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.name}</option>)}
            </select>
          </label>
        ) : null}

        {!isBalance(targetType) ? (
          <div className="mt-5 space-y-3">
            <label className="relative block">
              <Search className="absolute end-3 top-3 h-4 w-4 text-[var(--admin-muted)]" />
              <input className="admin-input pe-10" value={targetSearch} onChange={(event) => setTargetSearch(event.target.value)} placeholder="ابحث بالاسم أو الكود الداخلي" />
            </label>
            <select className="admin-input" value={targetId} onChange={(event) => setTargetId(event.target.value)} required>
              <option value="">اختر {giftTargetLabels[targetType]}</option>
              {targets.map((target) => <option key={target.id} value={target.id}>{target.name}{target.context ? ` - ${target.context}` : ''}</option>)}
            </select>
            {selectedTarget?.academicScopes?.length ? (
              <div className="flex flex-wrap gap-1.5">
                {selectedTarget.academicScopes.map((scope, index) => (
                  <span key={`${selectedTarget.id}-scope-${index}`} className="rounded-full bg-[var(--admin-primary-15)] px-2 py-1 text-sm font-black text-[var(--admin-primary)]">
                    {getAcademicScopeLabel(scope)}
                  </span>
                ))}
              </div>
            ) : null}
          </div>
        ) : (
          <label className="mt-5 block text-sm font-bold text-[var(--admin-text)]">قيمة الرصيد
            <input className="admin-input mt-2" type="number" min="0.01" step="0.01" value={amount} onChange={(event) => setAmount(event.target.value)} required />
          </label>
        )}
      </section>

      <section className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-base font-black text-[var(--admin-text)]">الطلاب <span className="text-[var(--admin-primary)]">({selectedStudents.length})</span></h2>
          <label className="relative w-full max-w-sm">
            <Search className="absolute end-3 top-3 h-4 w-4 text-[var(--admin-muted)]" />
            <input className="admin-input pe-10" value={studentSearch} onChange={(event) => setStudentSearch(event.target.value)} placeholder="ابحث بالاسم أو الهاتف" />
          </label>
        </div>
        {selectedStudentRows.length > 0 ? (
          <div className="mt-4 flex flex-wrap gap-2">
            {selectedStudentRows.map((student) => <button type="button" key={student.id} onClick={() => toggleStudent(student.id)} className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)]">{student.name}<X className="h-3.5 w-3.5" /></button>)}
          </div>
        ) : null}
        <div className="mt-4 max-h-72 overflow-y-auto rounded-lg border border-[var(--admin-border)]">
          {loading ? <div className="flex min-h-32 items-center justify-center"><Loader2 className="h-5 w-5 animate-spin text-[var(--admin-primary)]" /></div> : students.length === 0 ? <p className="p-8 text-center text-sm text-[var(--admin-muted)]">لا توجد نتائج.</p> : students.map((student) => {
            const selected = selectedStudents.includes(student.id);
            return <button type="button" key={student.id} onClick={() => toggleStudent(student.id)} className="flex w-full items-center justify-between border-b border-[var(--admin-border)] px-4 py-3 text-right last:border-0 hover:bg-[var(--admin-hover)]"><span><strong className="block text-sm text-[var(--admin-text)]">{student.name}</strong><small className="text-[var(--admin-muted)]">{student.context}</small></span><span className={`flex h-6 w-6 items-center justify-center rounded border ${selected ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-white' : 'border-[var(--admin-border)]'}`}>{selected ? <Check className="h-4 w-4" /> : null}</span></button>;
          })}
        </div>
      </section>

      <section className="grid gap-4 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 md:grid-cols-2">
        {targetType !== 'GeneralBalance' ? <label className="text-sm font-bold text-[var(--admin-text)]">تاريخ الانتهاء (اختياري)<input className="admin-input mt-2" type="datetime-local" value={expiresAt} onChange={(event) => setExpiresAt(event.target.value)} /></label> : <div />}
        {(targetType === 'Video' || targetType === 'Exam' || targetType === 'TeacherBalance') ? <label className="text-sm font-bold text-[var(--admin-text)]">{targetType === 'TeacherBalance' ? 'عدد المشتريات' : targetType === 'Video' ? 'عدد جلسات الفيديو' : 'عدد المحاولات'} (اختياري)<input className="admin-input mt-2" type="number" min="1" value={maxUses} onChange={(event) => setMaxUses(event.target.value)} /></label> : <div />}
        <label className="text-sm font-bold text-[var(--admin-text)] md:col-span-2">سبب الهدية<textarea className="admin-input mt-2 min-h-24 resize-y" maxLength={500} value={reason} onChange={(event) => setReason(event.target.value)} required /></label>
      </section>

      <div className="sticky bottom-4 flex items-center justify-between gap-4 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-sidebar)] p-4 shadow-lg">
        <p className="text-sm font-bold text-[var(--admin-muted)]">سيتم إصدار {giftTargetLabels[targetType]} إلى {selectedStudents.length} طالب.</p>
        <button type="submit" disabled={saving || selectedStudents.length === 0} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-5 text-sm font-bold text-white disabled:opacity-50">{saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />} إصدار الهدية</button>
      </div>
    </form>
  );
}
