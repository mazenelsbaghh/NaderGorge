'use client';

import { useState } from 'react';
import { AxiosError } from 'axios';
import { useAuthStore } from '@/stores/auth-store';
import { authService } from '@/services/auth-service';
import { AccessibleDialog } from '@/components/shared/AccessibleDialog';

interface ProfileCompletionModalProps {
  isOpen: boolean;
  onClose: () => void;
  onComplete: () => void;
}

export function ProfileCompletionModal({ isOpen, onClose, onComplete }: ProfileCompletionModalProps) {
  const parentPhoneId = 'profile-completion-parent-phone';
  const governorateId = 'profile-completion-governorate';
  const districtId = 'profile-completion-district';
  const schoolNameId = 'profile-completion-school-name';
  const { updateProfile } = useAuthStore();
  const [formData, setFormData] = useState({
    parentPhone: '',
    governorate: '',
    district: '',
    schoolName: '',
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

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
    <AccessibleDialog
      open={isOpen}
      onClose={onClose}
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
          <label htmlFor={districtId} className="auth-label">المنطقة / الحي</label>
          <input
            id={districtId}
            type="text"
            required
            className="auth-input"
            placeholder="مثال: مدينة نصر"
            value={formData.district}
            autoComplete="address-level2"
            onChange={(e) => setFormData({ ...formData, district: e.target.value })}
          />
        </div>
        <div>
          <label htmlFor={schoolNameId} className="auth-label">المدرسة</label>
          <input
            id={schoolNameId}
            type="text"
            required
            className="auth-input"
            placeholder="اسم المدرسة"
            value={formData.schoolName}
            autoComplete="organization"
            onChange={(e) => setFormData({ ...formData, schoolName: e.target.value })}
          />
        </div>
        <button type="submit" disabled={loading} className="auth-btn-primary mt-2">
          {loading ? 'جاري الحفظ...' : 'حفظ ومتابعة'}
        </button>
      </form>
    </AccessibleDialog>
  );
}
