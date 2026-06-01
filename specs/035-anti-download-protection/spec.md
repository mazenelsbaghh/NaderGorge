---
title: "Anti-Download DRM & IDM Protection"
status: "Draft"
created: "2026-03-31"
---

# Feature Specification: Anti-Download DRM & IDM Protection

## 1. Feature Description
The goal of this feature is to protect the educational video content in the Nader George Academy platform by actively preventing browser extensions (like Internet Download Manager - IDM) from intercepting and downloading the videos. The system must hide the download overlay button injected by these extensions and implement backend mechanisms to reject external unauthorized download attempts.

## 2. User Scenarios & Expected Behavior

**Scenario 1: Student with IDM watches a video**
*   **Context:** A student who has Internet Download Manager (or similar extensions) installed navigates to a lesson page.
*   **Action:** The student attempts to watch the embedded educational video.
*   **Expected Result:** The video plays smoothly, but the IDM download panel/button (which typically hovers in the corner of media elements) does not appear.

**Scenario 2: Student attempts to hijack the stream connection**
*   **Context:** A malicious user uses browser developer tools to sniff the network traffic and captures the streaming proxy URL.
*   **Action:** The user inputs the URL explicitly into a download manager or command-line tool.
*   **Expected Result:** The download request is rejected by the server, returning a "403 Forbidden" or "Access Denied" error instead of the video file.

**Scenario 3: Normal student playback**
*   **Context:** A regular student on any device accesses the lesson.
*   **Action:** Click play on the video.
*   **Expected Result:** The lesson video loads correctly and maintains stable playback without performance degradation caused by the anti-download protections.

## 3. Functional Requirements

### 3.1 Frontend Player Hardening
*   **REQ-1 (DOM Obfuscation):** The `<video>` element's `src` attribute must be actively hidden or destroyed from the DOM tree immediately after the browser establishes the media connection, preventing DOM-scraping extensions from locating the raw URL.
*   **REQ-2 (Overlay Prevention):** Visual interference mechanics (e.g., specific CSS isolation or custom Blob wrappers) must be employed to defeat the visual hover-button injection algorithms commonly used by download managers.

### 3.2 Backend Stream Protection
*   **REQ-3 (Strict Network Policies):** The streaming proxy layer MUST enforce strict network access control, relying on unique, one-time, or heavily validated authentication tokens rather than static URLs.
*   **REQ-4 (Origin and Destination Validation):** The proxy must vigorously validate `Sec-Fetch-Dest`, `Sec-Fetch-Mode`, and `Referer` headers to distinguish legitimate in-platform `<video>` playback requests from pure file download streams.
*   **REQ-5 (IP/Session Pinning):** Download requests that originate outside the active user's browsing context must be instantly blocked.

## 4. Success Criteria

*   **Metric 1:** The IDM floating download button successfully fails to render on the video player in Chrome and Edge environments.
*   **Metric 2:** Simulated external download attempts using standard utilities (IDM, wget, curl) on the video endpoints result in 100% failure (HTTP 403/Forbidden).
*   **Metric 3:** The playback experience for authorized students maintains a 99% success rate without unusual buffering or load failures directly attributed to the DRM layer.

## 5. Security & Privacy Defaults
*   Anti-download tracking will not capture PII (Personally Identifiable Information). Headers are evaluated dynamically and anonymously for access control.
*   No strict OS-level dependencies or third-party executable installations are required for the student context.

## 6. Assumptions & Out of Scope
*   **Out of Scope:** Preventing screen-recording software (e.g., OBS, Camtasia), external cameras recording the screen, or sophisticated kernel-level audio/video loopback captures.
*   **Assumption:** The protection targets the vast majority of consumer-grade browser extensions and native HTML media parsers, acknowledging that mathematically perfect DRM without Widevine/PlayReady is impossible for standard progressive `.mp4` files.
