import type { LiveSupportWhatsAppTemplate } from '@/services/live-support-service';

export function whatsAppTemplateParameterCount(template: LiveSupportWhatsAppTemplate) {
  return template.components.reduce((count, component) => count + ((component.text ?? '').match(/\{\{\d+\}\}/g)?.length ?? 0), 0);
}

export function renderWhatsAppTemplatePreview(template: LiveSupportWhatsAppTemplate, parameters: string[]) {
  let index = 0;
  return template.components
    .map(component => component.text ?? '')
    .filter(Boolean)
    .map(text => text.replace(/\{\{\d+\}\}/g, () => parameters[index++] || '…'))
    .join('\n');
}
