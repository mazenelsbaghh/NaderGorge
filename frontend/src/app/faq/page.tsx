'use client';

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import Link from 'next/link';

const faqs = [
  { q: 'How do I get access to a package?', a: 'You need an access code. Contact your teacher or school to receive one. Once you have a code, go to "Redeem Code" in your student dashboard and enter it. The package will be instantly available.' },
  { q: 'What happens if I fail an exam?', a: 'If you fail an exam, the next lesson will be locked until you pass. You can retake the exam as many times as needed. If you\'re stuck, ask your teacher to manually unlock the lesson for you.' },
  { q: 'How many times can I watch a video?', a: 'Each video has a watch limit set by your teacher (usually 3 times). Once you reach the limit, the video will be locked. If you need more views, your teacher can reset the watch limit from the admin panel.' },
  { q: 'Can I use my account on multiple devices?', a: 'You can register a limited number of devices with your account for security purposes. If you need to switch devices, ask your teacher to remove an old device from the admin panel.' },
  { q: 'How do I track my progress?', a: 'Your student dashboard shows overall progress, per-package completion, and a "Resume Learning" button that takes you directly to your next incomplete lesson.' },
  { q: 'What subjects are available?', a: 'Currently, the platform covers curriculum content for 1st, 2nd, and 3rd Secondary students. New packages and subjects are added regularly by Mr. Nader George.' },
  { q: 'Is the platform free?', a: 'Registration is free. Access to content packages requires a valid access code, which may be obtained through your school or directly from the teacher.' },
  { q: 'I forgot my password. What do I do?', a: 'Contact your teacher or the admin team to reset your password. You\'ll need to verify your phone number for security.' }
];

export default function FaqPage() {
  const [openIndex, setOpenIndex] = useState<number | null>(null);

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-950 dark:to-gray-900">
      {/* Nav */}
      <nav className="container mx-auto flex items-center justify-between p-6">
        <Link href="/" className="text-2xl font-extrabold bg-gradient-to-r from-indigo-600 to-purple-600 bg-clip-text text-transparent">
          Nader George Academy
        </Link>
        <div className="flex items-center gap-6">
          <Link href="/" className="text-sm font-semibold text-gray-600 dark:text-gray-300 hover:text-indigo-600 transition">Home</Link>
          <Link href="/about" className="text-sm font-semibold text-gray-600 dark:text-gray-300 hover:text-indigo-600 transition">About</Link>
          <Link href="/login" className="rounded-full bg-indigo-600 px-6 py-2.5 text-sm font-bold text-white shadow-lg hover:bg-indigo-700 transition">Sign In</Link>
        </div>
      </nav>

      <div className="container mx-auto px-6 py-16 max-w-3xl">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
          <div className="text-center mb-12">
            <span className="inline-block rounded-full bg-amber-100 dark:bg-amber-900/30 px-4 py-1.5 text-sm font-semibold text-amber-700 dark:text-amber-300 mb-4">❓ Help Center</span>
            <h1 className="text-4xl md:text-5xl font-extrabold text-gray-900 dark:text-white">Frequently Asked Questions</h1>
            <p className="mt-4 text-gray-600 dark:text-gray-400">Find answers to common questions about the platform.</p>
          </div>

          <div className="space-y-3">
            {faqs.map((faq, i) => (
              <motion.div key={i} initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.05 }}
                className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 overflow-hidden shadow-sm"
              >
                <button
                  onClick={() => setOpenIndex(openIndex === i ? null : i)}
                  className="w-full flex items-center justify-between p-5 text-left hover:bg-gray-50 dark:hover:bg-gray-800/50 transition"
                >
                  <span className="font-semibold text-gray-900 dark:text-white pr-4">{faq.q}</span>
                  <span className={`text-xl font-bold text-indigo-600 flex-shrink-0 transition-transform ${openIndex === i ? 'rotate-45' : ''}`}>+</span>
                </button>
                <AnimatePresence>
                  {openIndex === i && (
                    <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
                      <div className="px-5 pb-5 text-sm text-gray-600 dark:text-gray-400 leading-relaxed border-t border-gray-100 dark:border-gray-800 pt-4">
                        {faq.a}
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </motion.div>
            ))}
          </div>

          <div className="mt-12 text-center">
            <p className="text-gray-500 mb-4">Still have questions?</p>
            <Link href="/about" className="rounded-full bg-indigo-600 px-8 py-3 text-sm font-bold text-white shadow-lg hover:bg-indigo-700 hover:scale-105 transition-all">
              Contact the Teacher
            </Link>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
