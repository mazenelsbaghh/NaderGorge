'use client';

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import Link from 'next/link';
import { HelpCircle, ChevronDown, MessageCircle } from 'lucide-react';

const faqs = [
  { q: 'إزاي أقدر أفعّل باقة؟', a: 'محتاج كود تفعيل. تواصل مع المُعلم أو المدرسة عشان تاخد الكود. بعد ما تاخده، روح لصفحة "تفعيل كود" في لوحة التحكم واكتب الكود. الباقة هتظهر فوراً.' },
  { q: 'إيه اللي هيحصل لو مجبتش درجة النجاح في الامتحان؟', a: 'لو معديتش الامتحان، الدرس اللي بعده هيفضل مقفول لحد ما تنجح. تقدر تعيد الامتحان أكتر من مرة. لو محتاج مساعدة، كلم المُعلم يفتحلك الدرس.' },
  { q: 'أقدر أتفرج على الفيديو كام مرة؟', a: 'كل فيديو ليه عدد مرات مشاهدة محدد بيحددها المُعلم (عادةً ٣ مرات). لو خلصت المرات، الفيديو هيتقفل. لو محتاج مرات أكتر، المُعلم يقدر يعمل reset من لوحة التحكم.' },
  { q: 'أقدر أستخدم حسابي على أكتر من جهاز؟', a: 'تقدر تسجل عدد محدود من الأجهزة على حسابك لأسباب أمنية. لو عايز تغير جهاز، كلم المُعلم يشيل الجهاز القديم من لوحة التحكم.' },
  { q: 'إزاي أتابع تقدمي؟', a: 'لوحة التحكم بتاعتك بتعرض نسبة التقدم الكلي، والتقدم في كل باقة، وزرار "استكمال المذاكرة" اللي بياخدك لآخر درس وصلت ليه.' },
  { q: 'إيه المواد المتاحة؟', a: 'حالياً المنصة بتغطي المواد الدراسية لطلاب الصف الأول والتاني والتالت الثانوي. بيتم إضافة باقات جديدة بشكل دوري من فريق منصة مسار التعليمية.' },
  { q: 'المنصة مجانية؟', a: 'التسجيل مجاني. الوصول لمحتوى الباقات محتاج كود تفعيل ممكن تحصل عليه من المدرسة أو مباشرة من المُعلم.' },
  { q: 'نسيت كلمة المرور. أعمل إيه؟', a: 'تواصل مع المُعلم أو فريق الدعم عشان يعيدوا لك كلمة المرور. هتحتاج تأكد رقم تليفونك للأمان.' }
];

export default function FaqPageClient() {
  const [openIndex, setOpenIndex] = useState<number | null>(null);

  return (
    <div className="landing-page">
      <div className="landing-page__backdrop" />
      <div className="landing-page__texture" />

      <div className="relative z-10 mx-auto max-w-3xl px-6 pt-28 pb-16">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
          {/* Header */}
          <div className="mb-12 text-center">
            <div className="landing-chip mx-auto mb-5">
              <HelpCircle className="h-4 w-4" />
              <span>مركز المساعدة</span>
            </div>
            <h1 className="text-4xl font-black text-[var(--landing-ink)] md:text-5xl">
              الأسئلة الشائعة
            </h1>
            <p className="mt-4 text-base text-[var(--landing-muted)]">
              إجابات لأكتر الأسئلة المتكررة عن المنصة.
            </p>
          </div>

          {/* FAQ items */}
          <div className="space-y-3">
            {faqs.map((faq, i) => (
              <motion.div
                key={i}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: i * 0.04 }}
                className="landing-panel overflow-hidden rounded-[20px]"
              >
                <button
                  onClick={() => setOpenIndex(openIndex === i ? null : i)}
                  className="flex w-full items-center justify-between gap-4 p-5 text-right transition hover:bg-[var(--landing-card)]"
                >
                  <span className="font-bold text-[var(--landing-ink)]">{faq.q}</span>
                  <ChevronDown className={`h-5 w-5 shrink-0 text-[var(--landing-accent)] transition-transform ${openIndex === i ? 'rotate-180' : ''}`} />
                </button>
                <AnimatePresence>
                  {openIndex === i && (
                    <motion.div
                      initial={{ height: 0, opacity: 0 }}
                      animate={{ height: 'auto', opacity: 1 }}
                      exit={{ height: 0, opacity: 0 }}
                      transition={{ duration: 0.2 }}
                    >
                      <div className="px-5 pb-5 text-sm leading-7 text-[var(--landing-muted)]">
                        {faq.a}
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </motion.div>
            ))}
          </div>

          {/* CTA */}
          <div className="mt-12 text-center">
            <p className="mb-4 text-sm font-bold text-[var(--landing-muted)]">لسه عندك أسئلة؟</p>
            <Link
              href="/about"
              className="inline-flex items-center gap-2 rounded-full bg-[var(--landing-accent)] px-8 py-3.5 text-sm font-extrabold text-[var(--landing-accent-foreground)] shadow-[0_16px_40px_rgba(145,95,42,0.28)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
            >
              <MessageCircle className="h-4 w-4" />
              تواصل مع المُعلم
            </Link>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
