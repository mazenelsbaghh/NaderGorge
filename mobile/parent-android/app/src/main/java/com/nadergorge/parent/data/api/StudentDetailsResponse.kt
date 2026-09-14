package com.nadergorge.parent.data.api

data class StudentDetailsResponse(
    val studentName: String,
    val grade: String,
    val school: String,
    val avatarSlug: String?,
    val attendance: AttendanceInfo,
    val exams: List<ExamInfo>,
    val homeworks: List<HomeworkInfo>,
    val warnings: List<WarningInfo>,
    val teachers: List<TeacherInfo>? = null,
    val watchLessons: List<WatchLessonInfo>? = null,
    val balance: BalanceInfo? = null,
    val courses: List<CourseInfo>? = null
)

data class AttendanceInfo(
    val totalLessons: Int,
    val watchedLessons: Int,
    val completionRate: Double
)

data class ExamInfo(
    val examId: String? = null,
    val attemptId: String? = null,
    val packageId: String? = null,
    val packageName: String? = null,
    val termId: String? = null,
    val termTitle: String? = null,
    val teacherId: String? = null,
    val teacherName: String? = null,
    val examTitle: String,
    val score: Double,
    val totalScore: Double,
    val percentage: Double,
    val submittedAt: String? = null,
    val status: String? = null, // e.g. "NotStarted", "Passed" or "Failed"
    val mistakes: List<QuestionReviewInfo>? = null
)

data class HomeworkInfo(
    val homeworkId: String? = null,
    val teacherId: String? = null,
    val teacherName: String? = null,
    val packageId: String? = null,
    val packageName: String? = null,
    val termId: String? = null,
    val termTitle: String? = null,
    val title: String,
    val isSubmitted: Boolean,
    val submissionState: String? = null,
    val grade: String?,
    val submittedAt: String?,
    val mistakes: List<HomeworkAnswerReviewInfo>? = null
)

data class WarningInfo(
    val reason: String,
    val severity: String, // e.g. "High", "Medium", "Low"
    val createdAt: String
)

data class TeacherInfo(
    val teacherId: String,
    val teacherName: String,
    val specialization: String?,
    val profileImageUrl: String?
)

data class CourseInfo(
    val packageId: String,
    val packageName: String,
    val teacherId: String,
    val teacherName: String,
    val terms: List<CourseTermInfo>? = null
)

data class CourseTermInfo(
    val termId: String,
    val termTitle: String,
    val lessonCount: Int,
    val examCount: Int
)

data class WatchLessonInfo(
    val packageId: String = "",
    val packageName: String = "",
    val termId: String = "",
    val termTitle: String = "",
    val teacherId: String,
    val teacherName: String,
    val lessonId: String,
    val lessonTitle: String,
    val totalVideos: Int,
    val watchedVideos: Int,
    val watchCount: Int,
    val watchedSeconds: Int,
    val isCompleted: Boolean,
    val lastWatchedAt: String?
)

data class QuestionReviewInfo(
    val questionText: String,
    val studentAnswer: String?,
    val correctAnswer: String?,
    val writtenCorrection: String?,
    val pointsAwarded: Double,
    val points: Double
)

data class HomeworkAnswerReviewInfo(
    val questionText: String,
    val studentAnswer: String,
    val correctAnswer: String?,
    val writtenCorrection: String?,
    val scoreReceived: Int?,
    val points: Int
)

data class BalanceInfo(
    val currentBalance: Double = 0.0,
    val transactions: List<BalanceTransactionInfo>? = null
)

data class BalanceTransactionInfo(
    val amount: Double,
    val balanceAfter: Double,
    val transactionType: String,
    val description: String,
    val createdAt: String
)

data class ParentNotificationResponse(
    val id: String,
    val title: String,
    val body: String,
    val isRead: Boolean,
    val createdAt: String
)

data class ParentAppConfigResponse(
    val updateRequired: Boolean = false,
    val updateUrl: String = "",
    val updateMessage: String = "يوجد تحديث جديد لتطبيق ولي الأمر. برجاء تحديث التطبيق للمتابعة."
)
