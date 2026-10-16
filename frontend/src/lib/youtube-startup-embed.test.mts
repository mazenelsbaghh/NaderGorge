import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';

import { createDevToolsSuspensionScript } from './video-embed-devtools-guard.ts';

// Execute the actual generated document with only the browser, clock and
// third-party SDK replaced. No authorization or player code is mocked.
const source = readFileSync(new URL('./video-embed-html.ts', import.meta.url), 'utf8');
const generator = source.slice(source.indexOf('function escapeHtml('), source.indexOf('function generateVkEmbedHtml'));
const compiled = ts.transpileModule(generator, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
}).outputText;
const html: string = vm.runInNewContext(compiled + '\ngenerateYouTubeEmbedHtml("test-video", "Student", "")', {
  createDevToolsSuspensionScript,
});
const script = html.slice(html.indexOf('<script>') + '<script>'.length, html.lastIndexOf('</script>'));

class BrowserElement {
  id = '';
  src = '';
  style: Record<string, string> = {};
  children: BrowserElement[] = [];
  parent: BrowserElement | null = null;
  onerror?: () => void;
  classList = { add() {}, remove() {} };
  appendChild(child: BrowserElement) { child.parent = this; this.children.push(child); return child; }
  attachShadow() { return new BrowserElement(); }
  addEventListener() {}
  removeAttribute(name: string) { if (name === 'src') this.src = ''; }
  remove() { if (this.parent) this.parent.children = this.parent.children.filter(child => child !== this); }
  replaceWith(replacement: BrowserElement) {
    assert.ok(this.parent);
    replacement.parent = this.parent;
    this.parent.children[this.parent.children.indexOf(this)] = replacement;
  }
  getElementById(id: string): BrowserElement | null {
    return this.id === id ? this : this.children.map(child => child.getElementById(id)).find(Boolean) ?? null;
  }
}

function runEmbed(recovery = false) {
  let now = 0;
  let timerId = 0;
  const timers = new Map<number, { due: number; callback: () => void }>();
  const schedule = (callback: () => void, delay: number) => {
    timers.set(++timerId, { due: now + delay, callback });
    return timerId;
  };
  const messages: Array<{ type: string; data: Record<string, unknown> }> = [];
  const listeners = new Map<string, Array<(event?: unknown) => void>>();
  const shell = new BrowserElement(); shell.id = 'shell';
  const overlay = new BrowserElement(); overlay.id = 'click-overlay';
  const document = {
    head: new BrowserElement(), body: new BrowserElement(), visibilityState: 'visible',
    createElement: () => new BrowserElement(),
    createDocumentFragment: () => ({ childNodes: [] }),
    getElementById: (id: string) => id === 'shell' ? shell : id === 'click-overlay' ? overlay : null,
    querySelectorAll: () => [], querySelector: () => null, getElementsByTagName: () => [], addEventListener() {},
  };
  const window = {
    location: { origin: 'https://platform.test', search: recovery ? '?s=owned-session&youtubeRecovery=1' : '?s=owned-session', replace() {} },
    parent: { postMessage(message: typeof messages[number]) { messages.push(message); } },
    addEventListener(type: string, callback: (event?: unknown) => void) {
      listeners.set(type, [...(listeners.get(type) ?? []), callback]);
    },
    setTimeout: schedule,
    onYouTubeIframeAPIReady: () => {},
    YT: undefined as unknown,
  };
  const context = vm.createContext({
    window, document, navigator: { maxTouchPoints: 1, onLine: true }, URLSearchParams,
    setTimeout: schedule, clearTimeout: (id: number) => timers.delete(id),
    setInterval: () => 0, clearInterval() {},
    Date: class extends Date { static now() { return now; } },
  });
  vm.runInContext(script, context);

  type PlayerEvents = Record<string, (event: { target?: FakePlayer; data?: number }) => void>;
  const players: FakePlayer[] = [];
  class FakePlayer {
    events: PlayerEvents;
    frame: BrowserElement;
    destroyed = false;
    constructor(target: string | BrowserElement, options: { events: PlayerEvents }) {
      this.events = options.events;
      if (typeof target === 'string') {
        const placeholder = document.getElementById(target);
        assert.ok(placeholder);
        this.frame = new BrowserElement();
        placeholder.replaceWith(this.frame);
      } else this.frame = target;
      players.push(this);
    }
    getIframe() { return this.frame; }
    getDuration() { return 120; }
    getVolume() { return 100; }
    isMuted() { return false; }
    getAvailableQualityLevels() { return []; }
    getPlaybackQuality() { return 'auto'; }
    pauseVideo() {}
    destroy() { this.destroyed = true; }
  }
  return {
    messages, players,
    sdkError() { document.head.children[0].onerror?.(); },
    loadSdk() {
      const sdk = { Player: FakePlayer, PlayerState: { PLAYING: 1, ENDED: 0 } };
      window.YT = sdk;
      Object.assign(context, { YT: sdk });
      window.onYouTubeIframeAPIReady();
    },
    ready() { const player = players[0]; player.events.onReady({ target: player }); },
    providerError(code: number) { players[0].events.onError({ data: code }); },
    dispatch(type: string, event?: unknown) { listeners.get(type)?.forEach(callback => callback(event)); },
    advance(ms: number) {
      const end = now + ms;
      for (;;) {
        const next = [...timers].filter(([, timer]) => timer.due <= end).sort((a, b) => a[1].due - b[1].due)[0];
        if (!next) break;
        timers.delete(next[0]); now = next[1].due; next[1].callback();
      }
      now = end;
    },
  };
}

for (const failure of ['sdk_load', 'sdk_timeout', 'iframe_timeout', 'provider_error'] as const) {
  test(`2026-10-16 Chrome startup failure reports ${failure} once without a false ready`, () => {
    const embed = runEmbed();
    if (failure === 'iframe_timeout' || failure === 'provider_error') embed.loadSdk();
    if (failure === 'sdk_load') embed.sdkError();
    if (failure === 'provider_error') embed.providerError(153);
    embed.advance(60_000);
    assert.deepEqual(embed.messages.map(message => [message.type, message.data.phase]), [['error', failure]]);
    assert.equal(embed.messages[0].data.code, failure === 'provider_error' ? 153 : 0);
    if (failure === 'iframe_timeout' || failure === 'provider_error') {
      embed.ready();
      assert.equal(embed.players[0].destroyed, true);
    } else embed.loadSdk();
    assert.equal(embed.messages.length, 1, 'late provider callbacks cannot revive a failed embed');
  });
}

test('a fresh recovery document uses the privacy host and emits ready once with no later startup error', () => {
  const embed = runEmbed(true);
  embed.loadSdk();
  const url = new URL(embed.players[0].frame.src);
  assert.equal(url.origin, 'https://www.youtube-nocookie.com');
  assert.equal(url.pathname, '/embed/test-video');
  assert.equal(url.searchParams.get('enablejsapi'), '1');
  assert.equal(url.searchParams.get('origin'), 'https://platform.test');
  assert.equal(url.searchParams.get('controls'), '1', 'touch playback keeps native activation');
  embed.ready(); embed.ready(); embed.advance(60_000);
  assert.equal(embed.messages.filter(message => message.type === 'ready').length, 1);
  assert.equal(embed.messages.filter(message => message.type === 'error').length, 0);
  assert.equal(embed.messages.filter(message => message.type === 'autoplayBlocked').length, 1);
});

for (const termination of ['pagehide', 'security'] as const) {
  test(`${termination} stops startup deadlines and rejects late provider readiness`, () => {
    const embed = runEmbed();
    embed.loadSdk();
    if (termination === 'pagehide') embed.dispatch('pagehide', { persisted: false });
    else embed.dispatch('keydown', { key: 'F12', preventDefault() {}, stopImmediatePropagation() {} });
    embed.advance(60_000); embed.ready();
    assert.equal(embed.messages.filter(message => message.type === 'error' || message.type === 'ready').length, 0);
    assert.equal(embed.players[0].destroyed, true);
  });
}

test('returning from the back-forward cache can finish a pending recovery attempt', () => {
  const embed = runEmbed(true);
  embed.loadSdk();
  embed.dispatch('pagehide', { persisted: true });
  embed.advance(120_000);
  assert.equal(embed.messages.length, 0);
  embed.dispatch('pageshow', { persisted: true });
  embed.advance(10_000);
  embed.ready();
  embed.advance(60_000);
  assert.equal(embed.messages.filter(message => message.type === 'ready').length, 1);
  assert.equal(embed.messages.filter(message => message.type === 'error').length, 0);
});
