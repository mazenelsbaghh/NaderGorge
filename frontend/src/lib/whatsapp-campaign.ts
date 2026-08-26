import type {
  LiveSupportWhatsAppTemplate,
  WhatsAppCampaignAudienceFilters,
  WhatsAppCampaignAudiencePreview,
  WhatsAppCampaignTemplateComponentType,
  WhatsAppCampaignVariableMapping,
  WhatsAppCampaignVariableSource,
} from '@/services/live-support-service';

export interface WhatsAppTemplateParameterRequirement {
  key: string;
  componentType: WhatsAppCampaignTemplateComponentType;
  componentIndex: number;
  parameterIndex: number;
  buttonIndex?: number;
  parameterType: 'TEXT' | 'URL_SUFFIX';
  surroundingText: string;
}

interface WhatsAppTemplateInspectionFailure {
  supported: false;
  reason: string;
  parameters: [];
}

interface WhatsAppTemplateInspectionSuccess {
  supported: true;
  parameters: WhatsAppTemplateParameterRequirement[];
}

export type WhatsAppCampaignTemplateSupport =
  | WhatsAppTemplateInspectionFailure
  | WhatsAppTemplateInspectionSuccess;

type WhatsAppComponentInspection = WhatsAppTemplateInspectionFailure | (
  WhatsAppTemplateInspectionSuccess & { componentType: string }
);

type WhatsAppComponentsInspection = WhatsAppTemplateInspectionFailure | (
  WhatsAppTemplateInspectionSuccess & { hasBody: boolean }
);

const PLACEHOLDER_PATTERN = /\{\{\s*(\d+)\s*\}\}/g;
const ANY_PLACEHOLDER_PATTERN = /\{\{[^{}]*\}\}/g;
const DYNAMIC_URL_SUFFIX_PATTERN = /\{\{\s*1\s*\}\}$/;
const STUDENT_VARIABLE_SOURCES = new Set([
  'StudentFirstName', 'StudentFullName', 'ParentTrackingCode', 'EducationStage', 'GradeLevel',
  'StudyTrack', 'Governorate', 'SchoolName',
]);
const REFERENCE_VARIABLE_SOURCES = new Set(['TeacherName', 'SubjectName', 'PackageName', 'LessonName']);

export const WHATSAPP_CAMPAIGN_VARIABLE_SOURCES: ReadonlyArray<{
  value: WhatsAppCampaignVariableSource;
  label: string;
  referenceFacet?: 'teachers' | 'subjects' | 'packages' | 'lessons';
}> = [
  { value: 'StudentFirstName', label: 'اسم الطالب الأول' },
  { value: 'StudentFullName', label: 'اسم الطالب كاملًا' },
  { value: 'ParentTrackingCode', label: 'رقم متابعة الطالب' },
  { value: 'EducationStage', label: 'المرحلة التعليمية' },
  { value: 'GradeLevel', label: 'الصف الدراسي' },
  { value: 'StudyTrack', label: 'المسار الدراسي' },
  { value: 'Governorate', label: 'المحافظة' },
  { value: 'SchoolName', label: 'المدرسة' },
  { value: 'TeacherName', label: 'اسم المدرس', referenceFacet: 'teachers' },
  { value: 'SubjectName', label: 'اسم المادة', referenceFacet: 'subjects' },
  { value: 'PackageName', label: 'اسم الباقة', referenceFacet: 'packages' },
  { value: 'LessonName', label: 'اسم الحصة', referenceFacet: 'lessons' },
  { value: 'PurchaseDate', label: 'تاريخ الشراء' },
  { value: 'Literal', label: 'نص ثابت' },
];

export function whatsAppCampaignVariableSourceLabel(source: string) {
  return WHATSAPP_CAMPAIGN_VARIABLE_SOURCES.find((candidate) => candidate.value === source)?.label ?? 'قيمة الطالب';
}

export function availableWhatsAppCampaignVariableSources(
  templateCategory: string,
  parameterType: WhatsAppTemplateParameterRequirement['parameterType'],
) {
  if (parameterType === 'URL_SUFFIX') {
    return WHATSAPP_CAMPAIGN_VARIABLE_SOURCES.filter((source) => source.value === 'Literal');
  }
  return WHATSAPP_CAMPAIGN_VARIABLE_SOURCES.filter((source) =>
    source.value !== 'ParentTrackingCode' || templateCategory.toUpperCase() === 'UTILITY');
}

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
  const capability = inspectWhatsAppTemplateCapabilities(template);
  if (!capability.supported) return capability;
  if (!['MARKETING', 'UTILITY'].includes(template.category.toUpperCase())) {
    return unsupportedTemplate('الحملات تدعم قوالب MARKETING وUTILITY المعتمدة فقط.');
  }
  return capability;
}

export function inspectWhatsAppTemplateCapabilities(
  template: LiveSupportWhatsAppTemplate,
): WhatsAppCampaignTemplateSupport {
  const authorityFailure = synchronizedApprovedTemplateFailure(template);
  if (authorityFailure) return unsupportedTemplate(authorityFailure);
  const inspection = inspectTemplateComponents(template.components);
  if (!inspection.supported) return inspection;
  if (!inspection.hasBody) return unsupportedTemplate('القالب لا يحتوي مكوّن BODY نصيًا.');
  inspection.parameters.sort((left, right) =>
    left.componentIndex - right.componentIndex || left.parameterIndex - right.parameterIndex
  );
  return { supported: true, parameters: inspection.parameters };
}

function synchronizedApprovedTemplateFailure(template: LiveSupportWhatsAppTemplate) {
  if (!/^[0-9a-f]{64}$/i.test(template.fingerprint)) {
    return 'يجب مزامنة القالب بنجاح قبل استخدامه في حملة.';
  }
  if (template.status.toUpperCase() !== 'APPROVED') return 'القالب غير معتمد من واتساب.';
  return undefined;
}

function inspectTemplateComponents(
  components: LiveSupportWhatsAppTemplate['components'],
): WhatsAppComponentsInspection {
  const seenComponentTypes = new Set<string>();
  const parameters: WhatsAppTemplateParameterRequirement[] = [];
  let hasBody = false;
  for (const [componentIndex, component] of components.entries()) {
    const inspection = inspectTemplateComponent(component, componentIndex);
    if (!inspection.supported) return inspection;
    if (seenComponentTypes.has(inspection.componentType)) {
      return unsupportedTemplate(`القالب يحتوي أكثر من ${componentLabel(inspection.componentType)}؛ هذا التركيب غير مدعوم بأمان.`);
    }
    seenComponentTypes.add(inspection.componentType);
    hasBody ||= inspection.componentType === 'BODY';
    parameters.push(...inspection.parameters);
  }
  return { supported: true, parameters, hasBody };
}

function inspectTemplateComponent(
  component: LiveSupportWhatsAppTemplate['components'][number],
  componentIndex: number,
): WhatsAppComponentInspection {
  const componentType = (component.type ?? '').toUpperCase();
  if (!['HEADER', 'BODY', 'FOOTER', 'BUTTONS'].includes(componentType)) {
    return unsupportedTemplate('القالب يحتوي مكوّنًا لا يمكن إرساله بأمان من مركز الحملات.');
  }
  if (componentType === 'HEADER' && (component.format ?? 'TEXT').toUpperCase() !== 'TEXT') {
    return unsupportedTemplate('رأس القالب يحتوي وسائط ولا يوجد له مصدر وسائط معتمد داخل الحملات.');
  }
  const inspection = componentType === 'BUTTONS'
    ? inspectTemplateButtons(component.buttons, componentIndex)
    : inspectTextComponent(component.text ?? '', componentType, componentIndex);
  return inspection.supported ? { ...inspection, componentType } : inspection;
}

function inspectTextComponent(
  text: string,
  componentType: string,
  componentIndex: number,
): WhatsAppCampaignTemplateSupport {
  if (!text.trim()) return unsupportedTemplate(`${componentLabel(componentType)} بلا نص صالح.`);
  if (componentType === 'FOOTER' && (text.match(ANY_PLACEHOLDER_PATTERN)?.length ?? 0) > 0) {
    return unsupportedTemplate('تذييل القالب يجب أن يكون نصًا ثابتًا بلا متغيرات.');
  }
  return inspectTextParameters(text, componentType, componentIndex);
}

function inspectTextParameters(
  text: string,
  componentType: string,
  componentIndex: number,
): WhatsAppCampaignTemplateSupport {
  const allPlaceholders = text.match(ANY_PLACEHOLDER_PATTERN) ?? [];
  const matches = [...text.matchAll(PLACEHOLDER_PATTERN)];
  if (allPlaceholders.length !== matches.length) {
    return unsupportedTemplate('القالب يحتوي متغيرًا غير مرقّم. الحملات تدعم {{1}} و{{2}} فقط.');
  }

  const positions = [...new Set(matches.map((match) => Number(match[1])))].sort((left, right) => left - right);
  if (positions.some((position, index) => position !== index + 1)) {
    return unsupportedTemplate(`ترقيم متغيرات ${componentLabel(componentType)} يجب أن يبدأ من {{1}} بلا فجوات.`);
  }
  if (componentType !== 'HEADER' && componentType !== 'BODY') {
    return { supported: true, parameters: [] };
  }

  return {
    supported: true,
    parameters: positions.map((parameterIndex) => ({
      key: `${componentType}:${componentIndex}:${parameterIndex}`,
      componentType,
      componentIndex,
      parameterIndex,
      parameterType: 'TEXT',
      surroundingText: placeholderContext(text, parameterIndex),
    })),
  };
}

function inspectTemplateButtons(
  buttons: LiveSupportWhatsAppTemplate['components'][number]['buttons'],
  componentIndex: number,
): WhatsAppCampaignTemplateSupport {
  if (!buttons?.length) return unsupportedTemplate('مكوّن الأزرار لا يحتوي أزرارًا صالحة.');
  const parameters: WhatsAppTemplateParameterRequirement[] = [];
  for (const [buttonIndex, button] of buttons.entries()) {
    const inspection = inspectTemplateButton(button, componentIndex, buttonIndex);
    if (!inspection.supported) return inspection;
    parameters.push(...inspection.parameters);
  }
  return { supported: true, parameters };
}

function inspectTemplateButton(
  button: NonNullable<LiveSupportWhatsAppTemplate['components'][number]['buttons']>[number],
  componentIndex: number,
  buttonIndex: number,
): WhatsAppCampaignTemplateSupport {
  const buttonType = (button.type ?? '').toUpperCase();
  if (!button.text?.trim()) return unsupportedTemplate('أحد أزرار القالب بلا عنوان واضح.');
  if ((button.text.match(ANY_PLACEHOLDER_PATTERN)?.length ?? 0) > 0) {
    return unsupportedTemplate('نص زر القالب يجب أن يكون ثابتًا.');
  }
  if (buttonType === 'URL') return inspectUrlButton(button.url, componentIndex, buttonIndex);
  if (buttonType === 'PHONE_NUMBER') return inspectPhoneButton(button.phone_number);
  return unsupportedTemplate(`نوع الزر «${buttonType || 'غير معروف'}» يحتاج عقد إرسال خاصًا وغير متاح في الحملات حاليًا.`);
}

function inspectPhoneButton(phoneNumber: string | undefined): WhatsAppCampaignTemplateSupport {
  const normalizedPhoneNumber = phoneNumber?.trim() ?? '';
  if (!normalizedPhoneNumber || (normalizedPhoneNumber.match(ANY_PLACEHOLDER_PATTERN)?.length ?? 0) > 0) {
    return unsupportedTemplate('رقم زر الاتصال يجب أن يكون ثابتًا.');
  }
  return { supported: true, parameters: [] };
}

function inspectUrlButton(
  rawUrl: string | undefined,
  componentIndex: number,
  buttonIndex: number,
): WhatsAppCampaignTemplateSupport {
  const url = rawUrl?.trim() ?? '';
  if (!isSafeTemplateUrl(url)) return unsupportedTemplate('رابط أحد أزرار القالب غير صالح أو غير آمن.');
  const placeholders = url.match(ANY_PLACEHOLDER_PATTERN) ?? [];
  if (placeholders.length === 0) return { supported: true, parameters: [] };
  if (placeholders.length !== 1 || !DYNAMIC_URL_SUFFIX_PATTERN.test(url)) {
    return unsupportedTemplate('زر الرابط الديناميكي يجب أن ينتهي بمتغير واحد {{1}} فقط.');
  }
  return { supported: true, parameters: [dynamicUrlRequirement(componentIndex, buttonIndex, url)] };
}

function dynamicUrlRequirement(
  componentIndex: number,
  buttonIndex: number,
  url: string,
): WhatsAppTemplateParameterRequirement {
  return {
    key: `BUTTON:${componentIndex}:${buttonIndex}:1`,
    componentType: 'BUTTON',
    componentIndex,
    buttonIndex,
    parameterIndex: 1,
    parameterType: 'URL_SUFFIX',
    surroundingText: url,
  };
}

function isSafeTemplateUrl(url: string) {
  try {
    const parsed = new URL(url.replace(DYNAMIC_URL_SUFFIX_PATTERN, 'preview'));
    return parsed.protocol === 'https:' && Boolean(parsed.hostname) && !parsed.username && !parsed.password;
  } catch {
    return false;
  }
}

function placeholderContext(text: string, parameterIndex: number) {
  const pattern = new RegExp(`\\{\\{\\s*${parameterIndex}\\s*\\}\\}`);
  const match = pattern.exec(text);
  if (!match) return text.slice(0, 56).trim();
  const start = Math.max(0, match.index - 28);
  const end = Math.min(text.length, match.index + match[0].length + 28);
  return text.slice(start, end).trim();
}

function unsupportedTemplate(reason: string): WhatsAppTemplateInspectionFailure {
  return { supported: false, reason, parameters: [] };
}

export function validateWhatsAppVariableMappings(
  requirements: WhatsAppTemplateParameterRequirement[],
  mappings: WhatsAppCampaignVariableMapping[],
  filters?: WhatsAppCampaignAudienceFilters,
  templateCategory?: string,
) {
  const errors = requirements.flatMap((requirement) =>
    requirementMappingErrors(requirement, mappings, filters, templateCategory));
  if (mappings.some((mapping) => !requirements.some((requirement) => mappingMatchesRequirement(mapping, requirement)))) {
    errors.push('يوجد ربط متغير لا يطابق مكونات القالب الحالي. أعد اختيار القالب.');
  }
  return errors;
}

function requirementMappingErrors(
  requirement: WhatsAppTemplateParameterRequirement,
  mappings: WhatsAppCampaignVariableMapping[],
  filters?: WhatsAppCampaignAudienceFilters,
  templateCategory?: string,
) {
  const matches = mappings.filter((mapping) => mappingMatchesRequirement(mapping, requirement));
  if (matches.length === 0) {
    return [`اختر قيمة للمتغير ${requirement.parameterIndex} في ${requirementLabel(requirement)}.`];
  }
  if (matches.length > 1) {
    return [`يوجد أكثر من ربط للمتغير ${requirement.parameterIndex} في ${requirementLabel(requirement)}.`];
  }
  const error = mappingValueError(requirement, matches[0], filters, templateCategory);
  return error ? [error] : [];
}

function mappingValueError(
  requirement: WhatsAppTemplateParameterRequirement,
  mapping: WhatsAppCampaignVariableMapping,
  filters?: WhatsAppCampaignAudienceFilters,
  templateCategory?: string,
) {
  const label = requirementLabel(requirement);
  if (requirement.parameterType === 'URL_SUFFIX' && mapping.source !== 'Literal') {
    return `لاحقة الرابط في ${label} يجب أن تكون نصًا ثابتًا.`;
  }
  if (mapping.source === 'Literal' && !mapping.literalValue?.trim()) {
    return `اكتب النص الثابت للمتغير ${requirement.parameterIndex} في ${label}.`;
  }
  if (mapping.source === 'Literal' && !isSafeLiteral(mapping.literalValue ?? '')) {
    return `النص الثابت في ${label} طويل أو يحتوي رابطًا أو محارف غير مسموحة.`;
  }
  if (mapping.source === 'Literal' && mapping.referenceId) {
    return `النص الثابت في ${label} لا يقبل مرجع محتوى.`;
  }
  if (mapping.source === 'ParentTrackingCode' && templateCategory?.toUpperCase() !== 'UTILITY') {
    return 'رقم متابعة الطالب متاح في قوالب UTILITY فقط.';
  }
  if (STUDENT_VARIABLE_SOURCES.has(mapping.source) && (mapping.referenceId || mapping.literalValue != null)) {
    return `مصدر بيانات الطالب في ${label} لا يقبل قيمة أو مرجعًا إضافيًا.`;
  }
  if (REFERENCE_VARIABLE_SOURCES.has(mapping.source) && (!mapping.referenceId || mapping.literalValue != null)) {
    return `اختر المرجع المحدد للمتغير ${requirement.parameterIndex} في ${label}.`;
  }
  return mapping.source === 'PurchaseDate' ? purchaseDateMappingError(mapping, filters) : undefined;
}

function purchaseDateMappingError(
  mapping: WhatsAppCampaignVariableMapping,
  filters?: WhatsAppCampaignAudienceFilters,
) {
  const packageId = filters?.packageIds.length === 1 ? filters.packageIds[0] : undefined;
  const valid = filters?.hasPaidPurchase === true &&
    Boolean(filters.purchaseFromUtc && filters.purchaseToUtc) &&
    Boolean(packageId && mapping.referenceId === packageId) &&
    mapping.literalValue == null;
  return valid ? undefined : 'تاريخ الشراء يحتاج «اشترى ودفع»، باقة واحدة مطابقة، وفترة شراء كاملة.';
}

function isSafeLiteral(literalValue: string) {
  const normalized = literalValue.normalize('NFC').trim();
  return normalized.length <= 1_024 &&
    !/\p{Cc}/u.test(normalized) &&
    !/https?:\/\//i.test(normalized);
}

export function mappingMatchesRequirement(
  mapping: WhatsAppCampaignVariableMapping,
  requirement: WhatsAppTemplateParameterRequirement,
) {
  return mapping.componentType === requirement.componentType &&
    mapping.componentIndex === requirement.componentIndex &&
    mapping.position === requirement.parameterIndex &&
    (requirement.componentType === 'BUTTON'
      ? mapping.buttonIndex === requirement.buttonIndex
      : mapping.buttonIndex == null);
}

export function requirementLabel(requirement: WhatsAppTemplateParameterRequirement) {
  if (requirement.componentType !== 'BUTTON') return componentLabel(requirement.componentType);
  return `الزر ${Number(requirement.buttonIndex) + 1}`;
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

export function componentLabel(componentType: string) {
  if (componentType === 'HEADER') return 'رأس الرسالة';
  if (componentType === 'BODY') return 'نص الرسالة';
  if (componentType === 'FOOTER') return 'تذييل الرسالة';
  if (componentType === 'BUTTON' || componentType === 'BUTTONS') return 'مكوّن الأزرار';
  return 'مكوّن القالب';
}
