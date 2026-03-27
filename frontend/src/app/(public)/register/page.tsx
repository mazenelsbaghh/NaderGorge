'use client';

/**
 * Register Page — /register
 *
 * Design system: "Editorial Scholar" (same Admin tokens)
 * Token source:  useAuthTheme.ts
 * CSS:           ../auth.css  (all .auth-* utility classes)
 *
 * Features:
 *  - Light / Dark toggle (shared with login, persists in localStorage)
 *  - Full Egyptian governorates dropdown (27 محافظة)
 *  - Single-step registration: user + studentProfile created atomically
 *  - Zod client-side validation before API call
 */

import '../auth.css';

import Link from 'next/link';

import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useAuthTheme } from '@/hooks/useAuthTheme';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { RegistrationForm } from '@/components/forms/RegistrationForm';

export default function RegisterPage() {
  const { isDark, themeVars, toggleTheme } = useAuthTheme();

  useRootOverscrollBackground(themeVars);

  return (
    /*
     * auth-shell: same background (dot pattern + bg-color) as AdminShellChrome.
     * All --admin-* vars are injected inline via themeVars.
     */
    <div className="auth-shell" style={themeVars}>

      {/* ── Ambient Glow Orbs ── */}
      <div className="auth-shell__glow">
        <div className="auth-shell__glow-top" />
        <div className="auth-shell__glow-bottom" />
      </div>

      {/* ── Theme Toggle Bar ── */}
      <div className="auth-theme-bar">
        <AnimatedThemeToggler
          checked={isDark}
          onToggle={toggleTheme}
          aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
          title={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
          className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card-soft)]"
        />
      </div>

      {/* ── Main content ── */}
      <main className="relative z-10 w-full max-w-7xl px-4 py-10 sm:px-5 sm:py-16">

        {/* Logo Avatar */}
        <div className="auth-avatar">𓂀</div>

        {/* Heading */}
        <div className="mx-auto mb-8 max-w-3xl text-center">
          <h1
            className="text-3xl font-extrabold tracking-tight sm:text-4xl"
            style={{ color: 'var(--admin-text)' }}
          >
            افتح حسابك خطوة بخطوة
          </h1>
          <p
            className="mt-2 text-sm font-light"
            style={{ color: 'var(--admin-muted)' }}
          >
            سجل بياناتك عبر أربع مراحل واضحة: هوية، متابعة، مسار دراسي، ثم تأمين الحساب.
          </p>
        </div>

        {/* ── Glass Card ── */}
        <div className="auth-card p-3 sm:p-4 lg:p-5">
          <RegistrationForm />

          {/* Divider + Login link */}
          <div className="auth-divider" />
          <p className="text-center text-sm" style={{ color: 'var(--admin-muted)' }}>
            لديك حساب بالفعل؟{' '}
            <Link
              href="/login"
              className="font-bold transition-colors"
              style={{ color: 'var(--admin-primary)' }}
            >
              تسجيل الدخول
            </Link>
          </p>
        </div>

        {/* Footer branding */}
        <p className="auth-footer-caption mt-10">
          © 2026 THE EDITORIAL SCHOLAR ARCHIVE • ALL RIGHTS RESERVED
        </p>
      </main>
    </div>
  );
}
