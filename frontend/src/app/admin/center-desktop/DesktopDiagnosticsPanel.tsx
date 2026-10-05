'use client';

import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, Download, RefreshCw, X } from 'lucide-react';
import {
  downloadDesktopUpload, getDesktopDiagnostics,
  type DesktopDiagnostics, type DesktopReceipt,
} from '@/services/center-desktop-service';
import { operationLabel, osLabel, roleLabel, saveBlob, sizeLabel, timestamp } from './display';

export default function DesktopDiagnosticsPanel({ receipt, onClose }: { receipt: DesktopReceipt; onClose: () => void }) {
  const [detail, setDetail] = useState<DesktopDiagnostics | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState('');
  const [errorsOnly, setErrorsOnly] = useState(true);
  const downloadRequest = useRef<AbortController | null>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    titleRef.current?.focus();
    return () => { downloadRequest.current?.abort(); };
  }, []);
  useEffect(() => {
    const request = new AbortController();
    void getDesktopDiagnostics(receipt.uploadId, request.signal).then(value => {
      if (!request.signal.aborted) { setDetail(value); setLoading(false); }
    }).catch(() => {
      if (!request.signal.aborted) {
        setError('تعذر تحميل تفاصيل هذه النسخة أو التحقق من سلامتها. حاول مرة أخرى.');
        setLoading(false);
      }
    });
    return () => request.abort();
  }, [receipt.uploadId, retry]);

  const download = async () => {
    if (downloadRequest.current) return;
    const request = new AbortController();
    downloadRequest.current = request;
    setDownloading(true); setDownloadError('');
    try {
      const blob = await downloadDesktopUpload(receipt.uploadId, request.signal);
      if (!request.signal.aborted) saveBlob(blob, `massar-upload-${receipt.uploadId}.json`);
    } catch {
      if (!request.signal.aborted) setDownloadError('تعذر تنزيل النسخة. لم يُنزّل ملف ناقص؛ حاول مرة أخرى.');
    } finally {
      if (downloadRequest.current === request) downloadRequest.current = null;
      if (!request.signal.aborted) setDownloading(false);
    }
  };
  const events = detail?.events ?? [];
  const isWarning = (event: DesktopDiagnostics['events'][number]) => event.kind === 'error' && event.operation === 'ui.warning';
  const warningCount = events.filter(isWarning).length;
  const errorCount = events.filter(event => event.kind === 'error' && !isWarning(event)).length;
  const visible = events.filter(event => !errorsOnly || (event.kind === 'error' && !isWarning(event)));
  return (
    <section className="admin-panel space-y-5 p-4 md:p-6" aria-labelledby="desktop-detail-title" aria-busy={loading}>
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="mb-1 text-sm text-[var(--admin-muted)]">تفاصيل الرفع</p>
          <h2 id="desktop-detail-title" ref={titleRef} tabIndex={-1} className="break-all text-lg font-bold outline-none">{receipt.centerId} · {roleLabel(receipt.app.role)}</h2>
        </div>
        <button className="admin-btn-ghost" onClick={onClose} aria-label="إغلاق تفاصيل الرفع"><X className="h-5 w-5" /></button>
      </div>
      <dl className="grid grid-cols-2 gap-x-6 gap-y-4 border-y border-[var(--admin-border)] py-4 text-sm xl:grid-cols-4">
        <div><dt className="text-[var(--admin-muted)]">إصدار البرنامج وقت الرفع</dt><dd className="mt-1 font-semibold"><bdi>{receipt.app.version}</bdi></dd></div>
        <div><dt className="text-[var(--admin-muted)]">النظام</dt><dd className="mt-1">{osLabel(receipt.app.os)}</dd></div>
        <div><dt className="text-[var(--admin-muted)]">وقت الاستلام · القاهرة</dt><dd className="mt-1">{timestamp(receipt.receivedAt)}</dd></div>
        <div><dt className="text-[var(--admin-muted)]">الحجم</dt><dd className="mt-1"><bdi>{sizeLabel(receipt.size)}</bdi></dd></div>
      </dl>
      {loading && <p role="status">جاري تحميل سجل المشاكل…</p>}
      {error && <div role="alert" className="flex flex-wrap items-center gap-3 text-[var(--admin-danger)]"><AlertTriangle className="h-5 w-5" /><p>{error}</p><button className="admin-btn-ghost" onClick={() => { setError(''); setLoading(true); setRetry(value => value + 1); }}><RefreshCw className="me-2 inline h-4 w-4" />إعادة المحاولة</button></div>}
      {detail && !loading && !error && <>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h3 className="font-bold">سجل المشاكل <span className="font-normal text-[var(--admin-muted)]">· {errorCount} خطأ مسجل · {warningCount} تنبيه · {visible.length} حدث ظاهر</span></h3>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={errorsOnly} onChange={event => setErrorsOnly(event.target.checked)} />الأخطاء فقط</label>
        </div>
        <p className="text-sm text-[var(--admin-muted)]">دي أحداث تاريخية وقت رفع النسخة، وليست عدد المشاكل المفتوحة الآن. تنبيهات الواجهة منفصلة ويمكن عرضها بإلغاء «الأخطاء فقط». السجل القديم لا يتغير بعد تحديث البرنامج.</p>
        {detail.truncated && <p role="status" className="text-sm text-[var(--admin-warning)]">معروض آخر {detail.events.length} حدث من {detail.total}. النسخة الكاملة تحتوي على باقي السجل المحفوظ.</p>}
        {visible.length === 0 ? <p className="rounded-xl bg-[var(--admin-card-soft)] p-5">{errorsOnly ? 'لا توجد أخطاء مسجلة في الأحداث المعروضة لهذه النسخة.' : 'لا توجد أحداث تشخيصية محفوظة في هذه النسخة.'}</p> : <div className="max-h-[36rem] divide-y divide-[var(--admin-border)] overflow-y-auto rounded-xl border border-[var(--admin-border)]">
          {visible.map((event, index) => <article key={`${event.id}-${index}`} className="space-y-2 p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <strong className={isWarning(event) ? 'text-[var(--admin-warning)]' : event.kind === 'error' ? 'text-[var(--admin-danger)]' : 'text-[var(--admin-primary)]'}>{isWarning(event) ? 'تنبيه' : event.kind === 'error' ? 'خطأ' : 'بدء تشغيل'} · {operationLabel(event.operation)}</strong>
              <time className="text-xs text-[var(--admin-muted)]" dateTime={event.time}>{timestamp(event.time)}</time>
            </div>
            <div className="flex flex-wrap gap-3 text-sm"><span>الإصدار: <bdi>{event.version}</bdi></span><span>{osLabel(event.platform)}{event.role ? ` · ${roleLabel(event.role)}` : ''}</span></div>
            {event.errors?.map((problem, item) => <p key={item} className="break-words text-sm"><bdi>{problem.type}</bdi>{problem.code != null ? ` · كود الخطأ: ${problem.code}` : ''}</p>)}
            {event.kind === 'error' && !isWarning(event) && !event.frames?.length && !event.errors?.some(problem => problem.code != null) && <p className="text-sm text-[var(--admin-muted)]">هذا السجل القديم لا يحتوي على سبب أو موضع الخطأ؛ لا يمكن تأكيد إصلاحه من هذا التسجيل وحده. راجع رفعة أحدث بعد تكرار المشكلة.</p>}
            <details className="text-sm"><summary className="cursor-pointer py-1 text-[var(--admin-primary)]">تفاصيل فنية للدعم</summary><div className="mt-2 space-y-1 break-all text-[var(--admin-muted)]"><p>العملية: <bdi>{event.operation}</bdi></p><p>معرّف الحدث: <bdi>{event.id}</bdi></p>{event.frames?.map((frame, item) => <p key={item} dir="ltr" className="text-start">{frame.file}:{frame.line}:{frame.column}</p>)}</div></details>
          </article>)}
        </div>}
        <div className="flex flex-wrap items-start gap-3 border-t border-[var(--admin-border)] pt-4">
          <button className="admin-btn-ghost inline-flex items-center gap-2" disabled={!visible.length} onClick={() => saveBlob(new Blob([JSON.stringify({ receipt: detail.receipt, total: detail.total, truncated: detail.truncated, events: visible }, null, 2)], { type: 'application/json' }), `massar-diagnostics-${receipt.uploadId}.json`)}><Download className="h-4 w-4" />تنزيل السجل المعروض</button>
          <div className="space-y-2">
            <button className="admin-btn-ghost inline-flex items-center gap-2" disabled={downloading} onClick={() => void download()}><Download className="h-4 w-4" />{downloading ? 'جاري التنزيل…' : 'تنزيل النسخة'}</button>
            <p className="text-xs text-[var(--admin-muted)]">{detail.kind === 'database' ? 'تشمل بيانات الطلاب والحسابات؛ متاحة للإدارة فقط.' : 'تحتوي على التشخيصات فقط، بدون قاعدة بيانات.'}</p>
            {downloadError && <p role="alert" className="text-sm text-[var(--admin-danger)]">{downloadError}</p>}
          </div>
        </div>
      </>}
      <details className="text-xs text-[var(--admin-muted)]"><summary className="cursor-pointer py-2">معلومات النسخة والتحقق</summary><dl className="mt-2 space-y-2 break-all"><div><dt>معرّف الرفع</dt><dd><bdi>{receipt.uploadId}</bdi></dd></div><div><dt>وقت تجهيز النسخة</dt><dd>{timestamp(receipt.createdAt)}</dd></div><div><dt>بصمة البناء</dt><dd><bdi>{receipt.app.build}</bdi></dd></div><div><dt>بصمة ملف النسخة</dt><dd><bdi>{receipt.bundleSha256}</bdi></dd></div></dl></details>
    </section>
  );
}
