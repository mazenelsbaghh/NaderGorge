'use client';

import React from 'react';
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
  return (
    <AnimatePresence>
      {open && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="fixed inset-0 z-[60] flex items-center justify-center bg-black/30 p-4 backdrop-blur-sm"
          onClick={onClose}
        >
          <motion.div
            initial={{ scale: 0.96, y: 14 }}
            animate={{ scale: 1, y: 0 }}
            exit={{ scale: 0.96, y: 14 }}
            onClick={(e) => e.stopPropagation()}
            className={`flex max-h-[90vh] w-full flex-col rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-6 shadow-[0_24px_80px_rgba(0,0,0,0.4)] ${maxWidth}`}
          >
            {(title || subtitle) && (
              <div className="mb-6 flex items-center justify-between">
                <div>
                  {title && <h3 className="text-2xl font-black text-[var(--admin-text)]">{title}</h3>}
                  {subtitle && <p className="text-sm text-[var(--admin-muted)]">{subtitle}</p>}
                </div>
                <button
                  type="button"
                  onClick={onClose}
                  className="rounded-full bg-[var(--admin-card-strong)] px-4 py-2 text-sm font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-hover)]"
                >
                  إغلاق
                </button>
              </div>
            )}
            
            <div className="overflow-y-auto">
              {children}
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
