'use client';

import { useEffect, useMemo, useState } from 'react';
import { LoaderCircle, MessageCircle, X } from 'lucide-react';
import { liveSupportService, type LiveSupportWhatsAppTemplate } from '@/services/live-support-service';
import { requirementLabel } from '@/lib/whatsapp-campaign';
import {
  inspectDirectWhatsAppTemplate,
  renderWhatsAppTemplatePreview,
} from '@/components/live-support/staff/whatsapp-template';

interface WhatsAppTemplatePickerProps {
  disabled: boolean;
  onSend: (template: LiveSupportWhatsAppTemplate, parameters: string[], previewText: string) => Promise<void>;
}

export function WhatsAppTemplatePicker({ disabled, onSend }: WhatsAppTemplatePickerProps) {
  const [open, setOpen] = useState(false);
  const [templates, setTemplates] = useState<LiveSupportWhatsAppTemplate[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [parameters, setParameters] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');
  const selected = templates.find(template => template.id === selectedId);
  const selectedSupport = useMemo(
    () => selected ? inspectDirectWhatsAppTemplate(selected) : undefined,
    [selected],
  );
  const parameterRequirements = selectedSupport?.supported ? selectedSupport.parameters : [];
  const parametersComplete = parameters.length === parameterRequirements.length &&
    parameters.every(value => Boolean(value.trim()));

  useEffect(() => {
    if (!open || templates.length > 0) return;
    void loadTemplates();
  }, [open, templates.length]);

  useEffect(() => {
    setParameters(Array.from({ length: parameterRequirements.length }, () => ''));
  }, [parameterRequirements.length, selectedId]);

  async function loadTemplates() {
    setLoading(true);
    setError('');
    try {
      const available = await liveSupportService.getWhatsAppTemplates();
      const supported = available.filter(template => inspectDirectWhatsAppTemplate(template).supported);
      setTemplates(supported);
      setSelectedId(current => supported.some(template => template.id === current) ? current : supported[0]?.id ?? '');
    } catch { setError('تعذر تحميل قوالب واتساب.'); }
    finally { setLoading(false); }
  }

  async function send() {
    if (!selected || !selectedSupport?.supported || !parametersComplete) return;
    setSending(true);
    setError('');
    try {
      await onSend(selected, parameters.map(value => value.trim()), renderWhatsAppTemplatePreview(selected, parameters));
      setOpen(false);
    } catch { setError('تعذر إرسال قالب واتساب. راجع القيم وحاول مرة أخرى.'); }
    finally { setSending(false); }
  }

  if (!open) return <button type="button" disabled={disabled} onClick={() => setOpen(true)} className="inline-flex h-11 w-full items-center justify-center gap-1.5 rounded-xl bg-[#25D366] px-3 text-xs font-bold text-[#0A1D3D] hover:bg-[#20bf5b] disabled:opacity-50 sm:w-auto"><MessageCircle size={17}/>قالب واتساب</button>;

  return <section className="mb-3 rounded-xl bg-[var(--admin-card-soft)] p-3" aria-label="إرسال قالب واتساب">
    <div className="flex items-center justify-between gap-3">
      <div><h3 className="font-bold text-[var(--admin-text)]">إرسال قالب واتساب</h3><p className="mt-0.5 text-xs text-[var(--admin-muted)]">استخدم قالبًا معتمدًا لبدء المحادثة أو للرد خارج نافذة 24 ساعة.</p></div>
      <button type="button" onClick={() => setOpen(false)} aria-label="إغلاق قوالب واتساب" className="grid size-10 place-items-center rounded-lg hover:bg-[var(--admin-hover)]"><X size={18}/></button>
    </div>
    <div className="mt-3 flex flex-wrap gap-2">
      <select
        aria-label="قالب واتساب"
        disabled={loading || sending}
        value={selectedId}
        onChange={(event) => {
          setSelectedId(event.target.value);
          setParameters([]);
        }}
        className="min-h-11 min-w-56 flex-1 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]"
      >
        {templates.length === 0 ? <option value="">لا توجد قوالب معتمدة</option> : templates.map(template => <option key={template.id} value={template.id}>{template.name} · {template.language}</option>)}
      </select>
      {loading ? <span className="inline-flex min-h-11 items-center gap-2 px-3 text-sm text-[var(--admin-muted)]"><LoaderCircle className="animate-spin" size={16}/>جارٍ التحميل</span> : null}
    </div>
    {selected ? <div className="mt-3 space-y-2">
      {parameterRequirements.map((requirement, index) => {
        const value = parameters[index] ?? '';
        return (
          <label key={requirement.key} className="block text-xs font-bold text-[var(--admin-text)]">
            {requirementLabel(requirement)} · المتغير {requirement.parameterIndex}
            <input
              value={value}
              onChange={(event) => setParameters(current => current.map((entry, entryIndex) =>
                entryIndex === index ? event.target.value : entry))}
              maxLength={1000}
              className="mt-1 h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-normal outline-none focus-visible:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-15)]"
            />
          </label>
        );
      })}
      <p dir="auto" className="whitespace-pre-wrap rounded-lg bg-[var(--admin-card)] p-3 text-sm leading-6 text-[var(--admin-text)]">{renderWhatsAppTemplatePreview(selected, parameters)}</p>
      <button type="button" disabled={sending || !parametersComplete} onClick={() => void send()} className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50">{sending ? <LoaderCircle className="animate-spin" size={17}/> : <MessageCircle size={17}/>}إرسال القالب</button>
    </div> : null}
    {error ? <p role="alert" className="mt-2 text-sm font-medium text-[var(--admin-danger)]">{error}</p> : null}
  </section>;
}
