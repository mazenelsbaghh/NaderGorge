# Feature Specification: Secure Video Player with YouTube Link Protection

## Overview

**Short Name**: secure-video-player
**Feature Number**: 005
**Status**: Draft
**Created**: 2026-03-25

### Problem Statement

Students currently access lessons through embedded YouTube videos. The YouTube video URLs are visible in the browser's developer tools (inspect element), allowing students to:
1. Extract the original YouTube link and share it freely
2. Watch content outside the platform, bypassing access controls and watch limits
3. Potentially share paid content with non-paying users

This undermines the platform's monetization model and content protection strategy.

### Goal

Provide a custom video player overlay that plays lesson videos while preventing students from discovering or extracting the underlying YouTube video URLs through browser developer tools, page source, or network requests.

---

## User Stories

### US-1: Student Watches Protected Video
**As a** student with an active package subscription,  
**I want to** watch lesson videos through the platform's custom player,  
**so that** I get a seamless learning experience without being able to extract or share the video source.

**Acceptance Criteria:**
- The video plays smoothly within the platform's custom player
- Standard playback controls are available (play, pause, seek, volume, fullscreen)
- The YouTube branding and overlays are not visible
- The video URL is not discoverable through browser developer tools

### US-2: Student Cannot Extract Video Link
**As an** administrator,  
**I want** the video source URLs to be hidden from browser inspection,  
**so that** students cannot copy and share the direct YouTube links outside the platform.

**Acceptance Criteria:**
- The YouTube video ID is not present in the page's HTML source
- Network requests do not expose the direct YouTube embed URL in a human-readable format
- Right-click → "Inspect Element" does not reveal the video source
- The video link is fetched dynamically and obfuscated

### US-3: Admin Adds Videos with Protection Automatically Applied
**As an** administrator,  
**I want** video protection to be applied automatically when I add YouTube videos to lessons,  
**so that** I don't need to perform any additional configuration steps for content security.

**Acceptance Criteria:**
- Existing video upload workflow remains unchanged
- Protection is applied to all videos by default
- No additional admin action is required

---

## Functional Requirements

### FR-1: Custom Video Player Overlay
The platform must display lesson videos using a custom player interface that:
- Provides standard playback controls (play/pause, seek bar, volume, fullscreen, playback speed)
- Matches the platform's Pharaonic design theme (warm sand/gold colors)
- Supports responsive layout for desktop, tablet, and mobile
- Shows lesson title and progress indicators within the player chrome

### FR-2: Video Source Obfuscation
The system must prevent the YouTube video URL from being discoverable by:
- Not embedding the YouTube video ID directly in the page HTML
- Serving the video URL through a server-side proxy or token-based endpoint
- Ensuring the video source URL is not visible in the browser's Elements panel
- Using short-lived, single-use tokens for video URL retrieval

### FR-3: Server-Side Video URL Resolution
The system must:
- Store only the YouTube video ID in the database (current behavior)
- Resolve the playable video URL on the server side only when requested by an authenticated, authorized user
- Return a time-limited, obfuscated response that the player can consume
- Log each video access request for audit purposes

### FR-4: Watch Limit Enforcement
The protected player must integrate with the existing watch limit system:
- Each video play counts against the student's watch limit
- The watch count increments at a defined playback milestone (e.g., after 30 seconds of viewing)
- The player shows remaining watch count to the student
- When the limit is reached, the video becomes locked

### FR-5: Anti-Circumvention Measures
The system should implement reasonable anti-circumvention measures:
- Disable right-click context menu on the video player area
- Prevent drag-and-drop of the video element
- Clear video source data from the DOM after loading
- Obfuscate network request URLs for video retrieval

---

## User Scenarios & Testing

### Scenario 1: Student Watches Lesson Video
1. Student navigates to a lesson with video content
2. Student clicks the play button on the custom player
3. Video begins playing with full controls available
4. Student can pause, seek, adjust volume, and toggle fullscreen
5. Watch count increments after 30 seconds of viewing
6. Student finishes watching and returns to lesson content

### Scenario 2: Student Attempts to Extract Video URL
1. Student opens browser developer tools while video is playing
2. Student inspects the video element in the Elements panel
3. **Expected**: No YouTube URL or video ID is visible in the HTML
4. Student examines Network tab for video-related requests
5. **Expected**: Video URL requests use obfuscated tokens, not raw YouTube URLs
6. Student views page source
7. **Expected**: No YouTube IDs present in the raw HTML

### Scenario 3: Watch Limit Reached
1. Student has used all watch attempts for a video
2. Student tries to play the video again
3. System displays a message: "لقد استنفدت عدد المشاهدات المتاحة" (You've used all available views)
4. Play button is disabled
5. Student can request a watch limit reset from admin

### Scenario 4: Unauthorized Access Attempt
1. An unauthenticated user attempts to access the video URL endpoint directly
2. System returns an authentication error
3. A student without access to the specific package attempts to access a video
4. System returns an authorization error

---

## Success Criteria

| Criteria                                               | Target     |
|--------------------------------------------------------|------------|
| Video plays without buffering issues                   | 95% of the time |
| YouTube URL is not discoverable via inspect element     | 100%       |
| Video loads within acceptable time                      | Under 3 seconds |
| Watch limit enforcement accuracy                        | 100%       |
| Student satisfaction with player controls               | Comparable to YouTube |
| Platform content sharing incidents                       | Decrease by 90% |

---

## Key Entities

### VideoPlaybackSession
- **Student**: The student requesting the video
- **Lesson Video**: The video being played  
- **Session Token**: Short-lived, single-use token for video access
- **Created At**: When the session was initiated
- **Expires At**: When the access token expires
- **Watch Count**: Number of views consumed

---

## Scope

### In Scope
- Custom video player UI with standard controls
- Server-side video URL resolution and obfuscation
- DOM-level protection against casual inspection
- Integration with existing watch limit system
- Pharaonic theme styling for the player

### Out of Scope
- DRM (Digital Rights Management) implementation
- Video download prevention at OS/network level
- Watermarking of video content
- Protection against screen recording software
- Support for non-YouTube video providers

---

## Assumptions

1. YouTube videos will continue to be the primary video source
2. The current YouTube video ID storage in the database is sufficient
3. "Casual" protection (hiding from inspect element and network tab) is the target — not enterprise-grade DRM
4. Students with advanced technical skills may still find ways to access the video (accepted risk)
5. The existing watch limit tracking infrastructure will be reused
6. Server-side proxy for video URLs is acceptable from a performance perspective

---

## Dependencies

- Existing lesson video playback system
- Authentication and authorization system
- Watch limit tracking system
- Server-side endpoint for video URL resolution
