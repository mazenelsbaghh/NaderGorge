import React, { useState, useEffect, useCallback } from "react";
import { Search, Phone, MessageSquare, Calendar, UserPlus, AlertCircle, RefreshCw } from "lucide-react";
import { crmService, CrmStudentDto } from "@/services/crm-service";
import { hrService, EmployeeDto } from "@/services/hr-service";
import { getWhatsAppLink } from "@/utils/phone-utils";
import { CrmCallLogModal } from "./CrmCallLogModal";
import NeumorphButton from "@/components/ui/neumorph-button";
import toast from "react-hot-toast";
import { formatDate } from "@/components/admin/admin-utils";

interface CrmStudentQueueProps {
  mode: "admin" | "agent";
}

const CRM_STATUSES = [
  { value: "Unassigned", label: "غير مسند" },
  { value: "Assigned", label: "مسند للمتابعة" },
  { value: "InProgress", label: "قيد المتابعة والاتصال" },
  { value: "Cold", label: "بارد / لم يستجب" },
  { value: "Closed", label: "مغلق / منتهي" },
];

const CRM_PRIORITIES = [
  { value: "Low", label: "منخفضة" },
  { value: "Medium", label: "متوسطة" },
  { value: "High", label: "عالية" },
  { value: "Critical", label: "حرجة" },
];

export const CrmStudentQueue: React.FC<CrmStudentQueueProps> = ({ mode }) => {
  const [students, setStudents] = useState<CrmStudentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);

  // Search & Filter state
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [agentId, setAgentId] = useState("");
  const [priority, setPriority] = useState("");
  const [onlyOverdue, setOnlyOverdue] = useState(false);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  // Modal control
  const [selectedStudent, setSelectedStudent] = useState<{ id: string; name: string } | null>(null);

  const loadStudents = useCallback(async () => {
    try {
      setLoading(true);
      const res = await crmService.getStudents({
        page,
        pageSize: 10,
        search: search.trim() || undefined,
        status: status || undefined,
        agentId: mode === "admin" ? (agentId || undefined) : undefined,
        priority: priority || undefined,
        onlyOverdue,
      });
      setStudents(res.items);
      setTotalCount(res.totalCount);
    } catch {
      toast.error("تعذر تحميل قائمة الطلاب");
    } finally {
      setLoading(false);
    }
  }, [page, search, status, agentId, priority, onlyOverdue, mode]);

  useEffect(() => {
    void loadStudents();
  }, [loadStudents]);

  useEffect(() => {
    if (mode === "admin") {
      void hrService.listEmployees().then(setEmployees).catch(() => {});
    }
  }, [mode]);

  const handleAssignAgent = async (studentId: string, employeeId: string, currentPriority: string, currentNotes?: string) => {
    try {
      const assignedId = employeeId === "unassigned" ? undefined : employeeId;
      await crmService.assignStudent(studentId, {
        assignedAgentId: assignedId,
        priority: currentPriority,
        notes: currentNotes,
      });
      toast.success("تم تحديث الموظف المسؤول بنجاح");
      void loadStudents();
    } catch {
      toast.error("تعذر تعيين الموظف");
    }
  };

  const handlePriorityChange = async (studentId: string, newPriority: string, currentAgentId?: string, currentNotes?: string) => {
    try {
      await crmService.assignStudent(studentId, {
        assignedAgentId: currentAgentId || undefined,
        priority: newPriority,
        notes: currentNotes,
      });
      toast.success("تم تحديث الأولوية بنجاح");
      void loadStudents();
    } catch {
      toast.error("تعذر تحديث الأولوية");
    }
  };

  const openWhatsApp = (phone: string, name: string) => {
    const message = `مرحباً ${name}، معك فريق المتابعة والدعم الفني من منصة مسار. نود الاطمئنان على سير خطتك التعليمية ومساعدتك في أي استفسارات.`;
    const link = getWhatsAppLink(phone, message);
    window.open(link, "_blank");
  };

  return (
    <div className="space-y-6 text-right" dir="rtl">
      {/* Search and Filters Bar */}
      <div className="p-4 bg-[var(--admin-card)] border border-[var(--admin-border)] rounded-2xl flex flex-wrap gap-4 items-center shadow-sm">
        <div className="flex-1 min-w-[200px] flex items-center bg-[var(--admin-bg)] rounded-xl border border-[var(--admin-border)] px-3 py-2">
          <Search className="text-[var(--admin-muted)] h-4 w-4 ml-2" />
          <input
            type="text"
            placeholder="ابحث عن طالب بالاسم أو الهاتف..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            className="bg-transparent border-none outline-none text-xs text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] w-full text-right"
          />
        </div>

        <select
          value={status}
          onChange={(e) => {
            setStatus(e.target.value);
            setPage(1);
          }}
          className="admin-input min-w-[150px]"
        >
          <option value="">كل الحالات</option>
          {CRM_STATUSES.map((s) => (
            <option key={s.value} value={s.value}>
              {s.label}
            </option>
          ))}
        </select>

        {mode === "admin" && (
          <select
            value={agentId}
            onChange={(e) => {
              setAgentId(e.target.value);
              setPage(1);
            }}
            className="admin-input min-w-[180px]"
          >
            <option value="">كل موظفي المتابعة</option>
            {employees.map((emp) => (
              <option key={emp.userId} value={emp.userId}>
                {emp.fullName}
              </option>
            ))}
          </select>
        )}

        <select
          value={priority}
          onChange={(e) => {
            setPriority(e.target.value);
            setPage(1);
          }}
          className="admin-input min-w-[140px]"
        >
          <option value="">كل الأولويات</option>
          {CRM_PRIORITIES.map((p) => (
            <option key={p.value} value={p.value}>
              {p.label}
            </option>
          ))}
        </select>

        <label className="flex items-center gap-2 text-xs font-bold text-[var(--admin-text)] cursor-pointer">
          <input
            type="checkbox"
            checked={onlyOverdue}
            onChange={(e) => {
              setOnlyOverdue(e.target.checked);
              setPage(1);
            }}
            className="rounded border-[var(--admin-border)] text-[var(--admin-primary)] focus:ring-[var(--admin-primary)]"
          />
          عرض المتأخرين فقط
        </label>

        <button
          onClick={() => void loadStudents()}
          className="p-2.5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] hover:bg-[var(--admin-hover)] transition text-[var(--admin-muted)]"
          title="تحديث"
        >
          <RefreshCw className="h-4 w-4" />
        </button>
      </div>

      {/* Student List View */}
      {loading ? (
        <div className="text-center py-12 text-[var(--admin-muted)] text-sm">
          جاري تحميل القائمة...
        </div>
      ) : students.length > 0 ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {students.map((student) => {
            const isOverdue = student.nextFollowUpDate && new Date(student.nextFollowUpDate) < new Date() && student.crmStatus !== "Closed";

            return (
              <div
                key={student.studentId}
                className={`p-5 rounded-3xl border bg-[var(--admin-card)] transition-all flex flex-col justify-between gap-4 ${
                  isOverdue ? "border-red-400 dark:border-red-600 shadow-sm" : "border-[var(--admin-border)] shadow-sm hover:border-[var(--admin-primary)]"
                }`}
              >
                <div>
                  <div className="flex justify-between items-start">
                    <div>
                      <h3 className="text-sm font-black text-[var(--admin-text-strong)]">{student.studentName}</h3>
                      <p className="text-xs text-[var(--admin-muted)] mt-0.5" dir="ltr">{student.studentPhone}</p>
                    </div>

                    <div className="flex gap-2">
                      <span className={`px-2 py-0.5 rounded-full text-[9px] font-black ${
                        student.crmStatus === "Unassigned" ? "bg-gray-500/10 text-gray-500" :
                        student.crmStatus === "Assigned" ? "bg-blue-500/10 text-blue-500" :
                        student.crmStatus === "InProgress" ? "bg-amber-500/10 text-amber-500" :
                        student.crmStatus === "Cold" ? "bg-indigo-500/10 text-indigo-500" :
                        "bg-emerald-500/10 text-emerald-500"
                      }`}>
                        {CRM_STATUSES.find((s) => s.value === student.crmStatus)?.label || student.crmStatus}
                      </span>

                      {isOverdue && (
                        <span className="px-2 py-0.5 rounded-full text-[9px] font-black bg-red-500/15 text-red-500 flex items-center gap-1 animate-pulse">
                          <AlertCircle className="h-3 w-3" />
                          موعد المكالمة متأخر
                        </span>
                      )}
                    </div>
                  </div>

                  {student.notes && (
                    <p className="text-xs text-[var(--admin-muted)] mt-3 leading-relaxed border-t border-[var(--admin-border)] pt-2.5">
                      {student.notes}
                    </p>
                  )}
                </div>

                <div className="border-t border-[var(--admin-border)] pt-3 flex flex-wrap items-center justify-between gap-3 text-xs">
                  {/* Assignment Control (Admin mode) */}
                  {mode === "admin" ? (
                    <div className="flex items-center gap-2">
                      <UserPlus className="h-4 w-4 text-[var(--admin-muted)]" />
                      <select
                        value={student.assignedAgentId || "unassigned"}
                        onChange={(e) => handleAssignAgent(student.studentId, e.target.value, student.priority, student.notes)}
                        className="admin-input py-1 text-xs"
                      >
                        <option value="unassigned">غير مسند</option>
                        {employees.map((emp) => (
                          <option key={emp.userId} value={emp.userId}>
                            {emp.fullName}
                          </option>
                        ))}
                      </select>

                      <select
                        value={student.priority}
                        onChange={(e) => handlePriorityChange(student.studentId, e.target.value, student.assignedAgentId, student.notes)}
                        className="admin-input py-1 text-xs"
                      >
                        {CRM_PRIORITIES.map((p) => (
                          <option key={p.value} value={p.value}>
                            {p.label}
                          </option>
                        ))}
                      </select>
                    </div>
                  ) : (
                    <div className="text-xs text-[var(--admin-muted)] flex items-center gap-1 font-bold">
                      الأولوية: 
                      <span className={`px-2 py-0.5 rounded-full text-[10px] font-black ${
                        student.priority === "Critical" ? "bg-red-500/10 text-red-500" :
                        student.priority === "High" ? "bg-amber-500/10 text-amber-500" :
                        student.priority === "Medium" ? "bg-blue-500/10 text-blue-500" :
                        "bg-gray-500/10 text-gray-500"
                      }`}>
                        {CRM_PRIORITIES.find(p => p.value === student.priority)?.label || student.priority}
                      </span>
                    </div>
                  )}

                  {/* Contact Actions */}
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => openWhatsApp(student.studentPhone, student.studentName)}
                      className="p-2 rounded-xl bg-emerald-500/10 hover:bg-emerald-500/20 text-emerald-500 transition"
                      title="مراسلة عبر واتساب"
                    >
                      <MessageSquare className="h-4 w-4" />
                    </button>

                    <NeumorphButton
                      onClick={() => setSelectedStudent({ id: student.studentId, name: student.studentName })}
                      intent="primary"
                      size="sm"
                      pill
                    >
                      <Phone className="h-3.5 w-3.5 ml-1.5 inline" />
                      تسجيل مكالمة
                    </NeumorphButton>
                  </div>
                </div>

                {student.nextFollowUpDate && (
                  <div className="text-[10px] text-[var(--admin-muted)] flex items-center gap-1">
                    <Calendar className="h-3 w-3" />
                    موعد الاتصال القادم: {formatDate(student.nextFollowUpDate)}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      ) : (
        <div className="text-center py-12 text-[var(--admin-muted)] text-sm border-2 border-dashed border-[var(--admin-border)] rounded-3xl">
          لا يوجد طلاب يطابقون خيارات البحث الحالية.
        </div>
      )}

      {/* Pagination */}
      {totalCount > 10 && (
        <div className="flex justify-center items-center gap-4 pt-4">
          <NeumorphButton
            disabled={page === 1}
            onClick={() => setPage((p) => p - 1)}
            intent="ghost"
            size="sm"
          >
            السابق
          </NeumorphButton>
          <span className="text-xs font-bold text-[var(--admin-muted)]">
            صفحة {page} من {Math.ceil(totalCount / 10)}
          </span>
          <NeumorphButton
            disabled={page >= Math.ceil(totalCount / 10)}
            onClick={() => setPage((p) => p + 1)}
            intent="ghost"
            size="sm"
          >
            التالي
          </NeumorphButton>
        </div>
      )}

      {/* Call Log Modal */}
      {selectedStudent && (
        <CrmCallLogModal
          isOpen={true}
          onClose={() => setSelectedStudent(null)}
          studentId={selectedStudent.id}
          studentName={selectedStudent.name}
          onSuccess={loadStudents}
        />
      )}
    </div>
  );
};
