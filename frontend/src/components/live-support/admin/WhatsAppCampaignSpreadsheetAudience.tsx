'use client';

import { FileSpreadsheet, LoaderCircle, ShieldCheck, Upload, X } from 'lucide-react';
import { useRef, useState } from 'react';

import {
  getLiveSupportApiError,
  liveSupportService,
  type WhatsAppCampaignSpreadsheetInspection,
  type WhatsAppCampaignSpreadsheetRow,
} from '@/services/live-support-service';

interface WhatsAppCampaignSpreadsheetAudienceProps {
  inspection?: WhatsAppCampaignSpreadsheetInspection;
  phoneColumn: string;
  onChange: (
    inspection: WhatsAppCampaignSpreadsheetInspection | undefined,
    phoneColumn: string,
    rows: WhatsAppCampaignSpreadsheetRow[],
  ) => void;
}

export function WhatsAppCampaignSpreadsheetAudience({
  inspection,
  phoneColumn,
  onChange,
}: WhatsAppCampaignSpreadsheetAudienceProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState('');

  async function upload(file: File) {
    setUploading(true);
    setError('');
    try {
      const nextInspection = await liveSupportService.inspectWhatsAppCampaignSpreadsheet(file);
      onChange(nextInspection, '', []);
    } catch (cause) {
      setError(getLiveSupportApiError(cause, 'تعذر قراءة الشيت. تأكد من صيغة الملف والعناوين.'));
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  function selectPhoneColumn(columnName: string) {
    const rows = inspection?.rows.map((row) => ({
      rowNumber: row.rowNumber,
      phone: row.columns[columnName] ?? '',
      columns: row.columns,
    })) ?? [];
    onChange(inspection, columnName, rows);
  }

  return (
    <section aria-labelledby="spreadsheet-audience-heading" className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 id="spreadsheet-audience-heading" className="font-black text-[var(--admin-text)]">جمهور من Excel أو CSV</h3>
          <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">الصف الأول للعناوين، وكل صف بعده يمثل رسالة واحدة قبل فحص التكرار والرفض.</p>
        </div>
        <button type="button" onClick={() => inputRef.current?.click()} disabled={uploading} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-[var(--admin-primary-contrast)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)] disabled:opacity-60">
          {uploading ? <LoaderCircle aria-hidden="true" size={17} className="animate-spin" /> : <Upload aria-hidden="true" size={17} />}
          {uploading ? 'جارٍ قراءة الشيت…' : inspection ? 'استبدال الشيت' : 'رفع الشيت'}
        </button>
        <input ref={inputRef} type="file" accept=".xlsx,.csv" className="sr-only" onChange={(event) => { const file = event.target.files?.[0]; if (file) void upload(file); }} />
      </div>

      {error ? <p role="alert" className="rounded-xl bg-[var(--admin-danger-10)] p-3 text-sm font-bold text-[var(--admin-danger)]">{error}</p> : null}

      {!inspection ? (
        <button type="button" onClick={() => inputRef.current?.click()} className="grid min-h-44 w-full place-items-center rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 text-center transition-colors hover:border-[var(--admin-accent)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]">
          <span><FileSpreadsheet aria-hidden="true" size={34} className="mx-auto text-[var(--admin-primary)]" /><strong className="mt-3 block text-[var(--admin-text)]">اختر ملف XLSX أو CSV</strong><small className="mt-1 block text-[var(--admin-muted)]">حتى 25,000 صف و100 عمود — بحد أقصى 10MB</small></span>
        </button>
      ) : (
        <div className="rounded-2xl border border-[var(--admin-border)]">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
            <div className="flex min-w-0 items-center gap-3"><FileSpreadsheet aria-hidden="true" size={21} className="shrink-0 text-[var(--admin-success)]" /><div className="min-w-0"><strong className="block truncate text-sm text-[var(--admin-text)]" dir="auto">{inspection.fileName}</strong><span className="text-xs text-[var(--admin-muted)]">{formatNumber(inspection.rows.length)} صف · {formatNumber(inspection.headers.length)} عمود</span></div></div>
            <button type="button" onClick={() => onChange(undefined, '', [])} aria-label="إزالة الشيت" className="grid size-10 place-items-center rounded-lg text-[var(--admin-muted)] hover:bg-[var(--admin-danger-10)] hover:text-[var(--admin-danger)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]"><X aria-hidden="true" size={18} /></button>
          </div>
          <label className="block p-4">
            <span className="mb-1.5 block text-sm font-black text-[var(--admin-text)]">عمود رقم الواتساب</span>
            <select value={phoneColumn} onChange={(event) => selectPhoneColumn(event.target.value)} className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]">
              <option value="">اختر العمود الذي يحتوي أرقام واتساب</option>
              {inspection.headers.map((header) => <option key={header} value={header}>{header}</option>)}
            </select>
          </label>
          {phoneColumn ? <p className="flex items-start gap-2 border-t border-[var(--admin-border)] p-4 text-xs leading-5 text-[var(--admin-muted)]"><ShieldCheck aria-hidden="true" size={16} className="mt-0.5 shrink-0 text-[var(--admin-success)]" />ستُفحص الأرقام وتُحوّل للصيغة الدولية، ولن تظهر كاملة في المعاينة أو السجل.</p> : null}
        </div>
      )}
    </section>
  );
}

function formatNumber(value: number) {
  return new Intl.NumberFormat('ar-EG').format(value);
}
