"use client";

import { useState, useEffect } from "react";
import { Users, GraduationCap, Calendar, Phone, User, MapPin, School, ShieldCheck } from "lucide-react";
import { 
  AdminDataTable, 
  AdminColumn, 
  AdminStatCard, 
  AdminSearchToolbar, 
  AdminPageSkeleton 
} from "@/components/admin";
import { teacherService, TeacherStudentDto } from "@/services/teacher-service";
import toast from "react-hot-toast";

import { TeacherPage } from "@/components/teacher/TeacherShellChrome";
import { getEducationStageLabel, getGradeLevelLabel } from "@/lib/academic-labels";

export default function TeacherStudentsPageClient() {
  const [students, setStudents] = useState<TeacherStudentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");

  useEffect(() => {
    teacherService.getStudents()
      .then((res) => {
        if (res.success) {
          setStudents(res.data || []);
        }
      })
      .catch((err) => {
        console.error("Error fetching students:", err);
        toast.error("فشل في تحميل قائمة الطلاب");
      })
      .finally(() => setLoading(false));
  }, []);

  const filteredStudents = students.filter((s) => {
    const query = searchQuery.toLowerCase();
    return (
      s.fullName.toLowerCase().includes(query) ||
      s.phoneNumber.toLowerCase().includes(query) ||
      s.activatedPackageName.toLowerCase().includes(query)
    );
  });

  const columns: AdminColumn<TeacherStudentDto>[] = [
    {
      key: "student",
      label: "الطالب",
      render: (s) => (
        <div className="flex items-center gap-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-bold text-sm shadow-sm">
            <User className="h-5 w-5" />
          </div>
          <div>
            <div className="font-bold text-[var(--admin-text)]">{s.fullName}</div>
            <div className="flex items-center gap-1 text-xs text-[var(--admin-muted)] mt-0.5">
              <Phone className="h-3 w-3" />
              <span>{s.phoneNumber}</span>
            </div>
          </div>
        </div>
      ),
    },
    {
      key: "packageName",
      label: "الوصول الدراسي",
      render: (s) => (
        <div className="space-y-2">
          <span className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-bold text-[var(--admin-primary)]">
            {s.activatedPackageName}
          </span>
          <div className="text-xs font-bold text-[var(--admin-muted)]">
            {s.activePackageCount} باقة / {s.activeGrantCount} صلاحية نشطة
          </div>
        </div>
      ),
    },
    {
      key: "academic",
      label: "البيانات الدراسية",
      render: (s) => (
        <div className="space-y-1 text-xs font-bold text-[var(--admin-muted)]">
          <div>{getEducationStageLabel(s.educationStage)} - {getGradeLevelLabel(s.gradeLevel)}</div>
          <div>{s.studyTrack || "بدون شعبة"} - {s.schoolName || "مدرسة غير مسجلة"}</div>
        </div>
      ),
    },
    {
      key: "contacts",
      label: "التواصل",
      render: (s) => (
        <div className="space-y-1 text-xs font-bold text-[var(--admin-muted)]">
          <div>ولي الأمر: {s.parentPhone || "—"}</div>
          <div>الأم: {s.motherPhone || "—"}</div>
          <div>بديل الطالب: {s.secondaryPhone || "—"}</div>
        </div>
      ),
    },
    {
      key: "activatedAt",
      label: "تاريخ التفعيل",
      render: (s) => {
        const date = new Date(s.activatedAt);
        return (
          <div className="flex items-center gap-2 text-sm text-[var(--admin-muted)]">
            <Calendar className="h-4 w-4 text-[var(--admin-primary)]" />
            <span>{date.toLocaleDateString("ar-EG", { timeZone: 'Africa/Cairo', year: "numeric", month: "long", day: "numeric" })}</span>
          </div>
        );
      },
    },
  ];

  return (
    <TeacherPage
      activePath="/teacher/students"
      sectionLabel="قائمة الطلاب"
      pageTitle="الطلاب النشطون"
      subtitle="استعرض جميع الطلاب المشتركين والمفعلين لباقاتك الدراسية وتتبع تواريخ انضمامهم."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        <section className="grid grid-cols-1 gap-6 md:grid-cols-3">
          <AdminStatCard
            variant="light"
            icon={Users}
            label="إجمالي طلابك"
            value={students.length}
            subtitle="الطلاب المشتركون في باقاتك الدراسية"
          />
          <AdminStatCard
            variant="accent"
            icon={GraduationCap}
            label="الباقات النشطة"
            value={new Set(students.map((s) => s.activatedPackageName)).size}
            subtitle="عدد الباقات الفريدة التي تم تفعيلها"
          />
          <AdminStatCard
            variant="muted"
            icon={ShieldCheck}
            label="الصلاحيات النشطة"
            value={students.reduce((sum, student) => sum + (student.activeGrantCount || 0), 0)}
            subtitle="كل التفعيلات المرتبطة بباقاتك"
          />
        </section>

        {/* Search Bar */}
        <AdminSearchToolbar
          value={searchQuery}
          onChange={setSearchQuery}
          placeholder="ابحث عن طالب بالاسم، الهاتف، أو اسم الباقة الدراسي..."
        />

        {loading ? (
          <AdminPageSkeleton />
        ) : (
          <div className="space-y-6">
            <section className="grid gap-4 lg:grid-cols-2">
              {filteredStudents.map((student) => (
                <article key={`card-${student.id}`} className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <h3 className="truncate text-lg font-black text-[var(--admin-text)]">{student.fullName}</h3>
                      <p className="mt-1 flex items-center gap-1 text-xs font-bold text-[var(--admin-muted)]">
                        <Phone className="h-3.5 w-3.5" />
                        {student.phoneNumber}
                      </p>
                    </div>
                    <span className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
                      {student.studentCode || "بدون كود"}
                    </span>
                  </div>

                  <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    <Info icon={GraduationCap} label="الدراسة" value={`${getEducationStageLabel(student.educationStage)} - ${getGradeLevelLabel(student.gradeLevel)}`} />
                    <Info icon={School} label="المدرسة" value={student.schoolName || "غير مسجلة"} />
                    <Info icon={MapPin} label="العنوان" value={[student.governorate, student.district, student.address].filter(Boolean).join(" - ") || "غير مسجل"} />
                    <Info icon={Users} label="ولي الأمر" value={student.parentPhone || "غير مسجل"} />
                  </div>

                  <div className="mt-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3 text-xs font-bold text-[var(--admin-muted)]">
                    آخر باقة: <span className="text-[var(--admin-primary)]">{student.activatedPackageName}</span>، عدد الصلاحيات: {student.activeGrantCount}، آخر تفعيل: {student.lastActivationAt ? new Date(student.lastActivationAt).toLocaleDateString("ar-EG", { timeZone: 'Africa/Cairo' }) : "—"}
                  </div>
                </article>
              ))}
            </section>

            <AdminDataTable
              data={filteredStudents}
              columns={columns}
              loading={loading}
              rowKey={(s) => s.id}
              emptyMessage="لا يوجد طلاب مشتركون بعد أو لا توجد نتائج مطابقة لبحثك."
            />
          </div>
        )}
      </div>
    </TeacherPage>
  );
}

function Info({ icon: Icon, label, value }: { icon: typeof Users; label: string; value: string }) {
  return (
    <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-3">
      <div className="mb-1 flex items-center gap-2 text-xs font-black text-[var(--admin-muted)]">
        <Icon className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
        {label}
      </div>
      <p className="line-clamp-2 text-sm font-bold text-[var(--admin-text)]">{value}</p>
    </div>
  );
}
