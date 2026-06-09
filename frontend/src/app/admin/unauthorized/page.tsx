'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ShieldAlert, ArrowRight, LogOut } from 'lucide-react';
import { useAuthStore } from '@/stores/auth-store';

export default function UnauthorizedPage() {
  const router = useRouter();
  const clearAuth = useAuthStore((state) => state.clearAuth);

  const handleLogout = () => {
    clearAuth();
    router.replace('/login');
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-[#F6F7F8] px-4 py-12 dark:bg-[#090D16]">
      <div className="w-full max-w-md overflow-hidden rounded-[2rem] border border-[var(--admin-border,rgba(220,225,230,0.8))] bg-white p-8 text-center shadow-[0_12px_40px_rgba(10,29,61,0.06)] dark:border-gray-800 dark:bg-gray-900">
        <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-2xl bg-rose-50 text-rose-500 dark:bg-rose-950/30 dark:text-rose-400">
          <ShieldAlert className="h-8 w-8" />
        </div>

        <h1 className="mb-3 font-extrabold text-2xl tracking-tight text-[#0A1D3D] dark:text-white">
          غير مصرح بالدخول
        </h1>

        <p className="mb-8 text-sm leading-6 text-gray-500 dark:text-gray-400">
          عذراً، حسابك الحالي لا يمتلك الصلاحيات الكافية للوصول إلى هذه الصفحة الفنية. يرجى التواصل مع مسؤول النظام أو تسجيل الدخول بحساب مشرف يمتلك الصلاحية المطلوبة.
        </p>

        <div className="flex flex-col gap-3">
          <Link
            href="/admin"
            className="flex items-center justify-center gap-2 rounded-xl bg-[#0A1D3D] px-6 py-3.5 text-sm font-bold text-white transition hover:bg-[#0E8F8F] active:scale-95"
          >
            <span>العودة للرئيسية</span>
            <ArrowRight className="h-4 w-4" />
          </Link>

          <button
            onClick={handleLogout}
            className="flex items-center justify-center gap-2 rounded-xl border border-[var(--admin-border,rgba(220,225,230,0.8))] bg-transparent px-6 py-3.5 text-sm font-bold text-[#0A1D3D] transition hover:bg-gray-50 active:scale-95 dark:border-gray-800 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            <LogOut className="h-4 w-4" />
            <span>تسجيل الخروج</span>
          </button>
        </div>
      </div>
    </div>
  );
}
