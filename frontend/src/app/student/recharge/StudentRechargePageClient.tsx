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
import { rechargeService, type InitiateRechargeResponse } from '@/services/recharge-service';
import type { StudentRechargeRequestDto } from '@/services/recharge-service';
import toast from 'react-hot-toast';

export default function StudentRechargePageClient() {
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [amount, setAmount] = useState<number>(100);
  const [loading, setLoading] = useState(false);
  const [requests, setRequests] = useState<StudentRechargeRequestDto[]>([]);

  // Step 2 state
  const [rechargeData, setRechargeData] = useState<InitiateRechargeResponse | null>(null);
  const [senderPhone, setSenderPhone] = useState('');
  const [screenshot, setScreenshot] = useState<File | null>(null);
  const [screenshotPreview, setScreenshotPreview] = useState<string | null>(null);
  const [timeLeft, setTimeLeft] = useState<number>(1200); // 20 minutes in seconds
  const timerRef = useRef<NodeJS.Timeout | null>(null);

  // Step 3 state
  const [isMatched, setIsMatched] = useState(false);
  const [outcomeMessage, setOutcomeMessage] = useState('');
  const [reviewCode, setReviewCode] = useState('');
  const [reviewState, setReviewState] = useState<'checking' | 'approved' | 'manual' | 'rejected'>('manual');
  const [reviewTimeLeft, setReviewTimeLeft] = useState(60);

  const fetchRequests = async () => {
    try {
      setRequests(await rechargeService.getMyRequests());
    } catch {
      setRequests([]);
    }
  };

  useEffect(() => {
    void fetchRequests();
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
      const remainingSeconds = Math.max(0, 60 - elapsedSeconds);
      setReviewTimeLeft(remainingSeconds);

      try {
        const latestRequests = await rechargeService.getMyRequests();
        if (!isActive) return;

        setRequests(latestRequests);
        const currentRequest = latestRequests.find((request) => request.id === requestId);

        if (currentRequest?.status === 1 || currentRequest?.status === 2) {
          setIsMatched(true);
          setReviewState('approved');
          setOutcomeMessage('تمت الموافقة على الشحن وإضافة الرصيد لحسابك بنجاح.');
          toast.success('تمت الموافقة على الشحن وإضافة الرصيد.');
          return;
        }

        if (currentRequest?.status === 3) {
          setIsMatched(false);
          setReviewState('rejected');
          setOutcomeMessage(currentRequest.rejectionReason || 'تم رفض طلب الشحن. راجع بيانات التحويل أو تواصل مع الدعم.');
          return;
        }
      } catch {
        // Keep checking until the one-minute window ends.
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

    try {
      setLoading(true);
      const response = await rechargeService.initiate(amount);
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

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > 10 * 1024 * 1024) {
        toast.error('حجم الصورة يجب أن لا يتجاوز 10 ميجابايت.');
        return;
      }
      setScreenshot(file);
      setScreenshotPreview(URL.createObjectURL(file));
    }
  };

  const handleSubmitProof = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!rechargeData) return;

    if (!senderPhone.trim()) {
      toast.error('يرجى كتابة رقم الهاتف الذي قمت بالتحويل منه.');
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
        senderPhone.trim(),
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
          toast.success('تم شحن رصيدك وتفعيله تلقائياً بنجاح! 🎉');
        } else {
          setReviewState('checking');
          setReviewTimeLeft(60);
          setOutcomeMessage('جاري التأكد من وصول رسالة الشحن. انتظر لحظات قبل تحويل الطلب للمراجعة.');
          toast.success('تم استلام الإثبات وجاري التأكد من الشحن.');
        }
      } else {
        toast.error(response.message || 'تعذر تقديم طلب الشحن.');
      }
    } catch (err: any) {
      console.error(err);
      toast.error(err.response?.data?.message || 'فشل في رفع إثبات الشحن.');
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
                  type="text" 
                  required
                  placeholder="مثال: 01098765432"
                  value={senderPhone}
                  onChange={(e) => setSenderPhone(e.target.value)}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-3 font-mono text-sm font-bold text-[var(--admin-text)] focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)]"
                />
                <span className="text-[11px] text-[var(--admin-muted)] font-semibold">
                  ملاحظة: يجب كتابة الرقم بدقة لمطابقة الرسائل الواردة منه على السيرفر.
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
                    <span className="text-xs text-[var(--admin-muted)]">صورة صالحة لا تتعدى 10 ميجابايت</span>
                    <input 
                      type="file" 
                      accept="image/*"
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
                disabled={loading}
                className="w-full flex items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-15)] hover:brightness-110 active:scale-95 transition-all disabled:opacity-50"
              >
                {loading ? (
                  <span className="h-5 w-5 animate-spin border-2 border-white border-t-transparent rounded-full" />
                ) : (
                  <span>تأكيد التحويل وإرسال</span>
                )}
              </button>
            </div>
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
                  <span className="font-mono text-lg font-black">{reviewTimeLeft} ثانية</span>
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
                  setReviewTimeLeft(60);
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
              const statusLabel = request.status === 0 ? 'قيد المراجعة' : request.status === 1 ? 'تمت المطابقة' : request.status === 2 ? 'مقبول' : request.status === 3 ? 'مرفوض' : 'منتهي';
              const statusClass = request.status === 3
                ? 'bg-rose-500/10 text-rose-600'
                : request.status === 0
                  ? 'bg-amber-500/10 text-amber-600'
                  : 'bg-emerald-500/10 text-emerald-600';
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
                    {request.rejectionReason ? <span className="text-rose-600">سبب الرفض: {request.rejectionReason}</span> : null}
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
