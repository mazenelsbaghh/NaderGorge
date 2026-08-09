'use client';

import { useEffect, useMemo, useState } from 'react';
import { Layers, FileText, Calendar, PlayCircle, BookOpen, Award, Wallet, Info, Tags, type LucideIcon } from 'lucide-react';
import type { CodeType } from '@/services/code-service';
import {
  contentService,
  type PackageDto,
  type TermDto,
  type ContentSectionDto,
  type LessonSummaryDto,
  type LessonDetailDto,
} from '@/services/content-service';
import { Dropdown } from '@/components/ui/dropdown';
import { VideoTypeSelect } from '@/components/admin/VideoTypeSelect';
import type { PublicExamProductDto } from '@/services/admin-sales-service';
import { cairoDateTimeLocalToIso, formatCairoDateTimeLocal } from '@/components/admin/admin-utils';

// ── Types ────────────────────────────────────────────────────────────────────
type CodeTypeOption = CodeType | 'VideoType' | 'PublicExam';

export interface CodeTypeSelection {
  codeType: CodeTypeOption;
  packageId?: string;
  termId?: string;
  contentSectionId?: string;
  lessonId?: string;
  examId?: string;
  publicExamProductId?: string;
  videoTypeId?: string;
  includeFutureVideos?: boolean;
  videoTargetIds?: string[];
  balanceAmount?: number;
  expiresAt?: string;
  expireActivatedAccess?: boolean;
}

interface CodeTypeSelectorProps {
  value: CodeTypeSelection;
  onChange: (value: CodeTypeSelection) => void;
  errors?: Record<string, string>;
  packages: PackageDto[];
  selectedTeacherId?: string;
  publicExams?: PublicExamProductDto[];
}

// ── Code Types Metadata ──────────────────────────────────────────────────────
const CODE_TYPES: { type: CodeTypeOption; label: string; icon: LucideIcon; description: string }[] = [
  { type: 'Package', label: 'كورس / باكدج', icon: Layers, description: 'كود يفتح باكدج محدد للطالب' },
  { type: 'Term', label: 'ترم', icon: Calendar, description: 'كود يفتح ترم كامل بجميع شهوره' },
  { type: 'Month', label: 'شهر / قسم', icon: BookOpen, description: 'كود يفتح شهر دراسي كامل' },
  { type: 'Lesson', label: 'حصة', icon: FileText, description: 'كود لفتح حصة محددة' },
  { type: 'VideoType', label: 'نوع فيديو', icon: Tags, description: 'يفتح كل فيديوهات نوع محدد' },
  { type: 'Video', label: 'فيديوهات محددة', icon: PlayCircle, description: 'يفتح فيديوهات تختارها يدوياً' },
  { type: 'Exam', label: 'امتحان', icon: Award, description: 'كود لفتح امتحان محدد' },
  { type: 'PublicExam', label: 'امتحان عام', icon: Award, description: 'كود لفتح امتحان عام مستقل' },
  { type: 'Balance', label: 'شحن رصيد', icon: Wallet, description: 'يشحن محفظة الطالب بمبلغ نقدي' },
];



export function CodeTypeSelector({ value, onChange, errors = {}, packages, selectedTeacherId, publicExams = [] }: CodeTypeSelectorProps) {
  const [terms, setTerms] = useState<TermDto[]>([]);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [lessonDetail, setLessonDetail] = useState<LessonDetailDto | null>(null);

  // Handlers
  const setType = (codeType: CodeTypeOption) => {
    onChange({ codeType, expiresAt: value.expiresAt });
  };

  const handleField = (field: keyof CodeTypeSelection, val: string | string[] | number | boolean | undefined) => {
    onChange({ ...value, [field]: val });
  };

  const filteredPackages = useMemo(
    () => selectedTeacherId ? packages.filter((pkg) => pkg.teacherId === selectedTeacherId) : packages,
    [packages, selectedTeacherId]
  );

  const selectedPackage = useMemo(
    () => filteredPackages.find((pkg) => pkg.id === value.packageId) ?? null,
    [filteredPackages, value.packageId]
  );

  const canSelectTerms = !!value.packageId && !['Package', 'Balance'].includes(value.codeType);
  const canSelectSections = !!value.termId && ['Month', 'Lesson', 'Video', 'VideoType', 'Exam'].includes(value.codeType);
  const canSelectLessons = !!value.contentSectionId && ['Lesson', 'Video', 'VideoType', 'Exam'].includes(value.codeType);
  const canLoadLessonDetail = !!value.lessonId && ['Video', 'Exam'].includes(value.codeType);

  const visibleTerms = useMemo(() => (canSelectTerms ? terms : []), [canSelectTerms, terms]);
  const visibleSections = useMemo(() => (canSelectSections ? sections : []), [canSelectSections, sections]);
  const visibleLessons = useMemo(() => (canSelectLessons ? lessons : []), [canSelectLessons, lessons]);
  const activeLessonDetail = canLoadLessonDetail ? lessonDetail : null;

  const selectedTerm = useMemo(
    () => visibleTerms.find((term) => term.id === value.termId) ?? null,
    [visibleTerms, value.termId]
  );

  const selectedSection = useMemo(
    () => visibleSections.find((section) => section.id === value.contentSectionId) ?? null,
    [visibleSections, value.contentSectionId]
  );

  const shouldPreserveVideoType =
    value.codeType === 'VideoType';

  useEffect(() => {
    if (!canSelectTerms) {
      return;
    }

    let cancelled = false;
    contentService.getTerms(value.packageId!).then((response) => {
      if (cancelled) return;
      setTerms((response.data?.data || []) as TermDto[]);
    }).catch(() => {
      if (!cancelled) setTerms([]);
    });

    return () => {
      cancelled = true;
    };
  }, [canSelectTerms, value.packageId]);

  useEffect(() => {
    if (!canSelectSections) {
      return;
    }

    let cancelled = false;
    contentService.getSections(value.termId!).then((response) => {
      if (cancelled) return;
      setSections((response.data?.data || []) as ContentSectionDto[]);
    }).catch(() => {
      if (!cancelled) setSections([]);
    });

    return () => {
      cancelled = true;
    };
  }, [canSelectSections, value.termId]);

  useEffect(() => {
    if (!canSelectLessons) {
      return;
    }

    let cancelled = false;
    contentService.getLessons(value.contentSectionId!).then((response) => {
      if (cancelled) return;
      setLessons((response.data?.data || []) as LessonSummaryDto[]);
    }).catch(() => {
      if (!cancelled) setLessons([]);
    });

    return () => {
      cancelled = true;
    };
  }, [canSelectLessons, value.contentSectionId]);

  useEffect(() => {
    if (!canLoadLessonDetail) {
      return;
    }

    let cancelled = false;
    contentService.getLessonDetail(value.lessonId!).then((response) => {
      if (cancelled) return;
      setLessonDetail((response.data?.data || null) as LessonDetailDto | null);
    }).catch(() => {
      if (!cancelled) setLessonDetail(null);
    });

    return () => {
      cancelled = true;
    };
  }, [canLoadLessonDetail, value.lessonId]);

  useEffect(() => {
    if (value.codeType !== 'Exam') return;

    const nextExamId = activeLessonDetail?.examId || undefined;
    if (value.examId !== nextExamId) {
      onChange({ ...value, examId: nextExamId });
    }
  }, [activeLessonDetail?.examId, onChange, value]);

  const handlePackageChange = (packageId: string) => {
    onChange({
      ...value,
      packageId: packageId || undefined,
      termId: undefined,
      contentSectionId: undefined,
      lessonId: undefined,
      examId: undefined,
      videoTargetIds: undefined,
      videoTypeId: shouldPreserveVideoType ? value.videoTypeId : undefined,
    });
  };

  const handleTermChange = (termId: string) => {
    onChange({
      ...value,
      termId: termId || undefined,
      contentSectionId: undefined,
      lessonId: undefined,
      examId: undefined,
      videoTargetIds: undefined,
      videoTypeId: shouldPreserveVideoType ? value.videoTypeId : undefined,
    });
  };

  const handleSectionChange = (contentSectionId: string) => {
    onChange({
      ...value,
      contentSectionId: contentSectionId || undefined,
      lessonId: undefined,
      examId: undefined,
      videoTargetIds: undefined,
      videoTypeId: shouldPreserveVideoType ? value.videoTypeId : undefined,
    });
  };

  const handleLessonChange = (lessonId: string) => {
    onChange({
      ...value,
      lessonId: lessonId || undefined,
      examId: undefined,
      videoTargetIds: undefined,
      videoTypeId: shouldPreserveVideoType ? value.videoTypeId : undefined,
    });
  };

  const renderPackageSelect = () => (
    <div className="col-span-1 md:col-span-2">
      <Dropdown
        label="اختر الباكدج"
        value={value.packageId || ''}
        onChange={(v) => handlePackageChange(v as string)}
        placeholder="اختر الباكدج"
        searchable
        options={[
          {
            value: '',
            label: 'اختر الباكدج',
          },
          ...filteredPackages.map((pkg) => ({ value: pkg.id, label: pkg.name })),
        ]}
        error={errors.packageId}
      />
      {selectedPackage ? (
        <p className="mt-1 text-xs text-[var(--admin-muted)]">المحدد: {selectedPackage.name}</p>
      ) : null}
    </div>
  );

  const renderTermSelect = () => (
    <div className="col-span-1 md:col-span-2">
      <Dropdown
        label="اختر الترم"
        value={value.termId || ''}
        onChange={(v) => handleTermChange(v as string)}
        disabled={!value.packageId}
        placeholder={value.packageId ? 'اختر الترم' : 'اختر الباكدج أولاً'}
        options={[{ value: '', label: 'اختر الترم' }, ...visibleTerms.map((t) => ({ value: t.id, label: t.title }))]}
        error={errors.termId}
      />
      {selectedTerm ? <p className="mt-1 text-xs text-[var(--admin-muted)]">المحدد: {selectedTerm.title}</p> : null}
    </div>
  );

  const renderSectionSelect = () => (
    <div className="col-span-1 md:col-span-2">
      <Dropdown
        label="اختر الشهر / القسم"
        value={value.contentSectionId || ''}
        onChange={(v) => handleSectionChange(v as string)}
        disabled={!value.termId}
        placeholder={value.termId ? 'اختر الشهر / القسم' : 'اختر الترم أولاً'}
        options={[{ value: '', label: 'اختر الشهر / القسم' }, ...visibleSections.map((s) => ({ value: s.id, label: s.title }))]}
        error={errors.contentSectionId}
      />
      {selectedSection ? <p className="mt-1 text-xs text-[var(--admin-muted)]">المحدد: {selectedSection.title}</p> : null}
    </div>
  );

  const renderLessonSelect = (label = 'اختر الحصة') => (
    <div className="col-span-1 md:col-span-2">
      <Dropdown
        label={label}
        value={value.lessonId || ''}
        onChange={(v) => handleLessonChange(v as string)}
        disabled={!value.contentSectionId}
        placeholder={value.contentSectionId ? label : 'اختر الشهر / القسم أولاً'}
        searchable
        options={[{ value: '', label }, ...visibleLessons.map((l) => ({ value: l.id, label: l.title }))]}
        error={errors.lessonId}
      />
    </div>
  );

  return (
    <div className="space-y-6">
      {/* ── Type Grid ── */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
        {CODE_TYPES.map((ct) => {
          const isSelected = value.codeType === ct.type;
          return (
            <button
              key={ct.type}
              type="button"
              onClick={() => setType(ct.type)}
              className={`flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-[color,background-color,border-color,opacity,transform,box-shadow] ${
                isSelected
                  ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]'
                  : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:border-[var(--admin-primary)]/50'
              }`}
            >
              <ct.icon size={28} />
              <span className="font-bold text-sm">{ct.label}</span>
              <span className="text-xs opacity-70 text-center">{ct.description}</span>
            </button>
          );
        })}
      </div>
      {errors.codeType && <p className="text-sm text-red-500 mt-1">{errors.codeType}</p>}

      {/* ── Dynamic Target Inputs ── */}
      <div className="p-5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)]">
        <div className="flex items-center gap-2 mb-4 text-[var(--admin-text)]">
          <Info size={16} className="opacity-50" />
          <h3 className="font-bold text-sm">حدد هدف الكود</h3>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">

          {/* Package */}
          {value.codeType === 'Package' && (
            renderPackageSelect()
          )}

          {/* Term */}
          {value.codeType === 'Term' && (
            <>
              {renderPackageSelect()}
              {renderTermSelect()}
            </>
          )}

          {/* Month */}
          {value.codeType === 'Month' && (
            <>
              {renderPackageSelect()}
              {renderTermSelect()}
              {renderSectionSelect()}
            </>
          )}

          {/* Lesson */}
          {value.codeType === 'Lesson' && (
            <>
              {renderPackageSelect()}
              {renderTermSelect()}
              {renderSectionSelect()}
              {renderLessonSelect()}
            </>
          )}

          {/* Exam */}
          {value.codeType === 'Exam' && (
            <>
              {renderPackageSelect()}
              {renderTermSelect()}
              {renderSectionSelect()}
              {renderLessonSelect('اختر الحصة المرتبط بها الامتحان')}
              <div className="col-span-1 md:col-span-2 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-3 text-sm text-[var(--admin-text)]">
                {value.lessonId ? (
                  activeLessonDetail?.examId ? (
                    <span>سيتم استخدام امتحان هذه الحصة تلقائياً.</span>
                  ) : (
                    <span className="text-amber-600 dark:text-amber-400">هذه الحصة لا تحتوي على امتحان مرتبط.</span>
                  )
                ) : (
                  <span className="text-[var(--admin-muted)]">اختر الحصة أولاً لتحديد الامتحان المرتبط بها.</span>
                )}
              </div>
              {errors.examId && <p className="text-xs text-red-500 mt-1 col-span-1 md:col-span-2">{errors.examId}</p>}
            </>
          )}

          {value.codeType === 'PublicExam' && (
            <div className="col-span-1 md:col-span-2">
              <Dropdown
                label="اختر الامتحان العام"
                value={value.publicExamProductId || ''}
                onChange={(publicExamProductId) => handleField('publicExamProductId', (publicExamProductId as string) || undefined)}
                placeholder="اختر الامتحان العام"
                searchable
                options={[
                  { value: '', label: 'اختر الامتحان العام' },
                  ...publicExams
                    .filter((exam) => !exam.disabledAt)
                    .map((exam) => ({ value: exam.id, label: exam.examTitle })),
                ]}
                error={errors.publicExamProductId}
              />
              {!publicExams.length && <p className="mt-2 text-xs text-[var(--admin-muted)]">لا توجد امتحانات عامة متاحة للاختيار.</p>}
            </div>
          )}

          {/* Video Type */}
          {value.codeType === 'VideoType' && (
            <>
              <div className="col-span-1 md:col-span-2">
                <VideoTypeSelect
                  value={value.videoTypeId || ''}
                  onChange={(videoTypeId) => handleField('videoTypeId', videoTypeId || undefined)}
                  label="اختر نوع الفيديو"
                />
                {errors.videoTypeId && <p className="text-xs text-red-500 mt-1">{errors.videoTypeId}</p>}
              </div>
              <fieldset className="col-span-1 md:col-span-2 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
                <legend className="px-1 text-xs font-bold text-[var(--admin-muted)]">نطاق فيديوهات النوع</legend>
                <div className="mt-2 grid gap-3 md:grid-cols-2">
                  <label className="flex cursor-pointer items-start gap-3 rounded-md p-2 hover:bg-[var(--admin-card-strong)]">
                    <input
                      type="radio"
                      name="video-type-access-scope"
                      checked={value.includeFutureVideos === false}
                      onChange={() => handleField('includeFutureVideos', false)}
                    />
                    <span className="text-sm text-[var(--admin-text)]"><strong className="block">الفيديوهات الموجودة الآن فقط</strong><span className="text-xs text-[var(--admin-muted)]">لا يفتح أي فيديو يُضاف لاحقاً.</span></span>
                  </label>
                  <label className="flex cursor-pointer items-start gap-3 rounded-md p-2 hover:bg-[var(--admin-card-strong)]">
                    <input
                      type="radio"
                      name="video-type-access-scope"
                      checked={value.includeFutureVideos !== false}
                      onChange={() => handleField('includeFutureVideos', true)}
                    />
                    <span className="text-sm text-[var(--admin-text)]"><strong className="block">كل الفيديوهات الحالية والمستقبلية</strong><span className="text-xs text-[var(--admin-muted)]">يفتح تلقائياً أي فيديو جديد من هذا النوع داخل النطاق المحدد.</span></span>
                  </label>
                </div>
              </fieldset>
              {renderPackageSelect()}
              {renderTermSelect()}
              {renderSectionSelect()}
              {renderLessonSelect('اختر حصة لتضييق النطاق')}
            </>
          )}

          {/* Specific Videos */}
          {value.codeType === 'Video' && (
            <>
              {renderPackageSelect()}
              {renderTermSelect()}
              {renderSectionSelect()}
              {renderLessonSelect('اختر الحصة التي تحتوي على الفيديوهات')}
              <div className="col-span-1 md:col-span-2">
                <label className="text-xs font-bold text-[var(--admin-muted)] mb-2 block">اختر الفيديوهات</label>
                <div className="space-y-2 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3">
                  {!value.lessonId ? (
                    <p className="text-sm text-[var(--admin-muted)]">اختر الحصة أولاً.</p>
                  ) : activeLessonDetail?.videos?.length ? (
                    activeLessonDetail.videos.map((video) => {
                      const checked = value.videoTargetIds?.includes(video.id) ?? false;
                      return (
                        <label key={video.id} className="flex items-center gap-3 rounded-md px-2 py-2 hover:bg-[var(--admin-card-strong)] cursor-pointer">
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={(e) => {
                              const current = value.videoTargetIds ?? [];
                              const next = e.target.checked
                                ? [...current, video.id]
                                : current.filter((id) => id !== video.id);
                              handleField('videoTargetIds', next.length ? next : undefined);
                            }}
                          />
                          <span className="text-sm text-[var(--admin-text)]">{video.title}</span>
                        </label>
                      );
                    })
                  ) : (
                    <p className="text-sm text-[var(--admin-muted)]">لا توجد فيديوهات في هذه الحصة.</p>
                  )}
                </div>
                {errors.videoTargetIds && <p className="text-xs text-red-500 mt-1">{errors.videoTargetIds}</p>}
              </div>
            </>
          )}

          {/* Balance */}
          {value.codeType === 'Balance' && (
            <div className="col-span-1 md:col-span-2 lg:col-span-1">
              <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">قيمة الشحن (جنيه)</label>
              <input
                type="number"
                min="1"
                placeholder="50"
                className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
                value={value.balanceAmount || ''}
                onChange={(e) => handleField('balanceAmount', e.target.value ? Number(e.target.value) : undefined)}
                dir="ltr"
              />
              {errors.balanceAmount && <p className="text-xs text-red-500 mt-1">{errors.balanceAmount}</p>}
            </div>
          )}

        </div>

        {/* ── Optional fields ── */}
        <div className="grid grid-cols-1 gap-4 mt-4 pt-4 border-t border-[var(--admin-border)]/50">
          <div>
            <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 flex justify-between">
              <span>تاريخ الانتهاء</span>
              <span className="opacity-50 font-normal">اختياري</span>
            </label>
            <input
              type="datetime-local"
              className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)] [color-scheme:dark]"
              value={value.expiresAt ? formatCairoDateTimeLocal(value.expiresAt) : ''}
              onChange={(e) => handleField('expiresAt', e.target.value ? cairoDateTimeLocalToIso(e.target.value) : undefined)}
              dir="ltr"
            />
            {value.codeType !== 'Balance' && value.expiresAt && (
              <fieldset className="mt-3 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3">
                <legend className="px-1 text-xs font-bold text-[var(--admin-muted)]">بعد تاريخ الانتهاء</legend>
                <label className="mt-2 flex cursor-pointer items-start gap-3 text-sm text-[var(--admin-text)]">
                  <input type="radio" name="activated-access-expiry" checked={value.expireActivatedAccess !== false} onChange={() => handleField('expireActivatedAccess', true)} />
                  <span><strong className="block">ينتهي الوصول للمحتوى</strong><span className="text-xs text-[var(--admin-muted)]">حتى الطالب الذي فعّل الكود قبل التاريخ يتوقف وصوله.</span></span>
                </label>
                <label className="mt-3 flex cursor-pointer items-start gap-3 text-sm text-[var(--admin-text)]">
                  <input type="radio" name="activated-access-expiry" checked={value.expireActivatedAccess === false} onChange={() => handleField('expireActivatedAccess', false)} />
                  <span><strong className="block">يبقى المحتوى متاحًا لمن فعّل الكود</strong><span className="text-xs text-[var(--admin-muted)]">بعد التاريخ لا يمكن تفعيل كود جديد فقط.</span></span>
                </label>
              </fieldset>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
