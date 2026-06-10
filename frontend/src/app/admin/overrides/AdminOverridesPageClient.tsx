'use client';

import { devConsole } from '@/utils/dev-console';
import { FormEvent, useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { LockOpen, RefreshCcw, ShieldAlert } from 'lucide-react';

import { 
  AdminShellChrome,
  AdminStatCard 
} from '@/components/admin';
import { adminService } from '@/services/admin-service';
import { ContentSectionDto, LessonSummaryDto, PackageDto, VideoDto, contentService } from '@/services/content-service';
import NeumorphButton from '@/components/ui/neumorph-button';

interface UserItem {
  id: string;
  fullName: string;
  phoneNumber: string;
}

function getErrorMessage(error: unknown, fallback: string) {
  if (
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    typeof (error as { response?: unknown }).response === 'object' &&
    (error as { response?: { data?: unknown } }).response !== null
  ) {
    const response = (error as { response?: { data?: { message?: unknown } } }).response;
    if (typeof response?.data?.message === 'string') {
      return response.data.message;
    }
  }

  return fallback;
}

export default function AdminOverridesPageClient() {
  const [packages, setPackages] = useState<PackageDto[]>([]);
  const [students, setStudents] = useState<UserItem[]>([]);

  const [wPkgId, setWPkgId] = useState('');
  const [wSecId, setWSecId] = useState('');
  const [wLesId, setWLesId] = useState('');
  const [wVideoId, setWVideoId] = useState('');
  const [wStudentId, setWStudentId] = useState('');
  const [wSections, setWSections] = useState<ContentSectionDto[]>([]);
  const [wLessons, setWLessons] = useState<LessonSummaryDto[]>([]);
  const [wVideos, setWVideos] = useState<VideoDto[]>([]);
  const [wLoading, setWLoading] = useState(false);
  const [wResult, setWResult] = useState<string | null>(null);

  const [uPkgId, setUPkgId] = useState('');
  const [uSecId, setUSecId] = useState('');
  const [uLessonId, setULessonId] = useState('');
  const [uStudentId, setUStudentId] = useState('');
  const [uSections, setUSections] = useState<ContentSectionDto[]>([]);
  const [uLessons, setULessons] = useState<LessonSummaryDto[]>([]);
  const [uLoading, setULoading] = useState(false);
  const [uResult, setUResult] = useState<string | null>(null);

  useEffect(() => {
    void loadSharedData();
  }, []);

  async function loadSharedData() {
    try {
      const [packagesResponse, usersResponse] = await Promise.all([
        contentService.getPackages(),
        adminService.listUsers(1, 1000, ''),
      ]);
      setPackages((packagesResponse.data?.data || []) as PackageDto[]);
      setStudents((usersResponse?.items || []) as UserItem[]);
    } catch (error) {
      devConsole.error(error);
    }
  }

  async function handleWPkgChange(pkgId: string) {
    setWPkgId(pkgId);
    setWSecId('');
    setWLesId('');
    setWVideoId('');
    setWLessons([]);
    setWVideos([]);
    if (!pkgId) return setWSections([]);

    try {
      const response = await contentService.getSections(pkgId);
      setWSections((response.data?.data || []) as ContentSectionDto[]);
    } catch {
      setWSections([]);
    }
  }

  async function handleWSecChange(secId: string) {
    setWSecId(secId);
    setWLesId('');
    setWVideoId('');
    setWVideos([]);
    if (!secId) return setWLessons([]);

    try {
      const response = await contentService.getLessons(secId);
      setWLessons((response.data?.data || []) as LessonSummaryDto[]);
    } catch {
      setWLessons([]);
    }
  }

  async function handleWLesChange(lessonId: string) {
    setWLesId(lessonId);
    setWVideoId('');
    if (!lessonId) return setWVideos([]);

    try {
      const response = await contentService.getLessonDetail(lessonId);
      setWVideos((response.data?.data?.videos || []) as VideoDto[]);
    } catch {
      setWVideos([]);
    }
  }

  async function handleUPkgChange(pkgId: string) {
    setUPkgId(pkgId);
    setUSecId('');
    setULessonId('');
    setULessons([]);
    if (!pkgId) return setUSections([]);

    try {
      const response = await contentService.getSections(pkgId);
      setUSections((response.data?.data || []) as ContentSectionDto[]);
    } catch {
      setUSections([]);
    }
  }

  async function handleUSecChange(secId: string) {
    setUSecId(secId);
    setULessonId('');
    if (!secId) return setULessons([]);

    try {
      const response = await contentService.getLessons(secId);
      setULessons((response.data?.data || []) as LessonSummaryDto[]);
    } catch {
      setULessons([]);
    }
  }

  async function handleResetWatch(event: FormEvent) {
    event.preventDefault();
    setWLoading(true);
    setWResult(null);

    try {
      const response = await adminService.resetWatchLimit(wVideoId, wStudentId);
      setWResult(response.message || 'تم تصفير حد المشاهدة');
    } catch (error) {
      setWResult(getErrorMessage(error, 'تعذر تنفيذ العملية'));
    } finally {
      setWLoading(false);
    }
  }

  async function handleUnlockLesson(event: FormEvent) {
    event.preventDefault();
    setULoading(true);
    setUResult(null);

    try {
      const response = await adminService.manualUnlockLesson(uLessonId, uStudentId);
      setUResult(response.message || 'تم فتح الدرس');
    } catch (error) {
      setUResult(getErrorMessage(error, 'تعذر تنفيذ العملية'));
    } finally {
      setULoading(false);
    }
  }

  return (
    <AdminShellChrome
      activePath="/admin/overrides"
      sectionLabel="التعديلات اليدوية"
      pageTitle="أدوات الدعم الإداري"
      subtitle="تصفير المشاهدة وفتح الدروس يدويًا مع الحفاظ على سجل واضح."
    >
      <section className="mb-12 grid grid-cols-1 gap-6 md:grid-cols-3">
        <AdminStatCard
          variant="light"
          icon={RefreshCcw}
          label="المشاهدات"
          value={wVideos.length}
          subtitle="فيديوهات متاحة لإعادة الضبط"
        />
        <AdminStatCard
          variant="accent"
          icon={LockOpen}
          label="الدروس المغلقة"
          value={uLessons.length}
          subtitle="دروس قابلة للفتح اليدوي"
        />
        <AdminStatCard
          variant="muted"
          icon={ShieldAlert}
          label="الطلاب"
          value={students.length}
          subtitle="متاح تطبيق التعديلات عليهم"
        >
          <div className="absolute inset-x-0 bottom-0 h-1 bg-gradient-to-r from-transparent via-rose-500/20 to-transparent" />
        </AdminStatCard>
      </section>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        <motion.section initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="admin-panel">
          <div className="mb-6 flex items-center gap-3">
            <div className="admin-badge admin-badge--pill">
              <RefreshCcw className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-[var(--admin-text)]">تصفير حد مشاهدة الفيديو</h2>
              <p className="text-sm text-[var(--admin-muted)]">إزالة سجل المشاهدة لطالب وفيديو محدد.</p>
            </div>
          </div>

          <form onSubmit={handleResetWatch} className="space-y-4">
            <select value={wPkgId} onChange={(event) => handleWPkgChange(event.target.value)} className="admin-input">
              <option value="">اختر الباقة</option>
              {packages.map((pkg) => (
                <option key={pkg.id} value={pkg.id}>{pkg.name}</option>
              ))}
            </select>
            <select value={wSecId} onChange={(event) => handleWSecChange(event.target.value)} disabled={!wPkgId} className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:ring-2 focus:ring-[var(--admin-primary)] disabled:opacity-50">
              <option value="">اختر القسم</option>
              {wSections.map((section) => (
                <option key={section.id} value={section.id}>{section.order}. {section.title}</option>
              ))}
            </select>
            <select value={wLesId} onChange={(event) => handleWLesChange(event.target.value)} disabled={!wSecId} className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:ring-2 focus:ring-[var(--admin-primary)] disabled:opacity-50">
              <option value="">اختر الدرس</option>
              {wLessons.map((lesson) => (
                <option key={lesson.id} value={lesson.id}>{lesson.order}. {lesson.title}</option>
              ))}
            </select>
            <select value={wVideoId} onChange={(event) => setWVideoId(event.target.value)} disabled={!wLesId} required className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:ring-2 focus:ring-[var(--admin-primary)] disabled:opacity-50">
              <option value="">اختر الفيديو</option>
              {wVideos.map((video) => (
                <option key={video.id} value={video.id}>{video.title}</option>
              ))}
            </select>
            <select value={wStudentId} onChange={(event) => setWStudentId(event.target.value)} required className="admin-input">
              <option value="">اختر الطالب</option>
              {students.map((student) => (
                <option key={student.id} value={student.id}>{student.fullName} ({student.phoneNumber})</option>
              ))}
            </select>

            <div className="flex items-center justify-between gap-3 pt-4 border-t border-[var(--admin-border)]">
              <NeumorphButton type="submit" disabled={wLoading} loading={wLoading} intent="primary" size="lg" pill>
                تصفير الحد
              </NeumorphButton>
              {wResult ? <span className="text-sm font-semibold text-[var(--admin-primary)]">{wResult}</span> : null}
            </div>
          </form>
        </motion.section>

        <motion.section initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }} className="admin-panel">
          <div className="mb-6 flex items-center gap-3">
            <div className="admin-badge admin-badge--pill">
              <LockOpen className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-[var(--admin-text)]">فتح درس يدويًا</h2>
              <p className="text-sm text-[var(--admin-muted)]">تجاوز البوابات وفتح درس محدد لطالب.</p>
            </div>
          </div>

          <form onSubmit={handleUnlockLesson} className="space-y-4">
            <select value={uPkgId} onChange={(event) => handleUPkgChange(event.target.value)} className="admin-input">
              <option value="">اختر الباقة</option>
              {packages.map((pkg) => (
                <option key={pkg.id} value={pkg.id}>{pkg.name}</option>
              ))}
            </select>
            <select value={uSecId} onChange={(event) => handleUSecChange(event.target.value)} disabled={!uPkgId} className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:ring-2 focus:ring-[var(--admin-primary)] disabled:opacity-50">
              <option value="">اختر القسم</option>
              {uSections.map((section) => (
                <option key={section.id} value={section.id}>{section.order}. {section.title}</option>
              ))}
            </select>
            <select value={uLessonId} onChange={(event) => setULessonId(event.target.value)} disabled={!uSecId} required className="w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-4 py-3 text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:ring-2 focus:ring-[var(--admin-primary)] disabled:opacity-50">
              <option value="">اختر الدرس</option>
              {uLessons.map((lesson) => (
                <option key={lesson.id} value={lesson.id}>{lesson.order}. {lesson.title}</option>
              ))}
            </select>
            <select value={uStudentId} onChange={(event) => setUStudentId(event.target.value)} required className="admin-input">
              <option value="">اختر الطالب</option>
              {students.map((student) => (
                <option key={student.id} value={student.id}>{student.fullName} ({student.phoneNumber})</option>
              ))}
            </select>

            <div className="flex items-center justify-between gap-3 pt-4 border-t border-[var(--admin-border)]">
              <NeumorphButton type="submit" disabled={uLoading} loading={uLoading} intent="primary" size="lg" pill>
                فتح الدرس
              </NeumorphButton>
              {uResult ? <span className="text-sm font-semibold text-[var(--admin-primary)]">{uResult}</span> : null}
            </div>
          </form>
        </motion.section>
      </div>

      <div className="mt-6 rounded-[1.5rem] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 text-sm text-[var(--admin-muted)] shadow-sm">
        كل عمليات التعديل اليدوي يتم تسجيلها داخل سجل المراجعة مع الطابع الزمني وهوية المسؤول.
      </div>
    </AdminShellChrome>
  );
}
