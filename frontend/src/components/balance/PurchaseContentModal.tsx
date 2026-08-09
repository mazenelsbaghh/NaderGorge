'use client';

import { useEffect, useRef, useState } from 'react';
import { Wallet, AlertTriangle, CheckCircle, BadgePercent, X } from 'lucide-react';
import { InlineLoader } from '@/components/ui/loading-indicator';
import { balanceService, CodeType, PurchaseFundingPreviewDto } from '@/services/balance-service';
import { useRouter } from 'next/navigation';
import { invalidateMany } from '@/lib/cache-invalidation';

export interface PurchaseContentModalProps {
  isOpen: boolean;
  onClose: () => void;
  onPurchaseSuccess?: () => void | Promise<void>;
  contentType: CodeType;
  contentId: string;
  contentName: string;
  price: number;
}

export function PurchaseContentModal({
  isOpen,
  onClose,
  onPurchaseSuccess,
  contentType,
  contentId,
  contentName,
  price,
}: PurchaseContentModalProps) {
  const router = useRouter();
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const [preview, setPreview] = useState<PurchaseFundingPreviewDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [purchasing, setPurchasing] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [couponCode, setCouponCode] = useState('');
  const [appliedCouponCode, setAppliedCouponCode] = useState('');

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
      setLoading(true);
      setError('');
      setSuccess(false);
      setCouponCode('');
      setAppliedCouponCode('');
      balanceService.getPurchasePreview(contentType, contentId)
        .then(data => setPreview(data))
        .catch((err: unknown) => {
          const message = err instanceof Error ? err.message : 'تعذر تحميل رصيد المحفظة حالياً';
          setError(message);
        })
        .finally(() => setLoading(false));
    }

    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen, contentType, contentId, price]);

  useEffect(() => {
    if (!isOpen) return;

    closeButtonRef.current?.focus();

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !purchasing) {
        onClose();
      }
    };

    window.addEventListener('keydown', handleEscape);
    return () => window.removeEventListener('keydown', handleEscape);
  }, [isOpen, onClose, purchasing]);

  if (!isOpen) return null;

  const currentBalance = preview?.currentPaidBalance || 0;
  const isFree = price === 0;
  const isSufficient = isFree || preview?.isSufficient === true;
  const requiredRechargeAmount = Math.max(0, (preview?.paidAmountToUse ?? price) - currentBalance);
  const trimmedCouponCode = couponCode.trim();
  const couponInputChanged = trimmedCouponCode !== appliedCouponCode;
  const hasDiscount = (preview?.couponDiscountAmount ?? 0) > 0 || (preview?.printableCodeDiscountAmount ?? 0) > 0;
  const basePrice = preview?.price ?? price;
  const discountedPrice = preview?.discountedPrice ?? price;
  const displayPrice = discountedPrice;
  const hasVisiblePriceReduction = displayPrice < basePrice;

  const refreshPreview = async (nextCouponCode: string) => {
    setLoading(true);
    setError('');
    try {
      const couponCodes = nextCouponCode ? [nextCouponCode] : undefined;
      const data = await balanceService.getPurchasePreview(contentType, contentId, { couponCodes });
      setPreview(data);
      setAppliedCouponCode(nextCouponCode);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'تعذر تطبيق كود الخصم';
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  const handleApplyCoupon = async () => {
    await refreshPreview(trimmedCouponCode);
  };

  const handleRemoveCoupon = async () => {
    setCouponCode('');
    await refreshPreview('');
  };

  const handlePurchase = async () => {
    try {
      setPurchasing(true);
      setError('');
      const couponCodes = appliedCouponCode ? [appliedCouponCode] : undefined;
      await balanceService.purchaseContent(contentType, contentId, { couponCodes });
      setSuccess(true);
      setTimeout(() => {
        invalidateMany(['student:shell', 'student:balance', 'content:packages']);
        void onPurchaseSuccess?.();
        onClose();
      }, 1500);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'فشل في إتمام عملية الشراء';
      setError(message);
    } finally {
      setPurchasing(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center bg-[var(--admin-text)]/20 p-4" role="presentation">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="purchase-modal-title"
        aria-describedby="purchase-modal-description"
        className="max-h-[min(90dvh,42rem)] w-full max-w-md overflow-y-auto rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-2xl animate-in zoom-in-95 fade-in duration-200 sm:p-6"
        dir="rtl"
      >
        {success ? (
          <div role="status" aria-live="polite" className="flex flex-col items-center justify-center py-8 text-center animate-in fade-in slide-in-from-bottom-4">
            <div className="mb-4 rounded-full bg-[var(--admin-success-10)] p-4 text-[var(--admin-success)]">
              <CheckCircle className="h-10 w-10" />
            </div>
            <h3 id="purchase-modal-title" className="mb-2 text-2xl font-black text-[var(--admin-text)]">{isFree ? 'تم التفعيل بنجاح!' : 'تم الشراء بنجاح!'}</h3>
            <p className="font-medium text-[var(--admin-muted)]">{isFree ? 'تم تفعيل المحتوى في حسابك.' : 'تمت العملية بنجاح!'}</p>
          </div>
        ) : (
          <>
            <div className="mb-6 flex items-start justify-between gap-4">
              <h3 id="purchase-modal-title" className="text-xl font-extrabold text-[var(--admin-text)]">{isFree ? 'تأكيد التفعيل' : 'تأكيد الشراء'}</h3>
              <button
                ref={closeButtonRef}
                onClick={onClose}
                type="button"
                className="rounded-full bg-[var(--admin-card-soft)] p-2 text-[var(--admin-muted)] transition hover:bg-[var(--admin-card-strong)] hover:text-[var(--admin-text)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
                aria-label="إغلاق نافذة الشراء"
              >
                ✕
              </button>
            </div>

            <div className="space-y-6">
              <div id="purchase-modal-description" className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 text-center">
                <p className="mb-2 text-sm font-bold uppercase tracking-wider text-[var(--admin-muted)]">المحتوى المطلوب</p>
                <p className="text-lg font-black text-[var(--admin-text)]">{contentName}</p>
                <div className="mt-3 inline-flex flex-wrap items-center justify-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-4 py-1.5 text-xl font-black text-[var(--admin-primary)]">
                  {isFree ? 'مجاني' : (
                    <>
                      {hasVisiblePriceReduction ? (
                        <span className="text-sm font-bold text-[var(--admin-muted)] line-through">{basePrice} ج.م</span>
                      ) : null}
                      <span>{displayPrice} ج.م</span>
                    </>
                  )}
                </div>
              </div>

              {isFree ? (
                <div className="rounded-2xl border border-emerald-200 dark:border-emerald-800 bg-emerald-50 dark:bg-emerald-900/30 p-4 text-center">
                  <div className="flex items-center justify-center gap-2 text-sm font-bold text-emerald-600 dark:text-emerald-400">
                    <CheckCircle className="h-4 w-4 shrink-0" />
                    <p>هذا المحتوى مجاني ولا يحتاج رصيد. اضغط تفعيل للبدء مباشرة.</p>
                  </div>
                </div>
              ) : loading ? (
                <div className="flex items-center justify-center py-4">
                  <InlineLoader className="text-[var(--admin-primary)]" />
                </div>
              ) : (
                <div className="space-y-3">
                  <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                    <label htmlFor="purchase-coupon-code" className="mb-2 flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
                      <BadgePercent className="h-4 w-4 text-[var(--admin-primary)]" />
                      كود الخصم
                    </label>
                    <div className="flex flex-col gap-2 sm:flex-row">
                      <input
                        id="purchase-coupon-code"
                        value={couponCode}
                        onChange={(event) => setCouponCode(event.target.value)}
                        disabled={purchasing || loading}
                        placeholder="اكتب الكود هنا"
                        className="min-h-11 flex-1 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] outline-none transition placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary)]/20 disabled:opacity-70"
                        dir="ltr"
                      />
                      <div className="flex gap-2">
                        <button
                          type="button"
                          onClick={handleApplyCoupon}
                          disabled={purchasing || loading || !trimmedCouponCode || !couponInputChanged}
                          className="inline-flex min-h-11 flex-1 items-center justify-center rounded-xl bg-[var(--admin-primary)] px-4 py-2 text-sm font-black text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] disabled:opacity-60 sm:flex-none"
                        >
                          تطبيق
                        </button>
                        {appliedCouponCode ? (
                          <button
                            type="button"
                            onClick={handleRemoveCoupon}
                            disabled={purchasing || loading}
                            className="inline-flex h-11 w-11 items-center justify-center rounded-xl bg-[var(--admin-card-strong)] text-[var(--admin-muted)] transition hover:text-[var(--admin-text)] disabled:opacity-60"
                            aria-label="إزالة كود الخصم"
                          >
                            <X className="h-4 w-4" />
                          </button>
                        ) : null}
                      </div>
                    </div>
                    {appliedCouponCode && hasDiscount ? (
                      <p className="mt-2 text-xs font-bold text-[var(--admin-success)]">تم تطبيق الكود {appliedCouponCode}</p>
                    ) : appliedCouponCode ? (
                      <p className="mt-2 text-xs font-bold text-amber-700 dark:text-amber-300">تم فحص الكود لكنه لم يغيّر سعر هذا المحتوى.</p>
                    ) : couponInputChanged && trimmedCouponCode ? (
                      <p className="mt-2 text-xs font-bold text-amber-700 dark:text-amber-300">اضغط تطبيق لحساب الخصم قبل الشراء.</p>
                    ) : null}
                  </div>

                  <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                    <div className="mb-2 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                      <span className="text-sm font-bold text-[var(--admin-muted)]">الرصيد العام للمنصة</span>
                      <span className="flex items-center gap-1.5 font-mono text-sm font-bold text-[var(--admin-text)]">
                        <Wallet className="h-4 w-4 text-[var(--admin-primary)]" />
                        {currentBalance} ج.م
                      </span>
                    </div>
                    {hasDiscount || hasVisiblePriceReduction ? (
                      <div className="mt-3 grid gap-2 rounded-xl border border-[var(--admin-primary)]/20 bg-[var(--admin-primary)]/10 px-3 py-2 text-sm font-bold text-[var(--admin-text)]">
                        <div className="flex items-center justify-between">
                          <span>السعر قبل الخصم</span>
                          <span>{basePrice} ج.م</span>
                        </div>
                        {(preview?.couponDiscountAmount ?? 0) > 0 ? (
                          <div className="flex items-center justify-between text-[var(--admin-success)]">
                            <span>خصم الكوبون</span>
                            <span>-{preview?.couponDiscountAmount ?? 0} ج.م</span>
                          </div>
                        ) : null}
                        {(preview?.promotionalAmountToUse ?? 0) > 0 ? (
                          <div className="flex items-center justify-between text-[var(--admin-success)]">
                            <span>رصيد مخصص</span>
                            <span>-{preview?.promotionalAmountToUse} ج.م</span>
                          </div>
                        ) : null}
                        <div className="flex items-center justify-between border-t border-[var(--admin-border)] pt-2">
                          <span>السعر بعد الخصم</span>
                          <span>{discountedPrice} ج.م</span>
                        </div>
                      </div>
                    ) : null}
                    {(preview?.promotionalAmountToUse ?? 0) > 0 ? (
                      <div className="mt-3 flex items-center justify-between rounded-lg border border-emerald-500/20 bg-emerald-500/10 px-3 py-2 text-sm font-bold text-emerald-700 dark:text-emerald-300">
                        <span>رصيد مخصص مؤهل لهذا المحتوى</span>
                        <span>{preview?.promotionalAmountToUse} ج.م</span>
                      </div>
                    ) : null}
                    {!isFree ? (
                      <div className="mt-2 flex items-center justify-between px-1 text-xs font-bold text-[var(--admin-muted)]">
                        <span>المطلوب من الرصيد العام</span>
                        <span>{preview?.paidAmountToUse ?? price} ج.م</span>
                      </div>
                    ) : null}

                    {!isSufficient ? (
                       <div className="mt-4 flex items-start gap-2 rounded-xl bg-[var(--admin-danger-10)] p-3 text-sm font-bold text-[var(--admin-danger)]">
                         <AlertTriangle className="h-4 w-4 mt-0.5 shrink-0" />
                         <p>الرصيد المؤهل لهذا المدرس أو الرصيد العام غير كافٍ. يرجى شحن الرصيد المناسب للمتابعة.</p>
                       </div>
                    ) : (
                       <div className="mt-4 flex items-center gap-2 rounded-xl bg-[var(--admin-success-10)] p-3 text-sm font-bold text-[var(--admin-success)]">
                         <CheckCircle className="h-4 w-4 shrink-0" />
                         <p>رصيدك يكفي لإتمام هذه العملية.</p>
                       </div>
                    )}
                    </div>
                </div>
              )}

              {error && (
                <div role="alert" className="rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-3 text-center text-sm font-bold text-[var(--admin-danger)]">
                  {error}
                </div>
              )}

              <div className="flex flex-col gap-3 sm:flex-row">
                <button
                  onClick={onClose}
                  disabled={purchasing}
                  type="button"
                  className="inline-flex min-h-12 flex-1 items-center justify-center rounded-full bg-[var(--admin-card-soft)] px-4 py-3 text-sm font-bold text-[var(--admin-muted)] transition hover:bg-[var(--admin-card-strong)] hover:text-[var(--admin-text)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
                >
                  إلغاء
                </button>
                {isSufficient ? (
                  <button
                    onClick={handlePurchase}
                    disabled={purchasing || loading || (!!trimmedCouponCode && couponInputChanged)}
                    type="button"
                    className="inline-flex min-h-12 flex-[2] items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-4 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow-lg transition hover:bg-[var(--admin-primary-strong)] disabled:opacity-70 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
                  >
                    {purchasing && <InlineLoader />}
                    <span>{isFree ? 'تفعيل مجاني' : 'تأكيد الخصم والشراء'}</span>
                  </button>
                ) : (
                  <button
                    onClick={() => router.push(`/student/recharge?amount=${encodeURIComponent(requiredRechargeAmount.toFixed(2))}`)}
                    disabled={purchasing || loading}
                    type="button"
                    className="inline-flex min-h-12 flex-[2] items-center justify-center rounded-full bg-[var(--admin-card-strong)] px-4 py-3 text-sm font-black text-[var(--admin-text)] shadow-sm transition hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] disabled:opacity-70 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
                  >
                    شحن ورفع الإثبات
                  </button>
                )}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
