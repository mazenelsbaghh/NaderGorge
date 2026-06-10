"use client";

import { useState, useEffect } from "react";
import { Users, GraduationCap, Calendar, Phone, User } from "lucide-react";
import { 
  AdminDataTable, 
  AdminColumn, 
  AdminStatCard, 
  AdminSearchToolbar, 
  AdminPageSkeleton 
} from "@/components/admin";
import { teacherService, TeacherStudentDto } from "@/services/teacher-service";
import toast from "react-hot-toast";

import { TeacherShellChrome } from "@/components/teacher/TeacherShellChrome";

export default function TeacherStudentsPage() {
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
      label: "الباقة المفعلة",
      render: (s) => (
        <div className="flex items-center gap-2">
          <span className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1 text-xs font-bold text-[var(--admin-primary)]">
            {s.activatedPackageName}
          </span>
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
            <span>{date.toLocaleDateString("ar-EG", { year: "numeric", month: "long", day: "numeric" })}</span>
          </div>
        );
      },
    },
  ];

  return (
    <TeacherShellChrome
      activePath="/teacher/students"
      sectionLabel="قائمة الطلاب"
      pageTitle="الطلاب النشطون"
      subtitle="استعرض جميع الطلاب المشتركين والمفعلين لباقاتك الدراسية وتتبع تواريخ انضمامهم."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        {/* Stats Strip */}
        <section className="grid grid-cols-1 gap-6 md:grid-cols-2">
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
          <AdminDataTable
            data={filteredStudents}
            columns={columns}
            loading={loading}
            rowKey={(s) => s.id}
            emptyMessage="لا يوجد طلاب مشتركون بعد أو لا توجد نتائج مطابقة لبحثك."
          />
        )}
      </div>
    </TeacherShellChrome>
  );
}
