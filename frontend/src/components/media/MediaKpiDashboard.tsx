'use client';

import React, { useEffect, useState } from 'react';
import { 
  Video, 
  Clock, 
  AlertTriangle, 
  Award,
  RefreshCw
} from 'lucide-react';
import toast from 'react-hot-toast';
import { mediaService, MediaKpisDto } from '@/services/media-service';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function MediaKpiDashboard() {
  const [kpis, setKpis] = useState<MediaKpisDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchKpis();
  }, []);

  const fetchKpis = async () => {
    setLoading(true);
    try {
      const data = await mediaService.getMediaKpis();
      setKpis(data);
    } catch {
      toast.error('حدث خطأ أثناء تحميل مؤشرات الأداء');
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
        <NeumorphButton intent="ghost" size="sm" onClick={fetchKpis} disabled={loading}>
          <RefreshCw className={`h-4 w-4 ml-1.5 ${loading ? 'animate-spin' : ''}`} />
          تحديث المؤشرات
        </NeumorphButton>
      </div>

      {loading ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8 animate-pulse">
          {[1, 2, 3].map((n) => (
            <div key={n} className="h-32 rounded-3xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)]" />
          ))}
        </div>
      ) : (
        <>
          {/* Metrics Summary Cards */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
            {/* Total Published */}
            <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 shadow-md backdrop-blur-md relative overflow-hidden group hover:border-[var(--admin-primary-30)] transition-all">
              <div className="flex items-center justify-between mb-4">
                <span className="text-sm font-bold text-[var(--admin-muted)]">إجمالي الفيديوهات المنشورة</span>
                <span className="p-3 rounded-2xl bg-indigo-500/10 text-indigo-500">
                  <Video className="h-5 w-5" />
                </span>
              </div>
              <div className="flex items-baseline gap-2">
                <span className="text-3xl font-extrabold text-[var(--admin-text)]">{kpis?.totalPublished || 0}</span>
                <span className="text-xs text-[var(--admin-muted)]">فيديو مكتمل ونشط</span>
              </div>
              <div className="absolute -right-6 -bottom-6 opacity-[0.03] group-hover:scale-110 transition-transform duration-500">
                <Video className="h-32 w-32 text-indigo-500" />
              </div>
            </div>

            {/* Average Editing Time */}
            <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 shadow-md backdrop-blur-md relative overflow-hidden group hover:border-[var(--admin-primary-30)] transition-all">
              <div className="flex items-center justify-between mb-4">
                <span className="text-sm font-bold text-[var(--admin-muted)]">متوسط وقت المونتاج والإنتاج</span>
                <span className="p-3 rounded-2xl bg-amber-500/10 text-amber-500">
                  <Clock className="h-5 w-5" />
                </span>
              </div>
              <div className="flex items-baseline gap-2">
                <span className="text-3xl font-extrabold text-[var(--admin-text)]">{kpis?.averageEditingDays || 0}</span>
                <span className="text-xs text-[var(--admin-muted)]">أيام للفيديو الواحد</span>
              </div>
              <div className="absolute -right-6 -bottom-6 opacity-[0.03] group-hover:scale-110 transition-transform duration-500">
                <Clock className="h-32 w-32 text-amber-500" />
              </div>
            </div>

            {/* Total Editing Errors */}
            <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 shadow-md backdrop-blur-md relative overflow-hidden group hover:border-[var(--admin-primary-30)] transition-all">
              <div className="flex items-center justify-between mb-4">
                <span className="text-sm font-bold text-[var(--admin-muted)]">إجمالي أخطاء المونتاج المرصودة</span>
                <span className="p-3 rounded-2xl bg-rose-500/10 text-rose-500">
                  <AlertTriangle className="h-5 w-5" />
                </span>
              </div>
              <div className="flex items-baseline gap-2">
                <span className="text-3xl font-extrabold text-[var(--admin-text)]">{totalErrors}</span>
                <span className="text-xs text-[var(--admin-muted)]">ملاحظة خطأ تعديل</span>
              </div>
              <div className="absolute -right-6 -bottom-6 opacity-[0.03] group-hover:scale-110 transition-transform duration-500">
                <AlertTriangle className="h-32 w-32 text-rose-500" />
              </div>
            </div>
          </div>

          {/* Leaderboard Table Panel */}
          <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-lg">
            <div className="flex items-center gap-2 mb-6">
              <Award className="h-5 w-5 text-[var(--admin-primary)]" />
              <h3 className="text-lg font-bold text-[var(--admin-text)]">ترتيب وتقييم أداء المحررين</h3>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-right border-collapse">
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
                        لا توجد بيانات متاحة للمحررين في الوقت الحالي.
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
