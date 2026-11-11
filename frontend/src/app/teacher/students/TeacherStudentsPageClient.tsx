"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { isAxiosError } from "axios";
import { Download, Filter, GraduationCap, ShieldCheck, Users } from "lucide-react";
import toast from "react-hot-toast";

import { AdminDataTable, AdminSearchToolbar, AdminStatCard, type AdminColumn } from "@/components/admin";
import { TeacherPage } from "@/components/teacher/TeacherShellChrome";
import { getEducationStageLabel, getGradeLevelLabel, getStudyTrackLabel } from "@/lib/academic-labels";
import { cairoCurrentDate } from "@/lib/cairo-time";
import { teacherService, type TeacherStudentDto, type TeacherStudentsExportSelection } from "@/services/teacher-service";

const emptyFilters = { stage: "", grade: "", track: "", gender: "", governorate: "", packageId: "" };
type Filters = typeof emptyFilters;
type Option = { value: string; label: string };

const secondaryButton = "inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)] disabled:opacity-50";
const primaryButton = "inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 py-2 text-sm font-bold text-[var(--admin-primary-contrast)] hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--admin-primary)] disabled:opacity-50";

function filterOptions(values: (string | null | undefined)[], label: (value: string) => string): Option[] {
  return [...new Set(values.filter((value): value is string => Boolean(value)))]
    .map(value => ({ value, label: label(value) }))
    .sort((first, second) => first.label.localeCompare(second.label, "ar"));
}

function activePackages(student: TeacherStudentDto) {
  return student.activePackages ?? [{ id: student.activatedPackageName, name: student.activatedPackageName }];
}

function matches(student: TeacherStudentDto, filters: Filters, search: string) {
  if ((filters.stage && student.educationStage !== filters.stage) ||
      (filters.grade && student.gradeLevel !== filters.grade) ||
      (filters.track && student.studyTrack !== filters.track) ||
      (filters.gender && student.gender !== filters.gender) ||
      (filters.governorate && student.governorate !== filters.governorate) ||
      (filters.packageId && !activePackages(student).some(pkg => pkg.id === filters.packageId))) return false;

  return !search || [
    student.fullName, student.studentCode, student.phoneNumber, student.secondaryPhone,
    student.parentPhone, student.motherPhone, student.schoolName,
    ...activePackages(student).map(pkg => pkg.name),
  ].some(value => value?.toLowerCase().includes(search));
}

const activationDate = new Intl.DateTimeFormat("ar-EG-u-nu-latn", {
  timeZone: "Africa/Cairo", year: "numeric", month: "numeric", day: "numeric",
});

export default function TeacherStudentsPageClient() {
  const [students, setStudents] = useState<TeacherStudentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [loadVersion, setLoadVersion] = useState(0);
  const [searchQuery, setSearchQuery] = useState("");
  const [filters, setFilters] = useState(emptyFilters);
  const [showFilters, setShowFilters] = useState(false);
  const [showExport, setShowExport] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(() => new Set());
  const exportRequest = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    teacherService.getStudents(controller.signal)
      .then(res => {
        if (!res.success) throw new Error(res.message);
        if (!controller.signal.aborted) setStudents(res.data ?? []);
      })
      .catch(() => {
        if (!controller.signal.aborted) setLoadError(true);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [loadVersion]);

  useEffect(() => () => exportRequest.current?.abort(), []);

  const search = searchQuery.trim().toLowerCase();
  const filteredStudents = useMemo(
    () => students.filter(student => matches(student, filters, search)),
    [students, filters, search],
  );
  const packages = useMemo(() => {
    const names = new Map<string, string>();
    students.forEach(student => activePackages(student).forEach(pkg => names.set(pkg.id, pkg.name)));
    return [...names].map(([value, label]) => ({ value, label })).sort((a, b) => a.label.localeCompare(b.label, "ar"));
  }, [students]);
  const academicStudents = filters.stage ? students.filter(student => student.educationStage === filters.stage) : students;
  const activeFilters = Object.values(filters).filter(Boolean).length;
  const selectedIds = students.filter(student => selected.has(student.id)).map(student => student.id);
  const filterFields: { key: keyof Filters; label: string; options: Option[] }[] = [
    { key: "stage", label: "المرحلة الدراسية", options: filterOptions(students.map(student => student.educationStage), getEducationStageLabel) },
    { key: "grade", label: "الصف الدراسي", options: filterOptions(academicStudents.map(student => student.gradeLevel), getGradeLevelLabel) },
    { key: "track", label: "الشعبة / التخصص", options: filterOptions(academicStudents.map(student => student.studyTrack), getStudyTrackLabel) },
    { key: "packageId", label: "الباقة", options: packages },
    { key: "gender", label: "النوع", options: [{ value: "Male", label: "ذكر" }, { value: "Female", label: "أنثى" }] },
    { key: "governorate", label: "المحافظة", options: filterOptions(students.map(student => student.governorate), value => value) },
  ];

  function updateFilter(key: keyof Filters, value: string) {
    setFilters(current => key === "stage"
      ? { ...current, stage: value, grade: "", track: "" }
      : { ...current, [key]: value });
  }

  function toggleStudent(id: string) {
    setSelected(current => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function downloadStudents(selection: TeacherStudentsExportSelection) {
    if (exportRequest.current) return;
    const controller = new AbortController();
    exportRequest.current = controller;
    setExporting(true);
    try {
      const file = await teacherService.exportStudents(selection, controller.signal);
      if (controller.signal.aborted) return;
      const url = URL.createObjectURL(file);
      const link = document.createElement("a");
      link.href = url;
      link.download = `طلاب-المدرس-${cairoCurrentDate()}.xlsx`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      setShowExport(false);
    } catch (error) {
      if (!controller.signal.aborted) {
        toast.error(isAxiosError(error) && error.response?.status === 409
          ? "تغيّرت قائمة الطلاب المتاحين. حدّث الصفحة وحاول مرة أخرى."
          : "تعذر تنزيل ملف الطلاب. حاول مرة أخرى.");
      }
    } finally {
      exportRequest.current = null;
      if (!controller.signal.aborted) setExporting(false);
    }
  }

  const columns: AdminColumn<TeacherStudentDto>[] = [
    {
      key: "student",
      label: "الطالب",
      render: student => (
        <div className="flex items-center gap-2">
          <label className="inline-flex size-11 shrink-0 cursor-pointer items-center justify-center">
            <input type="checkbox" className="size-5 accent-[var(--admin-primary)]"
              aria-label={`تحديد ${student.fullName}`}
              checked={selected.has(student.id)} onChange={() => toggleStudent(student.id)} />
          </label>
          <div>
            <Link href={`/teacher/students/${student.id}`} aria-label={`عرض ملف الطالب ${student.fullName}`}
              className="font-bold text-[var(--admin-primary)] underline-offset-4 hover:underline">
              {student.fullName}
            </Link>
            <div className="mt-0.5 text-xs text-[var(--admin-muted)]"><bdi>{student.phoneNumber}</bdi></div>
            {student.studentCode && <div className="text-xs text-[var(--admin-muted)]"><bdi>{student.studentCode}</bdi></div>}
          </div>
        </div>
      ),
    },
    {
      key: "academic",
      label: "البيانات الدراسية",
      render: student => (
        <div className="space-y-1 text-xs">
          <div className="font-bold">{getEducationStageLabel(student.educationStage)} / {getGradeLevelLabel(student.gradeLevel)}</div>
          {student.studyTrack && <div>{getStudyTrackLabel(student.studyTrack)}</div>}
          {student.schoolName && <div className="text-[var(--admin-muted)]">{student.schoolName}</div>}
        </div>
      ),
    },
    {
      key: "access",
      label: "الباقات والتفعيل",
      render: student => (
        <div className="space-y-1 text-xs">
          <div className="font-bold">{student.activatedPackageName}</div>
          <div>{student.activePackageCount} باقة / {student.activeGrantCount} صلاحية</div>
          <div className="text-[var(--admin-muted)]">أول تفعيل {activationDate.format(new Date(student.activatedAt))}</div>
          {student.lastActivationAt && <div className="text-[var(--admin-muted)]">آخر تفعيل {activationDate.format(new Date(student.lastActivationAt))}</div>}
        </div>
      ),
    },
    {
      key: "contacts",
      label: "التواصل",
      render: student => (
        <div className="space-y-1 text-xs text-[var(--admin-muted)]">
          <div>ولي الأمر <bdi>{student.parentPhone || "—"}</bdi></div>
          {student.motherPhone && <div>الأم <bdi>{student.motherPhone}</bdi></div>}
          {student.secondaryPhone && <div>هاتف إضافي <bdi>{student.secondaryPhone}</bdi></div>}
        </div>
      ),
    },
    {
      key: "location",
      label: "العنوان",
      responsivePriority: "optional",
      render: student => [student.governorate, student.district, student.address].filter(Boolean).join("، ") || "—",
    },
  ];

  return (
    <TeacherPage
      activePath="/teacher/students"
      sectionLabel="قائمة الطلاب"
      pageTitle="الطلاب النشطون"
      subtitle="طلابك اللي عندهم وصول نشط لباقاتك الدراسية."
      action={<Link href="/teacher/student-follow-up" className={secondaryButton}>متابعة الطلاب</Link>}
    >
      <div className="space-y-6" dir="rtl">
        <section className="grid gap-4 md:grid-cols-3" aria-label="ملخص الطلاب">
          <AdminStatCard variant="light" icon={Users} label="إجمالي طلابك" value={students.length} />
          <AdminStatCard variant="accent" icon={GraduationCap} label="الباقات النشطة" value={packages.length} />
          <AdminStatCard variant="muted" icon={ShieldCheck} label="الصلاحيات النشطة"
            value={students.reduce((total, student) => total + student.activeGrantCount, 0)} />
        </section>

        <div className="space-y-3">
          <AdminSearchToolbar value={searchQuery} onChange={setSearchQuery} label="البحث عن طالب"
            placeholder="ابحث بالاسم، الهاتف، كود الطالب أو الباقة"
            actions={
              <>
                <button type="button" className={secondaryButton} aria-expanded={showFilters} aria-controls="teacher-student-filters"
                  onClick={() => setShowFilters(open => !open)}>
                  <Filter className="size-4" aria-hidden="true" />
                  {activeFilters ? `الفلاتر (${activeFilters})` : "الفلاتر"}
                </button>
                <button type="button" className={primaryButton} aria-expanded={showExport} aria-controls="teacher-student-export"
                  disabled={loading || loadError || students.length === 0} onClick={() => setShowExport(open => !open)}>
                  <Download className="size-4" aria-hidden="true" />
                  تنزيل Excel
                </button>
              </>
            }
          />

          <div id="teacher-student-filters" hidden={!showFilters}
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {filterFields.map(({ key, label, options }) => (
                <div key={key} className="grid min-w-0 gap-1.5 text-sm font-bold text-[var(--admin-text)]">
                  <label htmlFor={`teacher-student-filter-${key}`}>{label}</label>
                  <select id={`teacher-student-filter-${key}`} value={filters[key]}
                    onChange={event => updateFilter(key, event.target.value)} className="admin-input min-h-11 min-w-0">
                    <option value="">الكل</option>
                    {options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                </div>
              ))}
            </div>
            {activeFilters > 0 && (
              <button type="button" className={`${secondaryButton} mt-4`} onClick={() => setFilters(emptyFilters)}>
                مسح الفلاتر
              </button>
            )}
          </div>

          <div id="teacher-student-export" hidden={!showExport}
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
            <div className="flex flex-wrap items-center gap-2" aria-busy={exporting}>
              <button type="button" className={secondaryButton} disabled={exporting || !filteredStudents.length}
                onClick={() => void downloadStudents({ studentIds: filteredStudents.map(student => student.id) })}>
                تنزيل نتائج البحث والفلاتر ({filteredStudents.length})
              </button>
              <button type="button" className={secondaryButton} disabled={exporting || !selectedIds.length}
                onClick={() => void downloadStudents({ studentIds: selectedIds })}>
                تنزيل المحددين ({selectedIds.length})
              </button>
              <button type="button" className={secondaryButton} disabled={exporting || !students.length}
                onClick={() => void downloadStudents({ all: true })}>
                تنزيل كل طلابي ({students.length})
              </button>
              {exporting && <span role="status" className="text-sm text-[var(--admin-muted)]">جاري تجهيز الملف…</span>}
            </div>
          </div>
        </div>

        {selectedIds.length > 0 && (
          <div className="flex flex-wrap items-center gap-3 text-sm text-[var(--admin-text)]">
            <span role="status">عدد الطلاب المحددين {selectedIds.length}</span>
            <button type="button" className={secondaryButton} onClick={() => setSelected(new Set())}>إلغاء التحديد</button>
          </div>
        )}

        <AdminDataTable
          data={filteredStudents}
          columns={columns}
          loading={loading}
          rowKey={student => student.id}
          pageSize={20}
          errorMessage={loadError ? "تعذر تحميل قائمة الطلاب." : null}
          onRetry={() => {
            setLoadError(false);
            setLoading(true);
            setLoadVersion(version => version + 1);
          }}
          emptyMessage={students.length ? "لا توجد نتائج مطابقة للبحث والفلاتر." : "لا يوجد طلاب عندهم وصول نشط لباقاتك."}
        />
      </div>
    </TeacherPage>
  );
}
