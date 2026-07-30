'use client';

import { devConsole } from '@/utils/dev-console';
import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import { isAxiosError } from 'axios';
import { Banknote, Building2, Clock3, Eye, KeyRound, Layers, LayoutTemplate, Percent, Plus, Search, Sparkles, UserRound, Zap } from 'lucide-react';
import Link from 'next/link';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminModal,
} from '@/components/admin';
import { AcademicScopeSelector } from '@/components/admin/AcademicScopeSelector';
import { formatCompactNumber, formatDate } from '@/components/admin/admin-utils';
import { adminService, CodeGroupDto, VideoTypeDto } from '@/services/admin-service';
import { PackageDto, contentService } from '@/services/content-service';
import { teacherService, SubjectDto, TeacherDto } from '@/services/teacher-service';
import { codeService, type AcademicSubjectEligibility } from '@/services/code-service';
import { adminSalesService, type PublicExamProductDto } from '@/services/admin-sales-service';
import type { AcademicScopePayload } from '@/lib/academic-labels';
import { CodeTypeSelector, CodeTypeSelection } from '@/components/codes/CodeTypeSelector';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import { invalidateMany } from '@/lib/cache-invalidation';

export default function AdminCodesPageClient() {
  const [groups, setGroups] = useState<CodeGroupDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [videoTypes, setVideoTypes] = useState<VideoTypeDto[]>([]);
  const [publicExams, setPublicExams] = useState<PublicExamProductDto[]>([]);
  const [subjectEligibilities, setSubjectEligibilities] = useState<AcademicSubjectEligibility[]>([]);
  const [loading, setLoading] = useState(true);
  const [showGenModal, setShowGenModal] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>('All');
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>('All');
  
  // Generation Form State
  const [genCount, setGenCount] = useState(10);
  const [genSelection, setGenSelection] = useState<CodeTypeSelection>({ codeType: 'Package' });
  const [genTeacherId, setGenTeacherId] = useState('');
  const [genGroupName, setGenGroupName] = useState('');
  const [genRevenueOwner, setGenRevenueOwner] = useState<'Teacher' | 'Platform'>('Teacher');
  const [genRevenueAllocationMode, setGenRevenueAllocationMode] = useState<'Percentage' | 'FixedAmount'>('Percentage');
  const [genRevenueAllocationValue, setGenRevenueAllocationValue] = useState('');
  const [genAccountingTiming, setGenAccountingTiming] = useState<'OnActivation' | 'Immediate'>('OnActivation');
  const [genAcademicScopes, setGenAcademicScopes] = useState<AcademicScopePayload[]>([
    { scopeLevel: 'GradeAllSubjects', educationStage: 'Secondary', gradeLevel: 'FirstSecondary' },
  ]);
  const [genLoading, setGenLoading] = useState(false);

  const [packages, setPackages] = useState<PackageDto[]>([]);
  const loadDataInFlightRef = useRef<Promise<void> | null>(null);

  useEffect(() => {
    void loadData();
  }, []);

  async function loadData() {
    if (loadDataInFlightRef.current) {
      return loadDataInFlightRef.current;
    }

    const request = (async () => {
      try {
        setLoading(true);
        const [groupsData, packagesResponse, subjectsRes, teachersRes, videoTypesData, publicExamsData, eligibilityData] = await Promise.all([
          adminService.listCodeGroups(),
          contentService.getPackages(),
          teacherService.getSubjects().catch(() => ({ success: true, data: [] as SubjectDto[] })),
          teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] })),
          adminService.listVideoTypes(true).catch(() => [] as VideoTypeDto[]),
          adminSalesService.publicExams().catch(() => [] as PublicExamProductDto[]),
          codeService.getAcademicSubjectEligibilities(),
        ]);
        setGroups(groupsData || []);
        setPackages((packagesResponse.data?.data || []) as PackageDto[]);
        const scopedSubjects = Array.from(new Map(eligibilityData.map((eligibility) => [
          eligibility.subjectId,
          { id: eligibility.subjectId, name: eligibility.subjectName, description: '' },
        ])).values());
        setSubjects(subjectsRes.data?.length ? subjectsRes.data : scopedSubjects);
        setTeachers(teachersRes.data ?? []);
        setVideoTypes(videoTypesData);
        setPublicExams(publicExamsData);
        setSubjectEligibilities(eligibilityData);
      } catch (error) {
        if (!isAxiosError(error) || error.response?.status !== 429) {
          devConsole.error(error);
        }
      } finally {
        setLoading(false);
      }
    })();

    loadDataInFlightRef.current = request;

    try {
      await request;
    } finally {
      if (loadDataInFlightRef.current === request) {
        loadDataInFlightRef.current = null;
      }
    }
  }

  async function handleGenerate(event: FormEvent) {
    event.preventDefault();

    if (genAcademicScopes.some((scope) => scope.scopeLevel === 'Exact' && !scope.subjectId)) {
      toast.error('اختر مادة مفعّلة للمرحلة والصف، أو غيّر النطاق إلى كل مواد الصف.');
      return;
    }

    try {
      setGenLoading(true);
      const backendCodeType = genSelection.codeType === 'VideoType' ? 'Video' : genSelection.codeType === 'PublicExam' ? 'Exam' : genSelection.codeType;
      
      await codeService.createCodeGroup({
        groupName: genGroupName,
        codeType: backendCodeType,
        count: genCount,
        codeLength: 12,
        packageId: genSelection.packageId || undefined,
        termId: genSelection.termId || undefined,
        contentSectionId: genSelection.contentSectionId || undefined,
        lessonId: genSelection.lessonId || undefined,
        examId: genSelection.examId || undefined,
        publicExamProductId: genSelection.codeType === 'PublicExam' ? genSelection.publicExamProductId || undefined : undefined,
        videoTypeId: genSelection.codeType === 'VideoType' ? genSelection.videoTypeId || undefined : undefined,
        includeFutureVideos: genSelection.codeType === 'VideoType' ? genSelection.includeFutureVideos !== false : undefined,
        videoTargetIds: genSelection.codeType === 'Video' && genSelection.videoTargetIds && genSelection.videoTargetIds.length > 0 ? genSelection.videoTargetIds : undefined,
        balanceAmount: genSelection.balanceAmount || undefined,
        teacherId: genTeacherId || undefined,
        revenueOwner: genSelection.codeType === 'Balance' ? undefined : genRevenueOwner,
        revenueAllocationMode: genSelection.codeType === 'Balance' || !genRevenueAllocationValue ? undefined : genRevenueAllocationMode,
        revenueAllocationValue: genSelection.codeType === 'Balance' || !genRevenueAllocationValue ? undefined : Number(genRevenueAllocationValue),
        accountingTiming: genSelection.codeType === 'Balance' ? 'OnActivation' : genAccountingTiming,
        expiresAt: genSelection.expiresAt || undefined,
        expireActivatedAccess: genSelection.expiresAt ? genSelection.expireActivatedAccess !== false : undefined,
        academicScopes: genAcademicScopes,
      });

      toast.success('تم التوليد بنجاح!');
      invalidateMany(['codes:groups', 'content:packages', 'reports']);
      setShowGenModal(false);
      setGenSelection({ codeType: 'Package' });
      setGenTeacherId('');
      setGenGroupName('');
      setGenCount(10);
      setGenRevenueOwner('Teacher');
      setGenRevenueAllocationMode('Percentage');
      setGenRevenueAllocationValue('');
      setGenAccountingTiming('OnActivation');
      setGenAcademicScopes([{ scopeLevel: 'GradeAllSubjects', educationStage: 'Secondary', gradeLevel: 'FirstSecondary' }]);
      await loadData();
    } catch (error: unknown) {
      devConsole.error(error);
      const msg = isAxiosError<{ message?: string }>(error)
        ? error.response?.data?.message || 'تعذر إنشاء الأكواد. تأكد من إدخال جميع الحقول المطلوبة.'
        : 'تعذر إنشاء الأكواد. تأكد من إدخال جميع الحقول المطلوبة.';
      toast.error(msg);
    } finally {
      setGenLoading(false);
    }
  }

  const packageNameMap = useMemo(() => {
    return Object.fromEntries(packages.map((pkg) => [pkg.id, pkg.name]));
  }, [packages]);

  const teacherNameMap = useMemo(() => {
    return Object.fromEntries(teachers.map((teacher) => [teacher.id, teacher.fullName]));
  }, [teachers]);

  const videoTypeNameMap = useMemo(() => {
    return Object.fromEntries(videoTypes.map((type) => [type.id, type.name]));
  }, [videoTypes]);

  const filteredGroups = useMemo(() => {
    let list = groups;

    // Filter by Subject
    if (selectedSubjectId !== 'All') {
      list = list.filter((g) => {
        if (!g.packageId) return false;
        const pkg = packages.find((p) => p.id === g.packageId);
        return pkg?.subjectId === selectedSubjectId;
      });
    }

    // Filter by Teacher
    if (selectedTeacherId !== 'All') {
      list = list.filter((g) => g.teacherId === selectedTeacherId);
    }

    if (!searchQuery.trim()) return list;
    const q = searchQuery.toLowerCase().trim();
    return list.filter((g) => 
      g.name.toLowerCase().includes(q) || 
      g.id.toLowerCase().includes(q) ||
      (g.packageId && (packageNameMap[g.packageId] || g.packageId).toLowerCase().includes(q)) ||
      (g.videoTypeId && (videoTypeNameMap[g.videoTypeId] || g.videoTypeId).toLowerCase().includes(q)) ||
      (g.lessonId && g.lessonId.toLowerCase().includes(q)) ||
      (!g.teacherId && 'عام للمنصة'.includes(q))
    );
  }, [groups, searchQuery, packageNameMap, videoTypeNameMap, selectedSubjectId, selectedTeacherId, packages]);

  const totalCodes = groups.reduce((sum, group) => sum + group.codeCount, 0);
  const usedCodes = groups.reduce((sum, group) => sum + group.usedCount, 0);

  const groupColumns: AdminColumn<CodeGroupDto>[] = [
    {
      key: 'name',
      label: 'المجموعة',
      render: (g) => (
        <div>
          <div className="font-bold text-[var(--admin-text-strong)]">{g.name || 'دفعة بدون اسم'}</div>
          <div className="text-xs font-mono text-[var(--admin-muted)] mt-0.5">{g.id}</div>
        </div>
      ),
    },
    {
      key: 'createdAt',
      label: 'تاريخ الإنشاء',
      render: (g) => <span className="text-[var(--admin-muted)]">{formatDate(g.createdAt)}</span>,
    },
    {
      key: 'linking',
      label: 'الربط',
      render: (g: CodeGroupDto) => (
        <div className="font-semibold text-[var(--admin-text)]">
          <div>
            {g.codeType === 'Video' && g.videoTypeId
              ? `فيديوهات: ${videoTypeNameMap[g.videoTypeId] || g.videoTypeId}`
              : g.packageId
                ? 'Package'
                : g.lessonId
                  ? 'Lesson'
                  : 'عام'}
          </div>
          <div className="mt-1 text-xs text-[var(--admin-muted)] font-normal">
            {g.teacherId ? `المدرس: ${teacherNameMap[g.teacherId] || g.teacherId}` : 'عام للمنصة'}
          </div>
          {g.packageId ? (
            <div className="mt-1 text-xs text-[var(--admin-muted)] font-normal">{packageNameMap[g.packageId] || g.packageId}</div>
          ) : null}
        </div>
      ),
    },
    {
      key: 'usage',
      label: 'الاستخدام',
      render: (g) => (
        <div>
          <div className="text-sm font-bold text-[var(--admin-text)]">
            {formatCompactNumber(g.usedCount)} / {formatCompactNumber(g.codeCount)}
          </div>
          <div className="mt-2 h-1.5 rounded-full bg-[var(--admin-card-strong)] overflow-hidden border border-[var(--admin-border)]">
            <div
              className="h-full rounded-full bg-[var(--admin-primary-strong)]"
              style={{ width: `${g.codeCount === 0 ? 0 : (g.usedCount / g.codeCount) * 100}%` }}
            />
          </div>
        </div>
      ),
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (g) => (
        <div className="flex items-center justify-end gap-2">
          <Link href={`/admin/codes/${g.id}`} prefetch={false} passHref legacyBehavior>
            <NeumorphButton
              type="button"
              intent="icon"
              size="icon"
              title="عرض التفاصيل والطباعة"
            >
              <Eye className="h-5 w-5" />
            </NeumorphButton>
          </Link>
        </div>
      ),
    },
  ];

  return (
    <AdminShellChrome
      activePath="/admin/codes"
      sectionLabel="إدارة الأكواد"
      pageTitle="مجموعات أكواد الوصول"
      subtitle="إدارة التوليد والطباعة (QR) والاستخدام في شاشة واحدة."
      action={
        <div className="flex flex-wrap items-center gap-2">
          <Link href="/admin/codes/templates" prefetch={false} passHref legacyBehavior>
            <NeumorphButton type="button" intent="ghost" size="lg" pill>
              <LayoutTemplate className="h-4 w-4" />
              قوالب الطباعة
            </NeumorphButton>
          </Link>
          <NeumorphButton onClick={() => setShowGenModal(true)} intent="primary" size="lg" pill>
            <Plus className="h-4 w-4" />
            إنشاء دفعة جديدة
          </NeumorphButton>
        </div>
      }
    >
      {/* Mobile Fab */}
      <NeumorphButton
        type="button"
        onClick={() => setShowGenModal(true)}
        intent="primary"
        size="icon"
        pill
        className="fixed bottom-24 left-8 z-40 !h-14 !w-14 shadow-2xl md:hidden"
      >
        <Plus className="h-5 w-5" />
      </NeumorphButton>

      {/* Stats */}
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={KeyRound}
          label="إجمالي الأكواد"
          value={totalCodes}
        />
        <AdminStatCard
          variant="accent"
          icon={Sparkles}
          label="المستخدمة"
          value={usedCodes}
        />
        <AdminStatCard
          variant="muted"
          icon={Layers} // Wait Layers undefined, Using Box/Layers? Let's use Sparkles for now
          label="المجموعات"
          value={groups.length}
        />
      </section>

      {/* Search and Filters */}
      <div className="mb-6 flex flex-col md:flex-row gap-4 items-center mr-auto w-full max-w-3xl">
        <div className="flex flex-1 items-center bg-[var(--admin-card)] rounded-2xl border border-[var(--admin-border)] px-4 py-3 shadow-sm w-full">
          <Search className="text-[var(--admin-muted)] w-5 h-5 ml-2.5" />
          <input
            type="text"
            placeholder="ابحث عن اسم دفعة، ID، أو باقة مربوطة..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="bg-transparent border-none outline-none text-sm text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] w-full text-right"
            dir="rtl"
          />
        </div>
        
        <div className="flex gap-3 w-full md:w-auto">
          <select
            value={selectedSubjectId}
            onChange={(e) => setSelectedSubjectId(e.target.value)}
            className="admin-input flex-1 md:w-44"
          >
            <option value="All">كل المواد</option>
            {subjects.map((sub) => (
              <option key={sub.id} value={sub.id}>{sub.name}</option>
            ))}
          </select>

          <select
            value={selectedTeacherId}
            onChange={(e) => setSelectedTeacherId(e.target.value)}
            className="admin-input flex-1 md:w-44"
          >
            <option value="All">كل المدرسين</option>
            {teachers.map((t) => (
              <option key={t.id} value={t.id}>{t.fullName}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Code Groups Table */}
      <AdminDataTable
        data={filteredGroups}
        columns={groupColumns}
        loading={loading}
        rowKey={(g) => g.id}
        emptyMessage="لا توجد مجموعات أكواد بعد."
      />

      {/* Generation Modal */}
      <AdminModal
        open={showGenModal}
        onClose={() => setShowGenModal(false)}
        title="إنشاء دفعة أكواد"
        subtitle="توليد دفعة جديدة مع تحديد نوع الوصول"
        maxWidth="max-w-4xl"
      >
        <form onSubmit={handleGenerate} className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">اسم المجموعة (اختياري)</label>
              <input
                type="text"
                value={genGroupName}
                onChange={(e) => setGenGroupName(e.target.value)}
                className="admin-input"
                placeholder="مثلاً: دفعة الكورس المكثف"
                dir="auto"
              />
            </div>
            <div>
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">عدد الأكواد للمجموعة</label>
              <input
                type="number"
                min={1}
                max={10000}
                value={genCount}
                onChange={(e) => setGenCount(Number(e.target.value))}
                className="admin-input"
                placeholder="عدد الأكواد (مثلا: 100)"
                required
              />
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
            <div>
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">المدرس المسؤول عن الدفعة (اختياري)</label>
              <select
                value={genTeacherId}
                onChange={(e) => {
                  setGenTeacherId(e.target.value);
                  setGenSelection({
                    codeType: genSelection.codeType,
                    expiresAt: genSelection.expiresAt,
                    balanceAmount: genSelection.balanceAmount,
                  });
                }}
                className="admin-input"
              >
                <option value="">عام للمنصة</option>
                {teachers.map((teacher) => (
                  <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>
                ))}
              </select>
            </div>
            <div className="flex items-center rounded-xl bg-[var(--admin-card-strong)] px-4 py-3 text-xs font-semibold text-[var(--admin-muted)]">
              بدون مدرس: الكود عام للمنصة. مع مدرس: الرصيد يُحسب تحت هذا المدرس، وأكواد الباكدج يجب ربطها بباكدج محدد.
            </div>
          </div>

          <div className="pt-4 border-t border-[var(--admin-border)]">
            <CodeTypeSelector
              value={genSelection}
              onChange={setGenSelection}
              packages={packages}
              selectedTeacherId={genTeacherId || undefined}
              publicExams={publicExams}
            />
          </div>

          <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
            <div className="mb-3">
              <h3 className="text-sm font-bold text-[var(--admin-text-strong)]">النطاق الأكاديمي</h3>
              <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]">حدد الطلاب المسموح لهم باستخدام هذه الدفعة.</p>
            </div>
            <AcademicScopeSelector
              value={genAcademicScopes}
              onChange={setGenAcademicScopes}
              subjects={subjects.map((subject) => ({ id: subject.id, name: subject.name }))}
              subjectEligibilities={subjectEligibilities}
            />
          </div>

          {genSelection.codeType !== 'Balance' ? (
            <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
              <div className="mb-4 flex items-start justify-between gap-3">
                <div>
                  <h3 className="text-sm font-bold text-[var(--admin-text-strong)]">تفعيل الأرباح</h3>
                  <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]">
                    اختر هل قيمة الدفعة تتحسب للمدرس أم للمنصة، ومتى يظهر الربح في الحسابات.
                  </p>
                </div>
                <div className="rounded-xl bg-[var(--admin-card-strong)] p-3 text-[var(--admin-primary)]">
                  <Banknote className="h-5 w-5" />
                </div>
              </div>

              <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
                <div className="space-y-2">
                  <label className="text-xs font-bold text-[var(--admin-muted)]">الربح تابع لـ</label>
                  <div className="grid grid-cols-2 gap-2 rounded-xl bg-[var(--admin-bg)] p-1">
                    {[
                      { value: 'Teacher' as const, label: 'المدرس', icon: UserRound },
                      { value: 'Platform' as const, label: 'المنصة', icon: Building2 },
                    ].map((item) => {
                      const Icon = item.icon;
                      const active = genRevenueOwner === item.value;
                      return (
                        <button
                          key={item.value}
                          type="button"
                          onClick={() => setGenRevenueOwner(item.value)}
                          className={`flex items-center justify-center gap-2 rounded-lg px-3 py-2 text-xs font-bold transition ${
                            active
                              ? 'bg-[var(--admin-primary)] text-white shadow-sm'
                              : 'text-[var(--admin-muted)] hover:bg-[var(--admin-card)]'
                          }`}
                        >
                          <Icon className="h-4 w-4" />
                          {item.label}
                        </button>
                      );
                    })}
                  </div>
                  {genRevenueOwner === 'Teacher' && !genTeacherId ? (
                    <p className="text-xs font-semibold text-amber-600 dark:text-amber-400">اختيار المدرس مطلوب لو الربح تابع للمدرس.</p>
                  ) : null}
                </div>

                <div className="space-y-2">
                  <label className="text-xs font-bold text-[var(--admin-muted)]">طريقة الحساب</label>
                  <div className="grid grid-cols-2 gap-2 rounded-xl bg-[var(--admin-bg)] p-1">
                    {[
                      { value: 'Percentage' as const, label: 'نسبة', icon: Percent },
                      { value: 'FixedAmount' as const, label: 'مبلغ ثابت', icon: Banknote },
                    ].map((item) => {
                      const Icon = item.icon;
                      const active = genRevenueAllocationMode === item.value;
                      return (
                        <button
                          key={item.value}
                          type="button"
                          onClick={() => setGenRevenueAllocationMode(item.value)}
                          className={`flex items-center justify-center gap-2 rounded-lg px-3 py-2 text-xs font-bold transition ${
                            active
                              ? 'bg-[var(--admin-primary)] text-white shadow-sm'
                              : 'text-[var(--admin-muted)] hover:bg-[var(--admin-card)]'
                          }`}
                        >
                          <Icon className="h-4 w-4" />
                          {item.label}
                        </button>
                      );
                    })}
                  </div>
                  <input
                    type="number"
                    min={0}
                    max={genRevenueAllocationMode === 'Percentage' ? 100 : undefined}
                    step="0.01"
                    value={genRevenueAllocationValue}
                    onChange={(e) => setGenRevenueAllocationValue(e.target.value)}
                    placeholder={genRevenueAllocationMode === 'Percentage' ? 'مثلاً: 30' : 'مثلاً: 500'}
                    className="admin-input"
                    dir="ltr"
                  />
                  <p className="text-xs text-[var(--admin-muted)]">
                    لو سيبتها فاضية هيستخدم عمولة المدرس الافتراضية.
                  </p>
                </div>

                <div className="space-y-2">
                  <label className="text-xs font-bold text-[var(--admin-muted)]">توقيت التسجيل</label>
                  <div className="grid gap-2">
                    {[
                      { value: 'Immediate' as const, label: 'فوري عند إنشاء الدفعة', icon: Zap },
                      { value: 'OnActivation' as const, label: 'لا، حسب تفعيل الكود', icon: Clock3 },
                    ].map((item) => {
                      const Icon = item.icon;
                      const active = genAccountingTiming === item.value;
                      return (
                        <button
                          key={item.value}
                          type="button"
                          onClick={() => setGenAccountingTiming(item.value)}
                          className={`flex items-center justify-between rounded-xl border px-3 py-2 text-xs font-bold transition ${
                            active
                              ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]'
                              : 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/50'
                          }`}
                        >
                          <span>{item.label}</span>
                          <Icon className="h-4 w-4" />
                        </button>
                      );
                    })}
                  </div>
                  <p className="text-xs text-[var(--admin-muted)]">
                    الفوري يسجل إجمالي الدفعة الآن، أما حسب التفعيل فيسجل ربح كل كود عند استخدامه.
                  </p>
                </div>
              </div>
            </div>
          ) : null}

          <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)]">
            <NeumorphButton type="button" onClick={() => setShowGenModal(false)} intent="ghost" size="md">إلغاء</NeumorphButton>
            <NeumorphButton type="submit" disabled={genLoading} loading={genLoading} intent="primary" size="md" pill>
              توليد الدفعة
            </NeumorphButton>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}
