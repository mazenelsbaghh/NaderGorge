'use client';

import { useState } from 'react';
import { Check, X, ShieldAlert } from 'lucide-react';

interface AIPendingActionCardProps {
  action: {
    id: string;
    actionKey: string;
    safeProposalJson: string;
    status: string;
    expiresAt: string;
  };
  onConfirm: (proposalId: string) => Promise<void>;
  onCancel: (proposalId: string) => Promise<void>;
}

export function AIPendingActionCard({ action, onConfirm, onCancel }: AIPendingActionCardProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  let summary = 'إجراء إداري معلق';
  try {
    const parsed = JSON.parse(action.safeProposalJson);
    if (parsed.safeEffectSummaryAr) {
      summary = parsed.safeEffectSummaryAr;
    }
  } catch (e) {
    // Ignore JSON parse errors
  }

  const handleConfirm = async () => {
    setBusy(true);
    setError('');
    try {
      await onConfirm(action.id);
    } catch (err: any) {
      setError(err?.response?.data?.message || 'فشلت عملية التأكيد. قد يكون الإجراء قد انتهت صلاحيته.');
    } finally {
      setBusy(false);
    }
  };

  const handleCancel = async () => {
    setBusy(true);
    setError('');
    try {
      await onCancel(action.id);
    } catch (err: any) {
      setError(err?.response?.data?.message || 'فشل إلغاء الإجراء.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div dir="rtl" className="my-3 rounded-2xl border border-cyan-100 bg-cyan-50/50 p-4 shadow-sm transition-all hover:shadow-md">
      <div className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-cyan-100 text-cyan-800">
          <ShieldAlert size={20} />
        </span>
        <div className="flex-1 min-w-0">
          <h4 className="text-xs font-semibold text-cyan-900">تأكيد الإجراء المطلوب</h4>
          <p className="mt-1 text-sm font-medium text-slate-800 leading-relaxed">
            {summary}
          </p>
        </div>
      </div>

      {error && (
        <p className="mt-3 text-xs font-medium text-red-600 leading-normal" role="alert">
          {error}
        </p>
      )}

      <div className="mt-4 flex gap-2">
        <button
          type="button"
          disabled={busy}
          onClick={handleConfirm}
          className="flex h-9 flex-1 items-center justify-center gap-1.5 rounded-xl bg-cyan-700 text-xs font-bold text-white hover:bg-cyan-800 disabled:opacity-50 transition-colors"
        >
          <Check size={14} />
          <span>تأكيد الإجراء</span>
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={handleCancel}
          className="flex h-9 items-center justify-center gap-1.5 rounded-xl border border-slate-200 bg-white px-4 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition-colors"
        >
          <X size={14} />
          <span>إلغاء</span>
        </button>
      </div>
    </div>
  );
}
