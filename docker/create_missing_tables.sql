DO $$ 
BEGIN 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'question_bank_items' AND column_name = 'AudioUrl') THEN 
        ALTER TABLE "question_bank_items" ADD COLUMN "AudioUrl" text; 
    END IF; 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'question_bank_items' AND column_name = 'HintText') THEN 
        ALTER TABLE "question_bank_items" ADD COLUMN "HintText" text; 
    END IF; 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'question_bank_items' AND column_name = 'WrittenCorrection') THEN 
        ALTER TABLE "question_bank_items" ADD COLUMN "WrittenCorrection" text; 
    END IF; 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'question_bank_items' AND column_name = 'BaseText') THEN 
        ALTER TABLE "question_bank_items" ADD COLUMN "BaseText" text; 
    END IF; 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'question_bank_items' AND column_name = 'MistakeEndIndex') THEN 
        ALTER TABLE "question_bank_items" ADD COLUMN "MistakeEndIndex" integer; 
    END IF; 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'question_bank_items' AND column_name = 'MistakeStartIndex') THEN 
        ALTER TABLE "question_bank_items" ADD COLUMN "MistakeStartIndex" integer; 
    END IF; 
END $$;

CREATE TABLE IF NOT EXISTS "PlatformSettings" (
    "Id" uuid NOT NULL,
    "Key" text NOT NULL,
    "Value" text NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    CONSTRAINT "PK_PlatformSettings" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "ExtraWatchRequests" (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "LessonVideoId" uuid NOT NULL,
    "RejectionReason" character varying(1000),
    "ResolvedAt" timestamp without time zone,
    "Status" integer NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "UserId" uuid NOT NULL,
    CONSTRAINT "PK_ExtraWatchRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ExtraWatchRequests_lesson_videos_LessonVideoId" FOREIGN KEY ("LessonVideoId") REFERENCES "lesson_videos" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ExtraWatchRequests_users_UserId" FOREIGN KEY ("UserId") REFERENCES "users" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ExtraWatchRequests_LessonVideoId" ON "ExtraWatchRequests" ("LessonVideoId");
CREATE INDEX IF NOT EXISTS "IX_ExtraWatchRequests_UserId" ON "ExtraWatchRequests" ("UserId");

CREATE TABLE IF NOT EXISTS "essay_submissions" (
    "Id" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "QuestionId" uuid NOT NULL,
    "StudentExamAttemptId" uuid NOT NULL,
    "SubmittedText" character varying(4000),
    "Status" integer NOT NULL,
    "AiInitialScore" numeric(18,2),
    "AiFeedback" text,
    "TeacherFinalScore" numeric(18,2),
    "TeacherFeedback" text,
    "SubmittedAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "AudioUrl" character varying(2000),
    CONSTRAINT "PK_essay_submissions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_essay_submissions_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES "users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_essay_submissions_question_bank_items_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES "question_bank_items" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_essay_submissions_student_exam_attempts_StudentExamAttemptId" FOREIGN KEY ("StudentExamAttemptId") REFERENCES "student_exam_attempts" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_essay_submissions_QuestionId" ON "essay_submissions" ("QuestionId");
CREATE INDEX IF NOT EXISTS "IX_essay_submissions_StudentExamAttemptId" ON "essay_submissions" ("StudentExamAttemptId");
CREATE INDEX IF NOT EXISTS "IX_essay_submissions_StudentId" ON "essay_submissions" ("StudentId");
