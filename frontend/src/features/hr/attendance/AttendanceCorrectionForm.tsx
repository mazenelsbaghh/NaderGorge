'use client';
import { FormEvent, useState } from 'react';
import { isAxiosError } from 'axios';
import { FilePenLine } from 'lucide-react';
import toast from 'react-hot-toast';
import { AttendanceSessionDto, hrService } from '@/services/hr-service';
import { cairoDateTimeLocalToUtcISOString, parseUtcDateTime } from '@/lib/cairo-time';

export function AttendanceCorrectionForm({ sessions, onSubmitted }: { sessions: AttendanceSessionDto[]; onSubmitted?: () => void }) {
  const [sessionId, setSessionId] = useState(''); const [clockOut, setClockOut] = useState(''); const [reason, setReason] = useState(''); const [submitting, setSubmitting] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    const session = sessions.find((row) => row.id === sessionId);
    if (!session || !clockOut) { toast.error('اختر الجلسة وحدد وقت الانصراف الصحيح.'); return; }
    const proposedClockedOutAt = cairoDateTimeLocalToUtcISOString(clockOut);
    if (parseUtcDateTime(proposedClockedOutAt).getTime() <= parseUtcDateTime(session.clockedInAt).getTime()) {
      toast.error('وقت الانصراف يجب أن يكون بعد وقت الحضور.');
      return;
    }
    setSubmitting(true);
    try {
      await hrService.submitAttendanceCorrection({ attendanceSessionId: sessionId, proposedClockedOutAt, reason });
      toast.success('تم إرسال التصحيح للمراجعة');
      setSessionId(''); setClockOut(''); setReason(''); onSubmitted?.();
    } catch (cause: unknown) {
      const message = isAxiosError<{ message?: string }>(cause) ? cause.response?.data?.message : undefined;
      toast.error(message ?? 'تعذر إرسال التصحيح');
    } finally { setSubmitting(false); }
  }
  return <form onSubmit={submit} className="admin-panel space-y-4"><div className="flex items-center gap-2"><FilePenLine className="h-5 w-5 text-[var(--admin-primary)]" /><h2 className="font-black">طلب تصحيح حضور</h2></div><label className="block text-sm font-bold">الجلسة<select required value={sessionId} onChange={(e) => setSessionId(e.target.value)} className="admin-input mt-1 w-full"><option value="">اختر جلسة</option>{sessions.map((row) => <option key={row.id} value={row.id}>{row.workDate} · {row.state}</option>)}</select></label><label className="block text-sm font-bold">وقت الانصراف الصحيح<input aria-label="وقت الانصراف الصحيح" type="datetime-local" value={clockOut} onChange={(e) => setClockOut(e.target.value)} className="admin-input mt-1 w-full" /></label><label className="block text-sm font-bold">السبب<textarea required value={reason} onChange={(e) => setReason(e.target.value)} className="admin-input mt-1 min-h-24 w-full" /></label><button disabled={submitting} className="admin-btn-primary min-h-11 w-full">إرسال للمراجعة</button></form>;
}
