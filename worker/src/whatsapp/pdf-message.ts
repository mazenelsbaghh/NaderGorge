const maximumPdfBytes = 90 * 1024 * 1024;

export function pdfMessage(body: { media?: unknown; mimetype?: unknown; fileName?: unknown; caption?: unknown }) {
  if (body.mimetype !== 'application/pdf' || typeof body.media !== 'string' ||
      body.media.length > Math.ceil(maximumPdfBytes / 3) * 4 ||
      !/^[A-Za-z0-9+/]*={0,2}$/.test(body.media)) return null;
  const document = Buffer.from(body.media, 'base64');
  if (document.length > maximumPdfBytes || !document.subarray(0, 5).equals(Buffer.from('%PDF-'))) return null;
  const name = typeof body.fileName === 'string' ? body.fileName.split(/[\\/]/).pop()! : 'document.pdf';
  const fileName = name.replace(/[\x00-\x1f\x7f]/g, '').slice(0, 180);
  return {
    document,
    mimetype: 'application/pdf',
    fileName: fileName.toLowerCase().endsWith('.pdf') ? fileName : `${fileName || 'document'}.pdf`,
    caption: typeof body.caption === 'string' ? body.caption.slice(0, 4000) : '',
  };
}
