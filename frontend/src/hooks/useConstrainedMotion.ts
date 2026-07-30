'use client';

import { useEffect, useState } from 'react';

type NetworkInformation = {
  effectiveType?: string;
  saveData?: boolean;
  addEventListener?: (type: 'change', listener: () => void) => void;
  removeEventListener?: (type: 'change', listener: () => void) => void;
};

type NavigatorWithCapabilities = Navigator & {
  connection?: NetworkInformation;
  deviceMemory?: number;
};

type IdleWindow = Window & {
  requestIdleCallback?: (
    callback: () => void,
    options?: { timeout: number },
  ) => number;
  cancelIdleCallback?: (handle: number) => void;
};

function meetsDeviceAndNetworkBudget() {
  const navigatorWithCapabilities = navigator as NavigatorWithCapabilities;
  const connection = navigatorWithCapabilities.connection;

  if (connection?.saveData) return false;
  if (connection?.effectiveType && /(^|-)2g$/.test(connection.effectiveType)) {
    return false;
  }
  if (
    typeof navigatorWithCapabilities.deviceMemory === 'number' &&
    navigatorWithCapabilities.deviceMemory < 4
  ) {
    return false;
  }
  if (
    typeof navigator.hardwareConcurrency === 'number' &&
    navigator.hardwareConcurrency < 4
  ) {
    return false;
  }

  return true;
}

export function useConstrainedMotion() {
  const [isEligible, setIsEligible] = useState(false);

  useEffect(() => {
    const media = window.matchMedia('(prefers-reduced-motion: reduce)');
    const connection = (navigator as NavigatorWithCapabilities).connection;
    const idleWindow = window as IdleWindow;
    let idleHandle: number | undefined;
    let quietTimer: number | undefined;

    const cancelPending = () => {
      if (idleHandle !== undefined) {
        idleWindow.cancelIdleCallback?.(idleHandle);
        window.clearTimeout(idleHandle);
        idleHandle = undefined;
      }
      if (quietTimer !== undefined) {
        window.clearTimeout(quietTimer);
        quietTimer = undefined;
      }
    };

    const evaluate = () => {
      cancelPending();
      if (
        document.visibilityState !== 'visible' ||
        media.matches ||
        !meetsDeviceAndNetworkBudget()
      ) {
        setIsEligible(false);
        return;
      }

      const enable = () => {
        idleHandle = undefined;
        if (
          document.visibilityState === 'visible' &&
          !media.matches &&
          meetsDeviceAndNetworkBudget()
        ) {
          setIsEligible(true);
        }
      };

      if (idleWindow.requestIdleCallback) {
        idleHandle = idleWindow.requestIdleCallback(enable, { timeout: 1500 });
      } else {
        idleHandle = window.setTimeout(enable, 800);
      }
    };

    const postponeForInput = () => {
      setIsEligible(false);
      if (quietTimer !== undefined) window.clearTimeout(quietTimer);
      quietTimer = window.setTimeout(evaluate, 500);
    };

    evaluate();
    document.addEventListener('visibilitychange', evaluate);
    media.addEventListener('change', evaluate);
    connection?.addEventListener?.('change', evaluate);
    window.addEventListener('pointerdown', postponeForInput, { passive: true });
    window.addEventListener('keydown', postponeForInput);

    return () => {
      cancelPending();
      document.removeEventListener('visibilitychange', evaluate);
      media.removeEventListener('change', evaluate);
      connection?.removeEventListener?.('change', evaluate);
      window.removeEventListener('pointerdown', postponeForInput);
      window.removeEventListener('keydown', postponeForInput);
    };
  }, []);

  return {
    allowEnhancedMotion: isEligible,
    isPageVisible:
      typeof document === 'undefined' || document.visibilityState === 'visible',
  };
}
