'use client';

import React, { useEffect } from 'react';
import { useAuthStore } from '@/stores/auth-store';
import { useAuthTheme } from '@/hooks/useAuthTheme';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import dynamic from 'next/dynamic';
const RippleGrid = dynamic(() => import('@/components/ui/ripple-grid').then(mod => ({ default: mod.RippleGrid })), { ssr: false });
import { PlatformLogo } from '@/components/shared/PlatformLogo';
import { getSurfaceName, getSurfaceOrigins } from '@/packages/surface-runtime/config';

import './(public)/auth.css';

export default function NotFoundPage() {
  const { isDark, themeVars } = useAuthTheme();
  useRootOverscrollBackground();

  const { user, loadFromStorage } = useAuthStore();
  const surface = getSurfaceName();

  useEffect(() => {
    loadFromStorage();
  }, [loadFromStorage]);

  const origins = getSurfaceOrigins();
  const roles = user?.roles || [];

  // Determine the default home page for the user based on their role
  let homeLink = '/';
  if (roles.includes('Admin') || roles.includes('Supervisor')) {
    homeLink = `${origins.admin}/admin`;
  } else if (roles.includes('Teacher')) {
    homeLink = `${origins.teacher}/teacher`;
  } else if (roles.includes('Assistant') || roles.includes('Staff')) {
    homeLink = `${origins.assistant}/assistant`;
  } else if (roles.includes('Student')) {
    homeLink = `${origins.student}/student`;
  } else {
    // If not logged in, go to the active surface's login page
    if (surface === 'student') homeLink = `${origins.student}/login`;
    else if (surface === 'teacher') homeLink = `${origins.teacher}/login`;
    else if (surface === 'assistant') homeLink = `${origins.assistant}/login`;
    else if (surface === 'admin') homeLink = `${origins.admin}/login`;
    else homeLink = `${origins.landing}/`;
  }

  // Gateway specific text translation for the error title
  let portalTitle = 'المنصة';
  if (surface === 'student') portalTitle = 'بوابة الطالب';
  else if (surface === 'teacher') portalTitle = 'بوابة المعلم';
  else if (surface === 'assistant') portalTitle = 'بوابة المساعدين والموظفين';
  else if (surface === 'admin') portalTitle = 'بوابة الإدارة';

  return (
    <div 
      className="auth-shell relative flex min-h-[100dvh] w-full flex-col overflow-hidden bg-[var(--admin-bg)] text-[var(--admin-text)]" 
      style={themeVars}
    >
      {/* ── Ripple Interactive Background ── */}
      <div className="absolute inset-0 z-0 pointer-events-none">
        <RippleGrid
          gridColor={isDark ? '#64748b' : '#94a3b8'}
          rippleIntensity={0.035}
          gridSize={12}
          gridThickness={isDark ? 12 : 10}
          mouseInteraction={false}
          opacity={isDark ? 0.5 : 0.28}
        />
      </div>

      {/* ── Ambient Glow Orbs ── */}
      <div className="auth-shell__glow pointer-events-none">
        <div className="auth-shell__glow-top" />
        <div className="auth-shell__glow-bottom" />
      </div>

      {/* ── Main content ── */}
      <main className="relative z-10 w-full max-w-xl px-4 py-10 sm:px-5 sm:py-16 m-auto text-center">
        <div className="auth-avatar mb-8">
          <PlatformLogo variant="mark" size="lg" tone={isDark ? 'light' : 'dark'} priority />
        </div>

        <div className="space-y-6 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 sm:p-10 backdrop-blur-md sm:rounded-[28px] shadow-[0_12px_40px_var(--admin-shadow)]">
          <div className="space-y-3">
            <span className="inline-block px-3 py-1 text-xs font-bold tracking-wider rounded-full bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]">
              {portalTitle}
            </span>
            <h1 className="text-4xl sm:text-5xl font-black text-[var(--admin-text)] font-[family-name:var(--font-tajawal)]">
              404
            </h1>
            <p className="text-lg font-bold text-[var(--admin-text)]">
              الصفحة غير موجودة أو لا تخص هذا الحساب
            </p>
            <p className="text-sm text-[var(--admin-muted)] leading-relaxed">
              عذراً، يبدو أنك تحاول الوصول إلى صفحة غير متوفرة على هذا النطاق، أو أن حسابك لا يملك الصلاحيات الكافية لفتحها.
            </p>
          </div>

          <div className="pt-2">
            <a
              href={homeLink}
              className="inline-flex h-12 items-center justify-center rounded-xl bg-[var(--admin-primary)] px-6 font-bold text-[var(--admin-foreground)] transition hover:opacity-90 focus:outline-none focus:ring-2 focus:ring-[var(--admin-primary)] focus:ring-offset-2 focus:ring-offset-[var(--admin-card)]"
            >
              العودة للرئيسية
            </a>
          </div>
        </div>

        <p className="auth-footer-caption mt-10">
          © 2026 منصة مسار
        </p>
      </main>
    </div>
  );
}
