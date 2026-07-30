'use client';

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Check, Copy, KeyRound, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { useStudentShellStore } from '@/stores/student-shell-store';

export function ParentCodePopup() {
  const { parentTrackingCode, hasSeenTrackingCodePopup, acknowledgeTrackingPopup } = useStudentShellStore();
  const [copied, setCopied] = useState(false);
  const [isClosing, setIsClosing] = useState(false);
  const [isContractOpen, setIsContractOpen] = useState(true);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    const checkContract = () => {
      const open = typeof document !== 'undefined' && !!document.getElementById('ins-modal-title');
      setIsContractOpen(open);
    };

    checkContract();
    const interval = setInterval(checkContract, 300);
    return () => clearInterval(interval);
  }, []);

  console.log('ParentCodePopup RENDERING: code =', parentTrackingCode, 'seen =', hasSeenTrackingCodePopup, 'isContractOpen =', isContractOpen);

  if (!mounted) {
    return null;
  }

  // If the popup has already been seen, or if there is no tracking code, or if the onboarding contract modal is still open, do not display it.
  if (hasSeenTrackingCodePopup || !parentTrackingCode || isContractOpen) {
    return null;
  }

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(parentTrackingCode);
      setCopied(true);
      toast.success('تم نسخ رمز المتابعة بنجاح!');
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error('فشل نسخ الرمز. يرجى نسخه يدوياً.');
    }
  };

  const handleClose = async () => {
    if (isClosing) return;
    setIsClosing(true);
    try {
      await acknowledgeTrackingPopup();
    } catch (err) {
      console.error(err);
    } finally {
      setIsClosing(false);
    }
  };

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
        {/* Backdrop */}
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="absolute inset-0 bg-black/60 backdrop-blur-md"
          onClick={handleClose}
        />

        {/* Modal Container */}
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.95, y: 20 }}
          transition={{ type: 'spring', duration: 0.4 }}
          role="dialog"
          aria-modal="true"
          aria-labelledby="popup-title"
          className="relative w-full max-w-md overflow-hidden rounded-3xl border border-white/10 bg-[var(--admin-card)]/80 backdrop-blur-xl p-6 shadow-2xl text-[var(--admin-text)] flex flex-col items-center text-center"
        >
          {/* Close button icon at the top corner */}
          <button
            type="button"
            onClick={handleClose}
            aria-label="إغلاق"
            className="absolute top-4 left-4 p-2 rounded-full text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] transition"
          >
            <X className="h-5 w-5" />
          </button>

          {/* Graphic Icon */}
          <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
            <KeyRound className="h-7 w-7" />
          </div>

          {/* Content */}
          <h2 id="popup-title" className="text-xl font-black text-[var(--admin-text)] mb-2">
            تابع مستواك الدراسي مع ولي أمرك
          </h2>
          
          <p className="text-sm font-medium text-[var(--admin-muted)] leading-relaxed mb-6 max-w-sm">
            شارك رمز المتابعة هذا مع والدك أو والدتك ليتمكنوا من ربطه في تطبيق ولي الأمر ومتابعة درجاتك وتقارير حضورك لحظة بلحظة.
          </p>

          {/* Tracking Code Presentation */}
          <div className="w-full flex flex-col items-center justify-center gap-3 bg-[var(--admin-card-soft)] border border-[var(--admin-border)] rounded-2xl py-5 px-4 mb-2">
            <span className="text-xs font-black tracking-widest text-[var(--admin-muted)] uppercase">
              رمز المتابعة الخاص بك
            </span>
            <div className="font-mono text-3xl font-black tracking-[0.25em] text-[var(--admin-primary)] select-all mr-[0.25em]">
              {parentTrackingCode}
            </div>
            
            <button
              type="button"
              onClick={handleCopy}
              className="mt-2 flex items-center gap-2 px-5 py-2.5 rounded-full bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] font-bold text-sm hover:scale-105 active:scale-95 transition shadow-[0_4px_12px_var(--admin-shadow)]"
            >
              {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
              {copied ? 'تم النسخ' : 'نسخ الرمز'}
            </button>
          </div>

          <button
            type="button"
            onClick={handleClose}
            disabled={isClosing}
            className="mt-4 w-full py-3 rounded-2xl bg-[var(--admin-primary-10)] hover:bg-[var(--admin-primary-15)] text-[var(--admin-primary)] border border-[var(--admin-primary)]/20 font-bold transition flex justify-center items-center gap-2 text-sm focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
          >
            {isClosing ? 'جاري الحفظ...' : 'حفظ ومتابعة'}
          </button>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
