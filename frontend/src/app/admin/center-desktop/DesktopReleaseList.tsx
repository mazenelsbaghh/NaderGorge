'use client';

import { useEffect, useState } from 'react';
import { AlertTriangle, PackageCheck } from 'lucide-react';
import { getDesktopReleases, type DesktopRelease } from '@/services/center-desktop-service';
import { osLabel, roleLabel, sizeLabel } from './display';

export default function DesktopReleaseList({ refreshKey }: { refreshKey: number }) {
  const [result, setResult] = useState<{ releases: DesktopRelease[]; error: string; key: number }>({ releases: [], error: '', key: -1 });
  const loading = result.key !== refreshKey;
  const { releases, error } = result;
  useEffect(() => {
    const request = new AbortController();
    void getDesktopReleases(request.signal).then(releases => {
      if (!request.signal.aborted) setResult({ releases, error: '', key: refreshKey });
    }).catch(() => {
      if (!request.signal.aborted) setResult(previous => ({ ...previous, key: refreshKey, error: 'تعذر التحقق من إصدارات البرنامج. حاول التحديث مرة أخرى.' }));
    });
    return () => request.abort();
  }, [refreshKey]);
  return <section aria-label="إصدارات البرنامج" className="admin-panel space-y-5 p-4 md:p-6" aria-busy={loading}>
    <div><h2 className="text-lg font-bold">إصدارات البرنامج</h2><p className="mt-2 text-sm text-[var(--admin-muted)]">كل جهاز ينزّل الإصدار المناسب لنظامه ودوره. ظهور إصدار هنا لا يعني أنه اتثبّت على الأجهزة.</p></div>
    {error && <p role="alert" className="text-[var(--admin-danger)]">{error}</p>}
    {!releases.length && !error && <p role="status">{loading ? 'جاري فحص الإصدارات…' : 'لا توجد إصدارات معروضة من الخدمة.'}</p>}
    <div className="divide-y divide-[var(--admin-border)]">
      {releases.map(release => <article key={`${release.platform}-${release.role}`} className="py-5 first:pt-0 last:pb-0">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h3 className="font-semibold">{osLabel(release.platform)} · {roleLabel(release.role)}</h3>
          <span className={`inline-flex items-center gap-2 text-sm ${release.status === 'available' ? 'text-[var(--admin-success)]' : release.status === 'invalid' ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-muted)]'}`}>
            {release.status === 'available' ? <PackageCheck className="h-4 w-4" /> : <AlertTriangle className="h-4 w-4" />}
            {release.status === 'available' ? 'متاح للتنزيل من البرنامج' : release.status === 'missing' ? 'لم تُنشر نسخة بعد' : 'ملف الإصدار يحتاج مراجعة'}
          </span>
        </div>
        {release.manifest && <div className="mt-3 space-y-2 text-sm"><p>الإصدار: <bdi className="font-semibold">{release.manifest.version}</bdi> · <bdi>{sizeLabel(release.manifest.size)}</bdi></p><p className="whitespace-pre-wrap break-words text-[var(--admin-muted)]">{release.manifest.notes || 'لا توجد ملاحظات لهذا الإصدار.'}</p><details className="text-xs text-[var(--admin-muted)]"><summary className="cursor-pointer py-1">بيانات التحقق</summary><p className="mt-2 break-all">البناء: <bdi>{release.manifest.build}</bdi><br />SHA-256: <bdi>{release.manifest.sha256}</bdi></p></details></div>}
      </article>)}
    </div>
  </section>;
}
