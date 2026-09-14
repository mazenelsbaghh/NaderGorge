'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import {
  Wallet,
  ArrowRight,
  Copy,
  Upload,
  LoaderCircle,
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
  const [confirmationPhones, setConfirmationPhones] = useState<Record<string, string>>({});
  const [teachers, setTeachers] = useState<PublicTeacherDto[]>([]);
  const [teacherId, setTeacherId] = useState('');
  const [showPendingRequestDialog, setShowPendingRequestDialog] = useState(false);

  // Step 2 state
  const [rechargeData, setRechargeData] = useState<InitiateRechargeResponse | null>(null);
  const [senderPhone, setSenderPhone] = useState('');
  const [screenshot, setScreenshot] = useState<File | null>(null);
  const [screenshotPreview, setScreenshotPreview] = useState<string | null>(null);
  const [isPreparingScreenshot, setIsPreparingScreenshot] = useState(false);
  const [proofUploadProgress, setProofUploadProgress] = useState<number | null>(null);
  const [proofUploadError, setProofUploadError] = useState<string | null>(null);
  const [timeLeft, setTimeLeft] = useState<number>(3600); // one hour in seconds
  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const proofSubmissionInFlightRef = useRef(false);
  const screenshotPreparationIdRef = useRef(0);
  const screenshotInputRef = useRef<HTMLInputElement | null>(null);

  // Step 3 state
  const [isMatched, setIsMatched] = useState(false);
  const [outcomeMessage, setOutcomeMessage] = useState('');
  const [reviewCode, setReviewCode] = useState('');
  const [reviewState, setReviewState] = useState<'checking' | 'phone-confirmation' | 'approved' | 'manual' | 'rejected'>('manual');
  const [originalSenderPhone, setOriginalSenderPhone] = useState('');
  const [reviewTimeLeft, setReviewTimeLeft] = useState(RECHARGE_REVIEW_WINDOW_SECONDS);

  const clearScreenshotSelection = useCallback(() => {
    screenshotPreparationIdRef.current += 1;
    if (screenshotInputRef.current) {
      screenshotInputRef.current.value = '';
    }
    setScreenshot(null);
    setScreenshotPreview(null);
    setIsPreparingScreenshot(false);
    setProofUploadProgress(null);
    setProofUploadError(null);
  }, []);

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
        if (diff === 0 && !proofSubmissionInFlightRef.current) {
          clearScreenshotSelection();
          if (timerRef.current) {
            clearInterval(timerRef.current);
          }
          toast.error('انتهت صلاحية حجز المحفظة، يرجى البدء من جديد.');
          setStep(1);
          setRechargeData(null);
          void fetchRequests();
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
  }, [step, rechargeData, clearScreenshotSelection]);

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

        if (currentRequest?.requiresSenderPhoneConfirmation) {
          setIsMatched(false);
          setReviewState('phone-confirmation');
          setOriginalSenderPhone(currentRequest.originalSenderPhoneNumber || currentRequest.senderPhoneNumber);
          setSenderPhone(currentRequest.senderPhoneNumber);
          setOutcomeMessage('راجع رقم المحفظة المحول منها. يوجد تحويل قريب في 8 أرقام أو أكثر، ولن نضيف الرصيد قبل تأكيدك أو مراجعة الأدمن.');
          return;
        }

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
    if (pendingRequest) {
      setShowPendingRequestDialog(true);
      return;
    }
    if (amount <= 0) {
      toast.error('قيمة الشحن يجب أن تكون أكبر من صفر.');
      return;
    }
    if (!teacherId) {
      toast.error('اختر المدرس الذي تريد شحن رصيده.');
      return;
    }

    try {
      setLoading(true);
      const response = await rechargeService.initiate(amount, teacherId);
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
      const errors = err.response?.data?.errors as string[] | undefined;
      if (errors?.includes('PENDING_RECHARGE_REQUEST_EXISTS')) {
        await fetchRequests();
        setShowPendingRequestDialog(true);
      }
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
        e.target.value = '';
        return;
      }

      const preparationId = ++screenshotPreparationIdRef.current;
      setIsPreparingScreenshot(true);
      setProofUploadProgress(null);
      setProofUploadError(null);
      setScreenshot(null);
      setScreenshotPreview(null);
      try {
        // Normalize browser-supported images to a real WebP file. This prevents files
        // with misleading extensions or MIME types from reaching the server.
        const dataUrl = await compressImage(file, 1920, 1920, 0.9);
        const normalizedFile = new File(
          [await (await fetch(dataUrl)).blob()],
          renameFileToMatchBase64(file.name, dataUrl),
          { type: `image/${getExtensionFromBase64(dataUrl) === 'jpg' ? 'jpeg' : getExtensionFromBase64(dataUrl)}` }
        );
        if (preparationId !== screenshotPreparationIdRef.current) return;
        setScreenshot(normalizedFile);
        setScreenshotPreview(dataUrl);
      } catch {
        if (preparationId !== screenshotPreparationIdRef.current) return;
        setScreenshot(null);
        setScreenshotPreview(null);
        e.target.value = '';
        toast.error('تعذر قراءة هذه الصورة. إذا كانت من iPhone بصيغة HEIC، التقط Screenshot أو حوّلها إلى JPG.');
      } finally {
        if (preparationId === screenshotPreparationIdRef.current) {
          setIsPreparingScreenshot(false);
        }
      }
    }
  };

  const handleSubmitProof = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!rechargeData || proofSubmissionInFlightRef.current) return;

    if (isPreparingScreenshot) {
      toast('انتظر حتى يكتمل تجهيز الصورة قبل الإرسال.', { icon: '⏳' });
      return;
    }

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
      proofSubmissionInFlightRef.current = true;
      setLoading(true);
      setProofUploadError(null);
      setProofUploadProgress(0);
      const response = await rechargeService.submit(
        rechargeData.rechargeRequestId,
        normalizedSenderPhone,
        screenshot,
        { onUploadProgress: setProofUploadProgress },
      );

      if (response.success && response.data) {
        setIsMatched(response.data.isMatched);
        setReviewCode(response.data.reviewCode);
        setStep(3);
        void fetchRequests();
        if (response.data.requiresSenderPhoneConfirmation) {
          setReviewState('phone-confirmation');
          setOriginalSenderPhone(response.data.originalSenderPhoneNumber || normalizedSenderPhone);
          setOutcomeMessage(response.data.message);
          toast('راجع رقم المحفظة المحول منها وأكده مرة أخرى.', { icon: '⚠️' });
        } else if (response.data.isMatched) {
          setReviewState('approved');
          setOutcomeMessage(response.data.message || 'تمت الموافقة على الشحن وإضافة الرصيد لحسابك بنجاح.');
          refreshStudentBalance();
          toast.success(response.data.message || 'تم شحن رصيدك وتفعيله تلقائياً بنجاح! 🎉');
        } else {
          setReviewState('checking');
          setReviewTimeLeft(RECHARGE_REVIEW_WINDOW_SECONDS);
          setOutcomeMessage('جاري التأكد من وصول رسالة الشحن. انتظر لحظات قبل تحويل الطلب للمراجعة.');
          toast.success('تم استلام الإثبات وجاري التأكد من الشحن.');
        }
      } else {
        const message = response.message || 'تعذر رفع صورة إثبات التحويل.';
        setProofUploadError(message);
        toast.error(message);
      }
    } catch (error: unknown) {
      const message = (error as { response?: { data?: { message?: string } } }).response?.data?.message
        || 'لم نتلقَّ تأكيدًا بحفظ الصورة. تأكد من اتصال الإنترنت ثم أعد المحاولة.';
      setProofUploadError(message);
    } finally {
      proofSubmissionInFlightRef.current = false;
      setProofUploadProgress(null);
      setLoading(false);
    }
  };

  const confirmSenderPhone = async (requestId: string, value: string) => {
    if (proofSubmissionInFlightRef.current) return;

    const normalizedSenderPhone = normalizePhoneInput(value);
    if (!isValidEgyptianMobile(normalizedSenderPhone)) {
      toast.error('رقم الهاتف يجب أن يكون 11 رقم ويبدأ بـ 010 أو 011 أو 012 أو 015.');
      return;
    }

    try {
      proofSubmissionInFlightRef.current = true;
      setLoading(true);
      const response = await rechargeService.submit(
        requestId,
        normalizedSenderPhone,
        null,
        { confirmSenderPhone: true },
      );
      if (!response.success || !response.data) {
        toast.error(response.message || 'تعذر تأكيد رقم المحول.');
        return;
      }

      setSenderPhone(normalizedSenderPhone);
      setIsMatched(response.data.isMatched);
      setReviewCode(response.data.reviewCode);
      setReviewState(response.data.isMatched ? 'approved' : 'checking');
      setReviewTimeLeft(RECHARGE_REVIEW_WINDOW_SECONDS);
      setOutcomeMessage(response.data.message);
      await fetchRequests();
      if (response.data.isMatched) {
        refreshStudentBalance();
        toast.success('تم تأكيد الرقم ومطابقة التحويل وإضافة الرصيد.');
      } else {
        toast.success('تم تأكيد الرقم، والطلب مستمر في المراجعة.');
      }
    } catch {
      // The shared API client displays the server response once for this request.
    } finally {
      proofSubmissionInFlightRef.current = false;
      setLoading(false);
    }
  };

  const cancelActiveRecharge = async () => {
    if (!rechargeData || cancelling) return;
    setCancelling(true);
    try {
      await rechargeService.cancel(rechargeData.rechargeRequestId, 'ألغاه الطالب قبل رفع إثبات التحويل');
      toast.success('تم إلغاء الطلب، ويمكنك إنشاء طلب جديد الآن.');
      setStep(1);
      setRechargeData(null);
      setSenderPhone('');
      clearScreenshotSelection();
      await fetchRequests();
    } catch (error: unknown) {
      const message = (error as { response?: { data?: { message?: string } } }).response?.data?.message;
      toast.error(message ?? 'تعذر إلغاء الطلب.');
    } finally {
      setCancelling(false);
    }
  };

  const cancelPendingRecharge = async () => {
    if (!pendingRequest || cancelling) return;
    setCancelling(true);
    try {
      await rechargeService.cancel(pendingRequest.id, 'ألغاه الطالب لإنشاء طلب شحن جديد');
      toast.success('تم إلغاء الطلب، ويمكنك إنشاء طلب جديد الآن.');
      setShowPendingRequestDialog(false);
      await fetchRequests();
    } catch (error: unknown) {
      const message = (error as { response?: { data?: { message?: string } } }).response?.data?.message;
      toast.error(message ?? 'تعذر إلغاء الطلب.');
    } finally {
      setCancelling(false);
    }
  };

  const resumePendingRecharge = () => {
    if (!pendingRequest || pendingRequest.screenshotUrl || !pendingRequest.reservationExpiresAt) return;
    setRechargeData({
      rechargeRequestId: pendingRequest.id,
      reviewCode: pendingRequest.reviewCode,
      walletPhoneNumber: pendingRequest.walletPhoneNumber,
      walletLabel: pendingRequest.walletLabel,
      expirationTime: pendingRequest.reservationExpiresAt,
    });
    setAmount(pendingRequest.amount);
    setTeacherId(pendingRequest.teacherId ?? '');
    setReviewCode(pendingRequest.reviewCode);
    setSenderPhone(pendingRequest.senderPhoneNumber ?? '');
    clearScreenshotSelection();
    setShowPendingRequestDialog(false);
    setStep(2);
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

  const canSubmitProof = Boolean(screenshot)
    && !isPreparingScreenshot
    && isValidEgyptianMobile(normalizePhoneInput(senderPhone));
  const isUploadingProof = proofUploadProgress !== null;
  const displayedUploadProgress = proofUploadProgress ?? 0;
  const isConfirmingProofUpload = proofUploadProgress === 100;
  const pendingRequest = requests.find((request) => isPendingRechargeStatus(request.status));
  const hasPendingRequest = Boolean(pendingRequest);

  return (
    <div className="space-y-8 pb-10">

      {/* Hero Section */}
      <div className="group relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm transition-[color,background-color,border-color,opacity,transform,box-shadow] sm:p-8">
        <div className="absolute -left-20 -top-20 h-64 w-64 rounded-full bg-[var(--admin-primary-15)] blur-3xl transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-700 pointer-events-none" />
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
              <p className="text-sm font-semibold text-[var(--admin-muted)]">اختر المدرس أولًا، ثم أدخل القيمة التي تريد إضافتها إلى رصيدك لديه بالجنيه المصري.</p>
            </div>

            {hasPendingRequest && (
              <div className="space-y-3 rounded-2xl border border-amber-300 bg-amber-50 p-4 text-sm font-bold text-amber-900">
                <p>لديك طلب شحن معلق بالفعل. يجب حسمه أو إلغاؤه قبل إنشاء طلب جديد.</p>
                <button type="button" onClick={() => setShowPendingRequestDialog(true)} className="min-h-11 rounded-xl bg-amber-700 px-4 py-2 text-sm font-black text-white">
                  عرض الطلب المعلق
                </button>
              </div>
            )}

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
                    className={`rounded-xl border py-2.5 font-mono text-sm font-bold transition-[color,background-color,border-color,opacity,transform,box-shadow] ${
                      amount === val
                        ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-10)] text-[var(--admin-primary-strong)]'
                        : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                  >
                    {val} ج.م
                  </button>
                  ))}
              </div>
              <p className="text-sm font-semibold text-[var(--admin-muted)]">
                الأزرار اختصارات فقط. يمكنك كتابة أي رقم في خانة المبلغ.
              </p>
              <div className="space-y-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                <label htmlFor="recharge-teacher" className="block text-sm font-black text-[var(--admin-text)]">رصيد المدرس *</label>
                <select id="recharge-teacher" required value={teacherId} onChange={(event) => setTeacherId(event.target.value)} className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3 text-sm font-bold text-[var(--admin-text)] focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)]">
                  <option value="">اختر المدرس</option>
                  {teachers.map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>)}
                </select>
                <p className="text-sm font-semibold text-[var(--admin-muted)]">سيُستخدم الرصيد لشراء محتوى هذا المدرس فقط.</p>
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full flex items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] py-3.5 text-base font-black text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-15)] hover:brightness-110 active:scale-95 transition-[color,background-color,border-color,opacity,transform,box-shadow] disabled:opacity-50"
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
                <div className="text-sm font-semibold text-[var(--admin-muted)] leading-relaxed">
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
                  className="flex items-center gap-1 px-3 py-1.5 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] text-xs font-bold text-[var(--admin-muted)] hover:text-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow]"
                >
                  <Copy className="h-3.5 w-3.5" />
                  <span>نسخ الرقم</span>
                </button>
              </div>
              <div className="border-t border-[var(--admin-border)] pt-3 text-xs font-bold text-[var(--admin-text)] flex justify-between">
                <span>المبلغ المطلوب تحويله:</span>
                <span className="font-mono text-sm text-[var(--admin-primary)]">{amount} ج.م</span>
              </div>
              <div className="border-t border-[var(--admin-border)] pt-3 text-xs font-bold text-[var(--admin-text)] flex justify-between gap-3">
                <span>سيُضاف إلى رصيد:</span>
                <span className="truncate text-[var(--admin-primary)]">{teachers.find((teacher) => teacher.id === teacherId)?.fullName || 'المدرس المختار'}</span>
              </div>
            </div>

            {/* Form Inputs */}
            <div className="space-y-4">
              <div className="flex flex-col gap-2">
                <label htmlFor="recharge-sender-phone" className="text-sm font-bold text-[var(--admin-text)]">رقم الهاتف الذي قمت بالتحويل منه *</label>
                <input
                  id="recharge-sender-phone"
                  type="tel"
                  required
                  inputMode="numeric"
                  pattern="01[0125][0-9]{8}"
                  maxLength={11}
                  placeholder="مثال: 01098765432"
                  value={senderPhone}
                  onChange={(e) => setSenderPhone(normalizePhoneInput(e.target.value))}
                  aria-describedby="recharge-sender-phone-hint"
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3 font-mono text-sm font-bold text-[var(--admin-text)] focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)]"
                />
                <span id="recharge-sender-phone-hint" className="text-sm text-[var(--admin-muted)] font-semibold">
                  اكتب رقم المحفظة المحول منها كامل 11 رقم. الرقم الناقص لن تتم مطابقته.
                </span>
              </div>

              <div className="flex flex-col gap-2" aria-busy={isPreparingScreenshot || isUploadingProof}>
                <label className="text-sm font-bold text-[var(--admin-text)]">صورة إثبات التحويل (لقطة الشاشة) *</label>

                {isPreparingScreenshot ? (
                  <div
                    className="flex aspect-video flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed border-[var(--admin-primary)] bg-[var(--admin-primary-15)] px-6 text-center"
                    role="status"
                    aria-live="polite"
                  >
                    <LoaderCircle className="h-9 w-9 animate-spin text-[var(--admin-primary)]" aria-hidden="true" />
                    <div>
                      <p className="text-sm font-black text-[var(--admin-text)]">جارٍ تجهيز صورة التحويل…</p>
                      <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]">يتم تجهيز الصورة لتقليل حجمها وتسريع رفعها.</p>
                    </div>
                  </div>
                ) : screenshotPreview ? (
                  <div className="relative flex min-h-56 items-center justify-center overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)]">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={screenshotPreview}
                      alt="معاينة صورة إثبات التحويل المختارة"
                      className="max-h-[28rem] max-w-full object-contain"
                    />
                    <button
                      type="button"
                      onClick={clearScreenshotSelection}
                      disabled={isUploadingProof}
                      className="absolute top-2 right-2 min-h-11 rounded-lg bg-red-500/80 px-3 py-1 text-xs font-bold text-white transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:bg-red-500 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      حذف الصورة المختارة
                    </button>
                  </div>
                ) : (
                  <label
                    htmlFor="recharge-screenshot"
                    className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-8 transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:border-[var(--admin-primary)] hover:bg-[var(--admin-hover)] focus-within:ring-2 focus-within:ring-[var(--admin-primary)] focus-within:ring-offset-2"
                  >
                    <Upload className="h-8 w-8 text-[var(--admin-muted)]" />
                    <span className="text-sm font-bold text-[var(--admin-text)]">اختر صورة إثبات التحويل</span>
                    <span id="recharge-screenshot-hint" className="text-xs text-[var(--admin-muted)]">JPG أو PNG أو WEBP، بحد أقصى 10 ميجابايت</span>
                    <input
                      ref={screenshotInputRef}
                      id="recharge-screenshot"
                      type="file"
                      accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp"
                      required
                      onChange={handleFileChange}
                      aria-describedby="recharge-screenshot-hint"
                      className="sr-only"
                    />
                  </label>
                )}

                {screenshot && !isUploadingProof ? (
                  proofUploadError ? (
                    <div className="space-y-1.5 rounded-xl border border-rose-300 bg-rose-50 px-3 py-3 text-rose-950" role="alert">
                      <p className="text-xs font-black">تعذر تأكيد حفظ صورة التحويل.</p>
                      <p className="text-xs font-semibold leading-5">{proofUploadError}</p>
                      <p className="text-xs font-semibold leading-5">الصورة ما زالت مختارة. تحقّق من الإنترنت ثم أعد إرسالها إذا كان الطلب ما زال معلقًا.</p>
                    </div>
                  ) : (
                    <div className="flex items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 px-3 py-2.5 text-amber-950" role="status">
                      <CheckCircle className="mt-0.5 h-4 w-4 shrink-0 text-amber-700" aria-hidden="true" />
                      <p className="text-xs font-black leading-5">
                        تم اختيار الصورة فقط — اضغط «رفع الصورة وإرسال الطلب» لرفعها وحفظها على النظام.
                      </p>
                    </div>
                  )
                ) : null}

                {isUploadingProof ? (
                  <div className="space-y-2 rounded-xl border border-[var(--admin-primary)] bg-[var(--admin-primary-15)] px-3 py-3">
                    <p className="sr-only" role="status">
                      {isConfirmingProofUpload ? 'تم إرسال الصورة، جارٍ تأكيد حفظها.' : 'جارٍ رفع صورة التحويل.'}
                    </p>
                    <div className="flex items-center justify-between gap-3 text-xs font-black text-[var(--admin-text)]">
                      <span className="flex items-center gap-2">
                        <LoaderCircle className="h-4 w-4 animate-spin text-[var(--admin-primary)]" aria-hidden="true" />
                        {isConfirmingProofUpload
                          ? 'تم إرسال الصورة، جارٍ تأكيد حفظها…'
                          : 'جارٍ رفع صورة التحويل…'}
                      </span>
                      {!isConfirmingProofUpload ? <bdi dir="ltr" className="font-mono text-[var(--admin-primary)]">{displayedUploadProgress}%</bdi> : null}
                    </div>
                    {isConfirmingProofUpload ? (
                      <div className="h-2 overflow-hidden rounded-full bg-[var(--admin-border)]" aria-label="جارٍ تأكيد حفظ صورة التحويل">
                        <div className="h-full w-2/5 animate-pulse rounded-full bg-[var(--admin-primary)]" />
                      </div>
                    ) : (
                      <div
                        className="h-2 overflow-hidden rounded-full bg-[var(--admin-border)]"
                        role="progressbar"
                        aria-label="تقدم رفع صورة التحويل"
                        aria-valuemin={0}
                        aria-valuemax={100}
                        aria-valuenow={displayedUploadProgress}
                      >
                        <div
                          className="h-full origin-right rounded-full bg-[var(--admin-primary)] transition-transform duration-300 ease-out"
                          style={{ transform: `scaleX(${displayedUploadProgress / 100})` }}
                        />
                      </div>
                    )}
                    <p className="text-xs font-semibold text-[var(--admin-muted)]">ابقَ في هذه الصفحة حتى يظهر تأكيد استلام الطلب.</p>
                  </div>
                ) : null}
              </div>
            </div>

            {/* Actions */}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 sm:gap-4">
              <button
                type="button"
                onClick={() => void cancelActiveRecharge()}
                disabled={loading || cancelling}
                className="order-2 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] py-3 text-sm font-bold text-[var(--admin-muted)] transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:bg-[var(--admin-hover)] active:scale-95 disabled:opacity-50 sm:order-1"
              >
                {cancelling ? 'جارٍ الإلغاء...' : 'إلغاء الطلب والعودة'}
              </button>
              <button
                type="submit"
                disabled={loading || isPreparingScreenshot || !canSubmitProof}
                className="order-1 flex w-full items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-15)] transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:brightness-110 active:scale-95 disabled:opacity-50 sm:order-2"
              >
                {loading ? (
                  <>
                    <LoaderCircle className="h-5 w-5 animate-spin" aria-hidden="true" />
                    <span>{isConfirmingProofUpload ? 'جارٍ تأكيد الحفظ…' : 'جارٍ رفع الصورة…'}</span>
                  </>
                ) : (
                  <span>{proofUploadError ? 'إعادة رفع الصورة وإرسال الطلب' : 'رفع الصورة وإرسال الطلب'}</span>
                )}
              </button>
            </div>
            {!canSubmitProof && !isPreparingScreenshot && !isUploadingProof && (
              <p className="text-center text-xs font-bold text-amber-700 dark:text-amber-300">
                لن يُحفظ إثبات التحويل إلا بعد كتابة رقم المُحوِّل الصحيح والضغط على «رفع الصورة وإرسال الطلب».
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
              ) : reviewState === 'phone-confirmation' || reviewState === 'rejected' ? (
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
                    : reviewState === 'phone-confirmation'
                      ? 'أكد رقم المحفظة المحول منها'
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
              {reviewState === 'phone-confirmation' && rechargeData && (
                <div className="mx-auto max-w-sm space-y-3 rounded-2xl border border-amber-300 bg-amber-50 p-4 text-right text-amber-950">
                  <p className="text-sm font-black">الرقم الذي كتبته أول مرة:</p>
                  <p className="rounded-xl bg-white px-3 py-2 text-center font-mono text-lg font-black" dir="ltr">{originalSenderPhone}</p>
                  <label className="block text-sm font-bold">
                    اكتب الرقم الصحيح أو اتركه كما هو لتأكيد أنه صحيح
                    <input type="tel" inputMode="numeric" value={senderPhone} onChange={(event) => setSenderPhone(normalizePhoneInput(event.target.value))} className="mt-2 w-full rounded-xl border border-amber-300 bg-white px-4 py-3 text-center font-mono font-black" dir="ltr" />
                  </label>
                  <button type="button" disabled={loading || !isValidEgyptianMobile(senderPhone)} onClick={() => void confirmSenderPhone(rechargeData.rechargeRequestId, senderPhone)} className="w-full rounded-xl bg-amber-600 px-4 py-3 text-sm font-black text-white disabled:opacity-50">
                    {loading ? 'جارٍ التأكيد...' : 'تأكيد الرقم وإعادة المطابقة'}
                  </button>
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
                className="w-full py-3 rounded-xl bg-[var(--admin-primary)] text-sm font-bold text-[var(--admin-primary-contrast)] hover:brightness-110 active:scale-95 transition-[color,background-color,border-color,opacity,transform,box-shadow] block text-center"
              >
                الذهاب للمحفظة لرؤية الرصيد
              </Link>
              <button
                onClick={() => {
                  setStep(1);
                  setRechargeData(null);
                  setSenderPhone('');
                  clearScreenshotSelection();
                  setReviewState('manual');
                  setReviewTimeLeft(RECHARGE_REVIEW_WINDOW_SECONDS);
                }}
                className="w-full py-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] active:scale-95 transition-[color,background-color,border-color,opacity,transform,box-shadow]"
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
                    {request.requiresSenderPhoneConfirmation ? <div className="mt-2 space-y-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-amber-950"><p className="font-black">راجع رقم المحفظة المحول منها. الرقم المكتوب أول مرة: <span className="font-mono" dir="ltr">{request.originalSenderPhoneNumber || request.senderPhoneNumber}</span></p><input type="tel" inputMode="numeric" value={confirmationPhones[request.id] ?? request.senderPhoneNumber} onChange={(event) => setConfirmationPhones((current) => ({ ...current, [request.id]: normalizePhoneInput(event.target.value) }))} className="admin-input w-full text-center font-mono" dir="ltr" /><button type="button" disabled={loading} onClick={() => void confirmSenderPhone(request.id, confirmationPhones[request.id] ?? request.senderPhoneNumber)} className="rounded-lg bg-amber-600 px-3 py-2 font-black text-white disabled:opacity-50">تأكيد الرقم وإعادة المطابقة</button></div> : null}
                    {isPendingRechargeStatus(request.status) && cancelRequestId !== request.id ? <button type="button" onClick={() => { setCancelRequestId(request.id); setCancellationReason(''); }} className="mt-2 w-fit text-xs font-black text-rose-600 underline">إلغاء الطلب</button> : null}
                    {cancelRequestId === request.id ? <div className="mt-2 space-y-2 rounded-xl border border-rose-200 bg-rose-50 p-3"><label className="block font-black text-rose-700">سبب الإلغاء<textarea value={cancellationReason} onChange={(event) => setCancellationReason(event.target.value)} className="admin-input mt-1 min-h-20 w-full" maxLength={500} autoFocus /></label><div className="flex gap-2"><button type="button" disabled={cancelling} onClick={() => void cancelRechargeRequest()} className="rounded-lg bg-rose-600 px-3 py-2 font-black text-white">تأكيد الإلغاء</button><button type="button" disabled={cancelling} onClick={() => { setCancelRequestId(null); setCancellationReason(''); }} className="rounded-lg border px-3 py-2 font-black">رجوع</button></div></div> : null}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {showPendingRequestDialog && pendingRequest ? (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-slate-950/55 p-3 sm:items-center" role="dialog" aria-modal="true" aria-labelledby="pending-recharge-title">
          <div className="w-full max-w-md space-y-5 rounded-2xl border border-amber-300 bg-[var(--admin-card)] p-5 shadow-2xl sm:p-6">
            <div className="space-y-2">
              <h2 id="pending-recharge-title" className="text-xl font-black text-[var(--admin-text)]">يوجد طلب شحن معلق</h2>
              <p className="text-sm font-semibold leading-6 text-[var(--admin-muted)]">{pendingRequest.screenshotUrl ? 'تم رفع الإثبات والطلب ظاهر للإدارة وتحت المراجعة. يمكنك الانتظار أو إلغاء الطلب.' : 'هذا حجز لم يكتمل رفع إثباته بعد. استكمل نفس الطلب بدل إلغائه وإنشاء طلب جديد.'}</p>
            </div>
            <dl className="grid grid-cols-2 gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 text-sm">
              <div><dt className="font-bold text-[var(--admin-muted)]">المبلغ</dt><dd className="mt-1 font-mono font-black text-[var(--admin-text)]">{pendingRequest.amount} ج.م</dd></div>
              <div><dt className="font-bold text-[var(--admin-muted)]">كود المراجعة</dt><dd className="mt-1 font-mono font-black text-[var(--admin-primary)]">{pendingRequest.reviewCode}</dd></div>
              <div className="col-span-2"><dt className="font-bold text-[var(--admin-muted)]">المحفظة</dt><dd className="mt-1 font-black text-[var(--admin-text)]">{pendingRequest.walletLabel}</dd></div>
              <div className="col-span-2"><dt className="font-bold text-[var(--admin-muted)]">نوع الرصيد</dt><dd className="mt-1 font-black text-[var(--admin-text)]">{pendingRequest.teacherName ? `للأستاذ ${pendingRequest.teacherName}` : 'عام'}</dd></div>
            </dl>
            {!pendingRequest.screenshotUrl ? (
              <button type="button" disabled={cancelling || !pendingRequest.walletPhoneNumber} onClick={resumePendingRecharge} className="min-h-11 w-full rounded-xl bg-[var(--admin-primary)] px-4 py-3 text-sm font-black text-white disabled:opacity-50">
                استكمال الطلب ورفع الإثبات
              </button>
            ) : null}
            <div className="grid grid-cols-2 gap-3">
              <button type="button" disabled={cancelling} onClick={() => void cancelPendingRecharge()} className="min-h-11 rounded-xl bg-rose-600 px-4 py-3 text-sm font-black text-white disabled:opacity-50">
                {cancelling ? 'جارٍ الإلغاء...' : 'إلغاء الطلب'}
              </button>
              <button type="button" disabled={cancelling} onClick={() => setShowPendingRequestDialog(false)} className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm font-black text-[var(--admin-text)] disabled:opacity-50">
                رجوع
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
