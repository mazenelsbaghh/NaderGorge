import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';
import { youtubeQualityFullscreenGeometry, youtubeQualityPreviewScript } from './youtube-quality-preview.ts';

test('2026-10-03 native quality covers follow the parent shadow timer and ignore messages from other frames', () => {
  const classes = new Set<string>();
  const parent = {};
  let receiveMessage: (event: { origin: string; source: object; data: { type: string; visible: boolean } }) => void;
  const origin = 'https://student.test';
  // DOM and frame messaging are the browser boundary for the generated embed.
  vm.runInNewContext(youtubeQualityPreviewScript(), {
    window: { location: { origin }, parent, addEventListener(_type: string, listener: typeof receiveMessage) { receiveMessage = listener; } },
    document: {
      createElement() { return {}; },
      body: { appendChild() {}, classList: {
        toggle(name: string, enabled: boolean) { if (enabled) classes.add(name); else classes.delete(name); },
      } },
    },
  });
  const message = (visible: boolean) => ({ type: 'playerShadows', visible });
  receiveMessage!({ origin: 'https://other.test', source: parent, data: message(false) });
  receiveMessage!({ origin, source: {}, data: message(false) });
  assert.equal(classes.has('quality-shadows-hidden'), false);
  receiveMessage!({ origin, source: parent, data: message(false) });
  assert.equal(classes.has('quality-shadows-hidden'), true, 'the expired timer removes both native covers');
  receiveMessage!({ origin, source: parent, data: message(true) });
  assert.equal(classes.has('quality-shadows-hidden'), false, 'pause can restore the covers');
});

test('2026-10-03 center lesson fullscreen contains the complete widescreen picture without native chrome covering it', () => {
  for (const [width, height] of [[842, 388], [600, 220], [318, 700], [1920, 1080]]) {
    const frame = youtubeQualityFullscreenGeometry(width, height);
    const frameWidth = frame.canvasWidth * frame.scale;
    const frameHeight = frame.canvasHeight * frame.scale;
    const pictureHeight = frameWidth * 9 / 16;
    const pictureTop = frame.offsetTop + (frameHeight - pictureHeight) / 2;
    const epsilon = 0.001;
    assert.ok(frame.offsetLeft >= -epsilon);
    assert.ok(frame.offsetLeft + frameWidth <= width + epsilon);
    assert.ok(pictureTop >= -epsilon);
    assert.ok(pictureTop + pictureHeight <= height + epsilon);
    assert.ok(Math.abs(frameWidth - width) < epsilon || Math.abs(pictureHeight - height) < epsilon,
      'the complete picture must use the largest size that fits');
    assert.ok(frame.offsetTop + 48 * frame.scale <= epsilon,
      'the native header cover must stay above the visible surface');
    assert.ok(frame.offsetTop + frameHeight - 76 * frame.scale >= height - epsilon,
      'the native transport cover must stay below the visible surface');
  }
});
