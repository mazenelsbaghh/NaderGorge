'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Download, RefreshCw, RotateCcw } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import { walletService, type RechargeShiftReviewDto, type RechargeShiftReviewItemDto, type WalletDto } from '@/services/wallet-service';

function localInput(date: Date) {
  const shifted = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return shifted.toISOString().slice(0, 16);
}

function escapeXml(value: unknown) {
  return String(value ?? '').replace(/[<>&"']/g, character => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', '"': '&quot;', "'": '&apos;' })[character] ?? character);
}

export function RechargeShiftReviewWorkspace() {
  const initial = useMemo(() => {
    const end = new Date();
    const start = new Date(end); start.setHours(0, 0, 0, 0);
    return { from: localInput(start), to: localInput(end) };
  }, []);
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [walletId, setWalletId] = useState('');
  const [wallets, setWallets] = useState<WalletDto[]>([]);
  const [report, setReport] = useState<RechargeShiftReviewDto>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selected, setSelected] = useState<RechargeShiftReviewItemDto>();
  const [reason, setReason] = useState('');
  const [reversing, setReversing] = useState(false);

  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      setReport(await walletService.getRechargeShiftReview({ from: new Date(from).toISOString(), to: new Date(to).toISOString(), walletId: walletId || undefined }));
    } catch { setError('تعذر تحميل مراجعة الشيفت. راجع الفترة وحاول مرة أخرى.'); }
    finally { setLoading(false); }
  }, [from, to, walletId]);

  useEffect(() => { void walletService.getWallets().then(setWallets); }, []);
  useEffect(() => { void load(); }, [load]);

  async function reverseCredit() {
    if (!selected || !reason.trim()) return;
    setReversing(true); setError('');
    try {
      await walletService.reverseRechargeCredit(selected.rechargeRequestId, reason.trim());
      setSelected(undefined); setReason(''); await load();
    } catch { setError('لم يتم عكس الشحن. قد يكون الرصيد استُخدم أو تم عكس الطلب بالفعل.'); }
    finally { setReversing(false); }
  }

  function exportExcel() {
    if (!report?.items.length) return;
    const headers = ['الطالب', 'رقم الطالب', 'طريقة القبول', 'المبلغ', 'نوع الرصيد', 'المدرس', 'الرصيد قبل', 'الرصيد بعد', 'الرصيد الحالي', 'المحفظة', 'رقم المحول', 'الموظف/النظام', 'وقت القبول', 'اشتباه تكرار', 'حالة العكس'];
    const rows = report.items.map(item => [item.studentName, item.studentPhoneNumber, item.acceptanceMethod, item.amount, item.balanceScope, item.teacherName, item.balanceBefore, item.balanceAfter, item.currentBalance, `${item.walletLabel} ${item.walletPhoneNumber}`, item.senderPhoneNumber, item.resolvedByUserName, new Date(item.resolvedAt).toLocaleString('ar-EG', { timeZone: 'Africa/Cairo' }), item.duplicateReason || 'لا', item.isReversed ? 'تم العكس' : item.canReverse ? 'متاح' : item.reverseBlockedReason]);
    const xmlRows = [headers, ...rows].map(row => `<Row>${row.map(cell => `<Cell><Data ss:Type="String">${escapeXml(cell)}</Data></Cell>`).join('')}</Row>`).join('');
    const xml = `<?xml version="1.0"?><Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet"><Worksheet ss:Name="مراجعة الشيفت"><Table>${xmlRows}</Table></Worksheet></Workbook>`;
    const url = URL.createObjectURL(new Blob(['\ufeff', xml], { type: 'application/vnd.ms-excel;charset=utf-8' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = `recharge-shift-${from.slice(0, 10)}.xls`; anchor.click(); URL.revokeObjectURL(url);
  }

  const cards = [
    ['إجمالي المقبول', report?.acceptedCount ?? 0], ['المقبول يدويًا', report?.manualCount ?? 0],
    ['المقبول آليًا', report?.automaticCount ?? 0], ['مشتبه في تكراره', report?.suspectedDuplicateCount ?? 0],
    ['إجمالي الشحن', `${(report?.totalAmount ?? 0).toLocaleString('ar-EG')} ج.م`],
  ];

  return <div className="space-y-5" dir="rtl">
    <section className="admin-panel rounded-2xl p-5"><div className="grid gap-3 lg:grid-cols-4">
      <label className="text-sm font-bold">من<input type="datetime-local" className="admin-input mt-2" value={from} onChange={event => setFrom(event.target.value)} /></label>
      <label className="text-sm font-bold">إلى<input type="datetime-local" className="admin-input mt-2" value={to} onChange={event => setTo(event.target.value)} /></label>
      <label className="text-sm font-bold">المحفظة<select className="admin-input mt-2" value={walletId} onChange={event => setWalletId(event.target.value)}><option value="">كل المحافظ</option>{wallets.map(wallet => <option key={wallet.id} value={wallet.id}>{wallet.label} — {wallet.phoneNumber}</option>)}</select></label>
      <div className="flex items-end gap-2"><button type="button" className="admin-btn-primary flex-1" onClick={() => void load()}><RefreshCw className="h-4 w-4" /> تحديث</button><button type="button" className="admin-btn-ghost flex-1" disabled={!report?.items.length} onClick={exportExcel}><Download className="h-4 w-4" /> Excel</button></div>
    </div></section>
    <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">{cards.map(([label, value]) => <article key={String(label)} className="admin-panel rounded-2xl p-4"><p className="text-sm text-[var(--admin-muted)]">{label}</p><strong className="mt-2 block text-2xl">{value}</strong></article>)}</section>
    {error ? <p role="alert" className="rounded-xl bg-rose-50 p-4 font-bold text-rose-700">{error}</p> : null}
    <section className="admin-panel rounded-2xl p-5"><div className="overflow-x-auto"><table className="w-full min-w-[1300px] text-sm"><thead><tr className="text-right"><th>الطالب</th><th>القبول</th><th>المبلغ والرصيد</th><th>قبل / بعد / حالي</th><th>المحفظة والمحول</th><th>المسؤول والوقت</th><th>مراجعة التكرار</th><th>الإجراء</th></tr></thead><tbody>{report?.items.map(item => <tr key={item.rechargeRequestId} className="border-t border-[var(--admin-border)] align-top"><td><b>{item.studentName}</b><bdi className="block font-mono">{item.studentPhoneNumber}</bdi></td><td><span className={item.acceptanceMethod === 'آلي' ? 'font-bold text-emerald-600' : 'font-bold text-sky-700'}>{item.acceptanceMethod}</span></td><td><b>{item.amount.toLocaleString('ar-EG')} ج.م</b><span className="block text-xs text-[var(--admin-muted)]">{item.balanceScope}{item.teacherName ? ` — ${item.teacherName}` : ''}</span></td><td className="font-mono"><span>{item.balanceBefore ?? '—'}</span> / <span>{item.balanceAfter ?? '—'}</span> / <b>{item.currentBalance}</b></td><td><b>{item.walletLabel}</b><bdi className="block font-mono text-xs">من: {item.senderPhoneNumber || '—'}</bdi></td><td><b>{item.resolvedByUserName}</b><span className="block text-xs">{new Date(item.resolvedAt).toLocaleString('ar-EG', { timeZone: 'Africa/Cairo' })}</span></td><td>{item.suspectedDuplicate ? <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-1 font-bold text-amber-800"><AlertTriangle className="h-4 w-4" /> مشتبه</span> : <span className="text-[var(--admin-muted)]">لا يوجد</span>}</td><td>{item.isReversed ? <b className="text-rose-600">تم العكس</b> : <button type="button" className="admin-btn-ghost" disabled={!item.canReverse} title={item.reverseBlockedReason} onClick={() => setSelected(item)}><RotateCcw className="h-4 w-4" /> عكس آمن</button>}{!item.canReverse && !item.isReversed ? <small className="mt-1 block max-w-48 text-amber-700">{item.reverseBlockedReason}</small> : null}</td></tr>)}</tbody></table>{loading ? <p className="py-10 text-center">جارٍ التحميل…</p> : !report?.items.length ? <p className="py-10 text-center text-[var(--admin-muted)]">لا توجد عمليات مقبولة في الفترة.</p> : null}</div></section>
    {selected ? <div className="fixed inset-0 z-[var(--z-modal)] grid place-items-center bg-slate-950/55 p-4"><div className="admin-panel w-full max-w-lg rounded-2xl p-6"><h2 className="text-xl font-black">تأكيد عكس الشحن</h2><p className="mt-2 text-sm">سيُخصم {selected.amount} ج.م من رصيد الطالب والمحفظة ويُعكس القيد المالي. العملية مسجلة باسمك.</p><textarea className="admin-input mt-4 min-h-28" placeholder="اكتب سبب العكس المالي (إجباري)" value={reason} onChange={event => setReason(event.target.value)} /><div className="mt-4 flex gap-2"><button className="admin-btn-primary" type="button" disabled={!reason.trim() || reversing} onClick={() => void reverseCredit()}>{reversing ? 'جارٍ العكس…' : 'تأكيد العكس'}</button><button className="admin-btn-ghost" type="button" disabled={reversing} onClick={() => { setSelected(undefined); setReason(''); }}>إلغاء</button></div></div></div> : null}
  </div>;
}

export default function RechargeShiftReviewPageClient() {
  return <AdminPage activePath="/admin/recharge-shift-review" sectionLabel="المدفوعات" pageTitle="مراجعة شحن آخر الشيفت" subtitle="المقبول يدويًا وآليًا، الرصيد قبل وبعد، وكشف التكرار مع عكس مالي آمن."><RechargeShiftReviewWorkspace /></AdminPage>;
}
