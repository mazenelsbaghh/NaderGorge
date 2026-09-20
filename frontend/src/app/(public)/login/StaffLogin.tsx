'use client';

import type { CSSProperties } from 'react';
import { useState } from 'react';
import dynamic from 'next/dynamic';
import Link from 'next/link';

import { LoginForm } from '@/components/forms/LoginForm';
import { PlatformLogo } from '@/components/shared/PlatformLogo';

const CompactRegistrationInstructionsDialog = dynamic(
  () => import('@/components/registration/CompactRegistrationInstructionsDialog')
    .then((module) => module.CompactRegistrationInstructionsDialog),
  { ssr: false },
);

const COPY: Record<string, { title: string; description: string; welcome: string }> = {
  teacher: { title: 'بوابة المعلم', description: 'إدارة المحاضرات، الامتحانات، ومتابعة تقارير الطلاب.', welcome: 'التحكم الكامل بمجموعاتك، طلابك، وتقارير أدائهم.' },
  assistant: { title: 'بوابة المساعدين والموظفين', description: 'متابعة المهام اليومية، طلبات الحضور، وإدارة شؤون الطلاب.', welcome: 'إدارة العمليات اليومية وتسهيل شؤون الطلاب.' },
  admin: { title: 'بوابة الإدارة', description: 'إدارة المنصة بالكامل، إعدادات النظام، والصلاحيات.', welcome: 'اللوحة القيادية المتكاملة لإدارة النظام والتحكم بالصلاحيات.' },
};

export function StaffLogin({ surface, isDark, themeVars, onToggleTheme }: {
  surface: string;
  isDark: boolean;
  themeVars: CSSProperties;
  onToggleTheme: () => void;
}) {
  const [showInstructions, setShowInstructions] = useState(true);
  const copy = COPY[surface] ?? COPY.admin;
  return (
    <div className="auth-shell relative flex min-h-[100dvh] w-full flex-col overflow-y-auto bg-[var(--admin-bg)] text-[var(--admin-text)]" style={themeVars}>
      <div className="auth-shell__glow pointer-events-none"><div className="auth-shell__glow-top" /><div className="auth-shell__glow-bottom" /></div>
      <div className="auth-theme-bar"><button type="button" onClick={onToggleTheme} aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'} title={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'} className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"><span aria-hidden="true" className="text-xl leading-none">{isDark ? '☀' : '☾'}</span></button></div>
      <main className="auth-login-main"><section className="auth-login-card" aria-labelledby="login-page-title">
        <header className="auth-login-heading"><div className="auth-login-logo"><PlatformLogo variant="mark" size="md" tone={isDark ? 'light' : 'dark'} priority /></div><div><p className="auth-login-brand">منصة مسار</p><h1 id="login-page-title">{copy.title}</h1><p>{copy.description}</p></div></header>
        <div className="auth-login-body"><aside className="auth-login-intro" aria-label="عن منصة مسار"><h2>خطوتك التالية تبدأ من حسابك</h2><p>{copy.welcome}</p><Link href="/" className="auth-login-home-link">العودة إلى الصفحة الرئيسية</Link></aside><div className="auth-login-panel"><h2>تسجيل الدخول إلى حسابك</h2><LoginForm /></div></div>
      </section><p className="auth-footer-caption">© 2026 منصة مسار</p></main>
      {showInstructions ? <CompactRegistrationInstructionsDialog open mode="login" title="تعليمات مهمة قبل تسجيل الدخول" subtitle="هذه التعليمات تخص استخدام حسابك الحالي، وليست تعليمات إنشاء حساب جديد." confirmLabel="فهمت، متابعة لتسجيل الدخول" onClose={() => setShowInstructions(false)} /> : null}
    </div>
  );
}
