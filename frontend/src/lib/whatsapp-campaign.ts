import type {
  LiveSupportWhatsAppTemplate,
  WhatsAppCampaignAudienceFilters,
  WhatsAppCampaignAudiencePreview,
  WhatsAppCampaignVariableMapping,
} from '@/services/live-support-service';

export interface WhatsAppTemplateParameterRequirement {
  key: string;
  componentType: 'HEADER' | 'BODY';
  componentIndex: number;
  parameterIndex: number;
  surroundingText: string;
}

export interface WhatsAppCampaignTemplateSupport {
  supported: boolean;
  reason?: string;
  parameters: WhatsAppTemplateParameterRequirement[];
}

const PLACEHOLDER_PATTERN = /\{\{\s*(\d+)\s*\}\}/g;
const ANY_PLACEHOLDER_PATTERN = /\{\{[^{}]*\}\}/g;

export function createEmptyWhatsAppAudienceFilters(): WhatsAppCampaignAudienceFilters {
  return {
    contactRoles: ['StudentPrimary'],
    educationStages: [],
    gradeLevels: [],
    studyTracks: [],
    teacherIds: [],
    subjectIds: [],
    packageIds: [],
    crmStatuses: [],
    hasActiveAccess: null,
    hasPaidPurchase: null,
    purchaseFromUtc: null,
    purchaseToUtc: null,
    hasWatched: null,
    lessonIds: [],
    watchFromUtc: null,
    watchToUtc: null,
    hasExamAttempt: null,
    examIds: [],
    examFromUtc: null,
    examToUtc: null,
    hasHomeworkSubmission: null,
    homeworkIds: [],
    homeworkFromUtc: null,
    homeworkToUtc: null,
  };
}

export function inspectCampaignTemplate(
  template: LiveSupportWhatsAppTemplate,
): WhatsAppCampaignTemplateSupport {
  if (template.status.toUpperCase() !== 'APPROVED') {
    return { supported: false, reason: 'القالب غير معتمد من واتساب.', parameters: [] };
  }
  if (!['MARKETING', 'UTILITY'].includes(template.category.toUpperCase())) {
    return { supported: false, reason: 'الحملات تدعم قوالب MARKETING وUTILITY المعتمدة فقط.', parameters: [] };
  }

  const parameters: WhatsAppTemplateParameterRequirement[] = [];
  const seenTextComponentTypes = new Set<string>();
  let hasBody = false;

  for (const [componentIndex, component] of template.components.entries()) {
    const componentType = (component.type ?? '').toUpperCase();
    const format = (component.format ?? 'TEXT').toUpperCase();

    if (!['HEADER', 'BODY', 'FOOTER'].includes(componentType)) {
      return {
        supported: false,
        reason: 'هذه المرحلة تدعم قوالب النص فقط، بدون أزرار أو مكونات تفاعلية.',
        parameters: [],
      };
    }
    if (componentType === 'HEADER' && format !== 'TEXT') {
      return {
        supported: false,
        reason: 'رأس القالب يحتوي وسائط. الإرسال الجماعي الحالي يدعم رأسًا نصيًا فقط.',
        parameters: [],
      };
    }
    if (component.buttons?.length) {
      return {
        supported: false,
        reason: 'القوالب ذات الأزرار غير متاحة في الإصدار الحالي من الحملات.',
        parameters: [],
      };
    }
    const text = component.text ?? '';
    const allPlaceholders = text.match(ANY_PLACEHOLDER_PATTERN) ?? [];
    const numericPlaceholders = text.match(PLACEHOLDER_PATTERN) ?? [];
    if (allPlaceholders.length !== numericPlaceholders.length) {
      return {
        supported: false,
        reason: 'القالب يحتوي متغيرًا غير مرقّم. الحملات تدعم {{1}} و{{2}} فقط.',
        parameters: [],
      };
    }
    if (componentType === 'FOOTER' && allPlaceholders.length > 0) {
      return {
        supported: false,
        reason: 'تذييل القالب يجب أن يكون نصًا ثابتًا بلا متغيرات.',
        parameters: [],
      };
    }
    if (componentType !== 'HEADER' && componentType !== 'BODY') continue;

    if (seenTextComponentTypes.has(componentType)) {
      return {
        supported: false,
        reason: `القالب يحتوي أكثر من ${componentLabel(componentType)}؛ هذا التركيب غير مدعوم بأمان.`,
        parameters: [],
      };
    }
    seenTextComponentTypes.add(componentType);
    if (componentType === 'BODY') hasBody = true;

    for (const match of text.matchAll(PLACEHOLDER_PATTERN)) {
      const parameterIndex = Number(match[1]);
      if (!Number.isInteger(parameterIndex) || parameterIndex < 1) {
        return { supported: false, reason: 'ترقيم متغيرات القالب يجب أن يبدأ من {{1}}.', parameters: [] };
      }
      const start = Math.max(0, (match.index ?? 0) - 28);
      const end = Math.min(text.length, (match.index ?? 0) + match[0].length + 28);
      parameters.push({
        key: `${componentType}:${componentIndex}:${parameterIndex}`,
        componentType,
        componentIndex,
        parameterIndex,
        surroundingText: text.slice(start, end).trim(),
      });
    }
  }

  parameters.sort((left, right) =>
    left.componentIndex - right.componentIndex || left.parameterIndex - right.parameterIndex
  );

  if (!hasBody) {
    return { supported: false, reason: 'القالب لا يحتوي مكوّن BODY نصيًا.', parameters: [] };
  }

  for (const componentType of ['HEADER', 'BODY'] as const) {
    const positions = [...new Set(parameters
      .filter((parameter) => parameter.componentType === componentType)
      .map((parameter) => parameter.parameterIndex))]
      .sort((left, right) => left - right);
    if (positions.some((position, index) => position !== index + 1)) {
      return {
        supported: false,
        reason: `ترقيم متغيرات ${componentLabel(componentType)} يجب أن يبدأ من {{1}} بلا فجوات.`,
        parameters: [],
      };
    }
  }

  return {
    supported: true,
    parameters: parameters.filter((parameter, index, all) =>
      all.findIndex((candidate) => candidate.key === parameter.key) === index
    ),
  };
}

export function validateWhatsAppVariableMappings(
  requirements: WhatsAppTemplateParameterRequirement[],
  mappings: WhatsAppCampaignVariableMapping[],
  filters?: WhatsAppCampaignAudienceFilters,
) {
  return requirements.flatMap((requirement) => {
    const mapping = mappings.find((candidate) =>
      candidate.componentType === requirement.componentType &&
      candidate.position === requirement.parameterIndex
    );
    if (!mapping) return [`اختر قيمة للمتغير ${requirement.parameterIndex} في ${componentLabel(requirement.componentType)}.`];
    if (mapping.source === 'Literal' && !mapping.literalValue?.trim()) {
      return [`اكتب النص الثابت للمتغير ${requirement.parameterIndex} في ${componentLabel(requirement.componentType)}.`];
    }
    if (['TeacherName', 'SubjectName', 'PackageName', 'LessonName'].includes(mapping.source) && !mapping.referenceId) {
      return [`اختر المرجع المحدد للمتغير ${requirement.parameterIndex} في ${componentLabel(requirement.componentType)}.`];
    }
    if (mapping.source === 'PurchaseDate') {
      const packageId = filters?.packageIds.length === 1 ? filters.packageIds[0] : undefined;
      if (
        filters?.hasPaidPurchase !== true ||
        !filters.purchaseFromUtc ||
        !filters.purchaseToUtc ||
        !packageId ||
        mapping.referenceId !== packageId
      ) {
        return [`تاريخ الشراء يحتاج «اشترى ودفع»، باقة واحدة مطابقة، وفترة شراء كاملة.`];
      }
    }
    return [];
  });
}

export function validateWhatsAppAudienceFilters(filters: WhatsAppCampaignAudienceFilters) {
  const errors: string[] = [];
  const hasAcademicBase = hasWhatsAppAcademicAudienceBase(filters);
  if (filters.hasActiveAccess === false && !hasAcademicBase) {
    errors.push('«لا يملك وصولًا نشطًا» يحتاج مرحلة أو صفًا أو مسارًا أو محتوى محددًا.');
  }
  if (filters.hasPaidPurchase !== null && filters.hasPaidPurchase !== undefined) {
    if (!filters.purchaseFromUtc || !filters.purchaseToUtc) {
      errors.push('فلتر الشراء يحتاج فترة بداية ونهاية محددة.');
    }
    if (filters.hasPaidPurchase === false && !hasAcademicBase) {
      errors.push('«لم يشترِ ويدفع» يحتاج نطاقًا دراسيًا أو محتوى محددًا.');
    }
  } else if (filters.purchaseFromUtc || filters.purchaseToUtc) {
    errors.push('فترة الشراء تحتاج اختيار «اشترى ودفع» أو «لم يشترِ ويدفع».');
  }
  if (filters.hasWatched !== null && filters.hasWatched !== undefined) {
    if (filters.lessonIds.length === 0) {
      errors.push('فلتر المشاهدة يحتاج حصة محددة.');
    }
    if (!filters.watchFromUtc || !filters.watchToUtc) {
      errors.push('فلتر المشاهدة يحتاج فترة بداية ونهاية محددة.');
    }
    if (filters.hasWatched === false && !hasAcademicBase) errors.push('«لم يشاهدها» يحتاج نطاقًا دراسيًا أو محتوى محددًا.');
  } else if (filters.lessonIds.length > 0 || filters.watchFromUtc || filters.watchToUtc) {
    errors.push('نطاق المشاهدة يحتاج اختيار «شاهد الحصة» أو «لم يشاهدها».');
  }
  if (filters.hasExamAttempt !== null && filters.hasExamAttempt !== undefined) {
    if (filters.examIds.length === 0 || !filters.examFromUtc || !filters.examToUtc) {
      errors.push('فلتر الامتحان يحتاج امتحانًا محددًا وفترة بداية ونهاية.');
    }
    if (filters.hasExamAttempt === false && !hasAcademicBase) errors.push('«لم يمتحن» يحتاج نطاقًا دراسيًا أو محتوى محددًا.');
  } else if (filters.examIds.length > 0 || filters.examFromUtc || filters.examToUtc) {
    errors.push('نطاق الامتحان يحتاج اختيار «امتحن» أو «لم يمتحن».');
  }
  if (filters.hasHomeworkSubmission !== null && filters.hasHomeworkSubmission !== undefined) {
    if (filters.homeworkIds.length === 0 || !filters.homeworkFromUtc || !filters.homeworkToUtc) {
      errors.push('فلتر الواجب يحتاج واجبًا محددًا وفترة بداية ونهاية.');
    }
    if (filters.hasHomeworkSubmission === false && !hasAcademicBase) errors.push('«لم يسلّم الواجب» يحتاج نطاقًا دراسيًا أو محتوى محددًا.');
  } else if (filters.homeworkIds.length > 0 || filters.homeworkFromUtc || filters.homeworkToUtc) {
    errors.push('نطاق الواجب يحتاج اختيار «سلّم الواجب» أو «لم يسلّم الواجب».');
  }
  validateRange(filters.purchaseFromUtc, filters.purchaseToUtc, 'الشراء', errors);
  validateRange(filters.watchFromUtc, filters.watchToUtc, 'المشاهدة', errors);
  validateRange(filters.examFromUtc, filters.examToUtc, 'الامتحان', errors);
  validateRange(filters.homeworkFromUtc, filters.homeworkToUtc, 'الواجب', errors);
  return errors;
}

export function hasWhatsAppAcademicAudienceBase(filters: WhatsAppCampaignAudienceFilters) {
  return [
    filters.educationStages,
    filters.gradeLevels,
    filters.studyTracks,
    filters.teacherIds,
    filters.subjectIds,
    filters.packageIds,
    filters.lessonIds,
    filters.examIds,
    filters.homeworkIds,
  ].some((values) => values.length > 0);
}

function validateRange(from: string | null | undefined, to: string | null | undefined, label: string, errors: string[]) {
  if (!from || !to) return;
  const fromTime = new Date(from).getTime();
  const toTime = new Date(to).getTime();
  if (!Number.isFinite(fromTime) || !Number.isFinite(toTime) || fromTime >= toTime) {
    errors.push(`بداية فترة ${label} يجب أن تسبق نهايتها.`);
  } else if (toTime - fromTime > 366 * 24 * 60 * 60 * 1000) {
    errors.push(`فترة ${label} لا يمكن أن تتجاوز سنة ويومًا.`);
  }
}

export function isWhatsAppCampaignPreviewCurrent(
  preview: WhatsAppCampaignAudiencePreview,
  previewTemplateId: string,
  template: LiveSupportWhatsAppTemplate | undefined,
) {
  if (!template || previewTemplateId !== template.id) return false;
  if (preview.templateFingerprint !== template.fingerprint) return false;
  const expiresAt = new Date(preview.expiresAt).getTime();
  return Number.isFinite(expiresAt) && expiresAt > Date.now();
}

export function maskWhatsAppDestination(value: string) {
  const digits = value.replace(/\D/g, '');
  if (!digits) return 'رقم محجوب';
  const visibleTail = digits.slice(-2);
  return `•••• •••• ${visibleTail}`;
}

export function whatsappCampaignExcludedTotal(
  excludedByReason: WhatsAppCampaignAudiencePreview['excludedByReason'],
) {
  return Object.values(excludedByReason).reduce((total, value) => total + Math.max(0, value || 0), 0);
}

export function componentLabel(componentType: 'HEADER' | 'BODY') {
  return componentType === 'HEADER' ? 'رأس الرسالة' : 'نص الرسالة';
}
