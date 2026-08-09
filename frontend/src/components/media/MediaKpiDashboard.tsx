'use client';

import React, { useEffect, useState } from 'react';
import { 
  Video, 
  Clock, 
  AlertTriangle, 
  Award,
  RefreshCw
} from 'lucide-react';
import { mediaService, MediaKpisDto } from '@/services/media-service';
import { registerCacheStore } from '@/lib/cache-invalidation';

export default function MediaKpiDashboard() {
  const [kpis, setKpis] = useState<MediaKpisDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchKpis();
    const cleanupCacheStore = registerCacheStore('media:kpis', () => setKpis(null), fetchKpis);
    return cleanupCacheStore;
  }, []);

  const fetchKpis = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await mediaService.getMediaKpis();
      setKpis(data);
    } catch {
      setError('لم نتمكن من تحميل مؤشرات الأداء. تحقق من اتصالك ثم أعد المحاولة؛ لن تتأثر أي بيانات محفوظة.');
    } finally {
      setLoading(false);
    }
  };

  const totalErrors = kpis?.editorLeaderboard?.reduce((sum, current) => sum + current.totalErrors, 0) || 0;

  return (
    <div dir="rtl" className="w-full">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
        <div>
          <h2 className="text-xl font-bold text-[var(--admin-text)]">لوحة مؤشرات الأداء والتقارير</h2>
          <p className="text-sm text-[var(--admin-muted)] mt-1">رصد جودة وسرعة عمليات المونتاج والإنتاج، وتتبع تقييم المحررين.</p>
        </div>
        <button type="button" className="admin-btn-ghost min-h-11 px-4" onClick={fetchKpis} disabled={loading}>
          <RefreshCw className={`ms-1.5 h-4 w-4 ${loading ? 'animate-spin' : ''}`} aria-hidden="true" />
          تحديث المؤشرات
        </button>
      </div>

      {error && (
        <div role="alert" className="mb-6 flex flex-col gap-3 rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-sm font-bold leading-6 text-[var(--admin-text)]">{error}</p>
          <button type="button" className="admin-btn-ghost min-h-11 shrink-0 px-4" onClick={fetchKpis}>إعادة المحاولة</button>
        </div>
      )}

      {loading ? (
        <div className="mb-8 grid grid-cols-1 gap-3 md:grid-cols-3" aria-busy="true" aria-label="جارٍ تحميل المؤشرات">
          {[1, 2, 3].map((n) => (
            <div key={n} className="h-24 animate-pulse rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)]" />
          ))}
        </div>
      ) : (
        <>
          {/* Metrics Summary Cards */}
          <dl className="mb-8 grid grid-cols-1 overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] md:grid-cols-3">
            {/* Total Published */}
            <div className="border-b border-[var(--admin-border)] p-5 md:border-b-0 md:border-s">
              <div className="mb-3 flex items-center gap-3">
                <span className="rounded-xl bg-[var(--admin-primary-15)] p-2.5 text-[var(--admin-primary)]">
                  <Video className="h-5 w-5" aria-hidden="true" />
                </span>
                <dt className="text-sm font-bold text-[var(--admin-muted)]">إجمالي الفيديوهات المنشورة</dt>
              </div>
              <dd className="flex items-baseline gap-2">
                <span className="text-3xl font-extrabold text-[var(--admin-text)]">{kpis?.totalPublished ?? 0}</span>
                <span className="text-xs text-[var(--admin-muted)]">فيديو مكتمل ونشط</span>
              </dd>
            </div>

            {/* Average Editing Time */}
            <div className="border-b border-[var(--admin-border)] p-5 md:border-b-0 md:border-s">
              <div className="mb-3 flex items-center gap-3">
                <span className="rounded-xl bg-[var(--admin-warning-10)] p-2.5 text-[var(--admin-warning)]">
                  <Clock className="h-5 w-5" aria-hidden="true" />
                </span>
                <dt className="text-sm font-bold text-[var(--admin-muted)]">متوسط وقت المونتاج والإنتاج</dt>
              </div>
              <dd className="flex items-baseline gap-2">
                <span className="text-3xl font-extrabold text-[var(--admin-text)]">{kpis?.averageEditingDays ?? 0}</span>
                <span className="text-xs text-[var(--admin-muted)]">أيام للفيديو الواحد</span>
              </dd>
            </div>

            {/* Total Editing Errors */}
            <div className="p-5">
              <div className="mb-3 flex items-center gap-3">
                <span className="rounded-xl bg-[var(--admin-danger-10)] p-2.5 text-[var(--admin-danger)]">
                  <AlertTriangle className="h-5 w-5" aria-hidden="true" />
                </span>
                <dt className="text-sm font-bold text-[var(--admin-muted)]">إجمالي أخطاء المونتاج المرصودة</dt>
              </div>
              <dd className="flex items-baseline gap-2">
                <span className="text-3xl font-extrabold text-[var(--admin-text)]">{totalErrors}</span>
                <span className="text-xs text-[var(--admin-muted)]">ملاحظة خطأ تعديل</span>
              </dd>
            </div>
          </dl>

          {/* Leaderboard Table Panel */}
          <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-6">
            <div className="flex items-center gap-2 mb-6">
              <Award className="h-5 w-5 text-[var(--admin-primary)]" />
              <h3 className="text-lg font-bold text-[var(--admin-text)]">ترتيب وتقييم أداء المحررين</h3>
            </div>

            <div className="horizontal-scroll-region overflow-x-auto" tabIndex={0} role="region" aria-label="جدول ترتيب المحررين؛ يمكن تمريره أفقياً">
              <table className="w-full text-right border-collapse">
                <caption className="sr-only">ترتيب المحررين حسب الإنتاج والأخطاء ومتوسط الجودة</caption>
                <thead>
                  <tr className="border-b border-[var(--admin-border)] text-xs text-[var(--admin-muted)] font-bold">
                    <th className="pb-3 pr-4">اسم محرر المونتاج</th>
                    <th className="pb-3 text-center">المواد المنتجة</th>
                    <th className="pb-3 text-center">إجمالي الأخطاء المرصودة</th>
                    <th className="pb-3 text-center">معدل جودة الفيديو (متوسط الأخطاء)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--admin-border)]">
                  {kpis?.editorLeaderboard && kpis.editorLeaderboard.length > 0 ? (
                    kpis.editorLeaderboard.map((editor, index) => {
                      const errorRate = editor.totalProduced > 0 
                        ? Math.round((editor.totalErrors / editor.totalProduced) * 10) / 10 
                        : 0;

                      // Decide rating quality color
                      const errorRateColor = errorRate === 0 
                        ? 'text-emerald-500 bg-emerald-500/10' 
                        : errorRate < 1 
                        ? 'text-blue-500 bg-blue-500/10'
                        : errorRate < 2.5
                        ? 'text-amber-500 bg-amber-500/10'
                        : 'text-rose-500 bg-rose-500/10';

                      return (
                        <tr key={editor.editorId} className="hover:bg-[var(--admin-hover)]/30 transition-colors">
                          <td className="py-4 pr-4 font-bold text-xs text-[var(--admin-text)] flex items-center gap-3">
                            <span className="w-6 h-6 flex items-center justify-center rounded-full bg-[var(--admin-primary-15)] text-[var(--admin-primary)] text-xs font-bold">
                              {index + 1}
                            </span>
                            {editor.editorName}
                          </td>
                          <td className="py-4 text-center text-xs font-semibold text-[var(--admin-text)]">
                            {editor.totalProduced}
                          </td>
                          <td className="py-4 text-center text-xs text-rose-500 font-bold">
                            {editor.totalErrors}
                          </td>
                          <td className="py-4 text-center">
                            <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold ${errorRateColor}`}>
                              {errorRate} خطأ / فيديو
                            </span>
                          </td>
                        </tr>
                      );
                    })
                  ) : (
                    <tr>
                      <td colSpan={4} className="py-6 text-center text-xs text-[var(--admin-muted)]">
                        لا توجد بيانات بعد. ستظهر المؤشرات عند إسناد أول مادة لمحرر وتسجيل نتيجة الإنتاج.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
