'use client';

import Link from 'next/link';
import { useAuthStore } from '@/stores/auth-store';
import { useRouter } from 'next/navigation';

export function Navbar() {
  const { user, isAuthenticated, clearAuth } = useAuthStore();
  const router = useRouter();

  const handleLogout = () => {
    clearAuth();
    router.push('/login');
  };

  return (
    <nav className="sticky top-0 z-50 w-full border-b border-[var(--landing-line)] bg-[var(--landing-bg-soft)]/95 backdrop-blur supports-[backdrop-filter]:bg-[var(--landing-bg-soft)]/60">
      <div className="container mx-auto flex h-16 items-center justify-between px-4">
        <Link href="/" className="flex items-center gap-2">
          <span className="flex h-9 w-9 items-center justify-center rounded-full border border-[var(--landing-line)] bg-[var(--landing-card)] text-lg text-[var(--landing-accent)]">☥</span>
          <span className="text-xl font-bold text-[var(--landing-ink)]">
            نادر جورج
          </span>
        </Link>

        <div className="flex items-center gap-4">
          {isAuthenticated ? (
            <>
              <Link href="/student" className="text-sm font-medium text-[var(--landing-muted)] hover:text-[var(--landing-accent)] transition-colors">
                لوحة التحكم
              </Link>
              <span className="text-sm text-[var(--landing-muted)]">{user?.fullName}</span>
              <button
                onClick={handleLogout}
                className="text-sm font-medium text-red-600 hover:text-red-700 transition-colors"
              >
                تسجيل خروج
              </button>
            </>
          ) : (
            <>
              <Link href="/login" className="text-sm font-bold text-[var(--landing-muted)] hover:text-[var(--landing-accent)] transition-colors">
                تسجيل دخول
              </Link>
              <Link
                href="/register"
                className="rounded-full bg-[var(--landing-accent)] px-5 py-2 text-sm font-bold text-[var(--landing-accent-foreground)] shadow-[0_8px_20px_rgba(145,95,42,0.25)] hover:bg-[var(--landing-accent-strong)] transition-all hover:-translate-y-0.5"
              >
                إنشاء حساب
              </Link>
            </>
          )}
        </div>
      </div>
    </nav>
  );
}
