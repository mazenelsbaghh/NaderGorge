'use client';

import { useState } from 'react';
import { Download } from 'lucide-react';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import { exportAssessment, type AssessmentExportKind, type AssessmentExportSelection } from '@/services/assessment-export-service';
import { getApiErrorSummary } from '@/lib/api-errors';

export function AssessmentExportButtons({ kind, id, audience }: {
  kind: AssessmentExportKind; id: string; audience: 'admin' | 'teacher';
}) {
  const [busy, setBusy] = useState<AssessmentExportSelection | null>(null);
  const download = async (selection: AssessmentExportSelection) => {
    if (busy) return;
    setBusy(selection);
    try {
      const blob = await exportAssessment({ audience, kind, id, selection });
      saveAssessmentFile(blob, `${kind}-${id}-${selection}.xlsx`);
      toast.success(selection === 'grades' ? 'تم تنزيل درجات هذه النسخة.' : 'تم تنزيل كشف الغياب لهذه النسخة.');
    } catch (error: unknown) {
      toast.error(await downloadError(error));
    } finally {
      setBusy(null);
    }
  };

  return <div className="flex flex-wrap items-center gap-2" role="group" aria-label="تنزيل كشوف هذه النسخة" aria-busy={busy !== null}>
    <NeumorphButton type="button" className="min-h-11" disabled={busy !== null} onClick={() => void download('grades')}>
      <Download className="h-4 w-4" aria-hidden="true" />{busy === 'grades' ? 'جاري تجهيز الدرجات…' : 'تنزيل الدرجات إكسيل'}
    </NeumorphButton>
    <NeumorphButton type="button" intent="ghost" className="min-h-11" disabled={busy !== null} onClick={() => void download('absent')}>
      <Download className="h-4 w-4" aria-hidden="true" />{busy === 'absent' ? 'جاري تجهيز الغياب…' : 'تنزيل الغياب إكسيل'}
    </NeumorphButton>
  </div>;
}

function saveAssessmentFile(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

async function downloadError(error: unknown): Promise<string> {
  const response = (error as { response?: { data?: unknown } })?.response?.data;
  if (response instanceof Blob && response.type.includes('json')) {
    try {
      const payload = JSON.parse(await response.text()) as { message?: string };
      if (payload.message) return payload.message;
    } catch {
      return 'تعذر تجهيز ملف الإكسيل. أعد المحاولة.';
    }
  }
  return getApiErrorSummary(error, 'تعذر تجهيز ملف الإكسيل. أعد المحاولة.');
}
