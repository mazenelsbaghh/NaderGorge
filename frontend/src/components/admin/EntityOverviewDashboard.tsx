'use client';

import { BarChart3, CalendarDays, Receipt, Sparkles, type LucideIcon } from 'lucide-react';

interface OverviewStat {
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
}

const toneClassName: Record<NonNullable<OverviewStat['tone']>, string> = {
  primary: 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]',
  success: 'bg-green-500/10 text-green-700 dark:text-green-300',
  warning: 'bg-amber-500/10 text-amber-700 dark:text-amber-300',
  muted: 'bg-[var(--admin-card-strong)] text-[var(--admin-muted)]',
};

export function EntityOverviewDashboard({ entityType, details, stats = [] }: EntityOverviewDashboardProps) {
  const createdAt = details.createdAt
    ? details.createdAt
    : 'غير متاح';

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
              تظهر هنا المؤشرات بعد ربط مصدر بيانات حقيقي.
            </p>
          </div>
        </div>

        {stats.length > 0 ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {stats.map((stat) => {
              const Icon = stat.icon;
              return (
                <div
                  key={stat.label}
                  className="group relative overflow-hidden rounded-3xl bg-[var(--admin-card)] p-6 shadow-sm transition-all hover:shadow-lg"
                >
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
              );
            })}
          </div>
        ) : (
          <div className="rounded-3xl bg-[var(--admin-card)] p-8 shadow-sm">
            <div className="flex flex-col gap-5 md:flex-row md:items-center md:justify-between">
              <div className="max-w-2xl">
                <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
                  <BarChart3 className="h-6 w-6" />
                </div>
                <h4 className="text-lg font-black text-[var(--admin-text)]">لا توجد بيانات تحليلية بعد</h4>
                <p className="mt-2 text-sm leading-6 text-[var(--admin-muted)]">
                  لن نعرض أرقام تقديرية أو عشوائية هنا. عند توفر مصدر بيانات فعلي للمشتريات، التفاعل، أو الإكمال ستظهر المؤشرات تلقائيا.
                </p>
              </div>
              <div className="grid min-w-52 grid-cols-2 gap-3 text-sm">
                <div className="rounded-2xl bg-[var(--admin-card-strong)] p-4">
                  <Receipt className="mb-3 h-5 w-5 text-[var(--admin-primary)]" />
                  <p className="font-black text-[var(--admin-text)]">المشتريات</p>
                  <p className="text-xs text-[var(--admin-muted)]">بانتظار API</p>
                </div>
                <div className="rounded-2xl bg-[var(--admin-card-strong)] p-4">
                  <CalendarDays className="mb-3 h-5 w-5 text-[var(--admin-primary)]" />
                  <p className="font-black text-[var(--admin-text)]">الإكمال</p>
                  <p className="text-xs text-[var(--admin-muted)]">بانتظار API</p>
                </div>
              </div>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
