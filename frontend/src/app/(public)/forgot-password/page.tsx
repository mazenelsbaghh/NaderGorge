'use client';

import '../auth.css';

import { useState, useMemo } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { isAxiosError } from 'axios';
import { motion } from 'framer-motion';
import { ArrowRight, Check, Eye, EyeOff, LockKeyhole, Phone, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';

import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useAuthTheme } from '@/hooks/useAuthTheme';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { RippleGrid } from '@/components/ui/ripple-grid';
import { ShinyButton } from '@/components/ui/shiny-button';
import { authService } from '@/services/auth-service';
import { getDistrictsForGovernorate } from '@/data/governorate-districts';

const EGYPTIAN_GOVERNORATES = [
  'القاهرة', 'الجيزة', 'الإسكندرية', 'الدقهلية', 'البحيرة', 'الفيوم',
  'الغربية', 'الإسماعيلية', 'المنوفية', 'المنيا', 'القليوبية', 'الوادي الجديد',
  'السويس', 'أسوان', 'أسيوط', 'بني سويف', 'بورسعيد', 'دمياط',
  'الشرقية', 'جنوب سيناء', 'كفر الشيخ', 'مطروح', 'الأقصر', 'قنا',
  'شمال سيناء', 'سوهاج', 'البحر الأحمر',
];

export default function ForgotPasswordPage() {
  const router = useRouter();
  const { isDark, toggleTheme } = useAuthTheme();
  useRootOverscrollBackground();

  // Wizard state
  const [step, setStep] = useState<1 | 2>(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Step 1: Verification fields
  const [phoneNumber, setPhoneNumber] = useState('');
  const [parentPhone, setParentPhone] = useState('');
  const [governorate, setGovernorate] = useState('');
  const [district, setDistrict] = useState('');
  const [resetToken, setResetToken] = useState('');

  // Step 2: New password fields
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const selectStyle = { backgroundColor: 'var(--admin-card-soft)', color: 'var(--admin-text)' };
  const optionStyle = { background: 'var(--admin-bg)', color: 'var(--admin-text)' };

  // Load districts dynamically when governorate changes
  const districts = useMemo(() => {
    return governorate ? getDistrictsForGovernorate(governorate) : [];
  }, [governorate]);

  // Passwords validator checklist
  const passwordChecklist = useMemo(() => {
    return [
      { label: '8 أحرف على الأقل', valid: newPassword.length >= 8 },
      { label: 'كلمتا المرور متطابقتان', valid: !!confirmPassword && newPassword === confirmPassword },
    ];
  }, [newPassword, confirmPassword]);

  // Step 1: Verify Profile Fields
  const handleVerify = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!phoneNumber || !parentPhone || !governorate || !district) {
      setError('يرجى ملء جميع الحقول المطلوبة.');
      return;
    }

    setLoading(true);
    try {
      const response = await authService.verifyResetFields({
        phoneNumber,
        parentPhone,
        governorate,
        district,
      });

      const token = response.data.data.resetToken;
      setResetToken(token);
      setStep(2);
      toast.success('تم التحقق من بياناتك بنجاح. يرجى تعيين كلمة المرور الجديدة.');
    } catch (err: unknown) {
      const message = isAxiosError<{ message?: string }>(err)
        ? err.response?.data?.message
        : undefined;

      setError(message || 'عذرًا، فشل التحقق من البيانات. تأكد من صحتها وحاول مجددًا.');
    } finally {
      setLoading(false);
    }
  };

  // Step 2: Reset Password
  const handleReset = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (newPassword.length < 8) {
      setError('يجب أن تتكون كلمة المرور من 8 أحرف على الأقل.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setError('كلمتا المرور غير متطابقتين.');
      return;
    }

    setLoading(true);
    try {
      const response = await authService.resetPassword({
        token: resetToken,
        newPassword,
      });

      toast.success(response.data.message || 'تم تغيير كلمة المرور بنجاح.');
      router.push('/login');
    } catch (err: unknown) {
      const message = isAxiosError<{ message?: string }>(err)
        ? err.response?.data?.message
        : undefined;

      setError(message || 'عذرًا، فشل تغيير كلمة المرور. قد يكون الرابط منتهي الصلاحية.');
    } finally {
      setLoading(false);
    }
  };

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

      {/* ── Main Content ── */}
      <main className="relative z-10 w-full max-w-lg px-4 py-10 sm:px-5 sm:py-16 m-auto">
        {/* Logo Avatar */}
        <div className="auth-avatar mb-8">☥</div>

        {/* Heading */}
        <div className="mx-auto mb-8 text-center flex flex-col items-center">
          <h1
            className="text-2xl font-extrabold tracking-tight sm:text-3xl"
            style={{ color: 'var(--admin-text)' }}
          >
            إعادة تعيين كلمة المرور
          </h1>
          <p
            className="mt-2 text-sm font-light text-center max-w-sm"
            style={{ color: 'var(--admin-muted)' }}
          >
            {step === 1 
              ? 'يرجى إدخال بيانات حسابك الأكاديمي للتحقق من هويتك أولاً.' 
              : 'الآن، قم بتعيين كلمة مرور جديدة قوية لحسابك.'}
          </p>
        </div>

        {/* Glass Card */}
        <div className="space-y-5 rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 backdrop-blur-md sm:rounded-[28px] sm:p-7 shadow-[0_12px_40px_var(--admin-shadow)]">
          
          {error && <div className="auth-error-banner mb-2">{error}</div>}

          {step === 1 ? (
            <form onSubmit={handleVerify} className="space-y-5">
              {/* ── Student Phone Number ── */}
              <div>
                <label className="auth-label" htmlFor="reset-phone">
                  رقم هاتف الطالب
                </label>
                <div className="auth-input-wrap" dir="ltr">
                  <input
                    id="reset-phone"
                    type="tel"
                    required
                    className="auth-input"
                    placeholder="01XXXXXXXXX"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    style={{ paddingRight: '2.75rem' }}
                  />
                  <span className="auth-input-icon">
                    <Phone size={15} />
                  </span>
                </div>
              </div>

              {/* ── Parent Phone Number ── */}
              <div>
                <label className="auth-label" htmlFor="reset-parentPhone">
                  رقم هاتف ولي الأمر
                </label>
                <div className="auth-input-wrap" dir="ltr">
                  <input
                    id="reset-parentPhone"
                    type="tel"
                    required
                    className="auth-input"
                    placeholder="01XXXXXXXXX"
                    value={parentPhone}
                    onChange={(e) => setParentPhone(e.target.value)}
                    style={{ paddingRight: '2.75rem' }}
                  />
                  <span className="auth-input-icon">
                    <Phone size={15} />
                  </span>
                </div>
              </div>

              {/* ── Governorate Dropdown ── */}
              <div>
                <label className="auth-label" htmlFor="reset-governorate">
                  المحافظة
                </label>
                <select
                  id="reset-governorate"
                  required
                  data-select
                  className="auth-input"
                  value={governorate}
                  onChange={(e) => {
                    setGovernorate(e.target.value);
                    setDistrict(''); // Reset district
                  }}
                  style={selectStyle}
                >
                  <option value="" disabled style={optionStyle}>اختر المحافظة...</option>
                  {EGYPTIAN_GOVERNORATES.map((gov) => (
                    <option key={gov} value={gov} style={optionStyle}>{gov}</option>
                  ))}
                </select>
              </div>

              {/* ── District Dropdown ── */}
              <div>
                <label className="auth-label" htmlFor="reset-district">
                  المنطقة / الحي
                </label>
                <select
                  id="reset-district"
                  required
                  data-select
                  className="auth-input"
                  value={district}
                  onChange={(e) => setDistrict(e.target.value)}
                  style={selectStyle}
                  disabled={!governorate}
                >
                  <option value="" disabled style={optionStyle}>
                    {governorate ? 'اختر المنطقة / الحي...' : 'اختر المحافظة أولاً'}
                  </option>
                  {districts.map((d) => (
                    <option key={d} value={d} style={optionStyle}>{d}</option>
                  ))}
                </select>
              </div>

              {/* ── Submit button ── */}
              <div style={{ '--landing-accent': 'var(--admin-primary)', '--landing-ink': 'var(--admin-text)' } as React.CSSProperties}>
                <ShinyButton
                  type="submit"
                  disabled={loading}
                  className="w-full h-12 flex items-center justify-center mt-2 group"
                >
                  {loading ? 'جاري التحقق...' : 'التالي'}
                </ShinyButton>
              </div>
            </form>
          ) : (
            <form onSubmit={handleReset} className="space-y-5">
              {/* ── Password Checklist ── */}
              <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)]/80 p-4 mb-2 space-y-2">
                <p className="text-[10px] font-black uppercase tracking-[0.2em] text-[var(--admin-primary)] mb-1">متطلبات الأمان</p>
                {passwordChecklist.map((item) => (
                  <div key={item.label} className="flex items-center justify-between text-xs">
                    <span style={{ color: item.valid ? 'var(--admin-text)' : 'var(--admin-muted)' }}>{item.label}</span>
                    <span className={`flex h-5 w-5 items-center justify-center rounded-full ${item.valid ? 'bg-[var(--admin-primary)] text-[var(--admin-bg)]' : 'bg-[var(--admin-border)]/50 text-[var(--admin-muted)]'}`}>
                      <Check className="h-3 w-3" />
                    </span>
                  </div>
                ))}
              </div>

              {/* ── New Password ── */}
              <div>
                <label className="auth-label" htmlFor="reset-newPassword">
                  كلمة المرور الجديدة
                </label>
                <div className="auth-input-wrap" dir="ltr">
                  <input
                    id="reset-newPassword"
                    type={showPassword ? 'text' : 'password'}
                    required
                    className="auth-input"
                    placeholder="••••••••"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    style={{ paddingRight: '2.75rem' }}
                  />
                  <button
                    type="button"
                    className="auth-input-action"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={showPassword ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'}
                  >
                    {showPassword ? <EyeOff size={15} /> : <Eye size={15} />}
                  </button>
                </div>
              </div>

              {/* ── Confirm Password ── */}
              <div>
                <label className="auth-label" htmlFor="reset-confirmPassword">
                  تأكيد كلمة المرور الجديدة
                </label>
                <div className="auth-input-wrap" dir="ltr">
                  <input
                    id="reset-confirmPassword"
                    type={showConfirmPassword ? 'text' : 'password'}
                    required
                    className="auth-input"
                    placeholder="••••••••"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    style={{ paddingRight: '2.75rem' }}
                  />
                  <button
                    type="button"
                    className="auth-input-action"
                    onClick={() => setShowConfirmPassword((v) => !v)}
                    aria-label={showConfirmPassword ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'}
                  >
                    {showConfirmPassword ? <EyeOff size={15} /> : <Eye size={15} />}
                  </button>
                </div>
              </div>

              {/* ── Submit button ── */}
              <div style={{ '--landing-accent': 'var(--admin-primary)', '--landing-ink': 'var(--admin-text)' } as React.CSSProperties}>
                <ShinyButton
                  type="submit"
                  disabled={loading || passwordChecklist.some(item => !item.valid)}
                  className="w-full h-12 flex items-center justify-center mt-2 group"
                >
                  {loading ? 'جاري الحفظ...' : 'تأكيد تغيير كلمة المرور'}
                </ShinyButton>
              </div>
            </form>
          )}

          <div className="auth-divider my-6" />

          <p className="text-center text-sm" style={{ color: 'var(--admin-muted)' }}>
            تذكرت كلمة المرور؟{' '}
            <Link
              href="/login"
              className="font-bold transition-colors hover:opacity-80"
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
