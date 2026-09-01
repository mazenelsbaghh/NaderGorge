export const DEFAULT_MESSENGER_API_VERSION = 'v26.0';
export const MAX_MESSENGER_PAGES = 3;

export interface MessengerSettingsDraft {
  appId: string;
  appSecret: string;
  apiVersion: string;
}

export interface MessengerPageLinkDraft {
  accessToken: string;
  humanAgentEnabled: boolean;
}

export type MessengerPageConnectionState =
  | 'connected'
  | 'attention'
  | 'notChecked';

export type MessengerSetupState = 'ready' | 'attention' | 'incomplete';

export type MessengerPageOperationState =
  | 'idle'
  | 'linkPending'
  | 'unlinkPending';

const LINK_OPERATION_STATUSES = new Set([
  'Linking',
  'LinkUncertain',
  'LinkSettling',
]);

const UNLINK_OPERATION_STATUSES = new Set([
  'Unlinking',
  'UnlinkUncertain',
  'UnlinkSettling',
  'RemoteUnsubscribeConfirmed',
]);

const REPLACEABLE_OPERATION_STATUSES = new Set([
  'LinkUncertain',
  'UnlinkUncertain',
]);

export function messengerPageOperationState(
  connectionStatus?: string | null
): MessengerPageOperationState {
  if (connectionStatus && LINK_OPERATION_STATUSES.has(connectionStatus))
    return 'linkPending';
  if (connectionStatus && UNLINK_OPERATION_STATUSES.has(connectionStatus))
    return 'unlinkPending';
  return 'idle';
}

export function canReplaceMessengerPageToken(connectionStatus?: string | null) {
  return (
    messengerPageOperationState(connectionStatus) === 'idle' ||
    Boolean(
      connectionStatus && REPLACEABLE_OPERATION_STATUSES.has(connectionStatus)
    )
  );
}

export function validateMessengerSettings(
  draft: MessengerSettingsDraft,
  appSecretConfigured: boolean,
  supportedApiVersions: readonly string[],
  currentAppId = ''
) {
  const appId = draft.appId.trim();
  const appSecret = draft.appSecret.trim();
  if (!/^\d{5,32}$/.test(appId)) {
    return 'App ID يجب أن يتكوّن من أرقام فقط.';
  }
  if (!appSecretConfigured && !appSecret) {
    return 'أدخل App Secret لإكمال إعداد تطبيق Meta.';
  }
  if (currentAppId && currentAppId !== appId && !appSecret) {
    return 'أدخل App Secret الخاص بالتطبيق الجديد عند تغيير App ID.';
  }
  if (appSecret && appSecret.length < 16) {
    return 'App Secret أقصر من القيمة المتوقعة.';
  }
  if (!supportedApiVersions.includes(draft.apiVersion)) {
    return 'اختر إصدار Graph API متاحًا من القائمة.';
  }
  return null;
}

export function buildMessengerSettingsPayload(
  draft: MessengerSettingsDraft,
  expectedRevision: string
) {
  const appSecret = draft.appSecret.trim();
  return {
    appId: draft.appId.trim(),
    apiVersion: draft.apiVersion,
    expectedRevision,
    ...(appSecret ? { appSecret } : {}),
  };
}

export function validateMessengerPageLink(draft: MessengerPageLinkDraft) {
  const accessToken = draft.accessToken.trim();
  if (!accessToken) return 'ألصق Page Access Token أولًا.';
  if (/\s/.test(accessToken))
    return 'Page Access Token لا يجب أن يحتوي على مسافات.';
  if (accessToken.length < 20)
    return 'Page Access Token أقصر من القيمة المتوقعة.';
  return null;
}

export function pageConnectionState(page: {
  tokenValid: boolean | null;
  subscribed: boolean | null;
}): MessengerPageConnectionState {
  if (page.tokenValid === false || page.subscribed === false)
    return 'attention';
  if (page.tokenValid === true && page.subscribed === true) return 'connected';
  return 'notChecked';
}

export function messengerSetupState(settings: {
  appId: string;
  appSecretConfigured: boolean;
  verifyTokenConfigured: boolean;
  pages: ReadonlyArray<{
    tokenValid: boolean | null;
    subscribed: boolean | null;
  }>;
}): MessengerSetupState {
  if (
    !settings.appId ||
    !settings.appSecretConfigured ||
    !settings.verifyTokenConfigured ||
    settings.pages.length === 0
  ) {
    return 'incomplete';
  }
  return settings.pages.some(
    (page) => pageConnectionState(page) !== 'connected'
  )
    ? 'attention'
    : 'ready';
}

const MESSENGER_ERROR_MESSAGES: Readonly<Record<string, string>> = {
  MESSENGER_APP_ID_INVALID: 'App ID غير صالح.',
  MESSENGER_APP_SECRET_INVALID: 'App Secret غير صالح.',
  MESSENGER_APP_SECRET_REQUIRED_FOR_APP_CHANGE:
    'أدخل App Secret الخاص بالتطبيق الجديد عند تغيير App ID.',
  MESSENGER_API_VERSION_INVALID: 'إصدار Graph API غير مدعوم.',
  MESSENGER_CONFIGURATION_CONFLICT:
    'تغيّرت الإعدادات في جلسة أخرى. أعد التحميل ثم حاول مجددًا.',
  MESSENGER_PAGE_CONCURRENCY_CONFLICT:
    'تغيّرت الصفحات في جلسة أخرى. أعد التحميل ثم حاول مجددًا.',
  MESSENGER_APPLICATION_CONFIGURATION_INCOMPLETE:
    'أكمل App ID وApp Secret وVerify Token أولًا.',
  MESSENGER_APP_CHANGED_RELINK_REQUIRED:
    'تم تغيير App ID. حدّث توكن الصفحة لإعادة ربطها بالتطبيق الجديد.',
  MESSENGER_UNLINK_PAGES_BEFORE_APP_CHANGE:
    'ألغِ ربط كل صفحات Messenger أولًا قبل تغيير App ID.',
  MESSENGER_SETTINGS_REQUIRED: 'احفظ إعدادات تطبيق Meta أولًا.',
  MESSENGER_PAGE_LIMIT_EXCEEDED: 'تم الوصول إلى الحد الأقصى: ثلاث صفحات.',
  MESSENGER_PAGE_ACCESS_TOKEN_INVALID: 'Page Access Token غير صالح.',
  MESSENGER_PAGE_TOKEN_INVALID: 'Page Access Token غير صالح أو منتهي.',
  MESSENGER_GRAPH_190: 'Page Access Token غير صالح أو منتهي.',
  MESSENGER_PAGE_TOKEN_MISMATCH: 'التوكن لا يخص الصفحة المحددة.',
  MESSENGER_PAGE_TOKEN_APP_MISMATCH:
    'التوكن صادر لتطبيق Meta مختلف عن App ID المحفوظ.',
  MESSENGER_PAGE_TOKEN_TYPE_INVALID: 'التوكن ليس Page Access Token صالحًا.',
  MESSENGER_PAGE_TOKEN_PERMISSIONS_MISSING:
    'التوكن لا يحتوي على صلاحيات Messenger وإدارة الصفحة المطلوبة.',
  MESSENGER_PAGE_TOKEN_INSPECTION_INVALID:
    'تعذر التحقق من بيانات التوكن التي أعادها Meta.',
  MESSENGER_PAGE_NOT_FOUND: 'الصفحة غير موجودة أو لم تعد متاحة.',
  MESSENGER_PAGE_OPERATION_IN_PROGRESS:
    'هناك عملية جارية على الصفحة. انتظر قليلًا ثم أعد المحاولة.',
  MESSENGER_UNLINK_RETRY_PENDING:
    'تم حفظ التوكن الجديد وسيستكمل النظام إلغاء الربط بأمان.',
  MESSENGER_PAGE_NOT_SUBSCRIBED: 'الصفحة غير مشتركة في Webhook.',
  MESSENGER_SUBSCRIPTION_FIELDS_MISSING:
    'اشتراك الصفحة لا يحتوي على كل أحداث Messenger المطلوبة.',
  MESSENGER_SUBSCRIPTION_UNCERTAIN:
    'حالة الاشتراك غير مؤكدة الآن. استخدم فحص الربط بعد قليل.',
  MESSENGER_UNSUBSCRIBE_NOT_CONFIRMED:
    'لم يؤكد Meta إلغاء الاشتراك؛ لم تُحذف الصفحة حفاظًا على الاتساق.',
  MESSENGER_UNSUBSCRIBE_UNCERTAIN:
    'حالة إلغاء الاشتراك غير مؤكدة الآن؛ احتفظ النظام بالصفحة. افحص الربط ثم حاول مجددًا.',
  MESSENGER_META_UNAVAILABLE:
    'تعذر الوصول إلى Meta الآن. حاول مرة أخرى بعد قليل.',
  MESSENGER_GRAPH_UNAVAILABLE:
    'تعذر الوصول إلى Meta الآن. حاول مرة أخرى بعد قليل.',
  MESSENGER_GRAPH_REQUEST_FAILED:
    'رفض Meta الطلب. راجع صلاحيات التطبيق والتوكن.',
  MESSENGER_SECRET_DECRYPTION_FAILED:
    'تعذر قراءة السر المحفوظ. استبدله بقيمة جديدة.',
};

export function messengerErrorMessage(errorCode?: string | null) {
  if (!errorCode) return null;
  return MESSENGER_ERROR_MESSAGES[errorCode] ?? 'يحتاج الربط إلى مراجعة.';
}
