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
import { RippleGrid } from '@/components/ui/ripple-grid';
import { LoginForm } from '@/components/forms/LoginForm';
import { FeatureCarousel } from '@/components/ui/feature-carousel';

const LOGIN_STEPS = [
  {
    id: '1',
    step: 1,
    name: 'تسجيل الدخول',
    title: 'مرحباً بعودتك',
    description: 'سجل دخولك للوصول إلى لوحات المتابعة الأكاديمية الخاصة بـ The Editorial Scholar.',
  },
];

export default function LoginPage() {
  const { isDark, toggleTheme } = useAuthTheme();
  useRootOverscrollBackground();

  return (
    <div 
      className="auth-shell relative flex min-h-[100dvh] w-full flex-col overflow-hidden overflow-y-auto bg-[var(--admin-bg)] text-[var(--admin-text)]" 
    >
      {/* ── Ripple Interactive Background ── */}
      <div className="absolute inset-0 z-0 pointer-events-none">
        <RippleGrid
          gridColor={isDark ? '#c5a059' : '#d4a762'}
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
        />
      </div>

      {/* ── Main content (Matches Registration's 7xl size) ── */}
      <main className="relative z-10 w-full max-w-7xl px-4 py-10 sm:px-5 sm:py-16 m-auto">
        
        {/* Logo Avatar is now inside the layout or just centered at top */}
        <div className="auth-avatar mb-8">☥</div>

        <FeatureCarousel
          clickToAdvance={false}
          autoPlay={false}
          steps={LOGIN_STEPS}
          bgClass="!border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] min-h-[500px] sm:min-h-[550px] shadow-[0_28px_70px_var(--admin-shadow)]"
        >
          <div className="relative z-10 mt-8 sm:mt-10 w-full pr-4 md:pr-0">
            <div className="flex flex-col lg:flex-row gap-8 lg:gap-12 lg:items-center w-full">
              
              {/* Left Side: Welcoming Visual (Hidden on mobile) */}
              <div className="hidden lg:flex lg:w-[50%] xl:w-[45%] flex-col justify-center items-center text-center space-y-6">
                 <div className="flex h-32 w-32 items-center justify-center rounded-full shadow-[0_0_80px_var(--admin-primary-15)] animate-pulse border border-[var(--admin-border)]">
                    <span className="text-7xl" style={{ color: 'var(--admin-primary)' }}>𓂀</span>
                 </div>
                 <div className="space-y-2">
                   <h3 className="text-xl font-bold" style={{ color: 'var(--admin-text)' }}>نظام نادر جورج التعليمي</h3>
                   <p className="text-sm leading-relaxed" style={{ color: 'var(--admin-muted)' }}>
                     بيئة رقمية متكاملة لضمان التفوق وتتبع الأداء خطوة بخطوة.
                   </p>
                 </div>
              </div>

              {/* Right Side: Login Form Box */}
              <div className="w-full lg:w-[50%] xl:w-[50%]">
                <div className="space-y-5 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 backdrop-blur-md sm:rounded-[28px] sm:p-7 shadow-[0_12px_40px_var(--admin-shadow)]">
                  <LoginForm />
                  
                  <div className="auth-divider my-6" />
                  
                  <p className="text-center text-sm" style={{ color: 'var(--admin-muted)' }}>
                    ليس لديك حساب؟{' '}
                    <Link
                      href="/register"
                      className="font-bold transition-colors hover:opacity-80"
                      style={{ color: 'var(--admin-primary)' }}
                    >
                      إنشاء حساب طالب
                    </Link>
                  </p>
                </div>
              </div>

            </div>
          </div>
        </FeatureCarousel>

        <p className="auth-footer-caption mt-10">
          © 2026 THE EDITORIAL SCHOLAR ARCHIVE • ALL RIGHTS RESERVED
        </p>
      </main>
    </div>
  );
}
