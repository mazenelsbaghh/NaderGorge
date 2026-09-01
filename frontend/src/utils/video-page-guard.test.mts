import assert from 'node:assert/strict';
import test from 'node:test';

import { installLessonPageProtectionGuard } from './video-page-guard.ts';

function dispatchedContextMenuEvent(button: number, ctrlKey = false): Event {
  const event = new Event('contextmenu', { cancelable: true });
  Object.defineProperties(event, {
    button: { value: button },
    ctrlKey: { value: ctrlKey },
  });
  return event;
}

function dispatchedKeyDownEvent(
  key: string,
  modifiers: Partial<Pick<KeyboardEvent, 'altKey' | 'ctrlKey' | 'metaKey' | 'shiftKey'>> = {},
): Event {
  const event = new Event('keydown', { cancelable: true });
  Object.defineProperties(event, {
    altKey: { value: modifiers.altKey ?? false },
    ctrlKey: { value: modifiers.ctrlKey ?? false },
    key: { value: key },
    metaKey: { value: modifiers.metaKey ?? false },
    shiftKey: { value: modifiers.shiftKey ?? false },
  });
  return event;
}

// Production regression 2026-09-01: right-clicking outside the player exposed
// the browser extension download menu over protected lesson content.
test('lesson page blocks mouse right-click and macOS Control-click', () => {
  for (const [button, ctrlKey] of [[2, false], [0, true]] as const) {
    const documentTarget = new EventTarget();
    const cleanup = installLessonPageProtectionGuard(documentTarget as unknown as Document);
    const event = dispatchedContextMenuEvent(button, ctrlKey);

    documentTarget.dispatchEvent(event);
    assert.equal(event.defaultPrevented, true);
    cleanup();
  }
});

test('lesson page blocks the keyboard context menu used to reach extension actions', () => {
  const documentTarget = new EventTarget();
  const cleanup = installLessonPageProtectionGuard(documentTarget as unknown as Document);
  const event = dispatchedContextMenuEvent(0);

  documentTarget.dispatchEvent(event);
  assert.equal(event.defaultPrevented, true);
  cleanup();
});

test('lesson page restores mouse context menus after cleanup', () => {
  const documentTarget = new EventTarget();
  const cleanup = installLessonPageProtectionGuard(documentTarget as unknown as Document);

  const protectedEvent = dispatchedContextMenuEvent(2);
  documentTarget.dispatchEvent(protectedEvent);
  assert.equal(protectedEvent.defaultPrevented, true);

  cleanup();

  const eventAfterCleanup = dispatchedContextMenuEvent(2);
  documentTarget.dispatchEvent(eventAfterCleanup);
  assert.equal(eventAfterCleanup.defaultPrevented, false);
});

test('lesson page blocks common save, print, source, and developer-tools shortcuts', () => {
  const blockedShortcuts = [
    ['s', { ctrlKey: true }],
    ['p', { metaKey: true }],
    ['u', { ctrlKey: true }],
    ['F12', {}],
    ['F10', { shiftKey: true }],
    ['ContextMenu', {}],
    ['i', { ctrlKey: true, shiftKey: true }],
    ['k', { ctrlKey: true, shiftKey: true }],
    ['i', { altKey: true, metaKey: true }],
    ['c', { metaKey: true, shiftKey: true }],
  ] as const;

  for (const [key, modifiers] of blockedShortcuts) {
    const documentTarget = new EventTarget();
    const cleanup = installLessonPageProtectionGuard(documentTarget as unknown as Document);
    const event = dispatchedKeyDownEvent(key, modifiers);

    documentTarget.dispatchEvent(event);
    assert.equal(event.defaultPrevented, true, `${key} should be blocked`);
    cleanup();
  }
});

test('lesson page preserves ordinary keyboard input and restores shortcuts after cleanup', () => {
  const documentTarget = new EventTarget();
  const cleanup = installLessonPageProtectionGuard(documentTarget as unknown as Document);

  const ordinaryKey = dispatchedKeyDownEvent('c', { ctrlKey: true });
  documentTarget.dispatchEvent(ordinaryKey);
  assert.equal(ordinaryKey.defaultPrevented, false);

  cleanup();

  const shortcutAfterCleanup = dispatchedKeyDownEvent('s', { ctrlKey: true });
  documentTarget.dispatchEvent(shortcutAfterCleanup);
  assert.equal(shortcutAfterCleanup.defaultPrevented, false);
});
