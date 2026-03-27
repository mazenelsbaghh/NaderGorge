'use client';

import { useEffect, useState } from 'react';
import { CreditCard, ArrowDownRight, ArrowUpRight, Wallet } from 'lucide-react';
import { balanceService, StudentBalanceDto } from '@/services/balance-service';

export function BalanceDisplay() {
  const [balanceDto, setBalanceDto] = useState<StudentBalanceDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function fetchBalance() {
      try {
        const data = await balanceService.getBalance();
        setBalanceDto(data);
      } catch (error) {
        console.error('Failed to load balance', error);
      } finally {
        setLoading(false);
      }
    }
    fetchBalance();
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
      {/* Balance Card */}
      <div className="relative overflow-hidden rounded-[2rem] bg-gradient-to-br from-indigo-500 to-indigo-700 p-8 text-white shadow-lg">
        <div className="absolute -right-12 -top-12 h-40 w-40 rounded-full bg-white/10 blur-3xl mix-blend-overlay" />
        <div className="relative z-10 flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-white/80 uppercase tracking-widest mb-1">الرصيد المتاح</p>
            <h2 className="text-5xl font-black">{currentBalance.toLocaleString('ar-EG')} <span className="text-xl font-bold">ج.م</span></h2>
          </div>
          <div className="rounded-full bg-white/20 p-4 backdrop-blur-md">
            <Wallet className="h-8 w-8 text-white" />
          </div>
        </div>
        
        {currentBalance <= 0 && (
          <div className="mt-6 rounded-xl bg-rose-500/20 border border-rose-500/30 p-4 backdrop-blur-md flex items-center gap-3">
            <CreditCard className="h-5 w-5 text-rose-200" />
            <p className="text-sm font-medium text-rose-100">رصيدك غير كافٍ. يرجى شحن الرصيد باستخدام كود الشحن.</p>
          </div>
        )}
      </div>

      {/* Transactions List */}
      <div className="rounded-[2rem] border border-border bg-card p-6 shadow-sm">
        <h3 className="mb-4 text-lg font-bold text-foreground">سجل المعاملات الأخير</h3>
        
        {(!balanceDto?.recentTransactions || balanceDto.recentTransactions.length === 0) ? (
          <div className="text-center py-8 text-muted-foreground">
            لا توجد معاملات سابقة.
          </div>
        ) : (
          <div className="space-y-4">
            {balanceDto.recentTransactions.map((tx) => (
              <div key={tx.id} className="flex items-center justify-between border-b border-border pb-4 last:border-0 last:pb-0">
                <div className="flex items-center gap-4">
                  <div className={`rounded-xl p-3 ${
                    tx.amount > 0 ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400' 
                                  : 'bg-rose-500/10 text-rose-600 dark:text-rose-400'
                  }`}>
                    {tx.amount > 0 ? <ArrowUpRight className="h-5 w-5" /> : <ArrowDownRight className="h-5 w-5" />}
                  </div>
                  <div>
                    <h4 className="font-bold text-foreground">{tx.description}</h4>
                    <p className="text-xs text-muted-foreground mt-1">
                      {new Date(tx.createdAt).toLocaleDateString('ar-EG', {
                        year: 'numeric', month: 'short', day: 'numeric',
                        hour: 'numeric', minute: 'numeric'
                      })}
                    </p>
                  </div>
                </div>
                <div className="text-left font-mono">
                  <span className={`block font-bold ${
                    tx.amount > 0 ? 'text-emerald-600 dark:text-emerald-400' 
                                  : 'text-rose-600 dark:text-rose-400'
                  }`}>
                    {tx.amount > 0 ? '+' : ''}{tx.amount}
                  </span>
                  <span className="text-xs text-muted-foreground">الرصيد: {tx.balanceAfter}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
