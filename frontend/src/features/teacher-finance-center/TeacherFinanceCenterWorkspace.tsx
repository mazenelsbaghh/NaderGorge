'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ClipboardList,
  FileText,
  Landmark,
  Pencil,
  Plus,
  RefreshCw,
  Save,
  WalletCards,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { AdminModal } from '@/components/admin';
import { financeService } from '@/services/finance-service';
import type { TeacherDto } from '@/services/teacher-service';
import type {
  TeacherAgreement,
  TeacherAgreementAllocationMode,
  TeacherAgreementScopeType,
  TeacherAgreementTrigger,
  TeacherFinanceSummary,
  TeacherPriceBasis,
} from './types';
import { TeacherFinanceOperationsWorkspace } from './TeacherFinanceOperationsWorkspace';
import { cairoCurrentDate } from '@/lib/cairo-time';

type AgreementDraft = Omit<TeacherAgreement, 'id' | 'teacherId' | 'isActive'>;

type AgreementScopeChoice =
  | 'Everything'
  | 'AllPackages' | 'Package'
  | 'AllTerms' | 'Term'
  | 'AllContentSections' | 'ContentSection'
  | 'AllLessons' | 'Lesson'
  | 'AllLessonVideos' | 'LessonVideo'
  | 'AllPublicExams' | 'PublicExam'
  | 'AllSharedPackages' | 'SharedPackage'
  | 'AllCodeGroups' | 'CodeGroup';

type ScopeOption = {
  choice: AgreementScopeChoice;
  scopeType: TeacherAgreementScopeType;
  label: string;
  requiresId: boolean;
};

const aggregateScopeOptions: ScopeOption[] = [
  { choice: 'Everything', scopeType: 'Default', label: 'كل محتوى المدرس', requiresId: false },
  { choice: 'AllPackages', scopeType: 'Package', label: 'كل الكورسات والباقات', requiresId: false },
  { choice: 'AllTerms', scopeType: 'Term', label: 'كل الترمات', requiresId: false },
  { choice: 'AllContentSections', scopeType: 'ContentSection', label: 'كل الأقسام', requiresId: false },
  { choice: 'AllLessons', scopeType: 'Lesson', label: 'كل الحصص', requiresId: false },
  { choice: 'AllLessonVideos', scopeType: 'LessonVideo', label: 'كل الفيديوهات', requiresId: false },
  { choice: 'AllPublicExams', scopeType: 'PublicExam', label: 'كل الامتحانات', requiresId: false },
  { choice: 'AllSharedPackages', scopeType: 'SharedPackage', label: 'كل الباقات المشتركة', requiresId: false },
  { choice: 'AllCodeGroups', scopeType: 'CodeGroup', label: 'كل دفعات الأكواد', requiresId: false },
];

const specificScopeOptions: ScopeOption[] = [
  { choice: 'Package', scopeType: 'Package', label: 'كورس أو باقة محددة', requiresId: true },
  { choice: 'Term', scopeType: 'Term', label: 'ترم محدد', requiresId: true },
  { choice: 'ContentSection', scopeType: 'ContentSection', label: 'قسم محدد', requiresId: true },
  { choice: 'Lesson', scopeType: 'Lesson', label: 'حصة محددة', requiresId: true },
  { choice: 'LessonVideo', scopeType: 'LessonVideo', label: 'فيديو محدد', requiresId: true },
  { choice: 'PublicExam', scopeType: 'PublicExam', label: 'امتحان محدد', requiresId: true },
  { choice: 'SharedPackage', scopeType: 'SharedPackage', label: 'باقة مشتركة محددة', requiresId: true },
  { choice: 'CodeGroup', scopeType: 'CodeGroup', label: 'دفعة أكواد محددة', requiresId: true },
];

const scopeOptions = [...aggregateScopeOptions, ...specificScopeOptions];
const aggregateChoiceByScopeType: Record<TeacherAgreementScopeType, AgreementScopeChoice> = {
  Default: 'Everything',
  Package: 'AllPackages',
  Term: 'AllTerms',
  ContentSection: 'AllContentSections',
  Lesson: 'AllLessons',
  LessonVideo: 'AllLessonVideos',
  PublicExam: 'AllPublicExams',
  SharedPackage: 'AllSharedPackages',
  CodeGroup: 'AllCodeGroups',
};

function findScopeOption(choice: AgreementScopeChoice) {
  return scopeOptions.find((option) => option.choice === choice) ?? aggregateScopeOptions[0];
}

function scopeChoiceForAgreement(agreement: Pick<TeacherAgreement, 'scopeType' | 'scopeId'>): AgreementScopeChoice {
  return agreement.scopeId
    ? agreement.scopeType as AgreementScopeChoice
    : aggregateChoiceByScopeType[agreement.scopeType];
}

function scopeLabel(agreement: Pick<TeacherAgreement, 'scopeType' | 'scopeId'>) {
  return findScopeOption(scopeChoiceForAgreement(agreement)).label;
}

const triggerLabels: Record<TeacherAgreementTrigger, string> = {
  ContentSale: 'عند شراء المحتوى',
  CodeDelivery: 'عند تأكيد تسليم الأكواد',
  CodeActivation: 'عند تفعيل الكود',
};

const allocationLabels: Record<TeacherAgreementAllocationMode, string> = {
  Percentage: 'نسبة مئوية',
  FixedPerSale: 'مبلغ ثابت لكل بيع',
  FixedPerCode: 'مبلغ ثابت لكل كود',
  FixedPerBatch: 'مبلغ ثابت للدفعة',
};

const freshDraft = (): AgreementDraft => ({
  scopeType: 'Default',
  scopeId: '',
  trigger: 'ContentSale',
  allocationMode: 'Percentage',
  allocationValue: 0,
  priceBasis: 'NetAfterDiscount',
  effectiveFrom: cairoCurrentDate(),
  effectiveTo: '',
  reason: '',
});

function formatCurrency(value: number) {
  return `${value.toLocaleString('ar-EG-u-nu-latn', { maximumFractionDigits: 2 })} ج.م`;
}

function Metric({ label, value, tone = 'default' }: { label: string; value: number; tone?: 'default' | 'positive' | 'danger' }) {
  const toneClass = tone === 'positive'
    ? 'text-emerald-700 dark:text-emerald-400'
    : tone === 'danger'
      ? 'text-rose-700 dark:text-rose-400'
      : 'text-[var(--admin-text)]';
  return (
    <div className="min-w-0 border-s border-[var(--admin-border)] ps-4 first:border-s-0 first:ps-0">
      <p className="text-xs font-bold text-[var(--admin-muted)]">{label}</p>
      <p className={`mt-1 truncate font-mono text-lg font-black ${toneClass}`}>{formatCurrency(value)}</p>
    </div>
  );
}

export function TeacherFinanceCenterWorkspace({ teachers }: { teachers: TeacherDto[] }) {
  const [teacherId, setTeacherId] = useState('');
  const [summary, setSummary] = useState<TeacherFinanceSummary | null>(null);
  const [agreements, setAgreements] = useState<TeacherAgreement[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingAgreement, setEditingAgreement] = useState<TeacherAgreement | null>(null);
  const [draft, setDraft] = useState<AgreementDraft>(freshDraft);
  const [scopeChoice, setScopeChoice] = useState<AgreementScopeChoice>('Everything');
  const [isSaving, setIsSaving] = useState(false);

  const selectedTeacher = useMemo(
    () => teachers.find((teacher) => teacher.id === teacherId),
    [teacherId, teachers],
  );

  const loadTeacherFinance = useCallback(async () => {
    if (!teacherId) {
      setSummary(null);
      setAgreements([]);
      return;
    }
    setIsLoading(true);
    try {
      const [nextSummary, nextAgreements] = await Promise.all([
        financeService.getTeacherFinanceSummary(teacherId),
        financeService.getTeacherAgreements(teacherId),
      ]);
      setSummary(nextSummary);
      setAgreements(nextAgreements);
    } catch {
      toast.error('تعذر تحميل حساب المدرس واتفاقاته المالية');
    } finally {
      setIsLoading(false);
    }
  }, [teacherId]);

  useEffect(() => {
    void loadTeacherFinance();
  }, [loadTeacherFinance]);

  const openCreate = () => {
    if (!teacherId) {
      toast.error('اختر المدرس أولاً');
      return;
    }
    setEditingAgreement(null);
    setDraft(freshDraft());
    setScopeChoice('Everything');
    setIsModalOpen(true);
  };

  const openEdit = (agreement: TeacherAgreement) => {
    setEditingAgreement(agreement);
    setScopeChoice(scopeChoiceForAgreement(agreement));
    setDraft({
      scopeType: agreement.scopeType,
      scopeId: agreement.scopeId ?? '',
      trigger: agreement.trigger,
      allocationMode: agreement.allocationMode,
      allocationValue: agreement.allocationValue,
      priceBasis: agreement.priceBasis,
      effectiveFrom: agreement.effectiveFrom.slice(0, 10),
      effectiveTo: agreement.effectiveTo?.slice(0, 10) ?? '',
      reason: agreement.reason,
    });
    setIsModalOpen(true);
  };

  const changeScopeChoice = (nextChoice: AgreementScopeChoice) => {
    const nextOption = findScopeOption(nextChoice);
    setScopeChoice(nextChoice);
    setDraft((current) => ({
      ...current,
      scopeType: nextOption.scopeType,
      scopeId: nextOption.requiresId ? '' : undefined,
    }));
  };

  const saveAgreement = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!teacherId) return;
    const selectedScope = findScopeOption(scopeChoice);
    if (selectedScope.requiresId && !draft.scopeId?.trim()) {
      toast.error('أدخل معرّف المحتوى أو الباقة التي ينطبق عليها الاتفاق');
      return;
    }
    if (!draft.reason.trim()) {
      toast.error('اكتب سبباً واضحاً لتوثيق الاتفاق');
      return;
    }

    const payload: AgreementDraft = {
      ...draft,
      scopeType: selectedScope.scopeType,
      scopeId: selectedScope.requiresId ? draft.scopeId?.trim() : undefined,
      effectiveTo: draft.effectiveTo || undefined,
      reason: draft.reason.trim(),
    };
    setIsSaving(true);
    try {
      const result = editingAgreement
        ? await financeService.replaceTeacherAgreement(editingAgreement.id, payload)
        : await financeService.createTeacherAgreement(teacherId, payload);
      if (!result.success) {
        toast.error(result.message || 'تعذر حفظ الاتفاق المالي');
        return;
      }
      toast.success(editingAgreement ? 'تم استبدال الاتفاق مع حفظ السجل السابق' : 'تم إضافة الاتفاق المالي');
      setIsModalOpen(false);
      await loadTeacherFinance();
    } catch (error: any) {
      toast.error(error?.response?.data?.message || 'حدث خطأ أثناء حفظ الاتفاق');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <section id="finance-panel-teacher-center" role="tabpanel" aria-label="مركز مالية المدرسين">
      <div className="mb-6 border-b-2 border-[var(--admin-primary)] pb-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p className="mb-1 flex items-center gap-2 text-xs font-black text-[var(--admin-primary)]"><Landmark className="h-4 w-4" /> مركز الأدمن فقط</p>
            <h2 className="text-xl font-black tracking-tight text-[var(--admin-text)]">مالية المدرسين</h2>
            <p className="mt-1 text-sm text-[var(--admin-muted)]">اتفاقات مؤرخة، رصيد مستحق، وسجل القواعد التي تُحسب بها المبيعات والأكواد.</p>
          </div>
          <div className="flex w-full flex-col gap-2 sm:w-auto sm:flex-row">
            <select
              value={teacherId}
              onChange={(event) => setTeacherId(event.target.value)}
              className="min-h-11 min-w-[260px] rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-bold text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
              aria-label="اختر المدرس"
            >
              <option value="">اختر مدرساً لعرض حسابه</option>
              {teachers.filter((teacher) => teacher.isActive).map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>)}
            </select>
            <button type="button" onClick={() => void loadTeacherFinance()} disabled={!teacherId || isLoading} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-50">
              <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} /> تحديث
            </button>
          </div>
        </div>
      </div>

      {!teacherId ? (
        <div className="border border-dashed border-[var(--admin-border-strong)] bg-[var(--admin-card-soft)] px-6 py-12 text-center">
          <WalletCards className="mx-auto h-8 w-8 text-[var(--admin-primary)]" />
          <h3 className="mt-3 font-black text-[var(--admin-text)]">ابدأ باختيار مدرس</h3>
          <p className="mx-auto mt-1 max-w-lg text-sm text-[var(--admin-muted)]">ستظهر هنا المستحقات والديون والاتفاقات الفعالة وسجلها، بدون منح أي وصول للمدرس لهذه الشاشة.</p>
        </div>
      ) : (
        <>
          <div className="mb-7 grid gap-0 overflow-hidden border border-[var(--admin-border)] bg-[var(--admin-card)] sm:grid-cols-2 lg:grid-cols-5">
            <div className="border-b border-[var(--admin-border)] p-4 sm:col-span-2 lg:col-span-5">
              <p className="text-xs font-bold text-[var(--admin-muted)]">حساب المدرس المحدد</p>
              <p className="mt-1 font-black text-[var(--admin-text)]">{selectedTeacher?.fullName}</p>
            </div>
            <div className="p-4"><Metric label="إجمالي المستحق" value={summary?.totalEarned ?? 0} /></div>
            <div className="border-s border-[var(--admin-border)] p-4"><Metric label="المتاح للصرف" value={summary?.available ?? 0} tone="positive" /></div>
            <div className="border-s border-[var(--admin-border)] p-4"><Metric label="محجوز للتسوية" value={summary?.reserved ?? 0} /></div>
            <div className="border-s border-[var(--admin-border)] p-4"><Metric label="تم صرفه" value={summary?.paid ?? 0} /></div>
            <div className="border-s border-[var(--admin-border)] p-4"><Metric label="دين على المدرس" value={summary?.debt ?? 0} tone="danger" /></div>
          </div>

          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h3 className="flex items-center gap-2 font-black text-[var(--admin-text)]"><ClipboardList className="h-5 w-5 text-[var(--admin-primary)]" /> دفتر اتفاقات المدرس</h3>
              <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">الاتفاق الأحدث والأنسب لنوع المحتوى هو الذي يُستخدم عند تسجيل العملية.</p>
            </div>
            <button type="button" onClick={openCreate} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-[var(--admin-primary-contrast)] hover:opacity-90"><Plus className="h-4 w-4" /> اتفاق جديد</button>
          </div>

          {isLoading ? (
            <div className="border border-[var(--admin-border)] px-5 py-10 text-center text-sm font-bold text-[var(--admin-muted)]">جارِ تحميل الحساب...</div>
          ) : agreements.length === 0 ? (
            <div className="border border-dashed border-[var(--admin-border-strong)] bg-[var(--admin-card-soft)] px-6 py-10 text-center">
              <FileText className="mx-auto h-7 w-7 text-[var(--admin-muted)]" />
              <p className="mt-3 font-black text-[var(--admin-text)]">لا توجد اتفاقات مسجلة لهذا المدرس</p>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">أضف الاتفاق الافتراضي أولاً، ثم خصص الحصة أو الفيديو أو الباقة عندما تحتاج نسبة مختلفة.</p>
            </div>
          ) : (
            <div className="overflow-x-auto border border-[var(--admin-border)]">
              <table className="w-full min-w-[840px] text-right text-sm">
                <thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]"><tr>
                  <th className="px-4 py-3 font-black">النطاق</th><th className="px-4 py-3 font-black">وقت الاستحقاق</th><th className="px-4 py-3 font-black">طريقة الحساب</th><th className="px-4 py-3 font-black">الفترة</th><th className="px-4 py-3 font-black">التوثيق</th><th className="px-4 py-3" aria-label="إجراء" />
                </tr></thead>
                <tbody className="divide-y divide-[var(--admin-border)]">
                  {agreements.map((agreement) => <tr key={agreement.id} className={!agreement.isActive ? 'opacity-55' : 'hover:bg-[var(--admin-hover)]'}>
                    <td className="px-4 py-3"><p className="font-black text-[var(--admin-text)]">{scopeLabel(agreement)}</p>{agreement.scopeId && <p className="mt-1 max-w-40 truncate font-mono text-xs text-[var(--admin-muted)]" title={agreement.scopeId}>{agreement.scopeId}</p>}</td>
                    <td className="px-4 py-3 text-xs font-bold text-[var(--admin-text)]">{triggerLabels[agreement.trigger]}</td>
                    <td className="px-4 py-3"><p className="font-mono font-black text-[var(--admin-primary)]">{agreement.allocationMode === 'Percentage' ? `%${agreement.allocationValue}` : formatCurrency(agreement.allocationValue)}</p><p className="text-xs text-[var(--admin-muted)]">{allocationLabels[agreement.allocationMode]} · {agreement.priceBasis === 'Gross' ? 'الإجمالي' : 'بعد الخصم'}</p></td>
                    <td className="px-4 py-3 text-xs font-bold text-[var(--admin-muted)]">من {new Date(agreement.effectiveFrom).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'Africa/Cairo' })}<br />{agreement.effectiveTo ? `حتى ${new Date(agreement.effectiveTo).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'Africa/Cairo' })}` : 'مستمر'}</td>
                    <td className="max-w-56 px-4 py-3 text-xs text-[var(--admin-muted)]">{agreement.reason}</td>
                    <td className="px-4 py-3"><button type="button" onClick={() => openEdit(agreement)} className="inline-flex min-h-9 items-center gap-1 rounded-lg border border-[var(--admin-border)] px-3 text-xs font-bold text-[var(--admin-text)] hover:bg-[var(--admin-card-soft)]"><Pencil className="h-3.5 w-3.5" /> استبدال</button></td>
                  </tr>)}
                </tbody>
              </table>
            </div>
          )}

          {!isLoading && summary && (
            <TeacherFinanceOperationsWorkspace
              teacherId={teacherId}
              teacherName={selectedTeacher?.fullName ?? 'المدرس'}
              agreements={agreements}
              onChanged={loadTeacherFinance}
            />
          )}
        </>
      )}

      <AdminModal open={isModalOpen} onClose={() => setIsModalOpen(false)} title={editingAgreement ? 'استبدال اتفاق مالي' : 'إضافة اتفاق مالي'} subtitle={editingAgreement ? 'يُحفظ الاتفاق السابق في السجل وتبدأ القاعدة الجديدة من تاريخها.' : 'حدد قاعدة واضحة وقابلة للمراجعة قبل تسجيل المبيعات أو الأكواد.'} maxWidth="max-w-2xl">
        <form onSubmit={saveAgreement} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="text-sm font-bold text-[var(--admin-text)]">نطاق الاتفاق<select value={scopeChoice} onChange={(event) => changeScopeChoice(event.target.value as AgreementScopeChoice)} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none focus:border-[var(--admin-primary)]"><optgroup label="نطاقات عامة">{aggregateScopeOptions.map((option) => <option key={option.choice} value={option.choice}>{option.label}</option>)}</optgroup><optgroup label="عنصر محدد">{specificScopeOptions.map((option) => <option key={option.choice} value={option.choice}>{option.label}</option>)}</optgroup></select></label>
            <label className="text-sm font-bold text-[var(--admin-text)]">موعد الاستحقاق<select value={draft.trigger} onChange={(e) => setDraft((current) => ({ ...current, trigger: e.target.value as TeacherAgreementTrigger }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none">{(Object.keys(triggerLabels) as TeacherAgreementTrigger[]).map((trigger) => <option key={trigger} value={trigger}>{triggerLabels[trigger]}</option>)}</select></label>
          </div>
          {findScopeOption(scopeChoice).requiresId && <label className="block text-sm font-bold text-[var(--admin-text)]">معرّف العنصر المرتبط<input required value={draft.scopeId ?? ''} onChange={(e) => setDraft((current) => ({ ...current, scopeId: e.target.value }))} placeholder="الصق معرّف العنصر المحدد فقط" className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none focus:border-[var(--admin-primary)]" /></label>}
          <div className="grid gap-4 sm:grid-cols-3">
            <label className="text-sm font-bold text-[var(--admin-text)]">طريقة الحساب<select value={draft.allocationMode} onChange={(e) => setDraft((current) => ({ ...current, allocationMode: e.target.value as TeacherAgreementAllocationMode }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none">{(Object.keys(allocationLabels) as TeacherAgreementAllocationMode[]).map((mode) => <option key={mode} value={mode}>{allocationLabels[mode]}</option>)}</select></label>
            <label className="text-sm font-bold text-[var(--admin-text)]">القيمة<input required min="0" step="0.01" type="number" value={draft.allocationValue} onChange={(e) => setDraft((current) => ({ ...current, allocationValue: Number(e.target.value) }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none" /></label>
            <label className="text-sm font-bold text-[var(--admin-text)]">أساس السعر<select value={draft.priceBasis} onChange={(e) => setDraft((current) => ({ ...current, priceBasis: e.target.value as TeacherPriceBasis }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none"><option value="NetAfterDiscount">بعد الخصم</option><option value="Gross">الإجمالي قبل الخصم</option></select></label>
          </div>
          <div className="grid gap-4 sm:grid-cols-2"><label className="text-sm font-bold text-[var(--admin-text)]">ساري من<input required type="date" value={draft.effectiveFrom} onChange={(e) => setDraft((current) => ({ ...current, effectiveFrom: e.target.value }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none" /></label><label className="text-sm font-bold text-[var(--admin-text)]">ينتهي في (اختياري)<input type="date" value={draft.effectiveTo ?? ''} onChange={(e) => setDraft((current) => ({ ...current, effectiveTo: e.target.value }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none" /></label></div>
          <label className="block text-sm font-bold text-[var(--admin-text)]">سبب الاتفاق والتوثيق<textarea required rows={3} value={draft.reason} onChange={(e) => setDraft((current) => ({ ...current, reason: e.target.value }))} placeholder="مثال: نسبة فيديوهات مراجعة الترم الثاني المتفق عليها" className="mt-1.5 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm font-normal outline-none" /></label>
          <div className="flex justify-end gap-2 border-t border-[var(--admin-border)] pt-4"><button type="button" onClick={() => setIsModalOpen(false)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)]">إلغاء</button><button disabled={isSaving} type="submit" className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-60"><Save className="h-4 w-4" />{isSaving ? 'جارٍ الحفظ...' : editingAgreement ? 'حفظ البديل' : 'حفظ الاتفاق'}</button></div>
        </form>
      </AdminModal>
    </section>
  );
}
