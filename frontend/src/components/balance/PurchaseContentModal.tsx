'use client';

import { useState, useEffect } from 'react';
import { Wallet, AlertTriangle, CheckCircle } from 'lucide-react';
import { InlineLoader } from '@/components/ui/loading-indicator';
import { balanceService, CodeType, StudentBalanceDto } from '@/services/balance-service';
import { useRouter } from 'next/navigation';

export interface PurchaseContentModalProps {
  isOpen: boolean;
  onClose: () => void;
  contentType: CodeType;
  contentId: string;
  contentName: string;
  price: number;
}

export function PurchaseContentModal({
  isOpen,
  onClose,
  contentType,
  contentId,
  contentName,
  price,
}: PurchaseContentModalProps) {
  const router = useRouter();
  const [balanceDto, setBalanceDto] = useState<StudentBalanceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [purchasing, setPurchasing] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setLoading(true);
      setError('');
      setSuccess(false);
      balanceService.getBalance()
        .then(data => setBalanceDto(data))
        .catch(err => setError(err.message))
        .finally(() => setLoading(false));
    }
  }, [isOpen]);

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
        onClose();
        window.location.reload(); // Refresh to reflect purchased access
      }, 2000);
    } catch (err: any) {
      setError(err.message || 'فشل في إتمام عملية الشراء');
    } finally {
      setPurchasing(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md overflow-hidden rounded-[2rem] bg-card p-6 shadow-2xl animate-in zoom-in-95 fade-in duration-200" dir="rtl">
        {success ? (
          <div className="flex flex-col items-center justify-center py-8 text-center animate-in fade-in slide-in-from-bottom-4">
            <div className="mb-4 rounded-full bg-emerald-100 p-4 text-emerald-600 dark:bg-emerald-950/40 dark:text-emerald-400">
              <CheckCircle className="h-10 w-10" />
            </div>
            <h3 className="mb-2 text-2xl font-black text-foreground">تم الشراء بنجاح!</h3>
            <p className="text-muted-foreground font-medium">سيتم تحديث الصفحة الآن...</p>
          </div>
        ) : (
          <>
            <div className="mb-6 flex items-center justify-between">
              <h3 className="text-xl font-extrabold text-foreground">تأكيد الشراء</h3>
              <button 
                onClick={onClose}
                className="rounded-full bg-muted/50 p-2 text-muted-foreground transition hover:bg-muted hover:text-foreground"
              >
                ✕
              </button>
            </div>

            <div className="space-y-6">
              <div className="rounded-2xl bg-muted p-4 text-center">
                <p className="text-sm font-bold text-muted-foreground uppercase tracking-wider mb-2">المحتوى المطلوب</p>
                <p className="text-lg font-black text-foreground">{contentName}</p>
                <div className="mt-3 inline-block rounded-full bg-foreground/10 px-4 py-1.5 text-xl font-black text-foreground">
                  {price} ج.م
                </div>
              </div>

              {loading ? (
                <div className="flex items-center justify-center py-4">
                  <InlineLoader className="text-primary" />
                </div>
              ) : (
                <div className="rounded-2xl border border-border p-4">
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-sm font-bold text-muted-foreground">رصيد محفظتك</span>
                    <span className="flex items-center gap-1.5 font-mono text-sm font-bold text-foreground">
                      <Wallet className="h-4 w-4 text-primary" />
                      {currentBalance} ج.م
                    </span>
                  </div>
                  
                  {!isSufficient ? (
                     <div className="mt-4 rounded-xl bg-rose-500/10 p-3 text-sm font-bold text-rose-600 dark:bg-rose-950/40 dark:text-rose-400 flex items-start gap-2">
                       <AlertTriangle className="h-4 w-4 mt-0.5 shrink-0" />
                       <p>رصيدك غير كافٍ. يرجى شحن رصيدك للمتابعة.</p>
                     </div>
                  ) : (
                     <div className="mt-4 rounded-xl bg-emerald-500/10 p-3 text-sm font-bold text-emerald-600 dark:bg-emerald-950/40 dark:text-emerald-400 flex items-center gap-2">
                       <CheckCircle className="h-4 w-4 shrink-0" />
                       <p>رصيدك يكفي لإتمام هذه العملية.</p>
                     </div>
                  )}
                </div>
              )}

              {error && (
                <div className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm font-bold text-rose-700 dark:border-rose-900 dark:bg-rose-950/40 dark:text-rose-400 text-center">
                  {error}
                </div>
              )}

              <div className="flex gap-3">
                <button
                  onClick={onClose}
                  disabled={purchasing}
                  className="flex-1 rounded-full bg-muted py-3 text-sm font-bold text-muted-foreground transition hover:bg-muted/80"
                >
                  إلغاء
                </button>
                {isSufficient ? (
                  <button
                    onClick={handlePurchase}
                    disabled={purchasing || loading}
                    className="flex-[2] rounded-full bg-primary py-3 text-sm font-black text-primary-foreground shadow-lg transition hover:brightness-110 disabled:opacity-70 flex items-center justify-center gap-2"
                  >
                    {purchasing && <InlineLoader />}
                    <span>تأكيد الخصم والشراء</span>
                  </button>
                ) : (
                  <button
                    onClick={() => router.push('/student/balance')}
                    disabled={purchasing || loading}
                    className="flex-[2] rounded-full bg-foreground py-3 text-sm font-black text-background shadow-lg transition hover:brightness-110 disabled:opacity-70"
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
