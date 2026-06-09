import React, { useState, useEffect, useCallback } from "react";
import { X, Calendar, PhoneCall, Loader2 } from "lucide-react";
import { crmService, CrmCallLogDto } from "@/services/crm-service";
import NeumorphButton from "@/components/ui/neumorph-button";
import toast from "react-hot-toast";
import { formatDate } from "@/components/admin/admin-utils";

interface CrmCallLogModalProps {
  isOpen: boolean;
  onClose: () => void;
  studentId: string;
  studentName: string;
  onSuccess: () => void;
}

const OUTCOME_OPTIONS = [
  { value: "Completed", label: "تم الاتصال بنجاح" },
  { value: "Pending", label: "معلق / قيد المتابعة" },
  { value: "NoAnswer", label: "لم يرد" },
  { value: "Postponed", label: "تم التأجيل" },
  { value: "Closed", label: "مغلق / تم إلغاء الاشتراك" },
];

export const CrmCallLogModal: React.FC<CrmCallLogModalProps> = ({
  isOpen,
  onClose,
  studentId,
  studentName,
  onSuccess,
}) => {
  const [outcome, setOutcome] = useState("Completed");
  const [notes, setNotes] = useState("");
  const [nextFollowUpDate, setNextFollowUpDate] = useState("");
  const [history, setHistory] = useState<CrmCallLogDto[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const loadHistory = useCallback(async () => {
    try {
      setLoadingHistory(true);
      const data = await crmService.getCallHistory(studentId);
      setHistory(data);
    } catch {
      toast.error("تعذر تحميل تاريخ المكالمات");
    } finally {
      setLoadingHistory(false);
    }
  }, [studentId]);

  useEffect(() => {
    if (isOpen && studentId) {
      loadHistory();
    }
  }, [isOpen, studentId, loadHistory]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submitting) return;

    if ((outcome === "Postponed" || outcome === "Pending") && !nextFollowUpDate) {
      toast.error("يرجى تحديد موعد المتابعة القادم");
      return;
    }

    try {
      setSubmitting(true);
      await crmService.logCall(studentId, {
        outcome,
        notes: notes.trim(),
        nextFollowUpDate: nextFollowUpDate ? new Date(nextFollowUpDate).toISOString() : undefined,
      });
      toast.success("تم تسجيل المكالمة بنجاح");
      setNotes("");
      setNextFollowUpDate("");
      onSuccess();
      onClose();
    } catch {
      toast.error("فشل تسجيل المكالمة");
    } finally {
      setSubmitting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm animate-[fadeIn_0.2s_ease-out]">
      <div className="bg-[var(--admin-card)] border border-[var(--admin-border)] w-full max-w-4xl rounded-[2rem] shadow-[0_24px_70px_var(--admin-shadow)] overflow-hidden flex flex-col md:flex-row h-[80vh] max-h-[700px]" dir="rtl">
        {/* Right side: Log Call Form */}
        <div className="flex-1 p-6 border-l border-[var(--admin-border)] flex flex-col justify-between overflow-y-auto">
          <div>
            <div className="flex justify-between items-center mb-6">
              <h2 className="text-xl font-black text-[var(--admin-text-strong)] flex items-center gap-2">
                <PhoneCall className="h-5 w-5 text-[var(--admin-primary)]" />
                تسجيل مكالمة للطلب: {studentName}
              </h2>
              <button onClick={onClose} className="p-1 rounded-full hover:bg-[var(--admin-card-soft)] transition">
                <X className="h-5 w-5 text-[var(--admin-muted)]" />
              </button>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">نتيجة المكالمة</label>
                <select
                  value={outcome}
                  onChange={(e) => setOutcome(e.target.value)}
                  className="admin-input w-full"
                >
                  {OUTCOME_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>

              {(outcome === "Postponed" || outcome === "Pending") && (
                <div>
                  <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block flex items-center gap-1">
                    <Calendar className="h-3.5 w-3.5" />
                    تاريخ المتابعة القادمة
                  </label>
                  <input
                    type="datetime-local"
                    value={nextFollowUpDate}
                    onChange={(e) => setNextFollowUpDate(e.target.value)}
                    className="admin-input w-full text-right"
                    required
                  />
                </div>
              )}

              <div>
                <label className="text-xs font-bold text-[var(--admin-muted)] mb-1 block">تفاصيل / ملاحظات المكالمة</label>
                <textarea
                  rows={4}
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder="اكتب خلاصة المكالمة وما تم الاتفاق عليه..."
                  className="admin-input w-full resize-none"
                />
              </div>
            </form>
          </div>

          <div className="flex justify-end gap-3 pt-4 border-t border-[var(--admin-border)]">
            <NeumorphButton type="button" onClick={onClose} intent="ghost" size="md">إلغاء</NeumorphButton>
            <NeumorphButton
              type="submit"
              onClick={handleSubmit}
              disabled={submitting}
              loading={submitting}
              intent="primary"
              size="md"
              pill
            >
              حفظ وتحديث الحالة
            </NeumorphButton>
          </div>
        </div>

        {/* Left side: Call History Timeline */}
        <div className="w-full md:w-[350px] bg-[var(--admin-bg)] p-6 overflow-y-auto flex flex-col">
          <h3 className="text-sm font-black text-[var(--admin-text)] mb-4 border-b border-[var(--admin-border)] pb-2">تاريخ المتابعة السابقة</h3>
          
          {loadingHistory ? (
            <div className="flex-1 flex items-center justify-center">
              <Loader2 className="h-6 w-6 animate-spin text-[var(--admin-primary)]" />
            </div>
          ) : history.length > 0 ? (
            <div className="space-y-4 relative pr-4 border-r-2 border-dashed border-[var(--admin-border)] mr-2 flex-1">
              {history.map((log) => (
                <div key={log.id} className="relative group">
                  {/* Timeline bullet */}
                  <div className="absolute right-[-21px] top-1.5 w-2 h-2 rounded-full bg-[var(--admin-primary)] ring-4 ring-[var(--admin-bg)]" />
                  
                  <div className="text-[10px] text-[var(--admin-muted)] font-bold mb-0.5">
                    {formatDate(log.callDate)} - {log.agentName}
                  </div>
                  
                  <span className={`inline-block px-2 py-0.5 rounded-full text-[9px] font-black mb-1 ${
                    log.outcome === "Completed" ? "bg-emerald-500/10 text-emerald-500" :
                    log.outcome === "NoAnswer" ? "bg-red-500/10 text-red-500" :
                    log.outcome === "Postponed" ? "bg-amber-500/10 text-amber-500" :
                    "bg-gray-500/10 text-gray-500"
                  }`}>
                    {OUTCOME_OPTIONS.find(o => o.value === log.outcome)?.label || log.outcome}
                  </span>
                  
                  {log.notes && (
                    <p className="text-xs text-[var(--admin-text)] leading-relaxed">{log.notes}</p>
                  )}
                  {log.nextFollowUpDate && (
                    <div className="text-[9px] text-[var(--admin-primary)] mt-1 flex items-center gap-1 font-bold">
                      <Calendar className="h-3 w-3" />
                      المتابعة القادمة: {formatDate(log.nextFollowUpDate)}
                    </div>
                  )}
                </div>
              ))}
            </div>
          ) : (
            <div className="flex-1 flex items-center justify-center text-xs text-[var(--admin-muted)] italic text-center">
              لا توجد مكالمات مسجلة سابقاً لهذا الطالب.
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
