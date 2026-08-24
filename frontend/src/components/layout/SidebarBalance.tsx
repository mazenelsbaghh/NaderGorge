'use client';

import { useEffect } from 'react';
import { Wallet } from 'lucide-react';
import Link from 'next/link';
import { useStudentShellStore } from '@/stores/student-shell-store';

function formatFullBalance(amount: number) {
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(amount);
}

function formatCompactBalance(amount: number) {
  return new Intl.NumberFormat('en-US', {
    notation: 'compact',
    maximumFractionDigits: amount >= 1_000_000 ? 1 : 0,
  }).format(amount);
}

export function SidebarBalance() {
  const generalBalance = useStudentShellStore((state) => state.currentBalance);
  const promotionalBalance = useStudentShellStore((state) => state.promotionalBalance);
  const totalAvailableBalance = generalBalance + promotionalBalance;
  const loading = useStudentShellStore((state) => state.isLoading && useStudentShellStore.getState().lastFetchedAt === null);
  const fetchBootstrap = useStudentShellStore((state) => state.fetchBootstrap);

  useEffect(() => {
    void fetchBootstrap();

    const handleBalanceRefresh = () => {
      void fetchBootstrap(true);
    };

    window.addEventListener('refresh-student-balance', handleBalanceRefresh);
    return () => {
      window.removeEventListener('refresh-student-balance', handleBalanceRefresh);
    };
  }, [fetchBootstrap]);

  return (
    <Link 
      href="/student/balance"
      className="group/balance relative flex h-12 w-12 group-hover/sidebar:w-full group-hover/sidebar:px-4 flex-col group-hover/sidebar:flex-row items-center justify-center group-hover/sidebar:justify-start gap-2 overflow-hidden rounded-[18px] border border-[var(--admin-primary-15)] bg-[var(--admin-card-soft)] text-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow] hover:bg-[var(--admin-primary-15)] hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
      title={`إجمالي المتاح: ${formatFullBalance(totalAvailableBalance)} ج.م (عام: ${formatFullBalance(generalBalance)}، مدرسين: ${formatFullBalance(promotionalBalance)})`}
    >
      {loading ? (
        <div className="h-4 w-4 animate-spin rounded-full border-2 border-[var(--admin-primary-strong)] border-e-transparent flex-shrink-0" />
      ) : (
        <>
          <Wallet className="absolute top-1.5 h-3.5 w-3.5 opacity-50 group-hover:opacity-100 transition-opacity group-hover/sidebar:static group-hover/sidebar:h-5 group-hover/sidebar:w-5 flex-shrink-0" />
          <span className="absolute bottom-1.5 flex max-w-[2.85rem] items-baseline justify-center gap-0.5 overflow-hidden font-sans text-sm font-black leading-none tracking-tight group-hover/sidebar:static group-hover/sidebar:max-w-none group-hover/sidebar:text-sm flex-shrink-0">
            <span className="truncate group-hover/sidebar:hidden">{formatCompactBalance(totalAvailableBalance)}</span>
            <span className="hidden truncate group-hover/sidebar:inline">{formatFullBalance(totalAvailableBalance)}</span>
            <span className="text-[8px] font-bold group-hover/sidebar:text-sm">ج.م</span>
          </span>
          <span className="hidden group-hover/sidebar:block text-xs font-bold text-[var(--admin-muted)] truncate whitespace-nowrap me-auto">
            إجمالي المتاح
          </span>
        </>
      )}
    </Link>
  );
}
