'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { studentService, DashboardDto } from '@/services/student-service';
import { motion } from 'framer-motion';

export default function StudentDashboard() {
  const [data, setData] = useState<DashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const router = useRouter();

  useEffect(() => {
    studentService.getDashboard()
      .then(setData)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="mx-auto max-w-5xl p-6 space-y-6">
        <div className="h-8 w-48 bg-gray-200 dark:bg-gray-800 rounded-lg animate-pulse" />
        <div className="grid gap-4 md:grid-cols-4">
          {[1,2,3,4].map(i => <div key={i} className="h-28 rounded-2xl bg-gray-200 dark:bg-gray-800 animate-pulse" />)}
        </div>
        <div className="h-40 rounded-2xl bg-gray-200 dark:bg-gray-800 animate-pulse" />
      </div>
    );
  }

  const d = data || {
    studentName: 'Student',
    activePackages: [],
    resumePoint: null,
    upcomingExams: [],
    overallProgressPercent: 0,
    totalLessonsCompleted: 0,
    totalLessons: 0,
    codesRedeemed: 0
  };

  return (
    <div className="mx-auto max-w-5xl p-6 space-y-8">
      {/* Welcome Banner */}
      <motion.div initial={{ opacity: 0, y: -20 }} animate={{ opacity: 1, y: 0 }}
        className="rounded-3xl bg-gradient-to-r from-indigo-600 via-purple-600 to-pink-500 p-8 text-white shadow-2xl"
      >
        <h1 className="text-3xl font-extrabold">Welcome back, {d.studentName}!</h1>
        <p className="mt-2 text-indigo-100">Keep up the great work. Here&apos;s your learning overview.</p>
      </motion.div>

      {/* Stats Row */}
      <div className="grid gap-4 grid-cols-2 md:grid-cols-4">
        <StatCard label="Active Packages" value={d.activePackages.length} icon="📦" color="bg-blue-500" delay={0.05} />
        <StatCard label="Lessons Done" value={`${d.totalLessonsCompleted}/${d.totalLessons}`} icon="📚" color="bg-emerald-500" delay={0.1} />
        <StatCard label="Overall Progress" value={`${d.overallProgressPercent}%`} icon="📈" color="bg-purple-500" delay={0.15} />
        <StatCard label="Codes Redeemed" value={d.codesRedeemed} icon="🎟️" color="bg-amber-500" delay={0.2} />
      </div>

      {/* Resume Learning */}
      {d.resumePoint && (
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.25 }}
          className="rounded-2xl border border-indigo-200 dark:border-indigo-900/30 bg-gradient-to-br from-indigo-50 to-purple-50 dark:from-indigo-900/10 dark:to-purple-900/10 p-6 shadow-lg"
        >
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-semibold text-indigo-600 dark:text-indigo-400 uppercase tracking-wider">Continue Learning</p>
              <h3 className="text-xl font-bold mt-1 text-gray-900 dark:text-white">{d.resumePoint.lessonTitle}</h3>
              <p className="text-sm text-gray-500 mt-1">from {d.resumePoint.packageName} • Lesson {d.resumePoint.lessonOrder}</p>
            </div>
            <button
              onClick={() => router.push(`/student/packages/${d.resumePoint!.packageId}/lessons/${d.resumePoint!.lessonId}`)}
              className="rounded-xl bg-indigo-600 px-8 py-3 font-bold text-white shadow-lg hover:bg-indigo-700 hover:scale-105 transition-all"
            >
              Resume →
            </button>
          </div>
        </motion.div>
      )}

      {/* Package Cards */}
      <div>
        <h2 className="text-xl font-bold text-gray-900 dark:text-white mb-4">Your Packages</h2>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {d.activePackages.map((pkg, i) => (
            <motion.div key={pkg.id} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.3 + i * 0.05 }}
              onClick={() => router.push(`/student/packages/${pkg.id}`)}
              className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 shadow-sm hover:shadow-xl cursor-pointer transition-all hover:scale-[1.02]"
            >
              <h3 className="font-bold text-lg text-gray-900 dark:text-white">{pkg.name}</h3>
              <p className="text-sm text-gray-500 line-clamp-2 mt-2 mb-4">{pkg.description}</p>
              <div className="flex items-center justify-between text-sm">
                <span className="text-gray-500">{pkg.lessonsCompleted}/{pkg.totalLessons} lessons</span>
                <span className="font-bold text-indigo-600">{pkg.progressPercent}%</span>
              </div>
              <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2 mt-2">
                <div className="bg-indigo-600 h-2 rounded-full transition-all" style={{ width: `${pkg.progressPercent}%` }} />
              </div>
            </motion.div>
          ))}
          {d.activePackages.length === 0 && (
            <div className="col-span-full text-center py-12 border-2 border-dashed rounded-2xl text-gray-400">
              No active packages. <button onClick={() => router.push('/student/code-redemption')} className="text-indigo-600 underline font-semibold">Redeem a code</button> to get started!
            </div>
          )}
        </div>
      </div>

      {/* Upcoming Exams */}
      {d.upcomingExams.length > 0 && (
        <div>
          <h2 className="text-xl font-bold text-gray-900 dark:text-white mb-4">Upcoming Exams</h2>
          <div className="space-y-3">
            {d.upcomingExams.map((exam, i) => (
              <motion.div key={exam.examId} initial={{ opacity: 0, x: -20 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: 0.4 + i * 0.05 }}
                className="flex items-center justify-between rounded-xl border p-4 bg-white dark:bg-gray-900 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition"
              >
                <div className="flex items-center gap-4">
                  <div className="flex h-10 w-10 items-center justify-center rounded-full bg-red-100 dark:bg-red-900/30 text-red-600 dark:text-red-400 font-bold">📝</div>
                  <div>
                    <span className="font-semibold text-gray-900 dark:text-white">{exam.examTitle}</span>
                    <span className="block text-xs text-gray-400">{exam.lessonTitle}</span>
                  </div>
                </div>
                <button
                  onClick={() => router.push(`/student/exams/${exam.examId}`)}
                  className="rounded-lg bg-red-100 dark:bg-red-900/30 text-red-600 dark:text-red-400 px-4 py-2 text-sm font-bold hover:bg-red-200 transition"
                >
                  Take Exam
                </button>
              </motion.div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function StatCard({ label, value, icon, color, delay }: { label: string; value: string | number; icon: string; color: string; delay: number }) {
  return (
    <motion.div initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }} transition={{ delay }}
      className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white/80 dark:bg-gray-900/80 p-5 shadow-sm backdrop-blur-sm"
    >
      <div className="flex items-center gap-3 mb-3">
        <span className={`flex h-9 w-9 items-center justify-center rounded-lg ${color} text-white text-lg`}>{icon}</span>
        <h3 className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">{label}</h3>
      </div>
      <p className="text-2xl font-extrabold text-gray-900 dark:text-white">{value}</p>
    </motion.div>
  );
}
