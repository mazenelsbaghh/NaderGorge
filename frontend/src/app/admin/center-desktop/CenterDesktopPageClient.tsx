'use client';

import { useMemo, useState } from 'react';
import { AlertCircle, CheckCircle2, ChevronLeft, RefreshCw, Search } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import type { DesktopReceipt } from '@/services/center-desktop-service';
import DesktopDiagnosticsPanel from './DesktopDiagnosticsPanel';
import DesktopReleaseList from './DesktopReleaseList';
import { useDesktopOverview } from './useDesktopOverview';
import { osLabel, roleLabel, sizeLabel, timestamp } from './display';

export default function CenterDesktopPageClient() {
  const state = useDesktopOverview();
  const [tab, setTab] = useState<'uploads' | 'releases'>('uploads');
  const [releaseRefresh, setReleaseRefresh] = useState(0);
  const [query, setQuery] = useState('');
  const [role, setRole] = useState('');
  const [os, setOs] = useState('');
  const [selected, setSelected] = useState<DesktopReceipt | null>(null);
  const rows = useMemo(() => {
    const search = query.trim().toLowerCase();
    return state.uploads.filter(row =>
      (!role || row.app.role === role) && (!os || row.app.os === os) &&
      (!search || [row.centerId, row.uploadId, row.app.version, row.app.build].some(value => value.toLowerCase().includes(search)))
    ).sort((a, b) => b.receivedAt.localeCompare(a.receivedAt));
  }, [state.uploads, query, role, os]);
  const available = state.status?.available;
  const refresh = () => { setSelected(null); setReleaseRefresh(count => count + 1); void state.reload(); };

  return <AdminPage activePath="/admin/center-desktop" sectionLabel="إدارة السنتر" pageTitle="برنامج السنتر"
    subtitle="راجع النسخ والمشاكل المرفوعة من البرنامج، وتابع الإصدارات المتاحة لكل جهاز."
    action={<button className="admin-btn-ghost inline-flex items-center gap-2" onClick={refresh} disabled={state.loading}><RefreshCw className={`h-4 w-4 ${state.loading ? 'motion-safe:animate-spin' : ''}`} />تحديث</button>}>
    <div className="space-y-5" dir="rtl">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--admin-border)] pb-4">
        <div className={`flex items-center gap-2 text-sm ${available && !state.error ? 'text-[var(--admin-success)]' : 'text-[var(--admin-muted)]'}`}>
          {available && !state.error ? <CheckCircle2 className="h-4 w-4" /> : <AlertCircle className="h-4 w-4" />}
          <span>{state.loading ? 'جاري التحقق من الخدمة…' : state.error ? 'تعذر التحقق من الاتصال' : available ? 'خدمة النسخ متصلة' : state.status?.configured ? 'خدمة النسخ غير متاحة حاليًا' : 'ربط خدمة السنتر لم يكتمل بعد'}</span>
        </div>
        {state.updatedAt && <p className="text-xs text-[var(--admin-muted)]">آخر تحميل للقائمة: {timestamp(state.updatedAt)} · القاهرة</p>}
      </div>
      {state.error && <p role="alert" className="rounded-xl bg-[var(--admin-danger-10)] p-4 text-sm text-[var(--admin-danger)]">{state.error}</p>}
      {!state.loading && state.status && !available && !state.error && <section className="admin-panel space-y-3 p-6" aria-labelledby="desktop-unavailable-title">
        <h2 id="desktop-unavailable-title" className="text-lg font-bold">{state.status.configured ? 'تعذر الوصول لخدمة النسخ' : 'جاهزة للربط مع برنامج السنتر'}</h2>
        <p className="max-w-2xl text-sm leading-7 text-[var(--admin-muted)]">{state.status.configured ? 'النسخ والمشاكل غير متاحة للعرض الآن. حاول التحديث بعد رجوع الخدمة.' : 'بعد تفعيل خدمة الربط على مسار، ارفع نسخة من «المزامنة والتحديثات» داخل برنامج السنتر. هتظهر هنا بيانات الإصدار والمشاكل وقت الرفع.'}</p>
        <p className="text-sm">التسجيل والدفع داخل السنتر يفضلوا شغّالين أوفلاين زي ما هم.</p>
      </section>}
      {(available || state.uploads.length > 0) && <>
        <div className="flex flex-wrap gap-2" aria-label="أقسام برنامج السنتر">
          <button className={tab === 'uploads' ? 'admin-btn-primary' : 'admin-btn-ghost'} aria-pressed={tab === 'uploads'} onClick={() => setTab('uploads')}>النسخ والمشاكل</button>
          <button className={tab === 'releases' ? 'admin-btn-primary' : 'admin-btn-ghost'} aria-pressed={tab === 'releases'} onClick={() => { setSelected(null); setTab('releases'); }}>إصدارات البرنامج</button>
        </div>
        {tab === 'releases' ? <DesktopReleaseList refreshKey={releaseRefresh} /> : <>
          <div className="admin-panel grid items-end gap-3 p-4 md:grid-cols-[minmax(12rem,1fr)_10rem_10rem]">
            <label className="space-y-2 text-sm"><span>بحث في النسخ المحمّلة</span><div className="relative"><Search className="pointer-events-none absolute start-3 top-3 h-4 w-4 text-[var(--admin-muted)]" /><input type="search" className="admin-input w-full ps-10" value={query} placeholder="معرّف السنتر، الإصدار أو رقم الرفع" onChange={event => setQuery(event.target.value)} /></div></label>
            <label className="space-y-2 text-sm"><span>نوع الجهاز</span><select className="admin-input w-full" value={role} onChange={event => setRole(event.target.value)}><option value="">الكل</option><option value="host">الرئيسي</option><option value="client">الفرعي</option></select></label>
            <label className="space-y-2 text-sm"><span>النظام</span><select className="admin-input w-full" value={os} onChange={event => setOs(event.target.value)}><option value="">الكل</option><option value="windows">ويندوز</option><option value="macos">ماك</option></select></label>
          </div>
          <div className="flex flex-wrap justify-between gap-2 text-sm"><p>{rows.length} نسخة ظاهرة من {state.uploads.length} محمّلة</p><p className="text-[var(--admin-muted)]">النتائج تخص الرفعات، وليست حالة اتصال الأجهزة الآن.</p></div>
          {state.uploadError && <p role="alert" className="text-sm text-[var(--admin-danger)]">{state.uploadError}</p>}
          <section className="admin-panel overflow-hidden" aria-label="قائمة النسخ المرفوعة" aria-busy={state.loading || state.loadingMore}>
            <div className="overflow-x-auto">
              <table className="w-full text-start text-sm">
                <thead className="bg-[var(--admin-card-soft)] text-[var(--admin-text)]"><tr>{['السنتر والجهاز', 'إصدار البرنامج', 'وقت الاستلام · القاهرة', 'الحجم', 'التفاصيل'].map(label => <th key={label} scope="col" className="whitespace-nowrap px-4 py-3 text-start font-semibold">{label}</th>)}</tr></thead>
                <tbody className="divide-y divide-[var(--admin-border)]">
                  {rows.map(row => <tr key={row.uploadId} className={selected?.uploadId === row.uploadId ? 'bg-[var(--admin-primary-10)]' : 'bg-[var(--admin-card)]'}>
                    <td className="px-4 py-4"><p className="max-w-64 break-all font-semibold">{row.centerId}</p><p className="mt-1 text-xs text-[var(--admin-muted)]">{roleLabel(row.app.role)} · {osLabel(row.app.os)}</p></td>
                    <td className="px-4 py-4"><bdi>{row.app.version}</bdi></td>
                    <td className="px-4 py-4 whitespace-nowrap"><time dateTime={row.receivedAt}>{timestamp(row.receivedAt)}</time></td>
                    <td className="px-4 py-4 whitespace-nowrap"><bdi>{sizeLabel(row.size)}</bdi></td>
                    <td className="px-4 py-4"><button className="admin-btn-ghost inline-flex items-center gap-1 whitespace-nowrap" aria-label={`عرض مشاكل ${row.centerId} ${row.uploadId}`} aria-expanded={selected?.uploadId === row.uploadId} onClick={() => setSelected(row)}>تفاصيل المشاكل<ChevronLeft className="h-4 w-4" /></button></td>
                  </tr>)}
                </tbody>
              </table>
            </div>
            {!rows.length && <div className="p-8 text-center text-sm" role="status">{state.loading ? 'جاري تحميل النسخ…' : state.uploadError ? 'لم نتمكن من عرض النسخ. جرّب التحديث.' : query || role || os ? 'لا توجد نتائج مطابقة في النسخ المحمّلة. غيّر البحث أو حمّل المزيد.' : 'لم تُرفع أي نسخة بعد. استخدم زر المزامنة من برنامج السنتر لعرضها هنا.'}</div>}
          </section>
          {state.cursor && <div className="flex flex-wrap items-center justify-between gap-3"><p className="text-xs text-[var(--admin-muted)]">فيه نسخ إضافية. البحث والترتيب حسب الوقت بيشملوا النسخ المحمّلة فقط.</p><button className="admin-btn-ghost" disabled={state.loading || state.loadingMore} onClick={() => void state.loadMore()}>{state.loadingMore ? 'جاري التحميل…' : 'تحميل المزيد'}</button></div>}
          {selected && <DesktopDiagnosticsPanel key={selected.uploadId} receipt={selected} onClose={() => setSelected(null)} />}
        </>}
      </>}
    </div>
  </AdminPage>;
}
