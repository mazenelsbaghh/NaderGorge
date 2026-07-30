'use client';

import { useCallback, useEffect, useState } from 'react';
import { CheckCircle2, Clock3, Loader2, MinusCircle, PlusCircle, UserX } from 'lucide-react';
import toast from 'react-hot-toast';
import { hrPayrollService, type PayrollRuleDto } from '@/services/hr-payroll-service';

type AdjustmentTemplate = {
  code: string;
  name: string;
  description: string;
  expression: string;
  classification: 'Earning' | 'Deduction';
  icon: typeof Clock3;
};

const templates: AdjustmentTemplate[] = [
  { code: 'LATE_DEDUCT', name: 'خصم التأخير', description: 'خصم لكل دقيقة بعد بداية الشفت.', expression: 'attendance.late_minutes * rate', classification: 'Deduction', icon: Clock3 },
  { code: 'EARLY_LEAVE_DEDUCT', name: 'خصم الانصراف المبكر', description: 'خصم لكل دقيقة قبل نهاية الشفت.', expression: 'attendance.early_leave_minutes * rate', classification: 'Deduction', icon: MinusCircle },
  { code: 'ABSENCE_DEDUCT', name: 'خصم الغياب', description: 'خصم لكل يوم غياب معتمد في الحضور.', expression: 'attendance.absence_days * rate', classification: 'Deduction', icon: UserX },
  { code: 'OVERTIME_BONUS', name: 'بدل العمل الإضافي', description: 'زيادة لكل دقيقة إضافية مسجلة بعد الشفت.', expression: 'attendance.overtime_minutes * rate', classification: 'Earning', icon: PlusCircle },
];

function toArabicNumber(value: number) {
  return new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 }).format(value);
}

export function AttendanceAdjustmentRules() {
  const [rules, setRules] = useState<PayrollRuleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [savingCode, setSavingCode] = useState<string | null>(null);
  const [rates, setRates] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const config = await hrPayrollService.config();
      setRules(config.rules);
    } catch {
      toast.error('تعذر تحميل قواعد الحضور المالية');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const existingRule = (template: AdjustmentTemplate) => rules.find((rule) => rule.expression === template.expression && rule.isActive);

  async function publish(template: AdjustmentTemplate) {
    const rate = Number(rates[template.code]);
    if (!Number.isFinite(rate) || rate <= 0) {
      toast.error('أدخل قيمة صحيحة أكبر من صفر');
      return;
    }
    if (existingRule(template)) {
      toast.error('هذه القاعدة منشورة بالفعل');
      return;
    }

    setSavingCode(template.code);
    try {
      const config = await hrPayrollService.config();
      let component = config.components.find((item) => item.code === template.code);
      if (!component) {
        const created = await hrPayrollService.createComponent({
          code: template.code,
          name: template.name,
          classification: template.classification,
          isTaxable: false,
          isInsurable: false,
        });
        component = { id: created.id, code: template.code, name: template.name, classification: template.classification, isTaxable: false, isInsurable: false, isActive: true };
      }
      await hrPayrollService.createRule({
        payComponentId: component.id,
        name: template.name,
        expression: template.expression,
        rate,
        effectiveFrom: new Date().toISOString().slice(0, 10),
        effectiveTo: null,
        priority: 20,
      });
      toast.success(`تم تفعيل ${template.name}`);
      await load();
    } catch {
      toast.error('تعذر نشر القاعدة، راجع صلاحيات الرواتب أو حاول مرة أخرى');
    } finally {
      setSavingCode(null);
    }
  }

  if (loading) return <div className="admin-panel py-16 text-center"><Loader2 className="mx-auto h-6 w-6 animate-spin" /></div>;

  return (
    <div className="space-y-5">
      <section className="admin-panel max-w-4xl">
        <h2 className="text-xl font-black text-[var(--admin-text)]">قواعد تلقائية مرتبطة بالشفت والحضور</h2>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-[var(--admin-muted)]">تُقرأ الدقائق والأيام من سجل الحضور عند تجهيز دورة الراتب. التغييرات لا تؤثر في دورات راتب أُغلقت بالفعل.</p>
      </section>

      <section className="grid gap-4 lg:grid-cols-2">
        {templates.map((template) => {
          const activeRule = existingRule(template);
          const Icon = template.icon;
          const isEarning = template.classification === 'Earning';
          return (
            <article key={template.code} className="admin-panel flex flex-col gap-4 p-5">
              <div className="flex items-start gap-3">
                <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${isEarning ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300' : 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300'}`}><Icon className="h-5 w-5" /></span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2"><h3 className="font-black text-[var(--admin-text)]">{template.name}</h3>{activeRule && <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300"><CheckCircle2 className="h-3.5 w-3.5" />مفعّلة</span>}</div>
                  <p className="mt-1 text-sm text-[var(--admin-muted)]">{template.description}</p>
                </div>
              </div>
              {activeRule ? <div className="rounded-xl bg-[var(--admin-card-soft)] px-4 py-3 text-sm text-[var(--admin-text)]">القيمة الحالية: <strong>{toArabicNumber(activeRule.rate)} ج.م</strong> {template.code === 'ABSENCE_DEDUCT' ? 'لكل يوم' : 'لكل دقيقة'}</div> : <div className="flex flex-col gap-3 sm:flex-row sm:items-end"><label className="flex-1 text-sm font-bold text-[var(--admin-text)]">{template.code === 'ABSENCE_DEDUCT' ? 'قيمة الخصم لكل يوم (ج.م)' : 'القيمة لكل دقيقة (ج.م)'}<input inputMode="decimal" type="number" min="0.01" step="0.01" value={rates[template.code] ?? ''} onChange={(event) => setRates((current) => ({ ...current, [template.code]: event.target.value }))} placeholder="مثال: 5" className="admin-input mt-2" /></label><button type="button" disabled={savingCode === template.code} onClick={() => void publish(template)} className="admin-btn-primary min-h-11 justify-center whitespace-nowrap disabled:opacity-50">{savingCode === template.code ? <Loader2 className="h-4 w-4 animate-spin" /> : isEarning ? 'تفعيل الزيادة' : 'تفعيل الخصم'}</button></div>}
            </article>
          );
        })}
      </section>
    </div>
  );
}
