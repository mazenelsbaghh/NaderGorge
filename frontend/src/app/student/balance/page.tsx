import { Metadata } from 'next';
import { CreditCard, Wallet } from 'lucide-react';
import { BalanceDisplay } from '@/components/balance/BalanceDisplay';

export const metadata: Metadata = {
  title: 'المحفظة | أكاديمية نادر جورج',
  description: 'إدارة رصيد المحفظة ومعاملات الشراء',
};

export default function StudentBalancePage() {
  return (
    <div className="mx-auto max-w-4xl space-y-8">
      <div className="rounded-[2.5rem] bg-gradient-to-l from-indigo-100 to-indigo-50 p-8 dark:from-indigo-950/40 dark:to-indigo-900/10 border border-indigo-200/50 dark:border-indigo-800/30 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-black text-foreground mb-2">محفظتي</h1>
          <p className="text-[var(--admin-muted)] font-medium max-w-lg">تتبع رصيدك المتاح وسجل المعاملات السابقة لشراء المحتوى بطريقة مباشرة.</p>
        </div>
        <div className="rounded-[1.5rem] bg-white/50 p-4 shadow-sm backdrop-blur-md dark:bg-black/20">
          <Wallet className="h-8 w-8 text-indigo-600 dark:text-indigo-400" />
        </div>
      </div>
      
      <div className="mt-8 space-y-8">
        <BalanceDisplay />
        
        <div className="rounded-[2rem] border border-border bg-card p-6 shadow-sm">
          <h3 className="mb-4 text-lg font-bold text-foreground">شحن الرصيد</h3>
          <p className="text-muted-foreground text-sm mb-6 max-w-2xl leading-relaxed">
            يمكنك شحن رصيدك عبر إدخال كود الشحن من السنتر أو منافذ البيع المعتمدة. استخدم الرصيد المتاح لشراء الباقات والمفردات مباشرة بدون الحاجة لإدخال كود لكل حصة.
          </p>
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-full bg-indigo-100 dark:bg-indigo-900/30 text-indigo-600 dark:text-indigo-400">
              <CreditCard className="h-5 w-5" />
            </span>
            <span className="text-sm font-bold">يمكنك الشحن من خلال زر تفعيل الكود في القائمة الرئيسية.</span>
          </div>
        </div>
      </div>
    </div>
  );
}
