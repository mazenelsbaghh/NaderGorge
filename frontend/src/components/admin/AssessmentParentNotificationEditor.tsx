'use client';

import { useEffect, useState } from 'react';
import { assessmentRevisionService, type AssessmentKind, type AssessmentParentNotificationSettings } from '@/services/assessment-revision-service';
import type { LiveSupportWhatsAppTemplate } from '@/services/live-support-service';
import { inspectDirectWhatsAppTemplate, renderWhatsAppTemplatePreview } from '@/components/live-support/staff/whatsapp-template';
import { getApiErrorSummary } from '@/lib/api-errors';

const sources = [
  ['ParentName', 'اسم ولي الأمر', 'ولي أمر أحمد'], ['StudentName', 'اسم الطالب', 'أحمد محمد'],
  ['AssessmentName', 'اسم الامتحان أو الواجب', 'واجب الحصة الأولى'], ['Score', 'درجة الطالب', '35'],
  ['TotalScore', 'الدرجة النهائية', '40'], ['Percentage', 'النسبة المئوية', '87.5%'],
  ['Evaluation', 'التقييم', 'جيد جدًا'], ['SubjectName', 'المادة', 'التاريخ'],
  ['LessonName', 'الحصة', 'الحصة الأولى'], ['TeacherName', 'المدرس', 'اسم المدرس'],
  ['Literal', 'نص ثابت', ''],
] as const;

export function AssessmentParentNotificationEditor({ kind, settings, onChange, disabled = false }: {
  kind: AssessmentKind; settings: AssessmentParentNotificationSettings;
  onChange: (settings: AssessmentParentNotificationSettings) => void; disabled?: boolean;
}) {
  const [templates, setTemplates] = useState<LiveSupportWhatsAppTemplate[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);
  useEffect(() => {
    if (!settings.enabled) return;
    let cancelled = false;
    setLoading(true); setError('');
    assessmentRevisionService.notificationTemplates(kind)
      .then(loaded => { if (!cancelled) setTemplates(loaded); })
      .catch(cause => { if (!cancelled) setError(getApiErrorSummary(cause, 'تعذر تحميل قوالب واتساب.')); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [kind, settings.enabled, reload]);
  const supported = templates.filter(template => inspectDirectWhatsAppTemplate(template).supported
    && !template.components.some(component => component.type?.toUpperCase() === 'HEADER' && component.format?.toUpperCase() === 'IMAGE'));
  const selected = supported.find(template => template.id === settings.templateId);
  const support = selected ? inspectDirectWhatsAppTemplate(selected) : null;
  const changed = Boolean(selected && selected.fingerprint !== settings.templateFingerprint);
  const preview = selected ? renderWhatsAppTemplatePreview(selected, settings.parameters.map(parameter =>
    parameter.source === 'Literal' ? parameter.literal ?? '' : sources.find(source => source[0] === parameter.source)?.[2] ?? '')) : '';
  function selectTemplate(id: string) {
    const template = supported.find(candidate => candidate.id === id);
    const capability = template ? inspectDirectWhatsAppTemplate(template) : null;
    const defaults = ['ParentName', 'StudentName', 'Score', 'TotalScore', 'SubjectName', 'LessonName'];
    onChange({ enabled: true, templateId: template?.id ?? null, templateFingerprint: template?.fingerprint ?? null,
      parameters: capability?.supported ? capability.parameters.map((_, index) => ({ source: capability.parameters.length === 6 ? defaults[index] : 'StudentName' })) : [] });
  }
  return <fieldset disabled={disabled} className="space-y-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
    <legend className="px-2 text-lg font-bold text-[var(--admin-text)]">رسالة النتيجة لولي الأمر</legend>
    <label className="flex min-h-11 items-center gap-3 font-bold text-[var(--admin-text)]">
      <input type="checkbox" checked={settings.enabled} onChange={event => onChange({ ...settings, enabled: event.target.checked })} />
      إرسال واتساب تلقائيًا بعد اكتمال التصحيح
    </label>
    <p className="text-sm leading-6 text-[var(--admin-muted)]">تُرسل مرة واحدة لكل محاولة بعد اكتمال التصحيح، إلى أول رقم صالح حسب ترتيب ولي الأمر في الإعدادات.</p>
    {settings.enabled && <div className="space-y-4">
      {loading && <p role="status">جاري تحميل القوالب…</p>}
      {error && <div role="alert" className="space-y-2 text-[var(--admin-danger)]"><p>{error}</p><button type="button" className="admin-btn-ghost min-h-11" onClick={() => setReload(current => current + 1)}>إعادة تحميل القوالب</button></div>}
      <label className="block space-y-2 text-sm font-bold"><span>قالب رسالة النتيجة</span>
        <select className="admin-input w-full" disabled={loading} value={settings.templateId ?? ''} onChange={event => selectTemplate(event.target.value)}>
          <option value="">اختر قالب خدمة معتمدًا</option>
          {settings.templateId && !selected && <option value={settings.templateId}>القالب السابق غير متاح، اختر قالبًا آخر</option>}
          {supported.map(template => <option key={template.id} value={template.id}>{template.name} ({template.language})</option>)}
        </select>
      </label>
      {!loading && !error && supported.length === 0 && <p role="status" className="text-sm text-[var(--admin-muted)]">لا توجد قوالب خدمة نصية معتمدة. زامن القوالب من واتساب في مركز الدعم ثم حدّثها هنا.</p>}
      <button type="button" disabled={loading} className="admin-btn-ghost min-h-11" onClick={() => setReload(current => current + 1)}>تحديث القوالب</button>
      {changed && <div role="alert" className="text-sm text-[var(--admin-danger)]"><p>تغيّر القالب منذ الحفظ. حمّل نسخته الحالية وراجع المتغيرات قبل الحفظ.</p><button type="button" className="admin-btn-ghost min-h-11" onClick={() => selectTemplate(settings.templateId!)}>استخدام النسخة الحالية</button></div>}
      {support?.supported && settings.parameters.map((parameter, index) => <div key={index} className="grid gap-3 sm:grid-cols-2">
        <label className="block space-y-2 text-sm"><span>المتغير {index + 1} ({support.parameters[index]?.componentType === 'HEADER' ? 'العنوان' : 'نص الرسالة'})</span>
          <select className="admin-input" value={parameter.source} onChange={event => onChange({ ...settings, parameters: settings.parameters.map((previous, position) => position === index ? { source: event.target.value } : previous) })}>
            {sources.map(([source, label]) => <option key={source} value={source}>{label}</option>)}
          </select>
        </label>
        {parameter.source === 'Literal' && <label className="block space-y-2 text-sm"><span>النص الثابت للمتغير {index + 1}</span><input className="admin-input" maxLength={1000} value={parameter.literal ?? ''} onChange={event => onChange({ ...settings, parameters: settings.parameters.map((previous, position) => position === index ? { ...previous, literal: event.target.value } : previous) })} /></label>}
      </div>)}
      {preview && <div className="space-y-2 rounded-lg bg-[var(--admin-card-soft)] p-4"><p className="text-sm font-bold">معاينة ببيانات توضيحية</p><p dir="auto" className="whitespace-pre-wrap break-words text-sm leading-7">{preview}</p></div>}
    </div>}
  </fieldset>;
}
