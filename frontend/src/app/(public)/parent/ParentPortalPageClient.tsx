'use client';

import { FormEvent, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { AlertTriangle, ArrowRight, BookOpen, CheckCircle2, ClipboardCheck, LockKeyhole, LogOut, TrendingUp, UserRound } from 'lucide-react';
import { parentService, type ParentAcademicDetails } from '@/services/parent-service';
import { getGradeLevelLabel } from '@/lib/academic-labels';

const TOKEN_KEY = 'parent-tracking-token';

function formatDate(value?: string | null) {
  if (!value) return '—';
  return new Intl.DateTimeFormat('ar-EG-u-nu-latn', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Cairo' }).format(new Date(value));
}

export default function ParentPortalPageClient() {
  const [trackingCode, setTrackingCode] = useState('');
  const [studentName, setStudentName] = useState('');
  const [details, setDetails] = useState<ParentAcademicDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  const loadDetails = async (token: string) => {
    setLoading(true);
    setError('');
    try {
      const nextDetails = await parentService.getStudentDetails(token);
      setDetails(nextDetails);
      setStudentName(nextDetails.studentName);
    } catch (cause) {
      sessionStorage.removeItem(TOKEN_KEY);
      setDetails(null);
      setError(cause instanceof Error ? cause.message : 'تعذر تحميل بيانات المتابعة.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const token = sessionStorage.getItem(TOKEN_KEY);
    if (token) void loadDetails(token);
    else setLoading(false);
  }, []);

  const handleVerify = async (event: FormEvent) => {
    event.preventDefault();
    const normalized = trackingCode.trim().toUpperCase();
    if (normalized.length !== 6) {
      setError('اكتب رمز المتابعة المكوّن من 6 خانات.');
      return;
    }

    setSubmitting(true);
    setError('');
    try {
      const verification = await parentService.verifyCode(normalized);
      sessionStorage.setItem(TOKEN_KEY, verification.token);
      setStudentName(verification.studentName);
      await loadDetails(verification.token);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'رمز المتابعة غير صحيح.');
    } finally {
      setSubmitting(false);
    }
  };

  const completion = useMemo(() => Math.max(0, Math.min(100, details?.attendance.completionRate ?? 0)), [details]);

  if (loading) {
    return <main className="grid min-h-screen place-items-center bg-[var(--landing-bg)] px-4 text-[var(--landing-ink)]"><span role="status" className="text-base font-bold">جاري فتح متابعة الطالب...</span></main>;
  }

  if (!details) {
    return (
      <main className="min-h-screen bg-[var(--landing-bg)] px-4 pb-6 pt-28 text-[var(--landing-ink)] sm:px-6 sm:pt-32">
        <div className="mx-auto flex w-full max-w-lg flex-col gap-10 pt-4 sm:pt-12">
          <Link href="/" className="inline-flex w-fit items-center gap-2 text-sm font-extrabold text-[var(--landing-ink)] hover:text-[var(--landing-accent)]"><ArrowRight className="h-4 w-4" /> العودة للرئيسية</Link>
          <section className="rounded-2xl bg-white p-6 shadow-sm sm:p-8" dir="rtl">
            <span className="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-[var(--landing-teal-soft)] text-[var(--landing-accent)]"><UserRound className="h-6 w-6" /></span>
            <h1 className="mt-5 text-balance text-3xl font-black leading-tight">متابعة ولي الأمر</h1>
            <p className="mt-3 text-pretty text-sm font-semibold leading-7 text-[var(--landing-muted)]">أدخل رمز المتابعة الخاص بالطالب لمراجعة الدروس والاختبارات والواجبات والتنبيهات.</p>
            <form onSubmit={handleVerify} className="mt-7 space-y-4">
              <label className="block text-sm font-black" htmlFor="parent-tracking-code">رمز متابعة ولي الأمر</label>
              <input id="parent-tracking-code" aria-describedby={error ? 'parent-tracking-error parent-tracking-help' : 'parent-tracking-help'} aria-invalid={Boolean(error)} autoComplete="one-time-code" inputMode="text" maxLength={6} value={trackingCode} onChange={(event) => setTrackingCode(event.target.value.replace(/[^a-zA-Z0-9]/g, '').toUpperCase())} placeholder="مثال: A1B2C3" className="min-h-12 w-full rounded-xl border border-[var(--landing-line)] bg-[var(--landing-bg)] px-4 py-3 text-center text-xl font-black tracking-[0.24em] uppercase outline-none transition focus:border-[var(--landing-accent)] focus:ring-2 focus:ring-[var(--landing-accent)]/20" />
              {error && <p id="parent-tracking-error" role="alert" className="rounded-xl bg-[var(--admin-danger-10)] px-3 py-2 text-sm font-bold leading-6 text-[var(--admin-danger)]">{error}</p>}
              <button type="submit" disabled={submitting} className="landing-primary-button w-full justify-center disabled:cursor-not-allowed disabled:opacity-60"><LockKeyhole className="h-5 w-5" />{submitting ? 'جاري التحقق...' : 'عرض متابعة الطالب'}</button>
            </form>
            <p id="parent-tracking-help" className="mt-5 text-sm font-semibold leading-7 text-[var(--landing-muted)]">ستجد الرمز في حساب الطالب، ويمكنه مشاركته مع ولي الأمر.</p>
          </section>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-[var(--landing-bg)] px-4 pb-6 pt-28 text-[var(--landing-ink)] sm:px-6 sm:pt-32" dir="rtl">
      <div className="mx-auto max-w-6xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-4 border-b border-[var(--landing-line)] pb-5">
          <div><p className="text-sm font-bold text-[var(--landing-muted)]">متابعة ولي الأمر</p><h1 className="mt-1 text-2xl font-black">{studentName}</h1>{details.grade && <p className="mt-1 text-sm font-semibold text-[var(--landing-muted)]">{getGradeLevelLabel(details.grade)}{details.school ? `، ${details.school}` : ''}</p>}</div>
          <button type="button" onClick={() => { sessionStorage.removeItem(TOKEN_KEY); setDetails(null); setTrackingCode(''); }} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--landing-line)] bg-white px-4 py-2.5 text-sm font-bold transition-colors hover:bg-[var(--landing-bg-soft)] focus-visible:ring-2 focus-visible:ring-[var(--landing-accent)]"><LogOut className="h-4 w-4" />تغيير الطالب</button>
        </header>

        <section className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
          <div className="rounded-2xl bg-[var(--landing-ink)] p-6 text-white sm:p-8"><div className="flex items-start justify-between gap-4"><div><p className="text-sm font-bold text-white/75">تقدم الدروس</p><p className="mt-2 text-4xl font-black">{Math.round(completion)}%</p><p className="mt-2 text-sm font-semibold text-white/75">{details.attendance.watchedLessons} من {details.attendance.totalLessons} درس تمت متابعته</p></div><TrendingUp className="h-8 w-8 text-[var(--accent)]" /></div><div className="mt-7 h-3 overflow-hidden rounded-full bg-white/20"><div className="h-full rounded-full bg-[var(--landing-accent)] transition-[width] duration-500" style={{ width: `${completion}%` }} /></div></div>
          <div className="rounded-2xl bg-white p-6 shadow-sm"><p className="text-sm font-bold text-[var(--landing-muted)]">الرصيد المتاح</p><p className="mt-3 text-3xl font-black text-[var(--landing-accent)]">{new Intl.NumberFormat('ar-EG-u-nu-latn', { maximumFractionDigits: 2 }).format(details.balance.currentBalance)} ج.م</p><p className="mt-4 text-xs font-semibold leading-6 text-[var(--landing-muted)]">البيانات المعروضة للمتابعة فقط، ولا يمكن تنفيذ عمليات شراء من هذه الصفحة.</p></div>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-2xl bg-white p-6 shadow-sm"><h2 className="flex items-center gap-2 text-lg font-black"><BookOpen className="h-5 w-5 text-[var(--landing-accent)]" />الكورسات المسجلة</h2><div className="mt-5 space-y-4">{details.courses.length ? details.courses.map((course) => <div key={course.packageId} className="border-b border-[var(--landing-line)] pb-4 last:border-0 last:pb-0"><p className="font-black">{course.packageName}</p><p className="mt-1 text-sm font-semibold text-[var(--landing-muted)]">{course.teacherName}</p><p className="mt-2 text-xs font-bold text-[var(--landing-muted)]">{course.terms.map((term) => term.termTitle).join('، ') || 'لا توجد ترمات مفعلة'}</p></div>) : <p className="py-5 text-sm font-bold text-[var(--landing-muted)]">لا توجد كورسات مفعلة حاليًا.</p>}</div></div>
          <div className="rounded-2xl bg-white p-6 shadow-sm"><h2 className="flex items-center gap-2 text-lg font-black"><ClipboardCheck className="h-5 w-5 text-[var(--landing-accent)]" />آخر الاختبارات</h2><div className="mt-5 space-y-4">{details.exams.slice(0, 5).map((exam) => <div key={exam.examId} className="flex items-start justify-between gap-4 border-b border-[var(--landing-line)] pb-4 last:border-0 last:pb-0"><div><p className="font-black">{exam.examTitle}</p><p className="mt-1 text-xs font-bold text-[var(--landing-muted)]">{exam.packageName}، {exam.termTitle}</p><p className="mt-1 text-xs font-semibold text-[var(--landing-muted)]">{formatDate(exam.submittedAt)}</p></div><span className="rounded-lg bg-[var(--landing-teal-soft)] px-2.5 py-1 text-sm font-black text-[var(--landing-marker-text)]">{Math.round(exam.percentage)}%</span></div>)}{details.exams.length === 0 && <p className="py-5 text-sm font-bold text-[var(--landing-muted)]">لا توجد اختبارات مكتملة بعد.</p>}</div></div>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-2xl bg-white p-6 shadow-sm"><h2 className="flex items-center gap-2 text-lg font-black"><CheckCircle2 className="h-5 w-5 text-[var(--landing-accent)]" />متابعة الحصص</h2><div className="mt-5 space-y-3">{details.watchLessons.slice(0, 6).map((lesson) => <div key={lesson.lessonId} className="flex items-center justify-between gap-4"><div><p className="font-bold">{lesson.lessonTitle}</p><p className="mt-1 text-xs font-semibold text-[var(--landing-muted)]">{lesson.packageName}، {lesson.termTitle}</p></div><span className={`text-xs font-black ${lesson.isCompleted ? 'text-emerald-700' : 'text-amber-700'}`}>{lesson.isCompleted ? 'مكتملة' : `${lesson.watchedVideos}/${lesson.totalVideos} فيديو`}</span></div>)}{details.watchLessons.length === 0 && <p className="py-5 text-sm font-bold text-[var(--landing-muted)]">لا توجد حصص تمت متابعتها بعد.</p>}</div></div>
          <div className="rounded-2xl bg-white p-6 shadow-sm"><h2 className="flex items-center gap-2 text-lg font-black"><AlertTriangle className="h-5 w-5 text-amber-600" />التنبيهات</h2><div className="mt-5 space-y-3">{details.warnings.slice(0, 5).map((warning, index) => <div key={`${warning.createdAt}-${index}`} className="border-b border-[var(--landing-line)] pb-3 last:border-0 last:pb-0"><p className="font-bold">{warning.reason}</p><p className="mt-1 text-xs font-semibold text-[var(--landing-muted)]">{formatDate(warning.createdAt)}</p></div>)}{details.warnings.length === 0 && <p className="py-5 text-sm font-bold text-emerald-700">لا توجد تنبيهات حالية.</p>}</div></div>
        </section>
      </div>
    </main>
  );
}
