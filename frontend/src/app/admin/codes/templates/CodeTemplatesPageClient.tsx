'use client';

import { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { ArrowRight, ImagePlus, Pencil, Save, X } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import { adminSalesService, type PrintableTemplateDto } from '@/services/admin-sales-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { invalidateMany } from '@/lib/cache-invalidation';

type TemplateElement = { id: string; label: string; x: number; y: number; size?: number; anchor?: 'center' | 'top-left' };

const DEFAULT_TEMPLATE_ELEMENTS: TemplateElement[] = [
  { id: 'qr', label: 'QR', x: 26.1, y: 33.8, size: 24, anchor: 'center' },
  { id: 'code', label: 'الكود', x: 50, y: 18, size: 4, anchor: 'center' },
  { id: 'serial', label: 'السيريال', x: 50, y: 38, size: 3, anchor: 'center' },
];

function getElementDefaultSize(id: string) {
  if (id === 'qr') return 24;
  if (id === 'code') return 4;
  if (id === 'serial') return 3;
  return 3;
}

function getElementWidthPercent(element: TemplateElement, widthMm: number) {
  const size = element.size ?? getElementDefaultSize(element.id);
  if (element.id === 'qr') return Math.min(100, (size / widthMm) * 100);
  if (element.id === 'code') return Math.min(100, ((size * 10 * 0.72) / widthMm) * 100);
  if (element.id === 'serial') return Math.min(100, ((size * 4 * 0.72) / widthMm) * 100);
  return 0;
}

function getElementHeightPercent(element: TemplateElement, widthMm: number, heightMm: number) {
  if (element.id === 'qr') return (getElementWidthPercent(element, widthMm) * widthMm) / heightMm;
  const size = element.size ?? getElementDefaultSize(element.id);
  return Math.min(100, (size / heightMm) * 100);
}

function normalizeTemplateElements(elements: TemplateElement[], widthMm: number, heightMm: number) {
  return elements.map((element) => {
    const normalized = {
      ...element,
      size: element.size ?? getElementDefaultSize(element.id),
    };

    if (element.anchor === 'center') {
      return normalized;
    }

    return {
      ...normalized,
      x: Math.min(100, Math.max(0, normalized.x + getElementWidthPercent(normalized, widthMm) / 2)),
      y: Math.min(100, Math.max(0, normalized.y + getElementHeightPercent(normalized, widthMm, heightMm) / 2)),
      anchor: 'center' as const,
    };
  });
}

export default function CodeTemplatesPageClient() {
  const [loading, setLoading] = useState(false);
  const [uploadingBackground, setUploadingBackground] = useState(false);
  const [message, setMessage] = useState('');
  const [templates, setTemplates] = useState<PrintableTemplateDto[]>([]);
  const [form, setForm] = useState({
    id: '',
    name: 'قالب QR بسيط',
    widthMm: '85',
    heightMm: '55',
    backgroundColor: '#ffffff',
    backgroundImageUrl: '',
    isActive: true,
    elements: DEFAULT_TEMPLATE_ELEMENTS,
  });

  async function load() {
    setLoading(true);
    setMessage('');
    try {
      setTemplates(await adminSalesService.templates());
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'تعذر تحميل القوالب.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function saveTemplate() {
    setLoading(true);
    setMessage('');
    try {
      const savedTemplate = await adminSalesService.saveTemplate({
        id: form.id || null,
        name: form.name,
        widthMm: Number(form.widthMm),
        heightMm: Number(form.heightMm),
        backgroundColor: form.backgroundColor || null,
        backgroundImageUrl: form.backgroundImageUrl || null,
        layoutJson: JSON.stringify({ elements: form.elements }),
        isActive: form.isActive,
      });
      invalidateMany(['codes:groups', 'reports']);
      setForm((current) => ({ ...current, id: savedTemplate.id }));
      setMessage('تم حفظ القالب.');
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'فشل حفظ القالب.');
    } finally {
      setLoading(false);
    }
  }

  function editTemplate(template: PrintableTemplateDto) {
    let elements = form.elements;
    try {
      const parsed = JSON.parse(template.layoutJson || '{}') as { elements?: TemplateElement[] };
      if (Array.isArray(parsed.elements) && parsed.elements.length > 0) {
        elements = normalizeTemplateElements(parsed.elements, template.widthMm, template.heightMm);
      }
    } catch {
      elements = form.elements;
    }

    setForm({
      id: template.id,
      name: template.name,
      widthMm: String(template.widthMm),
      heightMm: String(template.heightMm),
      backgroundColor: template.backgroundColor || '#ffffff',
      backgroundImageUrl: template.backgroundImageUrl || '',
      isActive: template.isActive,
      elements,
    });
    setMessage(`جاري تعديل قالب: ${template.name}`);
  }

  function resetTemplateForm() {
    setForm({
      id: '',
      name: 'قالب QR بسيط',
      widthMm: '85',
      heightMm: '55',
      backgroundColor: '#ffffff',
      backgroundImageUrl: '',
      isActive: true,
      elements: DEFAULT_TEMPLATE_ELEMENTS,
    });
    setMessage('');
  }

  async function uploadBackgroundImage(file: File | null) {
    if (!file) return;
    setUploadingBackground(true);
    setMessage('');
    try {
      const imageUrl = await adminSalesService.uploadTemplateBackground(file);
      if (!imageUrl) throw new Error('الخادم لم يرجع رابط الصورة.');
      setForm((current) => ({ ...current, backgroundImageUrl: imageUrl }));
      setMessage('تم رفع صورة الخلفية.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'فشل رفع صورة الخلفية.');
    } finally {
      setUploadingBackground(false);
    }
  }

  return (
    <AdminPage
      activePath="/admin/codes"
      sectionLabel="الأكواد ▸ القوالب"
      pageTitle="قوالب طباعة الأكواد"
      subtitle="تصميم مكان QR والكود والسيريال داخل بطاقة الطباعة."
      action={
        <Link href="/admin/codes" className="inline-flex items-center gap-2 rounded-md border border-[var(--admin-border)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]">
          <ArrowRight className="h-4 w-4" />
          رجوع للأكواد
        </Link>
      }
    >
      <div className="space-y-5">
        {message && <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-bold text-amber-900">{message}</div>}

        <section className="grid gap-4 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 lg:grid-cols-[320px_minmax(0,1fr)]">
          <div className="grid content-start gap-3">
            <Field label="اسم القالب" value={form.name} onChange={(v) => setForm({ ...form, name: v })} />
            <Field label="العرض mm" type="number" value={form.widthMm} onChange={(v) => setForm({ ...form, widthMm: v })} />
            <Field label="الارتفاع mm" type="number" value={form.heightMm} onChange={(v) => setForm({ ...form, heightMm: v })} />
            <Field label="لون الخلفية" value={form.backgroundColor} onChange={(v) => setForm({ ...form, backgroundColor: v })} />
            <BackgroundImageUpload
              value={form.backgroundImageUrl}
              uploading={uploadingBackground}
              onUpload={uploadBackgroundImage}
              onClear={() => setForm({ ...form, backgroundImageUrl: '' })}
            />
            <ElementSizeControls
              elements={form.elements}
              onResize={(id, size) => setForm({
                ...form,
                elements: form.elements.map((element) => element.id === id ? { ...element, size } : element),
              })}
            />
            <label className="flex items-center gap-2 rounded-md border border-[var(--admin-border)] px-3 py-2 text-sm font-bold text-[var(--admin-text)]">
              <input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />
              مفعل
            </label>
            <button onClick={saveTemplate} disabled={loading} className="inline-flex items-center justify-center gap-2 rounded-md bg-[var(--admin-primary)] px-3 py-2 text-sm font-bold text-white hover:opacity-90 disabled:opacity-60">
              <Save className="h-4 w-4" />
              {form.id ? 'حفظ تعديل القالب' : 'حفظ القالب'}
            </button>
            {form.id && (
              <button type="button" onClick={resetTemplateForm} className="inline-flex items-center justify-center gap-2 rounded-md border border-[var(--admin-border)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]">
                قالب جديد
              </button>
            )}
          </div>

          <TemplateDesigner
            elements={form.elements}
            widthMm={Number(form.widthMm) || 85}
            heightMm={Number(form.heightMm) || 55}
            backgroundColor={form.backgroundColor}
            backgroundImageUrl={form.backgroundImageUrl}
            onMove={(id, x, y) => setForm({
              ...form,
              elements: form.elements.map((element) => element.id === id ? { ...element, x, y } : element),
            })}
          />
        </section>

        <section className="rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
          <h2 className="mb-3 text-lg font-black text-[var(--admin-text)]">القوالب المحفوظة</h2>
          {templates.length === 0 ? (
            <p className="text-sm font-bold text-[var(--admin-muted)]">لا توجد قوالب بعد.</p>
          ) : (
            <ul className="grid gap-2">
              {templates.map((template) => (
                <li key={template.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-2 text-sm font-bold text-[var(--admin-text)]">
                  <span>{template.name} - {template.widthMm}x{template.heightMm}mm - {template.isActive ? 'مفعل' : 'متوقف'}</span>
                  <button
                    type="button"
                    onClick={() => editTemplate(template)}
                    className="inline-flex h-9 items-center gap-2 rounded-md border border-[var(--admin-border)] px-3 text-xs font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"
                  >
                    <Pencil className="h-4 w-4" />
                    تعديل القالب
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </AdminPage>
  );
}

function BackgroundImageUpload({
  value,
  uploading,
  onUpload,
  onClear,
}: {
  value: string;
  uploading: boolean;
  onUpload: (file: File | null) => void;
  onClear: () => void;
}) {
  const inputRef = useRef<HTMLInputElement | null>(null);

  return (
    <div className="grid gap-2 text-sm">
      <span className="font-bold text-[var(--admin-muted)]">صورة الخلفية</span>
      <input
        ref={inputRef}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        className="hidden"
        onChange={(event) => {
          onUpload(event.target.files?.[0] ?? null);
          event.currentTarget.value = '';
        }}
      />
      <div className="grid gap-2 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2">
        {value ? (
          <div
            className="h-24 rounded-md border border-[var(--admin-border)] bg-white"
            style={{
              backgroundImage: `url(${resolveMediaUrl(value)})`,
              backgroundSize: 'cover',
              backgroundPosition: 'center',
            }}
          />
        ) : (
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={uploading}
            className="flex h-24 items-center justify-center gap-2 rounded-md border border-dashed border-[var(--admin-border)] text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-hover)] disabled:opacity-60"
          >
            <ImagePlus className="h-4 w-4" />
            {uploading ? 'جاري الرفع...' : 'اختيار صورة من الجهاز'}
          </button>
        )}

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={uploading}
            className="inline-flex h-9 items-center gap-2 rounded-md border border-[var(--admin-border)] px-3 text-xs font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-60"
          >
            <ImagePlus className="h-4 w-4" />
            {value ? 'تغيير الصورة' : 'رفع صورة'}
          </button>
          {value && (
            <button
              type="button"
              onClick={onClear}
              disabled={uploading}
              className="inline-flex h-9 items-center gap-2 rounded-md border border-red-200 px-3 text-xs font-bold text-red-700 hover:bg-red-50 disabled:opacity-60"
            >
              <X className="h-4 w-4" />
              مسح الصورة
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

function ElementSizeControls({
  elements,
  onResize,
}: {
  elements: TemplateElement[];
  onResize: (id: string, size: number) => void;
}) {
  return (
    <div className="grid gap-3 rounded-md border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 text-sm">
      <span className="font-black text-[var(--admin-text)]">أحجام عناصر القالب</span>
      {elements.map((element) => {
        const value = element.size ?? getElementDefaultSize(element.id);
        const isQr = element.id === 'qr';
        return (
          <label key={element.id} className="grid gap-1.5">
            <span className="flex items-center justify-between gap-3 font-bold text-[var(--admin-muted)]">
              <span>{element.label}</span>
              <span className="font-mono text-xs text-[var(--admin-text)]">{value.toFixed(1)}%</span>
            </span>
            <input
              type="range"
              min={isQr ? 10 : 1}
              max={isQr ? 50 : 12}
              step={isQr ? 1 : 0.1}
              value={value}
              onChange={(event) => onResize(element.id, Number(event.target.value))}
              className="w-full accent-[var(--admin-primary)]"
            />
          </label>
        );
      })}
    </div>
  );
}

function TemplateDesigner({
  elements,
  widthMm,
  heightMm,
  backgroundColor,
  backgroundImageUrl,
  onMove,
}: {
  elements: TemplateElement[];
  widthMm: number;
  heightMm: number;
  backgroundColor: string;
  backgroundImageUrl: string;
  onMove: (id: string, x: number, y: number) => void;
}) {
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const dragRef = useRef<{ id: string; offsetFromCenterX: number; offsetFromCenterY: number; pointerId: number } | null>(null);
  const aspectRatio = widthMm > 0 && heightMm > 0 ? `${widthMm} / ${heightMm}` : '85 / 55';
  const resolvedBackgroundImageUrl = resolveMediaUrl(backgroundImageUrl);
  const safeWidthMm = widthMm > 0 ? widthMm : 85;

  const moveElement = (id: string, clientX: number, clientY: number, offsetFromCenterX: number, offsetFromCenterY: number) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const element = canvas.querySelector<HTMLElement>(`[data-template-element="${id}"]`);
    const elementWidth = element?.offsetWidth ?? 0;
    const elementHeight = element?.offsetHeight ?? 0;
    const halfWidth = elementWidth / 2;
    const halfHeight = elementHeight / 2;
    const centerX = Math.max(halfWidth, Math.min(rect.width - halfWidth, clientX - rect.left - offsetFromCenterX));
    const centerY = Math.max(halfHeight, Math.min(rect.height - halfHeight, clientY - rect.top - offsetFromCenterY));
    const x = rect.width > 0 ? (centerX / rect.width) * 100 : 0;
    const y = rect.height > 0 ? (centerY / rect.height) * 100 : 0;
    onMove(id, Math.round(x * 10) / 10, Math.round(y * 10) / 10);
  };

  return (
    <div
      ref={canvasRef}
      className="relative w-full max-w-3xl touch-none overflow-hidden rounded-lg border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)]"
      style={{
        containerType: 'inline-size',
        aspectRatio,
        backgroundColor: backgroundColor || '#ffffff',
        backgroundImage: resolvedBackgroundImageUrl ? `url(${resolvedBackgroundImageUrl})` : undefined,
        backgroundSize: 'cover',
        backgroundPosition: 'center',
      }}
      onPointerMove={(event) => {
        const drag = dragRef.current;
        if (!drag || drag.pointerId !== event.pointerId) return;
        event.preventDefault();
        moveElement(drag.id, event.clientX, event.clientY, drag.offsetFromCenterX, drag.offsetFromCenterY);
      }}
      onPointerUp={(event) => {
        if (dragRef.current?.pointerId === event.pointerId) {
          event.currentTarget.releasePointerCapture(event.pointerId);
          dragRef.current = null;
        }
      }}
      onPointerCancel={(event) => {
        if (dragRef.current?.pointerId === event.pointerId) {
          event.currentTarget.releasePointerCapture(event.pointerId);
          dragRef.current = null;
        }
      }}
    >
      {elements.map((element) => (
        (() => {
          const size = element.size ?? getElementDefaultSize(element.id);
          const qrSizePercent = Math.min(100, (size / safeWidthMm) * 100);
          const textSizeCqw = (size / safeWidthMm) * 100;
          return (
        <button
          key={element.id}
          type="button"
          data-template-element={element.id}
          onPointerDown={(event) => {
            const rect = event.currentTarget.getBoundingClientRect();
            dragRef.current = {
              id: element.id,
              offsetFromCenterX: event.clientX - (rect.left + rect.width / 2),
              offsetFromCenterY: event.clientY - (rect.top + rect.height / 2),
              pointerId: event.pointerId,
            };
            canvasRef.current?.setPointerCapture(event.pointerId);
          }}
          className="absolute z-10 cursor-grab rounded-sm outline outline-1 outline-dashed outline-slate-400/80 transition outline-offset-2 hover:outline-slate-900 active:cursor-grabbing"
          style={{
            left: `${element.x}%`,
            top: `${element.y}%`,
            transform: 'translate(-50%, -50%)',
            width: element.id === 'qr' ? `${qrSizePercent}%` : undefined,
            fontSize: element.id !== 'qr' ? `${textSizeCqw}cqw` : undefined,
          }}
        >
          {element.id === 'qr' ? (
            <span className="grid aspect-square w-full place-items-center bg-white">
              <span className="grid h-[72%] w-[72%] place-items-center rounded-sm border-2 border-slate-900 font-mono text-sm font-black text-slate-900">QR</span>
            </span>
          ) : element.id === 'code' ? (
            <span className="font-mono font-black tracking-widest text-slate-950">1234567890</span>
          ) : element.id === 'serial' ? (
            <span className="font-mono font-black tracking-wide text-slate-950">0001</span>
          ) : (
            element.label
          )}
        </button>
          );
        })()
      ))}
    </div>
  );
}

function Field({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: string }) {
  return (
    <label className="grid gap-1 text-sm">
      <span className="font-bold text-[var(--admin-muted)]">{label}</span>
      <input type={type} value={value} onChange={(event) => onChange(event.target.value)} className="rounded-md border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)]" />
    </label>
  );
}
