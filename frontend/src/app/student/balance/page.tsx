"use client";

import { CreditCard, Sparkles, Wallet } from 'lucide-react';
import { BalanceDisplay } from '@/components/balance/BalanceDisplay';
import { CodeActivationForm } from '@/components/forms/CodeActivationForm';

export default function StudentBalancePage() {
  return (
    <div className="space-y-8 pb-10">
      
      {/* Hero Section */}
      <div className="group relative overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 shadow-sm transition-all sm:p-10">
        <div className="absolute -left-20 -top-20 h-64 w-64 rounded-full bg-[var(--admin-primary-15)] blur-[48px] transition-all duration-700 group-hover:scale-150" />
        <div className="absolute -bottom-20 -right-20 h-64 w-64 rounded-full bg-[var(--admin-primary-15)] blur-[48px] transition-all duration-700 group-hover:scale-150 group-hover:bg-[var(--admin-primary-20)]" />
        
        <div className="relative z-10 flex flex-col items-start gap-8 sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-3">
            <div className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary-20)] bg-[var(--admin-primary-10)] px-3 py-1 text-xs font-bold text-[var(--admin-primary-strong)]">
              <Sparkles className="h-3.5 w-3.5" />
              <span>رصيد الطالب</span>
            </div>
            <h1 className="text-3xl font-black text-[var(--admin-text)] sm:text-5xl">محفظتي</h1>
            <p className="max-w-md text-[var(--admin-muted)] text-sm sm:text-base leading-relaxed font-medium">
              تتبع رصيدك المتاح وسجل المعاملات السابقة. يمكنك شحن المحفظة مباشرة لاستخدامها في شراء الحصص.
            </p>
          </div>
          
          <div className="relative flex shrink-0 items-center justify-center">
            <div className="absolute inset-0 animate-pulse rounded-full bg-[var(--admin-primary-20)] blur-2xl" />
            <div className="relative flex h-24 w-24 items-center justify-center rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] shadow-xl backdrop-blur-xl rotate-3 transition-transform duration-500 group-hover:rotate-6 group-hover:scale-110">
              <Wallet className="h-10 w-10 text-[var(--admin-primary)]" />
            </div>
            <div className="absolute -left-4 -top-4 flex h-10 w-10 items-center justify-center rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-heavy)] shadow-lg backdrop-blur-md -rotate-6 transition-transform duration-500 group-hover:-rotate-12 group-hover:scale-110">
              <CreditCard className="h-5 w-5 text-[var(--admin-primary-strong)]" />
            </div>
          </div>
        </div>
      </div>
      
      <div className="grid gap-8 lg:grid-cols-12">
        {/* Right side: Balance Display (takes up more space) */}
        <div className="lg:col-span-12 xl:col-span-7">
          <BalanceDisplay />
        </div>
        
        {/* Left side: Code Redemption */}
        <div className="lg:col-span-12 xl:col-span-5 relative">
          <div className="sticky top-8 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm xl:p-8">
            <div className="mb-6 space-y-2">
              <h3 className="text-xl font-black text-[var(--admin-text)]">شحن باستخدام كود</h3>
              <p className="text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
                ادخل كود الشحن المقدم من السنتر لإضافة رصيد إلى محفظتك.
              </p>
            </div>
            
            <CodeActivationForm onSuccess={() => {
              window.dispatchEvent(new Event('refresh-student-balance'));
            }} />
          </div>
        </div>
      </div>
      
    </div>
  );
}
