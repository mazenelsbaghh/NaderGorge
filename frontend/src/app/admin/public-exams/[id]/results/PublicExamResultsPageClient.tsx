'use client';

import { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowRight, CheckCircle2, RefreshCcw, XCircle } from 'lucide-react';
import { adminSalesService, type PublicExamResultsDto } from '@/services/admin-sales-service';

export default function AdminPublicExamResultsPageClient({ productId }: { productId: string }) {
  const [report, setReport] = useState<PublicExamResultsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setReport(await adminSalesService.publicExamResults(productId));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'تعذر تحميل تقرير الامتحان العام.');
    } finally {
      setLoading(false);
    }
  }, [productId]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <main className="min-h-screen bg-slate-50 p-4 text-slate-950 md:p-6" dir="rtl">
      <div className="mx-auto max-w-7xl space-y-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <Link href="/admin/public-exams" className="mb-2 inline-flex items-center gap-2 text-sm font-bold text-slate-600 hover:text-slate-950">
              <ArrowRight className="h-4 w-4" />
              رجوع للامتحانات العامة
            </Link>
            <h1 className="text-2xl font-semibold">{report?.examTitle ?? 'تقرير الامتحان العام'}</h1>
            <p className="mt-1 text-sm text-slate-600">نتائج مستقلة عن امتحانات الحصص والفيديوهات.</p>
          </div>
          <button onClick={load} disabled={loading} className="inline-flex items-center gap-2 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium hover:bg-slate-100 disabled:opacity-60">
            <RefreshCcw className="h-4 w-4" />
            تحديث
          </button>
        </div>

        {error ? <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm font-bold text-red-700">{error}</div> : null}

        {loading ? (
          <div className="grid gap-4 md:grid-cols-3">
            {[1, 2, 3].map((item) => <div key={item} className="h-28 animate-pulse rounded-md bg-slate-200" />)}
          </div>
        ) : report ? (
          <>
            <div className="grid gap-3 md:grid-cols-4">
              <Metric label="المحاولات" value={report.attemptCount} />
              <Metric label="الناجحين" value={report.passedCount} />
              <Metric label="متوسط الدرجة" value={report.averageScore} />
              <Metric label="السعر" value={report.isPaid ? `${report.price} ج.م` : 'مجاني'} />
            </div>

            <section className="rounded-md border border-slate-200 bg-white p-4">
              <h2 className="mb-3 text-lg font-semibold">محاولات الطلاب</h2>
              <div className="overflow-x-auto">
                <table className="min-w-full text-sm">
                  <thead className="bg-slate-100 text-slate-600">
                    <tr>
                      <th className="px-3 py-2 text-right">الطالب</th>
                      <th className="px-3 py-2 text-right">الهاتف</th>
                      <th className="px-3 py-2 text-right">الدرجة</th>
                      <th className="px-3 py-2 text-right">الحالة</th>
                      <th className="px-3 py-2 text-right">التقييم</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.attempts.map((attempt) => (
                      <tr key={attempt.attemptId} className="border-t border-slate-100">
                        <td className="px-3 py-2 font-medium">{attempt.studentName}</td>
                        <td className="px-3 py-2">{attempt.studentPhone}</td>
                        <td className="px-3 py-2">{attempt.scoreAchieved}</td>
                        <td className="px-3 py-2">
                          <span className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-xs font-bold ${attempt.isPassed ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700'}`}>
                            {attempt.isPassed ? <CheckCircle2 className="h-3 w-3" /> : <XCircle className="h-3 w-3" />}
                            {attempt.isPassed ? 'ناجح' : 'غير ناجح'}
                          </span>
                        </td>
                        <td className="px-3 py-2">{attempt.evaluation}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="rounded-md border border-slate-200 bg-white p-4">
              <h2 className="mb-3 text-lg font-semibold">تحليل الأسئلة</h2>
              <div className="grid gap-2">
                {report.questions.map((question) => (
                  <div key={question.examQuestionId} className="rounded-md border border-slate-200 p-3">
                    <p className="font-medium">{question.text}</p>
                    <p className="mt-1 text-sm text-slate-600">
                      إجابات: {question.totalAnswers} - صحيحة: {question.correctAnswers} - نسبة الصحة: {question.correctPercentage}%
                    </p>
                  </div>
                ))}
              </div>
            </section>
          </>
        ) : null}
      </div>
    </main>
  );
}

function Metric({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-md border border-slate-200 bg-white p-4">
      <p className="text-sm font-medium text-slate-500">{label}</p>
      <p className="mt-2 text-2xl font-semibold text-slate-950">{value}</p>
    </div>
  );
}
