'use client';

import { devConsole } from '@/utils/dev-console';
import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import { isAxiosError } from 'axios';
import { Eye, KeyRound, Plus, Sparkles, Layers, Search } from 'lucide-react';
import Link from 'next/link';

import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminStatCard,
  AdminModal,
} from '@/components/admin';
import { formatCompactNumber, formatDate } from '@/components/admin/admin-utils';
import { adminService, CodeGroupDto } from '@/services/admin-service';
import { PackageDto, contentService } from '@/services/content-service';
import { teacherService, SubjectDto, TeacherDto } from '@/services/teacher-service';
import { codeService } from '@/services/code-service';
import { CodeTypeSelector, CodeTypeSelection } from '@/components/codes/CodeTypeSelector';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function AdminCodesPageClient() {
  const [groups, setGroups] = useState<CodeGroupDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showGenModal, setShowGenModal] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>('All');
  const [selectedTeacherId, setSelectedTeacherId] = useState<string>('All');
  
  // Generation Form State
  const [genCount, setGenCount] = useState(10);
  const [genSelection, setGenSelection] = useState<CodeTypeSelection>({ codeType: 'Package' });
  const [genGroupName, setGenGroupName] = useState('');
  const [genLoading, setGenLoading] = useState(false);

  const [packages, setPackages] = useState<PackageDto[]>([]);
  const loadDataInFlightRef = useRef<Promise<void> | null>(null);

  useEffect(() => {
    void loadData();
  }, []);

  async function loadData(options?: { force?: boolean }) {
    if (loadDataInFlightRef.current && !options?.force) {
      return loadDataInFlightRef.current;
    }

    const request = (async () => {
      try {
        setLoading(true);
        const [groupsData, packagesResponse, subjectsRes, teachersRes] = await Promise.all([
          adminService.listCodeGroups({ force: options?.force }),
          contentService.getPackages({ force: options?.force }),
          teacherService.getSubjects().catch(() => ({ success: true, data: [] as SubjectDto[] })),
          teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] }))
        ]);
        setGroups(groupsData || []);
        setPackages((packagesResponse.data?.data || []) as PackageDto[]);
        setSubjects(subjectsRes.data ?? []);
        setTeachers(teachersRes.data ?? []);
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
    
    try {
      setGenLoading(true);
      
      await codeService.createCodeGroup({
        groupName: genGroupName,
        codeType: genSelection.codeType,
        count: genCount,
        codeLength: 12,
        packageId: genSelection.packageId || undefined,
        termId: genSelection.termId || undefined,
        contentSectionId: genSelection.contentSectionId || undefined,
        lessonId: genSelection.lessonId || undefined,
        examId: genSelection.examId || undefined,
        videoTargetIds: genSelection.videoTargetIds && genSelection.videoTargetIds.length > 0 ? genSelection.videoTargetIds : undefined,
        balanceAmount: genSelection.balanceAmount || undefined,
        discountPercentage: genSelection.discountPercentage || undefined,
        expiresAt: genSelection.expiresAt || undefined,
      });

      toast.success('تم التوليد بنجاح!');
      setShowGenModal(false);
      setGenSelection({ codeType: 'Package' });
      setGenGroupName('');
      setGenCount(10);
      await loadData({ force: true });
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
      (g.lessonId && g.lessonId.toLowerCase().includes(q))
    );
  }, [groups, searchQuery, packageNameMap, selectedSubjectId, selectedTeacherId, packages]);

  const totalCodes = groups.reduce((sum, group) => sum + group.codeCount, 0);
  const usedCodes = groups.reduce((sum, group) => sum + group.usedCount, 0);

  const groupColumns: AdminColumn<CodeGroupDto>[] = [
    {
      key: 'name',
      label: 'المجموعة',
      render: (g) => (
        <div>
          <div className="font-bold text-[var(--admin-text-strong)]">{g.name || 'دفعة بدون اسم'}</div>
          <div className="text-[10px] font-mono text-[var(--admin-muted)] mt-0.5">{g.id}</div>
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
          <div>{g.packageId ? 'Package' : g.lessonId ? 'Lesson' : 'عام'}</div>
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
        <NeumorphButton onClick={() => setShowGenModal(true)} intent="primary" size="lg" pill>
          <Plus className="h-4 w-4" />
          إنشاء دفعة جديدة
        </NeumorphButton>
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

          <div className="pt-4 border-t border-[var(--admin-border)]">
            <CodeTypeSelector
              value={genSelection}
              onChange={setGenSelection}
              packages={packages}
            />
          </div>

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
