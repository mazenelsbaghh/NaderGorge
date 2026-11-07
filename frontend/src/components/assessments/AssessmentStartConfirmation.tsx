'use client';

import { useEffect, useState } from 'react';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import apiClient from '@/services/api-client';

interface QuestionCount {
  minimum: number;
  maximum: number;
}

interface EntrySummary {
  total: number;
  essay: QuestionCount;
  multipleChoice: QuestionCount;
  findTheMistake: QuestionCount;
}

const formatNumber = new Intl.NumberFormat('ar-EG').format;

function formatSummary(summary: EntrySummary) {
  const types = [
    { label: 'مقالي', ...summary.essay },
    { label: 'اختياري', ...summary.multipleChoice },
    { label: 'اكتشف الخطأ', ...summary.findTheMistake },
  ].filter(type => type.maximum > 0);
  const variable = types.some(type => type.minimum !== type.maximum);
  return [
    `عدد الأسئلة: ${formatNumber(summary.total)}`,
    ...types.map(type => type.minimum === type.maximum
      ? `${formatNumber(type.maximum)} ${type.label}`
      : `${type.label} من ${formatNumber(type.minimum)} إلى ${formatNumber(type.maximum)}`),
    ...(variable ? ['التوزيع بيتحدد بالسحب العشوائي عند بدء المحاولة'] : []),
  ].join(' · ');
}

export function AssessmentStartConfirmation({ kind, assessmentId, onConfirm, onCancel }: {
  kind: 'exam' | 'homework';
  assessmentId: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const [summary, setSummary] = useState<string | null>(null);
  const resource = kind === 'exam' ? 'exams' : 'homework';
  const label = kind === 'exam' ? 'الامتحان' : 'الواجب';

  useEffect(() => {
    const request = new AbortController();
    void apiClient.get<{ data: EntrySummary }>(`/${resource}/${encodeURIComponent(assessmentId)}/entry-summary`, {
      signal: request.signal,
      suppressErrorToast: true,
    }).then(({ data }) => {
      if (!request.signal.aborted) {
        setSummary(Number.isInteger(data.data?.total) ? formatSummary(data.data) : 'تعذّر عرض عدد الأسئلة.');
      }
    }).catch(() => {
      if (!request.signal.aborted) setSummary('تعذّر عرض عدد الأسئلة.');
    });
    return () => request.abort();
  }, [assessmentId, resource]);

  return (
    <ConfirmDialog
      open
      variant="primary"
      title={`هل أنت متأكد من دخول ${label}؟`}
      description="عند التأكيد ستدخل المحاولة. يبدأ وقت المحاولة الجديدة إذا كانت محددة بمدة، والإلغاء لا يبدأ محاولة جديدة."
      details={summary ?? 'جارٍ تحميل عدد الأسئلة…'}
      confirmDisabled={summary === null}
      confirmLabel={`نعم، دخول ${label}`}
      cancelLabel="إلغاء"
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );
}
