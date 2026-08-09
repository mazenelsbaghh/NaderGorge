'use client';

import Link from 'next/link';
import { FormEvent, useCallback, useEffect, useState } from 'react';
import { CheckCircle2, Search, XCircle } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import { walletService, type AdminIncomingSmsLogDto, type WalletDto } from '@/services/wallet-service';

type MatchFilter = 'all' | 'matched' | 'unmatched';

export function WalletMessagesWorkspace({ rechargePath = '/admin/recharge-verification' }: { rechargePath?: string }) {
  const [messages, setMessages] = useState<AdminIncomingSmsLogDto[]>([]);
  const [wallets, setWallets] = useState<WalletDto[]>([]);
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [matchFilter, setMatchFilter] = useState<MatchFilter>('all');
  const [walletId, setWalletId] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const response = await walletService.getSmsLogs({
        search: submittedSearch || undefined,
        isMatched: matchFilter === 'all' ? undefined : matchFilter === 'matched',
        walletId: walletId || undefined,
        page,
        pageSize: 50,
      });
      setMessages(response.items);
      setTotalCount(response.totalCount);
    } catch {
      setError('تعذر تحميل رسائل المحافظ.');
    } finally {
      setLoading(false);
    }
  }, [matchFilter, page, submittedSearch, walletId]);

  useEffect(() => { void walletService.getWallets().then(setWallets); }, []);
  useEffect(() => { void load(); }, [load]);

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    setPage(1);
    setSubmittedSearch(search.trim());
  }

  const pageCount = Math.max(1, Math.ceil(totalCount / 50));

  return <div className="space-y-5" dir="rtl">
      <section className="admin-panel rounded-2xl p-5">
        <form onSubmit={submitSearch} className="grid gap-3 lg:grid-cols-[minmax(280px,1fr)_220px_220px_auto]">
          <div className="relative"><Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" /><input className="admin-input ps-10" value={search} onChange={event => setSearch(event.target.value)} placeholder="ابحث برقم المحول أو نص الرسالة أو رقم المحفظة" /></div>
          <select className="admin-input" value={walletId} onChange={event => { setWalletId(event.target.value); setPage(1); }}><option value="">كل المحافظ</option>{wallets.map(wallet => <option key={wallet.id} value={wallet.id}>{wallet.label} — {wallet.phoneNumber}</option>)}</select>
          <select className="admin-input" value={matchFilter} onChange={event => { setMatchFilter(event.target.value as MatchFilter); setPage(1); }}><option value="all">الكل: مطابق وغير مطابق</option><option value="matched">المطابقة فقط</option><option value="unmatched">غير المطابقة فقط</option></select>
          <button className="admin-btn-primary" type="submit">بحث</button>
        </form>
      </section>

      <section className="admin-panel rounded-2xl p-5">
        <div className="mb-4 flex items-center justify-between"><h2 className="text-lg font-black">الرسائل ({totalCount})</h2><button className="admin-btn-ghost" type="button" onClick={() => void load()}>تحديث</button></div>
        {error ? <p role="alert" className="mb-4 text-rose-600">{error}</p> : null}
        <div className="overflow-x-auto"><table className="w-full min-w-[1100px] text-sm"><thead><tr className="text-right"><th>التاريخ</th><th>المحفظة</th><th>رقم المحول</th><th>المبلغ</th><th>رقم العملية</th><th>الرسالة</th><th>المطابقة</th><th>الطالب المرتبط</th></tr></thead><tbody>{messages.map(message => <tr key={message.id} className="border-t border-[var(--admin-border)] align-top"><td className="whitespace-nowrap">{new Date(message.receivedAt).toLocaleString('ar-EG', { dateStyle: 'medium', timeStyle: 'short' })}</td><td><b>{message.walletLabel}</b><bdi className="block font-mono text-xs text-[var(--admin-muted)]">{message.walletPhoneNumber}</bdi></td><td><bdi className="font-mono font-bold">{message.parsedSenderPhone || '—'}</bdi></td><td className="whitespace-nowrap font-mono font-bold">{message.parsedAmount != null ? `${message.parsedAmount} ج.م` : '—'}</td><td><bdi className="font-mono text-xs">{message.transferReference || '—'}</bdi></td><td><p className="max-w-xl whitespace-pre-wrap leading-6">{message.body}</p></td><td>{message.isMatched ? <span className="inline-flex items-center gap-1 font-bold text-emerald-600"><CheckCircle2 className="h-4 w-4" /> مطابقة</span> : <span className="inline-flex items-center gap-1 font-bold text-rose-600"><XCircle className="h-4 w-4" /> غير مطابقة</span>}</td><td>{message.matchedRechargeRequestId ? <div>{message.matchedStudentName ? <Link className="font-bold text-[var(--admin-primary)] hover:underline" href={`${rechargePath}?search=${encodeURIComponent(message.matchedStudentPhoneNumber || '')}`}>{message.matchedStudentName}</Link> : 'طلب شحن مرتبط'}<bdi className="block font-mono text-xs text-[var(--admin-muted)]">{message.matchedStudentPhoneNumber}</bdi></div> : '—'}</td></tr>)}</tbody></table>{!loading && messages.length === 0 ? <p className="py-10 text-center text-[var(--admin-muted)]">لا توجد رسائل بهذه المواصفات.</p> : null}{loading ? <p className="py-10 text-center text-[var(--admin-muted)]">جارٍ تحميل الرسائل…</p> : null}</div>
        <div className="mt-4 flex items-center justify-between"><button className="admin-btn-ghost" type="button" disabled={page <= 1} onClick={() => setPage(current => current - 1)}>السابق</button><span className="text-sm font-bold">صفحة {page} من {pageCount}</span><button className="admin-btn-ghost" type="button" disabled={page >= pageCount} onClick={() => setPage(current => current + 1)}>التالي</button></div>
      </section>
    </div>;
}

export default function WalletMessagesPageClient() {
  return <AdminPage activePath="/admin/wallet-messages" sectionLabel="المالية" pageTitle="رسائل المحافظ" subtitle="كل رسائل المحافظ مع البحث وحالة المطابقة بطلبات الشحن.">
    <WalletMessagesWorkspace />
  </AdminPage>;
}
