# Interactive video learning

Implementation and release preflight, September 2026. Production execution is recorded separately under artifacts/production; this note describes the reviewed source.

## Authoring and student workflow

The existing admin lesson profile include **التفاعل والمراجعة**. Settings are saved per video. Every tool defaults to disabled, including notes, bookmarks, and chapter aids. Only an active Admin can read or save authoring configuration or generate author drafts. Teacher reports remain scoped read-only. Existing and newly created videos without a configuration expose no tools. Admins can preview the existing player to select a timestamp, add activities manually, import available question-bank items, or review AI-generated drafts before publishing.

Activities support moment, chapter, and video-end placement. The student player pauses for required questions and submits answers for server scoring. The video-end challenge stays in the player until the student finishes reviewing. These are learning checkpoints; existing mandatory exams and access controls remain the authorization boundary.

The notebook stores private timestamped notes and bookmarks, understanding reactions, and concept mastery. Teacher questions create pending lesson comments and link approved replies back to the originating entry. Reports contain answer aggregates, reaction density, and submitted teacher questions, without private notes. Difficult moments also appear on the existing student mistakes page; scored answers contribute to the learning overview.

Cards reveal answers, terms reveal definitions and examples, and experiments use bounded linear, product, or ratio templates. Reaction density is shown in the notebook and inside the player above its controls. Density uses each student's latest reaction per 15-second bucket; report aggregation has not been load-tested at production scale.

A single tap exposes custom controls; right-side double tap seeks forward ten seconds. Native Bunny controls retain their existing central touch area, with seeking zones at the edges. Playback rates include 1.25 and 1.75. Chapter summaries, the chapter list, and mindmaps render inside the player. Chapter changes automatically open the relevant panel except on iPhone/iPod, where opening is manual. Native fullscreen behavior still requires testing on a physical iPhone and real video providers.

## Persistence and versioning

Migration `20260912230216_AddVideoLearning` adds configuration and student-entry tables. Publishing produces a new configuration version: prior answers and mastery remain stored but are excluded from the current activity series. Replacing the video source suspends the interactions until an author reviews and republishes their timing. Private entries and reactions are scoped to the source revision.

Requests check existing content ownership, student access, required exams, source revision, and configuration version. Answers are scored on the server; unanswered question keys and explanations are removed from student snapshots. Answer uniqueness is enforced per student, activity, and configuration. Students can delete their own notes, bookmarks, understanding, and mastery entries.

## AI

The existing worker exposes an authenticated `/internal/video-learning` endpoint. Backend integration uses the existing `WORKER_URL` and `WORKER_ADMIN_TOKEN`. Authoring and student tutoring have separate switches. Student modes cover explanation, example, foundations, practice, questions, and note drafting.

AI context comes from existing chapter summaries, not a newly fetched transcript. Missing summaries prevent generation. Author output is a draft and is validated before it can be published. The student daily allowance is configurable from 1 to 50 requests per video; authors have 30. The day boundary is UTC. Requests reserve quota before calling the provider, so unsuccessful generations also consume a slot. Successful duplicate request IDs reuse the stored result.

## Verification and remaining release checks

The application suite passed 1,350 tests with two existing skips; the focused video-learning suite passed 14 tests after the final backend change. The 12 browser scenarios passed across Chromium and WebKit. Type checking passed, lint had zero errors and one pre-existing warning, and 123 video-protection checks passed. The frontend production build and worker build were exercised locally. Browser tests use mocked application APIs and a synthetic video embed; AI unit tests use a provider double. They do not establish real-provider playback, live AI quality, or deployed database integration.

The full `make verify` run stopped at Docker configuration interpolation because `AI_MEDIA_RELAY_SECRET` was absent. The performance contract tests passed (26), but the performance budget gate correctly rejected measurements taken before these source changes. Inventory checks passed (15). Do not mark the entire repository verification gate as passed. Deployment must apply the migration through the existing release workflow and complete environment-dependent verification, real AI calls, and physical-device playback checks.

## Admin-only activation amendment

All new learning tools are disabled by default on existing and new lessons. An admin enables individual tools after adding a video in the lesson setup, using **التفاعل والمراجعة**. Teacher routes contain no activation editor; backend role checks independently reject teacher authoring and AI-draft requests. Chapter aids have a separate admin-controlled switch. Playback gestures and speeds remain standard player controls.

Release scope: the owner explicitly selected `all` on 2026-09-13. The release includes frontend, backend, worker, and the migrator with all learning tools disabled until an admin opts in.
