'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { BarChart3, BookOpen, FileQuestion, GraduationCap, RefreshCcw, Save, Shuffle, Timer } from 'lucide-react';
import { AdminShellChrome } from '@/components/admin';
import { AcademicScopeSelector } from '@/components/admin/AcademicScopeSelector';
import { getAcademicScopeLabel, type AcademicScopePayload } from '@/lib/academic-labels';
import { cairoDateTimeLocalToIso } from '@/components/admin/admin-utils';
import { adminSalesService, type PublicExamProductDto } from '@/services/admin-sales-service';
import { teacherService, type SubjectDto, type TeacherDto } from '@/services/teacher-service';
import NeumorphButton from '@/components/ui/neumorph-button';

export default function AdminPublicExamsPageClient() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [exams, setExams] = useState<PublicExamProductDto[]>([]);
  const [teachers, setTeachers] = useState<TeacherDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [academicScopes, setAcademicScopes] = useState<AcademicScopePayload[]>([
    { scopeLevel: 'GradeAllSubjects', educationStage: 'Secondary', gradeLevel: 'FirstSecondary' },
  ]);
  const [form, setForm] = useState({
    title: '',
    description: '',
    slug: '',
    teacherId: '',
    subjectId: '',
    gradeLevel: '',
    isPublished: true,
    isPaid: false,
    price: '0',
    passingScore: '1',
    totalScore: '1',
    durationMinutes: '',
    isRandomized: false,
    availableFrom: '',
    availableUntil: '',
  });

  async function load() {
    setLoading(true);
    setMessage('');
    try {
      const [nextExams, nextTeachers, nextSubjects] = await Promise.all([
        adminSalesService.publicExams(),
        teacherService.getTeachers().catch(() => ({ success: true, data: [] as TeacherDto[] })),
        teacherService.getSubjects().catch(() => ({ success: true, data: [] as SubjectDto[] })),
      ]);
      setExams(nextExams);
      setTeachers(nextTeachers.data ?? []);
      setSubjects(nextSubjects.data ?? []);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'تعذر تحميل الامتحانات العامة.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function createExam() {
    setLoading(true);
    setMessage('');
    try {
      if (!form.title.trim() || !form.slug.trim() || !form.subjectId) {
        setMessage('اسم الامتحان، الرابط، والمادة مطلوبين. الامتحان مستقل عن الحصص ولا يحتاج اختيار مدرس.');
        return;
      }
      const created = await adminSalesService.createPublicExam({
        title: form.title.trim(),
        description: form.description.trim() || null,
        slug: form.slug.trim(),
        teacherId: null,
        subjectId: form.subjectId,
        gradeLevel: form.gradeLevel.trim() || null,
        isPublished: form.isPublished,
        isPaid: form.isPaid,
        price: form.isPaid ? Number(form.price || 0) : 0,
        passingScore: Number(form.passingScore || 0),
        totalScore: Number(form.totalScore || 0),
        durationMinutes: form.durationMinutes ? Number(form.durationMinutes) : null,
        isRandomized: form.isRandomized,
        availableFrom: form.availableFrom ? cairoDateTimeLocalToIso(form.availableFrom) : null,
        availableUntil: form.availableUntil ? cairoDateTimeLocalToIso(form.availableUntil) : null,
        academicScopes,
      });
      setMessage('تم إنشاء الامتحان العام. افتحه الآن لإضافة الأسئلة.');
      setForm((current) => ({ ...current, title: '', description: '', slug: '' }));
      await load();
      router.push(`/admin/public-exams/${created.id}`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'فشل إنشاء الامتحان العام.');
    } finally {
      setLoading(false);
    }
  }

  const teacherNames = useMemo(() => Object.fromEntries(teachers.map((teacher) => [teacher.id, teacher.fullName])), [teachers]);
  const subjectNames = useMemo(() => Object.fromEntries(subjects.map((subject) => [subject.id, subject.name])), [subjects]);
  const availableSubjects = subjects;

  const updateTitle = (title: string) => {
    const nextSlug = form.slug || title.trim().toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9\u0600-\u06FF-]/g, '');
    setForm({ ...form, title, slug: nextSlug });
  };

  return (
    <AdminShellChrome
      activePath="/admin/public-exams"
      sectionLabel="الامتحانات العامة"
      pageTitle="الامتحانات العامة المستقلة"
      subtitle="أنشئ امتحاناً عاماً مستقلاً، ثم أضف الأسئلة من نفس بروفايل الامتحان المستخدم داخل الحصص."
      action={
        <button onClick={load} disabled={loading} className="inline-flex items-center gap-2 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-60">
          <RefreshCcw className="h-4 w-4" />
          تحديث
        </button>
      }
    >
      <div className="space-y-5">
        {message && <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-bold text-amber-900">{message}</div>}

        <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="mb-5 flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
            <div>
              <h2 className="flex items-center gap-2 text-lg font-black text-[var(--admin-text)]">
                <FileQuestion className="h-5 w-5 text-[var(--admin-primary)]" />
                إعداد امتحان عام جديد
              </h2>
              <p className="mt-1 max-w-2xl text-sm font-bold text-[var(--admin-muted)]">
                الامتحان هنا مستقل عن الحصة، لكنه يحتفظ بنفس بروفايل الأسئلة والمحاولات بعد الإنشاء.
              </p>
            </div>
            <NeumorphButton type="button" onClick={createExam} disabled={loading} loading={loading} intent="primary" size="lg" pill>
              <Save className="h-4 w-4" />
              إنشاء وفتح البروفايل
            </NeumorphButton>
          </div>

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1.15fr)_minmax(320px,0.85fr)]">
            <div className="space-y-5">
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="عنوان الامتحان" value={form.title} onChange={updateTitle} placeholder="مثال: اختبار شامل على الوحدة الأولى" />
                <Field label="الرابط" value={form.slug} onChange={(v) => setForm({ ...form, slug: v })} placeholder="unit-one-final" dir="ltr" />
              </div>

              <div className="grid gap-4 sm:grid-cols-3">
                <MetricField icon={GraduationCap} label="الدرجة النهائية" type="number" value={form.totalScore} onChange={(v) => setForm({ ...form, totalScore: v })} />
                <MetricField icon={BookOpen} label="درجة النجاح" type="number" value={form.passingScore} onChange={(v) => setForm({ ...form, passingScore: v })} />
                <MetricField icon={Timer} label="المدة بالدقائق" type="number" value={form.durationMinutes} onChange={(v) => setForm({ ...form, durationMinutes: v })} placeholder="بدون زمن" />
              </div>

              <label className="grid gap-1 text-sm">
                <span className="font-bold text-[var(--admin-muted)]">وصف أو تعليمات</span>
                <textarea
                  value={form.description}
                  onChange={(event) => setForm({ ...form, description: event.target.value })}
                  placeholder="تعليمات تظهر للطالب قبل بدء الامتحان."
                  className="min-h-24 resize-none rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-4 py-3 text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
                />
              </label>
            </div>

            <div className="space-y-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
              <div className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
                <BookOpen className="h-4 w-4 text-[var(--admin-primary)]" />
                المادة
              </div>
              <Select
                label="المادة"
                value={form.subjectId}
                onChange={(v) => setForm({ ...form, subjectId: v })}
                options={[['', 'اختر المادة'], ...availableSubjects.map((subject) => [subject.id, subject.name] as [string, string])]}
              />
              <Field label="الصف" value={form.gradeLevel} onChange={(v) => setForm({ ...form, gradeLevel: v })} placeholder="اختياري" />
              <div className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-xs font-bold text-[var(--admin-muted)]">
                الامتحان غير مربوط بحصة أو فيديو. المادة هنا لتحديد التصنيف والظهور للطلاب فقط.
              </div>
            </div>
          </div>

          <div className="mt-5 border-t border-[var(--admin-border)] pt-5">
            <div className="mb-3">
              <h3 className="text-sm font-black text-[var(--admin-text)]">نطاق ظهور الامتحان</h3>
              <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">يكفي تطابق نطاق واحد مع الطالب للسماح بالشراء أو الدخول.</p>
            </div>
            <AcademicScopeSelector value={academicScopes} onChange={setAcademicScopes} subjects={subjects} />
          </div>

          <div className="mt-5 grid gap-3 border-t border-[var(--admin-border)] pt-5 sm:grid-cols-2 lg:grid-cols-5">
            <Toggle label="منشور" checked={form.isPublished} onChange={(v) => setForm({ ...form, isPublished: v })} />
            <Toggle label="مدفوع" checked={form.isPaid} onChange={(v) => setForm({ ...form, isPaid: v })} />
            <Toggle label="ترتيب عشوائي" checked={form.isRandomized} onChange={(v) => setForm({ ...form, isRandomized: v })} icon={<Shuffle className="h-4 w-4" />} />
            <Field label="السعر" type="number" value={form.price} onChange={(v) => setForm({ ...form, price: v })} disabled={!form.isPaid} />
            <div className="grid gap-3 sm:col-span-2 lg:col-span-1">
              <Field label="متاح من" type="datetime-local" value={form.availableFrom} onChange={(v) => setForm({ ...form, availableFrom: v })} />
              <Field label="متاح حتى" type="datetime-local" value={form.availableUntil} onChange={(v) => setForm({ ...form, availableUntil: v })} />
            </div>
          </div>
        </section>

        <section className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
          <h2 className="mb-3 text-lg font-black text-[var(--admin-text)]">الامتحانات العامة</h2>
          {exams.length === 0 ? (
            <p className="text-sm font-bold text-[var(--admin-muted)]">لا توجد امتحانات عامة بعد.</p>
          ) : (
            <div className="grid gap-3">
              {exams.map((exam) => (
                <div key={exam.id} className="flex flex-col gap-3 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 md:flex-row md:items-center md:justify-between">
                  <div>
                    <h3 className="font-black text-[var(--admin-text)]">{exam.examTitle}</h3>
                    <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]">
                      {exam.isPaid ? `${exam.price} جنيه` : 'مجاني'} - {exam.isPublished ? 'منشور' : 'غير منشور'} - {exam.teacherId ? teacherNames[exam.teacherId] ?? exam.teacherId : 'بدون مدرس'} - {exam.subjectId ? subjectNames[exam.subjectId] ?? exam.subjectId : 'بدون مادة'}
                    </p>
                    {exam.academicScopes?.length ? (
                      <div className="mt-2 flex flex-wrap gap-1.5">
                        {exam.academicScopes.map((scope, index) => (
                          <span key={`${exam.id}-scope-${index}`} className="rounded-full bg-[var(--admin-primary-15)] px-2 py-1 text-[11px] font-black text-[var(--admin-primary)]">
                            {getAcademicScopeLabel(scope)}
                          </span>
                        ))}
                      </div>
                    ) : null}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Link href={`/admin/public-exams/${exam.id}`} className="inline-flex items-center gap-2 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-xs font-black text-[var(--admin-text)] hover:bg-[var(--admin-hover)]">
                      <FileQuestion className="h-4 w-4" />
                      فتح وإضافة أسئلة
                    </Link>
                    <Link href={`/admin/public-exams/${exam.id}/results`} className="inline-flex items-center gap-2 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-xs font-black text-[var(--admin-text)] hover:bg-[var(--admin-hover)]">
                      <BarChart3 className="h-4 w-4" />
                      النتائج
                    </Link>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
    </AdminShellChrome>
  );
}

function Field({ label, value, onChange, type = 'text', placeholder, dir, disabled = false }: { label: string; value: string; onChange: (value: string) => void; type?: string; placeholder?: string; dir?: 'rtl' | 'ltr'; disabled?: boolean }) {
  return (
    <label className="grid gap-1 text-sm">
      <span className="font-bold text-[var(--admin-muted)]">{label}</span>
      <input
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        dir={dir}
        disabled={disabled}
        className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-[var(--admin-text)] outline-none transition placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] disabled:opacity-60"
      />
    </label>
  );
}

function MetricField({ icon: Icon, ...props }: { icon: typeof GraduationCap; label: string; value: string; onChange: (value: string) => void; type?: string; placeholder?: string }) {
  return (
    <label className="grid gap-1 text-sm">
      <span className="flex items-center gap-2 font-bold text-[var(--admin-muted)]">
        <Icon className="h-4 w-4 text-[var(--admin-primary)]" />
        {props.label}
      </span>
      <input
        type={props.type ?? 'text'}
        value={props.value}
        onChange={(event) => props.onChange(event.target.value)}
        placeholder={props.placeholder}
        className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-background)] px-3 py-2 text-[var(--admin-text)] outline-none transition focus:border-[var(--admin-primary)]"
      />
    </label>
  );
}

function Select({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: Array<[string, string]> }) {
  return (
    <label className="grid gap-1 text-sm">
      <span className="font-bold text-[var(--admin-muted)]">{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)} className="rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]">
        {options.map(([optionValue, labelText]) => <option key={optionValue || labelText} value={optionValue}>{labelText}</option>)}
      </select>
    </label>
  );
}

function Toggle({ label, checked, onChange, icon }: { label: string; checked: boolean; onChange: (value: boolean) => void; icon?: React.ReactNode }) {
  return (
    <label className="flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-text)]">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      {icon}
      {label}
    </label>
  );
}
