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
import { useState } from 'react';
import { isAxiosError } from 'axios';
import { motion, useReducedMotion } from 'framer-motion';
import { Eye, EyeOff, Phone } from 'lucide-react';

import { useAuthStore } from '@/stores/auth-store';
import { authService, getDeviceFingerprint } from '@/services/auth-service';
import { Checkbox, Label } from '@/components/ui/checkbox';
import { ShinyButton } from '@/components/ui/shiny-button';
import { getSurfaceOrigins } from '@/packages/surface-runtime/config';

export function LoginForm() {
  const { setAuth } = useAuthStore();
  const reduceMotion = useReducedMotion();

  const [formData, setFormData] = useState({ phoneNumber: '', password: '' });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);

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
      const surface = process.env.NEXT_PUBLIC_APP_SURFACE;
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
        },
        accessToken,
        rememberMe
      );

      const roles = user.roles || [];
      const origins = getSurfaceOrigins();
      let redirectDestination = `${origins.student}/student`;

      if (roles.includes('Admin') || roles.includes('Supervisor')) {
        redirectDestination = `${origins.admin}/admin`;
      } else if (roles.includes('Teacher')) {
        redirectDestination = `${origins.teacher}/teacher`;
      } else if (roles.includes('Assistant') || roles.includes('Staff')) {
        redirectDestination = `${origins.assistant}/assistant`;
      }

      let targetUrl = '';
      if (typeof window !== 'undefined') {
        const params = new URLSearchParams(window.location.search);
        targetUrl = params.get('returnUrl') || '';
      }

      if (targetUrl) {
        window.location.replace(targetUrl);
      } else {
        window.location.replace(redirectDestination);
      }
    } catch (error: unknown) {
      const message = isAxiosError<{ message?: string }>(error)
        ? error.response?.data?.message
        : undefined;

      setError(message || 'فشل تسجيل الدخول. تأكد من البيانات.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <motion.form
      onSubmit={handleSubmit}
      initial={reduceMotion ? false : { opacity: 0, y: 10 }}
      animate={reduceMotion ? {} : { opacity: 1, y: 0 }}
      transition={{ duration: 0.28, ease: [0.22, 1, 0.36, 1] }}
      className="space-y-5"
    >
      {/* ── Error Banner ── */}
      {error && (
        <motion.div
          className="auth-error-banner"
          initial={reduceMotion ? false : { opacity: 0, y: -6 }}
          animate={reduceMotion ? {} : { opacity: 1, y: 0 }}
          transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
        >
          {error}
        </motion.div>
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
            <Phone size={15} />
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
            {showPassword ? <EyeOff size={15} /> : <Eye size={15} />}
          </button>
        </div>
      </div>

      {/* ── Remember me / Forgot ── */}
      <div className="auth-remember-row">
        <Checkbox
          id="login-remember"
          isSelected={rememberMe}
          onChange={setRememberMe}
        >
          <Checkbox.Control>
            <Checkbox.Indicator />
          </Checkbox.Control>
          <Checkbox.Content>
            <Label className="text-[var(--admin-text)]">تذكرني</Label>
          </Checkbox.Content>
        </Checkbox>
        <Link
          href="/forgot-password"
          className="text-xs font-bold underline-offset-2 hover:underline"
          style={{ color: 'var(--admin-primary)' }}
        >
          نسيت كلمة المرور؟
        </Link>
      </div>

      {/* ── Submit Button ── */}
      <div style={{ '--landing-accent': 'var(--admin-primary)', '--landing-ink': 'var(--admin-text)' } as React.CSSProperties}>
        <ShinyButton
          type="submit"
          disabled={loading}
          className="w-full h-12 flex items-center justify-center mt-2 group"
        >
          {loading ? 'جاري التحقق...' : 'تسجيل الدخول'}
        </ShinyButton>
      </div>
    </motion.form>
  );
}
