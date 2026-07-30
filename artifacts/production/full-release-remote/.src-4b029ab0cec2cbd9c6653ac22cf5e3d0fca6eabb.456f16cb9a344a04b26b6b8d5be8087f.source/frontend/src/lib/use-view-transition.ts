/**
 * useViewTransition — same-document View Transitions hook
 *
 * Wraps Next.js router.push with document.startViewTransition()
 * so DOM changes are captured as a transition snapshot, creating
 * a cinematic morph between routes.
 *
 * Gracefully degrades: if the browser doesn't support the API,
 * falls back to normal navigation.
 *
 * Usage:
 *   const navigateWithTransition = useViewTransition();
 *   navigateWithTransition('/student/packages/123');
 */

import { useRouter } from "next/navigation";
import { useCallback } from "react";

type ViewTransitionDocument = Document & {
  startViewTransition?: (cb: () => void | Promise<void>) => { finished: Promise<void> };
};

export function useViewTransition() {
  const router = useRouter();

  const navigate = useCallback(
    (href: string) => {
      const doc = document as ViewTransitionDocument;

      if (doc.startViewTransition) {
        doc.startViewTransition(() => {
          router.push(href);
        });
      } else {
        // Fallback: standard navigation
        router.push(href);
      }
    },
    [router],
  );

  return navigate;
}
