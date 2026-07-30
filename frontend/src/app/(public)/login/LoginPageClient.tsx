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

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useAuthStore } from '@/stores/auth-store';

import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useAuthTheme } from '@/hooks/useAuthTheme';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { LoginForm } from '@/components/forms/LoginForm';
import { PlatformLogo } from '@/components/shared/PlatformLogo';
import { getSurfaceName, getSurfaceOrigins } from '@/packages/surface-runtime/config';
import { resolveReturnNavigation } from '@/lib/safe-return-url';
import { RegistrationInstructionsModal } from '@/components/registration/RegistrationInstructionsModal';

function getLoginCopy(surface: string) {
  let title = 'بوابة الطالب';
  let description = 'ادخل مباشرة إلى دروسك، واجباتك، ومتابعة تقدمك الدراسي.';

  if (surface === 'teacher') {
    title = 'بوابة المعلم';
    description = 'إدارة المحاضرات، الامتحانات، ومتابعة تقارير الطلاب.';
  } else if (surface === 'assistant') {
    title = 'بوابة المساعدين والموظفين';
    description = 'متابعة المهام اليومية، طلبات الحضور، وإدارة شؤون الطلاب.';
  } else if (surface === 'admin') {
    title = 'بوابة الإدارة';
    description = 'إدارة المنصة بالكامل، إعدادات النظام، والصلاحيات.';
  }

  return { title, description };
}

export default function LoginPageClient() {
  const router = useRouter();
  const { isDark, themeVars, toggleTheme } = useAuthTheme();
  useRootOverscrollBackground();

  const { user, isAuthenticated, isLoading, loadFromStorage } = useAuthStore();
  const surface = getSurfaceName();
  const loginCopy = getLoginCopy(surface);
  const [showLoginInstructions, setShowLoginInstructions] = useState(true);

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

      // Default destinations
      let defaultDestination = `${origins.student}/student`;
      const hasAdmin = allowedDomains.includes('admin') || roles.some(r => r.toLowerCase().includes('admin') || r.toLowerCase().includes('supervisor'));
      const hasTeacher = allowedDomains.includes('teacher') || roles.some(r => r.toLowerCase().includes('teacher'));
      const hasAssistant = allowedDomains.includes('assistant') || roles.some(r => r.toLowerCase().includes('assistant') || r.toLowerCase().includes('staff'));
      const isEmployee = roles.some(r => r.toLowerCase() === 'employee');

      if (hasAdmin) {
        defaultDestination = `${origins.admin}/admin`;
      } else if (hasTeacher) {
        defaultDestination = `${origins.teacher}/teacher`;
      } else if (isEmployee) {
        defaultDestination = `${origins.assistant}/employee`;
      } else if (hasAssistant) {
        defaultDestination = `${origins.assistant}/assistant`;
      }

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
        style={themeVars}
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

  let welcomeText = 'دروس منظمة، امتحانات واضحة، وتقدم ظاهر في كل خطوة.';
  if (surface === 'teacher') {
    welcomeText = 'التحكم الكامل بمجموعاتك، طلابك، وتقارير أدائهم.';
  } else if (surface === 'assistant') {
    welcomeText = 'إدارة العمليات اليومية وتسهيل شؤون الطلاب.';
  } else if (surface === 'admin') {
    welcomeText = 'اللوحة القيادية المتكاملة لإدارة النظام والتحكم بالصلاحيات.';
  }

  return (
    <div
      className="auth-shell relative flex min-h-[100dvh] w-full flex-col overflow-y-auto bg-[var(--admin-bg)] text-[var(--admin-text)]"
      style={themeVars}
    >
      <div className="auth-shell__glow pointer-events-none">
        <div className="auth-shell__glow-top" />
        <div className="auth-shell__glow-bottom" />
      </div>

      <div className="auth-theme-bar">
        <AnimatedThemeToggler
          checked={isDark}
          onToggle={toggleTheme}
          aria-label={
            isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'
          }
          title={
            isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'
          }
          className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card-soft)]"
        />
      </div>

      <main className="auth-login-main">
        <section className="auth-login-card" aria-labelledby="login-page-title">
          <header className="auth-login-heading">
            <div className="auth-login-logo">
              <PlatformLogo
                variant="mark"
                size="md"
                tone={isDark ? 'light' : 'dark'}
                priority
              />
            </div>
            <div>
              <p className="auth-login-brand">منصة مسار</p>
              <h1 id="login-page-title">{loginCopy.title}</h1>
              <p>{loginCopy.description}</p>
            </div>
          </header>

          <div className="auth-login-body">
            <aside className="auth-login-intro" aria-label="عن منصة مسار">
              <h2>خطوتك التالية تبدأ من حسابك</h2>
              <p>{welcomeText}</p>
              <Link href="/" className="auth-login-home-link">
                العودة إلى الصفحة الرئيسية
              </Link>
            </aside>

            <div className="auth-login-panel">
              <h2>تسجيل الدخول إلى حسابك</h2>
              <LoginForm />

              {(surface === 'student' ||
                surface === 'landing' ||
                surface === 'all') && (
                <>
                  <div className="auth-divider" />
                  <p
                    className="text-center text-sm"
                    style={{ color: 'var(--admin-muted)' }}
                  >
                    ليس لديك حساب؟{' '}
                    <Link
                      href="/register"
                      className="font-bold transition-colors hover:opacity-80"
                      style={{ color: 'var(--admin-primary)' }}
                    >
                      إنشاء حساب طالب
                    </Link>
                  </p>
                </>
              )}
            </div>
          </div>
        </section>

        <p className="auth-footer-caption">© 2026 منصة مسار</p>
      </main>

      {showLoginInstructions ? <RegistrationInstructionsModal
        open
        mode="login"
        title="تعليمات مهمة قبل تسجيل الدخول"
        subtitle="هذه التعليمات تخص استخدام حسابك الحالي، وليست تعليمات إنشاء حساب جديد."
        confirmLabel="فهمت، متابعة لتسجيل الدخول"
        onClose={() => setShowLoginInstructions(false)}
      /> : null}
    </div>
  );
}
