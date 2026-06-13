'use client';

import type { ReactNode } from 'react';

import { AdminModal as AccessibleAdminModal } from '@/components/admin/AdminModal';
import NeumorphButton from '@/components/ui/neumorph-button';

const sizeMap = {
  sm: 'max-w-sm',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
} as const;

interface AdminModalProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
  size?: keyof typeof sizeMap;
}

export function AdminModal({
  open,
  onClose,
  title,
  children,
  size = 'md',
}: AdminModalProps) {
  return (
    <AccessibleAdminModal
      open={open}
      onClose={onClose}
      title={title}
      maxWidth={sizeMap[size]}
    >
      {children}
    </AccessibleAdminModal>
  );
}

interface ConfirmModalProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'danger' | 'primary' | 'ghost';
  loading?: boolean;
}

export function ConfirmModal({
  open,
  onClose,
  onConfirm,
  title,
  description,
  confirmLabel = 'تأكيد',
  cancelLabel = 'إلغاء',
  variant = 'primary',
  loading = false,
}: ConfirmModalProps) {
  return (
    <AdminModal open={open} onClose={onClose} title={title} size="sm">
      {description ? (
        <p className="mb-6 text-center text-sm font-medium leading-relaxed text-[var(--admin-muted)]">
          {description}
        </p>
      ) : null}

      <div className="flex items-center justify-center gap-3">
        <NeumorphButton intent="ghost" size="md" pill onClick={onClose}>
          {cancelLabel}
        </NeumorphButton>
        <NeumorphButton
          intent={variant === 'ghost' ? 'primary' : variant}
          size="md"
          pill
          onClick={onConfirm}
          loading={loading}
        >
          {confirmLabel}
        </NeumorphButton>
      </div>
    </AdminModal>
  );
}
