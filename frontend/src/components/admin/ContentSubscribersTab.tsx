'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Download, Search, Users, ChevronLeft, ChevronRight } from 'lucide-react';
import { AdminDataTable, type AdminColumn } from './AdminDataTable';
import { adminService, type ContentSubscriberDto } from '@/services/admin-service';
import toast from 'react-hot-toast';

interface ContentSubscribersTabProps {
  contentType: 'package' | 'term' | 'section';
  contentId: string;
  contentName: string;
}

const PAGE_SIZE = 10;

const EDUCATION_STAGE_MAP: Record<string, string> = {
  Primary: 'ابتدائي',
  Preparatory: 'إعدادي',
  Secondary: 'ثانوي',
};

const GRADE_LEVEL_MAP: Record<string, string> = {
  FirstPrimary: 'أولى ابتدائي',
  SecondPrimary: 'ثانية ابتدائي',
  ThirdPrimary: 'ثالثة ابتدائي',
  FourthPrimary: 'رابعة ابتدائي',
  FifthPrimary: 'خامسة ابتدائي',
  SixthPrimary: 'سادسة ابتدائي',
  FirstPreparatory: 'أولى إعدادي',
  SecondPreparatory: 'ثانية إعدادي',
  ThirdPreparatory: 'ثالثة إعدادي',
  FirstSecondary: 'أولى ثانوي',
  SecondSecondary: 'ثانية ثانوي',
  ThirdSecondary: 'ثالثة ثانوي',
};

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('ar-EG', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  } catch {
    return iso;
  }
}

export default function ContentSubscribersTab({
  contentType,
  contentId,
  contentName,
}: ContentSubscribersTabProps) {
  const router = useRouter();
  const [subscribers, setSubscribers] = useState<ContentSubscriberDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [search, setSearch] = useState('');
  const [exporting, setExporting] = useState(false);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const fetchSubscribers = useCallback(async (p: number, s: string) => {
    try {
      setLoading(true);
      setError(null);
      const result = await adminService.getContentSubscribers(contentType, contentId, p, PAGE_SIZE, s);
      if (result) {
        setSubscribers(result.items ?? []);
        setTotalCount(result.totalCount ?? 0);
      }
    } catch {
      setError('تعذر تحميل بيانات المشتركين');
    } finally {
      setLoading(false);
    }
  }, [contentType, contentId]);

  useEffect(() => {
    void fetchSubscribers(page, search);
  }, [fetchSubscribers, page, search]);

  // Cleanup debounce on unmount
  useEffect(() => {
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, []);

  const handleSearchChange = (value: string) => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setSearch(value);
      setPage(1);
    }, 300);
  };

  const handleExport = async () => {
    try {
      setExporting(true);
      await adminService.exportContentSubscribersCsv(contentType, contentId, contentName);
      toast.success('تم تنزيل ملف المشتركين');
    } catch {
      toast.error('تعذر تنزيل الملف');
    } finally {
      setExporting(false);
    }
  };

  const columns: AdminColumn<ContentSubscriberDto>[] = [
    {
      key: 'name',
      label: 'الاسم',
      render: (row) => (
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--admin-primary-15)] text-xs font-black text-[var(--admin-primary)]">
            {row.fullName?.charAt(0) ?? '?'}
          </div>
          <span className="max-w-[180px] truncate font-bold text-[var(--admin-text)]">{row.fullName}</span>
        </div>
      ),
    },
    {
      key: 'phone',
      label: 'الهاتف',
      render: (row) => (
        <span className="font-mono text-xs text-[var(--admin-muted)]" dir="ltr">{row.phone}</span>
      ),
    },
    {
      key: 'governorate',
      label: 'المحافظة',
      render: (row) => <span>{row.governorate || '—'}</span>,
    },
    {
      key: 'stage',
      label: 'المرحلة',
      render: (row) => <span>{EDUCATION_STAGE_MAP[row.educationStage] ?? row.educationStage ?? '—'}</span>,
    },
    {
      key: 'grade',
      label: 'الصف',
      render: (row) => <span>{GRADE_LEVEL_MAP[row.gradeLevel] ?? row.gradeLevel ?? '—'}</span>,
    },
    {
      key: 'enrolledAt',
      label: 'تاريخ الاشتراك',
      render: (row) => <span className="text-xs">{formatDate(row.enrolledAt)}</span>,
    },
    {
      key: 'status',
      label: 'الحالة',
      align: 'center',
      render: (row) => (
        <span
          className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${
            row.isActive
              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400'
              : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
          }`}
        >
          {row.isActive ? 'نشط' : 'ملغى'}
        </span>
      ),
    },
  ];

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="space-y-4">
      {/* Header with search and export */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <Users className="h-5 w-5 text-[var(--admin-primary)]" />
          <h3 className="text-lg font-black text-[var(--admin-text)]">
            الطلاب المشتركين
            {!loading && (
              <span className="mr-2 text-sm font-bold text-[var(--admin-muted)]">
                ({totalCount})
              </span>
            )}
          </h3>
        </div>
        <div className="flex items-center gap-3">
          {/* Search */}
          <div className="relative">
            <Search className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
            <input
              type="text"
              placeholder="بحث بالاسم أو رقم الهاتف..."
              onChange={(e) => handleSearchChange(e.target.value)}
              className="h-10 w-64 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] pr-10 pl-4 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
            />
          </div>
          {/* Export */}
          <button
            type="button"
            onClick={handleExport}
            disabled={exporting || totalCount === 0}
            className="inline-flex h-10 items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 text-sm font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-hover)] disabled:opacity-50"
          >
            <Download className="h-4 w-4" />
            {exporting ? 'جارٍ التنزيل...' : 'تنزيل CSV'}
          </button>
        </div>
      </div>

      {/* Table */}
      <AdminDataTable<ContentSubscriberDto>
        data={subscribers}
        columns={columns}
        loading={loading}
        rowKey={(item) => item.studentId}
        emptyMessage="لا يوجد طلاب مشتركين حالياً"
        errorMessage={error}
        onRetry={() => fetchSubscribers(page, search)}
        pagination={false}
        onRowClick={(row) => router.push(`/admin/users/${row.studentId}`)}
      />

      {/* Server-side pagination */}
      {totalCount > PAGE_SIZE && (
        <div className="flex items-center justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
          <span className="text-xs font-bold tracking-wide text-[var(--admin-muted)]">
            صفحة {page} من {totalPages} — إجمالي {totalCount} طالب
          </span>
          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              className="rounded-full p-2 text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] disabled:opacity-40"
              aria-label="الصفحة السابقة"
            >
              <ChevronRight className="h-5 w-5" />
            </button>
            <span className="px-3 text-sm font-bold text-[var(--admin-primary)]">{page}</span>
            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              className="rounded-full p-2 text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] disabled:opacity-40"
              aria-label="الصفحة التالية"
            >
              <ChevronLeft className="h-5 w-5" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
