'use client';

import { useCallback, useEffect, useId, useMemo, useState } from 'react';
import {
  AlertTriangle,
  Check,
  CheckCircle2,
  Clipboard,
  KeyRound,
  Link2,
  Loader2,
  MessageCircleMore,
  Plus,
  RefreshCw,
  Save,
  ShieldCheck,
  Trash2,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';

import { AdminConfirmationDialog } from '@/components/admin/AdminConfirmationDialog';
import { extractApiErrorMessages, getApiErrorSummary } from '@/lib/api-errors';
import {
  buildMessengerSettingsPayload,
  canReplaceMessengerPageToken,
  DEFAULT_MESSENGER_API_VERSION,
  MAX_MESSENGER_PAGES,
  messengerErrorMessage,
  messengerPageOperationState,
  messengerSetupState,
  pageConnectionState,
  validateMessengerPageLink,
  validateMessengerSettings,
  type MessengerPageLinkDraft,
  type MessengerSettingsDraft,
} from '@/lib/facebook-messenger-settings';
import { isRequestCancellation } from '@/services/api-client';
import {
  facebookMessengerAdminService,
  type FacebookMessengerAdminPage,
  type FacebookMessengerAdminSettings,
} from '@/services/facebook-messenger-admin-service';

const EMPTY_PAGE_FORM: MessengerPageLinkDraft = {
  accessToken: '',
  humanAgentEnabled: false,
};

const SETUP_COPY = {
  ready: {
    title: 'Messenger جاهز لاستقبال الرسائل',
    description: 'كل الصفحات المضافة اجتازت فحص التوكن والاشتراك.',
    className:
      'border-emerald-500/25 bg-emerald-500/10 text-emerald-800 dark:text-emerald-300',
    icon: CheckCircle2,
  },
  attention: {
    title: 'الربط يحتاج مراجعة',
    description: 'افحص الصفحات الظاهرة بحالة تحتاج تدخل قبل الاعتماد عليها.',
    className:
      'border-amber-500/25 bg-amber-500/10 text-amber-800 dark:text-amber-300',
    icon: AlertTriangle,
  },
  incomplete: {
    title: 'أكمل خطوات الربط الثلاث',
    description:
      'احفظ تطبيق Meta، أنشئ Verify Token، ثم اربط صفحة واحدة على الأقل.',
    className:
      'border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)]',
    icon: ShieldCheck,
  },
} as const;

function requestErrorText(error: unknown, fallback: string) {
  const responseCode = (() => {
    if (typeof error !== 'object' || error === null || !('response' in error))
      return null;
    const response = (error as { response?: { data?: unknown } }).response;
    const responseBody = response?.data;
    if (
      typeof responseBody !== 'object' ||
      responseBody === null ||
      !('code' in responseBody)
    )
      return null;
    return typeof responseBody.code === 'string' ? responseBody.code : null;
  })();
  const knownCode =
    responseCode ??
    extractApiErrorMessages(error).find((message) =>
      message.startsWith('MESSENGER_')
    );
  return (
    messengerErrorMessage(knownCode) ?? getApiErrorSummary(error, fallback)
  );
}

function formatAdminDate(timestamp?: string | null) {
  if (!timestamp) return 'لا يوجد بعد';
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) return 'غير متاح';
  return new Intl.DateTimeFormat('ar-EG', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Africa/Cairo',
  }).format(date);
}

async function copySecureValue(secretText: string, successMessage: string) {
  try {
    await navigator.clipboard.writeText(secretText);
    toast.success(successMessage);
  } catch {
    toast.error('تعذر النسخ تلقائيًا. حدّد القيمة وانسخها يدويًا.');
  }
}

function applySettingsDraft(
  settings: FacebookMessengerAdminSettings
): MessengerSettingsDraft {
  return {
    appId: settings.appId ?? '',
    appSecret: '',
    apiVersion: settings.apiVersion || DEFAULT_MESSENGER_API_VERSION,
  };
}

export function FacebookMessengerSettingsPanel() {
  const [settings, setSettings] =
    useState<FacebookMessengerAdminSettings | null>(null);
  const [draft, setDraft] = useState<MessengerSettingsDraft>({
    appId: '',
    appSecret: '',
    apiVersion: DEFAULT_MESSENGER_API_VERSION,
  });
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [settingsSaving, setSettingsSaving] = useState(false);
  const [settingsError, setSettingsError] = useState<string | null>(null);
  const [pageFormOpen, setPageFormOpen] = useState(false);
  const [editingPage, setEditingPage] =
    useState<FacebookMessengerAdminPage | null>(null);
  const [pageDraft, setPageDraft] =
    useState<MessengerPageLinkDraft>(EMPTY_PAGE_FORM);
  const [pageSaving, setPageSaving] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [busyPageId, setBusyPageId] = useState<string | null>(null);
  const [unlinkTarget, setUnlinkTarget] =
    useState<FacebookMessengerAdminPage | null>(null);
  const [rotateDialogOpen, setRotateDialogOpen] = useState(false);
  const [rotatingToken, setRotatingToken] = useState(false);
  const [oneTimeVerifyToken, setOneTimeVerifyToken] = useState<string | null>(
    null
  );
  const settingsErrorId = useId();
  const pageErrorId = useId();

  const applySettings = useCallback((next: FacebookMessengerAdminSettings) => {
    setSettings(next);
    setDraft(applySettingsDraft(next));
  }, []);

  const loadSettings = useCallback(
    async (signal?: AbortSignal, quiet = false) => {
      if (!quiet) setLoading(true);
      setLoadError(null);
      try {
        applySettings(await facebookMessengerAdminService.getSettings(signal));
      } catch (error) {
        if (isRequestCancellation(error)) return;
        setLoadError(requestErrorText(error, 'تعذر تحميل إعدادات Messenger.'));
      } finally {
        if (!quiet) setLoading(false);
      }
    },
    [applySettings]
  );

  useEffect(() => {
    const controller = new AbortController();
    void loadSettings(controller.signal);
    return () => controller.abort();
  }, [loadSettings]);

  const supportedApiVersions = useMemo(() => {
    const versions = settings?.supportedApiVersions?.filter(Boolean) ?? [];
    return versions.length > 0 ? versions : [DEFAULT_MESSENGER_API_VERSION];
  }, [settings?.supportedApiVersions]);

  if (loading) return <MessengerSettingsSkeleton />;

  if (!settings || loadError) {
    return (
      <section
        className="rounded-2xl border border-red-500/20 bg-red-500/10 px-6 py-12 text-center"
        dir="rtl"
        role="alert"
      >
        <AlertTriangle className="mx-auto h-7 w-7 text-red-700 dark:text-red-300" />
        <p className="mt-3 text-sm font-bold text-red-700 dark:text-red-300">
          {loadError ?? 'تعذر تحميل إعدادات Messenger.'}
        </p>
        <button
          type="button"
          onClick={() => void loadSettings()}
          className="mt-5 inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
        >
          <RefreshCw className="h-4 w-4" />
          إعادة المحاولة
        </button>
      </section>
    );
  }

  const setupState = messengerSetupState(settings);
  const setupCopy = SETUP_COPY[setupState];
  const SetupIcon = setupCopy.icon;
  const canLinkPages = Boolean(
    settings.appId &&
    settings.appSecretConfigured &&
    settings.verifyTokenConfigured
  );
  const pageLimitReached = settings.pages.length >= MAX_MESSENGER_PAGES;
  const editingOperation = messengerPageOperationState(
    editingPage?.connectionStatus
  );

  const saveSettings = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationError = validateMessengerSettings(
      draft,
      settings.appSecretConfigured,
      supportedApiVersions,
      settings.appId
    );
    if (validationError) {
      setSettingsError(validationError);
      return;
    }

    setSettingsSaving(true);
    setSettingsError(null);
    try {
      const updated = await facebookMessengerAdminService.updateSettings(
        buildMessengerSettingsPayload(draft, settings.revision)
      );
      applySettings(updated);
      toast.success('تم حفظ إعدادات تطبيق Meta.');
    } catch (error) {
      setSettingsError(requestErrorText(error, 'تعذر حفظ إعدادات تطبيق Meta.'));
    } finally {
      setSettingsSaving(false);
    }
  };

  const rotateVerifyToken = async () => {
    setRotatingToken(true);
    try {
      const rotation = await facebookMessengerAdminService.rotateVerifyToken(
        settings.revision
      );
      setOneTimeVerifyToken(rotation.verifyToken);
      setSettings((current) =>
        current
          ? {
              ...current,
              revision: rotation.revision,
              verifyTokenConfigured: true,
            }
          : current
      );
      setRotateDialogOpen(false);
      toast.success('تم إنشاء Verify Token جديد. انسخه الآن إلى Meta.');
    } catch (error) {
      toast.error(requestErrorText(error, 'تعذر إنشاء Verify Token جديد.'));
    } finally {
      setRotatingToken(false);
    }
  };

  const openPageForm = (page: FacebookMessengerAdminPage | null = null) => {
    setEditingPage(page);
    setPageDraft({
      accessToken: '',
      humanAgentEnabled: page?.humanAgentEnabled ?? false,
    });
    setPageError(null);
    setPageFormOpen(true);
  };

  const resetPageForm = () => {
    setPageFormOpen(false);
    setEditingPage(null);
    setPageDraft(EMPTY_PAGE_FORM);
    setPageError(null);
  };

  const closePageForm = () => {
    if (!pageSaving) resetPageForm();
  };

  const linkPage = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationError = validateMessengerPageLink(pageDraft);
    if (validationError) {
      setPageError(validationError);
      return;
    }

    setPageSaving(true);
    setPageError(null);
    try {
      const linkedPage = await facebookMessengerAdminService.linkPage({
        accessToken: pageDraft.accessToken.trim(),
        humanAgentEnabled: pageDraft.humanAgentEnabled,
        expectedRevision: settings.revision,
        ...(editingPage ? { existingPageRecordId: editingPage.id } : {}),
      });
      const operation = messengerPageOperationState(
        linkedPage.connectionStatus
      );
      toast.success(
        operation === 'unlinkPending'
          ? 'تم حفظ التوكن الجديد، وسيستكمل النظام إلغاء الربط بأمان.'
          : operation === 'linkPending'
            ? 'تم حفظ التوكن الجديد، وسيستكمل النظام تثبيت الربط بأمان.'
            : editingPage
              ? 'تم تحديث ربط الصفحة وفحصه.'
              : 'تم فحص الصفحة وربطها بنجاح.'
      );
      resetPageForm();
      await loadSettings(undefined, true);
    } catch (error) {
      setPageError(requestErrorText(error, 'تعذر فحص الصفحة وربطها.'));
      await loadSettings(undefined, true);
    } finally {
      setPageSaving(false);
    }
  };

  const checkPage = async (page: FacebookMessengerAdminPage) => {
    setBusyPageId(page.id);
    try {
      const pageCheck = await facebookMessengerAdminService.checkPage(page.id);
      setSettings((current) =>
        current
          ? {
              ...current,
              revision: pageCheck.revision,
              pages: current.pages.map((candidate) =>
                candidate.id === pageCheck.page.id ? pageCheck.page : candidate
              ),
            }
          : current
      );
      const connection = pageConnectionState(pageCheck.page);
      if (connection === 'connected')
        toast.success(`ربط «${pageCheck.page.displayName}» سليم.`);
      else
        toast.error(
          messengerErrorMessage(pageCheck.page.lastErrorCode) ??
            'الصفحة تحتاج مراجعة.'
        );
    } catch (error) {
      toast.error(requestErrorText(error, 'تعذر فحص ربط الصفحة.'));
    } finally {
      setBusyPageId(null);
    }
  };

  const unlinkPage = async () => {
    if (!unlinkTarget) return;
    setBusyPageId(unlinkTarget.id);
    try {
      applySettings(
        await facebookMessengerAdminService.unlinkPage(
          unlinkTarget.id,
          settings.revision
        )
      );
      toast.success(`تم إلغاء ربط «${unlinkTarget.displayName}».`);
      setUnlinkTarget(null);
    } catch (error) {
      toast.error(requestErrorText(error, 'تعذر إلغاء ربط الصفحة.'));
      await loadSettings(undefined, true);
    } finally {
      setBusyPageId(null);
    }
  };

  return (
    <section
      className="space-y-6"
      dir="rtl"
      aria-labelledby="messenger-settings-title"
    >
      <div
        className={`flex flex-col gap-4 rounded-2xl border p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6 ${setupCopy.className}`}
        aria-live="polite"
      >
        <div className="flex items-start gap-3">
          <SetupIcon className="mt-0.5 h-5 w-5 shrink-0" />
          <div>
            <h2 id="messenger-settings-title" className="font-black">
              {setupCopy.title}
            </h2>
            <p className="mt-1 text-sm font-semibold leading-6 opacity-80">
              {setupCopy.description}
            </p>
          </div>
        </div>
        <ol
          className="flex flex-wrap gap-2 text-xs font-black"
          aria-label="خطوات إعداد Messenger"
        >
          <SetupStep
            complete={Boolean(settings.appId && settings.appSecretConfigured)}
            number="١"
            label="التطبيق"
          />
          <SetupStep
            complete={settings.verifyTokenConfigured}
            number="٢"
            label="Webhook"
          />
          <SetupStep
            complete={settings.pages.length > 0}
            number="٣"
            label="الصفحات"
          />
        </ol>
      </div>

      <form
        onSubmit={saveSettings}
        className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-7"
        aria-describedby={settingsError ? settingsErrorId : undefined}
      >
        <div className="flex items-start gap-3">
          <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
            <KeyRound className="h-5 w-5" />
          </span>
          <div>
            <h3 className="text-lg font-black text-[var(--admin-text)]">
              تطبيق Meta
            </h3>
            <p className="mt-1 text-sm font-semibold leading-6 text-[var(--admin-muted)]">
              المفتاح السري يُحفظ مشفّرًا ولا يظهر مرة أخرى بعد الحفظ.
            </p>
          </div>
        </div>

        <div className="mt-6 grid gap-5 md:grid-cols-2">
          <label className="block text-sm font-bold text-[var(--admin-text)]">
            App ID
            <input
              value={draft.appId}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  appId: event.target.value.replace(/\D/g, '').slice(0, 32),
                }))
              }
              disabled={settingsSaving}
              inputMode="numeric"
              autoComplete="off"
              dir="ltr"
              placeholder="123456789012345"
              className="admin-input mt-2 text-left font-mono"
            />
          </label>

          <label className="block text-sm font-bold text-[var(--admin-text)]">
            إصدار Graph API
            <select
              value={draft.apiVersion}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  apiVersion: event.target.value,
                }))
              }
              disabled={settingsSaving}
              className="admin-input mt-2"
            >
              {supportedApiVersions.map((version) => (
                <option key={version} value={version}>
                  {version}
                </option>
              ))}
            </select>
          </label>

          <label className="block text-sm font-bold text-[var(--admin-text)] md:col-span-2">
            <span className="flex flex-wrap items-center gap-2">
              App Secret {settings.appSecretConfigured && <ConfiguredBadge />}
            </span>
            <input
              type="password"
              value={draft.appSecret}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  appSecret: event.target.value.trim(),
                }))
              }
              disabled={settingsSaving}
              autoComplete="new-password"
              spellCheck={false}
              dir="ltr"
              placeholder={
                settings.appSecretConfigured
                  ? 'اتركه فارغًا للاحتفاظ بالمفتاح الحالي'
                  : 'ألصق App Secret الكامل'
              }
              className="admin-input mt-2 text-left font-mono"
            />
            <span className="mt-2 block text-xs font-semibold leading-5 text-[var(--admin-muted)]">
              لن تعرض المنصة القيمة الحالية؛ إدخال قيمة جديدة يستبدلها.
            </span>
          </label>
        </div>

        {settingsError && (
          <div
            id={settingsErrorId}
            className="mt-5 rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm font-bold text-red-700 dark:text-red-300"
            role="alert"
          >
            {settingsError}
          </div>
        )}

        <div className="mt-6 flex justify-end border-t border-[var(--admin-border)] pt-5">
          <button
            type="submit"
            disabled={settingsSaving}
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-6 text-sm font-black text-[var(--admin-primary-contrast)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {settingsSaving ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Save className="h-4 w-4" />
            )}
            {settingsSaving ? 'جاري الحفظ...' : 'حفظ إعداد التطبيق'}
          </button>
        </div>
      </form>

      <section
        className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-7"
        aria-labelledby="messenger-webhook-title"
      >
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex items-start gap-3">
            <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
              <ShieldCheck className="h-5 w-5" />
            </span>
            <div>
              <h3
                id="messenger-webhook-title"
                className="text-lg font-black text-[var(--admin-text)]"
              >
                Webhook والتحقق
              </h3>
              <p className="mt-1 text-sm font-semibold leading-6 text-[var(--admin-muted)]">
                انسخ الرابط والتوكن إلى إعداد Messenger داخل Meta App.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={() => setRotateDialogOpen(true)}
            disabled={rotatingToken}
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 text-sm font-black text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:opacity-60"
          >
            <RefreshCw
              className={`h-4 w-4 ${rotatingToken ? 'animate-spin' : ''}`}
            />
            {settings.verifyTokenConfigured
              ? 'تدوير Verify Token'
              : 'إنشاء Verify Token'}
          </button>
        </div>

        <div className="mt-6 space-y-4">
          <div>
            <p className="mb-2 text-sm font-bold text-[var(--admin-text)]">
              Callback URL
            </p>
            <CopyField
              copyText={settings.webhookUrl}
              copyLabel="نسخ الرابط"
              onCopy={() =>
                copySecureValue(settings.webhookUrl, 'تم نسخ Callback URL.')
              }
            />
          </div>

          <div className="flex items-center justify-between gap-4 rounded-xl bg-[var(--admin-card-soft)] px-4 py-3">
            <div>
              <p className="text-sm font-black text-[var(--admin-text)]">
                Verify Token
              </p>
              <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]">
                {settings.verifyTokenConfigured
                  ? 'محفوظ ولا يمكن استعادته من الخادم.'
                  : 'لم يتم إنشاؤه بعد.'}
              </p>
            </div>
            {settings.verifyTokenConfigured ? (
              <ConfiguredBadge />
            ) : (
              <span className="rounded-full bg-amber-500/10 px-2.5 py-1 text-xs font-black text-amber-700 dark:text-amber-300">
                مطلوب
              </span>
            )}
          </div>

          {oneTimeVerifyToken && (
            <div
              className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-4"
              role="status"
              aria-live="polite"
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-black text-amber-900 dark:text-amber-200">
                    انسخ Verify Token الآن
                  </p>
                  <p className="mt-1 text-xs font-semibold leading-5 text-amber-800 dark:text-amber-300">
                    لن يعيده الخادم بعد مغادرة الصفحة. لا ترسله في رسالة أو
                    تحفظه في Git.
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setOneTimeVerifyToken(null)}
                  className="grid size-11 shrink-0 place-items-center rounded-xl text-amber-900 hover:bg-amber-500/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-600 dark:text-amber-200"
                  aria-label="إخفاء Verify Token"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
              <div className="mt-3">
                <CopyField
                  copyText={oneTimeVerifyToken}
                  copyLabel="نسخ التوكن"
                  onCopy={() =>
                    copySecureValue(oneTimeVerifyToken, 'تم نسخ Verify Token.')
                  }
                />
              </div>
            </div>
          )}
        </div>
      </section>

      <section className="space-y-4" aria-labelledby="messenger-pages-title">
        <div className="flex flex-col gap-4 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:flex-row sm:items-center sm:justify-between sm:p-7">
          <div className="flex items-start gap-3">
            <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
              <MessageCircleMore className="h-5 w-5" />
            </span>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h3
                  id="messenger-pages-title"
                  className="text-lg font-black text-[var(--admin-text)]"
                >
                  صفحات Facebook
                </h3>
                <span className="rounded-full bg-[var(--admin-card-soft)] px-2.5 py-1 text-xs font-black text-[var(--admin-muted)]">
                  {settings.pages.length} / {MAX_MESSENGER_PAGES}
                </span>
              </div>
              <p className="mt-1 max-w-2xl text-sm font-semibold leading-6 text-[var(--admin-muted)]">
                ألصق Page Access Token؛ الخادم يكتشف اسم الصفحة ورقمها، يفحص
                التوكن، ثم يشترك في Webhook.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={() => openPageForm()}
            disabled={!canLinkPages || pageLimitReached || pageSaving}
            title={
              !canLinkPages
                ? 'أكمل إعداد التطبيق وVerify Token أولًا'
                : pageLimitReached
                  ? 'تم الوصول إلى ثلاث صفحات'
                  : undefined
            }
            className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            <Plus className="h-4 w-4" />
            ربط صفحة
          </button>
        </div>

        {pageFormOpen && (
          <form
            onSubmit={linkPage}
            className="rounded-2xl border border-[var(--admin-primary)]/25 bg-[var(--admin-card)] p-5 sm:p-6"
            aria-describedby={pageError ? pageErrorId : undefined}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <h4 className="font-black text-[var(--admin-text)]">
                  {editingPage
                    ? editingOperation === 'unlinkPending'
                      ? `استكمال إلغاء ربط ${editingPage.displayName}`
                      : `تحديث ربط ${editingPage.displayName}`
                    : 'ربط صفحة جديدة'}
                </h4>
                <p className="mt-1 text-sm font-semibold leading-6 text-[var(--admin-muted)]">
                  {editingPage
                    ? editingOperation === 'unlinkPending'
                      ? 'ألصق توكن صالحًا لنفس الصفحة؛ سيُستخدم لإكمال الإلغاء فقط ولن يعكس اتجاه العملية.'
                      : editingOperation === 'linkPending'
                        ? 'ألصق توكن صالحًا لنفس الصفحة لاستكمال الربط المحجوز دون فتح عملية عكسية.'
                        : 'ألصق توكن جديدًا لنفس الصفحة؛ لن تُعرض القيمة المحفوظة.'
                    : 'بعد فحص هوية الصفحة، يُحفظ التوكن مشفّرًا ثم يُراجع اشتراك Meta.'}
                </p>
              </div>
              <button
                type="button"
                onClick={closePageForm}
                disabled={pageSaving}
                className="grid size-11 shrink-0 place-items-center rounded-xl border border-[var(--admin-border)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                aria-label="إغلاق نموذج ربط الصفحة"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="mt-5 grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(280px,.7fr)]">
              <label className="block text-sm font-bold text-[var(--admin-text)]">
                Page Access Token
                <input
                  type="password"
                  value={pageDraft.accessToken}
                  onChange={(event) =>
                    setPageDraft((current) => ({
                      ...current,
                      accessToken: event.target.value.trim(),
                    }))
                  }
                  disabled={pageSaving}
                  autoComplete="new-password"
                  spellCheck={false}
                  dir="ltr"
                  placeholder="ألصق التوكن الكامل"
                  className="admin-input mt-2 text-left font-mono"
                  autoFocus
                />
                <span className="mt-2 block text-xs font-semibold text-[var(--admin-muted)]">
                  القيمة write-only ولن تظهر بعد الربط.
                </span>
              </label>

              <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-sm font-black text-[var(--admin-text)]">
                      رد يدوي حتى 7 أيام
                    </p>
                    <p className="mt-1 text-xs font-semibold leading-5 text-[var(--admin-muted)]">
                      فعّله فقط بعد اعتماد HUMAN_AGENT من Meta. هذا لا يشغّل AI.
                    </p>
                  </div>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={pageDraft.humanAgentEnabled}
                    aria-label="تفعيل رد الموظف حتى سبعة أيام"
                    onClick={() =>
                      setPageDraft((current) => ({
                        ...current,
                        humanAgentEnabled: !current.humanAgentEnabled,
                      }))
                    }
                    disabled={pageSaving}
                    className={`relative h-7 w-12 shrink-0 rounded-full transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${pageDraft.humanAgentEnabled ? 'bg-[var(--admin-primary)]' : 'bg-[var(--admin-border)]'}`}
                  >
                    <span
                      className={`absolute right-1 top-1 size-5 rounded-full bg-white shadow-sm transition-transform ${pageDraft.humanAgentEnabled ? 'translate-x-0' : '-translate-x-5'}`}
                    />
                  </button>
                </div>
              </div>
            </div>

            {pageError && (
              <div
                id={pageErrorId}
                className="mt-5 rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm font-bold text-red-700 dark:text-red-300"
                role="alert"
              >
                {pageError}
              </div>
            )}

            <div className="mt-5 flex flex-col-reverse gap-2 border-t border-[var(--admin-border)] pt-5 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={closePageForm}
                disabled={pageSaving}
                className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 text-sm font-bold text-[var(--admin-text)] disabled:opacity-50"
              >
                إلغاء
              </button>
              <button
                type="submit"
                disabled={pageSaving}
                className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-6 text-sm font-black text-[var(--admin-primary-contrast)] disabled:opacity-50"
              >
                {pageSaving ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Link2 className="h-4 w-4" />
                )}
                {pageSaving
                  ? 'جاري فحص التوكن...'
                  : editingPage
                    ? editingOperation === 'unlinkPending'
                      ? 'احفظ واستكمل الإلغاء'
                      : editingOperation === 'linkPending'
                        ? 'احفظ واستكمل الربط'
                        : 'تحقق وحدّث الربط'
                    : 'اختبر واربط الصفحة'}
              </button>
            </div>
          </form>
        )}

        {settings.pages.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-12 text-center">
            <Link2 className="mx-auto h-8 w-8 text-[var(--admin-primary)]" />
            <h4 className="mt-4 text-lg font-black text-[var(--admin-text)]">
              لم تُربط أي صفحة بعد
            </h4>
            <p className="mx-auto mt-2 max-w-xl text-sm font-semibold leading-6 text-[var(--admin-muted)]">
              بعد حفظ تطبيق Meta وإنشاء Verify Token، اربط الصفحة الأولى بالتوكن
              الخاص بها. يمكن إضافة ثلاث صفحات كحد أقصى.
            </p>
          </div>
        ) : (
          <div className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
            <div className="divide-y divide-[var(--admin-border)]">
              {settings.pages.map((page) => (
                <MessengerPageRow
                  key={page.id}
                  page={page}
                  busy={busyPageId === page.id}
                  onCheck={() => void checkPage(page)}
                  onEdit={() => openPageForm(page)}
                  onUnlink={() => setUnlinkTarget(page)}
                />
              ))}
            </div>
          </div>
        )}

        <div className="rounded-xl bg-[var(--admin-card-soft)] px-4 py-3 text-xs font-semibold leading-6 text-[var(--admin-muted)]">
          زر الفحص يراجع صلاحية التوكن واشتراك الصفحة بدون إرسال رسالة. للتأكد
          من الاستقبال فعليًا، أرسل رسالة من حساب Facebook حقيقي ثم راقب «آخر
          رسالة واردة».
        </div>
      </section>

      <AdminConfirmationDialog
        open={rotateDialogOpen}
        onClose={() => setRotateDialogOpen(false)}
        onConfirm={rotateVerifyToken}
        title={
          settings.verifyTokenConfigured
            ? 'تدوير Verify Token'
            : 'إنشاء Verify Token'
        }
        consequence={
          settings.verifyTokenConfigured
            ? 'ستظهر القيمة الجديدة مرة واحدة فقط. حدّثها في Meta قبل إغلاق هذه الصفحة؛ القيمة القديمة لن تكون صالحة للتحقق الجديد.'
            : 'سيُنشئ الخادم قيمة آمنة تظهر مرة واحدة لتنسخها إلى Meta.'
        }
        confirmLabel={
          settings.verifyTokenConfigured ? 'أنشئ قيمة جديدة' : 'إنشاء التوكن'
        }
        isConfirming={rotatingToken}
      />

      <AdminConfirmationDialog
        open={unlinkTarget !== null}
        onClose={() => setUnlinkTarget(null)}
        onConfirm={unlinkPage}
        title="إلغاء ربط صفحة Facebook"
        consequence={`ستتوقف الرسائل الجديدة من «${unlinkTarget?.displayName ?? ''}» عن الدخول إلى الدعم المباشر، وستُزال بيانات الربط المحفوظة. المحادثات السابقة ستظل محفوظة.`}
        confirmLabel="إلغاء ربط الصفحة"
        variant="danger"
        isConfirming={Boolean(unlinkTarget && busyPageId === unlinkTarget.id)}
      />
    </section>
  );
}

function SetupStep({
  complete,
  number,
  label,
}: {
  complete: boolean;
  number: string;
  label: string;
}) {
  return (
    <li
      className={`inline-flex min-h-8 items-center gap-1.5 rounded-full px-3 ${complete ? 'bg-emerald-500/15 text-emerald-800 dark:text-emerald-300' : 'bg-[var(--admin-card)] text-[var(--admin-muted)]'}`}
    >
      <span
        className="grid size-5 place-items-center rounded-full bg-current/10"
        aria-hidden="true"
      >
        {complete ? <Check className="h-3 w-3" /> : number}
      </span>
      {label}
    </li>
  );
}

function ConfiguredBadge() {
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/10 px-2.5 py-1 text-xs font-black text-emerald-700 dark:text-emerald-300">
      <Check className="h-3 w-3" />
      محفوظ
    </span>
  );
}

function CopyField({
  copyText,
  copyLabel,
  onCopy,
}: {
  copyText: string;
  copyLabel: string;
  onCopy: () => void;
}) {
  return (
    <div className="flex flex-col gap-2 sm:flex-row" dir="ltr">
      <input
        value={copyText}
        readOnly
        spellCheck={false}
        className="admin-input min-w-0 flex-1 text-left font-mono text-xs"
        aria-label={
          copyLabel === 'نسخ الرابط' ? 'Callback URL' : 'Verify Token الجديد'
        }
      />
      <button
        type="button"
        onClick={onCopy}
        className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 text-sm font-black text-[var(--admin-text)] hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
      >
        <Clipboard className="h-4 w-4" />
        {copyLabel}
      </button>
    </div>
  );
}

function MessengerPageRow({
  page,
  busy,
  onCheck,
  onEdit,
  onUnlink,
}: {
  page: FacebookMessengerAdminPage;
  busy: boolean;
  onCheck: () => void;
  onEdit: () => void;
  onUnlink: () => void;
}) {
  const operation = messengerPageOperationState(page.connectionStatus);
  const canReplaceToken = canReplaceMessengerPageToken(page.connectionStatus);
  const state = pageConnectionState(page);
  const stateCopy =
    operation === 'linkPending'
      ? {
          label: 'جاري تثبيت الربط',
          className: 'bg-sky-500/10 text-sky-700 dark:text-sky-300',
        }
      : operation === 'unlinkPending'
        ? {
            label: 'جاري إلغاء الربط',
            className: 'bg-amber-500/10 text-amber-700 dark:text-amber-300',
          }
        : state === 'connected'
          ? {
              label: 'متصلة',
              className:
                'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300',
            }
          : state === 'attention'
            ? {
                label: 'تحتاج مراجعة',
                className: 'bg-amber-500/10 text-amber-700 dark:text-amber-300',
              }
            : {
                label: 'لم تُفحص',
                className:
                  'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]',
              };
  const errorMessage = messengerErrorMessage(page.lastErrorCode);

  return (
    <article className="grid gap-5 p-4 transition-colors hover:bg-[var(--admin-hover)] sm:p-5 xl:grid-cols-[minmax(0,1.1fr)_minmax(260px,.8fr)_auto] xl:items-center">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <h4 className="truncate text-base font-black text-[var(--admin-text)]">
            {page.displayName}
          </h4>
          <span
            className={`rounded-full px-2.5 py-1 text-xs font-black ${stateCopy.className}`}
          >
            {stateCopy.label}
          </span>
          <span className="rounded-full bg-[var(--admin-primary-15)] px-2.5 py-1 text-xs font-black text-[var(--admin-primary)]">
            موظفون فقط
          </span>
          {page.humanAgentEnabled && (
            <span className="rounded-full bg-[var(--admin-card-soft)] px-2.5 py-1 text-xs font-black text-[var(--admin-muted)]">
              نافذة 7 أيام
            </span>
          )}
        </div>
        <p
          className="mt-2 font-mono text-xs font-bold text-[var(--admin-muted)]"
          dir="ltr"
        >
          Page ID: {page.pageId}
        </p>
        <p className="mt-1 text-xs font-semibold text-[var(--admin-muted)]">
          آخر فحص: {formatAdminDate(page.lastCheckedAtUtc)}
        </p>
        {errorMessage && (
          <p className="mt-2 text-xs font-bold text-amber-700 dark:text-amber-300">
            {errorMessage}
          </p>
        )}
      </div>

      <dl className="grid grid-cols-2 gap-3 rounded-xl bg-[var(--admin-card-soft)] p-4 text-xs">
        <div>
          <dt className="font-bold text-[var(--admin-muted)]">التوكن</dt>
          <dd className="mt-1 font-black text-[var(--admin-text)]">
            {!page.accessTokenConfigured
              ? 'مطلوب'
              : page.tokenValid === true
                ? 'صالح'
                : page.tokenValid === false
                  ? 'غير صالح'
                  : 'لم يُفحص'}
          </dd>
        </div>
        <div>
          <dt className="font-bold text-[var(--admin-muted)]">الاشتراك</dt>
          <dd className="mt-1 font-black text-[var(--admin-text)]">
            {page.subscribed === true
              ? 'مفعّل'
              : page.subscribed === false
                ? 'غير مفعّل'
                : 'لم يُفحص'}
          </dd>
        </div>
        <div className="col-span-2 border-t border-[var(--admin-border)] pt-3">
          <dt className="font-bold text-[var(--admin-muted)]">
            آخر رسالة واردة
          </dt>
          <dd className="mt-1 font-black text-[var(--admin-text)]">
            {formatAdminDate(page.lastInboundAtUtc)}
          </dd>
        </div>
      </dl>

      <div className="flex flex-wrap items-center gap-2 xl:justify-end">
        <button
          type="button"
          onClick={onCheck}
          disabled={busy || operation !== 'idle'}
          className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 text-sm font-black text-[var(--admin-text)] hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:opacity-50"
        >
          {busy ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <RefreshCw className="h-4 w-4" />
          )}
          فحص الربط
        </button>
        <button
          type="button"
          onClick={onEdit}
          disabled={busy || !canReplaceToken}
          className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 text-sm font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] disabled:opacity-50"
        >
          <KeyRound className="h-4 w-4" />
          تحديث التوكن
        </button>
        <button
          type="button"
          onClick={onUnlink}
          disabled={busy || operation !== 'idle'}
          className="grid size-11 place-items-center rounded-xl text-red-600 hover:bg-red-500/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-600 disabled:opacity-50"
          aria-label={`إلغاء ربط صفحة ${page.displayName}`}
          title="إلغاء ربط الصفحة"
        >
          <Trash2 className="h-4 w-4" />
        </button>
      </div>
    </article>
  );
}

function MessengerSettingsSkeleton() {
  return (
    <div
      className="space-y-4"
      dir="rtl"
      aria-label="جاري تحميل إعدادات Messenger"
      aria-busy="true"
    >
      <div className="h-24 animate-pulse rounded-2xl bg-[var(--admin-card-soft)]" />
      <div className="h-72 animate-pulse rounded-2xl bg-[var(--admin-card-soft)]" />
      <div className="h-52 animate-pulse rounded-2xl bg-[var(--admin-card-soft)]" />
    </div>
  );
}
