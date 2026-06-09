'use client';

import { useEffect, useState, useCallback } from 'react';
import Link from 'next/link';
import {
  BookOpenText, Plus, ChevronLeft, Sparkles, Video, Search, Eye, Folder, FolderOpen, FileText,
} from 'lucide-react';
import { AdminShellChrome, AdminPageSkeleton, AdminStatCard } from '@/components/admin';
import { contentService, PackageDto, TermDto, ContentSectionDto, LessonSummaryDto } from '@/services/content-service';
import { adminService, AdminProgramDto } from '@/services/admin-service';
import { teacherService, SubjectDto, TeacherDto } from '@/services/teacher-service';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';

// ─── Create Package Inline Form ───────────────────────────────────────────────
function CreatePackageRow({ onSuccess, teachers, programs }: { onSuccess: () => void; teachers: TeacherDto[]; programs: AdminProgramDto[] }) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [price, setPrice] = useState('');
  const [selectedTeacherId, setSelectedTeacherId] = useState('');
  const [selectedProgramId, setSelectedProgramId] = useState('');
  const [saving, setSaving] = useState(false);

  async function handleCreate() {
    if (!name.trim() || !selectedProgramId || !selectedTeacherId) return;
    try {
      setSaving(true);
      await adminService.createPackage({
        name: name.trim(),
        description: description.trim(),
        price: Number(price) || 0,
        programId: selectedProgramId,
        teacherId: selectedTeacherId
      });
      toast.success('تمت إضافة الباقة بنجاح.');
      setName(''); setDescription(''); setPrice(''); setSelectedTeacherId(''); setSelectedProgramId('');
      setOpen(false);
      onSuccess();
    } catch {
      toast.error('حدث خطأ أثناء الإضافة.');
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
        onKeyDown={(e) => { if (e.key === 'Escape') setOpen(false); }}
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
      
      <select
        value={selectedProgramId}
        onChange={(e) => setSelectedProgramId(e.target.value)}
        className="admin-input"
      >
        <option value="">اختر البرنامج الدراسي...</option>
        {programs.map((p) => (
          <option key={p.id} value={p.id}>{p.name} ({p.subjectName})</option>
        ))}
      </select>

      <select
        value={selectedTeacherId}
        onChange={(e) => setSelectedTeacherId(e.target.value)}
        className="admin-input"
      >
        <option value="">اختر المدرس...</option>
        {teachers.map((t) => (
          <option key={t.id} value={t.id}>{t.fullName}</option>
        ))}
      </select>

      <div className="flex justify-end gap-2 pt-1">
        <button
          onClick={() => setOpen(false)}
          className="rounded-xl border border-[var(--admin-border)] px-4 py-2 text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-card-strong)] transition"
        >
          إلغاء
        </button>
        <NeumorphButton
          onClick={() => void handleCreate()}
          disabled={saving || !name.trim() || !selectedProgramId || !selectedTeacherId}
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
function LessonRow({ lesson }: { lesson: LessonSummaryDto }) {
  return (
    <div className="flex items-center justify-between py-2 px-3 rounded-xl hover:bg-[var(--admin-card-soft)] transition-colors">
      <div className="flex items-center gap-3 min-w-0">
        <FileText className="h-4 w-4 text-[var(--admin-muted)] shrink-0" />
        <span className="text-xs font-semibold text-[var(--admin-text)] truncate">{lesson.title}</span>
      </div>
      <Link
        href={`/admin/content/lessons/${lesson.id}`}
        className="p-1.5 rounded-lg text-[var(--admin-muted)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] transition"
        title="عرض الدرس"
      >
        <Eye className="h-4 w-4" />
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
        toast.error('تعذر تحميل الدروس.');
      } finally {
        setLoading(false);
      }
    }
  };

  return (
    <div className="space-y-1">
      <div
        onClick={toggleOpen}
        className="flex items-center justify-between py-2 px-3 rounded-xl hover:bg-[var(--admin-card-soft)] transition-colors cursor-pointer"
      >
        <div className="flex items-center gap-3 min-w-0">
          <ChevronLeft
            className={`h-4 w-4 text-[var(--admin-muted)] transition-transform duration-200 shrink-0 ${
              isOpen ? '-rotate-90' : ''
            }`}
          />
          <FolderOpen className="h-4 w-4 text-[var(--admin-primary)]/80 shrink-0" />
          <span className="text-xs font-bold text-[var(--admin-text)] truncate">{section.title}</span>
        </div>
        <Link
          href={`/admin/content/sections/${section.id}`}
          onClick={(e) => e.stopPropagation()}
          className="p-1.5 rounded-lg text-[var(--admin-muted)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] transition"
          title="عرض القسم"
        >
          <Eye className="h-4 w-4" />
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
        toast.error('تعذر تحميل الأقسام.');
      } finally {
        setLoading(false);
      }
    }
  };

  return (
    <div className="space-y-1">
      <div
        onClick={toggleOpen}
        className="flex items-center justify-between py-2 px-4 rounded-xl hover:bg-[var(--admin-card-strong)] transition-colors cursor-pointer"
      >
        <div className="flex items-center gap-3 min-w-0">
          <ChevronLeft
            className={`h-4 w-4 text-[var(--admin-muted)] transition-transform duration-200 shrink-0 ${
              isOpen ? '-rotate-90' : ''
            }`}
          />
          <Folder className="h-4.5 w-4.5 text-[var(--admin-primary)] shrink-0" />
          <span className="text-xs font-black text-[var(--admin-text)] truncate">{term.title}</span>
        </div>
        <Link
          href={`/admin/content/terms/${term.id}`}
          onClick={(e) => e.stopPropagation()}
          className="p-1.5 rounded-lg text-[var(--admin-muted)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] transition"
          title="عرض الترم"
        >
          <Eye className="h-4 w-4" />
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

  const toggleOpen = async () => {
    const nextState = !isOpen;
    setIsOpen(nextState);
    if (nextState && terms === null) {
      try {
        setLoading(true);
        const res = await contentService.getTerms(pkg.id);
        const list = (res.data?.data ?? []) as TermDto[];
        setTerms(list.sort((a, b) => a.order - b.order));
      } catch {
        toast.error('تعذر تحميل الأترم.');
      } finally {
        setLoading(false);
      }
    }
  };

  return (
    <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] shadow-sm transition-all hover:border-[var(--admin-primary)] hover:shadow-[0_0_0_1px_var(--admin-primary)] overflow-hidden">
      {/* Header card area */}
      <div
        onClick={toggleOpen}
        className="flex items-center gap-4 px-5 py-4 cursor-pointer hover:bg-[var(--admin-card)] transition-colors"
      >
        {/* Avatar */}
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-lg font-black text-[var(--admin-primary)]">
          {pkg.name.trim()[0]}
        </div>

        {/* Info */}
        <div className="flex-1 min-w-0">
          <p className="font-black text-[var(--admin-text)] leading-tight truncate">{pkg.name}</p>
          {pkg.description && (
            <p className="text-xs text-[var(--admin-muted)] mt-0.5 line-clamp-1">{pkg.description}</p>
          )}
          <p className="text-xs font-bold text-[var(--admin-primary)] mt-1">{pkg.price} جنيه</p>
        </div>

        {/* Action icons */}
        <div className="flex items-center gap-2 shrink-0">
          <Link
            href={`/admin/content/packages/${pkg.id}`}
            onClick={(e) => e.stopPropagation()}
            className="p-2 rounded-xl text-[var(--admin-muted)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] transition"
            title="عرض تفاصيل الباقة"
          >
            <Eye className="h-5 w-5" />
          </Link>
          <ChevronLeft
            className={`h-5 w-5 text-[var(--admin-muted)] transition-transform duration-200 ${
              isOpen ? '-rotate-90' : ''
            }`}
          />
        </div>
      </div>

      {/* Expanded terms list */}
      {isOpen && (
        <div className="border-t border-[var(--admin-border)] bg-[var(--admin-bg)] p-4 space-y-2">
          {loading ? (
            <div className="text-sm text-[var(--admin-muted)] py-4 text-center">جاري تحميل أترم الباقة...</div>
          ) : terms && terms.length > 0 ? (
            terms.map((term) => <TermRow key={term.id} term={term} />)
          ) : (
            <div className="text-sm text-[var(--admin-muted)] py-4 text-center">لا توجد أترم في هذه الباقة.</div>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────
export default function AdminContentPage() {
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [programs, setPrograms] = useState<AdminProgramDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>('All');
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>('All');

  const loadPackages = useCallback(async () => {
    try {
      setLoading(true);
      const [packagesRes, subjectsRes, teachersRes, programsRes] = await Promise.all([
        contentService.getPackages({ force: true }),
        teacherService.getSubjects().catch(() => ({ success: true, data: [] as SubjectDto[] })),
        teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] })),
        adminService.listPrograms().catch(() => [] as AdminProgramDto[])
      ]);
      setPackages(packagesRes.data?.data ?? []);
      setSubjects(subjectsRes.data ?? []);
      setTeachers(teachersRes.data ?? []);
      setPrograms(programsRes);
    } catch {
      toast.error('تعذر تحميل الباقات.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadPackages(); }, [loadPackages]);

  const filtered = packages.filter((p) => {
    const matchesSearch = !search.trim() || p.name.toLowerCase().includes(search.toLowerCase());
    const matchesSubject = selectedSubjectId === 'All' || p.subjectId === selectedSubjectId;
    const matchesTeacher = selectedTeacherId === 'All' || p.teacherId === selectedTeacherId;
    return matchesSearch && matchesSubject && matchesTeacher;
  });

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى"
      pageTitle="الباقات التعليمية"
      subtitle="كل باقة تحتوي على أترام وأقسام وحصص ودروس"
    >
      {loading ? (
        <AdminPageSkeleton />
      ) : (
        <div className="space-y-8">
          {/* Stats */}
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
            <AdminStatCard variant="accent" icon={BookOpenText} label="إجمالي الباقات" value={packages.length} />
            <AdminStatCard variant="light" icon={Sparkles} label="إجمالي الإيرادات" value={`${packages.reduce((s, p) => s + p.price, 0)} ج`} />
            <AdminStatCard variant="muted" icon={Video} label="نشطة" value={packages.length} />
          </div>

          {/* Search & Filters */}
          <div className="flex flex-col gap-4 md:flex-row md:items-center">
            <div className="relative flex-1">
              <Search className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="ابحث في الباقات..."
                className="admin-input pr-11"
              />
            </div>
            
            <div className="flex gap-3 min-w-[320px]">
              <select
                value={selectedSubjectId}
                onChange={(e) => setSelectedSubjectId(e.target.value)}
                className="admin-input flex-1"
              >
                <option value="All">كل المواد</option>
                {subjects.map((sub) => (
                  <option key={sub.id} value={sub.id}>{sub.name}</option>
                ))}
              </select>

              <select
                value={selectedTeacherId}
                onChange={(e) => setSelectedTeacherId(e.target.value)}
                className="admin-input flex-1"
              >
                <option value="All">كل المدرسين</option>
                {teachers.map((t) => (
                  <option key={t.id} value={t.id}>{t.fullName}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Package list */}
          <div className="space-y-3">
            {filtered.map((pkg) => <PackageCard key={pkg.id} pkg={pkg} />)}
            <CreatePackageRow onSuccess={loadPackages} teachers={teachers} programs={programs} />
          </div>
        </div>
      )}
    </AdminShellChrome>
  );
}
