'use client';

import React, { useEffect, useState, useCallback } from 'react';
import {
  FileText,
  BarChart3,
  Calendar,
  User,
  ShieldAlert,
  Search,
  RefreshCw,
  Eye,
  ArrowRightLeft,
  CheckCircle,
  AlertTriangle,
  XCircle,
  Activity,
  Coins,
  Film,
  Users,
  ChevronLeft,
  ChevronRight,
  TrendingUp,
  SlidersHorizontal,
} from 'lucide-react';
import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminModal,
  AdminStatCard,
} from '@/components/admin';
import { reportService, AuditLogDetailDto, KpiDashboardDto } from '@/services/report-service';
import { hrService, EmployeeDto } from '@/services/hr-service';
import toast from 'react-hot-toast';
import { motion } from 'framer-motion';

type TabType = 'audit' | 'kpi';

type ReportFilters = {
  startDate: string;
  endDate: string;
  employeeId: string;
  roleName: string;
  entityType: string;
};

const EMPTY_FILTERS: ReportFilters = {
  startDate: '',
  endDate: '',
  employeeId: '',
  roleName: '',
  entityType: '',
};

const getHttpStatus = (error: unknown) =>
  (error as { response?: { status?: number } } | null)?.response?.status;

export default function AdminReportsPageClient() {
  const [activeTab, setActiveTab] = useState<TabType>('audit');
  const [draftFilters, setDraftFilters] = useState<ReportFilters>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<ReportFilters>(EMPTY_FILTERS);
  
  // Audit Trail Ledger States
  const [auditLogs, setAuditLogs] = useState<AuditLogDetailDto[]>([]);
  const [auditLoading, setAuditLoading] = useState<boolean>(false);
  const [auditPage, setAuditPage] = useState<number>(1);
  const [auditPageSize] = useState<number>(20);
  const [auditTotalCount, setAuditTotalCount] = useState<number>(0);
  const [auditError, setAuditError] = useState<string | null>(null);
  
  // Selected Log for details modal
  const [selectedLog, setSelectedLog] = useState<AuditLogDetailDto | null>(null);

  // KPI Dashboard States
  const [kpiData, setKpiData] = useState<KpiDashboardDto | null>(null);
  const [kpiLoading, setKpiLoading] = useState<boolean>(false);
  const [kpiError, setKpiError] = useState<string | null>(null);

  // Employees list (for filters)
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);

  // Fetch employee list for filters
  const fetchFilterData = useCallback(async () => {
    try {
      const list = await hrService.listEmployees();
      setEmployees(list);
    } catch {
      toast.error('تعذر تحميل قائمة الموظفين للتصفية');
    }
  }, []);

  // Fetch Audit Logs List
  const fetchAuditLogs = useCallback(async () => {
    setAuditLoading(true);
    setAuditError(null);
    try {
      const paged = await reportService.getAuditLogs({
        startDate: appliedFilters.startDate || undefined,
        endDate: appliedFilters.endDate || undefined,
        performedByUserId: appliedFilters.employeeId || undefined,
        entityType: appliedFilters.entityType || undefined,
        page: auditPage,
        pageSize: auditPageSize,
      });
      setAuditLogs(paged.items);
      setAuditTotalCount(paged.totalCount);
    } catch (error: unknown) {
      setAuditError(
        getHttpStatus(error) === 403
          ? 'غير مصرح لك بعرض سجلات التدقيق والأمان.'
          : 'تعذر تحميل سجلات التدقيق. تحقق من الاتصال ثم أعد المحاولة.',
      );
    } finally {
      setAuditLoading(false);
    }
  }, [appliedFilters, auditPage, auditPageSize]);

  // Fetch KPI Dashboard Reports
  const fetchKpiDashboard = useCallback(async () => {
    setKpiLoading(true);
    setKpiError(null);
    try {
      const kpis = await reportService.getKpiDashboard({
        startDate: appliedFilters.startDate || undefined,
        endDate: appliedFilters.endDate || undefined,
        roleName: appliedFilters.roleName || undefined,
        employeeId: appliedFilters.employeeId || undefined,
      });
      setKpiData(kpis);
    } catch (error: unknown) {
      setKpiError(
        getHttpStatus(error) === 403
          ? 'غير مصرح لك بعرض لوحة مؤشرات الأداء.'
          : 'تعذر تحميل مؤشرات الأداء. تحقق من الاتصال ثم أعد المحاولة.',
      );
    } finally {
      setKpiLoading(false);
    }
  }, [appliedFilters]);

  // Initial loads
  useEffect(() => {
    fetchFilterData();
  }, [fetchFilterData]);

  useEffect(() => {
    if (activeTab === 'audit') {
      fetchAuditLogs();
    } else if (activeTab === 'kpi') {
      fetchKpiDashboard();
    }
  }, [activeTab, fetchAuditLogs, fetchKpiDashboard]);

  // Handle filter submission/resets
  const handleApplyFilters = () => {
    if (
      draftFilters.startDate
      && draftFilters.endDate
      && draftFilters.startDate > draftFilters.endDate
    ) {
      toast.error('يجب أن يكون تاريخ البدء قبل تاريخ الانتهاء.');
      return;
    }

    setAuditPage(1);
    setAppliedFilters({ ...draftFilters });
  };

  const handleClearFilters = () => {
    setDraftFilters({ ...EMPTY_FILTERS });
    setAppliedFilters({ ...EMPTY_FILTERS });
    setAuditPage(1);
    toast.success('تمت إعادة تعيين الفلاتر');
  };

  const updateDraftFilter = (key: keyof ReportFilters, value: string) => {
    setDraftFilters((current) => ({ ...current, [key]: value }));
  };

  const auditRangeStart = auditTotalCount === 0 ? 0 : (auditPage - 1) * auditPageSize + 1;
  const auditRangeEnd = Math.min((auditPage - 1) * auditPageSize + auditLogs.length, auditTotalCount);
  const auditTotalPages = Math.max(1, Math.ceil(auditTotalCount / auditPageSize));

  const formatDate = (isoString: string) => {
    return new Date(isoString).toLocaleString('ar-EG', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  };

  // Render pretty JSON payload differences
  const renderPayloadDiff = (payload: string | undefined) => {
    if (!payload) return <span className="text-[var(--admin-muted)] text-xs font-mono">لا يوجد بيانات</span>;
    try {
      // If it looks like JSON
      if (payload.trim().startsWith('{') || payload.trim().startsWith('[')) {
        const parsed = JSON.parse(payload);
        return (
          <pre className="bg-[var(--admin-bg)] p-3 rounded-lg text-xs font-mono overflow-auto max-h-[300px] border border-[var(--admin-border)] text-left dir-ltr">
            {JSON.stringify(parsed, null, 2)}
          </pre>
        );
      }
    } catch {
      // Non-JSON payloads are rendered as plain text below.
    }

    // Fallback to plain text
    return (
      <div className="bg-[var(--admin-bg)] p-3 rounded-lg text-xs font-mono overflow-auto max-h-[300px] border border-[var(--admin-border)] text-left dir-ltr whitespace-pre-wrap">
        {payload}
      </div>
    );
  };

  // Audit columns definitions
  const auditColumns: AdminColumn<AuditLogDetailDto>[] = [
    {
      key: 'createdAt',
      label: 'تاريخ الحدث',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-muted)]">{formatDate(item.createdAt)}</span>,
    },
    {
      key: 'action',
      label: 'نوع العملية',
      render: (item) => (
        <span className="font-bold px-2.5 py-1 text-xs rounded-full bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]">
          {item.action}
        </span>
      ),
    },
    {
      key: 'entityType',
      label: 'الكيان المتأثر',
      render: (item) => (
        <span className="font-mono text-xs font-semibold text-[var(--admin-text)]">
          {item.entityType} ({item.entityId ? item.entityId.substring(0, 8) + '...' : '-'})
        </span>
      ),
    },
    {
      key: 'performedByUserName',
      label: 'بواسطة',
      render: (item) => (
        <div>
          <div className="font-bold text-xs text-[var(--admin-text)]">{item.performedByUserName || 'نظام تلقائي'}</div>
          {item.performedByUserPhone && (
            <div className="text-[var(--admin-muted)] text-[10px] font-mono mt-0.5">{item.performedByUserPhone}</div>
          )}
        </div>
      ),
    },
    {
      key: 'ipAddress',
      label: 'عنوان IP',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-muted)]">{item.ipAddress || 'غير متوفر'}</span>,
    },
    {
      key: 'actions',
      label: 'التفاصيل',
      render: (item) => (
        <button
          type="button"
          onClick={() => setSelectedLog(item)}
          className="p-1.5 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)] hover:text-[var(--admin-primary)] transition"
          title="عرض قيم التغييرات"
          aria-label={`عرض تفاصيل عملية ${item.action}`}
        >
          <Eye className="h-4 w-4" />
        </button>
      ),
    },
  ];

  return (
    <AdminShellChrome
      activePath="/admin/reports"
      sectionLabel="سجل الأمان والتقارير"
      pageTitle="سجل التدقيق ولوحة التحليلات المركزية"
      subtitle="تتبع حركات النظام الحساسة للمدراء، وراقب إحصائيات ومؤشرات الأداء للإنتاج والمبيعات والمتابعة والمالية."
      action={
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => {
              if (activeTab === 'audit') fetchAuditLogs();
              else fetchKpiDashboard();
            }}
            disabled={auditLoading || kpiLoading}
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] flex items-center gap-1.5 transition"
          >
            <RefreshCw className={`h-4 w-4 ${auditLoading || kpiLoading ? 'animate-spin' : ''}`} />
            تحديث البيانات
          </button>
        </div>
      }
    >
      {/* Search & Filters Cockpit */}
      <section className="mb-8 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 shadow-[0_4px_24px_var(--admin-shadow)] backdrop-blur-xl">
        <div className="mb-4 flex items-center gap-2 text-xs font-black uppercase tracking-wider text-[var(--admin-muted)]">
          <SlidersHorizontal className="h-4 w-4 text-[var(--admin-primary)]" />
          تصفية وتقييد نتائج التقارير
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-5">
          {/* Start Date */}
          <div>
            <label htmlFor="report-start-date" className="block text-xs font-bold text-[var(--admin-muted)] mb-1">تاريخ البدء</label>
            <div className="relative">
              <Calendar className="absolute right-3 top-2.5 h-4 w-4 text-[var(--admin-muted)]" />
              <input
                id="report-start-date"
                type="date"
                value={draftFilters.startDate}
                onChange={(e) => updateDraftFilter('startDate', e.target.value)}
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-2 pl-3 pr-9 text-xs text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
              />
            </div>
          </div>

          {/* End Date */}
          <div>
            <label htmlFor="report-end-date" className="block text-xs font-bold text-[var(--admin-muted)] mb-1">تاريخ الانتهاء</label>
            <div className="relative">
              <Calendar className="absolute right-3 top-2.5 h-4 w-4 text-[var(--admin-muted)]" />
              <input
                id="report-end-date"
                type="date"
                value={draftFilters.endDate}
                onChange={(e) => updateDraftFilter('endDate', e.target.value)}
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-2 pl-3 pr-9 text-xs text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
              />
            </div>
          </div>

          {/* Employee Dropdown */}
          <div>
            <label htmlFor="report-employee" className="block text-xs font-bold text-[var(--admin-muted)] mb-1">الموظف / المسؤول</label>
            <div className="relative">
              <User className="absolute right-3 top-2.5 h-4 w-4 text-[var(--admin-muted)]" />
              <select
                id="report-employee"
                value={draftFilters.employeeId}
                onChange={(e) => updateDraftFilter('employeeId', e.target.value)}
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-2 pl-3 pr-9 text-xs text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] appearance-none"
              >
                <option value="">كل الموظفين والعملاء</option>
                {employees.map((emp) => (
                  <option key={emp.employeeProfile?.id || emp.id} value={emp.employeeProfile?.id || emp.id}>
                    {emp.fullName} ({emp.roles.join(', ')})
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Tab Specific Filters */}
          {activeTab === 'audit' ? (
            /* Entity Type for Audit Trail */
            <div>
              <label htmlFor="report-entity-type" className="block text-xs font-bold text-[var(--admin-muted)] mb-1">نوع الكيان</label>
              <div className="relative">
                <Search className="absolute right-3 top-2.5 h-4 w-4 text-[var(--admin-muted)]" />
                <select
                  id="report-entity-type"
                  value={draftFilters.entityType}
                  onChange={(e) => updateDraftFilter('entityType', e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-2 pl-3 pr-9 text-xs text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] appearance-none"
                >
                  <option value="">كل الكيانات</option>
                  <option value="TaskItem">مهام العمليات (TaskItem)</option>
                  <option value="CrmCallLog">مكالمات المبيعات (CrmCallLog)</option>
                  <option value="CrmStudentStatus">حالة طلاب CRM (CrmStudentStatus)</option>
                  <option value="MediaProductionPipeline">خطوط إنتاج الميديا (MediaProductionPipeline)</option>
                  <option value="SocialMediaPlan">خطط التواصل الاجتماعي (SocialMediaPlan)</option>
                  <option value="AccessCode">أكواد الشحن والتفعيل (AccessCode)</option>
                  <option value="PayrollRecord">سجلات الرواتب (PayrollRecord)</option>
                  <option value="AttendanceLog">سجلات الحضور (AttendanceLog)</option>
                </select>
              </div>
            </div>
          ) : (
            /* Role Name for KPI Dashboard */
            <div>
              <label htmlFor="report-role" className="block text-xs font-bold text-[var(--admin-muted)] mb-1">دور العمل</label>
              <div className="relative">
                <SlidersHorizontal className="absolute right-3 top-2.5 h-4 w-4 text-[var(--admin-muted)]" />
                <select
                  id="report-role"
                  value={draftFilters.roleName}
                  onChange={(e) => updateDraftFilter('roleName', e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-2 pl-3 pr-9 text-xs text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] appearance-none"
                >
                  <option value="">كل الأدوار</option>
                  <option value="Admin">مدير (Admin)</option>
                  <option value="Supervisor">مشرف (Supervisor)</option>
                  <option value="Assistant">مساعد (Assistant)</option>
                  <option value="Teacher">مدرس (Teacher)</option>
                  <option value="Staff">موظف (Staff)</option>
                </select>
              </div>
            </div>
          )}

          {/* Action buttons */}
          <div className="flex items-end gap-2">
            <button
              type="button"
              onClick={handleApplyFilters}
              disabled={auditLoading || kpiLoading}
              className="flex-1 rounded-xl bg-[var(--admin-primary)] py-2.5 text-xs font-black text-[var(--admin-primary-contrast)] hover:opacity-90 shadow-md transition"
            >
              تطبيق التصفية
            </button>
            <button
              type="button"
              onClick={handleClearFilters}
              disabled={auditLoading || kpiLoading}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-xs font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] transition"
              title="إعادة تعيين"
            >
              مسح
            </button>
          </div>
        </div>
      </section>

      {/* Tabs Switcher */}
      <div className="mb-8 flex justify-center">
        <div className="inline-flex gap-1 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-1.5 shadow-sm backdrop-blur-xl">
          <button
            type="button"
            onClick={() => setActiveTab('audit')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'audit'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-transparent text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <Activity className="h-4 w-4" />
            سجل تدقيق الأمان (Audit Trail)
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('kpi')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'kpi'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-transparent text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <BarChart3 className="h-4 w-4" />
            لوحة مؤشرات الأداء (KPI Cockpit)
          </button>
        </div>
      </div>

      {/* Tab: Central Audit Trail */}
      {activeTab === 'audit' && (
        <motion.div
          initial={{ opacity: 0, y: 15 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3 }}
        >
          <div className="mb-4 flex items-center justify-between">
            <h4 className="text-sm font-black text-[var(--admin-text)] flex items-center gap-2">
              <FileText className="h-4 w-4 text-[var(--admin-primary)]" />
              تتبع العمليات الأمنية وتغيير الحالات
            </h4>
            <span className="text-xs font-semibold text-[var(--admin-muted)]">
              إجمالي السجلات المطابقة: {auditError ? '—' : auditTotalCount} حركة
            </span>
          </div>

          <AdminDataTable
            data={auditLogs}
            columns={auditColumns}
            loading={auditLoading}
            pagination={false}
            rowKey={(item) => item.id}
            emptyMessage="لا توجد سجلات تدقيق تطابق شروط التصفية الحالية."
            errorMessage={auditError}
            onRetry={() => void fetchAuditLogs()}
          />

          {/* Pagination */}
          {!auditError && auditTotalCount > auditPageSize && (
            <div className="mt-6 flex items-center justify-between border-t border-[var(--admin-border)] pt-4">
              <span className="text-xs font-semibold text-[var(--admin-muted)]">
                عرض {auditRangeStart}-{auditRangeEnd} من أصل {auditTotalCount} حركة مسجلة
              </span>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  disabled={auditPage === 1 || auditLoading}
                  onClick={() => setAuditPage((prev) => prev - 1)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-40"
                  aria-label="الصفحة السابقة من سجلات التدقيق"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
                <span className="font-mono text-sm font-bold px-3">
                  صفحة {auditPage} من {auditTotalPages}
                </span>
                <button
                  type="button"
                  disabled={auditPage * auditPageSize >= auditTotalCount || auditLoading}
                  onClick={() => setAuditPage((prev) => prev + 1)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-40"
                  aria-label="الصفحة التالية من سجلات التدقيق"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
              </div>
            </div>
          )}
        </motion.div>
      )}

      {/* Tab: KPI Cockpit */}
      {activeTab === 'kpi' && (
        <motion.div
          initial={{ opacity: 0, y: 15 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3 }}
          className="space-y-8"
        >
          {kpiLoading ? (
            <div className="py-20 flex flex-col items-center justify-center gap-3">
              <RefreshCw className="h-8 w-8 text-[var(--admin-primary)] animate-spin" />
              <span className="text-sm font-bold text-[var(--admin-muted)]">جاري تجميع وحساب مؤشرات الأداء والتحليلات...</span>
            </div>
          ) : kpiError ? (
            <div role="alert" className="rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-6 py-10 text-center">
              <p className="text-sm font-bold text-[var(--admin-danger)]">{kpiError}</p>
              <button
                type="button"
                onClick={() => void fetchKpiDashboard()}
                className="mt-4 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-5 py-2 text-xs font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
              >
                إعادة المحاولة
              </button>
            </div>
          ) : kpiData ? (
            <>
              {/* Stat Cards Row */}
              <section className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-4">
                <AdminStatCard
                  variant="light"
                  icon={Users}
                  label="معدل الحضور والانضباط"
                  value={`${kpiData.attendance.presentRate}%`}
                  subtitle={`من أصل ${kpiData.attendance.totalLogs} سجل حضور`}
                />
                <AdminStatCard
                  variant="accent"
                  icon={TrendingUp}
                  label="معدل إنجاز المهام العملياتية"
                  value={`${kpiData.tasks.completionRate}%`}
                  subtitle={`إجمالي المهام: ${kpiData.tasks.totalTasks}`}
                />
                <AdminStatCard
                  variant="accent"
                  icon={Coins}
                  label="معدل مطابقة ودفع المحفظة"
                  value={`${kpiData.payments.autoMatchRate}%`}
                  subtitle={`من أصل ${kpiData.payments.totalTransactions} حركة دفع`}
                />
                <AdminStatCard
                  variant="light"
                  icon={Film}
                  label="متوسط إنتاج الفيديو والمحتوى"
                  value={`${kpiData.media.averageProductionDays} يوم`}
                  subtitle={`المنشور: ${kpiData.media.publishedCount} فيديوهات`}
                />
              </section>

              {/* KPI Chart Layout Grids */}
              <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                
                {/* 1. Attendance Chart */}
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
                  <h5 className="font-black text-sm text-[var(--admin-text)] mb-6 flex items-center gap-2">
                    <Activity className="h-4 w-4 text-emerald-500" />
                    توزيع حالات الانضباط والحضور للموظفين
                  </h5>
                  <div className="space-y-4">
                    {/* Status progress split */}
                    <div>
                      <div className="flex justify-between text-xs font-bold mb-1.5">
                        <span className="text-emerald-600 dark:text-emerald-400">حاضر في الموعد (Present)</span>
                        <span className="font-mono">{kpiData.attendance.presentCount} ({kpiData.attendance.presentRate}%)</span>
                      </div>
                      <div className="w-full bg-[var(--admin-card-soft)] h-3 rounded-full overflow-hidden">
                        <div
                          className="bg-emerald-500 h-full rounded-full transition-all duration-500"
                          style={{ width: `${kpiData.attendance.presentRate}%` }}
                        />
                      </div>
                    </div>

                    <div>
                      <div className="flex justify-between text-xs font-bold mb-1.5">
                        <span className="text-amber-600 dark:text-amber-400">متأخر (Late)</span>
                        <span className="font-mono">{kpiData.attendance.lateCount} ({kpiData.attendance.lateRate}%)</span>
                      </div>
                      <div className="w-full bg-[var(--admin-card-soft)] h-3 rounded-full overflow-hidden">
                        <div
                          className="bg-amber-500 h-full rounded-full transition-all duration-500"
                          style={{ width: `${kpiData.attendance.lateRate}%` }}
                        />
                      </div>
                    </div>

                    <div>
                      <div className="flex justify-between text-xs font-bold mb-1.5">
                        <span className="text-rose-600 dark:text-rose-400">غائب (Absent)</span>
                        <span className="font-mono">{kpiData.attendance.absentCount} ({kpiData.attendance.absentRate}%)</span>
                      </div>
                      <div className="w-full bg-[var(--admin-card-soft)] h-3 rounded-full overflow-hidden">
                        <div
                          className="bg-rose-500 h-full rounded-full transition-all duration-500"
                          style={{ width: `${kpiData.attendance.absentRate}%` }}
                        />
                      </div>
                    </div>
                  </div>
                </div>

                {/* 2. Tasks Ratio Chart */}
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
                  <h5 className="font-black text-sm text-[var(--admin-text)] mb-6 flex items-center gap-2">
                    <TrendingUp className="h-4 w-4 text-[var(--admin-primary)]" />
                    نسب وحالات مهام العمليات
                  </h5>
                  <div className="flex flex-col sm:flex-row items-center justify-around gap-6 h-full pb-4">
                    {/* SVG Progress Ring */}
                    <div className="relative flex items-center justify-center">
                      <svg className="w-32 h-32 transform -rotate-90">
                        <circle cx="64" cy="64" r="50" stroke="var(--admin-border)" strokeWidth="8" fill="transparent" />
                        <circle
                          cx="64"
                          cy="64"
                          r="50"
                          stroke="var(--admin-primary)"
                          strokeWidth="8"
                          fill="transparent"
                          strokeDasharray={2 * Math.PI * 50}
                          strokeDashoffset={2 * Math.PI * 50 * (1 - kpiData.tasks.completionRate / 100)}
                          className="transition-all duration-1000"
                          strokeLinecap="round"
                        />
                      </svg>
                      <div className="absolute text-center">
                        <span className="text-xl font-black text-[var(--admin-text)] block">{kpiData.tasks.completionRate}%</span>
                        <span className="text-[10px] font-bold text-[var(--admin-muted)]">نسبة الإنجاز</span>
                      </div>
                    </div>

                    <div className="space-y-3 flex-1 max-w-[200px]">
                      <div className="flex justify-between items-center rounded-xl bg-[var(--admin-card-soft)] p-2">
                        <span className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-1">
                          <CheckCircle className="h-3.5 w-3.5 text-emerald-500" />
                          المكتملة
                        </span>
                        <span className="font-mono text-xs font-bold text-[var(--admin-text)]">{kpiData.tasks.completedCount}</span>
                      </div>

                      <div className="flex justify-between items-center rounded-xl bg-[var(--admin-card-soft)] p-2">
                        <span className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-1">
                          <AlertTriangle className="h-3.5 w-3.5 text-amber-500" />
                          قيد التنفيذ
                        </span>
                        <span className="font-mono text-xs font-bold text-[var(--admin-text)]">{kpiData.tasks.pendingCount}</span>
                      </div>

                      <div className="flex justify-between items-center rounded-xl bg-[var(--admin-card-soft)] p-2">
                        <span className="text-xs font-bold text-[var(--admin-text)] flex items-center gap-1">
                          <XCircle className="h-3.5 w-3.5 text-rose-500" />
                          المتأخرة (Overdue)
                        </span>
                        <span className="font-mono text-xs font-bold text-[var(--admin-text)]">{kpiData.tasks.overdueCount}</span>
                      </div>
                    </div>
                  </div>
                </div>

                {/* 3. CRM Outcomes Distribution */}
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
                  <h5 className="font-black text-sm text-[var(--admin-text)] mb-6 flex items-center gap-2">
                    <Search className="h-4 w-4 text-[var(--admin-primary)]" />
                    مخرجات مكالمات خدمة العملاء (CRM Outcomes)
                  </h5>
                  <div className="space-y-4">
                    {kpiData.crmOutcomes.map((outcome) => {
                      const maxCount = Math.max(...kpiData.crmOutcomes.map(o => o.count), 1);
                      const percent = Math.round((outcome.count / maxCount) * 100);
                      
                      // Map outcomes to clean Arabic labels
                      const outcomeLabels: { [key: string]: string } = {
                        Completed: 'مكالمة ناجحة / مكتملة',
                        Pending: 'قيد المتابعة والانتظار',
                        NoAnswer: 'لم يرد على المكالمة',
                        Postponed: 'مؤجلة لموعد آخر',
                        Closed: 'مغلقة ومستبعدة',
                      };

                      return (
                        <div key={outcome.outcome}>
                          <div className="flex justify-between text-xs font-bold mb-1">
                            <span className="text-[var(--admin-text)]">{outcomeLabels[outcome.outcome] || outcome.outcome}</span>
                            <span className="font-mono text-[var(--admin-muted)]">{outcome.count} مكالمة</span>
                          </div>
                          <div className="w-full bg-[var(--admin-card-soft)] h-2 rounded-full overflow-hidden">
                            <div
                              className="bg-[var(--admin-primary)] h-full rounded-full transition-all duration-500"
                              style={{ width: `${percent}%` }}
                            />
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* 4. Payment Matching vs Coupon Activations */}
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
                  <h5 className="font-black text-sm text-[var(--admin-text)] mb-6 flex items-center gap-2">
                    <ArrowRightLeft className="h-4 w-4 text-emerald-500" />
                    توزيع عمليات شراء المحتوى وأكواد التفعيل
                  </h5>
                  <div className="flex flex-col items-center justify-center h-full pb-4">
                    <div className="w-full max-w-[320px] space-y-4">
                      {/* Split bar */}
                      <div className="h-6 w-full rounded-full overflow-hidden flex bg-[var(--admin-card-soft)] border border-[var(--admin-border)]">
                        <div
                          className="bg-emerald-500 h-full transition-all duration-500"
                          style={{ width: `${kpiData.payments.autoMatchRate}%` }}
                          title="شراء مباشر من المحفظة"
                        />
                        <div
                          className="bg-amber-500 h-full transition-all duration-500"
                          style={{ width: `${100 - kpiData.payments.autoMatchRate}%` }}
                          title="شحن وتفعيل أكواد كوبونات"
                        />
                      </div>

                      {/* Legend and stats */}
                      <div className="grid grid-cols-2 gap-4 pt-2">
                        <div className="text-center p-3 rounded-2xl bg-emerald-50 dark:bg-emerald-950/20 border border-emerald-100 dark:border-emerald-900/30">
                          <span className="block text-xs font-bold text-emerald-700 dark:text-emerald-400">شراء بالمحفظة (Auto)</span>
                          <span className="font-mono text-base font-black text-emerald-700 dark:text-emerald-400 mt-1 block">
                            {kpiData.payments.autoMatchedCount}
                          </span>
                          <span className="text-[10px] text-emerald-600 dark:text-emerald-500 font-bold">({kpiData.payments.autoMatchRate}%)</span>
                        </div>

                        <div className="text-center p-3 rounded-2xl bg-amber-50 dark:bg-amber-950/20 border border-amber-100 dark:border-amber-900/30">
                          <span className="block text-xs font-bold text-amber-700 dark:text-amber-400">تفعيل أكواد (Coupons)</span>
                          <span className="font-mono text-base font-black text-amber-700 dark:text-amber-400 mt-1 block">
                            {kpiData.payments.couponActivatedCount}
                          </span>
                          <span className="text-[10px] text-amber-600 dark:text-amber-500 font-bold">({Math.round(100 - kpiData.payments.autoMatchRate)}%)</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                {/* 5. Payroll Approvals Distribution */}
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm lg:col-span-2">
                  <h5 className="font-black text-sm text-[var(--admin-text)] mb-6 flex items-center gap-2">
                    <ShieldAlert className="h-4 w-4 text-[var(--admin-primary)]" />
                    حالة اعتماد وموافقة كشوفات رواتب الموظفين
                  </h5>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-6 items-center">
                    <div className="space-y-4">
                      {kpiData.payrollStatus.map((status) => {
                        const totalPayroll = kpiData.payrollStatus.reduce((acc, s) => acc + s.count, 0) || 1;
                        const percent = Math.round((status.count / totalPayroll) * 100);

                        // Colors for status
                        const statusColors: { [key: string]: string } = {
                          Approved: 'bg-emerald-500',
                          Draft: 'bg-amber-500',
                        };

                        const statusLabels: { [key: string]: string } = {
                          Approved: 'مسيرات رواتب معتمدة ومصروفة',
                          Draft: 'مسيرات رواتب مسودة / قيد المراجعة',
                        };

                        return (
                          <div key={status.status}>
                            <div className="flex justify-between text-xs font-bold mb-1">
                              <span className="text-[var(--admin-text)]">{statusLabels[status.status] || status.status}</span>
                              <span className="font-mono text-[var(--admin-muted)]">{status.count} موظف ({percent}%)</span>
                            </div>
                            <div className="w-full bg-[var(--admin-card-soft)] h-3 rounded-full overflow-hidden">
                              <div
                                className={`${statusColors[status.status] || 'bg-[var(--admin-primary)]'} h-full rounded-full transition-all duration-500`}
                                style={{ width: `${percent}%` }}
                              />
                            </div>
                          </div>
                        );
                      })}
                    </div>

                    <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 text-center">
                      <span className="block text-xs font-bold text-[var(--admin-muted)] mb-1">إجمالي رواتب الكشوفات المطروحة</span>
                      <span className="font-mono text-2xl font-black text-[var(--admin-text)]">
                        {kpiData.payrollStatus.reduce((acc, s) => acc + s.count, 0)} سجلات
                      </span>
                      <p className="text-[10px] text-[var(--admin-muted)] mt-2 leading-relaxed">
                        يتم تحديث نسب الاعتمادات والمسودات بمجرد اعتماد المشرفين للرواتب وإقفال الحركات المالية للشهور الجارية.
                      </p>
                    </div>
                  </div>
                </div>

              </div>
            </>
          ) : (
            <div className="py-20 text-center text-sm font-bold text-[var(--admin-muted)]">لا توجد بيانات متاحة لعرضها.</div>
          )}
        </motion.div>
      )}

      {/* Modal: Audit Log Payload Details Viewer */}
      <AdminModal
        open={!!selectedLog}
        onClose={() => setSelectedLog(null)}
        title="تفاصيل العملية والتعديلات المطروحة"
        subtitle={`مراجعة التغييرات للحدث المجرى بواسطة: ${selectedLog?.performedByUserName || 'نظام المنصة التلقائي'}`}
      >
        {selectedLog && (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <span className="block text-xs font-bold text-[var(--admin-muted)] mb-1">نوع العملية (Action)</span>
                <span className="font-mono text-xs font-bold text-[var(--admin-text)] bg-[var(--admin-card-soft)] px-2 py-1 rounded">
                  {selectedLog.action}
                </span>
              </div>
              <div>
                <span className="block text-xs font-bold text-[var(--admin-muted)] mb-1">الكيان المتأثر (Entity)</span>
                <span className="font-mono text-xs font-bold text-[var(--admin-text)] bg-[var(--admin-card-soft)] px-2 py-1 rounded">
                  {selectedLog.entityType}
                </span>
              </div>
            </div>

            <div className="border-t border-[var(--admin-border)] pt-4 space-y-4">
              <div>
                <span className="block text-xs font-bold text-rose-500 mb-1.5 flex items-center gap-1">
                  <ChevronRight className="h-3 w-3" />
                  القيم القديمة السابقة (Old Values)
                </span>
                {renderPayloadDiff(selectedLog.oldValues)}
              </div>

              <div>
                <span className="block text-xs font-bold text-emerald-500 mb-1.5 flex items-center gap-1">
                  <ChevronLeft className="h-3 w-3" />
                  القيم الجديدة المحدثة (New Values)
                </span>
                {renderPayloadDiff(selectedLog.newValues)}
              </div>
            </div>

            <div className="flex justify-end border-t border-[var(--admin-border)] pt-4">
              <button
                type="button"
                onClick={() => setSelectedLog(null)}
                className="rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-sm font-black text-[var(--admin-primary-contrast)]"
              >
                إغلاق النافذة
              </button>
            </div>
          </div>
        )}
      </AdminModal>
    </AdminShellChrome>
  );
}
