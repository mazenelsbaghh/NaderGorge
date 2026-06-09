'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { 
  Plus, 
  Calendar as CalendarIcon, 
  Send, 
  Music, 
  Link,
  ChevronRight,
  ChevronLeft
} from 'lucide-react';
import toast from 'react-hot-toast';
import { 
  mediaService, 
  SocialMediaPlanDto, 
  SocialPlatform, 
  SocialPlanStatus,
  MediaPipelineDto
} from '@/services/media-service';
import NeumorphButton from '@/components/ui/neumorph-button';

const YoutubeIcon = (props: any) => (
  <svg viewBox="0 0 24 24" width="24" height="24" stroke="currentColor" strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="M22.54 6.42a2.78 2.78 0 0 0-1.95-1.96C18.88 4 12 4 12 4s-6.88 0-8.59.46a2.78 2.78 0 0 0-1.95 1.96A29 29 0 0 0 1 11.75a29 29 0 0 0 .46 5.33 2.78 2.78 0 0 0 1.95 1.96C5.12 19.5 12 19.5 12 19.5s6.88 0 8.59-.46a2.78 2.78 0 0 0 1.95-1.96A29 29 0 0 0 23 11.75a29 29 0 0 0-.46-5.33z" />
    <polygon points="9.75 15.02 15.5 11.75 9.75 8.48 9.75 15.02" />
  </svg>
);

const FacebookIcon = (props: any) => (
  <svg viewBox="0 0 24 24" width="24" height="24" stroke="currentColor" strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <path d="M18 2h-3a5 5 0 0 0-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 0 1 1-1h3z" />
  </svg>
);

const InstagramIcon = (props: any) => (
  <svg viewBox="0 0 24 24" width="24" height="24" stroke="currentColor" strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <rect x="2" y="2" width="20" height="20" rx="5" ry="5" />
    <path d="M16 11.37A4 4 0 1 1 12.63 8 4 4 0 0 1 16 11.37z" />
    <line x1="17.5" y1="6.5" x2="17.51" y2="6.5" />
  </svg>
);

const PLATFORM_INFOS: Record<SocialPlatform, { label: string; icon: any; color: string; bg: string }> = {
  YouTube: { label: 'YouTube', icon: YoutubeIcon, color: 'text-rose-600', bg: 'bg-rose-50 dark:bg-rose-950/20' },
  Facebook: { label: 'Facebook', icon: FacebookIcon, color: 'text-blue-600', bg: 'bg-blue-50 dark:bg-blue-950/20' },
  Instagram: { label: 'Instagram', icon: InstagramIcon, color: 'text-pink-600', bg: 'bg-pink-50 dark:bg-pink-950/20' },
  TikTok: { label: 'TikTok', icon: Music, color: 'text-slate-800 dark:text-slate-200', bg: 'bg-slate-100 dark:bg-slate-900/40' },
  Telegram: { label: 'Telegram', icon: Send, color: 'text-sky-500', bg: 'bg-sky-50 dark:bg-sky-950/20' }
};

const STATUS_LABELS: Record<SocialPlanStatus, string> = {
  Draft: 'مسودة',
  Scripting: 'كتابة السيناريو',
  Scheduled: 'تم الجدولة',
  Published: 'تم النشر'
};

const STATUS_COLORS: Record<SocialPlanStatus, string> = {
  Draft: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
  Scripting: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400',
  Scheduled: 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400',
  Published: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400'
};

export default function SocialPlannerView() {
  const [plans, setPlans] = useState<SocialMediaPlanDto[]>([]);
  const [pipelines, setPipelines] = useState<MediaPipelineDto[]>([]);
  
  // Calendar navigation state
  const [currentDate, setCurrentDate] = useState(new Date());

  // Modal & Form state
  const [isCreateOpen, setCreateOpen] = useState(false);
  const [formData, setFormData] = useState({
    title: '',
    description: '',
    script: '',
    platform: 'YouTube' as SocialPlatform,
    status: 'Draft' as SocialPlanStatus,
    scheduledDate: '',
    mediaProductionPipelineId: ''
  });

  const fetchPlans = useCallback(async () => {
    try {
      // Fetch plans for current month
      const start = new Date(currentDate.getFullYear(), currentDate.getMonth(), 1).toISOString();
      const end = new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 0, 23, 59, 59).toISOString();
      
      const data = await mediaService.getSocialPlans({ startDate: start, endDate: end });
      setPlans(data || []);
    } catch {
      toast.error('حدث خطأ أثناء تحميل خطط النشر');
    }
  }, [currentDate]);

  const fetchPipelines = useCallback(async () => {
    try {
      // Load active pipelines to link to plans
      const data = await mediaService.getPipelines({ pageSize: 100 });
      setPipelines(data.items || []);
    } catch {
      // Silently fail or ignore pipeline fetch errors
    }
  }, []);

  useEffect(() => {
    fetchPlans();
    fetchPipelines();
  }, [fetchPlans, fetchPipelines]);

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.title || !formData.scheduledDate) {
      toast.error('العنوان وتاريخ النشر حقول مطلوبة');
      return;
    }

    try {
      const res = await mediaService.createSocialPlan({
        title: formData.title,
        description: formData.description || undefined,
        script: formData.script || undefined,
        platform: formData.platform,
        status: formData.status,
        scheduledDate: new Date(formData.scheduledDate).toISOString(),
        mediaProductionPipelineId: formData.mediaProductionPipelineId || undefined
      });

      if (res.success) {
        toast.success('تمت جدولة خطة النشر بنجاح');
        setCreateOpen(false);
        setFormData({
          title: '',
          description: '',
          script: '',
          platform: 'YouTube',
          status: 'Draft',
          scheduledDate: '',
          mediaProductionPipelineId: ''
        });
        fetchPlans();
      } else {
        toast.error(res.message || 'فشلت جدولة خطة النشر');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'حدث خطأ أثناء الحفظ');
    }
  };

  // Calendar logic helpers
  const getDaysInMonth = (year: number, month: number) => new Date(year, month + 1, 0).getDate();
  const getFirstDayOfMonth = (year: number, month: number) => {
    const day = new Date(year, month, 1).getDay();
    // Adjust for Arabic week starting on Saturday or typical Sun-Sat calendar
    return day;
  };

  const year = currentDate.getFullYear();
  const month = currentDate.getMonth();
  const daysInMonth = getDaysInMonth(year, month);
  const firstDayIndex = getFirstDayOfMonth(year, month);

  const monthNamesArabic = [
    'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
    'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'
  ];

  const daysOfWeekArabic = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

  const handlePrevMonth = () => {
    setCurrentDate(new Date(year, month - 1, 1));
  };

  const handleNextMonth = () => {
    setCurrentDate(new Date(year, month + 1, 1));
  };

  // Generate calendar grid array
  const calendarCells = [];
  // Empty slots for previous month overflow
  for (let i = 0; i < firstDayIndex; i++) {
    calendarCells.push(null);
  }
  // Days of the month
  for (let d = 1; d <= daysInMonth; d++) {
    calendarCells.push(new Date(year, month, d));
  }

  // Filter plans for a specific date cell
  const getPlansForDate = (date: Date) => {
    return plans.filter(p => {
      const planDate = new Date(p.scheduledDate);
      return planDate.getDate() === date.getDate() &&
             planDate.getMonth() === date.getMonth() &&
             planDate.getFullYear() === date.getFullYear();
    });
  };

  return (
    <div dir="rtl" className="w-full">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
        <div>
          <h2 className="text-xl font-bold text-[var(--admin-text)]">مخطط النشر الرقمي</h2>
          <p className="text-sm text-[var(--admin-muted)] mt-1">جدولة وتنسيق المنشورات وتوزيع السكربتات ومتابعة حالة المونتاج المرتبطة بها.</p>
        </div>
        <NeumorphButton intent="primary" size="md" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4 ml-1" />
          جدولة منشور جديد
        </NeumorphButton>
      </div>

      {/* Calendar Controls */}
      <div className="flex items-center justify-between mb-6 p-4 rounded-3xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)] backdrop-blur-md">
        <div className="flex items-center gap-3">
          <CalendarIcon className="h-5 w-5 text-[var(--admin-primary)]" />
          <span className="font-bold text-lg text-[var(--admin-text)]">
            {monthNamesArabic[month]} {year}
          </span>
        </div>
        
        <div className="flex gap-2">
          <NeumorphButton intent="ghost" size="sm" onClick={handlePrevMonth}>
            <ChevronRight className="h-4 w-4" />
            الشهر السابق
          </NeumorphButton>
          <NeumorphButton intent="ghost" size="sm" onClick={handleNextMonth}>
            الشهر التالي
            <ChevronLeft className="h-4 w-4" />
          </NeumorphButton>
        </div>
      </div>

      {/* Calendar Grid */}
      <div className="rounded-[28px] border border-[var(--admin-border)] overflow-hidden bg-[var(--admin-card)] shadow-lg">
        {/* Days of Week Header */}
        <div className="grid grid-cols-7 border-b border-[var(--admin-border)] bg-[var(--admin-hover)] text-center py-3">
          {daysOfWeekArabic.map(day => (
            <span key={day} className="text-xs font-bold text-[var(--admin-text)]">{day}</span>
          ))}
        </div>

        {/* Days Grid */}
        <div className="grid grid-cols-7 divide-x divide-y divide-[var(--admin-border)] min-h-[500px]">
          {calendarCells.map((cell, idx) => {
            if (!cell) {
              return <div key={`empty-${idx}`} className="bg-[var(--admin-bg)]/30 min-h-[100px]" />;
            }

            const cellPlans = getPlansForDate(cell);
            const isToday = new Date().toDateString() === cell.toDateString();

            return (
              <div 
                key={cell.toString()} 
                className={`p-2 min-h-[100px] flex flex-col gap-1.5 transition-all ${
                  isToday ? 'bg-[var(--admin-primary-10)]/20 border-2 border-[var(--admin-primary)]' : 'hover:bg-[var(--admin-hover)]/30'
                }`}
              >
                {/* Date Number */}
                <span className={`text-xs font-bold w-6 h-6 flex items-center justify-center rounded-full ${
                  isToday ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-muted)]'
                }`}>
                  {cell.getDate()}
                </span>

                {/* Plans inside this day */}
                <div className="flex flex-col gap-1 overflow-y-auto max-h-[120px] no-scrollbar">
                  {cellPlans.map(plan => {
                    const platInfo = PLATFORM_INFOS[plan.platform] || PLATFORM_INFOS.YouTube;
                    const PlatIcon = platInfo.icon;

                    return (
                      <div 
                        key={plan.id}
                        className={`p-1.5 rounded-xl border border-[var(--admin-border)] flex flex-col gap-1 text-[10px] bg-[var(--admin-card)] hover:border-[var(--admin-primary-30)] transition-all`}
                        title={`${plan.title} - ${STATUS_LABELS[plan.status]}`}
                      >
                        <div className="flex items-center justify-between gap-1">
                          <span className="font-bold text-[9px] text-[var(--admin-text)] truncate">{plan.title}</span>
                          <span className={platInfo.color}>
                            <PlatIcon className="h-3 w-3" />
                          </span>
                        </div>

                        {/* Status & Link status info */}
                        <div className="flex flex-wrap items-center gap-1 justify-between pt-1 border-t border-[var(--admin-border)]/50">
                          <span className={`px-1 rounded font-semibold scale-90 ${STATUS_COLORS[plan.status]}`}>
                            {STATUS_LABELS[plan.status]}
                          </span>
                          
                          {plan.mediaProductionPipelineId && (
                            <span 
                              className={`px-1 bg-[var(--admin-primary-15)] text-[var(--admin-primary)] rounded font-semibold scale-90 flex items-center gap-0.5`}
                              title={`فيديو مرتبطة: ${plan.mediaProductionPipelineTitle} (${plan.mediaProductionPipelineStage})`}
                            >
                              <Link className="h-2 w-2" />
                              فيديو
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* CREATE MODAL */}
      {isCreateOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-lg rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-2xl animate-in fade-in" dir="rtl">
            <h3 className="text-lg font-bold text-[var(--admin-text)] mb-4">جدولة منشور وقناة نشر جديدة</h3>
            
            <form onSubmit={handleCreateSubmit} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">عنوان المنشور *</label>
                <input 
                  type="text" 
                  required
                  placeholder="مثال: إعلان مسابقة الجبر الكبرى"
                  value={formData.title}
                  onChange={e => setFormData({ ...formData, title: e.target.value })}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">تفاصيل أو وصف المنشور</label>
                <textarea 
                  placeholder="تفاصيل التنسيق..."
                  value={formData.description}
                  onChange={e => setFormData({ ...formData, description: e.target.value })}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none h-16 resize-none"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-bold text-[var(--admin-text)]">السيناريو / نص المنشور (Script)</label>
                <textarea 
                  placeholder="اكتب السكربت أو نص المنشور الذي سيتم قراءته أو نسخه هنا..."
                  value={formData.script}
                  onChange={e => setFormData({ ...formData, script: e.target.value })}
                  className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] placeholder-[var(--admin-muted)] outline-none h-24 resize-none font-mono"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">شبكة النشر الرقمية</label>
                  <select 
                    value={formData.platform}
                    onChange={e => setFormData({ ...formData, platform: e.target.value as SocialPlatform })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                  >
                    <option value="YouTube">YouTube</option>
                    <option value="Facebook">Facebook</option>
                    <option value="Instagram">Instagram</option>
                    <option value="TikTok">TikTok</option>
                    <option value="Telegram">Telegram</option>
                  </select>
                </div>

                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">تاريخ وتوقيت النشر الجدولة *</label>
                  <input 
                    type="datetime-local" 
                    required
                    value={formData.scheduledDate}
                    onChange={e => setFormData({ ...formData, scheduledDate: e.target.value })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">حالة التخطيط</label>
                  <select 
                    value={formData.status}
                    onChange={e => setFormData({ ...formData, status: e.target.value as SocialPlanStatus })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                  >
                    <option value="Draft">مسودة</option>
                    <option value="Scripting">كتابة سيناريو</option>
                    <option value="Scheduled">مجدول للنشر</option>
                    <option value="Published">منشور ومكتمل</option>
                  </select>
                </div>

                <div className="flex flex-col gap-1">
                  <label className="text-xs font-bold text-[var(--admin-text)]">ربط بمادة مرئية من لوحة الإنتاج</label>
                  <select 
                    value={formData.mediaProductionPipelineId}
                    onChange={e => setFormData({ ...formData, mediaProductionPipelineId: e.target.value })}
                    className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 py-2.5 text-sm text-[var(--admin-text)] outline-none"
                  >
                    <option value="">لا توجد مادة مرتبطة...</option>
                    {pipelines.map(p => (
                      <option key={p.id} value={p.id}>{p.title} ({STATUS_LABELS.Draft /* Just as text label holder */})</option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="flex justify-end gap-3 mt-4 border-t border-[var(--admin-border)] pt-4">
                <NeumorphButton type="button" intent="ghost" onClick={() => setCreateOpen(false)}>
                  إلغاء
                </NeumorphButton>
                <NeumorphButton type="submit" intent="primary">
                  حفظ الجدولة
                </NeumorphButton>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
