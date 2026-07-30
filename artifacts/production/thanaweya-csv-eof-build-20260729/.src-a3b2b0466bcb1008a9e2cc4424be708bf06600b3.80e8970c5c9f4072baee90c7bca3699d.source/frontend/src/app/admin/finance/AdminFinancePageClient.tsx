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
  AdminConfirmationDialog,
  AdminModal,
} from '@/components/admin';
import {
  financeService,
  PayrollRecordDto,
  AdminPayoutDto,
  AdminCodeAccountingDto,
  AdminTeacherFinancialEventDto,
} from '@/services/finance-service';
import { teacherService, TeacherDto } from '@/services/teacher-service';
import { invalidateMany } from '@/lib/cache-invalidation';
import { contentService, PackageDto } from '@/services/content-service';
import toast from 'react-hot-toast';

type ActiveTab = 'payroll' | 'payouts' | 'codes' | 'review';
type ConfirmationAction =
  | { type: 'approve-payroll'; payrollId: string }
  | { type: 'delete-adjustment'; payrollId: string; adjustmentId: string };

const financeTabs: Array<{
  id: ActiveTab;
  label: string;
  compactLabel: string;
  Icon: typeof Calendar;
}> = [
  { id: 'payroll', label: 'مسيرات الرواتب الشهرية', compactLabel: 'الرواتب', Icon: Calendar },
  { id: 'payouts', label: 'طلبات سحب المعلمين', compactLabel: 'السحوبات', Icon: Coins },
  { id: 'codes', label: 'حركة تفعيل الأكواد وعمولات المعلمين', compactLabel: 'الأكواد', Icon: TrendingUp },
  { id: 'review', label: 'مراجعة البنود المالية', compactLabel: 'المراجعة', Icon: AlertCircle },
];

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
  const [confirmationAction, setConfirmationAction] = useState<ConfirmationAction | null>(null);
  const [isConfirmingAction, setIsConfirmingAction] = useState<boolean>(false);

  // Adjustment Modal state
  const [selectedPayrollForAdjustment, setSelectedPayrollForAdjustment] = useState<PayrollRecordDto | null>(null);
  const [adjType, setAdjType] = useState<number>(0); // 0 = Addition, 1 = Deduction
  const [adjAmount, setAdjAmount] = useState<string>('');
  const [adjReason, setAdjReason] = useState<string>('');
  const [isSubmittingAdjustment, setIsSubmittingAdjustment] = useState<boolean>(false);

  // Adjustments detail view (nested list modal)
  const [selectedPayrollForDetails, setSelectedPayrollForDetails] = useState<PayrollRecordDto | null>(null);

  // Payouts states
  const [payoutStatusFilter, setPayoutStatusFilter] = useState<string>('All'); // 'All', 'Pending', 'Approved', 'Paid', 'Rejected'
  const [payoutRecords, setPayoutRecords] = useState<AdminPayoutDto[]>([]);
  const [payoutLoading, setPayoutLoading] = useState<boolean>(false);
  const [resolvingPayoutId, setResolvingPayoutId] = useState<string | null>(null);

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

  // Teacher financial review states
  const [reviewStatusFilter, setReviewStatusFilter] = useState<string>('PendingReview');
  const [reviewTeacherId, setReviewTeacherId] = useState<string>('');
  const [reviewPage, setReviewPage] = useState<number>(1);
  const [reviewPageSize] = useState<number>(50);
  const [reviewTotalCount, setReviewTotalCount] = useState<number>(0);
  const [reviewData, setReviewData] = useState<AdminTeacherFinancialEventDto[]>([]);
  const [reviewLoading, setReviewLoading] = useState<boolean>(false);
  const [compensationTeacherId, setCompensationTeacherId] = useState<string>('');
  const [compensationAmount, setCompensationAmount] = useState<string>('');
  const [compensationReason, setCompensationReason] = useState<string>('');
  const [compensationSubmitting, setCompensationSubmitting] = useState<boolean>(false);

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
        invalidateMany(['finance:payroll', 'finance:teacher', 'reports']);
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

  const handleApprovePayroll = (payrollId: string) => {
    setConfirmationAction({ type: 'approve-payroll', payrollId });
  };

  const handleDeleteAdjustment = (payrollId: string, adjustmentId: string) => {
    setConfirmationAction({ type: 'delete-adjustment', payrollId, adjustmentId });
  };

  const handleConfirmAction = async () => {
    if (!confirmationAction) return;

    setIsConfirmingAction(true);
    try {
      const res = confirmationAction.type === 'approve-payroll'
        ? await financeService.approvePayroll(confirmationAction.payrollId)
        : await financeService.deletePayrollAdjustment(
          confirmationAction.payrollId,
          confirmationAction.adjustmentId,
        );

      if (res.success) {
        invalidateMany(['finance:payroll', 'finance:teacher', 'reports']);
        toast.success(
          confirmationAction.type === 'approve-payroll'
            ? 'تم اعتماد وقفل كشف المرتب بنجاح ✅'
            : 'تم حذف التسوية بنجاح',
        );
        fetchPayroll();

        if (confirmationAction.type === 'delete-adjustment' && selectedPayrollForDetails) {
          setPayrollRecords((prev) => {
            const updated = prev.find((item) => item.id === confirmationAction.payrollId);
            if (updated) setSelectedPayrollForDetails(updated);
            return prev;
          });
        }

        setConfirmationAction(null);
      } else {
        toast.error(
          res.message || (confirmationAction.type === 'approve-payroll'
            ? 'تعذر اعتماد كشف المرتب'
            : 'تعذر حذف التسوية'),
        );
      }
    } catch (err: any) {
      toast.error(
        err?.response?.data?.message || (confirmationAction.type === 'approve-payroll'
          ? 'حدث خطأ أثناء اعتماد المرتب'
          : 'حدث خطأ أثناء حذف التسوية'),
      );
    } finally {
      setIsConfirmingAction(false);
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
        invalidateMany(['finance:payroll', 'finance:teacher', 'reports']);
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

  // Payouts fetch
  const fetchPayouts = useCallback(async () => {
    setPayoutLoading(true);
    try {
      let status: number | undefined;
      if (payoutStatusFilter === 'Pending') status = 0;
      else if (payoutStatusFilter === 'Paid') status = 1;
      else if (payoutStatusFilter === 'Rejected') status = 2;
      else if (payoutStatusFilter === 'Approved') status = 3;

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
      setResolvingPayoutId(payoutId);
      const res = await financeService.resolvePayout(payoutId, {
        status,
        rejectionReason: reason,
      });

      if (res.success) {
        invalidateMany(['finance:teacher', 'student:balance', 'reports']);
        toast.success(status === 3 ? 'تم قبول طلب السحب وأصبح جاهزاً للصرف' : status === 1 ? 'تم تسجيل الصرف الفعلي بنجاح' : 'تم رفض طلب السحب');
        fetchPayouts();
      } else {
        toast.error(res.message || 'تعذر تحديث حالة طلب السحب');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء تحديث الطلب');
    } finally {
      setResolvingPayoutId(null);
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

  const fetchTeacherFinancialEvents = useCallback(async () => {
    setReviewLoading(true);
    try {
      const res = await financeService.getTeacherFinancialEvents({
        status: reviewStatusFilter || undefined,
        teacherId: reviewTeacherId || undefined,
        page: reviewPage,
        pageSize: reviewPageSize,
      });
      setReviewData(res.items);
      setReviewTotalCount(res.totalCount);
    } catch {
      toast.error('تعذر تحميل مراجعة البنود المالية');
    } finally {
      setReviewLoading(false);
    }
  }, [reviewStatusFilter, reviewTeacherId, reviewPage, reviewPageSize]);

  const handleReviewTeacherEvent = async (allocationId: string, status: 'Approved' | 'Rejected') => {
    try {
      const res = await financeService.reviewTeacherFinancialEvent(allocationId, { status });
      if (!res.success) {
        toast.error(res.message || 'تعذر تحديث المراجعة');
        return;
      }
      invalidateMany(['finance:teacher', 'reports']);
      toast.success(status === 'Approved' ? 'تم اعتماد البند المالي' : 'تم رفض البند المالي');
      fetchTeacherFinancialEvents();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'تعذر تحديث المراجعة');
    }
  };

  const handleManualCompensation = async () => {
    const amount = Number(compensationAmount);
    if (!compensationTeacherId || amount <= 0) {
      toast.error('حدد المدرس وقيمة تعويض أكبر من صفر');
      return;
    }

    setCompensationSubmitting(true);
    try {
      const res = await financeService.createManualTeacherCompensation({
        teacherId: compensationTeacherId,
        amount,
        reason: compensationReason || 'تعويض يدوي صريح',
      });
      if (!res.success) {
        toast.error(res.message || 'تعذر تسجيل التعويض');
        return;
      }
      invalidateMany(['finance:teacher', 'reports']);
      toast.success('تم تسجيل التعويض اليدوي');
      setCompensationAmount('');
      setCompensationReason('');
      fetchTeacherFinancialEvents();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'تعذر تسجيل التعويض');
    } finally {
      setCompensationSubmitting(false);
    }
  };

  // Handle tab routing
  useEffect(() => {
    if (activeTab === 'payroll') {
      fetchPayroll();
    } else if (activeTab === 'payouts') {
      fetchPayouts();
    } else if (activeTab === 'codes') {
      fetchTeachersAndPackages();
      fetchCodesAccounting();
    } else if (activeTab === 'review') {
      fetchTeachersAndPackages();
      fetchTeacherFinancialEvents();
    }
  }, [activeTab, fetchPayroll, fetchPayouts, fetchCodesAccounting, fetchTeacherFinancialEvents, fetchTeachersAndPackages, codesPage]);

  // Reset pagination on filter change
  useEffect(() => {
    setCodesPage(1);
  }, [filterTeacherId, filterPackageId, filterStartDate, filterEndDate]);

  useEffect(() => {
    setReviewPage(1);
  }, [reviewStatusFilter, reviewTeacherId]);

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
      ? (status === 0 ? 'Pending' : status === 1 ? 'Paid' : status === 2 ? 'Rejected' : status === 3 ? 'Approved' : 'Unknown')
      : status;

    switch (statusStr) {
      case 'Approved':
        return (
          <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2.5 py-1 text-xs font-bold text-blue-700 dark:bg-blue-950/40 dark:text-blue-400">
            <CheckCircle2 className="h-3 w-3" />
            جاهز للصرف
          </span>
        );
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
            بانتظار قبول السحب
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
      responsivePriority: 'secondary',
      render: (item) => <span className="font-bold font-mono">{formatEGP(item.basicSalary)}</span>,
    },
    {
      key: 'additions',
      label: 'إضافات',
      responsivePriority: 'optional',
      render: (item) => (
        <span className="font-bold font-mono text-emerald-600 dark:text-emerald-400">
          +{formatEGP(item.additions)}
        </span>
      ),
    },
    {
      key: 'deductions',
      label: 'خصومات',
      responsivePriority: 'optional',
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
      responsivePriority: 'secondary',
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
      responsivePriority: 'optional',
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
        const statusStr = typeof item.status === 'number'
          ? (item.status === 0 ? 'Pending' : item.status === 1 ? 'Paid' : item.status === 2 ? 'Rejected' : item.status === 3 ? 'Approved' : 'Unknown')
          : item.status;
        if (statusStr !== 'Pending' && statusStr !== 'Approved') return null;
        const isResolving = resolvingPayoutId === item.id;
        return (
          <div className="flex items-center justify-end gap-2">
            {statusStr === 'Pending' && (
              <button
                onClick={() => handleResolvePayout(item.id, 3)}
                disabled={isResolving}
                className="rounded-xl bg-emerald-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-60 flex items-center gap-1"
              >
                <Check className="h-3.5 w-3.5" />
                {isResolving ? 'جاري القبول...' : 'قبول السحب'}
              </button>
            )}
            {statusStr === 'Approved' && (
              <button
                onClick={() => handleResolvePayout(item.id, 1)}
                disabled={isResolving}
                className="rounded-xl bg-blue-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60 flex items-center gap-1"
              >
                <Check className="h-3.5 w-3.5" />
                {isResolving ? 'جاري التسجيل...' : 'تسجيل تم الصرف'}
              </button>
            )}
            {statusStr === 'Pending' && (
              <button
                onClick={() => setSelectedPayoutForRejection(item)}
                disabled={isResolving}
                className="rounded-xl bg-rose-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-rose-700 disabled:cursor-not-allowed disabled:opacity-60 flex items-center gap-1"
              >
                <CloseIcon className="h-3.5 w-3.5" />
                رفض الطلب
              </button>
            )}
          </div>
        );
      },
    },
  ];

  const codesColumns: AdminColumn<AdminCodeAccountingDto>[] = [
    {
      key: 'activationDate',
      label: 'تاريخ التفعيل',
      responsivePriority: 'secondary',
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
      responsivePriority: 'optional',
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
      responsivePriority: 'optional',
      render: (item) => <span className="font-mono text-xs font-bold text-[var(--admin-primary)]">{item.serialNumber}</span>,
    },
    {
      key: 'price',
      label: 'سعر تفعيل الباقة',
      responsivePriority: 'secondary',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-text)]">{formatEGP(item.price)}</span>,
    },
    {
      key: 'commissionRate',
      label: 'نسبة عمولة المعلم',
      responsivePriority: 'optional',
      render: (item) => <span className="font-mono text-xs font-bold text-emerald-600">%{item.commissionRate}</span>,
    },
    {
      key: 'commissionEarned',
      label: 'الأرباح المحتسبة للمعلم',
      render: (item) => <span className="font-mono text-sm font-black text-emerald-600 dark:text-emerald-400">{formatEGP(item.commissionEarned)}</span>,
    },
  ];

  const reviewColumns: AdminColumn<AdminTeacherFinancialEventDto>[] = [
    {
      key: 'occurredAt',
      label: 'التاريخ',
      responsivePriority: 'optional',
      render: (item) => <span className="font-mono text-xs text-[var(--admin-muted)]">{formatDate(item.occurredAt)}</span>,
    },
    {
      key: 'teacherName',
      label: 'المدرس',
      render: (item) => <span className="font-bold text-[var(--admin-text)]">{item.teacherName}</span>,
    },
    {
      key: 'contentNameSnapshot',
      label: 'المحتوى',
      render: (item) => (
        <div>
          <div className="font-bold text-[var(--admin-text)]">{item.contentNameSnapshot}</div>
          <div className="text-xs text-[var(--admin-muted)]">{item.sourceType} • {item.targetType}</div>
        </div>
      ),
    },
    {
      key: 'studentName',
      label: 'الطالب',
      responsivePriority: 'secondary',
      render: (item) => (
        <div>
          <div className="font-bold text-[var(--admin-text)]">{item.studentName || '—'}</div>
          <div className="font-mono text-xs text-[var(--admin-muted)]">{item.studentPhone || ''}</div>
        </div>
      ),
    },
    {
      key: 'paidAmount',
      label: 'القيمة',
      responsivePriority: 'secondary',
      render: (item) => (
        <div className="font-mono text-xs">
          <div className="font-black text-[var(--admin-text)]">{formatEGP(item.paidAmount)}</div>
          {item.promotionalAmount > 0 && <div className="text-[var(--admin-muted)]">خصم/مجاني: {formatEGP(item.promotionalAmount)}</div>}
        </div>
      ),
    },
    {
      key: 'teacherShareAmount',
      label: 'مستحق المدرس',
      render: (item) => <span className="font-mono text-sm font-black text-emerald-600">{formatEGP(item.teacherShareAmount)}</span>,
    },
    {
      key: 'reviewStatus',
      label: 'الحالة',
      render: (item) => <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-text)]">{item.reviewStatus}</span>,
    },
    {
      key: 'actions',
      label: 'الإجراء',
      align: 'left',
      render: (item) => item.reviewStatus === 'PendingReview' ? (
        <div className="flex justify-end gap-2">
          <button type="button" onClick={() => void handleReviewTeacherEvent(item.allocationId, 'Approved')} className="rounded-xl bg-emerald-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-emerald-700">
            اعتماد
          </button>
          <button type="button" onClick={() => void handleReviewTeacherEvent(item.allocationId, 'Rejected')} className="rounded-xl bg-rose-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-rose-700">
            رفض
          </button>
        </div>
      ) : null,
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
      <div className="mb-6 -mx-4 overflow-x-auto px-4 pb-1 [scrollbar-width:thin] sm:mx-0 sm:px-0">
        <div
          className="inline-flex min-w-max border-b border-[var(--admin-border)]"
          role="tablist"
          aria-label="أقسام العمليات المالية"
          onKeyDown={(event) => {
            if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
            event.preventDefault();
            const currentIndex = financeTabs.findIndex((tab) => tab.id === activeTab);
            const nextIndex = event.key === 'Home'
              ? 0
              : event.key === 'End'
                ? financeTabs.length - 1
                : (currentIndex + (event.key === 'ArrowLeft' ? 1 : -1) + financeTabs.length) % financeTabs.length;
            const nextTab = financeTabs[nextIndex];
            setActiveTab(nextTab.id);
            document.getElementById(`finance-tab-${nextTab.id}`)?.focus();
          }}
        >
          {financeTabs.map(({ id, label, compactLabel, Icon }) => {
            const isActive = activeTab === id;
            return (
              <button
                key={id}
                id={`finance-tab-${id}`}
                type="button"
                role="tab"
                aria-selected={isActive}
                aria-controls={`finance-panel-${id}`}
                tabIndex={isActive ? 0 : -1}
                onClick={() => setActiveTab(id)}
                className={`inline-flex min-h-11 shrink-0 items-center gap-2 border-b-2 px-3 py-2 text-sm font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 sm:px-5 ${
                  isActive
                    ? 'border-[var(--admin-primary)] text-[var(--admin-primary)]'
                    : 'border-transparent text-[var(--admin-muted)] hover:border-[var(--admin-border-strong)] hover:text-[var(--admin-text)]'
                }`}
                aria-label={label}
              >
                <Icon className="h-4 w-4" aria-hidden="true" />
                <span className="sm:hidden">{compactLabel}</span>
                <span className="hidden sm:inline">{label}</span>
              </button>
            );
          })}
        </div>
      </div>

      {/* Tab Contents: Payroll */}
      {activeTab === 'payroll' && (
        <div id="finance-panel-payroll" role="tabpanel" aria-labelledby="finance-tab-payroll">
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
        <div id="finance-panel-payouts" role="tabpanel" aria-labelledby="finance-tab-payouts">
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
                <option value="Pending">بانتظار قبول السحب</option>
                <option value="Approved">جاهزة للصرف</option>
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
        <div id="finance-panel-codes" role="tabpanel" aria-labelledby="finance-tab-codes">
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

      {activeTab === 'review' && (
        <div id="finance-panel-review" role="tabpanel" aria-labelledby="finance-tab-review">
          <div className="mb-6 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
            <h4 className="mb-3 text-sm font-black text-[var(--admin-text)]">مراجعة مستحقات المدرسين</h4>
            <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
              <div>
                <label className="mb-1 block text-xs font-bold text-[var(--admin-muted)]">حالة المراجعة</label>
                <select
                  value={reviewStatusFilter}
                  onChange={(e) => setReviewStatusFilter(e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
                >
                  <option value="PendingReview">معلقة</option>
                  <option value="Approved">معتمدة يدوياً</option>
                  <option value="AutoApproved">معتمدة تلقائياً</option>
                  <option value="Rejected">مرفوضة</option>
                  <option value="">كل الحالات</option>
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-bold text-[var(--admin-muted)]">المدرس</label>
                <select
                  value={reviewTeacherId}
                  onChange={(e) => setReviewTeacherId(e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
                >
                  <option value="">كل المدرسين</option>
                  {teachers.map((teacher) => (
                    <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>
                  ))}
                </select>
              </div>
              <div className="flex items-end justify-end">
                <button
                  onClick={() => void fetchTeacherFinancialEvents()}
                  disabled={reviewLoading}
                  className="rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-xs font-bold text-[var(--admin-primary-contrast)] hover:opacity-90 flex items-center gap-1"
                >
                  <RefreshCw className={`h-3.5 w-3.5 ${reviewLoading ? 'animate-spin' : ''}`} />
                  تحديث
                </button>
              </div>
            </div>
          </div>

          <AdminDataTable
            data={reviewData}
            columns={reviewColumns}
            loading={reviewLoading}
            rowKey={(item) => item.allocationId}
            emptyMessage="لا توجد بنود مالية مطابقة للفلاتر الحالية."
          />

          <div className="mt-6 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
            <h4 className="mb-3 text-sm font-black text-[var(--admin-text)]">تعويض يدوي صريح</h4>
            <p className="mb-4 text-xs font-bold text-[var(--admin-muted)]">
              يستخدم فقط لتعويض مدرس عن عملية مجانية/خصم كامل أو حالة خاصة. بدون هذا التسجيل لا تضاف مستحقات على العمليات ذات القيمة صفر.
            </p>
            <div className="grid grid-cols-1 gap-3 md:grid-cols-[1fr_160px_1fr_auto]">
              <select
                value={compensationTeacherId}
                onChange={(e) => setCompensationTeacherId(e.target.value)}
                className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
              >
                <option value="">اختر المدرس</option>
                {teachers.map((teacher) => (
                  <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>
                ))}
              </select>
              <input
                value={compensationAmount}
                onChange={(e) => setCompensationAmount(e.target.value)}
                inputMode="decimal"
                placeholder="المبلغ"
                className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
              />
              <input
                value={compensationReason}
                onChange={(e) => setCompensationReason(e.target.value)}
                placeholder="سبب التعويض"
                className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none"
              />
              <button
                type="button"
                onClick={() => void handleManualCompensation()}
                disabled={compensationSubmitting}
                className="rounded-xl bg-[var(--admin-primary)] px-5 py-2 text-xs font-bold text-[var(--admin-primary-contrast)] hover:opacity-90 disabled:opacity-60"
              >
                تسجيل
              </button>
            </div>
          </div>

          {reviewTotalCount > reviewPageSize && (
            <div className="mt-6 flex items-center justify-between border-t border-[var(--admin-border)] pt-4">
              <span className="text-xs font-semibold text-[var(--admin-muted)]">
                عرض {reviewData.length} من أصل {reviewTotalCount} بند
              </span>
              <div className="flex items-center gap-2">
                <button
                  disabled={reviewPage === 1 || reviewLoading}
                  onClick={() => setReviewPage((prev) => prev - 1)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-40"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
                <span className="px-3 font-mono text-sm font-bold">
                  صفحة {reviewPage} من {Math.ceil(reviewTotalCount / reviewPageSize)}
                </span>
                <button
                  disabled={reviewPage * reviewPageSize >= reviewTotalCount || reviewLoading}
                  onClick={() => setReviewPage((prev) => prev + 1)}
                  className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-40"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      <AdminConfirmationDialog
        open={confirmationAction !== null}
        onClose={() => setConfirmationAction(null)}
        onConfirm={() => handleConfirmAction()}
        title={confirmationAction?.type === 'approve-payroll' ? 'تأكيد اعتماد كشف المرتب' : 'تأكيد حذف التسوية'}
        consequence={confirmationAction?.type === 'approve-payroll'
          ? 'سيُعتمد كشف المرتب ويُقفل نهائياً، ولن تتمكن من إضافة تسويات أو تعديل بياناته بعد ذلك.'
          : 'سيُحذف هذا التعديل من كشف المرتب نهائياً، وقد يتغير صافي المبلغ المستحق للموظف.'}
        confirmLabel={confirmationAction?.type === 'approve-payroll' ? 'اعتماد وقفل الكشف' : 'حذف التسوية'}
        variant={confirmationAction?.type === 'delete-adjustment' ? 'danger' : 'primary'}
        isConfirming={isConfirmingAction}
      />

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
