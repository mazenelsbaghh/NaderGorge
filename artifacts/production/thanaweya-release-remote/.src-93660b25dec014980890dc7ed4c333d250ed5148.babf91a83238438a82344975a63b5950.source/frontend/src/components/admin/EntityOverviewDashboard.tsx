'use client';

import { useState } from 'react';
import { BarChart3, Sparkles, Pencil, Check, X, type LucideIcon } from 'lucide-react';

export interface OverviewStat {
  label: string;
  value: string | number;
  icon: LucideIcon;
  tone?: 'primary' | 'success' | 'warning' | 'muted';
}

interface EntityOverviewDashboardProps {
  entityType: 'باقة' | 'ترم' | 'قسم' | 'حصة';
  details: {
    title: string;
    description?: string | null;
    price?: number | null;
    status?: string | null;
    createdAt?: string;
  };
  stats?: OverviewStat[];
  loading?: boolean;
  onPriceUpdate?: (newPrice: number) => Promise<void>;
  children?: React.ReactNode;
}

const toneClassName: Record<NonNullable<OverviewStat['tone']>, string> = {
  primary: 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]',
  success: 'bg-green-500/10 text-green-700 dark:text-green-300',
  warning: 'bg-amber-500/10 text-amber-700 dark:text-amber-300',
  muted: 'bg-[var(--admin-card-strong)] text-[var(--admin-muted)]',
};

function StatSkeleton() {
  return (
    <div className="relative overflow-hidden rounded-3xl bg-[var(--admin-card)] p-6 shadow-sm">
      <div className="mb-4 h-12 w-12 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />
      <div className="mb-2 h-4 w-24 animate-pulse rounded-lg bg-[var(--admin-card-strong)]" />
      <div className="h-8 w-20 animate-pulse rounded-lg bg-[var(--admin-card-strong)]" />
    </div>
  );
}

export function EntityOverviewDashboard({ entityType, details, stats = [], loading = false, onPriceUpdate, children }: EntityOverviewDashboardProps) {
  const [editingPrice, setEditingPrice] = useState(false);
  const [priceValue, setPriceValue] = useState(details.price ?? 0);
  const [savingPrice, setSavingPrice] = useState(false);

  const createdAt = details.createdAt
    ? details.createdAt
    : 'غير متاح';

  const handlePriceSave = async () => {
    if (!onPriceUpdate) return;
    setSavingPrice(true);
    try {
      await onPriceUpdate(priceValue);
      setEditingPrice(false);
    } finally {
      setSavingPrice(false);
    }
  };

  return (
    <div className="space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-500 fill-mode-both">
      <div className="relative overflow-hidden rounded-3xl bg-[var(--admin-card-strong)] p-8 shadow-sm">
        <div className="absolute -left-20 -top-20 h-64 w-64 rounded-full bg-[var(--admin-primary)] opacity-5 blur-3xl" />
        <div className="absolute -right-10 -bottom-10 h-40 w-40 rounded-full bg-[var(--admin-accent)] opacity-5 blur-2xl" />

        <div className="relative z-10 grid grid-cols-1 gap-8 md:grid-cols-3">
          <div className="space-y-4 md:col-span-2">
            <div className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
              <Sparkles className="h-3.5 w-3.5" />
              {entityType}
            </div>
            <h2 className="text-3xl font-black tracking-tight text-[var(--admin-text)]">
              {details.title}
            </h2>
            <p className="max-w-xl text-base leading-relaxed text-[var(--admin-muted)]">
              {details.description || 'لم يتم إضافة وصف مفصل بعد. إضافة وصف واضح يساعد الطلاب على فهم المحتوى وتجربة التعلم بشكل أفضل.'}
            </p>
          </div>

          <div className="flex flex-col justify-center space-y-5 rounded-2xl bg-[var(--admin-card)] p-5">
            <div>
              <p className="mb-1 text-xs font-bold text-[var(--admin-muted)]">تسعير الطرح</p>
              {editingPrice ? (
                <div className="flex items-center gap-2">
                  <input
                    type="number"
                    min="0"
                    step="1"
                    value={priceValue}
                    onChange={(e) => setPriceValue(Number(e.target.value))}
                    className="w-24 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] px-3 py-2 text-lg font-black text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] transition-colors"
                    autoFocus
                    disabled={savingPrice}
                  />
                  <span className="text-sm text-[var(--admin-muted)]">ج.م</span>
                  <button
                    onClick={handlePriceSave}
                    disabled={savingPrice}
                    className="flex h-8 w-8 items-center justify-center rounded-full bg-green-500/15 text-green-600 transition-colors hover:bg-green-500 hover:text-white disabled:opacity-50"
                  >
                    <Check className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => { setEditingPrice(false); setPriceValue(details.price ?? 0); }}
                    disabled={savingPrice}
                    className="flex h-8 w-8 items-center justify-center rounded-full bg-red-500/15 text-red-500 transition-colors hover:bg-red-500 hover:text-white disabled:opacity-50"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </div>
              ) : (
                <div className="flex items-center gap-2">
                  <p className="text-3xl font-black text-[var(--admin-text)]">
                    {details.price && details.price > 0 ? (
                      <>
                        <span className="text-[var(--admin-primary)]">{details.price}</span>
                        <span className="mr-1 text-lg text-[var(--admin-muted)]">ج.م</span>
                      </>
                    ) : (
                      <span className="text-[var(--admin-success)]">مجانا</span>
                    )}
                  </p>
                  {onPriceUpdate && (
                    <button
                      onClick={() => { setPriceValue(details.price ?? 0); setEditingPrice(true); }}
                      className="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--admin-card-strong)] text-[var(--admin-muted)] transition-colors hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary)]"
                      title="تعديل السعر"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                  )}
                </div>
              )}
            </div>
            <div>
              <p className="mb-1 text-xs font-bold text-[var(--admin-muted)]">تاريخ الإنشاء</p>
              <p className="text-sm font-bold text-[var(--admin-text)]">{createdAt}</p>
            </div>
          </div>
        </div>
      </div>

      <section className="space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h3 className="text-xl font-black text-[var(--admin-text)]">أداء الـ{entityType}</h3>
            <p className="text-sm font-bold text-[var(--admin-muted)]">
              {loading ? 'جاري تحميل الإحصائيات...' : stats.length > 0 ? 'إحصائيات فعلية من بيانات المنصة' : 'لا تتوفر إحصائيات حاليا'}
            </p>
          </div>
        </div>

        {loading ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <StatSkeleton key={i} />
            ))}
          </div>
        ) : stats.length > 0 ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {stats.map((stat) => {
              const Icon = stat.icon;
              return (
                <div
                  key={stat.label}
                  className="group relative overflow-hidden rounded-3xl bg-[var(--admin-card)] p-6 shadow-sm transition-all hover:shadow-lg hover:-translate-y-0.5"
                >
                  <div className="absolute -right-6 -top-6 text-[var(--admin-primary-15)] opacity-30 transition-transform duration-700 group-hover:rotate-12 group-hover:scale-125 pointer-events-none">
                    <Icon className="h-28 w-28" />
                  </div>
                  <div className="relative z-10">
                    <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-text)] transition-colors group-hover:bg-[var(--admin-primary-15)] group-hover:text-[var(--admin-primary)]">
                      <Icon className="h-6 w-6" />
                    </div>
                    <p className="text-sm font-bold text-[var(--admin-muted)]">{stat.label}</p>
                    <p className="mt-1 text-3xl font-black tracking-tight text-[var(--admin-text)]">
                      {stat.value}
                    </p>
                    <div className={`mt-4 inline-flex rounded-full px-3 py-1 text-xs font-black ${toneClassName[stat.tone || 'muted']}`}>
                      بيانات فعلية
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        ) : (
          <div className="rounded-3xl bg-[var(--admin-card)] p-8 shadow-sm">
            <div className="flex flex-col items-center gap-3 py-6 text-center">
              <div className="mb-2 flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                <BarChart3 className="h-7 w-7" />
              </div>
              <h4 className="text-lg font-black text-[var(--admin-text)]">لا تتوفر إحصائيات بعد</h4>
              <p className="max-w-md text-sm leading-6 text-[var(--admin-muted)]">
                عند توفر بيانات فعلية من المنصة (مشتريات، تفاعل، أو إكمال) ستظهر المؤشرات تلقائيا.
              </p>
            </div>
          </div>
        )}
      </section>

      {children}
    </div>
  );
}
