import React, { useState, useEffect } from "react";
import { BarChart3, Users, CheckCircle, AlertCircle, Calendar } from "lucide-react";
import { crmService, CrmPerformanceReportDto } from "@/services/crm-service";
import toast from "react-hot-toast";

export const CrmReportsPanel: React.FC = () => {
  const [report, setReport] = useState<CrmPerformanceReportDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadReport();
  }, []);

  const loadReport = async () => {
    try {
      setLoading(true);
      const data = await crmService.getPerformanceReport();
      setReport(data);
    } catch {
      toast.error("فشل تحميل تقارير الأداء");
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="flex justify-center py-12 text-[var(--admin-muted)] text-sm">
        جاري تحميل التقارير والإحصائيات...
      </div>
    );
  }

  if (!report) return null;

  return (
    <div className="space-y-8 animate-[fadeIn_0.3s_ease-out]" dir="rtl">
      {/* Metrics Grid */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="flex justify-between items-start">
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)]">إجمالي الاتصالات</p>
              <h4 className="text-2xl font-black text-[var(--admin-text-strong)] mt-1">{report.totalCalls}</h4>
            </div>
            <div className="p-3 bg-[var(--admin-primary-15)] text-[var(--admin-primary)] rounded-xl">
              <BarChart3 className="h-5 w-5" />
            </div>
          </div>
        </div>

        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="flex justify-between items-start">
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)]">مكالمات ناجحة</p>
              <h4 className="text-2xl font-black text-emerald-500 mt-1">
                {report.outcomeBreakdown["Completed"] || 0}
              </h4>
            </div>
            <div className="p-3 bg-emerald-500/10 text-emerald-500 rounded-xl">
              <CheckCircle className="h-5 w-5" />
            </div>
          </div>
        </div>

        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="flex justify-between items-start">
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)]">لم يرد / مغلق</p>
              <h4 className="text-2xl font-black text-red-500 mt-1">
                {report.outcomeBreakdown["NoAnswer"] || 0}
              </h4>
            </div>
            <div className="p-3 bg-red-500/10 text-red-500 rounded-xl">
              <AlertCircle className="h-5 w-5" />
            </div>
          </div>
        </div>

        <div className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 shadow-sm">
          <div className="flex justify-between items-start">
            <div>
              <p className="text-xs font-bold text-[var(--admin-muted)]">مؤجلة / قيد المتابعة</p>
              <h4 className="text-2xl font-black text-amber-500 mt-1">
                {(report.outcomeBreakdown["Postponed"] || 0) + (report.outcomeBreakdown["Pending"] || 0)}
              </h4>
            </div>
            <div className="p-3 bg-amber-500/10 text-amber-500 rounded-xl">
              <Calendar className="h-5 w-5" />
            </div>
          </div>
        </div>
      </div>

      {/* Agents Performance Table */}
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] overflow-hidden shadow-sm">
        <div className="p-6 border-b border-[var(--admin-border)] flex items-center gap-2">
          <Users className="h-5 w-5 text-[var(--admin-primary)]" />
          <h3 className="text-base font-black text-[var(--admin-text-strong)]">نشاط وأداء الموظفين</h3>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-right border-collapse text-xs">
            <thead>
              <tr className="bg-[var(--admin-bg)] border-b border-[var(--admin-border)] text-[var(--admin-muted)] font-black uppercase">
                <th className="p-4">اسم الموظف</th>
                <th className="p-4 text-center">عدد الاتصالات الكلي</th>
                <th className="p-4 text-center">الاتصالات الناجحة</th>
                <th className="p-4 text-center">لم يرد / مغلق</th>
                <th className="p-4 text-center">نسبة النجاح</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--admin-border)]">
              {report.agentPerformance.map((agent) => {
                const successRate = agent.callsMade > 0 
                  ? Math.round((agent.completedCalls / agent.callsMade) * 100) 
                  : 0;

                return (
                  <tr key={agent.agentId} className="hover:bg-[var(--admin-card-soft)] transition">
                    <td className="p-4 font-bold text-[var(--admin-text)]">{agent.agentName}</td>
                    <td className="p-4 text-center font-bold">{agent.callsMade}</td>
                    <td className="p-4 text-center text-emerald-500 font-bold">{agent.completedCalls}</td>
                    <td className="p-4 text-center text-red-500 font-bold">{agent.noAnswerCalls}</td>
                    <td className="p-4 text-center">
                      <span className={`inline-block px-3 py-1 rounded-full font-black ${
                        successRate >= 60 ? "bg-emerald-500/10 text-emerald-500" :
                        successRate >= 40 ? "bg-amber-500/10 text-amber-500" :
                        "bg-red-500/10 text-red-500"
                      }`}>
                        {successRate}%
                      </span>
                    </td>
                  </tr>
                );
              })}

              {report.agentPerformance.length === 0 && (
                <tr>
                  <td colSpan={5} className="p-8 text-center text-[var(--admin-muted)] italic">
                    لا توجد إحصائيات للموظفين بعد.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
