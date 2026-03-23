'use client';

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAuthStore } from '@/stores/auth-store';
import { authService } from '@/services/auth-service';

interface ProfileCompletionModalProps {
  isOpen: boolean;
  onClose: () => void;
  onComplete: () => void;
}

export function ProfileCompletionModal({ isOpen, onClose, onComplete }: ProfileCompletionModalProps) {
  const { updateProfile } = useAuthStore();
  const [formData, setFormData] = useState({
    parentPhone: '',
    governorate: '',
    city: '',
    school: '',
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
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to complete profile');
    } finally {
      setLoading(false);
    }
  };

  const inputClass =
    'w-full rounded-xl border border-gray-200 bg-white/50 px-4 py-3 text-sm text-gray-900 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/20 transition-all dark:border-gray-700 dark:bg-gray-800/50 dark:text-white';
  const labelClass = 'block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5';

  return (
    <AnimatePresence>
      {isOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          <motion.div
            initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            className="absolute inset-0 bg-black/50 backdrop-blur-sm"
            onClick={onClose}
          />
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ duration: 0.3 }}
            className="relative z-10 w-full max-w-md rounded-2xl border border-gray-200 bg-white p-6 shadow-2xl dark:border-gray-700 dark:bg-gray-900"
          >
            <h2 className="text-xl font-bold text-gray-900 dark:text-white mb-1">Complete Your Profile</h2>
            <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">This is required to activate your access code.</p>

            {error && (
              <div className="rounded-xl bg-red-50 border border-red-200 p-3 text-sm text-red-600 mb-4 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
                {error}
              </div>
            )}

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className={labelClass}>Parent Phone Number</label>
                <input type="tel" required className={inputClass} placeholder="Parent's phone" dir="ltr"
                  value={formData.parentPhone} onChange={(e) => setFormData({ ...formData, parentPhone: e.target.value })} />
              </div>
              <div>
                <label className={labelClass}>Governorate</label>
                <input type="text" required className={inputClass} placeholder="e.g., Cairo"
                  value={formData.governorate} onChange={(e) => setFormData({ ...formData, governorate: e.target.value })} />
              </div>
              <div>
                <label className={labelClass}>City</label>
                <input type="text" required className={inputClass} placeholder="e.g., Nasr City"
                  value={formData.city} onChange={(e) => setFormData({ ...formData, city: e.target.value })} />
              </div>
              <div>
                <label className={labelClass}>School</label>
                <input type="text" required className={inputClass} placeholder="School name"
                  value={formData.school} onChange={(e) => setFormData({ ...formData, school: e.target.value })} />
              </div>
              <button type="submit" disabled={loading}
                className="w-full rounded-xl bg-gradient-to-r from-blue-600 to-indigo-600 px-6 py-3 text-sm font-semibold text-white shadow-lg shadow-blue-500/25 hover:from-blue-700 hover:to-indigo-700 disabled:opacity-50 transition-all">
                {loading ? 'Saving...' : 'Save & Continue'}
              </button>
            </form>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}
