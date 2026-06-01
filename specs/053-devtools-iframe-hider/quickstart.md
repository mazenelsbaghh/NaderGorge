# Quickstart: devtools-iframe-hider

## Implementation Steps

1. **Locate Proxy Handlers**
   Open `frontend/src/app/api/video/embed/route.ts`.

2. **Modify YouTube DevTools Watcher**
   - Locate the `setInterval` block monitoring `innerWidth` and `outerWidth`.
   - Update the `isOpen && !_devtoolsOpen` block to completely remove the YouTube `iframe` from the DOM (e.g. `ytIframe.remove()`).
   - Update the `!isOpen && _devtoolsOpen` block to recreate the YouTube `iframe` using the original `src` (`_u`), reattach it, and re-invoke `new YT.Player(...)` to restore interactive controls.

3. **Modify VK DevTools Watcher**
   - Apply the same pattern for VK.
   - When DevTools open, remove `iframe` from `wrap`.
   - When DevTools close, reconstruct the `iframe`, set `_u` as `src`, append it, and create a new `VK.VideoPlayer(iframe)` instance.

4. **Testing**
   - Launch application.
   - Load an embedded video page.
   - Open browser developer tools; UI should clear the iframe gracefully.
   - Close browser developer tools; UI should reconstruct the iframe and video should replay / be controllable.
