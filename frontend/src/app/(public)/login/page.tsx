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
 *  - Dot-grid background + ambient glow orbs (identical to AdminShellChrome)
 *  - Glassmorphism card with Admin CSS vars
 *  - Link to /register
 */

import '../auth.css';

import Link from 'next/link';

import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useAuthTheme } from '@/hooks/useAuthTheme';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { LoginForm } from '@/components/forms/LoginForm';

export default function LoginPage() {
  const { isDark, themeVars, toggleTheme } = useAuthTheme();

  useRootOverscrollBackground(themeVars);

  return (
    /*
     * auth-shell: sets background-color, dot-pattern bg-image, direction=rtl
     * All --admin-* vars are injected inline via themeVars (from useAuthTheme)
     */
    <div className="auth-shell" style={themeVars}>

      {/* ── Ambient Glow Orbs (decorative, pointer-events: none) ── */}
      <div className="auth-shell__glow">
        <div className="auth-shell__glow-top" />
        <div className="auth-shell__glow-bottom" />
      </div>

      {/* ── Theme Toggle Bar — fixed top-left like Admin sidebar moon icon ── */}
      <div className="auth-theme-bar">
        <AnimatedThemeToggler
          checked={isDark}
          onToggle={toggleTheme}
          aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
          title={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
          className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card-soft)]"
        />
      </div>

      {/* ── Main content centered ── */}
      <main className="relative z-10 w-full max-w-md px-5 py-14">

        {/* Logo Avatar */}
        <div className="auth-avatar">☥</div>

        {/* Heading */}
        <div className="mb-8 text-center">
          <h1
            className="text-3xl font-extrabold tracking-tight"
            style={{ color: 'var(--admin-text)' }}
          >
            مرحباً بعودتك
          </h1>
          <p
            className="mt-2 text-sm font-light"
            style={{ color: 'var(--admin-muted)' }}
          >
            The Editorial Scholar&nbsp;•&nbsp;نظام نادر جورج التعليمي
          </p>
        </div>

        {/* ── Glass Card ── */}
        <div className="auth-card p-7 sm:p-9">

          {/* Login form (handles API call + redirect internally) */}
          <LoginForm />

          {/* Divider + Register link */}
          <div className="auth-divider" />
          <p className="text-center text-sm" style={{ color: 'var(--admin-muted)' }}>
            ليس لديك حساب؟{' '}
            <Link
              href="/register"
              className="font-bold transition-colors"
              style={{ color: 'var(--admin-primary)' }}
            >
              إنشاء حساب طالب
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
