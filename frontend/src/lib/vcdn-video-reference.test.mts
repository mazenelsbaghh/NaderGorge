import assert from 'node:assert/strict';
import test from 'node:test';
import { parseVcdnVideoId } from './vcdn-video-reference.ts';

const cases: Array<[string, string | null]> = [
  ['11111111-1111-4111-8111-111111111111', '11111111-1111-4111-8111-111111111111'],
  [' https://stream.vcdn.me/11111111-1111-4111-8111-111111111111/master.m3u8 ', '11111111-1111-4111-8111-111111111111'],
  ['https://embed.vcdn.me/11111111-1111-4111-8111-111111111111', '11111111-1111-4111-8111-111111111111'],
  ['<iframe src="https://embed.vcdn.me/11111111-1111-4111-8111-111111111111" allowfullscreen></iframe>', '11111111-1111-4111-8111-111111111111'],
  ['https://embed.vcdn.me/embed/11111111-1111-4111-8111-111111111111', '11111111-1111-4111-8111-111111111111'],
  ['vid_abc123', null],
  ['https://www.vcdn.me/', null],
  ['https://embed.vcdn.me.evil.test/11111111-1111-4111-8111-111111111111', null],
  ['http://embed.vcdn.me/11111111-1111-4111-8111-111111111111', null],
  ['https://user@embed.vcdn.me/11111111-1111-4111-8111-111111111111', null],
  ['https://embed.vcdn.me:8443/11111111-1111-4111-8111-111111111111', null],
  ['https://stream.vcdn.me/11111111-1111-4111-8111-111111111111/master.m3u8?token=secret', null],
  ['https://embed.vcdn.me/11111111-1111-4111-8111-111111111111#other', null],
  ['https://embed.vcdn.me/11111111-1111-4111-8111-111111111111/other', null],
  ['https://embed.vcdn.me/%3Cscript%3E', null],
  ['<iframe src="javascript:alert(1)"></iframe>', null],
  ['', null], ['a'.repeat(128), null], ['a'.repeat(129), null],
];
for (const [source, expected] of cases) {
  test(`VCDN reference ${JSON.stringify(source)} resolves safely`, () => {
    assert.equal(parseVcdnVideoId(source), expected);
  });
}
