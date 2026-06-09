--
-- PostgreSQL database dump
--

\restrict ZS3qD5HbSBeFRGHmhsbRzbdKcpvdoO9TnbdSZLV260Cv2iQKiXQ3SiI1QSVRL6w

-- Dumped from database version 16.14
-- Dumped by pg_dump version 16.14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: StudentNotes; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."StudentNotes" (
    "Id" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "AdminId" uuid NOT NULL,
    "Content" text NOT NULL,
    "IsPinned" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public."StudentNotes" OWNER TO postgres;

--
-- Name: VideoPlaybackSessions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."VideoPlaybackSessions" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "LessonVideoId" uuid NOT NULL,
    "SessionToken" text NOT NULL,
    "EncryptionKey" text NOT NULL,
    "ExpiresAt" timestamp without time zone NOT NULL,
    "IsConsumed" boolean NOT NULL,
    "IpAddress" text,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public."VideoPlaybackSessions" OWNER TO postgres;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO postgres;

--
-- Name: access_code_activation_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.access_code_activation_logs (
    "Id" uuid NOT NULL,
    "AccessCodeId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "PackageId" uuid,
    "TeacherId" uuid NOT NULL,
    "Price" numeric(18,2) NOT NULL,
    "CommissionRate" numeric(18,2) NOT NULL,
    "CommissionEarned" numeric(18,2) NOT NULL,
    "ActivatedAt" timestamp without time zone NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.access_code_activation_logs OWNER TO postgres;

--
-- Name: access_codes; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.access_codes (
    "Id" uuid NOT NULL,
    "CodeHash" text NOT NULL,
    "CodePlaintext" text NOT NULL,
    "CodeGroupId" uuid NOT NULL,
    "IsConsumed" boolean NOT NULL,
    "ConsumedByUserId" uuid,
    "ConsumedAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "ExpiresAt" timestamp without time zone,
    "QrCodeUrl" text,
    "SerialNumber" bigint DEFAULT 0 NOT NULL
);


ALTER TABLE public.access_codes OWNER TO postgres;

--
-- Name: assistant_tasks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.assistant_tasks (
    "Id" uuid NOT NULL,
    "TaskType" integer NOT NULL,
    "ReferenceEntityId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "AssignedAssistantId" uuid,
    "Status" integer NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "CompletedAt" timestamp without time zone
);


ALTER TABLE public.assistant_tasks OWNER TO postgres;

--
-- Name: attendance_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.attendance_logs (
    "Id" uuid NOT NULL,
    "EmployeeId" uuid NOT NULL,
    "Date" date NOT NULL,
    "ClockIn" timestamp without time zone NOT NULL,
    "ClockOut" timestamp without time zone,
    "LateMinutes" integer NOT NULL,
    "Status" integer NOT NULL,
    "IpAddress" character varying(45) NOT NULL,
    "UserAgent" character varying(500) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.attendance_logs OWNER TO postgres;

--
-- Name: audit_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.audit_logs (
    "Id" uuid NOT NULL,
    "Action" character varying(100) NOT NULL,
    "EntityType" character varying(100) NOT NULL,
    "EntityId" uuid,
    "PerformedByUserId" uuid,
    "OldValues" text,
    "NewValues" text,
    "IpAddress" character varying(45),
    "CorrelationId" character varying(64),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.audit_logs OWNER TO postgres;

--
-- Name: balance_transactions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.balance_transactions (
    "Id" uuid NOT NULL,
    "StudentBalanceId" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "BalanceAfter" numeric(18,2) NOT NULL,
    "TransactionType" character varying(50) NOT NULL,
    "ReferenceId" uuid,
    "Description" character varying(500) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "PerformedByUserId" uuid
);


ALTER TABLE public.balance_transactions OWNER TO postgres;

--
-- Name: chat_message_read_states; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.chat_message_read_states (
    "MessageId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ReadAt" timestamp without time zone NOT NULL
);


ALTER TABLE public.chat_message_read_states OWNER TO postgres;

--
-- Name: chat_messages; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.chat_messages (
    "Id" uuid NOT NULL,
    "ChatRoomId" uuid NOT NULL,
    "SenderUserId" uuid NOT NULL,
    "Content" character varying(4000) NOT NULL,
    "Type" integer NOT NULL,
    "MediaUrl" character varying(2048),
    "MediaMetadata" character varying(4000),
    "IsPinned" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.chat_messages OWNER TO postgres;

--
-- Name: chat_participants; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.chat_participants (
    "ChatRoomId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "JoinedAt" timestamp without time zone NOT NULL,
    "LastReadMessageId" uuid
);


ALTER TABLE public.chat_participants OWNER TO postgres;

--
-- Name: chat_rooms; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.chat_rooms (
    "Id" uuid NOT NULL,
    "Name" character varying(100),
    "Type" integer NOT NULL,
    "TaskItemId" uuid,
    "IsArchived" boolean NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.chat_rooms OWNER TO postgres;

--
-- Name: code_groups; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.code_groups (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "TotalCodes" integer NOT NULL,
    "PackageId" uuid,
    "LessonId" uuid,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "BalanceAmount" numeric(18,2),
    "CodeType" integer DEFAULT 0 NOT NULL,
    "ContentSectionId" uuid,
    "DiscountPercentage" numeric(18,2),
    "ExamId" uuid,
    "ExpiresAt" timestamp without time zone,
    "QrDataGenerated" boolean DEFAULT false NOT NULL,
    "TermId" uuid,
    "TeacherId" uuid DEFAULT 'b4b82937-293e-48a3-a002-decf9a1efab8'::uuid NOT NULL
);


ALTER TABLE public.code_groups OWNER TO postgres;

--
-- Name: code_video_targets; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.code_video_targets (
    "Id" uuid NOT NULL,
    "CodeGroupId" uuid NOT NULL,
    "LessonVideoId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.code_video_targets OWNER TO postgres;

--
-- Name: community_post_comments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.community_post_comments (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "AuthorUserId" uuid NOT NULL,
    "Body" character varying(2000) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "RejectionReason" character varying(1000),
    "ReviewedAt" timestamp without time zone,
    "ReviewedByUserId" uuid,
    "Status" integer DEFAULT 0 NOT NULL
);


ALTER TABLE public.community_post_comments OWNER TO postgres;

--
-- Name: community_post_likes; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.community_post_likes (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.community_post_likes OWNER TO postgres;

--
-- Name: community_post_poll_options; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.community_post_poll_options (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "Text" character varying(200) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.community_post_poll_options OWNER TO postgres;

--
-- Name: community_post_poll_votes; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.community_post_poll_votes (
    "Id" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "PollOptionId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.community_post_poll_votes OWNER TO postgres;

--
-- Name: community_posts; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.community_posts (
    "Id" uuid NOT NULL,
    "AuthorUserId" uuid NOT NULL,
    "Body" character varying(4000) NOT NULL,
    "Status" integer NOT NULL,
    "ReviewedAt" timestamp without time zone,
    "ReviewedByUserId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "IsPoll" boolean DEFAULT false NOT NULL
);


ALTER TABLE public.community_posts OWNER TO postgres;

--
-- Name: content_sections; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.content_sections (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Order" integer NOT NULL,
    "TermId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Price" numeric DEFAULT 0.0 NOT NULL
);


ALTER TABLE public.content_sections OWNER TO postgres;

--
-- Name: crm_call_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.crm_call_logs (
    "Id" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "AgentId" uuid NOT NULL,
    "CallDate" timestamp without time zone NOT NULL,
    "Outcome" integer NOT NULL,
    "Notes" character varying(4000),
    "NextFollowUpDate" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.crm_call_logs OWNER TO postgres;

--
-- Name: crm_student_statuses; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.crm_student_statuses (
    "StudentId" uuid NOT NULL,
    "Status" integer NOT NULL,
    "AssignedAgentId" uuid,
    "Priority" integer NOT NULL,
    "NextFollowUpDate" timestamp without time zone,
    "LastCalledAt" timestamp without time zone,
    "Notes" character varying(4000)
);


ALTER TABLE public.crm_student_statuses OWNER TO postgres;

--
-- Name: custom_forms; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.custom_forms (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" character varying(2000) NOT NULL,
    "Slug" character varying(100) NOT NULL,
    "IsActive" boolean NOT NULL,
    "FieldsJson" text NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "VisitCount" integer DEFAULT 0 NOT NULL,
    "CoverImageUrl" text,
    "ExpiresAt" timestamp without time zone,
    "StartsAt" timestamp without time zone
);


ALTER TABLE public.custom_forms OWNER TO postgres;

--
-- Name: devices; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.devices (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "DeviceFingerprint" text NOT NULL,
    "DeviceName" text,
    "IpAddress" text,
    "LastUsedAt" timestamp without time zone NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "BrowserName" text,
    "DeviceType" text,
    "OsName" text
);


ALTER TABLE public.devices OWNER TO postgres;

--
-- Name: employee_profiles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.employee_profiles (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "BasicSalary" numeric(18,2) NOT NULL,
    "StandardStartTime" interval NOT NULL,
    "TargetDailyHours" integer NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.employee_profiles OWNER TO postgres;

--
-- Name: employee_vacations; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.employee_vacations (
    "Id" uuid NOT NULL,
    "EmployeeId" uuid NOT NULL,
    "StartDate" date NOT NULL,
    "EndDate" date NOT NULL,
    "Status" integer NOT NULL,
    "Reason" character varying(2000) NOT NULL,
    "HandledBy" uuid,
    "HandledAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.employee_vacations OWNER TO postgres;

--
-- Name: essay_submissions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.essay_submissions (
    "Id" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "QuestionId" uuid NOT NULL,
    "StudentExamAttemptId" uuid NOT NULL,
    "AnswerText" text NOT NULL,
    "AudioUrl" character varying(2000),
    "AiInitialScore" numeric(18,2),
    "AiFeedback" text,
    "TeacherFinalScore" numeric(18,2),
    "TeacherFeedback" text,
    "Status" integer NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "GradedByTeacherId" uuid
);


ALTER TABLE public.essay_submissions OWNER TO postgres;

--
-- Name: exam_questions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.exam_questions (
    "Id" uuid NOT NULL,
    "ExamId" uuid NOT NULL,
    "QuestionBankItemId" uuid NOT NULL,
    "Order" integer NOT NULL,
    "Points" numeric(18,2) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.exam_questions OWNER TO postgres;

--
-- Name: exams; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.exams (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" text NOT NULL,
    "PassingScore" numeric(18,2) NOT NULL,
    "TotalScore" numeric(18,2) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "DurationMinutes" integer,
    "IsMandatory" boolean DEFAULT false NOT NULL,
    "IsRandomized" boolean DEFAULT false NOT NULL,
    "DisplayQuestionCount" integer,
    "CreatedByTeacherId" uuid DEFAULT 'b4b82937-293e-48a3-a002-decf9a1efab8'::uuid NOT NULL
);


ALTER TABLE public.exams OWNER TO postgres;

--
-- Name: form_submissions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.form_submissions (
    "Id" uuid NOT NULL,
    "CustomFormId" uuid NOT NULL,
    "SubmittedDataJson" text NOT NULL,
    "Status" integer NOT NULL,
    "AdminNotes" character varying(2000),
    "SubmittedAt" timestamp without time zone NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.form_submissions OWNER TO postgres;

--
-- Name: gamification_action_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.gamification_action_logs (
    "Id" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "EventType" integer NOT NULL,
    "PointsAwarded" integer NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL
);


ALTER TABLE public.gamification_action_logs OWNER TO postgres;

--
-- Name: homework_answers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.homework_answers (
    "Id" uuid NOT NULL,
    "HomeworkSubmissionId" uuid NOT NULL,
    "QuestionId" uuid NOT NULL,
    "ProvidedAnswer" text NOT NULL,
    "ScoreReceived" integer
);


ALTER TABLE public.homework_answers OWNER TO postgres;

--
-- Name: homework_questions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.homework_questions (
    "Id" uuid NOT NULL,
    "HomeworkId" uuid NOT NULL,
    "Order" integer NOT NULL,
    "QuestionType" integer NOT NULL,
    "BodyText" text NOT NULL,
    "PossibleAnswers" text[],
    "CorrectAnswerKey" text,
    "PointsActive" integer NOT NULL
);


ALTER TABLE public.homework_questions OWNER TO postgres;

--
-- Name: homework_submissions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.homework_submissions (
    "Id" uuid NOT NULL,
    "HomeworkId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "StartedAt" timestamp without time zone NOT NULL,
    "SubmittedAt" timestamp without time zone,
    "GradedAt" timestamp without time zone,
    "Status" integer NOT NULL,
    "AssistantReviewerId" uuid,
    "AssistantNotes" text,
    "OverallScore" numeric(18,2) NOT NULL,
    "Evaluation" text
);


ALTER TABLE public.homework_submissions OWNER TO postgres;

--
-- Name: homeworks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.homeworks (
    "Id" uuid NOT NULL,
    "LessonId" uuid NOT NULL,
    "Title" character varying(255) NOT NULL,
    "Description" text,
    "IsMandatory" boolean NOT NULL,
    "PassingScoreThreshold" numeric(18,2),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    "TotalScore" numeric DEFAULT 0.0 NOT NULL,
    "IsRandomized" boolean DEFAULT false NOT NULL
);


ALTER TABLE public.homeworks OWNER TO postgres;

--
-- Name: lesson_comments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.lesson_comments (
    "Id" uuid NOT NULL,
    "LessonId" uuid NOT NULL,
    "AuthorUserId" uuid NOT NULL,
    "Body" character varying(2000) NOT NULL,
    "Status" integer NOT NULL,
    "ReviewedAt" timestamp without time zone,
    "ReviewedByUserId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.lesson_comments OWNER TO postgres;

--
-- Name: lesson_progress; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.lesson_progress (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "LessonId" uuid NOT NULL,
    "IsCompleted" boolean NOT NULL,
    "IsManuallyUnlocked" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.lesson_progress OWNER TO postgres;

--
-- Name: lesson_resources; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.lesson_resources (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "FileUrl" text NOT NULL,
    "ResourceType" text NOT NULL,
    "LessonId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.lesson_resources OWNER TO postgres;

--
-- Name: lesson_videos; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.lesson_videos (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Provider" text NOT NULL,
    "ProviderVideoId" text NOT NULL,
    "Order" integer NOT NULL,
    "MaxWatchCount" integer NOT NULL,
    "LessonId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "VideoTag" text,
    "ExamId" uuid,
    "IsProcessingAI" boolean DEFAULT false NOT NULL,
    "SubtitleUrl" text,
    "IsProcessingMindmaps" boolean DEFAULT false NOT NULL
);


ALTER TABLE public.lesson_videos OWNER TO postgres;

--
-- Name: lessons; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.lessons (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Summary" text NOT NULL,
    "Order" integer NOT NULL,
    "ContentSectionId" uuid NOT NULL,
    "ExamId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Price" numeric DEFAULT 0.0 NOT NULL
);


ALTER TABLE public.lessons OWNER TO postgres;

--
-- Name: media_production_pipelines; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.media_production_pipelines (
    "Id" uuid NOT NULL,
    "Title" character varying(250) NOT NULL,
    "Description" character varying(2000),
    "Stage" integer NOT NULL,
    "AssignedAgentId" uuid,
    "AssetFolderUrl" character varying(2000),
    "EditingErrorCount" integer NOT NULL,
    "PublishedAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.media_production_pipelines OWNER TO postgres;

--
-- Name: notification_events; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.notification_events (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ChannelType" integer NOT NULL,
    "Title" text NOT NULL,
    "Body" text NOT NULL,
    "Status" integer NOT NULL,
    "ReadAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL
);


ALTER TABLE public.notification_events OWNER TO postgres;

--
-- Name: packages; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.packages (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" text NOT NULL,
    "Price" numeric NOT NULL,
    "ProgramId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "IsActive" boolean DEFAULT false NOT NULL,
    "TeacherId" uuid DEFAULT 'b4b82937-293e-48a3-a002-decf9a1efab8'::uuid NOT NULL
);


ALTER TABLE public.packages OWNER TO postgres;

--
-- Name: payroll_adjustments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.payroll_adjustments (
    "Id" uuid NOT NULL,
    "PayrollRecordId" uuid NOT NULL,
    "Type" integer NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Reason" character varying(2000) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.payroll_adjustments OWNER TO postgres;

--
-- Name: payroll_records; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.payroll_records (
    "Id" uuid NOT NULL,
    "EmployeeProfileId" uuid NOT NULL,
    "Month" integer NOT NULL,
    "Year" integer NOT NULL,
    "BasicSalary" numeric(18,2) NOT NULL,
    "Status" integer NOT NULL,
    "ApprovedByUserId" uuid,
    "ApprovedAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.payroll_records OWNER TO postgres;

--
-- Name: programs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.programs (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" text NOT NULL,
    "TargetGrade" text NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "SubjectId" uuid DEFAULT 'd9b8a342-990a-4286-905e-fdebb2e3895e'::uuid NOT NULL
);


ALTER TABLE public.programs OWNER TO postgres;

--
-- Name: question_bank_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.question_bank_items (
    "Id" uuid NOT NULL,
    "Text" text NOT NULL,
    "DefaultPoints" numeric(18,2) NOT NULL,
    "Tags" character varying(500) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Type" integer DEFAULT 0 NOT NULL,
    "CreatedByTeacherId" uuid DEFAULT 'b4b82937-293e-48a3-a002-decf9a1efab8'::uuid NOT NULL,
    "SubjectId" uuid DEFAULT 'd9b8a342-990a-4286-905e-fdebb2e3895e'::uuid NOT NULL
);


ALTER TABLE public.question_bank_items OWNER TO postgres;

--
-- Name: question_options; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.question_options (
    "Id" uuid NOT NULL,
    "Text" text NOT NULL,
    "IsCorrect" boolean NOT NULL,
    "QuestionBankItemId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.question_options OWNER TO postgres;

--
-- Name: refresh_tokens; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.refresh_tokens (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Token" text NOT NULL,
    "ExpiresAt" timestamp without time zone NOT NULL,
    "IsRevoked" boolean NOT NULL,
    "DeviceFingerprint" text,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.refresh_tokens OWNER TO postgres;

--
-- Name: roles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.roles (
    "Id" uuid NOT NULL,
    "Name" character varying(50) NOT NULL,
    "Type" integer NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "PermissionsJson" character varying(4000) DEFAULT '[]'::character varying
);


ALTER TABLE public.roles OWNER TO postgres;

--
-- Name: social_media_plans; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.social_media_plans (
    "Id" uuid NOT NULL,
    "Title" character varying(250) NOT NULL,
    "Description" character varying(2000),
    "Script" character varying(4000),
    "Platform" integer NOT NULL,
    "Status" integer NOT NULL,
    "ScheduledDate" timestamp without time zone NOT NULL,
    "MediaProductionPipelineId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.social_media_plans OWNER TO postgres;

--
-- Name: student_access_grants; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.student_access_grants (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "PackageId" uuid,
    "LessonId" uuid,
    "AccessCodeId" uuid,
    "GrantedAt" timestamp without time zone NOT NULL,
    "ExpiresAt" timestamp without time zone,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "ContentSectionId" uuid,
    "ExamId" uuid,
    "GrantType" integer DEFAULT 0 NOT NULL,
    "LessonVideoId" uuid,
    "TermId" uuid
);


ALTER TABLE public.student_access_grants OWNER TO postgres;

--
-- Name: student_answers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.student_answers (
    "Id" uuid NOT NULL,
    "StudentExamAttemptId" uuid NOT NULL,
    "ExamQuestionId" uuid NOT NULL,
    "SelectedOptionId" uuid,
    "IsCorrect" boolean NOT NULL,
    "PointsAwarded" numeric(18,2) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "SubmittedText" character varying(2000),
    "HintUsed" boolean DEFAULT false NOT NULL
);


ALTER TABLE public.student_answers OWNER TO postgres;

--
-- Name: student_badges; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.student_badges (
    "Id" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "BadgeName" text NOT NULL,
    "UnlockedAt" timestamp without time zone NOT NULL
);


ALTER TABLE public.student_badges OWNER TO postgres;

--
-- Name: student_balances; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.student_balances (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "CurrentBalance" numeric(18,2) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.student_balances OWNER TO postgres;

--
-- Name: student_exam_attempts; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.student_exam_attempts (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ExamId" uuid NOT NULL,
    "ScoreAchieved" numeric(18,2) NOT NULL,
    "IsPassed" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Evaluation" text,
    "IsTimeExpired" boolean DEFAULT false NOT NULL,
    "StartedAt" timestamp without time zone
);


ALTER TABLE public.student_exam_attempts OWNER TO postgres;

--
-- Name: student_gamifications; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.student_gamifications (
    "StudentId" uuid NOT NULL,
    "TotalPoints" integer NOT NULL,
    "CurrentStreakCount" integer NOT NULL,
    "LongestStreakCount" integer NOT NULL,
    "LastTaskCompletedAt" timestamp without time zone,
    "LevelName" text NOT NULL
);


ALTER TABLE public.student_gamifications OWNER TO postgres;

--
-- Name: student_profiles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.student_profiles (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ParentPhone" character varying(20),
    "Governorate" character varying(100) DEFAULT ''::character varying NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Address" character varying(500) DEFAULT ''::character varying NOT NULL,
    "DateOfBirth" timestamp without time zone DEFAULT '-infinity'::timestamp with time zone NOT NULL,
    "EducationStage" integer DEFAULT 0 NOT NULL,
    "Gender" integer DEFAULT 0 NOT NULL,
    "GradeLevel" integer DEFAULT 0 NOT NULL,
    "IsFatherAlive" boolean DEFAULT false NOT NULL,
    "IsMotherAlive" boolean DEFAULT false NOT NULL,
    "StudentCode" character varying(100) DEFAULT ''::character varying,
    "StudyTrack" integer,
    "District" character varying(200),
    "SecondaryParentPhone" character varying(20),
    "SecondaryPhone" character varying(20),
    "FatherDateOfBirth" timestamp without time zone,
    "MotherDateOfBirth" timestamp without time zone,
    "MotherPhone" text,
    "Nationality" text,
    "SchoolName" text,
    "SchoolType" integer,
    "DarkThemePaletteId" character varying(100),
    "LightThemePaletteId" character varying(100),
    "CurrentMode" character varying(10) DEFAULT 'light'::character varying NOT NULL,
    "AvatarSlug" text
);


ALTER TABLE public.student_profiles OWNER TO postgres;

--
-- Name: student_status_trackers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.student_status_trackers (
    "StudentId" uuid NOT NULL,
    "CurrentStatus" integer NOT NULL,
    "ConsecutiveMissedHomeworks" integer NOT NULL,
    "ConsecutiveFailedExams" integer NOT NULL,
    "LastActiveAt" timestamp without time zone,
    "LastEvaluatedAt" timestamp without time zone NOT NULL
);


ALTER TABLE public.student_status_trackers OWNER TO postgres;

--
-- Name: subjects; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.subjects (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "NormalizedName" character varying(200) NOT NULL,
    "Description" text NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.subjects OWNER TO postgres;

--
-- Name: task_comments; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.task_comments (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Content" character varying(4000) NOT NULL,
    "AttachmentUrl" character varying(2048),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.task_comments OWNER TO postgres;

--
-- Name: task_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.task_items (
    "Id" uuid NOT NULL,
    "Title" character varying(255) NOT NULL,
    "Description" character varying(4000) NOT NULL,
    "AssigneeId" uuid NOT NULL,
    "CreatedById" uuid NOT NULL,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "DueDate" timestamp without time zone,
    "CompletedAt" timestamp without time zone,
    "ApprovedById" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "MediaPipelineId" uuid
);


ALTER TABLE public.task_items OWNER TO postgres;

--
-- Name: teacher_accounts; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.teacher_accounts (
    "Id" uuid NOT NULL,
    "TeacherId" uuid NOT NULL,
    "TotalEarnings" numeric(18,2) NOT NULL,
    "CurrentBalance" numeric(18,2) NOT NULL,
    "CommissionRate" numeric(18,2) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.teacher_accounts OWNER TO postgres;

--
-- Name: teacher_payouts; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.teacher_payouts (
    "Id" uuid NOT NULL,
    "TeacherId" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Status" integer NOT NULL,
    "RejectionReason" character varying(2000),
    "HandledByUserId" uuid,
    "HandledAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.teacher_payouts OWNER TO postgres;

--
-- Name: teacher_photos; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.teacher_photos (
    "Id" uuid NOT NULL,
    "TeacherId" uuid NOT NULL,
    "FileUrl" character varying(2000) NOT NULL,
    "IsActive" boolean NOT NULL,
    "UploadedAt" timestamp without time zone NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.teacher_photos OWNER TO postgres;

--
-- Name: teacher_profiles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.teacher_profiles (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Bio" text NOT NULL,
    "Specialization" character varying(200) NOT NULL,
    "CommissionRate" numeric(18,2) NOT NULL,
    "ProfileImageUrl" character varying(1000),
    "ContactInfo" character varying(500) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.teacher_profiles OWNER TO postgres;

--
-- Name: teacher_subjects; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.teacher_subjects (
    "TeacherId" uuid NOT NULL,
    "SubjectId" uuid NOT NULL
);


ALTER TABLE public.teacher_subjects OWNER TO postgres;

--
-- Name: terms; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.terms (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Order" integer NOT NULL,
    "PackageId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Price" numeric DEFAULT 0.0 NOT NULL
);


ALTER TABLE public.terms OWNER TO postgres;

--
-- Name: user_roles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_roles (
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL
);


ALTER TABLE public.user_roles OWNER TO postgres;

--
-- Name: users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users (
    "Id" uuid NOT NULL,
    "FullName" character varying(200) NOT NULL,
    "PhoneNumber" character varying(20) NOT NULL,
    "PasswordHash" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "IsProfileComplete" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "SuspensionReason" text
);


ALTER TABLE public.users OWNER TO postgres;

--
-- Name: video_chapters; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.video_chapters (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "StartTime" integer NOT NULL,
    "EndTime" integer NOT NULL,
    "SummaryText" character varying(2000) NOT NULL,
    "Order" integer NOT NULL,
    "LessonVideoId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "MindmapImageUrl" character varying(2000)
);


ALTER TABLE public.video_chapters OWNER TO postgres;

--
-- Name: video_overrides; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.video_overrides (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "LessonVideoId" uuid NOT NULL,
    "OriginalLimit" integer NOT NULL,
    "NewLimit" integer NOT NULL,
    "AddedViews" integer NOT NULL,
    "Reason" text NOT NULL,
    "PerformedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone
);


ALTER TABLE public.video_overrides OWNER TO postgres;

--
-- Name: video_watch_events; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.video_watch_events (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "LessonVideoId" uuid NOT NULL,
    "TimeWatchedInSeconds" integer NOT NULL,
    "WatchCount" integer NOT NULL,
    "IsLocked" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "CustomMaxWatchCount" integer
);


ALTER TABLE public.video_watch_events OWNER TO postgres;

--
-- Name: warning_events; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.warning_events (
    "Id" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "Severity" integer NOT NULL,
    "TriggerReason" text NOT NULL,
    "IsResolved" boolean NOT NULL,
    "ResolvedByAssistantId" uuid,
    "ResolutionNotes" text,
    "CreatedAt" timestamp without time zone NOT NULL
);


ALTER TABLE public.warning_events OWNER TO postgres;

--
-- Data for Name: StudentNotes; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."StudentNotes" ("Id", "StudentId", "AdminId", "Content", "IsPinned", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: VideoPlaybackSessions; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."VideoPlaybackSessions" ("Id", "UserId", "LessonVideoId", "SessionToken", "EncryptionKey", "ExpiresAt", "IsConsumed", "IpAddress", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20260323154931_InitialCreate	9.0.6
20260323155605_AddUS1CodeEntities	9.0.6
20260323160227_AddUS2ContentTrackingEntities	9.0.6
20260323161056_AddUS3ExamEntities	9.0.6
20260325211038_AddVideoPlaybackSession	9.0.6
20260326000830_AddPhase2AcademicOps	9.0.6
20260328000545_AddPhase3TermsAndCodes	9.0.6
20260328021623_AddRegistrationFieldUpdates	9.0.6
20260328034522_AddPackageIsActive	9.0.6
20260328041251_AddContentPricing	9.0.6
20260328045309_InlineExamsAndQuestions	9.0.6
20260328050813_AssessmentGradingUpdate	9.0.6
20260328052701_AddExamTimersAndDashboard	9.0.6
20260328061908_AddTimePerQuestionSecondsToExam	9.0.6
20260330040115_StudentProfileV2	9.0.6
20260331131211_UnifiedAssessmentBuilder	9.0.6
20260331173238_AddVideoChapters	9.0.6
20260401114742_AddChapterMindmapGeneration	9.0.6
20260401120228_AddMindmapImageUrlToVideoChapters	9.0.6
20260401121132_AddIsProcessingMindmapsToLessonVideo	9.0.6
20260408164959_AddLessonCommentsModeration	9.0.6
20260408171549_AddStudentCommunity	9.0.6
20260408175220_AddStudentThemePreferences	9.0.6
20260409141216_AddCommunityCommentModerationAndCriticalExamFixes	9.0.6
20260418174224_RemoveQuestionDuration	9.0.6
20260419214734_AddExamDisplayQuestionCount	9.0.6
20260601181311_AddStudentAvatarSlug	9.0.6
20260601200420_AddCustomFormsAndSubmissions	9.0.6
20260603181708_RemoveBunnyTelegramProviders	9.0.6
20260603201648_AddCustomFormVisitCount	9.0.6
20260604174309_AddStudentNotes	9.0.6
20260604180603_AddSuspensionReasonToUser	9.0.6
20260606163117_AddHintUsedToStudentAnswers	9.0.6
20260606165745_AddCustomMaxWatchCountToVideoWatchEvent	9.0.6
20260606170709_AddDeviceOsBrowserType	9.0.6
20260606172029_AddPerformedByToBalanceTransaction	9.0.6
20260606174246_AddVideoOverridesTable	9.0.6
20260606190227_AddPermissionsToRole	9.0.6
20260606195430_AddFormCoverImageUrl	9.0.6
20260606195712_AddFormStartsAndExpiresDates	9.0.6
20260607173303_AddSerialNumberToAccessCode	9.0.6
20260607190835_AddSupervisorAndStaffRoles	9.0.6
20260607191807_AddHREntities	9.0.6
20260607193635_AddOperationsTaskEntities	9.0.6
20260607200637_AddMultiTeacherSubjectArchitecture	9.0.6
20260609005123_AddChatEntities	9.0.6
20260609010613_AddCrmEntities	9.0.6
20260609012530_AddMediaEntities	9.0.6
20260609014519_AddPayrollAndTeacherFinance	9.0.6
\.


--
-- Data for Name: access_code_activation_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.access_code_activation_logs ("Id", "AccessCodeId", "StudentId", "PackageId", "TeacherId", "Price", "CommissionRate", "CommissionEarned", "ActivatedAt", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: access_codes; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.access_codes ("Id", "CodeHash", "CodePlaintext", "CodeGroupId", "IsConsumed", "ConsumedByUserId", "ConsumedAt", "CreatedAt", "UpdatedAt", "ExpiresAt", "QrCodeUrl", "SerialNumber") FROM stdin;
\.


--
-- Data for Name: assistant_tasks; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.assistant_tasks ("Id", "TaskType", "ReferenceEntityId", "StudentId", "AssignedAssistantId", "Status", "CreatedAt", "CompletedAt") FROM stdin;
\.


--
-- Data for Name: attendance_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.attendance_logs ("Id", "EmployeeId", "Date", "ClockIn", "ClockOut", "LateMinutes", "Status", "IpAddress", "UserAgent", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: audit_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.audit_logs ("Id", "Action", "EntityType", "EntityId", "PerformedByUserId", "OldValues", "NewValues", "IpAddress", "CorrelationId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: balance_transactions; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.balance_transactions ("Id", "StudentBalanceId", "Amount", "BalanceAfter", "TransactionType", "ReferenceId", "Description", "CreatedAt", "UpdatedAt", "PerformedByUserId") FROM stdin;
\.


--
-- Data for Name: chat_message_read_states; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.chat_message_read_states ("MessageId", "UserId", "ReadAt") FROM stdin;
\.


--
-- Data for Name: chat_messages; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.chat_messages ("Id", "ChatRoomId", "SenderUserId", "Content", "Type", "MediaUrl", "MediaMetadata", "IsPinned", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: chat_participants; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.chat_participants ("ChatRoomId", "UserId", "JoinedAt", "LastReadMessageId") FROM stdin;
\.


--
-- Data for Name: chat_rooms; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.chat_rooms ("Id", "Name", "Type", "TaskItemId", "IsArchived", "CreatedByUserId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: code_groups; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.code_groups ("Id", "Name", "TotalCodes", "PackageId", "LessonId", "CreatedByUserId", "CreatedAt", "UpdatedAt", "BalanceAmount", "CodeType", "ContentSectionId", "DiscountPercentage", "ExamId", "ExpiresAt", "QrDataGenerated", "TermId", "TeacherId") FROM stdin;
\.


--
-- Data for Name: code_video_targets; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.code_video_targets ("Id", "CodeGroupId", "LessonVideoId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: community_post_comments; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.community_post_comments ("Id", "PostId", "AuthorUserId", "Body", "CreatedAt", "UpdatedAt", "RejectionReason", "ReviewedAt", "ReviewedByUserId", "Status") FROM stdin;
\.


--
-- Data for Name: community_post_likes; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.community_post_likes ("Id", "PostId", "UserId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: community_post_poll_options; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.community_post_poll_options ("Id", "PostId", "Text", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: community_post_poll_votes; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.community_post_poll_votes ("Id", "PostId", "PollOptionId", "UserId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: community_posts; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.community_posts ("Id", "AuthorUserId", "Body", "Status", "ReviewedAt", "ReviewedByUserId", "CreatedAt", "UpdatedAt", "IsPoll") FROM stdin;
\.


--
-- Data for Name: content_sections; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.content_sections ("Id", "Title", "Order", "TermId", "CreatedAt", "UpdatedAt", "Price") FROM stdin;
\.


--
-- Data for Name: crm_call_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.crm_call_logs ("Id", "StudentId", "AgentId", "CallDate", "Outcome", "Notes", "NextFollowUpDate", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: crm_student_statuses; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.crm_student_statuses ("StudentId", "Status", "AssignedAgentId", "Priority", "NextFollowUpDate", "LastCalledAt", "Notes") FROM stdin;
\.


--
-- Data for Name: custom_forms; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.custom_forms ("Id", "Title", "Description", "Slug", "IsActive", "FieldsJson", "CreatedAt", "UpdatedAt", "VisitCount", "CoverImageUrl", "ExpiresAt", "StartsAt") FROM stdin;
\.


--
-- Data for Name: devices; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.devices ("Id", "UserId", "DeviceFingerprint", "DeviceName", "IpAddress", "LastUsedAt", "IsActive", "CreatedAt", "UpdatedAt", "BrowserName", "DeviceType", "OsName") FROM stdin;
\.


--
-- Data for Name: employee_profiles; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.employee_profiles ("Id", "UserId", "BasicSalary", "StandardStartTime", "TargetDailyHours", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: employee_vacations; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.employee_vacations ("Id", "EmployeeId", "StartDate", "EndDate", "Status", "Reason", "HandledBy", "HandledAt", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: essay_submissions; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.essay_submissions ("Id", "StudentId", "QuestionId", "StudentExamAttemptId", "AnswerText", "AudioUrl", "AiInitialScore", "AiFeedback", "TeacherFinalScore", "TeacherFeedback", "Status", "CreatedAt", "UpdatedAt", "GradedByTeacherId") FROM stdin;
\.


--
-- Data for Name: exam_questions; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.exam_questions ("Id", "ExamId", "QuestionBankItemId", "Order", "Points", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: exams; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.exams ("Id", "Title", "Description", "PassingScore", "TotalScore", "CreatedAt", "UpdatedAt", "DurationMinutes", "IsMandatory", "IsRandomized", "DisplayQuestionCount", "CreatedByTeacherId") FROM stdin;
\.


--
-- Data for Name: form_submissions; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.form_submissions ("Id", "CustomFormId", "SubmittedDataJson", "Status", "AdminNotes", "SubmittedAt", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: gamification_action_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.gamification_action_logs ("Id", "StudentId", "EventType", "PointsAwarded", "CreatedAt") FROM stdin;
\.


--
-- Data for Name: homework_answers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.homework_answers ("Id", "HomeworkSubmissionId", "QuestionId", "ProvidedAnswer", "ScoreReceived") FROM stdin;
\.


--
-- Data for Name: homework_questions; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.homework_questions ("Id", "HomeworkId", "Order", "QuestionType", "BodyText", "PossibleAnswers", "CorrectAnswerKey", "PointsActive") FROM stdin;
\.


--
-- Data for Name: homework_submissions; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.homework_submissions ("Id", "HomeworkId", "StudentId", "StartedAt", "SubmittedAt", "GradedAt", "Status", "AssistantReviewerId", "AssistantNotes", "OverallScore", "Evaluation") FROM stdin;
\.


--
-- Data for Name: homeworks; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.homeworks ("Id", "LessonId", "Title", "Description", "IsMandatory", "PassingScoreThreshold", "CreatedAt", "UpdatedAt", "TotalScore", "IsRandomized") FROM stdin;
\.


--
-- Data for Name: lesson_comments; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.lesson_comments ("Id", "LessonId", "AuthorUserId", "Body", "Status", "ReviewedAt", "ReviewedByUserId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: lesson_progress; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.lesson_progress ("Id", "UserId", "LessonId", "IsCompleted", "IsManuallyUnlocked", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: lesson_resources; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.lesson_resources ("Id", "Title", "FileUrl", "ResourceType", "LessonId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: lesson_videos; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.lesson_videos ("Id", "Title", "Provider", "ProviderVideoId", "Order", "MaxWatchCount", "LessonId", "CreatedAt", "UpdatedAt", "VideoTag", "ExamId", "IsProcessingAI", "SubtitleUrl", "IsProcessingMindmaps") FROM stdin;
\.


--
-- Data for Name: lessons; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.lessons ("Id", "Title", "Summary", "Order", "ContentSectionId", "ExamId", "CreatedAt", "UpdatedAt", "Price") FROM stdin;
\.


--
-- Data for Name: media_production_pipelines; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.media_production_pipelines ("Id", "Title", "Description", "Stage", "AssignedAgentId", "AssetFolderUrl", "EditingErrorCount", "PublishedAt", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: notification_events; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.notification_events ("Id", "UserId", "ChannelType", "Title", "Body", "Status", "ReadAt", "CreatedAt") FROM stdin;
\.


--
-- Data for Name: packages; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.packages ("Id", "Name", "Description", "Price", "ProgramId", "CreatedAt", "UpdatedAt", "IsActive", "TeacherId") FROM stdin;
\.


--
-- Data for Name: payroll_adjustments; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.payroll_adjustments ("Id", "PayrollRecordId", "Type", "Amount", "Reason", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: payroll_records; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.payroll_records ("Id", "EmployeeProfileId", "Month", "Year", "BasicSalary", "Status", "ApprovedByUserId", "ApprovedAt", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: programs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.programs ("Id", "Name", "Description", "TargetGrade", "CreatedAt", "UpdatedAt", "SubjectId") FROM stdin;
\.


--
-- Data for Name: question_bank_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.question_bank_items ("Id", "Text", "DefaultPoints", "Tags", "CreatedAt", "UpdatedAt", "Type", "CreatedByTeacherId", "SubjectId") FROM stdin;
\.


--
-- Data for Name: question_options; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.question_options ("Id", "Text", "IsCorrect", "QuestionBankItemId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: refresh_tokens; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.refresh_tokens ("Id", "UserId", "Token", "ExpiresAt", "IsRevoked", "DeviceFingerprint", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.roles ("Id", "Name", "Type", "CreatedAt", "UpdatedAt", "PermissionsJson") FROM stdin;
c77894a7-8910-4b3d-8e7c-b26a5cd5f1de	Supervisor	7	2026-06-09 01:54:50.468754	\N	[]
8e2b8c94-1a3b-4836-8c7c-9b7e3da342c8	Staff	8	2026-06-09 01:54:50.468756	\N	[]
07eb61ed-9387-428c-bc36-57c0d4008965	Admin	1	2026-06-09 01:55:25.073913	\N	[]
c341c045-6714-48e5-bd37-435eb272d861	Student	4	2026-06-09 01:55:25.073923	\N	[]
de02114f-6bfb-4ee2-b9a1-98125ae97a52	Teacher	2	2026-06-09 01:55:25.073923	\N	[]
f035bd4a-4b75-4172-8d62-f152709f840f	Assistant	3	2026-06-09 01:55:25.073923	\N	[]
\.


--
-- Data for Name: social_media_plans; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.social_media_plans ("Id", "Title", "Description", "Script", "Platform", "Status", "ScheduledDate", "MediaProductionPipelineId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: student_access_grants; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.student_access_grants ("Id", "UserId", "PackageId", "LessonId", "AccessCodeId", "GrantedAt", "ExpiresAt", "IsActive", "CreatedAt", "UpdatedAt", "ContentSectionId", "ExamId", "GrantType", "LessonVideoId", "TermId") FROM stdin;
\.


--
-- Data for Name: student_answers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.student_answers ("Id", "StudentExamAttemptId", "ExamQuestionId", "SelectedOptionId", "IsCorrect", "PointsAwarded", "CreatedAt", "UpdatedAt", "SubmittedText", "HintUsed") FROM stdin;
\.


--
-- Data for Name: student_badges; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.student_badges ("Id", "StudentId", "BadgeName", "UnlockedAt") FROM stdin;
\.


--
-- Data for Name: student_balances; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.student_balances ("Id", "UserId", "CurrentBalance", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: student_exam_attempts; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.student_exam_attempts ("Id", "UserId", "ExamId", "ScoreAchieved", "IsPassed", "CreatedAt", "UpdatedAt", "Evaluation", "IsTimeExpired", "StartedAt") FROM stdin;
\.


--
-- Data for Name: student_gamifications; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.student_gamifications ("StudentId", "TotalPoints", "CurrentStreakCount", "LongestStreakCount", "LastTaskCompletedAt", "LevelName") FROM stdin;
\.


--
-- Data for Name: student_profiles; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.student_profiles ("Id", "UserId", "ParentPhone", "Governorate", "CreatedAt", "UpdatedAt", "Address", "DateOfBirth", "EducationStage", "Gender", "GradeLevel", "IsFatherAlive", "IsMotherAlive", "StudentCode", "StudyTrack", "District", "SecondaryParentPhone", "SecondaryPhone", "FatherDateOfBirth", "MotherDateOfBirth", "MotherPhone", "Nationality", "SchoolName", "SchoolType", "DarkThemePaletteId", "LightThemePaletteId", "CurrentMode", "AvatarSlug") FROM stdin;
\.


--
-- Data for Name: student_status_trackers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.student_status_trackers ("StudentId", "CurrentStatus", "ConsecutiveMissedHomeworks", "ConsecutiveFailedExams", "LastActiveAt", "LastEvaluatedAt") FROM stdin;
\.


--
-- Data for Name: subjects; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.subjects ("Id", "Name", "NormalizedName", "Description", "CreatedAt", "UpdatedAt") FROM stdin;
d9b8a342-990a-4286-905e-fdebb2e3895e	التاريخ	history	مادة التاريخ للثانوية العامة	2026-06-09 04:54:49.372374	\N
\.


--
-- Data for Name: task_comments; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.task_comments ("Id", "TaskId", "UserId", "Content", "AttachmentUrl", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: task_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.task_items ("Id", "Title", "Description", "AssigneeId", "CreatedById", "Status", "Priority", "DueDate", "CompletedAt", "ApprovedById", "CreatedAt", "UpdatedAt", "MediaPipelineId") FROM stdin;
\.


--
-- Data for Name: teacher_accounts; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.teacher_accounts ("Id", "TeacherId", "TotalEarnings", "CurrentBalance", "CommissionRate", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: teacher_payouts; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.teacher_payouts ("Id", "TeacherId", "Amount", "Status", "RejectionReason", "HandledByUserId", "HandledAt", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: teacher_photos; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.teacher_photos ("Id", "TeacherId", "FileUrl", "IsActive", "UploadedAt", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: teacher_profiles; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.teacher_profiles ("Id", "UserId", "Bio", "Specialization", "CommissionRate", "ProfileImageUrl", "ContactInfo", "CreatedAt", "UpdatedAt") FROM stdin;
b4b82937-293e-48a3-a002-decf9a1efab8	c4b82937-293e-48a3-a002-decf9a1efab8	المدرس الافتراضي للمنصة	التاريخ	10.00	\N	01111111111	2026-06-09 04:54:49.372374	\N
\.


--
-- Data for Name: teacher_subjects; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.teacher_subjects ("TeacherId", "SubjectId") FROM stdin;
b4b82937-293e-48a3-a002-decf9a1efab8	d9b8a342-990a-4286-905e-fdebb2e3895e
\.


--
-- Data for Name: terms; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.terms ("Id", "Title", "Order", "PackageId", "CreatedAt", "UpdatedAt", "Price") FROM stdin;
\.


--
-- Data for Name: user_roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.user_roles ("UserId", "RoleId") FROM stdin;
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.users ("Id", "FullName", "PhoneNumber", "PasswordHash", "IsActive", "IsProfileComplete", "CreatedAt", "UpdatedAt", "SuspensionReason") FROM stdin;
c4b82937-293e-48a3-a002-decf9a1efab8	مدرس تاريخ افتراضي	01111111111	$2a$11$wK1mJz3B.gZq6u.RjT1RquWvV0G9t0h6YI4tZpXg9gq/o0s0z0z0	t	t	2026-06-09 04:54:49.372374	\N	\N
\.


--
-- Data for Name: video_chapters; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.video_chapters ("Id", "Title", "StartTime", "EndTime", "SummaryText", "Order", "LessonVideoId", "CreatedAt", "UpdatedAt", "MindmapImageUrl") FROM stdin;
\.


--
-- Data for Name: video_overrides; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.video_overrides ("Id", "UserId", "LessonVideoId", "OriginalLimit", "NewLimit", "AddedViews", "Reason", "PerformedByUserId", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: video_watch_events; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.video_watch_events ("Id", "UserId", "LessonVideoId", "TimeWatchedInSeconds", "WatchCount", "IsLocked", "CreatedAt", "UpdatedAt", "CustomMaxWatchCount") FROM stdin;
\.


--
-- Data for Name: warning_events; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.warning_events ("Id", "StudentId", "Severity", "TriggerReason", "IsResolved", "ResolvedByAssistantId", "ResolutionNotes", "CreatedAt") FROM stdin;
\.


--
-- Name: StudentNotes PK_StudentNotes; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."StudentNotes"
    ADD CONSTRAINT "PK_StudentNotes" PRIMARY KEY ("Id");


--
-- Name: VideoPlaybackSessions PK_VideoPlaybackSessions; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."VideoPlaybackSessions"
    ADD CONSTRAINT "PK_VideoPlaybackSessions" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: access_code_activation_logs PK_access_code_activation_logs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.access_code_activation_logs
    ADD CONSTRAINT "PK_access_code_activation_logs" PRIMARY KEY ("Id");


--
-- Name: access_codes PK_access_codes; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.access_codes
    ADD CONSTRAINT "PK_access_codes" PRIMARY KEY ("Id");


--
-- Name: assistant_tasks PK_assistant_tasks; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.assistant_tasks
    ADD CONSTRAINT "PK_assistant_tasks" PRIMARY KEY ("Id");


--
-- Name: attendance_logs PK_attendance_logs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.attendance_logs
    ADD CONSTRAINT "PK_attendance_logs" PRIMARY KEY ("Id");


--
-- Name: audit_logs PK_audit_logs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.audit_logs
    ADD CONSTRAINT "PK_audit_logs" PRIMARY KEY ("Id");


--
-- Name: balance_transactions PK_balance_transactions; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.balance_transactions
    ADD CONSTRAINT "PK_balance_transactions" PRIMARY KEY ("Id");


--
-- Name: chat_message_read_states PK_chat_message_read_states; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_message_read_states
    ADD CONSTRAINT "PK_chat_message_read_states" PRIMARY KEY ("MessageId", "UserId");


--
-- Name: chat_messages PK_chat_messages; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_messages
    ADD CONSTRAINT "PK_chat_messages" PRIMARY KEY ("Id");


--
-- Name: chat_participants PK_chat_participants; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_participants
    ADD CONSTRAINT "PK_chat_participants" PRIMARY KEY ("ChatRoomId", "UserId");


--
-- Name: chat_rooms PK_chat_rooms; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_rooms
    ADD CONSTRAINT "PK_chat_rooms" PRIMARY KEY ("Id");


--
-- Name: code_groups PK_code_groups; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.code_groups
    ADD CONSTRAINT "PK_code_groups" PRIMARY KEY ("Id");


--
-- Name: code_video_targets PK_code_video_targets; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.code_video_targets
    ADD CONSTRAINT "PK_code_video_targets" PRIMARY KEY ("Id");


--
-- Name: community_post_comments PK_community_post_comments; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_comments
    ADD CONSTRAINT "PK_community_post_comments" PRIMARY KEY ("Id");


--
-- Name: community_post_likes PK_community_post_likes; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_likes
    ADD CONSTRAINT "PK_community_post_likes" PRIMARY KEY ("Id");


--
-- Name: community_post_poll_options PK_community_post_poll_options; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_poll_options
    ADD CONSTRAINT "PK_community_post_poll_options" PRIMARY KEY ("Id");


--
-- Name: community_post_poll_votes PK_community_post_poll_votes; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_poll_votes
    ADD CONSTRAINT "PK_community_post_poll_votes" PRIMARY KEY ("Id");


--
-- Name: community_posts PK_community_posts; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_posts
    ADD CONSTRAINT "PK_community_posts" PRIMARY KEY ("Id");


--
-- Name: content_sections PK_content_sections; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.content_sections
    ADD CONSTRAINT "PK_content_sections" PRIMARY KEY ("Id");


--
-- Name: crm_call_logs PK_crm_call_logs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.crm_call_logs
    ADD CONSTRAINT "PK_crm_call_logs" PRIMARY KEY ("Id");


--
-- Name: crm_student_statuses PK_crm_student_statuses; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.crm_student_statuses
    ADD CONSTRAINT "PK_crm_student_statuses" PRIMARY KEY ("StudentId");


--
-- Name: custom_forms PK_custom_forms; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.custom_forms
    ADD CONSTRAINT "PK_custom_forms" PRIMARY KEY ("Id");


--
-- Name: devices PK_devices; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT "PK_devices" PRIMARY KEY ("Id");


--
-- Name: employee_profiles PK_employee_profiles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.employee_profiles
    ADD CONSTRAINT "PK_employee_profiles" PRIMARY KEY ("Id");


--
-- Name: employee_vacations PK_employee_vacations; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.employee_vacations
    ADD CONSTRAINT "PK_employee_vacations" PRIMARY KEY ("Id");


--
-- Name: essay_submissions PK_essay_submissions; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.essay_submissions
    ADD CONSTRAINT "PK_essay_submissions" PRIMARY KEY ("Id");


--
-- Name: exam_questions PK_exam_questions; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.exam_questions
    ADD CONSTRAINT "PK_exam_questions" PRIMARY KEY ("Id");


--
-- Name: exams PK_exams; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.exams
    ADD CONSTRAINT "PK_exams" PRIMARY KEY ("Id");


--
-- Name: form_submissions PK_form_submissions; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.form_submissions
    ADD CONSTRAINT "PK_form_submissions" PRIMARY KEY ("Id");


--
-- Name: gamification_action_logs PK_gamification_action_logs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.gamification_action_logs
    ADD CONSTRAINT "PK_gamification_action_logs" PRIMARY KEY ("Id");


--
-- Name: homework_answers PK_homework_answers; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_answers
    ADD CONSTRAINT "PK_homework_answers" PRIMARY KEY ("Id");


--
-- Name: homework_questions PK_homework_questions; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_questions
    ADD CONSTRAINT "PK_homework_questions" PRIMARY KEY ("Id");


--
-- Name: homework_submissions PK_homework_submissions; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_submissions
    ADD CONSTRAINT "PK_homework_submissions" PRIMARY KEY ("Id");


--
-- Name: homeworks PK_homeworks; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homeworks
    ADD CONSTRAINT "PK_homeworks" PRIMARY KEY ("Id");


--
-- Name: lesson_comments PK_lesson_comments; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_comments
    ADD CONSTRAINT "PK_lesson_comments" PRIMARY KEY ("Id");


--
-- Name: lesson_progress PK_lesson_progress; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_progress
    ADD CONSTRAINT "PK_lesson_progress" PRIMARY KEY ("Id");


--
-- Name: lesson_resources PK_lesson_resources; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_resources
    ADD CONSTRAINT "PK_lesson_resources" PRIMARY KEY ("Id");


--
-- Name: lesson_videos PK_lesson_videos; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_videos
    ADD CONSTRAINT "PK_lesson_videos" PRIMARY KEY ("Id");


--
-- Name: lessons PK_lessons; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lessons
    ADD CONSTRAINT "PK_lessons" PRIMARY KEY ("Id");


--
-- Name: media_production_pipelines PK_media_production_pipelines; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.media_production_pipelines
    ADD CONSTRAINT "PK_media_production_pipelines" PRIMARY KEY ("Id");


--
-- Name: notification_events PK_notification_events; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.notification_events
    ADD CONSTRAINT "PK_notification_events" PRIMARY KEY ("Id");


--
-- Name: packages PK_packages; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.packages
    ADD CONSTRAINT "PK_packages" PRIMARY KEY ("Id");


--
-- Name: payroll_adjustments PK_payroll_adjustments; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payroll_adjustments
    ADD CONSTRAINT "PK_payroll_adjustments" PRIMARY KEY ("Id");


--
-- Name: payroll_records PK_payroll_records; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payroll_records
    ADD CONSTRAINT "PK_payroll_records" PRIMARY KEY ("Id");


--
-- Name: programs PK_programs; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.programs
    ADD CONSTRAINT "PK_programs" PRIMARY KEY ("Id");


--
-- Name: question_bank_items PK_question_bank_items; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.question_bank_items
    ADD CONSTRAINT "PK_question_bank_items" PRIMARY KEY ("Id");


--
-- Name: question_options PK_question_options; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.question_options
    ADD CONSTRAINT "PK_question_options" PRIMARY KEY ("Id");


--
-- Name: refresh_tokens PK_refresh_tokens; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.refresh_tokens
    ADD CONSTRAINT "PK_refresh_tokens" PRIMARY KEY ("Id");


--
-- Name: roles PK_roles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT "PK_roles" PRIMARY KEY ("Id");


--
-- Name: social_media_plans PK_social_media_plans; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.social_media_plans
    ADD CONSTRAINT "PK_social_media_plans" PRIMARY KEY ("Id");


--
-- Name: student_access_grants PK_student_access_grants; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_access_grants
    ADD CONSTRAINT "PK_student_access_grants" PRIMARY KEY ("Id");


--
-- Name: student_answers PK_student_answers; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_answers
    ADD CONSTRAINT "PK_student_answers" PRIMARY KEY ("Id");


--
-- Name: student_badges PK_student_badges; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_badges
    ADD CONSTRAINT "PK_student_badges" PRIMARY KEY ("Id");


--
-- Name: student_balances PK_student_balances; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_balances
    ADD CONSTRAINT "PK_student_balances" PRIMARY KEY ("Id");


--
-- Name: student_exam_attempts PK_student_exam_attempts; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_exam_attempts
    ADD CONSTRAINT "PK_student_exam_attempts" PRIMARY KEY ("Id");


--
-- Name: student_gamifications PK_student_gamifications; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_gamifications
    ADD CONSTRAINT "PK_student_gamifications" PRIMARY KEY ("StudentId");


--
-- Name: student_profiles PK_student_profiles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_profiles
    ADD CONSTRAINT "PK_student_profiles" PRIMARY KEY ("Id");


--
-- Name: student_status_trackers PK_student_status_trackers; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_status_trackers
    ADD CONSTRAINT "PK_student_status_trackers" PRIMARY KEY ("StudentId");


--
-- Name: subjects PK_subjects; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.subjects
    ADD CONSTRAINT "PK_subjects" PRIMARY KEY ("Id");


--
-- Name: task_comments PK_task_comments; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_comments
    ADD CONSTRAINT "PK_task_comments" PRIMARY KEY ("Id");


--
-- Name: task_items PK_task_items; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_items
    ADD CONSTRAINT "PK_task_items" PRIMARY KEY ("Id");


--
-- Name: teacher_accounts PK_teacher_accounts; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_accounts
    ADD CONSTRAINT "PK_teacher_accounts" PRIMARY KEY ("Id");


--
-- Name: teacher_payouts PK_teacher_payouts; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_payouts
    ADD CONSTRAINT "PK_teacher_payouts" PRIMARY KEY ("Id");


--
-- Name: teacher_photos PK_teacher_photos; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_photos
    ADD CONSTRAINT "PK_teacher_photos" PRIMARY KEY ("Id");


--
-- Name: teacher_profiles PK_teacher_profiles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_profiles
    ADD CONSTRAINT "PK_teacher_profiles" PRIMARY KEY ("Id");


--
-- Name: teacher_subjects PK_teacher_subjects; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_subjects
    ADD CONSTRAINT "PK_teacher_subjects" PRIMARY KEY ("TeacherId", "SubjectId");


--
-- Name: terms PK_terms; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.terms
    ADD CONSTRAINT "PK_terms" PRIMARY KEY ("Id");


--
-- Name: user_roles PK_user_roles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_roles
    ADD CONSTRAINT "PK_user_roles" PRIMARY KEY ("UserId", "RoleId");


--
-- Name: users PK_users; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "PK_users" PRIMARY KEY ("Id");


--
-- Name: video_chapters PK_video_chapters; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_chapters
    ADD CONSTRAINT "PK_video_chapters" PRIMARY KEY ("Id");


--
-- Name: video_overrides PK_video_overrides; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_overrides
    ADD CONSTRAINT "PK_video_overrides" PRIMARY KEY ("Id");


--
-- Name: video_watch_events PK_video_watch_events; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_watch_events
    ADD CONSTRAINT "PK_video_watch_events" PRIMARY KEY ("Id");


--
-- Name: warning_events PK_warning_events; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.warning_events
    ADD CONSTRAINT "PK_warning_events" PRIMARY KEY ("Id");


--
-- Name: IX_StudentNotes_AdminId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_StudentNotes_AdminId" ON public."StudentNotes" USING btree ("AdminId");


--
-- Name: IX_StudentNotes_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_StudentNotes_StudentId" ON public."StudentNotes" USING btree ("StudentId");


--
-- Name: IX_VideoPlaybackSessions_LessonVideoId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_VideoPlaybackSessions_LessonVideoId" ON public."VideoPlaybackSessions" USING btree ("LessonVideoId");


--
-- Name: IX_VideoPlaybackSessions_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_VideoPlaybackSessions_UserId" ON public."VideoPlaybackSessions" USING btree ("UserId");


--
-- Name: IX_access_code_activation_logs_AccessCodeId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_access_code_activation_logs_AccessCodeId" ON public.access_code_activation_logs USING btree ("AccessCodeId");


--
-- Name: IX_access_code_activation_logs_PackageId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_access_code_activation_logs_PackageId" ON public.access_code_activation_logs USING btree ("PackageId");


--
-- Name: IX_access_code_activation_logs_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_access_code_activation_logs_StudentId" ON public.access_code_activation_logs USING btree ("StudentId");


--
-- Name: IX_access_code_activation_logs_TeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_access_code_activation_logs_TeacherId" ON public.access_code_activation_logs USING btree ("TeacherId");


--
-- Name: IX_access_codes_CodeGroupId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_access_codes_CodeGroupId" ON public.access_codes USING btree ("CodeGroupId");


--
-- Name: IX_access_codes_CodeHash; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_access_codes_CodeHash" ON public.access_codes USING btree ("CodeHash");


--
-- Name: IX_access_codes_ConsumedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_access_codes_ConsumedByUserId" ON public.access_codes USING btree ("ConsumedByUserId");


--
-- Name: IX_assistant_tasks_AssignedAssistantId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_assistant_tasks_AssignedAssistantId" ON public.assistant_tasks USING btree ("AssignedAssistantId");


--
-- Name: IX_assistant_tasks_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_assistant_tasks_StudentId" ON public.assistant_tasks USING btree ("StudentId");


--
-- Name: IX_attendance_logs_Date; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_attendance_logs_Date" ON public.attendance_logs USING btree ("Date");


--
-- Name: IX_attendance_logs_EmployeeId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_attendance_logs_EmployeeId" ON public.attendance_logs USING btree ("EmployeeId");


--
-- Name: IX_audit_logs_Action; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_audit_logs_Action" ON public.audit_logs USING btree ("Action");


--
-- Name: IX_audit_logs_CreatedAt; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_audit_logs_CreatedAt" ON public.audit_logs USING btree ("CreatedAt");


--
-- Name: IX_audit_logs_EntityType; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_audit_logs_EntityType" ON public.audit_logs USING btree ("EntityType");


--
-- Name: IX_audit_logs_PerformedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_audit_logs_PerformedByUserId" ON public.audit_logs USING btree ("PerformedByUserId");


--
-- Name: IX_balance_transactions_PerformedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_balance_transactions_PerformedByUserId" ON public.balance_transactions USING btree ("PerformedByUserId");


--
-- Name: IX_balance_transactions_StudentBalanceId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_balance_transactions_StudentBalanceId" ON public.balance_transactions USING btree ("StudentBalanceId");


--
-- Name: IX_chat_message_read_states_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_chat_message_read_states_UserId" ON public.chat_message_read_states USING btree ("UserId");


--
-- Name: IX_chat_messages_ChatRoomId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_chat_messages_ChatRoomId" ON public.chat_messages USING btree ("ChatRoomId");


--
-- Name: IX_chat_messages_CreatedAt; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_chat_messages_CreatedAt" ON public.chat_messages USING btree ("CreatedAt");


--
-- Name: IX_chat_messages_SenderUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_chat_messages_SenderUserId" ON public.chat_messages USING btree ("SenderUserId");


--
-- Name: IX_chat_participants_LastReadMessageId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_chat_participants_LastReadMessageId" ON public.chat_participants USING btree ("LastReadMessageId");


--
-- Name: IX_chat_participants_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_chat_participants_UserId" ON public.chat_participants USING btree ("UserId");


--
-- Name: IX_chat_rooms_CreatedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_chat_rooms_CreatedByUserId" ON public.chat_rooms USING btree ("CreatedByUserId");


--
-- Name: IX_chat_rooms_TaskItemId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_chat_rooms_TaskItemId" ON public.chat_rooms USING btree ("TaskItemId");


--
-- Name: IX_code_groups_CreatedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_code_groups_CreatedByUserId" ON public.code_groups USING btree ("CreatedByUserId");


--
-- Name: IX_code_groups_TeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_code_groups_TeacherId" ON public.code_groups USING btree ("TeacherId");


--
-- Name: IX_code_video_targets_CodeGroupId_LessonVideoId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_code_video_targets_CodeGroupId_LessonVideoId" ON public.code_video_targets USING btree ("CodeGroupId", "LessonVideoId");


--
-- Name: IX_code_video_targets_LessonVideoId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_code_video_targets_LessonVideoId" ON public.code_video_targets USING btree ("LessonVideoId");


--
-- Name: IX_community_post_comments_AuthorUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_comments_AuthorUserId" ON public.community_post_comments USING btree ("AuthorUserId");


--
-- Name: IX_community_post_comments_CreatedAt; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_comments_CreatedAt" ON public.community_post_comments USING btree ("CreatedAt");


--
-- Name: IX_community_post_comments_PostId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_comments_PostId" ON public.community_post_comments USING btree ("PostId");


--
-- Name: IX_community_post_comments_ReviewedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_comments_ReviewedByUserId" ON public.community_post_comments USING btree ("ReviewedByUserId");


--
-- Name: IX_community_post_comments_Status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_comments_Status" ON public.community_post_comments USING btree ("Status");


--
-- Name: IX_community_post_likes_PostId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_likes_PostId" ON public.community_post_likes USING btree ("PostId");


--
-- Name: IX_community_post_likes_PostId_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_community_post_likes_PostId_UserId" ON public.community_post_likes USING btree ("PostId", "UserId");


--
-- Name: IX_community_post_likes_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_likes_UserId" ON public.community_post_likes USING btree ("UserId");


--
-- Name: IX_community_post_poll_options_PostId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_poll_options_PostId" ON public.community_post_poll_options USING btree ("PostId");


--
-- Name: IX_community_post_poll_votes_PollOptionId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_poll_votes_PollOptionId" ON public.community_post_poll_votes USING btree ("PollOptionId");


--
-- Name: IX_community_post_poll_votes_PostId_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_community_post_poll_votes_PostId_UserId" ON public.community_post_poll_votes USING btree ("PostId", "UserId");


--
-- Name: IX_community_post_poll_votes_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_post_poll_votes_UserId" ON public.community_post_poll_votes USING btree ("UserId");


--
-- Name: IX_community_posts_AuthorUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_posts_AuthorUserId" ON public.community_posts USING btree ("AuthorUserId");


--
-- Name: IX_community_posts_CreatedAt; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_posts_CreatedAt" ON public.community_posts USING btree ("CreatedAt");


--
-- Name: IX_community_posts_ReviewedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_posts_ReviewedByUserId" ON public.community_posts USING btree ("ReviewedByUserId");


--
-- Name: IX_community_posts_Status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_community_posts_Status" ON public.community_posts USING btree ("Status");


--
-- Name: IX_content_sections_TermId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_content_sections_TermId" ON public.content_sections USING btree ("TermId");


--
-- Name: IX_crm_call_logs_AgentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_crm_call_logs_AgentId" ON public.crm_call_logs USING btree ("AgentId");


--
-- Name: IX_crm_call_logs_CallDate; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_crm_call_logs_CallDate" ON public.crm_call_logs USING btree ("CallDate");


--
-- Name: IX_crm_call_logs_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_crm_call_logs_StudentId" ON public.crm_call_logs USING btree ("StudentId");


--
-- Name: IX_crm_student_statuses_AssignedAgentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_crm_student_statuses_AssignedAgentId" ON public.crm_student_statuses USING btree ("AssignedAgentId");


--
-- Name: IX_crm_student_statuses_NextFollowUpDate; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_crm_student_statuses_NextFollowUpDate" ON public.crm_student_statuses USING btree ("NextFollowUpDate");


--
-- Name: IX_custom_forms_Slug; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_custom_forms_Slug" ON public.custom_forms USING btree ("Slug");


--
-- Name: IX_devices_UserId_DeviceFingerprint; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_devices_UserId_DeviceFingerprint" ON public.devices USING btree ("UserId", "DeviceFingerprint");


--
-- Name: IX_employee_profiles_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_employee_profiles_UserId" ON public.employee_profiles USING btree ("UserId");


--
-- Name: IX_employee_vacations_EmployeeId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_employee_vacations_EmployeeId" ON public.employee_vacations USING btree ("EmployeeId");


--
-- Name: IX_employee_vacations_HandledBy; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_employee_vacations_HandledBy" ON public.employee_vacations USING btree ("HandledBy");


--
-- Name: IX_essay_submissions_GradedByTeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_essay_submissions_GradedByTeacherId" ON public.essay_submissions USING btree ("GradedByTeacherId");


--
-- Name: IX_essay_submissions_QuestionId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_essay_submissions_QuestionId" ON public.essay_submissions USING btree ("QuestionId");


--
-- Name: IX_essay_submissions_StudentExamAttemptId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_essay_submissions_StudentExamAttemptId" ON public.essay_submissions USING btree ("StudentExamAttemptId");


--
-- Name: IX_essay_submissions_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_essay_submissions_StudentId" ON public.essay_submissions USING btree ("StudentId");


--
-- Name: IX_exam_questions_ExamId_QuestionBankItemId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_exam_questions_ExamId_QuestionBankItemId" ON public.exam_questions USING btree ("ExamId", "QuestionBankItemId");


--
-- Name: IX_exam_questions_QuestionBankItemId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_exam_questions_QuestionBankItemId" ON public.exam_questions USING btree ("QuestionBankItemId");


--
-- Name: IX_exams_CreatedByTeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_exams_CreatedByTeacherId" ON public.exams USING btree ("CreatedByTeacherId");


--
-- Name: IX_form_submissions_CustomFormId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_form_submissions_CustomFormId" ON public.form_submissions USING btree ("CustomFormId");


--
-- Name: IX_gamification_action_logs_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_gamification_action_logs_StudentId" ON public.gamification_action_logs USING btree ("StudentId");


--
-- Name: IX_homework_answers_HomeworkSubmissionId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_homework_answers_HomeworkSubmissionId" ON public.homework_answers USING btree ("HomeworkSubmissionId");


--
-- Name: IX_homework_answers_QuestionId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_homework_answers_QuestionId" ON public.homework_answers USING btree ("QuestionId");


--
-- Name: IX_homework_questions_HomeworkId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_homework_questions_HomeworkId" ON public.homework_questions USING btree ("HomeworkId");


--
-- Name: IX_homework_submissions_AssistantReviewerId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_homework_submissions_AssistantReviewerId" ON public.homework_submissions USING btree ("AssistantReviewerId");


--
-- Name: IX_homework_submissions_HomeworkId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_homework_submissions_HomeworkId" ON public.homework_submissions USING btree ("HomeworkId");


--
-- Name: IX_homework_submissions_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_homework_submissions_StudentId" ON public.homework_submissions USING btree ("StudentId");


--
-- Name: IX_lesson_comments_AuthorUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_comments_AuthorUserId" ON public.lesson_comments USING btree ("AuthorUserId");


--
-- Name: IX_lesson_comments_CreatedAt; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_comments_CreatedAt" ON public.lesson_comments USING btree ("CreatedAt");


--
-- Name: IX_lesson_comments_LessonId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_comments_LessonId" ON public.lesson_comments USING btree ("LessonId");


--
-- Name: IX_lesson_comments_ReviewedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_comments_ReviewedByUserId" ON public.lesson_comments USING btree ("ReviewedByUserId");


--
-- Name: IX_lesson_comments_Status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_comments_Status" ON public.lesson_comments USING btree ("Status");


--
-- Name: IX_lesson_progress_LessonId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_progress_LessonId" ON public.lesson_progress USING btree ("LessonId");


--
-- Name: IX_lesson_progress_UserId_LessonId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_lesson_progress_UserId_LessonId" ON public.lesson_progress USING btree ("UserId", "LessonId");


--
-- Name: IX_lesson_resources_LessonId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_resources_LessonId" ON public.lesson_resources USING btree ("LessonId");


--
-- Name: IX_lesson_videos_ExamId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_videos_ExamId" ON public.lesson_videos USING btree ("ExamId");


--
-- Name: IX_lesson_videos_LessonId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lesson_videos_LessonId" ON public.lesson_videos USING btree ("LessonId");


--
-- Name: IX_lessons_ContentSectionId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_lessons_ContentSectionId" ON public.lessons USING btree ("ContentSectionId");


--
-- Name: IX_media_production_pipelines_AssignedAgentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_media_production_pipelines_AssignedAgentId" ON public.media_production_pipelines USING btree ("AssignedAgentId");


--
-- Name: IX_media_production_pipelines_Stage; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_media_production_pipelines_Stage" ON public.media_production_pipelines USING btree ("Stage");


--
-- Name: IX_notification_events_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_notification_events_UserId" ON public.notification_events USING btree ("UserId");


--
-- Name: IX_packages_ProgramId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_packages_ProgramId" ON public.packages USING btree ("ProgramId");


--
-- Name: IX_packages_TeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_packages_TeacherId" ON public.packages USING btree ("TeacherId");


--
-- Name: IX_payroll_adjustments_PayrollRecordId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_payroll_adjustments_PayrollRecordId" ON public.payroll_adjustments USING btree ("PayrollRecordId");


--
-- Name: IX_payroll_records_ApprovedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_payroll_records_ApprovedByUserId" ON public.payroll_records USING btree ("ApprovedByUserId");


--
-- Name: IX_payroll_records_EmployeeProfileId_Month_Year; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_payroll_records_EmployeeProfileId_Month_Year" ON public.payroll_records USING btree ("EmployeeProfileId", "Month", "Year");


--
-- Name: IX_programs_SubjectId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_programs_SubjectId" ON public.programs USING btree ("SubjectId");


--
-- Name: IX_question_bank_items_CreatedByTeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_question_bank_items_CreatedByTeacherId" ON public.question_bank_items USING btree ("CreatedByTeacherId");


--
-- Name: IX_question_bank_items_SubjectId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_question_bank_items_SubjectId" ON public.question_bank_items USING btree ("SubjectId");


--
-- Name: IX_question_options_QuestionBankItemId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_question_options_QuestionBankItemId" ON public.question_options USING btree ("QuestionBankItemId");


--
-- Name: IX_refresh_tokens_Token; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_refresh_tokens_Token" ON public.refresh_tokens USING btree ("Token");


--
-- Name: IX_refresh_tokens_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_refresh_tokens_UserId" ON public.refresh_tokens USING btree ("UserId");


--
-- Name: IX_roles_Name; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_roles_Name" ON public.roles USING btree ("Name");


--
-- Name: IX_social_media_plans_MediaProductionPipelineId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_social_media_plans_MediaProductionPipelineId" ON public.social_media_plans USING btree ("MediaProductionPipelineId");


--
-- Name: IX_social_media_plans_ScheduledDate; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_social_media_plans_ScheduledDate" ON public.social_media_plans USING btree ("ScheduledDate");


--
-- Name: IX_student_access_grants_AccessCodeId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_student_access_grants_AccessCodeId" ON public.student_access_grants USING btree ("AccessCodeId");


--
-- Name: IX_student_access_grants_UserId_PackageId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_student_access_grants_UserId_PackageId" ON public.student_access_grants USING btree ("UserId", "PackageId");


--
-- Name: IX_student_answers_ExamQuestionId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_student_answers_ExamQuestionId" ON public.student_answers USING btree ("ExamQuestionId");


--
-- Name: IX_student_answers_SelectedOptionId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_student_answers_SelectedOptionId" ON public.student_answers USING btree ("SelectedOptionId");


--
-- Name: IX_student_answers_StudentExamAttemptId_ExamQuestionId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_student_answers_StudentExamAttemptId_ExamQuestionId" ON public.student_answers USING btree ("StudentExamAttemptId", "ExamQuestionId");


--
-- Name: IX_student_badges_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_student_badges_StudentId" ON public.student_badges USING btree ("StudentId");


--
-- Name: IX_student_balances_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_student_balances_UserId" ON public.student_balances USING btree ("UserId");


--
-- Name: IX_student_exam_attempts_ExamId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_student_exam_attempts_ExamId" ON public.student_exam_attempts USING btree ("ExamId");


--
-- Name: IX_student_exam_attempts_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_student_exam_attempts_UserId" ON public.student_exam_attempts USING btree ("UserId");


--
-- Name: IX_student_profiles_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_student_profiles_UserId" ON public.student_profiles USING btree ("UserId");


--
-- Name: IX_subjects_NormalizedName; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_subjects_NormalizedName" ON public.subjects USING btree ("NormalizedName");


--
-- Name: IX_task_comments_TaskId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_task_comments_TaskId" ON public.task_comments USING btree ("TaskId");


--
-- Name: IX_task_comments_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_task_comments_UserId" ON public.task_comments USING btree ("UserId");


--
-- Name: IX_task_items_ApprovedById; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_task_items_ApprovedById" ON public.task_items USING btree ("ApprovedById");


--
-- Name: IX_task_items_AssigneeId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_task_items_AssigneeId" ON public.task_items USING btree ("AssigneeId");


--
-- Name: IX_task_items_CreatedById; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_task_items_CreatedById" ON public.task_items USING btree ("CreatedById");


--
-- Name: IX_task_items_MediaPipelineId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_task_items_MediaPipelineId" ON public.task_items USING btree ("MediaPipelineId");


--
-- Name: IX_teacher_accounts_TeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_teacher_accounts_TeacherId" ON public.teacher_accounts USING btree ("TeacherId");


--
-- Name: IX_teacher_payouts_HandledByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_teacher_payouts_HandledByUserId" ON public.teacher_payouts USING btree ("HandledByUserId");


--
-- Name: IX_teacher_payouts_Status; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_teacher_payouts_Status" ON public.teacher_payouts USING btree ("Status");


--
-- Name: IX_teacher_payouts_TeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_teacher_payouts_TeacherId" ON public.teacher_payouts USING btree ("TeacherId");


--
-- Name: IX_teacher_photos_TeacherId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_teacher_photos_TeacherId" ON public.teacher_photos USING btree ("TeacherId");


--
-- Name: IX_teacher_profiles_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_teacher_profiles_UserId" ON public.teacher_profiles USING btree ("UserId");


--
-- Name: IX_teacher_subjects_SubjectId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_teacher_subjects_SubjectId" ON public.teacher_subjects USING btree ("SubjectId");


--
-- Name: IX_terms_PackageId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_terms_PackageId" ON public.terms USING btree ("PackageId");


--
-- Name: IX_user_roles_RoleId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_user_roles_RoleId" ON public.user_roles USING btree ("RoleId");


--
-- Name: IX_users_PhoneNumber; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_users_PhoneNumber" ON public.users USING btree ("PhoneNumber");


--
-- Name: IX_video_chapters_LessonVideoId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_video_chapters_LessonVideoId" ON public.video_chapters USING btree ("LessonVideoId");


--
-- Name: IX_video_overrides_LessonVideoId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_video_overrides_LessonVideoId" ON public.video_overrides USING btree ("LessonVideoId");


--
-- Name: IX_video_overrides_PerformedByUserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_video_overrides_PerformedByUserId" ON public.video_overrides USING btree ("PerformedByUserId");


--
-- Name: IX_video_overrides_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_video_overrides_UserId" ON public.video_overrides USING btree ("UserId");


--
-- Name: IX_video_watch_events_LessonVideoId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_video_watch_events_LessonVideoId" ON public.video_watch_events USING btree ("LessonVideoId");


--
-- Name: IX_video_watch_events_UserId_LessonVideoId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "IX_video_watch_events_UserId_LessonVideoId" ON public.video_watch_events USING btree ("UserId", "LessonVideoId");


--
-- Name: IX_warning_events_ResolvedByAssistantId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_warning_events_ResolvedByAssistantId" ON public.warning_events USING btree ("ResolvedByAssistantId");


--
-- Name: IX_warning_events_StudentId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_warning_events_StudentId" ON public.warning_events USING btree ("StudentId");


--
-- Name: StudentNotes FK_StudentNotes_users_AdminId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."StudentNotes"
    ADD CONSTRAINT "FK_StudentNotes_users_AdminId" FOREIGN KEY ("AdminId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: StudentNotes FK_StudentNotes_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."StudentNotes"
    ADD CONSTRAINT "FK_StudentNotes_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: VideoPlaybackSessions FK_VideoPlaybackSessions_lesson_videos_LessonVideoId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."VideoPlaybackSessions"
    ADD CONSTRAINT "FK_VideoPlaybackSessions_lesson_videos_LessonVideoId" FOREIGN KEY ("LessonVideoId") REFERENCES public.lesson_videos("Id") ON DELETE CASCADE;


--
-- Name: VideoPlaybackSessions FK_VideoPlaybackSessions_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."VideoPlaybackSessions"
    ADD CONSTRAINT "FK_VideoPlaybackSessions_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: access_code_activation_logs FK_access_code_activation_logs_access_codes_AccessCodeId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.access_code_activation_logs
    ADD CONSTRAINT "FK_access_code_activation_logs_access_codes_AccessCodeId" FOREIGN KEY ("AccessCodeId") REFERENCES public.access_codes("Id") ON DELETE CASCADE;


--
-- Name: access_code_activation_logs FK_access_code_activation_logs_packages_PackageId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.access_code_activation_logs
    ADD CONSTRAINT "FK_access_code_activation_logs_packages_PackageId" FOREIGN KEY ("PackageId") REFERENCES public.packages("Id") ON DELETE SET NULL;


--
-- Name: access_code_activation_logs FK_access_code_activation_logs_teacher_profiles_TeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.access_code_activation_logs
    ADD CONSTRAINT "FK_access_code_activation_logs_teacher_profiles_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES public.teacher_profiles("Id") ON DELETE RESTRICT;


--
-- Name: access_code_activation_logs FK_access_code_activation_logs_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.access_code_activation_logs
    ADD CONSTRAINT "FK_access_code_activation_logs_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: access_codes FK_access_codes_code_groups_CodeGroupId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.access_codes
    ADD CONSTRAINT "FK_access_codes_code_groups_CodeGroupId" FOREIGN KEY ("CodeGroupId") REFERENCES public.code_groups("Id") ON DELETE CASCADE;


--
-- Name: access_codes FK_access_codes_users_ConsumedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.access_codes
    ADD CONSTRAINT "FK_access_codes_users_ConsumedByUserId" FOREIGN KEY ("ConsumedByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: assistant_tasks FK_assistant_tasks_users_AssignedAssistantId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.assistant_tasks
    ADD CONSTRAINT "FK_assistant_tasks_users_AssignedAssistantId" FOREIGN KEY ("AssignedAssistantId") REFERENCES public.users("Id");


--
-- Name: assistant_tasks FK_assistant_tasks_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.assistant_tasks
    ADD CONSTRAINT "FK_assistant_tasks_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: attendance_logs FK_attendance_logs_employee_profiles_EmployeeId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.attendance_logs
    ADD CONSTRAINT "FK_attendance_logs_employee_profiles_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES public.employee_profiles("Id") ON DELETE CASCADE;


--
-- Name: audit_logs FK_audit_logs_users_PerformedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.audit_logs
    ADD CONSTRAINT "FK_audit_logs_users_PerformedByUserId" FOREIGN KEY ("PerformedByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: balance_transactions FK_balance_transactions_student_balances_StudentBalanceId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.balance_transactions
    ADD CONSTRAINT "FK_balance_transactions_student_balances_StudentBalanceId" FOREIGN KEY ("StudentBalanceId") REFERENCES public.student_balances("Id") ON DELETE CASCADE;


--
-- Name: balance_transactions FK_balance_transactions_users_PerformedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.balance_transactions
    ADD CONSTRAINT "FK_balance_transactions_users_PerformedByUserId" FOREIGN KEY ("PerformedByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: chat_message_read_states FK_chat_message_read_states_chat_messages_MessageId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_message_read_states
    ADD CONSTRAINT "FK_chat_message_read_states_chat_messages_MessageId" FOREIGN KEY ("MessageId") REFERENCES public.chat_messages("Id") ON DELETE CASCADE;


--
-- Name: chat_message_read_states FK_chat_message_read_states_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_message_read_states
    ADD CONSTRAINT "FK_chat_message_read_states_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: chat_messages FK_chat_messages_chat_rooms_ChatRoomId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_messages
    ADD CONSTRAINT "FK_chat_messages_chat_rooms_ChatRoomId" FOREIGN KEY ("ChatRoomId") REFERENCES public.chat_rooms("Id") ON DELETE CASCADE;


--
-- Name: chat_messages FK_chat_messages_users_SenderUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_messages
    ADD CONSTRAINT "FK_chat_messages_users_SenderUserId" FOREIGN KEY ("SenderUserId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: chat_participants FK_chat_participants_chat_messages_LastReadMessageId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_participants
    ADD CONSTRAINT "FK_chat_participants_chat_messages_LastReadMessageId" FOREIGN KEY ("LastReadMessageId") REFERENCES public.chat_messages("Id") ON DELETE SET NULL;


--
-- Name: chat_participants FK_chat_participants_chat_rooms_ChatRoomId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_participants
    ADD CONSTRAINT "FK_chat_participants_chat_rooms_ChatRoomId" FOREIGN KEY ("ChatRoomId") REFERENCES public.chat_rooms("Id") ON DELETE CASCADE;


--
-- Name: chat_participants FK_chat_participants_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_participants
    ADD CONSTRAINT "FK_chat_participants_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: chat_rooms FK_chat_rooms_task_items_TaskItemId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_rooms
    ADD CONSTRAINT "FK_chat_rooms_task_items_TaskItemId" FOREIGN KEY ("TaskItemId") REFERENCES public.task_items("Id") ON DELETE CASCADE;


--
-- Name: chat_rooms FK_chat_rooms_users_CreatedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.chat_rooms
    ADD CONSTRAINT "FK_chat_rooms_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: code_groups FK_code_groups_teacher_profiles_TeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.code_groups
    ADD CONSTRAINT "FK_code_groups_teacher_profiles_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES public.teacher_profiles("Id") ON DELETE CASCADE;


--
-- Name: code_groups FK_code_groups_users_CreatedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.code_groups
    ADD CONSTRAINT "FK_code_groups_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: code_video_targets FK_code_video_targets_code_groups_CodeGroupId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.code_video_targets
    ADD CONSTRAINT "FK_code_video_targets_code_groups_CodeGroupId" FOREIGN KEY ("CodeGroupId") REFERENCES public.code_groups("Id") ON DELETE CASCADE;


--
-- Name: code_video_targets FK_code_video_targets_lesson_videos_LessonVideoId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.code_video_targets
    ADD CONSTRAINT "FK_code_video_targets_lesson_videos_LessonVideoId" FOREIGN KEY ("LessonVideoId") REFERENCES public.lesson_videos("Id") ON DELETE CASCADE;


--
-- Name: community_post_comments FK_community_post_comments_community_posts_PostId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_comments
    ADD CONSTRAINT "FK_community_post_comments_community_posts_PostId" FOREIGN KEY ("PostId") REFERENCES public.community_posts("Id") ON DELETE CASCADE;


--
-- Name: community_post_comments FK_community_post_comments_users_AuthorUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_comments
    ADD CONSTRAINT "FK_community_post_comments_users_AuthorUserId" FOREIGN KEY ("AuthorUserId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: community_post_comments FK_community_post_comments_users_ReviewedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_comments
    ADD CONSTRAINT "FK_community_post_comments_users_ReviewedByUserId" FOREIGN KEY ("ReviewedByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: community_post_likes FK_community_post_likes_community_posts_PostId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_likes
    ADD CONSTRAINT "FK_community_post_likes_community_posts_PostId" FOREIGN KEY ("PostId") REFERENCES public.community_posts("Id") ON DELETE CASCADE;


--
-- Name: community_post_likes FK_community_post_likes_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_likes
    ADD CONSTRAINT "FK_community_post_likes_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: community_post_poll_options FK_community_post_poll_options_community_posts_PostId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_poll_options
    ADD CONSTRAINT "FK_community_post_poll_options_community_posts_PostId" FOREIGN KEY ("PostId") REFERENCES public.community_posts("Id") ON DELETE CASCADE;


--
-- Name: community_post_poll_votes FK_community_post_poll_votes_community_post_poll_options_PollO~; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_poll_votes
    ADD CONSTRAINT "FK_community_post_poll_votes_community_post_poll_options_PollO~" FOREIGN KEY ("PollOptionId") REFERENCES public.community_post_poll_options("Id") ON DELETE RESTRICT;


--
-- Name: community_post_poll_votes FK_community_post_poll_votes_community_posts_PostId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_poll_votes
    ADD CONSTRAINT "FK_community_post_poll_votes_community_posts_PostId" FOREIGN KEY ("PostId") REFERENCES public.community_posts("Id") ON DELETE CASCADE;


--
-- Name: community_post_poll_votes FK_community_post_poll_votes_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_post_poll_votes
    ADD CONSTRAINT "FK_community_post_poll_votes_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: community_posts FK_community_posts_users_AuthorUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_posts
    ADD CONSTRAINT "FK_community_posts_users_AuthorUserId" FOREIGN KEY ("AuthorUserId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: community_posts FK_community_posts_users_ReviewedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.community_posts
    ADD CONSTRAINT "FK_community_posts_users_ReviewedByUserId" FOREIGN KEY ("ReviewedByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: content_sections FK_content_sections_terms_TermId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.content_sections
    ADD CONSTRAINT "FK_content_sections_terms_TermId" FOREIGN KEY ("TermId") REFERENCES public.terms("Id") ON DELETE CASCADE;


--
-- Name: crm_call_logs FK_crm_call_logs_users_AgentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.crm_call_logs
    ADD CONSTRAINT "FK_crm_call_logs_users_AgentId" FOREIGN KEY ("AgentId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: crm_call_logs FK_crm_call_logs_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.crm_call_logs
    ADD CONSTRAINT "FK_crm_call_logs_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: crm_student_statuses FK_crm_student_statuses_users_AssignedAgentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.crm_student_statuses
    ADD CONSTRAINT "FK_crm_student_statuses_users_AssignedAgentId" FOREIGN KEY ("AssignedAgentId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: crm_student_statuses FK_crm_student_statuses_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.crm_student_statuses
    ADD CONSTRAINT "FK_crm_student_statuses_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: devices FK_devices_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT "FK_devices_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: employee_profiles FK_employee_profiles_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.employee_profiles
    ADD CONSTRAINT "FK_employee_profiles_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: employee_vacations FK_employee_vacations_employee_profiles_EmployeeId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.employee_vacations
    ADD CONSTRAINT "FK_employee_vacations_employee_profiles_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES public.employee_profiles("Id") ON DELETE CASCADE;


--
-- Name: employee_vacations FK_employee_vacations_users_HandledBy; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.employee_vacations
    ADD CONSTRAINT "FK_employee_vacations_users_HandledBy" FOREIGN KEY ("HandledBy") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: essay_submissions FK_essay_submissions_question_bank_items_QuestionId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.essay_submissions
    ADD CONSTRAINT "FK_essay_submissions_question_bank_items_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES public.question_bank_items("Id") ON DELETE CASCADE;


--
-- Name: essay_submissions FK_essay_submissions_student_exam_attempts_StudentExamAttemptId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.essay_submissions
    ADD CONSTRAINT "FK_essay_submissions_student_exam_attempts_StudentExamAttemptId" FOREIGN KEY ("StudentExamAttemptId") REFERENCES public.student_exam_attempts("Id") ON DELETE CASCADE;


--
-- Name: essay_submissions FK_essay_submissions_teacher_profiles_GradedByTeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.essay_submissions
    ADD CONSTRAINT "FK_essay_submissions_teacher_profiles_GradedByTeacherId" FOREIGN KEY ("GradedByTeacherId") REFERENCES public.teacher_profiles("Id");


--
-- Name: essay_submissions FK_essay_submissions_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.essay_submissions
    ADD CONSTRAINT "FK_essay_submissions_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: exam_questions FK_exam_questions_exams_ExamId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.exam_questions
    ADD CONSTRAINT "FK_exam_questions_exams_ExamId" FOREIGN KEY ("ExamId") REFERENCES public.exams("Id") ON DELETE CASCADE;


--
-- Name: exam_questions FK_exam_questions_question_bank_items_QuestionBankItemId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.exam_questions
    ADD CONSTRAINT "FK_exam_questions_question_bank_items_QuestionBankItemId" FOREIGN KEY ("QuestionBankItemId") REFERENCES public.question_bank_items("Id") ON DELETE CASCADE;


--
-- Name: exams FK_exams_teacher_profiles_CreatedByTeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.exams
    ADD CONSTRAINT "FK_exams_teacher_profiles_CreatedByTeacherId" FOREIGN KEY ("CreatedByTeacherId") REFERENCES public.teacher_profiles("Id") ON DELETE CASCADE;


--
-- Name: form_submissions FK_form_submissions_custom_forms_CustomFormId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.form_submissions
    ADD CONSTRAINT "FK_form_submissions_custom_forms_CustomFormId" FOREIGN KEY ("CustomFormId") REFERENCES public.custom_forms("Id") ON DELETE CASCADE;


--
-- Name: gamification_action_logs FK_gamification_action_logs_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.gamification_action_logs
    ADD CONSTRAINT "FK_gamification_action_logs_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: homework_answers FK_homework_answers_homework_questions_QuestionId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_answers
    ADD CONSTRAINT "FK_homework_answers_homework_questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES public.homework_questions("Id") ON DELETE CASCADE;


--
-- Name: homework_answers FK_homework_answers_homework_submissions_HomeworkSubmissionId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_answers
    ADD CONSTRAINT "FK_homework_answers_homework_submissions_HomeworkSubmissionId" FOREIGN KEY ("HomeworkSubmissionId") REFERENCES public.homework_submissions("Id") ON DELETE CASCADE;


--
-- Name: homework_questions FK_homework_questions_homeworks_HomeworkId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_questions
    ADD CONSTRAINT "FK_homework_questions_homeworks_HomeworkId" FOREIGN KEY ("HomeworkId") REFERENCES public.homeworks("Id") ON DELETE CASCADE;


--
-- Name: homework_submissions FK_homework_submissions_homeworks_HomeworkId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_submissions
    ADD CONSTRAINT "FK_homework_submissions_homeworks_HomeworkId" FOREIGN KEY ("HomeworkId") REFERENCES public.homeworks("Id") ON DELETE CASCADE;


--
-- Name: homework_submissions FK_homework_submissions_users_AssistantReviewerId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_submissions
    ADD CONSTRAINT "FK_homework_submissions_users_AssistantReviewerId" FOREIGN KEY ("AssistantReviewerId") REFERENCES public.users("Id");


--
-- Name: homework_submissions FK_homework_submissions_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.homework_submissions
    ADD CONSTRAINT "FK_homework_submissions_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: lesson_comments FK_lesson_comments_lessons_LessonId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_comments
    ADD CONSTRAINT "FK_lesson_comments_lessons_LessonId" FOREIGN KEY ("LessonId") REFERENCES public.lessons("Id") ON DELETE CASCADE;


--
-- Name: lesson_comments FK_lesson_comments_users_AuthorUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_comments
    ADD CONSTRAINT "FK_lesson_comments_users_AuthorUserId" FOREIGN KEY ("AuthorUserId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: lesson_comments FK_lesson_comments_users_ReviewedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_comments
    ADD CONSTRAINT "FK_lesson_comments_users_ReviewedByUserId" FOREIGN KEY ("ReviewedByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: lesson_progress FK_lesson_progress_lessons_LessonId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_progress
    ADD CONSTRAINT "FK_lesson_progress_lessons_LessonId" FOREIGN KEY ("LessonId") REFERENCES public.lessons("Id") ON DELETE CASCADE;


--
-- Name: lesson_progress FK_lesson_progress_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_progress
    ADD CONSTRAINT "FK_lesson_progress_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: lesson_resources FK_lesson_resources_lessons_LessonId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_resources
    ADD CONSTRAINT "FK_lesson_resources_lessons_LessonId" FOREIGN KEY ("LessonId") REFERENCES public.lessons("Id") ON DELETE CASCADE;


--
-- Name: lesson_videos FK_lesson_videos_exams_ExamId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_videos
    ADD CONSTRAINT "FK_lesson_videos_exams_ExamId" FOREIGN KEY ("ExamId") REFERENCES public.exams("Id");


--
-- Name: lesson_videos FK_lesson_videos_lessons_LessonId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lesson_videos
    ADD CONSTRAINT "FK_lesson_videos_lessons_LessonId" FOREIGN KEY ("LessonId") REFERENCES public.lessons("Id") ON DELETE CASCADE;


--
-- Name: lessons FK_lessons_content_sections_ContentSectionId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.lessons
    ADD CONSTRAINT "FK_lessons_content_sections_ContentSectionId" FOREIGN KEY ("ContentSectionId") REFERENCES public.content_sections("Id") ON DELETE CASCADE;


--
-- Name: media_production_pipelines FK_media_production_pipelines_users_AssignedAgentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.media_production_pipelines
    ADD CONSTRAINT "FK_media_production_pipelines_users_AssignedAgentId" FOREIGN KEY ("AssignedAgentId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: notification_events FK_notification_events_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.notification_events
    ADD CONSTRAINT "FK_notification_events_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: packages FK_packages_programs_ProgramId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.packages
    ADD CONSTRAINT "FK_packages_programs_ProgramId" FOREIGN KEY ("ProgramId") REFERENCES public.programs("Id") ON DELETE CASCADE;


--
-- Name: packages FK_packages_teacher_profiles_TeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.packages
    ADD CONSTRAINT "FK_packages_teacher_profiles_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES public.teacher_profiles("Id") ON DELETE CASCADE;


--
-- Name: payroll_adjustments FK_payroll_adjustments_payroll_records_PayrollRecordId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payroll_adjustments
    ADD CONSTRAINT "FK_payroll_adjustments_payroll_records_PayrollRecordId" FOREIGN KEY ("PayrollRecordId") REFERENCES public.payroll_records("Id") ON DELETE CASCADE;


--
-- Name: payroll_records FK_payroll_records_employee_profiles_EmployeeProfileId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payroll_records
    ADD CONSTRAINT "FK_payroll_records_employee_profiles_EmployeeProfileId" FOREIGN KEY ("EmployeeProfileId") REFERENCES public.employee_profiles("Id") ON DELETE CASCADE;


--
-- Name: payroll_records FK_payroll_records_users_ApprovedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.payroll_records
    ADD CONSTRAINT "FK_payroll_records_users_ApprovedByUserId" FOREIGN KEY ("ApprovedByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: programs FK_programs_subjects_SubjectId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.programs
    ADD CONSTRAINT "FK_programs_subjects_SubjectId" FOREIGN KEY ("SubjectId") REFERENCES public.subjects("Id") ON DELETE CASCADE;


--
-- Name: question_bank_items FK_question_bank_items_subjects_SubjectId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.question_bank_items
    ADD CONSTRAINT "FK_question_bank_items_subjects_SubjectId" FOREIGN KEY ("SubjectId") REFERENCES public.subjects("Id") ON DELETE CASCADE;


--
-- Name: question_bank_items FK_question_bank_items_teacher_profiles_CreatedByTeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.question_bank_items
    ADD CONSTRAINT "FK_question_bank_items_teacher_profiles_CreatedByTeacherId" FOREIGN KEY ("CreatedByTeacherId") REFERENCES public.teacher_profiles("Id") ON DELETE CASCADE;


--
-- Name: question_options FK_question_options_question_bank_items_QuestionBankItemId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.question_options
    ADD CONSTRAINT "FK_question_options_question_bank_items_QuestionBankItemId" FOREIGN KEY ("QuestionBankItemId") REFERENCES public.question_bank_items("Id") ON DELETE CASCADE;


--
-- Name: refresh_tokens FK_refresh_tokens_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.refresh_tokens
    ADD CONSTRAINT "FK_refresh_tokens_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: social_media_plans FK_social_media_plans_media_production_pipelines_MediaProducti~; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.social_media_plans
    ADD CONSTRAINT "FK_social_media_plans_media_production_pipelines_MediaProducti~" FOREIGN KEY ("MediaProductionPipelineId") REFERENCES public.media_production_pipelines("Id") ON DELETE SET NULL;


--
-- Name: student_access_grants FK_student_access_grants_access_codes_AccessCodeId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_access_grants
    ADD CONSTRAINT "FK_student_access_grants_access_codes_AccessCodeId" FOREIGN KEY ("AccessCodeId") REFERENCES public.access_codes("Id");


--
-- Name: student_access_grants FK_student_access_grants_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_access_grants
    ADD CONSTRAINT "FK_student_access_grants_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: student_answers FK_student_answers_exam_questions_ExamQuestionId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_answers
    ADD CONSTRAINT "FK_student_answers_exam_questions_ExamQuestionId" FOREIGN KEY ("ExamQuestionId") REFERENCES public.exam_questions("Id") ON DELETE CASCADE;


--
-- Name: student_answers FK_student_answers_question_options_SelectedOptionId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_answers
    ADD CONSTRAINT "FK_student_answers_question_options_SelectedOptionId" FOREIGN KEY ("SelectedOptionId") REFERENCES public.question_options("Id") ON DELETE CASCADE;


--
-- Name: student_answers FK_student_answers_student_exam_attempts_StudentExamAttemptId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_answers
    ADD CONSTRAINT "FK_student_answers_student_exam_attempts_StudentExamAttemptId" FOREIGN KEY ("StudentExamAttemptId") REFERENCES public.student_exam_attempts("Id") ON DELETE CASCADE;


--
-- Name: student_badges FK_student_badges_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_badges
    ADD CONSTRAINT "FK_student_badges_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: student_balances FK_student_balances_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_balances
    ADD CONSTRAINT "FK_student_balances_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: student_exam_attempts FK_student_exam_attempts_exams_ExamId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_exam_attempts
    ADD CONSTRAINT "FK_student_exam_attempts_exams_ExamId" FOREIGN KEY ("ExamId") REFERENCES public.exams("Id") ON DELETE CASCADE;


--
-- Name: student_exam_attempts FK_student_exam_attempts_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_exam_attempts
    ADD CONSTRAINT "FK_student_exam_attempts_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: student_gamifications FK_student_gamifications_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_gamifications
    ADD CONSTRAINT "FK_student_gamifications_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: student_profiles FK_student_profiles_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_profiles
    ADD CONSTRAINT "FK_student_profiles_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: student_status_trackers FK_student_status_trackers_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.student_status_trackers
    ADD CONSTRAINT "FK_student_status_trackers_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: task_comments FK_task_comments_task_items_TaskId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_comments
    ADD CONSTRAINT "FK_task_comments_task_items_TaskId" FOREIGN KEY ("TaskId") REFERENCES public.task_items("Id") ON DELETE CASCADE;


--
-- Name: task_comments FK_task_comments_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_comments
    ADD CONSTRAINT "FK_task_comments_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: task_items FK_task_items_media_production_pipelines_MediaPipelineId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_items
    ADD CONSTRAINT "FK_task_items_media_production_pipelines_MediaPipelineId" FOREIGN KEY ("MediaPipelineId") REFERENCES public.media_production_pipelines("Id") ON DELETE SET NULL;


--
-- Name: task_items FK_task_items_users_ApprovedById; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_items
    ADD CONSTRAINT "FK_task_items_users_ApprovedById" FOREIGN KEY ("ApprovedById") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: task_items FK_task_items_users_AssigneeId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_items
    ADD CONSTRAINT "FK_task_items_users_AssigneeId" FOREIGN KEY ("AssigneeId") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: task_items FK_task_items_users_CreatedById; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_items
    ADD CONSTRAINT "FK_task_items_users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES public.users("Id") ON DELETE RESTRICT;


--
-- Name: teacher_accounts FK_teacher_accounts_teacher_profiles_TeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_accounts
    ADD CONSTRAINT "FK_teacher_accounts_teacher_profiles_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES public.teacher_profiles("Id") ON DELETE CASCADE;


--
-- Name: teacher_payouts FK_teacher_payouts_teacher_profiles_TeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_payouts
    ADD CONSTRAINT "FK_teacher_payouts_teacher_profiles_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES public.teacher_profiles("Id") ON DELETE CASCADE;


--
-- Name: teacher_payouts FK_teacher_payouts_users_HandledByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_payouts
    ADD CONSTRAINT "FK_teacher_payouts_users_HandledByUserId" FOREIGN KEY ("HandledByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: teacher_photos FK_teacher_photos_users_TeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_photos
    ADD CONSTRAINT "FK_teacher_photos_users_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: teacher_profiles FK_teacher_profiles_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_profiles
    ADD CONSTRAINT "FK_teacher_profiles_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: teacher_subjects FK_teacher_subjects_subjects_SubjectId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_subjects
    ADD CONSTRAINT "FK_teacher_subjects_subjects_SubjectId" FOREIGN KEY ("SubjectId") REFERENCES public.subjects("Id") ON DELETE CASCADE;


--
-- Name: teacher_subjects FK_teacher_subjects_teacher_profiles_TeacherId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.teacher_subjects
    ADD CONSTRAINT "FK_teacher_subjects_teacher_profiles_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES public.teacher_profiles("Id") ON DELETE CASCADE;


--
-- Name: terms FK_terms_packages_PackageId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.terms
    ADD CONSTRAINT "FK_terms_packages_PackageId" FOREIGN KEY ("PackageId") REFERENCES public.packages("Id") ON DELETE CASCADE;


--
-- Name: user_roles FK_user_roles_roles_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_roles
    ADD CONSTRAINT "FK_user_roles_roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES public.roles("Id") ON DELETE CASCADE;


--
-- Name: user_roles FK_user_roles_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_roles
    ADD CONSTRAINT "FK_user_roles_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: video_chapters FK_video_chapters_lesson_videos_LessonVideoId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_chapters
    ADD CONSTRAINT "FK_video_chapters_lesson_videos_LessonVideoId" FOREIGN KEY ("LessonVideoId") REFERENCES public.lesson_videos("Id") ON DELETE CASCADE;


--
-- Name: video_overrides FK_video_overrides_lesson_videos_LessonVideoId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_overrides
    ADD CONSTRAINT "FK_video_overrides_lesson_videos_LessonVideoId" FOREIGN KEY ("LessonVideoId") REFERENCES public.lesson_videos("Id") ON DELETE CASCADE;


--
-- Name: video_overrides FK_video_overrides_users_PerformedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_overrides
    ADD CONSTRAINT "FK_video_overrides_users_PerformedByUserId" FOREIGN KEY ("PerformedByUserId") REFERENCES public.users("Id") ON DELETE SET NULL;


--
-- Name: video_overrides FK_video_overrides_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_overrides
    ADD CONSTRAINT "FK_video_overrides_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: video_watch_events FK_video_watch_events_lesson_videos_LessonVideoId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_watch_events
    ADD CONSTRAINT "FK_video_watch_events_lesson_videos_LessonVideoId" FOREIGN KEY ("LessonVideoId") REFERENCES public.lesson_videos("Id") ON DELETE CASCADE;


--
-- Name: video_watch_events FK_video_watch_events_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.video_watch_events
    ADD CONSTRAINT "FK_video_watch_events_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: warning_events FK_warning_events_users_ResolvedByAssistantId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.warning_events
    ADD CONSTRAINT "FK_warning_events_users_ResolvedByAssistantId" FOREIGN KEY ("ResolvedByAssistantId") REFERENCES public.users("Id");


--
-- Name: warning_events FK_warning_events_users_StudentId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.warning_events
    ADD CONSTRAINT "FK_warning_events_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict ZS3qD5HbSBeFRGHmhsbRzbdKcpvdoO9TnbdSZLV260Cv2iQKiXQ3SiI1QSVRL6w

