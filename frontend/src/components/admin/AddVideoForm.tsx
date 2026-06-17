'use client';

import { useState } from 'react';
import { adminService, type VideoProvider } from '@/services/admin-service';
import toast from 'react-hot-toast';
import { NumberField } from '@/components/ui/number-field';
import NeumorphButton from '@/components/ui/neumorph-button';
import { Dropdown } from '@/components/ui/dropdown';
import * as tus from 'tus-js-client';

interface AddVideoFormProps {
  lessonId: string;
  onSuccess?: () => void;
}

export function AddVideoForm({ lessonId, onSuccess }: AddVideoFormProps) {
  const [title, setTitle] = useState('');
  const [provider, setProvider] = useState<VideoProvider>('YouTube');
  const [urlOrEmbedCode, setUrlOrEmbedCode] = useState('');
  const [order, setOrder] = useState(1);
  const [limit, setLimit] = useState(3);
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [bunnyMode, setBunnyMode] = useState<'manual' | 'file' | 'url'>('manual');
  const [bunnyFile, setBunnyFile] = useState<File | null>(null);
  const [bunnySourceUrl, setBunnySourceUrl] = useState('');
  const [uploadProgress, setUploadProgress] = useState(0);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim()) return;
    if (provider === 'bunny' && bunnyMode === 'file' && !bunnyFile) return;
    if (provider === 'bunny' && bunnyMode === 'url' && !bunnySourceUrl.trim()) return;
    if (!(provider === 'bunny' && bunnyMode !== 'manual') && !urlOrEmbedCode.trim()) return;

    try {
      setSaving(true);
      if (provider === 'bunny' && bunnyMode === 'file' && bunnyFile) {
        setUploadProgress(0);
        const session = await adminService.createBunnyTusUpload({
          lessonId,
          title,
          order,
          maxWatchCount: limit,
          fileName: bunnyFile.name,
          fileSizeBytes: bunnyFile.size,
        });

        if (!session) throw new Error('Missing Bunny upload session');

        await new Promise<void>((resolve, reject) => {
          const upload = new tus.Upload(bunnyFile, {
            endpoint: session.tusEndpoint,
            headers: session.uploadHeaders,
            metadata: {
              filename: bunnyFile.name,
              filetype: bunnyFile.type || 'video/mp4',
            },
            onError: reject,
            onProgress: (uploaded, total) => setUploadProgress(total > 0 ? Math.round((uploaded / total) * 100) : 0),
            onSuccess: async () => {
              await adminService.completeBunnyUpload(session.bunnyVideoAssetId);
              resolve();
            },
          });
          upload.start();
        });
      } else if (provider === 'bunny' && bunnyMode === 'url') {
        await adminService.fetchBunnyVideo({
          lessonId,
          title,
          order,
          maxWatchCount: limit,
          sourceUrl: bunnySourceUrl,
        });
      } else {
        await adminService.createVideo({ lessonId, title, provider, urlOrEmbedCode, order, limit, isActive });
      }
      toast.success('تمت إضافة الفيديو بنجاح.');
      setTitle('');
      setUrlOrEmbedCode('');
      setBunnyFile(null);
      setBunnySourceUrl('');
      setUploadProgress(0);
      setOrder((prev) => prev + 1);
      onSuccess?.();
    } catch {
      toast.error('حدث خطأ أثناء إضافة الفيديو، أعد المحاولة.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form id="add-video-form" onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-4">
        <div className="flex-1 space-y-2 min-w-[200px]">
          <label className="text-xs font-bold text-[var(--admin-muted)]">عنوان الفيديو</label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="مثال: الدرس الأول - مراجعة"
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
            required
          />
        </div>
        <div className="w-40 space-y-2">
          <Dropdown
            label="المنصة"
            value={provider}
            onChange={(v) => setProvider(v as VideoProvider)}
            size="sm"
            options={[
              { value: 'YouTube', label: 'YouTube' },
              { value: 'vk', label: 'VK (فيكونتاكتي)' },
              { value: 'bunny', label: 'Bunny.net' },
            ]}
          />
        </div>
        <div className="flex-1 space-y-2 min-w-[200px]">
          <label className="text-xs font-bold text-[var(--admin-muted)]">رابط الفيديو (أو المعرف)</label>
          <input
            type="text"
            value={urlOrEmbedCode}
            onChange={(e) => {
              const val = e.target.value;
              if (val.includes('vk.com/video') || val.includes('vk.com/video_ext')) {
                setProvider('vk');
              } else if (val.includes('youtube.com') || val.includes('youtu.be')) {
                setProvider('YouTube');
              } else if (val.includes('mediadelivery.net')) {
                setProvider('bunny');
              }
              setUrlOrEmbedCode(val);
            }}
            placeholder={provider === 'vk' ? 'مثال: oid=-22822305&id=456241864' : provider === 'bunny' ? 'Bunny video GUID أو رابط player.mediadelivery.net' : 'رابط الفيديو'}
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
            required={!(provider === 'bunny' && bunnyMode !== 'manual')}
          />
        </div>
      </div>
      {provider === 'bunny' && (
        <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4">
          <div className="mb-3 flex flex-wrap gap-2">
            {[
              { value: 'manual', label: 'ربط بمعرف Bunny' },
              { value: 'file', label: 'رفع ملف' },
              { value: 'url', label: 'سحب من رابط' },
            ].map((option) => (
              <button
                key={option.value}
                type="button"
                onClick={() => setBunnyMode(option.value as typeof bunnyMode)}
                className={`rounded-full px-4 py-2 text-xs font-bold transition-colors ${bunnyMode === option.value ? 'bg-[var(--admin-primary)] text-white' : 'bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}
              >
                {option.label}
              </button>
            ))}
          </div>
          {bunnyMode === 'file' && (
            <div className="space-y-2">
              <input
                type="file"
                accept="video/*"
                onChange={(e) => setBunnyFile(e.target.files?.[0] ?? null)}
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)]"
              />
              {saving && uploadProgress > 0 && (
                <div className="h-2 overflow-hidden rounded-full bg-[var(--admin-border)]">
                  <div className="h-full bg-[var(--admin-primary)] transition-all" style={{ width: `${uploadProgress}%` }} />
                </div>
              )}
            </div>
          )}
          {bunnyMode === 'url' && (
            <input
              type="url"
              value={bunnySourceUrl}
              onChange={(e) => setBunnySourceUrl(e.target.value)}
              placeholder="رابط فيديو مباشر يمكن لـ Bunny الوصول إليه"
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)]"
            />
          )}
        </div>
      )}
      <div className="flex flex-wrap items-end gap-4">
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
          <NumberField value={limit} onChange={setLimit} minValue={1}>
            <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">الحد الأقصى للمشاهدات</NumberField.Label>
            <NumberField.Group className="h-[46px] w-full bg-[var(--admin-card)] hover:shadow-none">
              <NumberField.DecrementButton />
              <NumberField.Input className="bg-[var(--admin-card)]" />
              <NumberField.IncrementButton />
            </NumberField.Group>
          </NumberField>
        </div>
        <div className="flex items-center gap-2 h-[46px] px-2">
          <input
            id="new-video-is-active"
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
            className="h-4 w-4 rounded border-[var(--admin-border)] text-[var(--admin-primary)] focus:ring-[var(--admin-primary)] cursor-pointer"
          />
          <label htmlFor="new-video-is-active" className="text-sm font-bold text-[var(--admin-text)] cursor-pointer select-none">
            تفعيل الفيديو مباشرة للطلاب
          </label>
        </div>
        <NeumorphButton
          type="submit"
          disabled={saving || !title.trim() || (provider === 'bunny' && bunnyMode === 'file' ? !bunnyFile : provider === 'bunny' && bunnyMode === 'url' ? !bunnySourceUrl.trim() : !urlOrEmbedCode.trim())}
          loading={saving}
          intent="primary"
          size="lg"
          pill
          className="whitespace-nowrap ml-auto"
        >
          إضافة الفيديو
        </NeumorphButton>
      </div>
    </form>
  );
}
