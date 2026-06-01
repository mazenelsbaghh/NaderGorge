'use client';

import { useEffect, useState } from 'react';
import { CreditCard, ArrowDownRight, ArrowUpRight, Wallet } from 'lucide-react';
import { balanceService, StudentBalanceDto } from '@/services/balance-service';

export function BalanceDisplay() {
  const [balanceDto, setBalanceDto] = useState<StudentBalanceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    async function fetchBalance() {
      try {
        const data = await balanceService.getBalance();
        setBalanceDto(data);
        setError('');
      } catch {
        setError('تعذر تحميل بيانات المحفظة الآن.');
      } finally {
        setLoading(false);
      }
    }
    
    const handleRefresh = () => {
      void fetchBalance();
    };

    window.addEventListener('refresh-student-balance', handleRefresh);
    void fetchBalance();

    return () => {
      window.removeEventListener('refresh-student-balance', handleRefresh);
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

  return (
    <div className="flex flex-col gap-6">
      {error && (
        <div className="rounded-[1.5rem] border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-4 py-3 text-sm font-bold text-[var(--admin-danger)]">
          {error}
        </div>
      )}

      <div className="relative overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[linear-gradient(135deg,var(--admin-primary),var(--admin-primary-strong))] p-6 text-[var(--admin-primary-contrast)] shadow-[0_20px_48px_var(--admin-shadow)] sm:p-8">
        <div className="absolute -right-12 -top-12 h-40 w-40 rounded-full bg-[var(--admin-primary-15)] blur-3xl mix-blend-overlay" />
        <div className="relative z-10 flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="mb-1 text-sm font-medium tracking-[0.18em] opacity-80">الرصيد المتاح</p>
            <h2 className="text-4xl font-black leading-none sm:text-5xl">
              {currentBalance.toLocaleString('en-US')} <span className="text-lg font-bold sm:text-xl">ج.م</span>
            </h2>
          </div>
          <div className="self-start rounded-full border border-[var(--admin-primary-15)] bg-[var(--admin-primary-15)] p-4 backdrop-blur-md sm:self-auto">
            <Wallet className="h-8 w-8 text-[var(--admin-primary-contrast)]" />
          </div>
        </div>
        
        {currentBalance <= 0 && (
          <div className="mt-6 flex items-start gap-3 rounded-xl border border-[var(--admin-primary-15)] bg-[var(--admin-card-soft)]/15 p-4 backdrop-blur-md">
            <CreditCard className="mt-0.5 h-5 w-5 shrink-0 text-[var(--admin-primary-contrast)]" />
            <p className="text-sm font-medium leading-7 text-[var(--admin-primary-contrast)]">رصيدك غير كافٍ. يرجى شحن الرصيد باستخدام كود الشحن.</p>
          </div>
        )}
      </div>

      <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
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
                    tx.amount > 0
                      ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]'
                      : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'
                  }`}>
                    {tx.amount > 0 ? <ArrowUpRight className="h-5 w-5" /> : <ArrowDownRight className="h-5 w-5" />}
                  </div>
                  <div className="min-w-0 flex-1">
                    <h4 className="break-words text-sm font-bold leading-7 text-[var(--admin-text)] sm:text-base">{tx.description}</h4>
                    <p className="mt-1 text-xs text-[var(--admin-muted)]">
                      {new Date(tx.createdAt).toLocaleDateString('en-GB', {
                        year: 'numeric', month: 'short', day: 'numeric',
                        hour: 'numeric', minute: 'numeric'
                      })}
                    </p>
                  </div>
                </div>
                <div className="flex items-center justify-between gap-4 border-t border-[var(--admin-border)] pt-3 text-right font-mono sm:block sm:border-t-0 sm:pt-0 sm:text-left">
                  <span className={`block text-sm font-bold sm:text-base ${
                    tx.amount > 0 ? 'text-[var(--admin-success)]' : 'text-[var(--admin-danger)]'
                  }`}>
                    {tx.amount > 0 ? '+' : ''}{tx.amount} ج.م
                  </span>
                  <span className="text-xs text-[var(--admin-muted)]">الرصيد: {tx.balanceAfter} ج.م</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
