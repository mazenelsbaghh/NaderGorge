'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  ClipboardList,
  FileText,
  Pencil,
  Plus,
  Save,
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
  TeacherPriceBasis,
} from './types';
import { TeacherCodeBatches } from './TeacherCodeBatches';
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
  AllSources: 'الشراء والأكواد بنفس الاتفاق',
  ContentSale: 'عند شراء المحتوى',
  CodeDelivery: 'عند تأكيد تسليم الأكواد',
  CodeActivation: 'عند تفعيل الكود',
};

const allocationLabels: Record<TeacherAgreementAllocationMode, string> = {
  Percentage: 'نسبة المدرّس من البيع',
  FixedPerSale: 'مبلغ المدرّس لكل بيع أو كود',
  FixedPerCode: 'مبلغ المدرّس لكل بيع أو كود',
  FixedPerBatch: 'مبلغ المدرس الثابت للدفعة',
  PlatformFixedPerUnit: 'نصيب المنصة الثابت لكل بيع / كود',
};

const freshDraft = (): AgreementDraft => ({
  scopeType: 'Default',
  scopeId: '',
  trigger: 'AllSources',
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

export function TeacherFinanceCenterWorkspace({ teacher, onChanged }: { teacher: TeacherDto; onChanged: () => void }) {
  const teacherId = teacher.id;
  const [hasError, setHasError] = useState(false);
  const [agreements, setAgreements] = useState<TeacherAgreement[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingAgreement, setEditingAgreement] = useState<TeacherAgreement | null>(null);
  const [draft, setDraft] = useState<AgreementDraft>(freshDraft);
  const [scopeChoice, setScopeChoice] = useState<AgreementScopeChoice>('Everything');
  const [isSaving, setIsSaving] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const visibleAgreements = agreements.filter(agreement => showHistory || agreement.isActive);

  const loadTeacherFinance = useCallback(async () => {
    setIsLoading(true);
    setHasError(false);
    try {
      setAgreements(await financeService.getTeacherAgreements(teacherId));
    } catch {
      setHasError(true);
    } finally {
      setIsLoading(false);
    }
  }, [teacherId]);

  const refreshAccount = async () => { onChanged(); };

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
      trigger: 'AllSources',
      allocationMode: agreement.allocationMode,
      allocationValue: agreement.allocationValue,
      priceBasis: agreement.priceBasis,
      effectiveFrom: agreement.effectiveFrom.slice(0, 10),
      effectiveTo: '',
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
    if (draft.allocationMode === 'FixedPerBatch') { toast.error('اختار مبلغ لكل كود أو نسبة علشان ينفع نفس الاتفاق للشراء والأكواد'); return; }
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
      trigger: 'AllSources',
      effectiveTo: undefined,
      scopeType: selectedScope.scopeType,
      scopeId: selectedScope.requiresId ? draft.scopeId?.trim() : undefined,
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
      onChanged();
    } catch (error: any) {
      toast.error(error?.response?.data?.message || 'حدث خطأ أثناء حفظ الاتفاق');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <section id="finance-panel-teacher-center" role="tabpanel" aria-label="مركز مالية المدرسين">
      <details className="rounded-xl border border-[var(--admin-border)] p-4">
        <summary className="min-h-9 cursor-pointer font-bold">الاتفاق على حساب الأرباح</summary>
        <p className="my-4 text-sm leading-6 text-[var(--admin-muted)]">الربح بيتحسب بالاتفاق الساري وقت العملية. تغيير الاتفاق بيطبق من تاريخه، والحركات القديمة بتحتفظ بحسابها المسجل.</p>
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h3 className="flex items-center gap-2 font-black text-[var(--admin-text)]"><ClipboardList className="h-5 w-5 text-[var(--admin-primary)]" /> طريقة حساب المدرّس</h3>
              <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">الاتفاق الخاص بالمحتوى له الأولوية، وبعده الاتفاق العام. السجل القديم محفوظ للمراجعة.</p>
            </div>
            <button type="button" onClick={openCreate} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-[var(--admin-primary-contrast)] hover:opacity-90"><Plus className="h-4 w-4" /> اتفاق جديد</button>
          </div>

          <label className="mb-4 flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={showHistory} onChange={event => setShowHistory(event.target.checked)} />إظهار الاتفاقات السابقة</label>
          {hasError ? <p role="alert">تعذر تحميل الاتفاقات. <button type="button" className="min-h-11 px-3 underline" onClick={() => void loadTeacherFinance()}>إعادة المحاولة</button></p> : isLoading ? (
            <div className="border border-[var(--admin-border)] px-5 py-10 text-center text-sm font-bold text-[var(--admin-muted)]">جارِ تحميل الحساب...</div>
          ) : visibleAgreements.length === 0 ? (
            <div className="border border-dashed border-[var(--admin-border-strong)] bg-[var(--admin-card-soft)] px-6 py-10 text-center">
              <FileText className="mx-auto h-7 w-7 text-[var(--admin-muted)]" />
              <p className="mt-3 font-black text-[var(--admin-text)]">لا يوجد اتفاق حالي مسجل لهذا المدرس</p>
              <p className="mt-1 text-sm text-[var(--admin-muted)]">القاعدة الاحتياطية القديمة: {teacher.commissionRate}٪ للمدرّس. أضف اتفاقًا واضحًا لكل المحتوى، ثم استثناءات لما تحتاج.</p>
            </div>
          ) : (
            <div className="overflow-x-auto border border-[var(--admin-border)]">
              <table className="w-full min-w-[840px] text-right text-sm">
                <thead className="bg-[var(--admin-card-soft)] text-xs text-[var(--admin-muted)]"><tr>
                  <th className="px-4 py-3 font-black">النطاق</th><th className="px-4 py-3 font-black">ينطبق على</th><th className="px-4 py-3 font-black">طريقة الحساب</th><th className="px-4 py-3 font-black">الفترة</th><th className="px-4 py-3 font-black">التوثيق</th><th className="px-4 py-3" aria-label="إجراء" />
                </tr></thead>
                <tbody className="divide-y divide-[var(--admin-border)]">
                  {visibleAgreements.map((agreement) => <tr key={agreement.id} className={!agreement.isActive ? 'opacity-55' : 'hover:bg-[var(--admin-hover)]'}>
                    <td className="px-4 py-3"><p className="font-black text-[var(--admin-text)]">{scopeLabel(agreement)}</p><span className="text-xs text-[var(--admin-muted)]">{!agreement.isActive ? 'سابق' : new Date(agreement.effectiveFrom) > new Date() ? 'يبدأ لاحقًا' : 'ساري'}</span>{agreement.scopeId && <p className="mt-1 max-w-40 truncate font-mono text-xs text-[var(--admin-muted)]" title={agreement.scopeId}>{agreement.scopeId}</p>}</td>
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

      </details>
      <TeacherCodeBatches agreementVersion={agreements.map(agreement => agreement.id).join(',')} key={teacherId} teacherId={teacherId} teacherName={teacher.fullName} onChanged={onChanged} />
      <details className="mt-4 rounded-xl border border-[var(--admin-border)] p-4">
        <summary className="min-h-9 cursor-pointer font-bold">صرف مستحقات أو تسجيل مرتجع</summary>
          {!isLoading && !hasError && (
            <TeacherFinanceOperationsWorkspace
              teacherId={teacherId}
              teacherName={teacher.fullName}
              onChanged={refreshAccount}
            />
          )}
      </details>

      <AdminModal open={isModalOpen} onClose={() => setIsModalOpen(false)} title={editingAgreement ? 'استبدال اتفاق مالي' : 'إضافة اتفاق مالي'} subtitle={editingAgreement ? 'يُحفظ الاتفاق السابق في السجل وتبدأ القاعدة الجديدة من تاريخها.' : 'حدد قاعدة واضحة وقابلة للمراجعة قبل تسجيل المبيعات أو الأكواد.'} maxWidth="max-w-2xl">
        <form onSubmit={saveAgreement} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="text-sm font-bold text-[var(--admin-text)]">نطاق الاتفاق<select value={scopeChoice} onChange={(event) => changeScopeChoice(event.target.value as AgreementScopeChoice)} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none focus:border-[var(--admin-primary)]"><optgroup label="نطاقات عامة">{aggregateScopeOptions.map((option) => <option key={option.choice} value={option.choice}>{option.label}</option>)}</optgroup><optgroup label="عنصر محدد">{specificScopeOptions.map((option) => <option key={option.choice} value={option.choice}>{option.label}</option>)}</optgroup></select></label>
            <p className="rounded-xl bg-[var(--admin-card-soft)] p-3 text-sm leading-6">نفس الاتفاق للشراء ولكل الأكواد. بيستبدل قواعد الحساب القديمة لنفس النطاق من تاريخه. حساب الأكواد وقت التسليم أو الاستخدام بتختاره لكل دفعة تحت.</p>
          </div>
          {findScopeOption(scopeChoice).requiresId && <label className="block text-sm font-bold text-[var(--admin-text)]">معرّف العنصر المرتبط<input required value={draft.scopeId ?? ''} onChange={(e) => setDraft((current) => ({ ...current, scopeId: e.target.value }))} placeholder="الصق معرّف العنصر المحدد فقط" className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none focus:border-[var(--admin-primary)]" /></label>}
          <div className="grid gap-4 sm:grid-cols-3">
            <label className="text-sm font-bold text-[var(--admin-text)]">طريقة الحساب<select value={draft.allocationMode} onChange={(e) => setDraft((current) => ({ ...current, allocationMode: e.target.value as TeacherAgreementAllocationMode }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none">{draft.allocationMode === 'FixedPerBatch' && <option value="FixedPerBatch" disabled>مبلغ للدفعة — اختر طريقة موحدة</option>}{(Object.keys(allocationLabels) as TeacherAgreementAllocationMode[]).filter(mode => mode !== 'FixedPerBatch').map((mode) => <option key={mode} value={mode}>{allocationLabels[mode]}</option>)}</select></label>
            <label className="text-sm font-bold text-[var(--admin-text)]">{draft.allocationMode === 'Percentage' ? 'نسبة المدرّس (%)' : draft.allocationMode === 'PlatformFixedPerUnit' ? 'مبلغ المنصّة لكل بيع أو كود' : 'مبلغ المدرّس لكل بيع أو كود'}<input required min="0" max={draft.allocationMode === 'Percentage' ? 100 : undefined} step="0.01" type="number" value={draft.allocationValue} onChange={(e) => setDraft((current) => ({ ...current, allocationValue: Number(e.target.value) }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none" /></label>
            <label className="text-sm font-bold text-[var(--admin-text)]">أساس السعر<select value={draft.priceBasis} onChange={(e) => setDraft((current) => ({ ...current, priceBasis: e.target.value as TeacherPriceBasis }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none"><option value="NetAfterDiscount">بعد الخصم</option><option value="Gross">الإجمالي قبل الخصم</option></select></label>
          </div>
          <div className="grid gap-4 sm:grid-cols-2"><label className="text-sm font-bold text-[var(--admin-text)]">ساري من<input required type="date" value={draft.effectiveFrom} onChange={(e) => setDraft((current) => ({ ...current, effectiveFrom: e.target.value }))} className="mt-1.5 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm font-normal outline-none" /></label></div>
          <label className="block text-sm font-bold text-[var(--admin-text)]">سبب الاتفاق والتوثيق<textarea required rows={3} value={draft.reason} onChange={(e) => setDraft((current) => ({ ...current, reason: e.target.value }))} placeholder="مثال: نسبة فيديوهات مراجعة الترم الثاني المتفق عليها" className="mt-1.5 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm font-normal outline-none" /></label>
          <div className="flex justify-end gap-2 border-t border-[var(--admin-border)] pt-4"><button type="button" onClick={() => setIsModalOpen(false)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)]">إلغاء</button><button disabled={isSaving} type="submit" className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-60"><Save className="h-4 w-4" />{isSaving ? 'جارٍ الحفظ...' : editingAgreement ? 'حفظ البديل' : 'حفظ الاتفاق'}</button></div>
        </form>
      </AdminModal>
    </section>
  );
}
