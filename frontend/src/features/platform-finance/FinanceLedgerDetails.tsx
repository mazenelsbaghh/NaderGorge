'use client';

import { useEffect, useState } from 'react';
import platformFinanceService, { type FinanceAccountBalance, type FinanceJournal } from '@/services/platform-finance-service';
import { formatCairoDateTime } from '@/lib/cairo-time';
import { type FinancePeriod } from './FinancePeriodPicker';
import { financeMoney as money } from './FinanceProfitSummary';

export default function FinanceLedgerDetails({ period, accounts }: { period: FinancePeriod; accounts: FinanceAccountBalance[] }) {
  const [ledger, setLedger] = useState<FinanceJournal[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(false);
    void platformFinanceService.getLedger(period.from, period.to)
      .then(rows => { if (active) setLedger(rows); })
      .catch(() => { if (active) setError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [period.from, period.to, attempt]);

  return <div className="space-y-6 pb-5">
    <section aria-label="أرصدة الحسابات التفصيلية">
      <h3 className="mb-3 font-bold">أرصدة الحسابات التفصيلية</h3>
      <dl className="divide-y divide-[var(--admin-border)] text-sm">{accounts.map(account => <div key={account.accountId} className="flex flex-wrap justify-between gap-2 py-3"><dt>{account.code} · {account.name}</dt><dd className="font-bold tabular-nums">{money(account.balance)}</dd></div>)}</dl>
    </section>
    <section aria-label="القيود المحاسبية">
      <h3 className="font-bold">آخر ٥٠ حركة في الفترة</h3>
      {loading ? <p role="status" className="py-4 text-sm">جارٍ تحميل الحركات...</p> : error ? <div role="alert" className="py-4 text-sm">تعذر تحميل الحركات.<button type="button" className="admin-btn-ghost ms-3 min-h-11 px-3" onClick={() => setAttempt(value => value + 1)}>إعادة المحاولة</button></div> : ledger.length === 0 ? <p className="py-4 text-sm text-[var(--admin-muted)]">مفيش حركات مسجّلة في الفترة دي.</p> : ledger.map(entry => <details key={entry.id} className="border-b border-[var(--admin-border)] py-3 text-sm">
        <summary className="cursor-pointer leading-7">#{entry.sequenceNumber} · {entry.description} · {formatCairoDateTime(entry.occurredAt, { dateStyle: 'short' })}</summary>
        <dl className="space-y-3 pt-3">{entry.lines.map(line => <div key={line.id} className="flex flex-wrap justify-between gap-2"><dt>{line.accountCode} · {line.accountName}</dt><dd className="font-bold tabular-nums">مدين {money(line.debit)} · دائن {money(line.credit)}</dd></div>)}</dl>
      </details>)}
    </section>
  </div>;
}
