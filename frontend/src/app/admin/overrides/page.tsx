'use client';

import { useState } from 'react';
import { adminService } from '@/services/admin-service';
import { motion } from 'framer-motion';

export default function AdminOverridesPage() {
  // Watch Limit Reset
  const [wVideoId, setWVideoId] = useState('');
  const [wStudentId, setWStudentId] = useState('');
  const [wLoading, setWLoading] = useState(false);
  const [wResult, setWResult] = useState<string | null>(null);

  // Lesson Unlock
  const [uLessonId, setULessonId] = useState('');
  const [uStudentId, setUStudentId] = useState('');
  const [uLoading, setULoading] = useState(false);
  const [uResult, setUResult] = useState<string | null>(null);

  async function handleResetWatch(e: React.FormEvent) {
    e.preventDefault();
    setWLoading(true);
    setWResult(null);
    try {
      const res = await adminService.resetWatchLimit(wVideoId, wStudentId);
      setWResult(res.message || 'Watch limit successfully reset.');
      setWVideoId('');
      setWStudentId('');
    } catch (err: any) {
      setWResult(err?.response?.data?.message || 'Failed to reset watch limit.');
    } finally {
      setWLoading(false);
    }
  }

  async function handleUnlockLesson(e: React.FormEvent) {
    e.preventDefault();
    setULoading(true);
    setUResult(null);
    try {
      const res = await adminService.manualUnlockLesson(uLessonId, uStudentId);
      setUResult(res.message || 'Lesson successfully unlocked.');
      setULessonId('');
      setUStudentId('');
    } catch (err: any) {
      setUResult(err?.response?.data?.message || 'Failed to unlock lesson.');
    } finally {
      setULoading(false);
    }
  }

  return (
    <div className="container mx-auto p-6 max-w-4xl space-y-10">
      <div>
        <h1 className="text-3xl font-extrabold text-gray-900 dark:text-white">Admin Overrides</h1>
        <p className="text-gray-500 mt-1">Manually reset watch limits or unlock gated lessons for specific students.</p>
      </div>

      {/* Watch Limit Reset */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }}
        className="rounded-2xl border border-amber-200 dark:border-amber-900/40 bg-gradient-to-br from-amber-50 to-orange-50 dark:from-amber-900/10 dark:to-orange-900/10 p-8 shadow-lg"
      >
        <div className="flex items-center gap-3 mb-6">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-amber-500 text-white font-bold text-lg">🔄</div>
          <div>
            <h2 className="text-xl font-bold text-gray-900 dark:text-white">Reset Video Watch Limit</h2>
            <p className="text-sm text-gray-500">Clears all watch events for a specific student + video, allowing them to re-watch.</p>
          </div>
        </div>

        <form onSubmit={handleResetWatch} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-semibold mb-1.5 text-gray-700 dark:text-gray-300">Lesson Video ID</label>
              <input type="text" value={wVideoId} onChange={e => setWVideoId(e.target.value)} required 
                placeholder="Enter the GUID of the video"
                className="w-full rounded-xl border border-amber-200 dark:border-amber-800 bg-white dark:bg-gray-800 p-3 shadow-sm focus:ring-2 focus:ring-amber-400 outline-none"
              />
            </div>
            <div>
              <label className="block text-sm font-semibold mb-1.5 text-gray-700 dark:text-gray-300">Student User ID</label>
              <input type="text" value={wStudentId} onChange={e => setWStudentId(e.target.value)} required 
                placeholder="Enter the student's GUID"
                className="w-full rounded-xl border border-amber-200 dark:border-amber-800 bg-white dark:bg-gray-800 p-3 shadow-sm focus:ring-2 focus:ring-amber-400 outline-none"
              />
            </div>
          </div>
          <div className="flex items-center justify-between">
            <button type="submit" disabled={wLoading}
              className="rounded-xl bg-amber-600 px-8 py-3 font-bold text-white shadow-lg hover:bg-amber-700 disabled:opacity-50 transition-all hover:scale-105"
            >
              {wLoading ? 'Resetting...' : 'Reset Watch Limit'}
            </button>
            {wResult && (
              <motion.span initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="text-sm font-semibold text-amber-700 dark:text-amber-400">
                {wResult}
              </motion.span>
            )}
          </div>
        </form>
      </motion.div>

      {/* Manual Lesson Unlock */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 }}
        className="rounded-2xl border border-emerald-200 dark:border-emerald-900/40 bg-gradient-to-br from-emerald-50 to-teal-50 dark:from-emerald-900/10 dark:to-teal-900/10 p-8 shadow-lg"
      >
        <div className="flex items-center gap-3 mb-6">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-emerald-500 text-white font-bold text-lg">🔓</div>
          <div>
            <h2 className="text-xl font-bold text-gray-900 dark:text-white">Manual Lesson Unlock</h2>
            <p className="text-sm text-gray-500">Bypass exam gating for a specific student. Creates an audit log entry.</p>
          </div>
        </div>

        <form onSubmit={handleUnlockLesson} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-semibold mb-1.5 text-gray-700 dark:text-gray-300">Lesson ID</label>
              <input type="text" value={uLessonId} onChange={e => setULessonId(e.target.value)} required 
                placeholder="Enter the GUID of the lesson to unlock"
                className="w-full rounded-xl border border-emerald-200 dark:border-emerald-800 bg-white dark:bg-gray-800 p-3 shadow-sm focus:ring-2 focus:ring-emerald-400 outline-none"
              />
            </div>
            <div>
              <label className="block text-sm font-semibold mb-1.5 text-gray-700 dark:text-gray-300">Student User ID</label>
              <input type="text" value={uStudentId} onChange={e => setUStudentId(e.target.value)} required 
                placeholder="Enter the student's GUID"
                className="w-full rounded-xl border border-emerald-200 dark:border-emerald-800 bg-white dark:bg-gray-800 p-3 shadow-sm focus:ring-2 focus:ring-emerald-400 outline-none"
              />
            </div>
          </div>
          <div className="flex items-center justify-between">
            <button type="submit" disabled={uLoading}
              className="rounded-xl bg-emerald-600 px-8 py-3 font-bold text-white shadow-lg hover:bg-emerald-700 disabled:opacity-50 transition-all hover:scale-105"
            >
              {uLoading ? 'Unlocking...' : 'Unlock Lesson'}
            </button>
            {uResult && (
              <motion.span initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="text-sm font-semibold text-emerald-700 dark:text-emerald-400">
                {uResult}
              </motion.span>
            )}
          </div>
        </form>
      </motion.div>

      {/* Audit Note */}
      <div className="rounded-xl bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 p-4 text-sm text-gray-500">
        <strong>Note:</strong> All override actions are recorded in the audit log with your admin ID, the affected entity, and timestamp. These logs are immutable and available for review.
      </div>
    </div>
  );
}
