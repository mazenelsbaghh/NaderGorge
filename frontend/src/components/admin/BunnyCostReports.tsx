'use client';

import { useMemo, useState } from 'react';
import { CalendarDays, Database, RefreshCw, Wifi } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService, type BunnyCostReport } from '@/services/admin-service';
import NeumorphButton from '@/components/ui/neumorph-button';

function formatBytes(bytes: number) {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 GB';
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

function formatUsd(value: number) {
  return `$${Number(value || 0).toFixed(4)}`;
}

function currentMonth() {
  return new Date().toISOString().slice(0, 7);
}

export function BunnyCostReports() {
  const [month, setMonth] = useState(currentMonth());
  const [report, setReport] = useState<BunnyCostReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState(false);

  const period = useMemo(() => {
    const start = `${month}-01T00:00:00.000Z`;
    const end = new Date(`${month}-01T00:00:00.000Z`);
    end.setUTCMonth(end.getUTCMonth() + 1);
    return { start, end: end.toISOString() };
  }, [month]);

  const loadReport = async () => {
    setLoading(true);
    try {
      const data = await adminService.getBunnyCostReport({ month });
      setReport(data ?? null);
    } catch {
      toast.error('تعذر تحميل تقرير Bunny');
    } finally {
      setLoading(false);
    }
  };

  const syncReport = async () => {
    setSyncing(true);
    try {
      await adminService.syncBunnyUsage({ periodStart: period.start, periodEnd: period.end, forceRefresh: true });
      toast.success('تم تحديث snapshots الخاصة بـ Bunny');
      await loadReport();
    } catch {
      toast.error('تعذر تحديث بيانات Bunny');
    } finally {
      setSyncing(false);
    }
  };

  return (
    <section className="space-y-5" dir="rtl">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-xl font-black text-[var(--admin-text)]">تقارير تكلفة Bunny</h2>
          <p className="text-sm text-[var(--admin-muted)]">Snapshots شهرية للتخزين والباندويث والتكلفة.</p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <label className="space-y-1 text-right text-xs font-bold text-[var(--admin-muted)]">
            <span className="block">الشهر</span>
            <input
              type="month"
              value={month}
              onChange={(e) => setMonth(e.target.value)}
              className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm text-[var(--admin-text)]"
            />
          </label>
          <NeumorphButton type="button" onClick={loadReport} disabled={loading} loading={loading} intent="ghost" size="md">
            <CalendarDays className="h-4 w-4" />
            عرض التقرير
          </NeumorphButton>
          <NeumorphButton type="button" onClick={syncReport} disabled={syncing} loading={syncing} intent="primary" size="md">
            <RefreshCw className="h-4 w-4" />
            تحديث snapshots
          </NeumorphButton>
        </div>
      </div>

      {report && (
        <>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
            <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
              <div className="mb-2 flex items-center gap-2 text-sm font-bold text-[var(--admin-muted)]"><Database className="h-4 w-4" /> التخزين</div>
              <div className="font-mono text-2xl font-black text-[var(--admin-text)]">{formatBytes(report.platformStorageBytes)}</div>
            </div>
            <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
              <div className="mb-2 flex items-center gap-2 text-sm font-bold text-[var(--admin-muted)]"><Wifi className="h-4 w-4" /> الباندويث</div>
              <div className="font-mono text-2xl font-black text-[var(--admin-text)]">{formatBytes(report.platformBandwidthBytes)}</div>
            </div>
            <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
              <div className="mb-2 text-sm font-bold text-[var(--admin-muted)]">إجمالي التكلفة</div>
              <div className="font-mono text-2xl font-black text-[var(--admin-text)]">{formatUsd(report.platformTotalCostUsd)}</div>
              {report.estimatedBandwidthCount > 0 && <div className="mt-1 text-xs text-amber-600">يوجد {report.estimatedBandwidthCount} snapshot بباندويث تقديري.</div>}
            </div>
          </div>

          <div className="overflow-hidden rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
            <table className="w-full min-w-[760px] text-sm">
              <thead className="bg-[var(--admin-card-strong)] text-[var(--admin-muted)]">
                <tr>
                  <th className="px-4 py-3 text-right">الفيديو</th>
                  <th className="px-4 py-3 text-right">التخزين</th>
                  <th className="px-4 py-3 text-right">الباندويث</th>
                  <th className="px-4 py-3 text-right">تكلفة التخزين</th>
                  <th className="px-4 py-3 text-right">تكلفة الباندويث</th>
                  <th className="px-4 py-3 text-right">الإجمالي</th>
                </tr>
              </thead>
              <tbody>
                {report.videos.map((video) => (
                  <tr key={video.bunnyVideoAssetId} className="border-t border-[var(--admin-border)]">
                    <td className="px-4 py-3 font-bold text-[var(--admin-text)]">{video.title}</td>
                    <td className="px-4 py-3 font-mono">{formatBytes(video.storageBytes)}</td>
                    <td className="px-4 py-3 font-mono">{formatBytes(video.bandwidthBytes)}{video.isBandwidthEstimated ? ' *' : ''}</td>
                    <td className="px-4 py-3 font-mono">{formatUsd(video.storageCostUsd)}</td>
                    <td className="px-4 py-3 font-mono">{formatUsd(video.bandwidthCostUsd)}</td>
                    <td className="px-4 py-3 font-mono font-black">{formatUsd(video.totalCostUsd)}</td>
                  </tr>
                ))}
                {report.videos.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-[var(--admin-muted)]">لا توجد snapshots لهذا الشهر.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}
