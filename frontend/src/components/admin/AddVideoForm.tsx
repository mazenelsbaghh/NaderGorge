'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  adminService,
  type LessonCockpitVideoDto,
  type BunnyLibraryReferenceDto,
  type VideoProvider,
} from '@/services/admin-service';
import toast from 'react-hot-toast';
import { NumberField } from '@/components/ui/number-field';
import NeumorphButton from '@/components/ui/neumorph-button';
import { Dropdown } from '@/components/ui/dropdown';
import * as tus from 'tus-js-client';
import { getApiErrorSummary } from '@/lib/api-errors';
import { parseBunnyVideoReference } from '@/lib/bunny-video-reference';
import type { BunnyTusUploadSession } from '@/services/admin-service';
import { BunnyLibrarySelect } from './BunnyLibrarySelect';
import { VideoTypeSelect } from './VideoTypeSelect';
import { AdminConfirmationDialog } from './AdminConfirmationDialog';

interface AddVideoFormProps {
  lessonId: string;
  onSuccess?: () => void;
  editingVideo?: LessonCockpitVideoDto;
  onCancel?: () => void;
}

type BunnySourceMode = 'manual' | 'file' | 'fetch';
type BunnyPlaybackSelection = 0 | 1;

class BunnyTusTransferError extends Error {
  constructor(error: Error) {
    super(error.message);
    this.name = 'BunnyTusTransferError';
  }
}

function videoProviderForForm(provider?: string): VideoProvider {
  switch (provider?.trim().toLowerCase()) {
    case 'bunny':
      return 'bunny';
    case 'vk':
      return 'vk';
    default:
      return 'YouTube';
  }
}

function uploadFileToBunny(
  file: File,
  session: BunnyTusUploadSession,
  reportProgress: (percentage: number) => void
) {
  return new Promise<void>((resolve, reject) => {
    const upload = new tus.Upload(file, {
      endpoint: session.tusEndpoint,
      headers: session.uploadHeaders,
      metadata: {
        filename: file.name,
        filetype: file.type || 'video/mp4',
      },
      onError: (error) => reject(new BunnyTusTransferError(error)),
      onProgress: (uploaded, total) => reportProgress(total > 0 ? Math.round((uploaded / total) * 100) : 0),
      onSuccess: () => {
        adminService.completeBunnyUpload(session.bunnyVideoAssetId)
          .then(() => resolve())
          .catch(reject);
      },
    });
    upload.start();
  });
}

export function AddVideoForm({ lessonId, onSuccess, editingVideo, onCancel }: AddVideoFormProps) {
  const isEditing = Boolean(editingVideo);
  const activeToggleId = `video-form-is-active-${editingVideo?.id ?? 'new'}`;
  const [title, setTitle] = useState(() => editingVideo?.title ?? '');
  const [provider, setProvider] = useState<VideoProvider>(() => videoProviderForForm(editingVideo?.provider));
  const [urlOrEmbedCode, setUrlOrEmbedCode] = useState(() => editingVideo?.url ?? '');
  const [order, setOrder] = useState(() => editingVideo?.order ?? 1);
  const [limit, setLimit] = useState(() => editingVideo?.maxWatchCount ?? 3);
  const [isActive, setIsActive] = useState(() => editingVideo?.isActive ?? true);
  const [videoTypeId, setVideoTypeId] = useState(() => editingVideo?.videoType.id ?? '');
  const [videoTypesAvailable, setVideoTypesAvailable] = useState(false);
  const [saving, setSaving] = useState(false);
  const [bunnyMode, setBunnyMode] = useState<BunnySourceMode>('manual');
  const [bunnyFile, setBunnyFile] = useState<File | null>(null);
  const [bunnySourceUrl, setBunnySourceUrl] = useState('');
  const [bunnyStreamLibraryId, setBunnyStreamLibraryId] = useState(() => editingVideo?.bunnyLibrary?.id ?? '');
  const [bunnyLibraryAvailable, setBunnyLibraryAvailable] = useState(false);
  const [bunnyHlsAvailable, setBunnyHlsAvailable] = useState(Boolean(editingVideo?.bunnyLibrary?.hlsConfigured));
  const [bunnyPlaybackMode, setBunnyPlaybackMode] = useState<BunnyPlaybackSelection>(() => editingVideo?.bunnyPlaybackMode ?? 0);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [sourceChangeConfirmationOpen, setSourceChangeConfirmationOpen] = useState(false);

  useEffect(() => {
    if (!editingVideo) return;
    setBunnyPlaybackMode(editingVideo.bunnyPlaybackMode ?? 0);
  }, [editingVideo, editingVideo?.bunnyPlaybackMode]);

  const isBunny = provider === 'bunny';
  const bunnyReference = isBunny && bunnyMode === 'manual'
    ? parseBunnyVideoReference(urlOrEmbedCode)
    : null;
  const bunnyReferenceInvalid = isBunny && bunnyMode === 'manual' && Boolean(urlOrEmbedCode.trim()) && !bunnyReference;
  const bunnyModeReady = bunnyMode === 'file'
    ? Boolean(bunnyFile)
    : bunnyMode === 'fetch'
      ? Boolean(bunnySourceUrl.trim())
      : Boolean(bunnyReference);
  const sourceReady = isBunny ? bunnyModeReady : Boolean(urlOrEmbedCode.trim());
  const canSubmit = Boolean(
    title.trim() &&
    videoTypeId &&
    videoTypesAvailable &&
    sourceReady &&
    order >= 1 &&
    limit >= 0 &&
    (!isBunny || (bunnyStreamLibraryId && bunnyLibraryAvailable))
  );
  const sourceMayChange = Boolean(
    editingVideo && (
      provider !== videoProviderForForm(editingVideo.provider)
      || (isBunny && bunnyMode !== 'manual')
      || urlOrEmbedCode.trim() !== (editingVideo.url ?? '').trim()
    )
  );

  const handleSelectedBunnyLibraryChange = useCallback((library: BunnyLibraryReferenceDto | null) => {
    const hlsReady = Boolean(library?.hlsConfigured);
    setBunnyHlsAvailable(hlsReady);
    setBunnyPlaybackMode((current) => {
      if (!hlsReady) return 0;
      return editingVideo ? current : 1;
    });
  }, [editingVideo]);

  function selectProvider(nextProvider: VideoProvider) {
    if (nextProvider === provider) return;

    // A selected local file cannot be represented by the browser after its
    // input is unmounted. Clear every transient Bunny input while changing
    // source so a later save can never submit a hidden, stale file or URL.
    setProvider(nextProvider);
    setBunnyMode('manual');
    setBunnyFile(null);
    setBunnySourceUrl('');
    setBunnyStreamLibraryId('');
    setBunnyLibraryAvailable(false);
    setBunnyHlsAvailable(false);
    setBunnyPlaybackMode(0);
    setUploadProgress(0);
  }

  function selectBunnyMode(nextMode: BunnySourceMode) {
    if (nextMode === bunnyMode) return;

    setBunnyMode(nextMode);
    if (nextMode !== 'file') setBunnyFile(null);
    if (nextMode !== 'fetch') setBunnySourceUrl('');
    setUploadProgress(0);
  }

  async function saveVideo() {
    let replacementAssetId: string | null = null;

    try {
      setSaving(true);
      if (isBunny && bunnyMode === 'file' && bunnyFile) {
        setUploadProgress(0);
        const session = await adminService.createBunnyTusUpload({
          lessonId,
          title,
          order,
          maxWatchCount: limit,
          videoTypeId,
          fileName: bunnyFile.name,
          fileSizeBytes: bunnyFile.size,
          bunnyStreamLibraryId,
          isActive,
          existingLessonVideoId: editingVideo?.id,
          bunnyPlaybackMode,
        });

        if (!session) throw new Error('Missing Bunny upload session');
        if (isEditing) replacementAssetId = session.bunnyVideoAssetId;
        await uploadFileToBunny(bunnyFile, session, setUploadProgress);
      } else if (isBunny && bunnyMode === 'fetch') {
        await adminService.fetchBunnyVideo({
          lessonId,
          title,
          order,
          maxWatchCount: limit,
          videoTypeId,
          sourceUrl: bunnySourceUrl.trim(),
          bunnyStreamLibraryId,
          isActive,
          existingLessonVideoId: editingVideo?.id,
          bunnyPlaybackMode,
        });
      } else if (editingVideo) {
        await adminService.updateVideo(editingVideo.id, {
          title,
          provider,
          urlOrEmbedCode,
          order,
          limit,
          videoTypeId,
          isActive,
          bunnyStreamLibraryId: isBunny ? bunnyStreamLibraryId : null,
          bunnyPlaybackMode: isBunny ? bunnyPlaybackMode : 0,
        });
      } else {
        await adminService.createVideo({
          lessonId,
          title,
          provider,
          urlOrEmbedCode,
          order,
          limit,
          videoTypeId,
          isActive,
          bunnyStreamLibraryId: isBunny ? bunnyStreamLibraryId : undefined,
          bunnyPlaybackMode: isBunny ? bunnyPlaybackMode : 0,
        });
      }
      toast.success(isBunny && bunnyMode !== 'manual'
        ? isEditing
          ? 'بدأ استبدال مصدر الفيديو داخل Bunny. سيبقى الفيديو الحالي متاحًا حتى يكتمل التجهيز.'
          : 'تم إرسال الفيديو إلى Bunny، وسيظهر للطلاب تلقائيًا بعد اكتمال التجهيز.'
        : isEditing ? 'تم تعديل الفيديو بنجاح.' : 'تمت إضافة الفيديو بنجاح.');
      if (!isEditing) {
        setTitle('');
        setUrlOrEmbedCode('');
        setBunnyFile(null);
        setBunnySourceUrl('');
        setBunnyStreamLibraryId('');
        setBunnyLibraryAvailable(false);
        setBunnyHlsAvailable(false);
        setBunnyPlaybackMode(0);
        setUploadProgress(0);
        setOrder((prev) => prev + 1);
      }
      onSuccess?.();
      return true;
    } catch (requestError) {
      if (isEditing && replacementAssetId && requestError instanceof BunnyTusTransferError) {
        try {
          await adminService.cancelBunnyVideoReplacement(replacementAssetId);
        } catch {
          // The candidate can already be terminal; the server also expires
          // abandoned replacements, so the original video remains editable.
        }
      }
      const errorMessage = getApiErrorSummary(
        requestError,
        isEditing ? 'حدث خطأ أثناء تعديل الفيديو، أعد المحاولة.' : 'حدث خطأ أثناء إضافة الفيديو، أعد المحاولة.',
      );
      toast.error(errorMessage, { id: errorMessage });
      return false;
    } finally {
      setSaving(false);
    }
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit || saving) return;

    if (sourceMayChange) {
      setSourceChangeConfirmationOpen(true);
      return;
    }

    void saveVideo();
  }

  return (
    <form id={isEditing ? `edit-video-form-${editingVideo?.id}` : 'add-video-form'} onSubmit={handleSubmit} className="flex flex-col gap-4">
      {isEditing && <div className="text-sm font-bold text-[var(--admin-text)]">تعديل بيانات الفيديو</div>}
      <div className="flex flex-wrap items-end gap-4">
        <div className="flex-1 space-y-2 min-w-[200px]">
          <label className="text-xs font-bold text-[var(--admin-muted)]">عنوان الفيديو</label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="مثال: الدرس الأول - مراجعة"
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow]"
            required
          />
        </div>
        <div className="w-40 space-y-2">
          <Dropdown
            label="المنصة"
            value={provider}
            onChange={(v) => selectProvider(v as VideoProvider)}
            size="sm"
            options={[
              { value: 'YouTube', label: 'YouTube' },
              { value: 'vk', label: 'VK (فيكونتاكتي)' },
              { value: 'bunny', label: 'Bunny.net' },
            ]}
          />
        </div>
        {!isBunny && (
          <div className="flex-1 space-y-2 min-w-[200px]">
            <label className="text-xs font-bold text-[var(--admin-muted)]">رابط الفيديو (أو المعرف)</label>
            <input
              type="text"
              value={urlOrEmbedCode}
              onChange={(e) => {
                const val = e.target.value;
                if (val.includes('vk.com/video') || val.includes('vk.com/video_ext')) {
                  selectProvider('vk');
                } else if (val.includes('youtube.com') || val.includes('youtu.be')) {
                  selectProvider('YouTube');
                } else if (val.includes('mediadelivery.net')) {
                  selectProvider('bunny');
                }
                setUrlOrEmbedCode(val);
              }}
              placeholder={provider === 'vk' ? 'مثال: oid=-22822305&id=456241864' : 'رابط الفيديو'}
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow]"
              required
            />
          </div>
        )}
      </div>
      {isBunny && (
        <div className="space-y-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4">
          <div className="mb-3 flex flex-wrap gap-2">
            {[
              { value: 'manual', label: 'ربط فيديو موجود' },
              { value: 'file', label: 'رفع ملف' },
              { value: 'fetch', label: 'جلب من رابط مباشر' },
            ].map((option) => (
              <button
                key={option.value}
                type="button"
                onClick={() => selectBunnyMode(option.value as BunnySourceMode)}
                aria-pressed={bunnyMode === option.value}
                className={`rounded-full px-4 py-2 text-xs font-bold transition-colors ${bunnyMode === option.value ? 'bg-[var(--admin-primary)] text-white' : 'bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}
              >
                {option.label}
              </button>
            ))}
          </div>

          <BunnyLibrarySelect
            value={bunnyStreamLibraryId}
            onChange={setBunnyStreamLibraryId}
            detectedLibraryId={bunnyReference?.libraryId}
            currentLibrary={bunnyMode === 'manual' ? editingVideo?.bunnyLibrary : undefined}
            onAvailabilityChange={setBunnyLibraryAvailable}
            onSelectedLibraryChange={handleSelectedBunnyLibraryChange}
          />

          <fieldset className="space-y-2">
            <legend className="text-xs font-bold text-[var(--admin-muted)]">مشغل الفيديو</legend>
            <div className="grid gap-2 sm:grid-cols-2">
              <button type="button" onClick={() => setBunnyPlaybackMode(1)} disabled={!bunnyHlsAvailable} aria-pressed={bunnyPlaybackMode === 1} className={`min-h-14 rounded-xl border px-4 py-3 text-right transition-colors ${bunnyPlaybackMode === 1 ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)]'} disabled:cursor-not-allowed disabled:opacity-45`}>
                <span className="block text-sm font-black">مشغل المنصة HLS</span>
                <span className="mt-1 block text-xs font-semibold">تحكم المنصة + اختيار الجودة</span>
              </button>
              <button type="button" onClick={() => setBunnyPlaybackMode(0)} aria-pressed={bunnyPlaybackMode === 0} className={`min-h-14 rounded-xl border px-4 py-3 text-right transition-colors ${bunnyPlaybackMode === 0 ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)] text-[var(--admin-primary)]' : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)]'}`}>
                <span className="block text-sm font-black">مشغل Bunny</span>
                <span className="mt-1 block text-xs font-semibold">المشغل الحالي كما هو</span>
              </button>
            </div>
            {!bunnyHlsAvailable && <p className="text-xs font-semibold text-amber-700 dark:text-amber-300">جهّز CDN hostname وToken Key للمكتبة من إعدادات Bunny لتفعيل مشغل المنصة.</p>}
          </fieldset>

          {bunnyMode === 'manual' && (
            <div className="space-y-2">
              <label className="text-xs font-bold text-[var(--admin-muted)]">رابط Bunny الكامل أو Video GUID</label>
              <input
                type="text"
                value={urlOrEmbedCode}
                onChange={(event) => setUrlOrEmbedCode(event.target.value)}
                placeholder="https://player.mediadelivery.net/play/740733/..."
                dir="ltr"
                className={`w-full rounded-xl border bg-[var(--admin-card)] px-4 py-3 text-left font-mono text-sm text-[var(--admin-text)] outline-none focus:ring-1 ${bunnyReferenceInvalid ? 'border-red-500 focus:border-red-500 focus:ring-red-500' : 'border-[var(--admin-border)] focus:border-[var(--admin-primary)] focus:ring-[var(--admin-primary)]'}`}
                required
              />
              {bunnyReferenceInvalid && <p className="text-xs font-bold text-red-600 dark:text-red-400" role="alert">أدخل رابط Bunny من نوع play أو embed، أو GUID صحيحًا.</p>}
            </div>
          )}

          {bunnyMode === 'file' && (
            <div className="space-y-2">
              <label className="block text-xs font-bold text-[var(--admin-muted)]">ملف الفيديو</label>
              <input
                type="file"
                accept="video/*"
                onChange={(e) => setBunnyFile(e.target.files?.[0] ?? null)}
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)]"
              />
              {saving && uploadProgress > 0 && (
                <div className="h-2 overflow-hidden rounded-full bg-[var(--admin-border)]">
                  <div className="h-full bg-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow]" style={{ width: `${uploadProgress}%` }} />
                </div>
              )}
            </div>
          )}

          {bunnyMode === 'fetch' && (
            <div className="space-y-2">
              <label className="text-xs font-bold text-[var(--admin-muted)]">رابط ملف الفيديو المباشر</label>
              <input
                type="url"
                value={bunnySourceUrl}
                onChange={(event) => setBunnySourceUrl(event.target.value)}
                placeholder="https://example.com/video.mp4"
                dir="ltr"
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-left text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)]"
                required
              />
              <p className="text-xs font-semibold text-[var(--admin-muted)]">سيجلب Bunny الملف من الرابط ويبدأ ترميزه داخل المكتبة المختارة.</p>
            </div>
          )}
        </div>
      )}
      {sourceMayChange && (
        <p className="rounded-xl border border-amber-500/25 bg-amber-500/10 px-4 py-3 text-sm font-semibold leading-6 text-amber-800 dark:text-amber-200" role="status">
          تغيير المصدر سيبقي كود الفيديو وروابطه وسجل المشاهدات، لكنه سيعيد تهيئة الترجمة والفصول والتحليل الذكي المرتبطين بالمصدر القديم.
        </p>
      )}
      <div className="flex flex-wrap items-end gap-4">
        <div className="w-full md:w-56">
          <VideoTypeSelect
            value={videoTypeId}
            onChange={setVideoTypeId}
            onAvailabilityChange={setVideoTypesAvailable}
            currentTypeId={editingVideo?.videoType.id}
          />
        </div>
        <div className="w-32">
          <NumberField value={order} onChange={setOrder} minValue={1}>
            <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">ترتيب العرض</NumberField.Label>
            <NumberField.Group className="h-[46px] w-full bg-[var(--admin-card)] hover:shadow-none">
              <NumberField.DecrementButton />
              <NumberField.Input className="bg-[var(--admin-card)]" />
              <NumberField.IncrementButton />
            </NumberField.Group>
          </NumberField>
        </div>
        <div className="w-40">
          <NumberField value={limit} onChange={setLimit} minValue={0}>
            <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">الحد الأقصى للمشاهدات (0 = غير محدود)</NumberField.Label>
            <NumberField.Group className="h-[46px] w-full bg-[var(--admin-card)] hover:shadow-none">
              <NumberField.DecrementButton />
              <NumberField.Input className="bg-[var(--admin-card)]" />
              <NumberField.IncrementButton />
            </NumberField.Group>
          </NumberField>
        </div>
        <div className="flex items-center gap-2 h-[46px] px-2">
          <input
            id={activeToggleId}
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
            className="h-4 w-4 rounded border-[var(--admin-border)] text-[var(--admin-primary)] focus:ring-[var(--admin-primary)] cursor-pointer"
          />
          <label htmlFor={activeToggleId} className="text-sm font-bold text-[var(--admin-text)] cursor-pointer select-none">
            {isBunny && bunnyMode !== 'manual'
              ? 'تفعيل الفيديو تلقائيًا بعد اكتمال تجهيز Bunny'
              : 'تفعيل الفيديو مباشرة للطلاب'}
          </label>
        </div>
        {onCancel && (
          <NeumorphButton
            type="button"
            onClick={onCancel}
            disabled={saving}
            intent="ghost"
            size="lg"
            pill
          >
            إلغاء
          </NeumorphButton>
        )}
        <NeumorphButton
          type="submit"
          disabled={saving || !canSubmit}
          loading={saving}
          intent="primary"
          size="lg"
          pill
          className="whitespace-nowrap ms-auto"
        >
          {isEditing ? 'حفظ التعديلات' : 'إضافة الفيديو'}
        </NeumorphButton>
      </div>
      <AdminConfirmationDialog
        open={sourceChangeConfirmationOpen}
        onClose={() => setSourceChangeConfirmationOpen(false)}
        onConfirm={async () => {
          if (await saveVideo()) setSourceChangeConfirmationOpen(false);
        }}
        title="استبدال مصدر الفيديو"
        consequence="سيبقى نفس كود الفيديو وروابطه وسجل المشاهدات، لكن ستُزال الترجمة والفصول والتحليل والخرائط الذهنية المرتبطة بالمصدر القديم حتى لا تظهر على الفيديو الجديد."
        confirmLabel="استبدال المصدر"
        isConfirming={saving}
      />
    </form>
  );
}
