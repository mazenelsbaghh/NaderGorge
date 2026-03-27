'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  BookOpenText,
  Eye,
  Folder,
  GraduationCap,
  Plus,
  Search,
  Sparkles,
  Trash2,
  Video,
  Calendar,
} from 'lucide-react';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminModal,
  AdminSearchToolbar,
  AdminTabBar,
  AdminTab,
} from '@/components/admin';
import { formatCompactNumber, getInitials } from '@/components/admin/admin-utils';
import { adminService } from '@/services/admin-service';
import {
  ContentSectionDto,
  LessonSummaryDto,
  PackageDto,
  TermDto,
  VideoDto,
  contentService,
} from '@/services/content-service';
import toast from 'react-hot-toast';

type ActiveTab = 'packages' | 'terms' | 'sections' | 'lessons' | 'videos';
type ModalType = 'package' | 'term' | 'section' | 'lesson' | 'video' | null;

type PackageRow = PackageDto & {
  termCount: number;
  sectionCount: number;
  lessonCount: number;
  status: 'نشطة' | 'مسودة';
};

type TermRow = TermDto & {
  packageId: string;
  packageName: string;
  sectionCount: number;
};

type SectionRow = ContentSectionDto & {
  packageId: string;
  packageName: string;
  termId: string;
  termTitle: string;
  lessonCount: number;
};

type LessonRow = LessonSummaryDto & {
  packageId: string;
  packageName: string;
  termId: string;
  termTitle: string;
  sectionId: string;
  sectionTitle: string;
  videoCount: number;
};

type VideoRow = VideoDto & {
  packageId: string;
  packageName: string;
  termId: string;
  termTitle: string;
  sectionId: string;
  sectionTitle: string;
  lessonId: string;
  lessonTitle: string;
};

const TAB_OPTIONS: AdminTab<ActiveTab>[] = [
  { key: 'packages', label: 'الباقات', icon: BookOpenText },
  { key: 'terms', label: 'الأترام', icon: Calendar },
  { key: 'sections', label: 'الأقسام', icon: Folder },
  { key: 'lessons', label: 'الدروس', icon: GraduationCap },
  { key: 'videos', label: 'الفيديوهات', icon: Video },
];

function statusClass(status: 'نشطة' | 'مسودة') {
  return status === 'نشطة'
    ? 'bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-400'
    : 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400';
}

export default function AdminContentPage() {
  const [activeTab, setActiveTab] = useState<ActiveTab>('packages');
  const [search, setSearch] = useState('');
  const [packages, setPackages] = useState<PackageRow[]>([]);
  const [terms, setTerms] = useState<TermRow[]>([]);
  const [sections, setSections] = useState<SectionRow[]>([]);
  const [lessons, setLessons] = useState<LessonRow[]>([]);
  const [videos, setVideos] = useState<VideoRow[]>([]);
  const [loading, setLoading] = useState(true);

  const [saving, setSaving] = useState(false);
  const [modalType, setModalType] = useState<ModalType>(null);
  
  // Form State
  const [packageName, setPackageName] = useState('');
  const [packageDescription, setPackageDescription] = useState('');
  const [packagePrice, setPackagePrice] = useState(0);
  
  const [selectedPackageId, setSelectedPackageId] = useState('');
  const [selectedTermId, setSelectedTermId] = useState('');
  const [selectedSectionId, setSelectedSectionId] = useState('');
  
  const [entityTitle, setEntityTitle] = useState('');
  const [entitySummary, setEntitySummary] = useState('');
  const [entityOrder, setEntityOrder] = useState(1);
  
  const [videoProvider, setVideoProvider] = useState<'YouTube' | 'Vimeo' | 'Custom'>('YouTube');
  const [videoCode, setVideoCode] = useState('');
  const [videoLimit, setVideoLimit] = useState(3);

  useEffect(() => {
    void loadContentData();
  }, []);

  async function loadContentData() {
    try {
      setLoading(true);

      const packagesRes = await contentService.getPackages();
      const packageItems = (packagesRes.data?.data || []) as PackageDto[];

      // Load Terms
      const termRows: TermRow[] = [];
      await Promise.all(
        packageItems.map(async (pkg) => {
          try {
            const res = await contentService.getTerms(pkg.id);
            const items = (res.data?.data || []) as TermDto[];
            termRows.push(...items.map((t) => ({ ...t, packageId: pkg.id, packageName: pkg.name, sectionCount: 0 })));
          } catch {}
        })
      );

      // Load Sections
      const sectionRows: SectionRow[] = [];
      await Promise.all(
        termRows.map(async (term) => {
          try {
            const response = await contentService.getSections(term.id);
            const items = (response.data?.data || []) as ContentSectionDto[];
            sectionRows.push(
              ...items.map((sec) => ({
                ...sec,
                packageId: term.packageId,
                packageName: term.packageName,
                termId: term.id,
                termTitle: term.title,
                lessonCount: 0,
              }))
            );
          } catch {}
        })
      );

      // Load Lessons
      const lessonResults = await Promise.all(
        sectionRows.map(async (section) => {
          try {
            const response = await contentService.getLessons(section.id);
            return { section, items: (response.data?.data || []) as LessonSummaryDto[] };
          } catch {
            return { section, items: [] as LessonSummaryDto[] };
          }
        })
      );

      const lessonRows: LessonRow[] = lessonResults.flatMap(({ section, items }) =>
        items.map((lesson) => ({
          ...lesson,
          packageId: section.packageId,
          packageName: section.packageName,
          termId: section.termId,
          termTitle: section.termTitle,
          sectionId: section.id,
          sectionTitle: section.title,
          videoCount: 0,
        }))
      );

      // Map Counts Upwards
      const updatedSections = sectionRows.map((section) => ({
        ...section,
        lessonCount: lessonRows.filter((lesson) => lesson.sectionId === section.id).length,
      }));

      const updatedTerms = termRows.map((term) => ({
        ...term,
        sectionCount: updatedSections.filter((section) => section.termId === term.id).length,
      }));

      // Load Videos
      const lessonDetails = await Promise.all(
        lessonRows.map(async (lesson) => {
          try {
            const response = await contentService.getLessonDetail(lesson.id);
            const detail = response.data?.data;
            return { lessonId: lesson.id, videos: (detail?.videos || []) as VideoDto[] };
          } catch {
            return { lessonId: lesson.id, videos: [] as VideoDto[] };
          }
        })
      );

      const lessonVideoCountMap = new Map(lessonDetails.map((detail) => [detail.lessonId, detail.videos.length]));

      const updatedLessons = lessonRows.map((lesson) => ({
        ...lesson,
        videoCount: lessonVideoCountMap.get(lesson.id) || 0,
      }));

      const videoRows: VideoRow[] = lessonDetails.flatMap((detail) => {
        const lesson = updatedLessons.find((entry) => entry.id === detail.lessonId);
        if (!lesson) return [];
        return detail.videos.map((video) => ({
          ...video,
          packageId: lesson.packageId,
          packageName: lesson.packageName,
          termId: lesson.termId,
          termTitle: lesson.termTitle,
          sectionId: lesson.sectionId,
          sectionTitle: lesson.sectionTitle,
          lessonId: lesson.id,
          lessonTitle: lesson.title,
        }));
      });

      const packageRows: PackageRow[] = packageItems.map((pkg, index) => {
        const packageTerms = updatedTerms.filter((term) => term.packageId === pkg.id);
        const packageSections = updatedSections.filter((section) => section.packageId === pkg.id);
        const packageLessons = updatedLessons.filter((lesson) => lesson.packageId === pkg.id);

        return {
          ...pkg,
          termCount: packageTerms.length,
          sectionCount: packageSections.length,
          lessonCount: packageLessons.length,
          status: index % 3 === 1 ? 'مسودة' : 'نشطة',
        };
      });

      setPackages(packageRows);
      setTerms(updatedTerms.sort((a, b) => a.order - b.order));
      setSections(updatedSections.sort((a, b) => a.order - b.order));
      setLessons(updatedLessons.sort((a, b) => a.order - b.order));
      setVideos(videoRows.sort((a, b) => a.order - b.order));
      
      setSelectedPackageId((current) => current || packageRows[0]?.id || '');
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  }

  function resetModal() {
    setPackageName('');
    setPackageDescription('');
    setPackagePrice(0);
    setEntityTitle('');
    setEntitySummary('');
    setEntityOrder(1);
    setVideoProvider('YouTube');
    setVideoCode('');
    setVideoLimit(3);
  }

  function openModal(type: ModalType) {
    resetModal();
    if (type === 'term' && packages.length > 0) setSelectedPackageId(packages[0].id);
    if (type === 'section' && terms.length > 0) setSelectedTermId(terms[0].id);
    if (type === 'lesson' && sections.length > 0) setSelectedSectionId(sections[0].id);
    if (type === 'video' && lessons.length > 0) setSelectedSectionId(lessons[0].id); // For video we select lesson actually
    if (type === 'video' && lessons.length > 0) setSelectedSectionId(lessons[0].id); // Reuse state
    setModalType(type);
  }

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!modalType) return;

    try {
      setSaving(true);

      if (modalType === 'package') {
        await adminService.createPackage({ name: packageName, description: packageDescription, price: packagePrice });
      } else if (modalType === 'term') {
        await adminService.createTerm({ title: entityTitle, order: entityOrder, packageId: selectedPackageId });
      } else if (modalType === 'section') {
        await adminService.createSection({ title: entityTitle, order: entityOrder, termId: selectedTermId });
      } else if (modalType === 'lesson') {
        await adminService.createLesson({ title: entityTitle, summary: entitySummary, order: entityOrder, sectionId: selectedSectionId });
      } else if (modalType === 'video') {
        // Here selectedSectionId is used to store the lessonId for the video
        await adminService.createVideo({ title: entityTitle, provider: videoProvider, urlOrEmbedCode: videoCode, order: entityOrder, limit: videoLimit, lessonId: selectedSectionId });
      }

      setModalType(null);
      await loadContentData();
      toast.success('تم الحفظ بنجاح');
    } catch (error) {
      console.error(error);
      toast.error('تعذر حفظ العنصر');
    } finally {
      setSaving(false);
    }
  }

  const metrics = useMemo(() => {
    return {
      totalValue: packages.reduce((sum, pkg) => sum + pkg.price, 0),
      totalLessons: lessons.length,
      activePackages: packages.filter((pkg) => pkg.status === 'نشطة').length,
    };
  }, [lessons, packages]);

  // Columns Definitions
  const packageColumns: AdminColumn<PackageRow>[] = [
    {
      key: 'name',
      label: 'الباقة',
      render: (pkg) => (
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] font-black text-[var(--admin-primary)]">
            {getInitials(pkg.name)}
          </div>
          <div>
            <div className="font-bold text-[var(--admin-text)]">{pkg.name}</div>
            <div className="text-xs text-[var(--admin-muted)] mt-1">{pkg.description.slice(0, 50)}...</div>
          </div>
        </div>
      ),
    },
    {
      key: 'content',
      label: 'المحتويات',
      render: (pkg) => (
        <div className="text-sm font-semibold text-[var(--admin-text)]">
          {pkg.termCount} أترام <span className="text-[var(--admin-muted)] mx-1">•</span> {pkg.sectionCount} قسم <span className="text-[var(--admin-muted)] mx-1">•</span> {pkg.lessonCount} درس
        </div>
      ),
    },
    { key: 'price', label: 'السعر', render: (pkg) => <div className="font-bold text-[var(--admin-primary)]">{pkg.price} دك</div> },
    {
      key: 'status', label: 'الحالة', render: (pkg) => (
        <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${statusClass(pkg.status)}`}>
          <span className="h-1.5 w-1.5 rounded-full bg-current" />{pkg.status}
        </span>
      ),
    },
    {
      key: 'actions', label: 'إجراءات', align: 'left', render: () => (
        <div className="flex items-center justify-end gap-2">
          <button className="admin-btn-icon"><Eye className="h-4 w-4" /></button>
          <button className="rounded-full p-2 text-[var(--admin-muted)] transition-all hover:bg-rose-100 hover:text-rose-600"><Trash2 className="h-4 w-4" /></button>
        </div>
      ),
    },
  ];

  const termColumns: AdminColumn<TermRow>[] = [
    {
      key: 'title', label: 'الترم', render: (term) => (
        <div className="flex items-center gap-3">
          <div className="rounded-lg bg-[var(--admin-card-strong)] p-2 text-[var(--admin-muted)] border border-[var(--admin-border)]"><Calendar className="h-4 w-4" /></div>
          <div>
            <div className="font-bold text-[var(--admin-text)]">{term.title}</div>
            <div className="text-xs text-[var(--admin-muted)] mt-1 font-mono">ترتيب: {term.order}</div>
          </div>
        </div>
      ),
    },
    { key: 'package', label: 'الباقة المرتبطة', render: (term) => <span className="font-semibold text-[var(--admin-primary)]">{term.packageName}</span> },
    { key: 'content', label: 'المحتويات', render: (term) => <span className="text-[var(--admin-text)]">{term.sectionCount} أقسام</span> },
    { key: 'actions', label: 'إجراءات', align: 'left', render: () => (
        <div className="flex items-center justify-end gap-2">
          <button className="rounded-full p-2 text-[var(--admin-muted)] transition-all hover:text-rose-600"><Trash2 className="h-4 w-4" /></button>
        </div>
      )
    },
  ];

  const sectionColumns: AdminColumn<SectionRow>[] = [
    {
      key: 'title', label: 'القسم', render: (sec) => (
        <div className="flex items-center gap-3">
          <div className="rounded-lg bg-[var(--admin-card-strong)] p-2 text-[var(--admin-muted)] border border-[var(--admin-border)]"><Folder className="h-4 w-4" /></div>
          <div>
            <div className="font-bold text-[var(--admin-text)]">{sec.title}</div>
            <div className="text-xs text-[var(--admin-muted)] mt-1 font-mono">ترتيب: {sec.order}</div>
          </div>
        </div>
      ),
    },
    { key: 'location', label: 'المسار', render: (sec) => (
        <div>
          <div className="text-sm font-bold text-[var(--admin-text)]">{sec.termTitle}</div>
          <div className="text-xs text-[var(--admin-muted)] mt-1">{sec.packageName}</div>
        </div>
    )},
    { key: 'content', label: 'المحتويات', render: (sec) => <span className="text-[var(--admin-text)]">{sec.lessonCount} درس</span> },
    { key: 'actions', label: 'إجراءات', align: 'left', render: () => (
        <div className="flex items-center justify-end gap-2">
          <button className="rounded-full p-2 text-[var(--admin-muted)] transition-all hover:text-rose-600"><Trash2 className="h-4 w-4" /></button>
        </div>
      )
    },
  ];

  const lessonColumns: AdminColumn<LessonRow>[] = [
    {
      key: 'title', label: 'الدرس', render: (les) => (
        <div className="flex items-center gap-3">
          <div className="rounded-lg bg-[var(--admin-primary-15)] p-2 text-[var(--admin-primary)] border border-[var(--admin-border)]"><GraduationCap className="h-4 w-4" /></div>
          <div><div className="font-bold text-[var(--admin-text)]">{les.title}</div><div className="text-xs text-[var(--admin-muted)] mt-1 font-mono">ترتيب: {les.order}</div></div>
        </div>
      ),
    },
    { key: 'location', label: 'المسار', render: (les) => (
        <div>
          <div className="text-sm font-bold text-[var(--admin-text)]">{les.sectionTitle}</div>
          <div className="text-xs text-[var(--admin-muted)] mt-1 opacity-75">{les.termTitle} ▸ {les.packageName}</div>
        </div>
    )},
    { key: 'content', label: 'المحتويات', render: (les) => (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-card-strong)] px-3 py-1 text-xs font-bold text-[var(--admin-text)]">
          <Video className="h-3 w-3 text-[var(--admin-muted)]" /> {les.videoCount} فيديو
        </span>
    )},
    { key: 'actions', label: 'إجراءات', align: 'left', render: () => (
        <div className="flex items-center justify-end gap-2">
          <button className="rounded-full p-2 text-[var(--admin-muted)] transition-all hover:text-rose-600"><Trash2 className="h-4 w-4" /></button>
        </div>
    )},
  ];

  const videoColumns: AdminColumn<VideoRow>[] = [
    {
      key: 'title', label: 'الفيديو', render: (vid) => (
        <div className="flex items-center gap-3">
          <div className="rounded-lg bg-orange-100 p-2 text-orange-600 border border-orange-200"><Video className="h-4 w-4" /></div>
          <div><div className="font-bold text-[var(--admin-text)]">{vid.title}</div><div className="text-xs text-[var(--admin-muted)] mt-1 font-mono">مزود: <span className="uppercase">{vid.provider}</span></div></div>
        </div>
      ),
    },
    { key: 'location', label: 'المسار', render: (vid) => (
        <div>
          <div className="text-sm font-bold text-[var(--admin-text)]">{vid.lessonTitle}</div>
          <div className="text-xs text-[var(--admin-muted)] mt-1 opacity-75">{vid.sectionTitle} ▸ {vid.termTitle}</div>
        </div>
    )},
    { key: 'security', label: 'الحماية', render: (vid) => (
        <div className="font-semibold text-[var(--admin-text)]">حد المشاهدات: <span className="text-[var(--admin-primary)]">{vid.limit}</span></div>
    )},
    { key: 'actions', label: 'إجراءات', align: 'left', render: () => (
        <div className="flex items-center justify-end gap-2">
          <button className="rounded-full p-2 text-[var(--admin-muted)] transition-all hover:text-rose-600"><Trash2 className="h-4 w-4" /></button>
        </div>
    )},
  ];

  const currentData = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (activeTab === 'packages') return packages.filter((pkg) => !term || pkg.name.toLowerCase().includes(term));
    if (activeTab === 'terms') return terms.filter((t) => !term || t.title.toLowerCase().includes(term) || t.packageName.toLowerCase().includes(term));
    if (activeTab === 'sections') return sections.filter((s) => !term || s.title.toLowerCase().includes(term) || s.termTitle.toLowerCase().includes(term));
    if (activeTab === 'lessons') return lessons.filter((l) => !term || l.title.toLowerCase().includes(term) || l.sectionTitle.toLowerCase().includes(term));
    return videos.filter((v) => !term || v.title.toLowerCase().includes(term) || v.lessonTitle.toLowerCase().includes(term));
  }, [activeTab, lessons, packages, search, sections, terms, videos]);

  return (
    <AdminShellChrome
      activePath="/admin/content"
      sectionLabel="إدارة المحتوى"
      pageTitle="إدارة الباقات"
      subtitle="تنظيم وترتيب الدروس والفيديوهات للمقررات الأكاديمية"
      action={
        <button
          onClick={() => openModal('package')}
          className="inline-flex w-fit items-center gap-2 rounded-full bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] px-8 py-4 text-sm font-bold text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)] transition hover:brightness-110"
        >
          <Plus className="h-4 w-4" /> إضافة باقة جديدة
        </button>
      }
    >
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard variant="light" icon={Sparkles} label="إجمالي المبيعات" value={metrics.totalValue} subtitle="+١٢٪ هذا الشهر" />
        <AdminStatCard variant="accent" icon={Video} label="الدروس المرفوعة" value={metrics.totalLessons} subtitle={`${formatCompactNumber(videos.length)} فيديو مرتبط`} />
        <AdminStatCard variant="muted" icon={BookOpenText} label="الباقات النشطة" value={metrics.activePackages} />
      </section>

      <div className="mb-8 flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <AdminTabBar tabs={TAB_OPTIONS} activeTab={activeTab} onSelect={setActiveTab} />
        {activeTab !== 'packages' && (
          <button
            onClick={() => openModal(activeTab.slice(0, -1) as ModalType)}
            className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-6 py-2.5 text-sm font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-primary)] hover:text-white"
          >
            <Plus className="h-4 w-4" /> إضافة {TAB_OPTIONS.find((t) => t.key === activeTab)?.label.replace('ال', '').slice(0, -2)}
          </button>
        )}
      </div>

      <AdminSearchToolbar value={search} onChange={setSearch} placeholder={`ابحث في ${TAB_OPTIONS.find((t) => t.key === activeTab)?.label}...`} />

      {activeTab === 'packages' && <AdminDataTable data={currentData as PackageRow[]} columns={packageColumns} loading={loading} rowKey={(p) => p.id} />}
      {activeTab === 'terms' && <AdminDataTable data={currentData as TermRow[]} columns={termColumns} loading={loading} rowKey={(t) => t.id} />}
      {activeTab === 'sections' && <AdminDataTable data={currentData as SectionRow[]} columns={sectionColumns} loading={loading} rowKey={(s) => s.id} />}
      {activeTab === 'lessons' && <AdminDataTable data={currentData as LessonRow[]} columns={lessonColumns} loading={loading} rowKey={(l) => l.id} />}
      {activeTab === 'videos' && <AdminDataTable data={currentData as VideoRow[]} columns={videoColumns} loading={loading} rowKey={(v) => v.id} />}

      <AdminModal
        open={!!modalType}
        onClose={() => setModalType(null)}
        title={
          modalType === 'package' ? 'إضافة باقة' :
          modalType === 'term' ? 'إضافة ترم' :
          modalType === 'section' ? 'إضافة قسم' :
          modalType === 'lesson' ? 'إضافة درس' : 'إضافة فيديو'
        }
      >
        <form onSubmit={handleCreate} className="space-y-4">
          {modalType === 'package' && (
            <>
              <input type="text" value={packageName} onChange={(e) => setPackageName(e.target.value)} required placeholder="اسم الباقة" className="admin-input" />
              <textarea rows={3} value={packageDescription} onChange={(e) => setPackageDescription(e.target.value)} placeholder="وصف الباقة" className="admin-input" />
              <input type="number" min={0} value={packagePrice} onChange={(e) => setPackagePrice(Number(e.target.value))} required placeholder="السعر" className="admin-input" />
            </>
          )}

          {modalType === 'term' && (
            <>
              <select value={selectedPackageId} onChange={(e) => setSelectedPackageId(e.target.value)} required className="admin-input">
                <option value="">اختر الباقة</option>
                {packages.map((pkg) => <option key={pkg.id} value={pkg.id}>{pkg.name}</option>)}
              </select>
              <input type="text" value={entityTitle} onChange={(e) => setEntityTitle(e.target.value)} required placeholder="عنوان الترم (مثلا: الترم الأول)" className="admin-input" />
              <input type="number" min={1} value={entityOrder} onChange={(e) => setEntityOrder(Number(e.target.value))} required placeholder="الترتيب" className="admin-input" />
            </>
          )}

          {modalType === 'section' && (
            <>
              <select value={selectedTermId} onChange={(e) => setSelectedTermId(e.target.value)} required className="admin-input">
                <option value="">اختر الترم</option>
                {terms.map((t) => <option key={t.id} value={t.id}>{t.packageName} ▸ {t.title}</option>)}
              </select>
              <input type="text" value={entityTitle} onChange={(e) => setEntityTitle(e.target.value)} required placeholder="عنوان القسم" className="admin-input" />
              <input type="number" min={1} value={entityOrder} onChange={(e) => setEntityOrder(Number(e.target.value))} required placeholder="الترتيب" className="admin-input" />
            </>
          )}

          {modalType === 'lesson' && (
             <>
              <select value={selectedSectionId} onChange={(e) => setSelectedSectionId(e.target.value)} required className="admin-input">
                <option value="">اختر القسم</option>
                {sections.map((sec) => <option key={sec.id} value={sec.id}>{sec.termTitle} ▸ {sec.title}</option>)}
              </select>
              <input type="text" value={entityTitle} onChange={(e) => setEntityTitle(e.target.value)} required placeholder="عنوان الدرس" className="admin-input" />
              <textarea rows={2} value={entitySummary} onChange={(e) => setEntitySummary(e.target.value)} placeholder="الملخص" className="admin-input" />
              <input type="number" min={1} value={entityOrder} onChange={(e) => setEntityOrder(Number(e.target.value))} required placeholder="الترتيب" className="admin-input" />
             </>
          )}

          {modalType === 'video' && (
             <>
              <select value={selectedSectionId} onChange={(e) => setSelectedSectionId(e.target.value)} required className="admin-input">
                <option value="">اختر الدرس</option>
                {lessons.map((les) => <option key={les.id} value={les.id}>{les.sectionTitle} ▸ {les.title}</option>)}
              </select>
              <input type="text" value={entityTitle} onChange={(e) => setEntityTitle(e.target.value)} required placeholder="عنوان الفيديو" className="admin-input" />
              <select value={videoProvider} onChange={(e) => setVideoProvider(e.target.value as 'YouTube' | 'Vimeo' | 'Custom')} required className="admin-input">
                <option value="YouTube">YouTube</option>
                <option value="Vimeo">Vimeo</option>
                <option value="Custom">Custom</option>
              </select>
              <input type="text" value={videoCode} onChange={(e) => setVideoCode(e.target.value)} required placeholder="معرف الفيديو أو الكود المشفر" className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 font-mono text-left text-[var(--admin-text)] outline-none focus:ring-2 focus:ring-[var(--admin-primary)]" />
              <div className="grid grid-cols-2 gap-3">
                <input type="number" min={1} value={entityOrder} onChange={(e) => setEntityOrder(Number(e.target.value))} required placeholder="الترتيب" className="admin-input" />
                <input type="number" min={1} value={videoLimit} onChange={(e) => setVideoLimit(Number(e.target.value))} required placeholder="حد المشاهدة" className="admin-input" />
              </div>
             </>
          )}

          <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)] mt-4">
            <button type="button" onClick={() => setModalType(null)} className="px-4 py-2 font-semibold text-[var(--admin-muted)] transition hover:text-[var(--admin-text)]">إلغاء</button>
            <button type="submit" disabled={saving} className="admin-btn-primary">
              {saving ? 'جارٍ الحفظ...' : 'حفظ'}
            </button>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}
