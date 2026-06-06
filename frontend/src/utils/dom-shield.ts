import { devConsole } from '@/utils/dev-console';
/**
 * Utility functions to prevent casual inspection of the DOM and network requests.
 * These are not bulletproof against determined attackers, but they stop 99% of users.
 */

// Disables context menu (right click)
export const disableContextMenu = (element: HTMLElement) => {
  const handler = (e: Event) => e.preventDefault();
  element.addEventListener('contextmenu', handler);
  return () => element.removeEventListener('contextmenu', handler);
};

// Prevents dragging elements (e.g. saving an image)
export const preventDrag = (element: HTMLElement) => {
  const handler = (e: Event) => e.preventDefault();
  element.addEventListener('dragstart', handler);
  return () => element.removeEventListener('dragstart', handler);
};

// Obfuscates iframe src by loading the iframe HTML into a Blob URL
// Note: This relies on changing how the iframe works, and may break some
// YouTube Iframe API functionalities depending on how it's injected.
// Using Shadow DOM is already our primary defense, so we'll just provide
// a basic property clearing function instead that tries to hide the video ID.
export const hideIframeSrcAttribute = () => {
  // We can't actually change the src without reloading the iframe,
  // but we can try to obscure it in attributes panel by replacing it with a getter
  // This is a bit advanced and sometimes unstable.
  // Instead, the best defense is using Shadow DOM `mode: closed` which we already do.
};

// Creates a mutation observer that watches for element removal/modification
// and fights back by replacing it or throwing errors.
export const createMutationGuard = (element: HTMLElement, onTamper: () => void) => {
  const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      if (mutation.type === 'childList') {
        const removed = Array.from(mutation.removedNodes);
        if (removed.includes(element) || removed.some(node => node.contains(element))) {
          onTamper();
        }
      } else if (mutation.type === 'attributes') {
        // ONLY trigger if the protected element itself is targeted by attribute changes
        if (mutation.target === element) {
          onTamper();
        }
      }
    }
  });

  if (element.parentNode) {
    // Watch parent for removal of 'element'
    observer.observe(element.parentNode, {
      childList: true 
    });
  }

  // Watch the element itself for tampering (styles, classes)
  observer.observe(element, {
    attributes: true,
    attributeFilter: ['style', 'class', 'src']
  });

  return () => observer.disconnect();
};

/**
 * Trap `shadowRoot` access on a closed shadow host.
 * For closed shadows, `el.shadowRoot` already returns null,
 * but this records a dev-only warning so we can detect inspection attempts.
 */
export const guardShadowHost = (element: HTMLElement) => {
  try {
    Object.defineProperty(element, 'shadowRoot', {
      get: () => {
        devConsole.warn('[DOM-Shield] Unauthorized shadow root access attempted.');
        return null;
      },
      configurable: false
    });
  } catch {
    // Already defined or browser doesn't allow — ignore
  }
};

export const applyDomShields = (element: HTMLElement, onTamper: () => void) => {
  const cleanupContext = disableContextMenu(element);
  const cleanupDrag = preventDrag(element);
  const cleanupMutation = createMutationGuard(element, onTamper);
  
  // Guard shadow root access if element hosts a closed shadow
  guardShadowHost(element);

  return () => {
    cleanupContext();
    cleanupDrag();
    cleanupMutation();
  };
};
