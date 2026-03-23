'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { authService } from '@/services/auth-service';

export function RegistrationForm() {
  const router = useRouter();
  const [formData, setFormData] = useState({
    fullName: '',
    phoneNumber: '',
    password: '',
    confirmPassword: '',
    grade: '',
    track: '',
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (formData.password !== formData.confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setLoading(true);
    try {
      await authService.register({
        fullName: formData.fullName,
        phoneNumber: formData.phoneNumber,
        password: formData.password,
        grade: formData.grade || undefined,
        track: formData.track || undefined,
      });
      router.push('/login?registered=true');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Registration failed');
    } finally {
      setLoading(false);
    }
  };

  const inputClass =
    'w-full rounded-xl border border-gray-200 bg-white/50 px-4 py-3 text-sm text-gray-900 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/20 transition-all dark:border-gray-700 dark:bg-gray-800/50 dark:text-white';
  const labelClass = 'block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5';

  return (
    <motion.form
      onSubmit={handleSubmit}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4 }}
      className="space-y-4"
    >
      {error && (
        <div className="rounded-xl bg-red-50 border border-red-200 p-3 text-sm text-red-600 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {error}
        </div>
      )}

      <div>
        <label className={labelClass}>Full Name</label>
        <input
          type="text" required className={inputClass} placeholder="Enter your full name"
          value={formData.fullName} onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
        />
      </div>

      <div>
        <label className={labelClass}>Phone Number</label>
        <input
          type="tel" required className={inputClass} placeholder="01XXXXXXXXX" dir="ltr"
          value={formData.phoneNumber} onChange={(e) => setFormData({ ...formData, phoneNumber: e.target.value })}
        />
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className={labelClass}>Grade</label>
          <select className={inputClass} value={formData.grade} onChange={(e) => setFormData({ ...formData, grade: e.target.value })}>
            <option value="">Select Grade</option>
            <option value="1st Secondary">1st Secondary</option>
            <option value="2nd Secondary">2nd Secondary</option>
            <option value="3rd Secondary">3rd Secondary</option>
          </select>
        </div>
        <div>
          <label className={labelClass}>Track</label>
          <select className={inputClass} value={formData.track} onChange={(e) => setFormData({ ...formData, track: e.target.value })}>
            <option value="">Select Track</option>
            <option value="Scientific">Scientific</option>
            <option value="Literary">Literary</option>
            <option value="Math">Math</option>
          </select>
        </div>
      </div>

      <div>
        <label className={labelClass}>Password</label>
        <input
          type="password" required minLength={6} className={inputClass} placeholder="Min 6 characters"
          value={formData.password} onChange={(e) => setFormData({ ...formData, password: e.target.value })}
        />
      </div>
      <div>
        <label className={labelClass}>Confirm Password</label>
        <input
          type="password" required className={inputClass} placeholder="Re-enter password"
          value={formData.confirmPassword} onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
        />
      </div>

      <button
        type="submit" disabled={loading}
        className="w-full rounded-xl bg-gradient-to-r from-blue-600 to-indigo-600 px-6 py-3 text-sm font-semibold text-white shadow-lg shadow-blue-500/25 hover:from-blue-700 hover:to-indigo-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50 transition-all"
      >
        {loading ? 'Creating Account...' : 'Create Account'}
      </button>
    </motion.form>
  );
}
