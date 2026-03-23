'use client';

import { motion } from 'framer-motion';
import Link from 'next/link';

export default function LandingPage() {
  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-950 dark:to-gray-900">
      {/* Navigation */}
      <nav className="container mx-auto flex items-center justify-between p-6">
        <div className="text-2xl font-extrabold bg-gradient-to-r from-indigo-600 to-purple-600 bg-clip-text text-transparent">
          Nader George Academy
        </div>
        <div className="flex items-center gap-6">
          <Link href="/about" className="text-sm font-semibold text-gray-600 dark:text-gray-300 hover:text-indigo-600 transition">About</Link>
          <Link href="/faq" className="text-sm font-semibold text-gray-600 dark:text-gray-300 hover:text-indigo-600 transition">FAQ</Link>
          <Link href="/login" className="rounded-full bg-indigo-600 px-6 py-2.5 text-sm font-bold text-white shadow-lg hover:bg-indigo-700 hover:scale-105 transition-all">
            Sign In
          </Link>
        </div>
      </nav>

      {/* Hero */}
      <section className="container mx-auto px-6 pt-16 pb-24 text-center">
        <motion.div initial={{ opacity: 0, y: 30 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.6 }}>
          <span className="inline-block rounded-full bg-indigo-100 dark:bg-indigo-900/30 px-4 py-1.5 text-sm font-semibold text-indigo-700 dark:text-indigo-300 mb-6">
            🎓 The #1 Platform for Secondary Education
          </span>
          <h1 className="text-5xl md:text-7xl font-extrabold leading-tight text-gray-900 dark:text-white">
            Learn Smarter,<br />
            <span className="bg-gradient-to-r from-indigo-600 via-purple-600 to-pink-500 bg-clip-text text-transparent">Achieve More</span>
          </h1>
          <p className="mx-auto mt-6 max-w-2xl text-lg text-gray-600 dark:text-gray-400">
            Join thousands of students mastering their subjects with video lessons, interactive exams, and personalized progress tracking. Built by teachers, for students.
          </p>
          <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4">
            <Link href="/register" className="rounded-full bg-indigo-600 px-10 py-4 text-lg font-bold text-white shadow-2xl hover:bg-indigo-700 hover:scale-105 transition-all">
              Get Started Free
            </Link>
            <Link href="/about" className="rounded-full border-2 border-gray-300 dark:border-gray-700 px-10 py-4 text-lg font-bold text-gray-700 dark:text-gray-300 hover:border-indigo-400 hover:text-indigo-600 transition-all">
              Learn More
            </Link>
          </div>
        </motion.div>
      </section>

      {/* Features */}
      <section className="bg-white dark:bg-gray-900 py-24">
        <div className="container mx-auto px-6">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-extrabold text-gray-900 dark:text-white">Everything You Need to Excel</h2>
            <p className="mt-4 text-gray-500 max-w-xl mx-auto">A complete learning ecosystem designed to help secondary students reach their full potential.</p>
          </div>
          <div className="grid gap-8 md:grid-cols-2 lg:grid-cols-3">
            {features.map((f, i) => (
              <motion.div key={i} initial={{ opacity: 0, y: 20 }} whileInView={{ opacity: 1, y: 0 }} viewport={{ once: true }} transition={{ delay: i * 0.1 }}
                className="rounded-2xl border border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 p-8 hover:shadow-xl transition-shadow"
              >
                <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-indigo-100 dark:bg-indigo-900/30 text-2xl mb-4">{f.icon}</div>
                <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-2">{f.title}</h3>
                <p className="text-sm text-gray-500">{f.desc}</p>
              </motion.div>
            ))}
          </div>
        </div>
      </section>

      {/* Testimonials */}
      <section className="py-24 bg-gradient-to-br from-indigo-50 to-purple-50 dark:from-gray-900 dark:to-gray-800">
        <div className="container mx-auto px-6">
          <h2 className="text-3xl font-extrabold text-center text-gray-900 dark:text-white mb-12">What Students Say</h2>
          <div className="grid gap-8 md:grid-cols-3">
            {testimonials.map((t, i) => (
              <motion.div key={i} initial={{ opacity: 0, scale: 0.95 }} whileInView={{ opacity: 1, scale: 1 }} viewport={{ once: true }} transition={{ delay: i * 0.1 }}
                className="rounded-2xl bg-white dark:bg-gray-900 p-6 shadow-lg border border-gray-100 dark:border-gray-800"
              >
                <div className="flex items-center gap-3 mb-4">
                  <div className="h-10 w-10 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-white font-bold">{t.name[0]}</div>
                  <div>
                    <div className="font-bold text-sm">{t.name}</div>
                    <div className="text-xs text-gray-400">{t.grade}</div>
                  </div>
                </div>
                <p className="text-sm text-gray-600 dark:text-gray-400 italic">&ldquo;{t.quote}&rdquo;</p>
                <div className="mt-3 text-amber-400 text-sm">★★★★★</div>
              </motion.div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="py-24">
        <div className="container mx-auto px-6 text-center">
          <motion.div initial={{ opacity: 0 }} whileInView={{ opacity: 1 }} viewport={{ once: true }}
            className="rounded-3xl bg-gradient-to-r from-indigo-600 via-purple-600 to-pink-500 p-12 text-white shadow-2xl"
          >
            <h2 className="text-3xl md:text-4xl font-extrabold">Ready to Start Your Journey?</h2>
            <p className="mt-4 text-indigo-100 max-w-xl mx-auto">Join the academy now and get instant access to premium educational content.</p>
            <Link href="/register" className="mt-8 inline-block rounded-full bg-white px-10 py-4 text-lg font-bold text-indigo-600 shadow-lg hover:bg-gray-100 hover:scale-105 transition-all">
              Register Now
            </Link>
          </motion.div>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-gray-200 dark:border-gray-800 py-12 bg-gray-50 dark:bg-gray-900">
        <div className="container mx-auto px-6 flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="text-sm text-gray-500">© 2026 Nader George Academy. All rights reserved.</div>
          <div className="flex gap-6 text-sm text-gray-500">
            <Link href="/about" className="hover:text-indigo-600 transition">About</Link>
            <Link href="/faq" className="hover:text-indigo-600 transition">FAQ</Link>
            <Link href="/login" className="hover:text-indigo-600 transition">Login</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}

const features = [
  { icon: '🎥', title: 'HD Video Lessons', desc: 'Watch curated video lectures organized by subject and unit, with smart tracking and watch limits.' },
  { icon: '📝', title: 'Interactive Exams', desc: 'Auto-graded MCQ exams with instant results. Progress is gated to ensure mastery before moving on.' },
  { icon: '📊', title: 'Progress Tracking', desc: 'See your overall completion, per-package progress, and a personalized resume point to keep you on track.' },
  { icon: '🔐', title: 'Secure Access Codes', desc: 'Redeem unique codes to unlock packages. Each code is cryptographically secure and single-use.' },
  { icon: '🎓', title: 'Teacher-Led Curriculum', desc: 'Content structured by expert teachers following the official curriculum for 1st, 2nd, and 3rd Secondary.' },
  { icon: '📱', title: 'Device Management', desc: 'Multi-device support with fingerprint tracking. Flexible but secure — no account sharing allowed.' }
];

const testimonials = [
  { name: 'Ahmed M.', grade: '2nd Secondary', quote: 'The exam gating system really pushed me to understand each lesson before moving on. My grades improved significantly!' },
  { name: 'Sara K.', grade: '3rd Secondary', quote: 'I love how organized the content is. The video lessons are clear and the progress tracker keeps me motivated.' },
  { name: 'Omar H.', grade: '1st Secondary', quote: 'The resume feature is amazing — I can always pick up right where I left off. Best learning platform out there!' }
];
