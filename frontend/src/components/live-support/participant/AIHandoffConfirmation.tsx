'use client';

import { useRef, useState } from 'react';
import { UserCheck, X, Headphones } from 'lucide-react';
import { getLiveSupportApiError } from '@/services/live-support-service';

interface AIHandoffConfirmationProps {
  action: {
    id: string;
    actionKey: string;
    safeProposalJson: string;
    status: string;
    expiresAt: string;
  };
  onConfirm: () => Promise<void>;
  onCancel: () => Promise<void>;
}

export function AIHandoffConfirmation({ action, onConfirm, onCancel }: AIHandoffConfirmationProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const primaryButton = useRef<HTMLButtonElement>(null);
  const expired = new Date(action.expiresAt) <= new Date();

  let safeSummary = '';
  try {
    const parsed = JSON.parse(action.safeProposalJson);
    if (parsed.safeSummaryAr) {
      safeSummary = parsed.safeSummaryAr;
    }
    } catch {
    // Ignore JSON parse errors
  }

  const handleConfirm = async () => {
    setBusy(true);
    setError('');
    try {
      await onConfirm();
    } catch (error) {
      setError(getLiveSupportApiError(error, 'فشلت عملية التحويل للموظف البشري.'));
    } finally {
      setBusy(false);
      primaryButton.current?.focus();
    }
  };

  const handleCancel = async () => {
    setBusy(true);
    setError('');
    try {
      await onCancel();
    } catch (error) {
      setError(getLiveSupportApiError(error, 'فشل إلغاء طلب التحويل.'));
    } finally {
      setBusy(false);
      primaryButton.current?.focus();
    }
  };

  return (
    <div dir="rtl" role="region" aria-label="التحويل لموظف بشري" className="my-3 rounded-2xl border border-amber-100 bg-amber-50/50 p-4 shadow-sm transition-all hover:shadow-md">
      <div className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-amber-100 text-amber-800">
          <Headphones size={20} />
        </span>
        <div className="flex-1 min-w-0">
          <h4 className="text-xs font-semibold text-amber-900">التحويل لموظف بشري</h4>
          <p className="mt-1 text-sm font-medium text-slate-800 leading-relaxed">
            يريد مساعد الدعم تحويلك لموظف بشري للمساعدة في: 
            <span className="font-bold text-amber-950"> {safeSummary || 'حل مشكلتك التقنية'}</span>.
            <br />
            هل توافق على التحويل؟
          </p>
          <p className="mt-2 text-xs text-slate-500">ينتهي الطلب: <time dateTime={action.expiresAt}>{new Date(action.expiresAt).toLocaleTimeString('ar-EG')}</time></p>
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
          ref={primaryButton}
          disabled={busy || expired}
          onClick={handleConfirm}
          className="flex min-h-11 flex-1 items-center justify-center gap-1.5 rounded-xl bg-amber-700 text-xs font-bold text-white hover:bg-amber-800 disabled:opacity-50 transition-colors"
        >
          <UserCheck size={14} />
          <span>نعم، حوّلني</span>
        </button>
        <button
          type="button"
          disabled={busy || expired}
          onClick={handleCancel}
          className="flex min-h-11 items-center justify-center gap-1.5 rounded-xl border border-slate-200 bg-white px-4 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition-colors"
        >
          <X size={14} />
          <span>لا، استمر مع المساعد</span>
        </button>
      </div>
    </div>
  );
}
