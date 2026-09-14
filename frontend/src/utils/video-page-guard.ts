function shouldBlockLessonKeyboardShortcut(
  event: Pick<KeyboardEvent, 'altKey' | 'ctrlKey' | 'key' | 'metaKey' | 'shiftKey'>
): boolean {
  const key = event.key.toLowerCase();

  if (key === 'contextmenu' || key === 'f12' || (key === 'f10' && event.shiftKey)) return true;

  const primaryModifier = event.ctrlKey || event.metaKey;
  if (primaryModifier && !event.altKey && !event.shiftKey && ['p', 's', 'u'].includes(key)) {
    return true;
  }

  // Common Chromium/Firefox developer-tools shortcuts.
  if (event.ctrlKey && event.shiftKey && ['c', 'i', 'j', 'k'].includes(key)) {
    return true;
  }

  // macOS uses both Command+Option and Command+Shift variants.
  if (event.metaKey && event.altKey && ['c', 'i', 'j', 'u'].includes(key)) {
    return true;
  }

  return event.metaKey && event.shiftKey && key === 'c';
}

function stopBrowserAction(event: Event) {
  event.preventDefault();
}

export function installLessonPageProtectionGuard(target: Document): () => void {
  const handleContextMenu = (event: MouseEvent) => {
    // The extension entry is also reachable through the keyboard menu, so
    // mouse and keyboard context menus must follow the same lesson policy.
    stopBrowserAction(event);
  };
  const handleKeyDown = (event: KeyboardEvent) => {
    if (shouldBlockLessonKeyboardShortcut(event)) stopBrowserAction(event);
  };
  const listenerOptions: AddEventListenerOptions = { capture: true };

  target.addEventListener('contextmenu', handleContextMenu, listenerOptions);
  target.addEventListener('keydown', handleKeyDown, listenerOptions);

  return () => {
    target.removeEventListener('contextmenu', handleContextMenu, listenerOptions);
    target.removeEventListener('keydown', handleKeyDown, listenerOptions);
  };
}
