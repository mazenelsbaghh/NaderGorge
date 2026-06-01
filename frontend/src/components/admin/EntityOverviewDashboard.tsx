'use client';

import { 
  TrendingUp, Users, Eye, Receipt, BadgeCheck, Clock, 
  ArrowUpRight, Sparkles, AlertCircle 
} from 'lucide-react';

interface OverviewStat {
  label: string;
  value: string | number;
  trend: 'up' | 'down' | 'neutral';
  trendValue: string;
  icon: React.ElementType;
  variant: 'primary' | 'accent' | 'success' | 'warning';
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
  mockStats?: boolean;
}

export function EntityOverviewDashboard({ entityType, details, mockStats = true }: EntityOverviewDashboardProps) {
  
  // Generating believable mock stats based on the entity type to give a vivid dashboard feel
  const stats: OverviewStat[] = [
    {
      label: 'إجمالي المشتريات',
      value: details.price && details.price > 0 ? '1,248' : '3,490',
      trend: 'up',
      trendValue: '+12.5%',
      icon: Receipt,
      variant: 'primary'
    },
    {
      label: 'الطلاب النشطين',
      value: '845',
      trend: 'up',
      trendValue: '+5.2%',
      icon: Users,
      variant: 'success'
    },
    {
      label: 'معدل الإكمال',
      value: '78%',
      trend: 'neutral',
      trendValue: '0%',
      icon: BadgeCheck,
      variant: 'accent'
    },
    {
      label: 'متوسط وقت التفاعل',
      value: '1س 20د',
      trend: 'down',
      trendValue: '-2.1%',
      icon: Clock,
      variant: 'warning'
    }
  ];

  return (
    <div className="space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-500 fill-mode-both">
      
      {/* ─── Hero Summary Section ─── */}
      <div className="relative overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-8 shadow-sm">
        <div className="absolute -left-20 -top-20 h-64 w-64 rounded-full bg-[var(--admin-primary)] opacity-5 blur-3xl" />
        <div className="absolute -right-10 -bottom-10 h-40 w-40 rounded-full bg-[var(--admin-accent)] opacity-5 blur-2xl" />
        
        <div className="relative z-10 grid grid-cols-1 md:grid-cols-3 gap-8">
          <div className="md:col-span-2 space-y-4">
            <div className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)] border border-[var(--admin-primary)]/20">
              <Sparkles className="h-3.5 w-3.5" />
              مسودة {entityType}
            </div>
            <h2 className="text-3xl font-black text-[var(--admin-text)] tracking-tight">
              {details.title}
            </h2>
            <p className="text-base text-[var(--admin-muted)] leading-relaxed max-w-xl">
              {details.description || 'لم يتم إضافة وصف مفصل بعد. إضافة مسودة أو تفاصيل يساعد الطلاب على فهم المحتوى وتجربة التعلم بشكل أفضل.'}
            </p>
          </div>

          <div className="md:border-r border-[var(--admin-border)] md:pr-8 flex flex-col justify-center space-y-5">
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)] mb-1">تسعير الطرح</p>
              <p className="text-3xl font-black text-[var(--admin-text)]">
                {details.price && details.price > 0 ? (
                  <>
                    <span className="text-[var(--admin-primary)]">{details.price}</span>
                    <span className="text-lg text-[var(--admin-muted)] ml-1">ج.م</span>
                  </>
                ) : (
                  <span className="text-[var(--admin-success)]">مجانياً</span>
                )}
              </p>
            </div>
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)] mb-1">تاريخ الإنشاء</p>
              <p className="text-sm font-bold text-[var(--admin-text)]">
                {details.createdAt || new Date().toLocaleDateString('ar-EG', { year: 'numeric', month: 'long', day: 'numeric' })}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* ─── Warning / Disclaimer for Mock ─── */}
      {mockStats && (
        <div className="flex items-center gap-3 rounded-2xl border border-[var(--admin-warning)]/30 bg-[var(--admin-warning)]/5 px-5 py-4 text-sm font-bold text-[var(--admin-warning-fg,var(--admin-warning))] dark:text-yellow-400">
          <AlertCircle className="h-5 w-5 shrink-0" />
          <p>أرقام الإحصائيات معروضة بشكل توضيحي (Mock Data) لاختبار التصميم. سيتم ربطها بواجهة البيانات الفعلية في التحديثات القادمة.</p>
        </div>
      )}

      {/* ─── Metric Cards Grid ─── */}
      <h3 className="text-xl font-black text-[var(--admin-text)] pt-2">أداء الـ{entityType}</h3>
      <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat, idx) => {
          const Icon = stat.icon;
          const isUp = stat.trend === 'up';
          const isDown = stat.trend === 'down';
          
          return (
            <div 
              key={idx} 
              className="group relative overflow-hidden rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 transition-all hover:border-[var(--admin-primary)] hover:shadow-lg"
              style={{ animationDelay: `${(idx + 1) * 100}ms` }}
            >
              <div className="mb-4 flex items-center justify-between">
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-text)] group-hover:bg-[var(--admin-primary-15)] group-hover:text-[var(--admin-primary)] transition-colors">
                  <Icon className="h-6 w-6" />
                </div>
                
                <div className={`flex items-center gap-1 rounded-full px-2 py-1 text-xs font-bold ${
                  isUp ? 'bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-400' :
                  isDown ? 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400' :
                  'bg-[var(--admin-bg)] text-[var(--admin-muted)]'
                }`}>
                  {isUp && <TrendingUp className="h-3 w-3" />}
                  {isDown && <TrendingUp className="h-3 w-3 rotate-180" />}
                  {stat.trendValue}
                </div>
              </div>
              
              <div className="space-y-1">
                <p className="text-sm font-bold text-[var(--admin-muted)]">{stat.label}</p>
                <p className="text-3xl font-black text-[var(--admin-text)] tracking-tight group-hover:text-[var(--admin-primary)] transition-colors">
                  {stat.value}
                </p>
              </div>

              {/* Decorative background graph element */}
              <div className="absolute -bottom-6 -left-6 opacity-5 group-hover:opacity-10 transition-opacity">
                 <svg width="120" height="80" viewBox="0 0 120 80" className="stroke-[var(--admin-primary)] fill-none stroke-[3] stroke-linecap-round">
                   <path d="M0,80 Q20,60 40,70 T80,40 T120,20" />
                 </svg>
              </div>
            </div>
          );
        })}
      </div>

      {/* ─── Recent Activity Minimal List ─── */}
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8">
        <div className="mb-6 flex items-center justify-between">
          <div>
            <h3 className="text-lg font-black text-[var(--admin-text)]">آخر التفاعلات</h3>
            <p className="text-sm font-bold text-[var(--admin-muted)]">عمليات شراء، مشاهدات، أو تعليقات جديدة.</p>
          </div>
          <button className="text-sm font-bold text-[var(--admin-primary)] hover:underline flex items-center gap-1">
            عرض الكل <ArrowUpRight className="h-4 w-4" />
          </button>
        </div>

        <div className="space-y-4">
          {[1, 2, 3].map((item) => (
            <div key={item} className="flex items-center gap-4 rounded-2xl border border-[var(--admin-border)]/50 bg-[var(--admin-bg)]/50 p-4">
              <div className="h-10 w-10 shrink-0 rounded-full bg-[var(--admin-primary-15)] flex items-center justify-center text-[var(--admin-primary)] font-black text-sm">
                مـ
              </div>
              <div className="flex-1">
                <p className="text-sm font-bold text-[var(--admin-text)] cursor-pointer hover:text-[var(--admin-primary)]">محمود أحمد اشترى الـ{entityType}</p>
                <p className="text-xs text-[var(--admin-muted)]">منذ ساعتين • عبر الكود الورقي</p>
              </div>
              <div className="text-sm font-black text-[var(--admin-success)]">
                +{details.price || '0'} ج
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
