'use client';

import { useEffect, useState, useCallback, useRef } from 'react';
import Link from 'next/link';
import {
  BookOpenText, Plus, ChevronLeft, Sparkles, Video, Search, Eye, Folder, FolderOpen, FileText, Upload,
} from 'lucide-react';
import { AdminShellChrome, AdminPageSkeleton, AdminStatCard } from '@/components/admin';
import { contentService, PackageDto, TermDto, ContentSectionDto, LessonSummaryDto } from '@/services/content-service';
import { adminService } from '@/services/admin-service';
import { teacherService, SubjectDto, TeacherDto } from '@/services/teacher-service';
import NeumorphButton from '@/components/ui/neumorph-button';
import toast from 'react-hot-toast';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

const GRADE_NAMES: Record<string, string> = {
  FirstSecondary: 'الأول الثانوي',
  SecondSecondary: 'الثاني الثانوي',
  SecondaryGrade3: 'الثالث الثانوي',
  FirstBaccalaureate: 'الأول بكالوريا',
  SecondBaccalaureate: 'الثاني بكالوريا',
  PrimaryGrade1: 'الأول الابتدائي',
  PrimaryGrade2: 'الثاني الابتدائي',
  PrimaryGrade3: 'الثالث الابتدائي',
  PrimaryGrade4: 'الرابع الابتدائي',
  PrimaryGrade5: 'الخامس الابتدائي',
  PrimaryGrade6: 'السادس الابتدائي',
  PrepGrade1: 'الأول الإعدادي',
  PrepGrade2: 'الثاني الإعدادي',
  PrepGrade3: 'الثالث الإعدادي',
  AzhariPrimary1: 'الأول الابتدائي الأزهري',
  AzhariPrep1: 'الأول الإعدادي الأزهري',
  AzhariSecondary1: 'الأول الثانوي الأزهري',
  AmericanGrade9: 'Grade 9',
  AmericanGrade10: 'Grade 10',
  AmericanGrade11: 'Grade 11',
  AmericanGrade12: 'Grade 12',
};

function getTeacherPackageGrades(teacher: TeacherDto | undefined): { value: string; label: string }[] {
  if (!teacher || !teacher.specialization) return [];
  const specs = teacher.specialization.split(',');
  const list: { value: string; label: string }[] = [];
  
  const mapping: Record<string, { value: string; label: string }> = {
    'FirstSecondary': { value: '1st Secondary', label: 'الصف الأول الثانوي' },
    'SecondSecondary': { value: '2nd Secondary', label: 'الصف الثاني الثانوي' },
    'SecondaryGrade3': { value: '3rd Secondary', label: 'الصف الثالث الثانوي' },
    '1st Secondary': { value: '1st Secondary', label: 'الصف الأول الثانوي' },
    '2nd Secondary': { value: '2nd Secondary', label: 'الصف الثاني الثانوي' },
    '3rd Secondary': { value: '3rd Secondary', label: 'الصف الثالث الثانوي' },
  };

  specs.forEach(spec => {
    const trimmed = spec.trim();
    if (mapping[trimmed]) {
      list.push(mapping[trimmed]);
    } else {
      list.push({ value: trimmed, label: GRADE_NAMES[trimmed] || trimmed });
    }
  });

  return list;
}

// ─── Create Package Inline Form ───────────────────────────────────────────────
function CreatePackageRow({ 
  onSuccess, 
  teachers, 
  subjects,
  activeTeacherId
}: { 
  onSuccess: () => void; 
  teachers: TeacherDto[]; 
  subjects: SubjectDto[]; 
  activeTeacherId?: string | null;
}) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [price, setPrice] = useState('');
  const [selectedTeacherId, setSelectedTeacherId] = useState(activeTeacherId || '');
  const [selectedSubjectId, setSelectedSubjectId] = useState('');
  const [selectedGrade, setSelectedGrade] = useState('');
  const [saving, setSaving] = useState(false);
  
  // Image Upload States
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreview, setImagePreview] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (activeTeacherId) {
      setSelectedTeacherId(activeTeacherId);
    } else {
      setSelectedTeacherId('');
    }
  }, [activeTeacherId]);

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
    if (!name.trim() || !selectedSubjectId || !selectedTeacherId || !selectedGrade) return;
    try {
      setSaving(true);
      const newPkg = await adminService.createPackage({
        name: name.trim(),
        description: description.trim(),
        price: Number(price) || 0,
        subjectId: selectedSubjectId,
        targetGrade: selectedGrade,
        teacherId: selectedTeacherId
      });

      if (newPkg?.id && imageFile) {
        try {
          await adminService.uploadContentImage('package', newPkg.id, imageFile);
        } catch {
          toast.error('تم حفظ الباقة، لكن فشل رفع الصورة.');
        }
      }

      toast.success('تمت إضافة الباقة بنجاح.');
      setName(''); setDescription(''); setPrice(''); 
      if (!activeTeacherId) setSelectedTeacherId('');
      setSelectedSubjectId(''); setSelectedGrade('');
      setImageFile(null); setImagePreview(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
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
              <span className="text-[9px]">الحد الأقصى 10 ميجابايت (يتم تحويلها لـ WebP)</span>
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
      
      {!activeTeacherId && (
        <select
          value={selectedTeacherId}
          onChange={(e) => {
            setSelectedTeacherId(e.target.value);
            setSelectedSubjectId('');
            setSelectedGrade('');
          }}
          className="admin-input"
        >
          <option value="">اختر المدرس...</option>
          {teachers.map((t) => (
            <option key={t.id} value={t.id}>{t.fullName}</option>
          ))}
        </select>
      )}

      <select
        value={selectedSubjectId}
        onChange={(e) => {
          setSelectedSubjectId(e.target.value);
          setSelectedGrade('');
        }}
        className="admin-input"
        disabled={!selectedTeacherId}
      >
        <option value="">
          {selectedTeacherId ? 'اختر المادة...' : 'يرجى اختيار المدرس أولاً...'}
        </option>
        {(() => {
          const selectedTeacher = teachers.find(t => t.id === selectedTeacherId);
          const filteredSubjects = selectedTeacher
            ? subjects.filter(s => selectedTeacher.subjectIds?.includes(s.id))
            : [];
          return filteredSubjects.map((s) => (
            <option key={s.id} value={s.id}>{s.name}</option>
          ));
        })()}
      </select>

      <select
        value={selectedGrade}
        onChange={(e) => setSelectedGrade(e.target.value)}
        className="admin-input"
        disabled={!selectedSubjectId}
      >
        <option value="">اختر الصف الدراسي...</option>
        {(() => {
          const selectedTeacher = teachers.find(t => t.id === selectedTeacherId);
          const teacherGrades = getTeacherPackageGrades(selectedTeacher);
          return teacherGrades.map((g) => (
            <option key={g.value} value={g.value}>{g.label}</option>
          ));
        })()}
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
          disabled={saving || !name.trim() || !selectedSubjectId || !selectedTeacherId || !selectedGrade}
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
        prefetch={false}
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
          prefetch={false}
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
          prefetch={false}
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
            prefetch={false}
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
export default function AdminContentPageClient() {
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>('All');
  const selectedTeacherId = 'All';
  const [activeTeacherId, setActiveTeacherId] = useState<string | null>(null);

  const loadPackages = useCallback(async () => {
    try {
      setLoading(true);
      const [packagesRes, subjectsRes, teachersRes] = await Promise.all([
        contentService.getPackages({ force: true }),
        teacherService.getSubjects().catch(() => ({ success: true, data: [] as SubjectDto[] })),
        teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] }))
      ]);
      setPackages(packagesRes.data?.data ?? []);
      setSubjects(subjectsRes.data ?? []);
      setTeachers(teachersRes.data ?? []);
    } catch {
      toast.error('تعذر تحميل الباقات.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadPackages(); }, [loadPackages]);

  // Filter packages based on search, subject, and teacher
  const filtered = packages.filter((p) => {
    const matchesSearch = !search.trim() || p.name.toLowerCase().includes(search.toLowerCase());
    const matchesSubject = selectedSubjectId === 'All' || p.subjectId === selectedSubjectId;
    const matchesTeacher = activeTeacherId ? p.teacherId === activeTeacherId : (selectedTeacherId === 'All' || p.teacherId === selectedTeacherId);
    return matchesSearch && matchesSubject && matchesTeacher;
  });

  // Filter teachers for the grid view
  const filteredTeachers = teachers.filter((t) => {
    const matchesSearch = !search.trim() || t.fullName.toLowerCase().includes(search.toLowerCase());
    const matchesSubject = selectedSubjectId === 'All' || t.subjectIds?.includes(selectedSubjectId);
    return matchesSearch && matchesSubject;
  });

  const activeTeacher = teachers.find(t => t.id === activeTeacherId);

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى"
      pageTitle="المناهج التعليمية"
      subtitle={activeTeacher ? `إدارة باقات ومحتوى المعلم: ${activeTeacher.fullName}` : "اختر المعلم أولاً لتصفح وإدارة المحتوى الدراسي الخاص به"}
    >
      {loading ? (
        <AdminPageSkeleton />
      ) : (
        <div className="space-y-8 animate-[fadeIn_0.3s_ease-out]">
          
          {activeTeacher ? (
            /* Scoped Teacher View */
            <div className="space-y-6">
              <div className="flex items-center justify-between border-b border-[var(--admin-border)]/40 pb-4">
                <NeumorphButton
                  intent="primary"
                  size="md"
                  pill
                  onClick={() => {
                    setActiveTeacherId(null);
                    setSearch('');
                  }}
                  className="flex items-center gap-1.5"
                >
                  <ChevronLeft className="h-4 w-4 rotate-180" />
                  العودة لقائمة المعلمين
                </NeumorphButton>

                <div className="flex items-center gap-3">
                  {activeTeacher.profileImageUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img 
                      src={resolveMediaUrl(activeTeacher.profileImageUrl)} 
                      alt={activeTeacher.fullName} 
                      className="w-10 h-10 rounded-xl object-cover border border-[var(--admin-border)]"
                    />
                  ) : (
                    <div className="w-10 h-10 rounded-xl bg-[var(--admin-primary-15)] flex items-center justify-center text-[var(--admin-primary)] font-black text-sm">
                      {activeTeacher.fullName[0]}
                    </div>
                  )}
                  <div className="text-right">
                    <p className="text-sm font-black text-[var(--admin-text)] leading-tight">{activeTeacher.fullName}</p>
                    <p className="text-[10px] text-[var(--admin-muted)] mt-0.5">{activeTeacher.phoneNumber}</p>
                  </div>
                </div>
              </div>

              {/* Stats for Teacher */}
              <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
                <AdminStatCard variant="accent" icon={BookOpenText} label="باقات المعلم" value={filtered.length} />
                <AdminStatCard variant="light" icon={Sparkles} label="إيرادات الباقات" value={`${filtered.reduce((s, p) => s + p.price, 0)} ج`} />
                <AdminStatCard variant="muted" icon={Video} label="الباقات النشطة" value={filtered.length} />
              </div>

              {/* Search & Subject filter */}
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
                
                <div className="min-w-[200px]">
                  <select
                    value={selectedSubjectId}
                    onChange={(e) => setSelectedSubjectId(e.target.value)}
                    className="admin-input w-full"
                  >
                    <option value="All">كل المواد</option>
                    {subjects.filter(s => activeTeacher.subjectIds?.includes(s.id)).map((sub) => (
                      <option key={sub.id} value={sub.id}>{sub.name}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Package list */}
              <div className="space-y-4">
                {filtered.length > 0 ? (
                  filtered.map((pkg) => <PackageCard key={pkg.id} pkg={pkg} />)
                ) : (
                  <div className="text-center py-10 rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-sm text-[var(--admin-muted)] font-bold">
                    لا توجد باقات مضافة لهذا المعلم تلتزم بشروط الفلترة.
                  </div>
                )}
                <CreatePackageRow 
                  onSuccess={loadPackages} 
                  teachers={teachers} 
                  subjects={subjects} 
                  activeTeacherId={activeTeacherId}
                />
              </div>
            </div>
          ) : (
            /* Grid View of all Teachers */
            <div className="space-y-6">
              {/* Stats for Content */}
              <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
                <AdminStatCard variant="accent" icon={BookOpenText} label="إجمالي الباقات" value={packages.length} />
                <AdminStatCard variant="light" icon={Sparkles} label="إجمالي المعلمين" value={teachers.length} />
                <AdminStatCard variant="muted" icon={Video} label="إجمالي المواد" value={subjects.length} />
              </div>

              {/* Search & Subject filter */}
              <div className="flex flex-col gap-4 md:flex-row md:items-center">
                <div className="relative flex-1">
                  <Search className="absolute right-4 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                  <input
                    type="text"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="ابحث عن معلم بالاسم..."
                    className="admin-input pr-11"
                  />
                </div>
                
                <div className="min-w-[200px]">
                  <select
                    value={selectedSubjectId}
                    onChange={(e) => setSelectedSubjectId(e.target.value)}
                    className="admin-input w-full"
                  >
                    <option value="All">كل المواد</option>
                    {subjects.map((sub) => (
                      <option key={sub.id} value={sub.id}>{sub.name}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Teacher Cards Grid */}
              {filteredTeachers.length > 0 ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                  {filteredTeachers.map((teacher) => {
                    const teacherPackagesCount = packages.filter(p => p.teacherId === teacher.id).length;
                    const gradeList = teacher.specialization ? teacher.specialization.split(',') : [];
                    const teacherSubjects = subjects.filter(s => teacher.subjectIds?.includes(s.id));
                    
                    return (
                      <div 
                        key={teacher.id}
                        onClick={() => {
                          setActiveTeacherId(teacher.id);
                          setSelectedSubjectId('All');
                          setSearch('');
                        }}
                        className="group relative overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-6 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-[var(--admin-primary)] hover:shadow-md cursor-pointer flex flex-col justify-between min-h-[220px]"
                      >
                        <div>
                          <div className="flex items-center gap-4 mb-4">
                            {teacher.profileImageUrl ? (
                              // eslint-disable-next-line @next/next/no-img-element
                              <img 
                                src={resolveMediaUrl(teacher.profileImageUrl)} 
                                alt={teacher.fullName} 
                                className="w-14 h-14 rounded-2xl object-cover border border-[var(--admin-border)] shadow-sm"
                              />
                            ) : (
                              <div className="w-14 h-14 rounded-2xl bg-[var(--admin-primary-15)] flex items-center justify-center text-[var(--admin-primary)] font-black text-xl shadow-sm">
                                {teacher.fullName[0]}
                              </div>
                            )}
                            <div>
                              <h3 className="font-black text-[var(--admin-text)] text-base group-hover:text-[var(--admin-primary)] transition-colors">
                                {teacher.fullName}
                              </h3>
                              <p className="text-xs text-[var(--admin-muted)] font-mono mt-0.5">{teacher.phoneNumber}</p>
                            </div>
                          </div>

                          {/* Taught Grades */}
                          <div className="mb-3">
                            <p className="text-[10px] font-bold text-[var(--admin-muted)] mb-1">الصفوف الدراسية:</p>
                            <div className="flex flex-wrap gap-1">
                              {gradeList.length > 0 ? (
                                gradeList.map((val, idx) => (
                                  <span key={idx} className="inline-flex rounded-full bg-[var(--admin-primary-15)] px-2 py-0.5 text-[9px] font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/10">
                                    {GRADE_NAMES[val] || val}
                                  </span>
                                ))
                              ) : (
                                <span className="text-[10px] text-red-500 font-bold">غير محدد</span>
                              )}
                            </div>
                          </div>

                          {/* Subjects */}
                          <div className="mb-4">
                            <p className="text-[10px] font-bold text-[var(--admin-muted)] mb-1">المواد الدراسية:</p>
                            <div className="flex flex-wrap gap-1">
                              {teacherSubjects.length > 0 ? (
                                teacherSubjects.map((sub) => (
                                  <span key={sub.id} className="inline-flex rounded-full bg-[var(--admin-primary-15)] px-2 py-0.5 text-[9px] font-bold text-[var(--admin-primary)] border border-[var(--admin-primary)]/10">
                                    {sub.name}
                                  </span>
                                ))
                              ) : (
                                <span className="text-[10px] text-[var(--admin-muted)]">—</span>
                              )}
                            </div>
                          </div>
                        </div>

                        <div className="pt-4 border-t border-[var(--admin-border)] flex items-center justify-between mt-auto">
                          <span className="text-xs font-bold text-[var(--admin-muted)]">إجمالي الباقات:</span>
                          <span className="text-sm font-black text-[var(--admin-primary)] bg-[var(--admin-primary-15)] px-2.5 py-1 rounded-xl">
                            {teacherPackagesCount} باقات
                          </span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              ) : (
                <div className="text-center py-20 rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/50 text-[var(--admin-muted)] font-bold text-sm">
                  لا توجد نتائج مطابقة لفلترة المعلمين.
                </div>
              )}
            </div>
          )}

        </div>
      )}
    </AdminShellChrome>
  );
}
