import assert from 'node:assert/strict';
import { test } from 'node:test';
import { pdfMessage } from './pdf-message.js';

test('QR WhatsApp PDF payload retains bytes, safe file name, and caption', () => {
  const bytes = Buffer.from('%PDF-1.7\ncontent');
  const message = pdfMessage({ media: bytes.toString('base64'), mimetype: 'application/pdf', fileName: '../lesson.pdf', caption: 'lesson' });
  assert.ok(message);
  assert.deepEqual(message.document, bytes);
  assert.equal(message.fileName, 'lesson.pdf');
  assert.equal(message.caption, 'lesson');
});

test('QR WhatsApp rejects fake PDFs and unsupported document types', () => {
  assert.equal(pdfMessage({ media: Buffer.from('<html>fake</html>').toString('base64'), mimetype: 'application/pdf' }), null);
  assert.equal(pdfMessage({ media: Buffer.from('%PDF-1.7').toString('base64'), mimetype: 'application/msword' }), null);
  assert.equal(pdfMessage({ media: '***', mimetype: 'application/pdf' }), null);
});
