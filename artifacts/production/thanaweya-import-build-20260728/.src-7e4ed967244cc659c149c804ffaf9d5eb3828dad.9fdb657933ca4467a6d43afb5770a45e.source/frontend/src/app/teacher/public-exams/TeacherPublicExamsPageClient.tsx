"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { FileQuestion, Globe2, Plus, RefreshCcw, ShieldCheck } from "lucide-react";
import toast from "react-hot-toast";

import { AdminColumn, AdminDataTable, AdminStatCard } from "@/components/admin";
import { AcademicScopeSelector } from "@/components/admin/AcademicScopeSelector";
import { TeacherShellChrome } from "@/components/teacher/TeacherShellChrome";
import { getAcademicScopeLabel, type AcademicScopePayload } from "@/lib/academic-labels";
import { registerCacheStore } from "@/lib/cache-invalidation";
import { teacherService, type SubjectDto } from "@/services/teacher-service";
import type { PublicExamProductDto } from "@/services/admin-sales-service";

const initialScope: AcademicScopePayload[] = [
  { scopeLevel: "GradeAllSubjects", educationStage: "Secondary", gradeLevel: "FirstSecondary" },
];

export default function TeacherPublicExamsPageClient() {
  const [exams, setExams] = useState<PublicExamProductDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [academicScopes, setAcademicScopes] = useState<AcademicScopePayload[]>(initialScope);
  const [form, setForm] = useState({
    title: "",
    slug: "",
    subjectId: "",
    description: "",
    isPublished: true,
    isPaid: false,
    price: "0",
    passingScore: "1",
    totalScore: "1",
    durationMinutes: "",
  });

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const [nextExams, subjectResponse] = await Promise.all([
        teacherService.getPublicExams(),
        teacherService.getMySubjects(),
      ]);
      setExams(nextExams);
      setSubjects(subjectResponse.data ?? []);
      setForm((current) =>
        current.subjectId || !subjectResponse.data?.[0]?.id
          ? current
          : { ...current, subjectId: subjectResponse.data[0].id },
      );
    } catch {
      toast.error("تعذر تحميل الامتحانات العامة.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const cleanupCacheStore = registerCacheStore("teacher:public-exams", () => {}, () => void load());
    void load();
    return cleanupCacheStore;
  }, [load]);

  const subjectNames = useMemo(
    () => Object.fromEntries(subjects.map((subject) => [subject.id, subject.name])),
    [subjects],
  );

  const updateTitle = (title: string) => {
    const slug = form.slug || title.trim().toLowerCase().replace(/\s+/g, "-").replace(/[^a-z0-9\u0600-\u06FF-]/g, "");
    setForm({ ...form, title, slug });
  };

  const createExam = async () => {
    if (!form.title.trim() || !form.slug.trim() || !form.subjectId) {
      toast.error("اسم الامتحان، الرابط، والمادة مطلوبة.");
      return;
    }

    try {
      setSaving(true);
      await teacherService.createPublicExam({
        title: form.title.trim(),
        description: form.description.trim() || null,
        slug: form.slug.trim(),
        subjectId: form.subjectId,
        gradeLevel: null,
        isPublished: form.isPublished,
        isPaid: form.isPaid,
        price: form.isPaid ? Number(form.price || 0) : 0,
        passingScore: Number(form.passingScore || 0),
        totalScore: Number(form.totalScore || 0),
        durationMinutes: form.durationMinutes ? Number(form.durationMinutes) : null,
        isRandomized: false,
        availableFrom: null,
        availableUntil: null,
        academicScopes,
      });
      toast.success("تم إنشاء الامتحان العام وربطه بحسابك.");
      setForm((current) => ({ ...current, title: "", slug: "", description: "" }));
      await load();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "فشل إنشاء الامتحان العام.");
    } finally {
      setSaving(false);
    }
  };

  const columns: AdminColumn<PublicExamProductDto>[] = [
    {
      key: "exam",
      label: "الامتحان",
      render: (exam) => (
        <div>
          <p className="font-black text-[var(--admin-text)]">{exam.examTitle}</p>
          <p className="mt-1 text-xs font-bold text-[var(--admin-muted)]" dir="ltr">
            /public-exams/{exam.slug}
          </p>
        </div>
      ),
    },
    {
      key: "subject",
      label: "المادة والسعر",
      render: (exam) => (
        <div className="space-y-1 text-xs font-bold text-[var(--admin-muted)]">
          <p>{subjectNames[exam.subjectId ?? ""] ?? "مادة غير محددة"}</p>
          <p>{exam.isPaid ? `${exam.price} جنيه` : "مجاني"}</p>
        </div>
      ),
    },
    {
      key: "status",
      label: "الحالة",
      align: "center",
      render: (exam) => (
        <span
          className={`inline-flex rounded-full px-3 py-1 text-xs font-black ${
            exam.isPublished
              ? "bg-emerald-500/10 text-emerald-600"
              : "bg-[var(--admin-card-strong)] text-[var(--admin-muted)]"
          }`}
        >
          {exam.isPublished ? "منشور" : "غير منشور"}
        </span>
      ),
    },
    {
      key: "scope",
      label: "النطاق",
      render: (exam) => (
        <div className="flex max-w-sm flex-wrap gap-2">
          {exam.academicScopes?.length ? (
            exam.academicScopes.slice(0, 3).map((scope, index) => (
              <span
                key={`${exam.id}-${index}`}
                className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]"
              >
                {getAcademicScopeLabel(scope)}
              </span>
            ))
          ) : (
            <span className="text-xs font-bold text-[var(--admin-muted)]">غير محدد</span>
          )}
        </div>
      ),
    },
    {
      key: "actions",
      label: "الإجراء",
      align: "left",
      render: () => (
        <Link href="/student/public-exams" className="admin-btn-ghost inline-flex items-center gap-2">
          <Globe2 className="h-4 w-4" />
          صفحة الظهور
        </Link>
      ),
    },
  ];

  return (
    <TeacherShellChrome
      activePath="/teacher/public-exams"
      sectionLabel="الامتحانات العامة"
      pageTitle="امتحانات تظهر خارج الحصص"
      subtitle="أنشئ امتحاناً عاماً مستقلاً يظهر للطلاب في صفحة الامتحانات العامة ويرتبط بك تلقائياً."
      action={
        <button type="button" onClick={load} disabled={loading} className="admin-btn-ghost inline-flex items-center gap-2">
          <RefreshCcw className="h-4 w-4" />
          تحديث
        </button>
      }
    >
      <div className="space-y-8" dir="rtl">
        <section className="grid grid-cols-1 gap-6 md:grid-cols-3">
          <AdminStatCard variant="light" icon={FileQuestion} label="امتحاناتك العامة" value={exams.length} />
          <AdminStatCard variant="accent" icon={ShieldCheck} label="منشورة للطلاب" value={exams.filter((exam) => exam.isPublished).length} />
          <AdminStatCard variant="muted" icon={Globe2} label="مواد متاحة" value={subjects.length} />
        </section>

        <section className="admin-panel">
          <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 className="flex items-center gap-2 text-lg font-black text-[var(--admin-text)]">
                <FileQuestion className="h-5 w-5 text-[var(--admin-primary)]" />
                امتحان عام جديد
              </h2>
              <p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">
                المدرس والمادة يتم تثبيتهم على الامتحان من حسابك.
              </p>
            </div>
            <button
              type="button"
              onClick={createExam}
              disabled={saving}
              className="admin-btn-primary inline-flex items-center gap-2"
            >
              <Plus className="h-4 w-4" />
              {saving ? "جاري الإنشاء..." : "إنشاء الامتحان"}
            </button>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <Field label="اسم الامتحان" value={form.title} onChange={updateTitle} placeholder="مثال: امتحان الوحدة الأولى العام" />
            <Field label="رابط الظهور" value={form.slug} onChange={(value) => setForm({ ...form, slug: value })} dir="ltr" placeholder="unit-one-public" />
            <Select label="المادة" value={form.subjectId} onChange={(value) => setForm({ ...form, subjectId: value })} options={subjects.map((subject) => [subject.id, subject.name])} />
            <Field label="المدة بالدقائق" type="number" value={form.durationMinutes} onChange={(value) => setForm({ ...form, durationMinutes: value })} placeholder="اختياري" />
            <Field label="الدرجة النهائية" type="number" value={form.totalScore} onChange={(value) => setForm({ ...form, totalScore: value })} />
            <Field label="درجة النجاح" type="number" value={form.passingScore} onChange={(value) => setForm({ ...form, passingScore: value })} />
          </div>

          <label className="mt-4 grid gap-1 text-sm">
            <span className="font-bold text-[var(--admin-muted)]">تعليمات الامتحان</span>
            <textarea
              value={form.description}
              onChange={(event) => setForm({ ...form, description: event.target.value })}
              className="admin-input min-h-24 resize-none"
            />
          </label>

          <div className="mt-5 grid gap-3 sm:grid-cols-3">
            <Toggle label="منشور للطلاب" checked={form.isPublished} onChange={(checked) => setForm({ ...form, isPublished: checked })} />
            <Toggle label="مدفوع" checked={form.isPaid} onChange={(checked) => setForm({ ...form, isPaid: checked })} />
            <Field label="السعر" type="number" value={form.price} onChange={(value) => setForm({ ...form, price: value })} disabled={!form.isPaid} />
          </div>

          <div className="mt-5 border-t border-[var(--admin-border)] pt-5">
            <AcademicScopeSelector value={academicScopes} onChange={setAcademicScopes} subjects={subjects} />
          </div>
        </section>

        <AdminDataTable
          data={exams}
          columns={columns}
          loading={loading}
          rowKey={(exam) => exam.id}
          emptyMessage="لا توجد امتحانات عامة مرتبطة بك بعد."
        />
      </div>
    </TeacherShellChrome>
  );
}

function Field({
  label,
  value,
  onChange,
  type = "text",
  placeholder,
  dir,
  disabled = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  placeholder?: string;
  dir?: "rtl" | "ltr";
  disabled?: boolean;
}) {
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
        className="admin-input"
      />
    </label>
  );
}

function Select({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: Array<[string, string]>;
}) {
  return (
    <label className="grid gap-1 text-sm">
      <span className="font-bold text-[var(--admin-muted)]">{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)} className="admin-input">
        {options.map(([id, name]) => (
          <option key={id} value={id}>
            {name}
          </option>
        ))}
      </select>
    </label>
  );
}

function Toggle({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="flex min-h-11 items-center justify-between rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 text-sm font-bold text-[var(--admin-text)]">
      <span>{label}</span>
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        className="h-4 w-4 accent-[var(--admin-primary)]"
      />
    </label>
  );
}
