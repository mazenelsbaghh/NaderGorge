START TRANSACTION;

CREATE TABLE "VideoPlaybackSessions" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "LessonVideoId" uuid NOT NULL,
    "SessionToken" text NOT NULL,
    "EncryptionKey" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "IsConsumed" boolean NOT NULL,
    "IpAddress" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_VideoPlaybackSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_VideoPlaybackSessions_lesson_videos_LessonVideoId" FOREIGN KEY ("LessonVideoId") REFERENCES lesson_videos ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_VideoPlaybackSessions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_VideoPlaybackSessions_LessonVideoId" ON "VideoPlaybackSessions" ("LessonVideoId");

CREATE INDEX "IX_VideoPlaybackSessions_UserId" ON "VideoPlaybackSessions" ("UserId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260325211038_AddVideoPlaybackSession', '8.0.11');

COMMIT;

