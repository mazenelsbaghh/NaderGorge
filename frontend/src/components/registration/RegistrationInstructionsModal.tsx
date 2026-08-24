'use client';

import { useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  User,
  Users,
  MonitorSmartphone,
  ShieldAlert,
  GraduationCap,
  MessageSquareCode,
  X,
  BookOpen,
  type LucideIcon,
} from 'lucide-react';

import {
  LOGIN_INSTRUCTION_COPY,
  LOGIN_INSTRUCTION_NOTE,
  REGISTRATION_INSTRUCTION_COPY,
  REGISTRATION_INSTRUCTION_NOTE,
} from './registration-instruction-copy';

interface RegistrationInstructionsModalProps {
  open: boolean;
  onClose: () => void;
  confirmLabel?: string;
  title?: string;
  subtitle?: string;
  mode?: 'register' | 'login';
}

type InstructionPresentation = {
  icon: LucideIcon;
  color: string;
};

const REGISTRATION_PRESENTATION: Record<
  (typeof REGISTRATION_INSTRUCTION_COPY)[number]['key'],
  InstructionPresentation
> = {
  'full-name': {
    icon: User,
    color: 'text-amber-500 bg-amber-500/10 border-amber-500/20',
  },
  'parent-details': {
    icon: Users,
    color: 'text-blue-500 bg-blue-500/10 border-blue-500/20',
  },
  'device-limit': {
    icon: MonitorSmartphone,
    color: 'text-red-500 bg-red-500/10 border-red-500/20',
  },
  'duplicate-accounts': {
    icon: ShieldAlert,
    color: 'text-purple-500 bg-purple-500/10 border-purple-500/20',
  },
  'academic-details': {
    icon: GraduationCap,
    color: 'text-emerald-500 bg-emerald-500/10 border-emerald-500/20',
  },
  'whatsapp-numbers': {
    icon: MessageSquareCode,
    color: 'text-green-500 bg-green-500/10 border-green-500/20',
  },
};

const LOGIN_PRESENTATION: Record<
  (typeof LOGIN_INSTRUCTION_COPY)[number]['key'],
  InstructionPresentation
> = {
  'personal-account': {
    icon: User,
    color: 'text-blue-500 bg-blue-500/10 border-blue-500/20',
  },
  'private-password': {
    icon: ShieldAlert,
    color: 'text-red-500 bg-red-500/10 border-red-500/20',
  },
  'usual-devices': {
    icon: MonitorSmartphone,
    color: 'text-amber-500 bg-amber-500/10 border-amber-500/20',
  },
  'forgot-password': {
    icon: MessageSquareCode,
    color: 'text-emerald-500 bg-emerald-500/10 border-emerald-500/20',
  },
};

const REGISTRATION_INSTRUCTIONS = REGISTRATION_INSTRUCTION_COPY.map(
  (instruction) => ({
    ...instruction,
    ...REGISTRATION_PRESENTATION[instruction.key],
  }),
);

const LOGIN_INSTRUCTIONS = LOGIN_INSTRUCTION_COPY.map((instruction) => ({
  ...instruction,
  ...LOGIN_PRESENTATION[instruction.key],
}));

export function RegistrationInstructionsModal({
  open,
  onClose,
  confirmLabel = 'فهمت وموافق على الشروط',
  title = 'تعليمات وشروط هامة قبل التسجيل',
  subtitle = 'يرجى قراءتها بدقة قبل إنشاء الحساب أو تسجيل الدخول لأول مرة.',
  mode = 'register',
}: RegistrationInstructionsModalProps) {

  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  const instructions =
    mode === 'login' ? LOGIN_INSTRUCTIONS : REGISTRATION_INSTRUCTIONS;
  const instructionNote =
    mode === 'login' ? LOGIN_INSTRUCTION_NOTE : REGISTRATION_INSTRUCTION_NOTE;

  return (
    <AnimatePresence>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="ins-modal-title"
        className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center p-4"
        dir="rtl"
      >
        {/* Backdrop */}
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="absolute inset-0 bg-black/75 backdrop-blur-[4px]"
          onClick={onClose}
          aria-hidden="true"
        />

        {/* Modal Card */}
        <motion.div
          initial={{ opacity: 0, scale: 0.9, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.9, y: 20 }}
          transition={{ type: 'spring', damping: 25, stiffness: 350 }}
          className="relative z-10 w-full max-w-2xl rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-2xl flex flex-col my-8 max-h-[85vh] overflow-hidden"
        >
          {/* Header */}
          <div className="flex items-center justify-between gap-4 p-6 border-b border-[var(--admin-border)]">
            <div className="flex items-center gap-3">
              <div className="rounded-xl p-2.5 bg-[var(--admin-primary-15)] text-[var(--admin-primary)] border border-[var(--admin-primary)]/20 shadow-inner">
                <BookOpen className="h-6 w-6 animate-pulse" />
              </div>
              <div>
                <h2
                  id="ins-modal-title"
                  className="text-xl font-black text-[var(--admin-text)] tracking-tight"
                >
                  {title}
                </h2>
                <p className="text-xs font-bold text-[var(--admin-muted)] mt-0.5">
                  {subtitle}
                </p>
              </div>
            </div>
            <button
              onClick={onClose}
              aria-label="إغلاق"
              className="rounded-xl p-2 text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] border border-[var(--admin-border)]/50 bg-[var(--admin-card-soft)]"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          {/* Scrollable Content */}
          <div className="flex-1 overflow-y-auto p-6 space-y-4 custom-scrollbar">
            <div className="grid gap-4 sm:grid-cols-2">
              {instructions.map((ins, index) => {
                const IconComponent = ins.icon;
                return (
                  <div
                    key={index}
                    className="flex gap-4 p-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] transition-colors hover:border-[var(--admin-primary)]/20 text-right"
                  >
                    <div className={`h-11 w-11 shrink-0 rounded-xl border flex items-center justify-center ${ins.color}`}>
                      <IconComponent className="h-5 w-5" />
                    </div>
                    <div className="space-y-1">
                      <h4 className="text-sm font-black text-[var(--admin-text)]">
                        {ins.title}
                      </h4>
                      <p className="text-xs font-semibold text-[var(--admin-muted)] leading-relaxed">
                        {ins.description}
                      </p>
                    </div>
                  </div>
                );
              })}
            </div>

            <div className="p-4 rounded-2xl bg-amber-500/5 border border-amber-500/10 text-amber-500/90 text-xs font-semibold leading-relaxed flex gap-3 items-center text-right">
              <ShieldAlert className="h-5 w-5 shrink-0" />
              <span>
                {instructionNote}
              </span>
            </div>
          </div>

          {/* Actions */}
          <div className="flex justify-end border-t border-[var(--admin-border)] p-6 bg-[var(--admin-card-soft)]/50">
            <button
              onClick={onClose}
              className="h-12 w-full sm:w-auto rounded-2xl bg-[var(--admin-primary)] hover:brightness-110 text-[var(--admin-primary-contrast)] font-black text-sm px-10 transition-[color,background-color,border-color,opacity,transform,box-shadow] shadow-lg shadow-[var(--admin-primary)]/35 active:scale-[0.98]"
            >
              {confirmLabel}
            </button>
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
