'use client';

import { useEffect, useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import { CreditCard, ArrowDownRight, ArrowUpRight, Wallet, Upload, Gift, Clock3, ShieldCheck, KeyRound } from 'lucide-react';
import { balanceService, PromotionalBalanceDto, StudentBalanceDto } from '@/services/balance-service';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

function money(amount: number) {
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(amount);
}

function PromotionalAllocationCard({ allocation }: { allocation: PromotionalBalanceDto }) {
  const imageUrl = resolveMediaUrl(allocation.teacherProfileImageUrl);
  const title = allocation.teacherName ? `مخصص لمحتوى ${allocation.teacherName}` : 'صالح للمحتوى المدعوم';
  const displayName = allocation.teacherName ?? 'المحتوى المدعوم';
  const expiryLabel = allocation.expiresAt
    ? `ينتهي ${new Date(allocation.expiresAt).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'Africa/Cairo' })}`
    : 'بدون تاريخ انتهاء';
  const usageLabel = allocation.maxPurchaseCount
    ? `${allocation.purchaseCount}/${allocation.maxPurchaseCount} مشتريات`
    : 'استخدام مفتوح';
  const usedPercent = allocation.originalAmount > 0
    ? Math.min(100, Math.max(0, Math.round((allocation.consumedAmount / allocation.originalAmount) * 100)))
    : 0;
  const isEmpty = allocation.availableAmount <= 0;

  return (
    <article className={`overflow-hidden rounded-[1.25rem] border p-4 transition ${
      isEmpty
        ? 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] opacity-75'
        : 'border-[var(--admin-primary-15)] bg-[var(--admin-card)] shadow-sm'
    }`}>
      <div className="flex items-start gap-3">
        <div className="relative h-14 w-14 shrink-0 overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
          {imageUrl ? (
            <Image src={imageUrl} alt={displayName} fill sizes="56px" className="object-cover" />
          ) : (
            <div className="flex h-full w-full items-center justify-center bg-[var(--admin-primary-15)] text-lg font-black text-[var(--admin-primary)]">
              {displayName.trim().charAt(0) || 'م'}
            </div>
          )}
        </div>
        <div className="min-w-0 flex-1">
          <h4 className="truncate text-sm font-black text-[var(--admin-text)]">{title}</h4>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-xs font-bold text-[var(--admin-muted)]">
            <span className="inline-flex items-center gap-1"><Clock3 className="h-3.5 w-3.5" />{expiryLabel}</span>
            <span className="inline-flex items-center gap-1"><ShieldCheck className="h-3.5 w-3.5" />{usageLabel}</span>
          </div>
        </div>
      </div>

      <div className="mt-4 flex items-end justify-between gap-4">
        <div>
          <p className="text-xs font-bold text-[var(--admin-muted)]">الرصيد المتاح</p>
          <p className={`mt-1 text-2xl font-black ${isEmpty ? 'text-[var(--admin-muted)]' : 'text-[var(--admin-primary)]'}`}>
            {money(allocation.availableAmount)} <span className="text-sm">ج.م</span>
          </p>
        </div>
        <div className="text-left text-xs font-bold text-[var(--admin-muted)]">
          <p>الأصل {money(allocation.originalAmount)} ج.م</p>
          <p>المستخدم {money(allocation.consumedAmount)} ج.م</p>
        </div>
      </div>

      <div className="mt-4 h-2 overflow-hidden rounded-full bg-[var(--admin-border)]">
        <div className="h-full rounded-full bg-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow]" style={{ width: `${usedPercent}%` }} />
      </div>
    </article>
  );
}

export function BalanceDisplay() {
  const [balanceDto, setBalanceDto] = useState<StudentBalanceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchBalance = async () => {
    try {
      const data = await balanceService.getBalance();
      setBalanceDto(data);
      setError('');
    } catch {
      setError('تعذر تحميل بيانات المحفظة الآن.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const handleRefresh = () => {
      void fetchBalance();
    };

    window.addEventListener('refresh-student-balance', handleRefresh);
    void fetchBalance();

    const cleanupCacheStore = registerCacheStore('student:balance', () => {}, fetchBalance);

    return () => {
      window.removeEventListener('refresh-student-balance', handleRefresh);
      cleanupCacheStore();
    };
  }, []);

  if (loading) {
    return (
      <div className="animate-pulse rounded-[1.5rem] bg-[var(--admin-card-soft)] p-6">
        <div className="h-20 bg-[var(--admin-card-strong)] rounded-xl mb-4" />
        <div className="h-32 bg-[var(--admin-card-strong)] rounded-xl" />
      </div>
    );
  }

  const currentBalance = balanceDto?.currentBalance || 0;
  const promotionalBalance = balanceDto?.promotionalBalance || 0;

  return (
    <div className="flex flex-col gap-6">
      {error && (
        <div className="rounded-[1.5rem] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-4 py-3 text-sm font-bold text-[var(--admin-danger)]">
          {error}
        </div>
      )}

      <div className="relative overflow-hidden rounded-lg border border-[var(--admin-border)] bg-[var(--admin-primary)] p-6 text-[var(--admin-primary-contrast)] shadow-sm sm:p-8">
        <div className="relative z-10 flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="mb-1 text-sm font-medium tracking-[0.18em] opacity-80">الرصيد العام للمنصة</p>
            <h2 className="text-4xl font-black leading-none sm:text-5xl">
              {currentBalance.toLocaleString('en-US')} <span className="text-lg font-bold sm:text-xl">ج.م</span>
            </h2>
          </div>
          <div className="flex flex-col items-stretch gap-3 sm:items-end">
            <div className="self-start rounded-full border border-[var(--admin-primary-15)] bg-[var(--admin-primary-15)] p-4 backdrop-blur-md sm:self-auto">
              <Wallet className="h-8 w-8 text-[var(--admin-primary-contrast)]" />
            </div>
            <Link
              href="/student/recharge"
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-white px-5 py-2 text-sm font-black text-[var(--admin-primary)] shadow-sm transition hover:bg-white/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/80"
            >
              <Upload className="h-4 w-4" />
              <span>شحن بالتحويل ورفع الإثبات</span>
            </Link>
          </div>
        </div>

        {currentBalance <= 0 && (
          <div className="mt-6 flex items-start gap-3 rounded-xl border border-[var(--admin-primary-15)] bg-[var(--admin-card-soft)]/15 p-4 backdrop-blur-md">
            <CreditCard className="mt-0.5 h-5 w-5 shrink-0 text-[var(--admin-primary-contrast)]" />
            <p className="text-sm font-medium leading-7 text-[var(--admin-primary-contrast)]">رصيدك العام غير كافٍ. يمكنك الشحن بكود عام أو التحويل ورفع لقطة الشاشة.</p>
          </div>
        )}
      </div>

      <div className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-bold text-[var(--admin-muted)]">أرصدة المدرسين / الرصيد المخصص</p>
            <p className="mt-1 text-3xl font-black text-[var(--admin-text)]">{money(promotionalBalance)} <span className="text-base">ج.م</span></p>
          </div>
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
            <Gift className="h-6 w-6" />
          </div>
        </div>
        {(balanceDto?.promotionalAllocations?.length ?? 0) > 0 ? (
          <div className="mt-5 grid gap-3 lg:grid-cols-2">
            {balanceDto?.promotionalAllocations.map((allocation) => (
              <PromotionalAllocationCard key={allocation.id} allocation={allocation} />
            ))}
          </div>
        ) : (
          <p className="mt-3 text-sm text-[var(--admin-muted)]">لا يوجد رصيد ترويجي نشط حالياً.</p>
        )}
      </div>

      <div className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
        <h3 className="mb-4 text-lg font-bold text-[var(--admin-text)]">سجل المعاملات الأخير</h3>

        {(!balanceDto?.recentTransactions || balanceDto.recentTransactions.length === 0) ? (
          <div className="py-8 text-center text-[var(--admin-muted)]">
            لا توجد معاملات سابقة.
          </div>
        ) : (
          <div className="space-y-4">
            {balanceDto.recentTransactions.map((tx) => (
              <div key={tx.id} className="flex flex-col gap-4 rounded-[1.25rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex min-w-0 items-start gap-4 sm:items-center">
                  <div className={`rounded-xl p-3 ${
                    tx.affectsBalance === false
                      ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'
                      : tx.amount > 0
                      ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]'
                      : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'
                  }`}>
                    {tx.affectsBalance === false
                      ? <KeyRound className="h-5 w-5" />
                      : tx.amount > 0
                        ? <ArrowUpRight className="h-5 w-5" />
                        : <ArrowDownRight className="h-5 w-5" />}
                  </div>
                  <div className="min-w-0 flex-1">
                    <h4 className="break-words text-sm font-bold leading-7 text-[var(--admin-text)] sm:text-base">{tx.description}</h4>
                    <p className="mt-1 text-xs text-[var(--admin-muted)]">
                      {new Date(tx.createdAt).toLocaleDateString('en-GB', { timeZone: 'Africa/Cairo',
                        year: 'numeric', month: 'short', day: 'numeric',
                        hour: 'numeric', minute: 'numeric'
                      })}
                    </p>
                  </div>
                </div>
                <div className="flex items-center justify-between gap-4 border-t border-[var(--admin-border)] pt-3 text-right font-mono sm:block sm:border-t-0 sm:pt-0 sm:text-left">
                  {tx.affectsBalance === false ? (
                    <span className="block text-sm font-bold text-[var(--admin-primary)] sm:text-base">تم التفعيل بكود</span>
                  ) : (
                    <>
                      <span className={`block text-sm font-bold sm:text-base ${
                        tx.amount > 0 ? 'text-[var(--admin-success)]' : 'text-[var(--admin-danger)]'
                      }`}>
                        {tx.amount > 0 ? '+' : ''}{tx.amount} ج.م
                      </span>
                      <span className="text-xs text-[var(--admin-muted)]">الرصيد: {tx.balanceAfter} ج.م</span>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
