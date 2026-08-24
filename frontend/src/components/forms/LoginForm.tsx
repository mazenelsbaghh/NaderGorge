'use client';

/**
 * LoginForm Component
 *
 * Uses auth.css utility classes (.auth-input, .auth-label, .auth-btn-primary …)
 * and inline --admin-* CSS vars (injected by parent page via useAuthTheme).
 *
 * API: POST /auth/login → { accessToken, user } plus an HttpOnly refresh cookie.
 * On success: redirects Admin/Teacher to /admin, Students to /student.
 */

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import axios from 'axios';

import { useAuthStore } from '@/stores/auth-store';
import { authService, getDeviceFingerprint } from '@/services/auth-service';
import { getSurfaceOrigins, getSurfaceName } from '@/packages/surface-runtime/config';
import { resolveReturnNavigation } from '@/lib/safe-return-url';

export function LoginForm() {
  const { setAuth } = useAuthStore();
  const router = useRouter();

  const [formData, setFormData] = useState({ phoneNumber: '', password: '' });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(true);
  const errorRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!error) return;

    // The login page has its own scroll container. When an error is inserted
    // above the fields, keep the feedback in view without jumping the document
    // to an arbitrary position or hiding the submit action.
    const frame = window.requestAnimationFrame(() => {
      errorRef.current?.scrollIntoView({
        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches
          ? 'auto'
          : 'smooth',
        block: 'nearest',
        inline: 'nearest',
      });
    });

    return () => window.cancelAnimationFrame(frame);
  }, [error]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const { data } = await authService.login({
        phoneNumber: formData.phoneNumber,
        password: formData.password,
        deviceFingerprint: getDeviceFingerprint(),
        deviceName: navigator.userAgent.slice(0, 100),
      });

      const { accessToken, user } = data.data;

      // Role-based check
      const isStaff = user.roles.length > 0 && !user.roles.includes('Student');

      // On landing surface: block staff — they must use the admin portal
      const surface = getSurfaceName();
      if (isStaff && surface === 'landing') {
        setError('هذا الحساب مخصص للإدارة فقط. يرجى تسجيل الدخول من بوابة الإدارة.');
        return;
      }

      setAuth(
        {
          id: user.id,
          fullName: user.fullName,
          phone: user.phone,
          roles: user.roles,
          profileComplete: user.profileComplete,
          permissions: user.permissions || [],
          allowedDomains: user.allowedDomains || [],
          allowedNavbarItems: user.allowedNavbarItems || [],
        },
        accessToken,
        rememberMe
      );

      const roles = user.roles || [];
      const allowedDomains = user.allowedDomains || [];
      const origins = getSurfaceOrigins();
      let redirectDestination = `${origins.student}/student`;

      const hasAdmin = allowedDomains.includes('admin') || roles.some((r: string) => r.toLowerCase().includes('admin') || r.toLowerCase().includes('supervisor'));
      const hasTeacher = allowedDomains.includes('teacher') || roles.some((r: string) => r.toLowerCase().includes('teacher'));
      const hasAssistant = allowedDomains.includes('assistant') || roles.some((r: string) => r.toLowerCase().includes('assistant') || r.toLowerCase().includes('staff'));
      const isEmployee = roles.some((r: string) => r.toLowerCase() === 'employee');

      if (hasAdmin) {
        redirectDestination = `${origins.admin}/admin`;
      } else if (hasTeacher) {
        redirectDestination = `${origins.teacher}/teacher`;
      } else if (isEmployee) {
        redirectDestination = `${origins.assistant}/employee`;
      } else if (hasAssistant) {
        redirectDestination = `${origins.assistant}/assistant`;
      }

      let targetUrl = '';
      if (typeof window !== 'undefined') {
        const params = new URLSearchParams(window.location.search);
        targetUrl = params.get('returnUrl') || '';
      }

      const navigation = resolveReturnNavigation({
        returnUrl: targetUrl,
        defaultDestination: redirectDestination,
        surface: getSurfaceName(),
        currentOrigin: window.location.origin,
      });
      if (navigation.sameOrigin) {
        router.replace(navigation.href);
      } else {
        window.location.replace(navigation.href);
      }
    } catch (error: unknown) {
      const message = axios.isAxiosError<{ message?: string }>(error)
        ? error.response?.data?.message
        : undefined;

      setError(message || 'فشل تسجيل الدخول. تأكد من البيانات.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-5"
    >
      {/* ── Error Banner ── */}
      {error && (
        <div
          ref={errorRef}
          role="alert"
          aria-live="assertive"
          className="auth-error-banner"
        >
          {error}
        </div>
      )}

      {/* ── Phone Number ── */}
      <div>
        <label className="auth-label" htmlFor="login-phone">
          رقم الهاتف
        </label>
        <div className="auth-input-wrap" dir="ltr">
          <input
            id="login-phone"
            name="phoneNumber"
            type="tel"
            required
            className="auth-input"
            placeholder="01XXXXXXXXX"
            value={formData.phoneNumber}
            onChange={(e) =>
              setFormData({ ...formData, phoneNumber: e.target.value })
            }
            style={{ paddingRight: '2.75rem' }}
          />
          <span className="auth-input-icon">
            <span aria-hidden="true" className="text-sm leading-none">☎</span>
          </span>
        </div>
      </div>

      {/* ── Password ── */}
      <div>
        <label className="auth-label" htmlFor="login-password">
          كلمة المرور
        </label>
        <div className="auth-input-wrap" dir="ltr">
          <input
            id="login-password"
            name="password"
            type={showPassword ? 'text' : 'password'}
            required
            className="auth-input"
            placeholder="••••••••"
            value={formData.password}
            onChange={(e) =>
              setFormData({ ...formData, password: e.target.value })
            }
            style={{ paddingRight: '2.75rem' }}
          />
          <button
            type="button"
            className="auth-input-action"
            onClick={() => setShowPassword((value) => !value)}
            aria-label={showPassword ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'}
          >
            <span aria-hidden="true" className="text-sm leading-none">
              {showPassword ? '◌' : '◉'}
            </span>
          </button>
        </div>
      </div>

      {/* ── Remember me / Forgot ── */}
      <div className="auth-remember-row">
        <label
          htmlFor="login-remember"
          className="group relative flex cursor-pointer select-none items-center gap-3"
        >
          <input
            id="login-remember"
            type="checkbox"
            checked={rememberMe}
            onChange={(event) => setRememberMe(event.target.checked)}
            className="peer sr-only"
          />
          <span
            aria-hidden="true"
            className="inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-lg border-2 border-[var(--admin-border)] bg-[var(--admin-card)] text-sm font-black text-white transition peer-checked:border-[var(--admin-primary)] peer-checked:bg-[var(--admin-primary)] peer-focus-visible:ring-2 peer-focus-visible:ring-[var(--admin-primary)] peer-focus-visible:ring-offset-2 peer-focus-visible:ring-offset-[var(--admin-card)]"
          >
            {rememberMe ? '✓' : ''}
          </span>
          <span className="text-sm font-bold text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)]">
            تذكرني
          </span>
        </label>
        <Link
          href="/forgot-password"
          className="text-xs font-bold underline-offset-2 hover:underline"
          style={{ color: 'var(--admin-primary)' }}
        >
          نسيت كلمة المرور؟
        </Link>
      </div>

      {/* ── Submit Button ── */}
      <div>
        <button
          type="submit"
          disabled={loading}
          className="auth-btn-primary mt-2 flex h-12 w-full items-center justify-center"
        >
          {loading ? 'جاري التحقق...' : 'تسجيل الدخول'}
        </button>
      </div>
    </form>
  );
}
