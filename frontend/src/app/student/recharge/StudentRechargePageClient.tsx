'use client';

import { useState, useEffect, useRef } from 'react';
import {
  Wallet,
  ArrowRight,
  Copy,
  Upload,
  CheckCircle,
  Clock,
  ChevronLeft
} from 'lucide-react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { rechargeService, type InitiateRechargeResponse } from '@/services/recharge-service';
import type { StudentRechargeRequestDto } from '@/services/recharge-service';
import { studentService, type PublicTeacherDto } from '@/services/student-service';
import toast from 'react-hot-toast';
import { compressImage, getExtensionFromBase64, renameFileToMatchBase64 } from '@/utils/image-compressor';

const isApprovedRechargeStatus = (status: StudentRechargeRequestDto['status']) =>
  status === 1 || status === 2 || status === 'Matched' || status === 'Approved';

const isRejectedRechargeStatus = (status: StudentRechargeRequestDto['status']) =>
  status === 3 || status === 'Rejected';

const isPendingRechargeStatus = (status: StudentRechargeRequestDto['status']) =>
  status === 0 || status === 'Pending';

const normalizePhoneInput = (value: string) => value.replace(/\D/g, '').slice(0, 11);

const isValidEgyptianMobile = (value: string) => /^01[0125]\d{8}$/.test(value);

const RECHARGE_REVIEW_WINDOW_SECONDS = 60 * 60;

const getRechargeStatusLabel = (status: StudentRechargeRequestDto['status']) => {
  if (status === 0 || status === 'Pending') return 'قيد المراجعة';
  if (status === 1 || status === 'Matched') return 'تمت المطابقة';
  if (status === 2 || status === 'Approved') return 'مقبول';
  if (status === 3 || status === 'Rejected') return 'مرفوض';
  if (status === 5 || status === 'Cancelled') return 'ملغي';
  return 'منتهي';
};

const getRechargeStatusClass = (status: StudentRechargeRequestDto['status']) => {
  if (isRejectedRechargeStatus(status)) return 'bg-rose-500/10 text-rose-600';
  if (status === 5 || status === 'Cancelled') return 'bg-slate-500/10 text-slate-600';
  if (isPendingRechargeStatus(status)) return 'bg-amber-500/10 text-amber-600';
  return 'bg-emerald-500/10 text-emerald-600';
};

const refreshStudentBalance = () => {
  window.dispatchEvent(new Event('refresh-student-balance'));
};

export default function StudentRechargePageClient() {
  const searchParams = useSearchParams();
  const requestedAmount = Number(searchParams.get('amount'));
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [amount, setAmount] = useState<number>(() => (
    Number.isFinite(requestedAmount) && requestedAmount > 0 ? Number(requestedAmount.toFixed(2)) : 100
  ));
  const [loading, setLoading] = useState(false);
  const [requests, setRequests] = useState<StudentRechargeRequestDto[]>([]);
  const [cancelRequestId, setCancelRequestId] = useState<string | null>(null);
  const [cancellationReason, setCancellationReason] = useState('');
  const [cancelling, setCancelling] = useState(false);
  const [teachers, setTeachers] = useState<PublicTeacherDto[]>([]);
  const [balanceScope, setBalanceScope] = useState<'general' | 'teacher'>('general');
  const [teacherId, setTeacherId] = useState('');

  // Step 2 state
  const [rechargeData, setRechargeData] = useState<InitiateRechargeResponse | null>(null);
  const [senderPhone, setSenderPhone] = useState('');
  const [screenshot, setScreenshot] = useState<File | null>(null);
  const [screenshotPreview, setScreenshotPreview] = useState<string | null>(null);
  const [timeLeft, setTimeLeft] = useState<number>(3600); // one hour in seconds
  const timerRef = useRef<NodeJS.Timeout | null>(null);

  // Step 3 state
  const [isMatched, setIsMatched] = useState(false);
  const [outcomeMessage, setOutcomeMessage] = useState('');
  const [reviewCode, setReviewCode] = useState('');
  const [reviewState, setReviewState] = useState<'checking' | 'approved' | 'manual' | 'rejected'>('manual');
  const [reviewTimeLeft, setReviewTimeLeft] = useState(RECHARGE_REVIEW_WINDOW_SECONDS);

  const fetchRequests = async () => {
    try {
      setRequests(await rechargeService.getMyRequests());
    } catch {
      setRequests([]);
    }
  };

  const cancelRechargeRequest = async () => {
    if (!cancelRequestId || cancellationReason.trim().length < 3) {
      toast.error('اكتب سبب الإلغاء.');
      return;
    }
    setCancelling(true);
    try {
      const response = await rechargeService.cancel(cancelRequestId, cancellationReason.trim());
      toast.success(response.message || 'تم إلغاء طلب الشحن.');
      setCancelRequestId(null);
      setCancellationReason('');
      await fetchRequests();
    } catch (error: unknown) {
      const message = (error as { response?: { data?: { message?: string } } }).response?.data?.message;
      toast.error(message ?? 'تعذر إلغاء الطلب.');
    } finally { setCancelling(false); }
  };

  useEffect(() => {
    void fetchRequests();
    void studentService.getPublicTeachers().then(setTeachers).catch(() => setTeachers([]));
  }, []);

  useEffect(() => {
    if (step === 2 && rechargeData) {
      // Calculate remaining seconds
      const expiry = new Date(rechargeData.expirationTime).getTime();
      const calculateTimeLeft = () => {
        const diff = Math.max(0, Math.floor((expiry - Date.now()) / 1000));
        setTimeLeft(diff);
        if (diff === 0 && timerRef.current) {
          clearInterval(timerRef.current);
          toast.error('انتهت صلاحية حجز المحفظة، يرجى البدء من جديد.');
          setStep(1);
          setRechargeData(null);
        }
      };

      calculateTimeLeft();
      timerRef.current = setInterval(calculateTimeLeft, 1000);
    }

    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
    };
  }, [step, rechargeData]);

  useEffect(() => {
    if (step !== 3 || reviewState !== 'checking' || !rechargeData) return;

    let isActive = true;
    const startedAt = Date.now();
    const requestId = rechargeData.rechargeRequestId;

    const checkRequestStatus = async () => {
      const elapsedSeconds = Math.floor((Date.now() - startedAt) / 1000);
      const remainingSeconds = Math.max(0, RECHARGE_REVIEW_WINDOW_SECONDS - elapsedSeconds);
      setReviewTimeLeft(remainingSeconds);

      try {
        const latestRequests = await rechargeService.getMyRequests();
        if (!isActive) return;

        setRequests(latestRequests);
        const currentRequest = latestRequests.find((request) => request.id === requestId);

        if (currentRequest && isApprovedRechargeStatus(currentRequest.status)) {
          setIsMatched(true);
          setReviewState('approved');
          setOutcomeMessage('تمت الموافقة على الشحن وإضافة الرصيد لحسابك بنجاح.');
          refreshStudentBalance();
          toast.success('تمت الموافقة على الشحن وإضافة الرصيد.');
          return;
        }

        if (currentRequest && isRejectedRechargeStatus(currentRequest.status)) {
          setIsMatched(false);
          setReviewState('rejected');
          setOutcomeMessage(currentRequest.rejectionReason || 'تم رفض طلب الشحن. راجع بيانات التحويل أو تواصل مع الدعم.');
          return;
        }
      } catch {
        // Keep the waiting state; the request will fall back to manual review after the timeout.
      }

      if (remainingSeconds === 0) {
        setIsMatched(false);
        setReviewState('manual');
        setOutcomeMessage('طلبك تحت المراجعة الآن. سنطابق التحويل تلقائياً عند وصول رسالة المحفظة أو يراجعه الأدمن.');
      }
    };

    void checkRequestStatus();
    const intervalId = window.setInterval(() => {
      void checkRequestStatus();
    }, 3000);

    return () => {
      isActive = false;
      window.clearInterval(intervalId);
    };
  }, [step, reviewState, rechargeData]);

  const handleInitiate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (amount <= 0) {
      toast.error('قيمة الشحن يجب أن تكون أكبر من صفر.');
      return;
    }
    if (balanceScope === 'teacher' && !teacherId) {
      toast.error('اختر المدرس الذي تريد شحن رصيده.');
      return;
    }

    try {
      setLoading(true);
      const response = await rechargeService.initiate(amount, balanceScope === 'teacher' ? teacherId : undefined);
      if (response.success && response.data) {
        setRechargeData(response.data);
        setReviewCode(response.data.reviewCode);
        setStep(2);
        toast.success(response.message);
      } else {
        toast.error(response.message || 'تعذر بدء عملية الشحن.');
      }
    } catch (err: any) {
      console.error(err);
      toast.error(err.response?.data?.message || 'تعذر بدء عملية الشحن. يرجى المحاولة لاحقاً.');
    } finally {
      setLoading(false);
    }
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.type.startsWith('image/')) {
        toast.error('اختر صورة صحيحة لإثبات التحويل.');
        e.target.value = '';
        return;
      }
      if (file.size > 10 * 1024 * 1024) {
        toast.error('حجم الصورة يجب أن لا يتجاوز 10 ميجابايت.');
        return;
      }
      try {
        // Normalize browser-supported images to a real WebP file. This prevents files
        // with misleading extensions or MIME types from reaching the server.
        const dataUrl = await compressImage(file, 1920, 1920, 0.9);
        const normalizedFile = new File(
          [await (await fetch(dataUrl)).blob()],
          renameFileToMatchBase64(file.name, dataUrl),
          { type: `image/${getExtensionFromBase64(dataUrl) === 'jpg' ? 'jpeg' : getExtensionFromBase64(dataUrl)}` }
        );
        setScreenshot(normalizedFile);
        setScreenshotPreview(dataUrl);
      } catch {
        setScreenshot(null);
        setScreenshotPreview(null);
        e.target.value = '';
        toast.error('تعذر قراءة هذه الصورة. إذا كانت من iPhone بصيغة HEIC، التقط Screenshot أو حوّلها إلى JPG.');
      }
    }
  };

  const handleSubmitProof = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!rechargeData) return;

    const normalizedSenderPhone = normalizePhoneInput(senderPhone);

    if (!normalizedSenderPhone) {
      toast.error('يرجى كتابة رقم الهاتف الذي قمت بالتحويل منه.');
      return;
    }

    if (!isValidEgyptianMobile(normalizedSenderPhone)) {
      toast.error('رقم الهاتف يجب أن يكون 11 رقم ويبدأ بـ 010 أو 011 أو 012 أو 015.');
      return;
    }

    if (!screenshot) {
      toast.error('يرجى رفع صورة إثبات التحويل.');
      return;
    }

    try {
      setLoading(true);
      const response = await rechargeService.submit(
        rechargeData.rechargeRequestId,
        normalizedSenderPhone,
        screenshot
      );

      if (response.success && response.data) {
        setIsMatched(response.data.isMatched);
        setReviewCode(response.data.reviewCode);
        setStep(3);
        void fetchRequests();
        if (response.data.isMatched) {
          setReviewState('approved');
          setOutcomeMessage(response.data.message || 'تمت الموافقة على الشحن وإضافة الرصيد لحسابك بنجاح.');
          refreshStudentBalance();
          toast.success('تم شحن رصيدك وتفعيله تلقائياً بنجاح! 🎉');
        } else {
          setReviewState('checking');
          setReviewTimeLeft(RECHARGE_REVIEW_WINDOW_SECONDS);
          setOutcomeMessage('جاري التأكد من وصول رسالة الشحن. انتظر لحظات قبل تحويل الطلب للمراجعة.');
          toast.success('تم استلام الإثبات وجاري التأكد من الشحن.');
        }
      } else {
        toast.error(response.message || 'تعذر تقديم طلب الشحن.');
      }
    } catch {
      // The shared API client already displays server errors; avoid duplicate toasts.
    } finally {
      setLoading(false);
    }
  };

  const handleCopyNumber = (num: string) => {
    navigator.clipboard.writeText(num);
    toast.success('تم نسخ رقم المحفظة.');
  };

  const formatTimer = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  const canSubmitProof = Boolean(screenshot) && isValidEgyptianMobile(normalizePhoneInput(senderPhone));

  return (
    <div className="space-y-8 pb-10">

      {/* Hero Section */}
      <div className="group relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm transition-all sm:p-8">
        <div className="absolute -left-20 -top-20 h-64 w-64 rounded-full bg-[var(--admin-primary-15)] blur-[48px] transition-all duration-700 pointer-events-none" />
        <div className="relative z-10 flex flex-col items-start gap-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-3">
            <Link
              href="/student/balance"
              className="inline-flex items-center gap-1.5 text-xs font-bold text-[var(--admin-primary-strong)] hover:underline"
            >
              <ArrowRight className="h-3.5 w-3.5" />
              <span>العودة للمحفظة</span>
            </Link>
            <h1 className="text-3xl font-black text-[var(--admin-text)]">شحن الرصيد بالتحويل الرقمي</h1>
            <p className="max-w-xl text-[var(--admin-muted)] text-sm leading-relaxed font-medium">
              اشحن رصيد حسابك فوراً عن طريق تحويل كاش (فودافون كاش، اتصالات، أورانج) من أي رقم، ثم ارفع إثبات التحويل ليتم مطابقة عمليتك تلقائياً وبسرعة.
            </p>
          </div>
          <div className="relative flex h-16 w-16 items-center justify-center rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] shadow-md">
            <Wallet className="h-7 w-7 text-[var(--admin-primary)]" />
          </div>
        </div>
      </div>

      {/* Steps Indicator */}
      <div className="flex items-center justify-center gap-2 max-w-md mx-auto">
        <div className={`flex flex-col items-center gap-2 ${step >= 1 ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-muted)]'}`}>
          <div className={`h-8 w-8 rounded-full border flex items-center justify-center font-bold text-sm ${
            step === 1 ? 'bg-[var(--admin-primary)] text-white border-[var(--admin-primary)]' : 'bg-transparent border-[var(--admin-border)]'
          }`}>1</div>
          <span className="text-xs font-bold">تحديد المبلغ</span>
        </div>
        <div className="h-[1px] w-12 bg-[var(--admin-border)] mb-6" />
        <div className={`flex flex-col items-center gap-2 ${step >= 2 ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-muted)]'}`}>
          <div className={`h-8 w-8 rounded-full border flex items-center justify-center font-bold text-sm ${
            step === 2 ? 'bg-[var(--admin-primary)] text-white border-[var(--admin-primary)]' : 'bg-transparent border-[var(--admin-border)]'
          }`}>2</div>
          <span className="text-xs font-bold">التحويل ورفع الإثبات</span>
        </div>
        <div className="h-[1px] w-12 bg-[var(--admin-border)] mb-6" />
        <div className={`flex flex-col items-center gap-2 ${step >= 3 ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-muted)]'}`}>
          <div className={`h-8 w-8 rounded-full border flex items-center justify-center font-bold text-sm ${
            step === 3 ? 'bg-[var(--admin-primary)] text-white border-[var(--admin-primary)]' : 'bg-transparent border-[var(--admin-border)]'
          }`}>3</div>
          <span className="text-xs font-bold">اكتمال الشحن</span>
        </div>
      </div>

      {/* Main Content */}
      <div className="max-w-xl mx-auto rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm sm:p-8">

        {/* STEP 1: INITIATE RECHARGE */}
        {step === 1 && (
          <form onSubmit={handleInitiate} className="space-y-6">
            <div className="space-y-2">
              <h2 className="text-xl font-black text-[var(--admin-text)]">حدد قيمة الشحن المطلوبة</h2>
              <p className="text-sm font-semibold text-[var(--admin-muted)]">أدخل القيمة التي ترغب في تحويلها لمحفظة المنصة بالجنيه المصري.</p>
            </div>

            <div className="space-y-4">
              <div className="flex flex-col gap-2">
                <label className="text-sm font-bold text-[var(--admin-text)]">المبلغ المطلوب شحنه (ج.م) *</label>
                <div className="relative">
                  <input
                    type="number"
                    required
                    min="1"
                    step="1"
                    placeholder="مثال: 150"
                    value={amount || ''}
                    onChange={(e) => setAmount(Number(e.target.value))}
                    className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3 font-mono text-lg font-bold text-[var(--admin-text)] focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)] text-center"
                  />
                  <span className="absolute right-4 top-1/2 -translate-y-1/2 font-bold text-sm text-[var(--admin-muted)]">ج.م</span>
                </div>
              </div>

              {/* Predefined Amounts */}
              <div className="grid grid-cols-4 gap-2">
                {[50, 100, 200, 500].map((val) => (
                  <button
                    key={val}
                    type="button"
                    onClick={() => setAmount(val)}
                    className={`rounded-xl border py-2.5 font-mono text-sm font-bold transition-all ${
                      amount === val
                        ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-10)] text-[var(--admin-primary-strong)]'
                        : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                  >
                    {val} ج.م
                  </button>
                  ))}
              </div>
              <p className="text-[11px] font-semibold text-[var(--admin-muted)]">
                الأزرار اختصارات فقط. يمكنك كتابة أي رقم في خانة المبلغ.
              </p>
              <fieldset className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                <legend className="px-1 text-sm font-black text-[var(--admin-text)]">نوع الرصيد</legend>
                <div className="grid grid-cols-2 gap-2">
                  <button type="button" onClick={() => setBalanceScope('general')} className={`rounded-xl border p-3 text-right transition-all ${balanceScope === 'general' ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-10)] text-[var(--admin-primary-strong)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'}`}>
                    <span className="block text-sm font-black">رصيد عام</span><span className="mt-1 block text-[11px] font-semibold">يُستخدم مع أي مدرس</span>
                  </button>
                  <button type="button" onClick={() => setBalanceScope('teacher')} className={`rounded-xl border p-3 text-right transition-all ${balanceScope === 'teacher' ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-10)] text-[var(--admin-primary-strong)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'}`}>
                    <span className="block text-sm font-black">رصيد مدرس</span><span className="mt-1 block text-[11px] font-semibold">لشراء محتوى مدرس واحد</span>
                  </button>
                </div>
                {balanceScope === 'teacher' && (
                  <select required value={teacherId} onChange={(event) => setTeacherId(event.target.value)} className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3 text-sm font-bold text-[var(--admin-text)] focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)]">
                    <option value="">اختر المدرس</option>
                    {teachers.map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>)}
                  </select>
                )}
              </fieldset>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full flex items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] py-3.5 text-base font-black text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-15)] hover:brightness-110 active:scale-95 transition-all disabled:opacity-50"
            >
              {loading ? (
                <span className="h-5 w-5 animate-spin border-2 border-white border-t-transparent rounded-full" />
              ) : (
                <>
                  <span>الذهاب لخطوة الدفع</span>
                  <ChevronLeft className="h-5 w-5" />
                </>
              )}
            </button>
          </form>
        )}

        {/* STEP 2: SUBMIT TRANSACTION PROOF */}
        {step === 2 && rechargeData && (
          <form onSubmit={handleSubmitProof} className="space-y-6">
            <div className="bg-amber-500/10 border border-amber-500/30 text-[var(--admin-primary-strong)] rounded-2xl p-4 flex items-start gap-3">
              <Clock className="h-5 w-5 shrink-0 mt-0.5 animate-pulse text-amber-500" />
              <div className="space-y-1">
                <div className="font-bold text-sm">قم بالتحويل قبل انتهاء المهلة:</div>
                <div className="font-black text-2xl font-mono tracking-wider text-amber-600 dark:text-amber-500">
                  {formatTimer(timeLeft)}
                </div>
                <div className="text-[11px] font-semibold text-[var(--admin-muted)] leading-relaxed">
                  يتم حجز المحفظة مؤقتاً لتفادي تخطي حدود الاستقبال. كود المراجعة: <span className="font-mono font-black text-[var(--admin-text)]">{reviewCode}</span>
                </div>
              </div>
            </div>

            {/* Target Wallet Card */}
            <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 space-y-4">
              <div className="text-xs font-bold text-[var(--admin-muted)] tracking-wider uppercase">حول المبلغ إلى المحفظة التالية:</div>
              <div className="flex items-center justify-between">
                <div>
                  <div className="font-black text-xl text-[var(--admin-text)] tracking-wider font-mono">
                    {rechargeData.walletPhoneNumber}
                  </div>
                  <div className="text-xs font-bold text-[var(--admin-primary-strong)] mt-0.5">
                    {rechargeData.walletLabel}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => handleCopyNumber(rechargeData.walletPhoneNumber)}
                  className="flex items-center gap-1 px-3 py-1.5 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] text-xs font-bold text-[var(--admin-muted)] hover:text-[var(--admin-primary)] transition-all"
                >
                  <Copy className="h-3.5 w-3.5" />
                  <span>نسخ الرقم</span>
                </button>
              </div>
              <div className="border-t border-[var(--admin-border)] pt-3 text-xs font-bold text-[var(--admin-text)] flex justify-between">
                <span>المبلغ المطلوب تحويله:</span>
                <span className="font-mono text-sm text-[var(--admin-primary)]">{amount} ج.م</span>
              </div>
            </div>

            {/* Form Inputs */}
            <div className="space-y-4">
              <div className="flex flex-col gap-2">
                <label className="text-sm font-bold text-[var(--admin-text)]">رقم الهاتف الذي قمت بالتحويل منه *</label>
                <input
                  type="tel"
                  required
                  inputMode="numeric"
                  pattern="01[0125][0-9]{8}"
                  maxLength={11}
                  placeholder="مثال: 01098765432"
                  value={senderPhone}
                  onChange={(e) => setSenderPhone(normalizePhoneInput(e.target.value))}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3 font-mono text-sm font-bold text-[var(--admin-text)] focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)]"
                />
                <span className="text-[11px] text-[var(--admin-muted)] font-semibold">
                  اكتب رقم المحفظة المحول منها كامل 11 رقم. الرقم الناقص لن تتم مطابقته.
                </span>
              </div>

              <div className="flex flex-col gap-2">
                <label className="text-sm font-bold text-[var(--admin-text)]">صورة إثبات التحويل (لقطة الشاشة) *</label>

                {screenshotPreview ? (
                  <div className="relative rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] overflow-hidden aspect-video flex items-center justify-center">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={screenshotPreview}
                      alt="Screenshot Preview"
                      className="max-h-full object-contain"
                    />
                    <button
                      type="button"
                      onClick={() => {
                        setScreenshot(null);
                        setScreenshotPreview(null);
                      }}
                      className="absolute top-2 right-2 bg-red-500/80 hover:bg-red-500 text-white rounded-lg px-2.5 py-1 text-xs font-bold transition-all"
                    >
                      إلغاء الصورة
                    </button>
                  </div>
                ) : (
                  <label className="border-2 border-dashed border-[var(--admin-border)] hover:border-[var(--admin-primary)] rounded-2xl p-8 flex flex-col items-center justify-center gap-2 cursor-pointer transition-all bg-[var(--admin-card-soft)] hover:bg-[var(--admin-hover)]">
                    <Upload className="h-8 w-8 text-[var(--admin-muted)]" />
                    <span className="font-bold text-sm text-[var(--admin-text)]">اضغط لرفع لقطة الشاشة</span>
                    <span className="text-xs text-[var(--admin-muted)]">JPG أو PNG أو WEBP، بحد أقصى 10 ميجابايت</span>
                    <input
                      type="file"
                      accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp"
                      required
                      onChange={handleFileChange}
                      className="hidden"
                    />
                  </label>
                )}
              </div>
            </div>

            {/* Actions */}
            <div className="grid grid-cols-2 gap-4">
              <button
                type="button"
                onClick={() => {
                  if (confirm('هل أنت متأكد من إلغاء هذه المعاملة والعودة؟')) {
                    setStep(1);
                    setRechargeData(null);
                  }
                }}
                disabled={loading}
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] py-3 text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] active:scale-95 transition-all disabled:opacity-50"
              >
                إلغاء وتغيير القيمة
              </button>
              <button
                type="submit"
                disabled={loading || !canSubmitProof}
                className="w-full flex items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-15)] hover:brightness-110 active:scale-95 transition-all disabled:opacity-50"
              >
                {loading ? (
                  <span className="h-5 w-5 animate-spin border-2 border-white border-t-transparent rounded-full" />
                ) : (
                  <span>تأكيد التحويل وإرسال</span>
                )}
              </button>
            </div>
            {!canSubmitProof && (
              <p className="text-center text-xs font-bold text-amber-700 dark:text-amber-300">
                لن يتم إرسال الطلب للأدمن قبل كتابة رقم المُحوِّل الصحيح ورفع صورة إثبات التحويل.
              </p>
            )}
          </form>
        )}

        {/* STEP 3: OUTCOME */}
        {step === 3 && (
          <div className="space-y-6 text-center py-4">
            <div className="flex justify-center">
              {isMatched ? (
                <div className="h-20 w-20 rounded-full bg-emerald-500/10 flex items-center justify-center text-emerald-500">
                  <CheckCircle className="h-14 w-14" />
                </div>
              ) : reviewState === 'checking' ? (
                <div className="h-20 w-20 rounded-full bg-sky-500/10 flex items-center justify-center text-sky-500">
                  <Clock className="h-14 w-14 animate-spin" />
                </div>
              ) : reviewState === 'rejected' ? (
                <div className="h-20 w-20 rounded-full bg-rose-500/10 flex items-center justify-center text-rose-500">
                  <Clock className="h-14 w-14" />
                </div>
              ) : (
                <div className="h-20 w-20 rounded-full bg-amber-500/10 flex items-center justify-center text-amber-500">
                  <Clock className="h-14 w-14 animate-pulse" />
                </div>
              )}
            </div>

            <div className="space-y-2">
              <h2 className="text-2xl font-black text-[var(--admin-text)]">
                {isMatched
                  ? 'تمت الموافقة على الشحن!'
                  : reviewState === 'checking'
                    ? 'جاري التأكد من الشحن'
                    : reviewState === 'rejected'
                      ? 'تم رفض طلب الشحن'
                      : 'الطلب تحت المراجعة'}
              </h2>
              <p className="text-sm font-semibold text-[var(--admin-muted)] leading-relaxed max-w-md mx-auto">
                {outcomeMessage}
              </p>
              {reviewState === 'checking' && (
                <div className="mx-auto flex max-w-sm flex-col gap-2 rounded-2xl border border-sky-500/20 bg-sky-500/10 p-3 text-sm font-bold text-sky-700 dark:text-sky-300">
                  <span>ننتظر وصول رسالة المحفظة ومطابقتها تلقائياً.</span>
                  <span className="font-mono text-lg font-black" dir="ltr">{formatTimer(reviewTimeLeft)}</span>
                </div>
              )}
              {reviewCode && (
                <div className="mx-auto inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-sm font-black text-[var(--admin-text)]">
                  <span>كود المراجعة</span>
                  <span className="font-mono text-[var(--admin-primary)]">{reviewCode}</span>
                </div>
              )}
            </div>

            <div className="pt-4 border-t border-[var(--admin-border)] flex flex-col gap-2">
              <Link
                href="/student/balance"
                className="w-full py-3 rounded-xl bg-[var(--admin-primary)] text-sm font-bold text-[var(--admin-primary-contrast)] hover:brightness-110 active:scale-95 transition-all block text-center"
              >
                الذهاب للمحفظة لرؤية الرصيد
              </Link>
              <button
                onClick={() => {
                  setStep(1);
                  setRechargeData(null);
                  setSenderPhone('');
                  setScreenshot(null);
                  setScreenshotPreview(null);
                  setReviewState('manual');
                  setReviewTimeLeft(RECHARGE_REVIEW_WINDOW_SECONDS);
                }}
                className="w-full py-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] active:scale-95 transition-all"
              >
                شحن عملية جديدة
              </button>
            </div>
          </div>
        )}

      </div>

      <div className="mx-auto max-w-xl rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
        <h2 className="mb-4 text-lg font-black text-[var(--admin-text)]">طلباتي الأخيرة للشحن</h2>
        {requests.length === 0 ? (
          <p className="py-6 text-center text-sm font-semibold text-[var(--admin-muted)]">لا توجد طلبات شحن سابقة.</p>
        ) : (
          <div className="space-y-3">
            {requests.map((request) => {
              const statusLabel = getRechargeStatusLabel(request.status);
              const statusClass = getRechargeStatusClass(request.status);
              return (
                <div key={request.id} className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <p className="font-mono text-base font-black text-[var(--admin-text)]">{request.amount} ج.م</p>
                      <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">كود المراجعة: <span className="font-mono text-[var(--admin-primary)]">{request.reviewCode}</span></p>
                    </div>
                    <span className={`rounded-full px-3 py-1 text-xs font-black ${statusClass}`}>{statusLabel}</span>
                  </div>
                  <div className="mt-3 grid gap-1 text-xs font-semibold text-[var(--admin-muted)]">
                    <span>من: <span className="font-mono">{request.senderPhoneNumber || 'لم يرسل بعد'}</span></span>
                    <span>إلى: {request.walletLabel} <span className="font-mono">{request.walletPhoneNumber}</span></span>
                    <span>نوع الرصيد: {request.teacherName ? `للأستاذ ${request.teacherName}` : 'عام'}</span>
                    {request.rejectionReason ? <span className="text-rose-600">{request.status === 5 || request.status === 'Cancelled' ? 'سبب الإلغاء' : 'سبب الرفض'}: {request.rejectionReason}</span> : null}
                    {isPendingRechargeStatus(request.status) && cancelRequestId !== request.id ? <button type="button" onClick={() => { setCancelRequestId(request.id); setCancellationReason(''); }} className="mt-2 w-fit text-xs font-black text-rose-600 underline">إلغاء الطلب</button> : null}
                    {cancelRequestId === request.id ? <div className="mt-2 space-y-2 rounded-xl border border-rose-200 bg-rose-50 p-3"><label className="block font-black text-rose-700">سبب الإلغاء<textarea value={cancellationReason} onChange={(event) => setCancellationReason(event.target.value)} className="admin-input mt-1 min-h-20 w-full" maxLength={500} autoFocus /></label><div className="flex gap-2"><button type="button" disabled={cancelling} onClick={() => void cancelRechargeRequest()} className="rounded-lg bg-rose-600 px-3 py-2 font-black text-white">تأكيد الإلغاء</button><button type="button" disabled={cancelling} onClick={() => { setCancelRequestId(null); setCancellationReason(''); }} className="rounded-lg border px-3 py-2 font-black">رجوع</button></div></div> : null}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
