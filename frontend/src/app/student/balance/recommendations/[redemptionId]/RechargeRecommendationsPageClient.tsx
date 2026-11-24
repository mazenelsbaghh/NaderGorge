'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { BookOpen, Wallet } from 'lucide-react';
import { codeService, type RechargeRecommendation, type RechargeRecommendations } from '@/services/code-service';
import { PurchaseContentModal } from '@/components/balance/PurchaseContentModal';
import { getApiErrorSummary } from '@/lib/api-errors';

const money = (amount: number) => `${amount.toLocaleString('ar-EG')} جنيه`;
const contentLabels = { Package: 'كورس', Term: 'ترم', Month: 'شهر / قسم', Lesson: 'حصة' };

export default function RechargeRecommendationsPageClient({ redemptionId }: { redemptionId: string }) {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [retry, setRetry] = useState(0);
  const [recommendations, setRecommendations] = useState<RechargeRecommendations | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [purchase, setPurchase] = useState<RechargeRecommendation | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    void codeService.getRechargeRecommendations(redemptionId, page, controller.signal)
      .then((response) => { if (!controller.signal.aborted) setRecommendations(response); })
      .catch((loadError: unknown) => {
        if (!controller.signal.aborted) setError(getApiErrorSummary(loadError, 'تعذر تحميل اقتراحات المحتوى. رصيدك محفوظ.'));
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [redemptionId, page, retry]);

  return <div dir="rtl" className="mx-auto max-w-5xl space-y-6 pb-10">
    <header className="space-y-3">
      <p className="flex items-center gap-2 text-sm font-bold text-[var(--admin-primary)]"><Wallet className="h-5 w-5" aria-hidden="true" /> اختار محتواك بعد الشحن</p>
      <h1 className="text-2xl font-black text-[var(--admin-text)] sm:text-3xl">
        {recommendations ? `محتوى ${recommendations.teacherName}` : 'المحتوى المناسب لرصيدك'}
      </h1>
      {recommendations && <>
        <p className="text-base font-bold text-[var(--admin-text)]">قيمة الكود: {money(recommendations.rechargedAmount)} · إجمالي الرصيد المتاح للشراء عند المدرس: {money(recommendations.availableBalance)}</p>
        <p className="text-sm leading-7 text-[var(--admin-muted)]">رصيد الكود خاص بهذا المدرس. يظهر المحتوى المناسب لمرحلتك وصفك، بنفس قيمة الكود أولًا ثم الأقل سعرًا، من الأحدث للأقدم في كل مجموعة. وتظهر الاختيارات الأعلى سعرًا بعدهم لو إجمالي رصيدك يسمح.</p>
      </>}
      <Link href="/student/balance" className="inline-flex min-h-11 items-center text-sm font-bold text-[var(--admin-primary)] hover:underline">عرض محفظتي</Link>
    </header>

    {loading ? <div aria-label="جارٍ تحميل المحتوى" className="space-y-3 animate-pulse motion-reduce:animate-none">
      {[0, 1, 2].map((index) => <div key={index} className="h-32 rounded-2xl bg-[var(--admin-card-strong)]" />)}
    </div> : error ? <div role="alert" className="space-y-3 rounded-2xl border border-[var(--admin-border)] p-5">
      <p className="text-[var(--admin-danger)]">{error}</p>
      <button type="button" onClick={() => setRetry((current) => current + 1)} className="admin-btn-primary min-h-11">إعادة المحاولة</button>
    </div> : recommendations?.items.length === 0 ? <section className="space-y-3 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6">
      <h2 className="text-lg font-bold text-[var(--admin-text)]">لا يوجد محتوى جديد مناسب لرصيدك حاليًا</h2>
      <p className="text-sm leading-7 text-[var(--admin-muted)]">رصيدك محفوظ، والمحتوى الذي اشتركت فيه موجود في دروسك.</p>
      <Link href="/student/lessons" className="inline-flex min-h-11 items-center font-bold text-[var(--admin-primary)] hover:underline">افتح دروسي</Link>
    </section> : <div className="space-y-3">
      {recommendations?.items.map((item) => <article key={`${item.contentType}-${item.contentId}`}
        className="flex flex-col gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0 space-y-2">
          <p className="text-xs font-bold text-[var(--admin-primary)]">{contentLabels[item.contentType]}{item.price === recommendations.rechargedAmount ? ' · بنفس قيمة الكود' : ''}</p>
          <h2 className="break-words text-lg font-bold text-[var(--admin-text)] [overflow-wrap:anywhere]">{item.title}</h2>
          <p className="text-sm text-[var(--admin-muted)]">{[item.packageName, item.termName, item.sectionName].filter((name, index, names) => name && names.indexOf(name) === index).join(' ‹ ')}</p>
          <p className="font-bold text-[var(--admin-text)]">{money(item.price)}</p>
        </div>
        <button type="button" onClick={() => setPurchase(item)} aria-label={`شراء وفتح ${item.title}`}
          className="admin-btn-primary inline-flex min-h-11 shrink-0 items-center justify-center gap-2">
          <BookOpen className="h-4 w-4" aria-hidden="true" /> شراء وفتح
        </button>
      </article>)}
    </div>}

    {!loading && !error && recommendations && <nav aria-label="صفحات المحتوى" className="flex flex-wrap items-center justify-between gap-3">
      <button type="button" disabled={page === 1} onClick={() => setPage((current) => current - 1)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-[var(--admin-text)] disabled:opacity-40">السابق</button>
      <span className="text-sm text-[var(--admin-text)]">صفحة {page.toLocaleString('ar-EG')}</span>
      <button type="button" disabled={!recommendations.hasMore} onClick={() => setPage((current) => current + 1)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-[var(--admin-text)] disabled:opacity-40">التالي</button>
    </nav>}
    {purchase && <PurchaseContentModal isOpen onClose={() => setPurchase(null)} contentType={purchase.contentType}
      contentId={purchase.contentId} contentName={purchase.title} price={purchase.price}
      onPurchaseSuccess={() => { router.push(purchase.openUrl); }} />}
  </div>;
}
