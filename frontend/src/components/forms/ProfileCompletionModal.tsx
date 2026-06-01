'use client';

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { AxiosError } from 'axios';
import { useAuthStore } from '@/stores/auth-store';
import { authService } from '@/services/auth-service';

interface ProfileCompletionModalProps {
  isOpen: boolean;
  onClose: () => void;
  onComplete: () => void;
}

export function ProfileCompletionModal({ isOpen, onClose, onComplete }: ProfileCompletionModalProps) {
  const parentPhoneId = 'profile-completion-parent-phone';
  const governorateId = 'profile-completion-governorate';
  const cityId = 'profile-completion-city';
  const schoolId = 'profile-completion-school';
  const { updateProfile } = useAuthStore();
  const [formData, setFormData] = useState({
    parentPhone: '',
    governorate: '',
    city: '',
    school: '',
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      await authService.completeProfile(formData);
      updateProfile(true);
      onComplete();
    } catch (err: unknown) {
      const message = err instanceof AxiosError
        ? err.response?.data?.message
        : 'فشل في استكمال الملف الشخصي';
      setError(message || 'فشل في استكمال الملف الشخصي');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center px-4">
          <motion.div
            initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            className="absolute inset-0 bg-[color:rgba(28,28,22,0.5)] backdrop-blur-sm"
            onClick={onClose}
          />
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ duration: 0.3 }}
            role="dialog"
            aria-modal="true"
            aria-labelledby="profile-completion-title"
            className="auth-card relative z-10 w-full max-w-md p-6 sm:p-8"
          >
            <h2 id="profile-completion-title" className="text-xl font-black text-[var(--admin-text)] mb-1">استكمال الملف الشخصي</h2>
            <p className="text-sm font-medium text-[var(--admin-muted)] mb-5">مطلوب لتفعيل كود الوصول.</p>

            {error && (
              <div className="auth-error-banner mb-4">
                {error}
              </div>
            )}

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label htmlFor={parentPhoneId} className="auth-label">رقم ولي الأمر</label>
                <div className="auth-input-wrap" dir="ltr">
                  <input
                    id={parentPhoneId}
                    type="tel"
                    required
                    className="auth-input"
                    placeholder="01XXXXXXXXX"
                    value={formData.parentPhone}
                    autoComplete="tel-national"
                    onChange={(e) => setFormData({ ...formData, parentPhone: e.target.value })}
                  />
                </div>
              </div>
              <div>
                <label htmlFor={governorateId} className="auth-label">المحافظة</label>
                <input
                  id={governorateId}
                  type="text"
                  required
                  className="auth-input"
                  placeholder="مثال: القاهرة"
                  value={formData.governorate}
                  autoComplete="address-level1"
                  onChange={(e) => setFormData({ ...formData, governorate: e.target.value })}
                />
              </div>
              <div>
                <label htmlFor={cityId} className="auth-label">المدينة / الحي</label>
                <input
                  id={cityId}
                  type="text"
                  required
                  className="auth-input"
                  placeholder="مثال: مدينة نصر"
                  value={formData.city}
                  autoComplete="address-level2"
                  onChange={(e) => setFormData({ ...formData, city: e.target.value })}
                />
              </div>
              <div>
                <label htmlFor={schoolId} className="auth-label">المدرسة</label>
                <input
                  id={schoolId}
                  type="text"
                  required
                  className="auth-input"
                  placeholder="اسم المدرسة"
                  value={formData.school}
                  autoComplete="organization"
                  onChange={(e) => setFormData({ ...formData, school: e.target.value })}
                />
              </div>
              <button type="submit" disabled={loading} className="auth-btn-primary mt-2">
                {loading ? 'جاري الحفظ...' : 'حفظ ومتابعة'}
              </button>
            </form>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}
