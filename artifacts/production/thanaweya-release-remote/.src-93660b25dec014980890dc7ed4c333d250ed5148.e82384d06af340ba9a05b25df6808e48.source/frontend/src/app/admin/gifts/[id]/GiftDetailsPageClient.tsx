'use client';

import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { RefreshCw } from 'lucide-react';
import { AdminPageSkeleton, AdminShellChrome } from '@/components/admin';
import { GiftDetailsPanel } from '@/components/admin/gifts/GiftDetailsPanel';
import { adminGiftsService, type GiftDetailsDto } from '@/services/admin-gifts-service';

export default function GiftDetailsPageClient() {
  const { id } = useParams<{ id: string }>();
  const [gift, setGift] = useState<GiftDetailsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const load = useCallback(async () => { try { setLoading(true); setError(false); setGift(await adminGiftsService.details(id)); } catch { setError(true); } finally { setLoading(false); } }, [id]);
  useEffect(() => { void load(); }, [load]);
  return <AdminShellChrome activePath="/admin/gifts" sectionLabel="الهدايا" pageTitle={gift?.targetName ?? 'تفاصيل الهدية'} subtitle="حالة الإصدار ونتيجة كل مستفيد وسجل القيمة المستخدمة.">{loading ? <AdminPageSkeleton /> : error || !gift ? <div className="flex min-h-52 flex-col items-center justify-center gap-3 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)]"><p className="text-sm font-bold text-red-600">تعذر تحميل تفاصيل الهدية.</p><button type="button" onClick={() => void load()} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-4 text-sm font-bold text-white"><RefreshCw className="h-4 w-4" /> إعادة المحاولة</button></div> : <GiftDetailsPanel gift={gift} onChanged={load} />}</AdminShellChrome>;
}
