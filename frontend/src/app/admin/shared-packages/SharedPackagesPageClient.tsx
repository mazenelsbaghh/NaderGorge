'use client';

import { useEffect, useState } from 'react';
import { BookOpen, CheckCircle2, ImageIcon, Layers, PackagePlus, Plus, RefreshCw, Send, Trash2, Upload, X } from 'lucide-react';
import axios from 'axios';
import toast from 'react-hot-toast';
import Link from 'next/link';
import { AdminPage } from '@/components/admin';
import { AcademicScopeSelector } from '@/components/admin/AcademicScopeSelector';
import { contentService, type ContentSectionDto, type LessonSummaryDto, type PackageDto, type TermDto } from '@/services/content-service';
import { sharedPackageService, type SharedPackageListItem } from '@/services/shared-package-service';
import { teacherService, type SubjectDto, type TeacherDto } from '@/services/teacher-service';
import {
  getAcademicScopeLabel,
  getEducationStageLabel,
  getGradeLevelLabel,
  type AcademicScopePayload,
  type GradeLevel,
} from '@/lib/academic-labels';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { createClientId } from '@/lib/client-id';

const numberInputClass = 'h-11 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-bold text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]';
const fieldClass = 'h-11 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-bold text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]';

const contentTypes = [
  { value: '0', label: 'باقة' },
  { value: '1', label: 'ترم' },
  { value: '2', label: 'قسم / شهر' },
  { value: '3', label: 'حصة' },
];

type DraftOptionRow = {
  id: string;
  subjectId: string;
  teacherId: string;
  contentType: string;
  packageId: string;
  termId: string;
  sectionId: string;
  lessonId: string;
  itemPrice: string;
  allocationMode: string;
  allocationValue: string;
};

const createDraftOptionRow = (): DraftOptionRow => ({
  id: createClientId(),
  subjectId: '',
  teacherId: '',
  contentType: '0',
  packageId: '',
  termId: '',
  sectionId: '',
  lessonId: '',
  itemPrice: '0',
  allocationMode: '1',
  allocationValue: '50',
});

const readPayload = <T,>(response: any): T[] => response?.data?.data ?? response?.data ?? [];

const packageMatchesGrade = (pkg: PackageDto, gradeLevel: GradeLevel) => {
  const targetGrade = (pkg.targetGrade || '').trim();
  const aliases: Partial<Record<GradeLevel, string[]>> = {
    FirstSecondary: ['FirstSecondary', '1st Secondary', 'الأول الثانوي', 'الاول الثانوي', 'الأول الثانوى', 'اولى ثانوي'],
    SecondSecondary: ['SecondSecondary', '2nd Secondary', 'الثاني الثانوي', 'الثانى الثانوي', 'الثاني الثانوى', 'تانية ثانوي'],
    SecondaryGrade3: ['SecondaryGrade3', 'ThirdSecondary', '3rd Secondary', 'الثالث الثانوي', 'الثالث الثانوى', 'ثالثة ثانوي'],
  };
  const packageGrades = targetGrade.split(',').map((grade) => grade.trim());
  return !targetGrade || packageGrades.includes('All') || packageGrades.includes(gradeLevel) || packageGrades.some((grade) => Boolean(aliases[gradeLevel]?.includes(grade)));
};

const defaultAcademicScope: AcademicScopePayload = {
  scopeLevel: 'GradeAllSubjects',
  educationStage: 'Secondary',
  gradeLevel: 'FirstSecondary',
};

const getScopeGradeLevels = (scopes: AcademicScopePayload[]) => {
  if (scopes.some((scope) => scope.scopeLevel === 'PlatformWide' || scope.scopeLevel === 'StageWide')) {
    return null;
  }

  const scopedGrades = scopes
    .map((scope) => scope.gradeLevel)
    .filter((grade): grade is GradeLevel => Boolean(grade));

  return Array.from(new Set(scopedGrades));
};

const getPrimaryScopeFields = (scopes: AcademicScopePayload[]) => {
  const firstScopedValue = scopes.find((scope) => scope.educationStage || scope.gradeLevel);

  return {
    educationStage: firstScopedValue?.educationStage ?? undefined,
    gradeLevel: firstScopedValue?.gradeLevel ?? undefined,
  };
};

export default function SharedPackagesPageClient() {
  const [items, setItems] = useState<SharedPackageListItem[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [termsByPackage, setTermsByPackage] = useState<Record<string, TermDto[]>>({});
  const [sectionsByTerm, setSectionsByTerm] = useState<Record<string, ContentSectionDto[]>>({});
  const [lessonsBySection, setLessonsBySection] = useState<Record<string, LessonSummaryDto[]>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [price, setPrice] = useState('0');
  const [academicScopes, setAcademicScopes] = useState<AcademicScopePayload[]>([defaultAcademicScope]);
  const [rows, setRows] = useState<DraftOptionRow[]>([createDraftOptionRow()]);
  const [isPublished, setIsPublished] = useState(true);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string>('');
  const [imageProgress, setImageProgress] = useState(0);
  const [uploadingImageForId, setUploadingImageForId] = useState<string | null>(null);

  useEffect(() => {
    if (!imageFile) {
      setImagePreviewUrl('');
      return;
    }

    const previewUrl = URL.createObjectURL(imageFile);
    setImagePreviewUrl(previewUrl);
    return () => URL.revokeObjectURL(previewUrl);
  }, [imageFile]);

  const load = async () => {
    setLoading(true);
    try {
      const [shared, teacherRes, subjectRes, packageRes] = await Promise.all([
        sharedPackageService.listAdmin(),
        teacherService.getTeachers(),
        teacherService.getSubjects(),
        contentService.getPackages(),
      ]);
      setItems(shared);
      setTeachers(teacherRes.data ?? []);
      setSubjects(subjectRes.data ?? []);
      setPackages(packageRes.data?.data ?? []);
    } catch {
      toast.error('تعذر تحميل الباكدجات المشتركة');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const cleanupCacheStore = registerCacheStore('shared-packages:admin', () => {}, () => void load());
    void load();
    return cleanupCacheStore;
  }, []);

  const updateRow = (id: string, patch: Partial<DraftOptionRow>) => {
    setRows((current) => {
      const target = current.find((row) => row.id === id);
      if (!target) return current;

      // The price belongs to the subject path, not to every teacher alternative.
      // Keeping alternatives synchronized prevents an admin from accidentally
      // turning three teacher choices for one subject into triple the price.
      if (patch.itemPrice !== undefined && target.subjectId) {
        const itemPrice = patch.itemPrice;
        return current.map((row) => row.subjectId === target.subjectId
          ? { ...row, itemPrice }
          : row);
      }

      return current.map((row) => row.id === id ? { ...row, ...patch } : row);
    });
  };

  const addRow = () => {
    setRows((current) => [...current, createDraftOptionRow()]);
  };

  const removeRow = (id: string) => {
    setRows((current) => current.length <= 1 ? current : current.filter((row) => row.id !== id));
  };

  const teachersForSubject = (subjectId: string) =>
    teachers.filter((teacher) => !subjectId || teacher.subjectIds.includes(subjectId));

  const packagesForRow = (row: DraftOptionRow) => {
    const teacherSubjectMatches = packages.filter((pkg) =>
      (!row.teacherId || pkg.teacherId === row.teacherId)
      && (!row.subjectId || pkg.subjectId === row.subjectId)
    );

    const scopedGrades = getScopeGradeLevels(academicScopes);
    if (scopedGrades === null) {
      return teacherSubjectMatches;
    }

    return teacherSubjectMatches.filter((pkg) => scopedGrades.some((grade) => packageMatchesGrade(pkg, grade)));
  };

  const loadTerms = async (packageId: string) => {
    if (!packageId || termsByPackage[packageId]) return;
    const response = await contentService.getTerms(packageId);
    setTermsByPackage((current) => ({ ...current, [packageId]: readPayload<TermDto>(response) }));
  };

  const loadSections = async (termId: string) => {
    if (!termId || sectionsByTerm[termId]) return;
    const response = await contentService.getSections(termId);
    setSectionsByTerm((current) => ({ ...current, [termId]: readPayload<ContentSectionDto>(response) }));
  };

  const loadLessons = async (sectionId: string) => {
    if (!sectionId || lessonsBySection[sectionId]) return;
    const response = await contentService.getLessons(sectionId);
    setLessonsBySection((current) => ({ ...current, [sectionId]: readPayload<LessonSummaryDto>(response) }));
  };

  const resolveContentId = (row: DraftOptionRow) => {
    if (row.contentType === '0') return row.packageId;
    if (row.contentType === '1') return row.termId;
    if (row.contentType === '2') return row.sectionId;
    return row.lessonId;
  };

  const resolvePriceGroupKey = (row: DraftOptionRow) => row.subjectId || row.teacherId || row.id;

  const groupedChoicePrices = (candidateRows: DraftOptionRow[]) => {
    const groups = new Map<string, number[]>();

    candidateRows.forEach((row) => {
      const key = resolvePriceGroupKey(row);
      const current = groups.get(key) ?? [];
      const itemPrice = Number(row.itemPrice);
      current.push(Number.isFinite(itemPrice) ? itemPrice : 0);
      groups.set(key, current);
    });

    return Array.from(groups.values()).map((prices) => ({ prices }));
  };

  const choicePriceGroups = groupedChoicePrices(rows.filter((row) => row.subjectId && row.teacherId && resolveContentId(row)));
  const choicesTotal = choicePriceGroups.reduce((sum, group) => sum + (group.prices[0] || 0), 0);
  const basePrice = Number(price);

  useEffect(() => {
    const calculatedPrice = Number(choicesTotal.toFixed(2));
    if (basePrice !== calculatedPrice) {
      setPrice(String(calculatedPrice));
    }
  }, [basePrice, choicesTotal]);

  const submit = async () => {
    const numericPrice = Number(price);
    const validRows = rows.filter((row) => row.subjectId && row.teacherId && resolveContentId(row));
    if (!name.trim() || !Number.isFinite(numericPrice) || numericPrice <= 0 || validRows.length === 0) {
      toast.error('اكمل اسم وسعر الباكدج واختر مادة ومدرس ومحتوى واحد على الأقل');
      return;
    }

    if (academicScopes.length === 0) {
      toast.error('حدد نطاق بيع الباكدج أولاً');
      return;
    }

    if (validRows.some((row) => !Number.isFinite(Number(row.itemPrice)) || Number(row.itemPrice) <= 0)) {
      toast.error('سعر كل اختيار يجب أن يكون أكبر من صفر');
      return;
    }

    if (validRows.some((row) => !Number.isFinite(Number(row.allocationValue)) || Number(row.allocationValue) < 0)) {
      toast.error('قيمة نصيب المدرس لا يمكن أن تكون سالبة');
      return;
    }

    const duplicatedTeacherSubject = validRows.some((row, index) =>
      validRows.findIndex((candidate) => candidate.subjectId === row.subjectId && candidate.teacherId === row.teacherId) !== index
    );
    if (duplicatedTeacherSubject) {
      toast.error('لا تكرر نفس المدرس لنفس المادة داخل الباكدج');
      return;
    }

    if (validRows.some((row) => row.allocationMode === '1' && Number(row.allocationValue) > 100)) {
      toast.error('نسبة المدرس لا يمكن أن تتجاوز 100% من سعر الاختيار');
      return;
    }

    if (validRows.some((row) => row.allocationMode === '2' && Number(row.allocationValue) > Number(row.itemPrice))) {
      toast.error('المبلغ الثابت للمدرس لا يمكن أن يتجاوز سعر اختياره');
      return;
    }

    const validChoicePriceGroups = groupedChoicePrices(validRows);
    if (validChoicePriceGroups.some((group) => new Set(group.prices).size > 1)) {
      toast.error('كل بدائل نفس المادة يجب أن يكون لها نفس سعر الاختيار');
      return;
    }

    const validChoicesTotal = validChoicePriceGroups.reduce((sum, group) => sum + group.prices[0], 0);
    if (Math.abs(validChoicesTotal - numericPrice) > 0.01) {
      toast.error('مجموع أسعار الاختيارات يجب أن يساوي سعر الباكدج الأساسي');
      return;
    }

    setSaving(true);
    try {
      const primaryScopeFields = getPrimaryScopeFields(academicScopes);
      const res = await sharedPackageService.createAdmin({
        name: name.trim(),
        description: description.trim(),
        price: numericPrice,
        educationStage: primaryScopeFields.educationStage,
        gradeLevel: primaryScopeFields.gradeLevel,
        academicScopes,
        distributionMode: validRows.some((row) => row.allocationMode === '2') && validRows.some((row) => row.allocationMode === '1') ? 2 : validRows[0].allocationMode === '2' ? 1 : 0,
        isPublished,
        teachers: validRows.map((row, index) => ({
          teacherId: row.teacherId,
          subjectId: row.subjectId,
          allocationMode: Number(row.allocationMode),
          allocationValue: Number(row.allocationValue),
          displayOrder: index + 1,
        })),
        items: validRows.map((row) => ({
          teacherId: row.teacherId,
          subjectId: row.subjectId,
          contentType: Number(row.contentType),
          contentId: resolveContentId(row),
          price: Number(row.itemPrice),
        })),
      });
      if (!res.success) {
        toast.error(res.message || 'تعذر حفظ الباكدج');
        return;
      }
      if (imageFile && res.data?.id) {
        try {
          setImageProgress(0);
          await sharedPackageService.uploadAdminImage(res.data.id, imageFile, setImageProgress);
        } catch {
          toast.error('تم حفظ الباكدج، لكن فشل رفع الصورة.');
        } finally {
          setImageProgress(0);
        }
      }
      toast.success('تم حفظ الباكدج المشترك');
      setName('');
      setDescription('');
      setPrice('0');
      setAcademicScopes([defaultAcademicScope]);
      setRows([createDraftOptionRow()]);
      setImageFile(null);
      await load();
    } catch (error) {
      const message = axios.isAxiosError(error)
        ? error.response?.data?.message
        : null;
      toast.error(message || 'تعذر حفظ الباكدج');
    } finally {
      setSaving(false);
    }
  };

  const publish = async (id: string) => {
    try {
      const res = await sharedPackageService.publishAdmin(id);
      if (!res.success) {
        toast.error(res.message || 'تعذر النشر');
        return;
      }
      toast.success('تم نشر الباكدج');
      await load();
    } catch {
      toast.error('تعذر النشر');
    }
  };

  const selectImage = (file?: File) => {
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      toast.error('اختر ملف صورة صالحًا.');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      toast.error('حجم الصورة يجب ألا يتجاوز 10 ميجابايت.');
      return;
    }
    setImageFile(file);
  };

  const uploadExistingImage = async (id: string, file?: File) => {
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      toast.error('اختر ملف صورة صالحًا.');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      toast.error('حجم الصورة يجب ألا يتجاوز 10 ميجابايت.');
      return;
    }

    try {
      setUploadingImageForId(id);
      await sharedPackageService.uploadAdminImage(id, file);
      toast.success('تم تحديث صورة الباكدج وتحويلها إلى WebP.');
      await load();
    } catch {
      toast.error('تعذر رفع صورة الباكدج.');
    } finally {
      setUploadingImageForId(null);
    }
  };

  return (
    <AdminPage
      activePath="/admin/shared-packages"
      sectionLabel="المدرسين والحسابات"
      pageTitle="الباكدجات المشتركة"
      subtitle="حدد نطاق البيع أولاً، ثم اختر المواد والمدرسين والمحتوى المتاح لكل اختيار."
      action={(
        <div className="flex flex-wrap items-center gap-2">
          <Link href="/admin/subjects" className="inline-flex h-11 items-center gap-2 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]">
            <BookOpen className="h-4 w-4" /> إدارة المواد
          </Link>
          <button type="button" onClick={() => void load()} className="inline-flex h-11 items-center gap-2 rounded-lg border border-[var(--admin-border)] px-4 text-sm font-bold"><RefreshCw className="h-4 w-4" /> تحديث</button>
        </div>
      )}
    >
      <div className="grid gap-5 xl:grid-cols-[640px_1fr]">
        <section className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span className="grid h-11 w-11 place-items-center rounded-lg bg-[var(--admin-primary)] text-white"><PackagePlus className="h-5 w-5" /></span>
            <div>
              <h2 className="text-lg font-black text-[var(--admin-text)]">باكدج جديد</h2>
              <p className="text-sm text-[var(--admin-muted)]">كل مادة يمكن أن تحتوي أكثر من اختيار مدرس، والطالب يختار مدرساً واحداً عند الشراء.</p>
            </div>
          </div>

          <div className="grid gap-3">
            <input className={fieldClass} value={name} onChange={(e) => setName(e.target.value)} placeholder="اسم الباكدج" />
            <textarea className="min-h-24 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 text-sm font-bold text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="وصف مختصر" />
            <div className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-3">
              <p className="text-xs font-black text-[var(--admin-muted)]">سعر الباكدج، يُحسب من أسعار المواد المختارة</p>
              <p className="mt-1 text-xl font-black text-[var(--admin-text)]">{(Number.isFinite(basePrice) ? basePrice : 0).toLocaleString('ar-EG')} جنيه</p>
            </div>

            <div className="overflow-hidden rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
              <div className="relative aspect-video bg-[var(--admin-card)]">
                {imagePreviewUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={imagePreviewUrl} alt="معاينة صورة الباكدج" className="h-full w-full object-cover" />
                ) : (
                  <div className="flex h-full flex-col items-center justify-center gap-2 text-[var(--admin-muted)]">
                    <ImageIcon className="h-8 w-8" />
                    <span className="text-sm font-bold">لا توجد صورة للباكدج</span>
                  </div>
                )}
                {imageProgress > 0 && (
                  <div className="absolute inset-0 flex flex-col items-center justify-center bg-black/60 px-6 text-center text-white">
                    <span className="mb-2 text-sm font-bold">جاري رفع وتحويل الصورة... {imageProgress}%</span>
                    <div className="h-1.5 w-full max-w-[220px] overflow-hidden rounded-full bg-white/20">
                      <div className="h-full rounded-full bg-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow]" style={{ width: `${imageProgress}%` }} />
                    </div>
                  </div>
                )}
              </div>
              <div className="flex flex-wrap items-center justify-between gap-3 p-3">
                <div>
                  <h3 className="text-sm font-black text-[var(--admin-text)]">صورة الباكدج</h3>
                  <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">تُرفع على دومين المنصة وتتحول تلقائيًا إلى WebP.</p>
                </div>
                <div className="flex items-center gap-2">
                  {imageFile && (
                    <button type="button" onClick={() => setImageFile(null)} className="inline-flex h-10 items-center gap-2 rounded-lg border border-[var(--admin-border)] px-3 text-xs font-black text-rose-600">
                      <X className="h-4 w-4" /> إزالة
                    </button>
                  )}
                  <label className="inline-flex h-10 cursor-pointer items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-3 text-xs font-black text-white">
                    <Upload className="h-4 w-4" />
                    {imageFile ? 'تغيير الصورة' : 'رفع صورة'}
                    <input type="file" accept="image/*" className="hidden" onChange={(event) => selectImage(event.target.files?.[0])} />
                  </label>
                </div>
              </div>
            </div>

            <div className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3">
              <h3 className="text-sm font-black text-[var(--admin-text)]">نطاق بيع الباكدج</h3>
              <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">يمكن إضافة أكثر من نطاق، ويكفي تطابق واحد مع الطالب.</p>
              <div className="mt-3">
                <AcademicScopeSelector value={academicScopes} onChange={setAcademicScopes} subjects={subjects} />
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between gap-3">
                <h3 className="text-sm font-black text-[var(--admin-text)]">المواد والمدرسون والمحتوى</h3>
                <button type="button" onClick={addRow} className="inline-flex h-9 items-center gap-2 rounded-lg border border-[var(--admin-border)] px-3 text-xs font-black text-[var(--admin-text)]">
                  <Plus className="h-4 w-4" /> إضافة اختيار
                </button>
              </div>

              {rows.map((row, index) => {
                const rowPackages = packagesForRow(row);
                const rowTerms = termsByPackage[row.packageId] ?? [];
                const rowSections = sectionsByTerm[row.termId] ?? [];
                const rowLessons = lessonsBySection[row.sectionId] ?? [];
                const subjectTeachers = teachersForSubject(row.subjectId);

                return (
                  <div key={row.id} className="grid gap-2 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3">
                    <div className="flex items-center justify-between gap-3">
                      <span className="text-xs font-black text-[var(--admin-muted)]">اختيار {index + 1}</span>
                      <button type="button" onClick={() => removeRow(row.id)} className="grid h-8 w-8 place-items-center rounded-lg border border-[var(--admin-border)] text-rose-600 disabled:opacity-40" disabled={rows.length <= 1}>
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>

                    <div className="grid gap-2 md:grid-cols-2">
                      <select
                        className={fieldClass}
                        value={row.subjectId}
                        onChange={(e) => updateRow(row.id, { subjectId: e.target.value, teacherId: '', packageId: '', termId: '', sectionId: '', lessonId: '' })}
                      >
                        <option value="">اختر المادة</option>
                        {subjects.map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}
                      </select>
                      <select
                        className={fieldClass}
                        value={row.teacherId}
                        onChange={(e) => updateRow(row.id, { teacherId: e.target.value, packageId: '', termId: '', sectionId: '', lessonId: '' })}
                      >
                        <option value="">اختر المدرس داخل المادة</option>
                        {subjectTeachers.map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>)}
                      </select>
                    </div>

                    <div className="grid gap-2 md:grid-cols-2">
                      <select
                        className={fieldClass}
                        value={row.contentType}
                        onChange={(e) => {
                          const contentType = e.target.value;
                          updateRow(row.id, {
                            contentType,
                            termId: contentType === '0' ? '' : row.termId,
                            sectionId: contentType === '0' || contentType === '1' ? '' : row.sectionId,
                            lessonId: contentType === '3' ? row.lessonId : '',
                          });
                          if (contentType !== '0' && row.packageId) void loadTerms(row.packageId);
                          if ((contentType === '2' || contentType === '3') && row.termId) void loadSections(row.termId);
                          if (contentType === '3' && row.sectionId) void loadLessons(row.sectionId);
                        }}
                      >
                        {contentTypes.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}
                      </select>
                      <select
                        className={fieldClass}
                        value={row.packageId}
                        onChange={(e) => {
                          updateRow(row.id, { packageId: e.target.value, termId: '', sectionId: '', lessonId: '' });
                          void loadTerms(e.target.value);
                        }}
                      >
                        <option value="">اختر الباقة</option>
                        {rowPackages.map((pkg) => <option key={pkg.id} value={pkg.id}>{pkg.name}</option>)}
                      </select>
                      {row.subjectId && row.teacherId && rowPackages.length === 0 && (
                        <p className="md:col-span-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-bold text-amber-800">
                          لا توجد باقات مطابقة للمدرس والمادة ونطاق البيع الحالي. غيّر النطاق أو اربط المحتوى بالصف الصحيح أولاً.
                        </p>
                      )}
                    </div>

                    {row.contentType !== '0' && (
                      <select
                        className={fieldClass}
                        value={row.termId}
                        onChange={(e) => {
                          updateRow(row.id, { termId: e.target.value, sectionId: '', lessonId: '' });
                          void loadSections(e.target.value);
                        }}
                      >
                        <option value="">اختر الترم</option>
                        {rowTerms.map((term) => <option key={term.id} value={term.id}>{term.title}</option>)}
                      </select>
                    )}

                    {(row.contentType === '2' || row.contentType === '3') && (
                      <select
                        className={fieldClass}
                        value={row.sectionId}
                        onChange={(e) => {
                          updateRow(row.id, { sectionId: e.target.value, lessonId: '' });
                          void loadLessons(e.target.value);
                        }}
                      >
                        <option value="">اختر القسم / الشهر</option>
                        {rowSections.map((section) => <option key={section.id} value={section.id}>{section.title}</option>)}
                      </select>
                    )}

                    {row.contentType === '3' && (
                      <select className={fieldClass} value={row.lessonId} onChange={(e) => updateRow(row.id, { lessonId: e.target.value })}>
                        <option value="">اختر الحصة</option>
                        {rowLessons.map((lesson) => <option key={lesson.id} value={lesson.id}>{lesson.title}</option>)}
                      </select>
                    )}

                    <input className={numberInputClass} value={row.itemPrice} onChange={(e) => updateRow(row.id, { itemPrice: e.target.value })} inputMode="decimal" placeholder="سعر المادة" aria-label="سعر المادة" />

                    <div className="grid grid-cols-2 gap-3">
                      <select className={fieldClass} value={row.allocationMode} onChange={(e) => updateRow(row.id, { allocationMode: e.target.value })}>
                        <option value="1">نسبة من السعر</option>
                        <option value="2">مبلغ ثابت</option>
                      </select>
                      <input className={numberInputClass} value={row.allocationValue} onChange={(e) => updateRow(row.id, { allocationValue: e.target.value })} inputMode="decimal" placeholder="قيمة المدرس" />
                    </div>
                  </div>
                );
              })}

              <div className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 text-xs font-black text-[var(--admin-text)]">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span>إجمالي أسعار المواد / سعر الباكدج</span>
                  <span className={Math.abs(choicesTotal - basePrice) <= 0.01 ? 'text-emerald-700' : 'text-rose-700'}>
                    {choicesTotal.toLocaleString('ar-EG')} / {(Number.isFinite(basePrice) ? basePrice : 0).toLocaleString('ar-EG')} جنيه
                  </span>
                </div>
                {Number.isFinite(basePrice) && Math.abs(choicesTotal - basePrice) > 0.01 && choicesTotal > 0 && (
                  <div className="mt-3 flex flex-wrap items-center justify-between gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-amber-900">
                    <p>سعر الباكدج يُحدَّث تلقائيًا ليطابق أسعار المواد: {choicesTotal.toLocaleString('ar-EG')} جنيه.</p>
                  </div>
                )}
                {choicePriceGroups.some((group) => new Set(group.prices).size > 1) && (
                  <p className="mt-2 text-rose-700">بدائل نفس المادة يجب أن تحمل نفس سعر الاختيار.</p>
                )}
              </div>
            </div>

            <label className="flex h-11 items-center justify-between rounded-lg border border-[var(--admin-border)] px-3 text-sm font-bold text-[var(--admin-text)]">
              نشر بعد الحفظ
              <input type="checkbox" checked={isPublished} onChange={(e) => setIsPublished(e.target.checked)} />
            </label>
            <button type="button" disabled={saving} onClick={() => void submit()} className="inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-[var(--admin-primary)] px-4 text-sm font-black text-white disabled:opacity-60">
              <Send className="h-4 w-4" /> حفظ الباكدج
            </button>
          </div>
        </section>

        <section className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span className="grid h-11 w-11 place-items-center rounded-lg bg-[var(--admin-card-soft)] text-[var(--admin-primary)]"><Layers className="h-5 w-5" /></span>
            <div>
              <h2 className="text-lg font-black text-[var(--admin-text)]">الباكدجات المحفوظة</h2>
              <p className="text-sm text-[var(--admin-muted)]">{loading ? 'جاري التحميل...' : `${items.length} باكدج`}</p>
            </div>
          </div>

          <div className="grid gap-3">
            {items.map((item) => (
              <article key={item.id} className="flex flex-col gap-3 rounded-lg border border-[var(--admin-border)] p-4 md:flex-row md:items-center md:justify-between">
                <div className="flex min-w-0 items-center gap-3">
                  <div className="h-16 w-24 shrink-0 overflow-hidden rounded-lg bg-[var(--admin-card-soft)]">
                    {item.imageUrl ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img src={resolveMediaUrl(item.imageUrl)} alt={item.name} className="h-full w-full object-cover" />
                    ) : (
                      <div className="grid h-full w-full place-items-center text-[var(--admin-muted)]">
                        <ImageIcon className="h-5 w-5" />
                      </div>
                    )}
                  </div>
                  <div className="min-w-0">
                    <h3 className="truncate text-base font-black text-[var(--admin-text)]">{item.name}</h3>
                    <p className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-[var(--admin-muted)]">
                      <BookOpen className="h-4 w-4" />
                      <span>{item.teacherCount ?? 0} اختيار مدرس</span>
                      <span>•</span>
                      <span>{item.price.toLocaleString('ar-EG')} جنيه</span>
                      <span>•</span>
                      <span>{getEducationStageLabel(item.educationStage)} - {getGradeLevelLabel(item.gradeLevel)}</span>
                    </p>
                    {item.academicScopes?.length ? (
                      <div className="mt-2 flex flex-wrap gap-1.5">
                        {item.academicScopes.map((scope, index) => (
                          <span key={`${item.id}-scope-${index}`} className="rounded-full bg-[var(--admin-primary-15)] px-2 py-1 text-sm font-black text-[var(--admin-primary)]">
                            {getAcademicScopeLabel(scope)}
                          </span>
                        ))}
                      </div>
                    ) : null}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className={`rounded-full px-3 py-1 text-xs font-black ${item.isPublished ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'}`}>
                    {item.isPublished ? 'منشور' : 'مسودة'}
                  </span>
                  <label className={`inline-flex h-9 cursor-pointer items-center gap-2 rounded-lg border border-[var(--admin-border)] px-3 text-xs font-black text-[var(--admin-text)] ${uploadingImageForId === item.id ? 'pointer-events-none opacity-60' : ''}`}>
                    <Upload className="h-4 w-4" />
                    {uploadingImageForId === item.id ? 'جاري الرفع...' : item.imageUrl ? 'تغيير الصورة' : 'رفع صورة'}
                    <input
                      type="file"
                      accept="image/*"
                      className="hidden"
                      disabled={uploadingImageForId === item.id}
                      onChange={(event) => {
                        void uploadExistingImage(item.id, event.target.files?.[0]);
                        event.currentTarget.value = '';
                      }}
                    />
                  </label>
                  {!item.isPublished && (
                    <button type="button" onClick={() => void publish(item.id)} className="inline-flex h-9 items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-3 text-xs font-black text-white">
                      <CheckCircle2 className="h-4 w-4" /> نشر
                    </button>
                  )}
                </div>
              </article>
            ))}
            {!loading && items.length === 0 && <div className="rounded-lg border border-dashed border-[var(--admin-border)] p-8 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد باكدجات مشتركة بعد.</div>}
          </div>
        </section>
      </div>
    </AdminPage>
  );
}
