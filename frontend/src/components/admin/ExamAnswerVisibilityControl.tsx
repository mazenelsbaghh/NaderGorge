'use client';

import { useState } from 'react';
import { adminService } from '@/services/admin-service';

export function ExamAnswerVisibilityControl({ examId, visible, onChanged }: {
  examId: string; visible: boolean; onChanged: (visible: boolean) => void;
}) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const changeVisibility = async () => {
    setSaving(true);
    setError('');
    setMessage('');
    try {
      const response = await adminService.setExamAnswerVisibility(examId, !visible);
      if (!response.success) throw new Error(response.message || 'تعذر تغيير ظهور الإجابات.');
      onChanged(response.data.showAnswersToStudents);
      setMessage(response.data.showAnswersToStudents
        ? `الإجابات متاحة. تم إرسال إشعار إلى ${response.data.notifiedStudents} طالب.`
        : 'الطالب يشوف درجته فقط؛ الإجابات والتصحيح مخفيان.');
    } catch {
      setError('تعذر تغيير ظهور الإجابات. حاول مرة أخرى.');
    } finally { setSaving(false); }
  };
  return (
    <section className="space-y-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5" aria-label="ظهور إجابات الامتحان">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="font-black text-[var(--admin-text)]">إظهار الإجابات للطلاب</h2>
          <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
            {visible ? 'الإجابات والتصحيح متاحان مع الدرجة.' : 'الدرجة فقط ظاهرة. الإجابات والتصحيح مخفيان حتى تفتحهما.'}
          </p>
          <p className="text-sm leading-6 text-[var(--admin-muted)]">عند فتح الإجابات، يصل إشعار للطلاب الذين سلّموا الامتحان مع رابط نتيجتهم، دون تكرار الإشعار.</p>
        </div>
        <button type="button" role="switch" aria-checked={visible} aria-label="إظهار الإجابات للطلاب"
          disabled={saving} onClick={() => void changeVisibility()}
          className="min-h-11 rounded-xl bg-[var(--admin-primary)] px-5 py-3 text-sm font-black text-white disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)]">
          {saving ? 'جاري الحفظ…' : visible ? 'إخفاء الإجابات وإظهار الدرجة فقط' : 'فتح الإجابات وإشعار الطلاب'}
        </button>
      </div>
      {error && <p role="alert" className="text-sm text-[var(--admin-danger)]">{error}</p>}
      {message && <p role="status" className="text-sm text-[var(--admin-text)]">{message}</p>}
    </section>
  );
}
