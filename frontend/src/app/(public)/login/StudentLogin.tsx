'use client';

import './student-login.css';

import { useCallback, useState } from 'react';
import dynamic from 'next/dynamic';
import Image from 'next/image';
import Link from 'next/link';
import { Moon, Sun } from 'lucide-react';

import { LoginForm } from '@/components/forms/LoginForm';
import { PLATFORM_IDENTITY } from '@/packages/brand';

const LoginInstructions = dynamic(
  () => import('@/components/registration/CompactRegistrationInstructionsDialog')
    .then((module) => module.CompactRegistrationInstructionsDialog),
  { ssr: false },
);

export function StudentLogin({ isDark, onToggleTheme }: {
  isDark: boolean;
  onToggleTheme: () => void;
}) {
  const [showInstructions, setShowInstructions] = useState(false);
  const closeInstructions = useCallback(() => setShowInstructions(false), []);

  return (
    <div className="auth-shell student-login" data-theme={isDark ? 'dark' : 'light'}>
      <button
        className="student-login__theme"
        type="button"
        onClick={onToggleTheme}
        aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
      >
        {isDark ? <Sun size={24} aria-hidden="true" /> : <Moon size={24} aria-hidden="true" />}
      </button>

      <main className="student-login__main">
        <section className="student-login__content" aria-labelledby="student-login-title">
          <header className="student-login__heading">
            <div className="student-login__logo">
              <Image
                src={isDark ? PLATFORM_IDENTITY.logo.markLight : PLATFORM_IDENTITY.logo.full}
                alt={PLATFORM_IDENTITY.logo.alt}
                width={220}
                height={220}
                preload
              />
            </div>
            <h1 id="student-login-title">أهلًا برجوعك</h1>
            <p>سجّل دخولك وكمّل دروسك</p>
          </header>

          <LoginForm />

          <div className="student-login__registration">
            <p className="student-login__divider">جديد على مسار؟</p>
            <Link href="/register" className="student-login__register">إنشاء حساب طالب</Link>
          </div>
          <button
            type="button"
            className="student-login__help"
            onClick={() => setShowInstructions(true)}
            aria-haspopup="dialog"
          >
            محتاج مساعدة؟
          </button>
        </section>
      </main>

      {showInstructions && (
        <LoginInstructions
          open
          mode="login"
          title="مساعدة في تسجيل الدخول"
          subtitle="راجع تعليمات استخدام حسابك قبل الدخول."
          confirmLabel="العودة لتسجيل الدخول"
          onClose={closeInstructions}
        />
      )}
    </div>
  );
}
