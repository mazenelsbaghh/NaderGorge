import {
  inspectWhatsAppTemplateCapabilities,
  type WhatsAppCampaignTemplateSupport,
} from '../../../lib/whatsapp-campaign.ts';
import type { LiveSupportWhatsAppTemplate } from '@/services/live-support-service';

const DIRECT_TEXT_PLACEHOLDER_PATTERN = /\{\{(\d+)\}\}/g;

export function inspectDirectWhatsAppTemplate(
  template: LiveSupportWhatsAppTemplate,
): WhatsAppCampaignTemplateSupport {
  const capability = inspectWhatsAppTemplateCapabilities(template);
  if (!capability.supported) return capability;
  if (capability.parameters.some((parameter) => parameter.componentType === 'BUTTON')) {
    return unsupportedDirectTemplate('زر الرابط الديناميكي غير متاح في الإرسال المباشر حاليًا.');
  }
  if (directTextParameterCount(template) !== capability.parameters.length) {
    return unsupportedDirectTemplate('تركيب متغيرات النص يحتاج إرسالًا مخصصًا وغير متاح مباشرةً.');
  }
  return capability;
}

export function whatsAppTemplateParameterCount(template: LiveSupportWhatsAppTemplate) {
  const support = inspectDirectWhatsAppTemplate(template);
  return support.supported ? support.parameters.length : 0;
}

export function renderWhatsAppTemplatePreview(
  template: LiveSupportWhatsAppTemplate,
  parameters: string[],
) {
  const support = inspectDirectWhatsAppTemplate(template);
  if (!support.supported) return '';
  return template.components
    .map((component, componentIndex) => {
      const componentType = (component.type ?? '').toUpperCase();
      return (component.text ?? '').replace(DIRECT_TEXT_PLACEHOLDER_PATTERN, (_, rawPosition: string) => {
        const position = Number(rawPosition);
        const valueIndex = support.parameters.findIndex((requirement) =>
          requirement.componentType === componentType &&
          requirement.componentIndex === componentIndex &&
          requirement.parameterIndex === position);
        return valueIndex >= 0 ? parameters[valueIndex] || '…' : '…';
      });
    })
    .filter(Boolean)
    .join('\n');
}

function directTextParameterCount(template: LiveSupportWhatsAppTemplate) {
  return template.components.reduce((count, component) =>
    count + ((component.text ?? '').match(DIRECT_TEXT_PLACEHOLDER_PATTERN)?.length ?? 0), 0);
}

function unsupportedDirectTemplate(reason: string): WhatsAppCampaignTemplateSupport {
  return { supported: false, reason, parameters: [] };
}
