'use client';

/**
 * QrDisplay — Renders printable QR codes for a given set of plaintext codes.
 *
 * Uses `qrcode.react` to generate SVG QR codes.
 * Provides a Print button that uses a CSS @media print layout.
 */

import { QRCodeSVG } from 'qrcode.react';
import { FileDown, Printer } from 'lucide-react';
import { useRef, useState } from 'react';
import { getSurfaceOrigins } from '@/packages/surface-runtime/config';
import type { PrintableTemplateDto } from '@/services/admin-sales-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';

type PrintableCodeItem = {
  code: string;
  serialNumber?: number;
};

type TemplateElement = {
  id: string;
  label?: string;
  x: number;
  y: number;
  size?: number;
  anchor?: 'center' | 'top-left';
};

function getElementDefaultSize(id: string) {
  if (id === 'qr') return 24;
  if (id === 'code') return 4;
  if (id === 'serial') return 3;
  return 3;
}

function getElementWidthPercent(element: TemplateElement, cardWidthMm: number) {
  const size = element.size ?? getElementDefaultSize(element.id);
  if (element.id === 'qr') return Math.min(100, (size / cardWidthMm) * 100);
  if (element.id === 'code') return Math.min(100, ((size * 10 * 0.72) / cardWidthMm) * 100);
  if (element.id === 'serial') return Math.min(100, ((size * 4 * 0.72) / cardWidthMm) * 100);
  return 0;
}

function getElementHeightPercent(element: TemplateElement, cardWidthMm: number, cardHeightMm: number) {
  if (element.id === 'qr') {
    return (getElementWidthPercent(element, cardWidthMm) * cardWidthMm) / cardHeightMm;
  }
  const size = element.size ?? getElementDefaultSize(element.id);
  return Math.min(100, (size / cardHeightMm) * 100);
}

function normalizeTemplateElement(element: TemplateElement, cardWidthMm: number, cardHeightMm: number): TemplateElement {
  const normalized = {
    ...element,
    size: element.size ?? getElementDefaultSize(element.id),
  };

  if (element.anchor === 'center') {
    return normalized;
  }

  return {
    ...normalized,
    x: Math.min(100, Math.max(0, normalized.x + getElementWidthPercent(normalized, cardWidthMm) / 2)),
    y: Math.min(100, Math.max(0, normalized.y + getElementHeightPercent(normalized, cardWidthMm, cardHeightMm) / 2)),
    anchor: 'center',
  };
}

function getPrintableMediaUrl(url: string) {
  if (!url || /^(data:|blob:)/i.test(url)) return url;
  return `/api/media-proxy?url=${encodeURIComponent(url)}`;
}

function readBlobAsDataUrl(blob: Blob) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ''));
    reader.onerror = () => reject(reader.error ?? new Error('Unable to read image data.'));
    reader.readAsDataURL(blob);
  });
}

function loadImage(src: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('Unable to load image.'));
    image.src = src;
  });
}

async function fetchImageAsPngDataUrl(url: string) {
  const response = await fetch(url, { cache: 'no-store' });
  if (!response.ok) throw new Error('Unable to fetch printable background image.');
  const sourceDataUrl = await readBlobAsDataUrl(await response.blob());
  const image = await loadImage(sourceDataUrl);
  const canvas = document.createElement('canvas');
  canvas.width = image.naturalWidth || image.width;
  canvas.height = image.naturalHeight || image.height;
  const context = canvas.getContext('2d');
  if (!context) throw new Error('Unable to prepare image canvas.');
  context.drawImage(image, 0, 0);
  return {
    dataUrl: canvas.toDataURL('image/png'),
    width: canvas.width,
    height: canvas.height,
  };
}

async function svgToPngDataUrl(svg: SVGSVGElement, sizePx = 768) {
  const serializedSvg = new XMLSerializer().serializeToString(svg);
  const svgBlob = new Blob([serializedSvg], { type: 'image/svg+xml;charset=utf-8' });
  const objectUrl = URL.createObjectURL(svgBlob);
  try {
    const image = await loadImage(objectUrl);
    const canvas = document.createElement('canvas');
    canvas.width = sizePx;
    canvas.height = sizePx;
    const context = canvas.getContext('2d');
    if (!context) throw new Error('Unable to prepare QR canvas.');
    context.fillStyle = '#ffffff';
    context.fillRect(0, 0, sizePx, sizePx);
    context.drawImage(image, 0, 0, sizePx, sizePx);
    return canvas.toDataURL('image/png');
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

function drawCoverImage(
  pdf: { addImage: (imageData: string, format: string, x: number, y: number, width: number, height: number) => void },
  image: { dataUrl: string; width: number; height: number },
  pageWidthMm: number,
  pageHeightMm: number,
) {
  const scale = Math.max(pageWidthMm / image.width, pageHeightMm / image.height);
  const drawWidth = image.width * scale;
  const drawHeight = image.height * scale;
  pdf.addImage(image.dataUrl, 'PNG', (pageWidthMm - drawWidth) / 2, (pageHeightMm - drawHeight) / 2, drawWidth, drawHeight);
}

interface QrDisplayProps {
  codes: Array<string | PrintableCodeItem>;
  groupName?: string;
  baseUrl?: string;
  template?: PrintableTemplateDto | null;
}

function parseTemplateElements(template?: PrintableTemplateDto | null): TemplateElement[] {
  const cardWidthMm = template?.widthMm || 85;
  const cardHeightMm = template?.heightMm || 55;

  if (!template?.layoutJson) {
    return [
      { id: 'qr', x: 26.1, y: 33.8, size: 24, anchor: 'center' },
      { id: 'code', x: 50, y: 18, size: 4, anchor: 'center' },
      { id: 'serial', x: 50, y: 38, size: 3, anchor: 'center' },
    ];
  }

  try {
    const parsed = JSON.parse(template.layoutJson) as { elements?: TemplateElement[] };
    if (Array.isArray(parsed.elements) && parsed.elements.length > 0) {
      return parsed.elements.map((element) => normalizeTemplateElement(element, cardWidthMm, cardHeightMm));
    }
  } catch {
    // Fall back to the default layout.
  }

  return [
    { id: 'qr', x: 26.1, y: 33.8, size: 24, anchor: 'center' },
    { id: 'code', x: 50, y: 18, size: 4, anchor: 'center' },
    { id: 'serial', x: 50, y: 38, size: 3, anchor: 'center' },
  ];
}

export function QrDisplay({ codes, baseUrl, template }: QrDisplayProps) {
  const printRef = useRef<HTMLDivElement>(null);
  const [downloadingPdf, setDownloadingPdf] = useState(false);
  const [pdfError, setPdfError] = useState('');
  const origins = getSurfaceOrigins();
  const effectiveBaseUrl = baseUrl || origins.student;
  const isLocalhost = effectiveBaseUrl.includes('localhost') || effectiveBaseUrl.includes('0.0.0.0') || effectiveBaseUrl.includes('127.0.0.1');
  const printableCodes = codes.map((entry) => (typeof entry === 'string' ? { code: entry } : entry));
  const templateElements = parseTemplateElements(template);
  const cardWidthMm = template?.widthMm || 85;
  const cardHeightMm = template?.heightMm || 55;
  const backgroundImageUrl = getPrintableMediaUrl(resolveMediaUrl(template?.backgroundImageUrl));
  const backgroundColor = template?.backgroundColor || '#ffffff';

  const handlePrint = () => {
    if (!printRef.current) return;

    const printFrame = document.createElement('iframe');
    printFrame.style.position = 'fixed';
    printFrame.style.right = '0';
    printFrame.style.bottom = '0';
    printFrame.style.width = '0';
    printFrame.style.height = '0';
    printFrame.style.border = '0';
    document.body.appendChild(printFrame);

    const printDocument = printFrame.contentDocument || printFrame.contentWindow?.document;
    if (!printDocument) {
      printFrame.remove();
      return;
    }

    printDocument.open();
    printDocument.write(`
      <!doctype html>
      <html>
        <head>
          <meta charset="utf-8" />
          <title>${template?.name || 'Printable codes'}</title>
          <style>
            @page { size: ${cardWidthMm}mm ${cardHeightMm}mm; margin: 0; }
            html, body {
              width: ${cardWidthMm}mm;
              min-height: ${cardHeightMm}mm;
              margin: 0;
              padding: 0;
              background: #fff;
            }
            * { box-sizing: border-box; }
            .qr-print-root {
              width: ${cardWidthMm}mm;
              margin: 0;
              padding: 0;
            }
            .qr-print-card {
              container-type: inline-size;
              position: relative;
              overflow: hidden;
              width: ${cardWidthMm}mm !important;
              height: ${cardHeightMm}mm !important;
              margin: 0;
              break-after: page;
              page-break-after: always;
              background: ${backgroundColor};
              -webkit-print-color-adjust: exact;
              print-color-adjust: exact;
            }
            .qr-print-card:last-child {
              break-after: auto;
              page-break-after: auto;
            }
            .qr-template-background {
              position: absolute;
              inset: 0;
              width: 100%;
              height: 100%;
              object-fit: cover;
            }
            .qr-template-element {
              position: absolute;
              z-index: 1;
              transform: translate(-50%, -50%);
            }
            .qr-box {
              width: 100%;
              aspect-ratio: 1 / 1;
              background: #fff;
            }
            .qr-box svg {
              display: block;
              width: 100%;
              height: 100%;
            }
            .qr-code-text,
            .qr-serial-text {
              font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace;
              font-weight: 900;
              color: #020617;
              line-height: 1;
              white-space: nowrap;
              letter-spacing: .12em;
            }
          </style>
        </head>
        <body>${printRef.current.innerHTML}</body>
      </html>
    `);
    printDocument.close();

    const printWindow = printFrame.contentWindow;
    const images = Array.from(printDocument.images);
    const waitForImages = Promise.all(images.map((image) => {
      if (image.complete) return Promise.resolve();
      return new Promise<void>((resolve) => {
        image.onload = () => resolve();
        image.onerror = () => resolve();
      });
    }));

    void waitForImages.then(() => {
      printWindow?.focus();
      printWindow?.print();
      window.setTimeout(() => printFrame.remove(), 1000);
    });
  };

  const handleDownloadPdf = async () => {
    const root = printRef.current;
    if (!root || downloadingPdf) return;

    setDownloadingPdf(true);
    setPdfError('');
    try {
      const { jsPDF } = await import('jspdf');

      const pdf = new jsPDF({
        orientation: cardWidthMm > cardHeightMm ? 'landscape' : 'portrait',
        unit: 'mm',
        format: [cardWidthMm, cardHeightMm],
        compress: true,
      });
      const backgroundImage = backgroundImageUrl ? await fetchImageAsPngDataUrl(backgroundImageUrl) : null;

      const cards = Array.from(root.querySelectorAll<HTMLElement>('.qr-print-card'));
      for (let index = 0; index < cards.length; index += 1) {
        const card = cards[index];
        if (index > 0) {
          pdf.addPage([cardWidthMm, cardHeightMm], cardWidthMm > cardHeightMm ? 'landscape' : 'portrait');
        }

        pdf.setFillColor(backgroundColor);
        pdf.rect(0, 0, cardWidthMm, cardHeightMm, 'F');
        if (backgroundImage) {
          drawCoverImage(pdf, backgroundImage, cardWidthMm, cardHeightMm);
        }

        const item = printableCodes[index];
        for (const element of templateElements) {
          const xMm = (element.x / 100) * cardWidthMm;
          const yMm = (element.y / 100) * cardHeightMm;
          const sizeMm = element.size ?? getElementDefaultSize(element.id);

          if (element.id === 'qr') {
            const qrSvg = card.querySelector<SVGSVGElement>('.qr-box svg');
            if (!qrSvg) continue;
            const qrDataUrl = await svgToPngDataUrl(qrSvg);
            pdf.addImage(qrDataUrl, 'PNG', xMm - sizeMm / 2, yMm - sizeMm / 2, sizeMm, sizeMm);
            continue;
          }

          const text = element.id === 'code' ? item.code : element.id === 'serial' ? String(item.serialNumber ?? '') : '';
          if (!text) continue;
          pdf.setTextColor(2, 6, 23);
          pdf.setFont('courier', 'bold');
          pdf.setFontSize(sizeMm * 2.835);
          pdf.text(text, xMm, yMm, { align: 'center', baseline: 'middle' });
        }
      }

      const safeName = (template?.name || 'codes-template').replace(/[^\u0600-\u06FF\w.-]+/g, '-');
      pdf.save(`${safeName}.pdf`);
    } catch (error) {
      console.error('Failed to export printable code PDF', error);
      setPdfError('فشل تحميل PDF. جرّب تحديث الصفحة ثم اضغط تحميل PDF مرة أخرى.');
    } finally {
      setDownloadingPdf(false);
    }
  };

  if (!codes || codes.length === 0) return null;

  return (
    <div className="space-y-4">
      {/* ── Toolbar ── */}
      <div className="flex items-center justify-between print:hidden bg-[var(--admin-card-soft)] p-4 rounded-xl border border-[var(--admin-border)]">
        <div>
          <h3 className="text-2xl font-bold text-[var(--admin-text)]">طباعة رموز QR</h3>
          <p className="text-sm text-[var(--admin-muted)]">
            عدد الأكواد للطباعة: {codes.length}
            {template ? ` - القالب: ${template.name}` : ''}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            onClick={handlePrint}
            className="flex items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-4 py-2 font-bold text-white shadow-lg transition-opacity hover:opacity-90"
          >
            <Printer size={18} />
            <span>اطبع الكروت</span>
          </button>
          <button
            onClick={handleDownloadPdf}
            disabled={downloadingPdf}
            className="flex items-center gap-2 rounded-lg border border-[var(--admin-border)] bg-white px-4 py-2 font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)] disabled:opacity-60"
          >
            <FileDown size={18} />
            <span>{downloadingPdf ? 'جاري التحميل...' : 'تحميل PDF'}</span>
          </button>
        </div>
      </div>

      {isLocalhost && (
        <div className="p-4 bg-yellow-500/10 border border-yellow-500/30 text-yellow-500 rounded-xl text-sm font-bold flex flex-col gap-1 print:hidden">
          <span className="text-base flex items-center gap-1.5 font-bold">⚠️ تنبيه: عنوان الرابط الحالي محلي ({effectiveBaseUrl})</span>
          <span className="font-normal text-xs opacity-90 leading-relaxed">
            لن يتمكن الطلاب من مسح رمز QR بنجاح عبر هواتفهم لأنه يشير إلى خادم محلي. يرجى التأكد من ضبط متغير البيئة <code className="px-1.5 py-0.5 bg-yellow-500/20 rounded font-mono">NEXT_PUBLIC_APP_URL</code> بالرابط الفعلي للمنصة.
          </span>
        </div>
      )}

      {pdfError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-bold text-red-700 print:hidden">
          {pdfError}
        </div>
      )}

      {/* ── Printable Area ── */}
      {/* 
        In screen mode, displays a scrollable grid. 
        In print mode, forces an A4 grid without scrollbars.
      */}
      <div className="max-h-[600px] overflow-y-auto print:max-h-none print:overflow-visible">
        <div
          ref={printRef}
          className="qr-print-root grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 print:block"
        >
          {printableCodes.map((item) => {
            const qrUrl = `${effectiveBaseUrl}/api/qr/${item.code}`;
            return (
              <div
                key={item.code}
                className="qr-print-card relative overflow-hidden rounded-xl border-2 border-dashed border-gray-300 bg-white shadow-sm print:rounded-none print:border-0 print:shadow-none"
                style={{
                  containerType: 'inline-size',
                  width: `${cardWidthMm}mm`,
                  height: `${cardHeightMm}mm`,
                  maxWidth: '100%',
                  backgroundColor,
                }}
              >
                {backgroundImageUrl && (
                  // Use a real image element so Chrome includes the template in PDF even when "Background graphics" is off.
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={backgroundImageUrl}
                    alt=""
                    crossOrigin="anonymous"
                    className="qr-template-background absolute inset-0 h-full w-full object-cover"
                    draggable={false}
                  />
                )}
                {templateElements.map((element) => {
                  const size = element.size ?? getElementDefaultSize(element.id);
                  const qrSizePercent = Math.min(100, (size / cardWidthMm) * 100);
                  const textSizeCqw = (size / cardWidthMm) * 100;
                  return (
                    <div
                      key={`${item.code}-${element.id}`}
                      className="qr-template-element absolute z-10"
                      style={{
                        left: `${element.x}%`,
                        top: `${element.y}%`,
                        transform: 'translate(-50%, -50%)',
                        width: element.id === 'qr' ? `${qrSizePercent}%` : undefined,
                        fontSize: element.id !== 'qr' ? `${textSizeCqw}cqw` : undefined,
                      }}
                    >
                      {element.id === 'qr' ? (
                        <div className="qr-box aspect-square bg-white">
                          <QRCodeSVG
                            value={qrUrl}
                            size={256}
                            level="M"
                            includeMargin={false}
                            className="h-full w-full"
                          />
                        </div>
                      ) : element.id === 'code' ? (
                        <div className="qr-code-text font-mono font-black tracking-widest text-gray-950">
                          {item.code}
                        </div>
                      ) : element.id === 'serial' ? (
                        <div className="qr-serial-text font-mono font-black tracking-wide text-gray-950">
                          {item.serialNumber ?? ''}
                        </div>
                      ) : null}
                    </div>
                  );
                })}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
