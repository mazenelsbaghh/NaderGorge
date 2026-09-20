'use client';

/**
 * Login Page — /login
 *
 * Design system: "Editorial Scholar" dark/light mode
 * Token source:  useAuthTheme.ts  →  same values as useAdminTheme.ts
 * CSS:           ../auth.css  (all .auth-* utility classes)
 *
 * Features:
 *  - Light / Dark toggle pill (top-left) — persists via localStorage("admin-theme-mode")
 *  - Compact, static-first form layout
 *  - Admin CSS variables for shared theming
 *  - Link to /register
 */

import '../auth.css';

import { useEffect, type CSSProperties } from 'react';
import dynamic from 'next/dynamic';
import { StudentLogin } from './StudentLogin';
import { useRouter } from 'next/navigation';
import { useAuthStore } from '@/stores/auth-store';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { PlatformLogo } from '@/components/shared/PlatformLogo';
import { getRoleDestination, getSurfaceName, getSurfaceOrigins } from '@/packages/surface-runtime/config';
import { resolveReturnNavigation } from '@/lib/safe-return-url';

const StaffLogin = dynamic(
  () => import('./StaffLogin').then((module) => module.StaffLogin),
  {
    ssr: false,
    loading: () => (
      <div className="auth-shell flex min-h-[100dvh] items-center justify-center bg-[var(--admin-bg)] text-[var(--admin-text)]">
        <p role="status" aria-live="polite" aria-busy="true" className="font-bold">
          جارٍ تجهيز صفحة تسجيل الدخول…
        </p>
      </div>
    ),
  },
);

export default function LoginPageClient() {
  const router = useRouter();
  const { isDark, themeVars, toggleTheme } = useAdminTheme();

  const { user, isAuthenticated, isLoading, loadFromStorage } = useAuthStore();
  const surface = getSurfaceName();
  const authThemeVars = {
    ...themeVars,
    '--admin-footer': isDark ? '#d1c5b4' : '#0E8F8F',
  } as CSSProperties;

  useEffect(() => {
    loadFromStorage();
  }, [loadFromStorage]);

  useEffect(() => {
    if (isLoading) return;
    if (isAuthenticated) {
      let returnUrl = '';
      if (typeof window !== 'undefined') {
        const params = new URLSearchParams(window.location.search);
        returnUrl = params.get('returnUrl') || '';
      }

      const origins = getSurfaceOrigins();
      const roles = user?.roles || [];
      const allowedDomains = user?.allowedDomains || [];

      const defaultDestination = getRoleDestination(roles, allowedDomains, origins);

      const navigation = resolveReturnNavigation({
        returnUrl,
        defaultDestination,
        surface,
        currentOrigin: window.location.origin,
      });
      if (navigation.sameOrigin) {
        router.replace(navigation.href);
      } else {
        window.location.replace(navigation.href);
      }
    }
  }, [isAuthenticated, isLoading, router, user, surface]);

  if (isLoading || isAuthenticated) {
    return (
      <div
        className="auth-shell auth-redirect-screen relative flex min-h-[100dvh] w-full flex-col items-center justify-center bg-[var(--admin-bg)] text-[var(--admin-text)]"
        style={authThemeVars}
      >
        <section
          className="auth-redirect-card"
          aria-live="polite"
          aria-busy="true"
        >
          <div className="auth-redirect-logo">
            <PlatformLogo
              variant="mark"
              size="md"
              tone={isDark ? 'light' : 'dark'}
              priority
            />
            <span className="auth-redirect-loader" aria-hidden="true" />
          </div>

          <div className="auth-redirect-copy">
            <p className="auth-redirect-kicker">منصة مسار</p>
            <h1>جارٍ تجهيز حسابك</h1>
            <p>نتحقق من الجلسة وننقلك للمكان المناسب.</p>
          </div>

          <div className="auth-redirect-progress" aria-hidden="true">
            <span />
          </div>
        </section>
      </div>
    );
  }

  if (surface === 'student' || surface === 'landing' || surface === 'all') {
    return <StudentLogin isDark={isDark} onToggleTheme={toggleTheme} />;
  }

  return <StaffLogin surface={surface} isDark={isDark} themeVars={authThemeVars} onToggleTheme={toggleTheme} />;
}
