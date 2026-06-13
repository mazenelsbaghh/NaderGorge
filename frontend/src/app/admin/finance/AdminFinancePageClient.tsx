'use client';

import React, { useEffect, useState, useCallback } from 'react';
import {
  Coins,
  TrendingUp,
  Check,
  X as CloseIcon,
  Clock,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Calendar,
  User,
  BookOpen,
  ChevronLeft,
  ChevronRight,
  Plus,
  Lock,
  RefreshCw,
} from 'lucide-react';
import {
  AdminShellChrome,
  AdminDataTable,
  AdminColumn,
  AdminModal,
} from '@/components/admin';
import {
  financeService,
  PayrollRecordDto,
  AdminPayoutDto,
  AdminCodeAccountingDto,
} from '@/services/finance-service';
import { teacherService, TeacherDto } from '@/services/teacher-service';
import { contentService, PackageDto } from '@/services/content-service';
import toast from 'react-hot-toast';

type ActiveTab = 'payroll' | 'payouts' | 'codes';

const monthsList = [
  { value: 1, label: 'يناير (1)' },
  { value: 2, label: 'فبراير (2)' },
  { value: 3, label: 'مارس (3)' },
  { value: 4, label: 'أبريل (4)' },
  { value: 5, label: 'مايو (5)' },
  { value: 6, label: 'يونيو (6)' },
  { value: 7, label: 'يوليو (7)' },
  { value: 8, label: 'أغسطس (8)' },
  { value: 9, label: 'سبتمبر (9)' },
  { value: 10, label: 'أكتوبر (10)' },
  { value: 11, label: 'نوفمبر (11)' },
  { value: 12, label: 'ديسمبر (12)' },
];

export default function AdminFinancePageClient() {
  const [activeTab, setActiveTab] = useState<ActiveTab>('payroll');

  // Shared state loaders
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [packages, setPackages] = useState<PackageDto[]>([]);

  // Payroll states
  const currentMonth = new Date().getMonth() + 1;
  const currentYear = new Date().getFullYear();
  const [payrollMonth, setPayrollMonth] = useState<number>(currentMonth);
  const [payrollYear, setPayrollYear] = useState<number>(currentYear);
  const [payrollRecords, setPayrollRecords] = useState<PayrollRecordDto[]>([]);
  const [payrollLoading, setPayrollLoading] = useState<boolean>(false);
  const [isGenerating, setIsGenerating] = useState<boolean>(false);

  // Adjustment Modal state
  const [selectedPayrollForAdjustment, setSelectedPayrollForAdjustment] = useState<PayrollRecordDto | null>(null);
  const [adjType, setAdjType] = useState<number>(0); // 0 = Addition, 1 = Deduction
  const [adjAmount, setAdjAmount] = useState<string>('');
  const [adjReason, setAdjReason] = useState<string>('');
  const [isSubmittingAdjustment, setIsSubmittingAdjustment] = useState<boolean>(false);

  // Adjustments detail view (nested list modal)
  const [selectedPayrollForDetails, setSelectedPayrollForDetails] = useState<PayrollRecordDto | null>(null);

  // Payouts states
  const [payoutStatusFilter, setPayoutStatusFilter] = useState<string>('All'); // 'All', 'Pending', 'Paid', 'Rejected'
  const [payoutRecords, setPayoutRecords] = useState<AdminPayoutDto[]>([]);
  const [payoutLoading, setPayoutLoading] = useState<boolean>(false);

  // Payout Rejection Modal state
  const [selectedPayoutForRejection, setSelectedPayoutForRejection] = useState<AdminPayoutDto | null>(null);
  const [rejectionReason, setRejectionReason] = useState<string>('');
  const [isSubmittingRejection, setIsSubmittingRejection] = useState<boolean>(false);

  // Codes reconciliations states
  const [filterTeacherId, setFilterTeacherId] = useState<string>('');
  const [filterPackageId, setFilterPackageId] = useState<string>('');
  const [filterStartDate, setFilterStartDate] = useState<string>('');
  const [filterEndDate, setFilterEndDate] = useState<string>('');
  const [codesPage, setCodesPage] = useState<number>(1);
  const [codesPageSize] = useState<number>(20);
  const [codesTotalCount, setCodesTotalCount] = useState<number>(0);
  const [codesData, setCodesData] = useState<AdminCodeAccountingDto[]>([]);
  const [codesLoading, setCodesLoading] = useState<boolean>(false);

  // Fetch helpers
  const fetchTeachersAndPackages = useCallback(async () => {
    try {
      const [tRes, pRes] = await Promise.all([
        teacherService.getTeachers(),
        contentService.getPackages(),
      ]);
      if (tRes?.success) setTeachers(tRes.data);
      if (pRes?.data?.data) setPackages(pRes.data.data);
    } catch {
      toast.error('تعذر تحميل بيانات المعلمين أو الباقات');
    }
  }, []);

  const fetchPayroll = useCallback(async () => {
    setPayrollLoading(true);
    try {
      const data = await financeService.getPayroll(payrollMonth, payrollYear);
      setPayrollRecords(data);
    } catch {
      toast.error('تعذر تحميل كشوف المرتبات');
    } finally {
      setPayrollLoading(false);
    }
  }, [payrollMonth, payrollYear]);

  const handleGeneratePayroll = async () => {
    setIsGenerating(true);
    try {
      const res = await financeService.generatePayroll(payrollMonth, payrollYear);
      if (res.success) {
        toast.success(`تم إنشاء مسودة كشوف المرتبات لعدد ${res.data} موظف بنجاح ✅`);
        fetchPayroll();
      } else {
        toast.error(res.message || 'فشل توليد المرتبات');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء توليد المرتبات');
    } finally {
      setIsGenerating(false);
    }
  };

  const handleApprovePayroll = async (payrollId: string) => {
    if (!confirm('هل أنت متأكد من رغبتك في الموافقة على كشف المرتب هذا؟ سيؤدي ذلك إلى قفل التعديل عليه نهائياً.')) {
      return;
    }
    try {
      const res = await financeService.approvePayroll(payrollId);
      if (res.success) {
        toast.success('تم اعتماد وقفل كشف المرتب بنجاح ✅');
        fetchPayroll();
      } else {
        toast.error(res.message || 'تعذر اعتماد كشف المرتب');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء اعتماد المرتب');
    }
  };

  const handleSubmitAdjustment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPayrollForAdjustment) return;
    const amountNum = parseFloat(adjAmount);
    if (isNaN(amountNum) || amountNum <= 0) {
      toast.error('يرجى إدخال مبلغ صحيح أكبر من الصفر');
      return;
    }
    if (!adjReason.trim()) {
      toast.error('يرجى تحديد سبب التعديل');
      return;
    }

    setIsSubmittingAdjustment(true);
    try {
      const res = await financeService.addPayrollAdjustment(selectedPayrollForAdjustment.id, {
        type: adjType,
        amount: amountNum,
        reason: adjReason,
      });

      if (res.success) {
        toast.success('تمت إضافة التسوية بنجاح ✅');
        setSelectedPayrollForAdjustment(null);
        setAdjAmount('');
        setAdjReason('');
        fetchPayroll();
      } else {
        toast.error(res.message || 'تعذر إضافة التسوية');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء حفظ التسوية');
    } finally {
      setIsSubmittingAdjustment(false);
    }
  };

  const handleDeleteAdjustment = async (payrollId: string, adjustmentId: string) => {
    if (!confirm('هل أنت متأكد من حذف هذه التسوية؟')) return;
    try {
      const res = await financeService.deletePayrollAdjustment(payrollId, adjustmentId);
      if (res.success) {
        toast.success('تم حذف التسوية بنجاح');
        fetchPayroll();
        if (selectedPayrollForDetails) {
          // Refresh details view state
          setPayrollRecords((prev) => {
            const updated = prev.find((x) => x.id === payrollId);
            if (updated) {
              setSelectedPayrollForDetails(updated);
            }
            return prev;
          });
        }
      } else {
        toast.error(res.message || 'تعذر حذف التسوية');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء حذف التسوية');
    }
  };

  // Payouts fetch
  const fetchPayouts = useCallback(async () => {
    setPayoutLoading(true);
    try {
      let status: number | undefined;
      if (payoutStatusFilter === 'Pending') status = 0;
      else if (payoutStatusFilter === 'Paid') status = 1;
      else if (payoutStatusFilter === 'Rejected') status = 2;

      const data = await financeService.getPayouts(status);
      setPayoutRecords(data);
    } catch {
      toast.error('تعذر تحميل طلبات السحب المالية');
    } finally {
      setPayoutLoading(false);
    }
  }, [payoutStatusFilter]);

  const handleResolvePayout = async (payoutId: string, status: number, reason?: string) => {
    try {
      const res = await financeService.resolvePayout(payoutId, {
        status,
        rejectionReason: reason,
      });

      if (res.success) {
        toast.success(status === 1 ? 'تم اعتماد صرف المستحقات بنجاح 💵' : 'تم رفض طلب السحب ❌');
        fetchPayouts();
      } else {
        toast.error(res.message || 'تعذر تحديث حالة طلب السحب');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء تحديث الطلب');
    }
  };

  const handleSubmitRejection = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPayoutForRejection) return;
    if (!rejectionReason.trim()) {
      toast.error('يرجى كتابة سبب الرفض');
      return;
    }
    setIsSubmittingRejection(true);
    await handleResolvePayout(selectedPayoutForRejection.id, 2, rejectionReason);
    setIsSubmittingRejection(false);
    setSelectedPayoutForRejection(null);
    setRejectionReason('');
  };

  // Codes activation ledger fetch
  const fetchCodesAccounting = useCallback(async () => {
    setCodesLoading(true);
    try {
      const res = await financeService.getCodeAccounting({
        teacherId: filterTeacherId || undefined,
        packageId: filterPackageId || undefined,
        startDate: filterStartDate || undefined,
        endDate: filterEndDate || undefined,
        page: codesPage,
        pageSize: codesPageSize,
      });

      setCodesData(res.items);
      setCodesTotalCount(res.totalCount);
    } catch {
      toast.error('تعذر تحميل سجل تفعيل الأكواد');
    } finally {
      setCodesLoading(false);
    }
  }, [filterTeacherId, filterPackageId, filterStartDate, filterEndDate, codesPage, codesPageSize]);

  // Handle tab routing
  useEffect(() => {
    fetchTeachersAndPackages();
  }, [fetchTeachersAndPackages]);

  useEffect(() => {
    if (activeTab === 'payroll') {
      fetchPayroll();
    } else if (activeTab === 'payouts') {
      fetchPayouts();
    } else if (activeTab === 'codes') {
      fetchCodesAccounting();
    }
  }, [activeTab, fetchPayroll, fetchPayouts, fetchCodesAccounting, codesPage]);

  // Reset pagination on filter change
  useEffect(() => {
    setCodesPage(1);
  }, [filterTeacherId, filterPackageId, filterStartDate, filterEndDate]);

  // Formatter helpers
  const formatEGP = (amount: number) => {
    return `${amount.toLocaleString('ar-EG')} جنيها`;
  };

  const formatDate = (isoString: string) => {
    return new Date(isoString).toLocaleDateString('ar-EG', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  // Status Badge helpers
  const getPayrollStatusBadge = (status: string | number) => {
    const statusStr = typeof status === 'number'
      ? (status === 1 ? 'Approved' : 'Draft')
      : status;

    switch (statusStr) {
      case 'Approved':
      case 'Paid':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">
            <CheckCircle2 className="h-3 w-3" />
            معتمد
          </span>
        );
      case 'Draft':
      default:
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2.5 py-1 text-xs font-bold text-blue-700 dark:bg-blue-950/40 dark:text-blue-400">
            <AlertCircle className="h-3 w-3" />
            مسودة
          </span>
        );
    }
  };

  const getPayoutStatusBadge = (status: string | number) => {
    const statusStr = typeof status === 'number'
      ? (status === 0 ? 'Pending' : status === 1 ? 'Paid' : status === 2 ? 'Rejected' : 'Unknown')
      : status;

    switch (statusStr) {
      case 'Approved':
      case 'Paid':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400">
            <CheckCircle2 className="h-3 w-3" />
            معتمد
          </span>
        );
      case 'Rejected':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-rose-100 px-2.5 py-1 text-xs font-bold text-rose-700 dark:bg-rose-950/40 dark:text-rose-400">
            <XCircle className="h-3 w-3" />
            مرفوض
          </span>
        );
      case 'Pending':
      default:
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-1 text-xs font-bold text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 animate-pulse">
            <Clock className="h-3 w-3" />
            انتظار الصرف
          </span>
        );
    }
  };

  // Columns definition for Tables
  const payrollColumns: AdminColumn<PayrollRecordDto>[] = [
    {
      key: 'employeeName',
      label: 'اسم الموظف / المعلم',
      render: (item) => (
        <div className="flex items-center gap-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--admin-primary)]/15 text-[var(--admin-primary)]">
            <User className="h-4 w-4" />
          </div>
          <span className="font-bold text-[var(--admin-text)]">{item.employeeName}</span>
        </div>
      ),
    },
    {
      key: 'basicSalary',
      label: 'الراتب الأساسي',
      render: (item) => <span className="font-bold font-mono">{formatEGP(item.basicSalary)}</span>,
    },
    {
      key: 'additions',
      label: 'إضافات',
      render: (item) => (
        <span className="font-bold font-mono text-emerald-600 dark:text-emerald-400">
          +{formatEGP(item.additions)}
        </span>
      ),
    },
    {
      key: 'deductions',
      label: 'خصومات',
      render: (item) => (
        <span className="font-bold font-mono text-rose-600 dark:text-rose-400">
          -{formatEGP(item.deductions)}
        </span>
      ),
    },
    {
      key: 'netSalary',
      label: 'صافي الراتب المستحق',
      render: (item) => (
        <span className="font-black font-mono text-[var(--admin-primary)] text-base">
          {formatEGP(item.netSalary)}
        </span>
      ),
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (item) => getPayrollStatusBadge(item.status),
    },
    {
      key: 'actions',
      label: 'الإجراءات والتسويات',
      align: 'left',
      render: (item) => {
        const isDraft = item.status === 'Draft' || item.status === 0;
        return (
          <div className="flex items-center justify-end gap-2">
            <button
              onClick={() => {
                setSelectedPayrollForDetails(item);
              }}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-1.5 text-xs font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"
            >
              التسويات ({item.adjustments.length})
            </button>

            {isDraft && (
              <>
                <button
                  onClick={() => setSelectedPayrollForAdjustment(item)}
                  className="rounded-xl bg-[var(--admin-primary)]/10 px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-primary)]/20 flex items-center gap-1"
                >
                  <Plus className="h-3 w-3" />
                  تسوية جديدة
                </button>
                <button
                  onClick={() => handleApprovePayroll(item.id)}
                  className="rounded-xl bg-emerald-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-emerald-700 flex items-center gap-1"
                >
                  <Lock className="h-3.5 w-3.5" />
                  اعتماد وقفل
                </button>
              </>
            )}
            {!isDraft && (item.approvedByName || item.approvedAt) && (
              <span className="text-xs text-[var(--admin-muted)] text-left" title={item.approvedByName ? `بواسطة ${item.approvedByName} في ${item.approvedAt ? formatDate(item.approvedAt) : ''}` : undefined}>
                معتمد وقفل
              </span>
            )}
          </div>
        );
      },
    },
  ];

  const payoutsColumns: AdminColumn<AdminPayoutDto>[] = [
    {
      key: 'teacherName',
      label: 'اسم المعلم',
      render: (item) => (
        <div className="flex items-center gap-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--admin-primary)]/15 text-[var(--admin-primary)]">
            <Coins className="h-4 w-4" />
          </div>
          <span className="font-bold text-[var(--admin-text)]">{item.teacherName}</span>
        </div>
      ),
    },
    {
      key: 'amount',
      label: 'مبلغ السحب المطلوب',
      render: (item) => <span className="font-bold font-mono text-lg text-[var(--admin-text)]">{formatEGP(item.amount)}</span>,
    },
    {
      key: 'createdAt',
      label: 'تاريخ الطلب',
      render: (item) => <span className="font-mono text-sm">{formatDate(item.createdAt)}</span>,
    },
    {
      key: 'status',
      label: 'الحالة',
      render: (item) => (
        <div>
          {getPayoutStatusBadge(item.status)}
          {(item.status === 'Rejected' || item.status === 2) && item.rejectionReason && (
            <div className="text-xs text-rose-500 mt-1 max-w-[200px] truncate" title={item.rejectionReason}>
              السبب: {item.rejectionReason}
            </div>
          )}
        </div>
      ),
    },
    {
      key: 'handledBy',
      label: 'المسؤول المعالج',
      render: (item) =>
        item.handledByName ? (
          <div>
            <div className="text-xs font-bold">{item.handledByName}</div>
            <div className="text-xs text-[var(--admin-muted)] font-mono">
              {item.handledAt ? formatDate(item.handledAt) : ''}
            </div>
          </div>
        ) : (
          <span className="text-xs text-[var(--admin-muted)]">—</span>
        ),
    },
    {
      key: 'actions',
      label: 'الإجراءات',
      align: 'left',
      render: (item) => {
        if (item.status !== 'Pending' && item.status !== 0) return null;
        return (
          <div className="flex items-center justify-end gap-2">
            <button
              onClick={() => handleResolvePayout(item.id, 1)}
              className="rounded-xl bg-emerald-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-emerald-700 flex items-center gap-1"
            >
              <Check className="h-3.5 w-3.5" />
              تأكيد الدفع والصرف
            </button>
            <button
              onClick={() => setSelectedPayoutForRejection(item)}
              className="rounded-xl bg-rose-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-rose-700 flex items-center gap-1"
            >
              <CloseIcon className="h-3.5 w-3.5" />
              رفض الطلب
            </button>
          </div>
        );
      },
    },
  ];

  const codesColumns: AdminColumn<AdminCodeAccountingDto>[] = [
    {
      key: 'activationDate',
      label: 'تاريخ التفعيل',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-muted)]">{formatDate(item.activatedAt)}</span>,
    },
    {
      key: 'packageName',
      label: 'الباقة الدراسية',
      render: (item) => (
        <div className="flex items-center gap-1.5">
          <BookOpen className="h-4 w-4 text-[var(--admin-muted)]" />
          <span className="font-bold text-[var(--admin-text)]">{item.packageName}</span>
        </div>
      ),
    },
    {
      key: 'teacherName',
      label: 'المعلم صاحب الباقة',
      render: (item) => <span className="font-semibold text-xs text-[var(--admin-muted)]">{item.teacherName}</span>,
    },
    {
      key: 'studentName',
      label: 'الطالب المفعل',
      render: (item) => (
        <div>
          <span className="font-bold text-xs text-[var(--admin-text)]">{item.studentName}</span>
        </div>
      ),
    },
    {
      key: 'serialNumber',
      label: 'الرقم التسلسلي للكود',
      render: (item) => <span className="font-mono text-xs font-bold text-[var(--admin-primary)]">{item.serialNumber}</span>,
    },
    {
      key: 'price',
      label: 'سعر تفعيل الباقة',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-text)]">{formatEGP(item.price)}</span>,
    },
    {
      key: 'commissionRate',
      label: 'نسبة عمولة المعلم',
      render: (item) => <span className="font-mono text-xs font-bold text-emerald-600">%{item.commissionRate}</span>,
    },
    {
      key: 'commissionEarned',
      label: 'الأرباح المحتسبة للمعلم',
      render: (item) => <span className="font-mono text-sm font-black text-emerald-600 dark:text-emerald-400">{formatEGP(item.commissionEarned)}</span>,
    },
  ];

  const currentYearOptions = Array.from({ length: 5 }, (_, i) => currentYear - 2 + i);

  return (
    <AdminShellChrome
      activePath="/admin/finance"
      sectionLabel="المالية والأرباح"
      pageTitle="لوحة التحكم والعمليات المالية"
      subtitle="إدارة رواتب الموظفين الشهرية، مراجعة طلبات سحب مستحقات المعلمين، وتتبع حركة تفعيل الأكواد وتحصيل النسب."
    >
      {/* Tabs Menu */}
      <div className="mb-8 flex justify-center">
        <div className="inline-flex gap-1 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-1.5 shadow-sm backdrop-blur-xl">
          <button
            onClick={() => setActiveTab('payroll')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'payroll'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <Calendar className="h-4 w-4" />
            مسيرات الرواتب الشهرية
          </button>
          <button
            onClick={() => setActiveTab('payouts')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'payouts'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <Coins className="h-4 w-4" />
            طلبات سحب المعلمين
          </button>
          <button
            onClick={() => setActiveTab('codes')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'codes'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <TrendingUp className="h-4 w-4" />
            حركة تفعيل الأكواد وعمولات المعلمين
          </button>
        </div>
      </div>

      {/* Tab Contents: Payroll */}
      {activeTab === 'payroll' && (
        <div>
          {/* Filters Bar */}
          <div className="mb-6 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 flex flex-wrap gap-4 items-center justify-between">
            <div className="flex flex-wrap items-center gap-4">
              <div className="flex items-center gap-2">
                <span className="text-sm font-bold text-[var(--admin-muted)]">الشهر:</span>
                <select
                  value={payrollMonth}
                  onChange={(e) => setPayrollMonth(parseInt(e.target.value))}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
                >
                  {monthsList.map((m) => (
                    <option key={m.value} value={m.value}>
                      {m.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex items-center gap-2">
                <span className="text-sm font-bold text-[var(--admin-muted)]">السنة:</span>
                <select
                  value={payrollYear}
                  onChange={(e) => setPayrollYear(parseInt(e.target.value))}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
                >
                  {currentYearOptions.map((yr) => (
                    <option key={yr} value={yr}>
                      {yr}
                    </option>
                  ))}
                </select>
              </div>

              <button
                onClick={fetchPayroll}
                disabled={payrollLoading}
                className="rounded-xl bg-[var(--admin-card)] border border-[var(--admin-border)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] flex items-center gap-2 transition"
              >
                <RefreshCw className={`h-4 w-4 ${payrollLoading ? 'animate-spin' : ''}`} />
                تحديث العرض
              </button>
            </div>

            <button
              onClick={handleGeneratePayroll}
              disabled={isGenerating || payrollLoading}
              className="rounded-xl bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-black text-[var(--admin-primary-contrast)] shadow-[0_4px_12px_var(--admin-shadow)] hover:opacity-90 transition flex items-center gap-2 disabled:opacity-50"
            >
              {isGenerating ? 'جاري التوليد...' : 'توليد مرتبات الشهر'}
            </button>
          </div>

          <AdminDataTable
            data={payrollRecords}
            columns={payrollColumns}
            loading={payrollLoading}
            rowKey={(item) => item.id}
            emptyMessage="لا يوجد كشف مرتبات تم إنشاؤه لهذا الشهر حتى الآن. اضغط على 'توليد مرتبات الشهر' للبدء."
          />
        </div>
      )}

      {/* Tab Contents: Payouts */}
      {activeTab === 'payouts' && (
        <div>
          {/* Status filter bar */}
          <div className="mb-6 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <span className="text-sm font-bold text-[var(--admin-muted)]">حالة الطلبات:</span>
              <select
                value={payoutStatusFilter}
                onChange={(e) => setPayoutStatusFilter(e.target.value)}
                className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2 text-sm text-[var(--admin-text)] outline-none"
              >
                <option value="All">كل طلبات السحب</option>
                <option value="Pending">قيد انتظار الصرف</option>
                <option value="Paid">تم الصرف والاعتماد</option>
                <option value="Rejected">مرفوضة</option>
              </select>
            </div>
            <button
              onClick={fetchPayouts}
              disabled={payoutLoading}
              className="rounded-xl bg-[var(--admin-card)] border border-[var(--admin-border)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] flex items-center gap-2 transition"
            >
              <RefreshCw className={`h-4 w-4 ${payoutLoading ? 'animate-spin' : ''}`} />
              تحديث
            </button>
          </div>

          <AdminDataTable
            data={payoutRecords}
            columns={payoutsColumns}
            loading={payoutLoading}
            rowKey={(item) => item.id}
            emptyMessage="لا توجد طلبات سحب مالية مطابقة للفلتر المحدد حالياً."
          />
        </div>
      )}

      {/* Tab Contents: Codes */}
      {activeTab === 'codes' && (
        <div>
          {/* Detailed Filters ledger */}
          <div className="mb-6 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
            <h4 className="text-sm font-black mb-3 text-[var(--admin-text)]">فلاتر البحث والمطابقة</h4>
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
              <div>
                <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">المعلم صاحب الباقة</label>
                <select
                  value={filterTeacherId}
                  onChange={(e) => setFilterTeacherId(e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
                >
                  <option value="">كل المعلمين</option>
                  {teachers.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.fullName}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">الباقة الدراسية</label>
                <select
                  value={filterPackageId}
                  onChange={(e) => setFilterPackageId(e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
                >
                  <option value="">كل الباقات</option>
                  {packages.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">من تاريخ</label>
                <input
                  type="date"
                  value={filterStartDate}
                  onChange={(e) => setFilterStartDate(e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-[var(--admin-muted)] mb-1">إلى تاريخ</label>
                <input
                  type="date"
                  value={filterEndDate}
                  onChange={(e) => setFilterEndDate(e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
                />
              </div>
            </div>

            <div className="mt-4 flex justify-end gap-2">
              <button
                onClick={() => {
                  setFilterTeacherId('');
                  setFilterPackageId('');
                  setFilterStartDate('');
                  setFilterEndDate('');
                }}
                className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2 text-xs font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"
              >
                مسح الفلاتر
              </button>
              <button
                onClick={fetchCodesAccounting}
                disabled={codesLoading}
                className="rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-xs font-bold text-[var(--admin-primary-contrast)] hover:opacity-90 flex items-center gap-1"
              >
                <RefreshCw className={`h-3.5 w-3.5 ${codesLoading ? 'animate-spin' : ''}`} />
                تحديث وبحث
              </button>
            </div>
          </div>

          <AdminDataTable
            data={codesData}
            columns={codesColumns}
            loading={codesLoading}
            rowKey={(item) => item.id}
            emptyMessage="لم يتم العثور على أية تفعيلات مطابقة للفلاتر الحالية."
          />

          {/* Pagination Controls */}
          {codesTotalCount > codesPageSize && (
            <div className="mt-6 flex items-center justify-between border-t border-[var(--admin-border)] pt-4">
              <span className="text-xs font-semibold text-[var(--admin-muted)]">
                عرض {codesData.length} من أصل {codesTotalCount} عملية تفعيل
              </span>
              <div className="flex items-center gap-2">
                <button
                  disabled={codesPage === 1 || codesLoading}
                  onClick={() => setCodesPage((prev) => prev - 1)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-40"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
                <span className="font-mono text-sm font-bold px-3">
                  صفحة {codesPage} من {Math.ceil(codesTotalCount / codesPageSize)}
                </span>
                <button
                  disabled={codesPage * codesPageSize >= codesTotalCount || codesLoading}
                  onClick={() => setCodesPage((prev) => prev + 1)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-40"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: Add Payroll Adjustment */}
      <AdminModal
        open={selectedPayrollForAdjustment !== null}
        onClose={() => setSelectedPayrollForAdjustment(null)}
        title="إضافة تسوية مالية جديدة"
        subtitle={`إجراء إضافة أو خصم على كشف مرتب الموظف: ${selectedPayrollForAdjustment?.employeeName}`}
      >
        <form onSubmit={handleSubmitAdjustment} className="space-y-4">
          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-1.5">نوع التسوية</label>
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => setAdjType(0)}
                className={`rounded-xl border p-3 text-sm font-bold text-center transition ${
                  adjType === 0
                    ? 'border-emerald-500 bg-emerald-500/10 text-emerald-600'
                    : 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)]'
                }`}
              >
                إضافة إيجابية (+)
              </button>
              <button
                type="button"
                onClick={() => setAdjType(1)}
                className={`rounded-xl border p-3 text-sm font-bold text-center transition ${
                  adjType === 1
                    ? 'border-rose-500 bg-rose-500/10 text-rose-600'
                    : 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)]'
                }`}
              >
                خصم سلبي (-)
              </button>
            </div>
          </div>

          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-1">المبلغ المالي (جنيها)</label>
            <input
              type="number"
              step="0.01"
              required
              placeholder="مثال: 500"
              value={adjAmount}
              onChange={(e) => setAdjAmount(e.target.value)}
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
            />
          </div>

          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-1">سبب التسوية / التعديل</label>
            <textarea
              required
              rows={3}
              placeholder="اكتب تفاصيل أو سبب إجراء هذه التسوية لتوثيقها بالكشف..."
              value={adjReason}
              onChange={(e) => setAdjReason(e.target.value)}
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
            />
          </div>

          <div className="flex justify-end gap-2 border-t border-[var(--admin-border)] pt-4">
            <button
              type="button"
              onClick={() => setSelectedPayrollForAdjustment(null)}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-sm font-bold text-[var(--admin-text)]"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isSubmittingAdjustment}
              className="rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-50"
            >
              {isSubmittingAdjustment ? 'جاري الحفظ...' : 'حفظ وإضافة'}
            </button>
          </div>
        </form>
      </AdminModal>

      {/* MODAL 2: Adjustments details list */}
      <AdminModal
        open={selectedPayrollForDetails !== null}
        onClose={() => setSelectedPayrollForDetails(null)}
        title="التسويات المدرجة بالكشف"
        subtitle={`سجل التسويات التفصيلي لكشف مرتب: ${selectedPayrollForDetails?.employeeName}`}
      >
        <div className="space-y-4">
          {selectedPayrollForDetails?.adjustments.length === 0 ? (
            <div className="py-8 text-center text-sm text-[var(--admin-muted)]">
              لا توجد أية تسويات مضافة إلى هذا الكشف حتى الآن.
            </div>
          ) : (
            <div className="divide-y divide-[var(--admin-border)]">
              {selectedPayrollForDetails?.adjustments.map((adj) => (
                <div key={adj.id} className="py-3 flex items-start justify-between gap-3">
                  <div>
                    <div className="flex items-center gap-2">
                      <span
                        className={`rounded-full px-2 py-0.5 text-xs font-bold ${
                          adj.type === 'Addition'
                            ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400'
                            : 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400'
                        }`}
                      >
                        {adj.type === 'Addition' ? 'إضافة (+)' : 'خصم (-)'}
                      </span>
                      <span className="font-mono text-sm font-bold text-[var(--admin-text)]">
                        {formatEGP(adj.amount)}
                      </span>
                    </div>
                    <p className="text-xs text-[var(--admin-muted)] mt-1">{adj.reason}</p>
                    <span className="text-xs text-[var(--admin-muted)] mt-0.5 block font-mono">
                      {formatDate(adj.createdAt)}
                    </span>
                  </div>

                  {(selectedPayrollForDetails.status === 'Draft' || selectedPayrollForDetails.status === 0) && (
                    <button
                      onClick={() => handleDeleteAdjustment(selectedPayrollForDetails.id, adj.id)}
                      className="text-xs font-bold text-rose-500 hover:text-rose-600 rounded p-1 hover:bg-rose-50 dark:hover:bg-rose-950/20"
                    >
                      حذف
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}

          <div className="flex justify-end border-t border-[var(--admin-border)] pt-4">
            <button
              onClick={() => setSelectedPayrollForDetails(null)}
              className="rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-sm font-black text-[var(--admin-primary-contrast)]"
            >
              موافق / إغلاق
            </button>
          </div>
        </div>
      </AdminModal>

      {/* MODAL 3: Payout Rejection */}
      <AdminModal
        open={selectedPayoutForRejection !== null}
        onClose={() => setSelectedPayoutForRejection(null)}
        title="رفض طلب سحب المستحقات"
        subtitle={`كتابة سبب رفض طلب السحب الخاص بالمعلم: ${selectedPayoutForRejection?.teacherName}`}
      >
        <form onSubmit={handleSubmitRejection} className="space-y-4">
          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-1">مبلغ السحب المرفوض</label>
            <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-2 text-sm font-mono text-[var(--admin-muted)]">
              {selectedPayoutForRejection ? formatEGP(selectedPayoutForRejection.amount) : ''}
            </div>
          </div>

          <div>
            <label className="block text-sm font-bold text-[var(--admin-text)] mb-1">سبب الرفض الموجه للمعلم</label>
            <textarea
              required
              rows={3}
              placeholder="اكتب هنا سبب الرفض بالتفصيل ليظهر للمعلم في حساب الأرباح الخاص به..."
              value={rejectionReason}
              onChange={(e) => setRejectionReason(e.target.value)}
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]"
            />
          </div>

          <div className="flex justify-end gap-2 border-t border-[var(--admin-border)] pt-4">
            <button
              type="button"
              onClick={() => setSelectedPayoutForRejection(null)}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-sm font-bold text-[var(--admin-text)]"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isSubmittingRejection}
              className="rounded-xl bg-rose-600 px-5 py-2 text-sm font-black text-white hover:bg-rose-700 disabled:opacity-50"
            >
              {isSubmittingRejection ? 'جاري الرفض...' : 'رفض الطلب نهائياً'}
            </button>
          </div>
        </form>
      </AdminModal>
    </AdminShellChrome>
  );
}
