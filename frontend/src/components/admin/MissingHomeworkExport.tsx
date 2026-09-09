'use client';

import { useState } from 'react';
import NeumorphButton from '@/components/ui/neumorph-button';
import apiClient from '@/services/api-client';
import toast from 'react-hot-toast';

const csvCell = (value: string) => `"${(/^[\s]*[=+@-]/.test(value) ? `'${value}` : value).replaceAll('"', '""')}"`;

export function MissingHomeworkExport({ homeworkId }: { homeworkId: string }) {
  const [busy, setBusy] = useState(false);
  const download = async () => {
    if (busy) return;
    setBusy(true);
    try {
      const students = new Map<string, { name: string; phone: string }>();
      for (let page = 1; ; page++) {
        const response = await apiClient.get<{ data: { students: { studentId: string; name: string; phone: string }[]; hasMore: boolean } }>(`/admin/homework/${homeworkId}/missing-students`, { params: { page } });
        const batch = response.data.data;
        for (const student of batch.students) students.set(student.studentId, student);
        if (!batch.hasMore) break;
      }
      if (!students.size) { toast.success('كل الطلاب المستحقين سلّموا الواجب، أو لا يوجد طلاب مستحقون حاليًا.'); return; }
      const csv = '\uFEFF' + [['اسم الطالب', 'رقم الهاتف'], ...Array.from(students.values(), s => [s.name, `'${s.phone}`])].map(row => row.map(csvCell).join(',')).join('\r\n');
      const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
      const link = document.createElement('a');
      link.href = url; link.download = `homework-${homeworkId}-not-submitted.csv`;
      document.body.append(link); link.click(); link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      toast.success(`تم تجهيز ${students.size} طالب في ملف يفتح ببرنامج Excel.`);
    } catch { toast.error('تعذر تجهيز الملف كاملًا. أعد المحاولة.'); }
    finally { setBusy(false); }
  };
  return <NeumorphButton type="button" disabled={busy} onClick={() => void download()}>{busy ? 'جارٍ تجهيز الملف…' : 'تنزيل الطلاب الذين لم يسلّموا'}</NeumorphButton>;
}
