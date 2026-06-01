'use client';

import { useEffect, useMemo, useState } from 'react';
import { Layers, FileText, Calendar, PlayCircle, BookOpen, Award, Wallet, Info, type LucideIcon } from 'lucide-react';
import type { CodeType } from '@/services/code-service';
import {
  contentService,
  type PackageDto,
  type TermDto,
  type ContentSectionDto,
  type LessonSummaryDto,
  type LessonDetailDto,
} from '@/services/content-service';

// ── Types ────────────────────────────────────────────────────────────────────
export interface CodeTypeSelection {
  codeType: CodeType;
  packageId?: string;
  termId?: string;
  contentSectionId?: string;
  lessonId?: string;
  examId?: string;
  videoTargetIds?: string[];
  balanceAmount?: number;
  discountPercentage?: number;
  expiresAt?: string;
}

interface CodeTypeSelectorProps {
  value: CodeTypeSelection;
  onChange: (value: CodeTypeSelection) => void;
  errors?: Record<string, string>;
  packages: PackageDto[];
}

// ── Code Types Metadata ──────────────────────────────────────────────────────
const CODE_TYPES: { type: CodeType; label: string; icon: LucideIcon; description: string }[] = [
  { type: 'Package', label: 'كورس / باكدج', icon: Layers, description: 'كود يفتح الكورس كامل (السنة كاملة)' },
  { type: 'Term', label: 'ترم', icon: Calendar, description: 'كود يفتح ترم كامل بجميع شهوره' },
  { type: 'Month', label: 'شهر / قسم', icon: BookOpen, description: 'كود يفتح شهر دراسي كامل' },
  { type: 'Lesson', label: 'حصة', icon: FileText, description: 'كود لفتح حصة محددة' },
  { type: 'Video', label: 'مجموعة فيديوهات', icon: PlayCircle, description: 'يفتح فيديو أو أكثر بحرية' },
  { type: 'Exam', label: 'امتحان', icon: Award, description: 'كود لفتح امتحان محدد' },
  { type: 'Balance', label: 'شحن رصيد', icon: Wallet, description: 'يشحن محفظة الطالب بمبلغ نقدي' },
];

const selectClassName = 'w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]';

export function CodeTypeSelector({ value, onChange, errors = {}, packages }: CodeTypeSelectorProps) {
  const [terms, setTerms] = useState<TermDto[]>([]);
  const [sections, setSections] = useState<ContentSectionDto[]>([]);
  const [lessons, setLessons] = useState<LessonSummaryDto[]>([]);
  const [lessonDetail, setLessonDetail] = useState<LessonDetailDto | null>(null);

  // Handlers
  const setType = (codeType: CodeType) => {
    onChange({ codeType, discountPercentage: value.discountPercentage, expiresAt: value.expiresAt });
  };

  const handleField = (field: keyof CodeTypeSelection, val: string | string[] | number | undefined) => {
    onChange({ ...value, [field]: val });
  };

  const selectedPackage = useMemo(
    () => packages.find((pkg) => pkg.id === value.packageId) ?? null,
    [packages, value.packageId]
  );

  const canSelectTerms = !!value.packageId && !['Package', 'Balance'].includes(value.codeType);
  const canSelectSections = !!value.termId && ['Month', 'Lesson', 'Video', 'Exam'].includes(value.codeType);
  const canSelectLessons = !!value.contentSectionId && ['Lesson', 'Video', 'Exam'].includes(value.codeType);
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
    });
  };

  const handleSectionChange = (contentSectionId: string) => {
    onChange({
      ...value,
      contentSectionId: contentSectionId || undefined,
      lessonId: undefined,
      examId: undefined,
      videoTargetIds: undefined,
    });
  };

  const handleLessonChange = (lessonId: string) => {
    onChange({
      ...value,
      lessonId: lessonId || undefined,
      examId: undefined,
      videoTargetIds: undefined,
    });
  };

  const renderPackageSelect = () => (
    <div className="col-span-1 md:col-span-2">
      <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">اختر الباكدج</label>
      <select
        className={selectClassName}
        value={value.packageId || ''}
        onChange={(e) => handlePackageChange(e.target.value)}
      >
        <option value="">اختر الباكدج</option>
        {packages.map((pkg) => (
          <option key={pkg.id} value={pkg.id}>
            {pkg.name}
          </option>
        ))}
      </select>
      {selectedPackage ? <p className="mt-1 text-xs text-[var(--admin-muted)]">المحدد: {selectedPackage.name}</p> : null}
      {errors.packageId && <p className="text-xs text-red-500 mt-1">{errors.packageId}</p>}
    </div>
  );

  const renderTermSelect = () => (
    <div className="col-span-1 md:col-span-2">
      <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">اختر الترم</label>
      <select
        className={selectClassName}
        value={value.termId || ''}
        onChange={(e) => handleTermChange(e.target.value)}
        disabled={!value.packageId}
      >
        <option value="">{value.packageId ? 'اختر الترم' : 'اختر الباكدج أولاً'}</option>
        {visibleTerms.map((term) => (
          <option key={term.id} value={term.id}>
            {term.title}
          </option>
        ))}
      </select>
      {selectedTerm ? <p className="mt-1 text-xs text-[var(--admin-muted)]">المحدد: {selectedTerm.title}</p> : null}
      {errors.termId && <p className="text-xs text-red-500 mt-1">{errors.termId}</p>}
    </div>
  );

  const renderSectionSelect = () => (
    <div className="col-span-1 md:col-span-2">
      <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">اختر الشهر / القسم</label>
      <select
        className={selectClassName}
        value={value.contentSectionId || ''}
        onChange={(e) => handleSectionChange(e.target.value)}
        disabled={!value.termId}
      >
        <option value="">{value.termId ? 'اختر الشهر / القسم' : 'اختر الترم أولاً'}</option>
        {visibleSections.map((section) => (
          <option key={section.id} value={section.id}>
            {section.title}
          </option>
        ))}
      </select>
      {selectedSection ? <p className="mt-1 text-xs text-[var(--admin-muted)]">المحدد: {selectedSection.title}</p> : null}
      {errors.contentSectionId && <p className="text-xs text-red-500 mt-1">{errors.contentSectionId}</p>}
    </div>
  );

  const renderLessonSelect = (label = 'اختر الحصة') => (
    <div className="col-span-1 md:col-span-2">
      <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">{label}</label>
      <select
        className={selectClassName}
        value={value.lessonId || ''}
        onChange={(e) => handleLessonChange(e.target.value)}
        disabled={!value.contentSectionId}
      >
        <option value="">{value.contentSectionId ? label : 'اختر الشهر / القسم أولاً'}</option>
        {visibleLessons.map((lesson) => (
          <option key={lesson.id} value={lesson.id}>
            {lesson.title}
          </option>
        ))}
      </select>
      {errors.lessonId && <p className="text-xs text-red-500 mt-1">{errors.lessonId}</p>}
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
              className={`flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-all ${
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

          {/* Video */}
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

        {/* ── Optional fields (Discount + Expiration) ── */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4 pt-4 border-t border-[var(--admin-border)]/50">
          <div>
            <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 flex justify-between">
              <span>نسبة الخصم % </span>
              <span className="opacity-50 font-normal">اختياري</span>
            </label>
            <input
              type="number"
              min="1"
              max="100"
              placeholder="مثلا: 20"
              className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)]"
              value={value.discountPercentage || ''}
              onChange={(e) => handleField('discountPercentage', e.target.value ? Number(e.target.value) : undefined)}
              dir="ltr"
            />
          </div>

          <div>
            <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 flex justify-between">
              <span>تاريخ الانتهاء</span>
              <span className="opacity-50 font-normal">اختياري</span>
            </label>
            <input
              type="datetime-local"
              className="w-full bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-lg px-3 py-2 text-sm text-[var(--admin-text)] [color-scheme:dark]"
              value={value.expiresAt ? new Date(value.expiresAt).toISOString().slice(0, 16) : ''}
              onChange={(e) => handleField('expiresAt', e.target.value ? new Date(e.target.value).toISOString() : undefined)}
              dir="ltr"
            />
          </div>
        </div>
      </div>
    </div>
  );
}
