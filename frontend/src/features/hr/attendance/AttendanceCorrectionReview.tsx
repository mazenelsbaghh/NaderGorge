'use client';
import { useCallback, useEffect, useState } from 'react';
import { Check, RefreshCw, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { AttendanceCorrectionDto, hrService } from '@/services/hr-service';
import { formatCairoDateTime } from '@/lib/cairo-time';

function parseBeforeEvidence(serializedEvidence: string): Record<string, unknown> {
  try {
    return JSON.parse(serializedEvidence) as Record<string, unknown>;
  } catch (error) {
    if (error instanceof SyntaxError) return {};
    throw error;
  }
}

export function AttendanceCorrectionReview() {
  const [rows, setRows] = useState<AttendanceCorrectionDto[]>([]); const [loading, setLoading] = useState(true);
  const load = useCallback(async () => { setLoading(true); try { setRows(await hrService.listAttendanceCorrections()); } catch { toast.error('تعذر تحميل التصحيحات'); } finally { setLoading(false); } }, []);
  useEffect(() => { void load(); }, [load]);
  async function decide(row: AttendanceCorrectionDto, approve: boolean) { const reason = approve ? 'تمت مراجعة الفرق' : 'البيانات غير كافية'; try { await hrService.decideAttendanceCorrection(row.id, { approve, isHrDecision: row.state === 'PendingHr', reason, expectedVersion: row.version }); toast.success('تم حفظ القرار'); await load(); } catch { toast.error('تعذر حفظ القرار'); } }
  if (loading) return <div className="admin-panel py-16 text-center"><RefreshCw className="mx-auto h-6 w-6 animate-spin" /></div>;
  return <div className="space-y-3">{rows.length === 0 ? <div className="admin-panel py-14 text-center font-bold text-[var(--admin-muted)]">لا توجد تصحيحات للمراجعة.</div> : rows.map((row) => { const before = parseBeforeEvidence(row.beforeJson); const previousClockOut = typeof before.clockedOutAt === 'string' ? formatCairoDateTime(before.clockedOutAt, { dateStyle: 'medium', timeStyle: 'short' }) : 'غير مسجل'; return <article key={row.id} className="admin-panel"><div className="flex flex-wrap justify-between gap-3"><div><p className="font-black">{row.employee}</p><p className="text-sm text-[var(--admin-muted)]">{row.reason}</p></div><span className="admin-badge">{row.state}</span></div><div className="mt-4 grid gap-3 rounded-2xl bg-[var(--admin-card-soft)] p-4 sm:grid-cols-2"><div><p className="text-xs font-black text-[var(--admin-muted)]">قبل</p><p className="mt-1 text-sm font-bold">انصراف: {previousClockOut}</p></div><div><p className="text-xs font-black text-[var(--admin-primary)]">المطلوب</p><p className="mt-1 text-sm font-bold">انصراف: {row.proposedClockedOutAt ? formatCairoDateTime(row.proposedClockedOutAt, { dateStyle: 'medium', timeStyle: 'short' }) : 'بدون تغيير'}</p></div></div>{(row.state === 'PendingManager' || row.state === 'PendingHr') && <div className="mt-4 flex gap-2"><button onClick={() => void decide(row, true)} className="admin-btn-primary inline-flex min-h-11 items-center gap-2"><Check className="h-4 w-4" />اعتماد</button><button onClick={() => void decide(row, false)} className="admin-btn-secondary inline-flex min-h-11 items-center gap-2"><X className="h-4 w-4" />رفض</button></div>}</article>; })}</div>;
}
