export interface LocalizedApiError {
  field?: string;
  message: string;
}

const VALIDATION_FALLBACK =
  'بعض البيانات غير صحيحة. راجع الحقول المحددة ثم حاول مرة أخرى.';
const GENERIC_FALLBACK =
  'حدثت مشكلة أثناء تنفيذ الطلب. حاول مرة أخرى، وإذا استمرت المشكلة تواصل مع الدعم.';
const NETWORK_FALLBACK =
  'تعذر إكمال الطلب الآن. تحقق من اتصالك بالإنترنت ثم حاول مرة أخرى.';

const REGISTRATION_ERROR_RULES: ReadonlyArray<{
  matches: (message: string) => boolean;
  field?: string;
  message: string;
}> = [
  {
    matches: (message) =>
      message.includes('phone number already registered') ||
      message.includes('رقم الهاتف الأساسي مسجل بالفعل') ||
      message.includes('مسجل بالفعل'),
    field: 'phoneNumber',
    message: 'رقم الهاتف مسجل بالفعل. سجّل الدخول أو استخدم رقمًا آخر.',
  },
  {
    matches: (message) => message.includes("father's date of birth"),
    field: 'fatherDateOfBirth',
    message: 'تاريخ ميلاد الأب يجب أن يكون تاريخًا سابقًا لليوم.',
  },
  {
    matches: (message) => message.includes("mother's date of birth"),
    field: 'motherDateOfBirth',
    message: 'تاريخ ميلاد الأم يجب أن يكون تاريخًا سابقًا لليوم.',
  },
  {
    matches: (message) => message.includes('date of birth must be in the past'),
    field: 'dateOfBirth',
    message: 'تاريخ الميلاد يجب أن يكون تاريخًا سابقًا لليوم.',
  },
  {
    matches: (message) =>
      message.includes('full name must contain at least 4 parts') ||
      message.includes("'full name'"),
    field: 'fullName',
    message: 'اكتب الاسم رباعيًا، مثال: أحمد محمد محمود علي.',
  },
  {
    matches: (message) =>
      message.includes('invalid egyptian mother phone number'),
    field: 'motherPhone',
    message: 'اكتب رقم هاتف الأم صحيحًا، مثال: 01012345678.',
  },
  {
    matches: (message) =>
      message.includes('invalid egyptian parent phone number'),
    field: 'secondaryParentPhone',
    message: 'اكتب رقم ولي الأمر الإضافي صحيحًا، مثال: 01012345678.',
  },
  {
    matches: (message) =>
      message.includes('valid father phone number') ||
      message.includes('father phone'),
    field: 'parentPhone',
    message: 'اكتب رقم هاتف الأب صحيحًا، مثال: 01012345678.',
  },
  {
    matches: (message) => message.includes('invalid egyptian phone number'),
    field: 'phoneNumber',
    message: 'اكتب رقم هاتف مصري صحيحًا، مثال: 01012345678.',
  },
  {
    matches: (message) =>
      message.includes("'password'") ||
      message.includes('password must') ||
      message.includes('password is required'),
    field: 'password',
    message: 'كلمة المرور يجب أن تتكون من 8 أحرف على الأقل.',
  },
  {
    matches: (message) =>
      message.includes('study track is required') ||
      message.includes('a study track is required'),
    field: 'studyTrack',
    message: 'اختر الشعبة أو التخصص المناسب للصف الدراسي.',
  },
  {
    matches: (message) =>
      message.includes('study track must not be specified') ||
      message.includes('track must not be specified'),
    field: 'studyTrack',
    message: 'هذا الصف لا يحتاج إلى اختيار شعبة أو تخصص.',
  },
  {
    matches: (message) =>
      message.includes('track') && message.includes('not valid for grade'),
    field: 'studyTrack',
    message: 'الشعبة المختارة لا تتناسب مع الصف الدراسي.',
  },
  {
    matches: (message) =>
      message.includes('grade') &&
      message.includes('not valid for education stage'),
    field: 'gradeLevel',
    message: 'الصف الدراسي لا يتناسب مع المرحلة المختارة.',
  },
  {
    matches: (message) =>
      message.includes("'governorate'") ||
      message.includes('governorate must'),
    field: 'governorate',
    message: 'اختر المحافظة.',
  },
  {
    matches: (message) =>
      message.includes("'address'") || message.includes('address must'),
    field: 'address',
    message: 'اكتب العنوان بالتفصيل.',
  },
  {
    matches: (message) => message.includes("'education stage'"),
    field: 'educationStage',
    message: 'اختر المرحلة الدراسية.',
  },
  {
    matches: (message) => message.includes("'grade level'"),
    field: 'gradeLevel',
    message: 'اختر الصف الدراسي.',
  },
  {
    matches: (message) => message.includes("'school type'"),
    field: 'schoolType',
    message: 'اختر نوع المدرسة.',
  },
];

function responseDataFrom(error: unknown): Record<string, unknown> | undefined {
  if (typeof error !== 'object' || error === null || !('response' in error)) {
    return undefined;
  }

  const response = (error as { response?: { data?: unknown } }).response;
  return typeof response?.data === 'object' && response.data !== null
    ? (response.data as Record<string, unknown>)
    : undefined;
}

export function extractApiErrorMessages(error: unknown): string[] {
  const responseData = responseDataFrom(error);
  if (!responseData) return [];

  const details = Array.isArray(responseData.errors)
    ? responseData.errors.filter(
        (value): value is string =>
          typeof value === 'string' && value.trim().length > 0,
      )
    : [];
  const message =
    typeof responseData.message === 'string' &&
    responseData.message.trim().length > 0
      ? responseData.message
      : undefined;

  return [...details, ...(message ? [message] : [])];
}

export function localizeApiErrorMessage(
  message: string,
): LocalizedApiError {
  const normalizedMessage = message.trim().toLowerCase();
  const registrationRule = REGISTRATION_ERROR_RULES.find((rule) =>
    rule.matches(normalizedMessage),
  );

  if (registrationRule) {
    return {
      field: registrationRule.field,
      message: registrationRule.message,
    };
  }

  if (
    normalizedMessage === 'validation failed' ||
    normalizedMessage.includes('validation error')
  ) {
    return { message: VALIDATION_FALLBACK };
  }

  if (
    normalizedMessage.includes('network error') ||
    normalizedMessage.includes('timeout') ||
    normalizedMessage.includes('failed to fetch')
  ) {
    return { message: NETWORK_FALLBACK };
  }

  const containsArabic = /[\u0600-\u06ff]/.test(message);
  return { message: containsArabic ? message : GENERIC_FALLBACK };
}

export function getRegistrationApiErrors(
  error: unknown,
): LocalizedApiError[] {
  const messages = extractApiErrorMessages(error);
  if (messages.length === 0) {
    return [{ message: NETWORK_FALLBACK }];
  }

  const localized = messages
    .filter((message) => message.trim().toLowerCase() !== 'validation failed')
    .map(localizeApiErrorMessage);
  const usefulErrors =
    localized.length > 0
      ? localized
      : messages.map(localizeApiErrorMessage);

  return usefulErrors.filter(
    (errorItem, index, allErrors) =>
      allErrors.findIndex(
        (candidate) =>
          candidate.field === errorItem.field &&
          candidate.message === errorItem.message,
      ) === index,
  );
}

export function getApiErrorSummary(
  error: unknown,
  fallback = NETWORK_FALLBACK,
): string {
  const messages = extractApiErrorMessages(error);
  const detailedMessage = messages.find(
    (message) => message.trim().toLowerCase() !== 'validation failed',
  );

  if (detailedMessage) {
    return localizeApiErrorMessage(detailedMessage).message;
  }

  const topLevelMessage = messages[0];
  return topLevelMessage
    ? localizeApiErrorMessage(topLevelMessage).message
    : fallback;
}
