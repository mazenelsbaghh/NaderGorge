\set ON_ERROR_STOP on
\if :{?apply}
\else
  \set apply false
\endif
\if :{?before_utc}
\else
  \echo 'Required: before_utc (reviewed historical cutoff, including timezone).'
  \quit 2
\endif
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
CREATE TEMP TABLE repair_scope ON COMMIT DROP AS
SELECT '086d664a-f784-4cdd-a0c8-09948619bef4'::uuid AS lesson_id,
       :'before_utc'::timestamptz AT TIME ZONE 'UTC' AS before_utc;
DO $$ BEGIN
  IF (SELECT before_utc > now() AT TIME ZONE 'UTC' FROM repair_scope) THEN
    RAISE EXCEPTION 'Historical cutoff cannot be in the future';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM lessons l JOIN repair_scope s ON l."Id" = s.lesson_id
      WHERE l."Title" = 'المحاضرة الاولي: بناء الدولة المصرية الجزء الاول') THEN
    RAISE EXCEPTION 'Expected first lesson was not found';
  END IF;
END $$;
CREATE TEMP TABLE repair_videos ON COMMIT DROP AS
SELECT v."Id" AS video_id, v."Title" AS title,
       COALESCE(NULLIF(a."DurationSeconds", 0), MAX(p."TrackingDurationSeconds")) AS duration,
       MIN(p."TrackingDurationSeconds") AS session_min, MAX(p."TrackingDurationSeconds") AS session_max,
       COUNT(p."TrackingDurationSeconds") AS duration_observations
FROM lesson_videos v JOIN repair_scope s ON v."LessonId" = s.lesson_id
LEFT JOIN bunny_video_assets a ON a."LessonVideoId" = v."Id" AND a."SourceState" = 0
LEFT JOIN "VideoPlaybackSessions" p ON p."LessonVideoId" = v."Id"
    AND p."CreatedAt" < s.before_utc AND p."TrackingDurationSeconds" > 0
WHERE v."IsActive" AND v."ArchiveMode" = 0
GROUP BY v."Id", v."Title", a."DurationSeconds";
DO $$ BEGIN
  IF (SELECT COUNT(*) FROM repair_videos) <> 4 OR EXISTS
    (SELECT 1 FROM repair_videos WHERE duration IS NULL OR duration <= 0
      OR duration_observations < 20 OR session_max - session_min > 1) THEN
    RAISE EXCEPTION 'Expected four videos with consistent historical duration evidence; inspect before proceeding';
  END IF;
END $$;
CREATE TEMP TABLE repair_students ON COMMIT DROP AS
SELECT DISTINCT w."UserId" AS user_id
FROM video_watch_events w JOIN repair_videos v ON v.video_id = w."LessonVideoId"
CROSS JOIN repair_scope s
WHERE w."CreatedAt" < s.before_utc
  AND (w."TimeWatchedInSeconds" > 0 OR w."ActualWatchedSeconds" > 0 OR w."WatchCount" > 0);
CREATE TEMP TABLE repair_rows ON COMMIT DROP AS
SELECT s.user_id, v.video_id, w."Id" AS previous_id,
       w."LearningWatchedSeconds" AS previous_learning, w."LearningDurationSeconds" AS previous_duration,
       COALESCE(NULLIF(a."DurationSeconds", 0), own.duration, NULLIF(w."LearningDurationSeconds", 0), v.duration) AS duration,
       GREATEST(COALESCE(w."LearningWatchedSeconds", 0), CEIL(0.95 * COALESCE(NULLIF(a."DurationSeconds", 0), own.duration, NULLIF(w."LearningDurationSeconds", 0), v.duration))) AS target_learning
FROM repair_students s CROSS JOIN repair_videos v
LEFT JOIN video_watch_events w ON w."UserId" = s.user_id AND w."LessonVideoId" = v.video_id
LEFT JOIN bunny_video_assets a ON a."LessonVideoId" = v.video_id AND a."SourceState" = 0
LEFT JOIN LATERAL (SELECT MAX(p."TrackingDurationSeconds") AS duration FROM "VideoPlaybackSessions" p
  WHERE p."UserId" = s.user_id AND p."LessonVideoId" = v.video_id AND p."TrackingDurationSeconds" > 0) own ON true;
-- Only raise insufficient rows. Actual time, quota count and playback sessions are never manufactured.
DELETE FROM repair_rows WHERE previous_learning >= target_learning AND previous_duration = duration;
SELECT title, duration, duration_observations, session_min, session_max FROM repair_videos ORDER BY title;
SELECT (SELECT COUNT(*) FROM repair_students) AS eligible_students,
       COUNT(DISTINCT user_id) AS students_to_update, COUNT(*) AS video_rows_to_update FROM repair_rows;
\if :apply
  \if :{?actor_id}
  \else
    \echo 'Required for apply: actor_id and expected_students from the reviewed dry run.'
    \quit 2
  \endif
  \if :{?expected_students}
  \else
    \echo 'Required for apply: expected_students from the reviewed dry run.'
    \quit 2
  \endif
  SELECT set_config('massar.repair_actor', :'actor_id', true),
         set_config('massar.repair_expected', :'expected_students', true);
  DO $$ BEGIN
    IF (SELECT COUNT(*) FROM repair_students) <> current_setting('massar.repair_expected')::int THEN
      RAISE EXCEPTION 'Audience changed since reviewed dry run';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM users WHERE "Id" = current_setting('massar.repair_actor')::uuid) THEN
      RAISE EXCEPTION 'Audit actor was not found';
    END IF;
  END $$;
  INSERT INTO audit_logs ("Id", "Action", "EntityType", "EntityId", "PerformedByUserId", "ActorType",
      "OldValues", "NewValues", "Reason", "CorrelationId", "CreatedAt")
  SELECT gen_random_uuid(), 'HistoricalLearningProgress95', 'VideoWatchEvent', r.previous_id,
      current_setting('massar.repair_actor')::uuid, 'User',
      jsonb_build_object('userId', r.user_id, 'videoId', r.video_id, 'learning', r.previous_learning, 'duration', r.previous_duration)::text,
      jsonb_build_object('learning', r.target_learning, 'duration', r.duration)::text,
      'Owner-requested Nader first lesson historical learning progress correction',
      'nader-first-lesson-progress-95', now() AT TIME ZONE 'UTC' FROM repair_rows r;
  INSERT INTO video_watch_events ("Id", "UserId", "LessonVideoId", "LearningWatchedSeconds", "LearningDurationSeconds",
      "TimeWatchedInSeconds", "ActualWatchedSeconds", "LastPlaybackRate", "PlaybackRateBreakdownJson", "WatchCount", "IsLocked", "CreatedAt")
  SELECT gen_random_uuid(), user_id, video_id, target_learning, duration, 0, 0, 1, '{}', 0, false, now() AT TIME ZONE 'UTC'
  FROM repair_rows
  ON CONFLICT ("UserId", "LessonVideoId") DO UPDATE SET
      "LearningWatchedSeconds" = GREATEST(video_watch_events."LearningWatchedSeconds", EXCLUDED."LearningWatchedSeconds"),
      "LearningDurationSeconds" = EXCLUDED."LearningDurationSeconds";
  COMMIT;
\else
  ROLLBACK;
\endif
