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
      className="group relative flex h-12 w-12 flex-col items-center justify-center overflow-hidden rounded-[18px] border border-[var(--admin-primary-15)] bg-[var(--admin-card-soft)] text-[var(--admin-primary)] transition-all hover:bg-[var(--admin-primary-15)] hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
      title="رصيد المحفظة"
    >
      {loading ? (
        <div className="h-4 w-4 animate-spin rounded-full border-2 border-[var(--admin-primary-strong)] border-r-transparent" />
      ) : (
        <>
          <Wallet className="absolute top-1.5 h-3.5 w-3.5 opacity-50 group-hover:opacity-100 transition-opacity" />
          <span className="absolute bottom-1.5 font-sans text-[10px] font-black tracking-tighter">
            {balance}
          </span>
        </>
      )}
    </Link>
  );
}
