// The tracker is optional legacy state; learning and login activity remain authoritative.
export const studentActivityCte = `WITH student_activity AS (
    SELECT u."Id", GREATEST(
        u."CreatedAt",
        (SELECT t."LastActiveAt" FROM student_status_trackers t WHERE t."StudentId" = u."Id"),
        (SELECT MAX(d."LastUsedAt") FROM devices d WHERE d."UserId" = u."Id"),
        (SELECT MAX(COALESCE(w."UpdatedAt", w."CreatedAt")) FROM video_watch_events w
            WHERE w."UserId" = u."Id" AND
                (w."WatchCount" > 0 OR w."TimeWatchedInSeconds" > 0 OR w."ActualWatchedSeconds" > 0)),
        (SELECT MAX(e."CreatedAt") FROM student_exam_attempts e WHERE e."UserId" = u."Id"),
        (SELECT MAX(GREATEST(h."StartedAt", h."SubmittedAt")) FROM homework_submissions h
            WHERE h."StudentId" = u."Id")
    ) AS last_active_at
    FROM users u
    WHERE u."IsActive" AND NOT u."IsDeleted" AND EXISTS (
        SELECT 1 FROM user_roles r JOIN roles rol ON rol."Id" = r."RoleId"
        WHERE r."UserId" = u."Id" AND rol."Name" = 'Student'
    )
)`;

export const inactivityReason = 'Inactive for more than 7 days';

export const resolveActiveWarningsSql = `${studentActivityCte}
    UPDATE warning_events w SET "IsResolved" = TRUE,
        "ResolutionNotes" = 'Automatically resolved from recorded student activity.'
    FROM student_activity a
    WHERE w."StudentId" = a."Id" AND NOT w."IsResolved"
        AND w."TriggerReason" = $1 AND a.last_active_at >= NOW() - INTERVAL '7 days'`;

export const inactiveStudentsSql = `${studentActivityCte}
    SELECT a."Id" FROM student_activity a
    WHERE a.last_active_at < NOW() - INTERVAL '7 days'
        AND NOT EXISTS (SELECT 1 FROM warning_events w
            WHERE w."StudentId" = a."Id" AND NOT w."IsResolved" AND w."TriggerReason" = $1)`;
