"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import Link from "next/link";
import { BarChart3, BookOpenText, Plus, ChevronLeft, Sparkles, Video, Eye, Folder, FolderOpen, FileText, Upload, Layers3 } from "lucide-react";
import { AdminPageSkeleton, AdminSearchToolbar, AdminStatCard, AdminTabBar, ContentSummaryPanel } from "@/components/admin";
import { TeacherPage } from "@/components/teacher/TeacherShellChrome";
import { contentService, PACKAGE_CONTENT_MODE_OPTIONS, PackageDto, TermDto, ContentSectionDto, LessonSummaryDto, type PackageContentMode } from "@/services/content-service";
import { adminService } from "@/services/admin-service";
import { teacherService, SubjectDto } from "@/services/teacher-service";
import NeumorphButton from "@/components/ui/neumorph-button";
import toast from "react-hot-toast";
import { Dropdown } from "@/components/ui/dropdown";
import { GRADES_BY_STAGE, getGradeLevelLabel, type EducationStage, type GradeLevel } from "@/lib/academic-labels";

function getStageForGrade(grade: string): EducationStage | '' {
  for (const [stage, groups] of Object.entries(GRADES_BY_STAGE) as [EducationStage, typeof GRADES_BY_STAGE[EducationStage]][]) {
    if (groups.some((group) => group.grades.some((item) => item.value === grade))) {
      return stage;
    }
  }

  return '';
}

function getTeacherPackageGrades(profile: any): { value: string; label: string }[] {
  if (!profile || !profile.specialization) return [];
  const specs = profile.specialization.split(',');
  const list: { value: string; label: string }[] = [];

  const mapping: Record<string, { value: string; label: string }> = {
    'FirstSecondary': { value: '1st Secondary', label: getGradeLevelLabel('FirstSecondary') },
    'SecondSecondary': { value: '2nd Secondary', label: getGradeLevelLabel('SecondSecondary') },
    'SecondaryGrade3': { value: '3rd Secondary', label: getGradeLevelLabel('SecondaryGrade3') },
    '1st Secondary': { value: '1st Secondary', label: getGradeLevelLabel('1st Secondary') },
    '2nd Secondary': { value: '2nd Secondary', label: getGradeLevelLabel('2nd Secondary') },
    '3rd Secondary': { value: '3rd Secondary', label: getGradeLevelLabel('3rd Secondary') },
  };

  specs.forEach((spec: string) => {
    const trimmed = spec.trim();
    if (mapping[trimmed]) {
      list.push(mapping[trimmed]);
    } else {
      list.push({ value: trimmed, label: getGradeLevelLabel(trimmed) });
    }
  });

  return list;
}

// ─── Create Package Inline Form ───────────────────────────────────────────────
function CreatePackageRow({ onSuccess, subjects, profile }: { onSuccess: () => void; subjects: SubjectDto[]; profile: any }) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [price, setPrice] = useState("");
  const [selectedSubjectId, setSelectedSubjectId] = useState("");
  const [selectedGrades, setSelectedGrades] = useState<string[]>([]);
  const [contentMode, setContentMode] = useState<PackageContentMode>("TermWithSections");
  const [saving, setSaving] = useState(false);

  // Image Upload States
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreview, setImagePreview] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.type.startsWith('image/')) {
        toast.error('اختر ملف صورة صالحًا.');
        return;
      }
      if (file.size > 10 * 1024 * 1024) {
        toast.error('حجم الصورة يجب ألا يتجاوز 10 ميجابايت.');
        return;
      }
      setImageFile(file);
      const reader = new FileReader();
      reader.onloadend = () => {
        setImagePreview(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  };

  async function handleCreate() {
    if (!name.trim() || !selectedSubjectId || selectedGrades.length === 0) return;
    try {
      setSaving(true);
      const newPkg = await adminService.createPackage({
        name: name.trim(),
        description: description.trim(),
        price: Number(price) || 0,
        subjectId: selectedSubjectId,
        targetGrade: selectedGrades.join(','),
        academicScopes: selectedGrades.map((grade) => ({
          scopeLevel: 'Exact' as const,
          educationStage: getStageForGrade(grade) as EducationStage,
          gradeLevel: grade as GradeLevel,
          subjectId: selectedSubjectId,
        })),
        contentMode,
      });

      if (newPkg?.id && imageFile) {
        try {
          await adminService.uploadContentImage('package', newPkg.id, imageFile);
        } catch {
          toast.error('تم حفظ الباقة، لكن فشل رفع الصورة.');
        }
      }

      toast.success("تمت إضافة الباقة بنجاح.");
      setName(""); setDescription(""); setPrice(""); setSelectedSubjectId(""); setSelectedGrades([]); setContentMode("TermWithSections");
      setImageFile(null); setImagePreview(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
      setOpen(false);
      onSuccess();
    } catch {
      toast.error("حدث خطأ أثناء الإضافة.");
    } finally {
      setSaving(false);
    }
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="flex w-full items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-[var(--admin-border)] bg-transparent py-5 text-sm font-bold text-[var(--admin-muted)] transition hover:border-[var(--admin-primary)] hover:text-[var(--admin-primary)] hover:bg-[var(--admin-primary-15)]/20"
      >
        <Plus className="h-4 w-4" />
        إضافة باقة جديدة
      </button>
    );
  }

  return (
    <div className="rounded-2xl border-2 border-dashed border-[var(--admin-primary)] bg-[var(--admin-primary-15)]/30 p-5 space-y-3">
      <p className="text-sm font-black text-[var(--admin-primary)]">باقة جديدة</p>
      <input
        autoFocus
        type="text"
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="اسم الباقة، مثال: الباقة التأسيسية للأول الثانوي"
        className="admin-input"
        onKeyDown={(e) => { if (e.key === "Escape") setOpen(false); }}
      />
      <textarea
        rows={2}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        placeholder="وصف مختصر للباقة..."
        className="admin-input resize-none"
      />
      <input
        type="number"
        min={0}
        value={price}
        onChange={(e) => setPrice(e.target.value)}
        placeholder="السعر (جنيه مصري)"
        className="admin-input"
      />

      <div className="space-y-2 text-right">
        <span className="text-xs font-bold text-[var(--admin-muted)]">هيكل الباقة</span>
        <Dropdown
          value={contentMode}
          onChange={(value) => setContentMode((Array.isArray(value) ? value[0] : value) as PackageContentMode)}
          options={PACKAGE_CONTENT_MODE_OPTIONS.map((option) => ({ value: option.value, label: option.label }))}
          placeholder="اختر هيكل الباقة..."
          className="w-full"
        />
        <p className="text-xs text-[var(--admin-muted)]">
          {PACKAGE_CONTENT_MODE_OPTIONS.find((option) => option.value === contentMode)?.description}
        </p>
      </div>

      {/* صورة الباقة */}
      <div className="space-y-1 text-right">
        <span className="text-xs font-bold text-[var(--admin-muted)]">صورة الباقة (اختياري)</span>
        <div
          onClick={() => fileInputRef.current?.click()}
          className="relative flex flex-col items-center justify-center border-2 border-dashed border-[var(--admin-border)] rounded-2xl p-4 bg-[var(--admin-card)] hover:border-[var(--admin-primary)] cursor-pointer transition min-h-[100px]"
        >
          {imagePreview ? (
            <div className="relative w-full aspect-video rounded-xl overflow-hidden">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={imagePreview} alt="Preview" className="w-full h-full object-cover" />
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setImageFile(null);
                  setImagePreview(null);
                  if (fileInputRef.current) fileInputRef.current.value = '';
                }}
                className="absolute top-2 right-2 bg-red-500 text-white rounded-full p-1.5 hover:bg-red-600 transition shadow"
                title="إزالة الصورة"
              >
                <span className="block text-xs font-black px-1.5">إزالة</span>
              </button>
            </div>
          ) : (
            <div className="flex flex-col items-center justify-center gap-1.5 text-[var(--admin-muted)]">
              <Upload className="h-5 w-5 text-[var(--admin-primary)]" />
              <span className="text-xs font-bold">اضغط هنا لاختيار صورة للباقة</span>
              <span className="text-xs">الحد الأقصى 10 ميجابايت (يتم تحويلها لـ WebP)</span>
            </div>
          )}
          <input
            ref={fileInputRef}
            type="file"
            accept="image/*"
            className="hidden"
            onChange={handleFileChange}
          />
        </div>
      </div>

      <Dropdown
        value={selectedSubjectId}
        onChange={(val) => {
          const stringVal = Array.isArray(val) ? val[0] : val;
          setSelectedSubjectId(stringVal);
          setSelectedGrades([]);
        }}
        options={subjects.map((s) => ({ value: s.id, label: s.name }))}
        placeholder="اختر المادة..."
        className="w-full"
      />

      <Dropdown
        value={selectedGrades}
        onChange={(val) => {
          setSelectedGrades(Array.isArray(val) ? val : [val]);
        }}
        options={getTeacherPackageGrades(profile)}
        placeholder="اختر الصفوف والمراحل الدراسية..."
        disabled={!selectedSubjectId}
        multiple
        searchable
        className="w-full"
      />
      <p className="text-xs text-[var(--admin-muted)]">يمكن اختيار أكثر من صف لنفس الكورس.</p>

      <div className="flex justify-end gap-2 pt-1">
        <button
          type="button"
          onClick={() => setOpen(false)}
          className="rounded-xl border border-[var(--admin-border)] px-4 py-2 text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-card-strong)] transition"
        >
          إلغاء
        </button>
        <NeumorphButton
          onClick={() => void handleCreate()}
          disabled={saving || !name.trim() || !selectedSubjectId || selectedGrades.length === 0}
          loading={saving}
          intent="primary"
          size="md"
          pill
        >
          حفظ الباقة
        </NeumorphButton>
      </div>
    </div>
  );
}

// ─── Nested Rows ─────────────────────────────────────────────────────────────
function LessonRow({ lesson }: { lesson: Pick<LessonSummaryDto, 'id' | 'title'> }) {
  return (
    <div className="flex items-center justify-between py-2 px-3 rounded-xl hover:bg-[var(--admin-card-soft)] transition-colors">
      <div className="flex items-center gap-3 min-w-0">
        <FileText className="h-4 w-4 text-[var(--admin-muted)] shrink-0" />
        <span className="text-xs font-semibold text-[var(--admin-text)] truncate">{lesson.title}</span>
      </div>
      <Link
        href={`/teacher/packages/lessons/${lesson.id}`}
        className="p-1.5 rounded-lg text-[var(--admin-muted)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] transition"
        aria-label={`عرض الدرس ${lesson.title}`}
        title="عرض الدرس"
      >
        <Eye className="h-4 w-4" aria-hidden="true" />
      </Link>
    </div>
  );
}

function SectionRow({ section }: { section: ContentSectionDto }) {
  const [isOpen, setIsOpen] = useState(false);
  const [lessons, setLessons] = useState<LessonSummaryDto[] | null>(null);
  const [loading, setLoading] = useState(false);

  const toggleOpen = async () => {
    const nextState = !isOpen;
    setIsOpen(nextState);
    if (nextState && lessons === null) {
      try {
        setLoading(true);
        const res = await contentService.getLessons(section.id);
        const list = (res.data?.data ?? []) as LessonSummaryDto[];
        setLessons(list.sort((a, b) => a.order - b.order));
      } catch {
        toast.error("تعذر تحميل الدروس.");
      } finally {
        setLoading(false);
      }
    }
  };

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between gap-2 rounded-xl hover:bg-[var(--admin-card-soft)] transition-colors">
        <button
          type="button"
          onClick={toggleOpen}
          aria-expanded={isOpen}
          className="flex min-h-11 min-w-0 flex-1 items-center gap-3 rounded-xl py-2 px-3 text-right focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
        >
          <ChevronLeft
            className={`h-4 w-4 text-[var(--admin-muted)] transition-transform duration-200 shrink-0 ${
              isOpen ? "-rotate-90" : ""
            }`}
            aria-hidden="true"
          />
          <FolderOpen className="h-4 w-4 text-[var(--admin-primary)]/80 shrink-0" aria-hidden="true" />
          <span className="text-xs font-bold text-[var(--admin-text)] truncate">{section.title}</span>
        </button>
        <Link
          href={`/teacher/packages/sections/${section.id}`}
          className="p-1.5 rounded-lg text-[var(--admin-muted)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] transition"
          aria-label={`عرض القسم ${section.title}`}
          title="عرض القسم"
        >
          <Eye className="h-4 w-4" aria-hidden="true" />
        </Link>
      </div>

      {isOpen && (
        <div className="mr-6 pr-3 border-r-2 border-dashed border-[var(--admin-primary-15)]/40 space-y-1 my-1">
          {loading ? (
            <div className="text-xs text-[var(--admin-muted)] py-2 px-3">جاري تحميل الدروس...</div>
          ) : lessons && lessons.length > 0 ? (
            lessons.map((lesson) => <LessonRow key={lesson.id} lesson={lesson} />)
          ) : (
            <div className="text-xs text-[var(--admin-muted)] py-2 px-3">لا توجد دروس في هذا القسم.</div>
          )}
        </div>
      )}
    </div>
  );
}

function TermRow({ term }: { term: TermDto }) {
  const [isOpen, setIsOpen] = useState(false);
  const [sections, setSections] = useState<ContentSectionDto[] | null>(null);
  const [loading, setLoading] = useState(false);

  const toggleOpen = async () => {
    const nextState = !isOpen;
    setIsOpen(nextState);
    if (nextState && sections === null) {
      try {
        setLoading(true);
        const res = await contentService.getSections(term.id);
        const list = (res.data?.data ?? []) as ContentSectionDto[];
        setSections(list.sort((a, b) => a.order - b.order));
      } catch {
        toast.error("تعذر تحميل الأقسام.");
      } finally {
        setLoading(false);
      }
    }
  };

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between gap-2 rounded-xl hover:bg-[var(--admin-card-strong)] transition-colors">
        <button
          type="button"
          onClick={toggleOpen}
          aria-expanded={isOpen}
          className="flex min-h-11 min-w-0 flex-1 items-center gap-3 rounded-xl py-2 px-4 text-right focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
        >
          <ChevronLeft
            className={`h-4 w-4 text-[var(--admin-muted)] transition-transform duration-200 shrink-0 ${
              isOpen ? "-rotate-90" : ""
            }`}
            aria-hidden="true"
          />
          <Folder className="h-4.5 w-4.5 text-[var(--admin-primary)] shrink-0" aria-hidden="true" />
          <span className="text-xs font-black text-[var(--admin-text)] truncate">{term.title}</span>
        </button>
        <Link
          href={`/teacher/packages/terms/${term.id}`}
          className="p-1.5 rounded-lg text-[var(--admin-muted)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] transition"
          aria-label={`عرض الترم ${term.title}`}
          title="عرض الترم"
        >
          <Eye className="h-4 w-4" aria-hidden="true" />
        </Link>
      </div>

      {isOpen && (
        <div className="mr-6 pr-3 border-r-2 border-[var(--admin-primary-15)]/60 space-y-1 my-1">
          {loading ? (
            <div className="text-xs text-[var(--admin-muted)] py-2 px-4">جاري تحميل الأقسام...</div>
          ) : sections && sections.length > 0 ? (
            sections.map((section) => <SectionRow key={section.id} section={section} />)
          ) : (
            <div className="text-xs text-[var(--admin-muted)] py-2 px-4">لا توجد أقسام في هذا الترم.</div>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Package Card ─────────────────────────────────────────────────────────────
function PackageCard({ pkg }: { pkg: PackageDto }) {
  const [isOpen, setIsOpen] = useState(false);
  const [terms, setTerms] = useState<TermDto[] | null>(null);
  const [loading, setLoading] = useState(false);
  const contentMode = pkg.contentMode ?? "TermWithSections";
  const directSections = pkg.directSections ?? [];
  const directLessons = pkg.directLessons ?? [];

  const toggleOpen = async () => {
    const nextState = !isOpen;
    setIsOpen(nextState);
    if (nextState && contentMode === "TermWithSections" && terms === null) {
      try {
        setLoading(true);
        const res = await contentService.getTerms(pkg.id);
        const list = (res.data?.data ?? []) as TermDto[];
        setTerms(list.sort((a, b) => a.order - b.order));
      } catch {
        toast.error("تعذر تحميل الأترم.");
      } finally {
        setLoading(false);
      }
    }
  };

  return (
    <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] shadow-sm transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:border-[var(--admin-primary)] hover:shadow-[0_0_0_1px_var(--admin-primary)] overflow-hidden">
      <div className="flex items-center gap-2 px-3 py-3 hover:bg-[var(--admin-card)] transition-colors sm:px-5">
        <button
          type="button"
          onClick={toggleOpen}
          aria-expanded={isOpen}
          className="flex min-h-14 min-w-0 flex-1 items-center gap-4 rounded-xl px-2 py-1 text-right focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
        >
          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-lg font-black text-[var(--admin-primary)]">
            {pkg.name.trim()[0]}
          </div>

          <div className="flex-1 min-w-0">
            <p className="font-black text-[var(--admin-text)] leading-tight truncate">{pkg.name}</p>
            {pkg.description && (
              <p className="text-xs text-[var(--admin-muted)] mt-0.5 line-clamp-1">{pkg.description}</p>
            )}
            <p className="text-xs font-bold text-[var(--admin-primary)] mt-1">{pkg.price} جنيه</p>
          </div>

          <ChevronLeft
            className={`h-5 w-5 shrink-0 text-[var(--admin-muted)] transition-transform duration-200 ${
              isOpen ? "-rotate-90" : ""
            }`}
            aria-hidden="true"
          />
        </button>

        <Link
          href={`/teacher/packages/packages/${pkg.id}`}
          className="p-2 rounded-xl text-[var(--admin-muted)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] transition"
          aria-label={`عرض تفاصيل الباقة ${pkg.name}`}
          title="عرض تفاصيل الباقة"
        >
          <Eye className="h-5 w-5" aria-hidden="true" />
        </Link>
      </div>

      {isOpen && (
        <div className="border-t border-[var(--admin-border)] bg-[var(--admin-bg)] p-4 space-y-2">
          {loading ? (
            <div className="text-sm text-[var(--admin-muted)] py-4 text-center">جاري تحميل أترم الباقة...</div>
          ) : contentMode === "SectionWithLessons" && directSections.length > 0 ? (
            directSections.map((section) => <SectionRow key={section.id} section={section} />)
          ) : contentMode === "LessonsOnly" && directLessons.length > 0 ? (
            directLessons.map((lesson) => <LessonRow key={lesson.id} lesson={lesson} />)
          ) : contentMode === "TermWithSections" && terms && terms.length > 0 ? (
            terms.map((term) => <TermRow key={term.id} term={term} />)
          ) : (
            <div className="text-sm text-[var(--admin-muted)] py-4 text-center">
              {contentMode === "SectionWithLessons"
                ? "لا توجد أقسام في هذه الباقة."
                : contentMode === "LessonsOnly"
                  ? "لا توجد حصص في هذه الباقة."
                  : "لا توجد أترم في هذه الباقة."}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────
export default function TeacherContentPageClient() {
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [profile, setProfile] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [activeTab, setActiveTab] = useState<'summary' | 'content'>('summary');

  const loadPackages = useCallback(async () => {
    try {
      setLoading(true);
      const [res, subjectsRes, profileRes] = await Promise.all([
        contentService.getPackages(),
        teacherService.getMySubjects().catch(() => ({ success: true, data: [] as SubjectDto[] })),
        teacherService.getMyProfile().catch(() => ({ success: true, data: null }))
      ]);
      setPackages(res.data?.data ?? []);
      setSubjects(subjectsRes.data ?? []);
      if (profileRes && profileRes.success) {
        setProfile(profileRes.data);
      }
    } catch {
      toast.error("تعذر تحميل الباقات.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadPackages(); }, [loadPackages]);

  const filtered = search.trim()
    ? packages.filter((p) => p.name.toLowerCase().includes(search.toLowerCase()))
    : packages;

  return (
    <TeacherPage
      activePath="/teacher/packages"
      sectionLabel="إدارة المحتوى"
      pageTitle="الباقات التعليمية"
      subtitle="كل باقة تحتوي على أترام وأقسام وحصص ودروس خاصة بك"
    >
      {loading ? (
        <AdminPageSkeleton />
      ) : (
        <div className="space-y-8">
          <AdminTabBar
            tabs={[
              { key: 'summary', label: 'الملخص', icon: BarChart3 },
              { key: 'content', label: 'المحتوى', icon: Layers3 },
            ]}
            activeTab={activeTab}
            onSelect={setActiveTab}
          />

          {activeTab === 'summary' ? (
            <ContentSummaryPanel scope="teacher" />
          ) : (
            <>
          {/* Stats */}
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
            <AdminStatCard variant="accent" icon={BookOpenText} label="إجمالي الباقات" value={packages.length} />
            <AdminStatCard variant="light" icon={Sparkles} label="إجمالي الإيرادات" value={`${packages.reduce((s, p) => s + p.price, 0)} ج`} />
            <AdminStatCard variant="muted" icon={Video} label="نشطة" value={packages.length} />
          </div>

          {/* Search */}
          {packages.length > 3 && (
            <AdminSearchToolbar
              value={search}
              onChange={setSearch}
              placeholder="ابحث في الباقات..."
            />
          )}

          {/* Package list */}
          <div className="space-y-3">
            {filtered.map((pkg) => <PackageCard key={pkg.id} pkg={pkg} />)}
            <CreatePackageRow onSuccess={loadPackages} subjects={subjects} profile={profile} />
          </div>
            </>
          )}
        </div>
      )}
    </TeacherPage>
  );
}
