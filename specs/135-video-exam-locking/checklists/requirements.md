# Requirements Checklist: Video Exam Locking & Lesson Cascade Lock

- [ ] A video with a mandatory exam is locked (`isExamLocked = true`) if the student has not passed its own exam.
- [ ] Subsequent videos in the same lesson are locked if any preceding video has an unpassed mandatory exam.
- [ ] Subsequent lessons are locked if any video in the preceding lesson has an unpassed mandatory exam.
- [ ] The lesson lock reason correctly identifies the unpassed video exam (in Arabic).
- [ ] The frontend exam button for the active video is enabled if it is only locked by its own exam, but disabled if locked by a preceding video exam.
- [ ] Pytest e2e content flow validates the entire lock and unlock progression.
