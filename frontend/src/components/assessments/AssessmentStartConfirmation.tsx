'use client';

import { ConfirmDialog } from '@/components/ui/confirm-dialog';

export function AssessmentStartConfirmation({ kind, onConfirm, onCancel }: {
  kind: 'exam' | 'homework';
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const label = kind === 'exam' ? 'الامتحان' : 'الواجب';
  return (
    <ConfirmDialog
      open
      variant="primary"
      title={`هل أنت متأكد من دخول ${label}؟`}
      description="عند التأكيد ستدخل المحاولة. يبدأ وقت المحاولة الجديدة إذا كانت محددة بمدة، والإلغاء لا يبدأ محاولة جديدة."
      confirmLabel={`نعم، دخول ${label}`}
      cancelLabel="إلغاء"
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );
}
