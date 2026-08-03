'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Clipboard, Download, RefreshCw, Search, Server, Trash2, Workflow } from 'lucide-react';
import toast from 'react-hot-toast';

import { AdminPage } from '@/components/admin';
import { deleteSystemLogs, getSystemLogs, type SystemLogEntry } from '@/services/system-logs-service';

export default function AdminSystemLogsPageClient() {
  const [logs, setLogs] = useState<SystemLogEntry[]>([]);
  const [source, setSource] = useState('');
  const [level, setLevel] = useState('');
  const [search, setSearch] = useState('');
  const [period, setPeriod] = useState('today');
  const [from, setFrom] = useState(() => startOfTodayInput());
  const [to, setTo] = useState(() => endOfTodayInput());
  const [loading, setLoading] = useState(true);

  const loadLogs = useCallback(async () => {
    setLoading(true);
    try {
      setLogs(await getSystemLogs({ ...filterParams({ source, level, search, from, to }), limit: 200 }));
    } catch {
      toast.error('تعذر تحميل سجل النظام');
    } finally {
      setLoading(false);
    }
  }, [from, level, search, source, to]);

  useEffect(() => { void loadLogs(); }, [loadLogs]);
  useEffect(() => {
    const timer = window.setInterval(() => void loadLogs(), 30_000);
    return () => window.clearInterval(timer);
  }, [loadLogs]);

  const counts = useMemo(() => ({
    errors: logs.filter((log) => log.level === 'error' || log.level === 'critical').length,
    warnings: logs.filter((log) => log.level === 'warning').length,
  }), [logs]);

  const exportText = useMemo(() => logs.map(formatLog).join('\n\n────────────────────────\n\n'), [logs]);

  const changePeriod = (nextPeriod: string) => {
    setPeriod(nextPeriod);
    if (nextPeriod === 'today') { setFrom(startOfTodayInput()); setTo(endOfTodayInput()); }
    if (nextPeriod === '7days') { setFrom(daysAgoInput(7)); setTo(endOfTodayInput()); }
  };

  const clearVisibleLogs = async () => {
    if (!logs.length || !window.confirm(`سيتم مسح ${logs.length} سجل مطابق للفترة والفلاتر الحالية. هل أنت متأكد؟`)) return;
    const deletedCount = await deleteSystemLogs(logs.map((log) => log.id));
    toast.success(`تم مسح ${deletedCount} سجل`);
    await loadLogs();
  };

  return (
    <AdminPage
      activePath="/admin/system-logs"
      sectionLabel="المراقبة الفنية"
      pageTitle="سجل النظام"
      subtitle="أخطاء وتحذيرات الـ Backend والـ Worker — تحديث تلقائي كل 30 ثانية"
      action={(
        <button className="admin-btn-ghost flex items-center gap-2" onClick={() => void loadLogs()} disabled={loading}>
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} /> تحديث
        </button>
      )}
    >
      <div className="space-y-4">
        <div className="admin-panel flex flex-wrap items-center justify-between gap-4 px-5 py-4">
          <div className="flex items-center gap-3">
            <Server className="h-5 w-5 text-[var(--admin-primary)]" />
            <div><strong>{logs.length}</strong> <span className="text-sm text-[var(--admin-text-muted)]">سجل ظاهر</span></div>
          </div>
          <div className="flex items-center gap-5 text-sm">
            <span className="flex items-center gap-2"><i className="h-2.5 w-2.5 rounded-full bg-red-500" />{counts.errors} أخطاء</span>
            <span className="flex items-center gap-2"><i className="h-2.5 w-2.5 rounded-full bg-amber-500" />{counts.warnings} تحذيرات</span>
          </div>
        </div>

        <div className="admin-panel grid gap-3 p-4 md:grid-cols-[1fr_180px_180px]">
          <label className="relative">
            <Search className="pointer-events-none absolute right-3 top-3 h-4 w-4 text-[var(--admin-text-muted)]" />
            <input className="admin-input w-full pr-10" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="ابحث في الرسالة أو اسم الخدمة..." />
          </label>
          <select className="admin-input" value={source} onChange={(event) => setSource(event.target.value)}>
            <option value="">كل المصادر</option><option value="backend">Backend</option><option value="worker">Worker</option>
          </select>
          <select className="admin-input" value={level} onChange={(event) => setLevel(event.target.value)}>
            <option value="">كل المستويات</option><option value="error">Error</option><option value="critical">Critical</option><option value="warning">Warning</option>
          </select>
        </div>

        <div className="admin-panel flex flex-wrap items-end gap-3 p-4">
          <label className="min-w-40 text-sm"><span className="mb-1 block text-[var(--admin-text-muted)]">الفترة</span><select className="admin-input w-full" value={period} onChange={(event) => changePeriod(event.target.value)}><option value="today">اليوم</option><option value="7days">آخر 7 أيام</option><option value="custom">فترة مخصصة</option></select></label>
          <label className="text-sm"><span className="mb-1 block text-[var(--admin-text-muted)]">من</span><input type="datetime-local" className="admin-input" value={from} onChange={(event) => { setPeriod('custom'); setFrom(event.target.value); }} /></label>
          <label className="text-sm"><span className="mb-1 block text-[var(--admin-text-muted)]">إلى</span><input type="datetime-local" className="admin-input" value={to} onChange={(event) => { setPeriod('custom'); setTo(event.target.value); }} /></label>
          <div className="mr-auto flex flex-wrap gap-2">
            <button className="admin-btn-ghost flex items-center gap-2" disabled={!logs.length} onClick={async () => { await navigator.clipboard.writeText(exportText); toast.success('تم نسخ السجلات'); }}><Clipboard className="h-4 w-4" />نسخ الكل</button>
            <button className="admin-btn-ghost flex items-center gap-2" disabled={!logs.length} onClick={() => downloadLogs(exportText)}><Download className="h-4 w-4" />تنزيل TXT</button>
            <button className="admin-btn-ghost flex items-center gap-2 text-red-500" disabled={!logs.length} onClick={() => void clearVisibleLogs()}><Trash2 className="h-4 w-4" />مسح الظاهر</button>
          </div>
        </div>

        <div className="space-y-3">
          {!loading && logs.length === 0 && <div className="admin-panel p-10 text-center text-[var(--admin-text-muted)]"><AlertTriangle className="mx-auto mb-3 h-6 w-6" />لا توجد سجلات مطابقة حاليًا. جرّب تغيير الفلاتر أو انتظر التحديث التالي.</div>}
          {logs.map((log) => <LogCard key={log.id} log={log} />)}
        </div>
      </div>
    </AdminPage>
  );
}

function filterParams(filters: { source: string; level: string; search: string; from: string; to: string }) {
  return { source: filters.source || undefined, level: filters.level || undefined, search: filters.search.trim() || undefined, from: filters.from ? new Date(filters.from).toISOString() : undefined, to: filters.to ? new Date(filters.to).toISOString() : undefined };
}

function formatLog(log: SystemLogEntry) {
  return `[${log.timestamp}] [${log.source}] [${log.level}] ${log.category}\n${log.message}${log.exception ? `\n\n${log.exception}` : ''}`;
}

function downloadLogs(content: string) {
  const url = URL.createObjectURL(new Blob([content], { type: 'text/plain;charset=utf-8' }));
  const link = document.createElement('a'); link.href = url; link.download = `system-logs-${new Date().toISOString().slice(0, 10)}.txt`; link.click();
  URL.revokeObjectURL(url);
}

function localDateTimeInput(date: Date) {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

function startOfTodayInput() { const date = new Date(); date.setHours(0, 0, 0, 0); return localDateTimeInput(date); }
function endOfTodayInput() { const date = new Date(); date.setHours(23, 59, 59, 999); return localDateTimeInput(date); }
function daysAgoInput(days: number) { const date = new Date(); date.setDate(date.getDate() - days + 1); date.setHours(0, 0, 0, 0); return localDateTimeInput(date); }

function LogCard({ log }: { log: SystemLogEntry }) {
  const fullText = formatLog(log);
  const isError = log.level === 'error' || log.level === 'critical';
  const SourceIcon = log.source === 'worker' ? Workflow : Server;

  return (
    <article className="admin-panel p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className={`rounded-full px-2.5 py-1 font-semibold ${isError ? 'bg-red-500/10 text-red-500' : 'bg-amber-500/10 text-amber-500'}`}>{log.level.toUpperCase()}</span>
          <span className="flex items-center gap-1 text-[var(--admin-text-muted)]"><SourceIcon className="h-4 w-4" />{log.source}</span>
          <code className="rounded bg-black/5 px-2 py-1 dark:bg-white/5">{log.category}</code>
        </div>
        <div className="flex items-center gap-3 text-xs text-[var(--admin-text-muted)]">
          <time>{new Date(log.timestamp).toLocaleString('ar-EG')}</time>
          <button className="admin-btn-icon" title="نسخ الخطأ كاملًا" onClick={async () => { await navigator.clipboard.writeText(fullText); toast.success('تم نسخ السجل'); }}><Clipboard className="h-4 w-4" /></button>
        </div>
      </div>
      <p className="mt-3 whitespace-pre-wrap break-words font-mono text-sm leading-6">{log.message}</p>
      {log.exception && <details className="mt-3"><summary className="cursor-pointer text-sm font-semibold text-red-500">عرض التفاصيل الفنية</summary><pre dir="ltr" className="mt-2 max-h-80 overflow-auto whitespace-pre-wrap rounded-xl bg-black/90 p-4 text-xs text-red-100">{log.exception}</pre></details>}
    </article>
  );
}
