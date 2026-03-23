'use client';

import { motion } from 'framer-motion';
import Link from 'next/link';

export default function AboutPage() {
  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-950 dark:to-gray-900">
      {/* Nav */}
      <nav className="container mx-auto flex items-center justify-between p-6">
        <Link href="/" className="text-2xl font-extrabold bg-gradient-to-r from-indigo-600 to-purple-600 bg-clip-text text-transparent">
          Nader George Academy
        </Link>
        <div className="flex items-center gap-6">
          <Link href="/" className="text-sm font-semibold text-gray-600 dark:text-gray-300 hover:text-indigo-600 transition">Home</Link>
          <Link href="/faq" className="text-sm font-semibold text-gray-600 dark:text-gray-300 hover:text-indigo-600 transition">FAQ</Link>
          <Link href="/login" className="rounded-full bg-indigo-600 px-6 py-2.5 text-sm font-bold text-white shadow-lg hover:bg-indigo-700 transition">Sign In</Link>
        </div>
      </nav>

      <div className="container mx-auto px-6 py-16 max-w-4xl">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
          {/* Hero */}
          <div className="text-center mb-16">
            <span className="inline-block rounded-full bg-purple-100 dark:bg-purple-900/30 px-4 py-1.5 text-sm font-semibold text-purple-700 dark:text-purple-300 mb-4">About the Teacher</span>
            <h1 className="text-4xl md:text-5xl font-extrabold text-gray-900 dark:text-white leading-tight">
              Mr. Nader George
            </h1>
            <p className="mt-4 text-lg text-gray-600 dark:text-gray-400 max-w-2xl mx-auto">
              A passionate educator dedicated to making learning accessible, engaging, and effective for every secondary student.
            </p>
          </div>

          {/* Bio Card */}
          <div className="rounded-3xl bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 p-8 md:p-12 shadow-xl mb-12">
            <div className="flex flex-col md:flex-row gap-8 items-start">
              <div className="flex-shrink-0">
                <div className="h-32 w-32 rounded-2xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center text-white text-5xl font-bold shadow-lg">NG</div>
              </div>
              <div className="space-y-4">
                <h2 className="text-2xl font-bold text-gray-900 dark:text-white">Teaching Philosophy</h2>
                <p className="text-gray-600 dark:text-gray-400 leading-relaxed">
                  With over a decade of teaching experience, Mr. Nader George believes that every student has the potential to excel when given the right tools and environment. His approach combines structured curriculum delivery with modern technology to create a seamless learning experience.
                </p>
                <p className="text-gray-600 dark:text-gray-400 leading-relaxed">
                  The platform is designed to provide high-quality video lessons that students can revisit at their own pace, combined with auto-graded exams that ensure comprehension before progression. This gated learning approach has proven to significantly improve student outcomes.
                </p>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-12">
            {[
              { num: '10+', label: 'Years Experience' },
              { num: '5000+', label: 'Students Taught' },
              { num: '3', label: 'Grade Levels' },
              { num: '95%', label: 'Pass Rate' }
            ].map((s, i) => (
              <motion.div key={i} initial={{ opacity: 0, scale: 0.9 }} whileInView={{ opacity: 1, scale: 1 }} viewport={{ once: true }} transition={{ delay: i * 0.1 }}
                className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 text-center shadow-sm"
              >
                <div className="text-3xl font-extrabold bg-gradient-to-r from-indigo-600 to-purple-600 bg-clip-text text-transparent">{s.num}</div>
                <div className="text-sm text-gray-500 mt-1 font-medium">{s.label}</div>
              </motion.div>
            ))}
          </div>

          {/* CTA */}
          <div className="text-center">
            <Link href="/register" className="rounded-full bg-indigo-600 px-10 py-4 text-lg font-bold text-white shadow-2xl hover:bg-indigo-700 hover:scale-105 transition-all">
              Join the Academy
            </Link>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
