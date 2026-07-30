'use client';

import { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { Gift, Plus, RefreshCw, Search } from 'lucide-react';
import { AdminPageSkeleton, AdminPage } from '@/components/admin';
import { GiftLedgerTable } from '@/components/admin/gifts/GiftLedgerTable';
import { adminGiftsService, giftTargetLabels, type GiftPageDto, type GiftTargetType } from '@/services/admin-gifts-service';

export default function GiftsLedgerPageClient() {
  const [data, setData] = useState<GiftPageDto | null>(null);
  const [search, setSearch] = useState('');
  const [targetType, setTargetType] = useState<GiftTargetType | ''>('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const load = useCallback(async () => {
    try { setLoading(true); setError(false); setData(await adminGiftsService.list({ search: search || undefined, targetType })); }
    catch { setError(true); } finally { setLoading(false); }
  }, [search, targetType]);
  useEffect(() => { void load(); }, [load]);

  return <AdminPage activePath="/admin/gifts" sectionLabel="البيع والمحتوى" pageTitle="الهدايا والوصول المجاني" subtitle="إصدار وصول مباشر أو رصيد ترويجي مع سجل استخدام وإلغاء للمتبقي." action={<Link href="/admin/gifts/new" className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-4 text-sm font-bold text-white"><Plus className="h-4 w-4" /> هدية جديدة</Link>}>
    <div className="space-y-5">
      <div className="grid gap-3 md:grid-cols-[1fr_240px]"><label className="relative"><Search className="absolute right-3 top-3.5 h-4 w-4 text-[var(--admin-muted)]" /><input className="admin-input pr-10" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="ابحث باسم الطالب أو الموظف أو السبب" /></label><select className="admin-input" value={targetType} onChange={(event) => setTargetType(event.target.value as GiftTargetType | '')}><option value="">كل أنواع الهدايا</option>{Object.entries(giftTargetLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div>
      {loading ? <AdminPageSkeleton /> : error ? <div className="flex min-h-52 flex-col items-center justify-center gap-3 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)]"><p className="text-sm font-bold text-red-600">تعذر تحميل سجل الهدايا.</p><button type="button" onClick={() => void load()} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-4 text-sm font-bold text-white"><RefreshCw className="h-4 w-4" /> إعادة المحاولة</button></div> : !data?.items.length ? <div className="flex min-h-52 flex-col items-center justify-center rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 text-center"><Gift className="h-8 w-8 text-[var(--admin-primary)]" /><h2 className="mt-3 font-black text-[var(--admin-text)]">لا توجد هدايا مطابقة</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">ابدأ بإصدار هدية جديدة أو غيّر البحث.</p></div> : <GiftLedgerTable items={data.items} />}
    </div>
  </AdminPage>;
}
