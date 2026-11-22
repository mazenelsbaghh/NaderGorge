'use client';

import { useState } from 'react';
import { Download } from 'lucide-react';
import type { DesktopReceipt, DesktopStudent, DesktopStudentSearch } from '@/services/center-desktop-service';
import { saveBlob } from './display';

export default function DesktopStudentPdfButton({ student, profile, receipt }: {
  student: DesktopStudent; profile: NonNullable<DesktopStudentSearch['profile']>; receipt: DesktopReceipt;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const download = async () => {
    setBusy(true); setError('');
    try {
      const { createStudentPdf } = await import('./desktop-student-pdf');
      const blob = await createStudentPdf({ student, profile, receipt });
      const code = student.code.replace(/[^a-zA-Z0-9_-]/g, '').slice(0, 40) || 'student';
      saveBlob(blob, `student-report-${code}.pdf`);
    } catch {
      setError('تعذر تجهيز التقرير. حاول تنزيل PDF مرة أخرى.');
    } finally { setBusy(false); }
  };
  return <div className="space-y-1">
    <button type="button" className="admin-btn-ghost inline-flex items-center gap-2" disabled={busy} onClick={() => void download()}><Download aria-hidden className="h-4 w-4" />{busy ? 'جاري تجهيز PDF…' : 'تنزيل PDF للطالب'}</button>
    <p className="text-xs text-[var(--admin-muted)]">يشمل كل الشهور والمجموعات.</p>
    {error && <p role="alert" className="text-sm text-[var(--admin-danger)]">{error}</p>}
  </div>;
}
