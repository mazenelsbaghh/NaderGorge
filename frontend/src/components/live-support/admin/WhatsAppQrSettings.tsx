'use client';

import Image from 'next/image';
import { useEffect, useState } from 'react';
import { liveSupportService, getLiveSupportApiError, type SupportWhatsAppAccount, type SupportWhatsAppConnection } from '@/services/live-support-service';

const statusLabels: Record<string, string> = {
  Created: 'جاهز للربط', Connected: 'متصل', Connecting: 'جارٍ الاتصال',
  AwaitingQr: 'في انتظار مسح الرمز', Disconnected: 'غير متصل',
  NumberChanged: 'تم رفض الربط: امسح الرمز من نفس الرقم السابق أو أضف رقمًا جديدًا',
};
const buttonClass = 'min-h-11 rounded-lg border border-[var(--admin-border)] px-3 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] focus-visible:outline-2 disabled:opacity-50';

export function WhatsAppQrSettings() {
  const [accounts, setAccounts] = useState<SupportWhatsAppAccount[]>([]);
  const [name, setName] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [connection, setConnection] = useState<SupportWhatsAppConnection>();
  const [now, setNow] = useState(() => Date.now());
  const [waitingSince, setWaitingSince] = useState(0);

  useEffect(() => {
    let mounted = true;
    void liveSupportService.getWhatsAppAccounts().then(rows => { if (mounted) setAccounts(rows); })
      .catch(cause => { if (mounted) setError(getLiveSupportApiError(cause, 'تعذر إتمام الطلب. حاول مجددًا.')); })
      .finally(() => { if (mounted) setLoading(false); });
    return () => { mounted = false; };
  }, []);

  const selectedId = connection?.account.id;
  useEffect(() => {
    if (!selectedId) return;
    let mounted = true;
    let refreshing = false;
    const refresh = () => {
      setNow(Date.now());
      if (refreshing) return;
      refreshing = true;
      void liveSupportService.getWhatsAppConnection(selectedId).then(updated => {
        if (!mounted) return;
        const account = updated.account;
        setError('');
        setAccounts(rows => rows.map(row => row.id === account.id ? account : row));
        setConnection(account.status === 'Connected' ? undefined : updated);
      }).catch(cause => { if (mounted) setError(getLiveSupportApiError(cause, 'تعذر إتمام الطلب. حاول مجددًا.')); })
        .finally(() => { refreshing = false; });
    };
    refresh();
    const interval = window.setInterval(refresh, 3000);
    return () => { mounted = false; window.clearInterval(interval); };
  }, [selectedId]);

  async function operate(operation: () => Promise<void>) {
    setBusy(true); setError('');
    try { await operation(); }
    catch (cause) { setError(getLiveSupportApiError(cause, 'تعذر إتمام الطلب. حاول مجددًا.')); }
    finally { setBusy(false); }
  }

  const qrValid = connection?.qrExpiresAt && Date.parse(connection.qrExpiresAt) > now;
  return <section className="space-y-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5" aria-labelledby="whatsapp-qr-heading" dir="rtl">
    <div><h2 id="whatsapp-qr-heading" className="text-lg font-bold text-[var(--admin-text)]">أرقام واتساب بالـ QR</h2>
      <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">اربط كل رقم من الأجهزة المرتبطة في واتساب. الرسائل الجديدة تظهر في الدعم والرد يخرج من الرقم الذي استقبلها.</p></div>
    <form className="flex flex-wrap items-end gap-3" onSubmit={event => {
      event.preventDefault();
      void operate(async () => {
        const account = await liveSupportService.createWhatsAppAccount(name.trim());
        setAccounts(rows => [...rows, account]); setName('');
        setConnection(await liveSupportService.connectWhatsAppAccount(account.id)); setNow(Date.now()); setWaitingSince(Date.now());
      });
    }}>
      <label className="min-w-48 flex-1 text-sm font-bold text-[var(--admin-text)]">اسم الرقم
        <input required maxLength={80} value={name} onChange={event => setName(event.target.value)} placeholder="مثال: دعم الطلاب" className="mt-2 min-h-11 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 font-normal" /></label>
      <button disabled={busy || !name.trim()} className={buttonClass}>إضافة رقم واتساب</button>
    </form>
    {loading ? <p role="status">جارٍ تحميل الأرقام…</p> : accounts.length === 0 ? <p className="text-sm text-[var(--admin-muted)]">لم تربط أرقامًا بعد. أضف اسم الرقم لعرض رمز الربط.</p> :
      <ul className="divide-y divide-[var(--admin-border)]">{accounts.map(account => <li key={account.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
        <div><p className="font-bold text-[var(--admin-text)]">{account.name} {account.phoneNumber && <bdi className="mr-2 font-mono text-sm">{account.phoneNumber}</bdi>}</p><p role="status" className="mt-1 text-sm text-[var(--admin-muted)]">{statusLabels[account.status] ?? 'تعذر تحديد حالة الاتصال'}</p></div>
        <div className="flex flex-wrap gap-2">
          {account.status !== 'Connected' && <button type="button" disabled={busy} className={buttonClass} onClick={() => void operate(async () => { setConnection(await liveSupportService.connectWhatsAppAccount(account.id)); setNow(Date.now()); setWaitingSince(Date.now()); })}>عرض رمز الربط</button>}
          <button type="button" disabled={busy} className={buttonClass} onClick={() => void operate(async () => { const updated = await liveSupportService.getWhatsAppConnection(account.id); setAccounts(rows => rows.map(row => row.id === account.id ? updated.account : row)); if (updated.account.status !== 'Connected' && updated.qrDataUrl) { setConnection(updated); setNow(Date.now()); } })}>تحديث الحالة</button>
          {account.status === 'Connected' && <button type="button" disabled={busy} className={buttonClass} onClick={() => void operate(async () => { const updated = await liveSupportService.disconnectWhatsAppAccount(account.id); setAccounts(rows => rows.map(row => row.id === account.id ? updated : row)); setConnection(undefined); })}>فصل الرقم</button>}
        </div>
      </li>)}</ul>}
    {connection && <div className="flex flex-wrap items-center gap-5 rounded-xl bg-[var(--admin-card-strong)] p-4">
      {qrValid && connection.qrDataUrl ? <Image unoptimized src={connection.qrDataUrl} width={240} height={240} alt={`رمز ربط واتساب: ${connection.account.name}`} className="max-w-full rounded-lg bg-white p-2" /> :
        <div role="status" className="space-y-2 text-sm text-[var(--admin-text)]">
          <p>{connection.account.status === 'Disconnected' || connection.account.status === 'NumberChanged'
            ? 'تعذر إنشاء رمز الربط. أعد محاولة الاتصال.'
            : waitingSince && now - waitingSince > 60_000
              ? 'تأخر إنشاء رمز الربط. أعد المحاولة، وإذا استمر التعثر راجع اتصال خدمة واتساب.'
              : 'جارٍ تجهيز رمز ربط جديد تلقائيًا…'}</p>
          {(connection.account.status === 'Disconnected' || connection.account.status === 'NumberChanged' || (waitingSince && now - waitingSince > 60_000)) &&
            <button type="button" disabled={busy} className={buttonClass} onClick={() => void operate(async () => {
              setConnection(await liveSupportService.connectWhatsAppAccount(connection.account.id));
              setNow(Date.now()); setWaitingSince(Date.now());
            })}>إعادة محاولة الربط</button>}
        </div>}
      <div className="min-w-48 flex-1 text-sm leading-7 text-[var(--admin-text)]"><p className="font-bold">من موبايل رقم {connection.account.name}:</p><ol className="list-inside list-decimal"><li>افتح واتساب ثم الأجهزة المرتبطة.</li><li>اختر ربط جهاز وامسح الرمز.</li><li>انتظر ظهور حالة «متصل» هنا.</li></ol><button type="button" className="mt-2 underline" onClick={() => setConnection(undefined)}>إخفاء رمز الربط</button></div>
    </div>}
    {error && <p role="alert" className="text-sm text-[var(--admin-danger)]">{error}</p>}
  </section>;
}
