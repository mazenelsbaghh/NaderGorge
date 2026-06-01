# devtools-iframe-hider

## Feature Description
Dynamically hide the video iframe source (or completely remove the iframe) when the browser developer tools (Inspect Element) are opened, and automatically restore the iframe/source when DevTools are closed, to prevent users from copying the direct video URL.

## User Scenarios & Testing
- **Scenario 1: Opening DevTools**
  A user plays a video and tries to open DevTools to inspect the DOM and find the video URL.
  *Expected:* The system detects DevTools opening, immediately removes the iframe element entirely from the DOM (taking the URL with it), and pauses playback.
- **Scenario 2: Closing DevTools**
  The user closes DevTools.
  *Expected:* The system detects DevTools closure and automatically rebuilds the iframe with its original functionality and source, allowing the user to resume watching.

## Functional Requirements
1. The video embed page must monitor browser dimensions to reliably detect when DevTools are opened or closed.
2. When DevTools open, the system must completely remove the target `iframe` from the DOM to ensure the direct URL cannot be inspected.
3. When DevTools close, the system must re-create the `iframe` using the securely stored (memory-only) XOR-decoded URL and re-attach it to the DOM.
4. The system must re-initialize the VK/YouTube JS API players upon recreating the iframe to ensure interactive controls continue working.

## Success Criteria
- The video URL is verifiably absent from the Elements panel while DevTools are open.
- Opening and closing DevTools successfully toggles the video element without crashing the proxy page.
- The player JS API (VK/YouTube) successfully rebinds to the newly created iframe.

## Assumptions & Boundaries
- This applies specifically to the proxy page served at `/api/video/embed`.
- Uses window resize threshold heuristics (e.g., outer vs inner dimensions difference > 160px) as the cross-browser indicator of DevTools.
