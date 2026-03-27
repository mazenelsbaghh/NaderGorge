'use client';

/**
 * LoginForm Component
 *
 * Uses auth.css utility classes (.auth-input, .auth-label, .auth-btn-primary …)
 * and inline --admin-* CSS vars (injected by parent page via useAuthTheme).
 *
 * API: POST /auth/login → { accessToken, refreshToken, user }
 * On success: redirects Admin/Teacher to /admin, Students to /student.
 */

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { Eye, EyeOff, Phone } from 'lucide-react';

import { useAuthStore } from '@/stores/auth-store';
import { authService, getDeviceFingerprint } from '@/services/auth-service';

export function LoginForm() {
  const router = useRouter();
  const { setAuth } = useAuthStore();

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

      const { accessToken, refreshToken, user } = data.data;

      setAuth(
        {
          id: user.id,
          fullName: user.fullName,
          phone: user.phone,
          roles: user.roles,
          profileComplete: user.profileComplete,
        },
        accessToken,
        refreshToken,
        rememberMe
      );

      // Role-based redirect (same logic as original LoginForm)
      const isStaff = ['Admin', 'Teacher', 'Assistant'].some((r) =>
        user.roles.includes(r)
      );
      router.push(isStaff ? '/admin' : '/student');
    } catch (err: any) {
      setError(
        err.response?.data?.message || 'فشل تسجيل الدخول. تأكد من البيانات.'
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <motion.form
      onSubmit={handleSubmit}
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35 }}
      className="space-y-5"
    >
      {/* ── Error Banner ── */}
      {error && <div className="auth-error-banner">{error}</div>}

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
        <label className="auth-checkbox">
          <input
            type="checkbox"
            className="auth-checkbox__input"
            checked={rememberMe}
            onChange={(e) => setRememberMe(e.target.checked)}
          />
          <span className="auth-checkbox__mark" aria-hidden="true" />
          <span className="auth-checkbox__label">تذكرني</span>
        </label>
        <a
          href="#"
          className="text-xs font-bold underline-offset-2 hover:underline"
          style={{ color: 'var(--admin-primary)' }}
        >
          نسيت كلمة المرور؟
        </a>
      </div>

      {/* ── Submit Button ── */}
      <button type="submit" disabled={loading} className="auth-btn-primary">
        {loading ? 'جاري التحقق...' : 'تسجيل الدخول'}
      </button>
    </motion.form>
  );
}
