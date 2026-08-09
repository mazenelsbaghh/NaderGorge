'use client';

import { useEffect, useState, type CSSProperties } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import {
  ArrowLeft,
  ArrowRight,
  Award,
  BookOpen,
  GraduationCap,
  Layers3,
  MessageCircle,
  PackageOpen,
  PlayCircle,
  Phone,
  Send,
  Star,
  Video,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { studentService, type PublicTeacherDetailDto } from '@/services/student-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { PublicVideoPlayer } from '@/components/video/PublicVideoPlayer';
import { TeacherCommunityPanel } from './TeacherCommunityPanel';

type TeacherPublicProfilePageClientProps = {
  teacherId: string;
  visitor?: boolean;
};

function contentName(item: { name?: string; title?: string }) {
  return item.name || item.title || 'محتوى بدون اسم';
}

function formatPrice(price?: number) {
  return typeof price === 'number' ? `${price.toLocaleString('ar-EG-u-nu-latn')} ج` : 'متاح';
}

function safeExternalUrl(url?: string) {
  if (!url) return null;
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'https:' || parsed.protocol === 'http:' ? parsed.toString() : null;
  } catch {
    return null;
  }
}

export default function TeacherPublicProfilePageClient({ teacherId, visitor = false }: TeacherPublicProfilePageClientProps) {
  const [teacher, setTeacher] = useState<PublicTeacherDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'content' | 'community'>('content');

  useEffect(() => {
    if (!teacherId || teacherId === 'undefined') {
      setTeacher(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    studentService.getPublicTeacherDetail(teacherId)
      .then(setTeacher)
      .catch(() => toast.error('تعذر تحميل بروفايل المدرس'))
      .finally(() => setLoading(false));
  }, [teacherId]);

  if (loading) {
    return <div className="h-96 animate-pulse rounded-lg bg-[var(--student-card-strong)]" />;
  }

  if (!teacher) {
    return (
      <div className="rounded-lg border border-[var(--student-border)] bg-[var(--student-card)] p-10 text-center">
        <p className="font-black text-[var(--student-text)]">المدرس غير موجود.</p>
        <Link href={visitor ? '/' : '/student/teachers'} className="mt-4 inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--student-primary)] px-4 text-sm font-black text-white">
          <ArrowRight className="h-4 w-4" /> عودة للمدرسين
        </Link>
      </div>
    );
  }

  const resolvedImage = teacher.profileImageUrl ? resolveMediaUrl(teacher.profileImageUrl) : '';
  const displayName = teacher.fullName || teacher.displayName || 'مدرس';
  const subjects = teacher.subjectNames?.length ? teacher.subjectNames : teacher.subjects?.map((subject) => subject.name) ?? [];
  const packagesCount = teacher.packages.length;
  const sharedPackagesCount = teacher.sharedPackages.length;
  const lessonsCount = teacher.lessons.length;
  const firstPackageHref = teacher.packages[0]?.id
    ? visitor ? `/packages/${teacher.packages[0].id}` : `/student/packages/${teacher.packages[0].id}`
    : '/student/shared-packages';
  const socialLinks = [
    { label: 'فيسبوك', url: safeExternalUrl(teacher.facebookUrl) },
    { label: 'يوتيوب', url: safeExternalUrl(teacher.youtubeUrl) },
    { label: 'تليجرام', url: safeExternalUrl(teacher.telegramUrl) },
  ].filter((link): link is { label: string; url: string } => Boolean(link.url));
  const hasContactDetails = Boolean(teacher.contactInfo || teacher.assistantPhoneNumbers || socialLinks.length);

  const visitorTheme = visitor ? {
    '--student-bg': '#F6F7F8',
    '--student-text': '#0A1D3D',
    '--student-muted': '#2E3A47',
    '--student-primary': '#0A1D3D',
    '--student-primary-strong': '#021f45',
    '--student-card': '#FFFFFF',
    '--student-card-soft': '#EEF1F4',
    '--student-card-strong': '#DCE1E6',
    '--student-border': '#DCE1E6',
  } as CSSProperties : undefined;

  return (
    <div
      className={`min-h-screen bg-[#F6F7F8] px-4 py-5 sm:px-6 sm:py-8 ${visitor ? 'public-page-scroll' : ''}`}
      style={visitorTheme}
    >
    <div className="mx-auto max-w-6xl space-y-7 pb-10">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link href={visitor ? '/' : '/student/teachers'} className="inline-flex h-11 items-center gap-2 rounded-lg border border-[var(--student-border)] bg-[var(--student-card)] px-4 text-sm font-black text-[var(--student-text)] transition hover:border-[var(--student-primary)] hover:text-[var(--student-primary)]">
          <ArrowRight className="h-4 w-4" /> {visitor ? 'العودة للرئيسية' : 'عودة للمدرسين'}
        </Link>
        {teacher.introVideoUrl ? (
          <a href="#intro-video" className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--student-primary)] px-4 text-sm font-black text-white transition hover:opacity-90">
            <PlayCircle className="h-4 w-4" /> شاهد التعريف
          </a>
        ) : null}
      </div>

      <section className="overflow-hidden rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)]">
        <div className="relative min-h-[360px] bg-[#0A1D3D] text-white">
          {resolvedImage ? (
            <Image src={resolvedImage} alt="" fill className="object-cover opacity-22" sizes="100vw" priority />
          ) : null}
          <div className="absolute inset-0 bg-[linear-gradient(90deg,rgba(10,29,61,0.98)_0%,rgba(10,29,61,0.92)_48%,rgba(10,29,61,0.70)_100%)]" />
          <div className="absolute inset-x-0 bottom-0 h-px bg-[#D4A017]" />

          <div className="relative p-5 sm:p-7 lg:p-9">
            <div className="grid items-start gap-4 grid-cols-[minmax(0,1fr)_108px] sm:grid-cols-[minmax(0,1fr)_136px] sm:gap-6 lg:min-h-[320px] lg:grid-cols-[minmax(0,1fr)_280px] lg:items-center lg:gap-8">
            <div className="space-y-5">
              <div className="space-y-5">
                <div className="inline-flex w-fit items-center gap-2 rounded-full bg-white/10 px-3 py-1.5 text-xs font-black text-white">
                  <GraduationCap className="h-4 w-4 text-[#D4A017]" />
                  بروفايل مدرس
                </div>

                <div className="max-w-3xl">
                  <h1 className="text-3xl font-black leading-tight text-white sm:text-4xl">أ. {displayName}</h1>
                  <p className="mt-4 max-w-2xl text-sm font-bold leading-8 text-white/82 sm:text-base">
                    {teacher.bio || 'مدرس على منصة مسار. تابع الباقات والحصص المتاحة واختر خطوتك التالية بوضوح.'}
                  </p>
                </div>

                {subjects.length > 0 ? (
                  <div className="flex flex-wrap gap-2">
                    {subjects.map((subject) => (
                      <span key={subject} className="inline-flex min-h-9 items-center rounded-full bg-white px-3 text-xs font-black text-[#0A1D3D]">
                        {subject}
                      </span>
                    ))}
                  </div>
                ) : null}
              </div>

            </div>

            <div className="self-start lg:self-center">
              <div className="relative aspect-[4/5] w-full max-w-[108px] overflow-hidden rounded-xl border border-white/25 bg-white/8 sm:max-w-[136px] lg:mx-auto lg:max-h-[320px] lg:max-w-[250px] lg:rounded-2xl">
                {resolvedImage ? (
                  <Image src={resolvedImage} alt={displayName} fill className="object-cover" sizes="(max-width: 640px) 108px, (max-width: 1024px) 136px, 280px" priority />
                ) : (
                  <div className="grid h-full place-items-center bg-[linear-gradient(135deg,#0E8F8F,#0A1D3D)] text-4xl font-black text-white lg:text-7xl">
                    {displayName.charAt(0)}
                  </div>
                )}
              </div>
            </div>
            </div>

            <div className="mt-6 flex flex-col gap-3 sm:flex-row">
              <Link href={firstPackageHref} className="inline-flex h-12 flex-1 items-center justify-center gap-2 rounded-lg bg-[#0E8F8F] px-5 text-sm font-black text-white transition hover:bg-[#0b7b7b]">
                ابدأ من المحتوى المتاح <ArrowLeft className="h-4 w-4" />
              </Link>
              <a href="#teacher-content" className="inline-flex h-12 flex-1 items-center justify-center gap-2 rounded-lg border border-white/30 px-5 text-sm font-black text-white transition hover:bg-white/10">
                تصفح الباقات والحصص
              </a>
            </div>

            {teacher.introVideoUrl ? (
              <div id="intro-video" className="-mx-5 mt-5 overflow-hidden rounded-xl border border-white/20 bg-black sm:-mx-7 sm:mt-6 lg:mx-auto lg:max-w-4xl">
                <PublicVideoPlayer url={teacher.introVideoUrl} title={`الفيديو التعريفي للأستاذ ${displayName}`} />
              </div>
            ) : null}
          </div>
        </div>

        <div className="grid gap-px bg-[var(--student-border)] sm:grid-cols-4">
          <ProfileMetric icon={Star} label="التقييم" value={teacher.ratingAverage?.toFixed(1) ?? '0.0'} tone="gold" />
          <ProfileMetric icon={PackageOpen} label="باقات المدرس" value={packagesCount} />
          <ProfileMetric icon={Layers3} label="باكدجات مشتركة" value={sharedPackagesCount} />
          <ProfileMetric icon={Video} label="حصص للمعاينة" value={lessonsCount} />
        </div>
      </section>

      {hasContactDetails ? (
        <section className="rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-5">
          <div className="flex items-start gap-3">
            <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-[#0E8F8F]/10 text-[#0E8F8F]"><Phone className="h-5 w-5" /></span>
            <div className="min-w-0">
              <h2 className="text-lg font-black text-[var(--student-text)]">تواصل مع فريق الأستاذ {displayName}</h2>
              <p className="mt-1 text-sm font-bold leading-7 text-[var(--student-muted)]">للاستفسارات والدعم قبل أو بعد الاشتراك.</p>
            </div>
          </div>
          <div className="mt-5 grid gap-3 md:grid-cols-2">
            {teacher.contactInfo ? <ContactDetail label="التواصل المباشر" value={teacher.contactInfo} /> : null}
            {teacher.assistantPhoneNumbers ? <ContactDetail label="أرقام المساعدين" value={teacher.assistantPhoneNumbers} /> : null}
          </div>
          {socialLinks.length > 0 ? <div className="mt-4 flex flex-wrap gap-2">
            {socialLinks.map(({ label, url }) => <a key={label} href={url} target="_blank" rel="noreferrer" className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--student-border)] px-4 text-sm font-black text-[var(--student-text)] transition hover:border-[#0E8F8F] hover:text-[#0E8F8F]"><Send className="h-4 w-4" /> {label}</a>)}
          </div> : null}
        </section>
      ) : null}

      {!visitor ? <div id="teacher-content" className="rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-2">
        <div className="grid gap-2 sm:grid-cols-2">
          <button
            type="button"
            onClick={() => setActiveTab('content')}
            className={`flex min-h-12 items-center justify-center gap-2 rounded-xl px-4 text-sm font-black transition ${
              activeTab === 'content'
                ? 'bg-[#0E8F8F] text-white shadow-sm'
                : 'text-[var(--student-muted)] hover:bg-[var(--student-card-soft)] hover:text-[var(--student-text)]'
            }`}
          >
            <PackageOpen className="h-4 w-4" />
            المحتوى والباقات
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('community')}
            className={`flex min-h-12 items-center justify-center gap-2 rounded-xl px-4 text-sm font-black transition ${
              activeTab === 'community'
                ? 'bg-[#0A1D3D] text-white shadow-sm'
                : 'text-[var(--student-muted)] hover:bg-[var(--student-card-soft)] hover:text-[var(--student-text)]'
            }`}
          >
            <MessageCircle className="h-4 w-4" />
            مجتمع المدرس
          </button>
        </div>
      </div> : null}

      {visitor || activeTab === 'content' ? (
        <section className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
          <div className="space-y-5">
            <ContentList
              title="باقات المدرس"
              subtitle="المسارات الأساسية لهذا المدرس على مسار."
              icon={PackageOpen}
              items={teacher.packages}
              baseHref="/student/packages"
              emptyTitle="لا توجد باقات منشورة حالياً."
              showImages
              visitor={visitor}
              publicBaseHref="/packages"
              returnHref={`/student/teachers/${teacherId}`}
            />
            <ContentList
              title="حصص للمعاينة والتصفح"
              subtitle="نماذج وحصص متاحة لتكوين فكرة عن طريقة الشرح."
              icon={Video}
              items={teacher.lessons}
              baseHref="/student/lessons"
              emptyTitle="لا توجد حصص معاينة حالياً."
              visitor={visitor}
            />
          </div>

          <aside className="space-y-5">
            <ContentList
              title="الباكدجات المشتركة"
              subtitle="محتوى مشترك يظهر ضمن باكدجات عامة للمنصة."
              icon={Layers3}
              items={teacher.sharedPackages}
              baseHref="/student/shared-packages"
              emptyTitle="لا توجد باكدجات مشتركة لهذا المدرس."
              compact
              showImages
              visitor={visitor}
            />
            <section className="rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-5">
              <div className="flex items-start gap-3">
                <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-[#0E8F8F]/10 text-[#0E8F8F]">
                  <Award className="h-5 w-5" />
                </span>
                <div>
                  <h2 className="text-base font-black text-[var(--student-text)]">خطوتك التالية</h2>
                  <p className="mt-2 text-sm font-bold leading-7 text-[var(--student-muted)]">
                    ابدأ بباقة كاملة لو هدفك متابعة منتظمة، أو افتح حصة معاينة لو عايز تشوف طريقة الشرح أولاً.
                  </p>
                </div>
              </div>
            </section>
          </aside>
        </section>
      ) : (
        <TeacherCommunityPanel teacherId={teacher.id || teacher.teacherId || teacherId} />
      )}
    </div>
    </div>
  );
}

function ContactDetail({ label, value }: { label: string; value: string }) {
  return <div className="rounded-xl bg-[var(--student-card-soft)] px-4 py-3">
    <p className="text-xs font-black text-[var(--student-muted)]">{label}</p>
    <p className="mt-1 whitespace-pre-line text-sm font-black leading-7 text-[var(--student-text)]">{value}</p>
  </div>;
}

function ProfileMetric({
  icon: Icon,
  label,
  value,
  tone = 'teal',
}: {
  icon: typeof Star;
  label: string;
  value: string | number;
  tone?: 'teal' | 'gold';
}) {
  return (
    <div className="flex items-center gap-3 bg-[var(--student-card)] p-4">
      <span className={`grid h-11 w-11 place-items-center rounded-xl ${tone === 'gold' ? 'bg-[#D4A017]/12 text-[#9b7200]' : 'bg-[#0E8F8F]/10 text-[#0E8F8F]'}`}>
        <Icon className="h-5 w-5" />
      </span>
      <div>
        <p className="text-xs font-bold text-[var(--student-muted)]">{label}</p>
        <p className="mt-1 text-xl font-black text-[var(--student-text)]">{value}</p>
      </div>
    </div>
  );
}

function ContentList({
  title,
  subtitle,
  icon: Icon,
  items,
  baseHref,
  emptyTitle,
  compact = false,
  showImages = false,
  visitor = false,
  publicBaseHref,
  returnHref,
}: {
  title: string;
  subtitle: string;
  icon: typeof BookOpen;
  items: Array<{ id: string; name?: string; title?: string; price?: number; imageUrl?: string | null }>;
  baseHref?: string;
  emptyTitle: string;
  compact?: boolean;
  showImages?: boolean;
  visitor?: boolean;
  publicBaseHref?: string;
  returnHref?: string;
}) {
  return (
    <section className="rounded-2xl border border-[var(--student-border)] bg-[var(--student-card)] p-5">
      <div className="mb-5 flex items-start justify-between gap-4">
        <div>
          <h2 className="flex items-center gap-2 text-lg font-black text-[var(--student-text)]">
            <Icon className="h-5 w-5 text-[#0E8F8F]" /> {title}
          </h2>
          <p className="mt-1 text-sm font-bold leading-7 text-[var(--student-muted)]">{subtitle}</p>
        </div>
        <span className="rounded-full bg-[var(--student-card-soft)] px-3 py-1 text-xs font-black text-[var(--student-muted)]">
          {items.length}
        </span>
      </div>

      <div className={`grid gap-3 ${compact ? '' : 'sm:grid-cols-2'}`}>
        {items.map((item) => {
          const itemImage = item.imageUrl ? resolveMediaUrl(item.imageUrl) : '';
          const content = (
            <article className="group overflow-hidden rounded-xl border border-[var(--student-border)] bg-[var(--student-card-soft)] transition hover:border-[#0E8F8F] hover:bg-[var(--student-card)]">
              {showImages ? (
                <div className="relative aspect-[16/7] overflow-hidden bg-[#0A1D3D]">
                  {itemImage ? (
                    <Image src={itemImage} alt={contentName(item)} fill className="object-cover transition duration-200 group-hover:scale-105" sizes={compact ? '360px' : '520px'} />
                  ) : (
                    <div className="flex h-full items-center justify-between bg-[linear-gradient(135deg,#0A1D3D,#0E8F8F)] px-5 text-white">
                      <BookOpen className="h-8 w-8 opacity-80" />
                      <span className="text-3xl font-black opacity-30">{contentName(item).charAt(0)}</span>
                    </div>
                  )}
                  <div className="absolute inset-x-0 bottom-0 h-16 bg-[linear-gradient(180deg,transparent,rgba(10,29,61,0.72))]" />
                </div>
              ) : null}
              <div className="flex min-h-28 flex-col justify-between gap-4 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h3 className="line-clamp-2 text-sm font-black leading-6 text-[var(--student-text)]">{contentName(item)}</h3>
                    <p className="mt-1 flex items-center gap-1 text-xs font-bold text-[var(--student-muted)]"><GraduationCap className="h-3.5 w-3.5" /> محتوى تابع للمدرس</p>
                  </div>
                  <BookOpen className="h-4 w-4 shrink-0 text-[#0E8F8F]" />
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm font-black text-[var(--student-text)]">{formatPrice(item.price)}</span>
                  <span className="inline-flex items-center gap-1 text-xs font-black text-[#0E8F8F]">
                    عرض التفاصيل <ArrowLeft className="h-3.5 w-3.5 transition group-hover:-translate-x-1" />
                  </span>
                </div>
              </div>
            </article>
          );
          const href = visitor
            ? publicBaseHref ? `${publicBaseHref}/${item.id}` : '/register'
            : baseHref === '/student/shared-packages' ? baseHref : baseHref ? `${baseHref}/${item.id}${returnHref ? `?returnTo=${encodeURIComponent(returnHref)}` : ''}` : null;
          return href ? <Link key={item.id} href={href}>{content}</Link> : <div key={item.id}>{content}</div>;
        })}
        {items.length === 0 && (
          <div className="rounded-xl border border-dashed border-[var(--student-border)] bg-[var(--student-card-soft)] p-6 text-center">
            <MessageCircle className="mx-auto mb-3 h-8 w-8 text-[#0E8F8F]" />
            <p className="text-sm font-black text-[var(--student-text)]">{emptyTitle}</p>
            <p className="mt-1 text-xs font-bold text-[var(--student-muted)]">ارجع لاحقاً أو تابع المدرس من صفحة المدرسين.</p>
          </div>
        )}
      </div>
    </section>
  );
}
