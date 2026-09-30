'use client';

import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { adminGiftsService, type GiftLookupDto, type GiftTargetType } from '@/services/admin-gifts-service';

type ContentType = 'Lesson' | 'Video';

function useContentOptions(type: GiftTargetType, teacherId: string, parentId: string, enabled: boolean, search = '') {
  const key = `${type}:${teacherId}:${parentId}:${search}`;
  const [result, setResult] = useState<{ key: string; rows: GiftLookupDto[]; error: boolean } | null>(null);

  useEffect(() => {
    if (!enabled) return;
    let active = true;
    adminGiftsService.targets(type, teacherId, search, parentId || undefined)
      .then((rows) => { if (active) setResult({ key, rows, error: false }); })
      .catch(() => { if (active) setResult({ key, rows: [], error: true }); });
    return () => { active = false; };
  }, [type, teacherId, parentId, enabled, search, key]);

  const current = enabled && result?.key === key ? result : null;
  return { rows: current?.rows ?? [], loading: enabled && !current, error: current?.error ?? false };
}

function ContentSelect({ label, value, options, loading, error, onChange }: {
  label: string;
  value: string;
  options: GiftLookupDto[];
  loading: boolean;
  error: boolean;
  onChange: (value: string) => void;
}) {
  return <label className="block text-sm font-bold text-[var(--admin-text)]">
    {label}
    <select className="admin-input mt-2" value={value} onChange={(event) => onChange(event.target.value)} disabled={loading || error} required>
      <option value="">{loading ? 'جاري التحميل...' : error ? 'تعذر تحميل القائمة' : options.length ? `اختر ${label}` : 'لا توجد عناصر'}</option>
      {options.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
    </select>
  </label>;
}

export function GiftContentPicker({ targetType, teachers, teacherSearch, onTeacherSearchChange, onSelect }: {
  targetType: ContentType;
  teachers: GiftLookupDto[];
  teacherSearch: string;
  onTeacherSearchChange: (search: string) => void;
  onSelect: (target: GiftLookupDto | null) => void;
}) {
  const [teacherId, setTeacherId] = useState('');
  const [packageId, setPackageId] = useState('');
  const [termId, setTermId] = useState('');
  const [sectionId, setSectionId] = useState('');
  const [lessonId, setLessonId] = useState('');
  const [videoId, setVideoId] = useState('');
  const [packageSearch, setPackageSearch] = useState('');
  const [lessonSearch, setLessonSearch] = useState('');

  const packages = useContentOptions('Package', teacherId, '', !!teacherId, packageSearch);
  const terms = useContentOptions('Term', teacherId, packageId, !!packageId);
  const sections = useContentOptions('ContentSection', teacherId, termId, !!termId);
  const lessons = useContentOptions('Lesson', teacherId, sectionId, !!sectionId, lessonSearch);
  const videos = useContentOptions('Video', teacherId, lessonId, targetType === 'Video' && !!lessonId);

  useEffect(() => {
    if (!termId && terms.rows.length === 1 && terms.rows[0].isSystemContainer) setTermId(terms.rows[0].id);
  }, [termId, terms.rows]);
  useEffect(() => {
    if (!sectionId && sections.rows.length === 1 && sections.rows[0].isSystemContainer) setSectionId(sections.rows[0].id);
  }, [sectionId, sections.rows]);

  const clearTarget = () => onSelect(null);
  const selectLesson = (id: string) => {
    setLessonId(id);
    setVideoId('');
    onSelect(targetType === 'Lesson' ? lessons.rows.find((row) => row.id === id) ?? null : null);
  };

  return <div className="mt-5 space-y-4">
    <p className="text-sm text-[var(--admin-muted)]">اختر المدرس ثم الباقة، وبعدها سيظهر محتوى كل اختيار بالترتيب.</p>
    <label className="block text-sm font-bold text-[var(--admin-text)]">ابحث عن المدرس
      <input className="admin-input mt-2" value={teacherSearch} onChange={(event) => {
        onTeacherSearchChange(event.target.value); setTeacherId(''); setPackageId(''); setTermId(''); setSectionId(''); setLessonId(''); setVideoId(''); clearTarget();
      }} placeholder="اسم المدرس" />
    </label>
    <ContentSelect label="المدرس" value={teacherId} options={teachers} loading={false} error={false} onChange={(id) => {
      setTeacherId(id); setPackageSearch(''); setPackageId(''); setTermId(''); setSectionId(''); setLessonId(''); setVideoId(''); clearTarget();
    }} />
    {teacherId && <>
      <label className="block text-sm font-bold text-[var(--admin-text)]">ابحث عن الباقة
        <input className="admin-input mt-2" value={packageSearch} onChange={(event) => {
          setPackageSearch(event.target.value); setPackageId(''); setTermId(''); setSectionId(''); setLessonId(''); setVideoId(''); clearTarget();
        }} placeholder="اسم الباقة" />
      </label>
      <ContentSelect label="الباقة" value={packageId} options={packages.rows} loading={packages.loading} error={packages.error} onChange={(id) => {
        setPackageId(id); setTermId(''); setSectionId(''); setLessonId(''); setVideoId(''); clearTarget();
      }} />
    </>}
    {packageId && !terms.rows.some((row) => row.id === termId && row.isSystemContainer) &&
      <ContentSelect label="الترم" value={termId} options={terms.rows} loading={terms.loading} error={terms.error} onChange={(id) => {
        setTermId(id); setSectionId(''); setLessonId(''); setVideoId(''); clearTarget();
      }} />}
    {termId && !sections.rows.some((row) => row.id === sectionId && row.isSystemContainer) &&
      <ContentSelect label="القسم" value={sectionId} options={sections.rows} loading={sections.loading} error={sections.error} onChange={(id) => {
        setSectionId(id); setLessonId(''); setVideoId(''); clearTarget();
      }} />}
    {sectionId && <>
      <label className="block text-sm font-bold text-[var(--admin-text)]">ابحث عن الحصة
        <input className="admin-input mt-2" value={lessonSearch} onChange={(event) => {
          setLessonSearch(event.target.value); setLessonId(''); setVideoId(''); clearTarget();
        }} placeholder="اسم الحصة أو كودها" />
      </label>
      <ContentSelect label="الحصة" value={lessonId} options={lessons.rows} loading={lessons.loading} error={lessons.error} onChange={selectLesson} />
    </>}
    {targetType === 'Video' && lessonId && <ContentSelect label="الفيديو" value={videoId} options={videos.rows} loading={videos.loading} error={videos.error} onChange={(id) => {
      setVideoId(id); onSelect(videos.rows.find((row) => row.id === id) ?? null);
    }} />}
    {(packages.loading || terms.loading || sections.loading || lessons.loading || videos.loading) &&
      <Loader2 className="h-4 w-4 animate-spin text-[var(--admin-primary)]" aria-label="جاري تحميل المحتوى" />}
  </div>;
}
