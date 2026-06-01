# Research: devtools-iframe-hider

## 1. DevTools Detection Heuristics
**Decision**: Use `window.outerWidth - window.innerWidth > 160` (and height equivalent) as the primary cross-browser heuristic to detect if DevTools are opened.
**Rationale**: Native JS APIs do not provide a direct method to detect the DevTools state due to security policies. Calculating the delta between the browser's outer and inner dimensions reliably detects when the DevTools panel occupies screen space.
**Alternatives considered**: Polling `debugger` statement execution time (clunky, interrupts main thread visibly, triggers annoying breakpoints for devs).

## 2. Removing vs Reconstructing the iframe
**Decision**: We will temporarily remove the iframe from the DOM when DevTools open, and inject a new one when closed.
**Rationale**: Altering the `src` property via trapping causes critical breakages with YouTube API and VK Video Player API, as they rely on inspecting their own iframe targets to initiate postMessage boundaries.
**Alternatives considered**: Nullifying the `src` attribute upon iframe load (failed, broke API).
