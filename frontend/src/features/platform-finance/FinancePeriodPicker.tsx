'use client';

import { useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { cairoCurrentDate, cairoCurrentMonthPeriod } from '@/lib/cairo-time';

export type FinancePeriod = { from: string; to: string };
const financeDateLabel = (date: string) => date.split('-').reverse().join('/');

export default function FinancePeriodPicker({ period, onChange, loading, earliestDate }: {
  period: FinancePeriod;
  onChange: (period: FinancePeriod) => void;
  loading: boolean;
  earliestDate?: string;
}) {
  const [custom, setCustom] = useState(false);
  const [from, setFrom] = useState(period.from);
  const [to, setTo] = useState(period.to);
  const today = cairoCurrentDate();
  const presets = [
    { label: 'النهارده', from: today, to: today },
    { label: 'الشهر ده', from: cairoCurrentMonthPeriod().first, to: today },
    ...(earliestDate ? [{ label: 'من البداية', from: earliestDate, to: today }] : []),
  ];
  const invalid = !from || !to || from > to;

  return <section aria-label="فترة الحسابات" className="space-y-3">
    <div className="flex flex-wrap items-center gap-2">
      {presets.map(preset => <button key={preset.label} type="button"
        aria-pressed={!custom && period.from === preset.from && period.to === preset.to}
        disabled={loading}
        className="min-h-11 rounded-lg px-4 text-sm font-bold hover:bg-[var(--admin-hover)] aria-pressed:bg-[var(--admin-primary)] aria-pressed:text-[var(--admin-primary-contrast)] disabled:opacity-50"
        onClick={() => { setCustom(false); onChange({ from: preset.from, to: preset.to }); }}>
        {preset.label}
      </button>)}
      <button type="button" aria-expanded={custom} aria-controls="finance-custom-period"
        className="min-h-11 rounded-lg px-4 text-sm font-bold hover:bg-[var(--admin-hover)]"
        onClick={() => { setFrom(period.from); setTo(period.to); setCustom(!custom); }}>فترة تانية</button>
      <button type="button" disabled={loading} onClick={() => onChange({ ...period })}
        className="ms-auto inline-flex min-h-11 items-center gap-2 rounded-lg px-3 text-sm hover:bg-[var(--admin-hover)] disabled:opacity-50">
        <RefreshCw size={16} aria-hidden="true" />تحديث
      </button>
    </div>
    {custom && <form id="finance-custom-period" onSubmit={event => { event.preventDefault(); if (!invalid) onChange({ from, to }); }}
      className="flex flex-wrap items-end gap-3 rounded-xl bg-[var(--admin-card)] p-4">
      <label className="min-w-0 flex-1 text-sm font-bold sm:flex-none">من<input className="admin-input mt-2 block min-h-11 w-full" type="date" value={from} onChange={event => setFrom(event.target.value)} required /></label>
      <label className="min-w-0 flex-1 text-sm font-bold sm:flex-none">إلى<input className="admin-input mt-2 block min-h-11 w-full" type="date" value={to} onChange={event => setTo(event.target.value)} required /></label>
      <button className="admin-btn-primary min-h-11 px-4" type="submit" disabled={loading || invalid}>عرض الحسابات</button>
      {invalid && <p role="alert" className="w-full text-sm text-[var(--admin-danger)]">اختار تاريخ بداية قبل تاريخ النهاية.</p>}
    </form>}
    <p className="text-sm text-[var(--admin-muted)]">من {financeDateLabel(period.from)} إلى {financeDateLabel(period.to)}</p>
  </section>;
}
