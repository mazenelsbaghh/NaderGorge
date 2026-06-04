'use client';

import { useEffect, useState } from 'react';
import { Wallet } from 'lucide-react';
import { balanceService } from '@/services/balance-service';
import Link from 'next/link';

export function SidebarBalance() {
  const [balance, setBalance] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function fetchBalance() {
      try {
        const data = await balanceService.getBalance();
        setBalance(data.currentBalance);
      } catch {
        // Silent fail for sidebar
        setBalance(0);
      } finally {
        setLoading(false);
      }
    }
    
    // Custom event listener for balance refresh (dispatched by CodeActivationForm)
    const handleBalanceRefresh = () => {
      void fetchBalance();
    };
    
    window.addEventListener('refresh-student-balance', handleBalanceRefresh);
    void fetchBalance();
    
    return () => {
      window.removeEventListener('refresh-student-balance', handleBalanceRefresh);
    };
  }, []);

  return (
    <Link 
      href="/student/balance"
      className="group/balance relative flex h-12 w-12 group-hover/sidebar:w-full group-hover/sidebar:px-4 flex-col group-hover/sidebar:flex-row items-center justify-center group-hover/sidebar:justify-start gap-2 overflow-hidden rounded-[18px] border border-[var(--admin-primary-15)] bg-[var(--admin-card-soft)] text-[var(--admin-primary)] transition-all hover:bg-[var(--admin-primary-15)] hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
      title="رصيد المحفظة"
    >
      {loading ? (
        <div className="h-4 w-4 animate-spin rounded-full border-2 border-[var(--admin-primary-strong)] border-r-transparent flex-shrink-0" />
      ) : (
        <>
          <Wallet className="absolute top-1.5 h-3.5 w-3.5 opacity-50 group-hover:opacity-100 transition-opacity group-hover/sidebar:static group-hover/sidebar:h-5 group-hover/sidebar:w-5 flex-shrink-0" />
          <span className="absolute bottom-1.5 font-sans text-[10px] font-black tracking-tighter group-hover/sidebar:static group-hover/sidebar:text-sm flex-shrink-0">
            {balance} ج.م
          </span>
          <span className="hidden group-hover/sidebar:block text-xs font-bold text-[var(--admin-muted)] truncate whitespace-nowrap mr-auto">
            المحفظة
          </span>
        </>
      )}
    </Link>
  );
}
