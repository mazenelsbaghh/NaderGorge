'use client';

import React, { useEffect } from 'react';
import { AnimatePresence, motion } from 'framer-motion';

export interface AdminModalProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  subtitle?: string;
  maxWidth?: string;
  children: React.ReactNode;
}

export function AdminModal({
  open,
  onClose,
  title,
  subtitle,
  maxWidth = 'max-w-xl',
  children,
}: AdminModalProps) {
  useEffect(() => {
    if (!open) return;
    
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="fixed inset-0 z-[60] flex items-center justify-center bg-[var(--admin-text)]/30 p-4 backdrop-blur-[2px]"
          onClick={onClose}
        >
          <motion.div
            initial={{ scale: 0.96, y: 14 }}
            animate={{ scale: 1, y: 0 }}
            exit={{ scale: 0.96, y: 14 }}
            transition={{ duration: 0.18, ease: [0.16, 1, 0.3, 1] }}
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-labelledby={title ? "admin-modal-title" : undefined}
            aria-describedby={subtitle ? "admin-modal-subtitle" : undefined}
            className={`flex max-h-[90vh] w-full flex-col rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-5 shadow-[0_16px_40px_var(--admin-shadow)] sm:p-6 ${maxWidth}`}
          >
            {(title || subtitle) && (
              <div className="mb-5 flex items-start justify-between gap-4">
                <div className="min-w-0">
                  {title && <h3 id="admin-modal-title" className="text-2xl font-black text-[var(--admin-text)]">{title}</h3>}
                  {subtitle && <p id="admin-modal-subtitle" className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">{subtitle}</p>}
                </div>
                <button
                  type="button"
                  onClick={onClose}
                  className="shrink-0 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-4 py-2 text-sm font-bold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
                >
                  إغلاق
                </button>
              </div>
            )}
            
            <div className="min-h-0 overflow-y-auto">
              {children}
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
