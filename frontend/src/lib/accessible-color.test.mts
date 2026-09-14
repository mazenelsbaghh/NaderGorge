import assert from 'node:assert/strict';
import test from 'node:test';

import {
  accessibleColorPair,
  contrastRatio,
} from './accessible-color.ts';

const requestedColors = [
  '#ffffff',
  '#000000',
  '#777777',
  '#ff0000',
  '#0e8f8f',
  '#abc',
  'invalid',
];

for (const requestedColor of requestedColors) {
  test(`${requestedColor} produces a WCAG AA support color pair`, () => {
    const pair = accessibleColorPair(requestedColor);

    assert.ok(
      pair.contrastRatio >= 4.5,
      `${requestedColor} produced ${pair.contrastRatio.toFixed(2)}:1`,
    );
    assert.equal(
      pair.contrastRatio,
      contrastRatio(pair.color, pair.backgroundColor),
    );
  });
}

test('support colors report whether contrast correction was required', () => {
  assert.equal(accessibleColorPair('#ffffff').adjusted, false);
  assert.equal(accessibleColorPair('#ff0000').adjusted, true);
});
