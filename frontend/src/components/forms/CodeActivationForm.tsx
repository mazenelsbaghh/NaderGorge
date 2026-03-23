'use client';

import { useState } from 'react';
import { motion } from 'framer-motion';
import { authService } from '@/services/auth-service';

interface CodeActivationFormProps {
  onRequiresProfile: () => void;
  onSuccess: () => void;
}

export function CodeActivationForm({ onRequiresProfile, onSuccess }: CodeActivationFormProps) {
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    setLoading(true);

    try {
      const { data } = await authService.activateCode(code);
      if (data.data.requiresProfileCompletion) {
        onRequiresProfile();
      } else {
        setSuccess(data.data.message);
        setCode('');
        onSuccess();
      }
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to activate code');
    } finally {
      setLoading(false);
    }
  };

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
      {success && (
        <div className="rounded-xl bg-green-50 border border-green-200 p-3 text-sm text-green-600 dark:bg-green-900/20 dark:border-green-800 dark:text-green-400">
          {success}
        </div>
      )}

      <div className="flex gap-3">
        <input
          type="text"
          required
          minLength={6}
          placeholder="Enter your access code"
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
          className="flex-1 rounded-xl border border-gray-200 bg-white/50 px-4 py-3 text-sm font-mono tracking-widest text-gray-900 placeholder:text-gray-400 placeholder:tracking-normal placeholder:font-sans focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/20 transition-all dark:border-gray-700 dark:bg-gray-800/50 dark:text-white"
          dir="ltr"
        />
        <button
          type="submit"
          disabled={loading || code.length < 6}
          className="rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 px-6 py-3 text-sm font-semibold text-white shadow-lg shadow-emerald-500/25 hover:from-emerald-700 hover:to-teal-700 disabled:opacity-50 transition-all whitespace-nowrap"
        >
          {loading ? 'Activating...' : 'Activate Code'}
        </button>
      </div>
    </motion.form>
  );
}
