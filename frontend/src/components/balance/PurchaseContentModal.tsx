'use client';

import { useEffect, useRef, useState } from 'react';
import { Wallet, AlertTriangle, CheckCircle } from 'lucide-react';
import { InlineLoader } from '@/components/ui/loading-indicator';
import { balanceService, CodeType, StudentBalanceDto } from '@/services/balance-service';
import { useRouter } from 'next/navigation';

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
  const [balanceDto, setBalanceDto] = useState<StudentBalanceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [purchasing, setPurchasing] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
      setLoading(true);
      setError('');
      setSuccess(false);
      balanceService.getBalance()
        .then(data => setBalanceDto(data))
        .catch((err: unknown) => {
          const message = err instanceof Error ? err.message : 'تعذر تحميل رصيد المحفظة حالياً';
          setError(message);
        })
        .finally(() => setLoading(false));
    }

    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen]);

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

  const currentBalance = balanceDto?.currentBalance || 0;
  const isSufficient = currentBalance >= price;

  const handlePurchase = async () => {
    try {
      setPurchasing(true);
      setError('');
      await balanceService.purchaseContent(contentType, contentId);
      setSuccess(true);
      setTimeout(() => {
        void onPurchaseSuccess?.();
        onClose();
        router.refresh();
      }, 2000);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'فشل في إتمام عملية الشراء';
      setError(message);
    } finally {
      setPurchasing(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-[var(--admin-text)]/20 p-4 backdrop-blur-sm" role="presentation">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="purchase-modal-title"
        aria-describedby="purchase-modal-description"
        className="max-h-[min(90dvh,42rem)] w-full max-w-md overflow-y-auto rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-2xl animate-in zoom-in-95 fade-in duration-200 sm:p-6"
        dir="rtl"
      >
        {success ? (
          <div role="status" aria-live="polite" className="flex flex-col items-center justify-center py-8 text-center animate-in fade-in slide-in-from-bottom-4">
            <div className="mb-4 rounded-full bg-[var(--admin-success-10)] p-4 text-[var(--admin-success)]">
              <CheckCircle className="h-10 w-10" />
            </div>
            <h3 id="purchase-modal-title" className="mb-2 text-2xl font-black text-[var(--admin-text)]">تم الشراء بنجاح!</h3>
            <p className="font-medium text-[var(--admin-muted)]">سيتم تحديث الصفحة الآن...</p>
          </div>
        ) : (
          <>
            <div className="mb-6 flex items-start justify-between gap-4">
              <h3 id="purchase-modal-title" className="text-xl font-extrabold text-[var(--admin-text)]">تأكيد الشراء</h3>
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
                <div className="mt-3 inline-block rounded-full bg-[var(--admin-primary-15)] px-4 py-1.5 text-xl font-black text-[var(--admin-primary)]">
                  {price} ج.م
                </div>
              </div>

              {loading ? (
                <div className="flex items-center justify-center py-4">
                  <InlineLoader className="text-[var(--admin-primary)]" />
                </div>
              ) : (
                <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                  <div className="mb-2 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                    <span className="text-sm font-bold text-[var(--admin-muted)]">رصيد محفظتك</span>
                    <span className="flex items-center gap-1.5 font-mono text-sm font-bold text-[var(--admin-text)]">
                      <Wallet className="h-4 w-4 text-[var(--admin-primary)]" />
                      {currentBalance} ج.م
                    </span>
                  </div>
                  
                  {!isSufficient ? (
                     <div className="mt-4 flex items-start gap-2 rounded-xl bg-[var(--admin-danger-10)] p-3 text-sm font-bold text-[var(--admin-danger)]">
                       <AlertTriangle className="h-4 w-4 mt-0.5 shrink-0" />
                       <p>رصيدك غير كافٍ. يرجى شحن رصيدك للمتابعة.</p>
                     </div>
                  ) : (
                     <div className="mt-4 flex items-center gap-2 rounded-xl bg-[var(--admin-success-10)] p-3 text-sm font-bold text-[var(--admin-success)]">
                       <CheckCircle className="h-4 w-4 shrink-0" />
                       <p>رصيدك يكفي لإتمام هذه العملية.</p>
                     </div>
                  )}
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
                    disabled={purchasing || loading}
                    type="button"
                    className="inline-flex min-h-12 flex-[2] items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-4 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow-lg transition hover:bg-[var(--admin-primary-strong)] disabled:opacity-70 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
                  >
                    {purchasing && <InlineLoader />}
                    <span>تأكيد الخصم والشراء</span>
                  </button>
                ) : (
                  <button
                    onClick={() => router.push('/student/balance')}
                    disabled={purchasing || loading}
                    type="button"
                    className="inline-flex min-h-12 flex-[2] items-center justify-center rounded-full bg-[var(--admin-card-strong)] px-4 py-3 text-sm font-black text-[var(--admin-text)] shadow-sm transition hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)] disabled:opacity-70 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
                  >
                    شحن الرصيد
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
