import { resolveMediaUrl } from '@/utils/resolve-media-url';

export interface DownloadableMindmap {
  imageUrl: string;
  fileName: string;
}

const MIME_EXTENSIONS: Record<string, string> = {
  'image/jpeg': 'jpg',
  'image/png': 'png',
  'image/webp': 'webp',
};

export function safeFileName(name: string): string {
  return (
    name
      .trim()
      .replace(/[\\/:*?"<>|]+/g, '-')
      .replace(/\s+/g, '_') || 'mindmap'
  );
}

export function saveBlob(blob: Blob, fileName: string): void {
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(objectUrl);
}

export async function fetchMindmap(imageUrl: string): Promise<Blob> {
  const resolvedUrl = resolveMediaUrl(imageUrl);
  const downloadUrl = /^(data:|blob:)/i.test(resolvedUrl)
    ? resolvedUrl
    : `/api/media-proxy?url=${encodeURIComponent(resolvedUrl)}`;
  const response = await fetch(downloadUrl);
  if (!response.ok) {
    throw new Error(`Mindmap download failed with status ${response.status}`);
  }
  return response.blob();
}

function imageExtension(blob: Blob, imageUrl: string): string {
  const mimeExtension = MIME_EXTENSIONS[blob.type.toLowerCase()];
  if (mimeExtension) return mimeExtension;

  const urlExtension = imageUrl
    .split('.')
    .pop()
    ?.split(/[?#]/)[0]
    ?.toLowerCase();
  return urlExtension && /^[a-z0-9]{2,5}$/.test(urlExtension)
    ? urlExtension
    : 'jpg';
}

export async function downloadMindmap(
  imageUrl: string,
  fileName: string
): Promise<void> {
  const blob = await fetchMindmap(imageUrl);
  saveBlob(blob, `${safeFileName(fileName)}.${imageExtension(blob, imageUrl)}`);
}

async function mindmapFiles(mindmaps: DownloadableMindmap[]) {
  return Promise.all(
    mindmaps.map(async (mindmap) => {
      const blob = await fetchMindmap(mindmap.imageUrl);
      const extension = imageExtension(blob, mindmap.imageUrl);
      return {
        blob,
        fileName: `${safeFileName(mindmap.fileName)}.${extension}`,
      };
    })
  );
}

export async function downloadMindmapsZip(
  folderName: string,
  mindmaps: DownloadableMindmap[]
): Promise<void> {
  const [{ default: JSZip }, files] = await Promise.all([
    import('jszip'),
    mindmapFiles(mindmaps),
  ]);
  const zip = new JSZip();
  const folder = zip.folder(safeFileName(folderName));
  files.forEach((file) => folder?.file(file.fileName, file.blob));
  const archive = await zip.generateAsync({ type: 'blob' });
  saveBlob(archive, `${safeFileName(folderName)}_الخرائط_الذهنية.zip`);
}

async function imagePage(
  blob: Blob
): Promise<{ image: string; width: number; height: number }> {
  const bitmap = await createImageBitmap(blob);
  const canvas = document.createElement('canvas');
  canvas.width = bitmap.width;
  canvas.height = bitmap.height;
  canvas.getContext('2d')?.drawImage(bitmap, 0, 0);
  bitmap.close();
  return {
    image: canvas.toDataURL('image/jpeg', 0.92),
    width: canvas.width,
    height: canvas.height,
  };
}

export async function downloadMindmapsPdf(
  fileName: string,
  mindmaps: DownloadableMindmap[]
): Promise<void> {
  const [{ jsPDF }, files] = await Promise.all([
    import('jspdf'),
    mindmapFiles(mindmaps),
  ]);
  const pages = await Promise.all(files.map((file) => imagePage(file.blob)));
  const pdf = new jsPDF({
    orientation: 'landscape',
    unit: 'mm',
    format: 'a4',
    compress: true,
  });

  pages.forEach((page, index) => {
    if (index > 0) pdf.addPage('a4', 'landscape');
    const pageWidth = pdf.internal.pageSize.getWidth();
    const pageHeight = pdf.internal.pageSize.getHeight();
    const scale = Math.min(pageWidth / page.width, pageHeight / page.height);
    const width = page.width * scale;
    const height = page.height * scale;
    pdf.addImage(
      page.image,
      'JPEG',
      (pageWidth - width) / 2,
      (pageHeight - height) / 2,
      width,
      height
    );
  });

  pdf.save(`${safeFileName(fileName)}_الخرائط_الذهنية.pdf`);
}
