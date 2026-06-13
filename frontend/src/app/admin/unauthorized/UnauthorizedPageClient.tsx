'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ShieldAlert, ArrowRight, LogOut } from 'lucide-react';
import { useAuthStore } from '@/stores/auth-store';

export default function UnauthorizedPageClient() {
  const router = useRouter();
  const clearAuth = useAuthStore((state) => state.clearAuth);

  const handleLogout = () => {
    clearAuth();
    router.replace('/login');
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-[var(--admin-bg)] px-4 py-12">
      <div className="w-full max-w-md overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 text-center shadow-[0_12px_40px_var(--admin-shadow)]">
        <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-2xl bg-[var(--admin-danger-10)] text-[var(--admin-danger)]">
          <ShieldAlert className="h-8 w-8" />
        </div>

        <h1 className="mb-3 font-black text-2xl tracking-tight text-[var(--admin-text)]">
          غير مصرح بالدخول
        </h1>

        <p className="mb-8 text-sm leading-6 text-[var(--admin-muted)]">
          عذراً، حسابك الحالي لا يمتلك الصلاحيات الكافية للوصول إلى هذه الصفحة الفنية. يرجى التواصل مع مسؤول النظام أو تسجيل الدخول بحساب مشرف يمتلك الصلاحية المطلوبة.
        </p>

        <div className="flex flex-col gap-3">
          <Link
            href="/admin"
            prefetch={false}
            className="flex items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-bold text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] active:scale-95"
          >
            <span>العودة للرئيسية</span>
            <ArrowRight className="h-4 w-4" />
          </Link>

          <button
            onClick={handleLogout}
            className="flex items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] bg-transparent px-6 py-3.5 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] active:scale-95"
          >
            <LogOut className="h-4 w-4" />
            <span>تسجيل الخروج</span>
          </button>
        </div>
      </div>
    </div>
  );
}
