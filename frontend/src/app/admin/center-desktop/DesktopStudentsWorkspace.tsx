'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { getDesktopUploads, type DesktopReceipt } from '@/services/center-desktop-service';
import DesktopStudentsPanel from './DesktopStudentsPanel';

// The support cursor sorts UUIDs, so the newest snapshot can be on any page.
async function latestHosts(signal: AbortSignal) {
  const centers = new Map<string, DesktopReceipt>();
  const cursors = new Set<string>();
  let cursor = '';
  do {
    if (cursors.has(cursor)) throw new Error('Repeated support cursor');
    cursors.add(cursor);
    const page = await getDesktopUploads(signal, cursor, 500);
    for (const upload of page.uploads) {
      if (upload.app.role !== 'host') continue;
      const previous = centers.get(upload.centerId);
      if (!previous || Date.parse(upload.createdAt) > Date.parse(previous.createdAt) ||
          upload.createdAt === previous.createdAt && Date.parse(upload.receivedAt) > Date.parse(previous.receivedAt)) {
        centers.set(upload.centerId, upload);
      }
    }
    cursor = page.nextCursor;
  } while (cursor);
  return [...centers.values()].sort((a, b) => a.centerId.localeCompare(b.centerId));
}

export default function DesktopStudentsWorkspace() {
  const [snapshots, setSnapshots] = useState<DesktopReceipt[]>([]);
  const [centerId, setCenterId] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const active = useRef<AbortController | null>(null);
  const reload = useCallback(async () => {
    active.current?.abort();
    const request = new AbortController();
    active.current = request;
    setLoading(true); setError('');
    try {
      const hosts = await latestHosts(request.signal);
      if (!request.signal.aborted) setSnapshots(hosts);
    } catch {
      if (!request.signal.aborted) setError('تعذر التأكد من أحدث نسخة. البيانات الظاهرة تخص آخر نسخة تم تحميلها؛ حاول التحديث.');
    } finally {
      if (!request.signal.aborted) setLoading(false);
    }
  }, []);
  useEffect(() => {
    void reload();
    const timer = window.setInterval(() => { if (!document.hidden) void reload(); }, 60_000);
    return () => { window.clearInterval(timer); active.current?.abort(); };
  }, [reload]);
  const selected = snapshots.find(s => s.centerId === centerId) ?? snapshots[0];
  return <div className="space-y-4">
    <div className="flex flex-wrap items-end justify-between gap-3">
      <div><h2 className="text-xl font-bold">طلاب السنتر</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">ابحث في أحدث نسخة مرفوعة، وافتح سجل الطالب حسب الشهر والحصة.</p></div>
      <div className="flex flex-wrap items-end gap-3">
        {snapshots.length > 1 && <label className="space-y-1 text-sm"><span className="block">مصدر بيانات السنتر</span><select className="admin-input" value={selected?.centerId ?? ''} onChange={e => setCenterId(e.target.value)}>{snapshots.map(s => <option key={s.centerId} value={s.centerId}>{s.centerId}</option>)}</select></label>}
        <button type="button" className="admin-btn-ghost inline-flex items-center gap-2" disabled={loading} onClick={() => void reload()}><RefreshCw aria-hidden className={`h-4 w-4 ${loading ? 'motion-safe:animate-spin' : ''}`} />تحديث بيانات الطلاب</button>
      </div>
    </div>
    {error && <p role="alert" className="text-sm text-[var(--admin-danger)]">{error}</p>}
    {!selected && <div className="admin-panel p-6" role="status">{loading ? 'جاري تحديد أحدث نسخة بيانات…' : error ? 'لم تكتمل قراءة النسخ.' : 'لا توجد نسخة هوست مرفوعة بعد. ارفع نسخة من «المزامنة والتحديثات» في برنامج السنتر.'}</div>}
    {selected && <DesktopStudentsPanel key={selected.centerId} receipt={selected} onRefresh={() => void reload()} refreshing={loading} />}
  </div>;
}
