import assert from 'node:assert/strict';
import test from 'node:test';

import {
  configureWatermarkHtml,
  normalizeWatermarkSettings,
  watermarkLines,
} from './video-watermark.ts';

test('invalid watermark settings fall back to safe bounded values', () => {
  const settings = normalizeWatermarkSettings({
    WatermarkOpacity: '99',
    WatermarkFontSize: '-4',
    WatermarkBrandColor: 'red;position:fixed',
    WatermarkPosition: 'outside',
  });

  assert.equal(settings.WatermarkOpacity, '1');
  assert.equal(settings.WatermarkFontSize, '10');
  assert.equal(settings.WatermarkBrandColor, '#ffffff');
  assert.equal(settings.WatermarkPosition, 'top-left');
});

test('admin-selected identity lines and colors are preserved', () => {
  const settings = normalizeWatermarkSettings({
    WatermarkShowBrand: 'false',
    WatermarkShowStudentId: 'true',
    WatermarkStudentIdColor: '#12abef',
  });
  const lines = watermarkLines(settings, {
    name: 'أحمد', phone: '01000000000', studentId: 'STU-42',
  });

  assert.deepEqual(lines.map(line => line.text), ['أحمد', '01000000000', 'STU-42']);
  assert.equal(lines.at(-1)?.color, '#12abef');
});

test('custom watermark text cannot break out of the generated embed script', () => {
  const html = configureWatermarkHtml('<html><head></head><body></body></html>', {
    WatermarkShowCustom: 'true',
    WatermarkCustomText: '</script><script>globalThis.compromised=true</script>',
  }, { name: '', phone: '', studentId: '' });

  assert.equal(html.includes('</script><script>globalThis.compromised=true'), false);
  assert.match(html, /\\u003c\/script>/);
});
