# Quickstart: Video URL Protection

## Problem
YouTube embed URLs are visible in browser DevTools, allowing students to extract and share paid video content.

## Solution
Add **closed Shadow DOM** encapsulation to the existing `SecureVideoPlayer` to hide the iframe from DOM inspection, and strip the `src` attribute after load.

## Files to Change

| File | Action |
|---|---|
| `frontend/src/components/video/SecureVideoPlayer.tsx` | Modify `initPlayer()` to use closed Shadow DOM |
| `frontend/src/components/content/VideoPlayer.tsx` | Delete (legacy insecure player) |
| `frontend/src/utils/dom-shield.ts` | Optional: add Shadow DOM helper |

## Key Implementation

In `SecureVideoPlayer.tsx` → `initPlayer()`:

```typescript
// Before: container.appendChild(wrapper)
// After:
const shadow = container.attachShadow({ mode: 'closed' });
shadow.appendChild(wrapper);

// After onReady callback:
const iframe = shadow.querySelector('iframe');
if (iframe) {
  iframe.removeAttribute('src'); // Strip visible URL
}
```

## Verification

1. Open DevTools → Elements → the iframe is **not visible** (closed Shadow DOM)
2. Video plays normally with custom controls
3. Watch count tracking still works
