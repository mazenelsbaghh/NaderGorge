# Homework authoring controls, 2026-09-09

Owner selected build/deployment scope: `all`. Application changes affect frontend only; no database model or migration changes.

- Attached homework exposes its profile and an administrative preview. Teacher profiles use the teacher shell.
- Preview reads the existing authorized dashboard response and never starts or submits a student attempt. Model answers are hidden until requested.
- Each question links to its editor. Existing questions can be changed in the draft list, then persisted using the existing save command. Active homework and homework with attempts remain locked by the existing server policy and visible UI guards.
- Save failures retain edits and show an inline error.

Verification: `frontend/tests/browser/homework-authoring.mjs` covers admin and teacher navigation, preview without writes, selecting and editing the second question while preserving the first, failed-save recovery, and locks after attempts exist. WebKit uses mocked HTTP boundaries at a 390px viewport. No claim of physical-device testing.

Frontend lint/typecheck and the EF pending-model guard passed. Deployment outcome is recorded separately by the immutable production release tooling.
