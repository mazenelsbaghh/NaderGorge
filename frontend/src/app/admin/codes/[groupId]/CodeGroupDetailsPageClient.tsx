'use client';

import { devConsole } from '@/utils/dev-console';
import { useEffect, useMemo, useState } from 'react';
import type { LucideIcon } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowRight, Banknote, Building2, Clock3, Download, KeyRound, Link as LinkIcon, PencilLine, Percent, Printer, Save, Search, Sparkles, Trash2, User as UserIcon, UserRound, Zap } from 'lucide-react';
import Link from 'next/link';
import { isAxiosError } from 'axios';

import {
  AdminPage,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminPageSkeleton,
  AdminModal,
} from '@/components/admin';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { cairoDateTimeLocalToIso, formatCairoDateTimeLocal, formatDate } from '@/components/admin/admin-utils';
import { adminService, CodeDetailDto, CodeGroupDto } from '@/services/admin-service';
import { adminSalesService, type PrintableTemplateDto } from '@/services/admin-sales-service';
import { teacherService, type TeacherDto } from '@/services/teacher-service';
import { QrDisplay } from '@/components/codes/QrDisplay';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import { invalidateMany } from '@/lib/cache-invalidation';

function getCodeTypeLabel(type: CodeGroupDto['codeType']) {
  const labels: Record<CodeGroupDto['codeType'], string> = {
    Package: 'باكدج',
    Term: 'ترم',
    Month: 'شهر / قسم',
    Lesson: 'حصة',
    Video: 'فيديوهات',
    Exam: 'امتحان',
    Balance: 'شحن رصيد',
  };

  return labels[type] ?? type;
}

function InfoCell({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2">
      <div className="text-sm font-bold text-[var(--admin-muted)]">{label}</div>
      <div className="mt-1 break-all text-xs font-black text-[var(--admin-text)]">{value}</div>
    </div>
  );
}

function Segmented({
  label,
  value,
  options,
  disabled = false,
  onChange,
}: {
  label: string;
  value: string;
  options: Array<{ value: string; label: string; icon: LucideIcon }>;
  disabled?: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <div>
      <div className="mb-1 text-xs font-bold text-[var(--admin-muted)]">{label}</div>
      <div className="grid grid-cols-2 gap-2 rounded-xl bg-[var(--admin-card)] p-1">
        {options.map((option) => {
          const Icon = option.icon;
          const active = value === option.value;
          return (
            <button
              key={option.value}
              type="button"
              disabled={disabled}
              onClick={() => onChange(option.value)}
              className={`flex items-center justify-center gap-2 rounded-lg px-3 py-2 text-xs font-bold transition disabled:cursor-not-allowed disabled:opacity-50 ${
                active
                  ? 'bg-[var(--admin-primary)] text-white shadow-sm'
                  : 'text-[var(--admin-muted)] hover:bg-[var(--admin-card-strong)]'
              }`}
            >
              <Icon className="h-4 w-4" />
              {option.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}

export default function CodeGroupDetailsPageClient({ mode = 'admin' }: { mode?: 'admin' | 'assistant' }) {
  const isAssistant = mode === 'assistant';
  const codesBasePath = isAssistant ? '/assistant/codes' : '/admin/codes';
  const PageShell = isAssistant ? AssistantPage : AdminPage;
  const params = useParams();
  const router = useRouter();
  const groupId = params.groupId as string;

  const [group, setGroup] = useState<CodeGroupDto | null>(null);
  const [codes, setCodes] = useState<CodeDetailDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  
  const [loading, setLoading] = useState(true);
  const [codesLoading, setCodesLoading] = useState(true);
  const [savingOverview, setSavingOverview] = useState(false);
  const [removingUnused, setRemovingUnused] = useState(false);
  const [showRemoveUnusedModal, setShowRemoveUnusedModal] = useState(false);
  const [keepEmptyGroup, setKeepEmptyGroup] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [showQrPrint, setShowQrPrint] = useState(false);
  const [templates, setTemplates] = useState<PrintableTemplateDto[]>([]);
  const [templatesLoading, setTemplatesLoading] = useState(false);
  const [templatesLoaded, setTemplatesLoaded] = useState(false);
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const [overviewForm, setOverviewForm] = useState({
    name: '',
    teacherId: '',
    expiresAt: '',
    revenueOwner: 'Teacher' as 'Teacher' | 'Platform',
    revenueAllocationMode: 'Percentage' as 'Percentage' | 'FixedAmount',
    revenueAllocationValue: '',
    accountingTiming: 'OnActivation' as 'OnActivation' | 'Immediate',
  });

  useEffect(() => {
    async function loadGroupData() {
      const groupsPromise = adminService.listCodeGroups();
      const codesPromise = adminService.getCodeGroupDetails(groupId);

      try {
        setLoading(true);
        const [groupsData, teachersResponse] = await Promise.all([
          groupsPromise,
          teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] })),
        ]);

        const foundGroup = groupsData?.find((g) => g.id === groupId);
        if (foundGroup) {
          setGroup(foundGroup);
          setTeachers(teachersResponse.data ?? []);
        } else {
          toast.error('مجموعة الأكواد غير موجودة');
          router.push(codesBasePath);
          return;
        }
      } catch (error) {
        devConsole.error(error);
        toast.error('تعذر تحميل بيانات المجموعة');
      } finally {
        setLoading(false);
      }

      try {
        setCodesLoading(true);
        const data = await codesPromise;
        setCodes(data);
      } catch (error) {
        devConsole.error(error);
        toast.error('تعذر تحميل الأكواد');
      } finally {
        setCodesLoading(false);
      }
    }

    if (groupId) {
      void loadGroupData();
    }
  }, [codesBasePath, groupId, router]);

  useEffect(() => {
    if (!group) return;

    setOverviewForm({
      name: group.name || '',
      teacherId: group.teacherId || '',
      expiresAt: group.expiresAt ? formatCairoDateTimeLocal(group.expiresAt) : '',
      revenueOwner: group.revenueOwner || (group.teacherId ? 'Teacher' : 'Platform'),
      revenueAllocationMode: group.revenueAllocationMode === 'FixedAmount' ? 'FixedAmount' : 'Percentage',
      revenueAllocationValue: group.revenueAllocationValue != null ? String(group.revenueAllocationValue) : '',
      accountingTiming: group.accountingTiming || 'OnActivation',
    });
  }, [group]);

  const filteredCodes = useMemo(() => {
    if (!searchQuery.trim()) return codes;
    const q = searchQuery.toLowerCase().trim();
    return codes.filter((c) => 
      c.code.toLowerCase().includes(q) ||
      String(c.serialNumber).includes(q) ||
      (c.usedByUserId && c.usedByUserId.toLowerCase().includes(q)) ||
      (c.usedByStudentName && c.usedByStudentName.toLowerCase().includes(q)) ||
      (c.usedByStudentPhone && c.usedByStudentPhone.toLowerCase().includes(q))
    );
  }, [codes, searchQuery]);

  const selectedTemplate = useMemo(
    () => templates.find((template) => template.id === selectedTemplateId) ?? null,
    [templates, selectedTemplateId]
  );

  const teacherNameMap = useMemo(
    () => Object.fromEntries(teachers.map((teacher) => [teacher.id, teacher.fullName])),
    [teachers]
  );

  const targetSummary = useMemo(() => {
    if (!group) return [];
    return [
      { label: 'نوع الكود', value: getCodeTypeLabel(group.codeType) },
      { label: 'هدف الباكدج', value: group.packageId || '—' },
      { label: 'الترم', value: group.termId || '—' },
      { label: 'الشهر / القسم', value: group.contentSectionId || '—' },
      { label: 'الحصة', value: group.lessonId || '—' },
      { label: 'الامتحان', value: group.examId || '—' },
      { label: 'نوع الفيديو', value: group.videoTypeId || '—' },
      { label: 'قيمة الرصيد', value: group.balanceAmount != null ? `${group.balanceAmount} ج.م` : '—' },
    ].filter((item) => item.value !== '—' || ['نوع الكود'].includes(item.label));
  }, [group]);

  async function loadTemplatesIfNeeded() {
    if (templatesLoaded || templatesLoading) return;

    setTemplatesLoading(true);
    try {
      const templateData = await adminSalesService.templates();
      const activeTemplates = templateData.filter((template) => template.isActive);
      setTemplates(activeTemplates);
      setSelectedTemplateId((current) => current || activeTemplates[0]?.id || '');
      setTemplatesLoaded(true);
    } catch (error) {
      devConsole.error(error);
      toast.error('تعذر تحميل قوالب الطباعة');
    } finally {
      setTemplatesLoading(false);
    }
  }

  function openQrPrint() {
    setShowQrPrint(true);
    void loadTemplatesIfNeeded();
  }

  function exportCsv() {
    if (!group || codes.length === 0) return;

    const header = 'SerialNumber,Code,IsUsed,UsedAt,UsedByUserId,StudentName,StudentPhone,RedemptionSummary\n';
    const rows = codes
      .map(
        (code) =>
          `"${code.serialNumber}","${code.code}","${code.isUsed}","${code.usedAt ? formatDate(code.usedAt) : ''}","${
            code.usedByUserId || ''
          }","${code.usedByStudentName || ''}","${code.usedByStudentPhone || ''}","${code.redemptionSummary || ''}"`
      )
      .join('\n');
    const blob = new Blob([header + rows], { type: 'text/csv;charset=utf-8;' });
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `codes_${group.name || group.id}.csv`;
    anchor.click();
    window.URL.revokeObjectURL(url);
  }

  async function saveOverview() {
    if (!group) return;

    try {
      setSavingOverview(true);
      const response = await adminService.updateCodeGroupSettings(group.id, {
        name: overviewForm.name.trim() || group.name,
        teacherId: overviewForm.teacherId || null,
        expiresAt: overviewForm.expiresAt ? cairoDateTimeLocalToIso(overviewForm.expiresAt) : null,
        revenueOwner: group.codeType === 'Balance' ? null : overviewForm.revenueOwner,
        revenueAllocationMode:
          group.codeType === 'Balance' || !overviewForm.revenueAllocationValue
            ? null
            : overviewForm.revenueAllocationMode,
        revenueAllocationValue:
          group.codeType === 'Balance' || !overviewForm.revenueAllocationValue
            ? null
            : Number(overviewForm.revenueAllocationValue),
        accountingTiming: group.codeType === 'Balance' ? 'OnActivation' : overviewForm.accountingTiming,
      });

      if (!response.success) {
        toast.error(response.message || 'تعذر حفظ بيانات المجموعة');
        return;
      }

      invalidateMany(['codes:groups', 'reports']);
      const groupsData = await adminService.listCodeGroups();
      const nextGroup = groupsData?.find((item) => item.id === group.id);
      if (nextGroup) setGroup(nextGroup);
      toast.success('تم حفظ بيانات المجموعة');
    } catch (error: unknown) {
      devConsole.error(error);
      const message = isAxiosError<{ message?: string }>(error)
        ? error.response?.data?.message || 'تعذر حفظ بيانات المجموعة'
        : 'تعذر حفظ بيانات المجموعة';
      toast.error(message);
    } finally {
      setSavingOverview(false);
    }
  }

  async function removeUnusedCodes() {
    if (!group) return;
    try {
      setRemovingUnused(true);
      const response = await adminService.removeUnusedCodes(group.id, keepEmptyGroup);
      if (!response.success || !response.data) {
        toast.error(response.message || 'تعذر مسح الأكواد غير المستخدمة');
        return;
      }
      invalidateMany(['codes:groups', 'reports']);
      toast.success(`تم مسح ${response.data.removedCount} كود غير مستخدم.`);
      setShowRemoveUnusedModal(false);
      if (response.data.groupDeleted) {
        router.push(codesBasePath);
        return;
      }
      const [groupsData, codesData] = await Promise.all([
        adminService.listCodeGroups(),
        adminService.getCodeGroupDetails(group.id),
      ]);
      setGroup(groupsData?.find((item) => item.id === group.id) ?? null);
      setCodes(codesData ?? []);
    } catch (error) {
      devConsole.error(error);
      toast.error('تعذر مسح الأكواد غير المستخدمة');
    } finally {
      setRemovingUnused(false);
    }
  }

  const codeColumns: AdminColumn<CodeDetailDto>[] = [
    {
      key: 'serialNumber',
      label: 'السيريال (S/N)',
      render: (c) => (
        <span className="text-xs font-mono font-bold text-[var(--admin-text)]">
          #{c.serialNumber || '—'}
        </span>
      ),
    },
    {
      key: 'code',
      label: 'الكود',
      render: (c) => (
        <span className="bg-[var(--admin-card-strong)] px-3 py-1.5 rounded-xl border border-[var(--admin-border)] font-mono font-bold text-[var(--admin-text)] tracking-wider">
          {c.code}
        </span>
      ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (c) =>
        c.isUsed ? (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 px-3 py-1 text-xs font-bold">
            <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
            مستخدم
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-card-strong)] text-[var(--admin-muted)] px-3 py-1 text-xs font-bold border border-[var(--admin-border)]">
            <span className="h-1.5 w-1.5 rounded-full bg-zinc-400" />
            جديد
          </span>
        ),
    },
    {
      key: 'usedAt',
      label: 'وقت الاستخدام',
      render: (c) => <span className="text-sm text-[var(--admin-muted)] font-medium">{c.usedAt ? formatDate(c.usedAt, { dateStyle: 'medium', timeStyle: 'short' }) : '—'}</span>,
    },
    {
      key: 'redemptionSummary',
      label: 'استخدم في',
      render: (c) => <span className="text-sm font-bold text-[var(--admin-text)]">{c.redemptionSummary}</span>,
    },
    {
      key: 'usedBy',
      label: 'المستخدم / الطالب',
      render: (c) => {
        if (!c.isUsed || !c.usedByUserId) return <span className="text-[var(--admin-muted)]">—</span>;
        return (
          <Link href={`/admin/users/${c.usedByUserId}`} prefetch={false} className="group inline-flex flex-col items-start hover:underline">
            <span className="font-bold text-[var(--admin-primary)] group-hover:text-[var(--admin-primary-strong)] flex items-center gap-1">
              <UserIcon size={14} className="text-[var(--admin-primary)] animate-pulse" />
              {c.usedByStudentName || 'طالب مجهول الاسم'}
            </span>
            {c.usedByStudentPhone && (
              <span className="text-xs text-[var(--admin-muted)] font-mono mt-0.5">{c.usedByStudentPhone}</span>
            )}
          </Link>
        );
      },
    },
  ];

  if (loading) {
    return (
      <PageShell
        activePath={codesBasePath as never}
        sectionLabel="إدارة الأكواد"
        pageTitle="تفاصيل مجموعة الأكواد"
        subtitle="جاري تحميل البيانات..."
      >
        <AdminPageSkeleton />
      </PageShell>
    );
  }

  return (
    <PageShell
      activePath={codesBasePath as never}
      sectionLabel="إدارة الأكواد"
      pageTitle={group ? `تفاصيل: ${group.name || 'دفعة أكواد'}` : 'تفاصيل المجموعة'}
      subtitle="استعراض الأكواد، سجل الشحن، الربط، وطباعة كود الـ QR."
      action={
        <Link href={codesBasePath} prefetch={false} passHref legacyBehavior>
          <NeumorphButton intent="ghost" size="md">
            <ArrowRight className="h-4 w-4 ml-1.5" />
            العودة للمجموعات
          </NeumorphButton>
        </Link>
      }
    >
      {/* Stats / Info cards */}
      {group && (
        <>
          <section className="mb-6 grid grid-cols-1 gap-6 md:grid-cols-3">
            <AdminStatCard
              variant="light"
              icon={KeyRound}
              label="إجمالي الأكواد"
              value={group.codeCount}
              subtitle={`تاريخ التوليد: ${formatDate(group.createdAt)}`}
            />
            <AdminStatCard
              variant="accent"
              icon={Sparkles}
              label="المستخدمة"
              value={group.usedCount}
              subtitle={`${group.codeCount - group.usedCount} أكواد متبقية`}
            />
            <AdminStatCard
              variant="muted"
              icon={LinkIcon}
              label="الربط"
              value={getCodeTypeLabel(group.codeType)}
              subtitle={group.teacherId ? `المدرس: ${teacherNameMap[group.teacherId] || group.teacherId}` : 'عام للمنصة'}
            />
          </section>

          <section className="mb-8 rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
            <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
              <div>
                <div className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-3 py-1 text-xs font-bold text-[var(--admin-muted)]">
                  <PencilLine className="h-3.5 w-3.5" />
                  نظرة عامة قابلة للتعديل
                </div>
                <h2 className="mt-3 text-xl font-black text-[var(--admin-text-strong)]">بيانات المجموعة وإعدادات الأرباح</h2>
                <p className="mt-1 text-sm font-semibold text-[var(--admin-muted)]">
                  التعديلات على الأرباح تطبق على التفعيلات القادمة، ولا تعيد حساب أرباح سبق تسجيلها.
                </p>
              </div>
              <NeumorphButton type="button" onClick={saveOverview} disabled={savingOverview} loading={savingOverview} intent="primary" size="md">
                <Save className="h-4 w-4 ml-1.5" />
                حفظ التعديل
              </NeumorphButton>
            </div>

            <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
              <div className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                <div className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
                  <KeyRound className="h-4 w-4 text-[var(--admin-primary)]" />
                  بيانات أساسية
                </div>
                <label className="block">
                  <span className="mb-1 block text-xs font-bold text-[var(--admin-muted)]">اسم المجموعة</span>
                  <input
                    value={overviewForm.name}
                    onChange={(event) => setOverviewForm((current) => ({ ...current, name: event.target.value }))}
                    className="admin-input"
                    dir="auto"
                  />
                </label>
                <label className="block">
                  <span className="mb-1 block text-xs font-bold text-[var(--admin-muted)]">تاريخ الانتهاء</span>
                  <input
                    type="datetime-local"
                    value={overviewForm.expiresAt}
                    onChange={(event) => setOverviewForm((current) => ({ ...current, expiresAt: event.target.value }))}
                    className="admin-input [color-scheme:dark]"
                    dir="ltr"
                  />
                </label>
                <div className="grid grid-cols-2 gap-2 text-xs font-bold text-[var(--admin-muted)]">
                  <InfoCell label="تاريخ الإنشاء" value={formatDate(group.createdAt)} />
                  <InfoCell label="آخر تسجيل فوري" value={group.accountingRecordedAt ? formatDate(group.accountingRecordedAt) : 'لا يوجد'} />
                </div>
              </div>

              <div className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                <div className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
                  <LinkIcon className="h-4 w-4 text-[var(--admin-primary)]" />
                  الربط والهدف
                </div>
                <div className="grid grid-cols-1 gap-2">
                  {targetSummary.map((item) => (
                    <InfoCell key={item.label} label={item.label} value={item.value} />
                  ))}
                </div>
                {group.discountPercentage ? (
                  <div className="rounded-xl border border-amber-500/20 bg-amber-500/10 px-3 py-2 text-xs font-bold text-amber-700 dark:text-amber-300">
                    هذه دفعة قديمة عليها خصم محفوظ: {group.discountPercentage}%
                  </div>
                ) : null}
              </div>

              <div className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4">
                <div className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
                  <Banknote className="h-4 w-4 text-[var(--admin-primary)]" />
                  إعدادات الأرباح
                </div>
                <label className="block">
                  <span className="mb-1 block text-xs font-bold text-[var(--admin-muted)]">المدرس</span>
                  <select
                    value={overviewForm.teacherId}
                    onChange={(event) => setOverviewForm((current) => ({ ...current, teacherId: event.target.value }))}
                    className="admin-input"
                  >
                    <option value="">عام للمنصة</option>
                    {teachers.map((teacher) => (
                      <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>
                    ))}
                  </select>
                </label>
                {group.codeType !== 'Balance' ? (
                  <>
                    <Segmented
                      label="الربح تابع لـ"
                      value={overviewForm.revenueOwner}
                      options={[
                        { value: 'Teacher', label: 'المدرس', icon: UserRound },
                        { value: 'Platform', label: 'المنصة', icon: Building2 },
                      ]}
                      onChange={(value) => setOverviewForm((current) => ({ ...current, revenueOwner: value as 'Teacher' | 'Platform' }))}
                    />
                    <Segmented
                      label="طريقة الحساب"
                      value={overviewForm.revenueAllocationMode}
                      options={[
                        { value: 'Percentage', label: 'نسبة', icon: Percent },
                        { value: 'FixedAmount', label: 'مبلغ ثابت', icon: Banknote },
                      ]}
                      onChange={(value) => setOverviewForm((current) => ({ ...current, revenueAllocationMode: value as 'Percentage' | 'FixedAmount' }))}
                    />
                    <input
                      type="number"
                      min={0}
                      max={overviewForm.revenueAllocationMode === 'Percentage' ? 100 : undefined}
                      step="0.01"
                      value={overviewForm.revenueAllocationValue}
                      onChange={(event) => setOverviewForm((current) => ({ ...current, revenueAllocationValue: event.target.value }))}
                      placeholder={overviewForm.revenueAllocationMode === 'Percentage' ? 'مثلاً: 30' : 'مثلاً: 500'}
                      className="admin-input"
                      dir="ltr"
                    />
                    <Segmented
                      label="توقيت التسجيل"
                      value={overviewForm.accountingTiming}
                      disabled={Boolean(group.accountingRecordedAt)}
                      options={[
                        { value: 'Immediate', label: 'فوري', icon: Zap },
                        { value: 'OnActivation', label: 'حسب التفعيل', icon: Clock3 },
                      ]}
                      onChange={(value) => setOverviewForm((current) => ({ ...current, accountingTiming: value as 'OnActivation' | 'Immediate' }))}
                    />
                    {group.accountingRecordedAt ? (
                      <p className="text-xs font-bold text-[var(--admin-muted)]">توقيت التسجيل مقفول لأن أرباح فورية اتسجلت بالفعل.</p>
                    ) : null}
                  </>
                ) : (
                  <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-xs font-bold text-[var(--admin-muted)]">
                    كود شحن الرصيد لا يسجل أرباح محتوى.
                  </div>
                )}
              </div>
            </div>
          </section>
        </>
      )}

      {/* Toolbar / Search & Actions */}
      <div className="mb-8 flex flex-col md:flex-row gap-4 justify-between items-center bg-[var(--admin-card)] p-4 rounded-3xl border border-[var(--admin-border)] shadow-sm">
        
        {/* Search Input */}
        <div className="flex items-center bg-[var(--admin-surface)] rounded-2xl border border-[var(--admin-border)] px-4 py-2.5 w-full md:max-w-md">
          <Search className="text-[var(--admin-muted)] w-5 h-5 ml-2.5" />
          <input
            type="text"
            placeholder="ابحث عن كود، اسم طالب، أو رقم هاتف..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="bg-transparent border-none outline-none text-sm text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] w-full text-right"
            dir="rtl"
          />
        </div>

        {/* Action Tabs & Buttons */}
        <div className="flex flex-wrap gap-3 items-center justify-end w-full md:w-auto">
          <div className="flex gap-1.5 p-1 bg-[var(--admin-surface)] rounded-2xl border border-[var(--admin-border)]">
            <button
              onClick={() => setShowQrPrint(false)}
              className={`px-5 py-2 rounded-xl text-sm font-bold transition-[color,background-color,border-color,opacity,transform,box-shadow] ${
                !showQrPrint 
                  ? 'bg-[var(--admin-primary)] text-white shadow-sm' 
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              سجل الشحن والتفاصيل
            </button>
            <button
              onClick={openQrPrint}
              className={`px-5 py-2 rounded-xl text-sm font-bold flex items-center gap-2 transition-[color,background-color,border-color,opacity,transform,box-shadow] ${
                showQrPrint 
                  ? 'bg-[var(--admin-primary)] text-white shadow-sm' 
                  : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              <Printer size={16} />
              طباعة QR
            </button>
          </div>

          <NeumorphButton type="button" onClick={exportCsv} intent="ghost" size="md">
            <Download className="h-4 w-4 ml-1.5" />
            تصدير CSV
          </NeumorphButton>
          <NeumorphButton type="button" onClick={() => setShowRemoveUnusedModal(true)} intent="ghost" size="md" disabled={!codes.some((code) => !code.isUsed)}>
            <Trash2 className="h-4 w-4 ml-1.5" />
            مسح غير المستخدمة
          </NeumorphButton>
        </div>
      </div>

      {/* Content Area */}
      <div className="admin-panel mt-6">
        {showQrPrint ? (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 print:hidden">
              <div>
                <h3 className="text-sm font-black text-[var(--admin-text)]">اختيار قالب الطباعة</h3>
                <p className="text-xs font-bold text-[var(--admin-muted)]">كل كود سيظهر في صفحة مستقلة عند تحميل PDF.</p>
              </div>
              <select
                value={selectedTemplateId}
                onChange={(event) => setSelectedTemplateId(event.target.value)}
                className="min-h-10 min-w-60 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-bold text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
              >
                <option value="">القالب الافتراضي</option>
                {templates.map((template) => (
                  <option key={template.id} value={template.id}>
                    {template.name} ({template.widthMm}x{template.heightMm}mm)
                  </option>
                ))}
              </select>
              {templatesLoading ? (
                <span className="text-xs font-bold text-[var(--admin-muted)]">
                  جاري تحميل القوالب...
                </span>
              ) : null}
            </div>
            <QrDisplay
              codes={codes.map((c) => ({ code: c.code, serialNumber: c.serialNumber }))}
              groupName={group ? `${group.name || 'دفعة'} - ${formatDate(group.createdAt)}` : 'Batch'}
              template={selectedTemplate}
            />
          </div>
        ) : (
          <AdminDataTable
            data={filteredCodes}
            columns={codeColumns}
            loading={codesLoading}
            rowKey={(c) => c.code}
            emptyMessage="لا توجد أكواد تطابق البحث."
          />
        )}
      </div>
      <AdminModal
        open={showRemoveUnusedModal}
        onClose={() => !removingUnused && setShowRemoveUnusedModal(false)}
        title="مسح الأكواد غير المستخدمة"
      >
        <div className="space-y-4 text-right">
          <p className="text-sm font-semibold text-[var(--admin-muted)]">
            سيتم حذف {codes.filter((code) => !code.isUsed).length} كود غير مستخدم فقط. الأكواد التي تم شحنها/استخدامها ستبقى محفوظة في السجل.
          </p>
          <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-[var(--admin-border)] p-3">
            <input type="checkbox" checked={keepEmptyGroup} onChange={(event) => setKeepEmptyGroup(event.target.checked)} className="mt-1" />
            <span className="text-sm font-bold text-[var(--admin-text)]">
              الإبقاء على سجل الدفعة إذا أصبحت فارغة
              <span className="mt-1 block text-xs font-medium text-[var(--admin-muted)]">ألغِ الخيار لحذف الدفعة الفارغة بالكامل.</span>
            </span>
          </label>
          <div className="flex justify-end gap-2">
            <NeumorphButton type="button" intent="ghost" size="md" onClick={() => setShowRemoveUnusedModal(false)} disabled={removingUnused}>إلغاء</NeumorphButton>
            <NeumorphButton type="button" intent="primary" size="md" onClick={removeUnusedCodes} disabled={removingUnused} loading={removingUnused}>تأكيد المسح</NeumorphButton>
          </div>
        </div>
      </AdminModal>
    </PageShell>
  );
}
