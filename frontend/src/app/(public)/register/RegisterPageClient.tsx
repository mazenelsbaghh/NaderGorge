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
import { useState } from 'react';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useAuthTheme } from '@/hooks/useAuthTheme';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { RippleGrid } from '@/components/ui/ripple-grid';
import { RegistrationForm } from '@/components/forms/RegistrationForm';
import { RegistrationInstructionsModal } from '@/components/registration/RegistrationInstructionsModal';
import { PlatformLogo } from '@/components/shared/PlatformLogo';
import { Info } from 'lucide-react';

export default function RegisterPageClient() {
  const { isDark, themeVars, toggleTheme } = useAuthTheme();
  useRootOverscrollBackground();
  const [showInstructions, setShowInstructions] = useState(false);

  return (
    <div 
      className="auth-shell relative flex min-h-[100dvh] w-full flex-col overflow-hidden overflow-y-auto bg-[var(--admin-bg)] text-[var(--admin-text)]" 
      style={themeVars}
    >

      {/* ── Ripple Interactive Background ── */}
      <div className="absolute inset-0 z-0 pointer-events-none">
        <RippleGrid
          gridColor={isDark ? '#64748b' : '#94a3b8'}
          rippleIntensity={0.05}
          gridSize={10}
          gridThickness={isDark ? 15 : 12}
          mouseInteraction={true}
          mouseInteractionRadius={1.2}
          opacity={isDark ? 0.8 : 0.4}
        />
      </div>

      {/* ── Ambient Glow Orbs ── */}
      <div className="auth-shell__glow pointer-events-none">
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
          suppressHydrationWarning
        />
      </div>

      {/* ── Main content ── */}
      <main className="relative z-10 w-full max-w-7xl px-4 py-10 sm:px-5 sm:py-16 m-auto">

        {/* Logo Avatar */}
        <div className="auth-avatar">
          <PlatformLogo variant="mark" size="lg" tone={isDark ? 'light' : 'dark'} priority />
        </div>

        {/* Heading */}
        <div className="mx-auto mb-8 max-w-3xl text-center flex flex-col items-center">
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
            اكتب البيانات المطلوبة فقط ليظهر لك المسار الدراسي الصحيح.
          </p>
          <button
            onClick={() => setShowInstructions(true)}
            className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-full bg-[var(--admin-primary-15)] px-4 py-2 text-xs font-black text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-hover)] active:scale-[0.98] cursor-pointer shadow-sm"
          >
            <Info className="h-3.5 w-3.5 shrink-0" />
            <span>تعليمات التسجيل</span>
          </button>
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
          © 2026 منصة مسار
        </p>
      </main>

      <RegistrationInstructionsModal open={showInstructions} onClose={() => setShowInstructions(false)} />
    </div>
  );
}
